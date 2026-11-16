<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import {
  caDateTime,
  caErrorMessage,
  caExportReport,
  caListAudit,
  caMinutes,
  caOrderStatusMeta,
  caPaged,
  caReportsOverview,
  caYmd,
} from '@/composables/customerAppApi'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()
const tab = ref('overview')

// ---------------------------------------------------------------- range
function shiftDays(n) {
  const d = new Date()

  d.setDate(d.getDate() + n)

  return caYmd(d)
}

function monthStart(offset = 0) {
  const d = new Date()

  return caYmd(new Date(d.getFullYear(), d.getMonth() + offset, 1))
}

function monthEnd(offset = 0) {
  const d = new Date()

  return caYmd(new Date(d.getFullYear(), d.getMonth() + offset + 1, 0))
}

const PRESETS = [
  { key: 'today', title: 'اليوم', range: () => [caYmd(), caYmd()] },
  { key: 'week', title: 'آخر 7 أيام', range: () => [shiftDays(-6), caYmd()] },
  { key: 'month', title: 'هذا الشهر', range: () => [monthStart(), caYmd()] },
  { key: 'lastMonth', title: 'الشهر الماضي', range: () => [monthStart(-1), monthEnd(-1)] },
]

const from = ref(monthStart())
const to = ref(caYmd())

const activePreset = computed(() => PRESETS.find(p => {
  const [f, t] = p.range()

  return f === from.value && t === to.value
})?.key)

function applyPreset(p) {
  const [f, t] = p.range()

  from.value = f
  to.value = t
}

// ---------------------------------------------------------------- data
const ov = ref(null)
const loading = ref(false)
const error = ref('')
const exporting = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    ov.value = await caReportsOverview({ from: from.value, to: to.value })
  }
  catch (err) {
    ov.value = null
    error.value = caErrorMessage(err, 'تعذر تحميل النظرة العامة')
  }
  finally {
    loading.value = false
  }
}
watch([from, to], useDebounceFn(load, 300))

const o = computed(() => ov.value || {})

const conversion = computed(() => {
  const decided = (o.value.sold ?? 0) + (o.value.notSold ?? 0)

  return decided ? Math.round(((o.value.sold ?? 0) / decided) * 100) : null
})

const kpis = computed(() => [
  { title: 'طلبات البيع', value: o.value.orders ?? 0, sub: `قيد المتابعة الآن: ${o.value.open ?? 0}`, icon: 'tabler-receipt', color: 'primary' },
  { title: 'المبيعات', value: o.value.sold ?? 0, sub: conversion.value == null ? 'نسبة النجاح: —' : `نسبة النجاح: ${conversion.value}%`, icon: 'tabler-circle-check', color: 'success' },
  { title: 'مكتملة التسديد', value: o.value.finished ?? 0, sub: `لم تُبع: ${o.value.notSold ?? 0}`, icon: 'tabler-rosette-discount-check', color: 'info' },
  { title: 'المسوّقون', value: o.value.agents ?? 0, sub: `في الدوام الآن: ${o.value.workingNow ?? 0}`, icon: 'tabler-users', color: 'warning', to: '/customer-app-attendance' },
  { title: 'محلات مثبّتة', value: o.value.pins ?? 0, sub: 'محلات محتملة على الخريطة', icon: 'tabler-map-pin-plus', color: 'secondary', to: '/customer-app-prospects' },
])

const statusRows = computed(() => {
  const list = o.value.byStatus || []
  const total = list.reduce((s, r) => s + r.count, 0) || 1

  return list.map(r => ({ ...r, meta: caOrderStatusMeta(r.status), pct: Math.round((r.count / total) * 100) }))
})

const topItemMax = computed(() => Math.max(1, ...(o.value.topItems || []).map(i => i.quantity)))

const agentHeaders = [
  { title: 'المسوّق', key: 'fullName', sortable: true },
  { title: 'الطلبات', key: 'orders', sortable: true, align: 'center' },
  { title: 'المبيعات', key: 'sold', sortable: true, align: 'center' },
  { title: 'مكتملة التسديد', key: 'finished', sortable: true, align: 'center' },
  { title: 'لم تُبع', key: 'notSold', sortable: true, align: 'center' },
  { title: 'محلات مثبّتة', key: 'pins', sortable: true, align: 'center' },
  { title: 'أيام الدوام', key: 'workedDays', sortable: true, align: 'center' },
  { title: 'ساعات الدوام', key: 'workedMinutes', sortable: true, align: 'center' },
]

const EXPORTS = [
  { type: 'orders', title: 'الطلبات', icon: 'tabler-receipt' },
  { type: 'agents', title: 'أداء المسوّقين', icon: 'tabler-users' },
  { type: 'prospects', title: 'المحلات المثبّتة', icon: 'tabler-map-pin' },
]

async function exportCsv(type) {
  exporting.value = type
  try {
    const name = await caExportReport(type, { from: from.value, to: to.value })

    toast.success(`تم تنزيل ${name}`)
  }
  catch (err) {
    toast.error(caErrorMessage(err, 'تعذر التصدير'))
  }
  finally {
    exporting.value = ''
  }
}

// ---------------------------------------------------------------- audit
const ACTION_LABELS = {
  'agent.create': 'إنشاء حساب مسوّق',
  'agent.update': 'تعديل بيانات مسوّق',
  'agent.password': 'تغيير كلمة مرور مسوّق',
  'agent.suspend': 'إيقاف مسوّق',
  'agent.activate': 'تفعيل مسوّق',
  'settings.update': 'تعديل إعدادات التطبيق',
  'notification.broadcast': 'إشعار للمسوّقين',
  'report.export': 'تصدير تقرير',
  'prospect.delete': 'حذف محل محتمل',
}

const audit = ref([])
const auditTotal = ref(0)
const auditPage = ref(1)
const auditPageSize = ref(20)
const auditLoading = ref(false)
const auditError = ref('')
const auditFilters = ref({ actor: '', from: '', to: '' })
const auditLoaded = ref(false)

const auditHeaders = [
  { title: 'التاريخ', key: 'createdAt', sortable: false },
  { title: 'المنفّذ', key: 'actor', sortable: false },
  { title: 'الإجراء', key: 'action', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end' },
]

async function loadAudit() {
  auditLoading.value = true
  auditError.value = ''
  try {
    const f = auditFilters.value

    const res = caPaged(await caListAudit({
      actor: f.actor,
      from: f.from,
      to: f.to,
      page: auditPage.value,
      pageSize: auditPageSize.value,
    }))

    audit.value = res.items
    auditTotal.value = res.total
    auditLoaded.value = true
  }
  catch (err) {
    audit.value = []
    auditTotal.value = 0
    auditError.value = caErrorMessage(err, 'تعذر تحميل سجل الإجراءات')
  }
  finally {
    auditLoading.value = false
  }
}

watch(auditFilters, useDebounceFn(() => {
  if (auditPage.value !== 1)
    auditPage.value = 1
  else
    loadAudit()
}, 400), { deep: true })
watch([auditPage, auditPageSize], loadAudit)
watch(tab, t => {
  if (t === 'audit' && !auditLoaded.value)
    loadAudit()
})

const auditDetail = ref(null)

function pretty(v) {
  if (v == null || v === '')
    return '—'
  let parsed = v
  if (typeof v === 'string') {
    try {
      parsed = JSON.parse(v)
    }
    catch {
      return v
    }
  }

  return typeof parsed === 'string' ? parsed : JSON.stringify(parsed, null, 2)
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="text-h4 mb-1 d-flex align-center gap-2">
          <VIcon
            icon="tabler-layout-dashboard"
            color="primary"
          />
          نظرة عامة
        </h4>
        <p class="text-medium-emphasis mb-0">
          أداء المسوّقين الميدانيين: طلبات البيع، المبيعات، الدوام والمحلات المثبّتة.
        </p>
      </div>
    </div>

    <VTabs
      v-model="tab"
      class="mb-4"
    >
      <VTab value="overview">
        <VIcon
          icon="tabler-chart-pie"
          class="me-1"
        />الأداء
      </VTab>
      <VTab value="audit">
        <VIcon
          icon="tabler-history"
          class="me-1"
        />سجل الإجراءات
      </VTab>
    </VTabs>

    <VWindow v-model="tab">
      <VWindowItem value="overview">
        <VCard class="mb-4">
          <VCardText class="d-flex flex-wrap align-center gap-3">
            <div class="d-flex flex-wrap gap-2">
              <VChip
                v-for="p in PRESETS"
                :key="p.key"
                :color="activePreset === p.key ? 'primary' : undefined"
                :variant="activePreset === p.key ? 'flat' : 'tonal'"
                @click="applyPreset(p)"
              >
                {{ p.title }}
              </VChip>
            </div>
            <VTextField
              v-model="from"
              type="date"
              label="من"
              density="compact"
              hide-details
              style="max-inline-size: 170px;"
            />
            <VTextField
              v-model="to"
              type="date"
              label="إلى"
              density="compact"
              hide-details
              style="max-inline-size: 170px;"
            />
            <VSpacer />
            <VMenu>
              <template #activator="{ props }">
                <VBtn
                  v-bind="props"
                  variant="tonal"
                  prepend-icon="tabler-download"
                  :loading="!!exporting"
                >
                  تصدير Excel
                </VBtn>
              </template>
              <VList density="compact">
                <VListItem
                  v-for="e in EXPORTS"
                  :key="e.type"
                  :prepend-icon="e.icon"
                  :title="e.title"
                  @click="exportCsv(e.type)"
                />
              </VList>
            </VMenu>
            <VBtn
              variant="text"
              icon="tabler-refresh"
              :loading="loading"
              @click="load"
            />
          </VCardText>
        </VCard>

        <VAlert
          v-if="error"
          type="error"
          variant="tonal"
          class="mb-4"
        >
          {{ error }}
        </VAlert>

        <div class="ca-kpis mb-4">
          <VCard
            v-for="k in kpis"
            :key="k.title"
            :to="k.to"
            :loading="loading && !ov"
          >
            <VCardText class="d-flex align-center gap-3">
              <VAvatar
                :color="k.color"
                variant="tonal"
                rounded
                size="44"
              >
                <VIcon
                  :icon="k.icon"
                  size="24"
                />
              </VAvatar>
              <div class="min-w-0">
                <div class="text-body-2 text-medium-emphasis">
                  {{ k.title }}
                </div>
                <div class="text-h4 font-weight-bold">
                  {{ k.value }}
                </div>
                <div class="text-caption text-medium-emphasis text-truncate">
                  {{ k.sub }}
                </div>
              </div>
            </VCardText>
          </VCard>
        </div>

        <VRow class="mb-1">
          <VCol
            cols="12"
            md="6"
          >
            <VCard class="h-100">
              <VCardItem title="الطلبات حسب الحالة" />
              <VCardText>
                <div
                  v-if="!statusRows.length"
                  class="text-medium-emphasis py-6 text-center"
                >
                  لا توجد طلبات في هذه الفترة
                </div>
                <div
                  v-for="r in statusRows"
                  :key="r.status"
                  class="mb-3"
                >
                  <div class="d-flex align-center justify-space-between mb-1">
                    <span class="d-flex align-center gap-1">
                      <VIcon
                        :icon="r.meta.icon"
                        :color="r.meta.color"
                        size="18"
                      />
                      {{ r.label }}
                    </span>
                    <strong>{{ r.count }}</strong>
                  </div>
                  <VProgressLinear
                    :model-value="r.pct"
                    :color="r.meta.color"
                    height="8"
                    rounded
                  />
                </div>
              </VCardText>
            </VCard>
          </VCol>
          <VCol
            cols="12"
            md="6"
          >
            <VCard class="h-100">
              <VCardItem
                title="أكثر المواد مبيعاً"
                subtitle="كما كتبها المسوّقون في طلبات البيع المُباعة"
              />
              <VCardText>
                <div
                  v-if="!(o.topItems || []).length"
                  class="text-medium-emphasis py-6 text-center"
                >
                  لا توجد مبيعات في هذه الفترة
                </div>
                <div
                  v-for="(i, idx) in o.topItems || []"
                  :key="i.name"
                  class="d-flex align-center gap-3 mb-3"
                >
                  <VAvatar
                    size="26"
                    color="primary"
                    variant="tonal"
                    class="text-caption font-weight-bold"
                  >
                    {{ idx + 1 }}
                  </VAvatar>
                  <div class="flex-grow-1 min-w-0">
                    <div class="d-flex justify-space-between gap-2">
                      <span class="text-truncate">{{ i.name }}</span>
                      <strong>{{ i.quantity }}</strong>
                    </div>
                    <VProgressLinear
                      :model-value="(i.quantity / topItemMax) * 100"
                      color="primary"
                      height="5"
                      rounded
                    />
                  </div>
                </div>
              </VCardText>
            </VCard>
          </VCol>
        </VRow>

        <VCard>
          <VCardItem
            title="أداء المسوّقين"
            subtitle="مرتّبون حسب المبيعات في الفترة المحددة"
          />
          <VDataTable
            :headers="agentHeaders"
            :items="o.byAgent || []"
            :loading="loading"
            :items-per-page="25"
            item-value="userId"
            density="comfortable"
            no-data-text="لا يوجد مسوّقون بعد"
          >
            <template #item.fullName="{ item }">
              <div class="font-weight-medium">
                {{ item.fullName }}
              </div>
              <div
                class="text-caption text-medium-emphasis"
                dir="ltr"
              >
                {{ item.username }}
              </div>
            </template>
            <template #item.sold="{ item }">
              <VChip
                v-if="item.sold"
                color="success"
                size="small"
                variant="tonal"
              >
                {{ item.sold }}
              </VChip>
              <span
                v-else
                class="text-medium-emphasis"
              >0</span>
            </template>
            <template #item.workedMinutes="{ item }">
              {{ item.workedMinutes ? caMinutes(item.workedMinutes) : '—' }}
            </template>
          </VDataTable>
        </VCard>
      </VWindowItem>

      <VWindowItem value="audit">
        <VCard class="mb-4">
          <VCardText>
            <VRow dense>
              <VCol
                cols="12"
                md="4"
              >
                <VTextField
                  v-model="auditFilters.actor"
                  label="اسم المنفّذ"
                  density="compact"
                  clearable
                />
              </VCol>
              <VCol
                cols="6"
                md="4"
              >
                <VTextField
                  v-model="auditFilters.from"
                  type="date"
                  label="من"
                  density="compact"
                />
              </VCol>
              <VCol
                cols="6"
                md="4"
              >
                <VTextField
                  v-model="auditFilters.to"
                  type="date"
                  label="إلى"
                  density="compact"
                />
              </VCol>
            </VRow>
          </VCardText>
        </VCard>

        <VAlert
          v-if="auditError"
          type="error"
          variant="tonal"
          class="mb-4"
        >
          {{ auditError }}
        </VAlert>

        <VCard>
          <VDataTableServer
            v-model:page="auditPage"
            v-model:items-per-page="auditPageSize"
            :headers="auditHeaders"
            :items="audit"
            :items-length="auditTotal"
            :loading="auditLoading"
            item-value="id"
            density="comfortable"
            no-data-text="لا توجد إجراءات"
          >
            <template #item.createdAt="{ item }">
              <span class="text-no-wrap">{{ caDateTime(item.createdAt) }}</span>
            </template>
            <template #item.actor="{ item }">
              <div>{{ item.actorName || '—' }}</div>
              <div class="text-caption text-medium-emphasis">
                {{ item.actorRole }}
              </div>
            </template>
            <template #item.action="{ item }">
              {{ ACTION_LABELS[item.action] || item.action }}
            </template>
            <template #item.actions="{ item }">
              <VBtn
                size="small"
                variant="text"
                prepend-icon="tabler-eye"
                @click="auditDetail = item"
              >
                التفاصيل
              </VBtn>
            </template>
          </VDataTableServer>
        </VCard>
      </VWindowItem>
    </VWindow>

    <VDialog
      :model-value="!!auditDetail"
      max-width="900"
      scrollable
      @update:model-value="v => { if (!v) auditDetail = null }"
    >
      <VCard v-if="auditDetail">
        <VCardTitle class="pa-4 d-flex align-center gap-2">
          <VIcon icon="tabler-history" />
          {{ ACTION_LABELS[auditDetail.action] || auditDetail.action }}
          <VSpacer />
          <IconBtn @click="auditDetail = null">
            <VIcon icon="tabler-x" />
          </IconBtn>
        </VCardTitle>
        <VCardSubtitle class="px-4 pb-2">
          {{ auditDetail.actorName }} ({{ auditDetail.actorRole }}) · {{ caDateTime(auditDetail.createdAt) }}
        </VCardSubtitle>
        <VDivider />
        <VCardText>
          <VRow>
            <VCol
              v-if="auditDetail.before"
              cols="12"
              md="6"
            >
              <div class="text-subtitle-2 mb-1">
                قبل
              </div>
              <pre class="ca-json">{{ pretty(auditDetail.before) }}</pre>
            </VCol>
            <VCol
              cols="12"
              :md="auditDetail.before ? 6 : 12"
            >
              <div class="text-subtitle-2 mb-1">
                {{ auditDetail.before ? 'بعد' : 'البيانات' }}
              </div>
              <pre class="ca-json">{{ pretty(auditDetail.after) }}</pre>
            </VCol>
          </VRow>
        </VCardText>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.ca-kpis {
  display: grid;
  gap: 1rem;
  grid-template-columns: repeat(auto-fill, minmax(210px, 1fr));
}
.min-w-0 {
  min-inline-size: 0;
}
.ca-json {
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.04);
  direction: ltr;
  font-size: 0.78rem;
  max-block-size: 420px;
  overflow: auto;
  padding: 0.75rem;
  text-align: start;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
