<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as XLSX from 'xlsx'
import {
  directorCities,
  directorDelegateFollowupListCustomers,
  directorDelegateFollowupListSummary,
  directorDelegateFollowupLists,
  directorDelegateFollowupUpsertBoardOperatorRating,
} from '@/composables/directorApi'
import { useSalesBranches } from '@/composables/useSalesBranches'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'

defineOptions({ name: 'DirectorDelegateFollowup' })
definePage({
  meta: {
    layout: 'default',
    keepAlive: true,
  },
})

const CITY_STORAGE_KEY = 'director-delegate-followup-city'
const COLS_STORAGE_KEY = 'director-delegate-followup-board-col-order'

const toast = useToast()
const route = useRoute()
const router = useRouter()
const { branches, fetchCities } = useSalesBranches()
const { isDirectorOrMonitor } = useUserRole()

const gatewayCities = ref([])
const listsBusy = ref(false)
const listCards = ref([])
const summaryBusy = ref(false)
const summary = ref(null)
const detailCacheKey = ref('')

const customers = ref([])
const customersBusy = ref(false)
const customersCacheKey = ref('')
const tableVisible = ref(false)
const tableFilter = ref('all') // all | rating | legal | lt100 | 100to200

const rateDialog = ref(false)
const rateBusy = ref(false)
const rateScore = ref(8)
const rateNote = ref('')

const historyDialog = ref(false)
const historyTitle = ref('')
const historyRows = ref([])
const historyKind = ref('amount') // amount | rating | board | conversion
const historyExtra = ref('') // detail text under title

const namesDialog = ref(false)
const namesTitle = ref('')
const namesRows = ref([])

/** مبيعات جديدة بدون أي تسديد ≥ 3 أيام — واجهة فقط من بيانات الزبائن المحمّلة */
const NEW_SALE_UNPAID_MIN_DAYS = 3
const unpaidNewSalesDialog = ref(false)

const RATING_BUCKETS = [
  { key: 'ممتاز', label: 'ممتاز' },
  { key: 'جيد', label: 'جيد' },
  { key: 'ضعيف', label: 'ضعيف' },
  { key: 'متلكئ', label: 'متلكئ' },
  { key: 'سيء', label: 'سيء' },
]

/** ActivateInputRatingSlots_V1 — wired to follower day / BM visit / sales shop ratings */
const INPUT_RATING_SLOTS = [
  {
    key: 'follower',
    label: 'تقييم مسؤول اللجنة الميدانية (المتابع)',
    summaryKey: 'followerCommitteeRating',
    historyKey: 'followerCommitteeLast30Days',
    emptyHint: 'يظهر بعد إنهاء الجباية وتقييم القائمة',
  },
  {
    key: 'supervision',
    label: 'تقييم لجنة الإشراف (إدارة الفرع)',
    summaryKey: 'supervisionCommitteeRating',
    historyKey: 'supervisionCommitteeLast30Days',
    emptyHint: 'يظهر بعد مغادرة الفرع وتقييم القائمة',
  },
  {
    key: 'salesDelegate',
    label: 'تقييم مندوب المبيعات',
    summaryKey: 'salesDelegateRating',
    historyKey: 'salesDelegateLast30Days',
    emptyHint: 'يظهر بعد إكمال المبيع وتقييم مندوب القائمة (مرة يومياً)',
  },
]

const ALL_COLUMN_DEFS = [
  { title: 'بروفايل الزبون', key: 'profile', sortable: false, exportable: false },
  { title: 'العميل', key: 'customerName', exportable: true },
  { title: 'التقييم', key: 'phantomRatingLabel', exportable: true },
  { title: 'تاريخ البيع', key: 'dateSaleDevice', exportable: true },
  { title: 'سعر البيع', key: 'amountTotalSales', exportable: true },
  { title: 'الواصل', key: 'receiptsTotal', exportable: true },
  { title: 'الباقي', key: 'amountRemaining', exportable: true },
  { title: 'القسط', key: 'amountDaySales', exportable: true },
  { title: 'تاريخ اخر تسديد', key: 'lastPaymentDate', exportable: true },
  { title: 'مبلغ اخر تسديد', key: 'lastPaymentAmount', exportable: true },
  { title: 'نسبة التسديد', key: 'receiptRateDevice', exportable: true },
  { title: 'عدد التسديدات', key: 'countReceiptDevice', exportable: true },
  { title: 'عدد الأيام', key: 'numberOfDayDevice', exportable: true },
  { title: 'العنوان', key: 'address', exportable: true },
  { title: 'الهاتف', key: 'phoneNumber', exportable: true },
  { title: 'اسم المحل', key: 'shopName', exportable: true },
  { title: 'اسم الجابي', key: 'receiptName', exportable: true },
  { title: 'اسم البائع', key: 'saleName', exportable: true },
  { title: 'المباعة', key: 'itemsNames', exportable: true },
  { title: 'الملاحظات', key: 'notes', exportable: true },
]

function defaultColumnOrder() {
  return ALL_COLUMN_DEFS.map(c => c.key)
}

function loadColumnOrder() {
  try {
    const raw = JSON.parse(localStorage.getItem(COLS_STORAGE_KEY) || '[]')
    if (!Array.isArray(raw) || !raw.length)
      return defaultColumnOrder()
    const known = new Set(ALL_COLUMN_DEFS.map(c => c.key))
    const order = raw.filter(k => known.has(k))
    for (const c of ALL_COLUMN_DEFS) {
      if (!order.includes(c.key))
        order.push(c.key)
    }

    return order
  }
  catch {
    return defaultColumnOrder()
  }
}

const columnOrder = ref(loadColumnOrder())
const dragFromKey = ref(null)
const dragOverKey = ref(null)
const headerHoldReady = ref(false)
const headerPressKey = ref(null)
let headerDownAt = 0
let headerHoldTimer = null

const tableHeaders = computed(() =>
  columnOrder.value
    .map(key => ALL_COLUMN_DEFS.find(c => c.key === key))
    .filter(Boolean)
    .map(c => ({
      title: c.title,
      key: c.key,
      sortable: false,
    })))

const statusFilterItems = [
  { title: 'الكل', value: 'all' },
  ...RATING_BUCKETS.map(b => ({ title: b.label, value: b.key })),
  { title: 'قانونية', value: 'legal' },
  { title: 'باقي أقل من 100,000', value: 'lt100' },
  { title: 'باقي 100–200 ألف', value: '100to200' },
]

const cityCards = computed(() => {
  const fromGateway = (Array.isArray(gatewayCities.value) ? gatewayCities.value : [])
    .map(c => ({
      value: String(c.value || c.Value || c.cityValue || c.CityValue || '').trim(),
      name: String(c.name || c.Name || c.cityName || c.CityName || '').trim(),
    }))
    .filter(c => c.value)
  if (fromGateway.length)
    return fromGateway

  return (branches.value || [])
    .map(b => ({
      value: String(b.value || b.Value || '').trim(),
      name: String(b.name || b.Name || b.value || '').trim(),
    }))
    .filter(b => b.value)
})

function cityKey() {
  return String(route.query.city || '').trim()
}

const selectedListId = computed(() => {
  const n = Number(route.query.listId || 0)

  return Number.isFinite(n) && n > 0 ? n : 0
})

const selectedListName = computed(() => {
  const fromSummary = summary.value?.listName
  if (fromSummary)
    return String(fromSummary)
  const card = listCards.value.find(c => c.listId === selectedListId.value)

  return card?.listName || (selectedListId.value ? `قائمة ${selectedListId.value}` : '')
})

const baghdadTodayIso = computed(() => {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Baghdad',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date())
})

function softError(e, fallback) {
  return String(e?.response?.data?.message || e?.message || fallback || 'حدث خطأ')
}

function fmtNum(n) {
  const v = Number(n)
  if (!Number.isFinite(v))
    return '—'

  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(v)
}

function fmtScore(n) {
  const v = Number(n)
  if (!Number.isFinite(v))
    return '—'

  return v.toFixed(1)
}

function fmtDate(v) {
  if (!v)
    return '—'
  const d = new Date(v)
  if (Number.isNaN(d.getTime()))
    return String(v).slice(0, 10)

  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Baghdad',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(d)
}

function parseBaghdadDay(v) {
  if (!v)
    return null
  const iso = fmtDate(v)
  if (!iso || iso === '—')
    return null
  const d = new Date(`${iso}T12:00:00`)
  if (Number.isNaN(d.getTime()) || d.getFullYear() < 2000)
    return null

  return d
}

/** Inclusive days since sale (sale day = 1), Baghdad calendar. */
function daysSinceSaleInclusive(dateSaleDevice) {
  const sale = parseBaghdadDay(dateSaleDevice)
  if (!sale)
    return 0
  const todayIso = baghdadTodayIso.value
  const today = new Date(`${todayIso}T12:00:00`)
  const diff = Math.floor((today.getTime() - sale.getTime()) / 86400000)

  return diff + 1
}

function hasAnyPayment(c) {
  if (Number(c.receiptsTotal || 0) > 0)
    return true
  if (Number(c.lastPaymentAmount || 0) > 0)
    return true
  return parseBaghdadDay(c.lastPaymentDate) != null
}

function isNewSaleUnpaid3Days(c) {
  if (!c || c.isLegal)
    return false
  if (Number(c.amountRemaining || 0) <= 0)
    return false
  if (Number(c.amountTotalSales || 0) <= 0)
    return false
  if (hasAnyPayment(c))
    return false
  return daysSinceSaleInclusive(c.dateSaleDevice) >= NEW_SALE_UNPAID_MIN_DAYS
}

const unpaidNewSalesCustomers = computed(() =>
  (customers.value || []).filter(isNewSaleUnpaid3Days))

const unpaidNewSalesCount = computed(() => unpaidNewSalesCustomers.value.length)

function openUnpaidNewSalesDialog() {
  unpaidNewSalesDialog.value = true
}

function trendLabel(t) {
  const s = String(t || 'flat').toLowerCase()
  if (s === 'up')
    return 'صاعد'
  if (s === 'down')
    return 'هابط'

  return 'ثابت'
}

function trendClass(t) {
  const s = String(t || 'flat').toLowerCase()
  if (s === 'up')
    return 'trend-up'
  if (s === 'down')
    return 'trend-down'

  return 'trend-flat'
}

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const k of keys) {
    if (obj[k] !== undefined && obj[k] !== null)
      return obj[k]
  }

  return undefined
}

function normDayAmount(row) {
  return {
    date: row.date ?? row.Date ?? null,
    amount: Number(row.amount ?? row.Amount ?? 0),
  }
}

function normDayRating(row) {
  return {
    date: row.date ?? row.Date ?? null,
    score: Number(row.score ?? row.Score ?? 0),
    label: String(row.label ?? row.Label ?? ''),
    boardOperatorScore: row.boardOperatorScore ?? row.BoardOperatorScore ?? null,
    boardOperatorNote: row.boardOperatorNote ?? row.BoardOperatorNote ?? null,
  }
}

function normInputRating(raw) {
  if (!raw)
    return null
  const score = pick(raw, 'score', 'Score')
  if (score == null || score === '')
    return null

  return {
    listId: Number(pick(raw, 'listId', 'ListId') || 0),
    ratingDate: pick(raw, 'ratingDate', 'RatingDate') || null,
    score: Number(score),
    note: pick(raw, 'note', 'Note') || null,
    ratedByUserName: pick(raw, 'ratedByUserName', 'RatedByUserName') || null,
    hasRating: true,
  }
}

function normTierMoney(row) {
  return {
    label: String(row.label ?? row.Label ?? ''),
    requiredDue: Number(row.requiredDue ?? row.RequiredDue ?? 0),
    collectedYesterday: Number(row.collectedYesterday ?? row.CollectedYesterday ?? 0),
    collectedLast30Days: (row.collectedLast30Days ?? row.CollectedLast30Days ?? []).map(normDayAmount),
  }
}

function normCustomer(row) {
  return {
    customerID: Number(row.customerID ?? row.CustomerID ?? 0),
    customerName: row.customerName ?? row.CustomerName ?? '',
    phantomRatingLabel: String(row.phantomRatingLabel ?? row.PhantomRatingLabel ?? row.realRatingLabel ?? row.RealRatingLabel ?? ''),
    isLegal: Boolean(row.isLegal ?? row.IsLegal ?? false),
    amountDaySales: Number(row.amountDaySales ?? row.AmountDaySales ?? 0),
    amountRemaining: Number(row.amountRemaining ?? row.AmountRemaining ?? 0),
    amountTotalSales: Number(row.amountTotalSales ?? row.AmountTotalSales ?? 0),
    receiptsTotal: Number(row.receiptsTotal ?? row.ReceiptsTotal ?? 0),
    lastPaymentDate: row.lastPaymentDate ?? row.LastPaymentDate ?? null,
    lastPaymentAmount: Number(row.lastPaymentAmount ?? row.LastPaymentAmount ?? 0),
    dateSaleDevice: row.dateSaleDevice ?? row.DateSaleDevice ?? null,
    receiptRateDevice: row.receiptRateDevice ?? row.ReceiptRateDevice ?? null,
    countReceiptDevice: row.countReceiptDevice ?? row.CountReceiptDevice ?? null,
    numberOfDayDevice: row.numberOfDayDevice ?? row.NumberOfDayDevice ?? null,
    address: row.address ?? row.Address ?? '',
    phoneNumber: row.phoneNumber ?? row.PhoneNumber ?? '',
    shopName: row.shopName ?? row.ShopName ?? '',
    receiptName: row.receiptName ?? row.ReceiptName ?? '',
    saleName: row.saleName ?? row.SaleName ?? '',
    itemsNames: row.itemsNames ?? row.ItemsNames ?? '',
    notes: row.notes ?? row.Notes ?? '',
  }
}

function normListCard(row) {
  return {
    listId: Number(row.listId ?? row.ListId ?? 0),
    listName: String(row.listName ?? row.ListName ?? ''),
    realRating: Number(row.realRating ?? row.RealRating ?? row.phantomRating ?? row.PhantomRating ?? 0),
    realRatingLabel: String(row.realRatingLabel ?? row.RealRatingLabel ?? row.phantomRatingLabel ?? row.PhantomRatingLabel ?? ''),
    nonZeroedCustomerCount: Number(
      row.nonZeroedCustomerCount ?? row.NonZeroedCustomerCount ?? row.activeCustomerCount ?? row.ActiveCustomerCount ?? 0,
    ),
    lastPaymentDate: row.lastPaymentDate ?? row.LastPaymentDate ?? null,
  }
}

function normSummary(row) {
  if (!row)
    return null
  const board = pick(row, 'boardOperatorRating', 'BoardOperatorRating') || {}
  const conv = pick(row, 'conversion', 'Conversion') || {}

  return {
    listId: Number(pick(row, 'listId', 'ListId') || 0),
    listName: String(pick(row, 'listName', 'ListName') || ''),
    realRating: Number(pick(row, 'realRating', 'RealRating', 'phantomRating', 'PhantomRating') || 0),
    realRatingLabel: String(pick(row, 'realRatingLabel', 'RealRatingLabel', 'phantomRatingLabel', 'PhantomRatingLabel') || ''),
    realRatingWeekAgo: Number(pick(row, 'realRatingWeekAgo', 'RealRatingWeekAgo', 'phantomRatingWeekAgo', 'PhantomRatingWeekAgo') || 0),
    ratingTrend: String(pick(row, 'ratingTrend', 'RatingTrend') || 'flat'),
    nonZeroedCustomerCount: Number(pick(row, 'nonZeroedCustomerCount', 'NonZeroedCustomerCount', 'activeCustomerCount', 'ActiveCustomerCount') || 0),
    legalCustomerCount: Number(pick(row, 'legalCustomerCount', 'LegalCustomerCount') || 0),
    excellentCount: Number(pick(row, 'excellentCount', 'ExcellentCount') || 0),
    goodCount: Number(pick(row, 'goodCount', 'GoodCount') || 0),
    weakCount: Number(pick(row, 'weakCount', 'WeakCount') || 0),
    laggingCount: Number(pick(row, 'laggingCount', 'LaggingCount') || 0),
    badCount: Number(pick(row, 'badCount', 'BadCount') || 0),
    remainingLt100Count: Number(pick(row, 'remainingLt100Count', 'RemainingLt100Count') || 0),
    remaining100To200Count: Number(pick(row, 'remaining100To200Count', 'Remaining100To200Count') || 0),
    requiredDueToday: Number(pick(row, 'requiredDueToday', 'RequiredDueToday') || 0),
    collectedYesterday: Number(pick(row, 'collectedYesterday', 'CollectedYesterday') || 0),
    collectedLastFriday: Number(pick(row, 'collectedLastFriday', 'CollectedLastFriday') || 0),
    lastFridayDate: pick(row, 'lastFridayDate', 'LastFridayDate') || null,
    collectedLast30Days: (pick(row, 'collectedLast30Days', 'CollectedLast30Days') || []).map(normDayAmount),
    collectedLast30Fridays: (pick(row, 'collectedLast30Fridays', 'CollectedLast30Fridays') || []).map(normDayAmount),
    tierMoney: (pick(row, 'tierMoney', 'TierMoney') || []).map(normTierMoney),
    realRatingLast30Days: (pick(row, 'realRatingLast30Days', 'RealRatingLast30Days') || []).map(normDayRating),
    boardOperatorLast30Days: (pick(row, 'boardOperatorLast30Days', 'BoardOperatorLast30Days') || []).map(normDayRating),
    followerCommitteeLast30Days: (pick(row, 'followerCommitteeLast30Days', 'FollowerCommitteeLast30Days') || []).map(normDayRating),
    supervisionCommitteeLast30Days: (pick(row, 'supervisionCommitteeLast30Days', 'SupervisionCommitteeLast30Days') || []).map(normDayRating),
    salesDelegateLast30Days: (pick(row, 'salesDelegateLast30Days', 'SalesDelegateLast30Days') || []).map(normDayRating),
    conversion: {
      improved: Number(pick(conv, 'improved', 'Improved') || 0),
      declined: Number(pick(conv, 'declined', 'Declined') || 0),
      unchanged: Number(pick(conv, 'unchanged', 'Unchanged') || 0),
      summaryText: String(pick(conv, 'summaryText', 'SummaryText') || ''),
      targets: (pick(conv, 'targets', 'Targets') || [])
        .map(t => ({
          targetLabel: String(t.targetLabel ?? t.TargetLabel ?? ''),
          arrivedCount: Number(t.arrivedCount ?? t.ArrivedCount ?? 0),
          detailText: String(t.detailText ?? t.DetailText ?? ''),
          fromBreakdown: (t.fromBreakdown ?? t.FromBreakdown ?? []).map(f => ({
            fromLabel: String(f.fromLabel ?? f.FromLabel ?? ''),
            count: Number(f.count ?? f.Count ?? 0),
          })),
          customers: (t.customers ?? t.Customers ?? []).map(c => ({
            customerId: Number(c.customerId ?? c.CustomerId ?? 0),
            customerName: String(c.customerName ?? c.CustomerName ?? ''),
            fromLabel: String(c.fromLabel ?? c.FromLabel ?? ''),
          })),
          last30Days: (t.last30Days ?? t.Last30Days ?? []).map(d => ({
            date: d.date ?? d.Date ?? null,
            arrivedCount: Number(d.arrivedCount ?? d.ArrivedCount ?? 0),
            fromBreakdown: (d.fromBreakdown ?? d.FromBreakdown ?? []).map(f => ({
              fromLabel: String(f.fromLabel ?? f.FromLabel ?? ''),
              count: Number(f.count ?? f.Count ?? 0),
            })),
            customers: (d.customers ?? d.Customers ?? []).map(c => ({
              customerId: Number(c.customerId ?? c.CustomerId ?? 0),
              customerName: String(c.customerName ?? c.CustomerName ?? ''),
              fromLabel: String(c.fromLabel ?? c.FromLabel ?? ''),
            })),
          })),
        }))
        .filter(t => t.targetLabel && t.targetLabel !== 'غير شيء'),
    },
    boardOperatorRating: {
      listId: Number(pick(board, 'listId', 'ListId') || 0),
      ratingDate: pick(board, 'ratingDate', 'RatingDate') || null,
      score: pick(board, 'score', 'Score') == null ? null : Number(pick(board, 'score', 'Score')),
      note: pick(board, 'note', 'Note') || null,
      hasRating: Boolean(pick(board, 'hasRating', 'HasRating')),
    },
    followerCommitteeRating: normInputRating(pick(row, 'followerCommitteeRating', 'FollowerCommitteeRating')),
    supervisionCommitteeRating: normInputRating(pick(row, 'supervisionCommitteeRating', 'SupervisionCommitteeRating')),
    salesDelegateRating: normInputRating(pick(row, 'salesDelegateRating', 'SalesDelegateRating')),
  }
}

const tierCountMap = computed(() => {
  const s = summary.value
  if (!s)
    return {}

  return {
    ممتاز: s.excellentCount,
    جيد: s.goodCount,
    ضعيف: s.weakCount,
    متلكئ: s.laggingCount,
    سيء: s.badCount,
  }
})

const tierMoneyByLabel = computed(() => {
  const map = {}
  for (const t of summary.value?.tierMoney || [])
    map[t.label] = t

  return map
})

const filteredCustomers = computed(() => {
  let rows = customers.value
  const f = tableFilter.value
  if (f === 'legal')
    rows = rows.filter(c => c.isLegal)
  else if (f === 'lt100')
    rows = rows.filter(c => c.amountRemaining > 0 && c.amountRemaining < 100000)
  else if (f === '100to200')
    rows = rows.filter(c => c.amountRemaining >= 100000 && c.amountRemaining < 200000)
  else if (f && f !== 'all')
    rows = rows.filter(c => !c.isLegal && c.phantomRatingLabel === f)

  return rows
})

function persistColumnOrder() {
  localStorage.setItem(COLS_STORAGE_KEY, JSON.stringify(columnOrder.value))
}

function moveColumn(fromKey, toKey) {
  if (!fromKey || !toKey || fromKey === toKey)
    return
  const order = [...columnOrder.value]
  const from = order.indexOf(fromKey)
  const to = order.indexOf(toKey)
  if (from < 0 || to < 0)
    return
  order.splice(from, 1)
  order.splice(to, 0, fromKey)
  columnOrder.value = order
  persistColumnOrder()
}

function resetColumnOrder() {
  columnOrder.value = defaultColumnOrder()
  persistColumnOrder()
  toast.success('تمت إعادة ترتيب الأعمدة للافتراضي')
}

function clearHeaderHold() {
  clearTimeout(headerHoldTimer)
  headerHoldTimer = null
  headerPressKey.value = null
  headerHoldReady.value = false
}

function onHeaderPointerDown(key) {
  headerPressKey.value = key
  headerDownAt = Date.now()
  headerHoldReady.value = false
  clearTimeout(headerHoldTimer)
  headerHoldTimer = setTimeout(() => {
    if (headerPressKey.value === key)
      headerHoldReady.value = true
  }, 400)
}

function onHeaderDragStart(e, key) {
  const heldLongEnough = headerHoldReady.value || (Date.now() - headerDownAt >= 400)
  if (!heldLongEnough || headerPressKey.value !== key) {
    e.preventDefault()
    clearHeaderHold()

    return
  }
  dragFromKey.value = key
  e.dataTransfer.effectAllowed = 'move'
  try {
    e.dataTransfer.setData('text/plain', key)
  }
  catch { /* ignore */ }
}

function onHeaderDragOver(e, key) {
  if (!dragFromKey.value)
    return
  e.preventDefault()
  dragOverKey.value = key
}

function onHeaderDrop(e, key) {
  e.preventDefault()
  const from = dragFromKey.value || e.dataTransfer.getData('text/plain')
  moveColumn(from, key)
  dragFromKey.value = null
  dragOverKey.value = null
  clearHeaderHold()
}

function onHeaderDragEnd() {
  dragFromKey.value = null
  dragOverKey.value = null
  clearHeaderHold()
}

function exportCellValue(row, key) {
  switch (key) {
  case 'customerName': return row.customerName || 'لا يوجد'
  case 'phantomRatingLabel': return row.isLegal ? 'قانونية' : (row.phantomRatingLabel || '—')
  case 'itemsNames': return row.itemsNames || 'لا يوجد'
  case 'dateSaleDevice': return fmtDate(row.dateSaleDevice)
  case 'amountTotalSales': return row.amountTotalSales ?? 'لا يوجد'
  case 'amountDaySales': return row.amountDaySales ?? 'لا يوجد'
  case 'receiptsTotal': return row.receiptsTotal ?? 'لا يوجد'
  case 'amountRemaining': return row.amountRemaining ?? 'لا يوجد'
  case 'lastPaymentDate': return fmtDate(row.lastPaymentDate)
  case 'lastPaymentAmount': return row.lastPaymentAmount ?? 'لا يوجد'
  case 'receiptRateDevice':
    return row.receiptRateDevice != null ? `${row.receiptRateDevice}%` : '0%'
  case 'countReceiptDevice': return row.countReceiptDevice ?? 'لا يوجد'
  case 'numberOfDayDevice': return row.numberOfDayDevice ?? 'لا يوجد'
  case 'address': return row.address || 'لا يوجد'
  case 'phoneNumber': return row.phoneNumber || 'لا يوجد'
  case 'shopName': return row.shopName || 'لا يوجد'
  case 'receiptName': return row.receiptName || 'لا يوجد'
  case 'saleName': return row.saleName || 'لا يوجد'
  case 'notes': return row.notes || 'لا يوجد'
  default: return ''
  }
}

function exportToExcel() {
  const cols = tableHeaders.value
    .map(h => ALL_COLUMN_DEFS.find(c => c.key === h.key))
    .filter(c => c && c.exportable !== false)
  if (!cols.length || !filteredCustomers.value.length) {
    toast.warning('لا توجد صفوف للتصدير')

    return
  }
  const dataToExport = filteredCustomers.value.map(row => {
    const obj = {}
    for (const c of cols)
      obj[c.title] = exportCellValue(row, c.key)

    return obj
  })
  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(workbook, worksheet, 'Customers')
  XLSX.writeFile(workbook, `متابعة_مندوبين_${cityKey() || 'city'}_${selectedListId.value || 'list'}.xlsx`)
  toast.success(`تم تصدير ${dataToExport.length} زبون`)
}

function openCustomerProfile(item) {
  const city = cityKey()
  const customerId = item?.customerID
  if (!city || !customerId) {
    toast.warning('تعذر فتح بروفايل الزبون')

    return
  }
  router.push({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: city,
      customerId: String(customerId),
      name: item.customerName || '',
      phone: item.phoneNumber || '',
      returnTo: 'director-delegate-followup',
      listId: selectedListId.value ? String(selectedListId.value) : undefined,
    },
  })
}

function openCustomerDetailsPage() {
  const city = cityKey()
  const listId = selectedListId.value
  if (!city || !listId)
    return
  router.push({
    path: '/director-delegate-followup-customers',
    query: {
      city,
      listId: String(listId),
      ...(tableFilter.value && tableFilter.value !== 'all' ? { filter: tableFilter.value } : {}),
    },
  })
}

function openHistory(title, rows, kind = 'amount', extra = '') {
  historyTitle.value = title
  historyRows.value = Array.isArray(rows) ? rows : []
  historyKind.value = kind
  historyExtra.value = extra || ''
  historyDialog.value = true
}

function onRealRatingClick() {
  openHistory(
    'التقييم الحقيقي — آخر 30 يوماً',
    summary.value?.realRatingLast30Days || [],
    'rating',
  )
}

function onBoardOpClick(ev) {
  if (ev?.target?.closest?.('button'))
    return
  openHistory(
    'تقييم عامل اللوحة — آخر 30 يوماً',
    summary.value?.boardOperatorLast30Days || [],
    'board',
  )
}

function onInputRatingClick(slot) {
  openHistory(
    `${slot.label} — آخر 30 يوماً`,
    summary.value?.[slot.historyKey] || [],
    'board',
  )
}

function onRevenueClick(title, rows) {
  openHistory(title, rows || [], 'amount')
}

function onFridayClick() {
  openHistory(
    'واردات الجمع — آخر 30 جمعة',
    summary.value?.collectedLast30Fridays || [],
    'amount',
  )
}

function onConversionTargetClick(target) {
  if (!target)
    return
  openHistory(
    `التحويل إلى ${target.targetLabel} — آخر 30 يوماً`,
    target.last30Days || [],
    'conversion',
    target.detailText || '',
  )
}

function onConversionDateClick(row) {
  const customers = row?.customers || []
  namesTitle.value = `المتحولون — ${fmtDate(row?.date)}`
  namesRows.value = customers
  namesDialog.value = true
}

function toggleTableFilter(key) {
  if (tableVisible.value && tableFilter.value === key) {
    tableVisible.value = false
    tableFilter.value = 'all'

    return
  }
  tableFilter.value = key
  tableVisible.value = true
}

function showTableWithFilter(key = 'all') {
  tableFilter.value = key || 'all'
  tableVisible.value = true
}

function openRateDialog(ev) {
  ev?.stopPropagation?.()
  const cur = summary.value?.boardOperatorRating?.score
  rateScore.value = cur == null ? 8 : Number(cur)
  rateNote.value = summary.value?.boardOperatorRating?.note || ''
  rateDialog.value = true
}

async function saveBoardOperatorRating() {
  const city = cityKey()
  const listId = selectedListId.value
  if (!city || !listId)
    return
  const score = Number(rateScore.value)
  if (!Number.isFinite(score) || score < 0 || score > 10) {
    toast.error('التقييم يجب أن يكون بين 0 و 10')

    return
  }
  rateBusy.value = true
  try {
    const row = await directorDelegateFollowupUpsertBoardOperatorRating({
      cityValue: city,
      listId,
      score,
      note: rateNote.value,
    })
    if (summary.value) {
      summary.value.boardOperatorRating = {
        listId,
        ratingDate: row?.ratingDate ?? row?.RatingDate ?? baghdadTodayIso.value,
        score: Number(row?.score ?? row?.Score ?? score),
        note: row?.note ?? row?.Note ?? (rateNote.value || null),
        hasRating: true,
      }
    }
    rateDialog.value = false
    toast.success('تم حفظ تقييم عامل اللوحة')
    await loadListDetail({ force: true })
  }
  catch (e) {
    toast.error(softError(e, 'تعذر حفظ التقييم'))
  }
  finally {
    rateBusy.value = false
  }
}

async function ensureCityQuery() {
  const q = cityKey()
  if (q) {
    localStorage.setItem(CITY_STORAGE_KEY, q)

    return q
  }
  const stored = String(localStorage.getItem(CITY_STORAGE_KEY) || '').trim()
  if (stored) {
    await router.replace({ query: { ...route.query, city: stored } })

    return stored
  }

  return ''
}

async function openCity(value) {
  const v = String(value || '').trim()
  if (!v)
    return
  localStorage.setItem(CITY_STORAGE_KEY, v)
  await router.push({ query: { city: v } })
}

async function clearCity() {
  localStorage.removeItem(CITY_STORAGE_KEY)
  await router.push({ query: {} })
  listCards.value = []
  summary.value = null
  customers.value = []
  tableVisible.value = false
}

async function openList(listId) {
  const city = cityKey()
  if (!city || !listId)
    return
  await router.push({ query: { city, listId: String(listId) } })
}

async function backToLists() {
  const city = cityKey()
  await router.push({ query: city ? { city } : {} })
  summary.value = null
  customers.value = []
  tableVisible.value = false
  tableFilter.value = null
}

async function loadCities() {
  try {
    await fetchCities()
    const rows = await directorCities()
    gatewayCities.value = Array.isArray(rows) ? rows : []
  }
  catch {
    gatewayCities.value = []
  }
}

async function loadLists() {
  const city = cityKey()
  if (!city) {
    listCards.value = []

    return
  }
  listsBusy.value = true
  try {
    const rows = await directorDelegateFollowupLists({ cityValue: city })
    listCards.value = (Array.isArray(rows) ? rows : []).map(normListCard).filter(c => c.listId > 0)
  }
  catch (e) {
    listCards.value = []
    toast.error(softError(e, 'تعذر تحميل قوائم المندوبين'))
  }
  finally {
    listsBusy.value = false
  }
}

function isFollowupRouteActive() {
  return route.name === 'director-delegate-followup'
    || String(route.path || '').endsWith('/director-delegate-followup')
}

async function preloadCustomers(city, listId, cacheKey) {
  if (customersCacheKey.value === cacheKey && customers.value.length)
    return
  customersBusy.value = true
  try {
    const cust = await directorDelegateFollowupListCustomers({ cityValue: city, listId })
    customers.value = (Array.isArray(cust) ? cust : []).map(normCustomer).filter(c => c.customerID > 0)
    customersCacheKey.value = cacheKey
  }
  catch (e) {
    customers.value = []
    customersCacheKey.value = ''
    toast.error(softError(e, 'تعذر تحميل زبائن القائمة'))
  }
  finally {
    customersBusy.value = false
  }
}

async function loadListDetail({ force = false } = {}) {
  const city = cityKey()
  const listId = selectedListId.value
  if (!city || !listId) {
    summary.value = null
    detailCacheKey.value = ''

    return
  }
  const cacheKey = `${city}|${listId}`
  if (!force && detailCacheKey.value === cacheKey && summary.value) {
    preloadCustomers(city, listId, cacheKey)

    return
  }

  summaryBusy.value = true
  tableVisible.value = false
  tableFilter.value = 'all'
  try {
    const sumP = directorDelegateFollowupListSummary({ cityValue: city, listId })
      .then(sum => {
        summary.value = normSummary(sum)
        detailCacheKey.value = cacheKey
      })
    const custP = preloadCustomers(city, listId, cacheKey)
    await Promise.allSettled([sumP, custP])
  }
  catch (e) {
    summary.value = null
    detailCacheKey.value = ''
    toast.error(softError(e, 'تعذر تحميل ملخص القائمة'))
  }
  finally {
    summaryBusy.value = false
  }
}

watch(
  () => [route.name, route.path, route.query.city, route.query.listId],
  async () => {
    if (!isDirectorOrMonitor.value || !isFollowupRouteActive())
      return
    await ensureCityQuery()
    if (!cityKey())
      return
    if (selectedListId.value > 0)
      await loadListDetail()
    else
      await loadLists()
  },
)

onMounted(async () => {
  if (!isDirectorOrMonitor.value)
    return
  await loadCities()
  await ensureCityQuery()
  if (cityKey() && selectedListId.value > 0)
    await loadListDetail()
  else if (cityKey())
    await loadLists()
})
</script>

<template>
  <div class="dfu">
    <div class="dfu-head">
      <div>
        <h1 class="dfu-title">
          متابعة المندوبين
        </h1>
        <p class="dfu-sub muted">
          اضغط على الكونتينر لعرض تفاصيل آخر 30 يوماً — واضغط فئة غير المصفرين لإظهار/إخفاء الجدول
        </p>
      </div>
      <div class="dfu-actions">
        <VBtn
          v-if="cityKey()"
          variant="tonal"
          color="secondary"
          prepend-icon="tabler-map-pin"
          @click="clearCity"
        >
          تغيير المحافظة
        </VBtn>
        <VBtn
          v-if="selectedListId > 0"
          variant="tonal"
          color="primary"
          prepend-icon="tabler-arrow-right"
          @click="backToLists"
        >
          رجوع للقوائم
        </VBtn>
      </div>
    </div>

    <div
      v-if="!cityKey()"
      class="city-grid"
    >
      <button
        v-for="c in cityCards"
        :key="c.value"
        type="button"
        class="city-card"
        @click="openCity(c.value)"
      >
        <strong>{{ c.name || c.value }}</strong>
        <small class="muted">{{ c.value }}</small>
      </button>
    </div>

    <template v-else-if="selectedListId <= 0">
      <div
        v-if="listsBusy"
        class="muted pa-4"
      >
        جاري تحميل القوائم…
      </div>
      <div
        v-else
        class="list-cards"
      >
        <button
          v-for="card in listCards"
          :key="card.listId"
          type="button"
          class="list-card"
          @click="openList(card.listId)"
        >
          <strong class="list-card__name">{{ card.listName }}</strong>
          <span class="list-card__score">{{ fmtScore(card.realRating) }}</span>
          <span class="list-card__label">{{ card.realRatingLabel || 'التقييم الحقيقي' }}</span>
          <span class="list-card__count">
            غير المصفرين:
            <b>{{ card.nonZeroedCustomerCount }}</b>
          </span>
          <span class="list-card__meta muted">آخر تسديد: {{ fmtDate(card.lastPaymentDate) }}</span>
        </button>
      </div>
    </template>

    <template v-else>
      <div class="list-detail-head">
        <div>
          <h2>{{ selectedListName }}</h2>
          <p class="muted">
            لوحة المؤشرات — اضغط الفئة لإظهار الجدول أو استخدم عرض التفاصيل
          </p>
        </div>
        <VBtn
          color="primary"
          prepend-icon="tabler-table"
          @click="showTableWithFilter(tableFilter || 'all')"
        >
          عرض التفاصيل
        </VBtn>
      </div>

      <div
        v-if="summaryBusy && !summary"
        class="muted pa-4"
      >
        جاري تحميل اللوحة…
      </div>

      <template v-else-if="summary">
        <section class="line-box">
          <h3 class="line-box__title">
            التقييمات
          </h3>
          <div class="sub-grid">
            <button
              type="button"
              class="sub-card sub-card--click"
              @click="onRealRatingClick"
            >
              <span class="sub-card__label">التقييم الحقيقي</span>
              <strong class="sub-card__value">{{ fmtScore(summary.realRating) }}</strong>
              <small>{{ summary.realRatingLabel || '—' }}</small>
              <small :class="trendClass(summary.ratingTrend)">
                قبل أسبوع: {{ fmtScore(summary.realRatingWeekAgo) }} ({{ trendLabel(summary.ratingTrend) }})
              </small>
            </button>

            <div
              class="sub-card sub-card--click sub-card--action"
              role="button"
              tabindex="0"
              @click="onBoardOpClick"
              @keydown.enter="onBoardOpClick"
            >
              <span class="sub-card__label">تقييم عامل اللوحة</span>
              <template v-if="summary.boardOperatorRating?.score != null">
                <strong class="sub-card__value">{{ summary.boardOperatorRating.score }} / 10</strong>
                <small v-if="summary.boardOperatorRating.note">{{ summary.boardOperatorRating.note }}</small>
              </template>
              <template v-else>
                <div class="sub-card__empty">
                  لا يوجد تقييم لهذا اليوم
                </div>
              </template>
              <VBtn
                size="small"
                color="primary"
                class="mt-2"
                @click="openRateDialog"
              >
                قيم المندوب ليوم {{ baghdadTodayIso }}
              </VBtn>
            </div>

            <button
              v-for="slot in INPUT_RATING_SLOTS"
              :key="slot.key"
              type="button"
              class="sub-card sub-card--click"
              @click="onInputRatingClick(slot)"
            >
              <span class="sub-card__label">{{ slot.label }}</span>
              <template v-if="summary[slot.summaryKey]?.score != null">
                <strong class="sub-card__value">{{ summary[slot.summaryKey].score }} / 10</strong>
                <small v-if="summary[slot.summaryKey].note">{{ summary[slot.summaryKey].note }}</small>
                <small
                  v-if="summary[slot.summaryKey].ratedByUserName"
                  class="muted"
                >{{ summary[slot.summaryKey].ratedByUserName }}</small>
                <small
                  v-if="summary[slot.summaryKey].ratingDate"
                  class="muted"
                >{{ fmtDate(summary[slot.summaryKey].ratingDate) }}</small>
              </template>
              <template v-else>
                <div class="sub-card__empty">
                  لا يوجد تقييم بعد
                </div>
                <small class="muted">{{ slot.emptyHint }}</small>
              </template>
            </button>
          </div>
        </section>

        <section class="line-box">
          <h3 class="line-box__title">
            الواردات
          </h3>
          <div class="sub-grid">
            <div class="sub-card">
              <span class="sub-card__label">القسط المطلوب اليوم</span>
              <strong class="sub-card__value">{{ fmtNum(summary.requiredDueToday) }}</strong>
              <small>مجموع قسط غير المصفرين عدا القانونية</small>
            </div>

            <button
              type="button"
              class="sub-card sub-card--click sub-card--warn"
              @click="openUnpaidNewSalesDialog"
            >
              <span class="sub-card__label">مبيعات جديدة بدون جباية (3 أيام+)</span>
              <strong class="sub-card__value">{{ unpaidNewSalesCount }}</strong>
              <small>زبائن مسجّلون كمبيع وما عليهم أي تسديد</small>
            </button>

            <button
              type="button"
              class="sub-card sub-card--click"
              @click="onRevenueClick('واردات القائمة — آخر 30 يوماً', summary.collectedLast30Days)"
            >
              <span class="sub-card__label">واردات القائمة ليوم أمس</span>
              <strong class="sub-card__value">{{ fmtNum(summary.collectedYesterday) }}</strong>
            </button>

            <button
              type="button"
              class="sub-card sub-card--click"
              @click="onFridayClick"
            >
              <span class="sub-card__label">واردات يوم آخر جمعة</span>
              <strong class="sub-card__value">{{ fmtNum(summary.collectedLastFriday) }}</strong>
              <small>{{ fmtDate(summary.lastFridayDate) }}</small>
            </button>

            <button
              v-for="b in RATING_BUCKETS"
              :key="`tier-${b.key}`"
              type="button"
              class="sub-card sub-card--click"
              @click="onRevenueClick(`واردات ${b.label} — آخر 30 يوماً`, tierMoneyByLabel[b.key]?.collectedLast30Days)"
            >
              <span class="sub-card__label">واردات {{ b.label }}</span>
              <strong class="sub-card__value">
                {{ fmtNum(tierMoneyByLabel[b.key]?.requiredDue) }}
              </strong>
              <small>القسط المطلوب (افتراضي)</small>
              <div class="sub-card__row">
                <span>أمس:</span>
                <b>{{ fmtNum(tierMoneyByLabel[b.key]?.collectedYesterday) }}</b>
              </div>
            </button>
          </div>
        </section>

        <section class="line-box">
          <h3 class="line-box__title">
            غير المصفرين ({{ summary.nonZeroedCustomerCount }})
          </h3>
          <div class="sub-grid">
            <button
              v-for="b in RATING_BUCKETS"
              :key="`nz-${b.key}`"
              type="button"
              class="sub-card sub-card--click"
              :class="{ 'sub-card--active': tableVisible && tableFilter === b.key }"
              @click="toggleTableFilter(b.key)"
            >
              <span class="sub-card__label">{{ b.label }}</span>
              <strong class="sub-card__value">{{ tierCountMap[b.key] ?? 0 }}</strong>
              <small>القسط المطلوب: {{ fmtNum(tierMoneyByLabel[b.key]?.requiredDue) }}</small>
              <small class="muted">أمس وارد: {{ fmtNum(tierMoneyByLabel[b.key]?.collectedYesterday) }}</small>
            </button>
            <button
              type="button"
              class="sub-card sub-card--click"
              :class="{ 'sub-card--active': tableVisible && tableFilter === 'lt100' }"
              @click="toggleTableFilter('lt100')"
            >
              <span class="sub-card__label">باقي أقل من 100,000</span>
              <strong class="sub-card__value">{{ summary.remainingLt100Count }}</strong>
            </button>
            <button
              type="button"
              class="sub-card sub-card--click"
              :class="{ 'sub-card--active': tableVisible && tableFilter === '100to200' }"
              @click="toggleTableFilter('100to200')"
            >
              <span class="sub-card__label">باقي 100,000 – أقل من 200,000</span>
              <strong class="sub-card__value">{{ summary.remaining100To200Count }}</strong>
            </button>
            <button
              type="button"
              class="sub-card sub-card--click sub-card--legal"
              :class="{ 'sub-card--active': tableVisible && tableFilter === 'legal' }"
              @click="toggleTableFilter('legal')"
            >
              <span class="sub-card__label">القانونية</span>
              <strong class="sub-card__value">{{ summary.legalCustomerCount }}</strong>
            </button>
          </div>
        </section>

        <section class="line-box">
          <h3 class="line-box__title">
            التحويل
          </h3>
          <p class="muted kpi-hint">
            {{ summary.conversion.summaryText }} — اضغط الوجهة لمعرفة من أين جاؤوا (آخر 30 يوماً)
          </p>
          <div class="sub-grid">
            <button
              v-for="t in summary.conversion.targets"
              :key="`conv-${t.targetLabel}`"
              type="button"
              class="sub-card sub-card--click sub-card--conversion"
              @click="onConversionTargetClick(t)"
            >
              <span class="sub-card__label">إلى {{ t.targetLabel }}</span>
              <strong class="sub-card__value">{{ t.arrivedCount }} متحول</strong>
              <small>{{ t.detailText || 'لا متحولين اليوم' }}</small>
            </button>
          </div>
        </section>

        <section
          v-if="tableVisible"
          class="table-panel"
        >
          <div class="table-panel__head">
            <strong>جدول الزبائن</strong>
            <VSelect
              v-model="tableFilter"
              :items="statusFilterItems"
              item-title="title"
              item-value="value"
              label="فلتر الحالة"
              density="compact"
              variant="outlined"
              hide-details
              style="max-width: 220px"
            />
            <span class="muted">{{ filteredCustomers.length }} زبون</span>
            <VSpacer />
            <VBtn
              size="small"
              variant="tonal"
              prepend-icon="tabler-columns"
              @click="resetColumnOrder"
            >
              ترتيب افتراضي
            </VBtn>
            <VBtn
              size="small"
              color="success"
              prepend-icon="tabler-file-spreadsheet"
              :disabled="!filteredCustomers.length"
              @click="exportToExcel"
            >
              تصدير Excel
            </VBtn>
            <VBtn
              size="small"
              color="primary"
              variant="tonal"
              @click="openCustomerDetailsPage"
            >
              صفحة التفاصيل الكاملة
            </VBtn>
            <VBtn
              size="small"
              variant="text"
              @click="tableVisible = false"
            >
              إخفاء
            </VBtn>
          </div>
          <p class="muted table-hint">
            كل صف سطر واحد — رأس الأعمدة ثابت عند التمرير داخل الجدول — اضغط مطولاً على رأس العمود ثم اسحبه لإعادة الترتيب
          </p>
          <div class="dfu-inline-table-host">
            <VDataTable
              :headers="tableHeaders"
              :items="filteredCustomers"
              :loading="customersBusy"
              item-value="customerID"
              density="compact"
              class="dfu-inline-table"
              hide-default-footer
              :items-per-page="-1"
            >
              <template #headers="{ columns }">
                <tr>
                  <th
                    v-for="column in columns"
                    :key="column.key"
                    class="dfu-th"
                    :class="{
                      'dfu-th--armed': headerHoldReady && headerPressKey === column.key,
                      'dfu-th--over': dragOverKey === column.key,
                    }"
                    draggable="true"
                    @pointerdown="onHeaderPointerDown(column.key)"
                    @pointercancel="clearHeaderHold"
                    @dragstart="onHeaderDragStart($event, column.key)"
                    @dragover="onHeaderDragOver($event, column.key)"
                    @drop="onHeaderDrop($event, column.key)"
                    @dragend="onHeaderDragEnd"
                  >
                    <span class="dfu-th__label">{{ column.title }}</span>
                  </th>
                </tr>
              </template>
              <template #item.profile="{ item }">
                <VBtn
                  size="x-small"
                  color="secondary"
                  variant="tonal"
                  class="dfu-cell-nowrap"
                  @click="openCustomerProfile(item)"
                >
                  بروفايل
                </VBtn>
              </template>
              <template #item.customerName="{ item }">
                <span
                  class="dfu-cell-nowrap dfu-customer-name"
                  :title="item.customerName || ''"
                >{{ item.customerName || '—' }}</span>
              </template>
              <template #item.phantomRatingLabel="{ item }">
                <span class="dfu-cell-nowrap">{{ item.isLegal ? 'قانونية' : (item.phantomRatingLabel || '—') }}</span>
              </template>
              <template #item.amountDaySales="{ item }">
                <span class="dfu-cell-nowrap">{{ fmtNum(item.amountDaySales) }}</span>
              </template>
              <template #item.amountRemaining="{ item }">
                <span class="dfu-cell-nowrap">{{ fmtNum(item.amountRemaining) }}</span>
              </template>
              <template #item.receiptsTotal="{ item }">
                <span class="dfu-cell-nowrap">{{ fmtNum(item.receiptsTotal) }}</span>
              </template>
              <template #item.amountTotalSales="{ item }">
                <span class="dfu-cell-nowrap">{{ fmtNum(item.amountTotalSales) }}</span>
              </template>
              <template #item.lastPaymentAmount="{ item }">
                <span class="dfu-cell-nowrap">{{ fmtNum(item.lastPaymentAmount) }}</span>
              </template>
              <template #item.lastPaymentDate="{ item }">
                <span class="dfu-cell-nowrap">{{ fmtDate(item.lastPaymentDate) }}</span>
              </template>
              <template #item.dateSaleDevice="{ item }">
                <span class="dfu-cell-nowrap">{{ fmtDate(item.dateSaleDevice) }}</span>
              </template>
              <template #item.address="{ item }">
                <span
                  class="dfu-cell-nowrap"
                  :title="item.address || ''"
                >{{ item.address || '—' }}</span>
              </template>
              <template #item.phoneNumber="{ item }">
                <span class="dfu-cell-nowrap">{{ item.phoneNumber || '—' }}</span>
              </template>
              <template #item.shopName="{ item }">
                <span
                  class="dfu-cell-nowrap"
                  :title="item.shopName || ''"
                >{{ item.shopName || '—' }}</span>
              </template>
              <template #item.receiptName="{ item }">
                <span class="dfu-cell-nowrap">{{ item.receiptName || '—' }}</span>
              </template>
              <template #item.saleName="{ item }">
                <span class="dfu-cell-nowrap">{{ item.saleName || '—' }}</span>
              </template>
              <template #item.itemsNames="{ item }">
                <span
                  class="dfu-cell-nowrap"
                  :title="item.itemsNames || ''"
                >{{ item.itemsNames || '—' }}</span>
              </template>
              <template #item.notes="{ item }">
                <span
                  class="dfu-cell-nowrap"
                  :title="item.notes || ''"
                >{{ item.notes || '—' }}</span>
              </template>
              <template #item.receiptRateDevice="{ item }">
                <span class="dfu-cell-nowrap">{{ item.receiptRateDevice != null ? `${item.receiptRateDevice}%` : '—' }}</span>
              </template>
              <template #item.countReceiptDevice="{ item }">
                <span class="dfu-cell-nowrap">{{ item.countReceiptDevice ?? '—' }}</span>
              </template>
              <template #item.numberOfDayDevice="{ item }">
                <span class="dfu-cell-nowrap">{{ item.numberOfDayDevice ?? '—' }}</span>
              </template>
            </VDataTable>
          </div>
        </section>
      </template>
    </template>

    <VDialog
      v-model="historyDialog"
      max-width="720"
    >
      <VCard>
        <VCardTitle>{{ historyTitle }}</VCardTitle>
        <VCardText>
          <p
            v-if="historyExtra"
            class="muted mb-3"
          >
            اليوم: {{ historyExtra }}
          </p>
          <div
            v-if="!historyRows.length"
            class="sub-card__empty"
            style="min-height: 80px"
          >
            لا توجد بيانات لهذه الفترة بعد
          </div>
          <ul
            v-else
            class="hist-list hist-list--lg"
          >
            <li
              v-for="(row, idx) in historyRows"
              :key="idx"
              :class="{ 'hist-list__row--click': historyKind === 'conversion' }"
              @click="historyKind === 'conversion' ? onConversionDateClick(row) : undefined"
            >
              <span
                :class="{ 'hist-date--link': historyKind === 'conversion' }"
              >{{ fmtDate(row.date) }}</span>
              <template v-if="historyKind === 'amount'">
                <b>{{ fmtNum(row.amount) }}</b>
              </template>
              <template v-else-if="historyKind === 'rating'">
                <b>{{ fmtScore(row.score) }} · {{ row.label || '—' }}</b>
              </template>
              <template v-else-if="historyKind === 'board'">
                <b>{{ row.boardOperatorScore == null ? '—' : `${row.boardOperatorScore}/10` }}</b>
              </template>
              <template v-else-if="historyKind === 'conversion'">
                <div class="conv-day">
                  <b>{{ row.arrivedCount || 0 }} متحول</b>
                  <small class="muted">
                    {{
                      (row.fromBreakdown || []).length
                        ? (row.fromBreakdown || []).map(f => `من ${f.fromLabel}: ${f.count}`).join(' · ')
                        : '—'
                    }}
                  </small>
                </div>
              </template>
            </li>
          </ul>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="historyDialog = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="namesDialog"
      max-width="480"
    >
      <VCard>
        <VCardTitle>{{ namesTitle }}</VCardTitle>
        <VCardText>
          <div
            v-if="!namesRows.length"
            class="sub-card__empty"
            style="min-height: 64px"
          >
            لا زبائن متحولين في هذا اليوم
          </div>
          <ul
            v-else
            class="hist-list hist-list--lg"
          >
            <li
              v-for="c in namesRows"
              :key="c.customerId"
            >
              <span>{{ c.customerName || `#${c.customerId}` }}</span>
              <small class="muted">من {{ c.fromLabel || '—' }}</small>
            </li>
          </ul>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="namesDialog = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="unpaidNewSalesDialog"
      max-width="920"
    >
      <VCard>
        <VCardTitle>مبيعات جديدة بدون جباية (3 أيام+)</VCardTitle>
        <VCardText>
          <p class="muted mb-3">
            {{ unpaidNewSalesCount }} زبون — مسجّل كمبيع وما عليه أي تسديد منذ {{ NEW_SALE_UNPAID_MIN_DAYS }} أيام فأكثر
          </p>
          <div
            v-if="!unpaidNewSalesCustomers.length"
            class="sub-card__empty"
            style="min-height: 80px"
          >
            لا يوجد زبائن بهذه الحالة حالياً
          </div>
          <div
            v-else
            class="unpaid-new-table-host"
          >
            <table class="unpaid-new-table">
              <thead>
                <tr>
                  <th>الزبون</th>
                  <th>سعر البيع</th>
                  <th>الواصل</th>
                  <th>الباقي</th>
                  <th>تاريخ البيع</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="c in unpaidNewSalesCustomers"
                  :key="c.customerID"
                >
                  <td
                    class="unpaid-new-table__name"
                    :title="c.customerName || ''"
                  >
                    {{ c.customerName || '—' }}
                  </td>
                  <td>{{ fmtNum(c.amountTotalSales) }}</td>
                  <td>{{ fmtNum(c.receiptsTotal) }}</td>
                  <td>{{ fmtNum(c.amountRemaining) }}</td>
                  <td>{{ fmtDate(c.dateSaleDevice) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="unpaidNewSalesDialog = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="rateDialog"
      max-width="420"
    >
      <VCard>
        <VCardTitle>قيم المندوب ليوم {{ baghdadTodayIso }}</VCardTitle>
        <VCardText class="d-flex flex-column gap-3">
          <VTextField
            v-model.number="rateScore"
            type="number"
            min="0"
            max="10"
            step="1"
            label="التقييم (0–10)"
            density="compact"
            hide-details
          />
          <VTextarea
            v-model="rateNote"
            label="ملاحظة (اختياري)"
            rows="3"
            density="compact"
            hide-details
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="rateDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="rateBusy"
            @click="saveBoardOperatorRating"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.dfu { padding: 16px 20px 32px; }
.dfu-head {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 16px;
}
.dfu-title { font-size: 1.45rem; font-weight: 800; margin: 0; }
.dfu-sub { margin: 4px 0 0; }
.dfu-actions { display: flex; gap: 8px; flex-wrap: wrap; }
.muted { color: rgba(var(--v-theme-on-surface), 0.62); }

.city-grid, .list-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 12px;
}
.city-card, .list-card {
  text-align: start;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 14px;
  padding: 14px 16px;
  background: rgb(var(--v-theme-surface));
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.city-card:hover, .list-card:hover { border-color: rgb(var(--v-theme-primary)); }
.list-card__name { font-size: 1.05rem; }
.list-card__score { font-size: 1.35rem; font-weight: 800; color: rgb(var(--v-theme-primary)); }
.list-card__label { font-size: 0.85rem; }
.list-card__count { font-size: 0.9rem; }
.list-card__meta { font-size: 0.8rem; }

.list-detail-head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin: 8px 0 14px;
}
.list-detail-head h2 { margin: 0; font-size: 1.25rem; font-weight: 800; }
.kpi-hint { margin: 0 0 10px; font-size: 0.85rem; }
.table-hint { margin: 0 0 8px; font-size: 0.8rem; }
.conv-day { display: flex; flex-direction: column; align-items: flex-end; gap: 2px; text-align: end; }
.dfu-th {
  cursor: grab;
  user-select: none;
  white-space: nowrap;
  padding: 8px 10px !important;
}
.dfu-th--armed { outline: 2px solid rgba(var(--v-theme-primary), 0.45); }
.dfu-th--over { background: rgba(var(--v-theme-primary), 0.12); }

.line-box {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 16px;
  padding: 14px 16px 16px;
  margin-bottom: 16px;
  background: rgba(var(--v-theme-surface), 1);
}
.line-box__title { margin: 0 0 12px; font-size: 1.05rem; font-weight: 800; }
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
  font: inherit;
}
.sub-card--click { cursor: pointer; }
.sub-card--click:hover { border-color: rgb(var(--v-theme-primary)); }
.sub-card--warn {
  background: rgba(230, 140, 40, 0.08);
  border-color: rgba(230, 140, 40, 0.35);
}
.unpaid-new-table-host {
  max-height: min(60vh, 480px);
  overflow: auto;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 10px;
}
.unpaid-new-table {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
  min-width: 640px;
}
.unpaid-new-table th,
.unpaid-new-table td {
  padding: 8px 10px;
  white-space: nowrap;
  text-align: start;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.unpaid-new-table thead th {
  position: sticky;
  top: 0;
  z-index: 2;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 1px 0 rgba(var(--v-border-color), var(--v-border-opacity));
  font-weight: 700;
}
.unpaid-new-table__name {
  max-width: 14rem;
  overflow: hidden;
  text-overflow: ellipsis;
  font-weight: 600;
}
.sub-card--active {
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 2px rgba(var(--v-theme-primary), 0.2);
}
.sub-card__label { font-size: 0.8rem; opacity: 0.8; }
.sub-card__value { font-size: 1.25rem; font-weight: 800; }
.sub-card__empty {
  flex: 1;
  min-height: 36px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.04);
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(var(--v-theme-on-surface), 0.45);
}
.sub-card--placeholder { opacity: 0.85; }
.sub-card--legal {
  background: rgba(220, 60, 60, 0.08);
  border-color: rgba(220, 60, 60, 0.28);
}
.sub-card--conversion {
  background: rgba(var(--v-theme-primary), 0.06);
}
.sub-card__row {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  font-size: 0.85rem;
}
.conversion-line {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 0.95rem !important;
}
.conversion-line .up { color: #2e7d32; }
.conversion-line .down { color: #c62828; }
.conversion-line .same { opacity: 0.75; }

.hist-list {
  list-style: none;
  margin: 0;
  padding: 0;
}
.hist-list--lg { max-height: 420px; overflow: auto; }
.hist-list li {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 4px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  font-size: 0.95rem;
}
.hist-list__row--click { cursor: pointer; }
.hist-list__row--click:hover { background: rgba(var(--v-theme-primary), 0.06); }
.hist-date--link { color: rgb(var(--v-theme-primary)); text-decoration: underline; }

.table-panel {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 16px;
  padding: 12px;
  margin-bottom: 24px;
}
.table-panel__head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
  margin-bottom: 8px;
}
.dfu-inline-table-host {
  max-height: calc(100vh - 280px);
  overflow-x: auto;
  overflow-y: auto;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  background: rgb(var(--v-theme-surface));
}
.dfu-inline-table :deep(table) {
  min-width: 1800px;
  border-collapse: separate;
  border-spacing: 0;
}
.dfu-inline-table :deep(.v-table__wrapper) {
  overflow: visible !important;
  max-height: none !important;
}
.dfu-inline-table :deep(thead) {
  position: sticky;
  top: 0;
  z-index: 5;
}
.dfu-inline-table :deep(thead th),
.dfu-inline-table :deep(.dfu-th) {
  position: sticky !important;
  top: 0 !important;
  z-index: 5;
  background: rgb(var(--v-theme-surface)) !important;
  box-shadow: 0 1px 0 rgba(var(--v-border-color), var(--v-border-opacity));
  white-space: nowrap !important;
}
.dfu-th__label { pointer-events: none; white-space: nowrap; }
.dfu-inline-table :deep(td) {
  white-space: nowrap !important;
  vertical-align: middle;
  max-width: 18rem;
}
.dfu-cell-nowrap {
  display: inline-block;
  white-space: nowrap !important;
  max-width: 18rem;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: middle;
}
.dfu-customer-name {
  font-weight: 600;
  max-width: 14rem;
}
.trend-up { color: #2e7d32; }
.trend-down { color: #c62828; }
.trend-flat { opacity: 0.75; }
</style>
