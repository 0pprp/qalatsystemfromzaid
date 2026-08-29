<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from '@core/components/app-form-elements/AppTextField.vue'
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات الإحصائيات (StatisticsGetDTO)
const statsData = ref([])

// حالة التحميل
const loading = ref(false)

// فلاتر البحث: من التاريخ وإلى التاريخ
const filters = ref({
  fromDate: '', // يجب تعبئتها بصيغة yyyy-MM-dd
  toDate: '',    // يجب تعبئتها بصيغة yyyy-MM-dd
})

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  
  return "لا يوجد"
}

// دالة تنسيق التاريخ
const formattedDate = date =>
  date ? new Date(date.paymentDate).toLocaleDateString('en-CA')  : 'لا يوجد'

// تعريف أعمدة جدول الإحصائيات (المفاتيح تتطابق مع نموذج StatisticsGetDTO)
const headers = [
  { title: 'المندوب', key: 'delegateName' },
  { title: 'عدد العملاء', key: 'numberOfCustomer' },
  { title: 'سعر البيع', key: 'amountPrice' },
  { title: 'سعر الشراء', key: 'amountCost' },
  { title: 'القسط', key: 'amountDay' },
  { title: 'عدد المباع', key: 'numberOfItemSale' },
  { title: 'الواصل', key: 'amountReceipt' },
]


// إجماليات البيانات
const totals = computed(() => [
  {
    icon: 'tabler-users-group', // عدد العملاء الكلي
    value: statsData.value.reduce((sum, s) => sum + (s.numberOfCustomer || 0), 0),
    title: 'عدد العملاء الكلي',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-cash', // سعر البيع الكلي
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountPrice || 0), 0)),
    title: 'سعر البيع الكلي',
    color: "info",
    gradient: "linear-gradient(135deg, #0284c7 0%, #3b82f6 100%)",
  },
  {
    icon: 'tabler-cash', // سعر الشراء الكلي
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountCost || 0), 0)),
    title: 'سعر الشراء الكلي',
    color: "secondary",
    gradient: "linear-gradient(135deg, #475569 0%, #64748b 100%)",
  },
  {
    icon: 'tabler-calendar', // القسط الكلي
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountDay || 0), 0)),
    title: 'القسط الكلي',
    color: "success",
    gradient: "linear-gradient(135deg, #059669 0%, #10b981 100%)",
  },
  {
    icon: 'tabler-check', // عدد المباع الكلي
    value: statsData.value.reduce((sum, s) => sum + (s.numberOfItemSale || 0), 0),
    title: 'عدد المباع الكلي',
    color: "warning",
    gradient: "linear-gradient(135deg, #f59e0b 0%, #d97706 100%)",
  },
  {
    icon: 'tabler-credit-card', // الواصل الكلي
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountReceipt || 0), 0)),
    title: 'الواصل الكلي',
    color: "success",
    gradient: "linear-gradient(135deg, #10b981 0%, #34d399 100%)",
  },
])


// دالة جلب بيانات الإحصائيات من API باستخدام الفلاتر
async function fetchStats() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const fromDate = filters.value.fromDate || 'null'
    const toDate = filters.value.toDate || 'null'
    const response = await axios.get(`${apiUrl}Delegates/Delegates_NoStatistics/${fromDate}&&${toDate}`, { headers: authHeader })

    statsData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير بيانات الإحصائيات إلى Excel
function exportToExcel() {
  const dataToExport = statsData.value.map(s => ({
    'المندوب': s.delegateName || 'لا يوجد',
    'عدد العملاء': s.numberOfCustomer || 0,
    'سعر البيع': s.amountPrice || 0,
    'سعر الشراء': s.amountCost || 0,
    'القسط': s.amountDay || 0,
    'عدد المباع': s.numberOfItemSale || 0,
    'الواصل': s.amountReceipt || 0,
    'عدد المصفرين': s.numberOfCustomerZero || 0,
    'سعر بيع المصفرين': s.amountPriceZero || 0,
    'قسط المصفرين': s.amountDayZero || 0,
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Statistics")
  XLSX.writeFile(workbook, "Statistics.xlsx")
}

onMounted(() => {
  fetchStats()
})

function handleDateChangeFromDate(event) {
  const rawDate = event.target.value

  filters.value.fromDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function handleDateChangeToDate(event) {
  const rawDate = event.target.value

  filters.value.toDate = new Date(rawDate).toLocaleDateString('en-CA')
}
</script>

<template>
  <div>
    <!-- عرض إجماليات البيانات -->
    <VRow
      class="pa-5"
      style=" margin-block-start: -33px;margin-inline-end: -30px;"
    >
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
      <!-- فلاتر البحث: من التاريخ وإلى التاريخ -->
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
            prepend-inner-icon="tabler-calendar"
            type="date"
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
            prepend-inner-icon="tabler-calendar"
            type="date"
            @input="handleDateChangeToDate"
          />
        </VCol>
        <VRow style="margin-block-start: 42px;margin-inline: 10px 10px;">
          <VBtn
            color="primary"
            :loading="loading"
            style="margin-inline-start: 10px;"
            prepend-icon="tabler-search"
            :disabled="loading"
            @click="fetchStats"
          >
            بحث
          </VBtn>
          <VBtn
            color="success"
            style="margin-inline-start: 10px;"
            prepend-icon="tabler-plus"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VRow>
      </VRow>
      <!-- عرض بيانات الإحصائيات -->
      <VRow>
        <VDataTable
          :headers="headers"
          :items="statsData"
          :items-per-page="50"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          items-per-page-text="عدد السجل"
        >
          <template #item.delegateName="{ item }">
            <div class="font-weight-medium">
              {{ item.delegateName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.numberOfCustomer="{ item }">
            <VChip
              color="primary"
              size="small"
              class="font-weight-bold"
            >
              {{ item.numberOfCustomer || 0 }}
            </VChip>
          </template>
          <template #item.amountPrice="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.amountPrice) }}
            </div>
          </template>
          <template #item.amountCost="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.amountCost) }}
            </div>
          </template>
          <template #item.amountDay="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.amountDay) }}
            </div>
          </template>
          <template #item.numberOfItemSale="{ item }">
            <VChip
              color="secondary"
              size="small"
              class="font-weight-bold"
            >
              {{ item.numberOfItemSale || 0 }}
            </VChip>
          </template>
          <template #item.amountReceipt="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.amountReceipt) }}
            </div>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>
  </div>
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
