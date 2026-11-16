<script setup>
import { computed, ref, watch } from 'vue'
import { asList, branchApp, errorText, fmtDate, pick } from '@/composables/branchAppApi'
import { useToast } from '@/composables/useToast'

const props = defineProps({ city: { type: String, required: true } })
const toast = useToast()

const loading = ref(false)
const rows = ref([])
const search = ref('')

const headers = [
  { title: 'الزبون', key: 'name' },
  { title: 'الهاتف', key: 'phone' },
  { title: 'القائمة', key: 'list' },
  { title: 'من', key: 'from' },
  { title: 'إلى', key: 'to' },
  { title: 'تاريخ ممتاز', key: 'priorDate' },
  { title: 'تاريخ النزول', key: 'currentDate' },
  { title: 'خلال (يوم)', key: 'days', align: 'end' },
]

const items = computed(() => rows.value.map(r => ({
  name: pick(r, 'customerName', 'CustomerName') || '—',
  phone: pick(r, 'phoneNumber', 'PhoneNumber') || '',
  list: pick(r, 'delegateName', 'DelegateName') || '',
  from: pick(r, 'fromLabel', 'FromLabel') || 'ممتاز',
  to: pick(r, 'toLabel', 'ToLabel') || '',
  priorDate: pick(r, 'priorExcellentDate', 'PriorExcellentDate'),
  currentDate: pick(r, 'currentCaptureDate', 'CurrentCaptureDate'),
  days: pick(r, 'daysBetween', 'DaysBetween') ?? '',
})))

async function load() {
  loading.value = true
  try {
    rows.value = asList(await branchApp.declining(props.city))
  }
  catch (e) {
    rows.value = []
    toast.error(errorText(e, 'تعذر تحميل الزبائن المتدنية'))
  }
  finally {
    loading.value = false
  }
}

watch(() => props.city, load, { immediate: true })
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <span class="text-body-2 text-medium-emphasis">
        زبائن نزل تقييمهم من ممتاز إلى سيء/متلكئ خلال 15 يوماً
      </span>
      <VSpacer />
      <VTextField
        v-model="search"
        placeholder="بحث"
        density="compact"
        hide-details
        prepend-inner-icon="tabler-search"
        style="max-inline-size: 220px"
      />
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
      :search="search"
      :loading="loading"
      :items-per-page="50"
      density="compact"
      items-per-page-text="عدد الصفوف"
      page-text="{0}-{1} من {2}"
      no-data-text="لا يوجد زبائن متدنية حالياً"
    >
      <template #item.to="{ item }">
        <VChip
          size="small"
          color="error"
          label
        >
          {{ item.to }}
        </VChip>
      </template>
      <template #item.priorDate="{ item }">
        {{ fmtDate(item.priorDate) }}
      </template>
      <template #item.currentDate="{ item }">
        {{ fmtDate(item.currentDate) }}
      </template>
    </VDataTable>
  </div>
</template>
