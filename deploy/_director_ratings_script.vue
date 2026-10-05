<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSalesBranches } from '@/composables/useSalesBranches'
import {
  directorCities,
  directorStatsBranchMonth,
  directorStatsCatalog,
  directorStatsRatingCustomers,
  directorStatsTrend,
} from '@/composables/directorApi'
import { trendCacheKey, useDirectorRatingsCache } from '@/composables/useDirectorRatingsCache'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()
const router = useRouter()
const route = useRoute()
const cache = useDirectorRatingsCache()
const { branches, isLoading: branchesLoading, fetchCities } = useSalesBranches()

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
  { value: 'list', title: 'المندوبين', hint: 'تقييم كل قائمة' },
  { value: 'follower', title: 'المتابعين', hint: 'متوسط قوائم المتابع' },
  { value: 'employee', title: 'موظفو المبيعات', hint: 'تقييم مبيعات الموظف' },
]

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
  if (scope.value === 'list')
    return catalog.value.lists || []
  if (scope.value === 'follower')
    return catalog.value.followers || []
  if (scope.value === 'employee')
    return catalog.value.employees || []

  return []
})

const needsEntity = computed(() => scope.value !== 'branch')

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

const direction = computed(() =>
  String(trend.value?.direction || trend.value?.Direction || 'flat').toLowerCase())

const metricLabel = computed(() =>
  trend.value?.metricLabel || trend.value?.MetricLabel || 'متوسط نقاط زبائن تاريخ البيع — التقييم حتى اليوم')

const customerCount = computed(() => {
  const t = trend.value
  if (!t)
    return null
  const n = t.customerCount ?? t.CustomerCount

  return n == null ? null : Number(n)
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

const showBuckets = computed(() => trend.value != null)

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
  return trendCacheKey({
    cityValue,
    scope: scope.value,
    id: scope.value === 'branch' ? 0 : entityId.value,
    fromDate: fromDate.value,
    toDate: toDate.value,
  })
}

function openCity(city, { pushHistory = true } = {}) {
  if (!city?.value)
    return
  selected.value = city
  scope.value = 'branch'
  entityId.value = 0
  bucketDialog.value = false
  selectedPoint.value = null

  const cachedMonth = cache.getMonth(city.value) || hubMonthByCity.value[city.value] || null
  detailMonth.value = cachedMonth

  const cachedCatalog = cache.getCatalog(city.value)
  if (cachedCatalog)
    catalog.value = cachedCatalog

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
  selected.value = null
  trend.value = null
  detailMonth.value = null
  bucketDialog.value = false
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
  const hit = cache.getMonth(cityValue) || hubMonthByCity.value[cityValue]
  if (hit) {
    detailMonth.value = hit

    return
  }
  detailMonthBusy.value = true
  try {
    const row = await directorStatsBranchMonth(cityValue)
    detailMonth.value = row
    cache.setMonth(cityValue, row)
    hubMonthByCity.value = { ...hubMonthByCity.value, [cityValue]: row }
  }
  catch {
    detailMonth.value = null
  }
  finally {
    detailMonthBusy.value = false
  }
}

async function loadCatalog(cityValue) {
  const cached = cache.getCatalog(cityValue)
  if (cached) {
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
    catalog.value = mapped
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر تحميل قوائم المحافظة')
    catalog.value = { lists: [], followers: [], employees: [] }
  }
  finally {
    catalogBusy.value = false
  }
}

function normalizeEntities(rows) {
  return (Array.isArray(rows) ? rows : []).map(r => ({
    id: Number(r.id ?? r.Id ?? 0),
    name: r.name || r.Name || `#${r.id ?? r.Id}`,
  })).filter(r => r.id > 0)
}

async function loadTrend({ useCacheFirst = false } = {}) {
  if (!selected.value)
    return
  if (needsEntity.value && !(entityId.value > 0)) {
    trend.value = null

    return
  }
  const key = currentTrendKey()
  if (useCacheFirst) {
    const cached = cache.getTrend(key)
    if (cached) {
      trend.value = cached
      void refreshTrend(key)

      return
    }
  }
  await refreshTrend(key)
}

async function refreshTrend(key = currentTrendKey()) {
  if (!selected.value)
    return
  trendBusy.value = true
  selectedPoint.value = null
  try {
    const data = await directorStatsTrend({
      cityValue: selected.value.value,
      scope: scope.value,
      id: scope.value === 'branch' ? 0 : entityId.value,
      fromDate: fromDate.value,
      toDate: toDate.value,
    })
    trend.value = data
    cache.setTrend(key, data)
  }
  catch (err) {
    if (!trend.value)
      toast.error(err?.response?.data?.message || 'تعذر تحميل التقييم')
  }
  finally {
    trendBusy.value = false
  }
}

function mapCustomerRows(list, fallbackLabel) {
  return list.map(c => ({
    customerId: c.customerId ?? c.CustomerId,
    customerName: c.customerName || c.CustomerName || `زبون ${c.customerId ?? c.CustomerId}`,
    phone: c.phone || c.Phone || '',
    dateSale: c.dateSale ?? c.DateSale,
    lastPaymentDate: c.lastPaymentDate ?? c.LastPaymentDate,
    amountPaid: c.amountPaid ?? c.AmountPaid ?? 0,
    amountRemaining: c.amountRemaining ?? c.AmountRemaining ?? 0,
    amountTotal: c.amountTotal ?? c.AmountTotal ?? 0,
    ratingLabel: c.ratingLabel || c.RatingLabel || fallbackLabel,
    score: c.score ?? c.Score,
  }))
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

async function openBucket(bucket) {
  if (!selected.value)
    return
  if (needsEntity.value && !(entityId.value > 0)) {
    toast.error('اختر المندوب أو المتابع أو الموظف أولاً')

    return
  }

  activeBucket.value = bucket
  bucketCustomers.value = []
  bucketSearch.value = ''
  bucketError.value = ''
  bucketDialog.value = true
  bucketBusy.value = true
  try {
    const data = await directorStatsRatingCustomers({
      cityValue: selected.value.value,
      scope: scope.value,
      id: scope.value === 'branch' ? 0 : entityId.value,
      level: bucket.key,
      fromDate: fromDate.value,
      toDate: toDate.value,
      take: bucketPageSize,
      skip: 0,
    })
    bucketCustomers.value = mapCustomerRows(unwrapCustomerList(data), bucket.label)
  }
  catch (err) {
    bucketError.value = err?.response?.data?.message || err?.message || 'تعذر تحميل أسماء الزبائن'
  }
  finally {
    bucketBusy.value = false
  }
}

async function searchBucketServer() {
  if (!activeBucket.value || !selected.value)
    return
  bucketBusy.value = true
  bucketError.value = ''
  try {
    const data = await directorStatsRatingCustomers({
      cityValue: selected.value.value,
      scope: scope.value,
      id: scope.value === 'branch' ? 0 : entityId.value,
      level: activeBucket.value.key,
      fromDate: fromDate.value,
      toDate: toDate.value,
      take: bucketPageSize,
      skip: 0,
      q: bucketSearch.value.trim() || undefined,
    })
    bucketCustomers.value = mapCustomerRows(unwrapCustomerList(data), activeBucket.value.label)
  }
  catch (err) {
    bucketError.value = err?.response?.data?.message || err?.message || 'تعذر البحث'
  }
  finally {
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
    if (st.fromDate)
      fromDate.value = st.fromDate
    if (st.toDate)
      toDate.value = st.toDate
    if (st.scope)
      scope.value = st.scope
    if (st.city) {
      openCity({
        value: st.city,
        name: st.cityName || st.city,
      }, { pushHistory: !route.query.city })
      entityId.value = Number(st.entityId) || 0
      await Promise.all([
        loadCatalog(st.city),
        loadTrend({ useCacheFirst: true }),
        loadDetailMonth(st.city),
      ])
    }
    if (st.dialogOpen && st.bucketKey) {
      const bucket = ratingBuckets.find(b => b.key === st.bucketKey)
      if (bucket) {
        await openBucket(bucket)
        if (st.bucketSearch)
          bucketSearch.value = st.bucketSearch
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
  entityId.value = 0
  if (scope.value === 'branch')
    loadTrend({ useCacheFirst: true })
})

watch(entityId, () => {
  if (!needsEntity.value || entityId.value > 0)
    loadTrend({ useCacheFirst: true })
})

watch(() => route.query.city, () => {
  applyRouteCity()
})

onMounted(async () => {
  await loadCities()
  const restored = await restoreDirectorReturnState()
  if (!restored)
    applyRouteCity()
  void warmMonthStats()
})

onBeforeUnmount(() => {})
</script>
