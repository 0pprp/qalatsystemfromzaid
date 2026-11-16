<script setup>
import { onMounted, ref, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import {
  PA_ORDER_STATUSES,
  paDateTime,
  paErrorMessage,
  paGetOrder,
  paListOrders,
  paMeta,
  paMoney,
  paOrderCounts,
  paPaged,
  paSyncOrders,
} from '@/composables/promotersAppApi'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()
const route = useRoute()

const rows = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)
const listError = ref('')
const counts = ref({})
const tab = ref('all')
const search = ref('')
const searchApplied = ref('')
const from = ref('')
const to = ref('')
const userId = ref(route.query.userId ? Number(route.query.userId) : null)
const userName = ref(route.query.name || '')
let gen = 0

const TABS = [
  { value: 'all', title: 'الكل', statuses: '' },
  { value: 'open', title: 'قيد المتابعة', statuses: 'New,Transferred,FieldInspection', keys: ['New', 'Transferred', 'FieldInspection'] },
  { value: 'Sold', title: 'تم البيع', statuses: 'Sold', keys: ['Sold'] },
  { value: 'closed', title: 'لم يتم / ملغى', statuses: 'NotSold,Cancelled', keys: ['NotSold', 'Cancelled'] },
]

function tabCount(t) {
  return t.keys ? t.keys.reduce((s, k) => s + (counts.value[k] || 0), 0) : (counts.value.all || 0)
}

const headers = [
  { title: 'الطلب', key: 'orderNumber', sortable: false },
  { title: 'المروّج', key: 'username', sortable: false },
  { title: 'المنتج', key: 'productName', sortable: false },
  { title: 'الزبون', key: 'customerName', sortable: false },
  { title: 'العمولة', key: 'commission', sortable: false },
  { title: 'الحالة', key: 'status', sortable: false },
]

const applySearch = useDebounceFn(v => { searchApplied.value = String(v || '').trim() }, 400)

watch(search, v => applySearch(v))

async function load() {
  const g = ++gen

  loading.value = true
  listError.value = ''
  try {
    const t = TABS.find(x => x.value === tab.value)

    const [res, c] = await Promise.all([
      paListOrders({ status: t.statuses, q: searchApplied.value, userId: userId.value, from: from.value, to: to.value, page: page.value, pageSize: pageSize.value }),
      paOrderCounts(),
    ])

    if (g !== gen)
      return
    const p = paPaged(res)

    rows.value = p.items
    total.value = p.total
    counts.value = c || {}
  }
  catch (err) {
    if (g === gen)
      listError.value = paErrorMessage(err, 'تعذر تحميل الطلبات')
  }
  finally {
    if (g === gen)
      loading.value = false
  }
}

watch([tab, searchApplied, from, to, userId], () => { if (page.value !== 1) page.value = 1; else load() })
watch([page, pageSize], load)

function clearUser() {
  userId.value = null
  userName.value = ''
}

const syncing = ref(false)

async function syncAll() {
  syncing.value = true
  try {
    const r = await paSyncOrders()

    toast.success(`تمت المزامنة: أُرسل ${r.forwarded} وتغيرت حالة ${r.changed}`)
    load()
  }
  catch (err) {
    toast.error(paErrorMessage(err, 'تعذرت المزامنة'))
  }
  finally {
    syncing.value = false
  }
}

const detailOpen = ref(false)
const detail = ref(null)

async function openDetail(row) {
  try {
    detail.value = await paGetOrder(row.id)
    detailOpen.value = true
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="text-h4 mb-1 d-flex align-center gap-2">
          <VIcon
            icon="tabler-shopping-cart"
            color="primary"
          />
          طلبات المروّجين
        </h4>
        <p class="text-medium-emphasis mb-0">
          تصل الطلبات إلى «طلبات البيع» مباشرة بمصدر «المروج: أونلاين - …». العمولة تضاف للمحفظة فقط عندما يصبح الطلب «تم البيع».
        </p>
      </div>
      <VBtn
        variant="tonal"
        prepend-icon="tabler-refresh"
        :loading="syncing"
        @click="syncAll"
      >
        مزامنة مع المنظومة
      </VBtn>
    </div>

    <VCard class="mb-4">
      <VCardText>
        <VRow dense>
          <VCol
            cols="12"
            md="5"
          >
            <VTextField
              v-model="search"
              label="بحث (رقم الطلب، الزبون، الهاتف، المروّج، المنتج)"
              density="compact"
              clearable
              prepend-inner-icon="tabler-search"
            />
          </VCol>
          <VCol
            cols="6"
            md="2"
          >
            <VTextField
              v-model="from"
              type="date"
              label="من"
              density="compact"
            />
          </VCol>
          <VCol
            cols="6"
            md="2"
          >
            <VTextField
              v-model="to"
              type="date"
              label="إلى"
              density="compact"
            />
          </VCol>
          <VCol
            v-if="userId"
            cols="12"
            md="3"
            class="d-flex align-center"
          >
            <VChip
              closable
              color="primary"
              @click:close="clearUser"
            >
              المروّج: {{ userName || userId }} وفريقه
            </VChip>
          </VCol>
        </VRow>
      </VCardText>
    </VCard>

    <VAlert
      v-if="listError"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ listError }}
    </VAlert>

    <VCard>
      <VTabs v-model="tab">
        <VTab
          v-for="t in TABS"
          :key="t.value"
          :value="t.value"
        >
          {{ t.title }}
          <VChip
            size="x-small"
            class="ms-2"
          >
            {{ tabCount(t) }}
          </VChip>
        </VTab>
      </VTabs>
      <VDivider />
      <VDataTableServer
        v-model:page="page"
        v-model:items-per-page="pageSize"
        :headers="headers"
        :items="rows"
        :items-length="total"
        :loading="loading"
        hover
        no-data-text="لا توجد طلبات"
        @click:row="(_, { item }) => openDetail(item)"
      >
        <template #item.orderNumber="{ item }">
          <div class="py-2">
            <div
              class="font-weight-medium"
              dir="ltr"
              style="text-align: end;"
            >
              {{ item.orderNumber }}
            </div>
            <div class="text-caption text-medium-emphasis">
              {{ paDateTime(item.createdAt) }}
            </div>
          </div>
        </template>
        <template #item.username="{ item }">
          <code dir="auto">{{ item.username }}</code>
          <div
            v-if="item.parentUsername"
            class="text-caption text-medium-emphasis"
          >
            ضمن فريق {{ item.parentUsername }}
          </div>
        </template>
        <template #item.productName="{ item }">
          {{ item.productName }}<span v-if="item.quantity > 1"> ×{{ item.quantity }}</span>
          <div class="text-caption text-medium-emphasis">
            {{ paMoney(item.unitPrice * item.quantity) }}
          </div>
        </template>
        <template #item.customerName="{ item }">
          {{ item.customerName }}
          <div class="text-caption text-medium-emphasis">
            <span dir="ltr">{{ item.customerPhone }}</span> · {{ item.cityName }}
          </div>
        </template>
        <template #item.commission="{ item }">
          <div class="text-no-wrap">
            {{ paMoney(item.commissionTotal) }}
          </div>
          <div
            v-if="item.parentCommission"
            class="text-caption text-medium-emphasis text-no-wrap"
          >
            المروّج {{ paMoney(item.sellerCommission) }} · المنشئ {{ paMoney(item.parentCommission) }}
          </div>
        </template>
        <template #item.status="{ item }">
          <VChip
            :color="paMeta(PA_ORDER_STATUSES, item.status).color"
            size="small"
            variant="tonal"
          >
            {{ item.statusLabel }}
          </VChip>
        </template>
      </VDataTableServer>
    </VCard>

    <VDialog
      v-model="detailOpen"
      max-width="640"
      scrollable
    >
      <VCard v-if="detail">
        <VCardTitle class="d-flex align-center gap-2 pt-4">
          <span dir="ltr">{{ detail.order.orderNumber }}</span>
          <VChip
            :color="paMeta(PA_ORDER_STATUSES, detail.order.status).color"
            size="small"
            variant="tonal"
          >
            {{ detail.order.statusLabel }}
          </VChip>
          <VSpacer />
          <IconBtn @click="detailOpen = false">
            <VIcon icon="tabler-x" />
          </IconBtn>
        </VCardTitle>
        <VCardText>
          <VTable
            density="compact"
            class="mb-4"
          >
            <tbody>
              <tr>
                <td class="text-medium-emphasis">
                  المروّج
                </td><td><code dir="auto">{{ detail.order.username }}</code><span v-if="detail.order.parentUsername"> (فريق {{ detail.order.parentUsername }} — {{ Number(detail.order.parentCutPercent) }}%)</span></td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">
                  المنتج
                </td><td>{{ detail.order.productName }} × {{ detail.order.quantity }} — {{ paMoney(detail.order.unitPrice * detail.order.quantity) }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">
                  العمولة
                </td><td>{{ paMoney(detail.order.commissionTotal) }} (المروّج {{ paMoney(detail.order.sellerCommission) }}<span v-if="detail.order.parentCommission">، المنشئ {{ paMoney(detail.order.parentCommission) }}</span>)</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">
                  الزبون
                </td><td>{{ detail.order.customerName }} — <span dir="ltr">{{ detail.order.customerPhone }}</span></td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">
                  العنوان
                </td><td>{{ detail.order.cityName }} — {{ detail.order.address }}</td>
              </tr>
              <tr v-if="detail.order.notes">
                <td class="text-medium-emphasis">
                  ملاحظات
                </td><td>{{ detail.order.notes }}</td>
              </tr>
              <tr v-if="detail.order.publicReason">
                <td class="text-medium-emphasis">
                  السبب
                </td><td class="text-error">
                  {{ detail.order.publicReason }}
                </td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">
                  طلب البيع في المنظومة
                </td><td>
                  {{ detail.order.salesRequestId || '—' }} <span
                    v-if="detail.order.branchStatus"
                    class="text-caption"
                  >({{ detail.order.branchStatus }}، آخر مزامنة {{ paDateTime(detail.order.lastSyncAt) }})</span>
                </td>
              </tr>
            </tbody>
          </VTable>
          <VTimeline
            density="compact"
            side="end"
            truncate-line="both"
          >
            <VTimelineItem
              v-for="(e, i) in detail.timeline"
              :key="i"
              size="x-small"
              :dot-color="paMeta(PA_ORDER_STATUSES, e.status).color"
            >
              <div class="font-weight-medium">
                {{ e.statusLabel }}
              </div>
              <div class="text-caption text-medium-emphasis">
                {{ paDateTime(e.at) }}
              </div>
              <div
                v-if="e.note"
                class="text-caption"
              >
                {{ e.note }}
              </div>
            </VTimelineItem>
          </VTimeline>
        </VCardText>
      </VCard>
    </VDialog>
  </div>
</template>
