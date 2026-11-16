<script setup>
import { computed, ref, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import ArchiveBranchSelect from '@/components/ArchiveBranchSelect.vue'
import {
  ITEM_STATUS,
  REQUEST_STATUS,
  committeeAcceptRequest,
  committeeGiveRequest,
  committeeItems,
  committeeReceive,
  committeeRequests,
  committeeReturn,
  committeeToArchive,
  errorMessage,
  formatDay,
  money as formatMoney,
} from '@/composables/archiveFlowApi'

const toast = useToast()
const city = ref('')
const tab = ref('sales')
const loading = ref(false)
const busy = ref(false)
const papers = ref([])
const requests = ref([])
const selected = ref({})

// Note dialog for refusing / returning to the sales agent.
const noteAction = ref(null)
const note = ref('')

const sections = computed(() => [
  {
    key: 'incoming',
    title: 'وارد من موظفي المبيعات',
    hint: 'حدّد الأسماء التي استلمتها فعلاً، والباقي ارفضه ليعود لموظف المبيعات',
    items: papers.value.filter(p => p.status === 'ToCommittee'),
    actions: [
      { label: 'رفض المحدد', color: 'error', variant: 'text', run: ids => askNote('refuse', ids) },
      { label: 'استلام المحدد', color: 'primary', variant: 'elevated', run: ids => act(() => committeeReceive(city.value, ids, true), 'تم الاستلام') },
    ],
  },
  {
    key: 'holding',
    title: 'عندي',
    hint: 'سلّمها لمسؤول الأرشيف، أو أرجعها لموظف المبيعات',
    items: papers.value.filter(p => p.status === 'WithCommittee' || p.status === 'ArchiveRefused'),
    actions: [
      { label: 'إرجاع لموظف المبيعات', color: 'error', variant: 'text', run: ids => askNote('return', ids) },
      { label: 'تسليم لمسؤول الأرشيف', color: 'primary', variant: 'elevated', run: ids => act(() => committeeToArchive(city.value, ids), 'سُلّمت لمسؤول الأرشيف') },
    ],
  },
  {
    key: 'atArchive',
    title: 'بانتظار استلام مسؤول الأرشيف',
    hint: '',
    items: papers.value.filter(p => p.status === 'ToArchive'),
    actions: [],
  },
])

function idsOf(section) {
  return section.items.filter(i => selected.value[i.archiveId]).map(i => i.archiveId)
}

function toggleAll(section, value) {
  const next = { ...selected.value }
  for (const item of section.items)
    next[item.archiveId] = value
  selected.value = next
}

function allChecked(section) {
  return section.items.length > 0 && section.items.every(i => selected.value[i.archiveId])
}

async function load() {
  if (!city.value)
    return
  loading.value = true
  try {
    const [p, r] = await Promise.all([committeeItems(city.value), committeeRequests(city.value)])
    papers.value = p?.items || []
    requests.value = r?.items || []
    selected.value = {}
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل الأرشيف'))
  }
  finally {
    loading.value = false
  }
}

async function act(fn, message) {
  busy.value = true
  try {
    await fn()
    toast.success(message)
    noteAction.value = null
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحفظ'))
  }
  finally {
    busy.value = false
  }
}

function askNote(kind, ids) {
  noteAction.value = { kind, ids }
  note.value = ''
}

function confirmNote() {
  const text = note.value.trim()
  if (text.length < 2)
    return toast.error('اكتب السبب')
  const { kind, ids } = noteAction.value

  return kind === 'refuse'
    ? act(() => committeeReceive(city.value, ids, false, text), 'أُعيدت لموظف المبيعات')
    : act(() => committeeReturn(city.value, ids, text), 'أُعيدت لموظف المبيعات')
}

function money(value) {
  return `${Number(value || 0).toLocaleString('en-US')} د.ع`
}

watch(city, () => {
  papers.value = []
  requests.value = []
  load()
})
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold mb-1">
          الأرشيف
        </h1>
        <p class="text-body-2 text-medium-emphasis mb-0">
          استلم أرشيف موظفي المبيعات وسلّمه لمسؤول الأرشيف، وسلّم طلبات المتابعين
        </p>
      </div>
      <div class="d-flex align-center gap-2">
        <ArchiveBranchSelect v-model="city" />
        <VBtn
          icon="tabler-refresh"
          variant="tonal"
          :loading="loading"
          @click="load"
        />
      </div>
    </div>

    <VTabs
      v-model="tab"
      class="mb-4"
    >
      <VTab value="sales">
        أرشيف المبيعات
      </VTab>
      <VTab value="requests">
        طلبات المتابعين
        <VChip
          v-if="requests.filter(r => r.status === 'ToCommittee' || r.status === 'WithCommittee').length"
          size="x-small"
          color="warning"
          class="ms-2"
        >
          {{ requests.filter(r => r.status === 'ToCommittee' || r.status === 'WithCommittee').length }}
        </VChip>
      </VTab>
    </VTabs>

    <div
      v-if="loading"
      class="text-center py-10"
    >
      <VProgressCircular
        indeterminate
        color="primary"
      />
    </div>

    <template v-else-if="tab === 'sales'">
      <VCard
        v-for="section in sections"
        :key="section.key"
        class="mb-4"
      >
        <VCardText>
          <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-2">
            <div>
              <h2 class="text-h6 mb-1">
                {{ section.title }}
                <VChip
                  size="small"
                  class="ms-1"
                >
                  {{ section.items.length }}
                </VChip>
              </h2>
              <div
                v-if="section.hint"
                class="text-body-2 text-medium-emphasis"
              >
                {{ section.hint }}
              </div>
            </div>
            <div
              v-if="section.actions.length && section.items.length"
              class="d-flex flex-wrap gap-2"
            >
              <VBtn
                v-for="a in section.actions"
                :key="a.label"
                :color="a.color"
                :variant="a.variant"
                :disabled="!idsOf(section).length"
                :loading="busy"
                @click="a.run(idsOf(section))"
              >
                {{ a.label }} ({{ idsOf(section).length }})
              </VBtn>
            </div>
          </div>
          <div
            v-if="!section.items.length"
            class="text-medium-emphasis py-2"
          >
            لا يوجد
          </div>
          <template v-else>
            <VCheckbox
              v-if="section.actions.length"
              :model-value="allChecked(section)"
              label="تحديد الكل"
              hide-details
              density="compact"
              @update:model-value="v => toggleAll(section, v)"
            />
            <div
              v-for="item in section.items"
              :key="item.archiveId"
              class="paper-row"
            >
              <VCheckbox
                v-if="section.actions.length"
                v-model="selected[item.archiveId]"
                hide-details
                density="compact"
                color="primary"
              />
              <div class="flex-grow-1 min-w-0">
                <div class="font-weight-bold">
                  {{ item.customerName }}
                </div>
                <div class="text-body-2 text-medium-emphasis">
                  {{ item.employeeName }} · حزمة {{ item.bundleNumber }} · {{ item.saleDate }}
                  · {{ money(item.salePrice) }} · {{ item.itemName }}
                </div>
                <div
                  v-if="item.status === 'ArchiveRefused' && item.refusalNote"
                  class="text-error text-body-2"
                >
                  رفضه الأرشيف: {{ item.refusalNote }}
                </div>
              </div>
              <VChip
                v-if="item.status === 'ArchiveRefused'"
                size="x-small"
                color="error"
              >
                {{ ITEM_STATUS.ArchiveRefused.label }}
              </VChip>
            </div>
          </template>
        </VCardText>
      </VCard>
    </template>

    <VCard v-else>
      <VCardText>
        <div
          v-if="!requests.length"
          class="text-center text-medium-emphasis py-8"
        >
          لا توجد طلبات مسندة إليك
        </div>
        <div
          v-for="row in requests"
          :key="row.id"
          class="paper-row"
        >
          <div class="flex-grow-1 min-w-0">
            <div class="font-weight-bold">
              {{ row.customerName || `زبون ${row.customerId}` }}
            </div>
            <div class="text-body-2 text-medium-emphasis">
              المتابع: {{ row.followerName || '—' }}
              <span v-if="row.listName"> · {{ row.listName }}</span>
              · أُسند {{ formatDay(row.assignedAtUtc) }}
              <span v-if="row.sendNote"> · {{ row.sendNote }}</span>
              · {{ row.remaining > 0 ? `المتبقي ${formatMoney(row.remaining)}` : 'منتهٍ' }}
            </div>
            <div
              v-if="row.followerNote"
              class="text-body-2"
            >
              ملاحظة المتابع: {{ row.followerNote }}
            </div>
          </div>
          <VBtn
            v-if="row.status === 'ToCommittee'"
            color="primary"
            :loading="busy"
            @click="act(() => committeeAcceptRequest(city, row.id), 'تم الاستلام')"
          >
            استلام
          </VBtn>
          <VBtn
            v-else-if="row.status === 'WithCommittee'"
            color="primary"
            variant="tonal"
            :loading="busy"
            @click="act(() => committeeGiveRequest(city, row.id), 'سُلّم للمتابع — بانتظار تأكيده')"
          >
            تسليم للمتابع
          </VBtn>
          <VChip
            v-else
            size="small"
            :color="REQUEST_STATUS[row.status]?.color"
          >
            {{ REQUEST_STATUS[row.status]?.label || row.status }}
          </VChip>
        </div>
      </VCardText>
    </VCard>

    <VDialog
      :model-value="!!noteAction"
      max-width="440"
      @update:model-value="v => { if (!v) noteAction = null }"
    >
      <VCard v-if="noteAction">
        <VCardTitle class="pt-5">
          إرجاع {{ noteAction.ids.length }} اسم لموظف المبيعات
        </VCardTitle>
        <VCardText>
          <VTextField
            v-model="note"
            label="السبب"
            autofocus
            hide-details
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="noteAction = null"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="error"
            variant="elevated"
            :loading="busy"
            @click="confirmNote"
          >
            تأكيد
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.paper-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 4px;
  border-block-end: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.paper-row:last-child {
  border-block-end: none;
}

.min-w-0 {
  min-inline-size: 0;
}
</style>
