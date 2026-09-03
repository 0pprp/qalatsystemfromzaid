<script setup>
import { onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { branchRowKey, smGet, withCityQuery } from '@/composables/salesManagerApi'

const route = useRoute()
const rows = ref([])
const cityValue = ref('')
const employeeId = ref('')
const status = ref(typeof route.query.status === 'string' ? route.query.status : '')
const date = ref('')

async function load() {
  const q = []
  if (employeeId.value)
    q.push(`employeeId=${employeeId.value}`)
  if (status.value)
    q.push(`status=${encodeURIComponent(status.value)}`)
  if (date.value)
    q.push(`date=${date.value}`)
  const path = `sales${q.length ? `?${q.join('&')}` : ''}`

  rows.value = await smGet(withCityQuery(path, cityValue.value))
}

onMounted(load)
watch(() => route.query.status, value => {
  status.value = typeof value === 'string' ? value : ''
  load()
})
</script>

<template>
  <div>
    <h4 class="mb-4">
      {{ status === 'Pending' ? 'المبيعات المعلقة' : 'مبيعات الموظفين' }}
    </h4>
    <VRow class="mb-3">
      <VCol md="3">
        <SalesBranchFilter
          v-model="cityValue"
          @change="load"
        />
      </VCol>
      <VCol md="3">
        <VTextField
          v-model="employeeId"
          label="موظف"
          hide-details
        />
      </VCol>
      <VCol md="2">
        <VTextField
          v-model="status"
          label="الحالة"
          hide-details
        />
      </VCol>
      <VCol md="2">
        <VTextField
          v-model="date"
          type="date"
          label="التاريخ"
          hide-details
        />
      </VCol>
      <VCol md="2">
        <VBtn @click="load">
          تصفية
        </VBtn>
      </VCol>
    </VRow>
    <VTable>
      <thead>
        <tr>
          <th>الزبون</th><th>الموظف</th><th>المحافظة</th><th>التقييم</th><th>السعر</th><th>الحالة</th><th>التاريخ</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in rows"
          :key="branchRowKey(row, 'saleId')"
        >
          <td>{{ row.customerName }}</td>
          <td>{{ row.employeeName }}</td>
          <td>{{ row.branchName || row.cityName }}</td>
          <td>{{ row.evaluationName }}</td>
          <td>{{ row.finalSalePrice }}</td>
          <td>{{ row.status }}</td>
          <td>{{ row.createdAt }}</td>
        </tr>
      </tbody>
    </VTable>
  </div>
</template>
