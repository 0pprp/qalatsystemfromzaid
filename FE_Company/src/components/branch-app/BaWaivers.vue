<script setup>
import { computed, ref, watch } from 'vue'
import { asList, branchApp, errorText, fmtDate, money, pick } from '@/composables/branchAppApi'
import { useToast } from '@/composables/useToast'

const props = defineProps({ city: { type: String, required: true } })
const toast = useToast()

const loading = ref(false)
const rows = ref([])
const status = ref('')

const statusItems = [
  { title: 'الكل', value: '' },
  { title: 'قيد الانتظار', value: 'Pending' },
  { title: 'بانتظار المحاسب', value: 'AwaitingAccountant' },
  { title: 'موافق', value: 'Approved' },
  { title: 'مرفوض', value: 'Rejected' },
]

const statusMeta = {
  Pending: ['قيد الانتظار', 'warning'],
  AwaitingAccountant: ['بانتظار المحاسب', 'info'],
  Approved: ['موافق', 'success'],
  Rejected: ['مرفوض', 'error'],
}

const headers = [
  { title: 'الزبون', key: 'name' },
  { title: 'الطالب', key: 'by' },
  { title: 'الحالة', key: 'status' },
  { title: 'المبلغ', key: 'amount', align: 'end' },
  { title: 'الملاحظة', key: 'note' },
  { title: 'تاريخ الطلب', key: 'requested' },
  { title: 'تاريخ القرار', key: 'decided' },
]

const items = computed(() => rows.value.map(r => ({
  name: pick(r, 'customerName', 'CustomerName') || '—',
  by: pick(r, 'branchManagerDisplayName', 'BranchManagerDisplayName', 'branchManagerUserName', 'BranchManagerUserName') || '—',
  status: pick(r, 'status', 'Status') || '',
  amount: pick(r, 'approvedAmount', 'ApprovedAmount'),
  note: pick(r, 'note', 'Note') || '',
  requested: pick(r, 'requestedAtUtc', 'RequestedAtUtc'),
  decided: pick(r, 'decidedAtUtc', 'DecidedAtUtc'),
})))

async function load() {
  loading.value = true
  try {
    rows.value = asList(await branchApp.waivers(props.city, { status: status.value || undefined, pageSize: 200 }))
  }
  catch (e) {
    rows.value = []
    toast.error(errorText(e, 'تعذر تحميل طلبات التنازل'))
  }
  finally {
    loading.value = false
  }
}

watch([() => props.city, status], load, { immediate: true })
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <span class="text-body-2 text-medium-emphasis">
        طلبات التنازل على زبائن الفرع — طلب جديد من تبويب «أسماء قانونية»
      </span>
      <VSpacer />
      <VSelect
        v-model="status"
        :items="statusItems"
        density="compact"
        hide-details
        style="max-inline-size: 200px"
      />
      <VBtn
        variant="tonal"
        icon="tabler-refresh"
        size="small"
        :loading="loading"
        @click="load"
      />
    </div>
    <VDataTable
      :headers="headers"
      :items="items"
      :loading="loading"
      :items-per-page="50"
      density="compact"
      items-per-page-text="عدد الصفوف"
      page-text="{0}-{1} من {2}"
      no-data-text="لا توجد طلبات"
    >
      <template #item.status="{ item }">
        <VChip
          size="small"
          :color="statusMeta[item.status]?.[1] || 'secondary'"
          label
        >
          {{ statusMeta[item.status]?.[0] || item.status || '—' }}
        </VChip>
      </template>
      <template #item.amount="{ item }">
        {{ money(item.amount) }}
      </template>
      <template #item.note="{ item }">
        <span class="ba-wrap">{{ item.note || '—' }}</span>
      </template>
      <template #item.requested="{ item }">
        {{ fmtDate(item.requested, true) }}
      </template>
      <template #item.decided="{ item }">
        {{ fmtDate(item.decided, true) }}
      </template>
    </VDataTable>
  </div>
</template>
