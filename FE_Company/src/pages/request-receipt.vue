<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

const apiUrl = localStorage.getItem('LinkCity')

// بيانات طلبات التسديدات وغيرها من المتغيرات
const paymentsRequestsData = ref([])
const loading = ref(false)
const loadingApproveAll = ref(false)
const loadingDeleteAll = ref(false)
const loadingSame = ref(false)
const selectPaymentsData = ref([])

// باقي المتغيرات والدوال كما في كودك الأصلي ...
const formattedNumber = num => (num ? num.toLocaleString() + "  دع  " : '0')

// ... تعريف الفلاتر والقوائم والحقول ...
const filters = ref({
  textSearch: '',
  delegateID: null,
  paymentDate: '',
})

const delegateList = ref([])

const headers = [
  { title: 'تسديد', key: 'payment' },
  { title: 'حذف', key: 'delete' },
  { title: 'اسم العميل', key: 'customerName' },
  { title: 'تاريخ التسديد', key: 'paymentDate' },
  { title: 'اسم المندوب', key: 'delegateName' },
  { title: 'المبلغ', key: 'amount' },
  { title: 'سعر البيع', key: 'amountTotalSales' },
  { title: 'القسط', key: 'amountDaySales' },
  { title: 'الواصل', key: 'receiptsTotal' },
  { title: 'الباقي', key: 'amountRemaining' },
  { title: 'الموقع', key: 'location' },
  { title: 'عرض الموقع', key: 'showMap' },
]

// حساب الإجماليات
const totals = computed(() => [
  {
    icon: 'tabler-check',
    value: paymentsRequestsData.value.length,
    title: 'عدد الطلبات',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-currency-dollar',
    value: formattedNumber(
      paymentsRequestsData.value.reduce((sum, req) => sum + (req.amount || 0), 0),
    ),
    title: 'مجموع مبالغ التسديد',
    color: "success",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
])

// بيانات نافذة التسديد لطلب مفرد
const paymentDetails = ref({
  customersPaymentsRequestID: 0,
  customerName: '',
  delegateName: '',
  amount: 0,
  amountTotalSales: 0,
  amountDaySales: 0,
  receiptsTotal: 0,
  amountRemaining: 0,
  paymentDate: new Date().toISOString().split('T')[0],
})

const paymentDialog = ref(false)

const confirmDialog = ref({
  show: false,
  message: '',
  confirmFunction: null,
})

// دالة لفتح نافذة التأكيد مع تحديد الرسالة والدالة التي يجب تنفيذها عند التأكيد
function openConfirmDialog(message, confirmFunction) {
  confirmDialog.value.message = message
  confirmDialog.value.confirmFunction = confirmFunction
  confirmDialog.value.show = true
}

// دالة لتنفيذ الدالة المطلوبة عند التأكيد وإغلاق النافذة
async function executeConfirm() {
  if (confirmDialog.value.confirmFunction) {
    await confirmDialog.value.confirmFunction()
  }
  confirmDialog.value.show = false
}

// دوال جلب البيانات من API
async function fetchPaymentRequests() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const { textSearch, delegateID, paymentDate } = filters.value
    const url = `${apiUrl}CustomersPaymentsRequest/CustomersPaymentsRequest_GetAll/${textSearch || 'null'}&&${delegateID || 0}`
    const response = await axios.get(url, { headers: authHeader })

    paymentsRequestsData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

async function fetchDelegates() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Delegates/Delegates_GetDataAll`, { headers: authHeader })

    delegateList.value = response.data
  } catch (error) {
    console.error(error)
  }
}

onMounted(() => {
  fetchPaymentRequests()
  fetchDelegates()
})

// العمليات
async function approveAllRequests() {
  try {
    loadingApproveAll.value = true

    const authHeader = getAuthHeaders()

    await axios.put(`${apiUrl}CustomersPaymentsRequest/CustomersPaymentsRequest_ApproveAll`, {}, { headers: authHeader })
    await fetchPaymentRequests()
  } catch (error) {
    console.error("Error approving all requests:", error)
  } finally {
    loadingApproveAll.value = false
  }
}

async function deleteAllRequests() {
  try {
    loadingDeleteAll.value = true

    const authHeader = getAuthHeaders()

    await axios.delete(`${apiUrl}CustomersPaymentsRequest/CustomersPaymentsRequest_DeleteAll`, { headers: authHeader })
    await fetchPaymentRequests()
  } catch (error) {
    console.error("Error deleting all requests:", error)
  } finally {
    loadingDeleteAll.value = false
  }
}

async function deleteSameRequests() {
  try {
    loadingSame.value = true

    const authHeader = getAuthHeaders()

    await axios.delete(`${apiUrl}CustomersPaymentsRequest/CustomersPaymentsRequest_DeleteSame`, { headers: authHeader })
    await fetchPaymentRequests()
  } catch (error) {
    console.error("Error deleting duplicate requests:", error)
  } finally {
    loadingSame.value = false
  }
}

async function deleteSelectRequests() {
  try {
    if (selectPaymentsData.value.length > 0) {
      const ids = selectPaymentsData.value
        .map(payment => payment.customersPaymentsRequestID)
        .filter(id => id != null)

      const authHeader = getAuthHeaders()

      await axios.request({
        url: `${apiUrl}CustomersPaymentsRequest/CustomersPaymentsRequest_DeleteSelect`,
        method: "DELETE",
        data: ids,
        headers: {
          ...authHeader,
          "Content-Type": "application/json",
        },
      })

      await fetchPaymentRequests()
      selectPaymentsData.value = []
    }
  } catch (error) {
    console.error("Error deleting selected requests:", error)
  }
}


// باقي الدوال مثل فتح نافذة التسديد وحذف طلب مفرد وإرسال بيانات التسديد

function openPaymentDialog(item) {
  paymentDetails.value.customersPaymentsRequestID = item.customersPaymentsRequestID
  paymentDetails.value.customerName = item.customerName
  paymentDetails.value.delegateName = item.delegateName
  paymentDetails.value.amount = item.amount || 0
  paymentDetails.value.amountTotalSales = item.amountTotalSales || 0
  paymentDetails.value.amountDaySales = item.amountDaySales || 0
  paymentDetails.value.receiptsTotal = item.receiptsTotal || 0
  paymentDetails.value.amountRemaining = item.amountRemaining || 0
  paymentDetails.value.paymentDate = filters.value.paymentDate || new Date().toLocaleDateString('en-CA')
  paymentDialog.value = true
}

async function deleteRequest(customersPaymentsRequestID) {
  try {
    const authHeader = getAuthHeaders()

    await axios.delete(`${apiUrl}CustomersPaymentsRequest/CustomersPaymentsRequest_Delete/${customersPaymentsRequestID}`, { headers: authHeader })
    paymentsRequestsData.value = paymentsRequestsData.value.filter(req => req.customersPaymentsRequestID !== customersPaymentsRequestID)
  } catch (error) {
    console.error("Error deleting request:", error)
  }
}

async function submitPayment() {
  try {
    const authHeader = getAuthHeaders()

    await axios.put(`${apiUrl}CustomersPaymentsRequest/CustomersPaymentsRequest_Approve/${paymentDetails.value.customersPaymentsRequestID}`, {}, { headers: authHeader })
    paymentDialog.value = false
    paymentsRequestsData.value = paymentsRequestsData.value.filter(req => req.customersPaymentsRequestID !== paymentDetails.value.customersPaymentsRequestID)
  } catch (error) {
    console.error("Error processing payment:", error)
  }
}

function exportToExcel() {
  const dataToExport = paymentsRequestsData.value.map(req => ({
    'اسم العميل': req.customerName,
    'تاريخ التسديد': req.paymentDate,
    'اسم المندوب': req.delegateName,
    'المبلغ': req.amount,
    'سعر البيع': req.amountTotalSales,
    'القسط': req.amountDaySales,
    'الواصل': req.receiptsTotal,
    'الباقي': req.amountRemaining,
    'الموقع': req.location,
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "PaymentRequests")
  XLSX.writeFile(workbook, "PaymentRequests.xlsx")
}

async function handlePaymentDate(event) {
  try {
    const rawDate = event.target.value
    const authHeader = getAuthHeaders()

    await axios.put(`${apiUrl}CustomersPaymentsRequest/CustomersPaymentsRequest_ChangeDate/${new Date(rawDate).toLocaleDateString('en-CA')}`, {}, { headers: authHeader })
    await fetchPaymentRequests()
  } catch (error) {
    console.error("Error processing payment:", error)
  }
}


const formattedPaymentAmount = computed({
  get() {
    return paymentDetails.value.amount !== ''
      ? Number(paymentDetails.value.amount).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    paymentDetails.value.amount = isNaN(numeric) ? '' : numeric
  },
})

const onNumberInput = e => {
  const key = e.key
  const value = e.target.value
  const cursorPosition = e.target.selectionStart

  const isNumber = /\d/.test(key)
  const isMinus = key === '-' && cursorPosition === 0 && !value.includes('-')

  if (!isNumber && !isMinus) {
    e.preventDefault()
  }
}


// دالة فتح Google Maps باستخدام الموقع (كما في الكود السابق)
function openInGoogleMaps(location) {
  if (!location) return
  const url = `https://www.google.com/maps/search/?api=1&query=${location}`

  window.open(url, '_blank')
}
</script>

<template>
  <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, index) in totals"
      :key="index"
      cols="12"
      sm="6"
      md="3"
    >
      <ModernStatCard
        :title="card.title"
        :value="card.value"
        :icon="card.icon"
        :color="card.color"
        :gradient="card.gradient"
      />
    </VCol>
  </VRow>
  <VCard class="pa-10">
    <VForm>
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            ابحث عن اسم العميل
          </VLabel>
          <AppTextField
            v-model="filters.textSearch"
            prepend-inner-icon="tabler-user"
            placeholder="ابحث عن اسم العميل"
            clearable
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            المندوب
          </VLabel>
          <VAutocomplete
            v-model="filters.delegateID"
            prepend-inner-icon="tabler-user"
            :items="delegateList.map(d => ({ title: d.delegateName, value: d.delegateID }))"
            placeholder="اختر المندوب"
            clearable
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            تاريخ التسديد
          </VLabel>
          <AppTextField
            v-model="filters.paymentDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            placeholder="اختر التاريخ"
            clearable
            @input="handlePaymentDate"
          />
        </VCol>
        <VCol
          cols="12"
          class="d-flex flex-wrap gap-2 mt-8"
        >
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            prepend-icon="tabler-search"
            @click="fetchPaymentRequests"
          >
            بحث
          </VBtn>
          <VBtn
            color="success"
            :loading="loadingApproveAll"
            prepend-icon="tabler-check"
            @click="openConfirmDialog('هل أنت متأكد من الموافقة على جميع الطلبات؟', approveAllRequests)"
          >
            الموافقة على الجميع
          </VBtn>
          <VBtn
            color="error"
            :loading="loadingDeleteAll"
            prepend-icon="tabler-x"
            @click="openConfirmDialog('هل أنت متأكد من حذف جميع الطلبات؟', deleteAllRequests)"
          >
            حذف الجميع
          </VBtn>
          <VBtn
            color="#f38282"
            style="background-color: #f38282;"
            :loading="loadingSame"
            prepend-icon="tabler-x"
            @click="openConfirmDialog('هل أنت متأكد من حذف الطلبات المحددة؟', deleteSelectRequests)"
          >
            حذف المحدد
          </VBtn>
          <VBtn
            color="#eaaeae"
            style="background-color: #eaaeae;"
            :loading="loadingSame"
            prepend-icon="tabler-repeat"
            @click="openConfirmDialog('هل أنت متأكد من حذف الطلبات المكررة؟', deleteSameRequests)"
          >
            حذف المكرر
          </VBtn>
          <VBtn
            color="success"
            prepend-icon="tabler-file-export"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VCol>
      </VRow>

      <VRow>
        <VDataTable
          v-model="selectPaymentsData"
          show-select
          return-object
          :headers="headers"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          :items="paymentsRequestsData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <template #item.paymentDate="{ item }">
            <div>
              {{ filters.paymentDate ? filters.paymentDate : (item.paymentDate ? new Date(item.paymentDate).toLocaleDateString('en-CA') : 'لا يوجد') }}
            </div>
          </template>
          <template #item.customerName="{ item }">
            <div>
              {{ item.customerName }}
            </div>
          </template>
          <template #item.amount="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.amount) }}
            </div>
          </template>
          <template #item.amountTotalSales="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.amountTotalSales) }}
            </div>
          </template>
          <template #item.amountDaySales="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.amountDaySales) }}
            </div>
          </template>
          <template #item.receiptsTotal="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.receiptsTotal) }}
            </div>
          </template>
          <template #item.amountRemaining="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.amountRemaining) }}
            </div>
          </template>
          <template #item.payment="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                style="margin-block-end: 10px;"
                prepend-icon="tabler-check"
                color="primary"
                @click="openPaymentDialog(item)"
              >
                تسديد
              </VBtn>
            </div>
          </template>
          <template #item.delete="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                style="margin-block-end: 10px;"
                color="error"
                prepend-icon="tabler-trash"
                @click="deleteRequest(item.customersPaymentsRequestID)"
              >
                حذف
              </VBtn>
            </div>
          </template>
          <template #item.showMap="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                color="primary"
                style="margin-block-end: 10px;"
                prepend-icon="tabler-map-pin"
                @click="openInGoogleMaps(item.location)"
              >
                عرض على الخريطة
              </VBtn>
            </div>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>

  <!-- نافذة التسديد -->
  <VDialog
    v-model="paymentDialog"
    max-width="500px"
    content-class="modern-dialog"
  >
    <VCard>
      <div class="dialog-header pa-4 d-flex align-center justify-space-between">
        <div class="d-flex align-center gap-3">
          <VAvatar
            color="primary"
            variant="tonal"
            rounded
            size="48"
          >
            <VIcon
              icon="tabler-check"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              تسديد طلب
            </h4>
            <span class="text-caption text-medium-emphasis">تأكيد عملية التسديد</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          @click="paymentDialog = false"
        >
          <VIcon
            icon="tabler-x"
            size="24"
          />
        </VBtn>
      </div>
      <VCardText>
        <VRow>
          <VCol cols="12">
            <VLabel class="mb-2">
              اسم العميل
            </VLabel>
            <AppTextField
              v-model="paymentDetails.customerName"
              readonly
              prepend-inner-icon="tabler-user"
            />
          </VCol>
          <VCol cols="12">
            <VLabel class="mb-2">
              اسم المندوب
            </VLabel>
            <AppTextField
              v-model="paymentDetails.delegateName"
              prepend-inner-icon="tabler-briefcase"
              readonly
            />
          </VCol>
          <VCol cols="12">
            <VLabel class="mb-2">
              سعر البيع
            </VLabel>
            <AppTextField
              prepend-inner-icon="tabler-currency-dollar"
              :value="formattedNumber(paymentDetails.amountTotalSales)"
              readonly
            />
          </VCol>
          <VCol cols="12">
            <VLabel class="mb-2">
              القسط
            </VLabel>
            <AppTextField
              prepend-inner-icon="tabler-currency-dollar"
              :value="formattedNumber(paymentDetails.amountDaySales)"
              readonly
            />
          </VCol>
          <VCol cols="12">
            <VLabel class="mb-2">
              الواصل
            </VLabel>
            <AppTextField
              prepend-inner-icon="tabler-currency-dollar"
              :value="formattedNumber(paymentDetails.receiptsTotal)"
              readonly
            />
          </VCol>
          <VCol cols="12">
            <VLabel class="mb-2">
              الباقي
            </VLabel>
            <AppTextField
              prepend-inner-icon="tabler-alert-circle"
              :value="formattedNumber(paymentDetails.amountRemaining)"
              readonly
            />
          </VCol>
          <VCol cols="12">
            <VLabel class="mb-2">
              مبلغ التسديد
            </VLabel>
            <AppTextField
              v-model="formattedPaymentAmount"
              type="text"
              prepend-inner-icon="tabler-currency-dollar"
              readonly
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol cols="12">
            <VLabel class="mb-2">
              تاريخ التسديد
            </VLabel>
            <AppTextField
              v-model="paymentDetails.paymentDate"
              prepend-inner-icon="tabler-calendar"
              readonly
            />
          </VCol>
        </VRow>
      </VCardText>
      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="paymentDialog = false"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          @click="submitPayment"
        >
          <VIcon
            icon="tabler-check"
            class="me-2"
          />
          تسديد
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- نافذة رسالة التأكيد العامة -->
  <VDialog
    v-model="confirmDialog.show"
    max-width="400px"
    content-class="modern-dialog"
  >
    <VCard>
      <div class="dialog-header pa-4 d-flex align-center justify-space-between">
        <div class="d-flex align-center gap-3">
          <VAvatar
            color="error"
            variant="tonal"
            rounded
            size="48"
          >
            <VIcon
              icon="tabler-alert-triangle"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              تأكيد
            </h4>
            <span class="text-caption text-medium-emphasis">يرجى تأكيد الإجراء</span>
          </div>
        </div>
      </div>
      <VCardText>
        <p>{{ confirmDialog.message }}</p>
      </VCardText>
      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="confirmDialog.show = false"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إلغاء
        </VBtn>
        <VBtn
          color="primary"
          @click="executeConfirm"
        >
          <VIcon
            icon="tabler-check"
            class="me-2"
          />
          موافق
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>

<style scoped>
.v-btn {
  margin-block-start: 10px;
}

.v-btn + .v-btn {
  margin-inline-start: 10px;
}

.text-right {
  text-align: end;
}

.d-flex {
  display: flex;
}

.align-center {
  align-items: center;
}

.justify-end {
  justify-content: flex-end;
}

/* Premium numeric badges style */
:deep(.premium-amount) {
  display: inline-block !important;
  min-width: 100px !important;
  padding: 6px 12px !important;
  border-radius: 8px !important;
  font-weight: 700 !important;
  font-size: 0.95rem !important;
  text-align: center !important;
  font-family: 'Cairo', sans-serif !important;
  letter-spacing: 0.5px !important;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.04) !important;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1) !important;
  border: 1px solid transparent !important;
}

:deep(.premium-amount:hover) {
  transform: translateY(-1px) !important;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.08) !important;
}

/* Light Theme Styling */
.v-theme--light :deep(.amt-installment) {
  background-color: #f0f7ff !important;
  color: #0284c7 !important;
  border-color: #e0f2fe !important;
}
.v-theme--light :deep(.amt-installment:hover) {
  background-color: #e0f2fe !important;
}

.v-theme--light :deep(.amt-paid-yesterday) {
  background-color: #ecfdf5 !important;
  color: #059669 !important;
  border-color: #d1fae5 !important;
}
.v-theme--light :deep(.amt-paid-yesterday:hover) {
  background-color: #d1fae5 !important;
}

.v-theme--light :deep(.amt-total-sales) {
  background-color: #f8fafc !important;
  color: #475569 !important;
  border-color: #e2e8f0 !important;
}
.v-theme--light :deep(.amt-total-sales:hover) {
  background-color: #f1f5f9 !important;
}

.v-theme--light :deep(.amt-total-receipts) {
  background-color: #f0fdf4 !important;
  color: #16a34a !important;
  border-color: #dcfce7 !important;
}
.v-theme--light :deep(.amt-total-receipts:hover) {
  background-color: #dcfce7 !important;
}

.v-theme--light :deep(.amt-remaining) {
  background-color: #fff1f2 !important;
  color: #e11d48 !important;
  border-color: #ffe4e6 !important;
  font-weight: 800 !important;
}
.v-theme--light :deep(.amt-remaining:hover) {
  background-color: #ffe4e6 !important;
}

/* Dark Theme Styling */
.v-theme--dark :deep(.amt-installment) {
  background-color: rgba(2, 132, 199, 0.15) !important;
  color: #38bdf8 !important;
  border-color: rgba(56, 189, 248, 0.3) !important;
}
.v-theme--dark :deep(.amt-installment:hover) {
  background-color: rgba(2, 132, 199, 0.25) !important;
}

.v-theme--dark :deep(.amt-paid-yesterday) {
  background-color: rgba(5, 150, 105, 0.15) !important;
  color: #34d399 !important;
  border-color: rgba(52, 211, 153, 0.3) !important;
}
.v-theme--dark :deep(.amt-paid-yesterday:hover) {
  background-color: rgba(5, 150, 105, 0.25) !important;
}

.v-theme--dark :deep(.amt-total-sales) {
  background-color: rgba(71, 85, 105, 0.15) !important;
  color: #cbd5e1 !important;
  border-color: rgba(203, 213, 225, 0.2) !important;
}
.v-theme--dark :deep(.amt-total-sales:hover) {
  background-color: rgba(71, 85, 105, 0.25) !important;
}

.v-theme--dark :deep(.amt-total-receipts) {
  background-color: rgba(22, 163, 74, 0.15) !important;
  color: #4ade80 !important;
  border-color: rgba(74, 222, 128, 0.3) !important;
}
.v-theme--dark :deep(.amt-total-receipts:hover) {
  background-color: rgba(22, 163, 74, 0.25) !important;
}

.v-theme--dark :deep(.amt-remaining) {
  background-color: rgba(225, 29, 72, 0.15) !important;
  color: #fb7185 !important;
  border-color: rgba(251, 113, 133, 0.3) !important;
  font-weight: 800 !important;
}
.v-theme--dark :deep(.amt-remaining:hover) {
  background-color: rgba(225, 29, 72, 0.25) !important;
}
</style>
