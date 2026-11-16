<script setup>
import {
  ArcElement,
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  Filler,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  Title,
  Tooltip,
} from 'chart.js'
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { Bar, Doughnut, Line } from 'vue-chartjs'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { requestStatusLabel, smGet, withCityQuery } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'
import { isCentralSalesManager } from '@/composables/useSalesBranches'
import { useUserRole } from '@/composables/useUserRole'
import {
  aggregateBranches,
  branchesFromPayload,
  isExcludedOverviewBranch,
  normalizeBranch,
  roleLabel,
} from '@/utils/salesOverview'

definePage({
  meta: {
    layout: 'default',
  },
})

ChartJS.register(Title, Tooltip, Legend, BarElement, LineElement, PointElement, ArcElement, CategoryScale, LinearScale, Filler)
ChartJS.defaults.font.family = 'Cairo, Tahoma, sans-serif'

const toast = useToast()
const { isDirector } = useUserRole()
const baghdad = new Date(Date.now() + 3 * 60 * 60 * 1000)
const year = ref(baghdad.getUTCFullYear())
const month = ref(baghdad.getUTCMonth() + 1)
const cityValue = ref('')
const dayOpen = ref(false)
const dayLoading = ref(false)
const dayError = ref('')
const dayDetail = ref(null)
const dayYear = ref(baghdad.getUTCFullYear())
const dayMonth = ref(baghdad.getUTCMonth() + 1)
const dayDay = ref(baghdad.getUTCDate())
const loading = ref(false)
const loadError = ref('')
const failedBranches = ref([])
const allBranches = ref([])

const monthItems = [
  { title: 'كانون الثاني', value: 1 },
  { title: 'شباط', value: 2 },
  { title: 'آذار', value: 3 },
  { title: 'نيسان', value: 4 },
  { title: 'أيار', value: 5 },
  { title: 'حزيران', value: 6 },
  { title: 'تموز', value: 7 },
  { title: 'آب', value: 8 },
  { title: 'أيلول', value: 9 },
  { title: 'تشرين الأول', value: 10 },
  { title: 'تشرين الثاني', value: 11 },
  { title: 'كانون الأول', value: 12 },
]

const yearItems = computed(() => {
  const y = baghdad.getUTCFullYear()

  return [y - 1, y, y + 1].map(value => ({ title: String(value), value }))
})

const canPickBranch = computed(() => isCentralSalesManager())

const filterItems = computed(() => [
  { title: 'الكل', value: '' },
  ...allBranches.value.map(branch => ({
    title: branch.cityName || branch.cityValue || 'فرع',
    value: String(branch.cityValue || ''),
  })),
])

const selectedBranches = computed(() => {
  if (!cityValue.value)
    return allBranches.value

  return allBranches.value.filter(branch => String(branch.cityValue) === String(cityValue.value))
})

const scope = computed(() => aggregateBranches(selectedBranches.value))

const overviewWarnings = computed(() =>
  [...new Set(allBranches.value.map(branch => branch.overviewError).filter(Boolean))],
)

const scopeTitle = computed(() => {
  if (!cityValue.value)
    return allBranches.value.length > 1 ? 'كل الفروع' : (allBranches.value[0]?.cityName || 'الفرع')

  return selectedBranches.value[0]?.cityName || 'الفرع'
})

const palette = ['#0f766e', '#d97706', '#2563eb', '#be123c', '#7c3aed', '#0891b2', '#65a30d', '#c2410c']

function fmt(value) {
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(Math.round(Number(value) || 0))
}

function fmtScore(value) {
  if (value == null || value === '')
    return '—'
  const n = Number(value)
  if (!Number.isFinite(n))
    return '—'

  return n.toFixed(1)
}

function fmtShare(value) {
  if (value == null || value === '')
    return '—'
  const n = Number(value)
  if (!Number.isFinite(n))
    return '—'

  return `${n.toFixed(1)}%`
}

function fmtPct(value) {
  if (value == null || value === '')
    return '—'
  const n = Number(value)
  if (!Number.isFinite(n))
    return '—'
  const sign = n > 0 ? '+' : ''

  return `${sign}${n.toFixed(1)}%`
}

function growthClass(value) {
  const n = Number(value)
  if (!Number.isFinite(n) || value == null)
    return 'ov-flat'
  if (n > 0)
    return 'ov-up'
  if (n < 0)
    return 'ov-down'

  return 'ov-flat'
}

function statusLabel(status) {
  return requestStatusLabel[status] || status || '—'
}

const lineData = computed(() => ({
  labels: scope.value.daily.map(row => String(row.day)),
  datasets: [
    {
      label: 'سعر البيع',
      data: scope.value.daily.map(row => row.salePrice),
      borderColor: '#0f766e',
      backgroundColor: 'rgba(15, 118, 110, 0.15)',
      fill: true,
      tension: 0.3,
      yAxisID: 'y',
    },
    {
      label: 'عدد المبيعات',
      data: scope.value.daily.map(row => row.salesCount),
      borderColor: '#d97706',
      backgroundColor: '#d97706',
      tension: 0.3,
      yAxisID: 'y1',
    },
  ],
}))

const lineOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { position: 'bottom' } },
  scales: {
    y: { beginAtZero: true, position: 'left' },
    y1: { beginAtZero: true, position: 'right', grid: { drawOnChartArea: false } },
  },
}

const branchBarData = computed(() => ({
  labels: allBranches.value.map(branch => branch.cityName || branch.cityValue),
  datasets: [{
    label: 'سعر البيع',
    data: allBranches.value.map(branch => branch.monthSalePrice),
    backgroundColor: '#0f766e',
    borderRadius: 6,
  }],
}))

const branchBarOptions = {
  indexAxis: 'y',
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { display: false } },
  scales: { x: { beginAtZero: true } },
}

const categoryData = computed(() => ({
  labels: scope.value.categories.map(row => row.category),
  datasets: [{
    data: scope.value.categories.map(row => row.quantity),
    backgroundColor: palette,
  }],
}))

const doughnutOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { position: 'bottom' } },
}

const orderData = computed(() => ({
  labels: scope.value.orderStatuses.map(row => statusLabel(row.status)),
  datasets: [{
    data: scope.value.orderStatuses.map(row => row.count),
    backgroundColor: palette,
  }],
}))

const scoreChartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { display: false } },
  scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
}

const paymentBands = [
  { label: 'ممتاز', score: 10, color: '#15803d' },
  { label: 'جيد', score: 5, color: '#2563eb' },
  { label: 'ضعيف', score: 0, color: '#d97706' },
  { label: 'متلكئ', score: -5, color: '#c2410c' },
  { label: 'سيء', score: -10, color: '#be123c' },
]

const scoreChartData = computed(() => {
  const rows = scope.value.saleScores || []

  const countFor = band => {
    const byLabel = rows.find(row => row.label === band.label)
    if (byLabel)
      return byLabel.count
    
    return rows.find(row => row.score === band.score)?.count || 0
  }

  return {
    labels: paymentBands.map(band => band.label),
    datasets: [{
      label: 'المبيعات',
      data: paymentBands.map(countFor),
      backgroundColor: paymentBands.map(band => band.color),
      borderRadius: 6,
    }],
  }
})

const itemBarData = computed(() => {
  const rows = scope.value.items.slice(0, 8)

  return {
    labels: rows.map(row => row.itemName),
    datasets: [{
      label: 'الكمية',
      data: rows.map(row => row.quantity),
      backgroundColor: '#d97706',
      borderRadius: 6,
    }],
  }
})

const branchChartHeight = computed(() => Math.max(220, allBranches.value.length * 42))
const itemChartHeight = computed(() => Math.max(220, Math.min(scope.value.items.length, 8) * 36))

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const raw = await smGet(`dashboard?year=${year.value}&month=${month.value}`, { timeoutMs: 40000 })

    const branches = (raw?.branches
      ? raw.branches.map(row => normalizeBranch(row))
      : branchesFromPayload(raw)
    ).filter(branch => !isExcludedOverviewBranch(branch))

    allBranches.value = branches
    failedBranches.value = (raw?.failedBranches || raw?.FailedBranches || [])
      .filter(name => !isExcludedOverviewBranch({ cityName: name }))
    if (cityValue.value && !branches.some(branch => String(branch.cityValue) === String(cityValue.value)))
      cityValue.value = ''
  }
  catch (err) {
    const backend = err?.response?.data
    const message = backend?.message || backend?.Message || err?.message || 'تعذر تحميل نظرة عامة'

    loadError.value = err?.response?.status ? `${message} (${err.response.status})` : message
    allBranches.value = []
    toast.error(loadError.value)
  }
  finally {
    loading.value = false
  }
}

function selectBranch(value) {
  cityValue.value = value ? String(value) : ''
}

const dayChoices = computed(() => {
  const max = new Date(dayYear.value, dayMonth.value, 0).getDate()

  return Array.from({ length: max }, (_, index) => ({ title: String(index + 1), value: index + 1 }))
})

const dayBranches = computed(() =>
  (dayDetail.value?.branches || dayDetail.value?.Branches || [])
    .filter(branch => !isExcludedOverviewBranch(branch)),
)

const dayTitle = computed(() => `${dayDay.value} ${monthItems[dayMonth.value - 1]?.title || ''} ${dayYear.value}`)

function statusBits(statuses) {
  return (statuses || [])
    .map(row => `${statusLabel(row.status || row.Status)} ${fmt(row.count || row.Count)}`)
    .join(' · ')
}

function openDay() {
  dayOpen.value = true
  loadDay()
  nextTick(() => document.getElementById('ov-day')?.scrollIntoView({ behavior: 'smooth', block: 'start' }))
}

async function loadDay() {
  const max = new Date(dayYear.value, dayMonth.value, 0).getDate()
  if (dayDay.value > max)
    dayDay.value = max

  dayLoading.value = true
  dayError.value = ''
  try {
    const path = withCityQuery(
      `dashboard/day?year=${dayYear.value}&month=${dayMonth.value}&day=${dayDay.value}`,
      cityValue.value,
    )

    dayDetail.value = await smGet(path, { timeoutMs: 40000 })

    const warning = dayDetail.value?.dayError || dayDetail.value?.DayError
    if (warning)
      dayError.value = warning
  }
  catch (err) {
    const backend = err?.response?.data
    const message = backend?.message || backend?.Message || err?.message || 'تعذر تحميل اليوم'

    dayError.value = err?.response?.status ? `${message} (${err.response.status})` : message
    dayDetail.value = null
  }
  finally {
    dayLoading.value = false
  }
}

watch([dayYear, dayMonth, dayDay, cityValue], () => {
  if (dayOpen.value)
    loadDay()
})

watch([year, month], () => {
  load()
})

onMounted(load)
</script>

<template>
  <div class="ov-page">
    <div class="ov-head">
      <div>
        <h4 class="mb-1">
          نظرة عامة — {{ isDirector ? 'المبيعات' : 'إدارة المبيعات' }}
        </h4>
        <p class="ov-sub">
          {{ scopeTitle }} · {{ monthItems[month - 1]?.title }} {{ year }}
          · المبيعات المحذوفة غير محسوبة
        </p>
      </div>
      <div class="ov-filters">
        <VSelect
          v-model="month"
          :items="monthItems"
          item-title="title"
          item-value="value"
          label="الشهر"
          density="comfortable"
          hide-details
          class="ov-select"
        />
        <VSelect
          v-model="year"
          :items="yearItems"
          item-title="title"
          item-value="value"
          label="السنة"
          density="comfortable"
          hide-details
          class="ov-select"
        />
        <SalesBranchFilter
          v-if="canPickBranch"
          v-model="cityValue"
          allow-all
          :items-override="filterItems"
        />
        <VBtn
          :loading="loading"
          prepend-icon="tabler-refresh"
          @click="load"
        >
          تحديث
        </VBtn>
      </div>
    </div>

    <VProgressLinear
      v-if="loading"
      indeterminate
      color="#0f766e"
      class="mb-4 rounded"
    />
    <VAlert
      v-if="loadError"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ loadError }}
    </VAlert>
    <VAlert
      v-if="!loadError && overviewWarnings.length"
      type="warning"
      variant="tonal"
      class="mb-4"
    >
      {{ overviewWarnings.join(' ') }}
    </VAlert>
    <VAlert
      v-if="!loadError && failedBranches.length"
      type="warning"
      variant="tonal"
      class="mb-4"
    >
      {{ failedBranches.length < 4 ? `تعذر تحميل: ${failedBranches.join('، ')}` : `تعذر تحميل ${failedBranches.length} فروع، والأرقام المعروضة للفروع التي استجابت.` }}
    </VAlert>

    <VRow>
      <VCol
        v-for="card in [
          { label: 'مبيعات الشهر', value: fmt(scope.monthSalesCount), hint: 'عدد المبيعات المكتملة' },
          { label: 'سعر البيع', value: fmt(scope.monthSalePrice), hint: 'إجمالي سعر البيع بالدينار' },
          { label: 'المحصّل', value: fmtShare(scope.collectedPercent), hint: `${fmt(scope.monthCollected)} من سعر البيع، منذ تاريخ البيع` },
          { label: 'متوسط البيع', value: fmt(scope.averageSalePrice), hint: 'سعر البيع ÷ العدد' },
          { label: 'المقدمة', value: fmt(scope.monthDownPayment), hint: 'مجموع الدفعات المقدمة' },
          { label: 'القطع المباعة', value: fmt(scope.monthItemQuantity), hint: 'هواتف ودراجات وبقية المواد' },
          { label: 'مبيعات اليوم', value: fmt(scope.salesToday), hint: 'اضغط لتفاصيل أي يوم', day: true },
        ]"
        :key="card.label"
        cols="12"
        sm="6"
        lg="4"
      >
        <VCard
          class="ov-card"
          :class="{ 'ov-card-link': card.day }"
          :role="card.day ? 'button' : undefined"
          :tabindex="card.day ? 0 : undefined"
          @click="card.day && openDay()"
          @keydown.enter.prevent="card.day && openDay()"
        >
          <VCardText>
            <div class="ov-label">
              {{ card.label }}
            </div>
            <div class="ov-value">
              {{ card.value }}
            </div>
            <div class="ov-hint">
              {{ card.hint }}
            </div>
          </VCardText>
        </VCard>
      </VCol>
      <VCol
        cols="12"
        sm="6"
        lg="4"
      >
        <VCard class="ov-card">
          <VCardText>
            <div class="ov-label">
              نمو العدد عن الشهر السابق
            </div>
            <div
              class="ov-value"
              :class="growthClass(scope.salesCountGrowthPercent)"
            >
              {{ fmtPct(scope.salesCountGrowthPercent) }}
            </div>
            <div class="ov-hint">
              السابق {{ fmt(scope.previousMonthSalesCount) }}
            </div>
          </VCardText>
        </VCard>
      </VCol>
      <VCol
        cols="12"
        sm="6"
        lg="4"
      >
        <VCard class="ov-card">
          <VCardText>
            <div class="ov-label">
              نمو سعر البيع عن الشهر السابق
            </div>
            <div
              class="ov-value"
              :class="growthClass(scope.salePriceGrowthPercent)"
            >
              {{ fmtPct(scope.salePriceGrowthPercent) }}
            </div>
            <div class="ov-hint">
              السابق {{ fmt(scope.previousMonthSalePrice) }}
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <VCard
      v-if="dayOpen"
      id="ov-day"
      class="mt-2"
    >
      <VCardText>
        <div class="ov-head">
          <div>
            <h5 class="mb-1">
              تفاصيل اليوم · {{ dayTitle }}
            </h5>
            <p class="ov-hint mb-0">
              مبيعات هذا التاريخ وسعرها، والطلبات التي أُضيفت فيه. المبيعات المحذوفة غير محسوبة.
            </p>
          </div>
          <VBtn
            class="ov-day-close"
            variant="text"
            @click="dayOpen = false"
          >
            إغلاق
          </VBtn>
        </div>
        <div class="ov-filters mb-2">
          <VSelect
            v-model="dayDay"
            :items="dayChoices"
            item-title="title"
            item-value="value"
            label="اليوم"
            density="comfortable"
            hide-details
            class="ov-select"
          />
          <VSelect
            v-model="dayMonth"
            :items="monthItems"
            item-title="title"
            item-value="value"
            label="الشهر"
            density="comfortable"
            hide-details
            class="ov-select"
          />
          <VSelect
            v-model="dayYear"
            :items="yearItems"
            item-title="title"
            item-value="value"
            label="السنة"
            density="comfortable"
            hide-details
            class="ov-select"
          />
        </div>
        <VProgressLinear
          v-if="dayLoading"
          indeterminate
          color="#0f766e"
          class="my-3 rounded"
        />
        <VAlert
          v-if="dayError"
          type="warning"
          variant="tonal"
          class="my-3"
        >
          {{ dayError }}
        </VAlert>
        <template v-if="dayDetail">
          <VRow class="mt-1">
            <VCol
              v-for="card in [
                { label: 'المبيعات', value: fmt(dayDetail.salesCount), hint: 'عدد المبيعات المكتملة' },
                { label: 'سعر البيع', value: fmt(dayDetail.salePrice), hint: 'إجمالي سعر البيع' },
                { label: 'متوسط البيع', value: fmt(dayDetail.averageSalePrice), hint: 'سعر البيع ÷ العدد' },
                { label: 'المقدمة', value: fmt(dayDetail.downPayment), hint: 'دفعات هذا التاريخ' },
                { label: 'القطع', value: fmt(dayDetail.itemQuantity), hint: 'كمية المواد المباعة' },
                { label: 'طلبات مضافة', value: fmt(dayDetail.ordersAdded), hint: `أُنجز ${fmt(dayDetail.ordersCompleted)} · رُفض ${fmt(dayDetail.ordersRejected)}` },
              ]"
              :key="card.label"
              cols="12"
              sm="6"
              lg="4"
            >
              <div class="ov-label">
                {{ card.label }}
              </div>
              <div class="ov-value ov-value-sm">
                {{ card.value }}
              </div>
              <div class="ov-hint">
                {{ card.hint }}
              </div>
            </VCol>
          </VRow>

          <h5 class="ov-section">
            حسب الفرع
          </h5>
          <div
            v-if="dayBranches.length > 1"
            class="ov-table-wrap"
          >
            <table class="ov-table">
              <thead>
                <tr>
                  <th>الفرع</th>
                  <th>المبيعات</th>
                  <th>سعر البيع</th>
                  <th>الطلبات المضافة</th>
                  <th>المقدمة</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="branch in dayBranches"
                  :key="branch.cityValue || branch.cityName"
                  class="ov-branch-row"
                  @click="selectBranch(branch.cityValue)"
                >
                  <td>{{ branch.cityName }}</td>
                  <td>{{ fmt(branch.salesCount) }}</td>
                  <td>{{ fmt(branch.salePrice) }}</td>
                  <td>{{ fmt(branch.ordersAdded) }}</td>
                  <td>{{ fmt(branch.downPayment) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <p
            v-else
            class="ov-hint"
          >
            {{ dayBranches[0]?.cityName || scopeTitle }}
          </p>

          <VRow>
            <VCol
              cols="12"
              md="7"
            >
              <h5 class="ov-section">
                مبيعات الموظفين
              </h5>
              <div class="ov-table-wrap">
                <table class="ov-table">
                  <thead>
                    <tr>
                      <th>الموظف</th>
                      <th>المبيعات</th>
                      <th>سعر البيع</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="person in dayDetail.sellers || []"
                      :key="person.name"
                    >
                      <td>{{ person.name }}</td>
                      <td>{{ fmt(person.count) }}</td>
                      <td>{{ fmt(person.salePrice) }}</td>
                    </tr>
                    <tr v-if="!(dayDetail.sellers || []).length">
                      <td colspan="3">
                        لا توجد مبيعات في هذا التاريخ
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </VCol>
            <VCol
              cols="12"
              md="5"
            >
              <h5 class="ov-section">
                حالة الطلبات المضافة
              </h5>
              <div
                class="ov-chart"
                style="height: 220px;"
              >
                <Doughnut
                  v-if="(dayDetail.orderStatuses || []).length"
                  :data="{
                    labels: (dayDetail.orderStatuses || []).map(row => statusLabel(row.status)),
                    datasets: [{
                      data: (dayDetail.orderStatuses || []).map(row => row.count),
                      backgroundColor: palette,
                    }],
                  }"
                  :options="doughnutOptions"
                />
                <div
                  v-else
                  class="ov-empty"
                >
                  لا طلبات مضافة في هذا التاريخ
                </div>
              </div>
            </VCol>
          </VRow>

          <h5 class="ov-section">
            من أضاف الطلبات
          </h5>
          <div class="ov-table-wrap">
            <table class="ov-table">
              <thead>
                <tr>
                  <th>الاسم</th>
                  <th>الصفة</th>
                  <th>الطلبات</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="person in dayDetail.creators || []"
                  :key="`${person.name}-${person.role}`"
                >
                  <td>{{ person.name }}</td>
                  <td>{{ roleLabel(person.role) || '—' }}</td>
                  <td>{{ fmt(person.count) }}</td>
                </tr>
                <tr v-if="!(dayDetail.creators || []).length">
                  <td colspan="3">
                    لا طلبات مضافة في هذا التاريخ
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <h5 class="ov-section">
            طلبات موظفي المبيعات
          </h5>
          <div class="ov-table-wrap">
            <table class="ov-table">
              <thead>
                <tr>
                  <th>الموظف</th>
                  <th>الطلبات</th>
                  <th>الحالة</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="person in dayDetail.agents || []"
                  :key="person.name"
                >
                  <td>{{ person.name }}</td>
                  <td>{{ fmt(person.orders) }}</td>
                  <td>{{ statusBits(person.statuses) || '—' }}</td>
                </tr>
                <tr v-if="!(dayDetail.agents || []).length">
                  <td colspan="3">
                    لا طلبات مسندة في هذا التاريخ
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <h5 class="ov-section">
            المواد المباعة
          </h5>
          <div class="ov-table-wrap">
            <table class="ov-table">
              <thead>
                <tr>
                  <th>المادة</th>
                  <th>التصنيف</th>
                  <th>الكمية</th>
                  <th>سعر البيع</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="item in (dayDetail.items || []).slice(0, 8)"
                  :key="item.itemName"
                >
                  <td>{{ item.itemName }}</td>
                  <td>{{ item.category || '—' }}</td>
                  <td>{{ fmt(item.quantity) }}</td>
                  <td>{{ fmt(item.salePrice) }}</td>
                </tr>
                <tr v-if="!(dayDetail.items || []).length">
                  <td colspan="4">
                    لا مواد في هذا التاريخ
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </template>
      </VCardText>
    </VCard>

    <h5 class="ov-section">
      طلبات البيع
    </h5>
    <VRow>
      <VCol
        v-for="card in [
          { label: 'أُنشئت هذا الشهر', value: fmt(scope.ordersCreated) },
          { label: 'تحولت إلى بيع', value: fmt(scope.ordersConverted) },
          { label: 'نسبة التحويل', value: fmtPct(scope.orderConversionPercent) },
          { label: 'أُنجزت هذا الشهر', value: fmt(scope.ordersFinished) },
          { label: 'مرفوضة هذا الشهر', value: fmt(scope.ordersRejected) },
          { label: 'مفتوحة الآن', value: fmt(scope.ordersOpen) },
          { label: 'غير مسندة', value: fmt(scope.ordersUnassigned) },
          { label: 'معلقة', value: fmt(scope.ordersPending) },
        ]"
        :key="card.label"
        cols="6"
        md="3"
      >
        <VCard>
          <VCardText>
            <div class="ov-label">
              {{ card.label }}
            </div>
            <div class="ov-value ov-value-sm">
              {{ card.value }}
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <VRow class="mt-2">
      <VCol
        cols="12"
        lg="8"
      >
        <VCard>
          <VCardTitle>سعر البيع وعدد المبيعات خلال الشهر</VCardTitle>
          <VCardText>
            <div class="ov-chart">
              <Line
                v-if="scope.daily.length"
                :data="lineData"
                :options="lineOptions"
              />
              <div
                v-else
                class="ov-empty"
              >
                لا توجد أيام في هذا الشهر
              </div>
            </div>
          </VCardText>
        </VCard>
      </VCol>
      <VCol
        cols="12"
        lg="4"
      >
        <VCard class="h-100">
          <VCardTitle>حالة طلبات الشهر</VCardTitle>
          <VCardText>
            <div class="ov-chart">
              <Doughnut
                v-if="scope.orderStatuses.length"
                :data="orderData"
                :options="doughnutOptions"
              />
              <div
                v-else
                class="ov-empty"
              >
                لا توجد طلبات هذا الشهر
              </div>
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <h5 class="ov-section">
      حسب الفرع
    </h5>
    <VRow>
      <VCol
        cols="12"
        lg="5"
      >
        <VCard>
          <VCardTitle>سعر البيع لكل فرع</VCardTitle>
          <VCardText>
            <div
              class="ov-chart"
              :style="{ height: `${branchChartHeight}px` }"
            >
              <Bar
                v-if="allBranches.length"
                :data="branchBarData"
                :options="branchBarOptions"
              />
            </div>
          </VCardText>
        </VCard>
      </VCol>
      <VCol
        cols="12"
        lg="7"
      >
        <VCard>
          <VCardText class="pa-0">
            <div class="ov-table-wrap ov-branches">
              <table class="ov-table">
                <thead>
                  <tr>
                    <th>الفرع</th>
                    <th>العدد</th>
                    <th>سعر البيع</th>
                    <th>المحصّل</th>
                    <th>النمو</th>
                    <th>القطع</th>
                    <th>طلبات الشهر</th>
                    <th>تحويل</th>
                    <th>مفتوحة</th>
                    <th>في الدوام</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="branch in allBranches"
                    :key="branch.cityValue || branch.cityName"
                    :class="{ 'ov-active': cityValue && String(cityValue) === String(branch.cityValue) }"
                    @click="selectBranch(branch.cityValue)"
                  >
                    <td>{{ branch.cityName }}</td>
                    <td>{{ fmt(branch.monthSalesCount) }}</td>
                    <td>{{ fmt(branch.monthSalePrice) }}</td>
                    <td>{{ fmtShare(branch.collectedPercent) }}</td>
                    <td :class="growthClass(branch.salePriceGrowthPercent)">
                      {{ fmtPct(branch.salePriceGrowthPercent) }}
                    </td>
                    <td>{{ fmt(branch.monthItemQuantity) }}</td>
                    <td>{{ fmt(branch.ordersCreated) }}</td>
                    <td>{{ fmtPct(branch.orderConversionPercent) }}</td>
                    <td>{{ fmt(branch.ordersOpen) }}</td>
                    <td>{{ fmt(branch.employeesOnShift) }}</td>
                  </tr>
                  <tr v-if="!allBranches.length">
                    <td colspan="10">
                      لا توجد فروع محمّلة
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <h5 class="ov-section">
      المواد المباعة
    </h5>
    <VRow>
      <VCol
        cols="12"
        md="4"
      >
        <VCard class="h-100">
          <VCardTitle>التصنيف</VCardTitle>
          <VCardText>
            <div class="ov-chart">
              <Doughnut
                v-if="scope.categories.length"
                :data="categoryData"
                :options="doughnutOptions"
              />
              <div
                v-else
                class="ov-empty"
              >
                لا توجد مواد مباعة
              </div>
            </div>
          </VCardText>
        </VCard>
      </VCol>
      <VCol
        cols="12"
        md="8"
      >
        <VCard>
          <VCardTitle>أعلى المواد</VCardTitle>
          <VCardText>
            <div
              class="ov-chart"
              :style="{ height: `${itemChartHeight}px` }"
            >
              <Bar
                v-if="scope.items.length"
                :data="itemBarData"
                :options="branchBarOptions"
              />
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <h5 class="ov-section">
      تقييم تسديد مبيعات الشهر
    </h5>
    <p class="ov-hint mb-3">
      تقييم التطبيق للعميل حسب سرعة الدفع: ممتاز، جيد، ضعيف، متلكئ، أو سيء.
    </p>
    <VRow>
      <VCol
        cols="12"
        md="4"
      >
        <div class="ov-score-stack">
          <VCard>
            <VCardText>
              <div class="ov-label">
                متوسط النقاط
              </div>
              <div class="ov-value">
                {{ fmtScore(scope.averageSaleScore) }}
              </div>
              <div class="ov-hint">
                10 ممتاز · 5 جيد · 0 ضعيف · -5 متلكئ · -10 سيء
              </div>
              <div class="ov-hint">
                {{ fmt(scope.unscoredSalesCount) }} مبيع بلا بيانات تسديد
              </div>
            </VCardText>
          </VCard>
          <VCard>
            <VCardText>
              <div class="ov-label">
                ممتاز وجيد
              </div>
              <div class="ov-value">
                {{ fmt((scope.excellentSales || 0) + (scope.goodSales || 0)) }}
              </div>
              <div class="ov-hint">
                ممتاز {{ fmt(scope.excellentSales) }} · جيد {{ fmt(scope.goodSales) }}
              </div>
            </VCardText>
          </VCard>
          <VCard>
            <VCardText>
              <div class="ov-label">
                متلكئ وسيء
              </div>
              <div class="ov-value">
                {{ fmt((scope.laggingSales || 0) + (scope.badSales || 0)) }}
              </div>
              <div class="ov-hint">
                متلكئ {{ fmt(scope.laggingSales) }} · سيء {{ fmt(scope.badSales) }} · ضعيف {{ fmt(scope.weakSales) }}
              </div>
            </VCardText>
          </VCard>
        </div>
      </VCol>
      <VCol
        cols="12"
        md="8"
      >
        <VCard class="h-100">
          <VCardTitle>توزيع تقييم التسديد</VCardTitle>
          <VCardText>
            <div
              class="ov-chart"
              style="height: 260px;"
            >
              <Bar
                v-if="scope.scoredSalesCount"
                :data="scoreChartData"
                :options="scoreChartOptions"
              />
              <div
                v-else
                class="ov-empty"
              >
                لا يمكن تقييم تسديد مبيعات هذا الشهر
              </div>
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <h5 class="ov-section">
      أداء موظفي المبيعات
    </h5>
    <VRow>
      <VCol cols="12">
        <VCard>
          <VCardText class="pa-0">
            <div class="ov-table-wrap">
              <table class="ov-table">
                <thead>
                  <tr>
                    <th>الموظف</th>
                    <th>المبيعات</th>
                    <th>سعر البيع</th>
                    <th>متوسط النقاط</th>
                    <th>ممتاز</th>
                    <th>جيد</th>
                    <th>ضعيف</th>
                    <th>متلكئ</th>
                    <th>سيء</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="person in scope.sellers"
                    :key="person.name"
                  >
                    <td>{{ person.name }}</td>
                    <td>{{ fmt(person.salesCount) }}</td>
                    <td>{{ fmt(person.salePrice) }}</td>
                    <td>{{ fmtScore(person.averageScore) }}</td>
                    <td>{{ fmt(person.excellentSales) }}</td>
                    <td>{{ fmt(person.goodSales) }}</td>
                    <td>{{ fmt(person.weakSales) }}</td>
                    <td>{{ fmt(person.laggingSales) }}</td>
                    <td>{{ fmt(person.badSales) }}</td>
                  </tr>
                  <tr v-if="!scope.sellers.length">
                    <td colspan="9">
                      لا يوجد موظفون في هذا النطاق
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <h5 class="ov-section">
      الموصون بالبيع
    </h5>
    <VRow>
      <VCol cols="12">
        <VCard>
          <VCardText class="pa-0">
            <div class="ov-table-wrap">
              <table class="ov-table">
                <thead>
                  <tr>
                    <th>الموصي</th>
                    <th>الصفة</th>
                    <th>المبيعات</th>
                    <th>سعر البيع</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="person in scope.recommenders"
                    :key="`${person.name}-${person.role}`"
                  >
                    <td>{{ person.name }}</td>
                    <td>{{ roleLabel(person.role) || '—' }}</td>
                    <td>{{ fmt(person.salesCount) }}</td>
                    <td>{{ fmt(person.salePrice) }}</td>
                  </tr>
                  <tr v-if="!scope.recommenders.length">
                    <td colspan="4">
                      لا يوجد موصون مرتبطون بمبيعات هذا الشهر
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <h5 class="ov-section">
      التشغيل الآن
    </h5>
    <VRow>
      <VCol
        v-for="card in [
          { label: 'في الدوام', value: scope.employeesOnShift },
          { label: 'بدون دوام', value: scope.employeesOffShift },
          { label: 'موقع مباشر', value: scope.liveLocations },
          { label: 'طلبات معلقة', value: scope.pendingSales },
          { label: 'طلبات جديدة', value: scope.newSalesRequests },
        ]"
        :key="card.label"
        cols="6"
        md="4"
        lg="2"
      >
        <VCard>
          <VCardText>
            <div class="ov-label">
              {{ card.label }}
            </div>
            <div class="ov-value ov-value-sm">
              {{ fmt(card.value) }}
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>
  </div>
</template>

<style scoped>
.ov-page {
  display: block;
}

.ov-page :deep(.v-row) {
  flex: none;
  margin-bottom: 8px;
}

.ov-page :deep(.v-col) {
  min-width: 0;
}

.ov-head {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
}

.ov-sub {
  margin: 0;
  color: rgba(var(--v-theme-on-surface), 0.65);
}

.ov-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  min-width: 0;
  max-width: 100%;
}

.ov-filters :deep(.v-input),
.ov-select {
  flex: 1 1 160px;
  max-width: 220px;
  min-width: 140px;
}

.ov-card {
  height: auto;
}

.ov-card-link {
  cursor: pointer;
}

.ov-card-link:hover,
.ov-card-link:focus-visible {
  outline: 2px solid #0f766e;
  outline-offset: 2px;
}

.ov-day-close {
  flex: 0 0 auto;
  align-self: flex-start;
}

.ov-score-stack {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.ov-label {
  font-size: 0.85rem;
  color: rgba(var(--v-theme-on-surface), 0.65);
}

.ov-value {
  font-size: 1.7rem;
  font-weight: 700;
  line-height: 1.3;
}

.ov-value-sm {
  font-size: 1.35rem;
}

.ov-hint {
  font-size: 0.78rem;
  color: rgba(var(--v-theme-on-surface), 0.55);
}

.ov-up {
  color: #047857;
}

.ov-down {
  color: #be123c;
}

.ov-section {
  margin: 22px 0 10px;
}

.ov-chart {
  position: relative;
  height: 280px;
  overflow: hidden;
}

.ov-chart :deep(canvas) {
  max-width: 100% !important;
}

.ov-empty {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(var(--v-theme-on-surface), 0.55);
}

.ov-table-wrap {
  overflow-x: auto;
}

.ov-table {
  width: 100%;
  border-collapse: collapse;
}

.ov-table th,
.ov-table td {
  padding: 10px 12px;
  text-align: right;
  white-space: nowrap;
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.ov-table th {
  font-size: 0.78rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
}

.ov-branches tbody tr {
  cursor: pointer;
}

.ov-active {
  background: rgba(15, 118, 110, 0.12);
}
</style>
