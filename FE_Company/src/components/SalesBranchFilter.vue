<script setup>
import { computed, watch } from 'vue'
import { isCentralSalesManager, useSalesBranches } from '@/composables/useSalesBranches'

const props = defineProps({
  allowAll: { type: Boolean, default: false },
  itemsOverride: { type: Array, default: null },
})

const emit = defineEmits(['change'])
const cityValue = defineModel({ type: String, default: '' })
const { items, isLoading } = useSalesBranches()

const sourceItems = computed(() => {
  if (Array.isArray(props.itemsOverride) && props.itemsOverride.length)
    return props.itemsOverride

  return items.value
})

const selectableItems = computed(() => {
  // Non-central managers are bound to LinkCity tenancy — multi-province filter would
  // only change the label while still reading the home branch DB.
  if (isCentralSalesManager() || props.allowAll)
    return sourceItems.value

  const home = String(localStorage.getItem('CityName') || '').trim()
  const database = String(localStorage.getItem('Database') || '').trim()

  const locked = sourceItems.value.filter(i => {
    if (i.value === '')
      return false

    return i.title === home
      || String(i.value) === database
      || String(i.title || '').includes(home)
  })

  return locked.length ? locked : sourceItems.value.filter(i => i.value !== '')
})

watch(selectableItems, list => {
  if (isCentralSalesManager() || props.allowAll)
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
    :disabled="!isCentralSalesManager() && !allowAll && selectableItems.length <= 1"
    @update:model-value="onChange"
  />
</template>
