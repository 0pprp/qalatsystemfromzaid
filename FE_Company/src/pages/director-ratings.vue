<script setup>
import { computed, nextTick, onActivated, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSalesBranches } from '@/composables/useSalesBranches'
import {
  directorCities,
  directorStatsBranchMonth,
  directorStatsCatalog,
  directorStatsListPaymentsSum,
  directorStatsRatingCustomers,
  directorStatsTrend,
} from '@/composables/directorApi'
import {
  bucketCacheKey,
  trendCacheKey,
  useDirectorRatingsCache,
} from '@/composables/useDirectorRatingsCache'
import { trendMatchesRequest } from '@/composables/directorTrendIdentity'
import { warmDirectorCacheInBackground } from '@/composables/warmDirectorCache'
import { useToast } from '@/composables/useToast'
import { createRequestGeneration } from '@/utils/provinceCatalog'
import { shouldHidePaidRemaining } from '@/composables/legalFinancialVisibility'

defineOptions({ name: 'DirectorRatings' })
definePage({ meta: { layout: 'default', keepAlive: true } })

const toast = useToast()
const router = useRouter()
const route = useRoute()
const cache = useDirectorRatingsCache()
const { branches, isLoading: branchesLoading, fetchCities } = useSalesBranches()
const catalogGen = createRequestGeneration()
const trendGen = createRequestGeneration()
const monthGen = createRequestGeneration()
const listCardsGen = createRequestGeneration()
const bucketGen = createRequestGeneration()

const gatewayCities = ref(cache.getCities())
const search = ref('')
const selected = ref(null)
const scope = ref('branch')
const entityId = ref(0)
const catalog = ref({ lists: [], followers: [], employees: [] })
const trend = ref(null)
const catalogBusy = ref(false)
const trendBusy = ref(false)
const hubMonthByCity = ref(cache.getMonthMap())
const hubMonthBusy = ref(false)
const detailMonth = ref(null)
const detailMonthBusy = ref(false)
const warmBusy = ref(false)
const syncingRoute = ref(false)
/** Suppress scope/entity watches while restoring profile return state. */
const suppressSelectionWatch = ref(false)

const bucketDialog = ref(false)
const bucketBusy = ref(false)
const bucketError = ref('')
const bucketCustomers = ref([])
const bucketSearch = ref('')
const activeBucket = ref(null)
const bucketPageSize = 500

const today = new Date()
const pad = n => String(n).padStart(2, '0')
const iso = d => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
const fromDate = ref(iso(new Date(today.getFullYear(), today.getMonth(), 1)))
const toDate = ref(iso(today))

const scopes = [
  { value: 'branch', title: 'تقييم الفرع', hint: 'متوسط القوائم النشطة' },
  { value: 'follower', title: 'المتابعين', hint: 'كروت قوائم كل متابع' },
  { value: 'employee', title: 'موظفو المبيعات', hint: 'تقييم مبيعات الموظف' },
]

const drilledListId = ref(0)
const followerListCards = ref([])
const listCardsBusy = ref(false)

/** Per-list card display: rating | installment | payments */
const cardViewById = ref({})
const yesterdayIso = (() => {
  const d = new Date()
  d.setDate(d.getDate() - 1)

  return iso(d)
})()

const paymentsDialog = ref(false)
const paymentsBusy = ref(false)
const paymentsListId = ref(0)
const paymentsListName = ref('')
const paymentsFrom = ref(yesterdayIso)
const paymentsTo = ref(yesterdayIso)
const paymentsTotal = ref(null)
const paymentsCount = ref(null)

const cardPickDialog = ref(false)
const cardPickListId = ref(0)
const cardPickName = ref('')
const cardPickMode = ref('rating')
const cardPickDay = ref(yesterdayIso)
const cardPickPaymentsBusy = ref(false)

function cardPref(listId) {
  return cardViewById.value[listId] || { mode: 'rating', day: yesterdayIso, paymentsTotal: null }
}

function setCardPref(listId, patch) {
  cardViewById.value = {
    ...cardViewById.value,
    [listId]: { ...cardPref(listId), ...patch },
  }
}

function cardPrimaryText(card) {
  const pref = cardPref(card.id)
  if (pref.mode === 'installment')
    return card.installmentSum == null ? '—' : money(card.installmentSum)
  if (pref.mode === 'payments')
    return pref.paymentsTotal == null ? '…' : money(pref.paymentsTotal)

  return card.score == null ? '—' : card.score.toFixed(2)
}

function cardPrimaryLabel(card) {
  const pref = cardPref(card.id)
  if (pref.mode === 'installment')
    return 'مجموع القسط'
  if (pref.mode === 'payments')
    return `تسديدات ${pref.day || yesterdayIso}`

  return card.ratingLabel
}

const ratingBuckets = [
  { key: 'excellent', label: 'ممتاز', countKey: 'excellent' },
  { key: 'good', label: 'جيد', countKey: 'good' },
  { key: 'weak', label: 'ضعيف', countKey: 'weak' },
  { key: 'rejected', label: 'متلكئ', countKey: 'rejected' },
  { key: 'bad', label: 'سيء', countKey: 'bad' },
]

const cityCards = computed(() => {
  const fromGw = Array.isArray(gatewayCities.value) ? gatewayCities.value : []
  const fromBranches = (branches.value || []).map(b => ({
    value: String(b.value ?? b.Value ?? ''),
    name: b.name || b.Name || '',
  }))
  const map = new Map()
  for (const c of [...fromGw, ...fromBranches]) {
    const v = String(c.value || c.Value || '').trim()
    if (!v)
      continue
    if (!map.has(v))
      map.set(v, { value: v, name: c.name || c.Name || v })
  }
  let rows = [...map.values()]
  const q = search.value.trim()
  if (q)
    rows = rows.filter(r => r.name.includes(q) || r.value.includes(q))

  return rows.sort((a, b) => a.name.localeCompare(b.name, 'ar'))
})

const entityOptions = computed(() => {
  if (scope.value === 'follower')
    return catalog.value.followers || []
  if (scope.value === 'employee')
    return catalog.value.employees || []

  return []
})

const needsEntity = computed(() => scope.value !== 'branch')

/** المتابع مختار → كروت قوائمه (قبل الدخول لتفاصيل قائمة) */
const showFollowerListCards = computed(() =>
  scope.value === 'follower' && entityId.value > 0 && !(drilledListId.value > 0))

/** تفاصيل الرسم: فرع / موظف / متابع (تحت كروت القوائم أو بعد الدخول لقائمة) */
const showTrendDetail = computed(() => {
  if (scope.value === 'branch')
    return true
  if (scope.value === 'employee')
    return entityId.value > 0
  if (scope.value === 'follower')
    return entityId.value > 0

  return false
})

/** رسم تقييم المتابع ظاهر تحت كروت القوائم (قبل الدخول لقائمة) */
const showFollowerOverviewTrend = computed(() => showFollowerListCards.value)

/** Refuse to render scores that belong to another scope (e.g. follower while drilled into a list). */
const trendFitsCurrentView = computed(() => {
  if (!trend.value)
    return false
  const { scope: s, id } = trendScopeId()

  return trendMatchesRequest(trend.value, s, id)
})

const selectedFollowerName = computed(() => {
  const f = (catalog.value.followers || []).find(x => x.id === entityId.value)

  return f?.name || 'المتابع'
})

const drilledListName = computed(() => {
  const card = followerListCards.value.find(c => c.id === drilledListId.value)
  if (card?.name)
    return card.name
  const l = (catalog.value.lists || []).find(x => x.id === drilledListId.value)

  return l?.name || 'القائمة'
})

const scoreText = computed(() => {
  const t = trend.value
  if (!t)
    return '—'
  const end = t.endValue ?? t.EndValue
  if (end == null || Number.isNaN(Number(end)))
    return '—'

  return Number(end).toFixed(2)
})

const ratingLabel = computed(() =>
  trend.value?.ratingLabel || trend.value?.RatingLabel || '—')

const phantomScoreText = computed(() => {
  const t = trend.value
  if (!t)
    return null
  const end = t.phantomEndValue ?? t.PhantomEndValue
  if (end == null || Number.isNaN(Number(end)))
    return null

  return Number(end).toFixed(2)
})

const phantomMetricLabel = computed(() =>
  trend.value?.phantomMetricLabel || trend.value?.PhantomMetricLabel || 'التقييم الوهمي')

const direction = computed(() =>
  String(trend.value?.direction || trend.value?.Direction || 'flat').toLowerCase())

const metricLabel = computed(() =>
  trend.value?.metricLabel || trend.value?.MetricLabel || 'التقييم الحقيقي')

const customerCount = computed(() => {
  const t = trend.value
  if (!t)
    return null
  const n = t.customerCount ?? t.CustomerCount

  return n == null ? null : Number(n)
})

const periodCustomerCount = computed(() => {
  const t = trend.value
  if (!t)
    return null
  const n = t.periodCustomerCount ?? t.PeriodCustomerCount

  return n == null ? null : Number(n)
})

const periodScoreText = computed(() => {
  const t = trend.value
  if (!t)
    return '—'
  const v = t.periodEndValue ?? t.PeriodEndValue
  if (v == null || Number.isNaN(Number(v)))
    return '—'

  return Number(v).toFixed(2)
})

const periodRatingLabel = computed(() =>
  trend.value?.periodRatingLabel || trend.value?.PeriodRatingLabel || '—')

const periodMetricLabel = computed(() =>
  trend.value?.periodMetricLabel
  || trend.value?.PeriodMetricLabel
  || 'تقييم الفترة — زبائن تاريخ البيع ضمن الفترة')

const isActiveHeadlineScope = computed(() => {
  const s = String(scope.value || '').toLowerCase()

  return s === 'follower' || s === 'list' || s === 'branch' || drilledListId.value > 0
})

const points = computed(() => {
  const raw = trend.value?.points || trend.value?.Points || []

  return Array.isArray(raw) ? raw : []
})

const bucketCounts = computed(() => {
  const t = trend.value
  if (!t)
    return null

  return {
    excellent: Number(t.excellentCount ?? t.ExcellentCount ?? 0),
    good: Number(t.goodCount ?? t.GoodCount ?? 0),
    weak: Number(t.weakCount ?? t.WeakCount ?? 0),
    rejected: Number(t.rejectedCount ?? t.RejectedCount ?? 0),
    bad: Number(t.badCount ?? t.BadCount ?? 0),
  }
})

const showBuckets = computed(() => trendFitsCurrentView.value)

function monthBucket(raw, which = 'current') {
  if (!raw)
    return null
  const block = which === 'previous'
    ? (raw.previous || raw.Previous)
    : (raw.current || raw.Current)
  if (!block)
    return null
  const num = v => Number(v ?? 0)

  return {
    yearMonth: Number(block.yearMonth ?? block.YearMonth ?? 0),
    newCount: num(block.newCustomersCount ?? block.NewCustomersCount),
    newSale: num(block.newCustomersSaleSum ?? block.NewCustomersSaleSum),
    newDay: num(block.newCustomersDaySalesSum ?? block.NewCustomersDaySalesSum),
    zeroCount: num(block.zeroedCustomersCount ?? block.ZeroedCustomersCount),
    zeroSale: num(block.zeroedCustomersSaleSum ?? block.ZeroedCustomersSaleSum),
    zeroDay: num(block.zeroedCustomersDaySalesSum ?? block.ZeroedCustomersDaySalesSum),
    netCount: num(block.netCustomersCount ?? block.NetCustomersCount),
    netSale: num(block.netSaleSum ?? block.NetSaleSum),
    netDay: num(block.netDaySalesSum ?? block.NetDaySalesSum),
  }
}

const detailCurrent = computed(() => monthBucket(detailMonth.value, 'current'))
const detailPrevious = computed(() => monthBucket(detailMonth.value, 'previous'))

function formatYm(ym) {
  const n = Number(ym || 0)
  if (!n)
    return ''
  const y = Math.floor(n / 100)
  const m = n % 100

  return `${m}/${y}`
}

function signedText(value, asMoney = false) {
  const n = Number(value || 0)
  const sign = n > 0 ? '+' : ''
  if (asMoney)
    return `${sign}${money(n)} د.ع`

  return `${sign}${Math.round(n)}`
}

function signedClass(value) {
  const n = Number(value || 0)
  if (n > 0)
    return 'delta-pos'
  if (n < 0)
    return 'delta-neg'

  return 'delta-flat'
}

const filteredBucketCustomers = computed(() => {
  const q = bucketSearch.value.trim()
  if (!q)
    return bucketCustomers.value

  return bucketCustomers.value.filter(c =>
    String(c.customerName || '').includes(q)
    || String(c.phone || '').includes(q)
    || String(c.customerId || '').includes(q))
})

const bucketTruncated = computed(() =>
  bucketCustomers.value.length >= bucketPageSize
  && activeBucket.value
  && Number(bucketCounts.value?.[activeBucket.value.countKey] || 0) > bucketCustomers.value.length)

const selectedPoint = ref(null)

const chartGeom = computed(() => {
  const pts = points.value
  if (pts.length < 1)
    return null
  const vals = pts.map(p => Number(p.value ?? p.Value ?? 0))
  const dates = pts.map(p => String(p.date ?? p.Date ?? ''))
  const min = Math.min(...vals)
  const max = Math.max(...vals)
  const span = max - min || 1
  const w = 640
  const h = 160
  const padX = 12
  const padY = 16
  const n = vals.length
  const coords = vals.map((v, i) => {
    const x = n === 1 ? w / 2 : padX + (i / (n - 1)) * (w - padX * 2)
    const y = h - padY - ((v - min) / span) * (h - padY * 2)

    return { x, y, v, date: dates[i], index: i }
  })
  const path = coords.map((c, i) => `${i === 0 ? 'M' : 'L'}${c.x.toFixed(1)},${c.y.toFixed(1)}`).join(' ')
  const first = dates[0] || ''
  const last = dates[dates.length - 1] || ''

  return { path, coords, w, h, first, last, count: n }
})

function formatDay(isoDate) {
  if (!isoDate || isoDate.length < 10)
    return isoDate || ''
  const [, m, d] = isoDate.split('-')

  return `${Number(d)}/${Number(m)}`
}

function pickPoint(c) {
  selectedPoint.value = {
    date: c.date,
    value: c.v,
    label: formatDay(c.date),
  }
}

function clearPoint() {
  selectedPoint.value = null
}

function findCity(value) {
  const v = String(value || '').trim()
  if (!v)
    return null

  return cityCards.value.find(c => c.value === v) || { value: v, name: v }
}

function currentTrendKey(cityValue = selected.value?.value) {
  const { scope: s, id } = trendScopeId()

  return trendCacheKey({
    cityValue,
    scope: s,
    id,
    fromDate: fromDate.value,
    toDate: toDate.value,
  })
}

function openCity(city, { pushHistory = true } = {}) {
  if (!city?.value)
    return
  // Invalidate in-flight loads from the previous province/employee before painting.
  catalogGen.next()
  trendGen.next()
  monthGen.next()
  listCardsGen.next()
  bucketGen.next()

  selected.value = city
  scope.value = 'branch'
  entityId.value = 0
  drilledListId.value = 0
  followerListCards.value = []
  bucketDialog.value = false
  bucketCustomers.value = []
  activeBucket.value = null
  selectedPoint.value = null

  const cachedMonth = cache.getMonth(city.value) || hubMonthByCity.value[city.value] || null
  detailMonth.value = cachedMonth

  // Never keep previous city's employees/followers while the new catalog loads.
  const cachedCatalog = cache.getCatalog(city.value)
  catalog.value = cachedCatalog || { lists: [], followers: [], employees: [] }

  const cachedTrend = cache.getTrend(currentTrendKey(city.value))
  trend.value = cachedTrend || null

  loadCatalog(city.value)
  loadTrend({ useCacheFirst: true })
  loadDetailMonth(city.value)

  if (pushHistory && String(route.query.city || '') !== String(city.value)) {
    syncingRoute.value = true
    router.push({ path: '/director-ratings', query: { city: city.value } }).finally(() => {
      syncingRoute.value = false
    })
  }
}

function backToHub({ pushHistory = true } = {}) {
  catalogGen.next()
  trendGen.next()
  monthGen.next()
  listCardsGen.next()
  bucketGen.next()
  selected.value = null
  trend.value = null
  detailMonth.value = null
  catalog.value = { lists: [], followers: [], employees: [] }
  followerListCards.value = []
  bucketDialog.value = false
  bucketCustomers.value = []
  activeBucket.value = null
  selectedPoint.value = null
  if (pushHistory && route.query.city) {
    syncingRoute.value = true
    router.push({ path: '/director-ratings', query: {} }).finally(() => {
      syncingRoute.value = false
    })
  }
}

async function loadCities() {
  const cached = cache.getCities()
  if (cached.length)
    gatewayCities.value = cached
  try {
    gatewayCities.value = await directorCities() || []
    cache.setCities(gatewayCities.value)
  }
  catch {
    if (!gatewayCities.value.length)
      gatewayCities.value = []
  }
  await fetchCities()
}

async function warmMonthStats() {
  hubMonthBusy.value = true
  warmBusy.value = true
  try {
    const raw = await directorStatsBranchMonth()
    const rows = Array.isArray(raw) ? raw : (raw ? [raw] : [])
    const map = {}
    for (const row of rows) {
      const key = String(row.cityValue ?? row.CityValue ?? '').trim()
      if (key)
        map[key] = row
    }
    hubMonthByCity.value = map
    cache.setMonthMap(map)
    if (selected.value?.value && map[selected.value.value])
      detailMonth.value = map[selected.value.value]
  }
  catch {
    /* keep prior cache */
  }
  finally {
    hubMonthBusy.value = false
    warmBusy.value = false
  }
}

async function loadDetailMonth(cityValue) {
  if (!cityValue)
    return
  const token = monthGen.next()
  const hit = cache.getMonth(cityValue) || hubMonthByCity.value[cityValue]
  if (hit) {
    if (monthGen.isCurrent(token) && selected.value?.value === cityValue)
      detailMonth.value = hit

    return
  }
  detailMonthBusy.value = true
  try {
    const row = await directorStatsBranchMonth(cityValue)
    if (!monthGen.isCurrent(token) || selected.value?.value !== cityValue)
      return
    detailMonth.value = row
    cache.setMonth(cityValue, row)
    hubMonthByCity.value = { ...hubMonthByCity.value, [cityValue]: row }
  }
  catch {
    if (monthGen.isCurrent(token) && selected.value?.value === cityValue)
      detailMonth.value = null
  }
  finally {
    if (monthGen.isCurrent(token))
      detailMonthBusy.value = false
  }
}

async function loadCatalog(cityValue) {
  const token = catalogGen.next()
  const cached = cache.getCatalog(cityValue)
  if (cached) {
    if (catalogGen.isCurrent(token) && selected.value?.value === cityValue)
      catalog.value = cached

    return
  }
  catalogBusy.value = true
  try {
    const raw = await directorStatsCatalog(cityValue)
    const mapped = {
      lists: normalizeEntities(raw?.lists || raw?.Lists),
      followers: normalizeEntities(raw?.followers || raw?.Followers),
      employees: normalizeEntities(raw?.employees || raw?.Employees),
    }
    cache.setCatalog(cityValue, mapped)
    if (!catalogGen.isCurrent(token) || selected.value?.value !== cityValue)
      return
    catalog.value = mapped
  }
  catch (err) {
    if (!catalogGen.isCurrent(token) || selected.value?.value !== cityValue)
      return
    toast.error(err?.response?.data?.message || 'تعذر تحميل قوائم المحافظة')
    catalog.value = { lists: [], followers: [], employees: [] }
  }
  finally {
    if (catalogGen.isCurrent(token))
      catalogBusy.value = false
  }
}

function normalizeEntities(rows) {
  return (Array.isArray(rows) ? rows : []).map(r => ({
    id: Number(r.id ?? r.Id ?? 0),
    name: r.name || r.Name || `#${r.id ?? r.Id}`,
    assignedListIds: (Array.isArray(r.assignedListIds || r.AssignedListIds)
      ? (r.assignedListIds || r.AssignedListIds)
      : []).map(Number).filter(n => n > 0),
  })).filter(r => r.id > 0)
}

function trendScopeId() {
  if (drilledListId.value > 0)
    return { scope: 'list', id: drilledListId.value }
  if (scope.value === 'branch')
    return { scope: 'branch', id: 0 }

  return { scope: scope.value, id: entityId.value }
}

async function loadFollowerListCards() {
  const token = listCardsGen.next()
  if (!selected.value || scope.value !== 'follower' || !(entityId.value > 0)) {
    if (listCardsGen.isCurrent(token))
      followerListCards.value = []

    return
  }
  const followerId = entityId.value
  const cityValue = selected.value.value
  const follower = (catalog.value.followers || []).find(f => f.id === followerId)
  const ids = follower?.assignedListIds || []
  const lists = (catalog.value.lists || []).filter(l => ids.includes(l.id))
  if (!lists.length) {
    if (listCardsGen.isCurrent(token))
      followerListCards.value = []

    return
  }

  listCardsBusy.value = true
  try {
    const from = fromDate.value
    const to = toDate.value
    const settled = await Promise.all(lists.map(async l => {
      try {
        const data = await directorStatsTrend({
          cityValue,
          scope: 'list',
          id: l.id,
          fromDate: from,
          toDate: to,
        })
        // Reuse this exact list trend when the director opens the card — no second wait, no follower bleed.
        if (trendMatchesRequest(data, 'list', l.id)) {
          cache.setTrend(trendCacheKey({
            cityValue,
            scope: 'list',
            id: l.id,
            fromDate: from,
            toDate: to,
          }), data)
        }
        const end = data?.endValue ?? data?.EndValue
        const n = data?.customerCount ?? data?.CustomerCount
        const inst = data?.installmentSum ?? data?.InstallmentSum

        return {
          id: l.id,
          name: l.name,
          score: end == null || Number.isNaN(Number(end)) ? null : Number(end),
          ratingLabel: data?.ratingLabel || data?.RatingLabel || '—',
          customerCount: n == null ? null : Number(n),
          installmentSum: inst == null || Number.isNaN(Number(inst)) ? null : Number(inst),
          direction: String(data?.direction || data?.Direction || 'flat').toLowerCase(),
        }
      }
      catch {
        return {
          id: l.id,
          name: l.name,
          score: null,
          ratingLabel: '—',
          customerCount: null,
          installmentSum: null,
          direction: 'flat',
        }
      }
    }))
    if (!listCardsGen.isCurrent(token)
      || selected.value?.value !== cityValue
      || scope.value !== 'follower'
      || entityId.value !== followerId)
      return
    followerListCards.value = settled.sort((a, b) => a.name.localeCompare(b.name, 'ar'))
  }
  finally {
    if (listCardsGen.isCurrent(token))
      listCardsBusy.value = false
  }
}

function openCardPicker(card) {
  cardPickListId.value = card.id
  cardPickName.value = card.name
  const pref = cardPref(card.id)
  cardPickMode.value = pref.mode || 'rating'
  cardPickDay.value = pref.day || yesterdayIso
  cardPickDialog.value = true
}

async function applyCardPicker() {
  const id = cardPickListId.value
  if (!(id > 0))
    return
  if (cardPickMode.value === 'rating') {
    setCardPref(id, { mode: 'rating' })
    cardPickDialog.value = false
    openFollowerListCard(id)

    return
  }
  if (cardPickMode.value === 'installment') {
    setCardPref(id, { mode: 'installment' })
    cardPickDialog.value = false

    return
  }
  // payments for chosen day
  cardPickPaymentsBusy.value = true
  try {
    const day = cardPickDay.value || yesterdayIso
    const data = await directorStatsListPaymentsSum({
      cityValue: selected.value?.value,
      listId: id,
      fromDate: day,
      toDate: day,
    })
    const total = data?.totalAmount ?? data?.TotalAmount ?? 0
    setCardPref(id, {
      mode: 'payments',
      day,
      paymentsTotal: Number(total) || 0,
    })
    cardPickDialog.value = false
  }
  catch {
    toast.error('تعذر جلب تسديدات اليوم')
  }
  finally {
    cardPickPaymentsBusy.value = false
  }
}

function openPaymentsDialog(card, ev) {
  ev?.stopPropagation?.()
  paymentsListId.value = card.id
  paymentsListName.value = card.name
  paymentsFrom.value = yesterdayIso
  paymentsTo.value = yesterdayIso
  paymentsTotal.value = null
  paymentsCount.value = null
  paymentsDialog.value = true
  void loadPaymentsSum()
}

/** حقل «إلى» لا يقبل أقدم من «من» — نفس اليوم أو بعده فقط. */
function onPaymentsFromChange(value) {
  const from = String(value || paymentsFrom.value || '').slice(0, 10)
  const to = String(paymentsTo.value || '').slice(0, 10)
  if (from && to && to < from)
    paymentsTo.value = from
}

async function loadPaymentsSum() {
  if (!(paymentsListId.value > 0) || !selected.value?.value)
    return
  const from = String(paymentsFrom.value || yesterdayIso).slice(0, 10)
  let to = String(paymentsTo.value || yesterdayIso).slice(0, 10)
  if (to < from) {
    to = from
    paymentsTo.value = from
    toast.warning('تاريخ «إلى» يجب أن يكون نفس «من» أو بعده')
  }
  paymentsBusy.value = true
  try {
    const data = await directorStatsListPaymentsSum({
      cityValue: selected.value.value,
      listId: paymentsListId.value,
      fromDate: from,
      toDate: to,
    })
    paymentsTotal.value = Number(data?.totalAmount ?? data?.TotalAmount ?? 0)
    paymentsCount.value = Number(data?.paymentCount ?? data?.PaymentCount ?? 0)
  }
  catch {
    paymentsTotal.value = null
    paymentsCount.value = null
    toast.error('تعذر جلب مجموع التسديدات')
  }
  finally {
    paymentsBusy.value = false
  }
}

function openFollowerListCard(listId) {
  const id = Number(listId) || 0
  if (id <= 0 || !selected.value)
    return
  // Drop follower overview immediately — never keep المتابع numbers under a list title.
  trendGen.next()
  drilledListId.value = id
  trend.value = null
  selectedPoint.value = null
  bucketDialog.value = false
  bucketCustomers.value = []
  activeBucket.value = null

  const key = trendCacheKey({
    cityValue: selected.value.value,
    scope: 'list',
    id,
    fromDate: fromDate.value,
    toDate: toDate.value,
  })
  const cached = cache.getTrend(key)
  if (cached && trendMatchesRequest(cached, 'list', id)) {
    trend.value = cached
    void refreshTrend(key, { scope: 'list', id })

    return
  }
  void refreshTrend(key, { scope: 'list', id })
}

function backToFollowerLists() {
  trendGen.next()
  drilledListId.value = 0
  trend.value = null
  selectedPoint.value = null
  bucketDialog.value = false
  bucketCustomers.value = []
  activeBucket.value = null
  void loadTrend({ useCacheFirst: true })
}

async function loadTrend({ useCacheFirst = false } = {}) {
  if (!selected.value)
    return
  if (!showTrendDetail.value) {
    trend.value = null

    return
  }
  const { scope: apiScope, id: apiId } = trendScopeId()
  const key = currentTrendKey()
  if (useCacheFirst) {
    const cached = cache.getTrend(key)
    if (cached && trendMatchesRequest(cached, apiScope, apiId)) {
      trend.value = cached
      void refreshTrend(key, { scope: apiScope, id: apiId })

      return
    }
  }
  await refreshTrend(key, { scope: apiScope, id: apiId })
}

async function refreshTrend(key = currentTrendKey(), request = null) {
  if (!selected.value || !showTrendDetail.value)
    return
  const token = trendGen.next()
  const cityValue = selected.value.value
  const live = trendScopeId()
  const apiScope = request?.scope ?? live.scope
  const apiId = request?.id ?? live.id
  const from = fromDate.value
  const to = toDate.value
  const requestKey = key || trendCacheKey({
    cityValue,
    scope: apiScope,
    id: apiId,
    fromDate: from,
    toDate: to,
  })
  trendBusy.value = true
  selectedPoint.value = null
  try {
    const data = await directorStatsTrend({
      cityValue,
      scope: apiScope,
      id: apiId,
      fromDate: from,
      toDate: to,
    })
    if (!trendMatchesRequest(data, apiScope, apiId)) {
      console.warn('[director-ratings] trend scope/id mismatch', {
        expected: { scope: apiScope, id: apiId },
        got: { scope: data?.scope ?? data?.Scope, id: data?.id ?? data?.Id },
      })

      return
    }
    cache.setTrend(requestKey, data)
    // Only paint if UI still asks for this exact place (list vs follower).
    if (!trendGen.isCurrent(token) || currentTrendKey() !== requestKey)
      return
    trend.value = data
  }
  catch (err) {
    if (!trendGen.isCurrent(token) || currentTrendKey() !== requestKey)
      return
    trend.value = null
    toast.error(err?.response?.data?.message || 'تعذر تحميل التقييم')
  }
  finally {
    if (trendGen.isCurrent(token))
      trendBusy.value = false
  }
}

function mapCustomerRows(list, fallbackLabel) {
  return list.map(c => {
    const hide = shouldHidePaidRemaining(c)

    return {
      customerId: c.customerId ?? c.CustomerId,
      customerName: c.customerName || c.CustomerName || `زبون ${c.customerId ?? c.CustomerId}`,
      phone: c.phone || c.Phone || '',
      dateSale: c.dateSale ?? c.DateSale,
      lastPaymentDate: c.lastPaymentDate ?? c.LastPaymentDate,
      amountPaid: hide ? null : (c.amountPaid ?? c.AmountPaid ?? 0),
      amountRemaining: hide ? null : (c.amountRemaining ?? c.AmountRemaining ?? 0),
      amountTotal: c.amountTotal ?? c.AmountTotal ?? 0,
      ratingLabel: c.ratingLabel || c.RatingLabel || fallbackLabel,
      score: c.score ?? c.Score,
      hidePaidRemaining: hide,
      isLegal: c.isLegal ?? c.IsLegal ?? hide,
    }
  })
}

function unwrapCustomerList(data) {
  if (Array.isArray(data))
    return data
  if (Array.isArray(data?.items))
    return data.items
  if (Array.isArray(data?.data))
    return data.data

  return []
}

function currentBucketKey(level, q = '') {
  const { scope: s, id } = trendScopeId()

  return bucketCacheKey({
    cityValue: selected.value?.value,
    scope: s,
    id,
    level,
    fromDate: fromDate.value,
    toDate: toDate.value,
    q: q || '',
  })
}

async function openBucket(bucket, { useCacheFirst = true } = {}) {
  if (!selected.value)
    return
  if (!showTrendDetail.value) {
    toast.error('اختر القائمة أو الموظف أولاً')

    return
  }

  const token = bucketGen.next()
  activeBucket.value = bucket
  bucketSearch.value = ''
  bucketError.value = ''
  bucketDialog.value = true

  const key = currentBucketKey(bucket.key)
  if (useCacheFirst) {
    const cached = cache.getBucket(key)
    const expected = Number(bucketCounts.value?.[bucket.countKey])
    const countMatches = !Number.isFinite(expected) || (Array.isArray(cached) && cached.length === expected)
    if (Array.isArray(cached) && countMatches && (cached.length > 0 || expected === 0)) {
      if (bucketGen.isCurrent(token)) {
        bucketCustomers.value = cached
        bucketBusy.value = false
      }

      return
    }
  }

  bucketCustomers.value = []
  bucketBusy.value = true
  const cityValue = selected.value.value
  const { scope: apiScope, id: apiId } = trendScopeId()
  try {
    const data = await directorStatsRatingCustomers({
      cityValue,
      scope: apiScope,
      id: apiId,
      level: bucket.key,
      fromDate: fromDate.value,
      toDate: toDate.value,
      take: bucketPageSize,
      skip: 0,
    })
    const rows = mapCustomerRows(unwrapCustomerList(data), bucket.label)
    cache.setBucket(key, rows)
    if (!bucketGen.isCurrent(token) || currentBucketKey(bucket.key) !== key)
      return
    bucketCustomers.value = rows
  }
  catch (err) {
    if (!bucketGen.isCurrent(token))
      return
    bucketError.value = err?.response?.data?.message || err?.message || 'تعذر تحميل أسماء الزبائن'
  }
  finally {
    if (bucketGen.isCurrent(token))
      bucketBusy.value = false
  }
}

async function searchBucketServer() {
  if (!activeBucket.value || !selected.value)
    return
  const token = bucketGen.next()
  bucketBusy.value = true
  bucketError.value = ''
  const q = bucketSearch.value.trim()
  const bucket = activeBucket.value
  const key = currentBucketKey(bucket.key, q)
  const cached = cache.getBucket(key)
  if (Array.isArray(cached)) {
    if (bucketGen.isCurrent(token)) {
      bucketCustomers.value = cached
      bucketBusy.value = false
    }
    if (cached.length || !q)
      return
  }
  const cityValue = selected.value.value
  const { scope: apiScope, id: apiId } = trendScopeId()
  try {
    const data = await directorStatsRatingCustomers({
      cityValue,
      scope: apiScope,
      id: apiId,
      level: bucket.key,
      fromDate: fromDate.value,
      toDate: toDate.value,
      take: bucketPageSize,
      skip: 0,
      q: q || undefined,
    })
    const rows = mapCustomerRows(unwrapCustomerList(data), bucket.label)
    cache.setBucket(key, rows)
    if (!bucketGen.isCurrent(token) || currentBucketKey(bucket.key, q) !== key)
      return
    bucketCustomers.value = rows
  }
  catch (err) {
    if (!bucketGen.isCurrent(token))
      return
    bucketError.value = err?.response?.data?.message || err?.message || 'تعذر البحث'
  }
  finally {
    if (bucketGen.isCurrent(token))
      bucketBusy.value = false
  }
}

const RETURN_KEY = 'director-ratings-return-v1'

function saveDirectorReturnState() {
  try {
    sessionStorage.setItem(RETURN_KEY, JSON.stringify({
      city: selected.value?.value || '',
      cityName: selected.value?.name || '',
      scope: scope.value,
      entityId: entityId.value,
      drilledListId: drilledListId.value,
      fromDate: fromDate.value,
      toDate: toDate.value,
      bucketKey: activeBucket.value?.key || null,
      bucketSearch: bucketSearch.value || '',
      scrollY: window.scrollY || 0,
      dialogOpen: bucketDialog.value,
    }))
  }
  catch { /* ignore */ }
}

async function restoreDirectorReturnState() {
  let raw = null
  try {
    raw = sessionStorage.getItem(RETURN_KEY)
    if (raw)
      sessionStorage.removeItem(RETURN_KEY)
  }
  catch { /* ignore */ }
  if (!raw)
    return false
  try {
    const st = JSON.parse(raw)
    if (!st.city)
      return false

    suppressSelectionWatch.value = true
    catalogGen.next()
    trendGen.next()
    monthGen.next()
    listCardsGen.next()
    bucketGen.next()

    if (st.fromDate)
      fromDate.value = st.fromDate
    if (st.toDate)
      toDate.value = st.toDate

    const city = {
      value: String(st.city),
      name: st.cityName || st.city,
    }
    selected.value = city
    // Restore exact place — never call openCity (it forces scope=branch).
    scope.value = st.scope || 'branch'
    entityId.value = Number(st.entityId) || 0
    drilledListId.value = Number(st.drilledListId) || 0
    followerListCards.value = []
    bucketDialog.value = false
    bucketCustomers.value = []
    activeBucket.value = null
    selectedPoint.value = null

    const cachedMonth = cache.getMonth(city.value) || hubMonthByCity.value[city.value] || null
    detailMonth.value = cachedMonth
    catalog.value = cache.getCatalog(city.value) || { lists: [], followers: [], employees: [] }
    trend.value = cache.getTrend(currentTrendKey(city.value)) || null

    if (String(route.query.city || '') !== String(city.value)) {
      syncingRoute.value = true
      await router.push({ path: '/director-ratings', query: { city: city.value } }).finally(() => {
        syncingRoute.value = false
      })
    }

    await Promise.all([
      loadCatalog(city.value),
      loadDetailMonth(city.value),
      scope.value === 'follower' && entityId.value > 0
        ? Promise.all([
            loadFollowerListCards(),
            showTrendDetail.value ? loadTrend({ useCacheFirst: true }) : Promise.resolve(),
          ])
        : loadTrend({ useCacheFirst: true }),
    ])
    if (scope.value === 'follower' && entityId.value > 0 && drilledListId.value > 0)
      await loadFollowerListCards()

    if (st.dialogOpen && st.bucketKey) {
      const bucket = ratingBuckets.find(b => b.key === st.bucketKey)
      if (bucket) {
        await openBucket(bucket, { useCacheFirst: true })
        if (st.bucketSearch) {
          bucketSearch.value = st.bucketSearch
          await searchBucketServer()
        }
      }
    }
    await nextTick()
    if (st.scrollY != null)
      window.scrollTo(0, Number(st.scrollY) || 0)

    return true
  }
  catch {
    return false
  }
  finally {
    await nextTick()
    suppressSelectionWatch.value = false
  }
}

function openCustomerProfile(c) {
  const city = String(selected.value?.value || '').trim()
  if (!city) {
    bucketError.value = 'تعذر تحديد المحافظة للزبون'

    return
  }
  saveDirectorReturnState()
  router.push({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: city,
      customerId: c.customerId || '',
      name: c.customerName || '',
      phone: c.phone || '',
    },
  })
}

function money(value) {
  const n = Number(value || 0)
  if (!Number.isFinite(n))
    return '0'

  return n.toLocaleString('en-US')
}

function applyRouteCity() {
  if (syncingRoute.value)
    return
  const qCity = String(route.query.city || '').trim()
  if (!qCity) {
    if (selected.value)
      backToHub({ pushHistory: false })

    return
  }
  if (selected.value?.value === qCity)
    return
  openCity(findCity(qCity), { pushHistory: false })
}

watch(scope, () => {
  if (suppressSelectionWatch.value)
    return
  // Do not bump gens here — openCity already invalidated in-flight loads.
  // loadTrend/loadFollowerListCards bump their own tokens when they start.
  entityId.value = 0
  drilledListId.value = 0
  followerListCards.value = []
  trend.value = null
  bucketDialog.value = false
  bucketCustomers.value = []
  activeBucket.value = null
  if (scope.value === 'branch')
    loadTrend({ useCacheFirst: true })
})

watch(entityId, () => {
  if (suppressSelectionWatch.value)
    return
  drilledListId.value = 0
  followerListCards.value = []
  trend.value = null
  bucketDialog.value = false
  bucketCustomers.value = []
  activeBucket.value = null
  if (scope.value === 'follower' && entityId.value > 0) {
    void loadFollowerListCards()
    void loadTrend({ useCacheFirst: true })
  }
  else if (scope.value === 'employee' && entityId.value > 0)
    loadTrend({ useCacheFirst: true })
  else if (scope.value === 'branch')
    loadTrend({ useCacheFirst: true })
})

function applyPeriod() {
  if (showFollowerListCards.value) {
    void loadFollowerListCards()
    void loadTrend({ useCacheFirst: false })
  }
  else {
    void loadTrend({ useCacheFirst: false })
  }
}

watch(() => route.query.city, () => {
  applyRouteCity()
})

let mountedOnce = false

function clearStaleReturnToken() {
  try {
    sessionStorage.removeItem(RETURN_KEY)
  }
  catch { /* ignore */ }
}

/** Hub (كروت المحافظات) unless URL has ?city= — never auto-dive into a leftover province. */
async function enterRatingsLanding() {
  cache.reloadFromSession()
  const qCity = String(route.query.city || '').trim()
  if (!qCity) {
    clearStaleReturnToken()
    if (selected.value)
      backToHub({ pushHistory: false })

    return
  }
  const restored = await restoreDirectorReturnState()
  if (!restored)
    applyRouteCity()
}

onMounted(async () => {
  mountedOnce = true
  await loadCities()
  await enterRatingsLanding()
  void warmMonthStats()
  void warmDirectorCacheInBackground()
})

onActivated(async () => {
  if (!mountedOnce)
    return
  // Keep-alive return from profile: page state already visible — drop stale return token.
  if (bucketDialog.value) {
    clearStaleReturnToken()

    return
  }
  await enterRatingsLanding()
})

onBeforeUnmount(() => {})
</script>

<template>
  <div class="director-shell">
    <header class="director-hero">
      <div>
        <p class="eyebrow">
          لوحة التقييمات
        </p>
        <h1 :class="selected ? 'province-title' : 'hub-title'">
          {{ selected ? selected.name : 'التقييمات' }}
        </h1>
        <p class="sub">
          {{ selected
            ? 'الفترة = تاريخ البيع فقط — التقييم والتسديدات تُحسب حتى اليوم (الرسم من أول بيع في الفترة إلى اليوم). اضغط ممتاز/جيد/… للأسماء ثم البروفايل'
            : 'كل المحافظات في نظرة واحدة. اضغط كارت للمحافظة.' }}
        </p>
      </div>
      <div
        v-if="selected"
        class="hero-actions"
      >
        <VBtn
          variant="tonal"
          color="surface"
          @click="backToHub"
        >
          كل المحافظات
        </VBtn>
      </div>
    </header>

    <section
      v-if="!selected"
      class="hub"
    >
      <div class="hub-bar">
        <VTextField
          v-model="search"
          density="comfortable"
          variant="solo-filled"
          flat
          hide-details
          prepend-inner-icon="tabler-search"
          placeholder="بحث محافظة…"
          class="search"
        />
        <span class="count">
          {{ cityCards.length }} محافظة
          <template v-if="warmBusy"> · تجهيز الكاش…</template>
        </span>
      </div>

      <div
        v-if="branchesLoading && !cityCards.length"
        class="muted"
      >
        جاري تحميل المحافظات…
      </div>

      <div class="cards">
        <button
          v-for="city in cityCards"
          :key="city.value"
          type="button"
          class="province-card"
          @click="openCity(city)"
        >
          <span class="province-name">{{ city.name }}</span>
          <span class="province-cta">افتح التقييمات</span>
        </button>
      </div>
    </section>

    <section
      v-else
      class="detail"
    >
      <div class="filters">
        <VTextField
          v-model="fromDate"
          type="date"
          label="بيع من"
          density="comfortable"
          variant="outlined"
          hide-details
        />
        <VTextField
          v-model="toDate"
          type="date"
          label="بيع إلى"
          density="comfortable"
          variant="outlined"
          hide-details
        />
        <VBtn
          color="primary"
          size="large"
          :loading="trendBusy || listCardsBusy"
          @click="applyPeriod"
        >
          تطبيق فترة البيع
        </VBtn>
      </div>

      <div class="scope-row">
        <button
          v-for="s in scopes"
          :key="s.value"
          type="button"
          class="scope-chip"
          :class="{ active: scope === s.value }"
          @click="scope = s.value"
        >
          <strong>{{ s.title }}</strong>
          <small>{{ s.hint }}</small>
        </button>
      </div>

      <div
        v-if="needsEntity"
        class="entity-row"
      >
        <VSelect
          v-model="entityId"
          :items="entityOptions"
          item-title="name"
          item-value="id"
          :label="scope === 'follower' ? 'المتابع' : 'موظف المبيعات'"
          :loading="catalogBusy"
          density="comfortable"
          variant="outlined"
          hide-details
          clearable
          :disabled="!entityOptions.length"
        />
      </div>

      <!-- كروت قوائم المتابع -->
      <div
        v-if="showFollowerListCards"
        class="follower-lists"
      >
        <div class="follower-lists__head">
          <h3>قوائم {{ selectedFollowerName }}</h3>
          <p class="muted">
            بجانب كل كارت: مجموع القسط. اضغط الكارت لاختيار ماذا تعرض (تقييم / قسط / تسديدات يوم). زر التسديدات لفترة من–إلى (افتراضي أمس).
          </p>
        </div>
        <div
          v-if="listCardsBusy"
          class="muted"
        >
          جاري تحميل تقييم القوائم…
        </div>
        <div
          v-else-if="!followerListCards.length"
          class="muted"
        >
          لا قوائم مربوطة لهذا المتابع.
        </div>
        <div
          v-else
          class="list-cards"
        >
          <div
            v-for="card in followerListCards"
            :key="card.id"
            class="list-card"
            role="button"
            tabindex="0"
            @click="openCardPicker(card)"
            @keydown.enter="openCardPicker(card)"
          >
            <strong class="list-card__name">{{ card.name }}</strong>
            <span
              class="list-card__score"
              :data-dir="cardPref(card.id).mode === 'rating' ? card.direction : 'flat'"
            >
              {{ cardPrimaryText(card) }}
            </span>
            <span class="list-card__label">{{ cardPrimaryLabel(card) }}</span>
            <span class="list-card__count">
              الزبائن المسددين:
              <b>{{ card.customerCount == null ? '—' : card.customerCount }}</b>
            </span>
            <span class="list-card__installment">
              مجموع القسط:
              <b>{{ card.installmentSum == null ? '—' : money(card.installmentSum) }}</b>
            </span>
            <VBtn
              size="small"
              color="success"
              variant="tonal"
              class="mt-2"
              prepend-icon="tabler-cash"
              @click="openPaymentsDialog(card, $event)"
            >
              التسديدات
            </VBtn>
          </div>
        </div>
      </div>

      <VDialog
        v-model="cardPickDialog"
        max-width="420"
      >
        <VCard>
          <VCardTitle>عرض كارت «{{ cardPickName }}»</VCardTitle>
          <VCardText class="d-flex flex-column gap-3">
            <VRadioGroup v-model="cardPickMode">
              <VRadio
                label="التقييم (وفتح التفاصيل)"
                value="rating"
              />
              <VRadio
                label="مجموع القسط على الكارت"
                value="installment"
              />
              <VRadio
                label="مبلغ تسديدات يوم محدد على الكارت"
                value="payments"
              />
            </VRadioGroup>
            <VTextField
              v-if="cardPickMode === 'payments'"
              v-model="cardPickDay"
              type="date"
              label="اليوم"
              density="compact"
              hide-details
            />
          </VCardText>
          <VCardActions>
            <VSpacer />
            <VBtn
              variant="text"
              @click="cardPickDialog = false"
            >
              إلغاء
            </VBtn>
            <VBtn
              color="primary"
              :loading="cardPickPaymentsBusy"
              @click="applyCardPicker"
            >
              تطبيق
            </VBtn>
          </VCardActions>
        </VCard>
      </VDialog>

      <VDialog
        v-model="paymentsDialog"
        max-width="440"
      >
        <VCard>
          <VCardTitle>تسديدات «{{ paymentsListName }}»</VCardTitle>
          <VCardText class="d-flex flex-column gap-3">
            <div class="d-flex flex-wrap gap-2">
              <VTextField
                v-model="paymentsFrom"
                type="date"
                label="من تاريخ"
                density="compact"
                hide-details
                style="min-width: 140px"
                @update:model-value="onPaymentsFromChange"
              />
              <VTextField
                v-model="paymentsTo"
                type="date"
                label="إلى تاريخ"
                density="compact"
                hide-details
                style="min-width: 140px"
                :min="paymentsFrom || undefined"
              />
            </div>
            <VBtn
              color="primary"
              :loading="paymentsBusy"
              @click="loadPaymentsSum"
            >
              عرض المجموع
            </VBtn>
            <div
              v-if="paymentsTotal != null"
              class="payments-sum-box"
            >
              <div class="text-medium-emphasis">
                مبلغ التسديدات
              </div>
              <div class="text-h5 font-weight-bold">
                {{ money(paymentsTotal) }} دع
              </div>
              <div class="text-caption">
                عدد الحركات: {{ paymentsCount ?? 0 }}
              </div>
            </div>
          </VCardText>
          <VCardActions>
            <VSpacer />
            <VBtn
              variant="text"
              @click="paymentsDialog = false"
            >
              إغلاق
            </VBtn>
          </VCardActions>
        </VCard>
      </VDialog>

      <div
        v-if="showTrendDetail && drilledListId > 0"
        class="drill-bar"
      >
        <VBtn
          variant="text"
          prepend-icon="tabler-arrow-right"
          @click="backToFollowerLists"
        >
          رجوع لقوائم {{ selectedFollowerName }}
        </VBtn>
        <span class="drill-bar__title">{{ drilledListName }}</span>
      </div>

      <div
        v-if="showFollowerOverviewTrend"
        class="follower-overview-head"
      >
        <h3>جارت التقييم — {{ selectedFollowerName }}</h3>
        <p class="muted">
          متوسط تقييم قوائمه من الزبائن المسددين (خلال 30 يوماً) — الرسم أدناه تقييم الفترة حتى اليوم
        </p>
      </div>

      <div
        v-if="showTrendDetail"
        class="result-panel"
      >
        <div
          v-if="trendBusy && !trendFitsCurrentView"
          class="score-block"
        >
          <p class="muted">
            جاري تحميل تقييم {{ drilledListId > 0 ? 'القائمة' : 'المكان' }}…
          </p>
        </div>
        <div
          v-else-if="trendFitsCurrentView"
          class="score-block"
        >
          <p class="metric">
            {{ metricLabel }}
          </p>
          <p class="score">
            {{ scoreText }}
          </p>
          <template v-if="isActiveHeadlineScope && phantomScoreText">
            <p class="metric metric--phantom">
              {{ phantomMetricLabel }}
            </p>
            <p class="score score--phantom">
              {{ phantomScoreText }}
            </p>
          </template>
          <p
            class="label-pill"
            :data-dir="direction"
          >
            {{ ratingLabel }}
          </p>
          <p class="cust-count">
            {{ isActiveHeadlineScope ? 'الزبائن المسددين (خلال 30 يوماً):' : 'كل الزبائن ضمن الفترة:' }}
            <strong>{{ customerCount == null ? '—' : customerCount }}</strong>
          </p>
          <div
            v-if="isActiveHeadlineScope && periodCustomerCount != null"
            class="period-rating-box"
          >
            <p class="period-rating-box__title">
              {{ periodMetricLabel }}
            </p>
            <p class="period-rating-box__row">
              تقييم الفترة:
              <strong>{{ periodScoreText }}</strong>
              <span class="muted">· {{ periodRatingLabel }}</span>
            </p>
            <p class="period-rating-box__row">
              زبائن الفترة:
              <strong>{{ periodCustomerCount }}</strong>
            </p>
          </div>
          <div
            v-if="detailCurrent"
            class="month-inline"
          >
            <p>
              زبائن ضمن الفترة ({{ formatYm(detailCurrent.yearMonth) }}):
              <strong>{{ detailCurrent.newCount }}</strong>
            </p>
            <p>
              مصفرون:
              <strong>{{ detailCurrent.zeroCount }}</strong>
            </p>
            <p>
              الفرق:
              <strong :class="signedClass(detailCurrent.netCount)">
                {{ signedText(detailCurrent.netCount) }}
              </strong>
            </p>
          </div>
          <p
            v-if="chartGeom"
            class="day-count"
          >
            {{ chartGeom.count }} نقطة يومية بالرسم — اضغط نقطة لعرض يومها
          </p>
        </div>
        <div
          v-if="trendFitsCurrentView"
          class="spark"
        >
          <div
            v-if="selectedPoint"
            class="point-pop"
          >
            <strong>{{ selectedPoint.label }}</strong>
            <span>التقييم: {{ Number(selectedPoint.value).toFixed(2) }}</span>
            <button
              type="button"
              class="point-close"
              aria-label="إغلاق"
              @click="clearPoint"
            >
              ×
            </button>
          </div>
          <svg
            v-if="chartGeom"
            :viewBox="`0 0 ${chartGeom.w} ${chartGeom.h}`"
            preserveAspectRatio="none"
            class="trend-svg"
            role="img"
            aria-label="رسم التقييم اليومي"
          >
            <path
              :d="chartGeom.path"
              fill="none"
              stroke="currentColor"
              stroke-width="2.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
            <g
              v-for="c in chartGeom.coords"
              :key="c.index"
              class="pt"
              :class="{ active: selectedPoint && selectedPoint.date === c.date }"
              @click.stop="pickPoint(c)"
            >
              <circle
                :cx="c.x"
                :cy="c.y"
                r="14"
                class="hit"
              />
              <circle
                :cx="c.x"
                :cy="c.y"
                :r="selectedPoint && selectedPoint.date === c.date ? 6 : 4"
                class="dot"
              />
            </g>
          </svg>
          <div
            v-if="chartGeom"
            class="axis-row"
          >
            <span>{{ formatDay(chartGeom.first) }}</span>
            <span>{{ formatDay(chartGeom.last) }}</span>
          </div>
          <p
            v-if="!points.length && !trendBusy"
            class="muted"
          >
            لا نقاط في هذه الفترة — غيّر التاريخ أو النطاق.
          </p>
        </div>
      </div>

      <div
        v-if="detailMonthBusy && !detailCurrent"
        class="muted month-loading"
      >
        جاري تحميل إحصائيات الشهر…
      </div>

      <div
        v-if="detailPrevious || detailCurrent"
        class="month-stats"
      >
        <article
          v-if="detailPrevious"
          class="month-card month-card--prev"
        >
          <header>
            <h3>الشهر السابق</h3>
            <span>{{ formatYm(detailPrevious.yearMonth) }}</span>
          </header>
          <div class="month-section">
            <p class="month-label">زبائن ضمن التقييم (مبيعات الشهر)</p>
            <div class="metric-line"><span>العدد</span><strong>{{ detailPrevious.newCount }}</strong></div>
            <div class="metric-line"><span>سعر البيع</span><strong>{{ money(detailPrevious.newSale) }} د.ع</strong></div>
            <div class="metric-line"><span>القسط</span><strong>{{ money(detailPrevious.newDay) }} د.ع</strong></div>
          </div>
          <div class="month-section">
            <p class="month-label">مصفرون</p>
            <div class="metric-line"><span>العدد</span><strong>{{ detailPrevious.zeroCount }}</strong></div>
            <div class="metric-line"><span>سعر البيع</span><strong>{{ money(detailPrevious.zeroSale) }} د.ع</strong></div>
            <div class="metric-line"><span>القسط</span><strong>{{ money(detailPrevious.zeroDay) }} د.ع</strong></div>
          </div>
          <div class="month-section month-section--net">
            <p class="month-label">الفرق (تقييم − مصفرون)</p>
            <div class="metric-line">
              <span>فرق العدد</span>
              <strong :class="signedClass(detailPrevious.netCount)">{{ signedText(detailPrevious.netCount) }}</strong>
            </div>
            <div class="metric-line">
              <span>فرق سعر البيع</span>
              <strong :class="signedClass(detailPrevious.netSale)">{{ signedText(detailPrevious.netSale, true) }}</strong>
            </div>
            <div class="metric-line">
              <span>فرق القسط</span>
              <strong :class="signedClass(detailPrevious.netDay)">{{ signedText(detailPrevious.netDay, true) }}</strong>
            </div>
          </div>
        </article>

        <article
          v-if="detailCurrent"
          class="month-card month-card--cur"
        >
          <header>
            <h3>الشهر الحالي</h3>
            <span>{{ formatYm(detailCurrent.yearMonth) }}</span>
          </header>
          <div class="month-section">
            <p class="month-label">زبائن ضمن التقييم (مبيعات الشهر)</p>
            <div class="metric-line"><span>العدد</span><strong>{{ detailCurrent.newCount }}</strong></div>
            <div class="metric-line"><span>سعر البيع</span><strong>{{ money(detailCurrent.newSale) }} د.ع</strong></div>
            <div class="metric-line"><span>القسط</span><strong>{{ money(detailCurrent.newDay) }} د.ع</strong></div>
          </div>
          <div class="month-section">
            <p class="month-label">مصفرون</p>
            <div class="metric-line"><span>العدد</span><strong>{{ detailCurrent.zeroCount }}</strong></div>
            <div class="metric-line"><span>سعر البيع</span><strong>{{ money(detailCurrent.zeroSale) }} د.ع</strong></div>
            <div class="metric-line"><span>القسط</span><strong>{{ money(detailCurrent.zeroDay) }} د.ع</strong></div>
          </div>
          <div class="month-section month-section--net">
            <p class="month-label">الفرق (تقييم − مصفرون)</p>
            <div class="metric-line">
              <span>فرق العدد</span>
              <strong :class="signedClass(detailCurrent.netCount)">{{ signedText(detailCurrent.netCount) }}</strong>
            </div>
            <div class="metric-line">
              <span>فرق سعر البيع</span>
              <strong :class="signedClass(detailCurrent.netSale)">{{ signedText(detailCurrent.netSale, true) }}</strong>
            </div>
            <div class="metric-line">
              <span>فرق القسط</span>
              <strong :class="signedClass(detailCurrent.netDay)">{{ signedText(detailCurrent.netDay, true) }}</strong>
            </div>
          </div>
        </article>
      </div>

      <div
        v-if="showTrendDetail && showBuckets && bucketCounts"
        class="buckets"
      >
        <button
          v-for="b in ratingBuckets"
          :key="b.key"
          type="button"
          class="bucket-card"
          :class="{ active: activeBucket?.key === b.key && bucketDialog }"
          @click="openBucket(b)"
        >
          <span>{{ b.label }}</span>
          <strong>{{ bucketCounts[b.countKey] }}</strong>
        </button>
      </div>
    </section>

    <VDialog
      v-model="bucketDialog"
      max-width="920"
      scrollable
    >
      <VCard>
        <VCardTitle class="d-flex align-center justify-space-between flex-wrap ga-2">
          <span>
            زبائن {{ activeBucket?.label || '' }}
            <template v-if="selected">
              — {{ selected.name }}
            </template>
          </span>
          <VBtn
            icon="tabler-x"
            variant="text"
            @click="bucketDialog = false"
          />
        </VCardTitle>
        <VCardText>
          <div class="bucket-toolbar mb-4">
            <VTextField
              v-model="bucketSearch"
              density="comfortable"
              variant="outlined"
              hide-details
              prepend-inner-icon="tabler-search"
              placeholder="بحث بالاسم أو الهاتف…"
              @keyup.enter="searchBucketServer"
            />
            <VBtn
              color="primary"
              variant="tonal"
              :loading="bucketBusy"
              @click="searchBucketServer"
            >
              بحث في السيرفر
            </VBtn>
          </div>
          <p
            v-if="bucketTruncated"
            class="muted mb-3"
          >
            عُرض أول {{ bucketCustomers.length }} اسماً من أصل {{ bucketCounts?.[activeBucket.countKey] }} —
            استخدم البحث للوصول لزبون محدد.
          </p>
          <div
            v-if="bucketBusy"
            class="muted"
          >
            جاري تحميل الأسماء…
          </div>
          <VAlert
            v-else-if="bucketError"
            type="error"
            variant="tonal"
          >
            {{ bucketError }}
          </VAlert>
          <p
            v-else-if="filteredBucketCustomers.length === 0"
            class="muted"
          >
            لا زبائن في هذه الحالة.
          </p>
          <div
            v-else
            class="customer-grid"
          >
            <button
              v-for="c in filteredBucketCustomers"
              :key="c.customerId"
              type="button"
              class="customer-card"
              @click="openCustomerProfile(c)"
            >
              <strong class="customer-name">{{ c.customerName }}</strong>
              <div class="customer-meta">
                <span v-if="c.phone">{{ c.phone }}</span>
                <span>{{ c.ratingLabel }}</span>
                <span v-if="!c.hidePaidRemaining && c.amountRemaining != null">متبقي: {{ money(c.amountRemaining) }}</span>
              </div>
            </button>
          </div>
        </VCardText>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.director-shell {
  --ink: #14201b;
  --muted: #5c6b64;
  --sand: #eef3ef;
  --card: #f7faf8;
  --accent: #1f6f5b;
  --accent-deep: #0f3d32;
  --warn: #b45309;
  min-height: calc(100vh - 6rem);
  padding: 0.5rem 0.25rem 2rem;
  color: var(--ink);
}

.director-hero {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 1rem;
  padding: 1.25rem 1.5rem;
  border-radius: 1.25rem;
  background:
    radial-gradient(1200px 280px at 10% -40%, rgba(31, 111, 91, 0.28), transparent 60%),
    linear-gradient(135deg, #0f3d32 0%, #1a5648 55%, #244a3f 100%);
  color: #f4faf7;
  margin-bottom: 1.25rem;
}

.eyebrow {
  margin: 0;
  opacity: 0.75;
  letter-spacing: 0.08em;
  font-size: 0.75rem;
  text-transform: uppercase;
}

.director-hero h1 {
  margin: 0.2rem 0;
  font-size: clamp(1.75rem, 3vw, 2.4rem);
  font-weight: 700;
  line-height: 1.15;
}

.director-hero h1.province-title,
.director-hero h1.hub-title {
  color: #ffffff !important;
  font-weight: 800 !important;
  -webkit-text-fill-color: #ffffff;
  opacity: 1;
}

.sub {
  margin: 0;
  opacity: 0.85;
  max-width: 40rem;
}

.hub-bar {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 1rem;
}

.search {
  flex: 1;
  max-width: 28rem;
}

.count {
  color: var(--muted);
  font-size: 0.95rem;
}

.cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 0.95rem;
}

.province-card {
  text-align: start;
  border: 1px solid rgba(20, 32, 27, 0.08);
  background:
    linear-gradient(165deg, #ffffff 0%, #f4f8f6 100%);
  border-radius: 1.15rem;
  padding: 1.15rem 1.2rem 1.05rem;
  cursor: pointer;
  transition: transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease;
  box-shadow: 0 1px 0 rgba(15, 61, 50, 0.04);
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  min-height: 5.5rem;
}

.province-card:hover,
.province-card:focus-visible {
  transform: translateY(-2px);
  border-color: rgba(31, 111, 91, 0.35);
  box-shadow: 0 12px 28px rgba(15, 61, 50, 0.1);
  outline: none;
}

.province-name {
  display: block;
  font-size: 1.2rem;
  font-weight: 800;
  margin: 0;
  color: var(--accent-deep);
}

.province-cta {
  color: var(--accent);
  font-size: 0.9rem;
  margin-top: auto;
}

.province-deltas {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  margin-top: 0.15rem;
}

.delta-row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.75rem;
  font-size: 0.86rem;
}

.delta-row span {
  color: var(--muted);
}

.delta-row strong {
  font-variant-numeric: tabular-nums;
  font-weight: 800;
  white-space: nowrap;
}

.delta-pos { color: #15803d !important; }
.delta-neg { color: #b91c1c !important; }
.delta-flat { color: #4b5563 !important; }

.month-inline {
  margin-top: 0.65rem;
  padding-top: 0.55rem;
  border-top: 1px dashed rgba(20, 32, 27, 0.12);
  display: grid;
  gap: 0.2rem;
  font-size: 0.88rem;
  color: var(--muted);
}

.month-inline p {
  margin: 0;
}

.month-inline strong {
  color: var(--ink);
}

.month-loading {
  margin: 0.75rem 0;
}

.month-stats {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.85rem;
  margin: 1rem 0 1.15rem;
}

.month-card {
  background: #fff;
  border: 1px solid rgba(20, 32, 27, 0.08);
  border-radius: 1.1rem;
  padding: 1rem 1.1rem 1.05rem;
  box-shadow: 0 8px 22px rgba(15, 61, 50, 0.05);
}

.month-card--prev {
  background: linear-gradient(180deg, #f8faf9 0%, #eef3ef 100%);
}

.month-card--cur {
  border-color: rgba(31, 111, 91, 0.28);
  background: linear-gradient(180deg, #ffffff 0%, #eef7f3 100%);
}

.month-card header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-bottom: 0.7rem;
  gap: 0.5rem;
}

.month-card header h3 {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: var(--accent-deep);
}

.month-card header span {
  color: var(--muted);
  font-size: 0.85rem;
  font-variant-numeric: tabular-nums;
}

.month-section {
  padding: 0.45rem 0;
  border-top: 1px solid rgba(20, 32, 27, 0.06);
}

.month-section--net {
  background: rgba(31, 111, 91, 0.05);
  margin: 0.35rem -0.35rem 0;
  padding: 0.55rem 0.55rem 0.4rem;
  border-radius: 0.75rem;
  border-top: none;
}

.month-label {
  margin: 0 0 0.35rem;
  font-weight: 700;
  color: var(--accent);
  font-size: 0.9rem;
}

.metric-line {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  font-size: 0.9rem;
  margin-bottom: 0.28rem;
}

.metric-line span {
  color: var(--muted);
}

.metric-line strong {
  font-variant-numeric: tabular-nums;
}

@media (max-width: 900px) {
  .month-stats {
    grid-template-columns: 1fr;
  }
}

.filters {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: 0.75rem;
  margin-bottom: 1rem;
  align-items: end;
}

.scope-row {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.65rem;
  margin-bottom: 1rem;
}

.scope-chip {
  border: 1px solid rgba(20, 32, 27, 0.1);
  background: #fff;
  border-radius: 0.9rem;
  padding: 0.85rem 0.9rem;
  text-align: start;
  cursor: pointer;
  transition: border-color 0.15s ease, background 0.15s ease;
}

.scope-chip strong {
  display: block;
  font-size: 1rem;
}

.scope-chip small {
  color: var(--muted);
}

.scope-chip.active {
  border-color: var(--accent);
  background: rgba(31, 111, 91, 0.08);
}

.follower-lists {
  margin: 1.25rem 0 0.5rem;
}

.follower-lists__head h3 {
  margin: 0 0 0.25rem;
  font-size: 1.15rem;
}

.follower-lists__head .muted {
  margin: 0 0 1rem;
}

.follower-overview-head {
  margin: 1.5rem 0 0.75rem;
}

.follower-overview-head h3 {
  margin: 0 0 0.25rem;
  font-size: 1.2rem;
}

.follower-overview-head .muted {
  margin: 0;
}

.list-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 0.85rem;
}

.list-card {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.35rem;
  padding: 1rem 1.1rem;
  border-radius: 14px;
  border: 1px solid rgba(20, 32, 27, 0.1);
  background: linear-gradient(165deg, #ffffff 0%, #f4f8f6 100%);
  cursor: pointer;
  text-align: start;
  transition: border-color 0.15s ease, transform 0.15s ease;
}

.list-card:hover {
  border-color: var(--accent);
  transform: translateY(-1px);
}

.list-card__name {
  font-size: 1rem;
}

.list-card__score {
  font-size: 1.65rem;
  font-weight: 800;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}

.list-card__score[data-dir='up'] { color: #15803d; }
.list-card__score[data-dir='down'] { color: #b91c1c; }

.list-card__label {
  font-size: 0.85rem;
  opacity: 0.85;
}

.list-card__count {
  font-size: 0.8rem;
  opacity: 0.75;
}

.list-card__installment {
  font-size: 0.8rem;
  opacity: 0.85;
  color: #0f766e;
}

.payments-sum-box {
  padding: 12px 14px;
  border-radius: 12px;
  background: rgba(16, 185, 129, 0.1);
  border: 1px solid rgba(16, 185, 129, 0.25);
}

.drill-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  margin: 0.75rem 0 0.25rem;
}

.drill-bar__title {
  font-weight: 700;
  font-size: 1.05rem;
}

.entity-row {
  margin-bottom: 1rem;
  max-width: 28rem;
}

.result-panel {
  display: grid;
  grid-template-columns: minmax(180px, 240px) 1fr;
  gap: 1rem;
  background: var(--sand);
  border-radius: 1.15rem;
  padding: 1.25rem;
  min-height: 10rem;
}

.metric {
  margin: 0;
  color: var(--muted);
  font-size: 0.85rem;
}

.score {
  margin: 0.25rem 0;
  font-size: 2.75rem;
  font-weight: 800;
  letter-spacing: -0.03em;
  color: var(--accent-deep);
  line-height: 1;
}

.metric--phantom {
  margin-top: 0.85rem;
  color: #b45309;
}

.score--phantom {
  font-size: 2.75rem;
  color: #b45309;
}

.label-pill {
  display: inline-block;
  margin: 0;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
  background: rgba(31, 111, 91, 0.12);
  color: var(--accent-deep);
  font-weight: 600;
}

.label-pill[data-dir='down'] {
  background: rgba(180, 83, 9, 0.15);
  color: var(--warn);
}

.cust-count,
.day-count {
  margin: 0.35rem 0 0;
  color: var(--muted);
  font-size: 0.8rem;
}

.cust-count strong {
  color: var(--accent-deep);
  font-size: 1rem;
}

.period-rating-box {
  margin-top: 0.75rem;
  padding: 0.65rem 0.75rem;
  border-radius: 10px;
  border: 1px dashed rgba(31, 111, 91, 0.35);
  background: rgba(31, 111, 91, 0.06);
}

.period-rating-box__title {
  margin: 0 0 0.35rem;
  font-size: 0.75rem;
  color: var(--muted);
}

.period-rating-box__row {
  margin: 0.2rem 0 0;
  font-size: 0.85rem;
}

.period-rating-box__row strong {
  color: var(--accent-deep);
}

.spark {
  color: var(--accent);
  display: flex;
  flex-direction: column;
  justify-content: center;
  min-height: 10rem;
  position: relative;
}

.spark .trend-svg {
  width: 100%;
  height: 9rem;
  cursor: crosshair;
}

.spark .hit {
  fill: transparent;
  cursor: pointer;
}

.spark .dot {
  fill: currentColor;
  pointer-events: none;
}

.spark .pt.active .dot {
  fill: #0f3d32;
  stroke: #fff;
  stroke-width: 2;
}

.point-pop {
  position: absolute;
  top: 0.25rem;
  inset-inline-start: 0.5rem;
  z-index: 2;
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding: 0.45rem 0.7rem;
  border-radius: 0.65rem;
  background: #0f3d32;
  color: #f4faf7;
  font-size: 0.9rem;
  box-shadow: 0 8px 20px rgba(15, 61, 50, 0.25);
}

.point-pop strong {
  font-weight: 800;
}

.point-close {
  border: 0;
  background: transparent;
  color: inherit;
  font-size: 1.25rem;
  line-height: 1;
  cursor: pointer;
  padding: 0 0.15rem;
}

.axis-row {
  display: flex;
  justify-content: space-between;
  margin-top: 0.35rem;
  color: var(--muted);
  font-size: 0.75rem;
}

.buckets {
  margin-top: 1rem;
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 0.5rem;
}

.bucket-card {
  background: #fff;
  border-radius: 0.75rem;
  padding: 0.75rem;
  border: 1px solid rgba(20, 32, 27, 0.06);
  text-align: start;
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease, background 0.15s ease;
}

.bucket-card:hover,
.bucket-card.active {
  border-color: rgba(31, 111, 91, 0.45);
  background: rgba(31, 111, 91, 0.06);
  box-shadow: 0 8px 18px rgba(15, 61, 50, 0.08);
}

.bucket-card span {
  display: block;
  color: var(--muted);
  font-size: 0.8rem;
}

.bucket-card strong {
  font-size: 1.25rem;
}

.bucket-toolbar {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 0.75rem;
  align-items: center;
}

.customer-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 0.65rem;
}

.customer-card {
  text-align: start;
  border: 1px solid rgba(20, 32, 27, 0.08);
  background: var(--card);
  border-radius: 0.85rem;
  padding: 0.85rem 1rem;
  cursor: pointer;
  transition: border-color 0.15s ease, transform 0.12s ease;
}

.customer-card:hover {
  border-color: rgba(31, 111, 91, 0.4);
  transform: translateY(-1px);
}

.customer-name {
  display: block;
  font-size: 1.05rem;
  margin-bottom: 0.35rem;
}

.customer-meta {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  color: var(--muted);
  font-size: 0.8rem;
}

.muted {
  color: var(--muted);
}

@media (max-width: 960px) {
  .scope-row,
  .filters,
  .result-panel,
  .buckets {
    grid-template-columns: 1fr 1fr;
  }

  .filters,
  .bucket-toolbar {
    grid-template-columns: 1fr;
  }

  .result-panel {
    grid-template-columns: 1fr;
  }
}
</style>
