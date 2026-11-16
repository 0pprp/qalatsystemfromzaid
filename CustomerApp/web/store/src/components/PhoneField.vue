<template>
  <v-text-field
    :model-value="modelValue"
    :label="label"
    :rules="required ? [phoneRule] : [optionalPhoneRule]"
    type="tel"
    inputmode="tel"
    autocomplete="tel-national"
    dir="ltr"
    maxlength="16"
    placeholder="07XX XXX XXXX"
    persistent-hint
    :hint="hint"
    :error-messages="errorMessages"
    :disabled="disabled"
    :readonly="readonly"
    prepend-inner-icon="mdi-cellphone"
    class="phone-field"
    @update:model-value="onInput"
    @blur="onBlur"
  >
    <template #append-inner>
      <span class="text-caption text-medium-emphasis ltr text-no-wrap">+964</span>
    </template>
  </v-text-field>
</template>

<script setup>
import { phoneRule, optionalPhoneRule, normalizeIraqiPhone, toLatinDigits } from '@/utils/phone'
defineProps({
  modelValue: { type: String, default: '' },
  label: { type: String, default: 'رقم الموبايل' },
  hint: { type: String, default: 'رقم عراقي فقط يبدأ بـ 07 (مثال 07701234567) — يُقبل أيضاً +964' },
  required: { type: Boolean, default: true },
  errorMessages: { type: [String, Array], default: () => [] },
  disabled: Boolean,
  readonly: Boolean,
})
const emit = defineEmits(['update:modelValue'])
function onInput(v) {
  emit('update:modelValue', toLatinDigits(v ?? '').replace(/[^\d+\s-]/g, ''))
}
function onBlur(e) {
  const v = e?.target?.value
  if (v) {
    const n = normalizeIraqiPhone(v)
    if (/^07\d{9}$/.test(n)) emit('update:modelValue', n)
  }
}
</script>

<style scoped>
.phone-field :deep(input) { text-align: left; letter-spacing: 0.5px; }
</style>
