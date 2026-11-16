<template>
  <v-text-field
    :model-value="modelValue"
    placeholder="ابحث عن ثلاجة، شاشة، مكيف..."
    prepend-inner-icon="mdi-magnify"
    variant="solo-filled"
    flat
    density="compact"
    rounded="lg"
    hide-details
    clearable
    type="search"
    enterkeyhint="search"
    aria-label="بحث في المنتجات"
    bg-color="surface-variant"
    @update:model-value="$emit('update:modelValue', $event ?? '')"
    @keydown.enter.prevent="$emit('submit')"
    @click:clear="$emit('update:modelValue', ''); $emit('submit')"
  />
</template>

<script setup>
defineProps({ modelValue: { type: String, default: '' } })
defineEmits(['update:modelValue', 'submit'])
</script>
