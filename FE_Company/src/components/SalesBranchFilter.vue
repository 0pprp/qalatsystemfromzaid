<script setup>
import { computed, watch } from 'vue'
import { isCentralSalesManager, useSalesBranches } from '@/composables/useSalesBranches'

const emit = defineEmits(['change'])
const cityValue = defineModel({ type: String, default: '' })
const { items, isLoading } = useSalesBranches()

const selectableItems = computed(() => {
  // Non-central managers are bound to LinkCity tenancy — multi-province filter would
  // only change the label while still reading the home branch DB.
  if (isCentralSalesManager())
    return items.value

  const home = String(localStorage.getItem('CityName') || '').trim()
  const database = String(localStorage.getItem('Database') || '').trim()
  const locked = items.value.filter(i => {
    if (i.value === '')
      return false

    return i.title === home
      || String(i.value) === database
      || String(i.title || '').includes(home)
  })

  return locked.length ? locked : items.value.filter(i => i.value !== '')
})

watch(selectableItems, list => {
  if (isCentralSalesManager())
    return
  if (list.length === 1 && cityValue.value !== list[0].value)
    cityValue.value = list[0].value
}, { immediate: true })

function onChange(value) {
  emit('change', value ?? '')
}
</script>

<template>
  <VSelect
    v-model="cityValue"
    :items="selectableItems"
    item-title="title"
    item-value="value"
    label="المحافظة"
    hide-details
    :loading="isLoading"
    :disabled="!isCentralSalesManager() && selectableItems.length <= 1"
    @update:model-value="onChange"
  />
</template>
