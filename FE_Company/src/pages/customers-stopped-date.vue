<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import ModernStatCard from "@/components/ModernStatCard.vue"
import axios from 'axios'
import { computed, ref } from 'vue'
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات العملاء
const customersData = ref([])

// حالة التحميل
const loading = ref(false)

const defaultToDate = new Date().toLocaleDateString('en-CA')
const oneYearAgo = new Date()
oneYearAgo.setDate(oneYearAgo.getDate() - 365)
const defaultFromDate = oneYearAgo.toLocaleDateString('en-CA')

// فلاتر البحث حسب تاريخ التسديد
const filters = ref({
  fromDate: defaultFromDate,
  toDate: defaultToDate,
})

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  return "لا يوجد"
}

// تعريف عناوين الجدول بدون الأزرار
const headers = [
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
const totals = computed(() => [
  {
    icon: 'tabler-user',
    value: customersData.value.length,
    title: 'عدد العملاء',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-wallet',
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.amountTotalSales || 0), 0),
    ),
    title: 'إجمالي سعر البيع',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
  {
    icon: 'tabler-wallet',
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.costTotalSales || 0), 0),
    ),
    title: 'إجمالي سعر الشراء',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-credit-card',
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.amountDaySales || 0), 0),
    ),
    title: 'إجمالي القسط',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
  {
    icon: 'tabler-currency-dollar',
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.receiptsTotal || 0), 0),
    ),
    title: 'إجمالي الواصل',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
  {
    icon: 'tabler-currency-dollar',
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.amountRemaining || 0), 0),
    ),
    title: 'إجمالي المبلغ المتبقي',
    color: "secondary",
    gradient: "linear-gradient(135deg, #667db6 0%, #0082c8 100%, #0082c8 100%, #667db6 100%)",
  },
])

// دالة جلب بيانات العملاء بناءً على تاريخ آخر تسديد
async function fetchCustomers() {
  try {
    loading.value = true
    const authHeader = getAuthHeaders()

    const fromDate = filters.value.fromDate || 'null'
    const toDate = filters.value.toDate || 'null'

    const response = await axios.get(
      `${apiUrl}Customers/Customers_GetByLastPaymentDate/${fromDate}&&${toDate}`,
      { headers: authHeader }
    )

    customersData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

function handleDateChangeFromDate(event) {
  const rawDate = event.target.value
  if (rawDate) {
    filters.value.fromDate = new Date(rawDate).toLocaleDateString('en-CA')
  } else {
    filters.value.fromDate = ''
  }
}

function handleDateChangeToDate(event) {
  const rawDate = event.target.value
  if (rawDate) {
    filters.value.toDate = new Date(rawDate).toLocaleDateString('en-CA')
  } else {
    filters.value.toDate = ''
  }
}

// دالة تصدير بيانات العملاء إلى Excel
function exportToExcel() {
  const dataToExport = customersData.value.map(customer => ({
    'العميل': customer.customerName || 'لا يوجد',
    'المباعة': customer.itemsNames || 'لا يوجد',
    'التاريخ': customer.dateSaleDevice ? new Date(customer.dateSaleDevice).toLocaleDateString('en-CA') : 'لا يوجد',
    'سعر البيع': customer.amountTotalSales || 0,
    'سعر الشراء': customer.costTotalSales || 0,
    'القسط': customer.amountDaySales || 0,
    'الواصل': customer.receiptsTotal || 0,
    'الباقي': customer.amountRemaining || 0,
    'نسبة التسديد': customer.receiptRateDevice || 'لا يوجد',
    'عدد التسديدات': customer.countReceiptDevice || 0,
    'عدد الأيام': customer.numberOfDayDevice || 0,
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

  XLSX.utils.book_append_sheet(workbook, worksheet, "Stopped_Customers")
  XLSX.writeFile(workbook, "stopped_customers.xlsx")
}
</script>

<template>
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
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            من تاريخ اخر تسديد
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
            إلى تاريخ اخر تسديد
          </VLabel>
          <AppTextField
            v-model="filters.toDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            @input="handleDateChangeToDate"
          />
        </VCol>
        <VCol
          cols="12"
          md="6"
          class="d-flex align-center justify-start flex-wrap gap-2"
          style="margin-block-start: 22px;"
        >
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            prepend-icon="tabler-search"
            @click="fetchCustomers"
          >
            بحث
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

      <!-- عرض بيانات العملاء متوقفين بدون أزرار -->
      <VRow class="mt-4">
        <VDataTable
          :headers="headers"
          :items="customersData"
          :items-per-page="50"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <template v-slot:item.customerName="{ item }">
            <div>
              {{ item.customerName }}
            </div>
          </template>
          <template v-slot:item.itemsNames="{ item }">
            <div>
              {{ item.itemsNames || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.dateSaleDevice="{ item }">
            <div>
              {{ item.dateSaleDevice ? new Date(item.dateSaleDevice).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.amountTotalSales="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.amountTotalSales) }}
            </div>
          </template>
          <template v-slot:item.costTotalSales="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.costTotalSales) }}
            </div>
          </template>
          <template v-slot:item.amountDaySales="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.amountDaySales) }}
            </div>
          </template>
          <template v-slot:item.receiptsTotal="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.receiptsTotal) }}
            </div>
          </template>
          <template v-slot:item.amountRemaining="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.amountRemaining) }}
            </div>
          </template>
          <template v-slot:item.receiptRateDevice="{ item }">
            <div>
              {{ item.receiptRateDevice || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.countReceiptDevice="{ item }">
            <div>
              {{ item.countReceiptDevice !== undefined ? item.countReceiptDevice : 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.numberOfDayDevice="{ item }">
            <div>
              {{ item.numberOfDayDevice !== undefined ? item.numberOfDayDevice : 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.lastPaymentDate="{ item }">
            <div>
              {{ item.lastPaymentDate ? new Date(item.lastPaymentDate).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.address="{ item }">
            <div>
              {{ item.address || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.phoneNumber="{ item }">
            <div>
              {{ item.phoneNumber || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.shopName="{ item }">
            <div>
              {{ item.shopName || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.nearestFunctionPoint="{ item }">
            <div>
              {{ item.nearestFunctionPoint || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.delegateName="{ item }">
            <div>
              {{ item.delegateName || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.receiptName="{ item }">
            <div>
              {{ item.receiptName || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.saleName="{ item }">
            <div>
              {{ item.saleName || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:item.notes="{ item }">
            <div>
              {{ item.notes || 'لا يوجد' }}
            </div>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
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
