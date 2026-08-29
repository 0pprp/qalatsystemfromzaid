<script setup>
import { getAuthHeaders } from '@/services/tokenService' // تأكد من استيراد المكونات حسب المكتبة المستخدمة
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات التسديدات وقائمة المندوبين (يمكن مشاركة قائمة المندوبين مع العملاء)
const paymentsData = ref([])
const selectPaymentsData = ref({})
const paymentsDataCustomer = ref([])
const delegateList = ref([])
const customerPaymentIDToDelete = ref(null)
const confirmDeletePaymentDialog = ref(false)
const confirmDeleteSelectPaymentDialog = ref(false)
const currentCustomerID = ref(null)
const paymentsDialog = ref(false)

// حالة التحميل
const loading = ref(false)
const loadingSaveNewDate = ref(false)
const newDate = ref('')
const paymentDialog = ref(false)

const paymentDetails = ref({
  customerName: '',
  delegateName: '',
  amountTotalSales: 0,
  amountDaySales: 0,
  receiptsTotal: 0,
  amountRemaining: 0,
  paymentAmount: 0,
  paymentDate: new Date().toLocaleDateString('en-CA'),
})




const filterDateFrom = ref(null)
const filterDateTo = ref(null)

// حساب مجموع مبالغ التسديدات (عمود amountDenar)
const totalPaymentsSum = computed(() =>
  filteredPaymentsData.value.reduce((sum, payment) => sum + (Number(payment.amountDenar) || 0), 0),
)

// حساب عدد التسديدات (عدد السجلات)
const paymentsCount = computed(() => filteredPaymentsData.value.length)

const filteredPaymentsData = computed(() => {
  return paymentsDataCustomer.value.filter(payment => {
    const date = new Date(payment.paymentDate)

    const from = filterDateFrom.value
      ? new Date(filterDateFrom.value) // بدون تعديل
      : null

    const to = filterDateTo.value
      ? new Date(filterDateTo.value)
      : null

    if (to) {
      to.setDate(to.getDate() + 1)
    }

    return (!from || date >= from) && (!to || date < to)
  })
})

function clearDateFilters() {
  filterDateFrom.value = null
  filterDateTo.value = null
}


// دالة فتح Dialog التسديد وملئ البيانات
async function openPaymentDialog(item) {
  try {
    const response = await axios.get(`${apiUrl}Customers/Customers_InfoSimple/${item.customerID}`, { headers: getAuthHeaders() })
    const data = response.data

    console.log(data)
    currentCustomerID.value=data.customerID
    paymentDetails.value.customerName = data.customerName
    paymentDetails.value.delegateName = data.delegateName
    paymentDetails.value.amountTotalSales = data.amountTotalSales || 0
    paymentDetails.value.amountDaySales = data.amountDaySales || 0
    paymentDetails.value.receiptsTotal = data.receiptsTotal || 0
    paymentDetails.value.amountRemaining = data.amountRemaining || 0
    paymentDetails.value.paymentAmount = data.amountDaySales || 0
    paymentDetails.value.paymentDate = new Date().toLocaleDateString('en-CA')
    paymentDialog.value = true
  } catch (error) {
    console.error("Error fetching customer details:", error)
  }
}


async function submitPayment() {
  try {
    if(paymentDetails.value.paymentAmount>=1000){
      if (paymentDetails.value.amountRemaining > 0) {
        if (paymentDetails.value.paymentAmount <= paymentDetails.value.amountRemaining) {
          const authHeader = getAuthHeaders()

          const paymentData = {
            customerID: currentCustomerID.value,
            paymentAmount: paymentDetails.value.paymentAmount,
            paymentDate: paymentDetails.value.paymentDate,
          }

          const response =  await axios.post(`${apiUrl}CustomersPayments/CustomersPayments_Create`, paymentData, { headers: authHeader })
          if(response.data.amountDenar>0){
            paymentDialog.value = false
          }
        } else {
          alert('مبلغ التسديد أكبر من المتبقي.')
        }
      } else {
        alert('هذا العميل مصفر حسابه.')
      }
    }else {
      alert('يجب ان يكون مبلغ التسديد اكبر او يساوي 1000.')
    }
  } catch (error) {
    console.error('Error submitting payment:', error)
  }
}


// فلاتر البحث للتسديدات
const filtersPayments = ref({
  fromDate: '',       // من التاريخ (لتصفية تاريخ التسديد)
  toDate: '',         // إلى التاريخ
  delegateID: null,      // 0 تعني "الجميع"
  textSearch: '',      // نص البحث لاسم العميل (أو رقم الهاتف إن احتجت)
})

const changeDatePayment = ref({
  changeDate: '',
})

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  
  return "لا يوجد"
}

// تعريف عناوين جدول التسديدات
const paymentsHeaders = [
  { title: 'التسديدات', key: 'showReceipt' },
  { title: 'تسديد', key: 'addReceipt' },
  { title: 'حذف', key: 'delete' },
  { title: 'العميل', key: 'customerName' },
  { title: 'المبلغ المسدد', key: 'amountDenar' },
  { title: 'تاريخ التسديد', key: 'paymentDate' },
  { title: 'المندوب', key: 'delegateName' },
  { title: 'الموقع', key: 'location' },
  { title: 'عرض الموقع', key: 'showMap' },
]

const totals = computed(() => [
  {
    icon: 'tabler-check', // إجمالي عدد التسديدات
    value: paymentsData.value.length,
    title: 'إجمالي عدد التسديدات',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-wallet', // مجموع التسديدات
    value: formattedNumber(
      paymentsData.value.reduce((sum, payment) => sum + (Number(payment.amountDenar) || 0), 0),
    ),
    title: 'مجموع التسديدات',
    color: "success",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
])


// دالة جلب قائمة المندوبين (يمكن استخدامها للبحث)
async function fetchDelegates() {
  try {
    const authHeader = getAuthHeaders()

    const response = await axios.get(`${apiUrl}Delegates/Delegates_GetDataAll`, { headers: authHeader })

    delegateList.value = response.data
  } catch (error) {
    console.error(error)
  }
}

// دالة جلب بيانات التسديدات باستخدام الفلاتر
async function fetchPayments() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()

    const fromDate = filtersPayments.value.fromDate || 'null'
    const toDate = filtersPayments.value.toDate || 'null'
    const delegateID = filtersPayments.value.delegateID || 0
    const textSearch = filtersPayments.value.textSearch || 'null'

    const response = await axios.get(
      `${apiUrl}CustomersPayments/CustomersPayments_GetAll/${fromDate}&&${toDate}&&${delegateID}&&${textSearch}`, { headers: authHeader },
    )

    paymentsData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

async function fetchPaymentsCustomer(customerID) {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}CustomersPayments/CustomersPayments_GetByCustomerID/${customerID}`, { headers: authHeader })

    paymentsDataCustomer.value = response.data
  } catch (error) {
    console.error(error)
  }
}

// دالة تصدير بيانات التسديدات إلى Excel
function exportPaymentsToExcel() {
  const dataToExport = paymentsData.value.map(payment => ({
    'اسم العميل': payment.customerName || 'لا يوجد',
    'المبلغ المسدد': payment.amountDenar || 'لا يوجد',
    'تاريخ التسديد': payment.paymentDate ? new Date(payment.paymentDate).toLocaleDateString('en-CA')  : 'لا يوجد',
    'المندوب': payment.delegateName || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Payments")
  XLSX.writeFile(workbook, "payments.xlsx")
}

// دالة فتح Google Maps باستخدام الموقع (كما في الكود السابق)
function openInGoogleMaps(location) {
  if (!location) return
  const url = `https://www.google.com/maps/search/?api=1&query=${location}`

  window.open(url, '_blank')
}

onMounted(() => {
  fetchDelegates()
})

function handleDateChangeFromDate(event) {
  const rawDate = event.target.value

  filters.value.fromDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function handleDateChangeToDate(event) {
  const rawDate = event.target.value

  filters.value.toDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function handleDateChangeChangeDate(event) {
  const rawDate = event.target.value

  changeDatePayment.value.changeDate = new Date(rawDate).toLocaleDateString('en-CA')
}


async function deleteSelectPayment(){
  if (selectPaymentsData.value.length > 0){
    confirmDeleteSelectPaymentDialog.value = true
  }
}

async function deleteSelectReceiptData() {
  try {
    if (selectPaymentsData.value.length > 0) {
      const ids = selectPaymentsData.value
        .map(payment => payment.customerPaymentID)
        .filter(id => id != null)

      const authHeader = getAuthHeaders()

      await axios.request({
        url: `${apiUrl}CustomersPayments/CustomersPayments_DeleteSelect`,
        method: "DELETE",
        data: ids, // ✅ إرسال البيانات داخل body
        headers: {
          ...authHeader,
          "Content-Type": "application/json",
        },
      })

      confirmDeleteSelectPaymentDialog.value = false
      await fetchPayments()
      selectPaymentsData.value = null
    }
  } catch (error) {
    console.error("Error deleting request:", error)
  }
}

async function changeDatePaymentMethod() {
  try {
    if (selectPaymentsData.value.length > 0) {
      loadingSaveNewDate.value = true

      const ids = selectPaymentsData.value
        .map(payment => payment.customerPaymentID)
        .filter(id => id != null)

      const authHeader = getAuthHeaders()

      await axios.put(`${apiUrl}CustomersPayments/CustomersPayments_ChangePaymentDate`, {
        ids: ids,
        newDate: changeDatePayment.value.changeDate,
      }, {
        headers: {
          ...authHeader,
          "Content-Type": "application/json",
        },
      })

      await fetchPayments()
      selectPaymentsData.value = null
      loadingSaveNewDate.value = false
    }
  } catch (error) {
    console.error("Error updating payment dates:", error)
  }
}


// دالة تصدير بيانات العملاء إلى Excel
function exportReceiptToExcel() {
  const dataToExport = filteredPaymentsData.value.map(payment => ({
    'العميل': payment.customerName || 'لا يوجد',
    'التاريخ': payment.paymentDate ? new Date(payment.paymentDate).toLocaleDateString('en-CA')   : 'لا يوجد',
    'التسديد': payment.amountDenar || 0,
    'المندوب': payment.delegateName || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "receipts")
  XLSX.writeFile(workbook, "receipts.xlsx")
}


function deleteReceipt(customerPaymentID) {
  customerPaymentIDToDelete.value = customerPaymentID
  confirmDeletePaymentDialog.value = true
}


async function deleteReceiptData() {
  if (customerPaymentIDToDelete.value) {
    try {
      const authHeader = getAuthHeaders()

      await axios.delete(`${apiUrl}CustomersPayments/CustomersPayments_Delete/${customerPaymentIDToDelete.value}`, { headers: authHeader })
      paymentsData.value = paymentsData.value.filter(sale => sale.customerPaymentID !== customerPaymentIDToDelete.value)
      paymentsDataCustomer.value = paymentsDataCustomer.value.filter(sale => sale.customerPaymentID !== customerPaymentIDToDelete.value)
      confirmDeletePaymentDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}



function openPaymentsDialog(customerID) {
  clearDateFilters()
  currentCustomerID.value = customerID
  fetchPaymentsCustomer(customerID)
  paymentsDialog.value = true
}


const formattedPaymentAmount = computed({
  get() {
    return paymentDetails.value.paymentAmount !== ''
      ? Number(paymentDetails.value.paymentAmount).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    paymentDetails.value.paymentAmount = isNaN(numeric) ? '' : numeric
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
      <!-- فلاتر البحث للتسديدات -->
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            من التاريخ
          </VLabel>
          <AppTextField
            v-model="filtersPayments.fromDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            @input="handleDateChangeFromDate"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            إلى التاريخ
          </VLabel>
          <AppTextField
            v-model="filtersPayments.toDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            @input="handleDateChangeToDate"
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
            v-model="filtersPayments.delegateID"
            :items="delegateList.map(d => ({ title: d.delegateName, value: d.delegateID }))"
            placeholder="اختر المندوب"
            clearable
            prepend-inner-icon="tabler-user"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            اسم العميل
          </VLabel>
          <AppTextField
            v-model="filtersPayments.textSearch"
            placeholder="ادخل اسم العميل"
            clearable
            prepend-inner-icon="tabler-user"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            تغيير التاريخ التسديدات المحددة
          </VLabel>
          <AppTextField
            v-model="changeDatePayment.changeDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            @input="handleDateChangeChangeDate"
          />
        </VCol>

        <VCol
          cols="12"
          class="d-flex flex-wrap gap-2 mt-4"
        >
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            prepend-icon="tabler-search"
            @click="fetchPayments"
          >
            بحث
          </VBtn>
          <VBtn
            color="primary"
            :loading="loadingSaveNewDate"
            :disabled="loadingSaveNewDate"
            prepend-icon="tabler-calendar-check"
            @click="changeDatePaymentMethod"
          >
            حفظ التاريخ الجديد
          </VBtn>
          <VBtn
            color="success"
            prepend-icon="tabler-file-export"
            @click="exportPaymentsToExcel"
          >
            تصدير إلى Excel
          </VBtn>
          <VBtn
            color="error"
            prepend-icon="tabler-trash"
            @click="deleteSelectPayment"
          >
            حذف التسديدات المحددة
          </VBtn>
        </VCol>
      </VRow>
      <!-- عرض بيانات التسديدات -->
      <VRow>
        <VDataTable
          v-model="selectPaymentsData"
          :headers="paymentsHeaders"
          :items="paymentsData"
          :items-per-page="50"
          show-select
          return-object
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <!-- تنسيق الحقول داخل div بعرض 200px -->
          <template #item.customerName="{ item }">
            <div style="inline-size: 200px;">
              {{ item.customerName }}
            </div>
          </template>
          <template #item.amountDenar="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.amountDenar) }}
            </div>
          </template>
          <template #item.paymentDate="{ item }">
            <div style="inline-size: 200px;">
              {{ item.paymentDate ? new Date(item.paymentDate).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>
          <template #item.delegateName="{ item }">
            <div style="inline-size: 200px;">
              {{ item.delegateName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.showReceipt="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                color="primary"
                style="margin-block-end: 0;"
                prepend-icon="tabler-credit-card"
                @click="openPaymentsDialog(item.customerID)"
              >
                التسديدات
              </VBtn>
            </div>
          </template>

          <template #item.addReceipt="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                color="primary"
                style="margin-block-end: 0;"
                prepend-icon="tabler-credit-card"
                @click="openPaymentDialog(item)"
              >
                تسديد
              </VBtn>
            </div>
          </template>

          <template #item.delete="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                color="error"
                style="margin-block-end: 0;"
                prepend-icon="tabler-trash"
                @click="deleteReceipt(item.customerPaymentID)"
              >
                حذف
              </VBtn>
            </div>
          </template>

          <template #item.showMap="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                color="primary"
                style="margin-block-end: 0;"
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
  <VDialog
    v-model="paymentsDialog"
    fullscreen
    hide-overlay
    transition="dialog-bottom-transition"
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
              icon="tabler-credit-card"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              التسديدات
            </h4>
            <span class="text-caption text-medium-emphasis">سجل التسديدات التفصيلي</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          @click="paymentsDialog = false"
        >
          <VIcon
            icon="tabler-x"
            size="24"
          />
        </VBtn>
      </div>

      <VCardText>
        <!-- عرض إجماليات التسديدات بنفس نمط إجماليات بيانات العملاء -->
        <VRow class="mb-4">
          <VCol
            md="6"
            cols="12"
          >
            <VCard class="text-center elevation-1">
              <VLabel
                class="mb-2"
                style="font-size: 14px; margin-block-start: 20px;"
              >
                عدد التسديدات
              </VLabel>
              <VCardText class="text-h6 font-weight-bold">
                {{ paymentsCount }}
              </VCardText>
            </VCard>
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VCard class="text-center elevation-1">
              <VLabel
                class="mb-2"
                style="font-size: 14px; margin-block-start: 20px;"
              >
                مجموع التسديدات
              </VLabel>
              <VCardText class="text-h6 font-weight-bold">
                {{ formattedNumber(totalPaymentsSum) }}
              </VCardText>
            </VCard>
          </VCol>
        </VRow>

        <VRow class="mb-4">
          <VCol
            cols="12"
            md="6"
          >
            <VLabel class="mb-2">
              من تاريخ
            </VLabel>
            <AppTextField
              v-model="filterDateFrom"
              prepend-inner-icon="tabler-calendar"
              type="date"
              clearable
            />
          </VCol>
          <VCol
            cols="12"
            md="6"
          >
            <VLabel class="mb-2">
              إلى تاريخ
            </VLabel>
            <AppTextField
              v-model="filterDateTo"
              prepend-inner-icon="tabler-calendar"
              type="date"
              clearable
            />
          </VCol>
          <VCol
            cols="12"
            class="text-right"
          >
            <VBtn
              color="primary"
              style="margin: 10px;"
              prepend-icon="tabler-refresh"
              @click="clearDateFilters"
            >
              عرض الجميع
            </VBtn>
            <VBtn
              color="success"
              style="margin: 10px;"
              prepend-icon="tabler-file-export"
              @click="exportReceiptToExcel"
            >
              تصدير إلى Excel
            </VBtn>
            <VBtn
              color="error"
              style="margin: 10px;"
              prepend-icon="tabler-x"
              @click="paymentsDialog = false"
            >
              إغلاق
            </VBtn>
          </VCol>
        </VRow>
        <VDataTable
          :headers="[
            { title: 'العميل', key: 'customerName' },
            { title: 'مبلغ التسديد', key: 'amountDenar' },
            { title: 'تاريخ التسديد', key: 'paymentDate' },
            { title: 'المندوب', key: 'delegateName' },
            { title: 'الموقع', key: 'location' },
            { title: 'حذف', key: 'delete' },
            { title: 'عرض على الخريطة', key: 'showMap' },
          ]"
          :items="filteredPaymentsData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
        >
          <template #item.customerName="{ item }">
            <div style="inline-size: 200px;">
              {{ item.customerName }}
            </div>
          </template>
          <template #item.paymentDate="{ item }">
            {{ item.paymentDate ? new Date(item.paymentDate).toLocaleDateString('en-CA') : 'لا يوجد' }}
          </template>
          <template #item.amountDenar="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.amountDenar) }}
            </div>
          </template>
          <template #item.location="{ item }">
            <div style="inline-size: 200px;">
              {{ item.location || 'لا يوجد' }}
            </div>
          </template>
          <template #item.showMap="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                color="primary"
                style="margin-block-end: 0;"
                prepend-icon="tabler-map-pin"
                @click="openInGoogleMaps(item.location)"
              >
                عرض على الخريطة
              </VBtn>
            </div>
          </template>
          <template #item.delete="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                color="error"
                style="margin-block-end: 0;"
                prepend-icon="tabler-trash"
                @click="deleteReceipt(item.customerPaymentID)"
              >
                حذف
              </VBtn>
            </div>
          </template>
        </VDataTable>
      </VCardText>
    </VCard>
  </VDialog>
  <VDialog
    v-model="confirmDeletePaymentDialog"
    max-width="400px"
  >
    <VCard>
      <VCardText>
        <p>هل أنت متأكد من أنك تريد حذف هذا التسديد؟</p>
      </VCardText>
      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="confirmDeletePaymentDialog.value = false"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إلغاء
        </VBtn>
        <VBtn
          color="error"
          @click="deleteReceiptData"
        >
          <VIcon
            icon="tabler-trash"
            class="me-2"
          />
          حذف
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
  <VDialog
    v-model="confirmDeleteSelectPaymentDialog"
    max-width="400px"
  >
    <VCard>
      <VCardText>
        <p>هل أنت متأكد من أنك تريد حذف التسديدات المحددة؟</p>
      </VCardText>
      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="confirmDeleteSelectPaymentDialog.value = false"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إلغاء
        </VBtn>
        <VBtn
          color="error"
          @click="deleteSelectReceiptData"
        >
          <VIcon
            icon="tabler-trash"
            class="me-2"
          />
          حذف
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
  <VDialog
    v-model="paymentDialog"
    max-width="500px"
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
              icon="tabler-credit-card"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              التسديد
            </h4>
            <span class="text-caption text-medium-emphasis">إضافة تسديد جديد</span>
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
        <VForm>
          <VRow>
            <VCol cols="12">
              <VLabel class="mb-2">
                اسم العميل
              </VLabel>
              <VTextField
                v-model="paymentDetails.customerName"
                readonly
                prepend-inner-icon="tabler-user"
              />
            </VCol>

            <VCol cols="12">
              <VLabel class="mb-2">
                اسم المندوب
              </VLabel>
              <VTextField
                v-model="paymentDetails.delegateName"
                readonly
                prepend-inner-icon="tabler-briefcase"
              />
            </VCol>

            <VCol cols="12">
              <VLabel class="mb-2">
                سعر البيع
              </VLabel>
              <VTextField
                :value="formattedNumber(paymentDetails.amountTotalSales)"
                readonly
                prepend-inner-icon="tabler-currency-dollar"
              />
            </VCol>

            <VCol cols="12">
              <VLabel class="mb-2">
                القسط
              </VLabel>
              <VTextField
                :value="formattedNumber(paymentDetails.amountDaySales)"
                readonly
                prepend-inner-icon="tabler-currency-dollar"
              />
            </VCol>

            <VCol cols="12">
              <VLabel class="mb-2">
                الواصل
              </VLabel>
              <VTextField
                :value="formattedNumber(paymentDetails.receiptsTotal)"
                readonly
                prepend-inner-icon="tabler-currency-dollar"
              />
            </VCol>

            <VCol cols="12">
              <VLabel class="mb-2">
                الباقي
              </VLabel>
              <VTextField
                :value="formattedNumber(paymentDetails.amountRemaining)"
                readonly
                prepend-inner-icon="tabler-alert-circle"
              />
            </VCol>

            <VCol cols="12">
              <VLabel class="mb-2">
                مبلغ التسديد
              </VLabel>
              <VTextField
                v-model="formattedPaymentAmount"
                type="text"
                prepend-inner-icon="tabler-currency-dollar"
                @keypress="onNumberInput"
              />
            </VCol>

            <VCol cols="12">
              <VLabel class="mb-2">
                تاريخ التسديد
              </VLabel>
              <VTextField
                v-model="paymentDetails.paymentDate"
                type="date"
                prepend-inner-icon="tabler-calendar"
              />
            </VCol>
          </VRow>
        </VForm>
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
</template>

<style scoped>
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
.v-theme--light :deep(.amt-total-receipts) {
  background-color: #f0fdf4 !important;
  color: #16a34a !important;
  border-color: #dcfce7 !important;
}
.v-theme--light :deep(.amt-total-receipts:hover) {
  background-color: #dcfce7 !important;
}

/* Dark Theme Styling */
.v-theme--dark :deep(.amt-total-receipts) {
  background-color: rgba(22, 163, 74, 0.15) !important;
  color: #4ade80 !important;
  border-color: rgba(74, 222, 128, 0.3) !important;
}
.v-theme--dark :deep(.amt-total-receipts:hover) {
  background-color: rgba(22, 163, 74, 0.25) !important;
}
</style>
