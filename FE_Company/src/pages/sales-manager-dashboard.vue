<script setup>
import { onMounted, ref } from 'vue'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { smGet, withCityQuery } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const cityValue = ref('')
const loadError = ref('')

const cards = ref({
  employeesOnShift: 0,
  employeesOffShift: 0,
  liveLocations: 0,
  salesToday: 0,
  pendingSales: 0,
  newSalesRequests: 0,
})

function num(raw, ...keys) {
  if (!raw)
    return 0
  for (const key of keys) {
    const value = raw[key]
    if (value != null && value !== '')
      return Number(value) || 0
  }

  return 0
}

function mapDashboard(raw) {
  return {
    employeesOnShift: num(raw, 'employeesOnShift', 'EmployeesOnShift'),
    employeesOffShift: num(raw, 'employeesOffShift', 'EmployeesOffShift'),
    liveLocations: num(raw, 'liveLocations', 'LiveLocations'),
    salesToday: num(raw, 'salesToday', 'SalesToday'),
    pendingSales: num(raw, 'pendingSales', 'PendingSales'),
    newSalesRequests: num(raw, 'newSalesRequests', 'NewSalesRequests'),
  }
}

async function load() {
  loadError.value = ''
  try {
    const raw = await smGet(withCityQuery('dashboard', cityValue.value))
    cards.value = mapDashboard(raw)
  }
  catch (err) {
    const status = err?.response?.status
    const backend = err?.response?.data
    const message = backend?.message || backend?.Message || err?.message || 'تعذر تحميل نظرة عامة'
    loadError.value = status ? `${message} (${status})` : message
    console.error('dashboard failed', {
      cityValue: cityValue.value,
      status,
      body: backend,
      message,
    })
    toast.error(loadError.value)
  }
}

onMounted(load)
</script>

<template>
  <div>
    <h4 class="mb-4">
      نظرة عامة — إدارة المبيعات
    </h4>
    <VRow class="mb-4">
      <VCol
        cols="12"
        md="4"
      >
        <SalesBranchFilter
          v-model="cityValue"
          @change="load"
        />
      </VCol>
    </VRow>
    <VAlert
      v-if="loadError"
      class="mb-4"
      type="error"
      variant="tonal"
    >
      {{ loadError }}
    </VAlert>
    <VRow>
      <VCol
        cols="12"
        md="4"
      >
        <VCard><VCardText>الموظفون في الدوام<br><strong>{{ cards.employeesOnShift }}</strong></VCardText></VCard>
      </VCol>
      <VCol
        cols="12"
        md="4"
      >
        <VCard><VCardText>بدون دوام<br><strong>{{ cards.employeesOffShift }}</strong></VCardText></VCard>
      </VCol>
      <VCol
        cols="12"
        md="4"
      >
        <VCard><VCardText>الموقع المباشر<br><strong>{{ cards.liveLocations }}</strong></VCardText></VCard>
      </VCol>
      <VCol
        cols="12"
        md="4"
      >
        <VCard><VCardText>المبيعات اليوم<br><strong>{{ cards.salesToday }}</strong></VCardText></VCard>
      </VCol>
      <VCol
        cols="12"
        md="4"
      >
        <VCard><VCardText>الطلبات المعلقة<br><strong>{{ cards.pendingSales }}</strong></VCardText></VCard>
      </VCol>
      <VCol
        cols="12"
        md="4"
      >
        <VCard><VCardText>طلبات جديدة<br><strong>{{ cards.newSalesRequests }}</strong></VCardText></VCard>
      </VCol>
    </VRow>
  </div>
</template>
