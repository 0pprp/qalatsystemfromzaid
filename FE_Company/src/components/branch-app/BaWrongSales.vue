<script setup>
import { computed, ref, watch } from 'vue'
import { asList, branchApp, errorText, fmtDate, pick } from '@/composables/branchAppApi'
import { useToast } from '@/composables/useToast'

const props = defineProps({ city: { type: String, required: true } })
const toast = useToast()

const loading = ref(false)
const rows = ref([])
const reviewed = ref(false)

const dialog = ref(false)
const target = ref(null)
const detail = ref(null)
const detailLoading = ref(false)
const note = ref('')
const saving = ref(false)

const headers = computed(() => [
  { title: 'الزبون', key: 'name' },
  { title: 'الهاتف', key: 'phone' },
  { title: 'الطالب', key: 'by' },
  { title: 'التاريخ', key: 'date' },
  ...(reviewed.value
    ? [{ title: 'المراجع', key: 'reviewer' }, { title: 'ملاحظة المراجعة', key: 'reviewNote' }]
    : [{ title: '', key: 'actions', sortable: false, align: 'end' }]),
])

const items = computed(() => rows.value.map(r => ({
  id: pick(r, 'id', 'Id'),
  name: pick(r, 'customerName', 'CustomerName') || '—',
  phone: pick(r, 'customerPhone', 'CustomerPhone') || '',
  by: pick(r, 'requestingManagerDisplayName', 'RequestingManagerDisplayName') || '—',
  date: pick(r, 'requestedAtUtc', 'RequestedAtUtc'),
  reviewer: pick(r, 'branchManagerReviewerDisplayName', 'BranchManagerReviewerDisplayName') || '—',
  reviewNote: pick(r, 'branchManagerReviewNote', 'BranchManagerReviewNote') || '—',
})))

const reason = computed(() => pick(detail.value?.request, 'reason', 'Reason') || '')

async function load() {
  loading.value = true
  try {
    rows.value = asList(await branchApp.wrongSales(props.city, { reviewed: reviewed.value, pageSize: 100 }))
  }
  catch (e) {
    rows.value = []
    toast.error(errorText(e, 'تعذر تحميل المبيعات الخاطئة'))
  }
  finally {
    loading.value = false
  }
}

async function open(item) {
  target.value = item
  note.value = ''
  detail.value = null
  dialog.value = true
  detailLoading.value = true
  try {
    detail.value = await branchApp.wrongSale(item.id)
  }
  catch { /* the reason is optional context */ }
  finally {
    detailLoading.value = false
  }
}

async function save() {
  if (!target.value || note.value.trim().length < 3)
    return
  saving.value = true
  try {
    await branchApp.reviewWrongSale(target.value.id, note.value.trim())
    toast.success('تم تسجيل المراجعة')
    rows.value = rows.value.filter(r => pick(r, 'id', 'Id') !== target.value.id)
    dialog.value = false
  }
  catch (e) {
    toast.error(errorText(e, 'تعذر تسجيل المراجعة'))
  }
  finally {
    saving.value = false
  }
}

watch([() => props.city, reviewed], load, { immediate: true })
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <VChipGroup
        v-model="reviewed"
        mandatory
        selected-class="text-primary"
      >
        <VChip
          :value="false"
          filter
          variant="outlined"
        >
          بانتظار المراجعة
        </VChip>
        <VChip
          :value="true"
          filter
          variant="outlined"
        >
          تمت مراجعتها
        </VChip>
      </VChipGroup>
      <VSpacer />
      <VBtn
        variant="tonal"
        icon="tabler-refresh"
        size="small"
        :loading="loading"
        @click="load"
      />
    </div>
    <VDataTable
      :headers="headers"
      :items="items"
      :loading="loading"
      :items-per-page="50"
      density="compact"
      items-per-page-text="عدد الصفوف"
      page-text="{0}-{1} من {2}"
      no-data-text="لا توجد مبيعات خاطئة"
    >
      <template #item.date="{ item }">
        {{ fmtDate(item.date, true) }}
      </template>
      <template #item.reviewNote="{ item }">
        <span class="ba-wrap">{{ item.reviewNote }}</span>
      </template>
      <template #item.actions="{ item }">
        <VBtn
          size="small"
          variant="tonal"
          color="primary"
          @click="open(item)"
        >
          مراجعة
        </VBtn>
      </template>
    </VDataTable>

    <VDialog
      v-model="dialog"
      max-width="480"
    >
      <VCard :title="target?.name">
        <VCardText>
          <VProgressLinear
            v-if="detailLoading"
            indeterminate
            class="mb-3"
          />
          <div
            v-if="reason"
            class="mb-3 text-body-2"
          >
            <strong>السبب:</strong> {{ reason }}
          </div>
          <VTextarea
            v-model="note"
            label="ملاحظة المراجعة (إجبارية)"
            rows="3"
            auto-grow
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="dialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :disabled="note.trim().length < 3"
            :loading="saving"
            @click="save"
          >
            تمت المراجعة
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
