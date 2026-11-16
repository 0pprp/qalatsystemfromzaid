<script setup>
import { onMounted, ref } from 'vue'
import {
  paDateTime,
  paDeleteMaterial,
  paErrorMessage,
  paListMaterials,
  paRenameMaterial,
  paSize,
  paUploadMaterial,
} from '@/composables/promotersAppApi'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()
const items = ref([])
const loading = ref(false)
const listError = ref('')

async function load() {
  loading.value = true
  listError.value = ''
  try {
    items.value = await paListMaterials()
  }
  catch (err) {
    listError.value = paErrorMessage(err, 'تعذر تحميل المواد')
  }
  finally {
    loading.value = false
  }
}

// ---------------------------------------------------------------- upload (several files, one by one)
const fileInput = ref(null)
const queue = ref([]) // { name, progress, error, done }
const uploading = ref(false)
const uploadTitle = ref('')

function pickFiles() {
  fileInput.value?.click()
}

async function onFiles(e) {
  const files = Array.from(e.target.files || [])

  e.target.value = ''
  if (!files.length)
    return
  uploading.value = true
  queue.value = files.map(f => ({ name: f.name, progress: 0, error: '', done: false }))
  for (const [i, f] of files.entries()) {
    const q = queue.value[i]
    if (f.size > 300 * 1024 * 1024) {
      q.error = 'أكبر من 300 ميغابايت'
      continue
    }
    try {
      await paUploadMaterial(f, files.length === 1 ? uploadTitle.value : '', p => { q.progress = p })
      q.done = true
    }
    catch (err) {
      q.error = paErrorMessage(err, 'فشل الرفع')
    }
  }
  uploading.value = false
  uploadTitle.value = ''

  const ok = queue.value.filter(q => q.done).length
  if (ok)
    toast.success(`تم رفع ${ok} ملف`)
  if (queue.value.every(q => q.done))
    queue.value = []
  load()
}

// ---------------------------------------------------------------- rename / delete / preview
const renameItem = ref(null)
const renameValue = ref('')

function startRename(m) {
  renameItem.value = m
  renameValue.value = m.title
}

async function saveRename() {
  try {
    await paRenameMaterial(renameItem.value.id, renameValue.value)
    renameItem.value.title = renameValue.value.trim()
    renameItem.value = null
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
}

const deleteItem = ref(null)

async function confirmDelete() {
  try {
    await paDeleteMaterial(deleteItem.value.id)
    items.value = items.value.filter(i => i.id !== deleteItem.value.id)
    deleteItem.value = null
    toast.success('تم حذف المادة من التطبيق')
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
}

const preview = ref(null)

const KIND = {
  image: { icon: 'tabler-photo', title: 'صورة', color: 'info' },
  video: { icon: 'tabler-movie', title: 'فيديو', color: 'error' },
  pdf: { icon: 'tabler-file-type-pdf', title: 'PDF', color: 'warning' },
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="text-h4 mb-1 d-flex align-center gap-2">
          <VIcon
            icon="tabler-photo-video"
            color="primary"
          />
          المواد التسويقية
        </h4>
        <p class="text-medium-emphasis mb-0">
          صور وفيديوهات وملفات PDF يشاهدها المروّجون في التطبيق ويحمّلونها ويشاركونها. الحد الأقصى 300 ميغابايت للملف.
        </p>
      </div>
      <div class="d-flex gap-2 align-center">
        <VTextField
          v-model="uploadTitle"
          label="العنوان (لملف واحد، اختياري)"
          density="compact"
          hide-details
          style="min-width: 240px;"
          :disabled="uploading"
        />
        <VBtn
          color="primary"
          prepend-icon="tabler-upload"
          :loading="uploading"
          @click="pickFiles"
        >
          رفع ملفات
        </VBtn>
        <input
          ref="fileInput"
          type="file"
          multiple
          accept="image/jpeg,image/png,image/webp,image/gif,video/mp4,video/quicktime,video/webm,video/3gpp,application/pdf,.m4v"
          hidden
          @change="onFiles"
        >
      </div>
    </div>

    <VCard
      v-if="queue.length"
      class="mb-4 pa-3"
    >
      <div
        v-for="(q, i) in queue"
        :key="i"
        class="mb-2"
      >
        <div class="d-flex justify-space-between text-body-2">
          <span>{{ q.name }}</span>
          <span
            v-if="q.error"
            class="text-error"
          >{{ q.error }}</span>
          <span
            v-else-if="q.done"
            class="text-success"
          >تم</span>
          <span v-else>{{ q.progress }}%</span>
        </div>
        <VProgressLinear
          :model-value="q.done ? 100 : q.progress"
          :color="q.error ? 'error' : 'primary'"
          height="6"
          rounded
        />
      </div>
    </VCard>

    <VAlert
      v-if="listError"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ listError }}
    </VAlert>

    <VProgressLinear
      v-if="loading"
      indeterminate
      class="mb-2"
    />

    <VCard
      v-if="!loading && !items.length"
      class="pa-10 text-center text-medium-emphasis"
    >
      <VIcon
        icon="tabler-photo-up"
        size="48"
        class="mb-2"
      />
      <div>لا توجد مواد بعد — ارفع أول صورة أو فيديو أو ملف PDF</div>
    </VCard>

    <VRow>
      <VCol
        v-for="m in items"
        :key="m.id"
        cols="12"
        sm="6"
        md="4"
        lg="3"
      >
        <VCard class="h-100 d-flex flex-column">
          <div
            class="pa-thumb cursor-pointer"
            @click="preview = m"
          >
            <VImg
              v-if="m.kind === 'image'"
              :src="m.url"
              cover
              height="160"
            />
            <video
              v-else-if="m.kind === 'video'"
              :src="`${m.url}#t=0.5`"
              preload="metadata"
              muted
              class="pa-video"
            />
            <div
              v-else
              class="d-flex align-center justify-center h-100"
            >
              <VIcon
                icon="tabler-file-type-pdf"
                size="64"
                color="warning"
              />
            </div>
            <VChip
              :color="KIND[m.kind]?.color"
              size="x-small"
              class="pa-kind"
              :prepend-icon="KIND[m.kind]?.icon"
            >
              {{ KIND[m.kind]?.title }}
            </VChip>
          </div>
          <VCardText class="flex-grow-1 pb-1">
            <div class="font-weight-medium text-truncate">
              {{ m.title }}
            </div>
            <div class="text-caption text-medium-emphasis">
              {{ paSize(m.sizeBytes) }} · {{ paDateTime(m.createdAt) }}
            </div>
          </VCardText>
          <VCardActions class="pt-0">
            <IconBtn
              :href="m.url"
              target="_blank"
              rel="noopener noreferrer"
            >
              <VIcon icon="tabler-external-link" />
            </IconBtn>
            <IconBtn @click="startRename(m)">
              <VIcon icon="tabler-edit" />
            </IconBtn>
            <VSpacer />
            <IconBtn
              color="error"
              @click="deleteItem = m"
            >
              <VIcon icon="tabler-trash" />
            </IconBtn>
          </VCardActions>
        </VCard>
      </VCol>
    </VRow>

    <VDialog
      :model-value="!!preview"
      max-width="900"
      @update:model-value="v => { if (!v) preview = null }"
    >
      <VCard
        v-if="preview"
        :title="preview.title"
      >
        <VCardText class="text-center">
          <img
            v-if="preview.kind === 'image'"
            :src="preview.url"
            style="max-width: 100%; max-height: 70vh;"
          >
          <video
            v-else-if="preview.kind === 'video'"
            :src="preview.url"
            controls
            autoplay
            style="max-width: 100%; max-height: 70vh;"
          />
          <iframe
            v-else
            :src="preview.url"
            style="width: 100%; height: 70vh; border: 0;"
          />
        </VCardText>
      </VCard>
    </VDialog>

    <VDialog
      :model-value="!!renameItem"
      max-width="420"
      @update:model-value="v => { if (!v) renameItem = null }"
    >
      <VCard title="تعديل العنوان">
        <VCardText>
          <VTextField
            v-model="renameValue"
            label="العنوان"
            autofocus
            @keydown.enter="saveRename"
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="renameItem = null"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            @click="saveRename"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      :model-value="!!deleteItem"
      max-width="400"
      @update:model-value="v => { if (!v) deleteItem = null }"
    >
      <VCard title="حذف المادة">
        <VCardText>ستختفي «{{ deleteItem?.title }}» من التطبيق.</VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="deleteItem = null"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="error"
            @click="confirmDelete"
          >
            حذف
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.pa-thumb {
  position: relative;
  height: 160px;
  background: rgba(var(--v-theme-on-surface), 0.05);
}

.pa-video {
  width: 100%;
  height: 160px;
  object-fit: cover;
  display: block;
}

.pa-kind {
  position: absolute;
  top: 8px;
  inset-inline-start: 8px;
}
</style>
