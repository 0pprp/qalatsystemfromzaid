<script setup>
import { onMounted, ref } from 'vue'
import { locationStatusLabel, smGet } from '@/composables/salesManagerApi'

const rows = ref([])
const cityValue = ref('')
const shiftStatus = ref('')

async function load() {
  const q = []
  if (cityValue.value) q.push(`cityValue=${encodeURIComponent(cityValue.value)}`)
  if (shiftStatus.value) q.push(`shiftStatus=${encodeURIComponent(shiftStatus.value)}`)
  rows.value = await smGet(`employees${q.length ? `?${q.join('&')}` : ''}`)
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
        <VTextField
          v-model="cityValue"
          label="الفرع"
          hide-details
          @keyup.enter="load"
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
    <VTable>
      <thead>
        <tr>
          <th>الاسم</th><th>المحافظة</th><th>الدوام</th><th>الموقع</th><th>آخر تحديث</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in rows"
          :key="row.employeeId"
        >
          <td>{{ row.employeeName }}</td>
          <td>{{ row.cityName }}</td>
          <td>{{ row.shiftStatus }}</td>
          <td>{{ locationStatusLabel[row.locationStatus] || row.locationStatus }}</td>
          <td>{{ row.lastLocationAt || '—' }}</td>
        </tr>
      </tbody>
    </VTable>
  </div>
</template>
