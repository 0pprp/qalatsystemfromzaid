<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, errorMessage } from '../api'
import FlowBar from '../components/FlowBar.vue'
import MoveDialog from '../components/MoveDialog.vue'
import StepDialog from '../components/StepDialog.vue'
import UpcomingSteps from '../components/UpcomingSteps.vue'
import { MOVE_LABEL, day, daysSince, flowOf, fromUtc, money, nextStage, stageMeta, toast } from '../shared'

const route = useRoute()
const router = useRouter()

const stage = ref(String(route.query.stage || 'Recent'))
const search = ref('')
const searching = computed(() => search.value.trim().length >= 2)
const loading = ref(false)
const rows = ref([])
const counts = ref({})
const settings = ref({})
const stepTypes = ref([])

const meta = computed(() => stageMeta(stage.value))

// الدعوى القضائية والتنفيذ: each step is a card holding the debtors whose latest step it is.
const stepFilter = ref('all')
const showSteps = computed(() => Boolean(flowOf(stage.value)) && !searching.value)

const stepCards = computed(() => {
  if (!showSteps.value)
    return []
  const flow = flowOf(stage.value)
  const countOf = id => rows.value.filter(r => r.lastStepTypeId === id).length
  const types = stepTypes.value
    .filter(t => t.flow === flow && (t.isActive || countOf(t.id) > 0))
    .map(t => ({ key: t.id, name: t.name, count: countOf(t.id), mandatory: t.isMandatory }))

  return [
    { key: 'none', name: 'لم تبدأ الخطوات', count: rows.value.filter(r => !r.lastStepTypeId).length, muted: true },
    ...types,
  ]
})

const shownRows = computed(() => {
  if (!showSteps.value || stepFilter.value === 'all')
    return rows.value
  if (stepFilter.value === 'none')
    return rows.value.filter(r => !r.lastStepTypeId)

  return rows.value.filter(r => r.lastStepTypeId === stepFilter.value)
})
const closedCount = computed(() => counts.value.Closed?.count ?? 0)

async function loadBoard() {
  try {
    const board = await api.board()

    counts.value = Object.fromEntries((board.stages || []).map(s => [s.stage, s]))
    settings.value = board.settings || {}
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل المسار'))
  }
}

async function loadRows() {
  loading.value = true
  try {
    rows.value = searching.value
      ? await api.cases({ q: search.value.trim() })
      : await api.cases({ stage: stage.value })
  }
  catch (e) {
    rows.value = []
    toast.error(errorMessage(e, 'تعذر تحميل الأسماء'))
  }
  finally {
    loading.value = false
  }
}

const refresh = () => Promise.all([loadBoard(), loadRows()])

watch(stage, value => {
  stepFilter.value = 'all'
  search.value = ''
  router.replace({ query: { stage: value } })
  loadRows()
})

// A link such as /?stage=Execution opened while the board is already on screen.
watch(() => route.query.stage, value => {
  if (value && value !== stage.value)
    stage.value = String(value)
})

let searchTimer = null

watch(search, () => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(loadRows, 350)
})

// —— نقل ——
const moveDialog = ref(false)
const moveCase = ref(null)
const moveTo = ref('')

function askMove(row, to) {
  moveCase.value = row
  moveTo.value = to
  moveDialog.value = true
}

// —— خطوة ——
const stepDialog = ref(false)
const stepCase = ref(null)
const upcomingKey = ref(0)

function askStep(row) {
  stepCase.value = row
  stepDialog.value = true
}

const open = row => router.push(`/cases/${row.id}`)

onMounted(async () => {
  await refresh()
  try {
    stepTypes.value = await api.stepTypes()
  }
  catch { /* the step dialog reports it */ }
})
</script>

<template>
  <div class="page">
    <div class="d-flex flex-wrap align-center justify-space-between ga-3 mb-4">
      <div>
        <h1 class="page-title">
          مسار المدينين
        </h1>
        <div class="text-body-2 muted">
          كل مدين موجود في محطة واحدة من هذا المسار. اختر محطة لعرض أسمائها.
        </div>
      </div>
      <VTextField
        v-model="search"
        placeholder="بحث بالاسم أو الهاتف أو كود الارشيف"
        prepend-inner-icon="search"
        density="compact"
        hide-details
        clearable
        style="max-inline-size: 340px; min-inline-size: 260px;"
      />
    </div>

    <FlowBar
      v-model="stage"
      :counts="counts"
      class="mb-2"
    />
    <div class="d-flex justify-end mb-4">
      <VBtn
        variant="text"
        size="small"
        :color="stage === 'Closed' ? 'success' : undefined"
        prepend-icon="circle-check"
        @click="stage = 'Closed'"
      >
        الملفات المغلقة ({{ closedCount }})
      </VBtn>
    </div>

    <UpcomingSteps
      :refresh-key="upcomingKey"
      class="mb-4"
      @changed="loadRows"
    />

    <VCard>
      <div class="d-flex flex-wrap align-center ga-3 pa-4">
        <VAvatar
          :color="searching ? 'secondary' : meta.color"
          variant="tonal"
          rounded="lg"
        >
          <VIcon :icon="searching ? 'search' : meta.icon" />
        </VAvatar>
        <div>
          <div class="section-title">
            {{ searching ? `نتائج البحث عن «${search.trim()}»` : meta.label }}
          </div>
          <div class="text-body-2 muted">
            {{ searching ? 'من كل المحطات المفتوحة' : meta.hint }}
          </div>
        </div>
        <VSpacer />
        <VChip variant="tonal">
          <span class="num">{{ shownRows.length }}</span>&nbsp;اسم
        </VChip>
      </div>

      <div
        v-if="showSteps"
        class="steps"
      >
        <button
          type="button"
          class="steps__card"
          :class="{ 'steps__card--active': stepFilter === 'all' }"
          @click="stepFilter = 'all'"
        >
          <div class="steps__name">
            كل الخطوات
          </div>
          <div class="steps__count num">
            {{ rows.length }}
          </div>
        </button>
        <button
          v-for="(card, index) in stepCards"
          :key="card.key"
          type="button"
          class="steps__card"
          :class="{ 'steps__card--active': stepFilter === card.key, 'steps__card--muted': card.muted }"
          @click="stepFilter = card.key"
        >
          <div class="steps__name">
            <span
              v-if="!card.muted"
              class="steps__index num"
            >{{ index }}</span>
            {{ card.name }}
            <span
              v-if="card.mandatory"
              class="steps__must"
            >إلزامية</span>
          </div>
          <div class="steps__count num">
            {{ card.count }}
          </div>
        </button>
      </div>
      <VDivider />
      <VProgressLinear
        v-if="loading"
        indeterminate
        color="primary"
      />

      <VTable hover>
        <thead>
          <tr>
            <th>المدين</th>
            <th v-if="searching">
              المحطة
            </th>
            <th>المحامي</th>
            <th class="text-end">
              باقي الدين
            </th>
            <th>
              {{ stage === 'Decisions' && !searching ? 'القرار' : (flowOf(stage) && !searching ? 'آخر خطوة' : 'ملاحظات') }}
            </th>
            <th>في المحطة منذ</th>
            <th />
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in shownRows"
            :key="row.id"
            class="row-link"
            @click="open(row)"
          >
            <td>
              <div class="font-weight-medium">
                {{ row.fullName }}
              </div>
              <div class="text-caption muted">
                <span class="num">{{ row.phone || '' }}</span>
                <span v-if="row.cities"> · {{ row.cities }}</span>
                <span v-if="row.accountCount > 1"> · {{ row.accountCount }} حسابات</span>
              </div>
              <div v-if="row.archiveCodes">
                <span
                  v-for="code in row.archiveCodes.split(' · ')"
                  :key="code"
                  class="code me-1"
                >{{ code }}</span>
              </div>
            </td>
            <td v-if="searching">
              <VChip
                size="small"
                :color="stageMeta(row.stage).color"
                variant="tonal"
              >
                {{ stageMeta(row.stage).label }}
              </VChip>
            </td>
            <td>
              <span v-if="row.lawyerName">{{ row.lawyerName }}</span>
              <span
                v-else
                class="muted"
              >غير مسند</span>
            </td>
            <td class="text-end num font-weight-medium">
              {{ money(row.remainingSnapshot) }}
            </td>
            <td style="max-inline-size: 280px;">
              <template v-if="row.stage === 'Decisions'">
                <span v-if="row.decisionText">{{ row.decisionText }} <span class="muted num">({{ day(row.decisionDate) }})</span></span>
                <span
                  v-else
                  class="muted"
                >لم يُسجَّل نص القرار</span>
              </template>
              <template v-else-if="flowOf(row.stage)">
                <span v-if="row.lastStepName">{{ row.lastStepName }} <span class="muted num">({{ day(row.lastStepDate) }})</span></span>
                <span
                  v-else
                  class="muted"
                >لا توجد خطوات بعد</span>
              </template>
              <template v-else-if="row.stage === 'Closed'">
                {{ row.closeReason || '—' }}
              </template>
              <template v-else>
                {{ row.note || '—' }}
              </template>
            </td>
            <td>
              <span class="num">{{ fromUtc(row.stageEnteredAtUtc) }}</span>
              <div
                v-if="row.stage !== 'Closed'"
                class="text-caption muted"
              >
                {{ daysSince(row.stageEnteredAtUtc) }} يوم
              </div>
            </td>
            <td
              class="text-end text-no-wrap"
              @click.stop
            >
              <VBtn
                v-if="flowOf(row.stage)"
                size="small"
                variant="tonal"
                prepend-icon="plus"
                class="me-2"
                @click="askStep(row)"
              >
                خطوة
              </VBtn>
              <span
                v-if="row.stage === 'Judicial' && row.missingMandatory > 0"
                class="text-caption text-error"
              >
                باقي {{ row.missingMandatory }} خطوة إلزامية
              </span>
              <VBtn
                v-else-if="nextStage(row.stage)"
                size="small"
                color="primary"
                variant="flat"
                append-icon="arrow-left"
                @click="askMove(row, nextStage(row.stage).key)"
              >
                {{ MOVE_LABEL[row.stage] }}
              </VBtn>
            </td>
          </tr>
          <tr v-if="!loading && !shownRows.length">
            <td
              :colspan="searching ? 7 : 6"
              class="text-center muted py-10"
            >
              {{ searching ? 'لا يوجد مدين بهذا الاسم' : (showSteps && stepFilter !== 'all' ? 'لا توجد أسماء في هذه الخطوة' : 'لا توجد أسماء في هذه المحطة') }}
            </td>
          </tr>
        </tbody>
      </VTable>
    </VCard>

    <MoveDialog
      v-model="moveDialog"
      :legal-case="moveCase"
      :to="moveTo"
      :settings="settings"
      @done="refresh"
    />
    <StepDialog
      v-model="stepDialog"
      :legal-case="stepCase"
      :step-types="stepTypes"
      @done="loadRows(); upcomingKey++"
    />
  </div>
</template>

<style scoped>
.steps {
  display: grid;
  padding: 0 16px 16px;
  gap: 10px;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
}

.steps__card {
  padding: 12px 14px;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.14);
  border-radius: 10px;
  background: rgb(var(--v-theme-surface));
  color: rgb(var(--v-theme-on-surface));
  cursor: pointer;
  font-family: inherit;
  text-align: start;
  transition: border-color 0.15s, background 0.15s;
}

.steps__card:hover {
  border-color: rgb(var(--v-theme-primary));
}

.steps__card:focus-visible {
  outline: 2px solid rgb(var(--v-theme-accent));
  outline-offset: 2px;
}

.steps__card--muted {
  background: rgba(var(--v-theme-on-surface), 0.03);
}

.steps__card--active {
  border-color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.08);
  box-shadow: inset 0 0 0 1px rgb(var(--v-theme-primary));
}

.steps__name {
  display: flex;
  align-items: center;
  font-size: 0.88rem;
  font-weight: 500;
  gap: 8px;
}

.steps__index {
  display: inline-grid;
  flex: 0 0 20px;
  border-radius: 50%;
  background: rgba(var(--v-theme-primary), 0.12);
  block-size: 20px;
  color: rgb(var(--v-theme-primary));
  font-size: 0.72rem;
  font-weight: 700;
  place-items: center;
}

.steps__must {
  padding: 0 6px;
  border-radius: 4px;
  background: rgba(var(--v-theme-error), 0.12);
  color: rgb(var(--v-theme-error));
  font-size: 0.68rem;
  font-weight: 700;
  margin-inline-start: auto;
}

.steps__count {
  font-size: 1.5rem;
  font-weight: 700;
  line-height: 1.4;
}
</style>
