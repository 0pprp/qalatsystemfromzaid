<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import ModernStatCard from '@/components/ModernStatCard.vue'
import axios from 'axios'
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'
import { directorLegalWaiverAccountantRequests } from '@/composables/directorApi'
import { useMonitorLegalCity } from '@/composables/useMonitorLegalCity'

definePage({
  meta: {
    layout: 'default',
  },
})

const apiUrl = localStorage.getItem('LinkCity')
const router = useRouter()
const toast = useToast()
const { isAdmin, isMonitor } = useUserRole()
const {
  cityValue: monitorCity,
  cityItems: monitorCityItems,
  citiesLoading: monitorCitiesLoading,
  setCity: setMonitorCity,
} = useMonitorLegalCity()

const rows = ref([])
const loading = ref(false)
const actingId = ref(null)
const statusFilter = ref('Pending')
const confirmDialog = ref(false)
const selected = ref(null)

const statusItems = [
  { title: 'بانتظار الموافقة', value: 'Pending' },
  { title: 'مطبّقة', value: 'Applied' },
  { title: 'الكل', value: '' },
]

const headers = [
  { title: 'إجراء', key: 'actions', sortable: false },
  { title: 'الزبون', key: 'customerName' },
  { title: 'الهاتف', key: 'customerPhone' },
  { title: 'مبلغ التنازل', key: 'approvedAmount' },
  { title: 'من وافق (مفوض)', key: 'decidedByDisplayName' },
  { title: 'الحالة', key: 'status' },
  { title: 'ملاحظة الطلب', key: 'requestNote' },
  { title: 'تاريخ الطلب', key: 'requestedAt' },
  { title: 'المراجع', key: 'reviewedByName' },
  { title: 'تاريخ المراجعة', key: 'reviewedAt' },
]

const statusLabel = status => {
  if (status === 'Pending') return 'بانتظار'
  if (status === 'Applied') return 'مطبّق'

  return status || '-'
}

const statusColor = status => {
  if (status === 'Pending') return 'warning'
  if (status === 'Applied') return 'success'

  return 'primary'
}

const formattedNumber = num => {
  if (num == null) return '—'
  if (num === 0) return '0 دع'

  return `${Number(num).toLocaleString()} دع`
}

const formattedDateTime = date => {
  if (!date) return '—'

  return new Date(date).toLocaleString('ar-IQ')
}

const totals = computed(() => [
  {
    icon: 'tabler-clock',
    value: statusFilter.value === 'Pending'
      ? rows.value.length
      : rows.value.filter(r => r.status === 'Pending').length,
    title: 'بانتظار المحاسب',
    color: 'warning',
    gradient: 'linear-gradient(135deg, #fce38a 0%, #f38181 100%)',
  },
  {
    icon: 'tabler-cash',
    value: rows.value.length,
    title: statusFilter.value ? `المعروض (${statusLabel(statusFilter.value)})` : 'كل الطلبات',
    color: 'primary',
    gradient: 'linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)',
  },
])

async function fetchRows() {
  if (isMonitor.value && !monitorCity.value) {
    rows.value = []
    loading.value = false

    return
  }

  try {
    loading.value = true
    if (isMonitor.value) {
      const data = await directorLegalWaiverAccountantRequests(monitorCity.value, {
        status: statusFilter.value || undefined,
        take: 200,
        skip: 0,
      })
      rows.value = data?.items || []
    }
    else {
      const authHeader = getAuthHeaders()
      const params = { take: 200, skip: 0 }
      if (statusFilter.value)
        params.status = statusFilter.value

      const { data } = await axios.get(`${apiUrl}CustomerDecisions/WaiverAccountantRequests`, {
        headers: authHeader,
        params,
      })
      rows.value = data?.items || []
    }
  }
  catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'تعذر جلب طلبات التنازل')
  }
  finally {
    loading.value = false
  }
}

function openProfile(item) {
  const customerId = item.customerId || item.customerID
  if (isMonitor.value) {
    router.push({
      name: 'sales-manager-customer-profile',
      query: {
        cityValue: monitorCity.value,
        customerId,
        name: item.customerName || '',
        returnTo: 'waiver-accountant-requests',
      },
    })

    return
  }
  router.push({ name: 'customer-profile', query: { customerID: customerId } })
}

watch(monitorCity, () => {
  if (isMonitor.value)
    fetchRows()
})

function openApprove(item) {
  if (!isAdmin.value) {
    toast.warning('الموافقة من صلاحية المحاسب الرئيسي فقط')

    return
  }
  selected.value = item
  confirmDialog.value = true
}

async function submitApprove() {
  if (!selected.value || !isAdmin.value)
    return

  const id = selected.value.requestId
  try {
    actingId.value = id
    const authHeader = getAuthHeaders()
    await axios.put(
      `${apiUrl}CustomerDecisions/WaiverAccountantRequests/${id}/Approve`,
      {},
      { headers: authHeader },
    )
    confirmDialog.value = false
    toast.success('تمت الموافقة: خصم من سعر البيع وتسجيل الملاحظة')
    await fetchRows()
  }
  catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'فشل تطبيق التنازل')
  }
  finally {
    actingId.value = null
  }
}

onMounted(() => {
  fetchRows()
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
      اختر المحافظة لعرض طلبات التنازل
    </VAlert>
  </VCard>

  <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, index) in totals"
      :key="index"
      cols="12"
      sm="6"
      md="4"
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
    <div class="text-h6 mb-4">
      طلبات التنازل — موافقة المحاسب الرئيسي تخصم المبلغ من سعر البيع
    </div>

    <VRow class="mb-4">
      <VCol
        cols="12"
        md="4"
      >
        <VLabel class="mb-2">الحالة</VLabel>
        <VSelect
          v-model="statusFilter"
          :items="statusItems"
          item-title="title"
          item-value="value"
          @update:model-value="fetchRows"
        />
      </VCol>
      <VCol
        cols="12"
        md="8"
        class="d-flex align-end justify-end ga-2"
      >
        <VBtn
          color="primary"
          :loading="loading"
          prepend-icon="tabler-refresh"
          @click="fetchRows"
        >
          تحديث
        </VBtn>
      </VCol>
    </VRow>

    <VAlert
      v-if="!isAdmin"
      type="info"
      variant="tonal"
      class="mb-4"
    >
      عرض الطلبات فقط. الموافقة من صلاحية المحاسب الرئيسي (بدون رفض).
    </VAlert>

    <VDataTable
      :headers="headers"
      :items="rows"
      :loading="loading"
      class="text-no-wrap"
      items-per-page="25"
    >
      <template #item.actions="{ item }">
        <div class="d-flex ga-1">
          <IconBtn
            size="small"
            @click="openProfile(item)"
          >
            <VIcon icon="tabler-user" />
          </IconBtn>
          <VBtn
            v-if="isAdmin && item.status === 'Pending'"
            size="small"
            color="success"
            variant="tonal"
            :loading="actingId === item.requestId"
            @click="openApprove(item)"
          >
            موافقة
          </VBtn>
        </div>
      </template>

      <template #item.approvedAmount="{ item }">
        {{ formattedNumber(item.approvedAmount) }}
      </template>

      <template #item.status="{ item }">
        <VChip
          size="small"
          :color="statusColor(item.status)"
          label
        >
          {{ statusLabel(item.status) }}
        </VChip>
      </template>

      <template #item.requestedAt="{ item }">
        {{ formattedDateTime(item.requestedAt) }}
      </template>

      <template #item.reviewedAt="{ item }">
        {{ formattedDateTime(item.reviewedAt) }}
      </template>
    </VDataTable>
  </VCard>

  <VDialog
    v-model="confirmDialog"
    max-width="480"
  >
    <VCard>
      <VCardTitle>موافقة طلب التنازل</VCardTitle>
      <VCardText>
        <div class="mb-2">
          الزبون: <strong>{{ selected?.customerName }}</strong>
        </div>
        <div class="mb-2">
          المبلغ: <strong>{{ formattedNumber(selected?.approvedAmount) }}</strong>
        </div>
        <div class="text-medium-emphasis">
          سيتم خصم المبلغ من سعر البيع وكتابة ملاحظة على الزبون. لا يوجد رفض.
        </div>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          @click="confirmDialog = false"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="success"
          :loading="actingId != null"
          @click="submitApprove"
        >
          موافقة وتطبيق الخصم
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
