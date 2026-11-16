<script setup>
import { onMounted, ref } from 'vue'
import { archiveCities, errorMessage, rememberBranch, rememberedBranch } from '@/composables/archiveFlowApi'

/** Branch picker for the archive pages; remembers the last branch. */
const model = defineModel({ type: String, default: '' })
const cities = ref([])
const loading = ref(false)
const error = ref('')

onMounted(async () => {
  loading.value = true
  try {
    const rows = await archiveCities()
    cities.value = (Array.isArray(rows) ? rows : []).map(r => ({ title: r.name, value: String(r.value) }))
    const saved = rememberedBranch()
    const pick = cities.value.find(c => c.value === (model.value || saved)) || cities.value[0]
    if (pick && pick.value !== model.value)
      model.value = pick.value
  }
  catch (e) {
    error.value = errorMessage(e, 'تعذر تحميل الفروع')
  }
  finally {
    loading.value = false
  }
})

function change(value) {
  model.value = value
  rememberBranch(value)
}
</script>

<template>
  <VSelect
    :model-value="model"
    :items="cities"
    :loading="loading"
    :error-messages="error || undefined"
    label="الفرع"
    density="compact"
    hide-details="auto"
    style="min-inline-size: 200px; max-inline-size: 260px;"
    @update:model-value="change"
  />
</template>
