<script setup>
import { onMounted, ref } from 'vue'
import { directorWaiversList } from '@/composables/directorApi'
import { useCities } from '@/composables/useCities'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const { getCityName, fetchCities } = useCities()
const loading = ref(false)
const items = ref([])
const statusFilter = ref('')
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)
const noteDialog = ref(false)
const noteDialogText = ref('')
const noteDialogTitle = ref('')

const statusItems = [
  { title: 'الكل', value: '' },
  { title: 'قيد الانتظار', value: 'Pending' },
  { title: 'بانتظار المحاسب', value: 'AwaitingAccountant' },
  { title: 'موافق', value: 'Approved' },
  { title: 'مرفوض', value: 'Rejected' },
]

const headers = [
  { title: 'الزبون', key: 'customerName' },
  { title: 'المحافظة', key: 'cityName' },
  { title: 'المبلغ', key: 'approvedAmount' },
  { title: 'الحالة', key: 'status' },
  { title: 'ملاحظة', key: 'note' },
  { title: 'تاريخ الطلب', key: 'requestedAtUtc' },
  { title: 'تاريخ القرار', key: 'decidedAtUtc' },
]

const CATALOG_CITY_LABELS = {
  DatabaseCompanyBasra: 'البصرة',
  DatabaseCompanyBasraAlanwar: 'البصرة - الأنوار',
  DatabaseCompanyNajaf: 'النجف',
  DatabaseCompanyNajaf_DEMO: 'النجف - DEMO',
  DatabaseCompanyKarbala: 'كربلاء',
  DatabaseCompanyBaghdadKarkh: 'بغداد - الكرخ',
  DatabaseCompanyKarkh: 'بغداد - الكرخ',
  DatabaseCompanyBaghdadRusafa: 'بغداد - الرصافة',
  DatabaseCompanyBaghdadRosafa: 'بغداد - الرصافة',
  DatabaseCompanyRusafa: 'بغداد - الرصافة',
  DatabaseCompanyDiyala: 'ديالى',
  DatabaseCompanyKirkuk: 'كركوك',
  DatabaseCompanyMosul: 'الموصل',
  DatabaseCompanyNineveh: 'نينوى',
  DatabaseCompanyKot: 'واسط',
  DatabaseCompanyWasit: 'واسط',
  DatabaseCompanyMaysan: 'ميسان',
  DatabaseCompanyNasiriyah: 'ذي قار',
  DatabaseCompanyNasria: 'ذي قار',
  DatabaseCompanyDhiQar: 'ذي قار',
  DatabaseCompanyMuthanna: 'المثنى',
  DatabaseCompanyDiwaniyah: 'الديوانية',
  DatabaseCompanyQadisiyyah: 'الديوانية',
  DatabaseCompanyHilla: 'بابل',
  DatabaseCompanyBabil: 'بابل',
  DatabaseCompanyKarak: 'الكرخ - عقيل',
  DatabaseCompanyBaghdadKarak: 'الكرخ - عقيل',
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

function cityLabel(row) {
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

  return (name && !looksLikeCatalog(name) ? name : null) || value || '—'
}

function noteText(row) {
  return String(pick(row, 'note', 'Note', 'decisionNote', 'DecisionNote') || '').trim()
}

function openNote(row) {
  const text = noteText(row)
  if (!text)
    return
  noteDialogTitle.value = pick(row, 'customerName', 'CustomerName') || 'الملاحظة'
  noteDialogText.value = text
  noteDialog.value = true
}

function statusLabel(s) {
  if (s === 'Pending') return 'قيد الانتظار'
  if (s === 'AwaitingAccountant') return 'بانتظار المحاسب'
  if (s === 'Approved') return 'موافق'
  if (s === 'Rejected') return 'مرفوض'

  return s || '—'
}

function statusColor(s) {
  if (s === 'Pending') return 'warning'
  if (s === 'AwaitingAccountant') return 'info'
  if (s === 'Approved') return 'success'
  if (s === 'Rejected') return 'error'

  return 'secondary'
}

function money(n) {
  if (n == null || n === '') return '—'

  return `${Number(n).toLocaleString('ar-IQ')} د.ع`
}

function fmt(v) {
  if (!v) return '—'

  return new Date(v).toLocaleString('ar-IQ')
}

async function load() {
  try {
    loading.value = true
    const data = await directorWaiversList({
      status: statusFilter.value || undefined,
      page: page.value,
      pageSize: pageSize.value,
    })
    items.value = data?.items || []
    total.value = data?.total ?? items.value.length
  }
  catch (e) {
    console.error(e)
    toast.error(e.response?.data?.message || 'تعذر تحميل التنازلات')
    items.value = []
  }
  finally {
    loading.value = false
  }
}

onMounted(async () => {
  await fetchCities().catch(() => {})
  await load()
})
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between ga-3 mb-4">
      <div>
        <h4 class="text-h5 mb-1">
          التنازلات
        </h4>
        <p class="text-medium-emphasis mb-0">
          كل طلبات التنازل بكل الحالات (عرض فقط)
        </p>
      </div>
      <div class="d-flex ga-2 align-center">
        <VSelect
          v-model="statusFilter"
          :items="statusItems"
          item-title="title"
          item-value="value"
          label="الحالة"
          density="compact"
          style="min-inline-size: 200px"
          @update:model-value="() => { page = 1; load() }"
        />
        <VBtn
          color="primary"
          :loading="loading"
          prepend-icon="tabler-refresh"
          @click="load"
        >
          تحديث
        </VBtn>
      </div>
    </div>

    <VCard>
      <VDataTable
        :headers="headers"
        :items="items"
        :loading="loading"
        :items-per-page="pageSize"
      >
        <template #item.cityName="{ item }">
          {{ cityLabel(item) }}
        </template>
        <template #item.approvedAmount="{ item }">
          {{ money(item.approvedAmount) }}
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
        <template #item.note="{ item }">
          <button
            v-if="noteText(item)"
            type="button"
            class="text-start text-body-2 text-primary text-decoration-underline"
            style="white-space: normal; max-inline-size: 360px; background: none; border: 0; cursor: pointer; padding: 0"
            @click="openNote(item)"
          >
            {{ noteText(item) }}
          </button>
          <span v-else>—</span>
        </template>
        <template #item.requestedAtUtc="{ item }">
          {{ fmt(item.requestedAtUtc) }}
        </template>
        <template #item.decidedAtUtc="{ item }">
          {{ fmt(item.decidedAtUtc) }}
        </template>
      </VDataTable>
    </VCard>

    <VDialog
      v-model="noteDialog"
      max-width="640"
    >
      <VCard>
        <VCardTitle class="d-flex align-center">
          {{ noteDialogTitle }}
          <VSpacer />
          <VBtn
            icon
            variant="text"
            @click="noteDialog = false"
          >
            <VIcon icon="tabler-x" />
          </VBtn>
        </VCardTitle>
        <VCardText
          class="text-body-1"
          style="white-space: pre-wrap; line-height: 1.7"
        >
          {{ noteDialogText }}
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            color="primary"
            @click="noteDialog = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
