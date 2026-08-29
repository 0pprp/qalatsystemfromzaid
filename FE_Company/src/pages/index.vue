<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useTheme } from 'vuetify'
import { VBtn, VCard, VCol, VIcon, VProgressCircular, VRow } from 'vuetify/components'
import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

const theme = useTheme()
const apiUrl = localStorage.getItem('LinkCity')
const statsData = ref([])

// إعداد تواريخ الفلاتر
const today = new Date()
const prior = new Date()

today.setDate(prior.getDate() + 1)
prior.setDate(prior.getDate() - 30)

const filters = ref({
  fromDate: prior.toISOString().slice(0, 10),
  toDate: today.toISOString().slice(0, 10),
})

// بيانات الإحصائيات الرئيسية
const stats = ref({
  numberOfStores: 0,
  numberOfItems: 0,
  numberOfSuppliers: 0,
  numberOfPurchases: 0,
  numberOfDelegates: 0,
  numberOfCustomers: 0,
  numberOfSales: 0,
  numberOfPayments: 0,
  numberOfCashBoxes: 0,
  numberOfAdditionsToBox: 0,
  numberOfWithdrawalsFromBox: 0,
  numberOfTransfersBetweenBoxes: 0,
})

const loading = ref(false)
const refreshLoading = ref(false)

// تحديد لون النص بناءً على الوضع
const textColor = computed(() => {
  return theme.global.name.value === 'dark' ? 'white' : 'black'
})

// تصنيف الإحصائيات مع أيقونات Tabler
const statsCategories = computed(() => ({
  sales: {
    title: 'المبيعات والعملاء',
    color: 'success',
    items: [
      { key: 'numberOfSales', title: 'عدد المبيعات', icon: 'tabler-shopping-bag' },
      { key: 'numberOfCustomers', title: 'عدد العملاء', icon: 'tabler-users' },
      { key: 'numberOfPayments', title: 'عدد التسديدات', icon: 'tabler-credit-card' },
    ],
  },
  inventory: {
    title: 'المخزون والموردين',
    color: 'primary',
    items: [
      { key: 'numberOfItems', title: 'عدد العناصر', icon: 'tabler-package' },
      { key: 'numberOfStores', title: 'عدد المخازن', icon: 'tabler-building-store' },
      { key: 'numberOfSuppliers', title: 'عدد الموردين', icon: 'tabler-truck' },
      { key: 'numberOfPurchases', title: 'عدد المشتريات', icon: 'tabler-shopping-cart' },
    ],
  },
  finance: {
    title: 'الصناديق والمالية',
    color: 'warning',
    items: [
      { key: 'numberOfCashBoxes', title: 'الخزائن النقدية', icon: 'tabler-cash' },
      { key: 'numberOfAdditionsToBox', title: 'الإضافات للصندوق', icon: 'tabler-plus' },
      { key: 'numberOfWithdrawalsFromBox', title: 'السحوبات من الصندوق', icon: 'tabler-arrow-up' },
      { key: 'numberOfTransfersBetweenBoxes', title: 'النقل بين الصناديق', icon: 'tabler-arrows-exchange' },
    ],
  },
  system: {
    title: 'أخرى',
    color: 'info',
    items: [
      { key: 'numberOfDelegates', title: 'عدد المندوبين', icon: 'tabler-user-star' },
    ],
  },
}))

// ترويسات الجدول
const headers = [
  { title: 'المندوب', key: 'delegateName', align: 'center', class: 'font-weight-bold' },
  { title: 'عدد العملاء', key: 'numberOfCustomer', align: 'center' },
  { title: 'سعر البيع', key: 'amountPrice', align: 'center' },
  { title: 'سعر الشراء', key: 'amountCost', align: 'center' },
  { title: 'القسط', key: 'amountDay', align: 'center' },
  { title: 'عدد المباع', key: 'numberOfItemSale', align: 'center' },
  { title: 'الواصل', key: 'amountReceipt', align: 'center' },
  { title: 'عدد المصفرين', key: 'numberOfCustomerZero', align: 'center' },
  { title: 'سعر بيع المصفرين', key: 'amountPriceZero', align: 'center' },
  { title: 'قسط المصفرين', key: 'amountDayZero', align: 'center' },
]

// تنسيق الأرقام
const formatNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString()
  
  return "0"
}

const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  
  return "لا يوجد"
}

// جلب الإحصائيات الرئيسية
async function fetchStats() {
  try {
    const authHeader = getAuthHeaders()
    if(!apiUrl) return

    const response = await axios.get(`${apiUrl}Statistics/StatisticsApp_GetAll`, {
      headers: authHeader,
    })

    if (response.data) {
      stats.value = response.data
    }
  } catch (error) {
    console.error(error)
  }
}

// جلب إحصائيات المندوبين
async function fetchStatsDelegates() {
  try {
    loading.value = true
    
    if(!apiUrl) {
      loading.value = false
      
      return
    }

    const authHeader = getAuthHeaders()
    const fromDate = filters.value.fromDate || 'null'
    const toDate = filters.value.toDate || 'null'

    const response = await axios.get(`${apiUrl}Delegates/Delegates_Statistics/${fromDate}&&${toDate}`, { 
      headers: authHeader, 
    })

    statsData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// تحديث البيانات
const refreshData = () => {
  refreshLoading.value = true
  fetchStats().then(() => {
    refreshLoading.value = false
  })
  fetchStatsDelegates()
}

// تصدير إلى Excel
function exportToExcel() {
  const dataToExport = statsData.value.map(item => {
    const row = {}

    headers.forEach(header => {
      row[header.title] = item[header.key] ?? 0
    })
    
    return row
  })

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Statistics")
  XLSX.writeFile(workbook, "Statistics.xlsx")
}

// معالجة تغيير التواريخ
function handleDateChangeFromDate(event) {
  const rawDate = event.target.value

  filters.value.fromDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function handleDateChangeToDate(event) {
  const rawDate = event.target.value

  filters.value.toDate = new Date(rawDate).toLocaleDateString('en-CA')
}

// إحصائيات الإجماليات
const totals = computed(() => [
  {
    icon: 'tabler-users', 
    value: statsData.value.reduce((sum, s) => sum + (s.numberOfCustomer || 0), 0),
    title: 'عدد العملاء الكلي',
    color: 'primary',
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-cash', 
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountPrice || 0), 0)),
    title: 'سعر البيع الكلي',
    color: 'info',
    gradient: "linear-gradient(135deg, #0284c7 0%, #3b82f6 100%)",
  },
  {
    icon: 'tabler-cash', 
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountCost || 0), 0)),
    title: 'سعر الشراء الكلي',
    color: 'secondary',
    gradient: "linear-gradient(135deg, #475569 0%, #64748b 100%)",
  },
  {
    icon: 'tabler-calendar', 
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountDay || 0), 0)),
    title: 'القسط الكلي',
    color: 'success',
    gradient: "linear-gradient(135deg, #059669 0%, #10b981 100%)",
  },
  {
    icon: 'tabler-check', 
    value: statsData.value.reduce((sum, s) => sum + (s.numberOfItemSale || 0), 0),
    title: 'عدد المباع الكلي',
    color: 'warning',
    gradient: "linear-gradient(135deg, #f59e0b 0%, #d97706 100%)",
  },
  {
    icon: 'tabler-credit-card', 
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountReceipt || 0), 0)),
    title: 'الواصل الكلي',
    color: 'success',
    gradient: "linear-gradient(135deg, #10b981 0%, #34d399 100%)",
  },
  {
    icon: 'tabler-users-minus', 
    value: statsData.value.reduce((sum, s) => sum + (s.numberOfCustomerZero || 0), 0),
    title: 'إجمالي عدد المصفرين',
    color: 'primary',
    gradient: "linear-gradient(135deg, #6366f1 0%, #4f46e5 100%)",
  },
  {
    icon: 'tabler-currency-dollar-off', 
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountPriceZero || 0), 0)),
    title: 'إجمالي سعر بيع المصفرين',
    color: 'info',
    gradient: "linear-gradient(135deg, #0ea5e9 0%, #0284c7 100%)",
  },
  {
    icon: 'tabler-calendar-minus', 
    value: formattedNumber(statsData.value.reduce((sum, s) => sum + (s.amountDayZero || 0), 0)),
    title: 'إجمالي قسط المصفرين',
    color: 'error',
    gradient: "linear-gradient(135deg, #e11d48 0%, #fb7185 100%)",
  },
])

// جلب البيانات عند التحميل
onMounted(() => {
  fetchStats()
  fetchStatsDelegates()
})
</script>

<template>
  <div class="dashboard-container">
    <!-- Header -->
    <VCard
      class="dashboard-header"
      elevation="3"
    >
      <div class="header-content">
        <div class="header-text">
          <h1 class="header-title">
            <VIcon
              icon="tabler-chart-dots"
              size="32"
              class="me-3"
            />
            لوحة التحكم الرئيسية
          </h1>
          <p class="header-subtitle">
            نظرة شاملة على إحصائيات النظام والمؤشرات الرئيسية
          </p>
        </div>
        <div class="header-actions">
          <VBtn
            color="primary"
            prepend-icon="tabler-refresh"
            :loading="refreshLoading"
            class="action-btn"
            @click="refreshData"
          >
            تحديث
          </VBtn>
        </div>
      </div>
    </VCard>

    <!-- Main Dashboard Content -->
    <div class="dashboard-content">
      <!-- Summary Cards -->
      <VRow class="mb-6">
        <VCol
          cols="12"
          md="4"
        >
          <VCard
            class="summary-card"
            color="primary"
            elevation="2"
          >
            <div class="summary-content">
              <div class="summary-icon">
                <VIcon
                  icon="tabler-users"
                  size="48"
                  color="white"
                />
              </div>
              <div class="summary-text">
                <h3 class="summary-value text-white">
                  {{ formatNumber(stats.numberOfCustomers) }}
                </h3>
                <p class="summary-title text-white">
                  إجمالي العملاء
                </p>
              </div>
            </div>
          </VCard>
        </VCol>
        
        <VCol
          cols="12"
          md="4"
        >
          <VCard
            class="summary-card"
            color="success"
            elevation="2"
          >
            <div class="summary-content">
              <div class="summary-icon">
                <VIcon
                  icon="tabler-shopping-bag"
                  size="48"
                  color="white"
                />
              </div>
              <div class="summary-text">
                <h3 class="summary-value text-white">
                  {{ formatNumber(stats.numberOfSales) }}
                </h3>
                <p class="summary-title text-white">
                  إجمالي المبيعات
                </p>
              </div>
            </div>
          </VCard>
        </VCol>
        
        <VCol
          cols="12"
          md="4"
        >
          <VCard
            class="summary-card"
            color="info"
            elevation="2"
          >
            <div class="summary-content">
              <div class="summary-icon">
                <VIcon
                  icon="tabler-shopping-cart"
                  size="48"
                  color="white"
                />
              </div>
              <div class="summary-text">
                <h3 class="summary-value text-white">
                  {{ formatNumber(stats?.numberOfPurchases) }}
                </h3>
                <p class="summary-title text-white">
                  إجمالي المشتريات
                </p>
              </div>
            </div>
          </VCard>
        </VCol>
      </VRow>

      <!-- Statistics Grid -->
      <div class="statistics-grid">
        <div
          v-for="(category, categoryKey) in statsCategories"
          :key="categoryKey"
          class="category-section"
        >
          <VCard
            class="category-card"
            elevation="2"
          >
            <div
              class="category-header"
              :class="category.color"
            >
              <div class="d-flex align-center">
                <VIcon
                  :icon="category.items[0]?.icon"
                  class="me-2"
                  size="24"
                />
                <h3 class="category-title">
                  {{ category.title }}
                </h3>
              </div>
            </div>
            
            <div class="category-items">
              <div
                v-for="item in category.items"
                :key="item.key"
                class="stat-item"
              >
                <div class="stat-item-content">
                  <div
                    class="stat-item-icon"
                    :class="category.color"
                  >
                    <VIcon
                      :icon="item.icon"
                      size="24"
                    />
                  </div>
                  <div class="stat-item-details">
                    <div
                      class="stat-item-value"
                      :style="{ color: textColor }"
                    >
                      {{ formatNumber(stats?.[item.key]) }}
                    </div>
                    <div class="stat-item-title">
                      {{ item.title }}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </VCard>
        </div>
      </div>

      <!-- إحصائيات المندوبين -->
      <VCard
        class="dashboard-section mt-6 pa-6"
        elevation="2"
      >
        <div class="d-flex justify-space-between align-center mb-6 flex-wrap gap-4">
          <div>
            <h2 class="section-title">
              إحصائيات المندوبين
            </h2>
            <p class="section-subtitle">
              أداء المندوبين خلال الفترة المحددة
            </p>
          </div>
          <div class="d-flex align-center gap-2">
            <VBtn
              color="success"
              prepend-icon="tabler-file-spreadsheet"
              @click="exportToExcel"
            >
              تصدير Excel
            </VBtn>
          </div>
        </div>

        <!-- Loading for delegates -->
        <div
          v-if="loading"
          class="text-center py-5"
        >
          <VProgressCircular
            indeterminate
            color="primary"
          />
        </div>
        <div v-else>
          <!-- بطاقات الإجماليات للمندوبين -->
          <div class="totals-container mb-8">
            <VRow dense>
              <VCol
                v-for="(card, index) in totals"
                :key="index"
                cols="12"
                sm="6"
                md="4"
                lg="3"
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
          </div>

          <!-- فلترة البيانات -->
          <VCard
            class="filter-card pa-4 mb-6"
            elevation="1"
            variant="outlined"
          >
            <VRow>
              <VCol
                cols="12"
                md="4"
              >
                <span class="text-sm font-weight-medium mb-1 d-block">من التاريخ</span>
                <AppTextField
                  v-model="filters.fromDate"
                  type="date"
                  prepend-inner-icon="tabler-calendar"
                  @input="handleDateChangeFromDate"
                />
              </VCol>
              <VCol
                cols="12"
                md="4"
              >
                <span class="text-sm font-weight-medium mb-1 d-block">إلى التاريخ</span>
                <AppTextField
                  v-model="filters.toDate"
                  type="date"
                  prepend-inner-icon="tabler-calendar"
                  @input="handleDateChangeToDate"
                />
              </VCol>
              <VCol
                cols="12"
                md="4"
                style="margin-block-start: 20px;"
                class="d-flex align-center"
              >
                <VBtn
                  color="primary"
                  :loading="loading"
                  prepend-icon="tabler-search"
                  block
                  height="44"
                  @click="fetchStatsDelegates"
                >
                  بحث
                </VBtn>
              </VCol>
            </VRow>
          </VCard>

          <!-- جدول البيانات -->
          <VCard
            class="data-table-card"
            elevation="1"
          >
            <VDataTable
              :headers="headers"
              :items="statsData || []"
              :items-per-page="50"
              style="white-space: nowrap;"
              items-per-page-text="عدد السجل"
              hover
            >
              <template #item.delegateName="{ item }">
                <span class="font-weight-medium">{{ item?.delegateName || 'لا يوجد' }}</span>
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
                  {{ formattedNumber(item?.amountPrice) }}
                </div>
              </template>
              <template #item.amountCost="{ item }">
                <div class="premium-amount amt-total-sales">
                  {{ formattedNumber(item?.amountCost) }}
                </div>
              </template>
              <template #item.amountDay="{ item }">
                <div class="premium-amount amt-paid-yesterday">
                  {{ formattedNumber(item?.amountDay) }}
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
                  {{ formattedNumber(item?.amountReceipt) }}
                </div>
              </template>
              <template #item.numberOfCustomerZero="{ item }">
                <VChip
                  color="success"
                  size="small"
                  class="font-weight-bold"
                >
                  {{ item.numberOfCustomerZero || 0 }}
                </VChip>
              </template>
              <template #item.amountPriceZero="{ item }">
                <div class="premium-amount amt-installment">
                  {{ formattedNumber(item?.amountPriceZero) }}
                </div>
              </template>
              <template #item.amountDayZero="{ item }">
                <div class="premium-amount amt-paid-yesterday">
                  {{ formattedNumber(item?.amountDayZero) }}
                </div>
              </template>
            </VDataTable>
          </VCard>
        </div>
      </VCard>
    </div>
  </div>
</template>

<style scoped lang="scss">
.dashboard-container {
  padding: 24px;
  font-family: Cairo, "Segoe UI", sans-serif;
  min-block-size: 100vh;
}

.dashboard-header {
  overflow: hidden;
  border-radius: 16px;
  background: linear-gradient(135deg, #1e5799 0%, #207cca 100%);
  color: white;
  margin-block-end: 24px;

  .header-content {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 32px;

    @media (max-width: 960px) {
      flex-direction: column;
      gap: 20px;
      text-align: center;
    }
  }

  .header-text {
    flex: 1;
  }

  .header-title {
    display: flex;
    align-items: center;
    font-size: 2.2rem;
    font-weight: 700;
    margin-block-end: 8px;
  }

  .header-subtitle {
    margin: 0;
    font-size: 1.1rem;
    opacity: 0.9;
  }

  .header-actions {
    display: flex;
    gap: 12px;

    @media (max-width: 600px) {
      flex-direction: column;
      inline-size: 100%;

      .action-btn {
        inline-size: 100%;
      }
    }
  }
}

// Summary Cards
.summary-card {
  position: relative;
  overflow: hidden;
  border-radius: 16px;
  block-size: 140px;
  color: white;
  transition: transform 0.3s ease, box-shadow 0.3s ease;

  &::before {
    position: absolute;
    z-index: 1;
    background: linear-gradient(135deg, rgba(255, 255, 255, 10%) 0%, rgba(255, 255, 255, 0%) 100%);
    content: "";
    inset: 0;
  }

  &:hover {
    box-shadow: 0 12px 40px rgba(0, 0, 0, 20%);
    transform: translateY(-8px);
  }

  .summary-content {
    position: relative;
    z-index: 2;
    display: flex;
    align-items: center;
    padding: 24px;
    block-size: 100%;
    gap: 20px;
  }

  .summary-icon {
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 12px;
    background: rgba(255, 255, 255, 20%);
    block-size: 70px;
    inline-size: 70px;
  }

  .summary-text {
    flex: 1;
  }

  .summary-value {
    margin: 0;
    font-size: 2.5rem;
    font-weight: 800;
    line-height: 1;
  }

  .summary-title {
    color: rgba(255, 255, 255, 90%);
    font-size: 1.1rem;
    margin-block: 8px 0;
    margin-inline: 0;
    opacity: 0.9;
  }
}

// Statistics Grid
.statistics-grid {
  display: grid;
  gap: 24px;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  margin-block-end: 24px;

  @media (max-width: 600px) {
    grid-template-columns: 1fr;
  }
}

.category-section {
  .category-card {
    overflow: hidden;
    border-radius: 16px;
    block-size: 100%;
    transition: transform 0.3s ease, box-shadow 0.3s ease;

    &:hover {
      box-shadow: 0 10px 30px rgba(0, 0, 0, 15%);
      transform: translateY(-5px);
    }
  }

  .category-header {
    padding: 20px;
    color: white;

    &.primary {
      background: linear-gradient(135deg, rgb(var(--v-theme-primary)) 0%, rgba(var(--v-theme-primary), 0.8) 100%);
    }

    &.success {
      background: linear-gradient(135deg, rgb(var(--v-theme-success)) 0%, rgba(var(--v-theme-success), 0.8) 100%);
    }

    &.indigo {
      background: linear-gradient(135deg, rgb(var(--v-theme-info)) 0%, rgba(var(--v-theme-info), 0.8) 100%);
    }

    &.warning {
      background: linear-gradient(135deg, rgb(var(--v-theme-warning)) 0%, rgba(var(--v-theme-warning), 0.8) 100%);
    }

    &.info {
      background: linear-gradient(135deg, rgb(var(--v-theme-secondary)) 0%, rgba(var(--v-theme-secondary), 0.8) 100%);
    }
  }

  .category-title {
    margin: 0;
    font-size: 1.3rem;
    font-weight: 600;
  }

  .category-items {
    padding: 20px;
  }

  .stat-item {
    margin-block-end: 16px;

    &:last-child {
      margin-block-end: 0;
    }
  }

  .stat-item-content {
    display: flex;
    align-items: center;
    gap: 16px;
  }

  .stat-item-icon {
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 10px;
    block-size: 50px;
    inline-size: 50px;

    &.primary {
      background: rgba(var(--v-theme-primary), 0.1);
      color: rgb(var(--v-theme-primary));
    }

    &.success {
      background: rgba(var(--v-theme-success), 0.1);
      color: rgb(var(--v-theme-success));
    }

    &.warning {
      background: rgba(var(--v-theme-warning), 0.1);
      color: rgb(var(--v-theme-warning));
    }

    &.info {
      background: rgba(var(--v-theme-secondary), 0.1);
      color: rgb(var(--v-theme-secondary));
    }
  }

  .stat-item-details {
    flex: 1;
  }

  .stat-item-value {
    font-size: 1.5rem;
    font-weight: 700;
    line-height: 1;
    margin-block-end: 4px;
  }

  .stat-item-title {
    color: #666;
    font-size: 0.9rem;
    font-weight: 500;
  }
}

.total-card {
  transition: transform 0.2s;

  .total-value {
    font-size: 1.5rem;
    font-weight: bold;
    margin-block-start: 8px;
  }

  .total-title {
    font-size: 0.9rem;
    opacity: 0.8;
  }

  &:hover {
    transform: scale(1.02);
  }
}

.section-title {
  color: rgb(var(--v-theme-primary));
  font-size: 1.5rem;
  font-weight: bold;
}

.section-subtitle {
  color: gray;
  margin-block-end: 20px;
}

// Animation
@keyframes fade-in {
  from {
    opacity: 0;
    transform: translateY(20px);
  }

  to {
    opacity: 1;
    transform: translateY(0);
  }
}


// Premium numeric badges style
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

// Light Theme Styling
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

// Dark Theme Styling
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
