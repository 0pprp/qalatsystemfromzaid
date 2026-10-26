<script setup>
import { computed, nextTick, onActivated, onDeactivated, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  dmDelegateFollowupListCustomers,
  dmDelegateFollowupListSummary,
  dmFollowerCards,
  dmFollowerCollection,
  dmFollowerLegalFollowups,
  dmFollowerRemainingCustomers,
  dmFollowerSummaries,
  dmFollowerWorkBundle,
} from '@/composables/delegatedManagerApi'
import { formatIraqDate } from '@/composables/iraqDate'
import { useSalesBranches } from '@/composables/useSalesBranches'
import { useToast } from '@/composables/useToast'

defineOptions({ name: 'DmStats' })
definePage({
  meta: {
    layout: 'dm',
    // Keep board in memory when opening customer profile (no reload on رجوع).
    keepAlive: true,
  },
})

const CITY_KEY = 'dm-stats-city'
const toast = useToast()
const router = useRouter()
const route = useRoute()
const { branches, isLoading: branchesLoading, fetchCities } = useSalesBranches()

const cityValue = ref('')
const busy = ref(false)
const summariesBusy = ref(false)
const detailBusy = ref(false)
const followers = ref([])
const summariesById = ref({})
const selected = ref(null)
const workBundle = ref(null)
const legalRows = ref([])
const collection = ref({ lists: [], payments: [] })
const panel = ref('') // bucket | legal | imports | list | nonZeroed
const remainingBucket = ref('')
const remainingRows = ref([])
const remainingBusy = ref(false)
const listSummary = ref(null)
const listCustomers = ref([])
const listBusy = ref(false)
const selectedListId = ref(null)
const selectedListName = ref('')
const search = ref('')

/** Scroll + hydrate gate — KeepAlive return must not refetch. */
let cityWatchReady = false
let savedScrollY = 0
const boardReady = ref(false)

/** Module cache survives layout remount races (same idea as عمل المتابعين). */
const boardCache = {
  city: '',
  followers: [],
  summariesById: {},
  selected: null,
  workBundle: null,
  legalRows: [],
  collection: { lists: [], payments: [] },
  panel: '',
  remainingBucket: '',
  remainingRows: [],
  listSummary: null,
  listCustomers: [],
  selectedListId: null,
  selectedListName: '',
  search: '',
  scrollY: 0,
}

function captureScroll() {
  savedScrollY = window.scrollY || document.documentElement.scrollTop || 0
  boardCache.scrollY = savedScrollY
}

function restoreScroll() {
  const y = savedScrollY || boardCache.scrollY || 0
  nextTick(() => {
    requestAnimationFrame(() => {
      window.scrollTo({ top: y, left: 0, behavior: 'auto' })
    })
  })
}

function snapshotBoard() {
  boardCache.city = cityValue.value
  boardCache.followers = followers.value
  boardCache.summariesById = summariesById.value
  boardCache.selected = selected.value
  boardCache.workBundle = workBundle.value
  boardCache.legalRows = legalRows.value
  boardCache.collection = collection.value
  boardCache.panel = panel.value
  boardCache.remainingBucket = remainingBucket.value
  boardCache.remainingRows = remainingRows.value
  boardCache.listSummary = listSummary.value
  boardCache.listCustomers = listCustomers.value
  boardCache.selectedListId = selectedListId.value
  boardCache.selectedListName = selectedListName.value
  boardCache.search = search.value
  boardCache.scrollY = savedScrollY
}

function hydrateFromCache() {
  if (!boardCache.city || !boardCache.followers?.length)
    return false
  cityValue.value = boardCache.city
  followers.value = boardCache.followers
  summariesById.value = boardCache.summariesById || {}
  selected.value = boardCache.selected
  workBundle.value = boardCache.workBundle
  legalRows.value = boardCache.legalRows || []
  collection.value = boardCache.collection || { lists: [], payments: [] }
  panel.value = boardCache.panel || ''
  remainingBucket.value = boardCache.remainingBucket || ''
  remainingRows.value = boardCache.remainingRows || []
  listSummary.value = boardCache.listSummary
  listCustomers.value = boardCache.listCustomers || []
  selectedListId.value = boardCache.selectedListId
  selectedListName.value = boardCache.selectedListName || ''
  search.value = boardCache.search || ''
  savedScrollY = boardCache.scrollY || 0
  busy.value = false
  detailBusy.value = false
  return true
}

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] != null && obj[k] !== '')
      return obj[k]
  }
  return undefined
}

function fmt(n) {
  const v = Number(n)
  if (!Number.isFinite(v)) return '—'
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(v)
}

function fmtRating(n) {
  const v = Number(n)
  if (!Number.isFinite(v)) return '—'
  return v.toFixed(1)
}

function trendKey(row, ...keys) {
  return String(pick(row, ...keys) || 'flat').toLowerCase()
}

function trendIcon(t) {
  if (t === 'up') return 'tabler-trending-up'
  if (t === 'down') return 'tabler-trending-down'
  return 'tabler-minus'
}

function trendClass(t) {
  if (t === 'up') return 'is-up'
  if (t === 'down') return 'is-down'
  return 'is-flat'
}

function persistCity(v) {
  if (!v) return
  try { localStorage.setItem(CITY_KEY, v) } catch { /* ignore */ }
  if (String(route.query.cityValue || '') !== v) {
    router.replace({
      path: route.path,
      query: { ...route.query, cityValue: v },
    }).catch(() => {})
  }
}

const cityItems = computed(() =>
  (branches.value || [])
    .map(b => ({
      title: b.name || b.Name || String(b.value),
      value: String(b.value ?? b.Value ?? ''),
    }))
    .filter(c => c.value)
    .sort((a, b) => a.title.localeCompare(b.title, 'ar')),
)

const filteredFollowers = computed(() => {
  const q = search.value.trim()
  let rows = followers.value || []
  if (q) {
    rows = rows.filter(r =>
      String(pick(r, 'followerName', 'FollowerName') || '').includes(q)
      || String(pick(r, 'followerId', 'FollowerId') || '').includes(q))
  }
  return rows
})

const listCards = computed(() => {
  const b = workBundle.value || {}
  return Array.isArray(b.listCards) ? b.listCards : (b.ListCards || [])
})

const ratingBuckets = computed(() => {
  const b = workBundle.value || {}
  return Array.isArray(b.ratingBuckets) ? b.ratingBuckets : (b.RatingBuckets || [])
})

const nonZeroedCount = computed(() =>
  Number(pick(workBundle.value, 'ratedCustomerCount', 'RatedCustomerCount') || 0),
)

const remainingLe100 = computed(() =>
  Number(pick(workBundle.value, 'remainingLe100Count', 'RemainingLe100Count') || 0),
)

const remainingLt200 = computed(() =>
  Number(pick(workBundle.value, 'remainingLt200Count', 'RemainingLt200Count') || 0),
)

const legalCount = computed(() => legalRows.value.length)

const importsSum = computed(() => {
  const pays = collection.value.payments || []
  return pays.reduce((s, p) => s + (Number(pick(p, 'amount', 'Amount')) || 0), 0)
})

const importsYesterday = computed(() => {
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Baghdad' }).format(new Date())
  const y = new Date(`${today}T12:00:00`)
  y.setDate(y.getDate() - 1)
  const yIso = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Baghdad' }).format(y)
  return (collection.value.payments || [])
    .filter(p => String(pick(p, 'paymentDate', 'PaymentDate') || '').slice(0, 10) === yIso)
    .reduce((s, p) => s + (Number(pick(p, 'amount', 'Amount')) || 0), 0)
})

const panelTitle = computed(() => {
  if (panel.value === 'legal') return 'القضايا القانونية'
  if (panel.value === 'imports') return 'واردات التحصيل'
  if (panel.value === 'list') return `زبائن القائمة — ${selectedListName.value || ''}`
  if (panel.value === 'nonZeroed' || remainingBucket.value)
    return 'زبائن الشريحة'
  return ''
})

function cardImports(row) {
  const id = Number(pick(row, 'followerId', 'FollowerId') || 0)
  const s = summariesById.value[id]
  return Number(pick(s, 'collectionPaymentSum', 'CollectionPaymentSum') || 0)
}

function cardImportCount(row) {
  const id = Number(pick(row, 'followerId', 'FollowerId') || 0)
  const s = summariesById.value[id]
  return Number(pick(s, 'collectionPaymentCount', 'CollectionPaymentCount') || 0)
}

function resetDetailPanels() {
  panel.value = ''
  remainingBucket.value = ''
  remainingRows.value = []
  listSummary.value = null
  listCustomers.value = []
  selectedListId.value = null
  selectedListName.value = ''
}

async function loadProvinces() {
  await fetchCities()
  const fromQuery = String(route.query.cityValue || '').trim()
  let fromStore = ''
  try { fromStore = localStorage.getItem(CITY_KEY) || '' } catch { fromStore = '' }
  const items = cityItems.value
  const has = v => items.some(c => c.value === v)
  if (fromQuery && has(fromQuery))
    cityValue.value = fromQuery
  else if (fromStore && has(fromStore))
    cityValue.value = fromStore
  else if (!cityValue.value && items[0])
    cityValue.value = items[0].value
  if (cityValue.value)
    persistCity(cityValue.value)
}

async function loadSummariesInBackground() {
  if (!cityValue.value) return
  summariesBusy.value = true
  try {
    const summaries = await dmFollowerSummaries(cityValue.value).catch(() => [])
    const map = {}
    for (const s of (Array.isArray(summaries) ? summaries : [])) {
      const id = Number(pick(s, 'followerId', 'FollowerId') || 0)
      if (id) map[id] = s
    }
    summariesById.value = map
  }
  finally {
    summariesBusy.value = false
  }
}

async function loadFollowers() {
  if (!cityValue.value) return
  busy.value = true
  selected.value = null
  workBundle.value = null
  resetDetailPanels()
  try {
    const cards = await dmFollowerCards(cityValue.value)
    followers.value = Array.isArray(cards) ? cards : []
    snapshotBoard()
  }
  catch (e) {
    followers.value = []
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل المتابعين')
  }
  finally {
    busy.value = false
  }
  void loadSummariesInBackground()
}

async function openFollower(row) {
  selected.value = row
  resetDetailPanels()
  detailBusy.value = true
  workBundle.value = null
  legalRows.value = []
  collection.value = { lists: [], payments: [] }
  const fid = Number(pick(row, 'followerId', 'FollowerId') || 0)
  try {
    const bundle = await dmFollowerWorkBundle(cityValue.value, fid)
    workBundle.value = bundle || {}
    detailBusy.value = false
    requestAnimationFrame(() => {
      document.getElementById('dm-follower-detail')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    })
    const [legal, coll] = await Promise.all([
      dmFollowerLegalFollowups(cityValue.value, fid).catch(() => []),
      dmFollowerCollection(cityValue.value, fid).catch(() => ({})),
    ])
    legalRows.value = Array.isArray(legal) ? legal : []
    collection.value = {
      lists: pick(coll, 'lists', 'Lists') || [],
      payments: pick(coll, 'payments', 'Payments') || [],
    }
    snapshotBoard()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل لوحة المتابع')
    detailBusy.value = false
  }
}

async function openRemainingBucket(bucket) {
  if (!selected.value || !cityValue.value) return
  const fid = Number(pick(selected.value, 'followerId', 'FollowerId') || 0)
  if (panel.value === 'bucket' && remainingBucket.value === bucket) {
    resetDetailPanels()
    return
  }
  panel.value = 'bucket'
  remainingBucket.value = bucket
  selectedListId.value = null
  remainingBusy.value = true
  try {
    const rows = await dmFollowerRemainingCustomers(cityValue.value, fid, bucket)
    remainingRows.value = Array.isArray(rows) ? rows : []
  }
  catch (e) {
    remainingRows.value = []
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل الزبائن')
  }
  finally {
    remainingBusy.value = false
    snapshotBoard()
  }
}

async function openNonZeroed() {
  if (panel.value === 'nonZeroed') {
    resetDetailPanels()
    return
  }
  await openRemainingBucket('rated')
  panel.value = 'nonZeroed'
}

function openLegalPanel() {
  if (panel.value === 'legal') {
    resetDetailPanels()
    return
  }
  panel.value = 'legal'
  remainingBucket.value = ''
  selectedListId.value = null
}

function openImportsPanel() {
  if (panel.value === 'imports') {
    resetDetailPanels()
    return
  }
  panel.value = 'imports'
  remainingBucket.value = ''
  selectedListId.value = null
}

async function openListCard(lc) {
  if (!cityValue.value) return
  const listId = Number(pick(lc, 'listId', 'ListId') || 0)
  if (!listId) return
  if (panel.value === 'list' && selectedListId.value === listId) {
    resetDetailPanels()
    return
  }
  panel.value = 'list'
  remainingBucket.value = ''
  selectedListId.value = listId
  selectedListName.value = pick(lc, 'listName', 'ListName') || `قائمة ${listId}`
  listBusy.value = true
  listSummary.value = null
  listCustomers.value = []
  try {
    const [sum, custs] = await Promise.all([
      dmDelegateFollowupListSummary(cityValue.value, listId),
      dmDelegateFollowupListCustomers(cityValue.value, listId),
    ])
    listSummary.value = sum || null
    listCustomers.value = Array.isArray(custs) ? custs : []
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل تفاصيل القائمة')
  }
  finally {
    listBusy.value = false
    snapshotBoard()
  }
}

function openProfile(row) {
  const id = pick(row, 'customerId', 'CustomerId', 'customerID', 'CustomerID')
  if (!id || !cityValue.value) return
  captureScroll()
  snapshotBoard()
  persistCity(cityValue.value)
  router.push({
    path: '/dm-customer-profile',
    query: {
      cityValue: cityValue.value,
      customerId: String(id),
      name: pick(row, 'customerName', 'CustomerName') || '',
      phone: pick(row, 'phone', 'Phone', 'phoneNumber', 'PhoneNumber') || '',
      returnTo: 'dm-stats',
    },
  })
}

watch(cityValue, async (v, prev) => {
  if (!cityWatchReady || !v || v === prev) return
  persistCity(v)
  await loadFollowers()
})

onMounted(async () => {
  // Instant restore before any network (KeepAlive miss / layout remount).
  const restored = hydrateFromCache()
  await loadProvinces()
  // Prefer cached city over default first province.
  if (restored && boardCache.city)
    cityValue.value = boardCache.city
  cityWatchReady = true
  if (restored || followers.value.length) {
    boardReady.value = true
    restoreScroll()
    return
  }
  if (cityValue.value)
    await loadFollowers()
  boardReady.value = true
})

onDeactivated(() => {
  captureScroll()
  snapshotBoard()
})

onActivated(() => {
  // Stay as-is: selected follower, panels, rows — only restore scroll.
  cityWatchReady = true
  boardReady.value = true
  if (!followers.value.length)
    hydrateFromCache()
  restoreScroll()
})
</script>

<template>
  <div class="dm-board">
    <header class="dm-board__hero">
      <div>
        <p class="dm-board__eyebrow">
          لوحة التقييمات
        </p>
        <h1 class="dm-board__title">
          متابعة المتابعين
        </h1>
        <p class="dm-board__sub">
          كارت لكل متابع بتقييم قوائمه — ثم غير المصفرين والقانونية والواردات والمتبقي
        </p>
      </div>
      <div class="dm-board__filters">
        <VSelect
          v-model="cityValue"
          :items="cityItems"
          :loading="branchesLoading"
          item-title="title"
          item-value="value"
          label="المحافظة"
          density="comfortable"
          hide-details
          variant="outlined"
          class="dm-board__select"
          no-data-text="لا محافظات"
        />
        <VTextField
          v-model="search"
          label="بحث متابع"
          density="comfortable"
          hide-details
          variant="outlined"
          prepend-inner-icon="tabler-search"
          class="dm-board__select"
          clearable
        />
        <VBtn
          color="primary"
          class="dm-board__refresh"
          :loading="busy"
          prepend-icon="tabler-refresh"
          @click="loadFollowers"
        >
          تحديث
        </VBtn>
      </div>
    </header>

    <div
      v-if="busy"
      class="dm-board__loading"
    >
      <VProgressCircular
        indeterminate
        color="primary"
      />
    </div>

    <section
      v-else
      class="dm-fgrid"
    >
      <button
        v-for="(row, idx) in filteredFollowers"
        :key="pick(row, 'followerId', 'FollowerId')"
        type="button"
        class="dm-fcard"
        :class="{ 'dm-fcard--active': Number(pick(selected, 'followerId', 'FollowerId')) === Number(pick(row, 'followerId', 'FollowerId')) }"
        :style="{ '--i': Math.min(idx, 16) }"
        @click="openFollower(row)"
      >
        <div class="dm-fcard__name">
          {{ pick(row, 'followerName', 'FollowerName') || 'متابع' }}
        </div>
        <div class="dm-fcard__rating">
          <span>التقييم من القوائم</span>
          <strong>
            {{ fmtRating(pick(row, 'phantomRating', 'PhantomRating')) }}
            <small>{{ pick(row, 'phantomRatingLabel', 'PhantomRatingLabel') || '—' }}</small>
            <VIcon
              :icon="trendIcon(trendKey(row, 'ratingTrend', 'RatingTrend'))"
              size="18"
              :class="trendClass(trendKey(row, 'ratingTrend', 'RatingTrend'))"
            />
          </strong>
        </div>
        <div class="dm-fcard__metrics">
          <div>
            <span>غير المصفرين</span>
            <b>{{ fmt(pick(row, 'ratedCustomerCount', 'RatedCustomerCount')) }}</b>
          </div>
          <div>
            <span>الواردات</span>
            <b>{{ summariesBusy && !cardImportCount(row) ? '…' : fmt(cardImports(row)) }}</b>
            <small>{{ cardImportCount(row) }} دفعة</small>
          </div>
        </div>
      </button>
      <VAlert
        v-if="!filteredFollowers.length"
        type="info"
        variant="tonal"
      >
        لا متابعون لهذه المحافظة
      </VAlert>
    </section>

    <section
      v-if="selected"
      id="dm-follower-detail"
      class="dm-detail"
    >
      <div class="dm-detail__head">
        <div>
          <h2>{{ pick(selected, 'followerName', 'FollowerName') }}</h2>
          <p>
            اضغط أي كارت لفتح التفاصيل — مثل متابعة المندوبين
          </p>
        </div>
        <VBtn
          variant="tonal"
          @click="selected = null; resetDetailPanels()"
        >
          إغلاق
        </VBtn>
      </div>

      <div
        v-if="detailBusy"
        class="dm-board__loading"
      >
        <VProgressCircular
          indeterminate
          color="primary"
        />
      </div>

      <template v-else>
        <div class="dm-kpi">
          <button
            type="button"
            class="dm-kpi__card dm-kpi__card--accent dm-kpi__card--btn"
            :class="{ 'is-active': panel === 'nonZeroed' || remainingBucket === 'all' }"
            @click="openNonZeroed"
          >
            <span>غير المصفرين</span>
            <strong>{{ fmt(nonZeroedCount) }}</strong>
          </button>
          <button
            type="button"
            class="dm-kpi__card dm-kpi__card--legal dm-kpi__card--btn"
            :class="{ 'is-active': panel === 'legal' }"
            @click="openLegalPanel"
          >
            <span>القانونية</span>
            <strong>{{ fmt(legalCount) }}</strong>
          </button>
          <button
            type="button"
            class="dm-kpi__card dm-kpi__card--btn"
            :class="{ 'is-active': panel === 'imports' }"
            @click="openImportsPanel"
          >
            <span>الواردات (تحصيل المتابع)</span>
            <strong>{{ fmt(importsSum) }}</strong>
            <small>أمس: {{ fmt(importsYesterday) }}</small>
          </button>
          <button
            type="button"
            class="dm-kpi__card dm-kpi__card--btn"
            :class="{ 'is-active': remainingBucket === 'le100' }"
            @click="openRemainingBucket('le100')"
          >
            <span>باقي أقل من 100,000</span>
            <strong>{{ fmt(remainingLe100) }}</strong>
          </button>
          <button
            type="button"
            class="dm-kpi__card dm-kpi__card--btn"
            :class="{ 'is-active': remainingBucket === 'lt200' }"
            @click="openRemainingBucket('lt200')"
          >
            <span>باقي 100k – أقل من 200k</span>
            <strong>{{ fmt(remainingLt200) }}</strong>
          </button>
        </div>

        <h3 class="dm-sec-title">
          قوائم المتابع
        </h3>
        <div class="dm-lists">
          <button
            v-for="lc in listCards"
            :key="pick(lc, 'listId', 'ListId')"
            type="button"
            class="dm-list-card dm-list-card--btn"
            :class="{ 'is-active': panel === 'list' && selectedListId === Number(pick(lc, 'listId', 'ListId')) }"
            @click="openListCard(lc)"
          >
            <strong>{{ pick(lc, 'listName', 'ListName') || `قائمة ${pick(lc, 'listId', 'ListId')}` }}</strong>
            <div class="dm-list-card__row">
              <span>التقييم</span>
              <b>
                {{ fmtRating(pick(lc, 'phantomRating', 'PhantomRating')) }}
                <small>{{ pick(lc, 'phantomRatingLabel', 'PhantomRatingLabel') || '' }}</small>
              </b>
            </div>
            <div class="dm-list-card__row">
              <span>غير المصفرين</span>
              <b>{{ fmt(pick(lc, 'ratedCustomerCount', 'RatedCustomerCount')) }}</b>
            </div>
          </button>
          <VAlert
            v-if="!listCards.length"
            type="info"
            variant="tonal"
          >
            لا قوائم مسندة
          </VAlert>
        </div>

        <h3 class="dm-sec-title">
          شرائح غير المصفرين
        </h3>
        <div class="dm-buckets">
          <button
            v-for="rb in ratingBuckets"
            :key="pick(rb, 'levelKey', 'LevelKey')"
            type="button"
            class="dm-bucket"
            :class="{ 'is-active': remainingBucket === pick(rb, 'levelKey', 'LevelKey') }"
            @click="openRemainingBucket(pick(rb, 'levelKey', 'LevelKey'))"
          >
            <span>{{ pick(rb, 'label', 'Label') }}</span>
            <strong>{{ fmt(pick(rb, 'customerCount', 'CustomerCount')) }}</strong>
          </button>
        </div>

        <div
          v-if="panel"
          class="dm-table"
        >
          <div class="dm-table__head">
            <strong>{{ panelTitle }}</strong>
            <VProgressCircular
              v-if="remainingBusy || listBusy"
              indeterminate
              size="22"
              width="2"
              color="primary"
            />
          </div>

          <!-- List summary KPIs (like مندوبين) -->
          <div
            v-if="panel === 'list' && listSummary"
            class="dm-list-summary"
          >
            <div>
              <span>التقييم</span>
              <b>{{ fmtRating(pick(listSummary, 'phantomRating', 'PhantomRating')) }}</b>
            </div>
            <div>
              <span>غير المصفرين</span>
              <b>{{ fmt(pick(listSummary, 'openBalanceCount', 'OpenBalanceCount', 'ratedCustomerCount', 'RatedCustomerCount')) }}</b>
            </div>
            <div>
              <span>واردات</span>
              <b>{{ fmt(pick(listSummary, 'collectedToday', 'CollectedToday', 'collectionSum', 'CollectionSum')) }}</b>
            </div>
          </div>

          <!-- Bucket / non-zeroed customers -->
          <template v-if="panel === 'bucket' || panel === 'nonZeroed'">
            <button
              v-for="row in remainingRows"
              :key="pick(row, 'customerId', 'CustomerId')"
              type="button"
              class="dm-row"
              @click="openProfile(row)"
            >
              <div>
                <strong>{{ pick(row, 'customerName', 'CustomerName') || 'زبون' }}</strong>
                <div class="text-caption">
                  {{ pick(row, 'listName', 'ListName') || '' }}
                  · {{ pick(row, 'ratingLabel', 'RatingLabel') || '—' }}
                  · {{ pick(row, 'phone', 'Phone', 'phoneNumber', 'PhoneNumber') || '' }}
                </div>
              </div>
              <b>{{ fmt(pick(row, 'amountRemaining', 'AmountRemaining')) }}</b>
            </button>
            <VAlert
              v-if="!remainingBusy && !remainingRows.length"
              type="info"
              variant="tonal"
              class="ma-2"
            >
              لا زبائن في هذه الشريحة
            </VAlert>
          </template>

          <!-- Legal -->
          <template v-else-if="panel === 'legal'">
            <button
              v-for="(row, i) in legalRows"
              :key="pick(row, 'id', 'Id') || i"
              type="button"
              class="dm-row"
              @click="openProfile(row)"
            >
              <div>
                <strong>{{ pick(row, 'customerName', 'CustomerName') || 'زبون' }}</strong>
                <div class="text-caption">
                  {{ pick(row, 'statusLabel', 'StatusLabel') || '—' }}
                  · {{ pick(row, 'phone', 'Phone') || pick(row, 'address', 'Address') || '' }}
                </div>
              </div>
              <VChip
                size="small"
                :color="(row.isDone || row.IsDone) ? 'success' : 'warning'"
                variant="tonal"
              >
                {{ (row.isDone || row.IsDone) ? 'مكتمل' : 'قيد العمل' }}
              </VChip>
            </button>
            <VAlert
              v-if="!legalRows.length"
              type="info"
              variant="tonal"
              class="ma-2"
            >
              لا قضايا قانونية
            </VAlert>
          </template>

          <!-- Imports -->
          <template v-else-if="panel === 'imports'">
            <button
              v-for="(row, i) in (collection.payments || [])"
              :key="`${pick(row, 'customerId', 'CustomerId')}-${i}`"
              type="button"
              class="dm-row"
              @click="openProfile(row)"
            >
              <div>
                <strong>{{ pick(row, 'customerName', 'CustomerName') || 'زبون' }}</strong>
                <div class="text-caption">
                  {{ pick(row, 'listName', 'ListName') || '' }}
                  · {{ formatIraqDate(pick(row, 'paymentDate', 'PaymentDate')) || '—' }}
                </div>
              </div>
              <b>{{ fmt(pick(row, 'amount', 'Amount')) }}</b>
            </button>
            <VAlert
              v-if="!(collection.payments || []).length"
              type="info"
              variant="tonal"
              class="ma-2"
            >
              لا واردات تحصيل
            </VAlert>
          </template>

          <!-- List customers -->
          <template v-else-if="panel === 'list'">
            <button
              v-for="row in listCustomers"
              :key="pick(row, 'customerID', 'CustomerID', 'customerId', 'CustomerId')"
              type="button"
              class="dm-row dm-row--rich"
              @click="openProfile(row)"
            >
              <div class="dm-row__main">
                <strong>{{ pick(row, 'customerName', 'CustomerName') || 'زبون' }}</strong>
                <div class="text-caption">
                  {{ pick(row, 'phoneNumber', 'PhoneNumber', 'phone', 'Phone') || '—' }}
                  · {{ pick(row, 'phantomRatingLabel', 'PhantomRatingLabel', 'realRatingLabel', 'RealRatingLabel') || '—' }}
                  <template v-if="row.isLegal || row.IsLegal"> · قانونية</template>
                </div>
                <div class="dm-row__meta">
                  <span>البيع {{ formatIraqDate(pick(row, 'dateSaleDevice', 'DateSaleDevice')) || '—' }}</span>
                  <span>آخر تسديد {{ formatIraqDate(pick(row, 'lastPaymentDate', 'LastPaymentDate')) || '—' }}</span>
                  <span>تسديدات {{ pick(row, 'countReceiptDevice', 'CountReceiptDevice') ?? '—' }}</span>
                  <span>أيام {{ pick(row, 'numberOfDayDevice', 'NumberOfDayDevice') ?? '—' }}</span>
                </div>
              </div>
              <div class="dm-row__amounts">
                <div><span>البيع</span><b>{{ fmt(pick(row, 'amountTotalSales', 'AmountTotalSales')) }}</b></div>
                <div><span>الواصل</span><b>{{ fmt(pick(row, 'receiptsTotal', 'ReceiptsTotal')) }}</b></div>
                <div><span>الباقي</span><b>{{ fmt(pick(row, 'amountRemaining', 'AmountRemaining')) }}</b></div>
              </div>
            </button>
            <VAlert
              v-if="!listBusy && !listCustomers.length"
              type="info"
              variant="tonal"
              class="ma-2"
            >
              لا زبائن في هذه القائمة
            </VAlert>
          </template>
        </div>
      </template>
    </section>
  </div>
</template>

<style scoped>
.dm-board {
  --ink: #0c2f30;
  --muted: #5a7273;
  --line: rgba(13, 115, 119, 0.14);
  --teal: #0d7377;
  --teal-deep: #095c5f;
  --teal-soft: rgba(13, 115, 119, 0.1);
  --card: #ffffff;
  --bg: #f2f8f8;
  color: var(--ink);
}

.dm-board__hero {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-end;
  margin-block-end: 1.25rem;
  padding: 1.2rem 1.35rem;
  border-radius: 18px;
  border: 1px solid var(--line);
  background:
    radial-gradient(90% 120% at 100% 0%, rgba(13, 115, 119, 0.18), transparent 55%),
    linear-gradient(145deg, #f7fcfc, #e8f4f4);
  animation: rise 0.4s ease both;
}

.dm-board__eyebrow {
  margin: 0;
  color: var(--teal);
  font-size: 0.75rem;
  font-weight: 800;
  letter-spacing: 0.04em;
}

.dm-board__title {
  margin: 0.2rem 0 0.35rem;
  font-size: 1.55rem;
  font-weight: 800;
}

.dm-board__sub {
  margin: 0;
  color: var(--muted);
  font-size: 0.9rem;
}

.dm-board__filters {
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
  align-items: center;
}

.dm-board__select {
  min-inline-size: min(210px, 100%);
  background: #fff;
  border-radius: 12px;
}

.dm-board__refresh { font-weight: 700; }

.dm-board__loading {
  display: grid;
  place-items: center;
  min-block-size: 160px;
}

.dm-fgrid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 0.9rem;
  margin-block-end: 1.25rem;
}

.dm-fcard {
  text-align: start;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: var(--card);
  padding: 1rem 1.05rem;
  cursor: pointer;
  color: inherit;
  font: inherit;
  transition: transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease;
  animation: rise 0.45s ease both;
  animation-delay: calc(0.04s * var(--i, 0));
}

.dm-fcard:hover,
.dm-fcard:focus-visible {
  transform: translateY(-3px);
  box-shadow: 0 14px 28px rgba(9, 92, 95, 0.12);
  outline: none;
  border-color: rgba(13, 115, 119, 0.35);
}

.dm-fcard--active {
  border-color: var(--teal);
  background: linear-gradient(180deg, #fff, #f0fafa);
  box-shadow: 0 10px 22px rgba(9, 92, 95, 0.1);
}

.dm-fcard__name {
  font-weight: 800;
  font-size: 1.05rem;
  margin-block-end: 0.65rem;
}

.dm-fcard__rating span,
.dm-fcard__metrics span {
  display: block;
  font-size: 0.72rem;
  color: var(--muted);
  margin-block-end: 0.15rem;
}

.dm-fcard__rating strong {
  display: flex;
  align-items: baseline;
  gap: 0.35rem;
  font-size: 1.45rem;
  font-weight: 800;
}

.dm-fcard__rating small {
  font-size: 0.8rem;
  font-weight: 700;
  color: var(--teal-deep);
}

.dm-fcard__metrics {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.65rem;
  margin-block-start: 0.85rem;
  padding-block-start: 0.75rem;
  border-block-start: 1px solid var(--line);
}

.dm-fcard__metrics b {
  font-size: 1.05rem;
  font-weight: 800;
}

.dm-fcard__metrics small {
  display: block;
  color: var(--muted);
  font-size: 0.7rem;
}

.dm-detail {
  border: 1px solid var(--line);
  border-radius: 18px;
  background: var(--card);
  padding: 1.1rem 1.2rem 1.35rem;
  animation: rise 0.35s ease both;
}

.dm-detail__head {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
  margin-block-end: 1rem;
}

.dm-detail__head h2 {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 800;
}

.dm-detail__head p {
  margin: 0.25rem 0 0;
  color: var(--muted);
  font-size: 0.88rem;
}

.dm-kpi {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 0.7rem;
  margin-block-end: 1.1rem;
}

.dm-kpi__card {
  border: 1px solid var(--line);
  border-radius: 14px;
  padding: 0.85rem 0.95rem;
  background: #fbfefe;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.dm-kpi__card span {
  font-size: 0.75rem;
  color: var(--muted);
  font-weight: 700;
}

.dm-kpi__card strong {
  font-size: 1.35rem;
  font-weight: 800;
}

.dm-kpi__card small {
  color: var(--muted);
  font-size: 0.75rem;
}

.dm-kpi__card--accent { background: var(--teal-soft); border-color: rgba(13, 115, 119, 0.28); }
.dm-kpi__card--legal { background: rgba(220, 38, 38, 0.06); border-color: rgba(220, 38, 38, 0.2); }
.dm-kpi__card--btn {
  cursor: pointer;
  font: inherit;
  color: inherit;
  text-align: start;
  transition: transform 0.18s ease, border-color 0.18s ease;
}
.dm-kpi__card--btn:hover { transform: translateY(-2px); border-color: var(--teal); }
.dm-kpi__card--btn.is-active { border-color: var(--teal); background: var(--teal-soft); }

.dm-sec-title {
  margin: 0.4rem 0 0.65rem;
  font-size: 1rem;
  font-weight: 800;
  color: var(--teal-deep);
}

.dm-lists,
.dm-buckets {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(170px, 1fr));
  gap: 0.7rem;
  margin-block-end: 1rem;
}

.dm-list-card,
.dm-bucket {
  border: 1px solid var(--line);
  border-radius: 14px;
  padding: 0.85rem;
  background: #fff;
}

.dm-list-card--btn {
  cursor: pointer;
  font: inherit;
  color: inherit;
  text-align: start;
  transition: transform 0.18s ease, border-color 0.18s ease, background 0.18s ease;
}
.dm-list-card--btn:hover { transform: translateY(-2px); border-color: var(--teal); }
.dm-list-card--btn.is-active { border-color: var(--teal); background: var(--teal-soft); }

.dm-list-card__row {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
  margin-block-start: 0.45rem;
  font-size: 0.86rem;
}

.dm-list-card__row span { color: var(--muted); }

.dm-bucket {
  cursor: pointer;
  font: inherit;
  color: inherit;
  text-align: start;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  transition: transform 0.18s ease, border-color 0.18s ease;
}

.dm-bucket:hover { transform: translateY(-2px); border-color: var(--teal); }
.dm-bucket.is-active { border-color: var(--teal); background: var(--teal-soft); }
.dm-bucket span { font-size: 0.8rem; font-weight: 700; color: var(--muted); }
.dm-bucket strong { font-size: 1.35rem; font-weight: 800; }

.dm-table {
  border: 1px solid var(--line);
  border-radius: 14px;
  overflow: hidden;
}

.dm-table__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: var(--teal-soft);
}

.dm-list-summary {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 0.5rem;
  padding: 0.85rem 1rem;
  border-block-end: 1px solid var(--line);
  background: #fbfefe;
}

.dm-list-summary span {
  display: block;
  font-size: 0.72rem;
  color: var(--muted);
}

.dm-list-summary b {
  font-size: 1.1rem;
  font-weight: 800;
}

.dm-row {
  width: 100%;
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.75rem 1rem;
  border: 0;
  border-block-start: 1px solid var(--line);
  background: #fff;
  cursor: pointer;
  font: inherit;
  color: inherit;
  text-align: start;
  transition: background 0.15s ease;
}

.dm-row:hover { background: #f3fafa; }

.dm-row--rich {
  align-items: flex-start;
  flex-wrap: wrap;
}

.dm-row__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
  margin-block-start: 0.35rem;
  font-size: 0.72rem;
  color: var(--muted);
}

.dm-row__amounts {
  display: grid;
  grid-template-columns: repeat(3, minmax(70px, 1fr));
  gap: 0.45rem;
  text-align: end;
}

.dm-row__amounts span {
  display: block;
  font-size: 0.68rem;
  color: var(--muted);
}

.dm-row__amounts b {
  font-size: 0.92rem;
  font-weight: 800;
}

.is-up { color: #0f766e; }
.is-down { color: #dc2626; }
.is-flat { color: #64748b; }

@keyframes rise {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

@media (prefers-reduced-motion: reduce) {
  .dm-board__hero,
  .dm-fcard,
  .dm-detail { animation: none !important; }
  .dm-fcard:hover,
  .dm-kpi__card--btn:hover,
  .dm-list-card--btn:hover,
  .dm-bucket:hover { transform: none; }
}

@media (max-width: 720px) {
  .dm-board__select { min-inline-size: 100%; flex: 1 1 100%; }
  .dm-board__refresh { inline-size: 100%; }
  .dm-row__amounts { grid-template-columns: 1fr 1fr 1fr; width: 100%; text-align: start; }
}
</style>
