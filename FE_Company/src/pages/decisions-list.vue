<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import ModernStatCard from "@/components/ModernStatCard.vue"
import axios from 'axios'
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import * as XLSX from 'xlsx'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'
import { directorLegalDecisions } from '@/composables/directorApi'
import { useMonitorLegalCity } from '@/composables/useMonitorLegalCity'

const apiUrl = localStorage.getItem('LinkCity')
const router = useRouter()
const toast = useToast()
const { isMonitor } = useUserRole()
const {
  cityValue: monitorCity,
  cityItems: monitorCityItems,
  citiesLoading: monitorCitiesLoading,
  setCity: setMonitorCity,
} = useMonitorLegalCity()

const decisions = ref([])
const loading = ref(false)

const filters = ref({
  decisionType: 'الكل',
  fromDate: null,
  toDate: null,
})

const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "

  return "لا يوجد"
}

const formattedDateTime = date => {
  if (!date) return 'لا يوجد'

  return new Date(date).toLocaleString('ar-IQ')
}

const headers = [
  { title: 'الملف', key: 'actions', sortable: false },
  { title: 'الزبون', key: 'customerName' },
  { title: 'الهاتف', key: 'phoneNumber' },
  { title: 'نوع القرار', key: 'decisionType' },
  { title: 'من قرر', key: 'userName' },
  { title: 'النسبة %', key: 'paidPercent' },
  { title: 'مدفوع الأسبوع', key: 'weekPaid' },
  { title: 'سعر البيع', key: 'amountTotalSales' },
  { title: 'التاريخ', key: 'createdDate' },
  { title: 'ملاحظة', key: 'note' },
]

const typeColor = type => {
  if (type === 'قانونية' || type === 'طلب قانونية') return 'error'
  if (type === 'تنازل' || type === 'طلب تنازل') return 'success'
  if (type === 'وهمي') return 'warning'
  if (type === 'متواصل') return 'info'

  return 'primary'
}

const totals = computed(() => [
  {
    icon: 'tabler-list-check',
    value: decisions.value.length,
    title: 'كل القرارات',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-phone-call',
    value: decisions.value.filter(d => d.decisionType === 'متواصل').length,
    title: 'متواصل',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
  {
    icon: 'tabler-scale',
    value: decisions.value.filter(d => d.decisionType === 'قانونية' || d.decisionType === 'طلب قانونية').length,
    title: 'قانونية',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
  {
    icon: 'tabler-cash',
    value: decisions.value.filter(d => d.decisionType === 'تنازل' || d.decisionType === 'طلب تنازل').length,
    title: 'تنازل',
    color: "success",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
])

async function fetchDecisions() {
  if (isMonitor.value && !monitorCity.value) {
    decisions.value = []
    loading.value = false

    return
  }

  try {
    loading.value = true
    if (isMonitor.value) {
      const data = await directorLegalDecisions(monitorCity.value, {
        decisionType: filters.value.decisionType && filters.value.decisionType !== 'الكل'
          ? filters.value.decisionType
          : undefined,
        fromDate: filters.value.fromDate || undefined,
        toDate: filters.value.toDate || undefined,
      })
      decisions.value = Array.isArray(data) ? data : []
    }
    else {
      const authHeader = getAuthHeaders()
      const params = {}
      if (filters.value.decisionType && filters.value.decisionType !== 'الكل') {
        params.decisionType = filters.value.decisionType
      }
      if (filters.value.fromDate) params.fromDate = filters.value.fromDate
      if (filters.value.toDate) params.toDate = filters.value.toDate

      const response = await axios.get(`${apiUrl}CustomerDecisions/Decisions`, {
        headers: authHeader,
        params,
      })
      decisions.value = response.data || []
    }
  } catch (error) {
    console.error(error)
    toast.error('تعذر جلب القرارات')
  } finally {
    loading.value = false
  }
}

function openProfile(item) {
  const customerId = item.customerID || item.customerId
  if (isMonitor.value) {
    router.push({
      name: 'sales-manager-customer-profile',
      query: {
        cityValue: monitorCity.value,
        customerId,
        name: item.customerName || '',
        returnTo: 'decisions-list',
      },
    })

    return
  }
  router.push({ name: 'customer-profile', query: { customerID: customerId } })
}

function exportToExcel() {
  if (!decisions.value.length) {
    toast.error('لا توجد قرارات للتصدير')

    return
  }

  const dataToExport = decisions.value.map(d => ({
    'الزبون': d.customerName || d.CustomerName || '—',
    'الهاتف': d.phoneNumber || d.PhoneNumber || '—',
    'نوع القرار': d.decisionType || d.DecisionType || '—',
    'من قرر': d.userName || d.UserName || '—',
    'النسبة %': d.paidPercent ?? d.PaidPercent ?? 0,
    'مدفوع الأسبوع': d.weekPaid ?? d.WeekPaid ?? 0,
    'سعر البيع': d.amountTotalSales ?? d.AmountTotalSales ?? 0,
    'التاريخ': formattedDateTime(d.createdDate || d.CreatedDate),
    'ملاحظة': d.note || d.Note || '—',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(workbook, worksheet, 'سجل القرارات')

  const cityPart = isMonitor.value && monitorCity.value
    ? `_${String(monitorCity.value).replace(/[\\/:*?"<>|]/g, '_')}`
    : ''
  const stamp = new Date().toISOString().slice(0, 10)
  XLSX.writeFile(workbook, `سجل_القرارات${cityPart}_${stamp}.xlsx`)
  toast.success(`تم تصدير ${dataToExport.length} قرار`)
}

watch(monitorCity, () => {
  if (isMonitor.value)
    fetchDecisions()
})

onMounted(() => {
  fetchDecisions()
})
</script>

<template>
  <VCard
    v-if="isMonitor"
    class="pa-4 mb-4"
  >
    <VSelect
      :model-value="monitorCity"
      :items="monitorCityItems"
      :loading="monitorCitiesLoading"
      label="المحافظة"
      item-title="title"
      item-value="value"
      clearable
      @update:model-value="setMonitorCity"
    />
    <VAlert
      v-if="!monitorCity"
      type="info"
      variant="tonal"
      class="mt-3"
    >
      اختر المحافظة لعرض سجل القرارات
    </VAlert>
  </VCard>

  <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, index) in totals"
      :key="index"
      cols="12"
      sm="6"
      md="3"
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
    <VRow>
      <VCol
        md="3"
        cols="12"
      >
        <VLabel class="mb-2">نوع القرار</VLabel>
        <VAutocomplete
          v-model="filters.decisionType"
          :items="['الكل', 'متواصل', 'قانونية', 'طلب قانونية', 'وهمي', 'طلب تنازل', 'تنازل']"
          prepend-inner-icon="tabler-filter"
        />
      </VCol>
      <VCol
        md="3"
        cols="12"
      >
        <VLabel class="mb-2">من تاريخ</VLabel>
        <VTextField
          v-model="filters.fromDate"
          type="date"
        />
      </VCol>
      <VCol
        md="3"
        cols="12"
      >
        <VLabel class="mb-2">إلى تاريخ</VLabel>
        <VTextField
          v-model="filters.toDate"
          type="date"
        />
      </VCol>
      <VCol
        md="3"
        cols="12"
        class="d-flex align-end flex-wrap gap-2"
      >
        <VBtn
          color="primary"
          :loading="loading"
          prepend-icon="tabler-search"
          @click="fetchDecisions"
        >
          بحث
        </VBtn>
        <VBtn
          color="success"
          variant="tonal"
          prepend-icon="tabler-file-spreadsheet"
          :disabled="!decisions.length || loading"
          @click="exportToExcel"
        >
          تصدير إلى إكسل
        </VBtn>
      </VCol>
    </VRow>

    <VDataTable
      :headers="headers"
      :items="decisions"
      :loading="loading"
      :items-per-page="50"
      items-per-page-text="عدد السجل"
      class="text-no-wrap custom-data-table"
    >
      <template #item.actions="{ item }">
        <VBtn
          color="secondary"
          variant="tonal"
          prepend-icon="tabler-user"
          @click="openProfile(item)"
        >
          الملف
        </VBtn>
      </template>
      <template #item.decisionType="{ item }">
        <VChip
          :color="typeColor(item.decisionType)"
          size="small"
          label
        >
          {{ item.decisionType }}
        </VChip>
      </template>
      <template #item.paidPercent="{ item }">
        <div>{{ item.paidPercent ?? 0 }} %</div>
      </template>
      <template #item.weekPaid="{ item }">
        <div>{{ formattedNumber(item.weekPaid) }}</div>
      </template>
      <template #item.amountTotalSales="{ item }">
        <div>{{ formattedNumber(item.amountTotalSales) }}</div>
      </template>
      <template #item.createdDate="{ item }">
        <div>{{ formattedDateTime(item.createdDate) }}</div>
      </template>
      <template #item.note="{ item }">
        <div>{{ item.note || '—' }}</div>
      </template>
    </VDataTable>
  </VCard>
</template>
