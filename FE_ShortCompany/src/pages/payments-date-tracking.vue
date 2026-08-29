<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from '@core/components/app-form-elements/AppTextField.vue'
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات المتابعة وقائمة المندوبين
const followData = ref([])
const delegateList = ref([])

// حالة التحميل
const loading = ref(false)

// فلاتر البحث: البحث حسب المندوب وتاريخ التسديد ونوع العرض
const filters = ref({
  delegateID: null,      // 0 تعني "الجميع"
  paymentDate: '',    // تاريخ التسديد (يمكن تحديده بواسطة AppTextField من نوع date)
  showType: 'الجميع', // الخيارات: 'الجميع'، 'المسددين'، 'المتوقفين'
})

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  
  return "لا يوجد"
}

// دالة تنسيق التاريخ
const formattedDate = date =>
  date ? new Date(date).toLocaleDateString('en-CA') : 'لا يوجد'

// تعريف أعمدة جدول المتابعة بجميع الحقول المطلوبة (أسماء الحقول تبدأ بحرف صغير)
const headers = [
  { title: 'اسم العميل', key: 'customerName' },
  { title: 'رقم الهاتف', key: 'phoneNumber' },
  { title: 'القائمة', key: 'delegateName' }, // نفترض أن "القائمة" هنا هي اسم المندوب
  { title: 'المباع', key: 'itemsNames' },
  { title: 'تاريخ البيع', key: 'dateSaleDevice' },
  { title: 'عدد الأيام', key: 'numberOfDayDevice' },
  { title: 'سعر البيع', key: 'amountTotalSales' },
  { title: 'القسط', key: 'amountDaySales' },
  { title: 'الواصل', key: 'receiptsTotal' },
  { title: 'الباقي', key: 'amountRemaining' },
  { title: 'عدد التسديدات', key: 'countReceiptDevice' },
  { title: 'تاريخ اخر تسديد', key: 'lastPaymentDate' },
  { title: 'مبلغ التسديد', key: 'amountReceipt' },
]


const totals = computed(() => [
  {
    icon: 'tabler-user', // عدد العملاء
    value: followData.value.length,
    title: 'عدد العملاء',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-wallet', // سعر البيع
    value: formattedNumber(
      followData.value.reduce((sum, row) => sum + (row.amountTotalSales || 0), 0),
    ),
    title: 'سعر البيع',
    color: "success",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
  {
    icon: 'tabler-credit-card', // القسط
    value: formattedNumber(
      followData.value.reduce((sum, row) => sum + (row.amountDaySales || 0), 0),
    ),
    title: 'القسط',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
  {
    icon: 'tabler-currency-dollar', // الواصل
    value: formattedNumber(
      followData.value.reduce((sum, row) => sum + (row.receiptsTotal || 0), 0),
    ),
    title: 'الواصل',
    color: "success",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-currency-dollar', // الباقي
    value: formattedNumber(
      followData.value.reduce((sum, row) => sum + (row.amountRemaining || 0), 0),
    ),
    title: 'الباقي',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
])

// دالة جلب قائمة المندوبين من API
async function fetchDelegates() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Delegates/Delegates_GetDataAll`, { headers: authHeader })

    delegateList.value = response.data
  } catch (error) {
    console.error(error)
  }
}

// دالة جلب بيانات المتابعة باستخدام الفلاتر
async function fetchFollowData() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()

    const delegateID = filters.value.delegateID || 0
    const paymentDate = filters.value.paymentDate || 'null'
    const showType = filters.value.showType || 'الجميع'

    // استدعاء API الخاص بالمتابعة
    const response = await axios.get(`${apiUrl}Customers/Customers_Follow/${delegateID}&&${paymentDate}&&${showType}`, { headers: authHeader })

    followData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير بيانات المتابعة إلى Excel
function exportToExcel() {
  const dataToExport = followData.value.map(row => ({
    'اسم العميل': row.customerName || 'لا يوجد',
    'رقم الهاتف': row.phoneNumber || 'لا يوجد',
    'القائمة': row.delegateName || 'لا يوجد',
    'المباع': row.itemsNames || 'لا يوجد',
    'تاريخ البيع': row.dateSaleDevice ? formattedDate(row.dateSaleDevice) : 'لا يوجد',
    'عدد الأيام': row.numberOfDayDevice || 'لا يوجد',
    'سعر البيع': row.amountTotalSales || 'لا يوجد',
    'القسط': row.amountDaySales || 'لا يوجد',
    'الواصل': row.receiptsTotal || 'لا يوجد',
    'الباقي': row.amountRemaining || 'لا يوجد',
    'عدد التسديدات': row.countReceiptDevice || 'لا يوجد',
    'تاريخ اخر تسديد': row.lastPaymentDate ? formattedDate(row.lastPaymentDate) : 'لا يوجد',
    'مبلغ التسديد': row.amountReceipt || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "FollowData")
  XLSX.writeFile(workbook, "FollowData.xlsx")
}

onMounted(() => {
  fetchDelegates()
})

const colSlots = {
  customerName: 'item.customerName',
  phoneNumber: 'item.phoneNumber',
  delegateName: 'item.delegateName',
  itemsNames: 'item.itemsNames',
  dateSaleDevice: 'item.dateSaleDevice',
  numberOfDayDevice: 'item.numberOfDayDevice',
  amountTotalSales: 'item.amountTotalSales',
  amountDaySales: 'item.amountDaySales',
  receiptsTotal: 'item.receiptsTotal',
  amountRemaining: 'item.amountRemaining',
  countReceiptDevice: 'item.countReceiptDevice',
  lastPaymentDate: 'item.lastPaymentDate',
  amountReceipt: 'item.amountReceipt',
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
      <!-- فلاتر البحث: اختيار المندوب وتاريخ التسديد ونوع العرض -->
      <VRow>
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
            prepend-inner-icon="tabler-calendar"
            type="date"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            نوع العرض
          </VLabel>
          <VAutocomplete
            v-model="filters.showType"
            prepend-inner-icon="tabler-filter"
            :items="['الجميع', 'المسددين', 'المتوقفين']"
            placeholder="اختر النوع"
          />
        </VCol>
        <VRow style="margin-block-start: 41px;margin-inline-end: 10px;">
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            style="margin-inline-start: 20px;"
            prepend-icon="tabler-search"
            @click="fetchFollowData"
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
      <!-- عرض بيانات المتابعة -->
      <VRow>
        <VDataTable
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          :headers="headers"
          :items="followData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <!-- لكل حقل قالب بعرض 200px -->
          <template v-slot:[colSlots.customerName]="{ item }">
            <div>
              {{ item.customerName || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:[colSlots.phoneNumber]="{ item }">
            <div>
              {{ item.phoneNumber || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:[colSlots.delegateName]="{ item }">
            <div>
              {{ item.delegateName || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:[colSlots.itemsNames]="{ item }">
            <div>
              {{ item.itemsNames || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:[colSlots.dateSaleDevice]="{ item }">
            <div>
              {{ item.dateSaleDevice ? formattedDate(item.dateSaleDevice) : 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:[colSlots.numberOfDayDevice]="{ item }">
            <div>
              {{ item.numberOfDayDevice || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:[colSlots.amountTotalSales]="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.amountTotalSales) }}
            </div>
          </template>
          <template v-slot:[colSlots.amountDaySales]="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.amountDaySales) }}
            </div>
          </template>
          <template v-slot:[colSlots.receiptsTotal]="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.receiptsTotal) }}
            </div>
          </template>
          <template v-slot:[colSlots.amountRemaining]="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.amountRemaining) }}
            </div>
          </template>
          <template v-slot:[colSlots.countReceiptDevice]="{ item }">
            <div>
              {{ item.countReceiptDevice || 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:[colSlots.lastPaymentDate]="{ item }">
            <div>
              {{ item.lastPaymentDate ? formattedDate(item.lastPaymentDate) : 'لا يوجد' }}
            </div>
          </template>
          <template v-slot:[colSlots.amountReceipt]="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.amountReceipt) }}
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
