<template>
  <v-alert type="warning" variant="tonal" border="start" icon="mdi-gavel" class="mb-6">
    <b>مسودة أولية — بانتظار مراجعة المستشار القانوني للشركة.</b>
    <div class="text-body-2 mt-1">هذا النص نموذج مبدئي وقد يتغير قبل الإطلاق الرسمي. آخر تحديث: {{ updated }}.</div>
  </v-alert>
</template>

<script setup>
defineProps({ updated: { type: String, default: 'تشرين الثاني 2026' } })
</script>
