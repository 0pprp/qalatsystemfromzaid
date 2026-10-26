<script setup>
import { onMounted } from 'vue'
import { useDmExceptionsInbox } from '@/composables/useDmExceptionsInbox'

defineOptions({ name: 'DmExceptions' })
definePage({ meta: { layout: 'dm' } })

const {
  busy, status, visible, detailOpen, detail, decideNote, decideBusy,
  pick, load, openRow, decide,
} = useDmExceptionsInbox({ wrongSaleMode: false })

const statusItems = [
  { title: 'معلّق', value: 'Pending' },
  { title: 'موافق', value: 'Approved' },
  { title: 'مرفوض', value: 'Rejected' },
  { title: 'ملغى', value: 'Cancelled' },
]

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-4">
      <h1 class="text-h5 mb-0">
        طلبات الاستثناء
      </h1>
      <div class="d-flex gap-2 flex-wrap">
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
      v-if="!busy && !visible.length"
      type="info"
      variant="tonal"
    >
      لا طلبات
    </VAlert>

    <VCard
      v-for="row in visible"
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
          {{ pick(row, 'reason', 'Reason') || '—' }}
        </div>
      </VCardText>
    </VCard>

    <VDialog
      v-model="detailOpen"
      max-width="720"
    >
      <VCard v-if="detail">
        <VCardTitle>تفاصيل الاستثناء</VCardTitle>
        <VCardText>
          <div class="mb-2">
            <strong>{{ pick(pick(detail, 'request', 'Request') || detail, 'customerName', 'CustomerName') }}</strong>
          </div>
          <div class="text-body-2 mb-3">
            {{ pick(pick(detail, 'request', 'Request') || detail, 'reason', 'Reason') || '—' }}
          </div>
          <VTextarea
            v-if="String(pick(pick(detail, 'request', 'Request') || detail, 'status', 'Status') || '').toLowerCase() === 'pending'"
            v-model="decideNote"
            label="ملاحظة القرار"
            rows="2"
            class="mb-2"
          />
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
