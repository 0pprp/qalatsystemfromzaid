<script setup>
import { onMounted, ref } from 'vue'
import { resolveAccountantTutorialVideoUrl } from '@/composables/accountantTutorialVideoApi'

const loading = ref(true)
const videoUrl = ref('')
const opened = ref(false)

function openVideo() {
  const url = String(videoUrl.value || '').trim()
  if (!url)
    return
  window.open(url, '_blank', 'noopener,noreferrer')
  opened.value = true
}

onMounted(async () => {
  loading.value = true
  try {
    videoUrl.value = await resolveAccountantTutorialVideoUrl()
    if (videoUrl.value)
      openVideo()
  }
  finally {
    loading.value = false
  }
})
</script>

<template>
  <VCard class="pa-6 tutorial-card">
    <div class="text-center">
      <VAvatar
        color="primary"
        variant="tonal"
        size="72"
        class="mb-4"
      >
        <VIcon
          icon="tabler-video"
          size="40"
        />
      </VAvatar>

      <h1 class="text-h5 font-weight-bold mb-2">
        مشاهدة الفيديو التعليمي
      </h1>

      <div
        v-if="loading"
        class="py-8"
      >
        <VProgressCircular
          indeterminate
          color="primary"
        />
      </div>

      <template v-else-if="videoUrl">
        <p class="text-body-1 text-medium-emphasis mb-6">
          {{ opened ? 'تم فتح رابط الفيديو في تبويب جديد.' : 'اضغط لفتح رابط الفيديو التعليمي.' }}
        </p>
        <VBtn
          color="primary"
          prepend-icon="tabler-external-link"
          size="large"
          @click="openVideo"
        >
          فتح الفيديو
        </VBtn>
      </template>

      <template v-else>
        <VAlert
          type="info"
          variant="tonal"
          class="mt-4 text-body-1"
          density="comfortable"
        >
          لا يوجد رابط فيديو حاليا
        </VAlert>
      </template>
    </div>
  </VCard>
</template>

<style scoped>
.tutorial-card {
  margin-block: 1.5rem;
  margin-inline: auto;
  max-inline-size: 560px;
}
</style>
