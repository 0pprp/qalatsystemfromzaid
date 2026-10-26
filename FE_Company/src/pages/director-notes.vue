<script setup>
import { onMounted, ref } from 'vue'
import {
  directorNotesGet,
  directorNotesList,
  directorNotesMarkAllRead,
  directorNotesMarkRead,
  directorNotesUnreadCount,
} from '@/composables/directorApi'
import { formatIraqClock, formatIraqDate } from '@/composables/iraqDate'
import { useCities } from '@/composables/useCities'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const { getCityName, fetchCities } = useCities()
const loading = ref(false)
const items = ref([])
const unread = ref(0)
const selected = ref(null)
const detailOpen = ref(false)

const CATALOG_CITY_LABELS = {
  DatabaseCompanyBasra: 'البصرة',
  DatabaseCompanyBasraAlanwar: 'البصرة - الأنوار',
  DatabaseCompanyNajaf: 'النجف',
  DatabaseCompanyNajaf_DEMO: 'النجف - DEMO',
  DatabaseCompanyKarbala: 'كربلاء',
  DatabaseCompanyBaghdadKarkh: 'بغداد - الكرخ',
  DatabaseCompanyKarkh: 'بغداد - الكرخ',
  DatabaseCompanyBaghdadRusafa: 'بغداد - الرصافة',
  DatabaseCompanyRusafa: 'بغداد - الرصافة',
  DatabaseCompanyDiyala: 'ديالى',
  DatabaseCompanyKirkuk: 'كركوك',
  DatabaseCompanyMosul: 'الموصل',
  DatabaseCompanyNineveh: 'نينوى',
  DatabaseCompanyKot: 'واسط',
  DatabaseCompanyWasit: 'واسط',
  DatabaseCompanyMaysan: 'ميسان',
  DatabaseCompanyNasiriyah: 'ذي قار',
  DatabaseCompanyDhiQar: 'ذي قار',
  DatabaseCompanyMuthanna: 'المثنى',
  DatabaseCompanyDiwaniyah: 'الديوانية',
  DatabaseCompanyQadisiyyah: 'الديوانية',
  DatabaseCompanyHilla: 'بابل',
  DatabaseCompanyBabil: 'بابل',
  DatabaseCompanyKarak: 'الكرخ - عقيل',
  DatabaseCompanyKarakAqeel: 'الكرخ - عقيل',
  DatabaseCompanyRusafaAqeel: 'الرصافة - عقيل',
}

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function looksLikeCatalog(value) {
  const v = String(value || '').trim()
  if (!v)
    return true

  return v.startsWith('DatabaseCompany') || v.includes('_DEMO') || v === 'najaf-demo'
}

function provinceLabel(row) {
  const name = pick(row, 'cityName', 'CityName')
  const value = pick(row, 'cityValue', 'CityValue')
  if (name && !looksLikeCatalog(name))
    return name
  const fromCatalog = getCityName(value) || getCityName(name)
  if (fromCatalog)
    return fromCatalog
  const mapped = CATALOG_CITY_LABELS[value] || CATALOG_CITY_LABELS[name]
  if (mapped)
    return mapped

  return name || value || '—'
}

function noteDate(iso) {
  return formatIraqDate(iso) || '—'
}

function noteTime(iso) {
  return formatIraqClock(iso) || '—'
}

function sourceLabel(app) {
  const map = {
    delegate_application: 'مندوب',
    follower_application: 'متابع',
    branch_manager_application: 'مدير فرع',
  }

  return map[app] || app || '—'
}

async function load() {
  loading.value = true
  try {
    await fetchCities().catch(() => null)
    const [list, count] = await Promise.all([
      directorNotesList({ page: 1, pageSize: 50 }),
      directorNotesUnreadCount(),
    ])
    items.value = Array.isArray(list?.items) ? list.items : (Array.isArray(list?.Items) ? list.Items : [])
    unread.value = Number(pick(count, 'unread', 'Unread') ?? 0) || 0
  }
  catch (e) {
    items.value = []
    toast.error(e?.response?.data?.message || 'تعذر تحميل الملاحظات')
  }
  finally {
    loading.value = false
  }
}

async function openRow(row) {
  const id = pick(row, 'id', 'Id')
  selected.value = row
  detailOpen.value = true
  if (!id)
    return
  try {
    const detail = await directorNotesGet(id)
    if (detail)
      selected.value = { ...row, ...detail }
  }
  catch {
    /* keep list preview */
  }
  if (pick(row, 'isUnread', 'IsUnread') || pick(row, 'status', 'Status') === 'Unread') {
    try {
      await directorNotesMarkRead(id)
      row.isUnread = false
      row.status = 'Read'
      if (selected.value)
        selected.value = { ...selected.value, isUnread: false, status: 'Read' }
      if (unread.value > 0)
        unread.value -= 1
    }
    catch {
      /* keep unread badge if mark fails */
    }
  }
}

async function markAll() {
  try {
    await directorNotesMarkAllRead()
    toast.success('تم تعليم الكل كمقروء')
    await load()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || 'تعذر تعليم الكل')
  }
}

onMounted(load)
</script>

<template>
  <div>
    <VCard class="mb-4">
      <VCardTitle class="d-flex align-center justify-space-between flex-wrap gap-2">
        <div class="d-flex align-center gap-2">
          <span>ملاحظات التطبيقات</span>
          <VChip
            v-if="unread"
            color="error"
            size="small"
          >
            غير مقروء: {{ unread }}
          </VChip>
        </div>
        <div class="d-flex gap-2">
          <VBtn
            variant="tonal"
            color="primary"
            :loading="loading"
            @click="load"
          >
            تحديث
          </VBtn>
          <VBtn
            variant="tonal"
            color="secondary"
            :disabled="!unread"
            @click="markAll"
          >
            تعليم الكل كمقروء
          </VBtn>
        </div>
      </VCardTitle>
      <VCardText>
        <VDataTable
          :headers="[
            { title: 'الحالة', key: 'status', width: '90px' },
            { title: 'المرسل', key: 'sender' },
            { title: 'الدور', key: 'role', width: '110px' },
            { title: 'المحافظة', key: 'city' },
            { title: 'الموضوع', key: 'subject' },
            { title: 'التاريخ', key: 'date', width: '120px' },
            { title: 'الوقت', key: 'time', width: '120px' },
          ]"
          :items="items"
          :loading="loading"
          item-value="id"
          class="text-no-wrap"
          @click:row="(_, { item }) => openRow(item)"
        >
          <template #item.status="{ item }">
            <VChip
              size="small"
              :color="(item.isUnread || item.status === 'Unread') ? 'error' : 'success'"
            >
              {{ (item.isUnread || item.status === 'Unread') ? 'جديد' : 'مقروء' }}
            </VChip>
          </template>
          <template #item.sender="{ item }">
            {{ item.senderDisplayName || item.SenderDisplayName || '—' }}
          </template>
          <template #item.role="{ item }">
            {{ item.senderRole || item.SenderRole || sourceLabel(item.sourceApp || item.SourceApp) }}
          </template>
          <template #item.city="{ item }">
            {{ provinceLabel(item) }}
          </template>
          <template #item.subject="{ item }">
            {{ item.subject || item.Subject || item.bodyPreview || '—' }}
          </template>
          <template #item.date="{ item }">
            {{ noteDate(item.createdAtUtc || item.CreatedAtUtc) }}
          </template>
          <template #item.time="{ item }">
            {{ noteTime(item.createdAtUtc || item.CreatedAtUtc) }}
          </template>
          <template #no-data>
            لا توجد ملاحظات بعد
          </template>
        </VDataTable>
      </VCardText>
    </VCard>

    <VDialog
      v-model="detailOpen"
      max-width="560"
    >
      <VCard v-if="selected">
        <VCardTitle>
          {{ selected.subject || selected.Subject || 'ملاحظة' }}
        </VCardTitle>
        <VCardText>
          <div class="mb-2 text-medium-emphasis">
            {{ selected.senderDisplayName || selected.SenderDisplayName }}
            ·
            {{ selected.senderRole || selected.SenderRole }}
            ·
            {{ provinceLabel(selected) }}
          </div>
          <div class="mb-2 text-caption">
            {{ noteDate(selected.createdAtUtc || selected.CreatedAtUtc) }}
            ·
            {{ noteTime(selected.createdAtUtc || selected.CreatedAtUtc) }}
          </div>
          <p style="white-space: pre-wrap; font-family: Cairo, sans-serif;">
            {{ selected.body || selected.Body || selected.bodyPreview || '' }}
          </p>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            color="primary"
            @click="detailOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
