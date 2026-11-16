<template>
  <div>
    <div class="font-weight-bold mb-2">الفرع / المحافظة</div>
    <v-autocomplete
      :model-value="modelValue.cityValue"
      :items="branches"
      item-title="name"
      item-value="cityValue"
      placeholder="كل الفروع"
      clearable
      hide-details
      density="compact"
      prepend-inner-icon="mdi-map-marker-outline"
      no-data-text="لا توجد نتائج"
      @update:model-value="set('cityValue', $event)"
    />
    <div class="font-weight-bold mt-5 mb-2">طريقة الشراء</div>
    <v-radio-group :model-value="modelValue.purchaseType" hide-details density="compact" color="primary" @update:model-value="set('purchaseType', $event)">
      <v-radio :value="null" label="الكل" />
      <v-radio value="Cash" label="نقد" />
      <v-radio value="DailyInstallments" label="أقساط يومية" />
    </v-radio-group>
    <div v-if="modelValue.purchaseType === 'DailyInstallments'" class="text-caption text-warning mt-1">
      <v-icon size="14">mdi-store-alert-outline</v-icon> الأقساط اليومية لأصحاب المحلات التجارية فقط
    </div>
    <v-btn block variant="text" color="primary" class="mt-4" prepend-icon="mdi-filter-remove-outline" @click="$emit('reset')">إعادة الضبط</v-btn>
  </div>
</template>

<script setup>
const props = defineProps({ modelValue: { type: Object, required: true }, branches: { type: Array, default: () => [] } })
const emit = defineEmits(['update:modelValue', 'reset'])
function set(key, value) {
  emit('update:modelValue', { ...props.modelValue, [key]: value ?? null })
}
</script>
