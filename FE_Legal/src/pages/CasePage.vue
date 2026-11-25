<script setup>
import { computed, onMounted, ref } from 'vue'
import { api, errorMessage } from '../api'
import FlowBar from '../components/FlowBar.vue'
import MoveDialog from '../components/MoveDialog.vue'
import StepDialog from '../components/StepDialog.vue'
import UpcomingSteps from '../components/UpcomingSteps.vue'
import { CHARGE_KIND, EVENT_KIND, MOVE_LABEL, STAGES, day, flowOf, fromUtc, money, nextStage, stageIndex, stageMeta, toast } from '../shared'

const props = defineProps({ id: { type: [String, Number], required: true } })

const loading = ref(false)
const data = ref(null)
const settings = ref({})
const stepTypes = ref([])
const lawyers = ref([])

const c = computed(() => data.value?.legalCase || {})
const extras = computed(() => data.value?.extras || { steps: [], events: [], fees: [], charges: [], payments: [], requests: [] })
const totals = computed(() => data.value?.totals || { remaining: 0, penaltiesDue: 0, totalDue: 0 })
const stage = computed(() => c.value.stage || '')
const closed = computed(() => stage.value === 'Closed')
const next = computed(() => nextStage(stage.value))
const feesTotal = computed(() => extras.value.fees.reduce((sum, f) => sum + Number(f.amount || 0), 0))
const judicialSteps = computed(() => extras.value.steps.filter(s => s.flow === 'Judicial'))
const executionSteps = computed(() => extras.value.steps.filter(s => s.flow === 'Execution'))
const payments = computed(() => extras.value.payments.filter(p => Number(p.debtAmount) + Number(p.penaltyAmount) > 0))
/** Mandatory steps of the flow he is in, each marked done when logged at least once. */
const mandatory = computed(() => {
  const flow = flowOf(stage.value)

  return stepTypes.value
    .filter(t => t.flow === flow && t.isActive && t.isMandatory)
    .map(t => ({ id: t.id, name: t.name, done: extras.value.steps.some(s => s.stepTypeId === t.id && s.isDone !== false) }))
})

const missingMandatory = computed(() => mandatory.value.filter(m => !m.done))
const blocked = computed(() => stage.value === 'Judicial' && missingMandatory.value.length > 0)
/** Every station before the current one: the debtor can be returned to any of them. */
const backTargets = computed(() => closed.value ? [] : STAGES.slice(0, Math.max(0, stageIndex(stage.value))).reverse())

async function load() {
  loading.value = true
  try {
    data.value = await api.legalCase(props.id)
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل ملف المدين'))
  }
  finally {
    loading.value = false
  }
}

// —— نقل وخطوات ——
const moveDialog = ref(false)
const moveTo = ref('')
const stepDialog = ref(false)
const upcomingKey = ref(0)

async function setDone(step, done) {
  try {
    await api.stepDone(c.value.id, step.id, done)
    upcomingKey.value++
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحديث الخطوة'))
  }
}

function askMove(to) {
  moveTo.value = to
  moveDialog.value = true
}

async function removeStep(step) {
  if (!window.confirm(`حذف الخطوة «${step.stepName}» بتاريخ ${day(step.stepDate)}؟`))
    return
  try {
    await api.deleteStep(c.value.id, step.id)
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحذف'))
  }
}

// —— المحامي ——
async function setLawyer(lawyerId) {
  try {
    await api.assignLawyer(c.value.id, lawyerId)
    toast.ok(lawyerId ? 'أُسند المحامي' : 'أُلغي إسناد المحامي')
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر إسناد المحامي'))
  }
}

// —— حوار نصي عام: ابطال / تسديد كامل / إغلاق / ملاحظة ——
const textDialog = ref(false)
const textForm = ref({ title: '', description: '', label: '', value: '', confirm: '', color: 'primary', run: null })
const textSaving = ref(false)

function openText(config) {
  textForm.value = { value: '', color: 'primary', description: '', ...config }
  textDialog.value = true
}

async function submitText() {
  textSaving.value = true
  try {
    await textForm.value.run(textForm.value.value)
    textDialog.value = false
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحفظ'))
  }
  finally {
    textSaving.value = false
  }
}

function askClose() {
  openText({
    title: 'إغلاق الملف',
    description: 'يُغلق الملف تلقائياً عند التسديد الكامل. استخدم الإغلاق اليدوي للحالات الأخرى فقط.',
    label: 'سبب الإغلاق',
    confirm: 'إغلاق الملف',
    color: 'error',
    run: async reason => {
      if (!String(reason || '').trim())
        throw new Error('اكتب سبب الإغلاق')
      await api.close(c.value.id, reason)
      toast.ok('أُغلق الملف')
    },
  })
}

// The «تسديد» star: put on and taken off by hand.
const starring = ref(false)

async function togglePaying() {
  starring.value = true
  try {
    const paying = !c.value.isPaying

    await api.paying(c.value.id, paying)
    toast.ok(paying ? 'أُشّر المدين «تسديد»' : 'أُزيل تأشير التسديد')
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تغيير التأشير'))
  }
  finally {
    starring.value = false
  }
}

function editNote() {
  openText({
    title: 'ملاحظات الملف',
    label: 'ملاحظات',
    value: c.value.note || '',
    confirm: 'حفظ',
    run: async note => {
      await api.note(c.value.id, note)
    },
  })
}

// —— القرار ——
const decisionDialog = ref(false)
const decisionText = ref('')
const decisionDate = ref('')
const decisionSaving = ref(false)

function openDecision() {
  decisionText.value = c.value.decisionText || ''
  decisionDate.value = String(c.value.decisionDate || '').slice(0, 10)
  decisionDialog.value = true
}

async function saveDecision() {
  decisionSaving.value = true
  try {
    await api.decision(c.value.id, decisionText.value, decisionDate.value || null)
    decisionDialog.value = false
    toast.ok('حُفظ القرار')
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر حفظ القرار'))
  }
  finally {
    decisionSaving.value = false
  }
}

// —— صورة الرسم ——
const imageDialog = ref(false)
const imageUrl = ref('')

async function showImage(fee) {
  try {
    const blob = await api.feeImage(fee.id)

    if (imageUrl.value)
      URL.revokeObjectURL(imageUrl.value)
    imageUrl.value = URL.createObjectURL(blob)
    imageDialog.value = true
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر فتح الصورة'))
  }
}

onMounted(async () => {
  await load()
  try {
    const [board, types, allLawyers] = await Promise.all([api.board(), api.stepTypes(), api.lawyers()])

    settings.value = board.settings || {}
    stepTypes.value = types
    lawyers.value = allLawyers
  }
  catch { /* each dialog reports its own failure */ }
})
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
        :to="{ path: '/', query: { stage } }"
        class="back-link"
      >
        <VIcon
          icon="arrow-right"
          size="16"
        />
        {{ stageMeta(stage).label }}
      </RouterLink>

      <div class="d-flex flex-wrap align-center justify-space-between ga-3 mb-4">
        <div>
          <h1 class="page-title">
            {{ c.fullName }}
            <VBtn
              :icon="c.isPaying ? 'star-filled' : 'star'"
              :color="c.isPaying ? 'warning' : undefined"
              variant="text"
              size="small"
              :loading="starring"
              :title="c.isPaying ? 'إزالة تأشير التسديد' : 'تأشير تسديد'"
              @click="togglePaying"
            />
            <VChip
              v-if="c.isPaying"
              size="small"
              color="warning"
              variant="tonal"
            >
              تسديد
            </VChip>
          </h1>
          <div class="text-body-2 muted">
            <span class="num">{{ c.phone || 'بلا هاتف' }}</span>
            <span v-if="data.person?.address"> · {{ data.person.address }}</span>
            <span v-if="c.cities"> · {{ c.cities }}</span>
            <template v-if="c.archiveCodes">
              ·
              <span
                v-for="code in c.archiveCodes.split(' · ')"
                :key="code"
                class="code me-1"
              >{{ code }}</span>
            </template>
            · استُلم <span class="num">{{ fromUtc(c.sentAtUtc) }}</span>
          </div>
        </div>
        <div class="d-flex flex-wrap ga-2">
          <VMenu v-if="backTargets.length">
            <template #activator="{ props: menuProps }">
              <VBtn
                v-bind="menuProps"
                variant="text"
                prepend-icon="arrow-back-up"
                append-icon="chevron-down"
              >
                إرجاع إلى محطة سابقة
              </VBtn>
            </template>
            <VList density="compact">
              <VListItem
                v-for="target in backTargets"
                :key="target.key"
                :prepend-icon="target.icon"
                :title="target.label"
                @click="askMove(target.key)"
              />
            </VList>
          </VMenu>
          <VBtn
            v-if="flowOf(stage)"
            variant="tonal"
            prepend-icon="plus"
            @click="stepDialog = true"
          >
            إضافة خطوة
          </VBtn>
          <VBtn
            v-if="next"
            color="primary"
            variant="flat"
            size="large"
            append-icon="arrow-left"
            :disabled="blocked"
            :title="blocked ? 'أكمل الخطوات الإلزامية أولاً' : undefined"
            @click="askMove(next.key)"
          >
            {{ MOVE_LABEL[stage] }}
          </VBtn>
        </div>
      </div>

      <!-- المسار -->
      <VCard class="pa-4 mb-4">
        <FlowBar :current="stage" />
        <div
          v-if="closed"
          class="text-body-2 mt-3"
        >
          <VIcon
            icon="circle-check"
            color="success"
            size="18"
          />
          الملف مغلق منذ <span class="num">{{ fromUtc(c.closedAtUtc) }}</span> — {{ c.closeReason }}
        </div>
      </VCard>

      <VAlert
        v-if="data.staleCities?.length"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        تعذر الاتصال بـ {{ data.staleCities.join('، ') }} — أرصدة حساباتها من آخر تحديث.
      </VAlert>
      <VRow>
        <!-- العمود الرئيسي -->
        <VCol
          cols="12"
          lg="8"
        >
          <!-- المبالغ -->
          <VCard class="mb-4">
            <VRow no-gutters>
              <VCol
                cols="6"
                md="3"
                class="stat"
              >
                <div class="stat__label">
                  باقي الدين
                </div>
                <div class="stat__value num">
                  {{ money(totals.remaining) }}
                </div>
              </VCol>
              <VCol
                cols="6"
                md="3"
                class="stat"
              >
                <div class="stat__label">
                  غرامات مستحقة
                </div>
                <div class="stat__value num">
                  {{ money(totals.penaltiesDue) }}
                </div>
              </VCol>
              <VCol
                cols="6"
                md="3"
                class="stat"
              >
                <div class="stat__label">
                  المطلوب من المدين
                </div>
                <div class="stat__value num text-primary">
                  {{ money(totals.totalDue) }}
                </div>
              </VCol>
              <VCol
                cols="6"
                md="3"
                class="stat"
              >
                <div class="stat__label">
                  رسوم دفعها المحامون
                </div>
                <div class="stat__value num">
                  {{ money(feesTotal) }}
                </div>
              </VCol>
            </VRow>
            <VDivider />
            <div class="text-caption muted pa-3">
              التسديدات يسجّلها محاسب القانونية في لوحة الشركة. الغرامات: {{ extras.charges.map(x => `${CHARGE_KIND[x.kind] || x.kind} ${money(x.amount)}`).join('، ') || 'لا توجد' }}.
            </div>
          </VCard>

          <!-- الخطوات -->
          <VCard class="mb-4">
            <div class="d-flex align-center justify-space-between pa-4 pb-2">
              <div class="section-title">
                خطوات الدعوى والتنفيذ
              </div>
              <VBtn
                v-if="flowOf(stage)"
                size="small"
                variant="tonal"
                prepend-icon="plus"
                @click="stepDialog = true"
              >
                إضافة خطوة
              </VBtn>
            </div>
            <div
              v-if="mandatory.length"
              class="px-4 pb-3"
            >
              <div class="text-body-2 muted mb-1">
                الخطوات الإلزامية{{ stage === 'Judicial' ? ' (لا يُنقل إلى القرارات قبل إكمالها)' : '' }}
              </div>
              <VChip
                v-for="m in mandatory"
                :key="m.id"
                size="small"
                :color="m.done ? 'success' : 'error'"
                variant="tonal"
                :prepend-icon="m.done ? 'check' : 'clock'"
                class="me-1 mb-1"
              >
                {{ m.name }}
              </VChip>
            </div>
            <div
              v-if="!extras.steps.length"
              class="muted text-body-2 px-4 pb-4"
            >
              {{ flowOf(stage) ? 'لا توجد خطوات بعد. سجّل أول خطوة.' : 'تُسجَّل الخطوات بعد اقامة الدعوى القضائية.' }}
            </div>
            <template
              v-for="group in [{ title: 'الدعوى القضائية', rows: judicialSteps, icon: 'gavel' }, { title: 'التنفيذ', rows: executionSteps, icon: 'hammer' }]"
              :key="group.title"
            >
              <div
                v-if="group.rows.length"
                class="px-4 pb-3"
              >
                <div class="text-body-2 font-weight-medium mb-2">
                  <VIcon
                    :icon="group.icon"
                    size="16"
                  />
                  {{ group.title }} ({{ group.rows.length }})
                </div>
                <ol class="steps">
                  <li
                    v-for="(step, i) in group.rows"
                    :key="step.id"
                    class="steps__item"
                  >
                    <span class="steps__index num">{{ i + 1 }}</span>
                    <div class="steps__body">
                      <div>
                        <strong>{{ step.stepName }}</strong>
                        <span class="muted num ms-2">{{ day(step.stepDate) }}</span>
                        <VChip
                          v-if="step.isDone === false"
                          size="x-small"
                          :color="step.isOverdue ? 'error' : 'info'"
                          variant="tonal"
                          class="ms-2"
                        >
                          {{ step.isOverdue ? 'متأخرة' : 'مجدولة' }}
                        </VChip>
                      </div>
                      <div
                        v-if="step.note"
                        class="text-body-2"
                      >
                        {{ step.note }}
                      </div>
                      <div class="text-caption muted">
                        <template v-if="step.lawyerName">
                          المحامي: {{ step.lawyerName }}
                          <VChip
                            v-if="step.isSubstitute"
                            size="x-small"
                            color="warning"
                            variant="tonal"
                            class="mx-1"
                          >
                            لهذه الخطوة فقط
                          </VChip>
                          ·
                        </template>
                        سجّلها {{ step.createdBy }}
                      </div>
                    </div>
                    <VBtn
                      v-if="!closed && step.isDone === false"
                      size="x-small"
                      variant="tonal"
                      color="success"
                      @click="setDone(step, true)"
                    >
                      تمت
                    </VBtn>
                    <VBtn
                      v-if="!closed"
                      icon="trash"
                      size="x-small"
                      variant="text"
                      title="حذف الخطوة"
                      @click="removeStep(step)"
                    />
                  </li>
                </ol>
              </div>
            </template>
          </VCard>

          <!-- الحسابات -->
          <VCard class="mb-4">
            <div class="section-title pa-4 pb-2">
              حسابات المدين في الشركة ({{ data.accounts.length }})
            </div>
            <VTable density="compact">
              <thead>
                <tr>
                  <th>كود ارشيف القانونية</th>
                  <th>الاسم في الفرع</th>
                  <th>الفرع / القائمة</th>
                  <th>المواد</th>
                  <th>تاريخ البيع</th>
                  <th>آخر تسديد</th>
                  <th class="text-end">
                    المبلغ
                  </th>
                  <th class="text-end">
                    الباقي
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="a in data.accounts"
                  :key="a.id"
                >
                  <td>
                    <span
                      v-if="a.archiveCode"
                      class="code"
                    >{{ a.archiveCode }}</span>
                    <span
                      v-else
                      class="muted"
                    >—</span>
                  </td>
                  <td>{{ a.customerName }}</td>
                  <td>{{ a.cityName }} · {{ a.listName || '—' }}</td>
                  <td>{{ a.items || '—' }}</td>
                  <td class="num">
                    {{ day(a.saleDate) }}
                  </td>
                  <td class="num">
                    {{ day(a.lastPaymentDate) }}
                  </td>
                  <td class="text-end num">
                    {{ money(a.total) }}
                  </td>
                  <td class="text-end num font-weight-medium">
                    {{ money(a.remaining) }}
                  </td>
                </tr>
              </tbody>
            </VTable>
          </VCard>

          <!-- الرسوم -->
          <VCard class="mb-4">
            <div class="d-flex align-center justify-space-between pa-4 pb-2">
              <div class="section-title">
                الرسوم المدفوعة على هذا المدين
              </div>
              <div class="font-weight-bold num">
                {{ money(feesTotal) }}
              </div>
            </div>
            <VTable
              v-if="extras.fees.length"
              density="compact"
            >
              <thead>
                <tr>
                  <th>التاريخ</th>
                  <th>المحامي</th>
                  <th>البيان</th>
                  <th class="text-end">
                    المبلغ
                  </th>
                  <th />
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="fee in extras.fees"
                  :key="fee.id"
                >
                  <td class="num">
                    {{ fromUtc(fee.atUtc) }}
                  </td>
                  <td>
                    <RouterLink :to="`/lawyers/${fee.lawyerId}`">
                      {{ fee.lawyerName }}
                    </RouterLink>
                    <VChip
                      v-if="fee.outsideAssignment"
                      size="x-small"
                      variant="tonal"
                      color="warning"
                      class="ms-1"
                    >
                      غير مسند له
                    </VChip>
                  </td>
                  <td>{{ fee.note }}</td>
                  <td class="text-end num">
                    {{ money(fee.amount) }}
                  </td>
                  <td>
                    <VBtn
                      v-if="fee.hasImage"
                      icon="photo"
                      size="x-small"
                      variant="text"
                      title="عرض الصورة"
                      @click="showImage(fee)"
                    />
                  </td>
                </tr>
              </tbody>
            </VTable>
            <div
              v-else
              class="muted text-body-2 px-4 pb-4"
            >
              لا توجد رسوم. تُسجَّل الرسوم من صفحة المحامي.
            </div>
          </VCard>

          <!-- التسديدات -->
          <VCard>
            <div class="section-title pa-4 pb-2">
              تسديدات المدين (من لوحة الشركة)
            </div>
            <VTable
              v-if="payments.length"
              density="compact"
            >
              <thead>
                <tr>
                  <th>التاريخ</th>
                  <th>النوع</th>
                  <th class="text-end">
                    من الدين
                  </th>
                  <th class="text-end">
                    من الغرامات
                  </th>
                  <th>بواسطة</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="p in payments"
                  :key="p.id"
                >
                  <td class="num">
                    {{ fromUtc(p.atUtc, true) }}
                  </td>
                  <td>
                    {{ p.kind === 'Discount' ? 'خصم' : 'تسديد' }}
                    <span
                      v-if="p.receiptNumber"
                      class="text-caption muted num"
                    >{{ p.receiptNumber }}</span>
                  </td>
                  <td class="text-end num">
                    {{ money(p.debtAmount) }}
                  </td>
                  <td class="text-end num">
                    {{ money(p.penaltyAmount) }}
                  </td>
                  <td>{{ p.actor }}</td>
                </tr>
              </tbody>
            </VTable>
            <div
              v-else
              class="muted text-body-2 px-4 pb-4"
            >
              لم يسدد شيئاً عبر القانونية بعد.
            </div>
          </VCard>
        </VCol>

        <!-- العمود الجانبي -->
        <VCol
          cols="12"
          lg="4"
        >
          <UpcomingSteps
            :case-id="c.id"
            :refresh-key="upcomingKey"
            class="mb-4"
            @changed="load"
          />
          <VCard class="pa-4 mb-4">
            <div class="section-title mb-3">
              المحامي المسند
            </div>
            <VAutocomplete
              :model-value="c.lawyerId"
              :items="lawyers.filter(l => l.isActive || l.id === c.lawyerId)"
              item-title="name"
              item-value="id"
              placeholder="اختر محامياً"
              no-data-text="لا يوجد محامون — أضفهم من صفحة المحامون"
              :clearable="stage === 'Recent' || stage === 'Archive'"
              hide-details
              :disabled="closed"
              @update:model-value="setLawyer"
            />
            <RouterLink
              v-if="c.lawyerId"
              :to="`/lawyers/${c.lawyerId}`"
              class="text-body-2 d-inline-block mt-2"
            >
              فتح ملف المحامي
            </RouterLink>
          </VCard>

          <VCard class="pa-4 mb-4">
            <div class="d-flex align-center justify-space-between mb-2">
              <div class="section-title">
                القرار القضائي
              </div>
              <VBtn
                size="small"
                variant="text"
                prepend-icon="edit"
                :disabled="closed"
                @click="openDecision"
              >
                {{ c.decisionText ? 'تعديل' : 'تسجيل' }}
              </VBtn>
            </div>
            <div v-if="c.decisionText">
              <div style="white-space: pre-wrap;">
                {{ c.decisionText }}
              </div>
              <div class="text-caption muted num mt-1">
                {{ day(c.decisionDate) }}
              </div>
            </div>
            <div
              v-else
              class="muted text-body-2"
            >
              لم يُسجَّل قرار بعد.
            </div>
          </VCard>

          <VCard class="pa-4 mb-4">
            <div class="section-title mb-3">
              إجراءات على الملف
            </div>
            <div class="d-flex flex-column ga-2">
              <div class="text-caption muted">
                التسديد والخصم يسجّلهما محاسب القانونية من لوحة الشركة (تبويب التسديدات) لأي مدين وفي أي وقت.
              </div>
              <VBtn
                variant="text"
                prepend-icon="notes"
                block
                @click="editNote"
              >
                ملاحظات الملف
              </VBtn>
              <VBtn
                v-if="!closed"
                variant="text"
                color="error"
                prepend-icon="ban"
                block
                @click="askClose"
              >
                إغلاق الملف يدوياً
              </VBtn>
            </div>
            <div
              v-if="c.note"
              class="text-body-2 mt-3"
              style="white-space: pre-wrap;"
            >
              {{ c.note }}
            </div>
          </VCard>

          <VCard class="pa-4">
            <div class="section-title mb-3">
              سجل الملف
            </div>
            <div
              v-for="event in extras.events"
              :key="event.id"
              class="event"
            >
              <div class="event__dot" />
              <div>
                <div class="text-body-2">
                  <strong>{{ EVENT_KIND[event.kind] || event.kind }}</strong>
                  <span v-if="event.kind === 'Move' || event.kind === 'Close'">
                    : {{ stageMeta(event.fromStage).label }} ← {{ stageMeta(event.toStage).label }}
                  </span>
                </div>
                <div
                  v-if="event.note"
                  class="text-body-2"
                >
                  {{ event.note }}
                </div>
                <div class="text-caption muted">
                  {{ event.actor }} · <span class="num">{{ fromUtc(event.atUtc, true) }}</span>
                </div>
              </div>
            </div>
          </VCard>
        </VCol>
      </VRow>
    </template>

    <MoveDialog
      v-model="moveDialog"
      :legal-case="c"
      :to="moveTo"
      :settings="settings"
      @done="load"
    />
    <StepDialog
      v-model="stepDialog"
      :legal-case="c"
      :step-types="stepTypes"
      @done="load(); upcomingKey++"
    />

    <VDialog
      v-model="textDialog"
      max-width="520"
      :persistent="textSaving"
    >
      <VCard>
        <VCardTitle class="pt-5 px-6">
          {{ textForm.title }}
        </VCardTitle>
        <VCardText class="px-6">
          <p
            v-if="textForm.description"
            class="text-body-2 muted mb-4"
          >
            {{ textForm.description }}
          </p>
          <VTextarea
            v-model="textForm.value"
            :label="textForm.label"
            rows="3"
            autofocus
            hide-details
          />
        </VCardText>
        <VCardActions class="px-6 pb-5">
          <VSpacer />
          <VBtn
            variant="text"
            :disabled="textSaving"
            @click="textDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            :color="textForm.color"
            variant="flat"
            :loading="textSaving"
            @click="submitText"
          >
            {{ textForm.confirm }}
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="decisionDialog"
      max-width="560"
    >
      <VCard>
        <VCardTitle class="pt-5 px-6">
          القرار القضائي
        </VCardTitle>
        <VCardText class="px-6">
          <VTextarea
            v-model="decisionText"
            label="نص القرار"
            rows="4"
            class="mb-3"
            hide-details
          />
          <VTextField
            v-model="decisionDate"
            type="date"
            label="تاريخ القرار"
            hide-details
          />
        </VCardText>
        <VCardActions class="px-6 pb-5">
          <VSpacer />
          <VBtn
            variant="text"
            @click="decisionDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="decisionSaving"
            @click="saveDecision"
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

.back-link:hover {
  color: rgb(var(--v-theme-primary));
}

.steps {
  padding: 0;
  margin: 0;
  list-style: none;
}

.steps__item {
  display: flex;
  align-items: flex-start;
  padding-block: 8px;
  border-block-start: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  gap: 12px;
}

.steps__index {
  display: inline-grid;
  flex: 0 0 26px;
  border-radius: 50%;
  background: rgba(var(--v-theme-primary), 0.1);
  block-size: 26px;
  color: rgb(var(--v-theme-primary));
  font-size: 0.8rem;
  font-weight: 700;
  place-items: center;
}

.steps__body {
  flex: 1 1 auto;
}

.event {
  display: flex;
  padding-block-end: 12px;
  gap: 10px;
}

.event__dot {
  flex: 0 0 8px;
  border-radius: 50%;
  background: rgb(var(--v-theme-accent));
  block-size: 8px;
  margin-block-start: 8px;
}
</style>
