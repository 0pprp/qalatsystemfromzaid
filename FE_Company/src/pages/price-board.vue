<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import axios from 'axios'
import { computed, onMounted, ref, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'
import {
  directorCities,
  directorPriceBoardCreate,
  directorPriceBoardDelete,
  directorPriceBoardList,
  directorPriceBoardUpdate,
} from '@/composables/directorApi'

definePage({
  meta: {
    layout: 'default',
  },
})

const apiUrl = localStorage.getItem('LinkCity')
const toast = useToast()
const { isAdmin, isMonitor } = useUserRole()
const canWrite = computed(() => isMonitor.value)

const rows = ref([])
const loading = ref(false)
const cities = ref([])
const citiesLoading = ref(false)
const filterCity = ref('')
const dialog = ref(false)
const saving = ref(false)
const editingId = ref(null)
const form = ref({
  productName: '',
  salePrice: '',
  dailyInstallment: '',
  downPayment: '',
  targetCityValue: '',
  targetCityName: '',
})

/** فارزة كل 3 مراتب أثناء الكتابة */
function formatGroupedInput(raw) {
  const digits = String(raw ?? '').replace(/[^\d]/g, '')
  if (!digits)
    return ''

  return digits.replace(/\B(?=(\d{3})+(?!\d))/g, ',')
}

function parseGroupedInput(raw) {
  const digits = String(raw ?? '').replace(/[^\d]/g, '')
  if (!digits)
    return 0

  return Number(digits) || 0
}

function onMoneyInput(field, value) {
  form.value[field] = formatGroupedInput(value)
}

const cityItems = computed(() =>
  (cities.value || []).map(c => ({
    title: c.name || c.Name || c.value,
    value: String(c.value || c.Value || ''),
  })).filter(i => i.value),
)

const headers = computed(() => {
  const base = [
    { title: 'المنتج', key: 'productName' },
    { title: 'سعر البيع', key: 'salePrice' },
    { title: 'القسط اليومي', key: 'dailyInstallment' },
    { title: 'المقدمة', key: 'downPayment' },
    { title: 'المحافظة', key: 'targetCityName' },
    { title: 'آخر تحديث', key: 'updatedAtUtc' },
  ]
  if (canWrite.value)
    base.unshift({ title: 'إجراء', key: 'actions', sortable: false })

  return base
})

const money = n => {
  if (n == null || n === '') return '—'

  return `${Number(n).toLocaleString()} دع`
}

const fmtDate = raw => {
  if (!raw) return '—'
  const d = new Date(raw)
  if (Number.isNaN(d.getTime())) return String(raw)

  return d.toLocaleString('ar-IQ')
}

async function loadCities() {
  if (!isMonitor.value)
    return
  citiesLoading.value = true
  try {
    const raw = await directorCities()
    cities.value = Array.isArray(raw) ? raw : []
  }
  catch {
    cities.value = []
  }
  finally {
    citiesLoading.value = false
  }
}

async function fetchRows() {
  loading.value = true
  try {
    if (isMonitor.value) {
      const data = await directorPriceBoardList(filterCity.value || undefined)
      rows.value = Array.isArray(data) ? data : []
    }
    else if (isAdmin.value) {
      const { data } = await axios.get(`${apiUrl}accountant/price-board`, {
        headers: getAuthHeaders(),
        timeout: 60000,
      })
      rows.value = Array.isArray(data) ? data : []
    }
    else {
      rows.value = []
    }
  }
  catch (e) {
    rows.value = []
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل لوحة الأسعار')
  }
  finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  form.value = {
    productName: '',
    salePrice: '',
    dailyInstallment: '',
    downPayment: '',
    targetCityValue: filterCity.value || '',
    targetCityName: '',
  }
  syncCityName()
  dialog.value = true
}

function openEdit(row) {
  editingId.value = row.id
  form.value = {
    productName: row.productName || '',
    salePrice: formatGroupedInput(row.salePrice),
    dailyInstallment: formatGroupedInput(row.dailyInstallment),
    downPayment: formatGroupedInput(row.downPayment),
    targetCityValue: row.targetCityValue || '',
    targetCityName: row.targetCityName || '',
  }
  dialog.value = true
}

function syncCityName() {
  const hit = cityItems.value.find(c => c.value === form.value.targetCityValue)
  form.value.targetCityName = hit?.title || form.value.targetCityName || ''
}

async function save() {
  if (!canWrite.value)
    return
  syncCityName()
  saving.value = true
  try {
    const body = {
      productName: form.value.productName,
      salePrice: parseGroupedInput(form.value.salePrice),
      dailyInstallment: parseGroupedInput(form.value.dailyInstallment),
      downPayment: parseGroupedInput(form.value.downPayment),
      targetCityValue: form.value.targetCityValue,
      targetCityName: form.value.targetCityName,
    }
    if (editingId.value)
      await directorPriceBoardUpdate(editingId.value, body)
    else
      await directorPriceBoardCreate(body)
    dialog.value = false
    toast.success(editingId.value ? 'تم التعديل' : 'تمت الإضافة')
    await fetchRows()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر الحفظ')
  }
  finally {
    saving.value = false
  }
}

async function removeRow(row) {
  if (!canWrite.value || !confirm(`حذف «${row.productName}»؟`))
    return
  try {
    await directorPriceBoardDelete(row.id)
    toast.success('تم الحذف')
    await fetchRows()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر الحذف')
  }
}

watch(filterCity, () => {
  if (isMonitor.value)
    fetchRows()
})

onMounted(async () => {
  await loadCities()
  await fetchRows()
})
</script>

<template>
  <VCard class="pa-6">
    <div class="d-flex flex-wrap align-center justify-space-between ga-3 mb-4">
      <div>
        <div class="text-h6">
          لوحة الأسعار
        </div>
        <div class="text-body-2 text-medium-emphasis">
          <template v-if="canWrite">
            إضافة وتعديل وحذف إعلانات الأسعار حسب محافظة الهدف
          </template>
          <template v-else>
            أسعار محافظة فرعك (عرض فقط)
          </template>
        </div>
      </div>
      <div class="d-flex flex-wrap ga-2">
        <VSelect
          v-if="isMonitor"
          v-model="filterCity"
          :items="[{ title: 'كل المحافظات', value: '' }, ...cityItems]"
          :loading="citiesLoading"
          label="تصفية المحافظة"
          item-title="title"
          item-value="value"
          style="min-inline-size: 220px"
          density="comfortable"
          clearable
        />
        <VBtn
          color="primary"
          prepend-icon="tabler-refresh"
          :loading="loading"
          @click="fetchRows"
        >
          تحديث
        </VBtn>
        <VBtn
          v-if="canWrite"
          color="success"
          prepend-icon="tabler-plus"
          @click="openCreate"
        >
          إضافة
        </VBtn>
      </div>
    </div>

    <VDataTable
      :headers="headers"
      :items="rows"
      :loading="loading"
      class="text-no-wrap"
      items-per-page="25"
    >
      <template
        v-if="canWrite"
        #item.actions="{ item }"
      >
        <div class="d-flex ga-1">
          <IconBtn
            size="small"
            @click="openEdit(item)"
          >
            <VIcon icon="tabler-edit" />
          </IconBtn>
          <IconBtn
            size="small"
            color="error"
            @click="removeRow(item)"
          >
            <VIcon icon="tabler-trash" />
          </IconBtn>
        </div>
      </template>
      <template #item.salePrice="{ item }">
        {{ money(item.salePrice) }}
      </template>
      <template #item.dailyInstallment="{ item }">
        {{ money(item.dailyInstallment) }}
      </template>
      <template #item.downPayment="{ item }">
        {{ money(item.downPayment) }}
      </template>
      <template #item.targetCityName="{ item }">
        {{ item.targetCityName || item.targetCityValue || '—' }}
      </template>
      <template #item.updatedAtUtc="{ item }">
        {{ fmtDate(item.updatedAtUtc) }}
      </template>
    </VDataTable>
  </VCard>

  <VDialog
    v-model="dialog"
    max-width="560"
  >
    <VCard class="pa-4">
      <VCardTitle>{{ editingId ? 'تعديل إعلان' : 'إضافة إعلان' }}</VCardTitle>
      <VCardText>
        <VTextField
          v-model="form.productName"
          class="mb-3"
          label="اسم المنتج"
        />
        <VTextField
          :model-value="form.salePrice"
          class="mb-3"
          inputmode="numeric"
          label="سعر البيع"
          hint="مثال: 1,000,000"
          persistent-hint
          @update:model-value="v => onMoneyInput('salePrice', v)"
        />
        <VTextField
          :model-value="form.dailyInstallment"
          class="mb-3"
          inputmode="numeric"
          label="القسط اليومي"
          hint="مثال: 25,000"
          persistent-hint
          @update:model-value="v => onMoneyInput('dailyInstallment', v)"
        />
        <VTextField
          :model-value="form.downPayment"
          class="mb-3"
          inputmode="numeric"
          label="المقدمة"
          hint="مثال: 100,000"
          persistent-hint
          @update:model-value="v => onMoneyInput('downPayment', v)"
        />
        <VSelect
          v-model="form.targetCityValue"
          :items="cityItems"
          :loading="citiesLoading"
          label="محافظة الهدف"
          item-title="title"
          item-value="value"
          @update:model-value="syncCityName"
        />
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          @click="dialog = false"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="primary"
          :loading="saving"
          @click="save"
        >
          حفظ
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
