<script setup>
import { computed, ref, watch } from 'vue'
import { asList, branchApp, daysAgo, errorText, fmtDate, isoDay, pick } from '@/composables/branchAppApi'
import { useToast } from '@/composables/useToast'

const props = defineProps({ city: { type: String, required: true } })
const toast = useToast()

const loading = ref(false)
const rows = ref([])
const search = ref('')
const fromDate = ref(daysAgo(30))
const toDate = ref(isoDay(new Date()))

const headers = [
  { title: 'التاريخ', key: 'date' },
  { title: 'الزبون', key: 'name' },
  { title: 'الهاتف', key: 'phone' },
  { title: 'القرار', key: 'type' },
  { title: 'بواسطة', key: 'by' },
  { title: 'الملاحظة', key: 'note' },
]

const typeColor = { 'متواصل': 'info', 'قانونية': 'error', 'طلب قانونية': 'warning', 'وهمي': 'secondary' }

const items = computed(() => rows.value.map(r => ({
  date: pick(r, 'createdDate', 'CreatedDate'),
  name: pick(r, 'customerName', 'CustomerName') || '—',
  phone: pick(r, 'phoneNumber', 'PhoneNumber') || '',
  type: pick(r, 'decisionType', 'DecisionType') || '',
  by: pick(r, 'userName', 'UserName') || '—',
  note: pick(r, 'note', 'Note') || '',
})))

async function load() {
  loading.value = true
  try {
    rows.value = asList(await branchApp.decisions(props.city, { fromDate: fromDate.value, toDate: toDate.value }))
  }
  catch (e) {
    rows.value = []
    toast.error(errorText(e, 'تعذر تحميل سجل القرارات'))
  }
  finally {
    loading.value = false
  }
}

watch([() => props.city, fromDate, toDate], load, { immediate: true })
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <VTextField
        v-model="fromDate"
        type="date"
        label="من"
        density="compact"
        hide-details
        style="max-inline-size: 170px"
      />
      <VTextField
        v-model="toDate"
        type="date"
        label="إلى"
        density="compact"
        hide-details
        style="max-inline-size: 170px"
      />
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
      no-data-text="لا توجد قرارات في هذه الفترة"
    >
      <template #item.date="{ item }">
        {{ fmtDate(item.date, true) }}
      </template>
      <template #item.type="{ item }">
        <VChip
          size="small"
          :color="typeColor[item.type] || 'primary'"
          label
        >
          {{ item.type }}
        </VChip>
      </template>
      <template #item.note="{ item }">
        <span class="ba-wrap">{{ item.note || '—' }}</span>
      </template>
    </VDataTable>
  </div>
</template>
