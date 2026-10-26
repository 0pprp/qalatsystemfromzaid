<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { directorSummaryDrilldown, directorSummaryMatrix } from '@/composables/directorApi'
import { useToast } from '@/composables/useToast'

definePage({
  meta: {
    layout: 'default',
  },
})

const toast = useToast()
const baghdad = new Date(Date.now() + 3 * 60 * 60 * 1000)
const year = ref(baghdad.getUTCFullYear())
const month = ref(baghdad.getUTCMonth() + 1)
/** مثبت: سعر البيع فقط */
const metric = 'salePrice'
const loading = ref(false)
const matrix = ref(null)
const drillOpen = ref(false)
const drillLoading = ref(false)
const drill = ref(null)
const drillMeta = ref({ cityName: '', day: 0 })

const monthItems = Array.from({ length: 12 }, (_, i) => ({
  title: String(i + 1),
  value: i + 1,
}))

const yearItems = computed(() => {
  const y = baghdad.getUTCFullYear()

  return [y - 1, y, y + 1].map(v => ({ title: String(v), value: v }))
})

const branches = computed(() => matrix.value?.branches || [])
const daysInMonth = computed(() => matrix.value?.daysInMonth || 31)

function fmt(n) {
  const v = Number(n || 0)
  if (!Number.isFinite(v) || v === 0)
    return '—'

  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(Math.round(v))
}

function cellTotal(branch, day) {
  const row = (branch?.days || []).find(d => Number(d.day) === Number(day))

  return Number(row?.total || 0)
}

function dayRowTotal(day) {
  return branches.value.reduce((sum, b) => sum + cellTotal(b, day), 0)
}

function branchMonthSaleTotal(branch) {
  const days = Number(daysInMonth.value) || 0
  let sum = 0
  for (let d = 1; d <= days; d++)
    sum += cellTotal(branch, d)

  return sum
}

const allSaleGrandTotal = computed(() =>
  branches.value.reduce((sum, b) => sum + branchMonthSaleTotal(b), 0),
)

async function loadMatrix() {
  loading.value = true
  try {
    matrix.value = await directorSummaryMatrix({
      year: year.value,
      month: month.value,
      metric,
    })
  }
  catch (e) {
    console.error(e)
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل ملخص المبيعات')
    matrix.value = null
  }
  finally {
    loading.value = false
  }
}

async function openDrill(branch, day) {
  const total = cellTotal(branch, day)
  if (!total)
    return
  drillMeta.value = { cityName: branch.name || branch.cityValue, day }
  drillOpen.value = true
  drillLoading.value = true
  drill.value = null
  try {
    drill.value = await directorSummaryDrilldown({
      cityValue: branch.cityValue,
      year: year.value,
      month: month.value,
      day,
      metric,
    })
  }
  catch (e) {
    console.error(e)
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل التفاصيل')
    drillOpen.value = false
  }
  finally {
    drillLoading.value = false
  }
}

const drillTitle = computed(() =>
  `سعر البيع — ${drillMeta.value.cityName} — يوم ${drillMeta.value.day}`,
)

const drillRows = computed(() => drill.value?.rows || drill.value?.Rows || [])

watch([year, month], () => {
  loadMatrix()
})

onMounted(loadMatrix)
</script>

<template>
  <div class="ds-page">
    <div class="ds-toolbar">
      <div class="ds-title-block">
        <div class="ds-title">
          <VIcon
            icon="tabler-chart-bar"
            size="26"
            class="ds-title-icon"
          />
          <span>ملخص المبيعات</span>
        </div>
        <div class="ds-subtitle">
          سعر البيع حسب المحافظة والأيام (عرض الشهر)
        </div>
      </div>

      <div class="ds-filters">
        <VSelect
          v-model="month"
          :items="monthItems"
          item-title="title"
          item-value="value"
          label="الشهر"
          density="comfortable"
          variant="outlined"
          hide-details
          class="ds-select ds-select-sm"
        />
        <VSelect
          v-model="year"
          :items="yearItems"
          item-title="title"
          item-value="value"
          label="السنة"
          density="comfortable"
          variant="outlined"
          hide-details
          class="ds-select ds-select-sm"
        />
        <VBtn
          class="ds-refresh"
          :loading="loading"
          prepend-icon="tabler-refresh"
          @click="loadMatrix"
        >
          تحديث
        </VBtn>
      </div>
    </div>

    <VProgressLinear
      v-if="loading"
      indeterminate
      color="#0d9488"
      class="mb-3 rounded"
    />

    <template v-if="branches.length">
      <div class="ds-cards-scroll">
        <div
          v-for="b in branches"
          :key="`card-${b.cityValue}`"
          class="ds-card"
        >
          <div class="ds-card-head">
            <VIcon
              icon="tabler-map-pin"
              size="18"
              class="ds-card-pin"
            />
            <span>{{ b.name }}</span>
          </div>
          <div class="ds-card-body">
            <div class="ds-metric">
              <span class="ds-metric-label">مجموع سعر البيع بالمحافظة</span>
              <span class="ds-metric-value ds-metric-actual">{{ fmt(branchMonthSaleTotal(b)) }}</span>
            </div>
          </div>
        </div>

        <div class="ds-card ds-card-grand">
          <div class="ds-card-head ds-card-head-grand">
            <VIcon
              icon="tabler-coin"
              size="18"
            />
            <span>الإجمالي</span>
          </div>
          <div class="ds-card-body">
            <div class="ds-metric">
              <span class="ds-metric-label">مجموع سعر البيع</span>
              <span class="ds-metric-value ds-metric-grand-total">{{ fmt(allSaleGrandTotal) }}</span>
            </div>
          </div>
        </div>
      </div>

      <div class="ds-table-wrap">
        <table class="ds-table">
          <thead>
            <tr>
              <th class="ds-th ds-th-day">
                <VIcon
                  icon="tabler-calendar"
                  size="16"
                  class="me-1"
                />
                اليوم
              </th>
              <th
                v-for="b in branches"
                :key="`th-${b.cityValue}`"
                class="ds-th ds-th-branch"
              >
                <VIcon
                  icon="tabler-map-pin"
                  size="15"
                  class="me-1"
                />
                {{ b.name }}
              </th>
              <th class="ds-th ds-th-sum">
                <VIcon
                  icon="tabler-coin"
                  size="16"
                  class="me-1"
                />
                المجموع
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="day in daysInMonth"
              :key="day"
            >
              <td class="ds-td ds-td-day">
                {{ day }}
              </td>
              <td
                v-for="b in branches"
                :key="`${b.cityValue}-${day}`"
                class="ds-td ds-td-amount"
                :class="{ 'is-clickable': cellTotal(b, day) > 0 }"
                @click="openDrill(b, day)"
              >
                {{ fmt(cellTotal(b, day)) }}
              </td>
              <td class="ds-td ds-td-sum">
                {{ fmt(dayRowTotal(day)) }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <div
      v-else-if="!loading"
      class="ds-empty"
    >
      لا توجد بيانات فروع للعرض
    </div>

    <VDialog
      v-model="drillOpen"
      max-width="720"
      scrollable
    >
      <VCard>
        <VCardTitle class="d-flex align-center">
          <span>{{ drillTitle }}</span>
          <VSpacer />
          <VBtn
            icon
            variant="text"
            @click="drillOpen = false"
          >
            <VIcon icon="tabler-x" />
          </VBtn>
        </VCardTitle>
        <VDivider />
        <VCardText>
          <div
            v-if="drillLoading"
            class="py-8 text-center"
          >
            <VProgressCircular indeterminate />
          </div>
          <template v-else>
            <div class="mb-4 text-h6">
              المجموع: {{ fmt(drill?.grandTotal ?? drill?.GrandTotal) }}
            </div>
            <VTable
              v-if="drillRows.length"
              density="compact"
            >
              <thead>
                <tr>
                  <th>الزبون</th>
                  <th>سعر البيع</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="(row, idx) in drillRows"
                  :key="idx"
                >
                  <td>{{ row.name }}</td>
                  <td>{{ fmt(row.amount) }}</td>
                </tr>
              </tbody>
            </VTable>
            <div
              v-else
              class="text-medium-emphasis"
            >
              لا توجد تفاصيل لهذا اليوم
            </div>
          </template>
        </VCardText>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.ds-page {
  --ds-teal: #0f766e;
  --ds-teal-soft: #e6f4f2;
  --ds-teal-mid: #14b8a6;
  --ds-orange: #d97706;
  --ds-orange-soft: #fff4e5;
  --ds-orange-mid: #f59e0b;
  --ds-ink: #1f2937;
  --ds-muted: #6b7280;
  --ds-line: #e5e7eb;
  --ds-day-w: 88px;
  --ds-sum-w: 132px;
  --ds-branch-min: 128px;
}

.ds-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-block-end: 18px;
}

.ds-title-block {
  min-inline-size: 220px;
}

.ds-title {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--ds-ink);
  font-size: 1.55rem;
  font-weight: 800;
  line-height: 1.2;
}

.ds-title-icon {
  color: var(--ds-teal);
}

.ds-subtitle {
  margin-block-start: 4px;
  color: var(--ds-muted);
  font-size: 0.92rem;
}

.ds-filters {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}

.ds-select {
  background: #fff;
}

.ds-select-metric {
  min-inline-size: 170px;
  max-inline-size: 200px;
}

.ds-select-sm {
  min-inline-size: 100px;
  max-inline-size: 120px;
}

.ds-refresh {
  background: linear-gradient(135deg, #0f766e, #14b8a6) !important;
  color: #fff !important;
  font-weight: 700;
  letter-spacing: 0;
  min-block-size: 44px;
  padding-inline: 18px;
  border-radius: 12px;
  box-shadow: 0 6px 16px rgba(15, 118, 110, 0.25);
}

.ds-cards-scroll {
  display: flex;
  gap: 12px;
  overflow-x: auto;
  padding-block-end: 10px;
  margin-block-end: 14px;
  scrollbar-width: thin;
}

.ds-card {
  flex: 0 0 auto;
  inline-size: 168px;
  background: #fff;
  border: 1px solid var(--ds-line);
  border-radius: 14px;
  overflow: hidden;
  box-shadow: 0 4px 14px rgba(15, 23, 42, 0.06);
}

.ds-card-grand {
  border-color: #fcd34d;
  box-shadow: 0 6px 18px rgba(217, 119, 6, 0.14);
}

.ds-card-head {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 10px 12px;
  background: linear-gradient(180deg, #eef7f6, #e2f2f0);
  color: var(--ds-teal);
  font-size: 0.95rem;
  font-weight: 800;
  border-block-end: 1px solid #d7ebe8;
}

.ds-card-head-grand {
  background: linear-gradient(180deg, #ffe8c2, #ffd89a);
  color: #b45309;
  border-block-end-color: #f6c97a;
}

.ds-card-pin {
  color: #0ea5a0;
}

.ds-card-body {
  display: grid;
  gap: 10px;
  padding: 12px;
}

.ds-metric {
  display: grid;
  gap: 2px;
  text-align: center;
}

.ds-metric-label {
  color: var(--ds-muted);
  font-size: 0.75rem;
  font-weight: 600;
}

.ds-metric-value {
  font-size: 0.98rem;
  font-weight: 800;
  letter-spacing: 0.01em;
}

.ds-metric-total {
  color: var(--ds-ink);
}

.ds-metric-actual {
  color: var(--ds-teal);
}

.ds-metric-grand-total {
  color: var(--ds-orange);
}

.ds-table-wrap {
  overflow: auto;
  max-block-size: calc(100vh - 320px);
  background: #fff;
  border: 1px solid var(--ds-line);
  border-radius: 16px;
  box-shadow: 0 4px 18px rgba(15, 23, 42, 0.05);
}

.ds-table {
  border-collapse: separate;
  border-spacing: 0;
  inline-size: max-content;
  min-inline-size: 100%;
}

.ds-th,
.ds-td {
  box-sizing: border-box;
  padding: 12px 14px;
  text-align: center;
  white-space: nowrap;
  border-block-end: 1px solid var(--ds-line);
  border-inline-start: 1px solid #edf0f2;
  font-size: 0.95rem;
}

.ds-th {
  position: sticky;
  top: 0;
  z-index: 3;
  background: linear-gradient(180deg, #e8f4f3, #dcefee);
  color: var(--ds-teal);
  font-weight: 800;
}

.ds-th-day {
  right: 0;
  z-index: 6;
  inline-size: var(--ds-day-w);
  min-inline-size: var(--ds-day-w);
  max-inline-size: var(--ds-day-w);
  background: linear-gradient(180deg, #d9efec, #c7e7e3);
  box-shadow: -3px 0 8px rgba(15, 118, 110, 0.08);
}

.ds-th-branch {
  min-inline-size: var(--ds-branch-min);
}

.ds-th-sum {
  left: 0;
  z-index: 5;
  inline-size: var(--ds-sum-w);
  min-inline-size: var(--ds-sum-w);
  max-inline-size: var(--ds-sum-w);
  background: linear-gradient(180deg, #ffe7bf, #ffd899);
  color: #b45309;
  box-shadow: 3px 0 8px rgba(217, 119, 6, 0.1);
}

.ds-td {
  background: #fff;
  color: var(--ds-ink);
  font-weight: 600;
  min-block-size: 46px;
}

.ds-table tbody tr:nth-child(even) .ds-td:not(.ds-td-day):not(.ds-td-sum) {
  background: #fafbfc;
}

.ds-td-day {
  position: sticky;
  right: 0;
  z-index: 4;
  inline-size: var(--ds-day-w);
  min-inline-size: var(--ds-day-w);
  max-inline-size: var(--ds-day-w);
  background: var(--ds-teal-soft);
  color: var(--ds-teal);
  font-size: 1.05rem;
  font-weight: 800;
  box-shadow: -3px 0 8px rgba(15, 118, 110, 0.08);
}

.ds-td-amount.is-clickable {
  color: var(--ds-teal);
  cursor: pointer;
  font-weight: 700;
}

.ds-td-amount.is-clickable:hover {
  background: #eef8f6;
}

.ds-td-sum {
  position: sticky;
  left: 0;
  z-index: 4;
  inline-size: var(--ds-sum-w);
  min-inline-size: var(--ds-sum-w);
  max-inline-size: var(--ds-sum-w);
  background: var(--ds-orange-soft);
  color: var(--ds-orange);
  font-size: 1.02rem;
  font-weight: 800;
  box-shadow: 3px 0 8px rgba(217, 119, 6, 0.1);
}

.ds-empty {
  padding: 48px 16px;
  text-align: center;
  color: var(--ds-muted);
  background: #fff;
  border: 1px dashed var(--ds-line);
  border-radius: 16px;
}

@media (max-width: 900px) {
  .ds-toolbar {
    flex-direction: column;
    align-items: stretch;
  }

  .ds-filters {
    width: 100%;
  }

  .ds-select-metric,
  .ds-select-sm {
    flex: 1 1 auto;
    max-inline-size: none;
  }
}
</style>
