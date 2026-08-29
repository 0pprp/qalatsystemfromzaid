<script setup>
import { useToast } from '@/composables/useToast'

const { state } = useToast()
</script>

<template>
  <VSnackbar
    v-model="state.visible"
    :color="state.color"
    :timeout="state.timeout"
    location="top center"
  >
    {{ state.message }}
    
    <template #actions>
      <VBtn
        color="white"
        variant="text"
        @click="state.visible = false"
      >
        إغلاق
      </VBtn>
    </template>
  </VSnackbar>
</template>
