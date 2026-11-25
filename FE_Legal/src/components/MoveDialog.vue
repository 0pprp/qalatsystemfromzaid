<script setup>
import { computed, ref, watch } from 'vue'
import { api, errorMessage } from '../api'
import { day, money, stageIndex, stageMeta, toast } from '../shared'

/** Confirms a move along the flow and says plainly when it costs money. */
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  /** { id, fullName, stage, judicialStartedAtUtc, executionStartedAtUtc } */
  legalCase: { type: Object, default: null },
  to: { type: String, default: '' },
  settings: { type: Object, default: () => ({}) },
})

const emit = defineEmits(['update:modelValue', 'done'])

const note = ref('')
const saving = ref(false)

// Starting a judicial case needs a lawyer; he is chosen (or confirmed) right here.
const needsLawyer = computed(() => props.legalCase?.stage === 'Archive' && props.to === 'Judicial')
const lawyers = ref([])
const lawyerId = ref(null)

// Entering the legal archive issues كود ارشيف القانونية for every sale; the user sees them first.
const needsCodes = computed(() => props.legalCase?.stage === 'Recent' && props.to === 'Archive')
const codes = ref([])
const codesLoading = ref(false)
const codesError = computed(() => codes.value.find(line => line.error)?.error || '')

watch(() => props.modelValue, async open => {
  if (!open)
    return
  note.value = ''
  lawyerId.value = props.legalCase?.lawyerId || null
  codes.value = []
  if (needsCodes.value) {
    codesLoading.value = true
    try {
      codes.value = await api.archiveCodes(props.legalCase.id)
    }
    catch (e) {
      toast.error(errorMessage(e, 'تعذر تجهيز كود ارشيف القانونية'))
      emit('update:modelValue', false)
    }
    finally {
      codesLoading.value = false
    }
  }
  if (needsLawyer.value) {
    try {
      lawyers.value = (await api.lawyers()).filter(l => l.isActive || l.id === lawyerId.value)
    }
    catch (e) {
      toast.error(errorMessage(e, 'تعذر تحميل المحامين'))
    }
  }
})

const target = computed(() => stageMeta(props.to))

const fee = computed(() => {
  if (props.to === 'Execution' && !props.legalCase?.executionStartedAtUtc)
    return { amount: props.settings.ExecutionFee ?? 150000, what: 'التنفيذ' }

  return null
})

const back = computed(() => stageIndex(props.to) < stageIndex(props.legalCase?.stage))

async function confirm() {
  saving.value = true
  try {
    const result = await api.move(props.legalCase.id, props.to, note.value, needsLawyer.value ? lawyerId.value : null)

    toast.ok(result.charged
      ? `نُقل إلى «${target.value.label}» وسُجّلت أجور ${money(result.charged)}`
      : result.archiveCodes?.length
        ? `نُقل إلى «${target.value.label}» — كود الارشيف: ${result.archiveCodes.join(' ، ')}`
        : `نُقل إلى «${target.value.label}»`)
    emit('update:modelValue', false)
    emit('done', result)
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر النقل'))
  }
  finally {
    saving.value = false
  }
}
</script>

<template>
  <VDialog
    :model-value="modelValue"
    :max-width="needsCodes ? 720 : 520"
    :persistent="saving"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <VCard v-if="legalCase">
      <VCardTitle class="pt-5 px-6">
        {{ back ? 'إرجاع' : 'نقل' }} إلى «{{ target.label }}»
      </VCardTitle>
      <VCardText class="px-6">
        <p class="mb-3">
          المدين: <strong>{{ legalCase.fullName }}</strong>
        </p>
        <VAlert
          v-if="back"
          type="info"
          variant="tonal"
          density="comfortable"
          class="mb-4"
        >
          الإرجاع لا يلغي ما سُجّل: الأجور والغرامات والخطوات وكود الارشيف تبقى كما هي، ولا تُحتسب الأجور مرة ثانية عند التقدم من جديد.
        </VAlert>
        <VAlert
          v-if="fee"
          type="warning"
          variant="tonal"
          density="comfortable"
          class="mb-4"
        >
          بدء {{ fee.what }} يسجّل أجوراً قدرها <strong class="num">{{ money(fee.amount) }}</strong> د.ع:
          تُحتسب تسديداً لقلعة الضمان في القاصة، وتُضاف غرامة بنفس المبلغ على المدين في لوحة الشركة. لا يمكن التراجع عن هذه الخطوة.
        </VAlert>
        <template v-if="needsCodes">
          <div class="text-body-2 font-weight-medium mb-1">
            كود ارشيف القانونية — كود لكل بيع، يُكتب على أوراقه في الأرشيف
          </div>
          <VProgressLinear
            v-if="codesLoading"
            indeterminate
            color="primary"
            class="mb-3"
          />
          <VTable
            v-else
            density="compact"
            class="mb-3"
          >
            <thead>
              <tr>
                <th>البيع</th>
                <th>تاريخ البيع</th>
                <th class="text-end">
                  مبلغ البيع
                </th>
                <th>الكود</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="line in codes"
                :key="line.accountId"
              >
                <td>
                  {{ line.customerName }}
                  <div class="text-caption muted">
                    {{ line.cityName }}
                  </div>
                </td>
                <td class="num">
                  {{ day(line.saleDate) }}
                </td>
                <td class="text-end num">
                  {{ money(line.total) }}
                </td>
                <td>
                  <span
                    v-if="line.error"
                    class="text-error text-body-2"
                  >{{ line.error }}</span>
                  <template v-else>
                    <span class="code">{{ line.existingCode || line.proposedCode }}</span>
                    <span class="text-caption muted ms-1">{{ line.existingCode ? 'موجود مسبقاً' : 'جديد' }}</span>
                  </template>
                </td>
              </tr>
            </tbody>
          </VTable>
          <VAlert
            v-if="codesError"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            لا يمكن النقل قبل معالجة الحساب أعلاه.
          </VAlert>
        </template>
        <VAutocomplete
          v-if="needsLawyer"
          v-model="lawyerId"
          :items="lawyers"
          item-title="name"
          item-value="id"
          label="المحامي المسند (إلزامي)"
          no-data-text="لا يوجد محامون — أضف محامياً من صفحة المحامون أولاً"
          class="mb-3"
          hide-details
        />
        <VTextField
          v-model="note"
          label="ملاحظة (اختياري)"
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
          :disabled="(needsLawyer && !lawyerId) || (needsCodes && (codesLoading || Boolean(codesError)))"
          @click="confirm"
        >
          {{ needsCodes ? 'تأكيد الأكواد والنقل' : 'تأكيد' }}
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
