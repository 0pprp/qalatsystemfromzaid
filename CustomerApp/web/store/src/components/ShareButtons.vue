<template>
  <div class="d-flex flex-wrap align-center ga-2">
    <span v-if="label" class="text-body-2 text-medium-emphasis me-1">{{ label }}</span>
    <v-btn v-if="canNativeShare" size="small" variant="tonal" color="primary" prepend-icon="mdi-share-variant" @click="nativeShare">مشاركة</v-btn>
    <v-btn size="small" icon variant="tonal" color="primary" :aria-label="copied ? 'تم النسخ' : 'نسخ الرابط'" @click="copy">
      <v-icon>{{ copied ? 'mdi-check' : 'mdi-link-variant' }}</v-icon>
      <v-tooltip activator="parent" location="top">{{ copied ? 'تم النسخ' : 'نسخ الرابط' }}</v-tooltip>
    </v-btn>
    <v-btn size="small" icon variant="tonal" color="#25D366" :href="whatsapp" target="_blank" rel="noopener" aria-label="مشاركة عبر واتساب">
      <v-icon>mdi-whatsapp</v-icon>
    </v-btn>
    <v-btn size="small" icon variant="tonal" color="#229ED9" :href="telegram" target="_blank" rel="noopener" aria-label="مشاركة عبر تيليكرام">
      <v-icon>mdi-send</v-icon>
    </v-btn>
    <v-btn size="small" icon variant="tonal" color="#1877F2" :href="facebook" target="_blank" rel="noopener" aria-label="مشاركة عبر فيسبوك">
      <v-icon>mdi-facebook</v-icon>
    </v-btn>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { copyText } from '@/utils/clipboard'
import { toast } from '@/composables/useToast'

const props = defineProps({
  url: { type: String, required: true },
  title: { type: String, default: '' },
  text: { type: String, default: '' },
  label: { type: String, default: '' },
})
const copied = ref(false)
const canNativeShare = typeof navigator !== 'undefined' && !!navigator.share
const message = computed(() => [props.text || props.title, props.url].filter(Boolean).join('\n'))
const whatsapp = computed(() => `https://wa.me/?text=${encodeURIComponent(message.value)}`)
const telegram = computed(() => `https://t.me/share/url?url=${encodeURIComponent(props.url)}&text=${encodeURIComponent(props.text || props.title)}`)
const facebook = computed(() => `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(props.url)}`)

async function copy() {
  if (await copyText(props.url)) {
    copied.value = true
    toast('تم نسخ الرابط', 'success')
    setTimeout(() => (copied.value = false), 2000)
  } else toast('تعذّر النسخ، انسخ الرابط يدوياً', 'error')
}
async function nativeShare() {
  try {
    await navigator.share({ title: props.title, text: props.text || props.title, url: props.url })
  } catch { /* cancelled */ }
}
</script>
