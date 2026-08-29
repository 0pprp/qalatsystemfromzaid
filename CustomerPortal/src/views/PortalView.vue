<template>
  <div class="portal-app">

    <!-- ════════════════ شاشة التحميل ════════════════ -->
    <div v-if="loading" class="loading-screen">
      <div class="loading-content">
        <div class="logo-pulse">
          <v-icon icon="mdi-shield-account" size="72" color="#1565C0" />
        </div>
        <div class="loading-skeleton">
          <div class="skeleton-line skeleton-name" />
          <div class="skeleton-line skeleton-balance" />
          <div class="skeleton-line skeleton-info" />
          <div class="skeleton-cards">
            <div class="skeleton-card" v-for="i in 4" :key="i" />
          </div>
        </div>
        <p class="loading-text">جاري تحميل بيانات حسابكم...</p>
      </div>
    </div>

    <!-- ════════════════ شاشة الخطأ ════════════════ -->
    <div v-else-if="errorMessage" class="error-screen">
      <div class="error-card">
        <div class="error-icon-ring">
          <v-icon icon="mdi-alert-circle-outline" size="80" color="#EF4444" />
        </div>
        <h2 class="error-title">عذراً</h2>
        <p class="error-text">{{ errorMessage }}</p>
        <div class="error-divider" />
        <p class="error-hint">يرجى التواصل مع خدمة العملاء للحصول على المساعدة</p>
      </div>
    </div>

    <!-- ════════════════ المحتوى الرئيسي ════════════════ -->
    <div v-else class="portal-main">
      <!-- Hero Header -->
      <header class="hero-header">
        <div class="hero-bg" />
        <div class="hero-pattern" />
        <div class="hero-content">
          <div class="hero-top-row">
            <div class="brand-section">
              <div class="brand-icon-box">
                <v-icon icon="mdi-shield-check" size="28" color="#F59E0B" />
              </div>
              <div class="brand-text">
                <h1 class="brand-title">صفحة كشف حساب العميل</h1>
                <span class="brand-subtitle">نظام متابعة الحسابات والمبيعات</span>
              </div>
            </div>
            <div class="hero-chips">
              <v-chip
                v-if="cityName"
                size="small"
                variant="flat"
                class="hero-chip hero-chip-city"
              >
                <v-icon icon="mdi-city" size="16" class="ml-1" />
                {{ cityName }}
              </v-chip>
              <!-- Theme Toggle -->
              <v-tooltip
                :text="isDark ? 'التبديل إلى الوضع المضيء' : 'التبديل إلى الوضع الداكن'"
                location="bottom"
                :open-delay="500"
              >
                <template #activator="{ props: tipProps }">
                  <button
                    v-bind="tipProps"
                    class="theme-toggle-btn"
                    @click="toggleTheme"
                    :aria-label="isDark ? 'تفعيل الوضع المضيء' : 'تفعيل الوضع الداكن'"
                  >
                    <v-icon :icon="isDark ? 'mdi-white-balance-sunny' : 'mdi-weather-night'" size="18" />
                  </button>
                </template>
              </v-tooltip>
              <!-- متجر التسويق الإلكتروني — مفعل عند الإطلاق -->
              <v-tooltip
                text="سيتم تفعيل المتجر الإلكتروني قريبًا بعد إطلاق المنصة. يمكنك من خلاله استعراض جميع المنتجات المتوفرة، ومعرفة أسعارها وتفاصيلها، ومتابعة حالة الشحن والشراء من مكان واحد."
                location="bottom"
                :open-delay="300"
              >
                <template #activator="{ props: tooltipProps }">
                  <button
                    v-bind="tooltipProps"
                    class="hero-store-btn"
                    disabled
                    aria-label="المتجر الإلكتروني — قيد التجهيز"
                  >
                    <v-icon icon="mdi-shopping-outline" size="18" />
                    <span>تصفح المتجر</span>
                  </button>
                </template>
              </v-tooltip>
            </div>
          </div>

          <!-- Customer Identity -->
          <div class="hero-identity">
            <div class="avatar-ring">
              <v-avatar size="72" color="#1565C0" class="hero-avatar">
                <span class="avatar-text">{{ avatarInitials }}</span>
              </v-avatar>
            </div>
            <div class="identity-info">
              <h2 class="customer-name">{{ customer.customerName }}</h2>
              <div class="identity-meta">
                <span v-if="customer.phoneNumber" class="meta-item">
                  <v-icon icon="mdi-phone" size="14" color="#94a3b8" />
                  {{ customer.phoneNumber }}
                </span>
                <span v-if="customer.delegateName" class="meta-item">
                  <v-icon icon="mdi-account-tie" size="14" color="#94a3b8" />
                  {{ customer.delegateName }}
                </span>
              </div>
            </div>
          </div>
        </div>
        <div class="hero-wave">
          <svg viewBox="0 0 1440 60" preserveAspectRatio="none">
            <path d="M0,30 C360,60 1080,0 1440,30 L1440,60 L0,60 Z" fill="#f0f4f8" />
          </svg>
        </div>
      </header>

      <!-- Main Container -->
      <div class="main-container">
        <!-- ════ المخلص المالي - Financial Summary ════ -->
        <section class="stats-section">
          <div class="section-label">
            <v-icon icon="mdi-chart-pie" size="18" color="#64748b" />
            <span>الملخص المالي</span>
          </div>
          <div class="stats-grid">
            <div class="stat-card stat-sales">
              <div class="stat-top">
                <div class="stat-icon-box">
                  <v-icon icon="mdi-cart-arrow-down" size="22" color="#1565C0" />
                </div>
                <span class="stat-label">سعر البيع</span>
              </div>
              <div class="stat-value-wrap">
                <span class="stat-value">{{ formatCurrency(customer.amountTotalSales) }}</span>
              </div>
            </div>
            <div class="stat-card stat-paid">
              <div class="stat-top">
                <div class="stat-icon-box">
                  <v-icon icon="mdi-check-decagram" size="22" color="#10B981" />
                </div>
                <span class="stat-label">مجموع الواصل</span>
              </div>
              <div class="stat-value-wrap">
                <span class="stat-value">{{ formatCurrency(customer.receiptsTotal) }}</span>
              </div>
            </div>
            <div class="stat-card stat-remaining">
              <div class="stat-top">
                <div class="stat-icon-box">
                  <v-icon icon="mdi-clock-outline" size="22" color="#EF4444" />
                </div>
                <span class="stat-label">المبلغ المتبقي</span>
              </div>
              <div class="stat-value-wrap">
                <span class="stat-value">{{ formatCurrency(customer.amountRemaining) }}</span>
              </div>
            </div>
            <div class="stat-card stat-daily">
              <div class="stat-top">
                <div class="stat-icon-box">
                  <v-icon icon="mdi-calendar-month" size="22" color="#F59E0B" />
                </div>
                <span class="stat-label">القسط اليومي</span>
              </div>
              <div class="stat-value-wrap">
                <span class="stat-value">{{ formatCurrency(customer.amountDaySales) }}</span>
              </div>
            </div>
          </div>
          <!-- مبيعات الزبون (بدون فلترة) -->
          <div class="sales-mini-section" v-if="sales.length">
            <div class="section-label">
              <v-icon icon="mdi-shopping" size="18" color="#64748b" />
              <span>المبيعات</span>
              <span class="tab-badge">{{ sales.length }}</span>
            </div>
            <div class="table-wrap desktop-only">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>التاريخ</th>
                    <th>المبلغ</th>
                    <th>المواد</th>
                    <th>البائع</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(item, i) in sales" :key="item.customerSaleID">
                    <td class="td-num">{{ i + 1 }}</td>
                    <td>{{ formatDate(item.dateCreate) }}</td>
                    <td class="td-amount sales-amount">{{ formatCurrency(item.amountTotalSalesDenar || item.amountTotalSales) }}</td>
                    <td class="td-items" :title="item.itemsNames">{{ item.itemsNames || '-' }}</td>
                    <td>{{ item.saleName || '-' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div class="mobile-cards mobile-only">
              <div class="sale-card" v-for="(item, i) in sales" :key="item.customerSaleID">
                <div class="sale-card-header">
                  <span class="sale-card-num">#{{ i + 1 }}</span>
                  <span class="sale-card-date">{{ formatDate(item.dateCreate) }}</span>
                </div>
                <div class="sale-card-body">
                  <div class="sale-card-row">
                    <span class="sale-card-label">المبلغ</span>
                    <span class="sale-card-value sales-amount">{{ formatCurrency(item.amountTotalSalesDenar || item.amountTotalSales) }}</span>
                  </div>
                </div>
                <div class="sale-card-footer" v-if="item.itemsNames || item.saleName">
                  <span v-if="item.itemsNames" class="sale-card-items">{{ item.itemsNames }}</span>
                  <span v-if="item.saleName" class="sale-card-seller">{{ item.saleName }}</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <!-- ════ معلومات العميل - Customer Details ════ -->
        <section class="details-section">
          <button
            class="accordion-toggle"
            :class="{ open: detailsOpen }"
            @click="detailsOpen = !detailsOpen"
          >
            <div class="accordion-header">
              <v-icon icon="mdi-card-account-details-outline" size="18" color="#64748b" />
              <span>معلومات الحساب</span>
            </div>
            <v-icon
              :icon="detailsOpen ? 'mdi-chevron-up' : 'mdi-chevron-down'"
              size="22"
              color="#64748b"
            />
          </button>
          <div class="accordion-body" v-show="detailsOpen">
            <div class="details-grid">
              <div class="detail-item" v-if="customer.phoneNumber">
                <v-icon icon="mdi-phone-outline" size="20" color="#10B981" />
                <div class="detail-content">
                  <span class="detail-label">رقم الهاتف</span>
                  <a :href="`tel:${customer.phoneNumber}`" class="detail-value link">{{ customer.phoneNumber }}</a>
                </div>
              </div>
              <div class="detail-item" v-if="customer.address">
                <v-icon icon="mdi-map-marker-outline" size="20" color="#EF4444" />
                <div class="detail-content">
                  <span class="detail-label">العنوان</span>
                  <span class="detail-value">{{ customer.address }}</span>
                </div>
              </div>
              <div class="detail-item" v-if="customer.shopName">
                <v-icon icon="mdi-storefront-outline" size="20" color="#1565C0" />
                <div class="detail-content">
                  <span class="detail-label">اسم المحل</span>
                  <span class="detail-value">{{ customer.shopName }}</span>
                </div>
              </div>
              <div class="detail-item" v-if="customer.storeAddress">
                <v-icon icon="mdi-warehouse" size="20" color="#F59E0B" />
                <div class="detail-content">
                  <span class="detail-label">عنوان المخزن</span>
                  <span class="detail-value">{{ customer.storeAddress }}</span>
                </div>
              </div>
              <div class="detail-item" v-if="customer.nearestFunctionPoint">
                <v-icon icon="mdi-crosshairs-gps" size="20" color="#3B82F6" />
                <div class="detail-content">
                  <span class="detail-label">أقرب نقطة دالة</span>
                  <span class="detail-value">{{ customer.nearestFunctionPoint }}</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <!-- ════ التسديدات ════ -->
        <section class="data-section">
          <div class="data-tabs" style="justify-content: center;">
            <div class="data-tab active" style="pointer-events: none; border-bottom: none;">
              <v-icon icon="mdi-credit-card-check" size="20" />
              <span>التسديدات</span>
              <span class="tab-badge">{{ payments.length }}</span>
            </div>
          </div>

          <!-- Date Filter -->
          <div class="filter-bar">
            <div class="filter-label">
              <v-icon icon="mdi-calendar-filter" size="18" color="#64748b" />
              <span>تصفية حسب التاريخ</span>
            </div>
            <div class="filter-inputs">
              <div class="date-input-wrap">
                <v-icon icon="mdi-calendar-start" size="16" color="#94a3b8" />
                <input
                  v-model="dateFilter.from"
                  type="date"
                  class="date-input"
                  :max="dateFilter.to || todayStr"
                />
              </div>
              <span class="date-separator">إلى</span>
              <div class="date-input-wrap">
                <v-icon icon="mdi-calendar-end" size="16" color="#94a3b8" />
                <input
                  v-model="dateFilter.to"
                  type="date"
                  class="date-input"
                  :min="dateFilter.from"
                  :max="todayStr"
                />
              </div>
              <button class="btn-filter" @click="applyDateFilter" :disabled="tableLoading">
                <v-icon icon="mdi-magnify" size="18" />
                <span>تطبيق</span>
              </button>
              <button
                class="btn-reset"
                @click="resetDateFilter"
                :disabled="!dateFilter.from && !dateFilter.to"
              >
                <v-icon icon="mdi-close-circle" size="18" />
                <span>مسح</span>
              </button>
            </div>
          </div>

          <!-- Loading Overlay -->
          <div class="table-wrap" v-if="tableLoading">
            <div class="table-skeleton">
              <div class="sk-row" v-for="i in 5" :key="i">
                <div class="sk-cell" v-for="j in 4" :key="j" />
              </div>
            </div>
          </div>

          <template v-else>
            <!-- Desktop Payments Table -->
            <div class="table-wrap desktop-only">
              <table class="data-table" v-if="payments.length">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>التاريخ</th>
                    <th>المبلغ</th>
                    <th>المندوب</th>
                    <th>الموظف</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(item, i) in payments" :key="item.customerPaymentID">
                    <td class="td-num">{{ i + 1 }}</td>
                    <td>{{ formatDate(item.paymentDate) }}</td>
                    <td class="td-amount paid-amount">{{ formatCurrency(item.amountDenar) }}</td>
                    <td>{{ item.delegateName || '-' }}</td>
                    <td>{{ item.userName || '-' }}</td>
                  </tr>
                </tbody>
              </table>
              <div class="table-empty" v-else>
                <v-icon icon="mdi-inbox-outline" size="48" color="#94a3b8" />
                <p>لا توجد تسديدات للفترة المحددة</p>
              </div>
            </div>

            <!-- Mobile Payments Cards -->
            <div class="mobile-cards mobile-only">
              <div class="sale-card" v-for="(item, i) in payments" :key="item.customerPaymentID">
                <div class="sale-card-header">
                  <span class="sale-card-num">#{{ i + 1 }}</span>
                  <span class="sale-card-date">{{ formatDate(item.paymentDate) }}</span>
                </div>
                <div class="sale-card-body">
                  <div class="sale-card-row">
                    <span class="sale-card-label">المبلغ</span>
                    <span class="sale-card-value paid-amount">{{ formatCurrency(item.amountDenar) }}</span>
                  </div>
                </div>
                <div class="sale-card-footer">
                  <span v-if="item.delegateName">المندوب: {{ item.delegateName }}</span>
                  <span v-if="item.userName">الموظف: {{ item.userName }}</span>
                </div>
              </div>
              <div class="table-empty" v-if="!payments.length">
                <v-icon icon="mdi-inbox-outline" size="48" color="#94a3b8" />
                <p>لا توجد تسديدات للفترة المحددة</p>
              </div>
            </div>
          </template>
        </section>

        <!-- Footer -->
        <footer class="portal-footer">
          <div class="footer-brand">
            <v-icon icon="mdi-shield-check" size="20" color="#F59E0B" />
            <span>SalesHaider</span>
          </div>
          <p class="footer-copy">&copy; {{ new Date().getFullYear() }} جميع الحقوق محفوظة</p>
        </footer>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()

// ── City/Branch Mapping ───────────────────────────────
const CITY_API_MAP = {
  1: 'http://sharenewnajaf.alsaaeidy.com/api',
  2: 'http://sharenewrosafa.alsaaeidy.com/api',
  3: 'http://sharenewrkarak.alsaaeidy.com/api',
  4: 'http://sharenewkarbala.alsaaeidy.com/api',
  5: 'http://sharenewrbabil.alsaaeidy.com/api',
  6: 'http://sharenewdewania.alsaaeidy.com/api',
  7: 'http://sharenewkot.alsaaeidy.com/api',
  8: 'http://sharenewnasria.alsaaeidy.com/api',
  9: 'http://sharenewrbasra.alsaaeidy.com/api',
  10: 'http://sharenewmothana.alsaaeidy.com/api',
  11: 'http://sharenewrdeiala.alsaaeidy.com/api',
  12: 'http://sharenewrbasranwar.alsaaeidy.com/api',
  13: 'http://sharenewmusol.alsaaeidy.com/api',
  14: 'http://sharenewrkarkok.alsaaeidy.com/api',
  15: 'http://shortnewrosafaaqeel.alsaaeidy.com/api',
  16: 'http://sharenewmaysanl.alsaaeidy.com/api',
  17: 'http://beadminstop.alsaaeidy.com/api',
  18: 'http://shortnewkarakaqeel.alsaaeidy.com/api',
  19: 'http://testapp.alsaaeidy.com/api',
  20: 'http://monthmenu.alsaaeidy.com/api',
}

const CITY_NAMES = {
  1: 'النجف', 2: 'الرصافة', 3: 'الكرخ', 4: 'كربلاء', 5: 'الحلة',
  6: 'الديوانية', 7: 'الكوت', 8: 'الناصرية', 9: 'البصرة', 10: 'المثنى',
  11: 'ديالى', 12: 'الانوار', 13: 'الموصل', 14: 'كركوك', 15: 'الرصافة عقيل',
  16: 'ميسان عقيل', 17: 'قانونية الشركة', 18: 'الكرخ عقيل', 19: 'تجريبي', 20: 'الشهري',
}

const cityId = computed(() => {
  const c = parseInt(route.query.city)
  return CITY_API_MAP[c] ? c : null
})

const apiBase = computed(() => {
  if (cityId.value) return CITY_API_MAP[cityId.value]
  return import.meta.env.VITE_API_BASE_URL || '/api'
})

const cityName = computed(() => {
  if (cityId.value) return CITY_NAMES[cityId.value]
  return ''
})

// ── State ─────────────────────────────────────────────
const loading = ref(true)
const errorMessage = ref('')
const customer = ref({})
const sales = ref([])
const payments = ref([])
const tableLoading = ref(false)
const detailsOpen = ref(false)

// ── Dark/Light Mode ───────────────────────────────────
const isDark = ref(false)

function applyTheme() {
  document.documentElement.classList.toggle('portal-dark', isDark.value)
  try { localStorage.setItem('portal-theme', isDark.value ? 'dark' : 'light') } catch (_) { /* noop */ }
}

function toggleTheme() {
  isDark.value = !isDark.value
  applyTheme()
}

function initTheme() {
  try {
    const saved = localStorage.getItem('portal-theme')
    if (saved === 'dark') isDark.value = true
    else if (saved === 'light') isDark.value = false
    else isDark.value = window.matchMedia('(prefers-color-scheme: dark)').matches
  } catch (_) {
    isDark.value = window.matchMedia('(prefers-color-scheme: dark)').matches
  }
  applyTheme()
}

initTheme()

const todayStr = computed(() => new Date().toISOString().slice(0, 10))

const dateFilter = ref({ from: '', to: '' })

// ── Avatar Initials ───────────────────────────────────
const avatarInitials = computed(() => {
  const name = customer.value.customerName || ''
  const parts = name.trim().split(/\s+/)
  if (parts.length >= 2) return parts[0][0] + parts[parts.length - 1][0]
  return name.slice(0, 2)
})

// ── Helpers ───────────────────────────────────────────
function formatCurrency(value) {
  if (value == null) return '0 د.ع'
  const num = Number(value)
  return num.toLocaleString('en-US', { maximumFractionDigits: 0 }) + ' د.ع'
}

function formatDate(dateStr) {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleDateString('ar-IQ-u-nu-latn', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

// ── API ───────────────────────────────────────────────
async function apiCall(endpoint, params = {}) {
  const url = new URL(`${apiBase.value}/Portal/${endpoint}`, window.location.origin)
  url.searchParams.set('token', token.value)
  Object.entries(params).forEach(([key, value]) => {
    if (value) url.searchParams.set(key, value)
  })
  const response = await fetch(url.toString())
  if (!response.ok) {
    const err = await response.json().catch(() => ({ error: 'حدث خطأ غير متوقع' }))
    throw new Error(err.error || `خطأ ${response.status}`)
  }
  return response.json()
}

// ── Loaders ───────────────────────────────────────────
async function loadCustomerInfo() {
  const data = await apiCall('CustomerInfo')
  customer.value = data
}

async function loadSales(fromDate, toDate) {
  const params = {}
  if (fromDate) params.fromDate = fromDate
  if (toDate) params.toDate = toDate
  const data = await apiCall('Sales', params)
  sales.value = data || []
}

async function loadPayments(fromDate, toDate) {
  const params = {}
  if (fromDate) params.fromDate = fromDate
  if (toDate) params.toDate = toDate
  const data = await apiCall('Payments', params)
  payments.value = data || []
}

// ── Date Filter ───────────────────────────────────────
async function applyDateFilter() {
  tableLoading.value = true
  try {
    await loadPayments(dateFilter.value.from || undefined, dateFilter.value.to || undefined)
  } finally {
    tableLoading.value = false
  }
}

async function resetDateFilter() {
  dateFilter.value.from = ''
  dateFilter.value.to = ''
  await applyDateFilter()
}

// ── Token ─────────────────────────────────────────────
const token = computed(() => route.query.token || '')

// ── Init ──────────────────────────────────────────────
onMounted(async () => {
  if (!token.value) {
    loading.value = false
    errorMessage.value = 'الرابط غير صالح - المعرف مفقود'
    return
  }
  if (!cityId.value) {
    loading.value = false
    errorMessage.value = 'الرابط غير صالح - معرف الفرع غير معروف'
    return
  }
  try {
    await loadCustomerInfo()
    await Promise.allSettled([loadSales(), loadPayments()])
    loading.value = false
  } catch (err) {
    loading.value = false
    errorMessage.value = err.message || 'حدث خطأ أثناء تحميل البيانات'
  }
})
</script>

<style scoped>
/* ═════════════════════════════════════════════════════════
   PREMIUM PORTAL DESIGN — Cairo Font — RTL
   Dark/Light Mode • Fluid Typography • Fully Responsive
   ════════════════════════════════════════════════════════ */

/* ── CSS Custom Properties: Light Theme (default) ───── */
:root,
.portal-app {
  /* Brand */
  --brand-dark: #0a1929;
  --brand-blue: #1565C0;
  --brand-blue-hover: #1a4f9e;
  --brand-gold: #F59E0B;
  --brand-green: #10B981;
  --brand-red: #EF4444;
  --brand-teal: #06B6D4;

  /* Surfaces */
  --surface-bg: #f0f4f8;
  --surface-card: #ffffff;
  --surface-card-hover: #f8fafc;
  --surface-section: #ffffff;
  --surface-header: #f8fafc;
  --surface-hover: #f1f5f9;

  /* Text */
  --text-primary: #1e293b;
  --text-secondary: #334155;
  --text-muted: #64748b;
  --text-placeholder: #94a3b8;

  /* Borders */
  --border-color: #e2e8f0;
  --border-light: #f1f5f9;

  /* Shadows */
  --shadow-sm: 0 1px 2px rgba(0,0,0,0.04);
  --shadow-md: 0 4px 12px rgba(0,0,0,0.06);
  --shadow-lg: 0 8px 24px rgba(0,0,0,0.08);
  --shadow-card-hover: 0 8px 30px rgba(0,0,0,0.10);

  /* Radius */
  --radius-sm: 10px;
  --radius-md: 14px;
  --radius-lg: 20px;
  --radius-xl: 24px;

  /* Typography */
  --font-cairo: 'Cairo', 'Segoe UI', 'Arial', sans-serif;

  /* Hero (always dark gradient) */
  --hero-gradient: linear-gradient(160deg, #0a1929 0%, #102a43 30%, #1a3a5c 60%, #0d1b2a 100%);

  /* Other */
  --wave-fill: #f0f4f8;
  --skeleton-base: rgba(0,0,0,0.06);
  --skeleton-shine: rgba(0,0,0,0.12);
}

/* ── Dark Theme ─────────────────────────────────────── */
.portal-dark,
html.portal-dark .portal-app {
  /* Brand (keep brand colors mostly intact) */
  --brand-dark: #060e18;
  --brand-blue: #3b82f6;
  --brand-blue-hover: #2563eb;
  --brand-gold: #fbbf24;
  --brand-green: #34d399;
  --brand-red: #f87171;
  --brand-teal: #22d3ee;

  /* Surfaces */
  --surface-bg: #0f172a;
  --surface-card: #1e293b;
  --surface-card-hover: #273548;
  --surface-section: #1e293b;
  --surface-header: #162032;
  --surface-hover: #273548;

  /* Text */
  --text-primary: #e2e8f0;
  --text-secondary: #cbd5e1;
  --text-muted: #94a3b8;
  --text-placeholder: #64748b;

  /* Borders */
  --border-color: #334155;
  --border-light: #1e293b;

  /* Shadows */
  --shadow-sm: 0 1px 2px rgba(0,0,0,0.20);
  --shadow-md: 0 4px 12px rgba(0,0,0,0.30);
  --shadow-lg: 0 8px 24px rgba(0,0,0,0.40);
  --shadow-card-hover: 0 8px 30px rgba(0,0,0,0.50);

  /* Hero (always dark — slight tweak) */
  --hero-gradient: linear-gradient(160deg, #060e18 0%, #0f1f35 30%, #162d4a 60%, #060e18 100%);

  /* Other */
  --wave-fill: #0f172a;
  --skeleton-base: rgba(255,255,255,0.04);
  --skeleton-shine: rgba(255,255,255,0.08);
}

/* ── Base ───────────────────────────────────────────── */
.portal-app {
  --font-cairo: 'Cairo', 'Segoe UI', 'Arial', sans-serif;

  min-height: 100vh;
  background: var(--surface-bg);
  direction: rtl;
  font-family: var(--font-cairo);
  color: var(--text-primary);
  transition: background 0.35s ease, color 0.35s ease;
}

/* ── Loading Screen ─────────────────────────────────── */
.loading-screen {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #0a1929 0%, #1a3a5c 50%, #0d1b2a 100%);
}

.loading-content {
  text-align: center;
  max-width: 420px;
  width: 92%;
}

.logo-pulse {
  animation: pulse-ring 2s ease-in-out infinite;
  margin-bottom: 2rem;
}

@keyframes pulse-ring {
  0%, 100% { transform: scale(1); opacity: 1; }
  50% { transform: scale(1.08); opacity: 0.7; }
}

.loading-skeleton {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 1.5rem;
}

.skeleton-line {
  height: 16px;
  border-radius: 8px;
  background: linear-gradient(90deg, rgba(255,255,255,0.06) 0%, rgba(255,255,255,0.12) 50%, rgba(255,255,255,0.06) 100%);
  background-size: 200% 100%;
  animation: shimmer 1.5s ease-in-out infinite;
}

.skeleton-name { width: 60%; height: 24px; margin: 0 auto; }
.skeleton-balance { width: 40%; height: 28px; margin: 0 auto; }
.skeleton-info { width: 50%; margin: 0 auto; }

.skeleton-cards {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  margin-top: 8px;
}

.skeleton-card {
  height: 80px;
  border-radius: var(--radius-md);
  background: linear-gradient(90deg, rgba(255,255,255,0.05) 0%, rgba(255,255,255,0.10) 50%, rgba(255,255,255,0.05) 100%);
  background-size: 200% 100%;
  animation: shimmer 1.5s ease-in-out infinite;
}

@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

.loading-text {
  color: rgba(255,255,255,0.6);
  font-size: clamp(0.85rem, 2vw, 0.95rem);
  font-family: var(--font-cairo);
}

/* ── Error Screen ───────────────────────────────────── */
.error-screen {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--surface-bg);
  padding: clamp(0.8rem, 2vw, 1.5rem);
}

.error-card {
  background: var(--surface-card);
  border-radius: var(--radius-xl);
  padding: clamp(2rem, 4vw, 3rem) clamp(1.5rem, 3vw, 2.5rem);
  text-align: center;
  max-width: 440px;
  width: 100%;
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--border-color);
  transition: background 0.35s ease, border-color 0.35s ease;
}

.error-icon-ring {
  width: clamp(90px, 15vw, 120px);
  height: clamp(90px, 15vw, 120px);
  margin: 0 auto 1.5rem;
  border-radius: 50%;
  background: #fef2f2;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 3px solid #fecaca;
}

.error-title {
  font-family: var(--font-cairo);
  font-size: clamp(1.3rem, 3vw, 1.6rem);
  font-weight: 800;
  color: var(--brand-red);
  margin-bottom: 0.5rem;
}

.error-text {
  font-family: var(--font-cairo);
  color: var(--text-muted);
  font-size: clamp(0.88rem, 2vw, 0.95rem);
  line-height: 1.7;
}

.error-divider {
  width: 50px;
  height: 3px;
  background: var(--border-color);
  border-radius: 2px;
  margin: 1.5rem auto;
}

.error-hint {
  font-family: var(--font-cairo);
  color: var(--text-placeholder);
  font-size: clamp(0.78rem, 1.8vw, 0.85rem);
}

/* ── Hero Header ────────────────────────────────────── */
.hero-header {
  position: relative;
  overflow: hidden;
}

.hero-bg {
  position: absolute;
  inset: 0;
  background: var(--hero-gradient);
}

.hero-pattern {
  position: absolute;
  inset: 0;
  background-image:
    radial-gradient(circle at 20% 50%, rgba(59,130,246,0.15) 0%, transparent 60%),
    radial-gradient(circle at 80% 20%, rgba(245,158,11,0.08) 0%, transparent 50%),
    radial-gradient(circle at 50% 80%, rgba(6,182,212,0.08) 0%, transparent 50%);
}

.hero-content {
  position: relative;
  z-index: 1;
  padding: clamp(1rem, 3vw, 1.8rem) clamp(0.8rem, 3vw, 2rem) clamp(2rem, 5vw, 3.5rem);
  max-width: 1100px;
  margin: 0 auto;
}

.hero-top-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: clamp(0.6rem, 2vw, 1rem);
  margin-bottom: clamp(1.2rem, 3vw, 2rem);
}

.brand-section { display: flex; align-items: center; gap: clamp(8px, 2vw, 14px); }

.brand-icon-box {
  width: clamp(38px, 6vw, 48px);
  height: clamp(38px, 6vw, 48px);
  border-radius: var(--radius-md);
  background: rgba(245,158,11,0.15);
  display: flex;
  align-items: center;
  justify-content: center;
  backdrop-filter: blur(8px);
  flex-shrink: 0;
}

.brand-title {
  font-family: var(--font-cairo);
  font-size: clamp(1.1rem, 3vw, 1.5rem);
  font-weight: 800;
  color: #fff;
  letter-spacing: -0.5px;
}

.brand-subtitle {
  font-family: var(--font-cairo);
  font-size: clamp(0.68rem, 1.6vw, 0.8rem);
  color: rgba(255,255,255,0.55);
}

.hero-chips { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }

.hero-chip {
  font-family: var(--font-cairo) !important;
  font-weight: 600 !important;
  font-size: 0.8rem !important;
  backdrop-filter: blur(8px);
  height: 32px !important;
}

.hero-chip-city {
  background: rgba(255,255,255,0.12) !important;
  color: rgba(255,255,255,0.9) !important;
  border: 1px solid rgba(255,255,255,0.1);
}

/* ── Theme Toggle Button ────────────────────────────── */
.theme-toggle-btn {
  width: 34px;
  height: 34px;
  border-radius: 50%;
  border: 1px solid rgba(255,255,255,0.18);
  background: rgba(255,255,255,0.08);
  color: rgba(255,255,255,0.8);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  backdrop-filter: blur(8px);
  transition: all 0.3s ease;
  flex-shrink: 0;
}

.theme-toggle-btn:hover {
  background: rgba(255,255,255,0.16);
  border-color: rgba(255,255,255,0.35);
  color: #fcd34d;
  transform: rotate(15deg);
}

/* ── متجر التسويق الإلكتروني ──────────────────────── */
.hero-store-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 50px;
  border: 1px dashed rgba(245,158,11,0.5);
  background: rgba(245,158,11,0.10);
  color: rgba(255,255,255,0.55);
  font-family: var(--font-cairo);
  font-size: 0.8rem;
  font-weight: 600;
  cursor: not-allowed;
  backdrop-filter: blur(8px);
  transition: all 0.3s ease;
  white-space: nowrap;
  line-height: 1.5;
}

.hero-store-btn:hover {
  background: rgba(245,158,11,0.15);
  border-color: rgba(245,158,11,0.7);
}

.hero-store-btn:not(:disabled),
.hero-store-btn.active {
  background: rgba(245,158,11,0.20);
  color: #fcd34d;
  border-style: solid;
  cursor: pointer;
}

.hero-identity {
  display: flex;
  align-items: center;
  gap: clamp(0.8rem, 2vw, 1.2rem);
}

.avatar-ring {
  padding: 3px;
  border-radius: 50%;
  background: linear-gradient(135deg, #F59E0B, #3b82f6);
  flex-shrink: 0;
}

.hero-avatar {
  border: 3px solid #0a1929;
}

.avatar-text {
  font-family: var(--font-cairo);
  font-size: clamp(1rem, 2.5vw, 1.5rem);
  font-weight: 800;
  color: #fff;
  text-transform: uppercase;
}

.customer-name {
  font-family: var(--font-cairo);
  font-size: clamp(1.15rem, 3vw, 1.6rem);
  font-weight: 800;
  color: #fff;
  line-height: 1.3;
}

.identity-meta {
  display: flex;
  gap: clamp(0.6rem, 2vw, 1.2rem);
  margin-top: 4px;
  flex-wrap: wrap;
}

.meta-item {
  font-family: var(--font-cairo);
  font-size: clamp(0.72rem, 1.6vw, 0.82rem);
  color: rgba(255,255,255,0.65);
  display: flex;
  align-items: center;
  gap: 4px;
}

.hero-wave {
  position: relative;
  z-index: 1;
  margin-top: -1px;
  line-height: 0;
}

.hero-wave svg {
  width: 100%;
  height: clamp(28px, 5vw, 48px);
}

.hero-wave svg path {
  fill: var(--wave-fill);
  transition: fill 0.35s ease;
}

/* ── Main Container ─────────────────────────────────── */
.main-container {
  max-width: 1100px;
  margin: 0 auto;
  padding: 0 clamp(0.8rem, 2vw, 1.5rem) clamp(1.5rem, 4vw, 2.5rem);
}

/* ── Section Label ──────────────────────────────────── */
.section-label {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 1rem;
  font-family: var(--font-cairo);
  font-size: clamp(0.85rem, 2vw, 0.95rem);
  font-weight: 700;
  color: var(--text-muted);
}

/* ── Stats Section ──────────────────────────────────── */
.stats-section {
  margin-top: -10px;
  position: relative;
  z-index: 2;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: clamp(0.5rem, 1.5vw, 1rem);
}

.stat-card {
  background: var(--surface-card);
  border-radius: var(--radius-lg);
  padding: clamp(0.9rem, 2vw, 1.5rem) clamp(0.7rem, 1.5vw, 1.3rem);
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
  box-shadow: var(--shadow-sm);
  transition: all 0.3s ease;
  border: 1px solid var(--border-color);
  position: relative;
  overflow: hidden;
}

.stat-card::before {
  content: '';
  position: absolute;
  inset: 0;
  opacity: 0;
  background: linear-gradient(135deg, var(--brand-blue) 0%, transparent 60%);
  transition: opacity 0.3s ease;
  pointer-events: none;
}

.stat-card:hover {
  transform: translateY(-3px);
  box-shadow: var(--shadow-card-hover);
}

.stat-card:hover::before { opacity: 0.02; }

.stat-top {
  display: flex;
  align-items: center;
  gap: clamp(7px, 1.5vw, 10px);
}

.stat-icon-box {
  width: clamp(36px, 5vw, 44px);
  height: clamp(36px, 5vw, 44px);
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: transform 0.3s ease;
}

.stat-card:hover .stat-icon-box { transform: scale(1.08); }

.stat-sales .stat-icon-box { background: #eff6ff; }
.stat-paid .stat-icon-box { background: #ecfdf5; }
.stat-remaining .stat-icon-box { background: #fef2f2; }
.stat-daily .stat-icon-box { background: #fffbeb; }

.stat-label {
  font-family: var(--font-cairo);
  font-size: clamp(0.73rem, 1.5vw, 0.84rem);
  font-weight: 600;
  color: var(--text-muted);
  line-height: 1.3;
}

.stat-value-wrap {
  padding-top: 0.5rem;
  border-top: 1px solid var(--border-light);
  transition: border-color 0.35s ease;
}

.stat-value {
  font-family: var(--font-cairo);
  font-size: clamp(1rem, 2.5vw, 1.4rem);
  font-weight: 800;
  color: var(--text-primary);
  text-align: right;
  display: block;
  transition: color 0.35s ease;
}

/* ── Sales Mini Section (unfiltered, under stats) ──── */
.sales-mini-section {
  margin-top: clamp(1rem, 2vw, 1.5rem);
  background: var(--surface-card);
  border-radius: var(--radius-lg);
  padding: clamp(1rem, 2vw, 1.5rem);
  box-shadow: var(--shadow-sm);
  border: 1px solid var(--border-color);
  transition: background 0.35s ease, border-color 0.35s ease;
}

.sales-mini-section .section-label {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 0.75rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid var(--border-light);
  transition: border-color 0.35s ease;
}

.sales-mini-section .section-label span {
  font-family: var(--font-cairo);
  font-size: clamp(0.8rem, 1.8vw, 0.9rem);
  font-weight: 700;
  color: var(--text-muted);
}

.sales-mini-section .tab-badge {
  font-family: var(--font-cairo);
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--brand-blue);
  background: #eff6ff;
  padding: 2px 10px;
  border-radius: 50px;
}

/* ── Details Section ────────────────────────────────── */
.details-section {
  margin-top: clamp(1rem, 2vw, 1.5rem);
  background: var(--surface-card);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  border: 1px solid var(--border-color);
  overflow: hidden;
  transition: background 0.35s ease, border-color 0.35s ease;
}

.accordion-toggle {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: clamp(0.8rem, 2vw, 1rem) clamp(1rem, 2vw, 1.5rem);
  background: none;
  border: none;
  cursor: pointer;
  font-family: var(--font-cairo);
  transition: background 0.2s;
  color: var(--text-muted);
}

.accordion-toggle:hover { background: var(--surface-hover); }

.accordion-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: clamp(0.82rem, 1.8vw, 0.92rem);
  font-weight: 700;
  color: var(--text-muted);
}

.accordion-body {
  padding: 0 clamp(1rem, 2vw, 1.5rem) clamp(1rem, 2vw, 1.5rem);
}

.details-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: clamp(0.6rem, 1.5vw, 1rem);
}

.detail-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: clamp(0.6rem, 1.5vw, 0.85rem);
  border-radius: var(--radius-md);
  background: var(--surface-bg);
  transition: background 0.2s, transform 0.2s;
}

.detail-item:hover {
  background: var(--surface-hover);
  transform: translateX(-2px);
}

.detail-label {
  font-family: var(--font-cairo);
  font-size: clamp(0.7rem, 1.5vw, 0.76rem);
  font-weight: 600;
  color: var(--text-placeholder);
  display: block;
  margin-bottom: 2px;
}

.detail-value {
  font-family: var(--font-cairo);
  font-size: clamp(0.82rem, 1.8vw, 0.92rem);
  font-weight: 600;
  color: var(--text-secondary);
}

.detail-value.link {
  color: var(--brand-blue);
  direction: ltr;
  text-align: right;
  display: block;
}

/* ── Data Section ───────────────────────────────────── */
.data-section {
  margin-top: clamp(1rem, 2vw, 1.5rem);
  background: var(--surface-card);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  border: 1px solid var(--border-color);
  overflow: hidden;
  transition: background 0.35s ease, border-color 0.35s ease;
}

.data-tabs {
  display: flex;
  border-bottom: 1px solid var(--border-color);
  transition: border-color 0.35s ease;
}

.data-tab {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: clamp(0.7rem, 2vw, 1rem);
  border: none;
  background: transparent;
  font-family: var(--font-cairo);
  font-size: clamp(0.85rem, 2vw, 0.95rem);
  font-weight: 700;
  color: var(--text-placeholder);
  cursor: pointer;
  transition: all 0.25s ease;
  border-bottom: 3px solid transparent;
  position: relative;
  top: 1px;
}

.data-tab:hover { color: var(--text-muted); background: var(--surface-hover); }
.data-tab.active { color: var(--brand-blue); border-bottom-color: var(--brand-blue); }

.tab-badge {
  background: var(--border-color);
  color: var(--text-muted);
  font-size: 0.7rem;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 20px;
  min-width: 24px;
  text-align: center;
  transition: background 0.35s ease, color 0.35s ease;
}

.data-tab.active .tab-badge {
  background: #eff6ff;
  color: var(--brand-blue);
}

/* ── Filter Bar ─────────────────────────────────────── */
.filter-bar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.8rem;
  padding: clamp(0.7rem, 2vw, 1rem) clamp(0.8rem, 2vw, 1.2rem);
  background: var(--surface-header);
  border-bottom: 1px solid var(--border-color);
  transition: background 0.35s ease, border-color 0.35s ease;
}

.filter-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-family: var(--font-cairo);
  font-size: clamp(0.75rem, 1.6vw, 0.84rem);
  font-weight: 600;
  color: var(--text-muted);
}

.filter-inputs {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  flex-wrap: wrap;
}

.date-input-wrap {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0.4rem 0.7rem;
  background: var(--surface-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-sm);
  transition: border-color 0.2s, background 0.35s ease;
}

.date-input-wrap:focus-within { border-color: var(--brand-blue); }

.date-input {
  border: none;
  outline: none;
  font-family: var(--font-cairo);
  font-size: clamp(0.75rem, 1.6vw, 0.82rem);
  color: var(--text-primary);
  background: transparent;
  width: clamp(100px, 20vw, 135px);
}

.date-separator {
  font-family: var(--font-cairo);
  font-size: 0.8rem;
  color: var(--text-placeholder);
}

.btn-filter, .btn-reset {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 0.45rem 0.9rem;
  border-radius: var(--radius-sm);
  border: none;
  font-family: var(--font-cairo);
  font-size: clamp(0.75rem, 1.6vw, 0.82rem);
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-filter {
  background: var(--brand-blue);
  color: #fff;
}

.btn-filter:hover { background: var(--brand-blue-hover); }
.btn-filter:disabled { opacity: 0.5; cursor: not-allowed; }

.btn-reset {
  background: var(--surface-card);
  color: var(--text-muted);
  border: 1px solid var(--border-color);
}

.btn-reset:hover { background: #fee2e2; color: #ef4444; border-color: #fecaca; }
.btn-reset:disabled { opacity: 0.4; cursor: not-allowed; }

/* ── Table Skeleton ─────────────────────────────────── */
.table-skeleton {
  padding: 1rem 1.2rem;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.sk-row { display: flex; gap: 10px; }
.sk-cell {
  flex: 1;
  height: 18px;
  border-radius: 6px;
  background: linear-gradient(90deg, var(--skeleton-base) 0%, var(--skeleton-shine) 50%, var(--skeleton-base) 100%);
  background-size: 200% 100%;
  animation: shimmer 1.5s ease-in-out infinite;
}

/* ── Data Table ─────────────────────────────────────── */
.table-wrap { overflow-x: auto; -webkit-overflow-scrolling: touch; }

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-family: var(--font-cairo);
}

.data-table thead {
  background: var(--surface-header);
  position: sticky;
  top: 0;
  z-index: 1;
  transition: background 0.35s ease;
}

.data-table th {
  font-family: var(--font-cairo);
  font-size: clamp(0.72rem, 1.5vw, 0.78rem);
  font-weight: 700;
  color: var(--text-muted);
  text-align: center;
  padding: 0.9rem 0.7rem;
  white-space: nowrap;
  border-bottom: 2px solid var(--border-color);
  transition: border-color 0.35s ease;
}

.data-table th:first-child,
.data-table th:last-child { text-align: center; }

.data-table td {
  font-family: var(--font-cairo);
  font-size: clamp(0.78rem, 1.6vw, 0.86rem);
  color: var(--text-secondary);
  padding: 0.75rem 0.7rem;
  text-align: center;
  border-bottom: 1px solid var(--border-light);
  white-space: nowrap;
  transition: border-color 0.35s ease, color 0.35s ease;
}

.data-table tbody tr { transition: background 0.15s; }
.data-table tbody tr:hover { background: var(--surface-hover); }

.td-num { color: var(--text-placeholder); font-size: 0.8rem; }
.td-amount { font-weight: 700; text-align: center; }
.sales-amount { color: var(--brand-blue); }
.paid-amount { color: var(--brand-green); }
.td-items { max-width: 200px; overflow: hidden; text-overflow: ellipsis; }

.badge-remaining {
  display: inline-block;
  padding: 3px 10px;
  border-radius: 20px;
  font-size: 0.78rem;
  font-weight: 700;
  background: #fef3c7;
  color: #92400e;
}

.badge-remaining.zero { background: #d1fae5; color: #065f46; }

.table-empty {
  text-align: center;
  padding: clamp(2rem, 5vw, 3.5rem) 1rem;
  color: var(--text-placeholder);
  font-family: var(--font-cairo);
}

.table-empty p { margin-top: 0.5rem; font-size: clamp(0.82rem, 1.8vw, 0.92rem); }

/* ── Desktop/Mobile visibility ──────────────────────── */
.desktop-only { display: block; }
.mobile-only { display: none; }

/* ── Mobile Cards ───────────────────────────────────── */
.sale-card {
  background: var(--surface-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  margin: 0.7rem 1rem;
  overflow: hidden;
  box-shadow: var(--shadow-sm);
  transition: background 0.35s ease, border-color 0.35s ease;
}

.sale-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.7rem 1rem;
  background: var(--surface-header);
  border-bottom: 1px solid var(--border-light);
  transition: background 0.35s ease, border-color 0.35s ease;
}

.sale-card-num {
  font-family: var(--font-cairo);
  font-size: 0.8rem;
  font-weight: 700;
  color: var(--text-muted);
}

.sale-card-date {
  font-family: var(--font-cairo);
  font-size: 0.78rem;
  color: var(--text-placeholder);
}

.sale-card-body {
  padding: 0.7rem 1rem;
  display: flex;
  flex-wrap: wrap;
  gap: 0.8rem;
}

.sale-card-row {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 100px;
  flex: 1;
}

.sale-card-label {
  font-family: var(--font-cairo);
  font-size: 0.7rem;
  font-weight: 600;
  color: var(--text-placeholder);
}

.sale-card-value {
  font-family: var(--font-cairo);
  font-size: 0.88rem;
  font-weight: 700;
  color: var(--text-secondary);
}

.sale-card-footer {
  padding: 0.6rem 1rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-top: 1px solid var(--border-light);
  font-family: var(--font-cairo);
  font-size: 0.75rem;
  color: var(--text-placeholder);
  transition: border-color 0.35s ease;
}

.sale-card-items { max-width: 60%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* ── Pagination ─────────────────────────────────────── */
.pagination-bar {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 4px;
  padding: clamp(0.8rem, 2vw, 1rem);
  border-top: 1px solid var(--border-color);
  flex-wrap: wrap;
  transition: border-color 0.35s ease;
}

.page-btn {
  width: clamp(32px, 6vw, 38px);
  height: clamp(32px, 6vw, 38px);
  border-radius: var(--radius-sm);
  border: 1px solid var(--border-color);
  background: var(--surface-card);
  font-family: var(--font-cairo);
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-muted);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.page-btn:hover { background: var(--surface-hover); border-color: var(--text-placeholder); }
.page-btn.active { background: var(--brand-blue); color: #fff; border-color: var(--brand-blue); }
.page-btn:disabled { opacity: 0.3; cursor: not-allowed; }

/* ── Footer ─────────────────────────────────────────── */
.portal-footer {
  text-align: center;
  padding: clamp(1.5rem, 3vw, 2.5rem) 0 1rem;
}

.footer-brand {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  font-family: var(--font-cairo);
  font-size: clamp(0.88rem, 2vw, 0.95rem);
  font-weight: 700;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.footer-copy {
  font-family: var(--font-cairo);
  font-size: clamp(0.7rem, 1.5vw, 0.76rem);
  color: var(--text-placeholder);
  margin: 0;
}

/* ════════════════════════════════════════════════════════
   RESPONSIVE DESIGN
   ════════════════════════════════════════════════════════ */

/* Tablet & below */
@media (max-width: 960px) {
  .stats-grid { grid-template-columns: repeat(2, 1fr); gap: 0.7rem; }
  .hero-content { flex-direction: column; text-align: center; padding: 1.2rem 1rem 2.5rem; }
  .hero-identity { flex-direction: column; align-items: center; gap: 0.8rem; }
  .identity-meta { justify-content: center; }
  .hero-right { justify-content: center; }
  .hero-chips { justify-content: center; flex-wrap: wrap; }
  .hero-wave svg { height: clamp(24px, 4vw, 36px); }
  .customer-name { font-size: 1.3rem; }
  .details-grid { grid-template-columns: 1fr; }
  .filter-bar { flex-direction: column; align-items: flex-start; }
  .filter-inputs { width: 100%; }
  .date-input { width: clamp(90px, 18vw, 110px); }
  .portal-title { font-size: clamp(0.9rem, 2.5vw, 1.1rem); }
}

/* Mobile */
@media (max-width: 640px) {
  .stats-grid { grid-template-columns: 1fr; gap: 0.6rem; }
  .desktop-only { display: none !important; }
  .mobile-only { display: block; }
  .data-tab { font-size: 0.82rem; padding: 0.7rem 0.5rem; }
  .filter-bar { gap: 0.5rem; padding: 0.7rem 0.8rem; }
  .filter-inputs { flex-direction: column; align-items: stretch; }
  .date-input-wrap { justify-content: space-between; }
  .date-input { width: 100px; font-size: 0.75rem; }
  .btn-filter, .btn-reset {
    width: 100%; justify-content: center; padding: clamp(0.5rem, 1.5vw, 0.7rem);
  }
  .pagination-bar { gap: 3px; }
  .page-btn { width: 34px; height: 34px; font-size: 0.8rem; }
  .hero-chips { gap: 6px; }
  .hero-store-btn { font-size: 0.72rem; padding: 5px 10px; }
  .sales-mini-section { padding: 1rem; }
  .accordion-body { padding: 0 0.8rem 0.8rem; }
  .detail-item { padding: 0.6rem; }
  .theme-toggle-btn {
    width: clamp(32px, 8vw, 36px); height: clamp(32px, 8vw, 36px);
  }
  .theme-toggle-btn svg { width: clamp(16px, 4vw, 18px); height: clamp(16px, 4vw, 18px); }
  .sale-card-body { gap: 0.5rem; }
  .sale-card-row { min-width: 70px; }
  .error-card { padding: 2rem 1.2rem; }
  .error-icon-ring { width: 90px; height: 90px; }
  .error-title { font-size: 1.3rem; }
}

/* Small mobile */
@media (max-width: 380px) {
  .stats-grid { gap: 0.4rem; }
  .stat-card { padding: 0.85rem 0.6rem; gap: 0.5rem; }
  .stat-value { font-size: clamp(0.9rem, 5vw, 1rem); }
  .stat-label { font-size: 0.68rem; }
  .hero-content { padding: 1.2rem 0.8rem; gap: 0.8rem; }
  .customer-name { font-size: 1rem; }
  .data-tab { font-size: 0.72rem; gap: 4px; }
  .tab-badge { font-size: 0.65rem; padding: 1px 6px; }
  .date-input { width: 80px; font-size: 0.72rem; }
  .sale-card-row { min-width: 60px; }
  .sale-card-value { font-size: 0.78rem; }
}

/* Dark mode overrides for mixed-mode elements */
html.portal-dark .sales-mini-section .tab-badge { background: rgba(59,130,246,0.15); }
html.portal-dark .data-tab.active .tab-badge { background: rgba(59,130,246,0.15); }

/* Print styles */
@media print {
  .hero-wave, .filter-bar, .pagination-bar, .btn-filter, .btn-reset,
  .data-tabs, .sale-card-footer, .portal-footer { display: none !important; }
  .stats-card, .details-section, .data-section { box-shadow: none; break-inside: avoid; }
  .hero-store-btn { display: none !important; }
  .theme-toggle-btn { display: none !important; }
  body { background: #fff; }
  .data-table { font-size: 11px; }
}
</style>
