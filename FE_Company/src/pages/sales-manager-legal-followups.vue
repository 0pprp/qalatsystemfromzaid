<script setup>
import { onMounted, ref, watch } from 'vue'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { formatIraqDate } from '@/composables/iraqDate'
import { isCentralSalesManager } from '@/composables/useSalesBranches'
import { branchRowKey, smGet, smGetBlob, withCityQuery } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const cityValue = ref('')
const tab = ref('InProgress')
const rows = ref([])
const loading = ref(false)
const detail = ref(null)
const detailOpen = ref(false)
const docPreviewUrls = ref({})

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function docsOf(item) {
  return pick(item, 'documents', 'Documents') || []
}

async function load() {
  if (!cityValue.value) {
    rows.value = []

    return
  }
  loading.value = true
  try {
    const status = tab.value === 'Completed' ? 'Completed' : 'InProgress'
    rows.value = await smGet(withCityQuery(`legal-followups?status=${status}`, cityValue.value)) || []
  }
  catch (err) {
    rows.value = []
    toast.error(err?.response?.data?.message || 'تعذر تحميل المتابعات القانونية')
  }
  finally {
    loading.value = false
  }
}

function revokePreviews() {
  Object.values(docPreviewUrls.value).forEach(url => {
    try { URL.revokeObjectURL(url) }
    catch { /* ignore */ }
  })
  docPreviewUrls.value = {}
}

async function loadDocPreviews(item) {
  revokePreviews()
  const list = docsOf(item)
  const caseId = pick(item, 'id', 'Id')
  const next = {}
  for (const doc of list) {
    const id = pick(doc, 'id', 'Id')
    const type = pick(doc, 'documentType', 'DocumentType') || ''
    const downloadUrl = String(pick(doc, 'downloadUrl', 'DownloadUrl') || '')
    if (!id)
      continue
    try {
      let path = ''
      if (downloadUrl.includes('case-documents')) {
        path = isCentralSalesManager()
          ? `legal-followups/${encodeURIComponent(cityValue.value)}/${caseId}/case-documents/${id}/file`
          : `legal-followups/${caseId}/case-documents/${id}/file`
      }
      else {
        path = isCentralSalesManager()
          ? `customer-documents/${encodeURIComponent(cityValue.value)}/${id}/file`
          : `customer-documents/${id}/file`
      }
      const blob = await smGetBlob(path)
      next[`${type}-${id}`] = URL.createObjectURL(blob)
    }
    catch {
      // skip broken previews
    }
  }
  docPreviewUrls.value = next
}

async function openDetail(row) {
  const id = pick(row, 'id', 'Id')
  if (!cityValue.value || !id)
    return
  try {
    if (isCentralSalesManager())
      detail.value = await smGet(`legal-followups/${encodeURIComponent(cityValue.value)}/${id}`)
    else
      detail.value = await smGet(`legal-followups/${id}`)
    detailOpen.value = true
    await loadDocPreviews(detail.value)
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر فتح التفاصيل')
  }
}

function closeDetail() {
  detailOpen.value = false
  revokePreviews()
}

watch(tab, load)
onMounted(load)
</script>

<style scoped>
.legal-status-toggle {
  min-height: 48px;
  overflow: visible;
  flex-wrap: wrap;
}

.legal-status-toggle :deep(.v-btn) {
  min-height: 48px !important;
  height: auto !important;
  padding-block: 10px !important;
  letter-spacing: normal;
  white-space: nowrap;
  overflow: visible;
}

.legal-status-toggle :deep(.v-btn__content) {
  overflow: visible;
  white-space: nowrap;
  line-height: 1.5;
}
</style>

<template>
  <div>
    <h4 class="mb-4">
      القانونية
    </h4>
    <VRow
      class="mb-4"
      align="center"
    >
      <VCol
        cols="12"
        md="4"
      >
        <SalesBranchFilter
          v-model="cityValue"
          @change="load"
        />
      </VCol>
      <VCol
        cols="12"
        md="8"
      >
        <VBtnToggle
          v-model="tab"
          mandatory
          divided
          color="primary"
          class="legal-status-toggle w-100"
        >
          <VBtn
            value="InProgress"
            class="flex-grow-1 text-none px-6"
            height="48"
            min-width="140"
          >
            قيد العمل
          </VBtn>
          <VBtn
            value="Completed"
            class="flex-grow-1 text-none px-6"
            height="48"
            min-width="140"
          >
            تمت المتابعة
          </VBtn>
        </VBtnToggle>
      </VCol>
    </VRow>

    <VProgressLinear
      v-if="loading"
      indeterminate
      class="mb-2"
    />

    <VTable>
      <thead>
        <tr>
          <th>الزبون</th>
          <th>الهاتف</th>
          <th>المتابع</th>
          <th>الحالة</th>
          <th>تاريخ الإرسال</th>
          <th />
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in rows"
          :key="branchRowKey(row, 'id')"
        >
          <td>{{ pick(row, 'customerName', 'CustomerName') }}</td>
          <td>{{ pick(row, 'customerPhone', 'CustomerPhone') }}</td>
          <td>{{ pick(row, 'followerName', 'FollowerName') }}</td>
          <td>{{ pick(row, 'statusLabel', 'StatusLabel') || pick(row, 'status', 'Status') }}</td>
          <td>{{ formatIraqDate(pick(row, 'createdAtUtc', 'CreatedAtUtc')) }}</td>
          <td>
            <VBtn
              size="small"
              variant="tonal"
              @click="openDetail(row)"
            >
              تفاصيل
            </VBtn>
          </td>
        </tr>
      </tbody>
    </VTable>

    <VDialog
      v-model="detailOpen"
      max-width="860"
      @update:model-value="v => { if (!v) closeDetail() }"
    >
      <VCard v-if="detail">
        <VCardTitle>متابعة #{{ pick(detail, 'id', 'Id') }}</VCardTitle>
        <VCardText>
          <div class="mb-2">
            <strong>الزبون:</strong> {{ pick(detail, 'customerName', 'CustomerName') }}
          </div>
          <div class="mb-2">
            <strong>الهاتف:</strong> {{ pick(detail, 'customerPhone', 'CustomerPhone') }}
          </div>
          <div class="mb-2">
            <strong>العنوان:</strong> {{ pick(detail, 'customerAddress', 'CustomerAddress') }}
          </div>
          <div
            v-if="pick(detail, 'latitude', 'Latitude') != null"
            class="mb-2"
          >
            <strong>موقع المحل:</strong>
            {{ pick(detail, 'latitude', 'Latitude') }}, {{ pick(detail, 'longitude', 'Longitude') }}
          </div>
          <div class="mb-2">
            <strong>المتابع:</strong> {{ pick(detail, 'followerName', 'FollowerName') }}
          </div>
          <div class="mb-2">
            <strong>الحالة:</strong> {{ pick(detail, 'statusLabel', 'StatusLabel') }}
          </div>
          <div
            v-if="pick(detail, 'note', 'Note')"
            class="mb-2"
          >
            <strong>ملاحظة:</strong> {{ pick(detail, 'note', 'Note') }}
          </div>
          <div
            v-if="pick(detail, 'completionNote', 'CompletionNote')"
            class="mb-2"
          >
            <strong>ملاحظة الإكمال:</strong> {{ pick(detail, 'completionNote', 'CompletionNote') }}
          </div>

          <div class="mt-4 mb-2">
            <strong>المستمسكات</strong>
            ({{ docsOf(detail).length }})
          </div>
          <VRow v-if="docsOf(detail).length">
            <VCol
              v-for="doc in docsOf(detail)"
              :key="`${pick(doc, 'documentType', 'DocumentType')}-${pick(doc, 'id', 'Id')}`"
              cols="12"
              sm="6"
              md="4"
            >
              <div class="text-caption mb-1">
                {{ pick(doc, 'typeLabel', 'TypeLabel') || pick(doc, 'documentType', 'DocumentType') }}
              </div>
              <VImg
                v-if="docPreviewUrls[`${pick(doc, 'documentType', 'DocumentType')}-${pick(doc, 'id', 'Id')}`]"
                :src="docPreviewUrls[`${pick(doc, 'documentType', 'DocumentType')}-${pick(doc, 'id', 'Id')}`]"
                height="160"
                cover
                class="rounded border"
              />
              <VAlert
                v-else
                density="compact"
                type="info"
                variant="tonal"
              >
                لا توجد معاينة
              </VAlert>
            </VCol>
          </VRow>
          <VAlert
            v-else
            type="warning"
            variant="tonal"
            density="compact"
          >
            لا توجد مستمسكات مرفوعة بعد
          </VAlert>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn @click="closeDetail">
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
