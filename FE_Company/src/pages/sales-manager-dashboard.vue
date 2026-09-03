<script setup>
import { onMounted, ref } from 'vue'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { smGet, withCityQuery } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const cityValue = ref('')

const cards = ref({
  employeesOnShift: 0,
  employeesOffShift: 0,
  liveLocations: 0,
  salesToday: 0,
  pendingSales: 0,
  newSalesRequests: 0,
})

async function load() {
  try {
    cards.value = await smGet(withCityQuery('dashboard', cityValue.value))
  }
  catch {
    toast.error('تعذر تحميل نظرة عامة')
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
        <VCard><VCardText>المبيعات المعلقة<br><strong>{{ cards.pendingSales }}</strong></VCardText></VCard>
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
