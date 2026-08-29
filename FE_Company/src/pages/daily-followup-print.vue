<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import axios from 'axios'
import { computed, onMounted, ref, nextTick } from 'vue'
import { useRoute } from 'vue-router'

definePage({
  meta: {
    layout: 'blank',
  },
})

const route = useRoute()
const apiUrl = localStorage.getItem('LinkCity')

const delegateID = ref(Number(route.query.delegateID) || 0)
const paymentDate = ref(route.query.paymentDate || '')
const showType = ref(route.query.showType || 'المسددين')

const customersData = ref([])
const delegateList = ref([])
const loading = ref(true)

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

const selectedDelegateName = computed(() => {
  if (delegateID.value === 0) return 'كل القوائم'
  const del = delegateList.value.find(d => d.delegateID === delegateID.value)
  return del ? del.delegateName : 'كل القوائم'
})

const selectedDelegateReceiptName = computed(() => {
  if (delegateID.value === 0) return 'الجميع'
  const del = delegateList.value.find(d => d.delegateID === delegateID.value)
  return del ? del.receiptName || 'غير محدد' : 'الجميع'
})

// حساب إجمالي القسط والتسديدات
const totalInstallment = computed(() => {
  return filteredCustomers.value.reduce((sum, item) => sum + (item.amountDaySales || 0), 0)
})

const totalPayments = computed(() => {
  return filteredCustomers.value.reduce((sum, item) => sum + (item.amountReceipt || 0), 0)
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

    const response = await axios.get(
      `${apiUrl}Customers/Customers_Follow/${delegateID.value}&&${paymentDate.value}&&${showType.value}`,
      { headers: authHeader }
    )
    customersData.value = response.data
  } catch (error) {
    console.error("Error fetching followup data", error)
  } finally {
    loading.value = false
    // بعد تحميل البيانات، انتظر تحديث الـ DOM ثم شغل حوار الطباعة
    nextTick(() => {
      setTimeout(() => {
        window.print()
      }, 500)
    })
  }
}

// تشغيل الطباعة يدوياً
const handlePrint = () => {
  window.print()
}

// إغلاق النافذة
const handleClose = () => {
  window.close()
}

onMounted(async () => {
  await fetchDelegates()
  await fetchFollowup()
  
  // الاستماع لحدث إلغاء/إنهاء الطباعة لإغلاق التبويب تلقائياً
  window.onafterprint = () => {
    window.close()
  }
})
</script>

<template>
  <div class="print-page-wrapper">
    <!-- أزرار التحكم العلوية (تختفي عند الطباعة) -->
    <div class="no-print action-bar">
      <button class="action-btn print-btn" @click="handlePrint">
        <span class="btn-icon">🖨️</span>
        طباعة الوصل
      </button>
      <button class="action-btn close-btn" @click="handleClose">
        <span class="btn-icon">❌</span>
        إغلاق الصفحة
      </button>
    </div>

    <!-- حالة التحميل -->
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <div class="loading-text">جاري تحضير البيانات للطباعة...</div>
    </div>

    <!-- محتوى الوصل الفعلي -->
    <div v-else class="receipt-container">
      
      <!-- جدول معلومات الوصل -->
      <table class="receipt-table info-table">
        <tbody>
          <tr>
            <td class="val-col">{{ selectedDelegateName }}</td>
            <td class="lbl-col font-bg">اسم القائمة</td>
          </tr>
          <tr>
            <td class="val-col">{{ selectedDelegateReceiptName }}</td>
            <td class="lbl-col font-bg">اسم الجابي</td>
          </tr>
          <tr>
            <td class="val-col">{{ showType === 'المسددين' ? filteredCustomers.length : 0 }}</td>
            <td class="lbl-col font-bg">عدد التسديدات</td>
          </tr>
          <tr>
            <td class="val-col">{{ formattedArabicDate(paymentDate) }}</td>
            <td class="lbl-col font-bg">التاريخ</td>
          </tr>
        </tbody>
      </table>

      <!-- جدول تفاصيل العملاء -->
      <table class="receipt-table items-table">
        <thead>
          <tr v-if="showType === 'المسددين'">
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
            <template v-if="showType === 'المسددين'">
              <td class="customer-name">{{ item.customerName }}</td>
              <td class="number-col">{{ formattedNumber(item.amountDaySales) }}</td>
              <td class="number-col">{{ formattedNumber(item.amountReceipt) }}</td>
            </template>
            <template v-else>
              <td class="customer-name">{{ item.customerName }}</td>
              <td class="number-col">{{ item.numberOfDayPayment || 0 }}</td>
              <td class="number-col">{{ formattedNumber(item.amountDaySales) }}</td>
            </template>
          </tr>
        </tbody>
      </table>

      <!-- جدول إجماليات الوصل -->
      <table class="receipt-table summary-table">
        <tbody>
          <template v-if="showType === 'المسددين'">
            <tr>
              <td class="val-col highlight-val">{{ formattedNumber(totalInstallment) }}</td>
              <td class="lbl-col font-bg">مجموع القسط</td>
            </tr>
            <tr>
              <td class="val-col highlight-val">{{ formattedNumber(totalPayments) }}</td>
              <td class="lbl-col font-bg">مجموع التسديدات</td>
            </tr>
          </template>
          <template v-else>
            <tr>
              <td class="val-col highlight-val">{{ filteredCustomers.length }}</td>
              <td class="lbl-col font-bg">عدد العملاء</td>
            </tr>
            <tr>
              <td class="val-col highlight-val">{{ formattedNumber(totalInstallment) }}</td>
              <td class="lbl-col font-bg">مجموع القسط</td>
            </tr>
          </template>
        </tbody>
      </table>

      <!-- تذييل الوصل للجمالية والاحترافية -->
      <div class="receipt-footer">
        <div class="divider">***</div>
        <div class="footer-note">شركة قلعة الضمان لأنظمة الإدارة والتقسيط</div>
      </div>
    </div>
  </div>
</template>

<style>
/* Global print styles to completely clean the print page and prevent any dark mode leaks */
@media print {
  /* Hide the vertical menu, header, footer, customizer, sidebars, and native dialog elements */
  .layout-wrapper:not(.layout-blank),
  .layout-navbar,
  .layout-navigation,
  .layout-vertical-nav,
  .layout-header,
  .layout-footer,
  .customizer-trigger,
  .v-navigation-drawer,
  .v-app-bar,
  .v-system-bar,
  .no-print,
  .v-overlay-container,
  .v-overlay {
    display: none !important;
    visibility: hidden !important;
    opacity: 0 !important;
    height: 0 !important;
    width: 0 !important;
    padding: 0 !important;
    margin: 0 !important;
    overflow: hidden !important;
  }

  /* Reset page margins */
  @page {
    size: auto !important;
    margin: 0 !important;
  }

  html, body {
    background-color: #ffffff !important;
    color: #000000 !important;
    margin: 0 !important;
    padding: 0 !important;
    width: 100% !important;
    min-width: 100% !important;
    height: auto !important;
    min-height: 0 !important;
    display: block !important;
    font-family: 'Cairo', 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif !important;
  }

  /* Reset all background colors and text colors inside the print page to guarantee black on white */
  .print-page-wrapper,
  .receipt-container,
  .receipt-table,
  .receipt-table th,
  .receipt-table td,
  .receipt-footer,
  .divider,
  .footer-note,
  .customer-name,
  .number-col,
  .val-col,
  .highlight-val {
    background-color: #ffffff !important;
    color: #000000 !important;
    border-color: #000000 !important;
    opacity: 1 !important;
  }

  .receipt-table th,
  .lbl-col,
  .font-bg {
    background-color: #e0e0e0 !important;
    color: #000000 !important;
    -webkit-print-color-adjust: exact !important;
    print-color-adjust: exact !important;
  }

  .print-page-wrapper {
    background-color: #ffffff !important;
    padding: 0 !important;
    margin: 0 !important;
    width: 100% !important;
    min-width: 100% !important;
    min-height: auto !important;
    display: block !important;
  }

  .receipt-container {
    width: 80mm !important;
    max-width: 80mm !important;
    min-width: 80mm !important;
    box-shadow: none !important;
    padding: 4mm 6mm !important;
    margin: 0 auto !important; /* Centering the 80mm receipt */
    border: none !important;
    display: flex !important;
    flex-direction: column !important;
    align-items: center !important;
  }

  .receipt-table {
    display: table !important;
    width: 100% !important;
    border-collapse: collapse !important;
    margin-bottom: 4mm !important;
  }

  .receipt-table tr {
    display: table-row !important;
  }

  .receipt-table td,
  .receipt-table th {
    display: table-cell !important;
    border: 2px solid #000000 !important;
    padding: 6px 4px !important;
    text-align: center !important;
    font-weight: bold !important;
    font-size: 13px !important;
  }
}
</style>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Cairo:wght@400;600;700&display=swap');

.print-page-wrapper {
  direction: rtl;
  font-family: 'Cairo', 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  color: #000000;
  background-color: #f0f0f0;
  min-height: 100vh;
  padding: 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
}

/* شريط الأزرار العلوية */
.action-bar {
  display: flex;
  gap: 15px;
  margin-bottom: 20px;
  z-index: 100;
}

.action-btn {
  padding: 10px 20px;
  border-radius: 8px;
  border: none;
  font-family: 'Cairo', sans-serif;
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  box-shadow: 0 4px 6px rgba(0,0,0,0.1);
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 8px;
}

.btn-icon {
  font-size: 16px;
}

.print-btn {
  background-color: #6200ee;
  color: #ffffff;
}

.print-btn:hover {
  background-color: #4b00d1;
  transform: translateY(-2px);
}

.close-btn {
  background-color: #ffffff;
  color: #333333;
  border: 1px solid #cccccc;
}

.close-btn:hover {
  background-color: #eeeeee;
  transform: translateY(-2px);
}

/* حالة التحميل */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  margin-top: 100px;
}

.spinner {
  border: 4px solid rgba(0, 0, 0, 0.1);
  width: 36px;
  height: 36px;
  border-radius: 50%;
  border-left-color: #6200ee;
  animation: spin 1s linear infinite;
  margin-bottom: 15px;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.loading-text {
  font-weight: 600;
  color: #666;
}

/* محاكاة حاوية الوصل على الشاشة */
.receipt-container {
  width: 80mm;
  background-color: #ffffff;
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.15);
  padding: 4mm 6mm;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.receipt-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 4mm;
}

.receipt-table td, .receipt-table th {
  border: 2px solid #000000;
  padding: 6px 4px;
  text-align: center;
  font-weight: bold;
  font-size: 13px;
  color: #000000;
}

.receipt-table th {
  background-color: #e0e0e0;
}

.lbl-col {
  background-color: #e0e0e0;
  width: 40%;
}

.val-col {
  width: 60%;
  background-color: #ffffff;
}

.font-bg {
  background-color: #e0e0e0;
}

.customer-name {
  text-align: right !important;
  padding-right: 6px !important;
  font-size: 12px !important;
}

.number-col {
  font-size: 12px !important;
}

.highlight-val {
  background-color: #ffffff;
  color: #000000;
}

.receipt-footer {
  width: 100%;
  text-align: center;
  margin-top: 2mm;
}

.divider {
  font-size: 12px;
  font-weight: bold;
  letter-spacing: 2px;
  margin-bottom: 5px;
}

.footer-note {
  font-size: 10px;
  font-weight: bold;
  color: #333333;
}
</style>
