<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from '@core/components/app-form-elements/AppTextField.vue'
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات السحوبات من الخزائن
const withdrawalData = ref([])

// قائمة الخزائن (Boxs) جلبها من API
const boxList = ref([])

// حالة التحميل
const loading = ref(false)

// فلاتر البحث: من التاريخ إلى التاريخ واختيار الخزينة
const filters = ref({
  fromDate: '',  // بصيغة yyyy-MM-dd
  toDate: '',    // بصيغة yyyy-MM-dd
  boxID: null,       // 0 تعني "الجميع" أو قيمة محددة
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

// تعريف أعمدة جدول السحوبات (أسماء الحقول تبدأ بحرف صغير)
const headers = [
  { title: 'المبلغ', key: 'amountDenar' },
  { title: 'الخزينة', key: 'boxName' },
  { title: 'التاريخ', key: 'dateCreate' },
  { title: 'الغرض', key: 'purpose' },
  { title: 'الملاحظات', key: 'notes' },
]


const totals = computed(() => [
  {
    icon: 'tabler-file',  // أيقونة تمثل الإضافات
    value: withdrawalData.value.length,
    title: 'عدد السحوبات الكلي',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-cash',  // أيقونة تمثل المبلغ
    value: formattedNumber(
      withdrawalData.value.reduce((sum, item) => sum + (item.amountDenar || 0), 0),
    ),
    title: 'المبلغ الكلي',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
])

// دالة جلب قائمة الخزائن من API (Boxs_GetAllData)
async function fetchBoxes() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()

    const response = await axios.get(`${apiUrl}Accounts/Boxs_GetAllData`, { headers: authHeader })


    // تحويل البيانات إلى camelCase (إذا كانت البيانات في PascalCase)
    boxList.value = response.data.map(box => ({
      boxID: box.boxID,
      boxName: box.boxName,
    }))
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة جلب بيانات السحوبات من API باستخدام الفلاتر
async function fetchWithdrawalData() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()

    const fromDate = filters.value.fromDate || 'null'
    const toDate = filters.value.toDate || 'null'
    const boxID = filters.value.boxID || 0

    // استدعاء API الخاص بالسحوبات: نستخدم نقطة النهاية المفترضة
    const response = await axios.get(`${apiUrl}Accounts/WithdrawalFromBoxs_GetByDateByboxID/${fromDate}&&${toDate}&&${boxID}`, { headers: authHeader })

    withdrawalData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير بيانات السحوبات إلى Excel
function exportToExcel() {
  const dataToExport = withdrawalData.value.map(item => ({
    'المبلغ': item.amountDenar || 'لا يوجد',
    'الخزينة': item.boxName || 'لا يوجد',
    'التاريخ': item.dateCreate ? formattedDate(item.dateCreate) : 'لا يوجد',
    'الغرض': item.purpose || 'لا يوجد',
    'الملاحظات': item.notes || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "WithdrawalFromBox")
  XLSX.writeFile(workbook, "WithdrawalFromBox.xlsx")
}


const accountID = ref(null)
const confirmAccountDialog = ref(false)

function openDeleteDialog(id) {
  accountID.value = id
  confirmAccountDialog.value = true
}

function closeDeleteDialog(id) {
  confirmAccountDialog.value = false
}

async function deleteAccount() {
  if (accountID.value) {
    try {
      const authHeader = getAuthHeaders()

      await axios.delete(`${apiUrl}Accounts/WithdrawalFromBox_Delete/${accountID.value}`, { headers: authHeader })

      const index = withdrawalData.value.findIndex(sale => sale.withdrawalFromBoxID === accountID.value)
      if (index !== -1) {
        withdrawalData.value.splice(index, 1)
      }
      confirmAccountDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}


onMounted(() => {
  fetchBoxes()
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
    <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, i) in totals"
      :key="i"
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
      <!-- فلاتر البحث: من التاريخ، إلى التاريخ، واختيار الخزينة -->
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
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            الخزينة
          </VLabel>
          <VAutocomplete
            v-model="filters.boxID"
            :items="boxList.map(box => ({ title: box.boxName, value: box.boxID }))"
            placeholder="اختر الخزينة"
            prepend-inner-icon="tabler-wallet"
            clearable
          />
        </VCol>
        <VRow style="margin-block-start: 31px;margin-inline: 5px 10px;">
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            style="margin-inline-start: 10px;"
            prepend-icon="tabler-search"
            @click="fetchWithdrawalData"
          >
            بحث
          </VBtn>
          <VBtn
            color="success"
            style="margin-inline-start: 10px;"
            prepend-icon="tabler-file-export"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VRow>
      </VRow>

      <!-- عرض بيانات السحوبات -->
      <VRow>
        <VDataTable
          :headers="headers"
          :items="withdrawalData"
          :items-per-page="50"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <template #item.amountDenar="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.amountDenar) }}
            </div>
          </template>
          <template #item.boxName="{ item }">
            <div>
              {{ item.boxName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.dateCreate="{ item }">
            <div>
              {{ item.dateCreate ? formattedDate(item.dateCreate) : 'لا يوجد' }}
            </div>
          </template>
          <template #item.purpose="{ item }">
            <div>
              {{ item.purpose || 'لا يوجد' }}
            </div>
          </template>
          <template #item.notes="{ item }">
            <div>
              {{ item.notes || 'لا يوجد' }}
            </div>
          </template>
          <template #item.delete="{ item }">
            <div class="d-flex gap-1">
              <!--
                <VBtn
                color="error"
                style="margin-block-end: 10px;"
                prepend-icon="tabler-trash"
                @click="openDeleteDialog(item.withdrawalFromBoxID)"
                >
                حذف
                </VBtn> 
              -->
            </div>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>
  <!-- Dialog تأكيد الحذف -->
  <VDialog
    v-model="confirmAccountDialog"
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
            <span class="text-caption text-medium-emphasis">هل أنت متأكد من حذف هذا السند؟</span>
          </div>
        </div>
      </div>
      <VCardText>
        <p class="mb-0">
          لا يمكن التراجع عن هذا الإجراء.
        </p>
      </VCardText>
      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="closeDeleteDialog"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إلغاء
        </VBtn>
        <VBtn
          color="error"
          @click="deleteAccount"
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
  </div>
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
