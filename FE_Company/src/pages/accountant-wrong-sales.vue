<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import {
  accountantGetWrongSale,
  accountantListWrongSales,
  accountantReviewWrongSale,
} from '@/composables/accountantWrongSalesApi'

const toast = useToast()
const loading = ref(false)
const detailLoading = ref(false)
const reviewing = ref(false)
const error = ref('')
const reviewedTab = ref('pending')
const items = ref([])
const detailOpen = ref(false)
const detail = ref(null)
const reviewNote = ref('')
const selectedId = ref('')

const cityLabel = computed(() => localStorage.getItem('CityName') || 'المحافظة الحالية')
const isReviewedView = computed(() => reviewedTab.value === 'reviewed')

function pick(obj, ...keys) {
  if (!obj || typeof obj !== 'object')
    return ''
  for (const k of keys) {
    const v = obj[k]
    if (v != null && String(v).trim() !== '')
      return String(v).trim()
  }

  return ''
}

function formatWhen(raw) {
  if (!raw)
    return '—'
  const d = new Date(raw)
  if (Number.isNaN(d.getTime()))
    return String(raw)

  return d.toLocaleString('ar-IQ')
}

async function loadList() {
  loading.value = true
  error.value = ''
  try {
    const data = await accountantListWrongSales({ reviewed: isReviewedView.value })
    const raw = data?.items ?? data?.Items ?? []
    items.value = Array.isArray(raw) ? raw : []
  }
  catch (e) {
    items.value = []
    error.value = e?.response?.data?.message || e?.message || 'تعذر تحميل المبيعات الخاطئة'
    toast.error(error.value)
  }
  finally {
    loading.value = false
  }
}

async function openDetail(row) {
  const id = pick(row, 'id', 'Id')
  if (!id)
    return
  selectedId.value = id
  detailOpen.value = true
  detailLoading.value = true
  detail.value = null
  reviewNote.value = ''
  try {
    const data = await accountantGetWrongSale(id)
    detail.value = data
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل التفاصيل')
    detailOpen.value = false
  }
  finally {
    detailLoading.value = false
  }
}

const detailRequest = computed(() => detail.value?.request || detail.value?.Request || null)

async function submitReview() {
  const note = reviewNote.value.trim()
  if (note.length < 3) {
    toast.error('ملاحظة المراجعة إجبارية (3 أحرف على الأقل)')

    return
  }
  reviewing.value = true
  try {
    await accountantReviewWrongSale(selectedId.value, note)
    toast.success('تم حفظ المراجعة')
    detailOpen.value = false
    await loadList()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر حفظ المراجعة')
  }
  finally {
    reviewing.value = false
  }
}

watch(reviewedTab, () => {
  loadList()
})

onMounted(loadList)
</script>

<template>
  <div>
    <VCard class="mb-4">
      <VCardText class="d-flex flex-wrap align-center justify-space-between gap-3">
        <div>
          <h1 class="text-h5 font-weight-bold mb-1">
            المبيعات الخاطئة
          </h1>
          <p class="text-body-2 text-medium-emphasis mb-0">
            تقييمات المحل المنخفضة الواردة من المندوب/المتابع — {{ cityLabel }}
          </p>
        </div>
        <div class="d-flex align-center gap-2">
          <VBtnToggle
            v-model="reviewedTab"
            class="wrong-sales-tabs"
            mandatory
            density="comfortable"
            color="primary"
            divided
          >
            <VBtn
              value="pending"
              class="wrong-sales-tab-btn"
            >
              بانتظار المراجعة
            </VBtn>
            <VBtn
              value="reviewed"
              class="wrong-sales-tab-btn"
            >
              تمت المراجعة
            </VBtn>
          </VBtnToggle>
          <VBtn
            icon="tabler-refresh"
            variant="tonal"
            :loading="loading"
            @click="loadList"
          />
        </div>
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

    <VCard>
      <VCardText>
        <div
          v-if="loading"
          class="text-center py-10"
        >
          <VProgressCircular
            indeterminate
            color="primary"
          />
        </div>
        <div
          v-else-if="!items.length"
          class="text-center text-medium-emphasis py-10"
        >
          {{ isReviewedView ? 'لا توجد مبيعات خاطئة تمت مراجعتها' : 'لا توجد مبيعات خاطئة بانتظار المراجعة' }}
        </div>
        <VList
          v-else
          lines="three"
        >
          <VListItem
            v-for="row in items"
            :key="pick(row, 'id', 'Id')"
            class="mb-2 rounded border"
            @click="openDetail(row)"
          >
            <VListItemTitle class="font-weight-bold">
              {{ pick(row, 'customerName', 'CustomerName') || 'زبون' }}
            </VListItemTitle>
            <VListItemSubtitle>
              {{ pick(row, 'customerPhone', 'CustomerPhone') || '—' }}
              ·
              {{ formatWhen(pick(row, 'requestedAtUtc', 'RequestedAtUtc', 'createdAtUtc', 'CreatedAtUtc')) }}
            </VListItemSubtitle>
            <VListItemSubtitle class="text-wrap mt-1">
              {{ pick(row, 'reason', 'Reason', 'reasonHead', 'ReasonHead').slice(0, 160) }}
            </VListItemSubtitle>
            <template #append>
              <VIcon icon="tabler-chevron-left" />
            </template>
          </VListItem>
        </VList>
      </VCardText>
    </VCard>

    <VDialog
      v-model="detailOpen"
      max-width="720"
      scrollable
    >
      <VCard>
        <VCardTitle class="d-flex align-center justify-space-between">
          <span>تفاصيل المبيع الخاطئ</span>
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
          <template v-else-if="detailRequest">
            <div class="mb-3">
              <div class="text-caption text-medium-emphasis">
                الزبون
              </div>
              <div class="font-weight-bold">
                {{ pick(detailRequest, 'customerName', 'CustomerName') }}
              </div>
              <div>{{ pick(detailRequest, 'customerPhone', 'CustomerPhone') }}</div>
            </div>
            <div class="mb-3">
              <div class="text-caption text-medium-emphasis">
                السبب / التقييم
              </div>
              <pre class="reason-block">{{ pick(detailRequest, 'reason', 'Reason') }}</pre>
            </div>
            <div
              v-if="!isReviewedView"
              class="mt-4"
            >
              <VTextarea
                v-model="reviewNote"
                label="ملاحظة المراجعة *"
                rows="3"
                auto-grow
              />
            </div>
          </template>
        </VCardText>
        <VDivider />
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="detailOpen = false"
          >
            إغلاق
          </VBtn>
          <VBtn
            v-if="!isReviewedView"
            color="primary"
            :loading="reviewing"
            @click="submitReview"
          >
            حفظ المراجعة
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.reason-block {
  white-space: pre-wrap;
  font-family: inherit;
  margin: 0;
  background: rgba(var(--v-theme-on-surface), 0.04);
  border-radius: 10px;
  padding: 12px;
  line-height: 1.55;
}

.wrong-sales-tabs :deep(.v-btn) {
  min-width: 9.5rem;
  padding-inline: 1rem !important;
  white-space: nowrap;
  flex: 0 0 auto;
}

.wrong-sales-tab-btn {
  letter-spacing: normal !important;
}
</style>
