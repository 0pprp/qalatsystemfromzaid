<script setup>
import { getAuthHeaders } from '@/services/tokenService' // تأكد من استيراد المكونات حسب المكتبة المستخدمة
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات العملاء وقائمة المندوبين
const customersData = ref([])

// حالة التحميل
const loading = ref(false)
const customerPaymentIDToDelete = ref(null)
const confirmDeletePaymentDialog = ref(false)


// فلاتر البحث: فقط من التاريخ وإلى التاريخ
const filters = ref({
  fromDate: '',  // التاريخ الابتدائي لتصفية المصفرين (يستخدم تاريخ آخر تسديد)
  toDate: '',     // التاريخ النهائي لتصفية المصفرين
})

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  
  return "لا يوجد"
}


const filterDateFrom = ref(null)
const filterDateTo = ref(null)

// حساب مجموع مبالغ التسديدات (عمود amountDenar)
const totalPaymentsSum = computed(() =>
  filteredPaymentsData.value.reduce((sum, payment) => sum + (Number(payment.amountDenar) || 0), 0),
)

// حساب عدد التسديدات (عدد السجلات)
const paymentsCount = computed(() => filteredPaymentsData.value.length)


const filteredPaymentsData = computed(() => {
  return paymentsData.value.filter(payment => {
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

// تعريف عناوين جدول العملاء
const headers = [
  { title: 'التسديدات', key: 'showReceipt' },
  { title: 'العميل', key: 'customerName' },
  { title: 'المندوب', key: 'delegateName' },
  { title: 'المباعة', key: 'itemsNames' },
  { title: 'التاريخ', key: 'dateSaleDevice' },
  { title: 'سعر البيع', key: 'amountTotalSales' },
  { title: 'سعر الشراء', key: 'costTotalSales' },
  { title: 'القسط', key: 'amountDaySales' },
  { title: 'الواصل', key: 'receiptsTotal' },
  { title: 'الباقي', key: 'amountRemaining' },
  { title: 'نسبة التسديد', key: 'receiptRateDevice' },
  { title: 'عدد التسديدات', key: 'countReceiptDevice' },
  { title: 'عدد الأيام', key: 'numberOfDayDevice' },
  { title: 'تاريخ اخر تسديد', key: 'lastPaymentDate' },
  { title: 'العنوان', key: 'address' },
  { title: 'الهاتف', key: 'phoneNumber' },
  { title: 'اسم المحل', key: 'shopName' },
  { title: 'اقرب نقطة', key: 'nearestFunctionPoint' },
  { title: 'اسم الجابي', key: 'receiptName' },
  { title: 'اسم البائع', key: 'saleName' },
  { title: 'الملاحظات', key: 'notes' },
]

// إجماليات بيانات العملاء
// إجماليات بيانات العملاء
const totals = computed(() => [
  {
    icon: 'tabler-users', // عدد العملاء
    value: customersData.value.length,
    title: 'عدد العملاء',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-cash', // إجمالي سعر البيع
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.amountTotalSales || 0), 0),
    ),
    title: 'إجمالي سعر البيع',
    color: "info",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
  {
    icon: 'tabler-shopping-cart', // إجمالي سعر الشراء
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.costTotalSales || 0), 0),
    ),
    title: 'إجمالي سعر الشراء',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-credit-card', // إجمالي القسط
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.amountDaySales || 0), 0),
    ),
    title: 'إجمالي القسط',
    color: "secondary",
    gradient: "linear-gradient(135deg, #667db6 0%, #0082c8 100%, #0082c8 100%, #667db6 100%)",
  },
  {
    icon: 'tabler-check', // إجمالي الواصل
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.receiptsTotal || 0), 0),
    ),
    title: 'إجمالي الواصل',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
  {
    icon: 'tabler-alert-circle', // إجمالي المبلغ المتبقي
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.amountRemaining || 0), 0),
    ),
    title: 'إجمالي المبلغ المتبقي',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
])


// متغيرات Dialog التسديدات وبياناتها
const paymentsDialog = ref(false)
const paymentsData = ref([])
const currentCustomerID = ref(null)

// دالة جلب بيانات العملاء بناءً على التاريخ (المصفرين)
async function fetchCustomers() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()

    // تأكد من تمرير التاريخين بالشكل المطلوب (يمكن تعديل الصيغة حسب متطلبات الـ API)
    const fromDate = filters.value.fromDate || 'null'
    const toDate = filters.value.toDate || 'null'
    const response = await axios.get(`${apiUrl}Customers/Customers_GetAllZero/${fromDate}&&${toDate}`, { headers: authHeader })

    customersData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير بيانات العملاء إلى Excel
function exportToExcel() {
  const dataToExport = customersData.value.map(customer => ({
    'العميل': customer.customerName || 'لا يوجد',
    'المباعة': customer.itemsNames || 'لا يوجد',
    'التاريخ': customer.dateSaleDevice ? new Date(customer.dateSaleDevice).toLocaleDateString('en-CA') : 'لا يوجد',
    'سعر البيع': customer.amountTotalSales || 'لا يوجد',
    'سعر الشراء': customer.costTotalSales || 'لا يوجد',
    'القسط': customer.amountDaySales || 'لا يوجد',
    'الواصل': customer.receiptsTotal || 'لا يوجد',
    'الباقي': customer.amountRemaining || 'لا يوجد',
    'نسبة التسديد': customer.receiptRateDevice || 'لا يوجد',
    'عدد التسديدات': customer.countReceiptDevice || 'لا يوجد',
    'عدد الأيام': customer.numberOfDayDevice || 'لا يوجد',
    'تاريخ اخر تسديد': customer.lastPaymentDate ? new Date(customer.lastPaymentDate).toLocaleDateString('en-CA') : 'لا يوجد',
    'العنوان': customer.address || 'لا يوجد',
    'الهاتف': customer.phoneNumber || 'لا يوجد',
    'اسم المحل': customer.shopName || 'لا يوجد',
    'اقرب نقطة': customer.nearestFunctionPoint || 'لا يوجد',
    'المندوب': customer.delegateName || 'لا يوجد',
    'اسم الجابي': customer.receiptName || 'لا يوجد',
    'اسم البائع': customer.saleName || 'لا يوجد',
    'الملاحظات': customer.notes || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Customers")
  XLSX.writeFile(workbook, "customers.xlsx")
}

// دالة جلب بيانات التسديدات لعميل معين
async function fetchPayments(customerID) {
  try {
    const authHeader = getAuthHeaders()

    const response = await axios.get(`${apiUrl}CustomersPayments/CustomersPayments_GetByCustomerID/${customerID}`, { headers: authHeader })

    paymentsData.value = response.data
  } catch (error) {
    console.error(error)
  }
}

// دالة فتح Dialog التسديدات
function openPaymentsDialog(customerID) {
  clearDateFilters()
  currentCustomerID.value = customerID
  fetchPayments(customerID)
  paymentsDialog.value = true
}

// دالة فتح Google Maps باستخدام الموقع
function openInGoogleMaps(location) {
  if (!location) return
  const url = `https://www.google.com/maps/search/?api=1&query=${location}`

  window.open(url, '_blank')
}

function handleDateChangeFromDate(event) {
  const rawDate = event.target.value

  filters.value.fromDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function handleDateChangeToDate(event) {
  const rawDate = event.target.value

  filters.value.toDate = new Date(rawDate).toLocaleDateString('en-CA')
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
      confirmDeletePaymentDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}

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

// دالة فتح Dialog التسديد وملئ البيانات
function openPaymentDialog(item) {
  currentCustomerID.value = item.customerID
  paymentDetails.value.customerName = item.customerName
  paymentDetails.value.delegateName = item.delegateName
  paymentDetails.value.amountTotalSales = item.amountTotalSales || 0
  paymentDetails.value.amountDaySales = item.amountDaySales || 0
  paymentDetails.value.receiptsTotal = item.receiptsTotal || 0
  paymentDetails.value.amountRemaining = item.amountRemaining || 0
  paymentDetails.value.paymentAmount = item.amountDaySales || 0
  paymentDetails.value.paymentDate =  new Date().toLocaleDateString('en-CA')
  paymentDialog.value = true
}

async function submitPayment() {
  try {
    if (paymentDetails.value.amountRemaining > 0) {
      if (paymentDetails.value.paymentAmount <= paymentDetails.value.amountRemaining) {
        const authHeader = getAuthHeaders()

        const paymentData = {
          customerID: currentCustomerID.value,
          paymentAmount: paymentDetails.value.paymentAmount,
          paymentDate: paymentDetails.value.paymentDate,
        }

        const response =  await axios.post(`${apiUrl}CustomersPayments/CustomersPayments_Create`, paymentData, { headers: authHeader })
        const customer = customersData.value.find(customer => customer.customerID === currentCustomerID.value)
        if (customer) {
          customer.receiptsTotal = parseFloat(customer.receiptsTotal) + parseFloat(paymentDetails.value.paymentAmount)
          customer.amountRemaining = parseFloat(customer.amountRemaining) - parseFloat(paymentDetails.value.paymentAmount)
          customersData.value = [...customersData.value]
        }
        if(response.data.amountDenar>0){
          paymentDialog.value = false
        }
      } else {
        alert('مبلغ التسديد أكبر من المتبقي.')
      }
    } else {
      alert('هذا العميل مصفر حسابه.')
    }
  } catch (error) {
    console.error('Error submitting payment:', error)
  }
}


function exportReceiptToExcel() {
  const dataToExport = paymentsData.value.map(payment => ({
    'العميل': payment.customerName || 'لا يوجد',
    'التاريخ': payment.paymentDate ?  new Date(payment.paymentDate).toLocaleDateString('en-CA')  : 'لا يوجد',
    'التسديد': payment.amountDenar || 0,
    'المندوب': payment.delegateName || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "receipts")
  XLSX.writeFile(workbook, "receipts.xlsx")
}
</script>

<template>
  <!-- إحصائيات -->
  <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, index) in totals"
      :key="index"
      cols="12"
      sm="6"
      md="2"
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
      <!-- فلاتر البحث: عرض حقلي التاريخ فقط -->
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            من التاريخ
          </VLabel>
          <AppTextField
            v-model="filters.fromDate"
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
            v-model="filters.toDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            @input="handleDateChangeToDate"
          />
        </VCol>
        <VRow style="margin-block-start: 42px;margin-inline-end: 10px;">
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            prepend-icon="tabler-search"
            style="margin-inline-start: 20px;"
            @click="fetchCustomers"
          >
            بحث
          </VBtn>
          <VBtn
            color="success"
            style="margin-inline-start: 20px;"
            prepend-icon="tabler-file-export"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VRow>
      </VRow>

      <!-- عرض بيانات العملاء -->
      <VRow>
        <VDataTable
          :headers="headers"
          :items="customersData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          class="text-no-wrap custom-data-table"
        >
          <!-- تنسيق الحقول داخل div بعرض 200px -->
          <template #item.customerName="{ item }">
            <div>
              {{ item.customerName }}
            </div>
          </template>
          <template #item.itemsNames="{ item }">
            <div>
              {{ item.itemsNames || 'لا يوجد' }}
            </div>
          </template>
          <template #item.dateSaleDevice="{ item }">
            <div>
              {{ item.dateSaleDevice ? new Date(item.dateSaleDevice).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>
          <template #item.amountTotalSales="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.amountTotalSales) }}
            </div>
          </template>
          <template #item.costTotalSales="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.costTotalSales) }}
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
          <template #item.receiptRateDevice="{ item }">
            <div>
              {{ item.receiptRateDevice || 'لا يوجد' }}
            </div>
          </template>
          <template #item.countReceiptDevice="{ item }">
            <div>
              {{ item.countReceiptDevice !== undefined ? item.countReceiptDevice : 'لا يوجد' }}
            </div>
          </template>
          <template #item.numberOfDayDevice="{ item }">
            <div>
              {{ item.numberOfDayDevice !== undefined ? item.numberOfDayDevice : 'لا يوجد' }}
            </div>
          </template>
          <template #item.lastPaymentDate="{ item }">
            <div>
              {{ item.lastPaymentDate ? new Date(item.lastPaymentDate).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>
          <template #item.address="{ item }">
            <div>
              {{ item.address || 'لا يوجد' }}
            </div>
          </template>
          <template #item.phoneNumber="{ item }">
            <div>
              {{ item.phoneNumber || 'لا يوجد' }}
            </div>
          </template>
          <template #item.shopName="{ item }">
            <div>
              {{ item.shopName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.nearestFunctionPoint="{ item }">
            <div>
              {{ item.nearestFunctionPoint || 'لا يوجد' }}
            </div>
          </template>
          <template #item.delegateName="{ item }">
            <div>
              {{ item.delegateName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.receiptName="{ item }">
            <div>
              {{ item.receiptName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.saleName="{ item }">
            <div>
              {{ item.saleName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.notes="{ item }">
            <div>
              {{ item.notes || 'لا يوجد' }}
            </div>
          </template>
          <template #item.showReceipt="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                color="primary"
                style="margin-block-end: 0;"
                prepend-icon="tabler-receipt"
                @click="openPaymentsDialog(item.customerID)"
              >
                التسديدات
              </VBtn>
            </div>
          </template>
        </VDataTable>
      </VRow>
    </VForm>

    <VDialog
      v-model="paymentsDialog"
      fullscreen
      hide-overlay
      transition="dialog-bottom-transition"
      content-class="modern-dialog-fullscreen"
    >
      <VCard>
        <div class="dialog-header pa-4 d-flex align-center justify-space-between bg-primary">
          <div class="d-flex align-center gap-3">
            <VIcon
              icon="tabler-receipt"
              color="white"
              size="28"
            />
            <div>
              <h4 class="text-h6 font-weight-bold text-white">
                التسديدات
              </h4>
              <span class="text-caption text-white">عرض وإدارة تسديدات العميل</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="white"
            @click="paymentsDialog = false"
          >
            <VIcon
              icon="tabler-x"
              size="24"
            />
          </VBtn>
        </div>
        <VCardText class="pa-6">
          <!-- إجماليات -->
          <VRow class="mb-4">
            <VCol
              md="6"
              cols="12"
            >
              <ModernStatCard
                title="عدد التسديدات"
                :value="paymentsCount"
                icon="tabler-hash"
                color="info"
                gradient="linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)"
              />
            </VCol>
            <VCol
              md="6"
              cols="12"
            >
              <ModernStatCard
                title="مجموع التسديدات"
                :value="formattedNumber(totalPaymentsSum)"
                icon="tabler-sum"
                color="success"
                gradient="linear-gradient(135deg, #11998e 0%, #38ef7d 100%)"
              />
            </VCol>
          </VRow>

          <!-- فلاتر التاريخ -->
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

          <!-- جدول التسديدات -->
          <!-- جدول التسديدات -->
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
            class="text-no-wrap custom-data-table"
          >
            <template #item.customerName="{ item }">
              <div>{{ item.customerName }}</div>
            </template>
            <template #item.paymentDate="{ item }">
              {{ item.paymentDate ? new Date(item.paymentDate).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </template>
            <template #item.amountDenar="{ item }">
              {{ formattedNumber(item.amountDenar) || 0 }}
            </template>
            <template #item.location="{ item }">
              <div>{{ item.location || 'لا يوجد' }}</div>
            </template>

            <template #item.showMap="{ item }">
              <div class="d-flex gap-1">
                <VBtn
                  color="primary"
                  prepend-icon="tabler-map-pin"
                  style="margin-block-end: 0;"
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
                  prepend-icon="tabler-trash"
                  style="margin-block-end: 0;"
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
      content-class="modern-dialog"
    >
      <VCard class="pa-2">
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
                تأكيد الحذف
              </h4>
              <span class="text-caption text-medium-emphasis">هل أنت متأكد من أنك تريد حذف هذا التسديد؟</span>
            </div>
          </div>
        </div>
        <VCardText>
          <p class="mb-0">
            لا يمكن التراجع عن هذا الإجراء (للمسؤولين فقط).
          </p>
        </VCardText>
        <VCardActions class="justify-end gap-3 pa-4">
          <VBtn
            variant="tonal"
            color="secondary"
            @click="confirmDeletePaymentDialog = false"
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
      v-model="paymentDialog"
      max-width="500px"
      content-class="modern-dialog"
    >
      <VCard class="pa-2">
        <div class="dialog-header pa-4 d-flex align-center justify-space-between bg-primary text-white">
          <div class="d-flex align-center gap-3">
            <VAvatar
              color="white"
              variant="tonal"
              rounded
              size="40"
            >
              <VIcon
                icon="tabler-credit-card"
                size="24"
                class="text-white"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold text-white">
                التسديد
              </h4>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="white"
            @click="paymentDialog = false"
          >
            <VIcon
              icon="mdi-close"
              size="24"
            />
          </VBtn>
        </div>

        <VCardText class="pa-4">
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
                  prepend-inner-icon="tabler-credit-card"
                />
              </VCol>
              <VCol cols="12">
                <VLabel class="mb-2">
                  الواصل
                </VLabel>
                <VTextField
                  :value="formattedNumber(paymentDetails.receiptsTotal)"
                  readonly
                  prepend-inner-icon="tabler-chart-pie"
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
                  v-model="paymentDetails.paymentAmount"
                  type="number"
                  prepend-inner-icon="tabler-currency-dollar"
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
  </VCard>
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
