<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import ModernStatCard from "@/components/ModernStatCard.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// البيانات وحالة التحميل
const customersData = ref([])

// تصفية العملاء (استثناء القانونية IsLegal=true واستثناء المتوقفين الذين لم يسددوا منذ أكثر من عام numberOfDayPayment > 365 واستثناء المتبقي 0 أو أقل)
const filteredCustomers = computed(() => {
  return customersData.value.filter(item => {
    // استثناء القانونية
    if (item.isLegal === true) return false
    if (item.isFakeSale === true || item.isFakeSale === 'true') return false
    
    // استثناء المتوقفين
    if (item.numberOfDayPayment !== undefined && item.numberOfDayPayment !== null && item.numberOfDayPayment > 365) {
      return false
    }
    
    // جلب فقط العملاء الذين يكون AmountRemaining أكبر من 0
    const remaining = item.amountRemaining !== undefined ? item.amountRemaining : item.AmountRemaining
    if (remaining === undefined || remaining === null || remaining <= 0) {
      return false
    }
    
    return true
  })
})

const delegateList = ref([])
const loading = ref(false)
const printPreviewDialog = ref(false)
const selectedRollSize = ref('رول 80 مم')

// حساب تاريخ يوم أمس كتاريخ افتراضي
const yesterday = new Date()
yesterday.setDate(yesterday.getDate() - 1)
const defaultPaymentDate = yesterday.toLocaleDateString('en-CA')

// فلاتر البحث
const filters = ref({
  delegateID: 0,           // 0 تعني الجميع
  showType: 'المسددين',    // 'المسددين' أو 'الغير مسددين'
  paymentDate: defaultPaymentDate,
})

// دالة تنسيق الأرقام بالعملة المحلية
const formattedNumber = num => {
  if (num === 0) return "0 د.ع"
  if (num) return num.toLocaleString() + " د.ع"
  return "لا يوجد"
}

// دالة تنسيق التاريخ باللغة العربية
const formattedArabicDate = dateStr => {
  if (!dateStr) return 'لا يوجد'
  const date = new Date(dateStr)
  return date.toLocaleDateString('ar-IQ', { day: 'numeric', month: 'long', year: 'numeric' })
}

// دالة تنسيق التاريخ للـ Excel
const formattedDate = date =>
  date ? new Date(date).toLocaleDateString('en-CA') : 'لا يوجد'

// أسماء المندوبين وتفاصيلهم
const selectedDelegateName = computed(() => {
  if (filters.value.delegateID === 0) return 'كل القوائم'
  const del = delegateList.value.find(d => d.delegateID === filters.value.delegateID)
  return del ? del.delegateName : 'كل القوائم'
})

const selectedDelegateReceiptName = computed(() => {
  if (filters.value.delegateID === 0) return 'الجميع'
  const del = delegateList.value.find(d => d.delegateID === filters.value.delegateID)
  return del ? del.receiptName || 'غير محدد' : 'الجميع'
})

// حساب إجمالي القسط والتسديدات
const totalInstallment = computed(() => {
  return filteredCustomers.value.reduce((sum, item) => sum + (item.amountDaySales || 0), 0)
})

const totalPayments = computed(() => {
  return filteredCustomers.value.reduce((sum, item) => sum + (item.amountReceipt || 0), 0)
})

// تعريف عناوين الجدول ديناميكياً حسب نوع العرض
const headers = computed(() => {
  if (filters.value.showType === 'المسددين') {
    return [
      { title: 'اسم العميل', key: 'customerName' },
      { title: 'القسط اليومي', key: 'amountDaySales' },
      { title: 'المسدد يوم أمس', key: 'amountReceipt' },
      { title: 'تاريخ آخر تسديد', key: 'lastPaymentDate' },
      { title: 'المبلغ الكلي', key: 'amountTotalSales' },
      { title: 'المبلغ المستلم', key: 'receiptsTotal' },
      { title: 'المبلغ المتبقي', key: 'amountRemaining' },
      { title: 'العنوان', key: 'address' },
      { title: 'الهاتف', key: 'phoneNumber' },
    ]
  } else {
    return [
      { title: 'اسم العميل', key: 'customerName' },
      { title: 'عدد الأيام من آخر تسديد', key: 'numberOfDayPayment' },
      { title: 'القسط اليومي', key: 'amountDaySales' },
      { title: 'تاريخ آخر تسديد', key: 'lastPaymentDate' },
      { title: 'المبلغ الكلي', key: 'amountTotalSales' },
      { title: 'المبلغ المستلم', key: 'receiptsTotal' },
      { title: 'المبلغ المتبقي', key: 'amountRemaining' },
      { title: 'العنوان', key: 'address' },
      { title: 'الهاتف', key: 'phoneNumber' },
    ]
  }
})

// كروت الإحصائيات
const totals = computed(() => {
  if (filters.value.showType === 'المسددين') {
    return [
      {
        icon: 'tabler-user',
        value: filteredCustomers.value.length,
        title: 'عدد العملاء المسددين',
        color: "primary",
        gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
      },
      {
        icon: 'tabler-credit-card',
        value: formattedNumber(totalInstallment.value),
        title: 'مجموع القسط اليومي',
        color: "info",
        gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
      },
      {
        icon: 'tabler-currency-dollar',
        value: formattedNumber(totalPayments.value),
        title: 'مجموع التسديدات',
        color: "success",
        gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
      },
    ]
  } else {
    return [
      {
        icon: 'tabler-user',
        value: filteredCustomers.value.length,
        title: 'عدد العملاء غير المسددين',
        color: "primary",
        gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
      },
      {
        icon: 'tabler-credit-card',
        value: formattedNumber(totalInstallment.value),
        title: 'مجموع القسط اليومي',
        color: "info",
        gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
      },
    ]
  }
})

// جلب قائمة المندوبين
async function fetchDelegates() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Delegates/Delegates_GetDataAll`, { headers: authHeader })
    delegateList.value = response.data
  } catch (error) {
    console.error("Error fetching delegates", error)
  }
}

// جلب بيانات المتابعة اليومية
async function fetchFollowup() {
  try {
    loading.value = true
    const authHeader = getAuthHeaders()
    const delegateID = filters.value.delegateID || 0
    const paymentDate = filters.value.paymentDate || defaultPaymentDate
    const showType = filters.value.showType || 'المسددين'

    const response = await axios.get(
      `${apiUrl}Customers/Customers_Follow/${delegateID}&&${paymentDate}&&${showType}`,
      { headers: authHeader }
    )
    customersData.value = response.data
  } catch (error) {
    console.error("Error fetching followup data", error)
  } finally {
    loading.value = false
  }
}

// تصدير البيانات إلى ملف Excel
function exportToExcel() {
  const dataToExport = filteredCustomers.value.map(row => {
    const remainingVal = row.amountRemaining !== undefined ? row.amountRemaining : row.AmountRemaining
    const totalSalesVal = row.amountTotalSales !== undefined ? row.amountTotalSales : row.AmountTotalSales
    const receiptsTotalVal = row.receiptsTotal !== undefined ? row.receiptsTotal : row.ReceiptsTotal
    if (filters.value.showType === 'المسددين') {
      return {
        'اسم العميل': row.customerName || 'لا يوجد',
        'القسط اليومي': row.amountDaySales || 0,
        'المسدد يوم أمس': row.amountReceipt || 0,
        'تاريخ آخر تسديد': row.lastPaymentDate ? formattedDate(row.lastPaymentDate) : 'لا يوجد',
        'المبلغ الكلي': totalSalesVal || 0,
        'المبلغ المستلم': receiptsTotalVal || 0,
        'المبلغ المتبقي': remainingVal || 0,
        'العنوان': row.address || 'لا يوجد',
        'رقم الهاتف': row.phoneNumber || 'لا يوجد',
      }
    } else {
      return {
        'اسم العميل': row.customerName || 'لا يوجد',
        'عدد الأيام من آخر تسديد': row.numberOfDayPayment || 0,
        'القسط اليومي': row.amountDaySales || 0,
        'تاريخ آخر تسديد': row.lastPaymentDate ? formattedDate(row.lastPaymentDate) : 'لا يوجد',
        'المبلغ الكلي': totalSalesVal || 0,
        'المبلغ المستلم': receiptsTotalVal || 0,
        'المبلغ المتبقي': remainingVal || 0,
        'العنوان': row.address || 'لا يوجد',
        'رقم الهاتف': row.phoneNumber || 'لا يوجد',
      }
    }
  })

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "DailyFollowup")
  XLSX.writeFile(workbook, `Daily_Followup_${filters.value.showType}.xlsx`)
}

// تشغيل الطباعة الفعلية للجهاز عبر فتح التبويب المنفصل
function triggerPrint() {
  const queryParams = new URLSearchParams({
    delegateID: filters.value.delegateID,
    paymentDate: filters.value.paymentDate,
    showType: filters.value.showType
  }).toString();
  
  const printUrl = `${window.location.origin}${window.location.pathname}#/daily-followup-print?${queryParams}`;
  window.open(printUrl, '_blank');
}

onMounted(() => {
  fetchDelegates()
})
</script>

<template>
  <div>
    <!-- كروت الإحصائيات الفخمة -->
    <VRow class="stats-row mb-6">
      <VCol
        v-for="(card, index) in totals"
        :key="index"
        cols="12"
        sm="6"
        md="4"
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

    <!-- كارت الفلاتر والبحث -->
    <VCard class="pa-6 mb-6">
      <VForm>
        <VRow class="align-center">
          <!-- اختيار المندوب -->
          <VCol cols="12" md="3">
            <VLabel class="mb-2">المندوب</VLabel>
            <VAutocomplete
              v-model="filters.delegateID"
              prepend-inner-icon="tabler-user"
              :items="[{ title: 'كل القوائم (الجميع)', value: 0 }, ...delegateList.map(d => ({ title: d.delegateName, value: d.delegateID }))]"
              placeholder="اختر المندوب"
              clearable
            />
          </VCol>

          <!-- اختيار الحالة -->
          <VCol cols="12" md="3">
            <VLabel class="mb-2">الحالة</VLabel>
            <VAutocomplete
              v-model="filters.showType"
              prepend-inner-icon="tabler-filter"
              :items="['المسددين', 'الغير مسددين']"
              placeholder="اختر الحالة"
            />
          </VCol>

          <!-- التاريخ -->
          <VCol cols="12" md="3">
            <VLabel class="mb-2">تاريخ التقرير</VLabel>
            <AppTextField
              v-model="filters.paymentDate"
              type="date"
              prepend-inner-icon="tabler-calendar"
            />
          </VCol>

          <!-- أزرار العمليات -->
          <VCol cols="12" md="3">
            <VLabel class="mb-2" style="opacity: 0; visibility: hidden;">العمليات</VLabel>
            <div class="d-flex align-center gap-2">
              <VBtn
                color="primary"
                :loading="loading"
                :disabled="loading"
                prepend-icon="tabler-search"
                @click="fetchFollowup"
              >
                بحث
              </VBtn>
              <VBtn
                color="success"
                prepend-icon="tabler-file-export"
                :disabled="filteredCustomers.length === 0"
                @click="exportToExcel"
              >
                تصدير Excel
              </VBtn>
              <VBtn
                color="secondary"
                prepend-icon="tabler-printer"
                :disabled="filteredCustomers.length === 0"
                @click="printPreviewDialog = true"
              >
                طباعة وصل
              </VBtn>
            </div>
          </VCol>
        </VRow>
      </VForm>
    </VCard>

    <!-- جدول البيانات -->
    <VCard class="pa-6">
      <VRow>
        <VDataTable
          :headers="headers"
          :items="filteredCustomers"
          :items-per-page="50"
          style="overflow: hidden; block-size: 100%; white-space: nowrap;"
          items-per-page-text="عدد السجلات"
          class="text-no-wrap custom-data-table"
        >
          <template #item.customerName="{ item }">
            <div class="font-weight-medium customer-name-text">
              {{ item.customerName }}
            </div>
          </template>

          <template #item.amountDaySales="{ item }">
            <div
              class="premium-amount amt-installment"
            >
              {{ formattedNumber(item.amountDaySales) }}
            </div>
          </template>

          <template #item.amountReceipt="{ item }">
            <div
              class="premium-amount amt-paid-yesterday"
            >
              {{ formattedNumber(item.amountReceipt) }}
            </div>
          </template>

          <template #item.numberOfDayPayment="{ item }">
            <VChip
              color="warning"
              size="small"
              class="font-weight-bold"
            >
              {{ item.numberOfDayPayment || 0 }} يوم
            </VChip>
          </template>

          <template #item.lastPaymentDate="{ item }">
            <div
              class="font-weight-medium"
              style="color: #64748b;"
            >
              {{ item.lastPaymentDate ? new Date(item.lastPaymentDate).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>

          <template #item.amountTotalSales="{ item }">
            <div
              class="premium-amount amt-total-sales"
            >
              {{ formattedNumber(item.amountTotalSales !== undefined ? item.amountTotalSales : item.AmountTotalSales) }}
            </div>
          </template>

          <template #item.receiptsTotal="{ item }">
            <div
              class="premium-amount amt-total-receipts"
            >
              {{ formattedNumber(item.receiptsTotal !== undefined ? item.receiptsTotal : item.ReceiptsTotal) }}
            </div>
          </template>

          <template #item.amountRemaining="{ item }">
            <div
              class="premium-amount amt-remaining"
            >
              {{ formattedNumber(item.amountRemaining !== undefined ? item.amountRemaining : item.AmountRemaining) }}
            </div>
          </template>

          <template #item.address="{ item }">
            <div>{{ item.address || 'لا يوجد' }}</div>
          </template>

          <template #item.phoneNumber="{ item }">
            <div>{{ item.phoneNumber || 'لا يوجد' }}</div>
          </template>
        </VDataTable>
      </VRow>
    </VCard>

    <!-- مودال معاينة الوصل لطابعة الكاشير 80 مم -->
    <VDialog v-model="printPreviewDialog" max-width="400">
      <VCard class="pa-4 bg-grey-lighten-4">
        <VCardTitle class="d-flex justify-space-between align-center mb-3">
          <span class="font-weight-bold text-h6 text-purple-darken-3">معاينة ومشاركة الوصل</span>
          <div class="d-flex align-center gap-2">
            <VBtn
              color="purple-darken-2"
              variant="elevated"
              size="small"
              prepend-icon="tabler-printer"
              @click="triggerPrint"
            >
              طباعة
            </VBtn>
            <VBtn icon="tabler-x" variant="text" @click="printPreviewDialog = false" />
          </div>
        </VCardTitle>

        <VCardText class="d-flex justify-center pa-2">
          <!-- محاكاة شكل ورقة الكاشير على الشاشة -->
          <div class="receipt-screen-mock elevation-3 pa-4 white-bg" style="width: 100%; max-width: 320px; background: white; border: 1px solid #ccc; border-radius: 4px; direction: rtl; font-family: 'Cairo', sans-serif;">
            
            <!-- جدول المعلومات العلوية للوصل -->
            <table class="mock-info-table mb-4">
              <tr>
                <td class="mock-val">{{ selectedDelegateName }}</td>
                <td class="mock-lbl">اسم القائمة</td>
              </tr>
              <tr>
                <td class="mock-val">{{ selectedDelegateReceiptName }}</td>
                <td class="mock-lbl">اسم الجابي</td>
              </tr>
              <tr>
                <td class="mock-val">{{ filters.showType === 'المسددين' ? filteredCustomers.length : 0 }}</td>
                <td class="mock-lbl">عدد التسديدات</td>
              </tr>
              <tr>
                <td class="mock-val">{{ formattedArabicDate(filters.paymentDate) }}</td>
                <td class="mock-lbl">التاريخ</td>
              </tr>
            </table>

            <!-- جدول العملاء والأسعار -->
            <table class="mock-items-table mb-4">
              <thead>
                <tr v-if="filters.showType === 'المسددين'">
                  <th>اسم الزبون</th>
                  <th>القسط</th>
                  <th>المسدد</th>
                </tr>
                <tr v-else>
                  <th>اسم الزبون</th>
                  <th>عدد الأيام</th>
                  <th>القسط</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in filteredCustomers" :key="item.customerID">
                  <template v-if="filters.showType === 'المسددين'">
                    <td class="text-right">{{ item.customerName }}</td>
                    <td>{{ formattedNumber(item.amountDaySales) }}</td>
                    <td>{{ formattedNumber(item.amountReceipt) }}</td>
                  </template>
                  <template v-else>
                    <td class="text-right">{{ item.customerName }}</td>
                    <td>{{ item.numberOfDayPayment || 0 }}</td>
                    <td>{{ formattedNumber(item.amountDaySales) }}</td>
                  </template>
                </tr>
              </tbody>
            </table>

            <!-- جدول الإجماليات أسفل الوصل -->
            <table class="mock-summary-table">
              <template v-if="filters.showType === 'المسددين'">
                <tr>
                  <td class="mock-val">{{ formattedNumber(totalInstallment) }}</td>
                  <td class="mock-lbl">مجموع القسط</td>
                </tr>
                <tr>
                  <td class="mock-val">{{ formattedNumber(totalPayments) }}</td>
                  <td class="mock-lbl">مجموع التسديدات</td>
                </tr>
              </template>
              <template v-else>
                <tr>
                  <td class="mock-val">{{ filteredCustomers.length }}</td>
                  <td class="mock-lbl">عدد العملاء</td>
                </tr>
                <tr>
                  <td class="mock-val">{{ formattedNumber(totalInstallment) }}</td>
                  <td class="mock-lbl">مجموع القسط</td>
                </tr>
              </template>
            </table>

          </div>
        </VCardText>

        <VCardActions class="d-flex justify-center flex-wrap gap-2 mt-4 pa-0">
          <VBtn color="purple-darken-2" variant="elevated" prepend-icon="tabler-printer" @click="triggerPrint">
            طباعة
          </VBtn>
          <VAutocomplete
            v-model="selectedRollSize"
            :items="['رول 80 مم']"
            density="compact"
            variant="outlined"
            hide-details
            style="max-width: 140px;"
          />
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- المقطع الفعلي المخصص للطباعة فقط (يخفى تماماً على الشاشة ويظهر فقط عند استدعاء الطباعة) -->
    <div id="print-receipt-section" class="print-only">
      <div class="print-receipt-container">
        
        <!-- جدول المعلومات العلوي للطباعة -->
        <table class="print-info-table">
          <tr>
            <td class="val-col">{{ selectedDelegateName }}</td>
            <td class="lbl-col font-bg">اسم القائمة</td>
          </tr>
          <tr>
            <td class="val-col">{{ selectedDelegateReceiptName }}</td>
            <td class="lbl-col font-bg">اسم الجابي</td>
          </tr>
          <tr>
            <td class="val-col">{{ filters.showType === 'المسددين' ? filteredCustomers.length : 0 }}</td>
            <td class="lbl-col font-bg">عدد التسديدات</td>
          </tr>
          <tr>
            <td class="val-col">{{ formattedArabicDate(filters.paymentDate) }}</td>
            <td class="lbl-col font-bg">التاريخ</td>
          </tr>
        </table>

        <!-- جدول الحقول للطباعة -->
        <table class="print-items-table">
          <thead>
            <tr v-if="filters.showType === 'المسددين'">
              <th style="width: 50%;">اسم الزبون</th>
              <th style="width: 25%;">القسط</th>
              <th style="width: 25%;">المسدد</th>
            </tr>
            <tr v-else>
              <th style="width: 50%;">اسم الزبون</th>
              <th style="width: 25%;">عدد الأيام</th>
              <th style="width: 25%;">القسط</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in filteredCustomers" :key="item.customerID">
              <template v-if="filters.showType === 'المسددين'">
                <td style="text-align: right; padding-right: 4px;">{{ item.customerName }}</td>
                <td>{{ formattedNumber(item.amountDaySales) }}</td>
                <td>{{ formattedNumber(item.amountReceipt) }}</td>
              </template>
              <template v-else>
                <td style="text-align: right; padding-right: 4px;">{{ item.customerName }}</td>
                <td>{{ item.numberOfDayPayment || 0 }}</td>
                <td>{{ formattedNumber(item.amountDaySales) }}</td>
              </template>
            </tr>
          </tbody>
        </table>

        <!-- جدول الإجماليات للطباعة -->
        <table class="print-summary-table">
          <template v-if="filters.showType === 'المسددين'">
            <tr>
              <td class="val-col">{{ formattedNumber(totalInstallment) }}</td>
              <td class="lbl-col font-bg">مجموع القسط</td>
            </tr>
            <tr>
              <td class="val-col">{{ formattedNumber(totalPayments) }}</td>
              <td class="lbl-col font-bg">مجموع التسديدات</td>
            </tr>
          </template>
          <template v-else>
            <tr>
              <td class="val-col">{{ filteredCustomers.length }}</td>
              <td class="lbl-col font-bg">عدد العملاء</td>
            </tr>
            <tr>
              <td class="val-col">{{ formattedNumber(totalInstallment) }}</td>
              <td class="lbl-col font-bg">مجموع القسط</td>
            </tr>
          </template>
        </table>

      </div>
    </div>
  </div>
</template>

<style scoped>
/* تصميم محاكاة ورقة الكاشير في المودال */
.mock-info-table, .mock-items-table, .mock-summary-table {
  width: 100%;
  border-collapse: collapse;
  color: #000;
  font-size: 13px;
}

.mock-info-table td, .mock-summary-table td {
  border: 1.5px solid #000;
  padding: 6px;
  text-align: center;
  font-weight: bold;
}

.mock-lbl {
  background-color: #f3f3f3;
  width: 45%;
}

.mock-val {
  background-color: #ffffff;
}

.mock-items-table th, .mock-items-table td {
  border: 1.5px solid #000;
  padding: 6px;
  text-align: center;
  font-weight: bold;
}

.mock-items-table th {
  background-color: #f3f3f3;
}

/* تنسيق الطباعة الفعلي (يخفى تماماً عن العرض بالمتصفح) */
@media screen {
  .print-only {
    display: none !important;
  }
}

@media print {
  /* إخفاء واجهة الموقع بالكامل */
  body * {
    visibility: hidden !important;
  }

  /* إظهار قسم الطباعة فقط وبمواصفات طابعة الرول */
  #print-receipt-section, #print-receipt-section * {
    visibility: visible !important;
  }

  #print-receipt-section {
    position: absolute !important;
    left: 0 !important;
    top: 0 !important;
    width: 80mm !important;
    margin: 0 !important;
    padding: 2mm !important;
    box-sizing: border-box !important;
    direction: rtl !important;
    background-color: #ffffff !important;
  }

  .print-receipt-container {
    width: 100% !important;
    font-family: 'Cairo', sans-serif !important;
    color: #000000 !important;
  }

  .print-info-table, .print-items-table, .print-summary-table {
    width: 100% !important;
    border-collapse: collapse !important;
    margin-bottom: 4mm !important;
  }

  .print-info-table td, .print-summary-table td {
    border: 2px solid #000000 !important;
    padding: 5px !important;
    text-align: center !important;
    font-weight: bold !important;
    font-size: 13px !important;
    -webkit-print-color-adjust: exact !important;
    print-color-adjust: exact !important;
  }

  .print-info-table td.lbl-col, .print-summary-table td.lbl-col {
    background-color: #e0e0e0 !important;
    width: 40% !important;
  }

  .print-items-table th, .print-items-table td {
    border: 2px solid #000000 !important;
    padding: 5px 3px !important;
    text-align: center !important;
    font-weight: bold !important;
    font-size: 12px !important;
    -webkit-print-color-adjust: exact !important;
    print-color-adjust: exact !important;
  }

  .print-items-table th {
    background-color: #e0e0e0 !important;
  }

  @page {
    size: 80mm auto !important;
    margin: 0 !important;
  }
}

:deep(.customer-name-text) {
  transition: color 0.2s ease-in-out;
}

.v-theme--dark :deep(.customer-name-text) {
  color: #ffffff !important;
}

.v-theme--light :deep(.customer-name-text) {
  color: #000000 !important;
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
