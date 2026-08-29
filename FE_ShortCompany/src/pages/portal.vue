<template>
  <div class="portal-page">
    <!-- تحميل -->
    <div v-if="loading" class="d-flex justify-center align-center h-screen">
      <div class="text-center">
        <VProgressCircular
          indeterminate
          color="primary"
          size="64"
          width="4"
        />
        <p class="mt-4 text-medium-emphasis">جاري تحميل البيانات...</p>
      </div>
    </div>

    <!-- خطأ -->
    <div v-else-if="errorMessage" class="d-flex justify-center align-center h-screen pa-4">
      <VCard max-width="500" class="text-center pa-6" elevation="8">
        <VCardItem>
          <VIcon
            icon="tabler-alert-triangle"
            color="error"
            size="64"
            class="mb-4"
          />
          <VCardTitle class="text-error mb-2">عذراً</VCardTitle>
          <VCardText>{{ errorMessage }}</VCardText>
        </VCardItem>
      </VCard>
    </div>

    <!-- المحتوى الرئيسي -->
    <template v-else>
      <!-- الهيدر -->
      <VAppBar color="primary" elevation="4" class="px-4">
        <VToolbarTitle class="font-weight-bold text-h5">
          <VIcon icon="tabler-building-store" class="me-2" />
          بوابة العميل
        </VToolbarTitle>
        <VSpacer />
        <VChip color="white" variant="tonal" class="font-weight-medium">
          <VIcon icon="tabler-user-circle" size="18" class="me-1" />
          {{ customer.customerName }}
        </VChip>
      </VAppBar>

      <VContainer fluid class="pa-6" style="max-width: 1200px;">
        <!-- بطاقة معلومات العميل -->
        <VRow>
          <VCol cols="12">
            <VCard elevation="6" class="customer-card rounded-lg overflow-hidden">
              <div class="card-header-gradient pa-6">
                <VRow align="center">
                  <VCol cols="12" md="8">
                    <div class="d-flex align-center">
                      <VAvatar color="white" size="56" class="me-4">
                        <VIcon icon="tabler-user" color="primary" size="32" />
                      </VAvatar>
                      <div>
                        <h2 class="text-h4 font-weight-bold text-white mb-1">
                          {{ customer.customerName }}
                        </h2>
                        <VChip
                          :color="customer.isLegal ? 'info' : 'success'"
                          size="small"
                          variant="flat"
                          class="me-2"
                        >
                          {{ customer.isLegal ? 'حساب قانوني' : 'حساب عادي' }}
                        </VChip>
                        <span class="text-white opacity-80 text-body-2">
                          {{ customer.cityName }}
                        </span>
                      </div>
                    </div>
                  </VCol>
                  <VCol cols="12" md="4">
                    <div class="d-flex justify-end flex-wrap ga-2">
                      <VChip color="white" variant="tonal" size="small">
                        <VIcon icon="tabler-user" size="16" class="me-1" />
                        المندوب: {{ customer.delegateName || 'غير محدد' }}
                      </VChip>
                    </div>
                  </VCol>
                </VRow>
              </div>

              <VDivider />

              <VCardText class="pa-6">
                <VRow>
                  <!-- معلومات التواصل -->
                  <VCol cols="12" md="6">
                    <h3 class="text-h6 font-weight-bold mb-4 d-flex align-center">
                      <VIcon icon="tabler-address-book" color="primary" class="me-2" />
                      معلومات التواصل
                    </h3>
                    <VList density="compact" class="bg-transparent">
                      <VListItem v-if="customer.phoneNumber">
                        <template #prepend>
                          <VIcon icon="tabler-phone" color="success" size="20" />
                        </template>
                        <VListItemTitle class="text-body-2 text-medium-emphasis">رقم الهاتف</VListItemTitle>
                        <VListItemSubtitle class="text-body-1 font-weight-medium">
                          <a :href="`tel:${customer.phoneNumber}`" class="text-decoration-none text-primary">
                            {{ customer.phoneNumber }}
                          </a>
                        </VListItemSubtitle>
                      </VListItem>

                      <VListItem v-if="customer.address">
                        <template #prepend>
                          <VIcon icon="tabler-map-pin" color="error" size="20" />
                        </template>
                        <VListItemTitle class="text-body-2 text-medium-emphasis">العنوان</VListItemTitle>
                        <VListItemSubtitle class="text-body-1">{{ customer.address }}</VListItemSubtitle>
                      </VListItem>

                      <VListItem v-if="customer.shopName">
                        <template #prepend>
                          <VIcon icon="tabler-building-store" color="primary" size="20" />
                        </template>
                        <VListItemTitle class="text-body-2 text-medium-emphasis">اسم المحل</VListItemTitle>
                        <VListItemSubtitle class="text-body-1">{{ customer.shopName }}</VListItemSubtitle>
                      </VListItem>

                      <VListItem v-if="customer.storeAddress">
                        <template #prepend>
                          <VIcon icon="tabler-building-warehouse" color="warning" size="20" />
                        </template>
                        <VListItemTitle class="text-body-2 text-medium-emphasis">عنوان المخزن</VListItemTitle>
                        <VListItemSubtitle class="text-body-1">{{ customer.storeAddress }}</VListItemSubtitle>
                      </VListItem>

                      <VListItem v-if="customer.nearestFunctionPoint">
                        <template #prepend>
                          <VIcon icon="tabler-location" color="info" size="20" />
                        </template>
                        <VListItemTitle class="text-body-2 text-medium-emphasis">أقرب نقطة دالة</VListItemTitle>
                        <VListItemSubtitle class="text-body-1">{{ customer.nearestFunctionPoint }}</VListItemSubtitle>
                      </VListItem>
                    </VList>
                  </VCol>

                  <!-- الملخص المالي -->
                  <VCol cols="12" md="6">
                    <h3 class="text-h6 font-weight-bold mb-4 d-flex align-center">
                      <VIcon icon="tabler-wallet" color="primary" class="me-2" />
                      الملخص المالي
                    </h3>
                    <VRow>
                      <VCol cols="6" sm="6">
                        <VCard variant="outlined" class="stat-mini-card pa-3 text-center h-100" color="success">
                          <p class="text-caption text-medium-emphasis mb-1">إجمالي المشتريات</p>
                          <p class="text-h5 font-weight-bold text-success mb-0">
                            {{ formatCurrency(customer.amountTotalSales) }}
                          </p>
                        </VCard>
                      </VCol>
                      <VCol cols="6" sm="6">
                        <VCard variant="outlined" class="stat-mini-card pa-3 text-center h-100" color="primary">
                          <p class="text-caption text-medium-emphasis mb-1">إجمالي المسدد</p>
                          <p class="text-h5 font-weight-bold text-primary mb-0">
                            {{ formatCurrency(customer.receiptsTotal) }}
                          </p>
                        </VCard>
                      </VCol>
                      <VCol cols="6" sm="6">
                        <VCard variant="outlined" class="stat-mini-card pa-3 text-center h-100" color="error">
                          <p class="text-caption text-medium-emphasis mb-1">المبلغ المتبقي</p>
                          <p class="text-h5 font-weight-bold text-error mb-0">
                            {{ formatCurrency(customer.amountRemaining) }}
                          </p>
                        </VCard>
                      </VCol>
                      <VCol cols="6" sm="6">
                        <VCard variant="outlined" class="stat-mini-card pa-3 text-center h-100" color="warning">
                          <p class="text-caption text-medium-emphasis mb-1">القسط اليومي</p>
                          <p class="text-h5 font-weight-bold text-warning mb-0">
                            {{ formatCurrency(customer.amountDaySales) }}
                          </p>
                        </VCard>
                      </VCol>
                    </VRow>

                    <div class="mt-4" v-if="customer.itemsNames">
                      <p class="text-caption text-medium-emphasis mb-1">المواد المشتراة</p>
                      <p class="text-body-2">{{ customer.itemsNames }}</p>
                    </div>
                  </VCol>
                </VRow>
              </VCardText>
            </VCard>
          </VCol>
        </VRow>

        <!-- التبويبات: المبيعات والتسديدات -->
        <VRow class="mt-6">
          <VCol cols="12">
            <VCard elevation="4" class="rounded-lg">
              <VTabs v-model="activeTab" color="primary" class="px-4" grow>
                <VTab value="sales">
                  <VIcon icon="tabler-shopping-bag" size="20" class="me-2" />
                  <span class="text-body-1 font-weight-bold">المبيعات</span>
                  <VChip size="x-small" color="primary" variant="tonal" class="ms-2">
                    {{ sales.length }}
                  </VChip>
                </VTab>
                <VTab value="payments">
                  <VIcon icon="tabler-credit-card" size="20" class="me-2" />
                  <span class="text-body-1 font-weight-bold">التسديدات</span>
                  <VChip size="x-small" color="success" variant="tonal" class="ms-2">
                    {{ payments.length }}
                  </VChip>
                </VTab>
              </VTabs>

              <VDivider />

              <!-- فلتر التاريخ -->
              <VCardText class="pa-4 d-flex flex-wrap align-center ga-3 bg-surface-light">
                <span class="text-body-2 text-medium-emphasis font-weight-medium">
                  <VIcon icon="tabler-calendar" size="18" class="me-1" />
                  تصفية حسب التاريخ:
                </span>
                <VTextField
                  v-model="dateFilter.from"
                  type="date"
                  label="من تاريخ"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="max-width: 180px;"
                  :max="dateFilter.to || todayStr"
                />
                <VTextField
                  v-model="dateFilter.to"
                  type="date"
                  label="الى تاريخ"
                  density="compact"
                  variant="outlined"
                  hide-details
                  style="max-width: 180px;"
                  :min="dateFilter.from"
                  :max="todayStr"
                />
                <VBtn
                  color="primary"
                  variant="tonal"
                  size="small"
                  @click="applyDateFilter"
                  :loading="tableLoading"
                >
                  <VIcon icon="tabler-filter" size="18" class="me-1" />
                  تطبيق
                </VBtn>
                <VBtn
                  variant="text"
                  size="small"
                  @click="resetDateFilter"
                  :disabled="!dateFilter.from && !dateFilter.to"
                >
                  <VIcon icon="tabler-x" size="18" class="me-1" />
                  مسح الفلتر
                </VBtn>
              </VCardText>

              <VDivider />

              <!-- جدول المبيعات -->
              <VCardText v-if="activeTab === 'sales'" class="pa-0">
                <VDataTable
                  :headers="salesHeaders"
                  :items="sales"
                  :loading="tableLoading"
                  :items-per-page="10"
                  :page="salesPage"
                  @update:page="salesPage = $event"
                  items-per-page-text="عدد العناصر في الصفحة"
                  hover
                  fixed-header
                  class="elevation-0"
                >
                  <template #item.dateCreate="{ item }">
                    <span class="text-body-2">{{ formatDate(item.dateCreate) }}</span>
                  </template>
                  <template #item.amountTotalSalesDenar="{ item }">
                    <span class="font-weight-bold text-primary">
                      {{ formatCurrency(item.amountTotalSalesDenar || item.amountTotalSales) }}
                    </span>
                  </template>
                  <template #item.amountRemaining="{ item }">
                    <VChip
                      :color="(item.amountRemaining || 0) > 0 ? 'warning' : 'success'"
                      size="small"
                      variant="tonal"
                    >
                      {{ formatCurrency(item.amountRemaining) }}
                    </VChip>
                  </template>
                  <template #item.receiptsTotal="{ item }">
                    <span class="text-success font-weight-medium">
                      {{ formatCurrency(item.receiptsTotal) }}
                    </span>
                  </template>
                  <template #bottom />
                </VDataTable>
                <div class="d-flex justify-center pa-2" v-if="sales.length > 10">
                  <VPagination
                    v-model="salesPage"
                    :length="Math.ceil(sales.length / 10)"
                    color="primary"
                    variant="tonal"
                    rounded
                  />
                </div>
              </VCardText>

              <!-- جدول التسديدات -->
              <VCardText v-if="activeTab === 'payments'" class="pa-0">
                <VDataTable
                  :headers="paymentsHeaders"
                  :items="payments"
                  :loading="tableLoading"
                  :items-per-page="10"
                  :page="paymentsPage"
                  @update:page="paymentsPage = $event"
                  items-per-page-text="عدد العناصر في الصفحة"
                  hover
                  fixed-header
                  class="elevation-0"
                >
                  <template #item.paymentDate="{ item }">
                    <span class="text-body-2">{{ formatDate(item.paymentDate) }}</span>
                  </template>
                  <template #item.amountDenar="{ item }">
                    <span class="font-weight-bold text-success">
                      {{ formatCurrency(item.amountDenar) }}
                    </span>
                  </template>
                  <template #item.boxName="{ item }">
                    <VChip size="x-small" color="primary" variant="outlined">
                      {{ item.boxName || '-' }}
                    </VChip>
                  </template>
                  <template #bottom />
                </VDataTable>
                <div class="d-flex justify-center pa-2" v-if="payments.length > 10">
                  <VPagination
                    v-model="paymentsPage"
                    :length="Math.ceil(payments.length / 10)"
                    color="primary"
                    variant="tonal"
                    rounded
                  />
                </div>
              </VCardText>
            </VCard>
          </VCol>
        </VRow>

        <!-- تذييل -->
        <div class="text-center mt-8 mb-4">
          <VDivider class="mb-4" />
          <p class="text-caption text-medium-emphasis">
            © {{ new Date().getFullYear() }} جميع الحقوق محفوظة | شركة {{ customer.cityID === 6 ? 'المنهاج الذهبي' : 'قلعة الضمان' }}
          </p>
        </div>
      </VContainer>
    </template>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'

definePage({ meta: { layout: 'blank' } })

const route = useRoute()
const apiBase = import.meta.env.VITE_API_BASE_URL || '/api'

// State
const loading = ref(true)
const errorMessage = ref('')
const customer = ref({})
const sales = ref([])
const payments = ref([])
const tableLoading = ref(false)
const activeTab = ref('sales')
const salesPage = ref(1)
const paymentsPage = ref(1)

const todayStr = computed(() => new Date().toISOString().slice(0, 10))

const dateFilter = ref({
  from: '',
  to: '',
})

// Table headers
const salesHeaders = [
  { title: '#', key: 'customerSaleID', align: 'start', sortable: false, width: 60 },
  { title: 'تاريخ البيع', key: 'dateCreate', align: 'start' },
  { title: 'المبلغ (دينار)', key: 'amountTotalSalesDenar', align: 'center' },
  { title: 'المسدد', key: 'receiptsTotal', align: 'center' },
  { title: 'المتبقي', key: 'amountRemaining', align: 'center' },
  { title: 'عدد الاقساط', key: 'countReceiptDevice', align: 'center', width: 100 },
  { title: 'المواد', key: 'itemsNames', align: 'start' },
  { title: 'اسم البائع', key: 'saleName', align: 'start', width: 120 },
]

const paymentsHeaders = [
  { title: '#', key: 'customerPaymentID', align: 'start', sortable: false, width: 60 },
  { title: 'تاريخ التسديد', key: 'paymentDate', align: 'start' },
  { title: 'المبلغ (دينار)', key: 'amountDenar', align: 'center' },
  { title: 'رقم البوند', key: 'boundNumber', align: 'center', width: 100 },
  { title: 'الصندوق', key: 'boxName', align: 'center', width: 120 },
  { title: 'المندوب', key: 'delegateName', align: 'start', width: 120 },
  { title: 'اسم الموظف', key: 'userName', align: 'start', width: 120 },
]

// Get the token from URL query
const token = computed(() => route.query.token || '')

// Format currency
function formatCurrency(value) {
  if (value == null) return '0'
  const num = Number(value)
  return num.toLocaleString('en-US', { maximumFractionDigits: 0 }) + ' د.ع'
}

// Format date
function formatDate(dateStr) {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleDateString('ar-IQ', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

// API call helper
async function apiCall(endpoint, params = {}) {
  const url = new URL(`${apiBase}Portal/${endpoint}`, window.location.origin)
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

// Load customer info
async function loadCustomerInfo() {
  try {
    const data = await apiCall('CustomerInfo')
    customer.value = data
  } catch (err) {
    throw new Error(err.message || 'فشل تحميل معلومات العميل')
  }
}

// Load sales
async function loadSales(fromDate, toDate) {
  const params = {}
  if (fromDate) params.fromDate = fromDate
  if (toDate) params.toDate = toDate
  const data = await apiCall('Sales', params)
  sales.value = data || []
}

// Load payments
async function loadPayments(fromDate, toDate) {
  const params = {}
  if (fromDate) params.fromDate = fromDate
  if (toDate) params.toDate = toDate
  const data = await apiCall('Payments', params)
  payments.value = data || []
}

// Apply date filter
async function applyDateFilter() {
  tableLoading.value = true
  try {
    const [salesResult, paymentsResult] = await Promise.allSettled([
      loadSales(dateFilter.value.from || undefined, dateFilter.value.to || undefined),
      loadPayments(dateFilter.value.from || undefined, dateFilter.value.to || undefined),
    ])
  } catch (err) {
    console.error('Filter error:', err)
  } finally {
    tableLoading.value = false
  }
}

// Reset date filter
async function resetDateFilter() {
  dateFilter.value.from = ''
  dateFilter.value.to = ''
  await applyDateFilter()
}

// Init
onMounted(async () => {
  if (!token.value) {
    loading.value = false
    errorMessage.value = 'الرابط غير صالح - المعرف مفقود'
    return
  }

  try {
    await loadCustomerInfo()

    // Load sales and payments in parallel
    const [salesResult, paymentsResult] = await Promise.allSettled([
      loadSales(),
      loadPayments(),
    ])

    loading.value = false
  } catch (err) {
    loading.value = false
    errorMessage.value = err.message || 'حدث خطأ أثناء تحميل البيانات'
  }
})
</script>

<style scoped>
.portal-page {
  min-height: 100vh;
  background: #f5f5f5;
  direction: rtl;
  font-family: 'Tajawal', 'Segoe UI', sans-serif;
}

.card-header-gradient {
  background: linear-gradient(135deg, #1976d2 0%, #1565c0 50%, #0d47a1 100%);
}

.customer-card {
  border-radius: 16px !important;
}

.stat-mini-card {
  border-radius: 12px !important;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.stat-mini-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

:deep(.v-toolbar-title) {
  font-family: 'Tajawal', sans-serif;
}

:deep(.v-data-table) {
  font-family: 'Tajawal', sans-serif;
}

:deep(.v-card) {
  border-radius: 16px;
}

.text-white {
  color: #ffffff !important;
}

.opacity-80 {
  opacity: 0.8;
}

@media (max-width: 600px) {
  .portal-page {
    font-size: 14px;
  }

  :deep(.v-data-table) {
    font-size: 12px;
  }
}
</style>
