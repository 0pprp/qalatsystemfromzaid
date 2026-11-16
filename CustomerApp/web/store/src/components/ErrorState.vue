<template>
  <EmptyState icon="mdi-wifi-alert" color="error" :title="title" :text="message">
    <v-btn color="primary" variant="flat" prepend-icon="mdi-refresh" @click="$emit('retry')">إعادة المحاولة</v-btn>
  </EmptyState>
</template>

<script setup>
import EmptyState from './EmptyState.vue'
defineProps({
  title: { type: String, default: 'تعذّر تحميل البيانات' },
  message: { type: String, default: 'حدث خطأ أثناء الاتصال بالخادم.' },
})
defineEmits(['retry'])
</script>
