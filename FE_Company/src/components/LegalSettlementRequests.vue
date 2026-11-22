<script setup>
import { onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import {
  legalError,
  formatUtc,
  legalDecide,
  legalRequests,
  legalMoney,
  legalStatus,
} from '@/composables/legalDeskApi'

const emit = defineEmits(['changed', 'pay'])
const toast = useToast()
const router = useRouter()

const status = ref('Pending')
const loading = ref(false)
const rows = ref([])

const dialog = ref(false)
const current = ref(null)
const penalty = ref(0)
const note = ref('')
const saving = ref(false)

const STATUS = {
  Pending: { label: 'بانتظار القرار', color: 'warning' },
  Approved: { label: 'تمت الموافقة', color: 'success' },
  Rejected: { label: 'مرفوض', color: 'error' },
}

async function load() {
  loading.value = true
  try {
    rows.value = await legalRequests(status.value)
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل الطلبات'))
  }
  finally {
    loading.value = false
  }
}

function open(row) {
  current.value = row
  penalty.value = row.judicialStarted ? 0 : row.suggestedPenalty
  note.value = ''
  dialog.value = true
}

async function decide(approve) {
  saving.value = true
  try {
    const { personId } = await legalDecide(current.value.id, approve, approve && !current.value.judicialStarted ? penalty.value : 0, note.value)

    dialog.value = false
    if (approve) {
      toast.success('تمت الموافقة — أكمل التسديد')
      await load()
      emit('changed')
      emit('pay', personId)
    }
    else {
      toast.success('رُفض الطلب')
      await load()
      emit('changed')
    }
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر حفظ القرار'))
  }
  finally {
    saving.value = false
  }
}

watch(status, load)
onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 pa-4">
      <div class="text-body-2 text-medium-emphasis">
        مدين يريد تسديد كل ما عليه: وافق (مع غرامة التسديد الكامل إن لم تُقَم عليه دعوى) ثم سجّل التسديد.
      </div>
      <VChipGroup
        v-model="status"
        mandatory
        selected-class="text-primary"
      >
        <VChip
          value="Pending"
          filter
          variant="outlined"
        >
          بانتظار القرار
        </VChip>
        <VChip
          value="Approved"
          filter
          variant="outlined"
        >
          الموافق عليها
        </VChip>
        <VChip
          value="Rejected"
          filter
          variant="outlined"
        >
          المرفوضة
        </VChip>
      </VChipGroup>
    </div>

    <div>
      <VProgressLinear
        v-if="loading"
        indeterminate
      />
      <VTable density="comfortable">
        <thead>
          <tr>
            <th>المدين</th>
            <th>الحالة</th>
            <th class="text-end">
              باقي الدين
            </th>
            <th class="text-end">
              الغرامة
            </th>
            <th>طلبه</th>
            <th>ملاحظة الفريق</th>
            <th>الحالة</th>
            <th />
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in rows"
            :key="row.id"
          >
            <td>
              <RouterLink :to="{ path: '/legal-desk-debtor', query: { id: row.personId } }">
                {{ row.fullName }}
              </RouterLink>
              <div
                class="text-caption text-medium-emphasis"
                dir="ltr"
              >
                {{ row.phone }}
              </div>
            </td>
            <td>
              <VChip
                size="small"
                :color="legalStatus(row.stage).color"
                variant="tonal"
              >
                {{ legalStatus(row.stage).label }}
              </VChip>
            </td>
            <td class="text-end">
              {{ legalMoney(row.remainingSnapshot) }}
            </td>
            <td class="text-end">
              {{ legalMoney(row.status === 'Approved' ? row.approvedPenalty : row.suggestedPenalty) }}
            </td>
            <td>
              {{ row.requestedBy }}
              <div class="text-caption text-medium-emphasis">
                {{ formatUtc(row.requestedAtUtc, true) }}
              </div>
            </td>
            <td>{{ row.note || '—' }}</td>
            <td>
              <VChip
                size="small"
                :color="STATUS[row.status]?.color"
                variant="tonal"
              >
                {{ STATUS[row.status]?.label || row.status }}
              </VChip>
              <div
                v-if="row.decidedBy"
                class="text-caption text-medium-emphasis"
              >
                {{ row.decidedBy }} · {{ formatUtc(row.decidedAtUtc) }}
              </div>
            </td>
            <td>
              <VBtn
                v-if="row.status === 'Pending'"
                size="small"
                color="primary"
                @click="open(row)"
              >
                البت في الطلب
              </VBtn>
            </td>
          </tr>
          <tr v-if="!loading && !rows.length">
            <td
              colspan="8"
              class="text-center text-medium-emphasis py-8"
            >
              لا توجد طلبات
            </td>
          </tr>
        </tbody>
      </VTable>
    </div>

    <VDialog
      v-model="dialog"
      max-width="520"
    >
      <VCard v-if="current">
        <VCardTitle>طلب تسديد كامل — {{ current.fullName }}</VCardTitle>
        <VCardText>
          <p class="mb-3">
            باقي الدين حسب آخر تحديث: <strong>{{ legalMoney(current.remainingSnapshot) }}</strong>
          </p>
          <VTextField
            v-if="!current.judicialStarted"
            v-model.number="penalty"
            type="number"
            min="0"
            label="غرامة التسديد الكامل"
            density="compact"
            dir="ltr"
            hint="المدين لم تُقَم عليه دعوى: هذه الغرامة المقترحة، ويمكنك تعديلها"
            persistent-hint
            class="mb-4"
          />
          <VAlert
            v-else
            type="info"
            variant="tonal"
            density="compact"
            class="mb-4"
          >
            أُقيمت على هذا المدين دعوى، فغرامة الدعوى قائمة أصلاً ولا تُضاف غرامة تسديد كامل.
          </VAlert>
          <VTextField
            v-model="note"
            label="ملاحظة (اختياري)"
            density="compact"
            hide-details
          />
        </VCardText>
        <VCardActions>
          <VBtn
            color="error"
            variant="text"
            :disabled="saving"
            @click="decide(false)"
          >
            رفض الطلب
          </VBtn>
          <VSpacer />
          <VBtn
            variant="text"
            @click="dialog = false"
          >
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="saving"
            :disabled="Number(penalty) < 0"
            @click="decide(true)"
          >
            موافقة ومتابعة التسديد
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
