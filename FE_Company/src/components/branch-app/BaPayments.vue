<script setup>
import { computed, ref, watch } from 'vue'
import { asList, branchApp, errorText, fmtDate, isoDay, money, pick } from '@/composables/branchAppApi'
import { useToast } from '@/composables/useToast'

const props = defineProps({ city: { type: String, required: true } })
const toast = useToast()

const lists = ref([])
const listId = ref(0)
const fromDate = ref(isoDay(new Date()))
const toDate = ref(isoDay(new Date()))
const q = ref('')
const loading = ref(false)
const rows = ref([])

const headers = [
  { title: 'التاريخ', key: 'date' },
  { title: 'الزبون', key: 'name' },
  { title: 'الهاتف', key: 'phone' },
  { title: 'المبلغ', key: 'amount', align: 'end' },
]

const items = computed(() => rows.value.map(r => ({
  date: pick(r, 'paymentDate', 'PaymentDate'),
  name: pick(r, 'customerName', 'CustomerName') || '—',
  phone: pick(r, 'phoneNumber', 'PhoneNumber') || '',
  amount: Number(pick(r, 'amountDenar', 'AmountDenar') || 0),
})))

const total = computed(() => items.value.reduce((s, r) => s + r.amount, 0))

async function loadLists() {
  lists.value = []
  listId.value = 0
  rows.value = []
  try {
    const c = await branchApp.catalog(props.city)
    lists.value = (c?.lists ?? c?.Lists ?? []).map(l => ({ id: Number(pick(l, 'id', 'Id') || 0), name: pick(l, 'name', 'Name') || '—' }))
    if (lists.value.length)
      listId.value = lists.value[0].id
  }
  catch (e) {
    toast.error(errorText(e, 'تعذر تحميل القوائم'))
  }
}

async function load() {
  if (!listId.value) {
    rows.value = []

    return
  }
  loading.value = true
  try {
    rows.value = asList(await branchApp.payments({
      cityValue: props.city,
      listId: listId.value,
      fromDate: fromDate.value,
      toDate: toDate.value,
      q: q.value.trim() || undefined,
    }))
  }
  catch (e) {
    rows.value = []
    toast.error(errorText(e, 'تعذر تحميل التسديدات'))
  }
  finally {
    loading.value = false
  }
}

watch(() => props.city, loadLists, { immediate: true })
watch([listId, fromDate, toDate], load)
</script>

<template>
  <div>
    <VRow dense>
      <VCol
        cols="12"
        sm="4"
      >
        <VAutocomplete
          v-model="listId"
          :items="lists"
          item-title="name"
          item-value="id"
          label="القائمة"
          density="compact"
          hide-details
        />
      </VCol>
      <VCol
        cols="6"
        sm="2"
      >
        <VTextField
          v-model="fromDate"
          type="date"
          label="من"
          density="compact"
          hide-details
        />
      </VCol>
      <VCol
        cols="6"
        sm="2"
      >
        <VTextField
          v-model="toDate"
          type="date"
          label="إلى"
          density="compact"
          hide-details
        />
      </VCol>
      <VCol
        cols="12"
        sm="4"
        class="d-flex ga-2"
      >
        <VTextField
          v-model="q"
          placeholder="بحث باسم الزبون"
          density="compact"
          hide-details
          prepend-inner-icon="tabler-search"
          @keyup.enter="load"
        />
        <VBtn
          variant="tonal"
          icon="tabler-refresh"
          size="small"
          :loading="loading"
          @click="load"
        />
      </VCol>
    </VRow>

    <div class="d-flex ga-6 my-3 text-body-1">
      <span>عدد التسديدات: <strong>{{ items.length }}</strong></span>
      <span>المجموع: <strong>{{ money(total) }}</strong> د.ع</span>
    </div>

    <VDataTable
      :headers="headers"
      :items="items"
      :loading="loading"
      :items-per-page="50"
      density="compact"
      items-per-page-text="عدد الصفوف"
      page-text="{0}-{1} من {2}"
      no-data-text="لا توجد تسديدات في هذه الفترة"
    >
      <template #item.date="{ item }">
        {{ fmtDate(item.date, true) }}
      </template>
      <template #item.amount="{ item }">
        {{ money(item.amount) }}
      </template>
    </VDataTable>
  </div>
</template>
