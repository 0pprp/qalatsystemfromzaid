<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { api, errorMessage } from '../api'
import { day, toast } from '../shared'

/**
 * الخطوات القادمة: steps that were given a date and are not done yet, soonest first.
 * Overdue ones (date passed, still not done) come first, in red.
 * Scope: everything, one lawyer, or one debtor.
 */
const props = defineProps({
  lawyerId: { type: [Number, String], default: null },
  caseId: { type: [Number, String], default: null },
  /** Rows shown before «عرض الكل». */
  limit: { type: Number, default: 5 },
  /** Bump to make the card reload (e.g. after a step is added elsewhere on the page). */
  refreshKey: { type: Number, default: 0 },
})

const emit = defineEmits(['changed'])

const rows = ref([])
const loading = ref(false)
const expanded = ref(false)
const busy = ref(null)

const overdue = computed(() => rows.value.filter(r => r.daysLeft < 0).length)
const shown = computed(() => expanded.value ? rows.value : rows.value.slice(0, props.limit))

async function load() {
  loading.value = true
  try {
    rows.value = await api.upcomingSteps({ lawyerId: props.lawyerId, caseId: props.caseId })
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل الخطوات القادمة'))
  }
  finally {
    loading.value = false
  }
}

function when(row) {
  const n = row.daysLeft
  if (n === 0)
    return 'اليوم'
  if (n === 1)
    return 'غداً'
  if (n > 1)
    return `بعد ${n} يوم`

  return n === -1 ? 'متأخرة يوماً' : `متأخرة ${-n} يوم`
}

async function markDone(row) {
  busy.value = row.id
  try {
    await api.stepDone(row.caseId, row.id, true)
    toast.ok(`تمت الخطوة «${row.stepName}»`)
    await load()
    emit('changed')
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحديث الخطوة'))
  }
  finally {
    busy.value = null
  }
}

watch(() => [props.lawyerId, props.caseId, props.refreshKey], load)
onMounted(load)
</script>

<template>
  <VCard>
    <div class="d-flex flex-wrap align-center ga-3 pa-4 pb-2">
      <VAvatar
        :color="overdue ? 'error' : 'primary'"
        variant="tonal"
        rounded="lg"
        size="36"
      >
        <VIcon
          icon="calendar"
          size="20"
        />
      </VAvatar>
      <div class="section-title">
        الخطوات القادمة
      </div>
      <VChip
        size="small"
        variant="tonal"
      >
        {{ rows.length }}
      </VChip>
      <VChip
        v-if="overdue"
        size="small"
        color="error"
        variant="tonal"
      >
        {{ overdue }} متأخرة
      </VChip>
    </div>
    <VProgressLinear
      v-if="loading"
      indeterminate
      color="primary"
    />

    <div
      v-if="!loading && !rows.length"
      class="muted text-body-2 px-4 pb-4"
    >
      لا توجد خطوات مجدولة. عند إضافة خطوة بتاريخ قادم تظهر هنا حتى تُنجز.
    </div>

    <div
      v-for="row in shown"
      :key="row.id"
      class="up"
      :class="{ 'up--late': row.daysLeft < 0, 'up--today': row.daysLeft === 0 }"
    >
      <div class="up__date">
        <div class="num font-weight-bold">
          {{ day(row.stepDate) }}
        </div>
        <div class="up__when">
          {{ when(row) }}
        </div>
      </div>
      <div class="up__body">
        <div>
          <strong>{{ row.stepName }}</strong>
          <span class="muted"> · {{ row.flow === 'Execution' ? 'التنفيذ' : 'الدعوى القضائية' }}</span>
        </div>
        <div class="text-body-2">
          <RouterLink
            v-if="!caseId"
            :to="`/cases/${row.caseId}`"
          >
            {{ row.debtorName }}
          </RouterLink>
          <!-- On a debtor's own page his lawyer is already shown; name one only when it is a substitute. -->
          <template v-if="row.lawyerName && !lawyerId && (!caseId || row.isSubstitute)">
            <span v-if="!caseId"> · </span>المحامي: {{ row.lawyerName }}
          </template>
          <VChip
            v-if="row.isSubstitute"
            size="x-small"
            color="warning"
            variant="tonal"
            class="ms-1"
          >
            لهذه الخطوة فقط
          </VChip>
        </div>
        <div
          v-if="row.note"
          class="text-caption muted"
        >
          {{ row.note }}
        </div>
      </div>
      <VBtn
        size="small"
        variant="tonal"
        color="success"
        prepend-icon="check"
        :loading="busy === row.id"
        @click="markDone(row)"
      >
        تمت
      </VBtn>
    </div>

    <div
      v-if="rows.length > limit"
      class="text-center pa-2"
    >
      <VBtn
        variant="text"
        size="small"
        @click="expanded = !expanded"
      >
        {{ expanded ? 'عرض أقل' : `عرض الكل (${rows.length})` }}
      </VBtn>
    </div>
  </VCard>
</template>

<style scoped>
.up {
  display: flex;
  align-items: center;
  padding: 10px 16px;
  border-block-start: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  border-inline-start: 3px solid transparent;
  gap: 14px;
}

.up--today {
  border-inline-start-color: rgb(var(--v-theme-warning));
}

.up--late {
  border-inline-start-color: rgb(var(--v-theme-error));
  background: rgba(var(--v-theme-error), 0.04);
}

.up__date {
  flex: 0 0 104px;
}

.up__when {
  color: rgba(var(--v-theme-on-surface), 0.62);
  font-size: 0.8rem;
}

.up--late .up__when {
  color: rgb(var(--v-theme-error));
  font-weight: 700;
}

.up--today .up__when {
  color: rgb(var(--v-theme-warning));
  font-weight: 700;
}

.up__body {
  flex: 1 1 auto;
  min-inline-size: 0;
}
</style>
