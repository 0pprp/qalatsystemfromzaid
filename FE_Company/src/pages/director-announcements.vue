<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import {
  smDelete,
  smErrorMessage,
  smGet,
  smPost,
  smPostForm,
  smPut,
} from '@/composables/salesManagerApi'
import { salesGatewayBase } from '@/composables/useSalesBranches'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const loading = ref(false)
const saving = ref(false)
const uploading = ref(false)
const rows = ref([])
const dialog = ref(false)
const isNew = ref(false)
const bannerFile = ref(null)
const zoomFile = ref(null)
const form = ref(emptyForm())

function emptyForm() {
  return {
    id: null,
    appKey: 'delegate',
    contentType: 'text',
    textBody: '',
    imageFileName: '',
    imageUrl: '',
    zoomImageFileName: '',
    zoomImageUrl: '',
    hasLink: false,
    linkUrl: '',
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

const appTitleMap = {
  delegate: 'تطبيق المندوب',
  follower: 'تطبيق مسؤول اللجنة',
}

function appTitle(appKey) {
  return appTitleMap[appKey] || appKey || '—'
}

function mediaAbsolute(urlOrPath) {
  const raw = String(urlOrPath || '').trim()
  if (!raw)
    return ''
  if (/^https?:\/\//i.test(raw))
    return raw
  const gw = String(salesGatewayBase() || '').replace(/\/+$/, '')
  if (raw.startsWith('/api/')) {
    const root = gw.replace(/\/api\/?$/, '')

    return `${root}${raw}`
  }

  return `${gw}/${raw.replace(/^\/+/, '')}`
}

const previewBannerUrl = computed(() => mediaAbsolute(form.value.imageUrl))
const previewZoomUrl = computed(() => mediaAbsolute(form.value.zoomImageUrl))

watch(() => form.value.contentType, type => {
  if (type === 'text') {
    bannerFile.value = null
    zoomFile.value = null
  }
})

watch(() => form.value.hasLink, on => {
  if (!on)
    form.value.linkUrl = ''
})

async function load() {
  loading.value = true
  try {
    const list = await smGet('announcements')
    rows.value = Array.isArray(list) ? list : []
  }
  catch (err) {
    rows.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل الإعلانات'))
  }
  finally {
    loading.value = false
  }
}

function openAdd() {
  isNew.value = true
  form.value = emptyForm()
  bannerFile.value = null
  zoomFile.value = null
  dialog.value = true
}

function openEdit(row) {
  isNew.value = false
  form.value = {
    id: pick(row, 'id', 'Id'),
    appKey: pick(row, 'appKey', 'AppKey') || 'delegate',
    contentType: (pick(row, 'contentType', 'ContentType') || 'text').toLowerCase(),
    textBody: pick(row, 'textBody', 'TextBody') || '',
    imageFileName: pick(row, 'imageFileName', 'ImageFileName') || '',
    imageUrl: pick(row, 'imageUrl', 'ImageUrl') || '',
    zoomImageFileName: pick(row, 'zoomImageFileName', 'ZoomImageFileName') || '',
    zoomImageUrl: pick(row, 'zoomImageUrl', 'ZoomImageUrl') || '',
    hasLink: !!pick(row, 'hasLink', 'HasLink'),
    linkUrl: pick(row, 'linkUrl', 'LinkUrl') || '',
    isEnabled: pick(row, 'isEnabled', 'IsEnabled') !== false,
  }
  bannerFile.value = null
  zoomFile.value = null
  dialog.value = true
}

function onBannerChange(files) {
  const f = Array.isArray(files) ? files[0] : files
  bannerFile.value = f || null
}

function onZoomChange(files) {
  const f = Array.isArray(files) ? files[0] : files
  zoomFile.value = f || null
}

async function uploadOne(file, fallbackName) {
  const fd = new FormData()
  const fileName = (file && file.name) ? file.name : fallbackName
  fd.append('file', file, fileName)
  const res = await smPostForm('announcements/upload-image', fd)
  const name = pick(res, 'imageFileName', 'ImageFileName') || ''
  const url = pick(res, 'imageUrl', 'ImageUrl') || ''
  if (!name)
    throw new Error('لم يُرجع السيرفر اسم الملف')

  return { name, url }
}

async function ensureImagesUploaded() {
  if (form.value.contentType !== 'image') {
    return {
      banner: form.value.imageFileName || '',
      zoom: form.value.zoomImageFileName || '',
    }
  }

  uploading.value = true
  try {
    let banner = form.value.imageFileName || ''
    let zoom = form.value.zoomImageFileName || ''

    if (bannerFile.value) {
      const up = await uploadOne(bannerFile.value, 'banner.png')
      banner = up.name
      form.value.imageFileName = up.name
      form.value.imageUrl = up.url
    }
    if (zoomFile.value) {
      const up = await uploadOne(zoomFile.value, 'zoom.png')
      zoom = up.name
      form.value.zoomImageFileName = up.name
      form.value.zoomImageUrl = up.url
    }

    return { banner, zoom }
  }
  catch (err) {
    throw new Error(smErrorMessage(err, 'تعذر رفع صورة الإعلان'))
  }
  finally {
    uploading.value = false
  }
}

async function save() {
  const appKey = String(form.value.appKey || '').trim()
  const contentType = String(form.value.contentType || '').trim()
  if (!appKey) {
    toast.error('اختر التطبيق')

    return
  }
  if (contentType === 'text' && !String(form.value.textBody || '').trim()) {
    toast.error('أدخل نص الإعلان')

    return
  }
  if (contentType === 'image' && !bannerFile.value && !form.value.imageFileName) {
    toast.error('اختر صورة لوحة الإعلان')

    return
  }
  if (contentType === 'image' && !zoomFile.value && !form.value.zoomImageFileName) {
    toast.error('اختر صورة التكبير')

    return
  }
  if (form.value.hasLink && !String(form.value.linkUrl || '').trim()) {
    toast.error('أدخل الرابط')

    return
  }

  saving.value = true
  try {
    const images = await ensureImagesUploaded()
    const body = {
      appKey,
      contentType,
      textBody: contentType === 'text' ? String(form.value.textBody || '').trim() : '',
      imageFileName: contentType === 'image' ? images.banner : '',
      zoomImageFileName: contentType === 'image' ? images.zoom : '',
      hasLink: !!form.value.hasLink,
      linkUrl: form.value.hasLink ? String(form.value.linkUrl || '').trim() : '',
      isEnabled: !!form.value.isEnabled,
    }
    if (isNew.value)
      await smPost('announcements', body)
    else
      await smPut(`announcements/${form.value.id}`, body)

    toast.success(isNew.value ? 'تمت إضافة الإعلان' : 'تم حفظ التعديل')
    dialog.value = false
    await load()
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر حفظ الإعلان'))
  }
  finally {
    saving.value = false
  }
}

async function remove(row) {
  const id = pick(row, 'id', 'Id')
  if (!id)
    return
  if (!confirm('حذف هذا الإعلان؟'))
    return
  try {
    await smDelete(`announcements/${id}`)
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
          الإعلانات
        </h4>
        <p class="text-medium-emphasis mb-0">
          انشر نصاً أو صورتين (لوحة + تكبير) لتطبيق المندوب أو المتابع — تدور كل 3 ثوانٍ إن وُجد أكثر من إعلان.
        </p>
      </div>
      <VBtn
        color="success"
        prepend-icon="tabler-plus"
        @click="openAdd"
      >
        إضافة إعلان
      </VBtn>
    </div>

    <VCard>
      <VDataTable
        :headers="[
          { title: 'التطبيق', key: 'appKey' },
          { title: 'النوع', key: 'contentType' },
          { title: 'المحتوى', key: 'preview' },
          { title: 'رابط', key: 'hasLink' },
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
        <template #item.contentType="{ item }">
          {{ (pick(item, 'contentType', 'ContentType') || '') === 'image' ? 'صورة' : 'نص' }}
        </template>
        <template #item.preview="{ item }">
          <div
            v-if="(pick(item, 'contentType', 'ContentType') || '') === 'image'"
            class="d-flex align-center gap-2"
          >
            <VImg
              v-if="pick(item, 'imageUrl', 'ImageUrl')"
              :src="mediaAbsolute(pick(item, 'imageUrl', 'ImageUrl'))"
              width="64"
              height="40"
              cover
              class="rounded"
            />
            <span class="text-medium-emphasis">لوحة + تكبير</span>
          </div>
          <span
            v-else
            class="text-truncate d-inline-block"
            style="max-width: 280px"
          >{{ pick(item, 'textBody', 'TextBody') || '—' }}</span>
        </template>
        <template #item.hasLink="{ item }">
          <VChip
            size="small"
            :color="pick(item, 'hasLink', 'HasLink') ? 'primary' : 'default'"
            variant="tonal"
          >
            {{ pick(item, 'hasLink', 'HasLink') ? 'نعم' : 'لا' }}
          </VChip>
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
      max-width="600"
      persistent
    >
      <VCard>
        <VCardTitle class="d-flex align-center justify-space-between">
          <span>{{ isNew ? 'إضافة إعلان' : 'تعديل إعلان' }}</span>
          <VBtn
            icon
            variant="text"
            @click="dialog = false"
          >
            <VIcon icon="tabler-x" />
          </VBtn>
        </VCardTitle>
        <VCardText class="d-flex flex-column gap-4">
          <VSelect
            v-model="form.appKey"
            label="التطبيق"
            :items="[
              { title: 'مندوب', value: 'delegate' },
              { title: 'مسؤول اللجنة', value: 'follower' },
            ]"
          />

          <div>
            <div class="text-body-2 mb-2">
              نوع المحتوى
            </div>
            <div class="d-flex flex-wrap gap-4">
              <VCheckbox
                :model-value="form.contentType === 'text'"
                label="نص"
                hide-details
                @update:model-value="v => { if (v) form.contentType = 'text' }"
              />
              <VCheckbox
                :model-value="form.contentType === 'image'"
                label="صورة"
                hide-details
                @update:model-value="v => { if (v) form.contentType = 'image' }"
              />
            </div>
          </div>

          <VTextarea
            v-if="form.contentType === 'text'"
            v-model="form.textBody"
            label="نص الإعلان"
            rows="4"
            auto-grow
          />

          <div
            v-else
            class="d-flex flex-column gap-4"
          >
            <div>
              <VFileInput
                label="صورة لوحة الإعلان (الشريط في الرئيسية)"
                accept="image/jpeg,image/png,image/webp,image/gif"
                prepend-icon="tabler-photo"
                show-size
                hint="المفضل: 1200×560 — حوالي 200–400 كيلوبايت"
                persistent-hint
                @update:model-value="onBannerChange"
              />
              <VImg
                v-if="previewBannerUrl && !bannerFile"
                :src="previewBannerUrl"
                max-height="120"
                class="rounded mt-3"
                cover
              />
            </div>
            <div>
              <VFileInput
                label="صورة التكبير (عند الضغط — إطار 80%)"
                accept="image/jpeg,image/png,image/webp,image/gif"
                prepend-icon="tabler-zoom-in"
                show-size
                hint="المفضل: 1080×1920 — حوالي 400 كيلوبايت إلى 1.5 ميغابايت"
                persistent-hint
                @update:model-value="onZoomChange"
              />
              <VImg
                v-if="previewZoomUrl && !zoomFile"
                :src="previewZoomUrl"
                max-height="180"
                class="rounded mt-3"
                cover
              />
            </div>
          </div>

          <VCheckbox
            v-model="form.hasLink"
            label="يحتوي رابط نقل لمكان آخر؟"
            hide-details
          />
          <VTextField
            v-if="form.hasLink"
            v-model="form.linkUrl"
            label="أدخل الرابط هنا"
            placeholder="https://..."
            dir="ltr"
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
            :loading="saving || uploading"
            @click="save"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
