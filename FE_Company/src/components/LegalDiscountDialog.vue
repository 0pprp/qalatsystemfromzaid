<script setup>
import { computed, ref, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import { legalDiscount, legalError, legalMoney, legalPerson } from '@/composables/legalDeskApi'

/**
 * خصم: an amount taken off what the debtor owes (debt first, then the legal penalties).
 * No money is received and no receipt is issued. Never more than everything he owes.
 */
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  personId: { type: Number, default: 0 },
})

const emit = defineEmits(['update:modelValue', 'done'])

const toast = useToast()
const loading = ref(false)
const saving = ref(false)
const person = ref({})
const totals = ref({ remaining: 0, penaltiesDue: 0, totalDue: 0 })
const amount = ref(null)
const note = ref('')

const value = computed(() => Number(amount.value) || 0)
const invalid = computed(() => value.value <= 0 || value.value > totals.value.totalDue)

async function open() {
  loading.value = true
  amount.value = null
  note.value = ''
  try {
    const data = await legalPerson(props.personId)

    person.value = data.person || {}
    totals.value = data.totals
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل أرصدة المدين'))
    emit('update:modelValue', false)
  }
  finally {
    loading.value = false
  }
}

watch(() => props.modelValue, shown => {
  if (shown && props.personId)
    open()
})

async function save() {
  saving.value = true
  try {
    const res = await legalDiscount(props.personId, value.value, note.value)

    toast.success(res.closed ? 'سُجّل الخصم وأُغلق الملف' : 'سُجّل الخصم')
    emit('update:modelValue', false)
    emit('done', res)
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تسجيل الخصم'))
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
      <VCardTitle>خصم — {{ person.fullName || '' }}</VCardTitle>
      <VProgressLinear
        v-if="loading"
        indeterminate
      />
      <VCardText v-else>
        <div class="d-flex justify-space-between mb-1">
          <span class="text-medium-emphasis">باقي الدين</span>
          <span>{{ legalMoney(totals.remaining) }}</span>
        </div>
        <div class="d-flex justify-space-between mb-1">
          <span class="text-medium-emphasis">غرامات القانونية</span>
          <span>{{ legalMoney(totals.penaltiesDue) }}</span>
        </div>
        <div class="d-flex justify-space-between font-weight-bold mb-4">
          <span>كل المطلوب</span>
          <span>{{ legalMoney(totals.totalDue) }}</span>
        </div>
        <VTextField
          v-model.number="amount"
          type="number"
          min="0"
          :max="totals.totalDue"
          label="مبلغ الخصم"
          dir="ltr"
          density="compact"
          autofocus
          :error="value > totals.totalDue"
          :hint="value > totals.totalDue ? 'الخصم أكبر من كل المطلوب' : 'يُخصم من الدين أولاً ثم من الغرامات'"
          persistent-hint
          class="mb-3"
        />
        <VTextField
          v-model="note"
          label="سبب الخصم (اختياري)"
          density="compact"
          hide-details
        />
        <div class="text-caption text-medium-emphasis mt-3">
          الخصم يُنقص المطلوب من المدين ويُحسب ضمن مجموع القاصة، ولا يصدر له وصل.
        </div>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          :disabled="saving"
          @click="emit('update:modelValue', false)"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="warning"
          variant="flat"
          :loading="saving"
          :disabled="loading || invalid"
          @click="save"
        >
          خصم {{ legalMoney(value) }}
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
