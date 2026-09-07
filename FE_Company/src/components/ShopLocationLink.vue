<script setup>
import { computed } from 'vue'
import { googleMapsUrl, shopCoordsCaption } from '@/composables/shopMaps'

const props = defineProps({
  shop: {
    type: Object,
    default: null,
  },
})

const url = computed(() => googleMapsUrl(props.shop))
const caption = computed(() => shopCoordsCaption(props.shop))
</script>

<template>
  <div
    v-if="url"
    class="d-flex align-center flex-wrap ga-2 my-2"
  >
    <span class="font-weight-medium">موقع المحل</span>
    <VBtn
      size="small"
      color="primary"
      :href="url"
      target="_blank"
      rel="noopener noreferrer"
    >
      فتح في خرائط Google
    </VBtn>
    <span class="text-caption text-medium-emphasis">{{ caption }}</span>
  </div>
</template>
