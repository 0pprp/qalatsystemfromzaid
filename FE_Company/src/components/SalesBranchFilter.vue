<script setup>
import { useSalesBranches } from '@/composables/useSalesBranches'

const emit = defineEmits(['change'])
const cityValue = defineModel({ type: String, default: '' })
const { items, isLoading } = useSalesBranches()

function onChange(value) {
  emit('change', value ?? '')
}
</script>

<template>
  <VSelect
    v-model="cityValue"
    :items="items"
    item-title="title"
    item-value="value"
    label="المحافظة"
    hide-details
    :loading="isLoading"
    @update:model-value="onChange"
  />
</template>
