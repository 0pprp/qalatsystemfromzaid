<script setup>
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import ArchiveBranchSelect from '@/components/ArchiveBranchSelect.vue'
import {
  ITEM_STATUS,
  archiveBoard,
  archiveBundle,
  archiveDecide,
  archivePhoto,
  errorMessage,
} from '@/composables/archiveFlowApi'

const toast = useToast()
const city = ref('')
const loading = ref(false)
const detailLoading = ref(false)
const saving = ref(false)
const showApproved = ref(false)
const emptyBoard = () => ({ current: [], ready: [], needsFix: [], atCommittee: [], atArchive: [], approved: [] })
const board = ref(emptyBoard())
const detailOpen = ref(false)
const detail = ref(null)
const selected = ref({})
const refuseOpen = ref(false)
const refuseNote = ref('')
const photoUrls = ref([])

const sections = computed(() => {
  const rows = [
    { key: 'atArchive', title: 'وصلت إليك', hint: 'سلّمتها اللجنة — حدّد الأسماء التي استلمتها', items: board.value.atArchive, color: 'warning' },
    { key: 'atCommittee', title: 'عند لجنة المراقبة', hint: 'بانتظار استلام اللجنة أو تسليمها لك', items: board.value.atCommittee, color: 'info' },
    { key: 'needsFix', title: 'أُعيدت لموظف المبيعات', hint: 'فيها أسماء مرفوضة بانتظار إعادة الإرسال', items: board.value.needsFix, color: 'error' },
    { key: 'ready', title: 'جاهزة ولم تُرسل', hint: 'انتهت الفترة وما زال الأرشيف عند موظف المبيعات', items: board.value.ready, color: 'success' },
    { key: 'current', title: 'الحزمة الحالية', hint: 'فترة العشرة أيام لم تنتهِ بعد', items: board.value.current, color: 'default' },
  ]
  if (showApproved.value)
    rows.push({ key: 'approved', title: 'مؤرشفة بالكامل', hint: 'اكتمل استلام كل أوراقها', items: board.value.approved, color: 'success' })

  return rows
})

const items = computed(() => detail.value?.items || [])
const waiting = computed(() => items.value.filter(i => i.status === 'ToArchive'))
const chosenIds = computed(() => waiting.value.filter(i => selected.value[i.archiveId]).map(i => i.archiveId))

function formatDate(raw) {
  const parts = String(raw || '').slice(0, 10).split('-')

  return parts.length === 3 ? `${parts[0]}/${parts[1]}/${parts[2]}` : (raw || '—')
}

function money(value) {
  return `${Number(value || 0).toLocaleString('en-US')} د.ع`
}

// Most recent period first; same period falls back to the higher bundle number.
function newestFirst(list) {
  return [...(list || [])].sort((a, b) =>
    String(b.periodStart || '').localeCompare(String(a.periodStart || ''))
    || String(b.periodEnd || '').localeCompare(String(a.periodEnd || ''))
    || (Number(b.bundleNumber) || 0) - (Number(a.bundleNumber) || 0))
}

async function loadBoard() {
  if (!city.value)
    return
  loading.value = true
  try {
    const data = await archiveBoard(city.value)
    board.value = {
      current: newestFirst(data?.current),
      ready: newestFirst(data?.ready),
      needsFix: newestFirst(data?.needsFix),
      atCommittee: newestFirst(data?.atCommittee),
      atArchive: newestFirst(data?.atArchive),
      approved: newestFirst(data?.approved),
    }
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل أرشيف المبيعات'))
  }
  finally {
    loading.value = false
  }
}

function resetPhotos() {
  for (const url of photoUrls.value)
    URL.revokeObjectURL(url)
  photoUrls.value = []
}

async function openBundle(row) {
  detailOpen.value = true
  detailLoading.value = true
  detail.value = null
  selected.value = {}
  resetPhotos()
  try {
    const data = await archiveBundle(city.value, row.bundleId)
    detail.value = data
    selected.value = Object.fromEntries((data?.items || []).filter(i => i.status === 'ToArchive').map(i => [i.archiveId, true]))
    const targets = (data?.photoIds || []).length ? data.photoIds : (data?.hasPhoto ? [null] : [])
    const urls = []
    for (const photoId of targets) {
      try {
        urls.push(URL.createObjectURL(await archivePhoto(city.value, row.bundleId, photoId)))
      }
      catch { /* a missing picture should not hide the papers */ }
    }
    photoUrls.value = urls
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل الحزمة'))
    detailOpen.value = false
  }
  finally {
    detailLoading.value = false
  }
}

function selectAll(value) {
  selected.value = Object.fromEntries(waiting.value.map(i => [i.archiveId, value]))
}

async function decide(accept) {
  if (!chosenIds.value.length)
    return toast.error('حدّد الأسماء أولاً')
  if (!accept && refuseNote.value.trim().length < 2)
    return toast.error('اكتب سبب الرفض')
  saving.value = true
  try {
    await archiveDecide(city.value, chosenIds.value, accept, accept ? null : refuseNote.value.trim())
    toast.success(accept ? 'تم استلام الأسماء المحددة' : 'أُعيدت الأسماء المحددة للجنة')
    refuseOpen.value = false
    refuseNote.value = ''
    await openBundle(detail.value)
    await loadBoard()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحفظ'))
  }
  finally {
    saving.value = false
  }
}

watch(city, () => {
  board.value = emptyBoard()
  loadBoard()
})
onBeforeUnmount(resetPhotos)
</script>

<template>
  <div>
    <VCard class="mb-4">
      <VCardText class="d-flex flex-wrap align-center justify-space-between gap-3">
        <div>
          <h1 class="text-h5 font-weight-bold mb-1">
            أرشيف المبيعات
          </h1>
          <p class="text-body-2 text-medium-emphasis mb-0">
            موظف المبيعات ← لجنة المراقبة ← الأرشيف
          </p>
        </div>
        <div class="d-flex flex-wrap align-center gap-3">
          <ArchiveBranchSelect v-model="city" />
          <VSwitch
            v-model="showApproved"
            color="primary"
            hide-details
            label="المؤرشفة"
          />
          <VBtn
            icon="tabler-refresh"
            variant="tonal"
            :loading="loading"
            @click="loadBoard"
          />
        </div>
      </VCardText>
    </VCard>

    <div
      v-if="loading"
      class="text-center py-10"
    >
      <VProgressCircular
        indeterminate
        color="primary"
      />
    </div>

    <template v-else>
      <VCard
        v-for="section in sections"
        :key="section.key"
        class="mb-4"
      >
        <VCardText>
          <div class="d-flex align-center justify-space-between gap-2 mb-2">
            <div>
              <h2 class="text-h6 mb-1">
                {{ section.title }}
              </h2>
              <div class="text-body-2 text-medium-emphasis">
                {{ section.hint }}
              </div>
            </div>
            <VChip
              size="small"
              :color="section.items.length ? section.color : undefined"
            >
              {{ section.items.length }}
            </VChip>
          </div>
          <div
            v-if="!section.items.length"
            class="text-medium-emphasis py-2"
          >
            لا توجد حزم
          </div>
          <VList v-else>
            <VListItem
              v-for="row in section.items"
              :key="row.bundleId"
              class="mb-2 rounded border"
              @click="openBundle(row)"
            >
              <VListItemTitle class="font-weight-bold">
                حزمة {{ row.bundleNumber }} · {{ row.employeeName || 'موظف مبيعات' }}
              </VListItemTitle>
              <VListItemSubtitle>
                {{ formatDate(row.periodStart) }} — {{ formatDate(row.periodEnd) }}
                · {{ row.itemCount }} اسم
                <span v-if="row.recipientName"> · المستلم: {{ row.recipientName }}</span>
                <span v-if="section.key === 'atArchive'"> · بانتظارك {{ row.toArchiveCount }}</span>
              </VListItemSubtitle>
              <template #append>
                <VIcon icon="tabler-chevron-left" />
              </template>
            </VListItem>
          </VList>
        </VCardText>
      </VCard>
    </template>

    <VDialog
      v-model="detailOpen"
      max-width="860"
      scrollable
    >
      <VCard>
        <VCardTitle class="d-flex align-center justify-space-between">
          <span>{{ detail ? `حزمة ${detail.bundleNumber} — ${detail.employeeName}` : 'الحزمة' }}</span>
          <VBtn
            icon="tabler-x"
            variant="text"
            @click="detailOpen = false"
          />
        </VCardTitle>
        <VDivider />
        <VCardText>
          <div
            v-if="detailLoading"
            class="text-center py-8"
          >
            <VProgressCircular
              indeterminate
              color="primary"
            />
          </div>
          <template v-else-if="detail">
            <div class="mb-3 text-body-2">
              {{ formatDate(detail.periodStart) }} — {{ formatDate(detail.periodEnd) }}
              <span v-if="detail.recipientName"> · المستلم من اللجنة: {{ detail.recipientName }}</span>
              <span v-if="detail.note"> · ملاحظة الموظف: {{ detail.note }}</span>
            </div>
            <div
              v-if="photoUrls.length"
              class="archive-photos mb-4"
            >
              <img
                v-for="(url, index) in photoUrls"
                :key="url"
                :src="url"
                :alt="`صورة التسليم ${index + 1}`"
                class="archive-photo"
              >
            </div>
            <div
              v-if="waiting.length"
              class="d-flex gap-2 mb-3"
            >
              <VBtn
                size="small"
                variant="tonal"
                @click="selectAll(true)"
              >
                تحديد الكل
              </VBtn>
              <VBtn
                size="small"
                variant="text"
                @click="selectAll(false)"
              >
                إلغاء التحديد
              </VBtn>
            </div>
            <div
              v-for="item in items"
              :key="item.archiveId"
              class="archive-item mb-2"
            >
              <VCheckbox
                v-if="item.status === 'ToArchive'"
                v-model="selected[item.archiveId]"
                hide-details
                density="compact"
                color="primary"
              />
              <div class="flex-grow-1">
                <div class="font-weight-bold">
                  {{ item.customerName }}
                </div>
                <div class="text-body-2">
                  {{ formatDate(item.saleDate) }} · {{ money(item.salePrice) }} · {{ item.itemName }}
                  · {{ item.listName || 'بدون قائمة' }}
                </div>
                <div
                  v-if="item.refusalNote"
                  class="text-error text-body-2"
                >
                  {{ item.refusalNote }}
                </div>
              </div>
              <VChip
                size="x-small"
                :color="ITEM_STATUS[item.status]?.color"
              >
                {{ ITEM_STATUS[item.status]?.label || item.status }}
              </VChip>
            </div>
            <VTextField
              v-if="refuseOpen"
              v-model="refuseNote"
              class="mt-3"
              label="سبب الرفض (يُعاد للجنة)"
              autofocus
              hide-details
            />
          </template>
        </VCardText>
        <VDivider />
        <VCardActions v-if="waiting.length">
          <span class="text-body-2 ms-2">المحدد: {{ chosenIds.length }}</span>
          <VSpacer />
          <VBtn
            v-if="!refuseOpen"
            color="error"
            variant="text"
            :disabled="!chosenIds.length"
            @click="refuseOpen = true"
          >
            رفض المحدد
          </VBtn>
          <template v-else>
            <VBtn
              variant="text"
              @click="refuseOpen = false"
            >
              إلغاء
            </VBtn>
            <VBtn
              color="error"
              :loading="saving"
              @click="decide(false)"
            >
              تأكيد الرفض
            </VBtn>
          </template>
          <VBtn
            v-if="!refuseOpen"
            color="primary"
            variant="elevated"
            :disabled="!chosenIds.length"
            :loading="saving"
            @click="decide(true)"
          >
            استلام المحدد
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.archive-photos {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.archive-photo {
  display: block;
  width: 160px;
  height: 160px;
  border-radius: 12px;
  object-fit: cover;
}

.archive-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
}
</style>
