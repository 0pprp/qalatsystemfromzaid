<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { formatIraqTime } from '@/composables/gpsTrack'
import { branchRowKey, smGet, withCityQuery } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const router = useRouter()
const rows = ref([])
const cityValue = ref('')
const employeeId = ref('')
const date = ref('')

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function money(value) {
  const n = Number(value || 0)
  if (!Number.isFinite(n))
    return '0 د.ع'

  return `${n.toLocaleString('en-US')} د.ع`
}

function saleCity(row) {
  return pick(row, 'cityValue', 'CityValue') || cityValue.value
}

async function load() {
  const q = ['status=Completed']
  if (employeeId.value)
    q.push(`employeeId=${employeeId.value}`)
  if (date.value)
    q.push(`date=${date.value}`)
  try {
    rows.value = await smGet(withCityQuery(`sales?${q.join('&')}`, cityValue.value)) || []
  }
  catch (err) {
    rows.value = []
    toast.error(err?.response?.data?.message || 'تعذر تحميل المبيعات')
  }
}

function openProfile(row) {
  const city = saleCity(row)
  if (!city) {
    toast.error('حدد المحافظة أولاً')

    return
  }
  router.push({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: city,
      customerId: pick(row, 'customerId', 'CustomerId') || '',
      name: pick(row, 'customerName', 'CustomerName', 'fullName', 'FullName') || '',
      phone: pick(row, 'customerPhone', 'CustomerPhone', 'phone', 'Phone') || '',
    },
  })
}

onMounted(load)
</script>

<template>
  <div>
    <h4 class="mb-4">
      المبيعات
    </h4>
    <VRow class="mb-3">
      <VCol md="4">
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
      <VCol md="3">
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
          <th>الزبون</th>
          <th>الموظف</th>
          <th>المحافظة</th>
          <th>السعر النهائي</th>
          <th>القسط</th>
          <th>المقدمة</th>
          <th>التاريخ</th>
          <th>بروفايل الزبون</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in rows"
          :key="branchRowKey(row, 'saleId')"
        >
          <td>{{ row.customerName || row.fullName }}</td>
          <td>{{ row.employeeName }}</td>
          <td>{{ row.province || row.branchName || row.cityName }}</td>
          <td>{{ money(row.finalSalePrice) }}</td>
          <td>{{ money(row.dailyInstallment) }}</td>
          <td>{{ money(row.downPayment) }}</td>
          <td>{{ formatIraqTime(pick(row, 'completedAt', 'CompletedAt', 'createdAt', 'CreatedAt')) }}</td>
          <td>
            <VBtn
              size="small"
              color="primary"
              @click="openProfile(row)"
            >
              فتح بروفايل الزبون
            </VBtn>
          </td>
        </tr>
      </tbody>
    </VTable>
  </div>
</template>
