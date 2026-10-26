<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as XLSX from 'xlsx'
import {
  directorCities,
  directorSalesFollowupDetail,
  directorSalesFollowupEmployees,
  directorSalesFollowupGetAppBlock,
  directorSalesFollowupSetAppBlock,
} from '@/composables/directorApi'
import { useSalesBranches } from '@/composables/useSalesBranches'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'

defineOptions({ name: 'DirectorSalesFollowup' })
definePage({
  meta: {
    layout: 'default',
    keepAlive: true,
  },
})

const CITY_KEY = 'director-sales-followup-city'
const ALL_CITIES = '__all__'
const COLS_STORAGE_KEY = 'director-sales-followup-col-order'
const toast = useToast()
const route = useRoute()
const router = useRouter()
const { branches, fetchCities } = useSalesBranches()
const { isDirectorOrMonitor } = useUserRole()

const gatewayCities = ref([])
const cityValue = ref('')
const busy = ref(false)
const cards = ref([])
const selectedId = ref(null)
const selectedCityValue = ref('')
const detailBusy = ref(false)
const detail = ref(null)

const customers = ref([])
const customersBusy = ref(false)
const tableVisible = ref(false)
const tableFilter = ref('all')
const searchText = ref('')
const focusMode = ref(false)

const appBlocked = ref(false)
const appBlockBusy = ref(false)

/** Dialog: آخر 30 تقييم ≤6 أو متوسطات النظام */
const histDialog = ref(false)
const histTitle = ref('')
const histMode = ref('low') // low | system
const histLow = ref([])
const histSystem = ref([])
const detailCache = new Map()

const convDialog = ref(false)
const convTitle = ref('')
const convExtra = ref('')
const convRows = ref([])
const namesDialog = ref(false)
const namesTitle = ref('')
const namesRows = ref([])

const RATING_BUCKETS = [
  { key: 'ممتاز', label: 'ممتاز' },
  { key: 'جيد', label: 'جيد' },
  { key: 'ضعيف', label: 'ضعيف' },
  { key: 'متلكئ', label: 'متلكئ' },
  { key: 'سيء', label: 'سيء' },
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
  { title: 'المصفرين', value: 'zeroed' },
]

const cityOptions = computed(() => {
  const fromGw = (gatewayCities.value || []).map(c => ({
    title: c.name || c.Name || c.value || c.Value,
    value: String(c.value || c.Value || ''),
  })).filter(c => c.value)
  const base = fromGw.length
    ? fromGw
    : (branches.value || []).map(b => ({
      title: b.name || b.Name || b.value || b.Value,
      value: String(b.value || b.Value || ''),
    })).filter(c => c.value)
  return [{ title: 'كل المحافظات', value: ALL_CITIES }, ...base]
})

const provinceOnlyOptions = computed(() =>
  cityOptions.value.filter(c => c.value !== ALL_CITIES),
)

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] != null && obj[k] !== '')
      return obj[k]
  }
  return undefined
}

function ratingColor(label) {
  const t = String(label || '')
  if (t.includes('سيء')) return 'error'
  if (t.includes('متلكئ') || t.includes('مرفوض')) return 'warning'
  if (t.includes('ضعيف')) return 'grey'
  if (t.includes('جيد')) return 'info'
  if (t.includes('ممتاز')) return 'success'
  return 'primary'
}

function fmtNum(n) {
  const v = Number(n)
  if (!Number.isFinite(v))
    return '—'
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(v)
}

function fmtDate(v) {
  if (!v) return '—'
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return String(v)
  return d.toLocaleDateString('en-GB')
}

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
    if (am == null && bm == null) return 0
    if (am == null) return 1
    if (bm == null) return -1
    return bm - am
  })
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

/** مصفر: بيع فعلي + الواصل ≥ سعر البيع + الباقي ≤ 0 (غير قانوني) */
function isZeroedCustomer(c) {
  const sale = Number(c?.amountTotalSales ?? 0)
  const paid = Number(c?.receiptsTotal ?? 0)
  const rem = Number(c?.amountRemaining ?? 0)
  return !c?.isLegal && sale > 0 && paid >= sale && rem <= 0
}

const filteredCustomers = computed(() => {
  let rows = customers.value
  const f = tableFilter.value
  if (f === 'legal')
    rows = rows.filter(c => c.isLegal)
  else if (f === 'zeroed')
    rows = rows.filter(isZeroedCustomer)
  else if (f && f !== 'all')
    rows = rows.filter(c => !c.isLegal && Number(c.amountRemaining) > 0 && c.phantomRatingLabel === f)

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
  const empName = pick(detail.value?.employee || detail.value?.Employee, 'employeeName', 'EmployeeName') || selectedId.value
  XLSX.writeFile(workbook, `متابعة_مبيعات_${cityValue.value || 'city'}_${empName}.xlsx`)
  toast.success(`تم تصدير ${dataToExport.length} زبون`)
}

function activeEmployeeCity() {
  return String(selectedCityValue.value || (cityValue.value !== ALL_CITIES ? cityValue.value : '') || '').trim()
}

function openCustomerProfile(item) {
  const city = activeEmployeeCity()
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
      returnTo: 'director-sales-followup',
      employeeId: selectedId.value ? String(selectedId.value) : undefined,
    },
  })
}

function bucket(detailObj, key) {
  const block = pick(detailObj, key, key[0].toUpperCase() + key.slice(1))
    || pick(detailObj, key.replace(/^[a-z]/, c => c.toUpperCase()))
  if (!block) return { high: [], low: [] }
  return {
    high: pick(block, 'high', 'High') || [],
    low: pick(block, 'low', 'Low') || [],
  }
}

const followerBuckets = computed(() => bucket(detail.value, 'followerSaleAuthRatings'))
const collectorBuckets = computed(() => bucket(detail.value, 'collectionDelegateRatings'))

/** كروت الفلتر — زبائن مفتوحون (غير مصفرين) + قانونية */
const openCustomers = computed(() =>
  (customers.value || []).filter(c => c.isLegal || Number(c.amountRemaining) > 0))

const salesCustomerCount = computed(() =>
  Number(pick(detail.value?.employee || detail.value?.Employee, 'customerCount', 'CustomerCount')
    || customers.value.length
    || 0),
)

const tierCountMap = computed(() => {
  const map = Object.fromEntries(RATING_BUCKETS.map(b => [b.key, 0]))
  for (const c of openCustomers.value) {
    if (c.isLegal)
      continue
    const label = String(c.phantomRatingLabel || '')
    if (label in map)
      map[label] += 1
  }
  return map
})

const legalCount = computed(() => openCustomers.value.filter(c => c.isLegal).length)

const zeroedCount = computed(() => {
  // Prefer local rule so the card matches the table even before BE redeploy.
  if ((customers.value || []).length)
    return customers.value.filter(isZeroedCustomer).length
  const fromApi = pick(detail.value, 'zeroedCustomerCount', 'ZeroedCustomerCount')
  return fromApi != null && Number.isFinite(Number(fromApi)) ? Number(fromApi) : 0
})

const systemHistory = computed(() => {
  const rows = pick(detail.value, 'systemRatingHistory', 'SystemRatingHistory') || []
  return Array.isArray(rows) ? rows : []
})

const conversion = computed(() => {
  const raw = pick(detail.value, 'conversion', 'Conversion') || {}
  return {
    summaryText: pick(raw, 'summaryText', 'SummaryText') || 'ازداد 0 · نقص 0 · مساوي 0',
    targets: (pick(raw, 'targets', 'Targets') || []).map(t => ({
      targetLabel: pick(t, 'targetLabel', 'TargetLabel') || '',
      arrivedCount: Number(pick(t, 'arrivedCount', 'ArrivedCount') || 0),
      detailText: pick(t, 'detailText', 'DetailText') || '',
      last30Days: (pick(t, 'last30Days', 'Last30Days') || []).map(d => ({
        date: pick(d, 'date', 'Date'),
        arrivedCount: Number(pick(d, 'arrivedCount', 'ArrivedCount') || 0),
        fromBreakdown: (pick(d, 'fromBreakdown', 'FromBreakdown') || []).map(f => ({
          fromLabel: pick(f, 'fromLabel', 'FromLabel') || '',
          count: Number(pick(f, 'count', 'Count') || 0),
        })),
        customers: (pick(d, 'customers', 'Customers') || []).map(c => ({
          customerId: Number(pick(c, 'customerId', 'CustomerId') || 0),
          customerName: pick(c, 'customerName', 'CustomerName') || '',
          fromLabel: pick(c, 'fromLabel', 'FromLabel') || '',
        })),
      })),
    })),
  }
})

function toggleTableFilter(key) {
  if (focusMode.value && tableFilter.value === key) {
    tableFilter.value = 'all'
    tableVisible.value = false
    focusMode.value = false
    return
  }
  tableFilter.value = key
  tableVisible.value = true
  focusMode.value = true
}

function onConversionTargetClick(target) {
  if (!target)
    return
  convTitle.value = `التحويل إلى ${target.targetLabel} — آخر 30 يوماً`
  convExtra.value = target.detailText || ''
  convRows.value = target.last30Days || []
  convDialog.value = true
}

function onConversionDateClick(row) {
  namesTitle.value = `المتحولون — ${fmtDate(row?.date)}`
  namesRows.value = row?.customers || []
  namesDialog.value = true
}

async function loadCities() {
  try {
    gatewayCities.value = await directorCities() || []
  }
  catch {
    gatewayCities.value = []
  }
  await fetchCities().catch(() => {})
}

async function loadCards() {
  if (!cityValue.value) {
    cards.value = []
    return
  }
  busy.value = true
  try {
    if (cityValue.value === ALL_CITIES) {
      const cities = provinceOnlyOptions.value
      const batches = await Promise.all(cities.map(async c => {
        try {
          const data = await directorSalesFollowupEmployees({ cityValue: c.value })
          const items = pick(data, 'items', 'Items') || (Array.isArray(data) ? data : [])
          return (Array.isArray(items) ? items : []).map(emp => ({
            ...emp,
            cityValue: c.value,
            cityName: c.title,
            CityValue: c.value,
            CityName: c.title,
          }))
        }
        catch {
          return []
        }
      }))
      cards.value = batches.flat().sort((a, b) =>
        Number(pick(a, 'averageScore', 'AverageScore') || 0)
        - Number(pick(b, 'averageScore', 'AverageScore') || 0))
    }
    else {
      const data = await directorSalesFollowupEmployees({ cityValue: cityValue.value })
      const items = pick(data, 'items', 'Items') || (Array.isArray(data) ? data : [])
      const cityTitle = cityOptions.value.find(c => c.value === cityValue.value)?.title || cityValue.value
      cards.value = (Array.isArray(items) ? items : []).map(emp => ({
        ...emp,
        cityValue: cityValue.value,
        cityName: cityTitle,
        CityValue: cityValue.value,
        CityName: cityTitle,
      }))
    }
  }
  catch (err) {
    cards.value = []
    toast.error(err?.response?.data?.message || err?.message || 'تعذر تحميل موظفي المبيعات')
  }
  finally {
    busy.value = false
  }
}

function applyDetailPayload(d) {
  detail.value = d
  const rows = pick(d, 'customers', 'Customers') || []
  customers.value = (Array.isArray(rows) ? rows : []).map(normCustomer).filter(c => c.customerID > 0)
  // Don't force table open — focus mode controls visibility
}

function openHistDialog(kind) {
  histSystem.value = []
  if (kind === 'auth') {
    histMode.value = 'low'
    histTitle.value = 'تقييم المبيعات الجديدة والمصادقة — آخر 30 (≥6 فما دون)'
    histLow.value = followerBuckets.value.low
  }
  else if (kind === 'collector') {
    histMode.value = 'low'
    histTitle.value = 'تقييم مندوب الجباية — آخر 30 (≥6 فما دون)'
    histLow.value = collectorBuckets.value.low
  }
  else if (kind === 'system') {
    histMode.value = 'system'
    histTitle.value = 'تقييم النظام — آخر 30 متوسط يومي'
    histLow.value = []
    histSystem.value = systemHistory.value
  }
  else {
    return
  }
  histDialog.value = true
}

async function refreshAppBlock(city, employeeId) {
  appBlocked.value = false
  if (!city || !employeeId) return
  try {
    const st = await directorSalesFollowupGetAppBlock({ cityValue: city, employeeId })
    appBlocked.value = !!(pick(st, 'isBlocked', 'IsBlocked'))
  }
  catch {
    appBlocked.value = false
  }
}

async function toggleAppBlock() {
  const city = activeEmployeeCity()
  const id = selectedId.value
  if (!city || !id) return
  const next = !appBlocked.value
  if (next && !window.confirm('إيقاف تطبيق هذا المندوب؟ لن يستطيع فتح التطبيق أو بدء الدوام.'))
    return
  appBlockBusy.value = true
  try {
    await directorSalesFollowupSetAppBlock({
      cityValue: city,
      employeeId: id,
      isBlocked: next,
    })
    appBlocked.value = next
    toast.success(next ? 'تم إيقاف التطبيق' : 'تم تفعيل التطبيق')
  }
  catch (err) {
    toast.error(err?.response?.data?.message || err?.message || 'تعذر تحديث حالة التطبيق')
  }
  finally {
    appBlockBusy.value = false
  }
}

async function openEmployee(emp) {
  const id = pick(emp, 'employeeId', 'EmployeeId')
  if (!id) return
  const empCity = String(pick(emp, 'cityValue', 'CityValue') || (cityValue.value !== ALL_CITIES ? cityValue.value : '') || '').trim()
  if (!empCity) {
    toast.warning('المحافظة غير معروفة لهذا الموظف')
    return
  }
  selectedId.value = id
  selectedCityValue.value = empCity
  focusMode.value = false
  tableFilter.value = 'all'
  tableVisible.value = false

  detail.value = {
    employee: emp,
    Employee: emp,
    followerSaleAuthRatings: { high: [], low: [] },
    collectionDelegateRatings: { high: [], low: [] },
    systemRatingHistory: [],
    conversion: { summaryText: '', targets: [] },
    customers: [],
  }
  void refreshAppBlock(empCity, id)

  const cacheKey = `${empCity}|${id}`
  if (detailCache.has(cacheKey)) {
    applyDetailPayload(detailCache.get(cacheKey))
    detailBusy.value = false
    customersBusy.value = false
    return
  }

  detailBusy.value = true
  customersBusy.value = true
  try {
    const d = await directorSalesFollowupDetail({
      cityValue: empCity,
      employeeId: id,
    })
    detailCache.set(cacheKey, d)
    applyDetailPayload(d)
  }
  catch (err) {
    toast.error(err?.response?.data?.message || err?.message || 'تعذر تحميل التفاصيل')
  }
  finally {
    detailBusy.value = false
    customersBusy.value = false
  }
}

function persistCity(v) {
  const s = String(v || '').trim()
  if (s)
    localStorage.setItem(CITY_KEY, s)
  router.replace({ query: { ...route.query, city: s || undefined } })
}

watch(cityValue, async (v) => {
  persistCity(v)
  selectedId.value = null
  selectedCityValue.value = ''
  detail.value = null
  customers.value = []
  focusMode.value = false
  tableVisible.value = false
  detailCache.clear()
  await loadCards()
})

onMounted(async () => {
  if (!isDirectorOrMonitor.value) {
    toast.error('غير مصرح')
    return
  }
  await loadCities()
  const fromRoute = String(route.query.city || '').trim()
  const fromStore = String(localStorage.getItem(CITY_KEY) || '').trim()
  cityValue.value = fromRoute || fromStore || cityOptions.value[0]?.value || ''
  if (cityValue.value)
    await loadCards()
  const empFromRoute = Number(route.query.employeeId || 0)
  if (empFromRoute > 0)
    await openEmployee({ employeeId: empFromRoute })
})
</script>

<template>
  <div class="sales-followup pa-4">
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h1 class="text-h5 mb-1">
          متابعة المبيعات
        </h1>
        <p class="text-medium-emphasis mb-0">
          كروت الموظفين · خط التقييمات (≤6) · عدد الزبائن والمصفرين
        </p>
      </div>
      <VSelect
        v-if="!focusMode"
        v-model="cityValue"
        :items="cityOptions"
        item-title="title"
        item-value="value"
        label="المحافظة"
        density="comfortable"
        style="min-inline-size: 220px;"
        hide-details
      />
    </div>

    <VProgressLinear
      v-if="busy && !focusMode"
      indeterminate
      class="mb-3"
    />

    <Transition name="sf-fade">
      <div
        v-if="!focusMode"
        class="emp-grid"
      >
        <button
          v-for="(emp, idx) in cards"
          :key="`${pick(emp, 'cityValue', 'CityValue')}-${pick(emp, 'employeeId', 'EmployeeId')}`"
          type="button"
          class="emp-card"
          :class="{ 'emp-card--active': selectedId === pick(emp, 'employeeId', 'EmployeeId') && selectedCityValue === pick(emp, 'cityValue', 'CityValue') }"
          :style="{ '--i': Math.min(idx, 20) }"
          @click="openEmployee(emp)"
        >
          <div class="emp-card__name">
            {{ pick(emp, 'employeeName', 'EmployeeName') || '—' }}
          </div>
          <div class="text-caption text-medium-emphasis mt-1">
            {{ pick(emp, 'cityName', 'CityName') || '—' }}
          </div>
          <div class="text-caption mt-2">
            عدد المبيعات: <b>{{ pick(emp, 'customerCount', 'CustomerCount') ?? 0 }}</b>
          </div>
          <VChip
            size="small"
            class="mt-2"
            :color="ratingColor(pick(emp, 'ratingLabel', 'RatingLabel'))"
          >
            {{ pick(emp, 'ratingLabel', 'RatingLabel') || '—' }}
            ({{ pick(emp, 'averageScore', 'AverageScore') ?? '—' }})
          </VChip>
        </button>
      </div>
    </Transition>

    <VAlert
      v-if="!busy && !focusMode && cityValue && !cards.length"
      type="info"
      variant="tonal"
      class="mt-4"
    >
      لا يوجد موظفو مبيعات نشطون لهذه المحافظة.
    </VAlert>

    <VCard
      v-if="selectedId"
      class="mt-6 detail-card"
      :class="{ 'detail-card--focus': focusMode }"
      variant="outlined"
    >
      <VCardTitle
        v-if="!focusMode"
        class="d-flex flex-wrap align-center justify-space-between gap-2"
      >
        <span>
          {{ pick(detail?.employee || detail?.Employee, 'employeeName', 'EmployeeName') || 'تفاصيل الموظف' }}
          <small class="text-medium-emphasis ms-2">{{ selectedCityValue }}</small>
        </span>
        <div class="d-flex align-center gap-2">
          <VBtn
            size="small"
            :color="appBlocked ? 'success' : 'error'"
            :variant="appBlocked ? 'tonal' : 'flat'"
            :loading="appBlockBusy"
            @click="toggleAppBlock"
          >
            {{ appBlocked ? 'تفعيل التطبيق' : 'إيقاف تطبيق هذا المندوب' }}
          </VBtn>
          <VBtn
            size="small"
            variant="text"
            @click="selectedId = null; selectedCityValue = ''; detail = null; customers = []; focusMode = false"
          >
            إغلاق
          </VBtn>
        </div>
      </VCardTitle>
      <VCardText>
        <VProgressLinear
          v-if="detailBusy"
          indeterminate
          class="mb-3"
        />
        <template v-else-if="detail">
          <Transition name="sf-fade">
            <div
              v-if="!focusMode"
              class="slot-grid mb-4"
            >
              <button
                type="button"
                class="slot-card slot-card--click"
                @click="openHistDialog('auth')"
              >
                <div class="slot-card__title">
                  تقييم المبيعات الجديدة والمصادقة
                  <span class="slot-card__hint">آخر 30 · 6 فما دون فقط</span>
                </div>
                <div class="slot-card__score warn-text">
                  {{ followerBuckets.low.length }}
                </div>
                <div class="text-caption text-medium-emphasis mt-1">
                  اضغط لعرض الدرجة والسبب والتاريخ
                </div>
              </button>

              <button
                type="button"
                class="slot-card slot-card--click"
                @click="openHistDialog('collector')"
              >
                <div class="slot-card__title">
                  تقييم مندوب الجباية
                  <span class="slot-card__hint">آخر 30 · 6 فما دون فقط</span>
                </div>
                <div class="slot-card__score warn-text">
                  {{ collectorBuckets.low.length }}
                </div>
                <div class="text-caption text-medium-emphasis mt-1">
                  اضغط لعرض الدرجة والسبب والتاريخ
                </div>
              </button>

              <button
                type="button"
                class="slot-card slot-card--click"
                @click="openHistDialog('system')"
              >
                <div class="slot-card__title">
                  تقييم النظام
                  <span class="slot-card__hint">اضغط لآخر 30 متوسط يومي</span>
                </div>
                <div class="slot-card__score">
                  {{ pick(detail.employee || detail.Employee, 'ratingLabel', 'RatingLabel') || '—' }}
                  <small>({{ pick(detail.employee || detail.Employee, 'averageScore', 'AverageScore') ?? '—' }})</small>
                </div>
                <div class="text-caption text-medium-emphasis mt-1">
                  المتوسط الحالي من {{ salesCustomerCount }} زبون
                </div>
              </button>
            </div>
          </Transition>

          <section class="line-box mt-2 sf-customers-box">
            <h3 class="line-box__title">
              عدد الزبائن (عدد مبيعاته) — {{ salesCustomerCount }}
            </h3>
            <p class="muted kpi-hint">
              اضغط الكرت لفتح الجدول وإخفاء بقية الصفحة — اضغط مرة أخرى للإلغاء
            </p>
            <div class="sub-grid">
              <button
                v-for="b in RATING_BUCKETS"
                :key="`nz-${b.key}`"
                type="button"
                class="sub-card sub-card--click"
                :class="{ 'sub-card--active': focusMode && tableFilter === b.key }"
                @click="toggleTableFilter(b.key)"
              >
                <span class="sub-card__label">{{ b.label }}</span>
                <strong class="sub-card__value">{{ tierCountMap[b.key] ?? 0 }}</strong>
              </button>
              <button
                type="button"
                class="sub-card sub-card--click sub-card--legal"
                :class="{ 'sub-card--active': focusMode && tableFilter === 'legal' }"
                @click="toggleTableFilter('legal')"
              >
                <span class="sub-card__label">القانونية</span>
                <strong class="sub-card__value">{{ legalCount }}</strong>
              </button>
              <button
                type="button"
                class="sub-card sub-card--click sub-card--zeroed"
                :class="{ 'sub-card--active': focusMode && tableFilter === 'zeroed' }"
                @click="toggleTableFilter('zeroed')"
              >
                <span class="sub-card__label">عدد المصفرين</span>
                <strong class="sub-card__value">{{ zeroedCount }}</strong>
                <small>الواصل = سعر البيع</small>
              </button>
            </div>
          </section>

          <Transition name="sf-fade">
            <section
              v-if="!focusMode"
              class="line-box mt-4"
            >
              <h3 class="line-box__title">
                التحويل
              </h3>
              <p class="muted kpi-hint">
                {{ conversion.summaryText }} — اضغط الوجهة لمعرفة من أين جاؤوا (آخر 30 يوماً)
              </p>
              <div class="sub-grid">
                <button
                  v-for="t in conversion.targets"
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
          </Transition>
        </template>

        <Transition name="sf-slide">
          <section
            v-if="tableVisible && focusMode"
            class="table-panel mt-4"
          >
          <div class="table-panel__head">
            <strong>جدول الزبائن</strong>
            <VTextField
              v-model="searchText"
              label="بحث"
              density="compact"
              variant="outlined"
              hide-details
              clearable
              style="max-width: 200px"
            />
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
              variant="text"
              @click="tableVisible = false"
            >
              إخفاء
            </VBtn>
          </div>
          <p class="muted table-hint">
            نفس جدول متابعة المندوبين — رأس الأعمدة ثابت — اضغط مطولاً على رأس العمود ثم اسحبه لإعادة الترتيب
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
        </Transition>
      </VCardText>
    </VCard>

    <VDialog
      v-model="convDialog"
      max-width="720"
    >
      <VCard>
        <VCardTitle>{{ convTitle }}</VCardTitle>
        <VCardText>
          <p
            v-if="convExtra"
            class="muted mb-3"
          >
            اليوم: {{ convExtra }}
          </p>
          <div
            v-if="!convRows.length"
            class="sub-card__empty"
          >
            لا توجد بيانات لهذه الفترة بعد
          </div>
          <ul
            v-else
            class="hist-list hist-list--lg"
          >
            <li
              v-for="(row, idx) in convRows"
              :key="idx"
              class="hist-list__row--click"
              @click="onConversionDateClick(row)"
            >
              <span class="hist-date--link">{{ fmtDate(row.date) }}</span>
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
            </li>
          </ul>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="convDialog = false"
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
      v-model="histDialog"
      max-width="640"
    >
      <VCard>
        <VCardTitle>{{ histTitle }}</VCardTitle>
        <VCardText>
          <template v-if="histMode === 'system'">
            <div
              v-if="!histSystem.length"
              class="text-caption text-medium-emphasis"
            >
              لا متوسطات بعد
            </div>
            <div
              v-for="(row, i) in histSystem"
              :key="`sys-${i}`"
              class="hist-row"
            >
              <strong>{{ fmtDate(pick(row, 'ratingDate', 'RatingDate')) }}</strong>
              <span>{{ pick(row, 'ratingLabel', 'RatingLabel') || '—' }}</span>
              <span class="text-medium-emphasis">متوسط {{ pick(row, 'averageScore', 'AverageScore') ?? '—' }}</span>
              <span class="text-caption">{{ pick(row, 'customerCount', 'CustomerCount') ?? 0 }} زبون</span>
            </div>
          </template>
          <template v-else>
            <div
              v-if="!histLow.length"
              class="text-caption text-medium-emphasis"
            >
              لا تقييمات ≤6
            </div>
            <div
              v-for="(row, i) in histLow"
              :key="`hl-${i}`"
              class="hist-row"
            >
              <strong>{{ pick(row, 'score', 'Score') }}/10</strong>
              <span class="text-medium-emphasis">{{ pick(row, 'note', 'Note') || 'بدون سبب' }}</span>
              <span class="text-caption">{{ fmtDate(pick(row, 'ratingDate', 'RatingDate')) }}</span>
            </div>
          </template>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="histDialog = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.emp-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 0.75rem;
}
.emp-card {
  text-align: start;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 12px;
  padding: 0.9rem 1rem;
  background: rgb(var(--v-theme-surface));
  cursor: pointer;
  animation: sf-rise 0.18s ease both;
  animation-delay: calc(0.015s * var(--i, 0));
  transition: border-color 0.12s ease, box-shadow 0.12s ease, transform 0.12s ease;
}
.emp-card:hover {
  transform: translateY(-1px);
}
.emp-card--active {
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 1px rgb(var(--v-theme-primary));
}
.emp-card__name {
  font-weight: 700;
  font-size: 1.05rem;
}
.slot-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 1rem;
}
@media (min-width: 900px) {
  .slot-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}
.line-box {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 16px;
  padding: 14px 16px 16px;
  background: rgba(var(--v-theme-surface), 1);
}
.line-box__title {
  margin: 0 0 8px;
  font-size: 1.05rem;
  font-weight: 800;
}
.kpi-hint {
  margin: 0 0 10px;
  font-size: 0.85rem;
}
.sub-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 10px;
}
.sub-card {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-height: 96px;
  background: rgba(var(--v-theme-on-surface), 0.02);
  text-align: start;
  color: inherit;
  font: inherit;
  transition: border-color 0.12s ease, box-shadow 0.12s ease, transform 0.12s ease, background 0.12s ease;
}
.sub-card--click { cursor: pointer; }
.sub-card--click:hover { border-color: rgb(var(--v-theme-primary)); transform: translateY(-1px); }
.sub-card--active {
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 2px rgba(var(--v-theme-primary), 0.2);
  transform: scale(1.02);
}
.sub-card--legal {
  background: rgba(220, 60, 60, 0.08);
  border-color: rgba(220, 60, 60, 0.28);
}
.sub-card--conversion {
  background: rgba(var(--v-theme-primary), 0.06);
}
.sub-card__label { font-size: 0.8rem; opacity: 0.8; }
.sub-card__value { font-size: 1.25rem; font-weight: 800; }
.sub-card__empty {
  min-height: 64px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.04);
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(var(--v-theme-on-surface), 0.45);
}
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
.conv-day {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
  text-align: end;
}
.slot-card {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 12px;
  padding: 0.85rem;
  background: rgba(var(--v-theme-on-surface), 0.02);
  text-align: start;
  color: inherit;
  font: inherit;
}
.slot-card--click {
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}
.slot-card--click:hover {
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 1px rgba(var(--v-theme-primary), 0.25);
}
.slot-card__title {
  font-weight: 700;
  margin-block-end: 0.65rem;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}
.slot-card__hint {
  font-weight: 500;
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.55);
}
.slot-card__score {
  font-size: 1.25rem;
  font-weight: 700;
}
.hist-preview {
  font-size: 0.82rem;
  border-block-start: 1px dashed rgba(var(--v-theme-on-surface), 0.12);
  padding-block-start: 0.45rem;
}
.split-block {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.65rem;
}
.split-col {
  border: 1px dashed rgba(var(--v-theme-on-surface), 0.15);
  border-radius: 8px;
  padding: 0.5rem;
  max-block-size: 320px;
  overflow: auto;
}
.split-col__head {
  font-weight: 700;
  font-size: 0.85rem;
  margin-block-end: 0.4rem;
}
.success-text { color: rgb(var(--v-theme-success)); }
.warn-text { color: rgb(var(--v-theme-warning)); }
.hist-row {
  display: grid;
  gap: 0.1rem;
  padding-block: 0.35rem;
  border-block-end: 1px solid rgba(var(--v-theme-on-surface), 0.06);
  font-size: 0.82rem;
}
.muted { color: rgba(var(--v-theme-on-surface), 0.62); }
.table-hint { margin: 0 0 8px; font-size: 0.8rem; }
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
.dfu-th {
  cursor: grab;
  user-select: none;
  white-space: nowrap;
  padding: 8px 10px !important;
}
.dfu-th--armed { outline: 2px solid rgba(var(--v-theme-primary), 0.45); }
.dfu-th--over { background: rgba(var(--v-theme-primary), 0.12); }
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
.sub-card--zeroed {
  background: rgba(13, 115, 119, 0.08);
  border-color: rgba(13, 115, 119, 0.3);
}
.detail-card--focus {
  border-color: rgba(13, 115, 119, 0.35);
  box-shadow: 0 12px 32px rgba(9, 92, 95, 0.08);
  transition: border-color 0.12s ease, box-shadow 0.12s ease;
}
.sf-fade-enter-active,
.sf-fade-leave-active {
  transition: opacity 0.12s ease, transform 0.12s ease;
}
.sf-fade-enter-from,
.sf-fade-leave-to {
  opacity: 0;
  transform: translateY(4px);
}
.sf-slide-enter-active,
.sf-slide-leave-active {
  transition: opacity 0.14s ease, transform 0.14s ease;
}
.sf-slide-enter-from,
.sf-slide-leave-to {
  opacity: 0;
  transform: translateY(6px);
}
@keyframes sf-rise {
  from { opacity: 0; transform: translateY(6px); }
  to { opacity: 1; transform: translateY(0); }
}
@media (prefers-reduced-motion: reduce) {
  .emp-card,
  .sf-fade-enter-active,
  .sf-fade-leave-active,
  .sf-slide-enter-active,
  .sf-slide-leave-active { animation: none !important; transition: none !important; }
}
</style>
