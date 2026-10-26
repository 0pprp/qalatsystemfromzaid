<script setup>
import { onMounted, ref } from 'vue'
import { dmWaiverDecide, dmWaiverDetail, dmWaivers } from '@/composables/delegatedManagerApi'
import { useToast } from '@/composables/useToast'

defineOptions({ name: 'DmWaivers' })
definePage({ meta: { layout: 'dm' } })

const toast = useToast()
const busy = ref(false)
const items = ref([])
const status = ref('Pending')
const detailOpen = ref(false)
const detail = ref(null)
const decideNote = ref('')
const approvedAmount = ref(null)
const decideBusy = ref(false)

const statusItems = [
  { title: 'معلّق', value: 'Pending' },
  { title: 'موافق', value: 'Approved' },
  { title: 'مرفوض', value: 'Rejected' },
]

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] != null && obj[k] !== '')
      return obj[k]
  }
  return undefined
}

async function load() {
  busy.value = true
  try {
    const data = await dmWaivers({ page: 1, pageSize: 50, status: status.value || undefined })
    items.value = pick(data, 'items', 'Items') || []
  }
  catch (e) {
    items.value = []
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل التنازلات')
  }
  finally {
    busy.value = false
  }
}

async function openRow(row) {
  const id = pick(row, 'id', 'Id')
  if (!id) return
  try {
    detail.value = await dmWaiverDetail(id)
    detailOpen.value = true
    decideNote.value = ''
    approvedAmount.value = null
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر فتح التفاصيل')
  }
}

async function decide(decision) {
  const req = pick(detail.value, 'request', 'Request') || detail.value
  const id = pick(req, 'id', 'Id')
  if (!id) return
  decideBusy.value = true
  try {
    await dmWaiverDecide(id, {
      decision,
      note: decideNote.value,
      approvedAmount: decision === 'Approved' ? approvedAmount.value : undefined,
    })
    toast.success(decision === 'Approved' ? 'تمت الموافقة' : 'تم الرفض')
    detailOpen.value = false
    await load()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر حفظ القرار')
  }
  finally {
    decideBusy.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-4">
      <h1 class="text-h5 mb-0">
        طلبات التنازل
      </h1>
      <div class="d-flex gap-2">
        <VSelect
          v-model="status"
          :items="statusItems"
          item-title="title"
          item-value="value"
          density="compact"
          hide-details
          style="min-inline-size: 140px"
          @update:model-value="load"
        />
        <VBtn
          variant="tonal"
          :loading="busy"
          @click="load"
        >
          تحديث
        </VBtn>
      </div>
    </div>

    <VProgressLinear
      v-if="busy"
      indeterminate
      class="mb-3"
    />
    <VAlert
      v-if="!busy && !items.length"
      type="info"
      variant="tonal"
    >
      لا طلبات تنازل
    </VAlert>

    <VCard
      v-for="row in items"
      :key="pick(row, 'id', 'Id')"
      variant="outlined"
      class="mb-2 dm-row"
      @click="openRow(row)"
    >
      <VCardText>
        <div class="d-flex justify-space-between gap-2 flex-wrap">
          <strong>{{ pick(row, 'customerName', 'CustomerName') || 'زبون' }}</strong>
          <VChip
            size="small"
            variant="tonal"
          >
            {{ pick(row, 'status', 'Status') || '—' }}
          </VChip>
        </div>
        <div class="text-caption text-medium-emphasis mt-1">
          {{ pick(row, 'cityName', 'CityName') || '—' }}
          ·
          المبلغ: {{ pick(row, 'requestedAmount', 'RequestedAmount') ?? pick(row, 'amount', 'Amount') ?? '—' }}
        </div>
      </VCardText>
    </VCard>

    <VDialog
      v-model="detailOpen"
      max-width="640"
    >
      <VCard v-if="detail">
        <VCardTitle>تفاصيل التنازل</VCardTitle>
        <VCardText>
          <div class="mb-2">
            <strong>{{ pick(pick(detail, 'request', 'Request') || detail, 'customerName', 'CustomerName') }}</strong>
          </div>
          <div class="text-body-2 mb-3">
            {{ pick(pick(detail, 'request', 'Request') || detail, 'note', 'Note')
              || pick(pick(detail, 'request', 'Request') || detail, 'reason', 'Reason')
              || '—' }}
          </div>
          <template v-if="String(pick(pick(detail, 'request', 'Request') || detail, 'status', 'Status') || '').toLowerCase() === 'pending'">
            <VTextField
              v-model="approvedAmount"
              type="number"
              label="المبلغ المعتمد (عند الموافقة)"
              class="mb-2"
            />
            <VTextarea
              v-model="decideNote"
              label="ملاحظة"
              rows="2"
            />
          </template>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="detailOpen = false"
          >
            إغلاق
          </VBtn>
          <template v-if="String(pick(pick(detail, 'request', 'Request') || detail, 'status', 'Status') || '').toLowerCase() === 'pending'">
            <VBtn
              color="error"
              variant="tonal"
              :loading="decideBusy"
              @click="decide('Rejected')"
            >
              رفض
            </VBtn>
            <VBtn
              color="success"
              :loading="decideBusy"
              @click="decide('Approved')"
            >
              موافقة
            </VBtn>
          </template>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.dm-row { cursor: pointer; }
.dm-row:hover { border-color: rgb(var(--v-theme-primary)); }
</style>
