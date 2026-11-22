<script setup>
import { computed, ref, watch } from 'vue'
import { api, errorMessage } from '../api'
import { flowOf, toast, today } from '../shared'

/** Logs a step (اقامة دعوى، تبليغ، مرافعة…) on a debtor that is in the judicial flow or in التنفيذ. */
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  /** { id, fullName, stage } */
  legalCase: { type: Object, default: null },
  stepTypes: { type: Array, default: () => [] },
})

const emit = defineEmits(['update:modelValue', 'done'])

const stepTypeId = ref(null)
const date = ref(today())
const note = ref('')
const saving = ref(false)

// The step is done by the assigned lawyer unless another one is picked for this step only.
// The choice is not remembered: every new step starts again from the assigned lawyer.
const lawyers = ref([])
const lawyerId = ref(null)
const assignedId = computed(() => props.legalCase?.lawyerId || null)
const substitute = computed(() => Boolean(lawyerId.value) && lawyerId.value !== assignedId.value)

const options = computed(() => {
  const flow = flowOf(props.legalCase?.stage)

  return props.stepTypes.filter(t => t.flow === flow && t.isActive)
})

watch(() => props.modelValue, async open => {
  if (!open)
    return
  stepTypeId.value = null
  date.value = today()
  note.value = ''
  lawyerId.value = assignedId.value
  try {
    lawyers.value = (await api.lawyers()).filter(l => l.isActive || l.id === assignedId.value)
  }
  catch { /* the step can still be logged for the assigned lawyer */ }
})

async function save() {
  if (!stepTypeId.value) {
    toast.error('اختر الخطوة')

    return
  }
  saving.value = true
  try {
    await api.addStep(props.legalCase.id, stepTypeId.value, note.value, date.value, substitute.value ? lawyerId.value : null)
    toast.ok(date.value > today() ? 'جُدولت الخطوة وأُضيفت إلى الخطوات القادمة' : 'سُجّلت الخطوة')
    emit('update:modelValue', false)
    emit('done')
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تسجيل الخطوة'))
  }
  finally {
    saving.value = false
  }
}
</script>

<template>
  <VDialog
    :model-value="modelValue"
    max-width="520"
    :persistent="saving"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <VCard v-if="legalCase">
      <VCardTitle class="pt-5 px-6">
        خطوة جديدة — {{ legalCase.fullName }}
      </VCardTitle>
      <VCardText class="px-6">
        <div class="text-body-2 muted mb-2">
          اختر الخطوة (يمكن تكرار نفس الخطوة أكثر من مرة) — <span class="text-error">*</span> إلزامية
        </div>
        <VChipGroup
          v-model="stepTypeId"
          column
          mandatory
          selected-class="text-primary"
          class="mb-3"
        >
          <VChip
            v-for="t in options"
            :key="t.id"
            :value="t.id"
            variant="outlined"
            filter
          >
            {{ t.name }}<span
              v-if="t.isMandatory"
              class="text-error"
            >&nbsp;*</span>
          </VChip>
        </VChipGroup>
        <div
          v-if="!options.length"
          class="text-body-2 text-error mb-3"
        >
          لا توجد خطوات معرّفة لهذه المرحلة. أضفها من الإعدادات.
        </div>
        <VAutocomplete
          v-model="lawyerId"
          :items="lawyers"
          item-title="name"
          item-value="id"
          label="محامي هذه الخطوة"
          :hint="substitute ? 'لهذه الخطوة فقط — الخطوة التالية تعود للمحامي المسند' : 'المحامي المسند للمدين. غيّره إن كلّف محامٍ آخر بهذه الخطوة فقط'"
          persistent-hint
          class="mb-3"
        />
        <VTextField
          v-model="date"
          type="date"
          label="تاريخ الخطوة"
          :hint="date > today() ? 'تاريخ قادم: تُسجَّل كخطوة مجدولة وتظهر في «الخطوات القادمة» حتى تُنجز' : 'اختر تاريخاً قادماً لجدولة الخطوة (موعد مرافعة مثلاً)'"
          persistent-hint
          class="mb-3"
        />
        <VTextarea
          v-model="note"
          label="ملاحظة (اختياري)"
          rows="2"
          hide-details
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
          تسجيل
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
