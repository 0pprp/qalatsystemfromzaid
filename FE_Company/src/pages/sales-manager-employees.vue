<script setup>
import { onMounted, ref } from 'vue'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { branchRowKey, locationStatusLabel, smGetEmployees } from '@/composables/salesManagerApi'
import { createRequestGeneration } from '@/utils/provinceCatalog'

const rows = ref([])
const cityValue = ref('')
const shiftStatus = ref('')
const loading = ref(false)
const loadError = ref('')
const requestGen = createRequestGeneration()

async function load() {
  const token = requestGen.next()
  loading.value = true
  loadError.value = ''
  // Clear immediately so Najaf rows never linger while Karkh/Basra loads.
  rows.value = []

  try {
    const q = []
    if (shiftStatus.value)
      q.push(`shiftStatus=${encodeURIComponent(shiftStatus.value)}`)
    const data = await smGetEmployees(cityValue.value, q.join('&'))
    if (!requestGen.isCurrent(token))
      return
    rows.value = data
  }
  catch (err) {
    if (!requestGen.isCurrent(token))
      return
    rows.value = []
    loadError.value = err?.response?.data?.message
      || err?.message
      || 'تعذر تحميل الموظفين'
  }
  finally {
    if (requestGen.isCurrent(token))
      loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <h4 class="mb-4">
      الموظفون
    </h4>
    <VRow class="mb-3">
      <VCol
        cols="12"
        md="4"
      >
        <SalesBranchFilter
          v-model="cityValue"
          @change="load"
        />
      </VCol>
      <VCol
        cols="12"
        md="4"
      >
        <VSelect
          v-model="shiftStatus"
          :items="[{ title: 'الكل', value: '' }, { title: 'Active', value: 'Active' }, { title: 'NoShift', value: 'NoShift' }]"
          item-title="title"
          item-value="value"
          label="حالة الدوام"
          hide-details
          @update:model-value="load"
        />
      </VCol>
    </VRow>
    <div
      v-if="loading"
      class="text-medium-emphasis mb-3"
    >
      جاري التحميل...
    </div>
    <VAlert
      v-if="loadError"
      type="error"
      variant="tonal"
      class="mb-3"
    >
      {{ loadError }}
    </VAlert>
    <VTable>
      <thead>
        <tr>
          <th>الاسم</th><th>المحافظة</th><th>الدوام</th><th>الموقع</th><th>آخر تحديث</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in rows"
          :key="branchRowKey(row)"
        >
          <td>{{ row.employeeName }}</td>
          <td>{{ row.branchName || row.cityName }}</td>
          <td>{{ row.shiftStatus }}</td>
          <td>{{ locationStatusLabel[row.locationStatus] || row.locationStatus }}</td>
          <td>{{ row.lastLocationAt || '—' }}</td>
        </tr>
      </tbody>
    </VTable>
  </div>
</template>
