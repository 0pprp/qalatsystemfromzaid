<script setup>
import { computed, ref, watch } from 'vue'
import { asList, branchApp, daysAgo, errorText, fmtDate, isoDay, money, pick } from '@/composables/branchAppApi'
import { useToast } from '@/composables/useToast'

const props = defineProps({ city: { type: String, required: true } })
const toast = useToast()

const scopes = [
  { value: 'branch', title: 'الفرع كاملاً' },
  { value: 'list', title: 'قائمة' },
  { value: 'follower', title: 'متابع' },
  { value: 'employee', title: 'موظف مبيعات' },
]

const catalog = ref({ lists: [], followers: [], employees: [] })
const scope = ref('branch')
const entityId = ref(0)
const fromDate = ref(daysAgo(30))
const toDate = ref(isoDay(new Date()))
const trend = ref(null)
const loading = ref(false)

const levelDialog = ref(false)
const levelTitle = ref('')
const levelRows = ref([])
const levelLoading = ref(false)

const entities = computed(() => {
  const key = { list: 'lists', follower: 'followers', employee: 'employees' }[scope.value]
  if (!key)
    return []

  return (catalog.value[key] || []).map(e => ({ id: Number(pick(e, 'id', 'Id') || 0), name: pick(e, 'name', 'Name') || '—' }))
})

const levels = computed(() => {
  const t = trend.value || {}

  return [
    { key: 'excellent', title: 'ممتاز', color: 'success', count: pick(t, 'excellentCount', 'ExcellentCount') },
    { key: 'good', title: 'جيد', color: 'info', count: pick(t, 'goodCount', 'GoodCount') },
    { key: 'weak', title: 'ضعيف', color: 'warning', count: pick(t, 'weakCount', 'WeakCount') },
    { key: 'rejected', title: 'متلكئ', color: 'error', count: pick(t, 'rejectedCount', 'RejectedCount') },
    { key: 'bad', title: 'سيء', color: 'error', count: pick(t, 'badCount', 'BadCount') },
    { key: 'legal', title: 'قانونية', color: 'secondary', count: pick(t, 'legalCount', 'LegalCount') },
  ].filter(l => l.count !== undefined)
})

const levelHeaders = [
  { title: 'الزبون', key: 'name' },
  { title: 'الهاتف', key: 'phone' },
  { title: 'تاريخ البيع', key: 'dateSale' },
  { title: 'آخر تسديد', key: 'lastPayment' },
  { title: 'الواصل', key: 'paid', align: 'end' },
  { title: 'الباقي', key: 'remaining', align: 'end' },
]

const levelItems = computed(() => levelRows.value.map(r => {
  const hide = !!pick(r, 'hidePaidRemaining', 'HidePaidRemaining')

  return {
    name: pick(r, 'customerName', 'CustomerName') || '—',
    phone: pick(r, 'phone', 'Phone') || '',
    dateSale: pick(r, 'dateSale', 'DateSale'),
    lastPayment: pick(r, 'lastPaymentDate', 'LastPaymentDate'),
    paid: hide ? '—' : money(pick(r, 'amountPaid', 'AmountPaid')),
    remaining: hide ? '—' : money(pick(r, 'amountRemaining', 'AmountRemaining')),
  }
}))

const ready = computed(() => scope.value === 'branch' || entityId.value > 0)

async function loadCatalog() {
  catalog.value = { lists: [], followers: [], employees: [] }
  try {
    const c = await branchApp.catalog(props.city)
    catalog.value = {
      lists: c?.lists ?? c?.Lists ?? [],
      followers: c?.followers ?? c?.Followers ?? [],
      employees: c?.employees ?? c?.Employees ?? [],
    }
  }
  catch (e) {
    toast.error(errorText(e, 'تعذر تحميل القوائم'))
  }
}

async function loadTrend() {
  trend.value = null
  if (!ready.value)
    return
  loading.value = true
  try {
    trend.value = await branchApp.ratingTrend({
      cityValue: props.city,
      scope: scope.value,
      id: scope.value === 'branch' ? 0 : entityId.value,
      fromDate: fromDate.value,
      toDate: toDate.value,
    })
  }
  catch (e) {
    toast.error(errorText(e, 'تعذر تحميل التقييم'))
  }
  finally {
    loading.value = false
  }
}

async function openLevel(level) {
  levelTitle.value = `${level.title} (${level.count ?? 0})`
  levelRows.value = []
  levelDialog.value = true
  levelLoading.value = true
  try {
    levelRows.value = asList(await branchApp.ratingCustomers({
      cityValue: props.city,
      scope: scope.value,
      id: scope.value === 'branch' ? 0 : entityId.value,
      level: level.key,
      fromDate: fromDate.value,
      toDate: toDate.value,
    }))
  }
  catch (e) {
    toast.error(errorText(e, 'تعذر تحميل الزبائن'))
  }
  finally {
    levelLoading.value = false
  }
}

function num(v, digits = 2) {
  const n = Number(v)

  return Number.isFinite(n) ? n.toFixed(digits) : '—'
}

watch(() => props.city, async () => {
  scope.value = 'branch'
  entityId.value = 0
  await loadCatalog()
  await loadTrend()
}, { immediate: true })

watch(scope, () => {
  entityId.value = 0
  if (scope.value === 'branch')
    loadTrend()
  else
    trend.value = null
})
watch([entityId, fromDate, toDate], () => loadTrend())
</script>

<template>
  <div>
    <VRow dense>
      <VCol
        cols="12"
        sm="3"
      >
        <VSelect
          v-model="scope"
          :items="scopes"
          label="النطاق"
          density="compact"
          hide-details
        />
      </VCol>
      <VCol
        v-if="scope !== 'branch'"
        cols="12"
        sm="3"
      >
        <VAutocomplete
          v-model="entityId"
          :items="entities"
          item-title="name"
          item-value="id"
          label="اختر"
          density="compact"
          hide-details
        />
      </VCol>
      <VCol
        cols="6"
        sm="3"
      >
        <VTextField
          v-model="fromDate"
          type="date"
          label="من"
          density="compact"
          hide-details
        />
      </VCol>
      <VCol
        cols="6"
        sm="3"
      >
        <VTextField
          v-model="toDate"
          type="date"
          label="إلى"
          density="compact"
          hide-details
        />
      </VCol>
    </VRow>

    <VProgressLinear
      v-if="loading"
      indeterminate
      class="mt-4"
    />

    <div
      v-if="!ready"
      class="text-medium-emphasis text-center py-8"
    >
      اختر من القائمة لعرض التقييم
    </div>

    <div
      v-else-if="trend"
      class="mt-4"
    >
      <div class="d-flex flex-wrap ga-6 mb-4">
        <div>
          <div class="text-caption text-medium-emphasis">
            {{ pick(trend, 'metricLabel', 'MetricLabel') || 'التقييم' }}
          </div>
          <div class="text-h4">
            {{ num(pick(trend, 'endValue', 'EndValue')) }}
            <span class="text-body-1 text-medium-emphasis">{{ pick(trend, 'ratingLabel', 'RatingLabel') || '' }}</span>
          </div>
        </div>
        <div>
          <div class="text-caption text-medium-emphasis">
            التغيّر خلال الفترة
          </div>
          <div
            class="text-h6"
            :class="pick(trend, 'direction', 'Direction') === 'down' ? 'text-error' : pick(trend, 'direction', 'Direction') === 'up' ? 'text-success' : ''"
          >
            <span dir="ltr">{{ num(pick(trend, 'changeAbsolute', 'ChangeAbsolute')) }}</span>
          </div>
        </div>
        <div>
          <div class="text-caption text-medium-emphasis">
            عدد الزبائن
          </div>
          <div class="text-h6">
            {{ pick(trend, 'customerCount', 'CustomerCount') ?? '—' }}
          </div>
        </div>
        <div v-if="pick(trend, 'periodEndValue', 'PeriodEndValue') !== undefined">
          <div class="text-caption text-medium-emphasis">
            تقييم الفترة
          </div>
          <div class="text-h6">
            {{ num(pick(trend, 'periodEndValue', 'PeriodEndValue')) }}
            <span class="text-body-2 text-medium-emphasis">{{ pick(trend, 'periodRatingLabel', 'PeriodRatingLabel') || '' }}</span>
          </div>
        </div>
      </div>

      <div class="text-caption text-medium-emphasis mb-2">
        اضغط على المستوى لعرض الزبائن
      </div>
      <div class="d-flex flex-wrap ga-2">
        <VChip
          v-for="l in levels"
          :key="l.key"
          :color="l.color"
          label
          @click="openLevel(l)"
        >
          {{ l.title }}: {{ l.count ?? 0 }}
        </VChip>
      </div>
    </div>

    <VDialog
      v-model="levelDialog"
      max-width="900"
    >
      <VCard :title="levelTitle">
        <VCardText>
          <VDataTable
            :headers="levelHeaders"
            :items="levelItems"
            :loading="levelLoading"
            :items-per-page="25"
            density="compact"
            items-per-page-text="عدد الصفوف"
            page-text="{0}-{1} من {2}"
            no-data-text="لا يوجد زبائن"
          >
            <template #item.dateSale="{ item }">
              {{ fmtDate(item.dateSale) }}
            </template>
            <template #item.lastPayment="{ item }">
              {{ fmtDate(item.lastPayment) }}
            </template>
          </VDataTable>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn @click="levelDialog = false">
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
