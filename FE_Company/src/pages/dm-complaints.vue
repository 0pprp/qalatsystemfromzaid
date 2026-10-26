<script setup>
import { computed, onMounted, ref } from 'vue'
import {
  dmComplaintDetail,
  dmComplaintMarkRead,
  dmComplaints,
  dmComplaintsMarkAllRead,
  dmComplaintsUnreadCount,
} from '@/composables/delegatedManagerApi'
import { useToast } from '@/composables/useToast'

defineOptions({ name: 'DmComplaints' })
definePage({ meta: { layout: 'dm' } })

const toast = useToast()
const busy = ref(false)
const items = ref([])
const unread = ref(0)
const unreadOnly = ref(false)
const detail = ref(null)
const detailOpen = ref(false)

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] != null && obj[k] !== '')
      return obj[k]
  }
  return undefined
}

function fmtDate(v) {
  if (!v) return '—'
  const d = new Date(v)
  return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleString('en-GB')
}

async function load() {
  busy.value = true
  try {
    const [list, count] = await Promise.all([
      dmComplaints({ page: 1, pageSize: 50, unreadOnly: unreadOnly.value || undefined }),
      dmComplaintsUnreadCount(),
    ])
    items.value = pick(list, 'items', 'Items') || []
    unread.value = Number(pick(count, 'unread', 'Unread') || 0)
  }
  catch (e) {
    items.value = []
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل الشكاوى')
  }
  finally {
    busy.value = false
  }
}

async function openRow(row) {
  const id = pick(row, 'id', 'Id')
  if (!id) return
  try {
    detail.value = await dmComplaintDetail(id)
    detailOpen.value = true
    if (!pick(row, 'readAtUtc', 'ReadAtUtc')) {
      await dmComplaintMarkRead(id)
      await load()
    }
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر فتح الشكوى')
  }
}

async function markAll() {
  try {
    await dmComplaintsMarkAllRead()
    toast.success('تم تعليم الكل كمقروء')
    await load()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر التعليم')
  }
}

const empty = computed(() => !busy.value && !items.value.length)
onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-4">
      <div>
        <h1 class="text-h5 mb-1">
          الرسائل والشكاوى
        </h1>
        <p class="text-medium-emphasis mb-0">
          غير مقروء: {{ unread }}
        </p>
      </div>
      <div class="d-flex flex-wrap gap-2">
        <VSwitch
          v-model="unreadOnly"
          label="غير المقروء فقط"
          hide-details
          density="compact"
          @update:model-value="load"
        />
        <VBtn
          variant="tonal"
          @click="markAll"
        >
          تعليم الكل مقروء
        </VBtn>
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
      v-if="empty"
      type="info"
      variant="tonal"
    >
      لا رسائل
    </VAlert>

    <VCard
      v-for="row in items"
      :key="pick(row, 'id', 'Id')"
      variant="outlined"
      class="mb-2 dm-row"
      :class="{ 'dm-row--unread': !pick(row, 'readAtUtc', 'ReadAtUtc') }"
      @click="openRow(row)"
    >
      <VCardText class="d-flex justify-space-between gap-3">
        <div>
          <div class="font-weight-bold">
            {{ pick(row, 'subject', 'Subject') || pick(row, 'title', 'Title') || 'شكوى' }}
          </div>
          <div class="text-caption text-medium-emphasis">
            {{ pick(row, 'cityName', 'CityName') || '—' }}
            ·
            {{ pick(row, 'sourceApp', 'SourceApp') || '—' }}
            ·
            {{ fmtDate(pick(row, 'createdAtUtc', 'CreatedAtUtc')) }}
          </div>
        </div>
        <VChip
          v-if="!pick(row, 'readAtUtc', 'ReadAtUtc')"
          size="x-small"
          color="primary"
        >
          جديد
        </VChip>
      </VCardText>
    </VCard>

    <VDialog
      v-model="detailOpen"
      max-width="640"
    >
      <VCard v-if="detail">
        <VCardTitle>
          {{ pick(detail, 'subject', 'Subject') || pick(detail, 'title', 'Title') || 'تفاصيل' }}
        </VCardTitle>
        <VCardText>
          <p class="text-body-2 white-space-pre-wrap">
            {{ pick(detail, 'body', 'Body') || pick(detail, 'message', 'Message') || '—' }}
          </p>
          <div class="text-caption text-medium-emphasis mt-3">
            {{ pick(detail, 'fromDisplayName', 'FromDisplayName') || '—' }}
            ·
            {{ fmtDate(pick(detail, 'createdAtUtc', 'CreatedAtUtc')) }}
          </div>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="detailOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.dm-row { cursor: pointer; }
.dm-row:hover { border-color: rgb(var(--v-theme-primary)); }
.dm-row--unread { border-inline-start: 3px solid rgb(var(--v-theme-primary)); }
.white-space-pre-wrap { white-space: pre-wrap; }
</style>
