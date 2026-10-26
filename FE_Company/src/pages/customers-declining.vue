<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import ModernStatCard from '@/components/ModernStatCard.vue'
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const apiUrl = localStorage.getItem('LinkCity')
const router = useRouter()
const toast = useToast()

const rows = ref([])
const loading = ref(false)

const formattedDate = date =>
  date ? new Date(date).toLocaleDateString('en-CA') : 'لا يوجد'

const headers = [
  { title: 'اسم العميل', key: 'customerName' },
  { title: 'رقم الهاتف', key: 'phoneNumber' },
  { title: 'القائمة', key: 'delegateName' },
  { title: 'من', key: 'fromLabel' },
  { title: 'إلى', key: 'toLabel' },
  { title: 'الأيام', key: 'daysBetween' },
  { title: 'تاريخ الممتاز', key: 'priorExcellentDate' },
  { title: 'تاريخ الحالي', key: 'currentCaptureDate' },
  { title: 'الملف', key: 'actions', sortable: false },
]

const totals = computed(() => [
  {
    icon: 'tabler-trending-down',
    value: rows.value.length,
    title: 'عدد الزبائن المتدنية',
    color: 'error',
    gradient: 'linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)',
  },
])

async function fetchDeclining() {
  try {
    loading.value = true
    const base = String(apiUrl || '').trim()
    if (!base) {
      toast.error('لم تُحدَّد المحافظة — اختر محافظة من تسجيل الدخول ثم أعد المحاولة')

      return
    }
    const authHeader = getAuthHeaders()
    const response = await axios.get(
      `${base}branch-manager/customers/declining`,
      { headers: authHeader, params: { refresh: true } },
    )
    rows.value = Array.isArray(response.data) ? response.data : []
  }
  catch (error) {
    console.error(error)
    const status = error.response?.status
    const msg = error.response?.data?.message
    if (status === 403)
      toast.error(msg || 'غير مصرح بعرض الزبائن المتدنية')
    else
      toast.error(msg || 'تعذر جلب الزبائن المتدنية')
  }
  finally {
    loading.value = false
  }
}

function openProfile(item) {
  const id = item.customerId ?? item.CustomerId ?? item.customerID
  if (!id) return
  router.push({ name: 'customer-profile', query: { customerID: id } })
}

onMounted(() => {
  fetchDeclining()
})
</script>

<template>
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
    <VRow class="mb-4">
      <VCol cols="12">
        <div class="text-h6 mb-2">
          الزبائن المتدنية — كان التقييم ممتاز ثم أصبح سيء أو متلكئ خلال 15 يوماً
        </div>
        <VBtn
          color="primary"
          :loading="loading"
          prepend-icon="tabler-refresh"
          @click="fetchDeclining"
        >
          تحديث
        </VBtn>
      </VCol>
    </VRow>

    <VDataTable
      :headers="headers"
      :items="rows"
      :loading="loading"
      :items-per-page="50"
      items-per-page-text="عدد السجل"
      class="text-no-wrap custom-data-table"
    >
      <template #item.customerName="{ item }">
        <div>{{ item.customerName || 'لا يوجد' }}</div>
      </template>
      <template #item.phoneNumber="{ item }">
        <div>{{ item.phoneNumber || 'لا يوجد' }}</div>
      </template>
      <template #item.delegateName="{ item }">
        <div>{{ item.delegateName || 'لا يوجد' }}</div>
      </template>
      <template #item.fromLabel="{ item }">
        <VChip
          color="success"
          size="small"
          label
        >
          {{ item.fromLabel || 'ممتاز' }}
        </VChip>
      </template>
      <template #item.toLabel="{ item }">
        <VChip
          color="error"
          size="small"
          label
        >
          {{ item.toLabel }}
        </VChip>
      </template>
      <template #item.priorExcellentDate="{ item }">
        <div>{{ formattedDate(item.priorExcellentDate) }}</div>
      </template>
      <template #item.currentCaptureDate="{ item }">
        <div>{{ formattedDate(item.currentCaptureDate) }}</div>
      </template>
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
    </VDataTable>
  </VCard>
</template>
