<script setup>
import { computed, ref, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import { legalError, legalMoney, legalPay, legalPerson } from '@/composables/legalDeskApi'

/**
 * The one place a تسديد is taken from a debtor (used by the «التسديدات» tab).
 * Opens on a debtor id, reads his current balances, and records an amount per sale
 * plus a part against the legal penalties.
 */
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  personId: { type: Number, default: 0 },
  /** Start with every amount filled in (full settlement). */
  full: { type: Boolean, default: false },
})

const emit = defineEmits(['update:modelValue', 'paid'])

const toast = useToast()
const loading = ref(false)
const paying = ref(false)
const person = ref({})
const totals = ref({ remaining: 0, penaltiesDue: 0, totalDue: 0 })
const lines = ref([])
const penalty = ref(0)
const note = ref('')
const stale = ref([])

const debt = computed(() => lines.value.reduce((sum, l) => sum + (Number(l.amount) || 0), 0))
const total = computed(() => debt.value + (Number(penalty.value) || 0))

const invalid = computed(() =>
  total.value <= 0
  || lines.value.some(l => Number(l.amount) < 0 || Number(l.amount) > l.remaining)
  || Number(penalty.value) < 0
  || Number(penalty.value) > totals.value.penaltiesDue)

async function open() {
  loading.value = true
  lines.value = []
  penalty.value = 0
  note.value = ''
  try {
    const data = await legalPerson(props.personId)

    person.value = data.person || {}
    totals.value = data.totals
    stale.value = data.staleCities || []
    lines.value = (data.accounts || [])
      .filter(a => a.remaining > 0)
      .map(a => ({
        accountId: a.id,
        label: `${a.customerName} — ${a.cityName}`,
        code: a.archiveCode,
        items: a.items,
        remaining: a.remaining,
        amount: props.full ? a.remaining : 0,
      }))
    penalty.value = props.full ? data.totals.penaltiesDue : 0
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل أرصدة المدين'))
    emit('update:modelValue', false)
  }
  finally {
    loading.value = false
  }
}

watch(() => props.modelValue, value => {
  if (value && props.personId)
    open()
})

function fillAll() {
  for (const line of lines.value)
    line.amount = line.remaining
  penalty.value = totals.value.penaltiesDue
}

async function pay() {
  paying.value = true
  try {
    const res = await legalPay(
      props.personId,
      lines.value.filter(l => Number(l.amount) > 0).map(l => ({ accountId: l.accountId, amount: Number(l.amount) })),
      Number(penalty.value) || 0,
      note.value,
    )

    toast.success(res.closed ? 'تم التسديد الكامل وأُغلق الملف' : `تم تسجيل التسديد — وصل ${res.receiptNumber || ''}`)
    emit('update:modelValue', false)
    emit('paid', res)
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تسجيل التسديد'))
  }
  finally {
    paying.value = false
  }
}
</script>

<template>
  <VDialog
    :model-value="modelValue"
    max-width="760"
    :persistent="paying"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <VCard>
      <VCardTitle class="d-flex align-center justify-space-between">
        <span>تسديد — {{ person.fullName || '' }}</span>
        <VBtn
          size="small"
          variant="tonal"
          :disabled="loading"
          @click="fillAll"
        >
          تسديد كل المطلوب ({{ legalMoney(totals.totalDue) }})
        </VBtn>
      </VCardTitle>
      <VProgressLinear
        v-if="loading"
        indeterminate
      />
      <VCardText v-else>
        <VAlert
          v-if="stale.length"
          type="warning"
          variant="tonal"
          density="compact"
          class="mb-3"
        >
          تعذر الاتصال بـ {{ stale.join('، ') }} — أرصدة حساباتها من آخر تحديث.
        </VAlert>
        <VTable density="compact">
          <thead>
            <tr>
              <th>الحساب</th>
              <th class="text-end">
                الباقي
              </th>
              <th style="inline-size: 190px;">
                المبلغ المسدد
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="line in lines"
              :key="line.accountId"
            >
              <td>
                {{ line.label }}
                <div class="text-caption text-medium-emphasis">
                  <span
                    v-if="line.code"
                    dir="ltr"
                  >{{ line.code }} · </span>{{ line.items }}
                </div>
              </td>
              <td class="text-end">
                {{ legalMoney(line.remaining) }}
              </td>
              <td>
                <VTextField
                  v-model.number="line.amount"
                  type="number"
                  min="0"
                  :max="line.remaining"
                  density="compact"
                  hide-details
                  dir="ltr"
                  :error="Number(line.amount) > line.remaining || Number(line.amount) < 0"
                />
              </td>
            </tr>
            <tr>
              <td class="font-weight-medium">
                غرامات القانونية
              </td>
              <td class="text-end">
                {{ legalMoney(totals.penaltiesDue) }}
              </td>
              <td>
                <VTextField
                  v-model.number="penalty"
                  type="number"
                  min="0"
                  :max="totals.penaltiesDue"
                  density="compact"
                  hide-details
                  dir="ltr"
                  :disabled="totals.penaltiesDue <= 0"
                  :error="Number(penalty) > totals.penaltiesDue || Number(penalty) < 0"
                />
              </td>
            </tr>
            <tr class="font-weight-bold">
              <td colspan="2">
                مجموع التسديد
              </td>
              <td class="text-end">
                {{ legalMoney(total) }}
              </td>
            </tr>
          </tbody>
        </VTable>
        <VTextField
          v-model="note"
          label="ملاحظة (اختياري)"
          density="compact"
          hide-details
          class="mt-3"
        />
        <div class="text-caption text-medium-emphasis mt-2">
          التسديد يُسجَّل في القانونية فقط: الزبون القانوني خرج من فرعه، فلا يدخل المبلغ قاصة الفرع ولا كشفه هناك.
        </div>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          :disabled="paying"
          @click="emit('update:modelValue', false)"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="success"
          variant="flat"
          :loading="paying"
          :disabled="loading || invalid"
          @click="pay"
        >
          تسديد {{ legalMoney(total) }}
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
