<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { formatIraqTime } from '@/composables/gpsTrack'
import {
  branchRowKey,
  evaluationLabel,
  requestHistoryLabel,
  requestStatusLabel,
  salesManagerBase,
  smGet,
  smGetBlob,
  smGetEmployees,
  smPost,
  withCityQuery,
} from '@/composables/salesManagerApi'
import { isDemo } from '@/composables/useCities'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const rows = ref([])
const tab = ref('all')
const cityValue = ref('')
const selected = ref(null)
const detail = ref(null)
const profile = ref(null)
const profileOpen = ref(false)
const saleDetail = ref(null)
const shopImageUrl = ref('')
const shopImageOpen = ref(false)
const managerNote = ref('')
const employees = ref([])
const assignEmployeeId = ref(null)
const assignCityValue = ref('')
const returnOpen = ref(false)
const returnNote = ref('')
const busy = ref(false)

const tabs = [
  { value: 'all', title: 'الكل' },
  { value: 'sold', title: 'تم البيع' },
  { value: 'pending', title: 'معلقة' },
  { value: 'prepared', title: 'مجهز للبيع' },
  { value: 'rejected', title: 'مرفوض' },
]

const allStatuses = [
  'New',
  'Assigned',
  'Viewed',
  'Returned',
  'Pending',
  'PreparedForSale',
  'InProgress',
  'ConvertedToSale',
  'Rejected',
  'Completed',
]

function matchesTab(status) {
  const s = String(status || '')
  switch (tab.value) {
    case 'sold':
      return s === 'Completed'
    case 'pending':
      return s === 'Pending'
    case 'prepared':
      return s === 'PreparedForSale' || s === 'InProgress' || s === 'ConvertedToSale'
    case 'rejected':
      return s === 'Rejected'
    default:
      return allStatuses.includes(s)
  }
}

function statusColor(status) {
  switch (String(status || '')) {
    case 'Completed':
      return 'success'
    case 'Pending':
      return 'warning'
    case 'PreparedForSale':
    case 'InProgress':
    case 'ConvertedToSale':
      return 'info'
    case 'Rejected':
      return 'error'
    case 'Returned':
      return 'warning'
    default:
      return 'secondary'
  }
}

function isPrepared(status) {
  return status === 'PreparedForSale' || status === 'InProgress' || status === 'ConvertedToSale'
}

const visibleRows = computed(() => rows.value.filter(row => matchesTab(row.status)))

function statusText(status) {
  if (status === 'Completed')
    return 'تم البيع'

  return requestStatusLabel[status] || status
}

async function load() {
  rows.value = await smGet(withCityQuery('sales-requests', cityValue.value)) || []
  if (selected.value)
    await openDetails(selected.value, false)
}

async function openDetails(row, resetAssign = true) {
  selected.value = row
  const city = row.cityValue
  try {
    detail.value = await smGet(`sales-requests/${encodeURIComponent(city)}/${row.id}`)
  }
  catch {
    detail.value = row
  }
  if (resetAssign) {
    assignCityValue.value = detail.value?.cityValue || city || ''
    assignEmployeeId.value = null
    returnNote.value = ''
    returnOpen.value = false
    managerNote.value = ''
  }
  await loadEmployees()
}

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function money(value) {
  const n = Number(value || 0)
  if (!Number.isFinite(n))
    return '0 د.ع'

  return `${n.toLocaleString('en-US')} د.ع`
}

function areaText(shop) {
  const area = Number(pick(shop, 'shopArea', 'ShopArea') ?? 0)

  return `${area} م²`
}

const latestShop = computed(() => profile.value?.latestShop || profile.value?.LatestShop || null)
const profileSales = computed(() => profile.value?.sales || profile.value?.Sales || [])
const profileNotes = computed(() => profile.value?.notes || profile.value?.Notes || [])
const profileEvaluations = computed(() => profile.value?.evaluations || profile.value?.Evaluations || [])
const profileRequests = computed(() => profile.value?.salesRequests || profile.value?.SalesRequests || [])
const profileHistory = computed(() => profile.value?.history || profile.value?.History || [])

function customerProfilePath(city, params) {
  return isDemo()
    ? `customers/profile?${params}`
    : `customers/${encodeURIComponent(city)}/profile?${params}`
}

function customerNotesPath(city) {
  return isDemo()
    ? 'customers/notes'
    : `customers/${encodeURIComponent(city)}/notes`
}

function shopImagePath(city, saleId) {
  return isDemo()
    ? `sales/${saleId}/shop-image`
    : `sales/${encodeURIComponent(city)}/${saleId}/shop-image`
}

function saleDetailPath(city, saleId) {
  return isDemo()
    ? `sales/${saleId}`
    : `sales/${encodeURIComponent(city)}/${saleId}`
}

function currentSaleId(row, d) {
  return pick(d, 'convertedToSaleId', 'ConvertedToSaleId')
    || pick(row, 'convertedToSaleId', 'ConvertedToSaleId')
    || pick(d, 'saleId', 'SaleId')
    || pick(row, 'saleId', 'SaleId')
}

async function loadShopImage(city, saleId) {
  if (shopImageUrl.value) {
    URL.revokeObjectURL(shopImageUrl.value)
    shopImageUrl.value = ''
  }
  if (!saleId)
    return
  try {
    const blob = await smGetBlob(shopImagePath(city, saleId))
    if (blob && blob.size)
      shopImageUrl.value = URL.createObjectURL(blob)
  }
  catch {
    shopImageUrl.value = ''
  }
}

async function openCustomerProfile(row, d = null) {
  if (row)
    selected.value = row
  profileOpen.value = true
  let source = d || detail.value
  if (!source && row?.id) {
    try {
      source = isDemo()
        ? await smGet(`sales-requests/${row.id}`)
        : await smGet(`sales-requests/${encodeURIComponent(row.cityValue)}/${row.id}`)
    }
    catch {
      source = row
    }
  }
  await loadProfile(row || selected.value, source || row)
}

async function loadProfile(row, d) {
  profile.value = null
  saleDetail.value = null
  await loadShopImage('', null)
  const city = d?.cityValue || row?.cityValue
  if (!city)
    return
  const params = new URLSearchParams()
  const customerId = pick(d, 'existingCustomerId', 'ExistingCustomerId') || pick(row, 'existingCustomerId', 'ExistingCustomerId')
  const name = pick(d, 'customerName', 'CustomerName') || pick(row, 'customerName', 'CustomerName')
  const phone = pick(d, 'customerPhone', 'CustomerPhone') || pick(row, 'customerPhone', 'CustomerPhone')
  if (customerId)
    params.set('customerId', String(customerId))
  if (name)
    params.set('name', name)
  if (phone)
    params.set('phone', phone)
  if (![...params.keys()].length)
    return
  try {
    profile.value = await smGet(customerProfilePath(city, params))
    const shop = profile.value?.latestShop || profile.value?.LatestShop
    const saleId = currentSaleId(row, d) || pick(shop, 'saleId', 'SaleId')
    if (saleId) {
      await loadShopImage(city, saleId)
      try {
        saleDetail.value = await smGet(saleDetailPath(city, saleId))
      }
      catch {
        saleDetail.value = null
      }
    }
  }
  catch {
    profile.value = null
  }
}

const profileName = computed(() =>
  pick(profile.value, 'customerName', 'CustomerName')
  || pick(saleDetail.value, 'fullName', 'FullName')
  || pick(detail.value, 'customerName', 'CustomerName')
  || pick(selected.value, 'customerName', 'CustomerName')
  || '')
const profilePhone = computed(() =>
  pick(profile.value, 'phone', 'Phone')
  || pick(saleDetail.value, 'phone', 'Phone')
  || pick(detail.value, 'customerPhone', 'CustomerPhone')
  || pick(selected.value, 'customerPhone', 'CustomerPhone')
  || '')
const profileProvince = computed(() =>
  pick(saleDetail.value, 'province', 'Province')
  || pick(detail.value, 'customerProvince', 'CustomerProvince')
  || pick(selected.value, 'customerProvince', 'CustomerProvince')
  || pick(profile.value, 'cityName', 'CityName')
  || pick(selected.value, 'cityName', 'CityName')
  || '')
const profileAddress = computed(() =>
  pick(saleDetail.value, 'address', 'Address')
  || pick(detail.value, 'customerAddress', 'CustomerAddress')
  || '')
const profileNationalId = computed(() =>
  pick(saleDetail.value, 'nationalCardNumber', 'NationalCardNumber')
  || '')

async function addManagerNote() {
  const note = managerNote.value.trim()
  if (!note) {
    toast.error('الملاحظة مطلوبة')

    return
  }
  const d = detail.value || selected.value
  const city = d?.cityValue
  if (!city)
    return
  busy.value = true
  try {
    await smPost(customerNotesPath(city), {
      customerId: pick(d, 'existingCustomerId', 'ExistingCustomerId'),
      customerName: pick(d, 'customerName', 'CustomerName'),
      customerPhone: pick(d, 'customerPhone', 'CustomerPhone'),
      note,
    })
    managerNote.value = ''
    toast.success('تمت إضافة الملاحظة')
    await loadProfile(selected.value, d)
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر حفظ الملاحظة')
  }
  finally {
    busy.value = false
  }
}

async function loadEmployees() {
  const city = assignCityValue.value || detail.value?.cityValue
  if (!city) {
    employees.value = []

    return
  }
  try {
    employees.value = await smGetEmployees(city)
  }
  catch {
    employees.value = []
  }
}

const historyRows = computed(() => {
  const d = detail.value
  if (!d)
    return []
  if (Array.isArray(d.history) && d.history.length)
    return d.history
  if (Array.isArray(d.History) && d.History.length)
    return d.History
  if (Array.isArray(d.timeline) && d.timeline.length) {
    return d.timeline.map(item => ({
      createdAtUtc: item.atUtc || item.AtUtc,
      event: item.event || item.Event,
      actorName: '',
      note: item.detail || item.Detail,
    }))
  }

  return []
})

function historyTime(row) {
  return formatIraqTime(row.createdAtUtc || row.CreatedAtUtc)
}

function historyEvent(row) {
  const ev = row.event || row.Event || ''

  return requestHistoryLabel[ev] || ev
}

function historyActor(row) {
  return row.actorName || row.ActorName || row.actorType || row.ActorType || ''
}

function historyNote(row) {
  return row.note || row.Note || row.detail || row.Detail || ''
}

async function assign() {
  const d = detail.value
  if (!d || d.status !== 'New')
    return
  if (!assignCityValue.value || !assignEmployeeId.value) {
    toast.error('اختر المحافظة وموظف المبيعات')

    return
  }
  const employee = employees.value.find(e => e.employeeId === assignEmployeeId.value)
  busy.value = true
  try {
    detail.value = await smPost(
      `sales-requests/${encodeURIComponent(d.cityValue)}/${d.id}/assign`,
      {
        employeeId: assignEmployeeId.value,
        employeeName: employee?.employeeName,
        cityValue: assignCityValue.value,
        cityName: employee?.cityName || employee?.branchName,
      },
    )
    toast.success('تم إسناد الطلب')
    await load()
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر إسناد الطلب')
  }
  finally {
    busy.value = false
  }
}

async function sendReturn() {
  const note = returnNote.value.trim()
  if (!note) {
    toast.error('ملاحظة الإعادة مطلوبة')

    return
  }
  const d = detail.value
  const requestId = d.id ?? d.Id
  const returnPath = isDemo()
    ? `sales-requests/${requestId}/return`
    : `sales-requests/${encodeURIComponent(d.cityValue)}/${requestId}/return`
  busy.value = true
  try {
    detail.value = await smPost(returnPath, { note })
    returnOpen.value = false
    returnNote.value = ''
    toast.success('تمت إعادة الطلب لنفس الموظف')
    await load()
  }
  catch (err) {
    const status = err?.response?.status
    const backend = err?.response?.data
    const message = backend?.message || err?.message || 'تعذر إعادة الطلب'
    console.error('return request failed', {
      url: `${salesManagerBase()}${returnPath}`,
      status,
      body: backend,
      message,
    })
    toast.error(status ? `${message} (${status})` : message)
  }
  finally {
    busy.value = false
  }
}

onMounted(load)
onUnmounted(() => {
  if (shopImageUrl.value)
    URL.revokeObjectURL(shopImageUrl.value)
})
</script>

<template>
  <div>
    <div class="d-flex justify-space-between mb-4">
      <h4>طلبات المبيعات</h4>
      <VBtn
        color="primary"
        :to="{ name: 'sales-manager-request-create' }"
      >
        + طلب مبيع جديد
      </VBtn>
    </div>
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
    </VRow>
    <VChipGroup
      v-model="tab"
      column
    >
      <VChip
        v-for="item in tabs"
        :key="item.value"
        :value="item.value"
        filter
      >
        {{ item.title }}
      </VChip>
    </VChipGroup>
    <VRow class="mt-4">
      <VCol
        v-for="row in visibleRows"
        :key="branchRowKey(row, 'id')"
        cols="12"
        md="6"
      >
        <VCard
          :class="{ 'border-primary': selected?.id === row.id && selected?.cityValue === row.cityValue }"
          @click="openDetails(row)"
        >
          <VCardText>
            <div class="d-flex align-center justify-space-between gap-2 mb-2">
              <div>طلب #{{ row.id }} — {{ row.branchName || row.cityName }}</div>
              <VChip
                size="small"
                :color="statusColor(row.status)"
              >
                {{ statusText(row.status) }}
              </VChip>
            </div>
            <strong>{{ row.customerName }}</strong>
            <div>{{ row.targetEmployeeName || 'غير مسند' }} — {{ row.branchName || row.cityName }}</div>
            <VChip
              v-if="row.status === 'Returned'"
              size="small"
              color="warning"
              class="mt-1"
            >
              معاد للموظف
            </VChip>
            <div
              v-if="row.status === 'Returned' && (row.returnNote || row.ReturnNote)"
              class="text-error mt-1"
            >
              ملاحظة الإعادة: {{ row.returnNote || row.ReturnNote }}
            </div>
            <div
              v-if="row.status === 'Pending' && (row.pendingNote || row.PendingNote)"
              class="mt-1"
            >
              ملاحظة التعليق: {{ row.pendingNote || row.PendingNote }}
            </div>
            <div class="text-medium-emphasis">
              {{ formatIraqTime(row.createdAtUtc) }}
            </div>
            <VBtn
              v-if="row.status === 'Completed'"
              class="mt-3"
              color="primary"
              size="small"
              @click.stop="openCustomerProfile(row)"
            >
              فتح بروفايل الزبون
            </VBtn>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <VDialog
      :model-value="!!detail"
      max-width="840"
      @update:model-value="v => { if (!v) { detail = null; if (!profileOpen) selected = null } }"
    >
      <VCard v-if="detail">
        <VCardTitle>طلب #{{ detail.id }}</VCardTitle>
        <VCardText>
          <div>الزبون: {{ detail.customerName }}</div>
          <div>الموظف: {{ detail.targetEmployeeName || 'غير مسند' }}</div>
          <div>المحافظة: {{ detail.cityName || detail.branchName || detail.cityValue }}</div>
          <div class="d-flex align-center gap-2 mt-1">
            <span>الحالة:</span>
            <VChip
              size="small"
              :color="statusColor(detail.status)"
            >
              {{ statusText(detail.status) }}
            </VChip>
            <VChip
              v-if="detail.status === 'Returned'"
              size="small"
              color="warning"
            >
              معاد للموظف
            </VChip>
          </div>
          <div v-if="isPrepared(detail.status)" class="mt-1">
            مجهز للبيع — الموظف هو من يكمل البيع.
          </div>
          <div v-if="detail.status === 'Pending' && (detail.pendingNote || detail.PendingNote)">
            ملاحظة التعليق: {{ detail.pendingNote || detail.PendingNote }}
          </div>
          <div v-if="detail.rejectionReason || detail.RejectionReason">
            سبب الرفض: {{ detail.rejectionReason || detail.RejectionReason }}
          </div>
          <div
            v-if="detail.returnNote || detail.ReturnNote"
            class="text-error font-weight-bold"
          >
            ملاحظة الإعادة: {{ detail.returnNote || detail.ReturnNote }}
          </div>
          <div class="text-medium-emphasis mt-2">
            الإنشاء: {{ formatIraqTime(detail.createdAtUtc) }}
          </div>
          <div
            v-if="detail.assignedAtUtc"
            class="text-medium-emphasis"
          >
            الإسناد: {{ formatIraqTime(detail.assignedAtUtc) }}
          </div>
          <div
            v-if="detail.viewedAtUtc"
            class="text-medium-emphasis"
          >
            المشاهدة: {{ formatIraqTime(detail.viewedAtUtc) }}
          </div>
          <div
            v-if="detail.rejectedAtUtc"
            class="text-medium-emphasis"
          >
            الرفض: {{ formatIraqTime(detail.rejectedAtUtc) }}
          </div>
          <div
            v-if="detail.completedAtUtc"
            class="text-medium-emphasis"
          >
            الاكتمال: {{ formatIraqTime(detail.completedAtUtc) }}
          </div>

          <template v-if="detail.status === 'New'">
            <VDivider class="my-4" />
            <div class="font-weight-bold mb-2">
              إسناد الطلب
            </div>
            <div class="mb-2">
              المحافظة: {{ detail.cityName || detail.branchName || assignCityValue }}
            </div>
            <VSelect
              v-model="assignEmployeeId"
              :items="employees"
              item-title="employeeName"
              item-value="employeeId"
              label="موظف المبيعات"
              :disabled="!assignCityValue"
            />
            <VBtn
              class="mt-3"
              color="primary"
              :loading="busy"
              :disabled="!assignEmployeeId"
              @click="assign"
            >
              إسناد لموظف
            </VBtn>
          </template>

          <template v-if="detail.status === 'Assigned' || detail.status === 'Viewed'">
            <VDivider class="my-4" />
            <div>الموظف الحالي: {{ detail.targetEmployeeName || 'غير مسند' }}</div>
            <div>الحالة: {{ statusText(detail.status) }}</div>
          </template>

          <template v-if="detail.status === 'Rejected'">
            <VDivider class="my-4" />
            <VBtn
              color="warning"
              :disabled="!(detail.targetEmployeeId || detail.employeeId)"
              @click="returnOpen = true"
            >
              إعادة للموظف
            </VBtn>
            <div class="text-medium-emphasis mt-2">
              تُعاد لنفس الموظف: {{ detail.targetEmployeeName }}
            </div>
          </template>

          <template v-if="detail.status === 'Completed'">
            <VDivider class="my-4" />
            <VBtn
              color="primary"
              @click="openCustomerProfile(selected, detail)"
            >
              فتح بروفايل الزبون
            </VBtn>
          </template>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            السجل
          </div>
          <div
            v-if="!historyRows.length"
            class="text-medium-emphasis"
          >
            لا يوجد سجل بعد.
          </div>
          <VTimeline
            v-else
            density="compact"
            side="end"
          >
            <VTimelineItem
              v-for="(item, i) in historyRows"
              :key="item.id || item.Id || i"
              size="x-small"
            >
              <div>{{ historyTime(item) }}</div>
              <div>{{ historyEvent(item) }}</div>
              <div v-if="historyActor(item)">
                {{ historyActor(item) }}
              </div>
              <div v-if="historyNote(item)">
                {{ historyNote(item) }}
              </div>
            </VTimelineItem>
          </VTimeline>
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="detail = null; selected = null"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="profileOpen"
      max-width="920"
      scrollable
    >
      <VCard>
        <VCardTitle>بروفايل الزبون</VCardTitle>
        <VCardText>
          <div class="font-weight-bold mb-2">
            بيانات الزبون
          </div>
          <div>الاسم: {{ profileName }}</div>
          <div>الهاتف: {{ profilePhone }}</div>
          <div>المحافظة: {{ profileProvince }}</div>
          <div>العنوان: {{ profileAddress || '—' }}</div>
          <div>رقم البطاقة: {{ profileNationalId || '—' }}</div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            تفاصيل المحل
          </div>
          <div v-if="!latestShop" class="text-medium-emphasis">
            لا توجد بيانات محل بعد.
          </div>
          <div v-else>
            <div>اسم المحل: {{ pick(latestShop, 'shopName', 'ShopName') }}</div>
            <div>نوع النشاط: {{ pick(latestShop, 'shopBusinessType', 'ShopBusinessType') }}</div>
            <div>قيمة البضاعة التقديرية: {{ money(pick(latestShop, 'shopStockEstimatedValue', 'ShopStockEstimatedValue')) }}</div>
            <div>الإيراد اليومي التقديري: {{ money(pick(latestShop, 'estimatedDailyRevenue', 'EstimatedDailyRevenue')) }}</div>
            <div>الطول: {{ pick(latestShop, 'shopLength', 'ShopLength') }} م</div>
            <div>العرض: {{ pick(latestShop, 'shopWidth', 'ShopWidth') }} م</div>
            <div>المساحة: {{ areaText(latestShop) }}</div>
            <VImg
              v-if="shopImageUrl"
              :src="shopImageUrl"
              max-height="180"
              class="mt-3"
              style="cursor: pointer"
              @click="shopImageOpen = true"
            />
            <div v-else class="text-medium-emphasis mt-2">
              لا توجد صورة محل.
            </div>
          </div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            التقييم
          </div>
          <div v-if="!profileEvaluations.length" class="text-medium-emphasis">
            لا يوجد تقييم بعد.
          </div>
          <div
            v-for="item in profileEvaluations"
            :key="pick(item, 'saleId', 'SaleId')"
            class="mb-2"
          >
            <div>{{ pick(item, 'evaluationName', 'EvaluationName') || evaluationLabel(pick(item, 'evaluationLevel', 'EvaluationLevel')) }}</div>
            <div v-if="pick(item, 'evaluationNote', 'EvaluationNote')">
              ملاحظة التقييم: {{ pick(item, 'evaluationNote', 'EvaluationNote') }}
            </div>
          </div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            المبيعات
          </div>
          <div v-if="!profileSales.length" class="text-medium-emphasis">
            لا توجد عمليات بيع بعد.
          </div>
          <div
            v-for="item in profileSales"
            :key="pick(item, 'saleId', 'SaleId')"
            class="mb-2"
          >
            <div>
              <strong v-if="Number(pick(item, 'saleId', 'SaleId')) === Number(currentSaleId(selected, detail))">البيع الحالي</strong>
              <span v-else>مبيعات سابقة</span>
              — بيع #{{ pick(item, 'saleId', 'SaleId') }}
            </div>
            <div>المبلغ: {{ money(pick(item, 'finalSalePrice', 'FinalSalePrice')) }}</div>
            <div v-if="pick(item, 'dailyInstallment', 'DailyInstallment')">
              القسط اليومي: {{ money(pick(item, 'dailyInstallment', 'DailyInstallment')) }}
            </div>
            <div class="text-medium-emphasis">
              التاريخ: {{ formatIraqTime(pick(item, 'date', 'Date')) }}
            </div>
          </div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            الطلبات
          </div>
          <div v-if="!profileRequests.length" class="text-medium-emphasis">
            لا توجد طلبات سابقة.
          </div>
          <div
            v-for="item in profileRequests"
            :key="pick(item, 'id', 'Id')"
            class="mb-2"
          >
            <div>طلب #{{ pick(item, 'id', 'Id') }} — {{ statusText(pick(item, 'status', 'Status')) }}</div>
            <div v-if="pick(item, 'rejectionReason', 'RejectionReason')">
              سبب الرفض: {{ pick(item, 'rejectionReason', 'RejectionReason') }}
            </div>
            <div v-if="pick(item, 'pendingNote', 'PendingNote')">
              ملاحظة التعليق: {{ pick(item, 'pendingNote', 'PendingNote') }}
            </div>
            <div v-if="pick(item, 'returnNote', 'ReturnNote')">
              ملاحظة الإعادة: {{ pick(item, 'returnNote', 'ReturnNote') }}
            </div>
          </div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            السجل
          </div>
          <div
            v-if="!profileHistory.length"
            class="text-medium-emphasis"
          >
            لا يوجد سجل بعد.
          </div>
          <VTimeline
            v-else
            density="compact"
            side="end"
          >
            <VTimelineItem
              v-for="(item, i) in profileHistory"
              :key="item.id || item.Id || `p-${i}`"
              size="x-small"
            >
              <div>{{ historyTime(item) }}</div>
              <div>{{ historyEvent(item) }}</div>
              <div v-if="historyActor(item)">
                {{ historyActor(item) }}
              </div>
              <div v-if="historyNote(item)">
                {{ historyNote(item) }}
              </div>
            </VTimelineItem>
          </VTimeline>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            الملاحظات
          </div>
          <div v-if="!profileNotes.length" class="text-medium-emphasis">
            لا توجد ملاحظات بعد.
          </div>
          <div
            v-for="(item, i) in profileNotes"
            :key="pick(item, 'id', 'Id') || i"
            class="mb-2"
          >
            <div>{{ pick(item, 'authorRole', 'AuthorRole') }} — {{ pick(item, 'authorName', 'AuthorName') }}</div>
            <div>{{ pick(item, 'note', 'Note') }}</div>
            <div class="text-medium-emphasis">
              {{ formatIraqTime(pick(item, 'createdAtUtc', 'CreatedAtUtc')) }}
            </div>
          </div>
          <VTextarea
            v-model="managerNote"
            class="mt-2"
            label="ملاحظة مسؤول المبيعات"
            auto-grow
          />
          <VBtn
            class="mt-2"
            color="primary"
            :loading="busy"
            :disabled="!managerNote.trim()"
            @click="addManagerNote"
          >
            إضافة ملاحظة
          </VBtn>
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="profileOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="shopImageOpen"
      max-width="720"
    >
      <VCard v-if="shopImageUrl">
        <VImg :src="shopImageUrl" />
        <VCardActions>
          <VBtn
            variant="text"
            @click="shopImageOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="returnOpen"
      max-width="420"
    >
      <VCard>
        <VCardTitle>إعادة للموظف</VCardTitle>
        <VCardText>
          <VTextarea
            v-model="returnNote"
            label="ملاحظة الإعادة *"
            auto-grow
          />
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="returnOpen = false"
          >
            رجوع
          </VBtn>
          <VBtn
            color="warning"
            :loading="busy"
            :disabled="!returnNote.trim()"
            @click="sendReturn"
          >
            إعادة
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
