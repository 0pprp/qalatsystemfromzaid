<template>
  <v-card class="soft-card h-100" rounded="xl" :class="{ 'bg-brand-gradient': highlight }">
    <v-card-text class="pa-3 pa-md-4">
      <div class="d-flex align-center mb-1">
        <v-icon :color="highlight ? 'white' : color" size="20" class="me-1">{{ icon }}</v-icon>
        <span class="text-caption" :class="highlight ? '' : 'text-medium-emphasis'">{{ label }}</span>
      </div>
      <div class="value num" :class="highlight ? 'text-white' : `text-${color}`">{{ value }}</div>
      <div v-if="sub" class="text-caption mt-1" :class="highlight ? '' : 'text-medium-emphasis'">{{ sub }}</div>
      <slot />
    </v-card-text>
  </v-card>
</template>

<script setup>
defineProps({
  icon: String,
  label: String,
  value: String,
  sub: String,
  color: { type: String, default: 'primary' },
  highlight: Boolean,
})
</script>

<style scoped>
.value { font-weight: 800; font-size: 1.15rem; line-height: 1.4; }
@media (min-width: 960px) { .value { font-size: 1.3rem; } }
</style>
