<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import {
  employeeApiPath,
  locationStatusLabel,
  smGet,
  smGetEmployees,
} from '@/composables/salesManagerApi'
import { createRequestGeneration } from '@/utils/provinceCatalog'

const router = useRouter()
const rows = ref([])
const cityValue = ref('')
const shiftStatus = ref('')
const loading = ref(false)
const loadError = ref('')
const requestGen = createRequestGeneration()

const dialog = ref(false)
const trendBusy = ref(false)
const trendError = ref('')
const trend = ref(null)
const selectedPoint = ref(null)
const selectedEmployee = ref(null)

const bucketDialog = ref(false)
const bucketBusy = ref(false)
const bucketError = ref('')
const bucketCustomers = ref([])
const activeBucket = ref(null)

const BUCKETS = [
  { key: 'excellent', label: 'ممتاز', countKeys: ['excellentCount', 'ExcellentCount'] },
  { key: 'good', label: 'جيد', countKeys: ['goodCount', 'GoodCount'] },
  { key: 'weak', label: 'ضعيف', countKeys: ['weakCount', 'WeakCount'] },
  { key: 'rejected', label: 'متلكئ', countKeys: ['rejectedCount', 'RejectedCount'] },
  { key: 'bad', label: 'سيء', countKeys: ['badCount', 'BadCount'] },
]

async function load() {
  const token = requestGen.next()
  loading.value = true
  loadError.value = ''
  rows.value = []

  try {
    const q = []
    if (shiftStatus.value)
      q.push(`shiftStatus=${encodeURIComponent(shiftStatus.value)}`)
    const data = await smGetEmployees(cityValue.value, q.join('&'))
    if (!requestGen.isCurrent(token))
      return
    rows.value = data
  }
  catch (err) {
    if (!requestGen.isCurrent(token))
      return
    rows.value = []
    loadError.value = err?.response?.data?.message
      || err?.message
      || 'تعذر تحميل الموظفين'
  }
  finally {
    if (requestGen.isCurrent(token))
      loading.value = false
  }
}

function ratingText(row) {
  const v = row.latestRating ?? row.LatestRating
  if (v == null || Number.isNaN(Number(v)))
    return '—'

  return Number(v).toFixed(2)
}

function phantomRatingText(row) {
  const v = row.phantomLatestRating ?? row.PhantomLatestRating
  if (v == null || Number.isNaN(Number(v)))
    return '—'

  return Number(v).toFixed(2)
}

function bucketCount(bucket) {
  if (!trend.value)
    return 0
  for (const k of bucket.countKeys) {
    const v = trend.value[k]
    if (v != null)
      return Number(v) || 0
  }

  return 0
}

const points = computed(() => {
  const raw = trend.value?.points || trend.value?.Points || []

  return Array.isArray(raw) ? raw : []
})

const chartGeom = computed(() => {
  const pts = points.value
  if (pts.length < 1)
    return null
  const vals = pts.map(p => Number(p.value ?? p.Value ?? 0))
  const dates = pts.map(p => String(p.date ?? p.Date ?? ''))
  const min = Math.min(...vals)
  const max = Math.max(...vals)
  const span = max - min || 1
  const w = 720
  const h = 180
  const padX = 10
  const padY = 14
  const n = vals.length
  const coords = vals.map((v, i) => {
    const x = n === 1 ? w / 2 : padX + (i / (n - 1)) * (w - padX * 2)
    const y = h - padY - ((v - min) / span) * (h - padY * 2)

    return { x, y, v, date: dates[i], index: i }
  })
  const path = coords.map((c, i) => `${i === 0 ? 'M' : 'L'}${c.x.toFixed(1)},${c.y.toFixed(1)}`).join(' ')

  return {
    path,
    coords,
    w,
    h,
    first: dates[0] || '',
    last: dates[dates.length - 1] || '',
    count: n,
  }
})

function formatDay(isoDate) {
  if (!isoDate)
    return '—'
  const s = String(isoDate).slice(0, 10)
  if (s.length < 10)
    return isoDate || '—'
  const [, m, d] = s.split('-')

  return `${Number(d)}/${Number(m)}`
}

function formatMoney(v) {
  const n = Number(v)
  if (!Number.isFinite(n))
    return '—'

  return `${n.toLocaleString('en-US')} د.ع`
}

function formatLastUpdate(row) {
  const raw = row.lastLocationAt || row.LastLocationAt || row.capturedAt || row.CapturedAt
  if (!raw)
    return '—'
  try {
    const d = new Date(raw)
    if (Number.isNaN(d.getTime()))
      return '—'

    return d.toLocaleString('ar-IQ', { hour12: false })
  }
  catch {
    return '—'
  }
}

function pickPoint(c) {
  selectedPoint.value = { date: c.date, value: c.v, label: formatDay(c.date) }
}

async function openRating(row) {
  const id = Number(row.employeeId ?? row.EmployeeId ?? 0)
  const city = String(row.cityValue || row.CityValue || cityValue.value || '').trim()
  if (!(id > 0))
    return
  selectedEmployee.value = row
  selectedPoint.value = null
  trend.value = null
  trendError.value = ''
  activeBucket.value = null
  bucketDialog.value = false
  dialog.value = true
  trendBusy.value = true
  try {
    const path = employeeApiPath(city, id, '/rating-trend')
    trend.value = await smGet(path)
  }
  catch (err) {
    trendError.value = err?.response?.data?.message || err?.message || 'تعذر تحميل تقييم الموظف'
  }
  finally {
    trendBusy.value = false
  }
}

async function openBucket(bucket) {
  const row = selectedEmployee.value
  if (!row)
    return
  const id = Number(row.employeeId ?? row.EmployeeId ?? 0)
  const city = String(row.cityValue || row.CityValue || cityValue.value || '').trim()
  if (!(id > 0))
    return

  activeBucket.value = bucket
  bucketCustomers.value = []
  bucketError.value = ''
  bucketDialog.value = true
  bucketBusy.value = true
  try {
    const path = employeeApiPath(city, id, `/rating-customers?level=${encodeURIComponent(bucket.key)}`)
    const data = await smGet(path)
    const list = Array.isArray(data) ? data : (Array.isArray(data?.data) ? data.data : [])
    bucketCustomers.value = list.map(c => ({
      customerId: c.customerId ?? c.CustomerId,
      customerName: c.customerName || c.CustomerName || `زبون ${c.customerId ?? c.CustomerId}`,
      phone: c.phone || c.Phone || '',
      dateSale: c.dateSale ?? c.DateSale,
      lastPaymentDate: c.lastPaymentDate ?? c.LastPaymentDate,
      amountPaid: c.amountPaid ?? c.AmountPaid ?? 0,
      amountRemaining: c.amountRemaining ?? c.AmountRemaining ?? 0,
      amountTotal: c.amountTotal ?? c.AmountTotal ?? 0,
      ratingLabel: c.ratingLabel || c.RatingLabel || bucket.label,
    }))
  }
  catch (err) {
    bucketError.value = err?.response?.data?.message || err?.message || 'تعذر تحميل زبائن الحالة'
  }
  finally {
    bucketBusy.value = false
  }
}

function openCustomerProfile(c) {
  const row = selectedEmployee.value
  const city = String(row?.cityValue || row?.CityValue || cityValue.value || '').trim()
  if (!city) {
    bucketError.value = 'تعذر تحديد المحافظة للزبون'

    return
  }
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

onMounted(load)
</script>

<template>
  <div>
    <h4 class="mb-4">
      موظفين المبيعات
    </h4>
    <VRow class="mb-3">
      <VCol
        cols="12"
        md="4"
      >
        <SalesBranchFilter
          v-model="cityValue"
          @change="load"
        />
      </VCol>
      <VCol
        cols="12"
        md="4"
      >
        <VSelect
          v-model="shiftStatus"
          :items="[{ title: 'الكل', value: '' }, { title: 'Active', value: 'Active' }, { title: 'NoShift', value: 'NoShift' }]"
          item-title="title"
          item-value="value"
          label="حالة الدوام"
          hide-details
          @update:model-value="load"
        />
      </VCol>
    </VRow>
    <div
      v-if="loading"
      class="text-medium-emphasis mb-3"
    >
      جاري التحميل...
    </div>
    <VAlert
      v-if="loadError"
      type="error"
      variant="tonal"
      class="mb-3"
    >
      {{ loadError }}
    </VAlert>
    <VTable>
      <thead>
        <tr>
          <th>الاسم</th>
          <th>المحافظة</th>
          <th>الدوام</th>
          <th>الموقع</th>
          <th>آخر تحديث</th>
          <th>التقييم الحقيقي</th>
          <th>التقييم الوهمي</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in rows"
          :key="`${row.employeeId}-${row.cityValue || ''}`"
        >
          <td>{{ row.employeeName || row.EmployeeName }}</td>
          <td>{{ row.cityName || row.CityName || '—' }}</td>
          <td>{{ row.shiftStatus || row.ShiftStatus || '—' }}</td>
          <td>{{ locationStatusLabel[row.locationStatus || row.LocationStatus] || row.locationStatus || '—' }}</td>
          <td>{{ formatLastUpdate(row) }}</td>
          <td>
            <button
              type="button"
              class="rating-link"
              @click="openRating(row)"
            >
              {{ ratingText(row) }}
            </button>
          </td>
          <td>
            <button
              type="button"
              class="rating-link"
              @click="openRating(row)"
            >
              {{ phantomRatingText(row) }}
            </button>
          </td>
        </tr>
      </tbody>
    </VTable>

    <VDialog
      v-model="dialog"
      max-width="920"
      scrollable
    >
      <VCard>
        <VCardTitle class="d-flex align-center justify-space-between">
          <span>
            تقييم {{ selectedEmployee?.employeeName || selectedEmployee?.EmployeeName || 'الموظف' }}
          </span>
          <VBtn
            icon="tabler-x"
            variant="text"
            @click="dialog = false"
          />
        </VCardTitle>
        <VCardText>
          <div
            v-if="trendBusy"
            class="text-medium-emphasis"
          >
            جاري تحميل نقاط التقييم من أول بيع…
          </div>
          <VAlert
            v-else-if="trendError"
            type="error"
            variant="tonal"
          >
            {{ trendError }}
          </VAlert>
          <template v-else-if="trend">
            <div class="trend-meta mb-3">
              <div>
                <small class="text-medium-emphasis">التقييم الحقيقي</small>
                <strong class="d-block text-h5">
                  {{ Number(trend.endValue ?? trend.EndValue ?? 0).toFixed(2) }}
                </strong>
                <span class="text-caption">{{ trend.ratingLabel || trend.RatingLabel || '' }}</span>
              </div>
              <div v-if="(trend.phantomEndValue ?? trend.PhantomEndValue) != null">
                <small class="text-medium-emphasis">التقييم الوهمي</small>
                <strong class="d-block text-h5">
                  {{ Number(trend.phantomEndValue ?? trend.PhantomEndValue ?? 0).toFixed(2) }}
                </strong>
                <span class="text-caption">{{ trend.phantomRatingLabel || trend.PhantomRatingLabel || '' }}</span>
              </div>
              <div>
                <small class="text-medium-emphasis">الزبائن (كل المبيعات المكتملة)</small>
                <strong class="d-block">
                  {{ trend.customerCount ?? trend.CustomerCount ?? 0 }}
                </strong>
              </div>
              <div>
                <small class="text-medium-emphasis">نقاط يومية</small>
                <strong class="d-block">{{ chartGeom?.count || 0 }}</strong>
              </div>
              <div>
                <small class="text-medium-emphasis">الفترة</small>
                <strong class="d-block">
                  {{ formatDay(String(trend.fromDate || trend.FromDate || '').slice(0, 10)) }}
                  →
                  {{ formatDay(String(trend.toDate || trend.ToDate || '').slice(0, 10)) }}
                </strong>
              </div>
            </div>

            <div
              v-if="selectedPoint"
              class="point-pop mb-2"
            >
              <strong>{{ selectedPoint.label }}</strong>
              <span>التقييم: {{ Number(selectedPoint.value).toFixed(2) }}</span>
            </div>

            <div
              v-if="chartGeom"
              class="spark"
            >
              <svg
                :viewBox="`0 0 ${chartGeom.w} ${chartGeom.h}`"
                preserveAspectRatio="none"
                class="trend-svg"
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
                  @click.stop="pickPoint(c)"
                >
                  <circle
                    :cx="c.x"
                    :cy="c.y"
                    r="10"
                    class="hit"
                  />
                  <circle
                    :cx="c.x"
                    :cy="c.y"
                    :r="selectedPoint && selectedPoint.date === c.date ? 5 : 3"
                    class="dot"
                  />
                </g>
              </svg>
              <div class="axis-row">
                <span>{{ formatDay(chartGeom.first) }}</span>
                <span>{{ formatDay(chartGeom.last) }}</span>
              </div>
            </div>
            <p
              v-else
              class="text-medium-emphasis"
            >
              لا نقاط تقييم لهذا الموظف.
            </p>

            <div class="buckets mt-4">
              <button
                v-for="b in BUCKETS"
                :key="b.key"
                type="button"
                class="bucket-card"
                :class="{ active: activeBucket?.key === b.key }"
                @click="openBucket(b)"
              >
                <span>{{ b.label }}</span>
                <strong>{{ bucketCount(b) }}</strong>
              </button>
            </div>
          </template>
        </VCardText>
      </VCard>
    </VDialog>

    <VDialog
      v-model="bucketDialog"
      max-width="860"
      scrollable
    >
      <VCard>
        <VCardTitle class="d-flex align-center justify-space-between">
          <span>
            زبائن {{ activeBucket?.label || '' }}
            —
            {{ selectedEmployee?.employeeName || selectedEmployee?.EmployeeName || '' }}
          </span>
          <VBtn
            icon="tabler-x"
            variant="text"
            @click="bucketDialog = false"
          />
        </VCardTitle>
        <VCardText>
          <div
            v-if="bucketBusy"
            class="text-medium-emphasis"
          >
            جاري تحميل الزبائن…
          </div>
          <VAlert
            v-else-if="bucketError"
            type="error"
            variant="tonal"
          >
            {{ bucketError }}
          </VAlert>
          <p
            v-else-if="bucketCustomers.length === 0"
            class="text-medium-emphasis"
          >
            لا زبائن في هذه الحالة.
          </p>
          <div
            v-else
            class="customer-grid"
          >
            <button
              v-for="c in bucketCustomers"
              :key="c.customerId"
              type="button"
              class="customer-card"
              @click="openCustomerProfile(c)"
            >
              <strong class="customer-name">{{ c.customerName }}</strong>
              <div class="customer-meta">
                <span>تاريخ البيع: {{ formatDay(c.dateSale) }}</span>
                <span>آخر تسديد: {{ formatDay(c.lastPaymentDate) }}</span>
                <span>واصل: {{ formatMoney(c.amountPaid) }}</span>
                <span>متبقي: {{ formatMoney(c.amountRemaining) }}</span>
              </div>
            </button>
          </div>
        </VCardText>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.rating-link {
  border: 0;
  background: transparent;
  color: #1f6f5b;
  font-weight: 800;
  font-size: 1.05rem;
  cursor: pointer;
  text-decoration: underline;
  text-underline-offset: 3px;
}

.trend-meta {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.75rem;
}

.spark {
  color: #1f6f5b;
}

.trend-svg {
  width: 100%;
  height: 11rem;
  cursor: crosshair;
}

.hit {
  fill: transparent;
  cursor: pointer;
}

.dot {
  fill: currentColor;
  pointer-events: none;
}

.axis-row {
  display: flex;
  justify-content: space-between;
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-size: 0.8rem;
}

.point-pop {
  display: inline-flex;
  gap: 0.75rem;
  padding: 0.4rem 0.7rem;
  border-radius: 0.6rem;
  background: #0f3d32;
  color: #f4faf7;
}

.buckets {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 0.5rem;
}

.bucket-card {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 0.65rem;
  padding: 0.65rem;
  background: transparent;
  text-align: start;
  cursor: pointer;
  transition: border-color 0.15s ease, background 0.15s ease;
}

.bucket-card:hover,
.bucket-card.active {
  border-color: #1f6f5b;
  background: rgba(31, 111, 91, 0.08);
}

.bucket-card span {
  display: block;
  font-size: 0.8rem;
  opacity: 0.7;
}

.bucket-card strong {
  display: block;
  font-size: 1.15rem;
  margin-top: 0.15rem;
}

.customer-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
}

.customer-card {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  border-radius: 0.75rem;
  padding: 0.85rem 1rem;
  background: transparent;
  text-align: start;
  cursor: pointer;
  transition: border-color 0.15s ease, background 0.15s ease;
}

.customer-card:hover {
  border-color: #1f6f5b;
  background: rgba(31, 111, 91, 0.06);
}

.customer-name {
  display: block;
  color: #1f6f5b;
  font-size: 1.05rem;
  margin-bottom: 0.55rem;
  text-decoration: underline;
  text-underline-offset: 3px;
}

.customer-meta {
  display: grid;
  gap: 0.25rem;
  font-size: 0.9rem;
  opacity: 0.9;
}

@media (max-width: 700px) {
  .trend-meta,
  .buckets,
  .customer-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}
</style>
