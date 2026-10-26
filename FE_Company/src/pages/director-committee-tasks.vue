<script setup>
import { onMounted, ref } from 'vue'
import { smDelete, smErrorMessage, smGet, smPost, smPut } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const loading = ref(false)
const saving = ref(false)
const rows = ref([])
const dialog = ref(false)
const isNew = ref(false)
const form = ref(emptyForm())

function emptyForm() {
  return {
    id: null,
    body: '',
    isEnabled: true,
  }
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

function preview(text) {
  const t = String(text || '').trim().replace(/\s+/g, ' ')
  if (!t)
    return '—'
  return t.length > 80 ? `${t.slice(0, 80)}…` : t
}

async function load() {
  loading.value = true
  try {
    const list = await smGet('committee-tasks')
    rows.value = Array.isArray(list) ? list : []
  }
  catch (err) {
    rows.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل مهام اللجنة'))
  }
  finally {
    loading.value = false
  }
}

function openAdd() {
  isNew.value = true
  form.value = emptyForm()
  dialog.value = true
}

function openEdit(row) {
  isNew.value = false
  form.value = {
    id: pick(row, 'id', 'Id'),
    body: pick(row, 'body', 'Body') || '',
    isEnabled: pick(row, 'isEnabled', 'IsEnabled') !== false,
  }
  dialog.value = true
}

async function save() {
  const body = String(form.value.body || '').trim()
  if (!body) {
    toast.error('أدخل نص المهمة')

    return
  }

  saving.value = true
  try {
    const payload = {
      body,
      isEnabled: !!form.value.isEnabled,
    }
    if (isNew.value)
      await smPost('committee-tasks', payload)
    else
      await smPut(`committee-tasks/${form.value.id}`, payload)

    toast.success(isNew.value ? 'تمت إضافة المهمة' : 'تم حفظ التعديل')
    dialog.value = false
    await load()
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر حفظ المهمة'))
  }
  finally {
    saving.value = false
  }
}

async function remove(row) {
  const id = pick(row, 'id', 'Id')
  if (!id)
    return
  if (!confirm('حذف هذه المهمة؟'))
    return
  try {
    await smDelete(`committee-tasks/${id}`)
    toast.success('تم الحذف')
    await load()
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر الحذف'))
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="mb-1">
          مهام اللجنة
        </h4>
        <p class="text-medium-emphasis mb-0">
          نصوص طويلة تظهر ككروت صغيرة في تطبيق مسؤول اللجنة — عند الضغط يُعرض النص كاملاً.
        </p>
      </div>
      <VBtn
        color="success"
        prepend-icon="tabler-plus"
        @click="openAdd"
      >
        إضافة مهمة
      </VBtn>
    </div>

    <VCard>
      <VDataTable
        :headers="[
          { title: 'المعاينة', key: 'preview' },
          { title: 'مفعّل', key: 'isEnabled' },
          { title: '', key: 'actions', sortable: false, align: 'end' },
        ]"
        :items="rows"
        :loading="loading"
        item-value="id"
        density="comfortable"
      >
        <template #item.preview="{ item }">
          <span class="text-wrap">{{ preview(pick(item, 'body', 'Body')) }}</span>
        </template>
        <template #item.isEnabled="{ item }">
          <VChip
            size="small"
            :color="pick(item, 'isEnabled', 'IsEnabled') !== false ? 'success' : 'secondary'"
            variant="tonal"
          >
            {{ pick(item, 'isEnabled', 'IsEnabled') !== false ? 'نعم' : 'لا' }}
          </VChip>
        </template>
        <template #item.actions="{ item }">
          <VBtn
            icon
            variant="text"
            color="primary"
            @click="openEdit(item)"
          >
            <VIcon icon="tabler-edit" />
          </VBtn>
          <VBtn
            icon
            variant="text"
            color="error"
            @click="remove(item)"
          >
            <VIcon icon="tabler-trash" />
          </VBtn>
        </template>
      </VDataTable>
    </VCard>

    <VDialog
      v-model="dialog"
      max-width="640"
      persistent
    >
      <VCard>
        <VCardTitle class="d-flex align-center justify-space-between">
          <span>{{ isNew ? 'إضافة مهمة' : 'تعديل مهمة' }}</span>
          <VBtn
            icon
            variant="text"
            @click="dialog = false"
          >
            <VIcon icon="tabler-x" />
          </VBtn>
        </VCardTitle>
        <VCardText class="d-flex flex-column gap-4">
          <VTextarea
            v-model="form.body"
            label="نص المهمة (حر / طويل)"
            rows="10"
            auto-grow
            counter="8000"
          />
          <VCheckbox
            v-model="form.isEnabled"
            label="مفعّل (يظهر في التطبيق)"
            hide-details
          />
        </VCardText>
        <VCardActions class="justify-end pa-4">
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
  </div>
</template>
