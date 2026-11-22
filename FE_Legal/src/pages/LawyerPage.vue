<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { api, errorMessage } from '../api'
import AmountDialog from '../components/AmountDialog.vue'
import FlowBar from '../components/FlowBar.vue'
import UpcomingSteps from '../components/UpcomingSteps.vue'
import { STAGES, day, fromUtc, money, stageMeta, toast } from '../shared'

const props = defineProps({ id: { type: [String, Number], required: true } })

const loading = ref(false)
const data = ref(null)
const cash = ref(null)

const lawyer = computed(() => data.value?.lawyer || {})
const cases = computed(() => data.value?.cases || [])
const transactions = computed(() => data.value?.transactions || [])
const fees = computed(() => transactions.value.filter(t => t.kind === 'Fee'))
const received = computed(() => transactions.value.filter(t => t.kind !== 'Fee'))
const substituteSteps = computed(() => data.value?.substituteSteps || [])

// The same path as the main board, holding only this lawyer's debtors.
const flowStage = ref('')

const flowCounts = computed(() => Object.fromEntries(STAGES.map(s => {
  const rows = cases.value.filter(c => c.stage === s.key)

  return [s.key, { count: rows.length, remainingSnapshot: rows.reduce((sum, c) => sum + Number(c.remainingSnapshot || 0), 0) }]
})))

const closedCases = computed(() => cases.value.filter(c => c.stage === 'Closed'))
const flowRows = computed(() => cases.value.filter(c => c.stage === flowStage.value))

/** Opens on the first station that has debtors, so the page is not empty on arrival. */
function pickStage() {
  if (flowStage.value && (flowStage.value === 'Closed' || flowCounts.value[flowStage.value]?.count))
    return
  flowStage.value = STAGES.find(s => flowCounts.value[s.key].count)?.key || STAGES[0].key
}

async function load() {
  loading.value = true
  try {
    data.value = await api.lawyer(props.id)
    pickStage()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل ملف المحامي'))
  }
  finally {
    loading.value = false
  }
}

// —— المحفظة ——
const transferDialog = ref(false)
const returnDialog = ref(false)

async function openTransfer() {
  cash.value = null
  transferDialog.value = true
  try {
    cash.value = (await api.registers()).cash
  }
  catch { /* the server still enforces the limit */ }
}

// —— دفع رسم ——
const feeDialog = ref(false)
const feeForm = ref({ caseId: null, amount: null, note: '', outside: false, image: null })
const feeSaving = ref(false)
const allCases = ref([])
const allCasesLoading = ref(false)

const assignedOpen = computed(() => cases.value.filter(c => c.stage !== 'Closed'))

const feeOptions = computed(() => (feeForm.value.outside
  ? allCases.value.filter(c => c.lawyerId !== lawyer.value.id)
  : assignedOpen.value
).map(c => ({
  value: c.id,
  title: c.fullName,
  subtitle: `${stageMeta(c.stage).label}${c.lawyerName && feeForm.value.outside ? ` · محاميه: ${c.lawyerName}` : ''}`,
})))

function openFee() {
  feeForm.value = { caseId: null, amount: null, note: '', outside: false, image: null }
  feeDialog.value = true
}

watch(() => feeForm.value.outside, async outside => {
  feeForm.value.caseId = null
  if (!outside || allCases.value.length)
    return
  allCasesLoading.value = true
  try {
    allCases.value = await api.cases({})
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل المدينين'))
  }
  finally {
    allCasesLoading.value = false
  }
})

function pickImage(event) {
  const file = event.target.files?.[0] || null
  if (file && file.size > 8 * 1024 * 1024) {
    toast.error('حجم الصورة أكبر من 8MB')
    event.target.value = ''

    return
  }
  feeForm.value.image = file
}

async function saveFee() {
  const form = feeForm.value
  if (!form.caseId) {
    toast.error('اختر المدين الذي دُفع الرسم عنه')

    return
  }
  if (!(Number(form.amount) > 0)) {
    toast.error('أدخل مبلغ الرسم')

    return
  }
  if (!form.note.trim()) {
    toast.error('اكتب بيان الرسم')

    return
  }
  feeSaving.value = true
  try {
    await api.addFee(lawyer.value.id, {
      caseId: form.caseId,
      amount: Math.round(Number(form.amount)),
      note: form.note.trim(),
      outsideAssignment: form.outside,
      image: form.image,
    })
    feeDialog.value = false
    toast.ok('سُجّل الرسم وخُصم من المحفظة')
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تسجيل الرسم'))
  }
  finally {
    feeSaving.value = false
  }
}

// —— تعديل ——
const editDialog = ref(false)
const editForm = ref({})
const editSaving = ref(false)

function openEdit() {
  editForm.value = { id: lawyer.value.id, name: lawyer.value.name, branch: lawyer.value.branch, phone: lawyer.value.phone, notes: lawyer.value.notes, isActive: lawyer.value.isActive }
  editDialog.value = true
}

async function saveEdit() {
  editSaving.value = true
  try {
    await api.saveLawyer(editForm.value)
    editDialog.value = false
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحفظ'))
  }
  finally {
    editSaving.value = false
  }
}

// —— صورة الرسم ——
const imageDialog = ref(false)
const imageUrl = ref('')

async function showImage(tx) {
  try {
    const blob = await api.feeImage(tx.id)

    if (imageUrl.value)
      URL.revokeObjectURL(imageUrl.value)
    imageUrl.value = URL.createObjectURL(blob)
    imageDialog.value = true
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر فتح الصورة'))
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <VProgressLinear
      v-if="loading && !data"
      indeterminate
      color="primary"
    />
    <template v-if="data">
      <RouterLink
        to="/lawyers"
        class="back-link"
      >
        <VIcon
          icon="arrow-right"
          size="16"
        />
        المحامون
      </RouterLink>
      <div class="d-flex flex-wrap align-center justify-space-between ga-3 mb-4">
        <div>
          <h1 class="page-title">
            {{ lawyer.name }}
            <VChip
              v-if="!lawyer.isActive"
              size="small"
              variant="tonal"
              class="ms-2"
            >
              غير فعّال
            </VChip>
          </h1>
          <div class="text-body-2 muted">
            <span v-if="lawyer.branch">{{ lawyer.branch }} · </span>
            <span class="num">{{ lawyer.phone || 'بلا هاتف' }}</span>
            <span v-if="lawyer.notes"> · {{ lawyer.notes }}</span>
          </div>
        </div>
        <div class="d-flex flex-wrap ga-2">
          <VBtn
            variant="text"
            prepend-icon="edit"
            @click="openEdit"
          >
            تعديل
          </VBtn>
          <VBtn
            variant="tonal"
            prepend-icon="arrow-back-up"
            :disabled="lawyer.balance <= 0"
            @click="returnDialog = true"
          >
            إرجاع للقاصة
          </VBtn>
          <VBtn
            variant="tonal"
            color="primary"
            prepend-icon="transfer"
            @click="openTransfer"
          >
            تحويل مبلغ للمحفظة
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            prepend-icon="receipt"
            @click="openFee"
          >
            دفع رسم عن مدين
          </VBtn>
        </div>
      </div>

      <VCard class="mb-4">
        <VRow no-gutters>
          <VCol
            cols="6"
            md="3"
            class="stat"
          >
            <div class="stat__label">
              رصيد المحفظة
            </div>
            <div
              class="stat__value num"
              :class="lawyer.balance < 0 ? 'text-error' : 'text-primary'"
            >
              {{ money(lawyer.balance) }}
            </div>
            <div
              v-if="lawyer.balance < 0"
              class="text-caption text-error"
            >
              دفع من جيبه — حوِّل له الفرق
            </div>
          </VCol>
          <VCol
            cols="6"
            md="3"
            class="stat"
          >
            <div class="stat__label">
              مبالغ استلمها
            </div>
            <div class="stat__value num">
              {{ money(lawyer.received) }}
            </div>
          </VCol>
          <VCol
            cols="6"
            md="3"
            class="stat"
          >
            <div class="stat__label">
              رسوم دفعها
            </div>
            <div class="stat__value num">
              {{ money(lawyer.feesPaid) }}
            </div>
          </VCol>
          <VCol
            cols="6"
            md="3"
            class="stat"
          >
            <div class="stat__label">
              المدينون المسندون
            </div>
            <div class="stat__value num">
              {{ lawyer.openCases }}
              <span class="text-body-2 muted">مفتوح من {{ lawyer.totalCases }}</span>
            </div>
          </VCol>
        </VRow>
      </VCard>

      <UpcomingSteps
        :lawyer-id="lawyer.id"
        class="mb-4"
        @changed="load"
      />

      <!-- مسار مدينيه -->
      <div class="section-title mb-2">
        مدينوه على المسار
      </div>
      <FlowBar
        v-model="flowStage"
        :counts="flowCounts"
        class="mb-2"
      />
      <div class="d-flex justify-end mb-3">
        <VBtn
          variant="text"
          size="small"
          :color="flowStage === 'Closed' ? 'success' : undefined"
          prepend-icon="circle-check"
          @click="flowStage = 'Closed'"
        >
          الملفات المغلقة ({{ closedCases.length }})
        </VBtn>
      </div>
      <VCard class="mb-4">
        <div class="d-flex align-center ga-3 pa-4">
          <VAvatar
            :color="stageMeta(flowStage).color"
            variant="tonal"
            rounded="lg"
          >
            <VIcon :icon="stageMeta(flowStage).icon" />
          </VAvatar>
          <div class="section-title">
            {{ stageMeta(flowStage).label }}
          </div>
          <VSpacer />
          <VChip variant="tonal">
            <span class="num">{{ flowRows.length }}</span>&nbsp;اسم
          </VChip>
        </div>
        <VDivider />
        <VTable hover>
          <thead>
            <tr>
              <th>المدين</th>
              <th class="text-end">
                باقي الدين
              </th>
              <th>آخر خطوة</th>
              <th class="text-end">
                رسوم مدفوعة عليه
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="row in flowRows"
              :key="row.id"
              class="row-link"
              @click="$router.push(`/cases/${row.id}`)"
            >
              <td>
                <span class="font-weight-medium">{{ row.fullName }}</span>
                <VChip
                  v-if="row.isAnnulled"
                  size="x-small"
                  color="accent"
                  variant="flat"
                  class="ms-1"
                >
                  ابطال
                </VChip>
                <div v-if="row.archiveCodes">
                  <span
                    v-for="code in row.archiveCodes.split(' · ')"
                    :key="code"
                    class="code me-1"
                  >{{ code }}</span>
                </div>
              </td>
              <td class="text-end num">
                {{ money(row.remainingSnapshot) }}
              </td>
              <td>
                <span v-if="row.lastStepName">{{ row.lastStepName }} <span class="muted num">({{ day(row.lastStepDate) }})</span></span>
                <span
                  v-else
                  class="muted"
                >—</span>
              </td>
              <td class="text-end num">
                {{ money(row.feesTotal) }}
              </td>
            </tr>
            <tr v-if="!flowRows.length">
              <td
                colspan="4"
                class="text-center muted py-8"
              >
                لا يوجد لهذا المحامي مدينون في هذه المحطة
              </td>
            </tr>
          </tbody>
        </VTable>
      </VCard>

      <VCard
        v-if="substituteSteps.length"
        class="mb-4"
      >
        <div class="section-title pa-4 pb-0">
          خطوات كُلِّف بها عن مدينين غير مسندين له ({{ substituteSteps.length }})
        </div>
        <div class="text-body-2 muted px-4 pb-2">
          المدين يبقى مسنداً لمحاميه؛ هذا المحامي أدّى الخطوة فقط.
        </div>
        <VTable density="compact">
          <thead>
            <tr>
              <th>التاريخ</th>
              <th>المدين</th>
              <th>الخطوة</th>
              <th>محاميه المسند</th>
              <th>ملاحظة</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="step in substituteSteps"
              :key="step.id"
            >
              <td class="num">
                {{ day(step.stepDate) }}
              </td>
              <td>
                <RouterLink :to="`/cases/${step.caseId}`">
                  {{ step.debtorName }}
                </RouterLink>
              </td>
              <td>{{ step.stepName }} <span class="muted">({{ step.flow === 'Execution' ? 'تنفيذ' : 'دعوى' }})</span></td>
              <td>{{ step.assignedLawyerName || '—' }}</td>
              <td>{{ step.note || '—' }}</td>
            </tr>
          </tbody>
        </VTable>
      </VCard>

      <VRow>
        <VCol cols="12">
          <VCard class="mb-4">
            <div class="section-title pa-4 pb-2">
              الرسوم التي دفعها ({{ fees.length }})
            </div>
            <VTable
              v-if="fees.length"
              density="compact"
            >
              <thead>
                <tr>
                  <th>التاريخ</th>
                  <th>المدين</th>
                  <th>البيان</th>
                  <th class="text-end">
                    المبلغ
                  </th>
                  <th />
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="tx in fees"
                  :key="tx.id"
                >
                  <td class="num">
                    {{ fromUtc(tx.atUtc) }}
                  </td>
                  <td>
                    <RouterLink :to="`/cases/${tx.caseId}`">
                      {{ tx.debtorName }}
                    </RouterLink>
                    <VChip
                      v-if="tx.outsideAssignment"
                      size="x-small"
                      color="warning"
                      variant="tonal"
                      class="ms-1"
                    >
                      غير مسند له
                    </VChip>
                  </td>
                  <td>{{ tx.note }}</td>
                  <td class="text-end num">
                    {{ money(tx.amount) }}
                  </td>
                  <td>
                    <VBtn
                      v-if="tx.hasImage"
                      icon="photo"
                      size="x-small"
                      variant="text"
                      title="عرض الصورة"
                      @click="showImage(tx)"
                    />
                  </td>
                </tr>
              </tbody>
            </VTable>
            <div
              v-else
              class="muted text-body-2 px-4 pb-4"
            >
              لم يدفع رسوماً بعد.
            </div>
          </VCard>

          <VCard>
            <div class="section-title pa-4 pb-2">
              حركات المحفظة مع القاصة ({{ received.length }})
            </div>
            <VTable
              v-if="received.length"
              density="compact"
            >
              <thead>
                <tr>
                  <th>التاريخ</th>
                  <th>الحركة</th>
                  <th>ملاحظة</th>
                  <th class="text-end">
                    المبلغ
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="tx in received"
                  :key="tx.id"
                >
                  <td class="num">
                    {{ fromUtc(tx.atUtc, true) }}
                  </td>
                  <td>{{ tx.kind === 'Transfer' ? 'استلام من القاصة' : 'إرجاع للقاصة' }}</td>
                  <td>{{ tx.note || '—' }}</td>
                  <td
                    class="text-end num font-weight-medium"
                    :class="tx.kind === 'Transfer' ? 'text-success' : 'text-error'"
                  >
                    {{ tx.kind === 'Transfer' ? '+' : '−' }}{{ money(tx.amount) }}
                  </td>
                </tr>
              </tbody>
            </VTable>
            <div
              v-else
              class="muted text-body-2 px-4 pb-4"
            >
              لم يستلم مبالغ بعد.
            </div>
          </VCard>
        </VCol>
      </VRow>
    </template>

    <AmountDialog
      v-model="transferDialog"
      :title="`تحويل مبلغ إلى محفظة ${lawyer.name || ''}`"
      :description="`يُخصم المبلغ من قاصة القانونية ويُضاف إلى محفظة المحامي.${cash === null ? '' : ` رصيد القاصة الآن ${money(cash)}.`}`"
      confirm-label="تحويل"
      :action="(amount, note) => api.transfer(lawyer.id, amount, note)"
      success-text="تم التحويل إلى المحفظة"
      @done="load"
    />
    <AmountDialog
      v-model="returnDialog"
      :title="`إرجاع مبلغ من محفظة ${lawyer.name || ''}`"
      description="يُخصم المبلغ من محفظة المحامي ويعود إلى قاصة القانونية."
      confirm-label="إرجاع"
      :max="Number(lawyer.balance) || 0"
      max-label="رصيد المحفظة"
      :action="(amount, note) => api.giveBack(lawyer.id, amount, note)"
      success-text="أُرجع المبلغ إلى القاصة"
      @done="load"
    />

    <!-- دفع رسم -->
    <VDialog
      v-model="feeDialog"
      max-width="560"
      :persistent="feeSaving"
    >
      <VCard>
        <VCardTitle class="pt-5 px-6">
          دفع رسم عن مدين
        </VCardTitle>
        <VCardText class="px-6">
          <p class="text-body-2 muted mb-4">
            يُخصم الرسم من محفظة {{ lawyer.name }} ويظهر في ملف المدين.
          </p>
          <VAutocomplete
            v-model="feeForm.caseId"
            :items="feeOptions"
            item-title="title"
            item-value="value"
            :label="feeForm.outside ? 'المدين (من غير المسندين له)' : 'المدين (من المسندين له)'"
            :loading="allCasesLoading"
            :no-data-text="feeForm.outside ? 'لا يوجد مدينون' : 'لا يوجد مدينون مسندون له'"
            class="mb-1"
            hide-details
          >
            <template #item="{ props: itemProps, item }">
              <VListItem
                v-bind="itemProps"
                :subtitle="item.raw.subtitle"
              />
            </template>
          </VAutocomplete>
          <VCheckbox
            v-model="feeForm.outside"
            label="الرسم عن مدين غير مسند لهذا المحامي (حالة غير معتادة)"
            color="warning"
            density="compact"
            hide-details
            class="mb-3"
          />
          <VTextField
            v-model.number="feeForm.amount"
            type="number"
            min="0"
            label="مبلغ الرسم (د.ع)"
            dir="ltr"
            :hint="feeForm.amount > 0 ? money(feeForm.amount) : ''"
            persistent-hint
            class="mb-3"
          />
          <VTextarea
            v-model="feeForm.note"
            label="بيان الرسم (ما هو هذا الرسم؟)"
            rows="2"
            class="mb-3"
            hide-details
          />
          <label class="upload">
            <VIcon
              icon="photo"
              size="20"
            />
            <span>{{ feeForm.image ? feeForm.image.name : 'إرفاق صورة الوصل (اختياري)' }}</span>
            <input
              type="file"
              accept="image/jpeg,image/png,image/webp"
              hidden
              @change="pickImage"
            >
          </label>
        </VCardText>
        <VCardActions class="px-6 pb-5">
          <VSpacer />
          <VBtn
            variant="text"
            :disabled="feeSaving"
            @click="feeDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="feeSaving"
            @click="saveFee"
          >
            تسجيل الرسم
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="editDialog"
      max-width="480"
    >
      <VCard>
        <VCardTitle class="pt-5 px-6">
          بيانات المحامي
        </VCardTitle>
        <VCardText class="px-6">
          <VTextField
            v-model="editForm.name"
            label="الاسم"
            class="mb-3"
            hide-details
          />
          <VTextField
            v-model="editForm.branch"
            label="الفرع"
            class="mb-3"
            hide-details
          />
          <VTextField
            v-model="editForm.phone"
            label="الهاتف"
            dir="ltr"
            class="mb-3"
            hide-details
          />
          <VTextarea
            v-model="editForm.notes"
            label="ملاحظات"
            rows="2"
            hide-details
          />
          <VSwitch
            v-model="editForm.isActive"
            label="فعّال (يظهر عند إسناد المدينين)"
            color="primary"
            hide-details
          />
        </VCardText>
        <VCardActions class="px-6 pb-5">
          <VSpacer />
          <VBtn
            variant="text"
            @click="editDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="editSaving"
            @click="saveEdit"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="imageDialog"
      max-width="820"
    >
      <VCard class="pa-2">
        <img
          :src="imageUrl"
          alt="صورة الرسم"
          style="max-inline-size: 100%;"
        >
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.back-link {
  display: inline-flex;
  align-items: center;
  margin-block-end: 6px;
  color: rgba(var(--v-theme-on-surface), 0.62);
  font-size: 0.85rem;
  gap: 4px;
  text-decoration: none;
}

.debtor {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 10px;
  border-block-start: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  color: inherit;
  gap: 12px;
  text-decoration: none;
}

.debtor:hover {
  background: rgba(var(--v-theme-primary), 0.04);
}

.upload {
  display: flex;
  align-items: center;
  padding: 12px 14px;
  border: 1px dashed rgba(var(--v-theme-on-surface), 0.3);
  border-radius: 10px;
  color: rgba(var(--v-theme-on-surface), 0.7);
  cursor: pointer;
  gap: 10px;
}

.upload:hover {
  border-color: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-primary));
}
</style>
