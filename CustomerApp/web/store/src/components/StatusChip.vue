<template>
  <v-chip :color="meta.color" size="small" variant="tonal" class="font-weight-bold" :prepend-icon="meta.icon">
    {{ label || meta.label || status }}
  </v-chip>
</template>

<script setup>
import { computed } from 'vue'
import { ORDER_STATUS } from '@/utils/enums'
const props = defineProps({ status: String, label: String })
const meta = computed(() => ORDER_STATUS[props.status] || { color: 'grey', label: props.status })
</script>
