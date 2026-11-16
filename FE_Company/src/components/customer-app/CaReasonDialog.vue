<script setup>
import { ref, watch } from 'vue'

/**
 * Generic "reason" dialog used by customer-app pages (deny, suspend, reject payout …).
 * `submit` must return a Promise; the dialog stays open and shows the error if it rejects.
 */
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, default: '' },
  icon: { type: String, default: 'tabler-message-2' },
  description: { type: String, default: '' },
  alertType: { type: String, default: 'info' },
  label: { type: String, default: 'السبب' },
  placeholder: { type: String, default: '' },
  required: { type: Boolean, default: true },
  initialValue: { type: String, default: '' },
  quickPicks: { type: Array, default: () => [] },
  secondaryLabel: { type: String, default: '' },
  secondaryHint: { type: String, default: '' },
  confirmText: { type: String, default: 'تأكيد' },
  confirmColor: { type: String, default: 'primary' },
  maxLength: { type: Number, default: 500 },
  submit: { type: Function, required: true },
})

const emit = defineEmits(['update:modelValue', 'done'])

const value = ref('')
const secondary = ref('')
const busy = ref(false)
const error = ref('')

watch(() => props.modelValue, open => {
  if (open) {
    value.value = props.initialValue || ''
    secondary.value = ''
    error.value = ''
    busy.value = false
  }
})

async function onConfirm() {
  const text = String(value.value || '').trim()
  if (props.required && !text) {
    error.value = `${props.label} مطلوب`

    return
  }
  busy.value = true
  error.value = ''
  try {
    const result = await props.submit(text, String(secondary.value || '').trim())

    emit('done', result)
    emit('update:modelValue', false)
  }
  catch (err) {
    error.value = err?.message || 'تعذر تنفيذ العملية'
  }
  finally {
    busy.value = false
  }
}
</script>

<template>
  <VDialog
    :model-value="props.modelValue"
    max-width="620"
    :persistent="busy"
    @update:model-value="v => emit('update:modelValue', v)"
  >
    <VCard>
      <VCardTitle class="d-flex align-center gap-2 pa-4">
        <VAvatar
          :color="props.confirmColor"
          variant="tonal"
          size="36"
        >
          <VIcon :icon="props.icon" />
        </VAvatar>
        <span>{{ props.title }}</span>
      </VCardTitle>
      <VDivider />
      <VCardText class="pt-4">
        <VAlert
          v-if="props.description"
          :type="props.alertType"
          variant="tonal"
          density="compact"
          class="mb-4"
        >
          {{ props.description }}
        </VAlert>
        <div
          v-if="props.quickPicks.length"
          class="mb-3"
        >
          <div class="text-caption text-medium-emphasis mb-1">
            أسباب شائعة (اضغط للاختيار)
          </div>
          <div class="d-flex flex-wrap gap-2">
            <VChip
              v-for="pick in props.quickPicks"
              :key="pick"
              size="small"
              :color="value === pick ? props.confirmColor : 'default'"
              :variant="value === pick ? 'flat' : 'tonal'"
              @click="value = pick"
            >
              {{ pick }}
            </VChip>
          </div>
        </div>
        <VTextarea
          v-model="value"
          :label="props.label + (props.required ? ' *' : ' (اختياري)')"
          :placeholder="props.placeholder"
          rows="3"
          auto-grow
          :counter="props.maxLength"
          :maxlength="props.maxLength"
          autofocus
        />
        <VTextarea
          v-if="props.secondaryLabel"
          v-model="secondary"
          class="mt-3"
          :label="`${props.secondaryLabel} (اختياري)`"
          :hint="props.secondaryHint"
          persistent-hint
          rows="2"
          auto-grow
          maxlength="1000"
        />
        <slot />
        <VAlert
          v-if="error"
          type="error"
          variant="tonal"
          density="compact"
          class="mt-3"
        >
          {{ error }}
        </VAlert>
      </VCardText>
      <VDivider />
      <VCardActions class="pa-3">
        <VSpacer />
        <VBtn
          variant="tonal"
          color="secondary"
          :disabled="busy"
          @click="emit('update:modelValue', false)"
        >
          إلغاء
        </VBtn>
        <VBtn
          :color="props.confirmColor"
          variant="flat"
          :loading="busy"
          @click="onConfirm"
        >
          {{ props.confirmText }}
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
