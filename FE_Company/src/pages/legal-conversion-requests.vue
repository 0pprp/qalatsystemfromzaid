<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import ModernStatCard from '@/components/ModernStatCard.vue'
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'

const apiUrl = localStorage.getItem('LinkCity')
const router = useRouter()
const toast = useToast()
const { isAdmin } = useUserRole()

const rows = ref([])
const total = ref(0)
const loading = ref(false)
const actingId = ref(null)
const statusFilter = ref('Pending')
const reviewDialog = ref(false)
const reviewMode = ref('approve')
const reviewNote = ref('')
const selected = ref(null)

const statusItems = [
  { title: 'بانتظار الموافقة', value: 'Pending' },
  { title: 'موافق عليها', value: 'Approved' },
  { title: 'مرفوضة', value: 'Rejected' },
  { title: 'الكل', value: '' },
]

const headers = [
  { title: 'إجراء', key: 'actions', sortable: false },
  { title: 'الزبون', key: 'customerName' },
  { title: 'الهاتف', key: 'phoneNumber' },
  { title: 'طلب من', key: 'requestedByName' },
  { title: 'الحالة', key: 'status' },
  { title: 'نسبة الأسبوع %', key: 'paidPercent' },
  { title: 'مدفوع الأسبوع', key: 'weekPaid' },
  { title: 'ملاحظة الطلب', key: 'requestNote' },
  { title: 'تاريخ الطلب', key: 'requestedAt' },
  { title: 'المراجع', key: 'reviewedByName' },
  { title: 'تاريخ المراجعة', key: 'reviewedAt' },
  { title: 'ملاحظة المراجعة', key: 'reviewNote' },
]

const statusLabel = status => {
  if (status === 'Pending') return 'بانتظار'
  if (status === 'Approved') return 'موافق'
  if (status === 'Rejected') return 'مرفوض'

  return status || '-'
}

const statusColor = status => {
  if (status === 'Pending') return 'warning'
  if (status === 'Approved') return 'success'
  if (status === 'Rejected') return 'error'

  return 'primary'
}

const formattedNumber = num => {
  if (num === 0) return '0'
  if (num) return `${num.toLocaleString()} دع`

  return 'لا يوجد'
}

const formattedDateTime = date => {
  if (!date) return 'لا يوجد'

  return new Date(date).toLocaleString('ar-IQ')
}

const totals = computed(() => [
  {
    icon: 'tabler-clock',
    value: statusFilter.value === 'Pending' ? total.value : rows.value.filter(r => r.status === 'Pending').length,
    title: 'بانتظار الموافقة',
    color: 'warning',
    gradient: 'linear-gradient(135deg, #fce38a 0%, #f38181 100%)',
  },
  {
    icon: 'tabler-list',
    value: total.value,
    title: statusFilter.value ? `إجمالي (${statusLabel(statusFilter.value)})` : 'إجمالي الطلبات',
    color: 'primary',
    gradient: 'linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)',
  },
])

async function fetchRows() {
  try {
    loading.value = true
    const authHeader = getAuthHeaders()
    const params = { take: 200, skip: 0 }
    if (statusFilter.value)
      params.status = statusFilter.value

    const { data } = await axios.get(`${apiUrl}CustomerDecisions/LegalConversionRequests`, {
      headers: authHeader,
      params,
    })
    rows.value = data?.items || []
    total.value = data?.total ?? rows.value.length
  }
  catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'تعذر جلب طلبات القانونية')
  }
  finally {
    loading.value = false
  }
}

function openProfile(item) {
  router.push({ name: 'customer-profile', query: { customerID: item.customerId || item.customerID } })
}

function openReview(item, mode) {
  if (!isAdmin.value) {
    toast.warning('الموافقة/الرفض من صلاحية المحاسب الرئيسي فقط')

    return
  }
  selected.value = item
  reviewMode.value = mode
  reviewNote.value = ''
  reviewDialog.value = true
}

async function submitReview() {
  if (!selected.value || !isAdmin.value)
    return

  const id = selected.value.requestId
  const path = reviewMode.value === 'approve' ? 'Approve' : 'Reject'
  try {
    actingId.value = id
    const authHeader = getAuthHeaders()
    await axios.put(
      `${apiUrl}CustomerDecisions/LegalConversionRequests/${id}/${path}`,
      { note: reviewNote.value || null },
      { headers: authHeader },
    )
    reviewDialog.value = false
    toast.success(reviewMode.value === 'approve' ? 'تمت الموافقة وتحويل الزبون لقانونية' : 'تم رفض الطلب')
    await fetchRows()
  }
  catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'فشل تنفيذ المراجعة')
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
      عرض الطلبات فقط. الموافقة والرفض من صلاحية المحاسب الرئيسي.
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
            @click="openReview(item, 'approve')"
          >
            موافقة
          </VBtn>
          <VBtn
            v-if="isAdmin && item.status === 'Pending'"
            size="small"
            color="error"
            variant="tonal"
            :loading="actingId === item.requestId"
            @click="openReview(item, 'reject')"
          >
            رفض
          </VBtn>
        </div>
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

      <template #item.weekPaid="{ item }">
        {{ formattedNumber(item.weekPaid) }}
      </template>

      <template #item.paidPercent="{ item }">
        {{ item.paidPercent != null ? `${item.paidPercent} %` : '-' }}
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
    v-model="reviewDialog"
    max-width="480"
  >
    <VCard>
      <VCardTitle>
        {{ reviewMode === 'approve' ? 'موافقة تحويل قانونية' : 'رفض طلب قانونية' }}
      </VCardTitle>
      <VCardText>
        <div class="mb-3 text-body-1">
          الزبون: <strong>{{ selected?.customerName }}</strong>
        </div>
        <VTextarea
          v-model="reviewNote"
          label="ملاحظة المراجعة (اختياري)"
          rows="3"
        />
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          @click="reviewDialog = false"
        >
          إلغاء
        </VBtn>
        <VBtn
          :color="reviewMode === 'approve' ? 'success' : 'error'"
          :loading="actingId != null"
          @click="submitReview"
        >
          تأكيد
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
