<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  CA_TARGET_STATUS,
  caDate,
  caErrorMessage,
  caIraqToday,
  caMinutes,
  caTargetsDay,
  caYmd,
} from '@/composables/customerAppApi'
import CaTargetHistoryDialog from '@/components/customer-app/CaTargetHistoryDialog.vue'

definePage({ meta: { layout: 'default' } })

const route = useRoute()
const router = useRouter()

const date = ref(/^\d{4}-\d{2}-\d{2}$/.test(String(route.query.date || '')) ? String(route.query.date) : caIraqToday())
const report = ref(null)
const loading = ref(false)
const error = ref('')
const filter = ref('all')
const historyAgent = ref(null)

async function load() {
  loading.value = true
  error.value = ''
  try {
    report.value = await caTargetsDay(date.value)
  }
  catch (e) {
    error.value = caErrorMessage(e, 'تعذر تحميل الأهداف')
    report.value = null
  }
  finally {
    loading.value = false
  }
}

watch(date, d => {
  router.replace({ query: { ...route.query, date: d } })
  load()
})

function shiftDay(n) {
  const d = new Date(`${date.value}T12:00:00`)

  d.setDate(d.getDate() + n)
  date.value = caYmd(d)
}

const rows = computed(() => visible.value.filter(a => filter.value === 'all' || a.result.status === filter.value))

// Agents who neither worked nor have a target are left out.
const visible = computed(() => (report.value?.agents || []).filter(a => a.result.status !== 'none' || a.result.worked))

const headers = [
  { title: 'المسوّق', key: 'fullName', sortable: false },
  { title: 'وقت العمل', key: 'work', sortable: false },
  { title: 'المحلات', key: 'pins', sortable: false, align: 'center' },
  { title: 'النتيجة', key: 'status', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end' },
]

function pct(r) {
  return r.targetMinutes > 0 ? Math.min(100, Math.round((r.workMinutes / r.targetMinutes) * 100)) : 0
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <div class="flex-grow-1">
        <h4 class="text-h4 mb-1 d-flex align-center gap-2">
          <VIcon
            icon="tabler-target-arrow"
            color="primary"
          />
          الأهداف اليومية
        </h4>
        <p class="text-medium-emphasis mb-0">
          وقت العمل اليومي لكل مسوّق مقابل الهدف. يُحسب وقت العمل من فترات تثبيت المحلات فقط (انقطاع {{ report?.breakGapMinutes ?? 15 }} دقيقة دون تثبيت ينهي الفترة)، ولا يرى المسوّق الهدف.
        </p>
      </div>
      <div class="d-flex align-center gap-1">
        <IconBtn @click="shiftDay(-1)">
          <VIcon icon="tabler-chevron-right" />
        </IconBtn>
        <VTextField
          v-model="date"
          type="date"
          density="compact"
          hide-details
          style="min-inline-size: 170px;"
        />
        <IconBtn @click="shiftDay(1)">
          <VIcon icon="tabler-chevron-left" />
        </IconBtn>
      </div>
    </div>

    <VAlert
      v-if="error"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ error }}
    </VAlert>

    <VAlert
      v-if="report && report.defaultTargetMinutes === 0"
      type="info"
      variant="tonal"
      class="mb-4"
    >
      لا يوجد هدف عام. حدّد عدد ساعات العمل في
      <RouterLink :to="{ name: 'customer-app-settings' }">
        إعدادات التطبيق
      </RouterLink>
      ، أو هدفاً خاصاً لمسوّق من «السجل».
    </VAlert>

    <VRow
      v-if="report"
      class="mb-2"
    >
      <VCol
        v-for="k in ['met', 'missed', 'off']"
        :key="k"
        cols="12"
        sm="4"
      >
        <VCard
          variant="tonal"
          :color="CA_TARGET_STATUS[k].color"
          class="pa-4 ca-t-kpi"
          :class="{ 'is-active': filter === k }"
          @click="filter = filter === k ? 'all' : k"
        >
          <div class="d-flex align-center gap-3">
            <VIcon
              :icon="CA_TARGET_STATUS[k].icon"
              size="32"
            />
            <div>
              <div class="text-h3 font-weight-bold">
                {{ report[k] }}
              </div>
              <div>{{ CA_TARGET_STATUS[k].title }}</div>
            </div>
          </div>
        </VCard>
      </VCol>
    </VRow>

    <VCard :loading="loading">
      <VCardItem>
        <VCardTitle>
          {{ report ? caDate(report.date) : '' }}
          <VChip
            v-if="report?.isToday"
            size="small"
            color="primary"
            variant="tonal"
            class="ms-2"
          >
            اليوم — النتائج تتحدث حتى نهاية اليوم
          </VChip>
        </VCardTitle>
        <VCardSubtitle v-if="report">
          الهدف العام: {{ report.defaultTargetMinutes > 0 ? `${caMinutes(report.defaultTargetMinutes)} يومياً` : 'غير مفعّل' }}
          <template v-if="filter !== 'all'">
            · يعرض «{{ CA_TARGET_STATUS[filter].title }}» فقط
          </template>
        </VCardSubtitle>
      </VCardItem>
      <VDataTable
        :headers="headers"
        :items="rows"
        item-value="userId"
        :items-per-page="-1"
        hide-default-footer
        density="comfortable"
        no-data-text="لا توجد نتائج لهذا اليوم"
        hover
        @click:row="(_, { item }) => historyAgent = { id: item.userId, fullName: item.fullName }"
      >
        <template #item.fullName="{ item }">
          <div class="font-weight-medium">
            {{ item.fullName }}
          </div>
          <div class="text-caption text-medium-emphasis">
            {{ item.username }}
          </div>
        </template>
        <template #item.work="{ item }">
          <div class="d-flex align-center gap-2">
            <b>{{ caMinutes(item.result.workMinutes) }}</b>
            <span
              v-if="item.result.targetMinutes > 0"
              class="text-caption text-medium-emphasis"
            >من {{ caMinutes(item.result.targetMinutes) }}</span>
          </div>
          <VProgressLinear
            v-if="item.result.targetMinutes > 0"
            :model-value="pct(item.result)"
            :color="item.result.met ? 'success' : 'primary'"
            height="4"
            rounded
            style="max-inline-size: 180px;"
          />
        </template>
        <template #item.pins="{ item }">
          {{ item.result.pins }}
        </template>
        <template #item.status="{ item }">
          <VChip
            size="small"
            :color="CA_TARGET_STATUS[item.result.status].color"
            :prepend-icon="CA_TARGET_STATUS[item.result.status].icon"
            variant="tonal"
          >
            {{ CA_TARGET_STATUS[item.result.status].title }}
          </VChip>
        </template>
        <template #item.actions="{ item }">
          <IconBtn
            size="small"
            title="المحلات المثبّتة"
            :to="{ name: 'customer-app-prospects', query: { userId: item.userId, date } }"
            @click.stop
          >
            <VIcon icon="tabler-map-pin" />
          </IconBtn>
          <VBtn
            size="small"
            variant="text"
            prepend-icon="tabler-history"
            @click.stop="historyAgent = { id: item.userId, fullName: item.fullName }"
          >
            السجل
          </VBtn>
        </template>
      </VDataTable>
    </VCard>

    <CaTargetHistoryDialog
      :agent="historyAgent"
      @close="historyAgent = null"
      @changed="load"
    />
  </div>
</template>

<style scoped>
.ca-t-kpi {
  cursor: pointer;
}

.ca-t-kpi.is-active {
  outline: 2px solid currentcolor;
}
</style>
