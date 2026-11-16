<template>
  <div class="gallery">
    <div class="stage soft-card">
      <template v-if="current">
        <v-img v-if="current.type === 'image'" :src="current.url" :alt="alt" aspect-ratio="1" contain class="bg-white" />
        <video
          v-else-if="current.type === 'video'"
          :key="current.url"
          :src="current.url"
          controls
          playsinline
          preload="metadata"
          class="media-video"
        />
        <div v-else-if="current.type === 'youtube'" class="yt-wrap">
          <iframe
            v-if="embedUrl"
            :src="embedUrl"
            title="فيديو المنتج"
            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
            allowfullscreen
            loading="lazy"
          />
          <div v-else class="d-flex align-center justify-center h-100 text-medium-emphasis">رابط الفيديو غير صالح</div>
        </div>
      </template>
      <div v-else class="d-flex align-center justify-center empty"><v-icon size="64" color="grey">mdi-image-off-outline</v-icon></div>
      <template v-if="media.length > 1">
        <v-btn class="nav prev" icon="mdi-chevron-right" size="small" variant="elevated" aria-label="السابق" @click="go(-1)" />
        <v-btn class="nav next" icon="mdi-chevron-left" size="small" variant="elevated" aria-label="التالي" @click="go(1)" />
      </template>
    </div>
    <div v-if="media.length > 1" class="thumbs hide-scrollbar mt-3">
      <button
        v-for="(m, i) in media"
        :key="i"
        class="thumb"
        :class="{ active: i === index }"
        type="button"
        :aria-label="`عرض الوسائط ${i + 1}`"
        @click="index = i"
      >
        <img v-if="m.type === 'image'" :src="m.url" alt="" />
        <img v-else-if="m.type === 'youtube' && ytThumb(m.url)" :src="ytThumb(m.url)" alt="" />
        <div v-else class="vid-thumb"><v-icon color="white">mdi-video</v-icon></div>
        <v-icon v-if="m.type !== 'image'" class="play" color="white" size="22">mdi-play-circle</v-icon>
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { youtubeEmbedUrl, youtubeThumb } from '@/utils/youtube'

const props = defineProps({ media: { type: Array, default: () => [] }, alt: { type: String, default: '' } })
const index = ref(0)
watch(() => props.media, () => (index.value = 0))
const current = computed(() => props.media[index.value])
const embedUrl = computed(() => (current.value?.type === 'youtube' ? youtubeEmbedUrl(current.value.url) : null))
const ytThumb = youtubeThumb
function go(d) {
  const n = props.media.length
  index.value = (index.value + d + n) % n
}
</script>

<style scoped>
.stage { position: relative; border-radius: 20px; overflow: hidden; background: #fff; aspect-ratio: 1; }
.empty { aspect-ratio: 1; background: #f3f5f5; }
.media-video { width: 100%; height: 100%; background: #000; display: block; object-fit: contain; }
.yt-wrap { position: absolute; inset: 0; background: #000; }
.yt-wrap iframe { width: 100%; height: 100%; border: 0; }
.nav { position: absolute; top: 50%; transform: translateY(-50%); opacity: 0.9; }
.nav.prev { right: 10px; }
.nav.next { left: 10px; }
.thumbs { display: flex; gap: 8px; overflow-x: auto; padding-bottom: 2px; }
.thumb {
  position: relative; flex: 0 0 72px; height: 72px; border-radius: 12px; overflow: hidden;
  border: 2px solid transparent; background: #eef2f2; cursor: pointer; padding: 0;
}
.thumb.active { border-color: #006a64; }
.thumb img { width: 100%; height: 100%; object-fit: cover; display: block; }
.vid-thumb { width: 100%; height: 100%; background: linear-gradient(135deg, #004d48, #2e9c8f); display: flex; align-items: center; justify-content: center; }
.play { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); filter: drop-shadow(0 1px 3px rgba(0, 0, 0, 0.6)); }
</style>
