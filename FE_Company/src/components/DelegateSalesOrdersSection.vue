<script setup>
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { directorDelegateFollowupListSalesOrders } from '@/composables/directorApi'

const props = defineProps({
  cityValue: { type: String, default: '' },
  listId: { type: Number, default: 0 },
  listName: { type: String, default: '' },
})

const router = useRouter()
const busy = ref(false)
const error = ref('')
const data = ref(null)
const dialog = ref(false)
const dialogKey = ref('all')
const dialogSearch = ref('')

const PREVIEW_COUNT = 5

function pick(o, camel) {
  if (!o)
    return undefined
  if (o[camel] !== undefined)
    return o[camel]

  return o[camel.charAt(0).toUpperCase() + camel.slice(1)]
}

function normOrder(o) {
  return {
    requestId: pick(o, 'requestId'),
    customerName: pick(o, 'customerName') || '',
    customerPhone: pick(o, 'customerPhone') || '',
    createdAtIraq: pick(o, 'createdAtIraq'),
    outcome: pick(o, 'outcome') || 'open',
    statusLabel: pick(o, 'statusLabel') || '',
    rejectionReason: pick(o, 'rejectionReason') || '',
    saleRequestType: pick(o, 'saleRequestType') || '',
    targetEmployeeName: pick(o, 'targetEmployeeName') || '',
    shopRating: pick(o, 'shopRating'),
    isWrongSale: !!pick(o, 'isWrongSale'),
    evaluationLevel: Number(pick(o, 'evaluationLevel') || 0),
    evaluationLabel: pick(o, 'evaluationLabel') || '',
    customerId: pick(o, 'customerId'),
    soldAtIraq: pick(o, 'soldAtIraq'),
    finalSalePrice: pick(o, 'finalSalePrice'),
    isNewCustomer: pick(o, 'isNewCustomer'),
    oldCustomerReason: pick(o, 'oldCustomerReason') || '',
    matchedCustomerId: pick(o, 'matchedCustomerId'),
    matchedCustomerName: pick(o, 'matchedCustomerName') || '',
    paymentRatingLabel: pick(o, 'paymentRatingLabel') || '',
  }
}

function normCounts(arr) {
  return (Array.isArray(arr) ? arr : []).map(x => ({
    key: String(pick(x, 'key') ?? ''),
    label: pick(x, 'label') || '',
    count: Number(pick(x, 'count') || 0),
  }))
}

function norm(raw) {
  if (!raw)
    return null

  return {
    total: Number(pick(raw, 'total') || 0),
    sold: Number(pick(raw, 'sold') || 0),
    rejected: Number(pick(raw, 'rejected') || 0),
    open: Number(pick(raw, 'open') || 0),
    conversionPercent: Number(pick(raw, 'conversionPercent') || 0),
    newCustomerSales: Number(pick(raw, 'newCustomerSales') || 0),
    newCustomerSalesThisMonth: Number(pick(raw, 'newCustomerSalesThisMonth') || 0),
    oldCustomerSales: Number(pick(raw, 'oldCustomerSales') || 0),
    soldThisMonth: Number(pick(raw, 'soldThisMonth') || 0),
    ordersThisMonth: Number(pick(raw, 'ordersThisMonth') || 0),
    wrongSales: Number(pick(raw, 'wrongSales') || 0),
    totalSoldValue: Number(pick(raw, 'totalSoldValue') || 0),
    averageShopRating: pick(raw, 'averageShopRating'),
    averageDaysToSale: pick(raw, 'averageDaysToSale'),
    evaluations: normCounts(pick(raw, 'evaluations')),
    paymentRatings: normCounts(pick(raw, 'paymentRatings')),
    orders: (pick(raw, 'orders') || []).map(normOrder),
  }
}

async function load() {
  data.value = null
  error.value = ''
  if (!props.cityValue || !(props.listId > 0))
    return
  const key = `${props.cityValue}|${props.listId}`
  busy.value = true
  try {
    const raw = await directorDelegateFollowupListSalesOrders({ cityValue: props.cityValue, listId: props.listId })
    if (`${props.cityValue}|${props.listId}` === key)
      data.value = norm(raw)
  }
  catch (e) {
    error.value = String(e?.response?.data?.message || e?.message || 'تعذر تحميل طلبات البيع')
  }
  finally {
    busy.value = false
  }
}

watch(() => [props.cityValue, props.listId], load, { immediate: true })

function isThisMonth(v) {
  if (!v)
    return false
  const d = new Date(v)
  if (Number.isNaN(d.getTime()))
    return false
  const now = new Date(Date.now() + 3 * 3600 * 1000) // Baghdad
  const s = String(v)

  // Server sends Iraq wall-clock without offset: read year/month from the string.
  const m = /^(\d{4})-(\d{2})/.exec(s)
  if (m)
    return Number(m[1]) === now.getUTCFullYear() && Number(m[2]) === now.getUTCMonth() + 1

  return d.getFullYear() === now.getUTCFullYear() && d.getMonth() === now.getUTCMonth()
}

/** Every card = one filter over the orders; the popup shows exactly the counted rows. */
const FILTERS = {
  all: { title: 'كل طلبات البيع', fn: () => true },
  sold: { title: 'صارت مبيعات', fn: o => o.outcome === 'sold' },
  rejected: { title: 'مرفوضة', fn: o => o.outcome === 'rejected' },
  open: { title: 'قيد العمل', fn: o => o.outcome === 'open' },
  newAll: { title: 'مبيعات جديدة (كل الفترة)', fn: o => o.outcome === 'sold' && o.isNewCustomer === true },
  newMonth: { title: 'مبيعات جديدة هذا الشهر', fn: o => o.outcome === 'sold' && o.isNewCustomer === true && isThisMonth(o.soldAtIraq) },
  old: { title: 'مبيعات لزبائن قدامى', fn: o => o.outcome === 'sold' && o.isNewCustomer === false },
  soldMonth: { title: 'مبيعات هذا الشهر', fn: o => o.outcome === 'sold' && isThisMonth(o.soldAtIraq) },
  ordersMonth: { title: 'طلبات هذا الشهر', fn: o => isThisMonth(o.createdAtIraq) },
  wrong: { title: 'مبيعات خاطئة (تقييم محل منخفض)', fn: o => o.isWrongSale },
}

function filterFor(key) {
  if (FILTERS[key])
    return FILTERS[key]
  if (key.startsWith('eval:')) {
    const lvl = Number(key.slice(5))
    const label = data.value?.evaluations.find(e => e.key === String(lvl))?.label || ''

    return { title: `تقييم موظف المبيعات: ${label}`, fn: o => o.evaluationLevel === lvl }
  }
  if (key.startsWith('pay:')) {
    const label = key.slice(4)

    return { title: `تقييم التسديد الحالي: ${label}`, fn: o => o.outcome === 'sold' && o.paymentRatingLabel === label }
  }

  return FILTERS.all
}

const dialogTitle = computed(() => filterFor(dialogKey.value).title)

const dialogRows = computed(() => {
  if (!data.value)
    return []
  const f = filterFor(dialogKey.value).fn
  const q = dialogSearch.value.trim()

  return data.value.orders.filter(f).filter(o => !q
    || o.customerName.includes(q)
    || String(o.customerPhone).includes(q)
    || String(o.requestId) === q)
})

const preview = computed(() => (data.value?.orders || []).slice(0, PREVIEW_COUNT))

function open(key) {
  dialogKey.value = key
  dialogSearch.value = ''
  dialog.value = true
}

function fmtNum(n) {
  const v = Number(n)

  return Number.isFinite(v) ? new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(v) : '—'
}

function fmtDate(v) {
  return v ? String(v).slice(0, 10) : '—'
}

function outcomeColor(o) {
  return o.outcome === 'sold' ? 'success' : o.outcome === 'rejected' ? 'error' : 'warning'
}

function evalColor(level) {
  return ({ 5: 'success', 4: 'teal', 3: 'info', 2: 'warning', 1: 'error' })[level] || 'grey'
}

function payColor(label) {
  return ({ 'ممتاز': 'success', 'جيد': 'info', 'ضعيف': 'warning', 'متلكئ': 'deep-orange', 'سيء': 'error' })[label] || 'grey'
}

function profileHref(o) {
  return router.resolve({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: props.cityValue,
      customerId: String(o.customerId),
      name: o.customerName || '',
      phone: o.customerPhone || '',
    },
  }).href
}

function customerKind(o) {
  if (o.outcome !== 'sold')
    return '—'
  if (o.isNewCustomer === true)
    return 'زبون جديد'
  if (o.isNewCustomer === false)
    return `قديم — ${o.oldCustomerReason}${o.matchedCustomerName ? ` (${o.matchedCustomerName})` : ''}`

  return '—'
}
</script>

<template>
  <section class="dso line-box">
    <div class="dso__head">
      <h3 class="line-box__title">
        طلبات بيع المندوب
      </h3>
      <VBtn
        v-if="data && data.total"
        size="small"
        variant="tonal"
        prepend-icon="tabler-list"
        @click="open('all')"
      >
        عرض كل الطلبات
      </VBtn>
    </div>

    <div
      v-if="busy && !data"
      class="muted pa-2"
    >
      جاري تحميل طلبات البيع…
    </div>
    <div
      v-else-if="error"
      class="text-error pa-2"
    >
      {{ error }}
      <VBtn
        size="small"
        variant="text"
        @click="load"
      >
        إعادة المحاولة
      </VBtn>
    </div>
    <div
      v-else-if="data && !data.total"
      class="muted pa-2"
    >
      لم يرسل مندوب هذه القائمة أي طلب بيع.
    </div>

    <template v-else-if="data">
      <p class="muted dso__hint">
        نسبة التحويل {{ data.conversionPercent }}% (من الطلبات التي حُسمت) — اضغط أي كونتينر لعرض الطلبات المحسوبة فيه
      </p>

      <div class="sub-grid">
        <button
          type="button"
          class="sub-card sub-card--click"
          @click="open('all')"
        >
          <span class="sub-card__label">كل الطلبات</span>
          <strong class="sub-card__value">{{ data.total }}</strong>
          <small>هذا الشهر: {{ data.ordersThisMonth }}</small>
        </button>
        <button
          type="button"
          class="sub-card sub-card--click dso--good"
          @click="open('sold')"
        >
          <span class="sub-card__label">صارت مبيعات</span>
          <strong class="sub-card__value">{{ data.sold }}</strong>
          <small>قيمة المبيعات: {{ fmtNum(data.totalSoldValue) }}</small>
          <small v-if="data.averageDaysToSale != null">معدل أيام البيع: {{ data.averageDaysToSale }}</small>
        </button>
        <button
          type="button"
          class="sub-card sub-card--click dso--bad"
          @click="open('rejected')"
        >
          <span class="sub-card__label">مرفوضة</span>
          <strong class="sub-card__value">{{ data.rejected }}</strong>
        </button>
        <button
          type="button"
          class="sub-card sub-card--click"
          @click="open('open')"
        >
          <span class="sub-card__label">قيد العمل</span>
          <strong class="sub-card__value">{{ data.open }}</strong>
        </button>
        <button
          type="button"
          class="sub-card sub-card--click dso--new"
          @click="open('newAll')"
        >
          <span class="sub-card__label">مبيعات جديدة (كل الفترة)</span>
          <strong class="sub-card__value">{{ data.newCustomerSales }}</strong>
          <small>زبائن غير موجودين سابقاً بالاسم الثلاثي أو الهاتف</small>
        </button>
        <button
          type="button"
          class="sub-card sub-card--click dso--new"
          @click="open('newMonth')"
        >
          <span class="sub-card__label">مبيعات جديدة هذا الشهر</span>
          <strong class="sub-card__value">{{ data.newCustomerSalesThisMonth }}</strong>
          <small>كل مبيعات الشهر: {{ data.soldThisMonth }}</small>
        </button>
        <button
          type="button"
          class="sub-card sub-card--click"
          @click="open('old')"
        >
          <span class="sub-card__label">مبيعات لزبائن قدامى</span>
          <strong class="sub-card__value">{{ data.oldCustomerSales }}</strong>
        </button>
        <button
          type="button"
          class="sub-card sub-card--click"
          :class="{ 'dso--bad': data.wrongSales > 0 }"
          @click="open('wrong')"
        >
          <span class="sub-card__label">مبيعات خاطئة</span>
          <strong class="sub-card__value">{{ data.wrongSales }}</strong>
          <small v-if="data.averageShopRating != null">معدل تقييم المحل: {{ data.averageShopRating }} / 10</small>
        </button>
      </div>

      <h4 class="dso__sub">
        تقييم موظف المبيعات للطلبات
      </h4>
      <div class="dso__chips">
        <VChip
          v-for="e in data.evaluations"
          :key="`ev-${e.key}`"
          :color="evalColor(Number(e.key))"
          variant="tonal"
          label
          class="dso__chip"
          @click="open(`eval:${e.key}`)"
        >
          {{ e.label }}: <strong class="ms-1">{{ e.count }}</strong>
        </VChip>
      </div>

      <h4 class="dso__sub">
        تقييم التسديد الحالي للمبيعات
      </h4>
      <div class="dso__chips">
        <VChip
          v-for="p in data.paymentRatings"
          :key="`pay-${p.key}`"
          :color="payColor(p.key)"
          variant="tonal"
          label
          class="dso__chip"
          @click="open(`pay:${p.key}`)"
        >
          {{ p.label }}: <strong class="ms-1">{{ p.count }}</strong>
        </VChip>
      </div>

      <h4 class="dso__sub">
        آخر الطلبات
      </h4>
      <div class="dso__table-host">
        <VTable
          density="compact"
          class="dso__table"
        >
          <thead>
            <tr>
              <th>التاريخ</th>
              <th>الزبون</th>
              <th>الحالة</th>
              <th>تقييم الموظف</th>
              <th>نوع الزبون</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="o in preview"
              :key="`pv-${o.requestId}`"
            >
              <td>{{ fmtDate(o.createdAtIraq) }}</td>
              <td>{{ o.customerName }}</td>
              <td>
                <VChip
                  size="x-small"
                  :color="outcomeColor(o)"
                  label
                >
                  {{ o.statusLabel }}
                </VChip>
              </td>
              <td>{{ o.evaluationLabel }}</td>
              <td>{{ customerKind(o) }}</td>
            </tr>
          </tbody>
        </VTable>
      </div>
    </template>

    <VDialog
      v-model="dialog"
      max-width="1200"
      scrollable
    >
      <VCard>
        <VCardTitle class="d-flex align-center flex-wrap gap-2">
          <span>{{ dialogTitle }} — {{ listName }} ({{ dialogRows.length }})</span>
          <VSpacer />
          <VTextField
            v-model="dialogSearch"
            density="compact"
            hide-details
            clearable
            placeholder="بحث بالاسم أو الهاتف أو رقم الطلب"
            prepend-inner-icon="tabler-search"
            style="max-width: 300px"
          />
          <VBtn
            icon="tabler-x"
            variant="text"
            @click="dialog = false"
          />
        </VCardTitle>
        <VCardText>
          <div
            v-if="!dialogRows.length"
            class="muted pa-4 text-center"
          >
            لا توجد طلبات
          </div>
          <VTable
            v-else
            density="compact"
            class="dso__table"
          >
            <thead>
              <tr>
                <th>#</th>
                <th>تاريخ الطلب</th>
                <th>الزبون</th>
                <th>الهاتف</th>
                <th>الحالة</th>
                <th>تقييم الموظف</th>
                <th>تقييم المحل</th>
                <th>تاريخ البيع</th>
                <th>سعر البيع</th>
                <th>نوع الزبون</th>
                <th>التسديد الحالي</th>
                <th>الموظف</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="o in dialogRows"
                :key="`dl-${o.requestId}`"
              >
                <td>{{ o.requestId }}</td>
                <td>{{ fmtDate(o.createdAtIraq) }}</td>
                <td>
                  <a
                    v-if="o.customerId"
                    :href="profileHref(o)"
                    target="_blank"
                    rel="noopener noreferrer"
                  >{{ o.customerName }}</a>
                  <span v-else>{{ o.customerName }}</span>
                  <div
                    v-if="o.saleRequestType === 'Old'"
                    class="muted text-caption"
                  >
                    مبيع قديم
                  </div>
                </td>
                <td dir="ltr">
                  {{ o.customerPhone || '—' }}
                </td>
                <td>
                  <VChip
                    size="x-small"
                    :color="outcomeColor(o)"
                    label
                  >
                    {{ o.statusLabel }}
                  </VChip>
                  <div
                    v-if="o.rejectionReason"
                    class="text-caption text-error"
                  >
                    {{ o.rejectionReason }}
                  </div>
                </td>
                <td>
                  <VChip
                    size="x-small"
                    :color="evalColor(o.evaluationLevel)"
                    variant="tonal"
                    label
                  >
                    {{ o.evaluationLabel }}
                  </VChip>
                </td>
                <td>
                  {{ o.shopRating != null ? `${o.shopRating}/10` : '—' }}
                  <VChip
                    v-if="o.isWrongSale"
                    size="x-small"
                    color="error"
                    label
                  >
                    خاطئ
                  </VChip>
                </td>
                <td>{{ fmtDate(o.soldAtIraq) }}</td>
                <td>{{ o.finalSalePrice != null ? fmtNum(o.finalSalePrice) : '—' }}</td>
                <td>{{ customerKind(o) }}</td>
                <td>
                  <VChip
                    v-if="o.paymentRatingLabel"
                    size="x-small"
                    :color="payColor(o.paymentRatingLabel)"
                    variant="tonal"
                    label
                  >
                    {{ o.paymentRatingLabel }}
                  </VChip>
                  <span v-else>—</span>
                </td>
                <td>{{ o.targetEmployeeName || '—' }}</td>
              </tr>
            </tbody>
          </VTable>
        </VCardText>
      </VCard>
    </VDialog>
  </section>
</template>

<style scoped>
.line-box {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 16px;
  padding: 14px 16px 16px;
  margin-bottom: 16px;
  background: rgba(var(--v-theme-surface), 1);
}
.line-box__title { margin: 0; font-size: 1.05rem; font-weight: 800; }
.dso__head { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 10px; }
.dso__hint { margin: 0 0 10px; font-size: 0.85rem; }
.dso__sub { margin: 16px 0 8px; font-size: 0.95rem; font-weight: 700; }
.dso__chips { display: flex; flex-wrap: wrap; gap: 8px; }
.dso__chip { cursor: pointer; }
.muted { color: rgba(var(--v-theme-on-surface), 0.62); }
.sub-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 10px;
}
.sub-card {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-height: 110px;
  background: rgba(var(--v-theme-on-surface), 0.02);
  text-align: start;
  color: inherit;
}
.sub-card--click { cursor: pointer; }
.sub-card--click:hover { border-color: rgb(var(--v-theme-primary)); }
.sub-card__label { font-size: 0.8rem; opacity: 0.8; }
.sub-card__value { font-size: 1.25rem; font-weight: 800; }
.dso--good { background: rgba(40, 160, 90, 0.07); border-color: rgba(40, 160, 90, 0.3); }
.dso--bad { background: rgba(220, 60, 60, 0.07); border-color: rgba(220, 60, 60, 0.28); }
.dso--new { background: rgba(var(--v-theme-primary), 0.07); }
.dso__table-host { overflow-x: auto; }
.dso__table th { white-space: nowrap; }
</style>
