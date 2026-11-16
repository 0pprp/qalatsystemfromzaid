<template>
  <div class="qa-container py-10">
    <EmptyState icon="mdi-map-marker-question-outline" title="الصفحة غير موجودة" text="ربما تم نقل الصفحة أو أن الرابط غير صحيح.">
      <v-btn color="primary" :to="{ name: 'home' }">العودة للرئيسية</v-btn>
    </EmptyState>
  </div>
</template>

<script setup>
import EmptyState from '@/components/EmptyState.vue'
</script>
