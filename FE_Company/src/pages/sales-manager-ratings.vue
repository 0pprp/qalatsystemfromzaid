<script setup>
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import {
  smStatsCatalog,
  smStatsRatingCustomers,
  smStatsTrend,
} from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()
const router = useRouter()

const cityValue = ref('')
const selected = computed(() => {
  const v = String(cityValue.value || '').trim()

  return v ? { value: v, name: v } : null
})
const scope = ref('branch')
const entityId = ref(0)
const catalog = ref({ lists: [], followers: [], employees: [] })
const trend = ref(null)
const catalogBusy = ref(false)
const trendBusy = ref(false)
const catalogCache = new Map()

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

const phantomScoreText = computed(() => {
  const t = trend.value
  if (!t)
    return null
  const end = t.phantomEndValue ?? t.PhantomEndValue
  if (end == null || Number.isNaN(Number(end)))
    return null

  return Number(end).toFixed(2)
})

const phantomRatingLabel = computed(() =>
  trend.value?.phantomRatingLabel || trend.value?.PhantomRatingLabel || '')

const phantomMetricLabel = computed(() =>
  trend.value?.phantomMetricLabel || trend.value?.PhantomMetricLabel || 'التقييم الوهمي')

const direction = computed(() =>
  String(trend.value?.direction || trend.value?.Direction || 'flat').toLowerCase())

const metricLabel = computed(() =>
  trend.value?.metricLabel
  || trend.value?.MetricLabel
  || 'التقييم الحقيقي')

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

async function onCityChange() {
  scope.value = 'branch'
  entityId.value = 0
  trend.value = null
  const city = String(cityValue.value || '').trim()
  if (!city)
    return
  await loadCatalog(city)
  await loadTrend()
}

async function loadCatalog(cityValueArg) {
  if (catalogCache.has(cityValueArg)) {
    catalog.value = catalogCache.get(cityValueArg)

    return
  }
  catalogBusy.value = true
  try {
    const raw = await smStatsCatalog(cityValueArg)
    const mapped = {
      lists: normalizeEntities(raw?.lists || raw?.Lists),
      followers: normalizeEntities(raw?.followers || raw?.Followers),
      employees: normalizeEntities(raw?.employees || raw?.Employees),
    }
    catalogCache.set(cityValueArg, mapped)
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

async function loadTrend() {
  if (!selected.value)
    return
  if (needsEntity.value && !(entityId.value > 0)) {
    trend.value = null

    return
  }
  trendBusy.value = true
  selectedPoint.value = null
  try {
    trend.value = await smStatsTrend({
      cityValue: selected.value.value,
      scope: scope.value,
      id: scope.value === 'branch' ? 0 : entityId.value,
      fromDate: fromDate.value,
      toDate: toDate.value,
    })
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر تحميل التقييم')
    trend.value = null
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
    const data = await smStatsRatingCustomers({
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
    const data = await smStatsRatingCustomers({
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

const RETURN_KEY = 'sales-manager-ratings-return-v1'

function saveDirectorReturnState() {
  try {
    sessionStorage.setItem(RETURN_KEY, JSON.stringify({
      city: selected.value?.value || '',
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
  let st = null
  try {
    st = JSON.parse(sessionStorage.getItem(RETURN_KEY) || 'null')
    sessionStorage.removeItem(RETURN_KEY)
  }
  catch {
    st = null
  }
  if (!st?.city)
    return
  cityValue.value = st.city
  if (st.scope)
    scope.value = st.scope
  if (st.entityId != null)
    entityId.value = Number(st.entityId) || 0
  if (st.fromDate)
    fromDate.value = st.fromDate
  if (st.toDate)
    toDate.value = st.toDate
  await onCityChange()
  if (st.bucketKey) {
    const bucket = ratingBuckets.find(b => b.key === st.bucketKey)
    if (bucket) {
      bucketSearch.value = st.bucketSearch || ''
      await openBucket(bucket)
      if (st.bucketSearch)
        await searchBucketServer()
    }
  }
  await nextTick()
  if (st.scrollY)
    window.scrollTo(0, Number(st.scrollY) || 0)
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

watch(scope, () => {
  entityId.value = 0
  if (scope.value === 'branch')
    loadTrend()
})

watch(entityId, () => {
  if (!needsEntity.value || entityId.value > 0)
    loadTrend()
})

watch(cityValue, async (v, prev) => {
  if (v && v !== prev)
    await onCityChange()
})

onMounted(async () => {
  await restoreDirectorReturnState()
})
</script>

<template>
  <div class="director-shell">
    <header class="director-hero">
      <div>
        <p class="eyebrow">
          إدارة المبيعات
        </p>
        <h1 class="province-title">
          التقييمات
        </h1>
        <p class="sub">
          الفترة = تاريخ البيع فقط — التقييم والتسديدات تُحسب حتى اليوم. الرسم يمتد من أول بيع في الفترة إلى اليوم.
        </p>
      </div>
    </header>

    <div class="mb-4">
      <SalesBranchFilter v-model="cityValue" />
    </div>

    <section
      v-if="!selected"
      class="detail"
    >
      <p class="muted">
        اختر المحافظة لعرض التقييم.
      </p>
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
          :loading="trendBusy"
          @click="loadTrend"
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
          :label="scope === 'list' ? 'المندوب / القائمة' : scope === 'follower' ? 'المتابع' : 'موظف المبيعات'"
          :loading="catalogBusy"
          density="comfortable"
          variant="outlined"
          hide-details
          clearable
          :disabled="!entityOptions.length"
        />
      </div>

      <div class="result-panel">
        <div class="score-block">
          <p class="metric">
            {{ metricLabel }}
          </p>
          <p class="score">
            {{ scoreText }}
          </p>
          <p
            class="label-pill"
            :data-dir="direction"
          >
            {{ ratingLabel }}
          </p>
          <p class="cust-count">
            كل الزبائن ضمن الفترة:
            <strong>{{ customerCount == null ? '—' : customerCount }}</strong>
          </p>
          <div
            v-if="phantomScoreText"
            class="phantom-box"
          >
            <p class="phantom-box__title">
              {{ phantomMetricLabel }}
            </p>
            <p class="phantom-box__row">
              <strong>{{ phantomScoreText }}</strong>
              <span class="muted">· {{ phantomRatingLabel || '—' }}</span>
            </p>
          </div>
          <p
            v-if="chartGeom"
            class="day-count"
          >
            {{ chartGeom.count }} نقطة يومية بالرسم — اضغط نقطة لعرض يومها
          </p>
        </div>
        <div class="spark">
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
        v-if="showBuckets && bucketCounts"
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
                <span v-if="c.amountRemaining != null">متبقي: {{ money(c.amountRemaining) }}</span>
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

.director-hero h1.province-title {
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
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 0.85rem;
}

.province-card {
  text-align: start;
  border: 1px solid rgba(20, 32, 27, 0.08);
  background: var(--card);
  border-radius: 1rem;
  padding: 1.15rem 1.2rem;
  cursor: pointer;
  transition: transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease;
  box-shadow: 0 1px 0 rgba(15, 61, 50, 0.04);
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
  font-weight: 700;
  margin-bottom: 0.45rem;
}

.province-cta {
  color: var(--accent);
  font-size: 0.9rem;
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

.phantom-box {
  margin-top: 0.75rem;
  padding: 0.65rem 0.75rem;
  border-radius: 10px;
  border: 1px dashed rgba(31, 111, 91, 0.35);
  background: rgba(31, 111, 91, 0.06);
}

.phantom-box__title {
  margin: 0 0 0.35rem;
  font-size: 0.75rem;
  color: var(--muted);
}

.phantom-box__row {
  margin: 0;
  font-size: 0.9rem;
}

.phantom-box__row strong {
  color: var(--accent-deep);
}

.phantom-box__row .muted {
  color: var(--muted);
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
