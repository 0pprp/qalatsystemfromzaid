<script setup>
import { computed, onMounted, ref } from 'vue'
import { smDelete, smErrorMessage, smGet, smPost, smPut } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const loading = ref(false)
const saving = ref(false)
const rows = ref([])
const apps = ref([])
const dialog = ref(false)
const isNew = ref(false)
const form = ref(emptyForm())

function emptyForm() {
  return {
    id: null,
    appKey: 'delegate',
    questionAr: '',
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

const appTitleMap = computed(() => {
  const map = {}
  for (const a of apps.value) {
    const key = pick(a, 'appKey', 'AppKey')
    const title = pick(a, 'titleAr', 'TitleAr')
    if (key)
      map[key] = title || key
  }

  return map
})

function appTitle(appKey) {
  return appTitleMap.value[appKey] || appKey || '—'
}

async function load() {
  loading.value = true
  try {
    const [list, appList] = await Promise.all([
      smGet('tutorial-videos'),
      smGet('tutorial-videos/apps'),
    ])
    rows.value = Array.isArray(list) ? list : []
    apps.value = Array.isArray(appList) ? appList : []
  }
  catch (err) {
    rows.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل الفيديوهات التعليمية'))
  }
  finally {
    loading.value = false
  }
}

function openAdd() {
  isNew.value = true
  form.value = emptyForm()
  if (apps.value.length)
    form.value.appKey = pick(apps.value[0], 'appKey', 'AppKey') || 'delegate'
  dialog.value = true
}

function openEdit(row) {
  isNew.value = false
  form.value = {
    id: pick(row, 'id', 'Id'),
    appKey: pick(row, 'appKey', 'AppKey') || 'delegate',
    questionAr: pick(row, 'questionAr', 'QuestionAr') || '',
    videoUrl: pick(row, 'videoUrl', 'VideoUrl') || '',
    isEnabled: pick(row, 'isEnabled', 'IsEnabled') !== false,
  }
  dialog.value = true
}

async function save() {
  const appKey = String(form.value.appKey || '').trim()
  const questionAr = String(form.value.questionAr || '').trim()
  const videoUrl = String(form.value.videoUrl || '').trim()
  if (!appKey) {
    toast.error('اختر التطبيق')

    return
  }
  if (!questionAr) {
    toast.error('السؤال مطلوب')

    return
  }
  if (!videoUrl) {
    toast.error('رابط الفيديو مطلوب')

    return
  }

  saving.value = true
  try {
    const body = {
      appKey,
      questionAr,
      videoUrl,
      isEnabled: !!form.value.isEnabled,
    }
    if (isNew.value)
      await smPost('tutorial-videos', body)
    else
      await smPut(`tutorial-videos/${form.value.id}`, body)

    toast.success(isNew.value ? 'تمت إضافة الفيديو' : 'تم حفظ التعديل')
    dialog.value = false
    await load()
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر حفظ الفيديو'))
  }
  finally {
    saving.value = false
  }
}

async function remove(row) {
  const id = pick(row, 'id', 'Id')
  const question = pick(row, 'questionAr', 'QuestionAr') || ''
  if (!id)
    return
  if (!confirm(`حذف السؤال: ${question}؟`))
    return
  try {
    await smDelete(`tutorial-videos/${id}`)
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
          الفيديوهات التعليمية
        </h4>
        <p class="text-medium-emphasis mb-0">
          أضف سؤالاً ورابط فيديو لكل تطبيق — يظهر كقائمة تساؤلات داخل التطبيق.
        </p>
      </div>
      <!-- في RTL العنصر الأخير يظهر يسار الصفحة -->
      <VBtn
        color="success"
        prepend-icon="tabler-plus"
        @click="openAdd"
      >
        إضافة فيديو
      </VBtn>
    </div>

    <VCard>
      <VDataTable
        :headers="[
          { title: 'التطبيق', key: 'appKey' },
          { title: 'السؤال', key: 'questionAr' },
          { title: 'رابط الفيديو', key: 'videoUrl' },
          { title: 'مفعّل', key: 'isEnabled' },
          { title: '', key: 'actions', sortable: false, align: 'end' },
        ]"
        :items="rows"
        :loading="loading"
        item-value="id"
        class="text-no-wrap"
        density="comfortable"
      >
        <template #item.appKey="{ item }">
          <strong>{{ appTitle(pick(item, 'appKey', 'AppKey')) }}</strong>
        </template>
        <template #item.questionAr="{ item }">
          {{ pick(item, 'questionAr', 'QuestionAr') || '—' }}
        </template>
        <template #item.videoUrl="{ item }">
          <span
            class="text-truncate d-inline-block"
            style="max-width: 280px;"
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
          <div class="d-flex gap-2 justify-end">
            <VBtn
              size="small"
              variant="tonal"
              color="primary"
              @click="openEdit(item)"
            >
              تعديل
            </VBtn>
            <VBtn
              size="small"
              variant="tonal"
              color="error"
              @click="remove(item)"
            >
              حذف
            </VBtn>
          </div>
        </template>
        <template #no-data>
          <div class="pa-6 text-center text-medium-emphasis">
            لا توجد فيديوهات بعد — اضغط «إضافة فيديو».
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
          {{ isNew ? 'إضافة فيديو' : 'تعديل فيديو' }}
        </VCardTitle>
        <VCardText>
          <VSelect
            v-model="form.appKey"
            class="mb-3"
            label="التطبيق *"
            :items="apps.map(a => ({
              title: pick(a, 'titleAr', 'TitleAr'),
              value: pick(a, 'appKey', 'AppKey'),
            }))"
            item-title="title"
            item-value="value"
            variant="outlined"
            density="comfortable"
          />
          <VTextField
            v-model="form.questionAr"
            class="mb-3"
            label="السؤال *"
            hint="يظهر كزر داخل التطبيق — مثال: كيفية إرسال التسديدات"
            persistent-hint
            variant="outlined"
            density="comfortable"
          />
          <VTextField
            v-model="form.videoUrl"
            class="mb-3"
            label="رابط الفيديو *"
            hint="رابط YouTube أو أي رابط https"
            persistent-hint
            variant="outlined"
            density="comfortable"
          />
          <VSwitch
            v-model="form.isEnabled"
            color="success"
            label="مفعّل للتطبيق"
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
            color="success"
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
