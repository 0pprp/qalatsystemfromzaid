<script setup>
import { onMounted, ref } from 'vue'
import { smErrorMessage, smGet, smPost, smPut } from '@/composables/salesManagerApi'
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
    appKey: '',
    titleAr: '',
    videoUrl: '',
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

async function load() {
  loading.value = true
  try {
    const data = await smGet('tutorial-links')
    rows.value = Array.isArray(data) ? data : []
  }
  catch (err) {
    rows.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل روابط الفيديوهات'))
  }
  finally {
    loading.value = false
  }
}

function openEdit(row) {
  isNew.value = false
  form.value = {
    appKey: pick(row, 'appKey', 'AppKey') || '',
    titleAr: pick(row, 'titleAr', 'TitleAr') || '',
    videoUrl: pick(row, 'videoUrl', 'VideoUrl') || '',
    isEnabled: pick(row, 'isEnabled', 'IsEnabled') !== false,
  }
  dialog.value = true
}

function openAdd() {
  isNew.value = true
  form.value = emptyForm()
  dialog.value = true
}

async function save() {
  const appKey = String(form.value.appKey || '').trim()
  const titleAr = String(form.value.titleAr || '').trim()
  const videoUrl = String(form.value.videoUrl || '').trim()
  if (!appKey) {
    toast.error('مفتاح التطبيق مطلوب (مثل delegate)')

    return
  }
  if (!titleAr) {
    toast.error('اسم التطبيق مطلوب')

    return
  }
  if (form.value.isEnabled && !videoUrl) {
    toast.error('رابط الفيديو مطلوب عند التفعيل')

    return
  }

  saving.value = true
  try {
    const body = {
      appKey,
      titleAr,
      videoUrl,
      isEnabled: !!form.value.isEnabled,
    }
    if (isNew.value)
      await smPost('tutorial-links', body)
    else
      await smPut(`tutorial-links/${encodeURIComponent(appKey)}`, body)

    toast.success('تم حفظ رابط الفيديو')
    dialog.value = false
    await load()
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر حفظ الرابط'))
  }
  finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="mb-1">
          روابط الفيديوهات التعليمية
        </h4>
        <p class="text-medium-emphasis mb-0">
          حدّث رابط فيديو كل تطبيق من هنا — التطبيقات تقرأ الرابط مباشرة عند الضغط على زر الفيديو.
        </p>
      </div>
      <div class="d-flex gap-2">
        <VBtn
          variant="tonal"
          :loading="loading"
          @click="load"
        >
          تحديث
        </VBtn>
        <VBtn
          color="primary"
          @click="openAdd"
        >
          إضافة تطبيق
        </VBtn>
      </div>
    </div>

    <VCard>
      <VDataTable
        :headers="[
          { title: 'التطبيق', key: 'titleAr' },
          { title: 'المفتاح', key: 'appKey' },
          { title: 'الرابط', key: 'videoUrl' },
          { title: 'مفعّل', key: 'isEnabled' },
          { title: '', key: 'actions', sortable: false, align: 'end' },
        ]"
        :items="rows"
        :loading="loading"
        item-value="appKey"
        class="text-no-wrap"
        density="comfortable"
      >
        <template #item.titleAr="{ item }">
          <strong>{{ pick(item, 'titleAr', 'TitleAr') || '—' }}</strong>
        </template>
        <template #item.appKey="{ item }">
          <code>{{ pick(item, 'appKey', 'AppKey') }}</code>
        </template>
        <template #item.videoUrl="{ item }">
          <span
            class="text-truncate d-inline-block"
            style="max-width: 340px;"
            :title="pick(item, 'videoUrl', 'VideoUrl') || ''"
          >
            {{ pick(item, 'videoUrl', 'VideoUrl') || '—' }}
          </span>
        </template>
        <template #item.isEnabled="{ item }">
          <VChip
            size="small"
            :color="pick(item, 'isEnabled', 'IsEnabled') ? 'success' : 'secondary'"
            variant="tonal"
          >
            {{ pick(item, 'isEnabled', 'IsEnabled') ? 'مفعّل' : 'متوقف' }}
          </VChip>
        </template>
        <template #item.actions="{ item }">
          <VBtn
            size="small"
            variant="tonal"
            color="primary"
            @click="openEdit(item)"
          >
            تعديل
          </VBtn>
        </template>
        <template #no-data>
          <div class="pa-6 text-center text-medium-emphasis">
            لا توجد روابط بعد — أضف تطبيقاً أو عدّل الصفوف الافتراضية.
          </div>
        </template>
      </VDataTable>
    </VCard>

    <VDialog
      v-model="dialog"
      max-width="560"
    >
      <VCard>
        <VCardTitle>
          {{ isNew ? 'إضافة رابط فيديو لتطبيق' : 'تعديل رابط الفيديو' }}
        </VCardTitle>
        <VCardText>
          <VTextField
            v-model="form.appKey"
            class="mb-3"
            label="مفتاح التطبيق *"
            hint="مثال: delegate / follower / sales-employee"
            persistent-hint
            :disabled="!isNew"
            variant="outlined"
            density="comfortable"
          />
          <VTextField
            v-model="form.titleAr"
            class="mb-3"
            label="اسم التطبيق *"
            variant="outlined"
            density="comfortable"
          />
          <VTextField
            v-model="form.videoUrl"
            class="mb-3"
            label="رابط الفيديو"
            hint="رابط YouTube أو أي رابط https"
            persistent-hint
            variant="outlined"
            density="comfortable"
          />
          <VSwitch
            v-model="form.isEnabled"
            color="primary"
            label="مفعّل للتطبيقات"
            hide-details
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
  </div>
</template>
