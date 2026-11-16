<template>
  <div class="text-center py-10 px-4 d-flex flex-column align-center">
    <v-avatar :color="color" variant="tonal" size="88" class="mb-4">
      <v-icon size="44">{{ icon }}</v-icon>
    </v-avatar>
    <div class="text-h6 font-weight-bold mb-1">{{ title }}</div>
    <div v-if="text" class="text-body-2 text-medium-emphasis mb-4" style="max-width: 420px">{{ text }}</div>
    <slot />
  </div>
</template>

<script setup>
defineProps({
  icon: { type: String, default: 'mdi-package-variant' },
  title: { type: String, required: true },
  text: { type: String, default: '' },
  color: { type: String, default: 'primary' },
})
</script>
