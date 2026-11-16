<template>
  <div class="review-block">
    <div class="d-flex align-center mb-1">
      <span class="text-caption text-medium-emphasis font-weight-bold">{{ title }}</span>
      <v-spacer />
      <v-btn size="x-small" variant="text" color="primary" prepend-icon="mdi-pencil" @click="$emit('edit')">تعديل</v-btn>
    </div>
    <div class="text-body-2" style="line-height: 1.9"><slot /></div>
  </div>
</template>

<script setup>
defineProps({ title: String })
defineEmits(['edit'])
</script>

<style scoped>
.review-block { border: 1px solid rgba(0, 0, 0, 0.08); border-radius: 14px; padding: 10px 14px; background: #fafcfc; }
</style>
