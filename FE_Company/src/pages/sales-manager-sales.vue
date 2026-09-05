<script setup>
import { onMounted, ref } from 'vue'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { formatIraqTime } from '@/composables/gpsTrack'
import { branchRowKey, smGet, smGetBlob, withCityQuery } from '@/composables/salesManagerApi'
import { isDemo } from '@/composables/useCities'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const rows = ref([])
const cityValue = ref('')
const employeeId = ref('')
const date = ref('')
const profileOpen = ref(false)
const profile = ref(null)
const profileRow = ref(null)

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

function salePath(row, suffix) {
  const id = pick(row, 'saleId', 'SaleId')
  const city = saleCity(row)
  if (isDemo())
    return `sales/${id}${suffix}`

  return `sales/${encodeURIComponent(city)}/${id}${suffix}`
}

function customerProfilePath(city, params) {
  return isDemo()
    ? `customers/profile?${params}`
    : `customers/${encodeURIComponent(city)}/profile?${params}`
}

function isContract(doc) {
  const type = String(pick(doc, 'type', 'Type') || '')
  return type === 'Contract' || type === 'PreviewContract'
}

function isPromissory(doc) {
  const type = String(pick(doc, 'type', 'Type') || '')
  return type === 'PromissoryNote' || type === 'PreviewPromissoryNote'
}

async function load() {
  const q = ['status=Completed']
  if (employeeId.value)
    q.push(`employeeId=${employeeId.value}`)
  if (date.value)
    q.push(`date=${date.value}`)
  rows.value = await smGet(withCityQuery(`sales?${q.join('&')}`, cityValue.value)) || []
}

async function openDocument(row, kind) {
  try {
    const docs = await smGet(salePath(row, '/documents')) || []
    const list = Array.isArray(docs) ? docs : []
    const doc = list.find(item => (kind === 'contract' ? isContract(item) : isPromissory(item)))
    if (!doc) {
      toast.error(kind === 'contract' ? 'عقد البيع غير متوفر' : 'وصل الأمانة غير متوفر')

      return
    }
    const id = pick(doc, 'documentId', 'DocumentId')
    const blob = await smGetBlob(salePath(row, `/documents/${id}/download`))
    const url = URL.createObjectURL(blob)
    window.open(url, '_blank')
  }
  catch {
    toast.error('تعذر فتح المستند')
  }
}

async function openProfile(row) {
  profileRow.value = row
  profileOpen.value = true
  profile.value = null
  const city = saleCity(row)
  if (!city)
    return
  const params = new URLSearchParams()
  const customerId = pick(row, 'customerId', 'CustomerId')
  const name = pick(row, 'customerName', 'CustomerName', 'fullName', 'FullName')
  const phone = pick(row, 'customerPhone', 'CustomerPhone', 'phone', 'Phone')
  if (customerId)
    params.set('customerId', String(customerId))
  if (name)
    params.set('name', name)
  if (phone)
    params.set('phone', phone)
  if (![...params.keys()].length)
    return
  try {
    profile.value = await smGet(customerProfilePath(city, params))
  }
  catch {
    profile.value = null
  }
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
          <th>العقد</th>
          <th>وصل الأمانة</th>
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
          <td>
            <VBtn
              size="small"
              variant="text"
              @click="openDocument(row, 'contract')"
            >
              عقد البيع
            </VBtn>
          </td>
          <td>
            <VBtn
              size="small"
              variant="text"
              @click="openDocument(row, 'promissory')"
            >
              وصل الأمانة
            </VBtn>
          </td>
          <td>
            <VBtn
              size="small"
              color="primary"
              @click="openProfile(row)"
            >
              بروفايل الزبون
            </VBtn>
          </td>
        </tr>
      </tbody>
    </VTable>

    <VDialog
      v-model="profileOpen"
      max-width="640"
    >
      <VCard>
        <VCardTitle>بروفايل الزبون</VCardTitle>
        <VCardText>
          <div>الاسم: {{ pick(profile, 'customerName', 'CustomerName') || pick(profileRow, 'customerName', 'fullName') }}</div>
          <div>الهاتف: {{ pick(profile, 'phone', 'Phone') || pick(profileRow, 'customerPhone', 'phone') }}</div>
          <div>المحافظة: {{ pick(profileRow, 'province', 'cityName', 'branchName') }}</div>
          <div v-if="pick(profile, 'latestShop', 'LatestShop')">
            المحل: {{ pick(profile.latestShop || profile.LatestShop, 'shopName', 'ShopName') }}
          </div>
          <div class="text-medium-emphasis mt-2">
            {{ formatIraqTime(pick(profileRow, 'completedAt', 'CompletedAt', 'createdAt', 'CreatedAt')) }}
          </div>
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="profileOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
