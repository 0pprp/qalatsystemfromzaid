<script setup>
import { computed, ref, watch } from 'vue'
import { asList, branchApp, errorText, fmtDate, money, pick } from '@/composables/branchAppApi'
import { useToast } from '@/composables/useToast'

const props = defineProps({ city: { type: String, required: true } })
const toast = useToast()

const loading = ref(false)
const rows = ref([])
const search = ref('')

const dialog = ref(false)
const detail = ref(null)
const detailLoading = ref(false)

const headers = [
  { title: 'التاريخ', key: 'date' },
  { title: 'الزبون', key: 'name' },
  { title: 'الهاتف', key: 'phone' },
  { title: 'الموظف', key: 'employee' },
  { title: 'سعر البيع', key: 'price', align: 'end' },
  { title: 'المقدمة', key: 'down', align: 'end' },
  { title: 'القسط اليومي', key: 'daily', align: 'end' },
]

function mapSale(r) {
  return {
    id: Number(pick(r, 'saleId', 'SaleId') || 0),
    date: pick(r, 'completedAt', 'CompletedAt', 'createdAt', 'CreatedAt'),
    name: pick(r, 'fullName', 'FullName') || '—',
    phone: pick(r, 'phone', 'Phone') || '',
    employee: pick(r, 'employeeName', 'EmployeeName') || '—',
    price: pick(r, 'finalSalePrice', 'FinalSalePrice'),
    down: pick(r, 'downPayment', 'DownPayment'),
    daily: pick(r, 'dailyInstallment', 'DailyInstallment'),
  }
}

const items = computed(() => rows.value.map(mapSale))

const detailView = computed(() => {
  const r = detail.value
  if (!r)
    return null

  return {
    ...mapSale(r),
    province: pick(r, 'province', 'Province') || '—',
    address: [pick(r, 'address', 'Address'), pick(r, 'nearestLandmark', 'NearestLandmark')].filter(Boolean).join(' — ') || '—',
    evaluation: pick(r, 'evaluationLevel', 'EvaluationLevel') ?? '—',
    evaluationNote: pick(r, 'evaluationNote', 'EvaluationNote') || '',
    lines: (r.items ?? r.Items ?? []).map(i => ({
      name: pick(i, 'itemName', 'ItemName', 'productName', 'ProductName', 'name', 'Name') || '—',
      qty: pick(i, 'quantity', 'Quantity', 'qty', 'Qty') ?? 1,
      price: pick(i, 'salePrice', 'SalePrice', 'price', 'Price', 'unitPrice', 'UnitPrice'),
    })),
  }
})

async function load() {
  loading.value = true
  try {
    rows.value = asList(await branchApp.sales(props.city))
  }
  catch (e) {
    rows.value = []
    toast.error(errorText(e, 'تعذر تحميل المبيعات'))
  }
  finally {
    loading.value = false
  }
}

async function open(_e, { item }) {
  const id = item?.id ?? item?.raw?.id
  if (!id)
    return
  detail.value = null
  dialog.value = true
  detailLoading.value = true
  try {
    detail.value = await branchApp.sale(props.city, id)
  }
  catch (e) {
    toast.error(errorText(e, 'تعذر تحميل تفاصيل المبيع'))
    dialog.value = false
  }
  finally {
    detailLoading.value = false
  }
}

watch(() => props.city, load, { immediate: true })
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <span class="text-body-2 text-medium-emphasis">
        المبيعات الجديدة المكتملة في الفرع — اضغط على المبيع لعرض التفاصيل
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
      hover
      items-per-page-text="عدد الصفوف"
      page-text="{0}-{1} من {2}"
      no-data-text="لا توجد مبيعات"
      @click:row="open"
    >
      <template #item.date="{ item }">
        {{ fmtDate(item.date, true) }}
      </template>
      <template #item.price="{ item }">
        {{ money(item.price) }}
      </template>
      <template #item.down="{ item }">
        {{ money(item.down) }}
      </template>
      <template #item.daily="{ item }">
        {{ money(item.daily) }}
      </template>
    </VDataTable>

    <VDialog
      v-model="dialog"
      max-width="560"
    >
      <VCard :title="detailView?.name || 'تفاصيل المبيع'">
        <VCardText>
          <VProgressLinear
            v-if="detailLoading"
            indeterminate
          />
          <template v-if="detailView">
            <VTable density="compact">
              <tbody>
                <tr><td>الهاتف</td><td>{{ detailView.phone || '—' }}</td></tr>
                <tr><td>الموظف</td><td>{{ detailView.employee }}</td></tr>
                <tr><td>التاريخ</td><td>{{ fmtDate(detailView.date, true) }}</td></tr>
                <tr><td>المحافظة</td><td>{{ detailView.province }}</td></tr>
                <tr><td>العنوان</td><td>{{ detailView.address }}</td></tr>
                <tr><td>سعر البيع</td><td>{{ money(detailView.price) }}</td></tr>
                <tr><td>المقدمة</td><td>{{ money(detailView.down) }}</td></tr>
                <tr><td>القسط اليومي</td><td>{{ money(detailView.daily) }}</td></tr>
                <tr><td>التقييم</td><td>{{ detailView.evaluation }} {{ detailView.evaluationNote }}</td></tr>
              </tbody>
            </VTable>
            <div
              v-if="detailView.lines.length"
              class="mt-4"
            >
              <div class="text-subtitle-2 mb-1">
                المواد
              </div>
              <VTable density="compact">
                <tbody>
                  <tr
                    v-for="(l, i) in detailView.lines"
                    :key="i"
                  >
                    <td>{{ l.name }}</td>
                    <td>× {{ l.qty }}</td>
                    <td class="text-end">
                      {{ money(l.price) }}
                    </td>
                  </tr>
                </tbody>
              </VTable>
            </div>
          </template>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn @click="dialog = false">
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
