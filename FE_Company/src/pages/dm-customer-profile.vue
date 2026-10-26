<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  dmAddCustomerNote,
  dmCustomerNotes,
  dmCustomerProfile,
} from '@/composables/delegatedManagerApi'
import { formatIraqDate } from '@/composables/iraqDate'
import { useToast } from '@/composables/useToast'

defineOptions({ name: 'DmCustomerProfile' })
definePage({ meta: { layout: 'dm' } })

const toast = useToast()
const route = useRoute()
const router = useRouter()

const cityValue = computed(() => String(route.query.cityValue || '').trim())
const customerId = computed(() => Number(route.query.customerId || 0))
const initialName = computed(() => String(route.query.name || '').trim())
const initialPhone = computed(() => String(route.query.phone || '').trim())

const busy = ref(false)
const saving = ref(false)
const profile = ref(null)
const notes = ref([])
const noteText = ref('')

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] != null && obj[k] !== '')
      return obj[k]
  }
  return undefined
}

function money(v) {
  const n = Number(v)
  if (!Number.isFinite(n)) return '—'
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(n)
}

function iraqDayKey(value) {
  if (value == null || value === '') return ''
  const text = String(value).trim()
  const m = text.match(/^(\d{4})[-/](\d{2})[-/](\d{2})/)
  if (m) return `${m[1]}-${m[2]}-${m[3]}`
  const ms = Date.parse(text)
  if (!Number.isFinite(ms)) return ''
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Baghdad' }).format(new Date(ms))
}

function iraqTodayKey() {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Baghdad' }).format(new Date())
}

/** أيام من تاريخ البيع حتى اليوم (توقيت بغداد). */
function daysSinceSale(sale) {
  const ka = iraqDayKey(sale)
  const kb = iraqTodayKey()
  if (!ka) return null
  const da = new Date(`${ka}T12:00:00`)
  const db = new Date(`${kb}T12:00:00`)
  const diff = Math.round((db - da) / 86400000)
  return Number.isFinite(diff) ? Math.max(0, diff) : null
}

function sameLabel(a, b) {
  const x = String(a || '').trim()
  const y = String(b || '').trim()
  if (!x || !y) return false
  return x === y || x.replace(/\s+/g, '') === y.replace(/\s+/g, '')
}

const paymentSummary = computed(() =>
  pick(profile.value, 'paymentSummary', 'PaymentSummary') || {},
)

const payments = computed(() => {
  const rows = pick(profile.value, 'payments', 'Payments') || []
  return Array.isArray(rows) ? [...rows].sort((a, b) => {
    const da = Date.parse(pick(a, 'paymentDate', 'PaymentDate') || '') || 0
    const db = Date.parse(pick(b, 'paymentDate', 'PaymentDate') || '') || 0
    return db - da
  }) : []
})

const officialSales = computed(() => {
  const rows = pick(profile.value, 'officialSales', 'OfficialSales') || []
  return Array.isArray(rows) ? [...rows].sort((a, b) => {
    const da = Date.parse(pick(a, 'dateCreate', 'DateCreate') || '') || 0
    const db = Date.parse(pick(b, 'dateCreate', 'DateCreate') || '') || 0
    return db - da
  }) : []
})

const draftSales = computed(() => {
  const rows = pick(profile.value, 'sales', 'Sales') || []
  return Array.isArray(rows) ? rows : []
})

const title = computed(() =>
  pick(profile.value, 'customerName', 'CustomerName') || initialName.value || 'ملف الزبون',
)

const saleDate = computed(() => {
  const fromOfficial = pick(officialSales.value[0], 'dateCreate', 'DateCreate')
  if (fromOfficial) return fromOfficial
  return pick(draftSales.value[0], 'date', 'Date')
})

const lastPaymentDate = computed(() =>
  pick(paymentSummary.value, 'lastPaymentDate', 'LastPaymentDate')
  || pick(payments.value[0], 'paymentDate', 'PaymentDate'),
)

const receiptCount = computed(() => {
  const fromSummary = pick(paymentSummary.value, 'countReceiptDevice', 'CountReceiptDevice')
  if (fromSummary != null) return Number(fromSummary)
  return payments.value.length
})

/** عدد أيام التسديد = أيام من تاريخ البيع إلى اليوم. */
const paymentDayCount = computed(() => daysSinceSale(saleDate.value))

const listLabel = computed(() => {
  const delegate = pick(profile.value, 'delegateName', 'DelegateName') || ''
  const list = pick(profile.value, 'customerListName', 'CustomerListName') || ''
  if (sameLabel(delegate, list))
    return delegate || list || '—'
  if (delegate && list)
    return `${delegate} · ${list}`
  return delegate || list || '—'
})

function saleCardMetrics(sale) {
  const date = pick(sale, 'dateCreate', 'DateCreate', 'date', 'Date')
  const total = pick(sale, 'amountTotalSales', 'AmountTotalSales', 'finalSalePrice', 'FinalSalePrice')
  const paid = pick(sale, 'receiptsTotal', 'ReceiptsTotal', 'downPayment', 'DownPayment')
  const rem = pick(sale, 'amountRemaining', 'AmountRemaining')
  const remaining = rem != null
    ? rem
    : (total != null && paid != null ? Number(total) - Number(paid) : null)
  return {
    date,
    total,
    paid,
    remaining,
    daysSince: daysSinceSale(date),
    items: pick(sale, 'itemsNames', 'ItemsNames', 'saleName', 'SaleName') || '',
    employee: pick(sale, 'userName', 'UserName', 'employeeName', 'EmployeeName') || '',
    status: pick(sale, 'status', 'Status') || '',
  }
}

/** Prior sales = official matches by triple name + phone, excluding the newest as "current" when duplicates. */
const priorSaleCards = computed(() => {
  const official = officialSales.value
  if (official.length > 1)
    return official.slice(1).map(s => ({ kind: 'official', raw: s, ...saleCardMetrics(s) }))
  if (official.length === 1 && draftSales.value.length)
    return draftSales.value
      .filter(s => {
        const d = iraqDayKey(pick(s, 'date', 'Date'))
        const o = iraqDayKey(pick(official[0], 'dateCreate', 'DateCreate'))
        return d && o && d !== o
      })
      .map(s => ({ kind: 'draft', raw: s, ...saleCardMetrics(s) }))
  return draftSales.value.slice(1).map(s => ({ kind: 'draft', raw: s, ...saleCardMetrics(s) }))
})

function goBack() {
  // Prefer history.back so KeepAlive DmStats restores exact scroll/selection.
  if (String(route.query.returnTo || '') === 'dm-stats' && window.history.length > 1) {
    router.back()
    return
  }
  const city = cityValue.value
  router.push({
    path: '/dm-stats',
    query: city ? { cityValue: city } : {},
  })
}

async function load() {
  if (!cityValue.value || !customerId.value) {
    toast.error('معرّف الزبون أو المحافظة ناقص')
    return
  }
  busy.value = true
  try {
    const [raw, notesRaw] = await Promise.all([
      dmCustomerProfile(cityValue.value, customerId.value),
      dmCustomerNotes(cityValue.value, customerId.value).catch(() => []),
    ])
    profile.value = raw && typeof raw === 'object' ? raw : null
    const embedded = pick(raw, 'notes', 'Notes')
    notes.value = Array.isArray(notesRaw) && notesRaw.length
      ? notesRaw
      : (Array.isArray(embedded) ? embedded : (pick(notesRaw, 'items', 'Items') || []))
  }
  catch (e) {
    profile.value = null
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل الملف')
  }
  finally {
    busy.value = false
  }
}

async function submitNote() {
  const text = noteText.value.trim()
  if (!text) {
    toast.error('اكتب الملاحظة أولاً')
    return
  }
  saving.value = true
  try {
    await dmAddCustomerNote(cityValue.value, {
      customerId: customerId.value,
      noteText: text,
    })
    noteText.value = ''
    toast.success('تمت إضافة الملاحظة')
    await load()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر حفظ الملاحظة')
  }
  finally {
    saving.value = false
  }
}

watch(() => route.query, load, { deep: true })
onMounted(load)
</script>

<template>
  <div class="dm-prof">
    <header class="dm-prof__hero">
      <div class="dm-prof__head">
        <VBtn
          icon
          variant="text"
          @click="goBack"
        >
          <VIcon icon="tabler-arrow-right" />
        </VBtn>
        <div>
          <p class="dm-prof__eyebrow">
            ملف الزبون
          </p>
          <h1>{{ title }}</h1>
          <p class="dm-prof__sub">
            مطابقة الاسم الثلاثي ورقم الهاتف للمبيعات السابقة · المحافظة: {{ cityValue || '—' }}
          </p>
        </div>
      </div>
      <VBtn
        color="primary"
        variant="tonal"
        :loading="busy"
        prepend-icon="tabler-refresh"
        @click="load"
      >
        تحديث
      </VBtn>
    </header>

    <div
      v-if="busy"
      class="dm-prof__loading"
    >
      <VProgressCircular
        indeterminate
        color="primary"
      />
    </div>

    <template v-else-if="profile">
      <div class="dm-prof__grid">
        <section class="dm-prof__card">
          <h2>معلومات شخصية</h2>
          <div class="dm-kv">
            <span>الاسم</span>
            <strong>{{ pick(profile, 'customerName', 'CustomerName') || initialName || '—' }}</strong>
          </div>
          <div class="dm-kv">
            <span>الهاتف</span>
            <strong>{{ pick(profile, 'phone', 'Phone') || initialPhone || '—' }}</strong>
          </div>
          <div class="dm-kv">
            <span>العنوان</span>
            <strong>{{ pick(profile, 'address', 'Address') || '—' }}</strong>
          </div>
          <div class="dm-kv">
            <span>المحافظة</span>
            <strong>{{ pick(profile, 'province', 'Province', 'cityName', 'CityName') || cityValue || '—' }}</strong>
          </div>
          <div class="dm-kv">
            <span>القائمة</span>
            <strong>{{ listLabel }}</strong>
          </div>
          <div
            v-if="profile.isLegal || profile.IsLegal"
            class="dm-kv"
          >
            <span>الحالة</span>
            <strong class="is-legal">قانونية</strong>
          </div>
        </section>

        <section class="dm-prof__card dm-prof__card--accent">
          <h2>ملخص البيع الحالي</h2>
          <div class="dm-kv">
            <span>سعر البيع</span>
            <strong>{{ money(pick(paymentSummary, 'amountTotalSales', 'AmountTotalSales')) }}</strong>
          </div>
          <div class="dm-kv">
            <span>الواصل</span>
            <strong>{{ money(pick(paymentSummary, 'receiptsTotal', 'ReceiptsTotal')) }}</strong>
          </div>
          <div class="dm-kv">
            <span>الباقي</span>
            <strong>{{ money(pick(paymentSummary, 'amountRemaining', 'AmountRemaining')) }}</strong>
          </div>
          <div class="dm-kv">
            <span>تاريخ البيع</span>
            <strong>{{ formatIraqDate(saleDate) || '—' }}</strong>
          </div>
          <div class="dm-kv">
            <span>تاريخ آخر تسديد</span>
            <strong>{{ formatIraqDate(lastPaymentDate) || '—' }}</strong>
          </div>
          <div class="dm-kv">
            <span>عدد التسديدات</span>
            <strong>{{ receiptCount ?? '—' }}</strong>
          </div>
          <div class="dm-kv">
            <span>عدد أيام التسديد</span>
            <strong>{{ paymentDayCount ?? '—' }}</strong>
          </div>
        </section>

        <section class="dm-prof__card">
          <h2>ملاحظات</h2>
          <VTextarea
            v-model="noteText"
            label="ملاحظة جديدة"
            rows="3"
            variant="outlined"
            density="comfortable"
            class="mb-2"
          />
          <VBtn
            color="primary"
            :loading="saving"
            @click="submitNote"
          >
            إضافة ملاحظة
          </VBtn>
          <VDivider class="my-4" />
          <VAlert
            v-if="!notes.length"
            type="info"
            variant="tonal"
          >
            لا ملاحظات بعد
          </VAlert>
          <div
            v-for="(n, i) in notes"
            :key="pick(n, 'noteId', 'NoteId') || i"
            class="dm-note"
          >
            <div>{{ pick(n, 'noteText', 'NoteText', 'note', 'Note') || '—' }}</div>
            <div class="text-caption">
              {{ pick(n, 'createdByName', 'CreatedByName', 'authorName', 'AuthorName', 'userName', 'UserName') || '' }}
              ·
              {{ formatIraqDate(pick(n, 'createdAtUtc', 'CreatedAtUtc', 'createdAt', 'CreatedAt', 'date', 'Date')) || '' }}
            </div>
          </div>
        </section>
      </div>

      <section class="dm-prof__section">
        <h2>المبيعات السابقة</h2>
        <p class="dm-prof__hint">
          تُطابق عبر الاسم الثلاثي ورقم الهاتف من سجل المبيعات الرسمية
        </p>
        <div class="dm-sale-grid">
          <article
            v-for="(sale, idx) in priorSaleCards"
            :key="pick(sale.raw, 'customerSaleId', 'CustomerSaleId', 'saleId', 'SaleId') || idx"
            class="dm-sale-card"
            :style="{ '--i': Math.min(idx, 12) }"
          >
            <div class="dm-sale-card__title">
              بيع سابق {{ idx + 1 }}
              <small v-if="sale.items">{{ sale.items }}</small>
            </div>
            <div class="dm-kv">
              <span>سعر البيع</span>
              <strong>{{ money(sale.total) }}</strong>
            </div>
            <div class="dm-kv">
              <span>الواصل</span>
              <strong>{{ money(sale.paid) }}</strong>
            </div>
            <div class="dm-kv">
              <span>الباقي</span>
              <strong>{{ money(sale.remaining) }}</strong>
            </div>
            <div class="dm-kv">
              <span>تاريخ البيع</span>
              <strong>{{ formatIraqDate(sale.date) || '—' }}</strong>
            </div>
            <div class="dm-kv">
              <span>تاريخ آخر تسديد</span>
              <strong>—</strong>
            </div>
            <div class="dm-kv">
              <span>عدد التسديدات</span>
              <strong>—</strong>
            </div>
            <div class="dm-kv">
              <span>عدد أيام التسديد</span>
              <strong>{{ sale.daysSince ?? '—' }}</strong>
            </div>
            <div class="dm-kv">
              <span>الموظف</span>
              <strong>{{ sale.employee || '—' }}</strong>
            </div>
            <div
              v-if="sale.status"
              class="dm-kv"
            >
              <span>الحالة</span>
              <strong>{{ sale.status }}</strong>
            </div>
          </article>
          <VAlert
            v-if="!priorSaleCards.length"
            type="info"
            variant="tonal"
          >
            لا مبيعات سابقة مطابقة لهذا الاسم/الهاتف
          </VAlert>
        </div>
      </section>

      <section class="dm-prof__section">
        <h2>سجل التسديدات</h2>
        <div class="dm-pay-table">
          <div class="dm-pay-table__head">
            <span>التاريخ</span>
            <span>المبلغ</span>
            <span>ملاحظة / أصناف</span>
          </div>
          <div
            v-for="(p, i) in payments"
            :key="pick(p, 'customerPaymentId', 'CustomerPaymentId') || i"
            class="dm-pay-table__row"
          >
            <span>{{ formatIraqDate(pick(p, 'paymentDate', 'PaymentDate')) || '—' }}</span>
            <b>{{ money(pick(p, 'amount', 'Amount')) }}</b>
            <span>{{ pick(p, 'note', 'Note', 'itemsNames', 'ItemsNames') || '—' }}</span>
          </div>
          <VAlert
            v-if="!payments.length"
            type="info"
            variant="tonal"
            class="ma-2"
          >
            لا تسديدات مسجّلة
          </VAlert>
        </div>
      </section>
    </template>

    <VAlert
      v-else
      type="warning"
      variant="tonal"
    >
      تعذر عرض بيانات الزبون
    </VAlert>
  </div>
</template>

<style scoped>
.dm-prof {
  --ink: #0c2f30;
  --muted: #5a7273;
  --line: rgba(13, 115, 119, 0.14);
  --teal: #0d7377;
  --teal-deep: #095c5f;
  --teal-soft: rgba(13, 115, 119, 0.1);
  color: var(--ink);
}

.dm-prof__hero {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 1rem;
  align-items: center;
  margin-block-end: 1.25rem;
  padding: 1.1rem 1.25rem;
  border-radius: 18px;
  border: 1px solid var(--line);
  background:
    radial-gradient(90% 120% at 0% 0%, rgba(13, 115, 119, 0.16), transparent 55%),
    linear-gradient(145deg, #f7fcfc, #e8f4f4);
}

.dm-prof__head {
  display: flex;
  gap: 0.35rem;
  align-items: flex-start;
}

.dm-prof__eyebrow {
  margin: 0;
  color: var(--teal);
  font-size: 0.75rem;
  font-weight: 800;
}

.dm-prof__hero h1 {
  margin: 0.15rem 0;
  font-size: 1.4rem;
  font-weight: 800;
}

.dm-prof__sub {
  margin: 0;
  color: var(--muted);
  font-size: 0.85rem;
}

.dm-prof__loading {
  display: grid;
  place-items: center;
  min-block-size: 180px;
}

.dm-prof__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 0.9rem;
  margin-block-end: 1.25rem;
}

.dm-prof__card {
  border: 1px solid var(--line);
  border-radius: 16px;
  background: #fff;
  padding: 1rem 1.1rem;
  animation: rise 0.4s ease both;
}

.dm-prof__card--accent {
  background: linear-gradient(180deg, #f3fafa, #fff);
  border-color: rgba(13, 115, 119, 0.28);
}

.dm-prof__card h2,
.dm-prof__section h2 {
  margin: 0 0 0.75rem;
  font-size: 1.05rem;
  font-weight: 800;
  color: var(--teal-deep);
}

.dm-kv {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.45rem 0;
  border-block-end: 1px solid var(--line);
  font-size: 0.92rem;
}

.dm-kv span { color: var(--muted); font-weight: 600; }
.dm-kv strong { font-weight: 800; text-align: start; }
.is-legal { color: #dc2626; }

.dm-note {
  padding: 0.65rem 0;
  border-block-end: 1px solid var(--line);
}

.dm-prof__section {
  margin-block-end: 1.35rem;
}

.dm-prof__hint {
  margin: -0.35rem 0 0.75rem;
  color: var(--muted);
  font-size: 0.85rem;
}

.dm-pay-table {
  border: 1px solid var(--line);
  border-radius: 14px;
  overflow: hidden;
  background: #fff;
}

.dm-pay-table__head,
.dm-pay-table__row {
  display: grid;
  grid-template-columns: 140px 120px 1fr;
  gap: 0.75rem;
  padding: 0.7rem 1rem;
  align-items: center;
}

.dm-pay-table__head {
  background: var(--teal-soft);
  font-weight: 800;
  font-size: 0.85rem;
}

.dm-pay-table__row {
  border-block-start: 1px solid var(--line);
  font-size: 0.9rem;
}

.dm-sale-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 0.85rem;
}

.dm-sale-card {
  border: 1px solid var(--line);
  border-radius: 14px;
  background: #fff;
  padding: 0.95rem 1rem;
  animation: rise 0.4s ease both;
  animation-delay: calc(0.04s * var(--i, 0));
}

.dm-sale-card__title {
  font-weight: 800;
  margin-block-end: 0.55rem;
  color: var(--teal-deep);
}

.dm-sale-card__title small {
  display: block;
  margin-block-start: 0.2rem;
  color: var(--muted);
  font-weight: 600;
  font-size: 0.78rem;
}

@keyframes rise {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

@media (max-width: 720px) {
  .dm-pay-table__head,
  .dm-pay-table__row {
    grid-template-columns: 1fr 1fr;
  }
  .dm-pay-table__head span:last-child,
  .dm-pay-table__row span:last-child {
    grid-column: 1 / -1;
  }
}
</style>
