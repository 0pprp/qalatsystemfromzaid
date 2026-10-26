<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as XLSX from 'xlsx'
import {
  directorCities,
  directorDelegateFollowupListCustomers,
  directorDelegateFollowupListSummary,
  directorDelegateFollowupLists,
  directorDelegateFollowupPayments,
  directorStatsListPaymentsSum,
} from '@/composables/directorApi'
import { useSalesBranches } from '@/composables/useSalesBranches'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'

defineOptions({ name: 'DirectorDelegateFollowupCustomers' })
definePage({
  meta: {
    layout: 'default',
    keepAlive: true,
  },
})

const CITY_STORAGE_KEY = 'director-delegate-followup-city'
const COLS_STORAGE_KEY = 'director-delegate-followup-col-order'

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
const customersBusy = ref(false)
const customers = ref([])
const searchText = ref('')
/** Dropdown: all | rating labels | legal */
const statusFilter = ref('all')

/** Baghdad calendar yesterday (YYYY-MM-DD) for payments sum default. */
const yesterdayIso = (() => {
  const fmt = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Baghdad',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  })
  const todayStr = fmt.format(new Date())
  const [y, m, d] = todayStr.split('-').map(Number)
  const utcNoon = Date.UTC(y, m - 1, d, 12, 0, 0)
  const yday = new Date(utcNoon - 86400000)

  return fmt.format(yday)
})()

const listPaymentsDialog = ref(false)
const listPaymentsBusy = ref(false)
const listPaymentsFrom = ref(yesterdayIso)
const listPaymentsTo = ref(yesterdayIso)
const listPaymentsTotal = ref(null)
const listPaymentsCount = ref(null)

const RATING_BUCKETS = [
  { key: 'ممتاز', label: 'ممتاز' },
  { key: 'جيد', label: 'جيد' },
  { key: 'ضعيف', label: 'ضعيف' },
  { key: 'متلكئ', label: 'متلكئ' },
  { key: 'سيء', label: 'سيء' },
]

const paymentsListDialog = ref(false)
const paymentsBusy = ref(false)
const paymentsRows = ref([])
const currentCustomer = ref(null)

const topScrollEl = ref(null)
const bottomScrollEl = ref(null)
const tableInnerEl = ref(null)
const syncingScroll = ref(false)
const tableScrollWidth = ref(0)
let resizeObserver = null

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
  const fromSummary = summary.value?.listName || summary.value?.ListName
  if (fromSummary)
    return String(fromSummary)
  const card = listCards.value.find(c => c.listId === selectedListId.value)

  return card?.listName || (selectedListId.value ? `قائمة ${selectedListId.value}` : '')
})

const filterCaption = computed(() => {
  const f = statusFilter.value
  if (!f || f === 'all')
    return ''
  if (f === 'legal')
    return 'عرض: قانونية'

  return `عرض: ${f}`
})

function lastPaymentSortMs(value) {
  if (value == null || value === '')
    return null
  const t = Date.parse(String(value))

  return Number.isFinite(t) ? t : null
}

/** Newest last-payment first; missing dates at the end. */
function sortByLastPaymentDesc(rows) {
  return [...rows].sort((a, b) => {
    const am = lastPaymentSortMs(a.lastPaymentDate)
    const bm = lastPaymentSortMs(b.lastPaymentDate)
    if (am == null && bm == null)
      return 0
    if (am == null)
      return 1
    if (bm == null)
      return -1

    return bm - am
  })
}

const statusFilterItems = [
  { title: 'الكل', value: 'all' },
  ...RATING_BUCKETS.map(b => ({ title: b.label, value: b.key })),
  { title: 'قانونية', value: 'legal' },
]

const filteredCustomers = computed(() => {
  let rows = customers.value
  const f = statusFilter.value
  if (f === 'legal')
    rows = rows.filter(c => c.isLegal)
  else if (f && f !== 'all')
    rows = rows.filter(c => !c.isLegal && c.phantomRatingLabel === f)

  const q = searchText.value.trim().toLowerCase()
  if (q) {
    rows = rows.filter(c => {
      const name = String(c.customerName || '').toLowerCase()
      const phone = String(c.phoneNumber || '')
      const shop = String(c.shopName || '').toLowerCase()

      return name.includes(q) || phone.includes(q) || shop.includes(q)
    })
  }

  return sortByLastPaymentDesc(rows)
})

/** Default order: profile → core money cols → rest → التسديدات + المباعة → ملاحظات */
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
  { title: 'التسديدات', key: 'showReceipt', sortable: false, exportable: false },
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
      if (order.includes(c.key))
        continue
      // Keep التقييم right after العميل when upgrading saved layouts.
      if (c.key === 'phantomRatingLabel') {
        const nameIdx = order.indexOf('customerName')
        if (nameIdx >= 0) {
          order.splice(nameIdx + 1, 0, c.key)
          continue
        }
      }
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
      sortable: false, // avoid conflict with long-press reorder
    })))

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
      returnTo: 'director-delegate-followup-customers',
      listId: selectedListId.value ? String(selectedListId.value) : undefined,
    },
  })
}

function exportCellValue(row, key) {
  switch (key) {
  case 'customerName': return row.customerName || 'لا يوجد'
  case 'phantomRatingLabel': return row.phantomRatingLabel || '—'
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
  const city = cityKey() || 'city'
  const list = selectedListId.value || 'list'
  XLSX.writeFile(workbook, `متابعة_مندوبين_${city}_${list}.xlsx`)
  toast.success(`تم تصدير ${dataToExport.length} زبون`)
}

function softError(e, fallback) {
  return e?.response?.data?.message
    || e?.response?.data?.Message
    || e?.message
    || fallback
}

function fmtNum(v) {
  const n = Number(v)

  return Number.isFinite(n) ? n.toLocaleString('en-US') : '—'
}

function fmtDate(v) {
  if (!v)
    return '—'
  const d = new Date(v)
  if (Number.isNaN(d.getTime()))
    return String(v)

  return d.toLocaleDateString('en-CA')
}

function fmtScore(v) {
  const n = Number(v)
  if (!Number.isFinite(n))
    return '—'

  return n.toFixed(2)
}

function trendLabel(t) {
  const s = String(t || 'flat').toLowerCase()
  if (s === 'up')
    return 'صاعد'
  if (s === 'down')
    return 'هابط'

  return 'مستقر'
}

function trendClass(t) {
  const s = String(t || 'flat').toLowerCase()
  if (s === 'up')
    return 'trend-up'
  if (s === 'down')
    return 'trend-down'

  return 'trend-flat'
}

function normCustomer(row) {
  return {
    customerID: Number(row.customerID ?? row.CustomerID ?? 0),
    customerName: row.customerName ?? row.CustomerName ?? '',
    itemsNames: row.itemsNames ?? row.ItemsNames ?? '',
    dateSaleDevice: row.dateSaleDevice ?? row.DateSaleDevice ?? null,
    amountTotalSales: Number(row.amountTotalSales ?? row.AmountTotalSales ?? 0),
    amountDaySales: Number(row.amountDaySales ?? row.AmountDaySales ?? 0),
    receiptsTotal: Number(row.receiptsTotal ?? row.ReceiptsTotal ?? 0),
    amountRemaining: Number(row.amountRemaining ?? row.AmountRemaining ?? 0),
    receiptRateDevice: row.receiptRateDevice ?? row.ReceiptRateDevice ?? null,
    countReceiptDevice: row.countReceiptDevice ?? row.CountReceiptDevice ?? null,
    numberOfDayDevice: row.numberOfDayDevice ?? row.NumberOfDayDevice ?? null,
    lastPaymentDate: row.lastPaymentDate ?? row.LastPaymentDate ?? null,
    lastPaymentAmount: Number(row.lastPaymentAmount ?? row.LastPaymentAmount ?? 0),
    address: row.address ?? row.Address ?? '',
    phoneNumber: row.phoneNumber ?? row.PhoneNumber ?? '',
    shopName: row.shopName ?? row.ShopName ?? '',
    receiptName: row.receiptName ?? row.ReceiptName ?? '',
    saleName: row.saleName ?? row.SaleName ?? '',
    notes: row.notes ?? row.Notes ?? '',
    delegateID: Number(row.delegateID ?? row.DelegateID ?? 0),
    delegateName: row.delegateName ?? row.DelegateName ?? '',
    phantomRatingLabel: String(row.phantomRatingLabel ?? row.PhantomRatingLabel ?? row.realRatingLabel ?? row.RealRatingLabel ?? ''),
    isLegal: Boolean(row.isLegal ?? row.IsLegal ?? false),
    isActiveRating: Boolean(row.isActiveRating ?? row.IsActiveRating ?? false),
  }
}

function normListCard(row) {
  return {
    listId: Number(row.listId ?? row.ListId ?? 0),
    listName: String(row.listName ?? row.ListName ?? ''),
    phantomRating: Number(row.phantomRating ?? row.PhantomRating ?? 0),
    phantomRatingLabel: String(row.phantomRatingLabel ?? row.PhantomRatingLabel ?? ''),
    activeCustomerCount: Number(row.activeCustomerCount ?? row.ActiveCustomerCount ?? 0),
    lastPaymentDate: row.lastPaymentDate ?? row.LastPaymentDate ?? null,
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
  statusFilter.value = 'all'
  listPaymentsDialog.value = false
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

const detailCacheKey = ref('')

function isFollowupRouteActive() {
  return route.name === 'director-delegate-followup-customers'
    || String(route.path || '').includes('director-delegate-followup-customers')
}

function backToBoard() {
  const city = cityKey()
  const listId = selectedListId.value
  router.push({
    path: '/director-delegate-followup',
    query: {
      ...(city ? { city } : {}),
      ...(listId ? { listId: String(listId) } : {}),
    },
  })
}

async function loadListDetail({ force = false } = {}) {
  const city = cityKey()
  const listId = selectedListId.value
  if (!city || !listId) {
    summary.value = null
    customers.value = []
    detailCacheKey.value = ''

    return
  }
  const cacheKey = `${city}|${listId}`
  // KeepAlive return from profile: reuse in-memory rows (no reload flash).
  if (!force && detailCacheKey.value === cacheKey && customers.value.length > 0) {
    summaryBusy.value = false
    customersBusy.value = false
    await nextTick()
    measureTableWidth()
    setupResizeObserver()

    return
  }
  summaryBusy.value = true
  customersBusy.value = true
  summary.value = null
  customers.value = []
  statusFilter.value = 'all'
  const sumP = directorDelegateFollowupListSummary({ cityValue: city, listId })
    .then(sum => {
      summary.value = sum || null
      summaryBusy.value = false
    })
    .catch(() => {
      summary.value = null
      summaryBusy.value = false
    })
  const custP = directorDelegateFollowupListCustomers({ cityValue: city, listId })
    .then(async cust => {
      customers.value = (Array.isArray(cust) ? cust : []).map(normCustomer).filter(c => c.customerID > 0)
      detailCacheKey.value = cacheKey
      customersBusy.value = false
      await nextTick()
      measureTableWidth()
      setupResizeObserver()
    })
    .catch(e => {
      customers.value = []
      detailCacheKey.value = ''
      customersBusy.value = false
      toast.error(softError(e, 'تعذر تحميل زبائن القائمة'))
    })
  await Promise.allSettled([sumP, custP])
}

/** Scrollport that owns vertical + horizontal scroll when fixed-header is on. */
function tableScrollPort() {
  return bottomScrollEl.value?.querySelector('.v-table__wrapper')
    || tableInnerEl.value?.querySelector('.v-table__wrapper')
    || bottomScrollEl.value
}

function measureTableWidth() {
  const port = tableScrollPort()
  const table = bottomScrollEl.value?.querySelector('table')
    || tableInnerEl.value?.querySelector('table')
  const w = table?.scrollWidth || port?.scrollWidth || 0
  tableScrollWidth.value = w
}

function onTopScroll() {
  const port = tableScrollPort()
  if (syncingScroll.value || !topScrollEl.value || !port)
    return
  syncingScroll.value = true
  port.scrollLeft = topScrollEl.value.scrollLeft
  syncingScroll.value = false
}

function onBottomScroll() {
  const port = tableScrollPort()
  if (syncingScroll.value || !topScrollEl.value || !port)
    return
  syncingScroll.value = true
  topScrollEl.value.scrollLeft = port.scrollLeft
  syncingScroll.value = false
}

function setupResizeObserver() {
  resizeObserver?.disconnect()
  const el = bottomScrollEl.value
  if (!el || typeof ResizeObserver === 'undefined')
    return
  resizeObserver = new ResizeObserver(() => measureTableWidth())
  resizeObserver.observe(el)
  const table = el.querySelector('table')
  if (table)
    resizeObserver.observe(table)
  const port = tableScrollPort()
  if (port) {
    port.removeEventListener('scroll', onBottomScroll)
    port.addEventListener('scroll', onBottomScroll, { passive: true })
  }
}

function openListPaymentsDialog() {
  if (!(selectedListId.value > 0) || !cityKey())
    return
  listPaymentsFrom.value = yesterdayIso
  listPaymentsTo.value = yesterdayIso
  listPaymentsTotal.value = null
  listPaymentsCount.value = null
  listPaymentsDialog.value = true
  void loadListPaymentsSum()
}

/** حقل «إلى» لا يقبل أقدم من «من» — نفس اليوم أو بعده فقط. */
function onListPaymentsFromChange(value) {
  const from = String(value || listPaymentsFrom.value || '').slice(0, 10)
  const to = String(listPaymentsTo.value || '').slice(0, 10)
  if (from && to && to < from)
    listPaymentsTo.value = from
}

async function loadListPaymentsSum() {
  const listId = selectedListId.value
  const city = cityKey()
  if (!(listId > 0) || !city)
    return
  const from = String(listPaymentsFrom.value || yesterdayIso).slice(0, 10)
  let to = String(listPaymentsTo.value || yesterdayIso).slice(0, 10)
  if (to < from) {
    to = from
    listPaymentsTo.value = from
    toast.warning('تاريخ «إلى» يجب أن يكون نفس «من» أو بعده')
  }
  listPaymentsBusy.value = true
  try {
    const data = await directorStatsListPaymentsSum({
      cityValue: city,
      listId,
      fromDate: from,
      toDate: to,
    })
    listPaymentsTotal.value = Number(data?.totalAmount ?? data?.TotalAmount ?? 0)
    listPaymentsCount.value = Number(data?.paymentCount ?? data?.PaymentCount ?? 0)
  }
  catch (e) {
    listPaymentsTotal.value = null
    listPaymentsCount.value = null
    toast.error(softError(e, 'تعذر جلب مجموع التسديدات'))
  }
  finally {
    listPaymentsBusy.value = false
  }
}

async function openPaymentsList(item) {
  const city = cityKey()
  if (!city || !item?.customerID)
    return
  currentCustomer.value = item
  paymentsListDialog.value = true
  paymentsBusy.value = true
  paymentsRows.value = []
  try {
    const rows = await directorDelegateFollowupPayments({
      cityValue: city,
      customerId: item.customerID,
    })
    paymentsRows.value = (Array.isArray(rows) ? rows : []).map(r => ({
      amountDenar: Number(r.amountDenar ?? r.AmountDenar ?? 0),
      paymentDate: r.paymentDate ?? r.PaymentDate ?? null,
      delegateName: r.delegateName ?? r.DelegateName ?? '',
      location: r.location ?? r.Location ?? '',
    }))
  }
  catch (e) {
    toast.error(softError(e, 'تعذر جلب التسديدات'))
  }
  finally {
    paymentsBusy.value = false
  }
}

watch(
  () => [route.name, route.path, route.query.city, route.query.listId],
  async () => {
    if (!isDirectorOrMonitor.value || !isFollowupRouteActive())
      return
    await ensureCityQuery()
    if (!cityKey() || selectedListId.value <= 0) {
      await router.replace({ path: '/director-delegate-followup', query: cityKey() ? { city: cityKey() } : {} })

      return
    }
    await loadListDetail()
  },
)

watch(filteredCustomers, async () => {
  await nextTick()
  measureTableWidth()
  setupResizeObserver()
})

onMounted(async () => {
  if (!isDirectorOrMonitor.value)
    return
  await loadCities()
  await ensureCityQuery()
  const qFilter = String(route.query.filter || '').trim()
  if (qFilter)
    statusFilter.value = qFilter
  if (cityKey() && selectedListId.value > 0)
    await loadListDetail()
  else
    await router.replace({ path: '/director-delegate-followup', query: cityKey() ? { city: cityKey() } : {} })
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  const port = tableScrollPort()
  port?.removeEventListener('scroll', onBottomScroll)
})
</script>

<template>
  <div class="dfu">
    <div class="dfu-head">
      <div>
        <h1 class="dfu-title">
          تفاصيل زبائن القائمة
        </h1>
        <p class="dfu-sub muted">
          {{ selectedListName }} — فلتر الحالة ثم الجدول
        </p>
      </div>
      <div class="dfu-actions">
        <VBtn
          variant="tonal"
          color="primary"
          prepend-icon="tabler-arrow-right"
          @click="backToBoard"
        >
          رجوع للوحة
        </VBtn>
      </div>
    </div>

    <div
      v-if="!cityKey() || selectedListId <= 0"
      class="muted pa-4"
    >
      اختر قائمة من لوحة متابعة المندوبين أولاً.
    </div>

    <template v-else>
      <div class="table-toolbar">
        <VTextField
          v-model="searchText"
          density="comfortable"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="tabler-search"
          label="بحث باسم الزبون / الهاتف / المحل"
          class="search-field"
        />
        <VSelect
          v-model="statusFilter"
          :items="statusFilterItems"
          item-title="title"
          item-value="value"
          label="فلتر الحالة"
          density="comfortable"
          variant="outlined"
          hide-details
          style="max-width: 220px"
        />
        <span class="muted">{{ filteredCustomers.length }} / {{ customers.length }} زبون</span>
        <VChip
          v-if="statusFilter && statusFilter !== 'all'"
          color="primary"
          size="small"
          closable
          @click:close="statusFilter = 'all'"
        >
          {{ filterCaption }}
        </VChip>
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
          تصدير إلى Excel
        </VBtn>
      </div>
      <p class="muted table-hint">
        الصفوف مرتبة بأحدث تاريخ آخر تسديد أولاً — صف عناوين الأعمدة ثابت أثناء التمرير — اضغط مطولاً على رأس العمود ثم اسحبه لإعادة الترتيب؛ التصدير يعكس الصفوف الظاهرة بعد الفلترة وترتيب الأعمدة الحالي
      </p>

      <!-- سكرول أفقي ثابت أعلى (يتزامن مع غلاف الجدول) -->
      <div
        ref="topScrollEl"
        class="h-scroll h-scroll--top"
        @scroll="onTopScroll"
      >
        <div
          class="h-scroll__spacer"
          :style="{ width: `${Math.max(tableScrollWidth, 800)}px` }"
        />
      </div>

      <div
        ref="bottomScrollEl"
        class="dfu-table-host"
      >
        <div
          ref="tableInnerEl"
          class="table-wrap"
        >
          <VDataTable
            :headers="tableHeaders"
            :items="filteredCustomers"
            :loading="customersBusy"
            item-value="customerID"
            density="comfortable"
            class="dfu-table"
            hide-default-footer
            :items-per-page="-1"
            :sort-by="[{ key: 'lastPaymentDate', order: 'desc' }]"
            must-sort
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
                size="small"
                color="secondary"
                variant="tonal"
                prepend-icon="tabler-user"
                @click="openCustomerProfile(item)"
              >
                بروفايل
              </VBtn>
            </template>
            <template #item.customerName="{ item }">
              <span
                class="dfu-customer-name"
                :title="item.customerName || ''"
              >{{ item.customerName || '—' }}</span>
            </template>
            <template #item.phantomRatingLabel="{ item }">
              <span class="dfu-customer-name">{{ item.isLegal ? 'قانونية' : (item.phantomRatingLabel || '—') }}</span>
            </template>
            <template #item.showReceipt="{ item }">
              <VBtn
                size="small"
                color="primary"
                prepend-icon="tabler-receipt"
                @click="openPaymentsList(item)"
              >
                التسديدات
              </VBtn>
            </template>
            <template #item.amountTotalSales="{ item }">
              <span class="dfu-customer-name">{{ fmtNum(item.amountTotalSales) }}</span>
            </template>
            <template #item.amountDaySales="{ item }">
              <span class="dfu-customer-name">{{ fmtNum(item.amountDaySales) }}</span>
            </template>
            <template #item.receiptsTotal="{ item }">
              <span class="dfu-customer-name">{{ fmtNum(item.receiptsTotal) }}</span>
            </template>
            <template #item.amountRemaining="{ item }">
              <span class="dfu-customer-name">{{ fmtNum(item.amountRemaining) }}</span>
            </template>
            <template #item.lastPaymentAmount="{ item }">
              <span class="dfu-customer-name">{{ fmtNum(item.lastPaymentAmount) }}</span>
            </template>
            <template #item.dateSaleDevice="{ item }">
              <span class="dfu-customer-name">{{ fmtDate(item.dateSaleDevice) }}</span>
            </template>
            <template #item.lastPaymentDate="{ item }">
              <span class="dfu-customer-name">{{ fmtDate(item.lastPaymentDate) }}</span>
            </template>
            <template #item.address="{ item }">
              <span
                class="dfu-customer-name"
                :title="item.address || ''"
              >{{ item.address || '—' }}</span>
            </template>
            <template #item.itemsNames="{ item }">
              <span
                class="dfu-customer-name"
                :title="item.itemsNames || ''"
              >{{ item.itemsNames || '—' }}</span>
            </template>
            <template #item.notes="{ item }">
              <span
                class="dfu-customer-name"
                :title="item.notes || ''"
              >{{ item.notes || '—' }}</span>
            </template>
          </VDataTable>
        </div>
      </div>
    </template>

    <!-- مجموع تسديدات القائمة -->
    <VDialog
      v-model="listPaymentsDialog"
      max-width="440"
    >
      <VCard>
        <VCardTitle>تسديدات «{{ selectedListName }}»</VCardTitle>
        <VCardText class="d-flex flex-column gap-3">
          <div class="d-flex flex-wrap gap-2">
            <VTextField
              v-model="listPaymentsFrom"
              type="date"
              label="من تاريخ"
              density="compact"
              hide-details
              style="min-width: 140px"
              @update:model-value="onListPaymentsFromChange"
            />
            <VTextField
              v-model="listPaymentsTo"
              type="date"
              label="إلى تاريخ"
              density="compact"
              hide-details
              style="min-width: 140px"
              :min="listPaymentsFrom || undefined"
            />
          </div>
          <VBtn
            color="primary"
            :loading="listPaymentsBusy"
            @click="loadListPaymentsSum"
          >
            عرض المجموع
          </VBtn>
          <div
            v-if="listPaymentsTotal != null"
            class="list-payments-sum-box"
          >
            <div class="text-medium-emphasis">
              مبلغ التسديدات
            </div>
            <div class="text-h5 font-weight-bold">
              {{ fmtNum(listPaymentsTotal) }} دع
            </div>
            <div class="text-caption">
              عدد الحركات: {{ listPaymentsCount ?? 0 }}
            </div>
          </div>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="listPaymentsDialog = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- قائمة التسديدات -->
    <VDialog
      v-model="paymentsListDialog"
      max-width="720"
    >
      <VCard>
        <VCardTitle>تسديدات — {{ currentCustomer?.customerName }}</VCardTitle>
        <VCardText>
          <div
            v-if="paymentsBusy"
            class="muted"
          >
            جاري التحميل…
          </div>
          <VDataTable
            v-else
            :headers="[
              { title: 'المبلغ', key: 'amountDenar' },
              { title: 'التاريخ', key: 'paymentDate' },
              { title: 'المندوب', key: 'delegateName' },
              { title: 'الموقع', key: 'location' },
            ]"
            :items="paymentsRows"
            density="compact"
            hide-default-footer
            :items-per-page="-1"
          >
            <template #item.amountDenar="{ item }">
              {{ fmtNum(item.amountDenar) }}
            </template>
            <template #item.paymentDate="{ item }">
              {{ fmtDate(item.paymentDate) }}
            </template>
          </VDataTable>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="paymentsListDialog = false"
          >
            إغلاق
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
.city-card:hover, .list-card:hover {
  border-color: rgb(var(--v-theme-primary));
}
.list-card__name { font-size: 1.05rem; }
.list-card__score { font-size: 1.35rem; font-weight: 800; color: rgb(var(--v-theme-primary)); }
.list-card__label { font-size: 0.85rem; }
.list-card__count { font-size: 0.9rem; }
.list-card__meta { font-size: 0.8rem; }

.list-detail-head { margin: 8px 0 14px; }
.list-detail-head h2 { margin: 0; font-size: 1.25rem; }

.kpi-stack { display: flex; flex-direction: column; gap: 14px; margin-bottom: 16px; }
.kpi-section__title {
  margin: 0;
  font-size: 1rem;
  font-weight: 700;
}
.kpi-section__hint { margin: 2px 0 8px; font-size: 0.8rem; }
.kpi-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 10px;
}
.kpi-card {
  border-radius: 14px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  padding: 12px 14px;
  background: rgb(var(--v-theme-surface));
  display: flex;
  flex-direction: column;
  gap: 4px;
  text-align: start;
  color: inherit;
  font: inherit;
}
.kpi-card--clickable {
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}
.kpi-card--clickable:hover {
  border-color: rgb(var(--v-theme-primary));
}
.kpi-card--active {
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 2px rgba(var(--v-theme-primary), 0.22);
}
.kpi-label { font-size: 0.8rem; opacity: 0.7; }
.kpi-value { font-size: 1.4rem; font-weight: 800; }
.trend-up { color: #15803d; }
.trend-down { color: #b91c1c; }
.trend-flat { opacity: 0.75; }

.table-toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}
.search-field { max-width: 360px; flex: 1; }
.table-hint { margin: 0 0 8px; font-size: 0.78rem; }
.dfu-th {
  cursor: grab;
  user-select: none;
  white-space: nowrap;
  touch-action: none;
}
.dfu-th:active { cursor: grabbing; }
.dfu-th--armed {
  outline: 2px solid rgba(var(--v-theme-primary), 0.55);
  background: rgba(var(--v-theme-primary), 0.12) !important;
}
.dfu-th--over {
  box-shadow: inset 0 -3px 0 rgb(var(--v-theme-primary));
}
.dfu-th__label { pointer-events: none; }

.h-scroll {
  overflow-x: auto;
  overflow-y: hidden;
}
.h-scroll--top {
  z-index: 6;
  height: 14px;
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.h-scroll__spacer { height: 1px; }

/* Scroll inside host so sticky headers stay visible while browsing rows. */
.dfu-table-host {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  max-height: calc(100vh - 220px);
  overflow-x: auto;
  overflow-y: auto;
  background: rgb(var(--v-theme-surface));
}
.table-wrap { min-width: 0; }
.dfu-table :deep(table) {
  min-width: 1800px;
  border-collapse: separate;
  border-spacing: 0;
}
.dfu-table :deep(.v-table__wrapper) {
  overflow: visible !important;
  max-height: none !important;
}
.dfu-table :deep(thead) {
  position: sticky;
  top: 0;
  z-index: 5;
}
.dfu-table :deep(thead th),
.dfu-table :deep(.dfu-th) {
  position: sticky !important;
  top: 0 !important;
  z-index: 5;
  background: rgb(var(--v-theme-surface)) !important;
  box-shadow: 0 1px 0 rgba(var(--v-border-color), var(--v-border-opacity));
  white-space: nowrap !important;
}
.dfu-table :deep(td) {
  white-space: nowrap !important;
  vertical-align: middle;
  max-width: 18rem;
}
.list-payments-sum-box {
  padding: 12px 14px;
  border-radius: 12px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-primary), 0.06);
}
.dfu-customer-name {
  display: inline-block;
  white-space: nowrap !important;
  font-weight: 600;
  max-width: 14rem;
  overflow: hidden;
  text-overflow: ellipsis;
}
.dfu-table :deep(td:has(.dfu-customer-name)) {
  white-space: nowrap !important;
  min-width: 12rem;
}
</style>
