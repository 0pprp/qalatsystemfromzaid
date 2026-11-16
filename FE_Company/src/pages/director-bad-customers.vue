<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as XLSX from 'xlsx'
import {
  directorCities,
  directorCitiesCached,
  directorDelegateFollowupListCustomers,
  directorDelegateFollowupLists,
  directorDelegateFollowupListsCached,
  directorDelegateFollowupPayments,
} from '@/composables/directorApi'
import { useSalesBranches } from '@/composables/useSalesBranches'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'

/**
 * «متابعة السيئين» — same flow as متابعة المندوبين (المحافظة → القائمة)،
 * ثم جدول زبائن القائمة المصنّفين «سيء» أو «متلكئ» فقط.
 * الضغط على الزبون يفتح تسديدات آخر 30 يوماً يوماً بيوم (مع أيام عدم التسديد).
 */
defineOptions({ name: 'DirectorBadCustomers' })
definePage({
  meta: {
    layout: 'default',
    keepAlive: true,
  },
})

const CITY_STORAGE_KEY = 'director-bad-customers-city'
const COLS_STORAGE_KEY = 'director-bad-customers-col-order'
const BAD_LABELS = ['سيء', 'متلكئ']
const PAYMENT_WINDOW_DAYS = 30

const toast = useToast()
const route = useRoute()
const router = useRouter()
const { branches, fetchCities } = useSalesBranches()
const { canOpenOversightBoards, isAdmin } = useUserRole()

/** المحاسب الرئيسي: فرعه المحدد فقط (LinkCity) — بدون اختيار محافظة. */
const canOpenBoard = computed(() => canOpenOversightBoards.value || isAdmin.value)

function accountantCityKey() {
  return String(localStorage.getItem('CityName') || '').trim() || 'branch'
}

const gatewayCities = ref(directorCitiesCached())
const listsBusy = ref(false)
const listCards = ref([])
const customersBusy = ref(false)
const customers = ref([])
const detailCacheKey = ref('')
const searchText = ref('')
const statusFilter = ref('all') // all | سيء | متلكئ

const paymentsDialog = ref(false)
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

const selectedCityName = computed(() => {
  const v = cityKey()
  const c = cityCards.value.find(x => x.value === v)

  return c?.name || v
})

const selectedListId = computed(() => {
  const n = Number(route.query.listId || 0)

  return Number.isFinite(n) && n > 0 ? n : 0
})

const selectedListName = computed(() => {
  const card = listCards.value.find(c => c.listId === selectedListId.value)

  return card?.listName || (selectedListId.value ? `قائمة ${selectedListId.value}` : '')
})

// ── Dates (Baghdad calendar) ─────────────────────────────────────
const baghdadFmt = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Baghdad',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

function baghdadTodayIso() {
  return baghdadFmt.format(new Date())
}

function shiftIsoDate(iso, days) {
  const [y, m, d] = String(iso || '').split('-').map(Number)
  if (!y || !m || !d)
    return ''
  const dt = new Date(Date.UTC(y, m - 1, d))
  dt.setUTCDate(dt.getUTCDate() + days)

  return dt.toISOString().slice(0, 10)
}

const WEEKDAYS_AR = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت']

function weekdayAr(iso) {
  const [y, m, d] = String(iso || '').split('-').map(Number)
  if (!y || !m || !d)
    return ''

  return WEEKDAYS_AR[new Date(Date.UTC(y, m - 1, d)).getUTCDay()]
}

/** Payment timestamp → Baghdad YYYY-MM-DD. Server-local strings (no TZ) are taken as-is. */
function toBaghdadIso(v) {
  if (!v)
    return ''
  const s = String(v)
  const hasTz = /z$|[+-]\d{2}:?\d{2}$/i.test(s)
  if (!hasTz && /^\d{4}-\d{2}-\d{2}/.test(s))
    return s.slice(0, 10)
  const d = new Date(s)
  if (Number.isNaN(d.getTime()))
    return ''

  return baghdadFmt.format(d)
}

function fmtDate(v) {
  if (!v)
    return '—'
  const iso = toBaghdadIso(v)

  return iso || String(v)
}

function fmtNum(v) {
  const n = Number(v)

  return Number.isFinite(n) ? n.toLocaleString('en-US') : '—'
}

function fmtScore(v) {
  const n = Number(v)

  return Number.isFinite(n) ? n.toFixed(2) : '—'
}

function softError(e, fallback) {
  return e?.response?.data?.message
    || e?.response?.data?.Message
    || e?.message
    || fallback
}

// ── Table ────────────────────────────────────────────────────────
const statusFilterItems = [
  { title: 'الكل (سيء + متلكئ)', value: 'all' },
  { title: 'سيء', value: 'سيء' },
  { title: 'متلكئ', value: 'متلكئ' },
]

const badCustomers = computed(() =>
  customers.value.filter(c => !c.isLegal && BAD_LABELS.includes(c.phantomRatingLabel)))

const badCount = computed(() => badCustomers.value.filter(c => c.phantomRatingLabel === 'سيء').length)
const laggingCount = computed(() => badCustomers.value.filter(c => c.phantomRatingLabel === 'متلكئ').length)

function lastPaymentSortMs(value) {
  if (value == null || value === '')
    return null
  const t = Date.parse(String(value))

  return Number.isFinite(t) ? t : null
}

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

const filteredCustomers = computed(() => {
  let rows = badCustomers.value
  const f = statusFilter.value
  if (f && f !== 'all')
    rows = rows.filter(c => c.phantomRatingLabel === f)

  const q = String(searchText.value || '').trim().toLowerCase()
  if (q) {
    rows = rows.filter(c => [c.customerName, c.phoneNumber, c.shopName, c.address, String(c.customerID)]
      .some(v => String(v || '').toLowerCase().includes(q)))
  }

  return sortByLastPaymentDesc(rows)
})

const ALL_COLUMN_DEFS = [
  { title: 'بروفايل الزبون', key: 'profile', exportable: false },
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
    .map(c => ({ title: c.title, key: c.key, sortable: false })))

function persistColumnOrder() {
  try {
    localStorage.setItem(COLS_STORAGE_KEY, JSON.stringify(columnOrder.value))
  }
  catch { /* ignore */ }
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
  XLSX.writeFile(workbook, `متابعة_السيئين_${cityKey() || 'city'}_${selectedListId.value || 'list'}.xlsx`)
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
      returnTo: 'director-bad-customers',
      listId: selectedListId.value ? String(selectedListId.value) : undefined,
    },
  })
}

// ── Normalizers ──────────────────────────────────────────────────
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
    phantomRatingLabel: String(row.phantomRatingLabel ?? row.PhantomRatingLabel ?? row.realRatingLabel ?? row.RealRatingLabel ?? '').trim(),
    isLegal: Boolean(row.isLegal ?? row.IsLegal ?? false),
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

// ── Navigation / loading ─────────────────────────────────────────
function isRouteActive() {
  return route.name === 'director-bad-customers'
    || String(route.path || '').endsWith('/director-bad-customers')
}

async function ensureCityQuery() {
  if (isAdmin.value) {
    const own = accountantCityKey()
    if (cityKey() !== own) {
      const sameList = !route.query.city ? route.query : {}
      await router.replace({ query: { ...sameList, city: own } })
    }

    return own
  }
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
  customers.value = []
  detailCacheKey.value = ''
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
  statusFilter.value = 'all'
  searchText.value = ''
}

/** Both branch sources in parallel; cached names stay on screen meanwhile (and on failure). */
async function loadCities() {
  await Promise.allSettled([
    fetchCities(),
    directorCities().then(rows => {
      if (Array.isArray(rows))
        gatewayCities.value = rows
    }),
  ])
}

async function loadLists() {
  const city = cityKey()
  if (!city) {
    listCards.value = []

    return
  }
  // Last copy for this branch shows instantly; fresh copy replaces it when ready.
  const cached = directorDelegateFollowupListsCached(city)
  listCards.value = cached ? cached.map(normListCard).filter(c => c.listId > 0) : []
  listsBusy.value = !cached
  try {
    const rows = await directorDelegateFollowupLists({ cityValue: city })
    if (cityKey() === city)
      listCards.value = (Array.isArray(rows) ? rows : []).map(normListCard).filter(c => c.listId > 0)
  }
  catch (e) {
    if (!cached)
      listCards.value = []
    toast.error(softError(e, 'تعذر تحميل القوائم'))
  }
  finally {
    listsBusy.value = false
  }
}

async function loadCustomers({ force = false } = {}) {
  const city = cityKey()
  const listId = selectedListId.value
  if (!city || !listId) {
    customers.value = []
    detailCacheKey.value = ''

    return
  }
  const cacheKey = `${city}|${listId}`
  if (!force && detailCacheKey.value === cacheKey && customers.value.length) {
    await nextTick()
    measureTableWidth()
    setupResizeObserver()

    return
  }
  if (!listCards.value.length)
    void loadLists() // list name for the header on direct open
  customersBusy.value = true
  customers.value = []
  statusFilter.value = 'all'
  searchText.value = ''
  try {
    const rows = await directorDelegateFollowupListCustomers({ cityValue: city, listId })
    customers.value = (Array.isArray(rows) ? rows : []).map(normCustomer).filter(c => c.customerID > 0 && !c.isLegal)
    detailCacheKey.value = cacheKey
  }
  catch (e) {
    customers.value = []
    detailCacheKey.value = ''
    toast.error(softError(e, 'تعذر تحميل زبائن القائمة'))
  }
  finally {
    customersBusy.value = false
    await nextTick()
    measureTableWidth()
    setupResizeObserver()
  }
}

// ── Synced top horizontal scrollbar (same as تفاصيل زبائن القائمة) ─
function tableScrollPort() {
  return bottomScrollEl.value?.querySelector('.v-table__wrapper')
    || tableInnerEl.value?.querySelector('.v-table__wrapper')
    || bottomScrollEl.value
}

function measureTableWidth() {
  const port = tableScrollPort()
  const table = bottomScrollEl.value?.querySelector('table')
    || tableInnerEl.value?.querySelector('table')
  tableScrollWidth.value = table?.scrollWidth || port?.scrollWidth || 0
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

// ── Payments popup: last 30 days ─────────────────────────────────
const windowEndIso = ref(baghdadTodayIso())
const windowStartIso = computed(() => shiftIsoDate(windowEndIso.value, -(PAYMENT_WINDOW_DAYS - 1)))

const paymentsInWindow = computed(() => {
  const start = windowStartIso.value
  const end = windowEndIso.value

  return paymentsRows.value.filter(p => p.day && p.day >= start && p.day <= end)
})

/** One row per calendar day (newest first), including days with no payment. */
const paymentDays = computed(() => {
  const byDay = new Map()
  for (const p of paymentsInWindow.value) {
    const cur = byDay.get(p.day) || { amount: 0, count: 0, delegates: new Set() }
    cur.amount += p.amountDenar
    cur.count += 1
    if (p.delegateName)
      cur.delegates.add(p.delegateName)
    byDay.set(p.day, cur)
  }
  const out = []
  for (let i = 0; i < PAYMENT_WINDOW_DAYS; i++) {
    const day = shiftIsoDate(windowEndIso.value, -i)
    const hit = byDay.get(day)
    out.push({
      day,
      weekday: weekdayAr(day),
      isFriday: weekdayAr(day) === 'الجمعة',
      paid: Boolean(hit),
      amount: hit?.amount || 0,
      count: hit?.count || 0,
      delegates: hit ? [...hit.delegates].join('، ') : '',
    })
  }

  return out
})

const paymentsCount30 = computed(() => paymentsInWindow.value.length)
const paymentsSum30 = computed(() => paymentsInWindow.value.reduce((s, p) => s + p.amountDenar, 0))
const paidDays30 = computed(() => paymentDays.value.filter(d => d.paid).length)
const unpaidDays30 = computed(() => PAYMENT_WINDOW_DAYS - paidDays30.value)

const paymentDayHeaders = [
  { title: 'التاريخ', key: 'day', sortable: false },
  { title: 'اليوم', key: 'weekday', sortable: false },
  { title: 'الحالة', key: 'paid', sortable: false },
  { title: 'المبلغ', key: 'amount', sortable: false },
  { title: 'عدد التسديدات', key: 'count', sortable: false },
  { title: 'المندوب', key: 'delegates', sortable: false },
]

function paymentDayRowProps({ item }) {
  if (item.paid)
    return { class: 'pay-row--paid' }

  return { class: item.isFriday ? 'pay-row--friday' : 'pay-row--unpaid' }
}

async function openPayments(item) {
  const city = cityKey()
  if (!city || !item?.customerID)
    return
  currentCustomer.value = item
  windowEndIso.value = baghdadTodayIso()
  paymentsRows.value = []
  paymentsDialog.value = true
  paymentsBusy.value = true
  try {
    const rows = await directorDelegateFollowupPayments({ cityValue: city, customerId: item.customerID })
    paymentsRows.value = (Array.isArray(rows) ? rows : []).map(r => {
      const paymentDate = r.paymentDate ?? r.PaymentDate ?? null

      return {
        amountDenar: Number(r.amountDenar ?? r.AmountDenar ?? 0) || 0,
        paymentDate,
        day: toBaghdadIso(paymentDate),
        delegateName: String(r.delegateName ?? r.DelegateName ?? '').trim(),
      }
    })
  }
  catch (e) {
    toast.error(softError(e, 'تعذر جلب التسديدات'))
  }
  finally {
    paymentsBusy.value = false
  }
}

function onRowClick(event, { item }) {
  if (event?.target?.closest?.('button, a'))
    return
  openPayments(item)
}

// ── Lifecycle ────────────────────────────────────────────────────
async function syncFromRoute() {
  if (!canOpenBoard.value || !isRouteActive())
    return
  await ensureCityQuery()
  if (!cityKey())
    return
  if (selectedListId.value > 0)
    await loadCustomers()
  else
    await loadLists()
}

watch(() => [route.name, route.path, route.query.city, route.query.listId], syncFromRoute)

watch(filteredCustomers, async () => {
  await nextTick()
  measureTableWidth()
  setupResizeObserver()
})

onMounted(async () => {
  if (!canOpenBoard.value)
    return
  if (!isAdmin.value)
    void loadCities() // don't block lists/table on branch names
  await syncFromRoute()
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  tableScrollPort()?.removeEventListener('scroll', onBottomScroll)
})
</script>

<template>
  <div class="dfu">
    <div class="dfu-head">
      <div>
        <h1 class="dfu-title">
          متابعة السيئين
        </h1>
        <p class="dfu-sub muted">
          <template v-if="!cityKey()">
            اختر المحافظة
          </template>
          <template v-else-if="selectedListId <= 0">
            {{ selectedCityName }} — اختر القائمة
          </template>
          <template v-else>
            {{ selectedCityName }} — {{ selectedListName }} — زبائن «سيء» و«متلكئ» — اضغط على الزبون لعرض تسديدات آخر 30 يوماً
          </template>
        </p>
      </div>
      <div class="dfu-actions">
        <VBtn
          v-if="cityKey() && !isAdmin"
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

    <!-- 1) المحافظة -->
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

    <!-- 2) القائمة -->
    <template v-else-if="selectedListId <= 0">
      <div
        v-if="listsBusy"
        class="muted pa-4"
      >
        جاري تحميل القوائم…
      </div>
      <div
        v-else-if="!listCards.length"
        class="muted pa-4"
      >
        لا توجد قوائم نشطة لهذه المحافظة.
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

    <!-- 3) جدول السيئين والمتلكئين -->
    <template v-else>
      <div class="table-toolbar">
        <VTextField
          v-model="searchText"
          density="comfortable"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="tabler-search"
          label="بحث باسم الزبون / الهاتف / المحل / العنوان"
          class="search-field"
        />
        <VSelect
          v-model="statusFilter"
          :items="statusFilterItems"
          item-title="title"
          item-value="value"
          label="التقييم"
          density="comfortable"
          variant="outlined"
          hide-details
          style="max-width: 220px"
        />
        <VChip
          color="error"
          size="small"
          variant="tonal"
        >
          سيء: {{ badCount }}
        </VChip>
        <VChip
          color="warning"
          size="small"
          variant="tonal"
        >
          متلكئ: {{ laggingCount }}
        </VChip>
        <span class="muted">{{ filteredCustomers.length }} / {{ badCustomers.length }} زبون</span>
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
        اضغط على أي زبون لعرض تسديداته في آخر 30 يوماً — الصفوف مرتبة بأحدث تاريخ آخر تسديد أولاً — اضغط مطولاً على رأس العمود ثم اسحبه لإعادة الترتيب
      </p>

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
            class="dfu-table dfu-table--clickable"
            hide-default-footer
            :items-per-page="-1"
            no-data-text="لا يوجد زبائن سيئين أو متلكئين في هذه القائمة"
            @click:row="onRowClick"
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
                @click.stop="openCustomerProfile(item)"
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
              <VChip
                size="small"
                :color="item.phantomRatingLabel === 'سيء' ? 'error' : 'warning'"
                variant="tonal"
              >
                {{ item.phantomRatingLabel || '—' }}
              </VChip>
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

    <!-- تسديدات آخر 30 يوماً -->
    <VDialog
      v-model="paymentsDialog"
      max-width="820"
      scrollable
    >
      <VCard>
        <VCardTitle class="d-flex align-center gap-2 flex-wrap">
          <span>تسديدات آخر 30 يوماً — {{ currentCustomer?.customerName }}</span>
          <VChip
            v-if="currentCustomer?.phantomRatingLabel"
            size="small"
            :color="currentCustomer.phantomRatingLabel === 'سيء' ? 'error' : 'warning'"
            variant="tonal"
          >
            {{ currentCustomer.phantomRatingLabel }}
          </VChip>
        </VCardTitle>
        <VCardSubtitle>
          من {{ windowStartIso }} إلى {{ windowEndIso }}
        </VCardSubtitle>
        <VCardText>
          <div
            v-if="paymentsBusy"
            class="muted pa-2"
          >
            جاري التحميل…
          </div>
          <template v-else>
            <div class="pay-kpis">
              <div class="pay-kpi">
                <span class="pay-kpi__label">عدد التسديدات</span>
                <strong class="pay-kpi__value">{{ paymentsCount30 }}</strong>
              </div>
              <div class="pay-kpi">
                <span class="pay-kpi__label">مجموع التسديدات</span>
                <strong class="pay-kpi__value">{{ fmtNum(paymentsSum30) }} دع</strong>
              </div>
              <div class="pay-kpi">
                <span class="pay-kpi__label">أيام التسديد</span>
                <strong class="pay-kpi__value pay-kpi__value--ok">{{ paidDays30 }}</strong>
              </div>
              <div class="pay-kpi">
                <span class="pay-kpi__label">أيام بدون تسديد</span>
                <strong class="pay-kpi__value pay-kpi__value--bad">{{ unpaidDays30 }}</strong>
              </div>
            </div>

            <VDataTable
              :headers="paymentDayHeaders"
              :items="paymentDays"
              item-value="day"
              density="compact"
              hide-default-footer
              :items-per-page="-1"
              :row-props="paymentDayRowProps"
              class="pay-table"
            >
              <template #item.paid="{ item }">
                <VChip
                  v-if="item.paid"
                  size="x-small"
                  color="success"
                  variant="tonal"
                >
                  سدّد
                </VChip>
                <VChip
                  v-else
                  size="x-small"
                  :color="item.isFriday ? 'secondary' : 'error'"
                  variant="tonal"
                >
                  لم يسدد
                </VChip>
              </template>
              <template #item.amount="{ item }">
                {{ item.paid ? fmtNum(item.amount) : '—' }}
              </template>
              <template #item.count="{ item }">
                {{ item.paid ? item.count : '—' }}
              </template>
              <template #item.delegates="{ item }">
                {{ item.delegates || '—' }}
              </template>
            </VDataTable>
          </template>
        </VCardText>
        <VCardActions>
          <VBtn
            variant="tonal"
            color="secondary"
            prepend-icon="tabler-user"
            @click="paymentsDialog = false; openCustomerProfile(currentCustomer)"
          >
            بروفايل الزبون
          </VBtn>
          <VSpacer />
          <VBtn
            variant="text"
            @click="paymentsDialog = false"
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
.city-card:hover, .list-card:hover { border-color: rgb(var(--v-theme-primary)); }
.list-card__name { font-size: 1.05rem; }
.list-card__score { font-size: 1.35rem; font-weight: 800; color: rgb(var(--v-theme-primary)); }
.list-card__label { font-size: 0.85rem; }
.list-card__count { font-size: 0.9rem; }
.list-card__meta { font-size: 0.8rem; }

.table-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}
.search-field { max-width: 360px; min-width: 220px; flex: 1; }
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
.dfu-th--over { box-shadow: inset 0 -3px 0 rgb(var(--v-theme-primary)); }
.dfu-th__label { pointer-events: none; }

.h-scroll { overflow-x: auto; overflow-y: hidden; }
.h-scroll--top {
  z-index: 6;
  height: 14px;
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.h-scroll__spacer { height: 1px; }

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
.dfu-table :deep(thead) { position: sticky; top: 0; z-index: 5; }
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
.dfu-table--clickable :deep(tbody tr) { cursor: pointer; }
.dfu-table--clickable :deep(tbody tr:hover td) { background: rgba(var(--v-theme-primary), 0.06); }
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

.pay-kpis {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 10px;
  margin-bottom: 14px;
}
.pay-kpi {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  padding: 10px 12px;
  background: rgba(var(--v-theme-primary), 0.05);
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.pay-kpi__label { font-size: 0.8rem; opacity: 0.7; }
.pay-kpi__value { font-size: 1.3rem; font-weight: 800; }
.pay-kpi__value--ok { color: rgb(var(--v-theme-success)); }
.pay-kpi__value--bad { color: rgb(var(--v-theme-error)); }
.pay-table :deep(tr.pay-row--unpaid td) { background: rgba(var(--v-theme-error), 0.06); }
.pay-table :deep(tr.pay-row--friday td) { opacity: 0.7; }
</style>
