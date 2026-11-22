<script setup>
import { ref, watch } from 'vue'
import { errorMessage } from '../api'
import { money, toast } from '../shared'

/** One amount + one note, used for every simple money movement (company funds, expense, wallet top-up, return). */
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, required: true },
  description: { type: String, default: '' },
  confirmLabel: { type: String, default: 'حفظ' },
  noteLabel: { type: String, default: 'ملاحظة (اختياري)' },
  noteRequired: { type: Boolean, default: false },
  /** When set, amounts above it are refused before the server is asked. */
  max: { type: Number, default: null },
  maxLabel: { type: String, default: '' },
  /** async (amount, note) => void */
  action: { type: Function, required: true },
  successText: { type: String, default: 'تم الحفظ' },
})

const emit = defineEmits(['update:modelValue', 'done'])

const amount = ref(null)
const note = ref('')
const saving = ref(false)

watch(() => props.modelValue, open => {
  if (open) {
    amount.value = null
    note.value = ''
  }
})

const tooMuch = () => props.max !== null && Number(amount.value) > props.max

async function save() {
  const value = Number(amount.value)
  if (!Number.isFinite(value) || value <= 0) {
    toast.error('أدخل مبلغاً صحيحاً')

    return
  }
  if (tooMuch()) {
    toast.error(`المبلغ أكبر من ${props.maxLabel} (${money(props.max)})`)

    return
  }
  if (props.noteRequired && !note.value.trim()) {
    toast.error('اكتب البيان')

    return
  }
  saving.value = true
  try {
    await props.action(Math.round(value), note.value.trim())
    toast.ok(props.successText)
    emit('update:modelValue', false)
    emit('done')
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحفظ'))
  }
  finally {
    saving.value = false
  }
}
</script>

<template>
  <VDialog
    :model-value="modelValue"
    max-width="480"
    :persistent="saving"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <VCard>
      <VCardTitle class="pt-5 px-6">
        {{ title }}
      </VCardTitle>
      <VCardText class="px-6">
        <p
          v-if="description"
          class="text-body-2 muted mb-4"
        >
          {{ description }}
        </p>
        <VTextField
          v-model.number="amount"
          type="number"
          min="0"
          label="المبلغ (د.ع)"
          dir="ltr"
          autofocus
          :error="tooMuch()"
          :hint="max !== null ? `${maxLabel}: ${money(max)}` : (amount > 0 ? money(amount) : '')"
          persistent-hint
          class="mb-3"
          @keyup.enter="save"
        />
        <VTextField
          v-model="note"
          :label="noteLabel"
          hide-details
          @keyup.enter="save"
        />
      </VCardText>
      <VCardActions class="px-6 pb-5">
        <VSpacer />
        <VBtn
          variant="text"
          :disabled="saving"
          @click="emit('update:modelValue', false)"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="primary"
          variant="flat"
          :loading="saving"
          @click="save"
        >
          {{ confirmLabel }}
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
