<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import ModernStatCard from "@/components/ModernStatCard.vue"
import axios from 'axios'
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import * as XLSX from 'xlsx'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'
import {
  directorLegalConversionApprove,
  directorLegalConversionReject,
  directorLegalConversionRequests,
  directorLegalWaiverAccountantRequests,
  directorLegalWeakWeekPayers,
} from '@/composables/directorApi'
import { useMonitorLegalCity } from '@/composables/useMonitorLegalCity'

const apiUrl = localStorage.getItem('LinkCity')
const route = useRoute()
const router = useRouter()
const toast = useToast()
const { canDecide, isAdmin, isMonitor } = useUserRole()
const canReviewLegal = computed(() => isMonitor.value)
const {
  cityValue: monitorCity,
  cityItems: monitorCityItems,
  citiesLoading: monitorCitiesLoading,
  setCity: setMonitorCity,
} = useMonitorLegalCity()

/** لوحة الأسماء | طلبات التحويل | التنازلات (عرض للمراقب) */
const boardTab = ref('open')
const namesLoaded = ref(false)
const legalLoaded = ref(false)
const waiverLoaded = ref(false)
const waiverRows = ref([])
const waiverLoading = ref(false)
const waiverStatusFilter = ref('')

const waiverStatusItems = [
  { title: 'الكل', value: '' },
  { title: 'بانتظار المحاسب', value: 'Pending' },
  { title: 'مطبّقة', value: 'Applied' },
]

const waiverHeaders = [
  { title: 'الزبون', key: 'customerName' },
  { title: 'الهاتف', key: 'customerPhone' },
  { title: 'مبلغ التنازل', key: 'approvedAmount' },
  { title: 'من وافق (مفوض)', key: 'decidedByDisplayName' },
  { title: 'الحالة', key: 'status' },
  { title: 'ملاحظة', key: 'requestNote' },
  { title: 'تاريخ الطلب', key: 'requestedAt' },
]

const payersData = ref([])
const loading = ref(false)
const legalRows = ref([])
const legalLoading = ref(false)
const legalStatusFilter = ref('Pending')
const legalActingId = ref(null)
const legalBulkActing = ref(false)
const legalReviewDialog = ref(false)
const legalReviewMode = ref('approve')
const legalReviewNote = ref('')
const legalSelected = ref(null)
/** Multi-select requestIds for bulk approve/reject */
const legalCheckedIds = ref([])
const legalBulkIds = ref([])
const decideDialog = ref(false)
const submitting = ref(false)
const selectedCustomer = ref(null)
const decisionForm = ref({
  decisionType: '',
  note: '',
})

const noteRequiredDialog = ref(false)

const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "

  return "لا يوجد"
}

const formattedDate = date =>
  date ? new Date(date).toLocaleDateString('en-CA') : 'لا يوجد'

function daysSinceLastPayment(row) {
  const raw = row.lastPaymentDate ?? row.LastPaymentDate
  if (!raw) return 9999
  const d = new Date(raw)
  if (Number.isNaN(d.getTime())) return 9999
  const today = new Date()
  const a = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate())
  const b = Date.UTC(d.getFullYear(), d.getMonth(), d.getDate())

  return Math.floor((a - b) / 86400000)
}

const headers = computed(() => [
  { title: canDecide.value ? 'اتخاذ قرار' : 'الملف', key: 'actions', sortable: false },
  { title: 'اسم العميل', key: 'customerName' },
  { title: 'رقم الهاتف', key: 'phoneNumber' },
  { title: 'القائمة', key: 'delegateName' },
  { title: 'المباع', key: 'itemsNames' },
  { title: 'سعر البيع', key: 'amountTotalSales' },
  { title: 'المدفوع هذا الأسبوع', key: 'weekPaid' },
  { title: 'النسبة %', key: 'paidPercent' },
  { title: 'الباقي', key: 'amountRemaining' },
  { title: 'تاريخ اخر تسديد', key: 'lastPaymentDate' },
])

const decisionOptions = [
  { title: 'متواصل معه', value: 'متواصل', color: 'info', icon: 'tabler-phone-call' },
  { title: 'إرسال للقانونية', value: 'قانونية', color: 'error', icon: 'tabler-scale' },
  { title: 'مبيع وهمي', value: 'وهمي', color: 'warning', icon: 'tabler-alert-octagon' },
  { title: 'التنازل', value: 'تنازل', color: 'success', icon: 'tabler-cash' },
]

const totals = computed(() => [
  {
    icon: 'tabler-user',
    value: payersData.value.length,
    title: 'عدد الزبائن',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-percentage',
    value: payersData.value.length
      ? (payersData.value.reduce((sum, row) => sum + (row.paidPercent || 0), 0) / payersData.value.length).toFixed(2) + ' %'
      : '0 %',
    title: 'متوسط النسبة',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-wallet',
    value: formattedNumber(
      payersData.value.reduce((sum, row) => sum + (row.amountTotalSales || 0), 0),
    ),
    title: 'سعر البيع',
    color: "success",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
  {
    icon: 'tabler-currency-dollar',
    value: formattedNumber(
      payersData.value.reduce((sum, row) => sum + (row.weekPaid || 0), 0),
    ),
    title: 'مدفوع الأسبوع',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
])

function applyWeakPayerRows(rows) {
  payersData.value = (rows || []).filter(r => {
    if (Number(r.amountRemaining ?? r.AmountRemaining ?? 0) <= 0) return false

    return daysSinceLastPayment(r) >= 40
  })
  namesLoaded.value = true
}

async function fetchWeakPayers(force = false) {
  if (namesLoaded.value && !force)
    return

  if (isMonitor.value && !monitorCity.value) {
    payersData.value = []
    namesLoaded.value = false
    loading.value = false

    return
  }

  try {
    loading.value = true
    if (isMonitor.value) {
      const data = await directorLegalWeakWeekPayers(monitorCity.value)
      applyWeakPayerRows(Array.isArray(data) ? data : [])
    }
    else {
      const authHeader = getAuthHeaders()
      // نفس مصدر تطبيق إدارة الفرع — الفلتر الثقيل في الـ SP (متبقي + ≥40 يوم)
      const response = await axios.get(`${apiUrl}CustomerDecisions/WeakWeekPayers`, {
        headers: authHeader,
        timeout: 60000,
      })
      applyWeakPayerRows(response.data || [])
    }
  } catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'تعذر جلب لوحة الأسماء')
  } finally {
    loading.value = false
  }
}

function waiverStatusLabel(status) {
  if (status === 'Pending') return 'بانتظار المحاسب'
  if (status === 'Applied') return 'مطبّق'

  return status || '—'
}

function waiverStatusColor(status) {
  if (status === 'Pending') return 'warning'
  if (status === 'Applied') return 'success'

  return 'secondary'
}

async function fetchWaiverRows(force = false) {
  if (waiverLoaded.value && !force)
    return

  if (isMonitor.value && !monitorCity.value) {
    waiverRows.value = []
    waiverLoaded.value = false
    waiverLoading.value = false

    return
  }

  try {
    waiverLoading.value = true
    if (isMonitor.value) {
      const data = await directorLegalWaiverAccountantRequests(monitorCity.value, {
        status: waiverStatusFilter.value || undefined,
        take: 200,
        skip: 0,
      })
      waiverRows.value = data?.items || []
    }
    else if (isAdmin.value) {
      const params = { take: 200, skip: 0 }
      if (waiverStatusFilter.value)
        params.status = waiverStatusFilter.value
      const { data } = await axios.get(`${apiUrl}CustomerDecisions/WaiverAccountantRequests`, {
        headers: getAuthHeaders(),
        params,
        timeout: 60000,
      })
      waiverRows.value = data?.items || []
    }
    else {
      waiverRows.value = []
    }
    waiverLoaded.value = true
  }
  catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'تعذر جلب طلبات التنازل')
    waiverRows.value = []
  }
  finally {
    waiverLoading.value = false
  }
}

function openDecide(item) {
  if (!canDecide.value) {
    toast.warning('اتخاذ القرار من صلاحية مدير الفرع فقط')

    return
  }

  selectedCustomer.value = item
  decisionForm.value = { decisionType: '', note: '' }
  decideDialog.value = true
}

function openProfile(item) {
  const customerId = item.customerID || item.customerId
  if (isMonitor.value) {
    router.push({
      name: 'sales-manager-customer-profile',
      query: {
        cityValue: monitorCity.value,
        customerId,
        name: item.customerName || item.CustomerName || '',
        returnTo: 'decision-board',
      },
    })

    return
  }
  router.push({
    name: 'customer-profile',
    query: { customerID: customerId },
  })
}

async function submitDecision() {
  if (!canDecide.value) {
    toast.warning('اتخاذ القرار من صلاحية مدير الفرع فقط')

    return
  }

  if (!selectedCustomer.value || !decisionForm.value.decisionType) {
    toast.warning('اختر نوع القرار')

    return
  }

  const type = decisionForm.value.decisionType
  const note = (decisionForm.value.note || '').trim()
  if (!note) {
    noteRequiredDialog.value = true

    return
  }

  try {
    submitting.value = true
    const authHeader = getAuthHeaders()
    const decidedId = selectedCustomer.value.customerID

    if (type === 'تنازل') {
      await axios.post(`${apiUrl}branch-manager/waivers`, {
        customerId: decidedId,
        customerName: selectedCustomer.value.customerName || null,
        customerPhone: selectedCustomer.value.phoneNumber || null,
        note,
      }, { headers: authHeader })
      toast.success('تم إرسال طلب التنازل إلى المدير المفوض')
    }
    else {
      await axios.post(`${apiUrl}CustomerDecisions/Decide`, {
        customerID: decidedId,
        decisionType: type,
        note: note || null,
      }, { headers: authHeader })

      const isLegalRequest = type === 'قانونية'
      toast.success(isLegalRequest
        ? 'تم إرسال طلب القانونية للمحاسب — خرج من ضعفاء المدير ويبقى عند المندوب حتى الموافقة'
        : 'تم حفظ القرار')
    }

    decideDialog.value = false
    payersData.value = payersData.value.filter(row =>
      Number(row.customerID ?? row.CustomerID) !== Number(decidedId))
    namesLoaded.value = false
    await fetchWeakPayers(true)
  }
  catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'فشل حفظ القرار')
  }
  finally {
    submitting.value = false
  }
}

const LEGAL_COLS_STORAGE_KEY = 'decision-board-legal-col-order'

/** طلبات التحويل: حقول الطلب + تفاصيل الزبون (متابعة المندوبين) */
const LEGAL_COLUMN_DEFS = [
  { title: 'اختيار', key: 'select', sortable: false, exportable: false, reorderable: false },
  { title: 'إجراء', key: 'actions', sortable: false, exportable: false },
  { title: 'الزبون', key: 'customerName', exportable: true },
  { title: 'الحالة', key: 'status', exportable: true },
  { title: 'طلب من', key: 'requestedByName', exportable: true },
  { title: 'القائمة', key: 'delegateName', exportable: true },
  { title: 'نسبة الأسبوع %', key: 'paidPercent', exportable: true },
  { title: 'مدفوع الأسبوع', key: 'weekPaid', exportable: true },
  { title: 'ملاحظة الطلب', key: 'requestNote', exportable: true },
  { title: 'تاريخ الطلب', key: 'requestedAt', exportable: true },
  { title: 'راجع بواسطة', key: 'reviewedByName', exportable: true },
  { title: 'ملاحظة المراجعة', key: 'reviewNote', exportable: true },
  { title: 'تاريخ المراجعة', key: 'reviewedAt', exportable: true },
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

function defaultLegalColumnOrder() {
  return LEGAL_COLUMN_DEFS.map(c => c.key)
}

function loadLegalColumnOrder() {
  try {
    const raw = JSON.parse(localStorage.getItem(LEGAL_COLS_STORAGE_KEY) || '[]')
    if (!Array.isArray(raw) || !raw.length)
      return defaultLegalColumnOrder()
    const known = new Set(LEGAL_COLUMN_DEFS.map(c => c.key))
    let order = raw.filter(k => known.has(k) && k !== 'select')
    for (const c of LEGAL_COLUMN_DEFS) {
      if (c.key === 'select')
        continue
      if (!order.includes(c.key))
        order.push(c.key)
    }
    // Checkbox column always first (RTL: rightmost)
    order = ['select', ...order]

    return order
  }
  catch {
    return defaultLegalColumnOrder()
  }
}

const legalColumnOrder = ref(loadLegalColumnOrder())
const legalDragFromKey = ref(null)
const legalDragOverKey = ref(null)
const legalHeaderHoldReady = ref(false)
const legalHeaderPressKey = ref(null)
let legalHeaderDownAt = 0
let legalHeaderHoldTimer = null

const legalTableHeaders = computed(() =>
  legalColumnOrder.value
    .map(key => LEGAL_COLUMN_DEFS.find(c => c.key === key))
    .filter(Boolean)
    .map(c => ({
      title: c.title,
      key: c.key,
      sortable: false,
    })))

function persistLegalColumnOrder() {
  localStorage.setItem(LEGAL_COLS_STORAGE_KEY, JSON.stringify(legalColumnOrder.value))
}

function moveLegalColumn(fromKey, toKey) {
  if (!fromKey || !toKey || fromKey === toKey)
    return
  if (fromKey === 'select' || toKey === 'select')
    return
  const order = [...legalColumnOrder.value]
  const from = order.indexOf(fromKey)
  const to = order.indexOf(toKey)
  if (from < 0 || to < 0)
    return
  order.splice(from, 1)
  order.splice(to, 0, fromKey)
  // keep select pinned first
  const without = order.filter(k => k !== 'select')
  legalColumnOrder.value = ['select', ...without]
  persistLegalColumnOrder()
}

function resetLegalColumnOrder() {
  legalColumnOrder.value = defaultLegalColumnOrder()
  persistLegalColumnOrder()
  toast.success('تمت إعادة ترتيب الأعمدة للافتراضي')
}

function clearLegalHeaderHold() {
  clearTimeout(legalHeaderHoldTimer)
  legalHeaderHoldTimer = null
  legalHeaderPressKey.value = null
  legalHeaderHoldReady.value = false
}

function isLegalColReorderable(key) {
  const def = LEGAL_COLUMN_DEFS.find(c => c.key === key)

  return def?.reorderable !== false && key !== 'select'
}

function onLegalHeaderPointerDown(key) {
  if (!isLegalColReorderable(key))
    return
  legalHeaderPressKey.value = key
  legalHeaderDownAt = Date.now()
  legalHeaderHoldReady.value = false
  clearTimeout(legalHeaderHoldTimer)
  legalHeaderHoldTimer = setTimeout(() => {
    if (legalHeaderPressKey.value === key)
      legalHeaderHoldReady.value = true
  }, 400)
}

function onLegalHeaderDragStart(e, key) {
  if (!isLegalColReorderable(key)) {
    e.preventDefault()

    return
  }
  const heldLongEnough = legalHeaderHoldReady.value || (Date.now() - legalHeaderDownAt >= 400)
  if (!heldLongEnough || legalHeaderPressKey.value !== key) {
    e.preventDefault()
    clearLegalHeaderHold()

    return
  }
  legalDragFromKey.value = key
  e.dataTransfer.effectAllowed = 'move'
  try {
    e.dataTransfer.setData('text/plain', key)
  }
  catch { /* ignore */ }
}

const legalPendingRows = computed(() =>
  legalRows.value.filter(r => r.status === 'Pending'))

const legalAllPendingChecked = computed(() => {
  const pending = legalPendingRows.value
  if (!pending.length)
    return false

  return pending.every(r => legalCheckedIds.value.includes(r.requestId))
})

const legalSomePendingChecked = computed(() => {
  const pending = legalPendingRows.value
  if (!pending.length)
    return false
  const n = pending.filter(r => legalCheckedIds.value.includes(r.requestId)).length

  return n > 0 && n < pending.length
})

const legalCheckedPendingCount = computed(() =>
  legalPendingRows.value.filter(r => legalCheckedIds.value.includes(r.requestId)).length)

function toggleLegalCheck(requestId, checked) {
  const id = Number(requestId)
  const set = new Set(legalCheckedIds.value)
  if (checked)
    set.add(id)
  else
    set.delete(id)
  legalCheckedIds.value = [...set]
}

function toggleLegalCheckAll(checked) {
  if (!checked) {
    legalCheckedIds.value = []

    return
  }
  legalCheckedIds.value = legalPendingRows.value.map(r => r.requestId)
}

function openLegalBulkReview(mode) {
  if (!canReviewLegal.value) {
    toast.warning('الموافقة/الرفض من صلاحية المراقب فقط')

    return
  }
  const ids = legalPendingRows.value
    .filter(r => legalCheckedIds.value.includes(r.requestId))
    .map(r => r.requestId)
  if (!ids.length) {
    toast.warning('حدّد طلبات بانتظار الموافقة أولاً')

    return
  }
  legalBulkIds.value = ids
  legalSelected.value = null
  legalReviewMode.value = mode
  legalReviewNote.value = ''
  legalReviewDialog.value = true
}

function onLegalHeaderDragOver(e, key) {
  if (!legalDragFromKey.value)
    return
  e.preventDefault()
  legalDragOverKey.value = key
}

function onLegalHeaderDrop(e, key) {
  e.preventDefault()
  const from = legalDragFromKey.value || e.dataTransfer.getData('text/plain')
  moveLegalColumn(from, key)
  legalDragFromKey.value = null
  legalDragOverKey.value = null
  clearLegalHeaderHold()
}

function onLegalHeaderDragEnd() {
  legalDragFromKey.value = null
  legalDragOverKey.value = null
  clearLegalHeaderHold()
}

const legalTopScrollEl = ref(null)
const legalBottomScrollEl = ref(null)
const legalTableScrollWidth = ref(1200)
let legalScrollSyncLock = false

function syncLegalTableWidth() {
  nextTick(() => {
    const host = legalBottomScrollEl.value
    if (!host)
      return
    const table = host.querySelector('table')
    legalTableScrollWidth.value = Math.max(table?.scrollWidth || 0, host.scrollWidth || 0, 1200)
  })
}

function onLegalTopScroll() {
  if (legalScrollSyncLock || !legalBottomScrollEl.value || !legalTopScrollEl.value)
    return
  legalScrollSyncLock = true
  legalBottomScrollEl.value.scrollLeft = legalTopScrollEl.value.scrollLeft
  legalScrollSyncLock = false
}

function onLegalBottomScroll() {
  if (legalScrollSyncLock || !legalBottomScrollEl.value || !legalTopScrollEl.value)
    return
  legalScrollSyncLock = true
  legalTopScrollEl.value.scrollLeft = legalBottomScrollEl.value.scrollLeft
  legalScrollSyncLock = false
}

watch(legalRows, () => syncLegalTableWidth(), { deep: true })
watch(legalTableHeaders, () => syncLegalTableWidth())
watch(boardTab, v => {
  if (v === 'legal')
    nextTick(() => syncLegalTableWidth())
})

const legalStatusItems = [
  { title: 'بانتظار الموافقة', value: 'Pending' },
  { title: 'موافق عليها', value: 'Approved' },
  { title: 'مرفوضة', value: 'Rejected' },
  { title: 'الكل', value: '' },
]

function legalStatusLabel(status) {
  if (status === 'Pending') return 'بانتظار'
  if (status === 'Approved') return 'موافق'
  if (status === 'Rejected') return 'مرفوض'

  return status || '—'
}

function legalStatusColor(status) {
  if (status === 'Pending') return 'warning'
  if (status === 'Approved') return 'success'
  if (status === 'Rejected') return 'error'

  return 'primary'
}

function legalFmtDate(v) {
  if (!v)
    return '—'
  const d = new Date(v)
  if (Number.isNaN(d.getTime()))
    return '—'

  return d.toLocaleDateString('en-CA')
}

function legalExportCell(row, key) {
  switch (key) {
  case 'customerName': return row.customerName || '—'
  case 'status': return legalStatusLabel(row.status)
  case 'requestedByName': return row.requestedByName || '—'
  case 'delegateName': return row.delegateName || '—'
  case 'paidPercent': return row.paidPercent ?? '—'
  case 'weekPaid': return row.weekPaid ?? '—'
  case 'requestNote': return row.requestNote || '—'
  case 'requestedAt': return legalFmtDate(row.requestedAt)
  case 'reviewedByName': return row.reviewedByName || '—'
  case 'reviewNote': return row.reviewNote || '—'
  case 'reviewedAt': return legalFmtDate(row.reviewedAt)
  case 'dateSaleDevice': return legalFmtDate(row.dateSaleDevice)
  case 'amountTotalSales': return row.amountTotalSales ?? '—'
  case 'receiptsTotal': return row.receiptsTotal ?? '—'
  case 'amountRemaining': return row.amountRemaining ?? '—'
  case 'amountDaySales': return row.amountDaySales ?? '—'
  case 'lastPaymentDate': return legalFmtDate(row.lastPaymentDate)
  case 'lastPaymentAmount': return row.lastPaymentAmount ?? '—'
  case 'receiptRateDevice':
    return row.receiptRateDevice != null ? `${row.receiptRateDevice}%` : '—'
  case 'countReceiptDevice': return row.countReceiptDevice ?? '—'
  case 'numberOfDayDevice': return row.numberOfDayDevice ?? '—'
  case 'address': return row.address || '—'
  case 'phoneNumber': return row.phoneNumber || '—'
  case 'shopName': return row.shopName || '—'
  case 'receiptName': return row.receiptName || '—'
  case 'saleName': return row.saleName || '—'
  case 'itemsNames': return row.itemsNames || '—'
  case 'notes': return row.notes || '—'
  default: return ''
  }
}

function exportLegalToExcel() {
  const cols = legalTableHeaders.value
    .map(h => LEGAL_COLUMN_DEFS.find(c => c.key === h.key))
    .filter(c => c && c.exportable !== false)
  if (!cols.length || !legalRows.value.length) {
    toast.warning('لا توجد صفوف للتصدير')

    return
  }
  const dataToExport = legalRows.value.map(row => {
    const obj = {}
    for (const c of cols)
      obj[c.title] = legalExportCell(row, c.key)

    return obj
  })
  const ws = XLSX.utils.json_to_sheet(dataToExport)
  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'LegalRequests')
  const city = monitorCity.value || 'branch'
  XLSX.writeFile(wb, `طلبات_تحويل_قانونية_${city}.xlsx`)
  toast.success('تم تصدير Excel')
}

async function fetchLegalRows(force = false) {
  if (legalLoaded.value && !force)
    return

  if (isMonitor.value && !monitorCity.value) {
    legalRows.value = []
    legalLoaded.value = false
    legalLoading.value = false

    return
  }

  try {
    legalLoading.value = true
    if (isMonitor.value) {
      const data = await directorLegalConversionRequests(monitorCity.value, {
        status: legalStatusFilter.value || undefined,
        take: 200,
        skip: 0,
      })
      legalRows.value = data?.items || []
    }
    else {
      const authHeader = getAuthHeaders()
      const params = { take: 200, skip: 0 }
      if (legalStatusFilter.value)
        params.status = legalStatusFilter.value

      const { data } = await axios.get(`${apiUrl}CustomerDecisions/LegalConversionRequests`, {
        headers: authHeader,
        params,
      })
      legalRows.value = data?.items || []
    }
    legalLoaded.value = true
    const pendingIds = new Set(
      (legalRows.value || []).filter(r => r.status === 'Pending').map(r => r.requestId),
    )
    legalCheckedIds.value = legalCheckedIds.value.filter(id => pendingIds.has(id))
    syncLegalTableWidth()
  }
  catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'تعذر جلب طلبات التحويل')
  }
  finally {
    legalLoading.value = false
  }
}

function openLegalReview(item, mode) {
  if (!canReviewLegal.value) {
    toast.warning('الموافقة/الرفض من صلاحية المراقب فقط')

    return
  }
  legalBulkIds.value = []
  legalSelected.value = item
  legalReviewMode.value = mode
  legalReviewNote.value = ''
  legalReviewDialog.value = true
}

async function applyLegalReviewOne(id, mode, note) {
  if (isMonitor.value) {
    if (!monitorCity.value)
      throw new Error('اختر المحافظة أولاً')
    if (mode === 'approve')
      await directorLegalConversionApprove(monitorCity.value, id, note || null)
    else
      await directorLegalConversionReject(monitorCity.value, id, note)
  }
  else {
    const path = mode === 'approve' ? 'Approve' : 'Reject'
    await axios.put(
      `${apiUrl}CustomerDecisions/LegalConversionRequests/${id}/${path}`,
      { note: note || null },
      { headers: getAuthHeaders() },
    )
  }
}

async function submitLegalReview() {
  if (!canReviewLegal.value)
    return

  const bulk = legalBulkIds.value.length > 0
  if (!bulk && !legalSelected.value)
    return

  if (legalReviewMode.value === 'reject' && !String(legalReviewNote.value || '').trim()) {
    toast.warning('ملاحظة الرفض مطلوبة')

    return
  }

  const ids = bulk ? [...legalBulkIds.value] : [legalSelected.value.requestId]
  const mode = legalReviewMode.value
  const note = legalReviewNote.value || null

  try {
    if (bulk)
      legalBulkActing.value = true
    else
      legalActingId.value = ids[0]

    let ok = 0
    let fail = 0
    for (const id of ids) {
      try {
        await applyLegalReviewOne(id, mode, note)
        ok++
      }
      catch (err) {
        console.error(err)
        fail++
      }
    }

    legalReviewDialog.value = false
    legalBulkIds.value = []
    legalCheckedIds.value = []
    if (fail === 0) {
      toast.success(mode === 'approve'
        ? (ok > 1 ? `تمت الموافقة على ${ok} طلبات` : 'تمت الموافقة وتحويل الزبون لقانونية')
        : (ok > 1 ? `تم رفض ${ok} طلبات` : 'تم رفض الطلب'))
    }
    else {
      toast.warning(`تم: ${ok} — فشل: ${fail}`)
    }
    legalLoaded.value = false
    await fetchLegalRows(true)
  }
  catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || error.message || 'فشل تنفيذ المراجعة')
  }
  finally {
    legalActingId.value = null
    legalBulkActing.value = false
  }
}

function syncTabFromRoute() {
  const t = String(route.query.tab || '')
  if (t === 'legal')
    boardTab.value = 'legal'
  else if (t === 'waivers' || t === 'waiver')
    boardTab.value = 'waivers'
  else
    boardTab.value = 'open'
}

watch(boardTab, tab => {
  if (tab === 'legal')
    fetchLegalRows()
  else if (tab === 'waivers')
    fetchWaiverRows()
  else
    fetchWeakPayers()
})

watch(monitorCity, () => {
  if (!isMonitor.value)
    return
  namesLoaded.value = false
  legalLoaded.value = false
  waiverLoaded.value = false
  if (boardTab.value === 'legal')
    fetchLegalRows(true)
  else if (boardTab.value === 'waivers')
    fetchWaiverRows(true)
  else
    fetchWeakPayers(true)
})

onMounted(() => {
  syncTabFromRoute()
  if (boardTab.value === 'legal')
    fetchLegalRows()
  else if (boardTab.value === 'waivers')
    fetchWaiverRows()
  else
    fetchWeakPayers(true)
})
</script>

<template>
  <VCard
    v-if="isMonitor"
    class="pa-4 mb-4"
  >
    <div class="text-subtitle-1 mb-2">
      أسماء قانونية — لوحة المراقب
    </div>
    <VSelect
      :model-value="monitorCity"
      :items="monitorCityItems"
      :loading="monitorCitiesLoading"
      label="المحافظة"
      item-title="title"
      item-value="value"
      clearable
      @update:model-value="setMonitorCity"
    />
    <VAlert
      v-if="!monitorCity"
      type="info"
      variant="tonal"
      class="mt-3"
    >
      اختر المحافظة لعرض اللوحة وطلبات التحويل والتنازلات
    </VAlert>
  </VCard>

  <VCard class="pa-4 mb-4">
    <VTabs
      v-model="boardTab"
      color="primary"
    >
      <VTab value="open">
        لوحة الأسماء
      </VTab>
      <VTab value="legal">
        طلبات التحويل
      </VTab>
      <VTab
        v-if="isMonitor || isAdmin"
        value="waivers"
      >
        التنازلات
      </VTab>
    </VTabs>
  </VCard>

  <template v-if="boardTab === 'open'">
    <VRow class="stats-row mb-6">
      <VCol
        v-for="(card, index) in totals"
        :key="index"
        cols="12"
        sm="6"
        md="3"
      >
        <ModernStatCard
          :title="card.title"
          :value="card.value"
          :icon="card.icon"
          :color="card.color"
          :gradient="card.gradient"
        />
      </VCol>
    </VRow>

    <VCard class="pa-10">
      <VRow class="mb-4">
        <VCol cols="12">
          <div class="text-h6 mb-2">
            لوحة الأسماء — نفس الأسماء في تطبيق إدارة الفرع
          </div>
          <div class="text-body-2 text-medium-emphasis mb-3">
            ضعفاء الأسبوع (≤ 3.8%) · متبقي &gt; 0 · آخر تسديد منذ ≥ 40 يوماً · بلا قرار بعد
          </div>
          <VAlert
            v-if="!canDecide"
            type="info"
            variant="tonal"
            class="mb-4"
          >
            اتخاذ القرار من صلاحية مدير الفرع فقط. يمكنك فتح الملف وكتابة ملاحظة.
          </VAlert>
          <VBtn
            color="primary"
            :loading="loading"
            prepend-icon="tabler-refresh"
            class="me-2"
            @click="fetchWeakPayers(true)"
          >
            تحديث اللوحة
          </VBtn>
          <VBtn
            color="secondary"
            variant="tonal"
            prepend-icon="tabler-list-check"
            to="/decisions-list"
          >
            سجل القرارات
          </VBtn>
        </VCol>
      </VRow>

      <VDataTable
        :headers="headers"
        :items="payersData"
        :loading="loading"
        :items-per-page="50"
        style="overflow: hidden; block-size: 100%;white-space: nowrap;"
        items-per-page-text="عدد السجل"
        class="text-no-wrap custom-data-table"
      >
      <template #item.actions="{ item }">
        <div class="d-flex gap-1">
          <VBtn
            v-if="canDecide"
            color="primary"
            prepend-icon="tabler-gavel"
            @click="openDecide(item)"
          >
            اتخاذ قرار
          </VBtn>
          <VBtn
            color="secondary"
            variant="tonal"
            prepend-icon="tabler-user"
            @click="openProfile(item)"
          >
            الملف
          </VBtn>
        </div>
      </template>
      <template #item.customerName="{ item }">
        <div>{{ item.customerName }}</div>
      </template>
      <template #item.phoneNumber="{ item }">
        <div>{{ item.phoneNumber || 'لا يوجد' }}</div>
      </template>
      <template #item.delegateName="{ item }">
        <div>{{ item.delegateName || 'لا يوجد' }}</div>
      </template>
      <template #item.itemsNames="{ item }">
        <div>{{ item.itemsNames || 'لا يوجد' }}</div>
      </template>
      <template #item.amountTotalSales="{ item }">
        <div class="premium-amount amt-total-sales">
          {{ formattedNumber(item.amountTotalSales) }}
        </div>
      </template>
      <template #item.weekPaid="{ item }">
        <div class="premium-amount amt-total-receipts">
          {{ formattedNumber(item.weekPaid) }}
        </div>
      </template>
      <template #item.paidPercent="{ item }">
        <VChip
          color="error"
          size="small"
          label
        >
          {{ item.paidPercent ?? 0 }} %
        </VChip>
      </template>
      <template #item.amountRemaining="{ item }">
        <div class="premium-amount amt-remaining">
          {{ formattedNumber(item.amountRemaining) }}
        </div>
      </template>
      <template #item.lastPaymentDate="{ item }">
        <div>{{ item.lastPaymentDate ? formattedDate(item.lastPaymentDate) : 'لا يوجد' }}</div>
      </template>
      </VDataTable>
    </VCard>
  </template>

  <VCard
    v-else-if="boardTab === 'legal'"
    class="pa-10 legal-board-card"
  >
    <div class="text-h6 mb-4">
      {{ canReviewLegal ? 'طلبات التحويل القانونية — موافقة المراقب' : 'طلبات التحويل القانونية — عرض فقط' }}
    </div>
    <VRow class="mb-4">
      <VCol
        cols="12"
        md="4"
      >
        <VSelect
          v-model="legalStatusFilter"
          :items="legalStatusItems"
          item-title="title"
          item-value="value"
          label="الحالة"
          @update:model-value="() => { legalLoaded = false; fetchLegalRows(true) }"
        />
      </VCol>
      <VCol
        cols="12"
        md="8"
        class="d-flex align-end justify-end flex-wrap ga-2"
      >
        <VBtn
          v-if="canReviewLegal"
          size="small"
          color="success"
          prepend-icon="tabler-checks"
          :disabled="!legalCheckedPendingCount"
          :loading="legalBulkActing"
          @click="openLegalBulkReview('approve')"
        >
          موافقة المحدد ({{ legalCheckedPendingCount }})
        </VBtn>
        <VBtn
          v-if="canReviewLegal"
          size="small"
          color="error"
          prepend-icon="tabler-x"
          :disabled="!legalCheckedPendingCount"
          :loading="legalBulkActing"
          @click="openLegalBulkReview('reject')"
        >
          رفض المحدد ({{ legalCheckedPendingCount }})
        </VBtn>
        <VBtn
          size="small"
          variant="tonal"
          prepend-icon="tabler-columns"
          @click="resetLegalColumnOrder"
        >
          ترتيب افتراضي
        </VBtn>
        <VBtn
          size="small"
          color="success"
          variant="tonal"
          prepend-icon="tabler-file-spreadsheet"
          :disabled="!legalRows.length"
          @click="exportLegalToExcel"
        >
          تصدير إلى Excel
        </VBtn>
        <VBtn
          color="primary"
          :loading="legalLoading"
          prepend-icon="tabler-refresh"
          @click="fetchLegalRows(true)"
        >
          تحديث
        </VBtn>
      </VCol>
    </VRow>
    <VAlert
      v-if="!canReviewLegal"
      type="info"
      variant="tonal"
      class="mb-4"
    >
      عرض الطلبات فقط. الموافقة والرفض من صلاحية المراقب.
    </VAlert>
    <p class="legal-table-hint">
      حدّد صفوفاً بالمربع ثم موافقة/رفض المحدد — رأس الأعمدة ثابت داخل الجدول عند التمرير — اضغط مطولاً على رأس العمود ثم اسحبه لإعادة الترتيب
    </p>
    <div
      ref="legalTopScrollEl"
      class="legal-h-scroll legal-h-scroll--top"
      @scroll="onLegalTopScroll"
    >
      <div
        class="legal-h-scroll__spacer"
        :style="{ width: `${Math.max(legalTableScrollWidth, 800)}px` }"
      />
    </div>
    <div
      ref="legalBottomScrollEl"
      class="legal-table-host"
      @scroll="onLegalBottomScroll"
    >
      <VDataTable
        :headers="legalTableHeaders"
        :items="legalRows"
        :loading="legalLoading"
        items-per-page="50"
        density="comfortable"
        class="legal-dfu-table"
        item-value="requestId"
      >
        <template #headers="{ columns }">
          <tr>
            <th
              v-for="column in columns"
              :key="column.key"
              class="legal-dfu-th"
              :class="{
                'legal-dfu-th--armed': legalHeaderHoldReady && legalHeaderPressKey === column.key,
                'legal-dfu-th--over': legalDragOverKey === column.key,
                'legal-dfu-th--fixed': column.key === 'select',
              }"
              :draggable="isLegalColReorderable(column.key)"
              @pointerdown="onLegalHeaderPointerDown(column.key)"
              @pointercancel="clearLegalHeaderHold"
              @dragstart="onLegalHeaderDragStart($event, column.key)"
              @dragover="onLegalHeaderDragOver($event, column.key)"
              @drop="onLegalHeaderDrop($event, column.key)"
              @dragend="onLegalHeaderDragEnd"
            >
              <template v-if="column.key === 'select'">
                <VCheckbox
                  v-if="canReviewLegal"
                  :model-value="legalAllPendingChecked"
                  :indeterminate="legalSomePendingChecked"
                  density="compact"
                  hide-details
                  @update:model-value="toggleLegalCheckAll"
                />
                <span
                  v-else
                  class="legal-dfu-th__label"
                >اختيار</span>
              </template>
              <span
                v-else
                class="legal-dfu-th__label"
              >{{ column.title }}</span>
            </th>
          </tr>
        </template>
        <template #item.select="{ item }">
          <VCheckbox
            v-if="canReviewLegal && item.status === 'Pending'"
            :model-value="legalCheckedIds.includes(item.requestId)"
            density="compact"
            hide-details
            @update:model-value="v => toggleLegalCheck(item.requestId, v)"
          />
          <span v-else>—</span>
        </template>
        <template #item.actions="{ item }">
          <div class="d-flex ga-1 legal-cell-nowrap">
            <IconBtn
              size="small"
              @click="openProfile(item)"
            >
              <VIcon icon="tabler-user" />
            </IconBtn>
            <VBtn
              v-if="canReviewLegal && item.status === 'Pending'"
              size="small"
              color="success"
              variant="tonal"
              :loading="legalActingId === item.requestId"
              @click="openLegalReview(item, 'approve')"
            >
              موافقة
            </VBtn>
            <VBtn
              v-if="canReviewLegal && item.status === 'Pending'"
              size="small"
              color="error"
              variant="tonal"
              :loading="legalActingId === item.requestId"
              @click="openLegalReview(item, 'reject')"
            >
              رفض
            </VBtn>
          </div>
        </template>
        <template #item.customerName="{ item }">
          <span
            class="legal-cell-nowrap legal-customer-name"
            :title="item.customerName || ''"
          >{{ item.customerName || '—' }}</span>
        </template>
        <template #item.status="{ item }">
          <VChip
            size="small"
            :color="legalStatusColor(item.status)"
            label
            class="legal-cell-nowrap"
          >
            {{ legalStatusLabel(item.status) }}
          </VChip>
        </template>
        <template #item.requestedAt="{ item }">
          <span class="legal-cell-nowrap">{{ legalFmtDate(item.requestedAt) }}</span>
        </template>
        <template #item.reviewedAt="{ item }">
          <span class="legal-cell-nowrap">{{ legalFmtDate(item.reviewedAt) }}</span>
        </template>
        <template #item.dateSaleDevice="{ item }">
          <span class="legal-cell-nowrap">{{ legalFmtDate(item.dateSaleDevice) }}</span>
        </template>
        <template #item.lastPaymentDate="{ item }">
          <span class="legal-cell-nowrap">{{ legalFmtDate(item.lastPaymentDate) }}</span>
        </template>
        <template #item.amountTotalSales="{ item }">
          <span class="legal-cell-nowrap">{{ formattedNumber(item.amountTotalSales) }}</span>
        </template>
        <template #item.receiptsTotal="{ item }">
          <span class="legal-cell-nowrap">{{ formattedNumber(item.receiptsTotal) }}</span>
        </template>
        <template #item.amountRemaining="{ item }">
          <span class="legal-cell-nowrap">{{ formattedNumber(item.amountRemaining) }}</span>
        </template>
        <template #item.amountDaySales="{ item }">
          <span class="legal-cell-nowrap">{{ formattedNumber(item.amountDaySales) }}</span>
        </template>
        <template #item.weekPaid="{ item }">
          <span class="legal-cell-nowrap">{{ formattedNumber(item.weekPaid) }}</span>
        </template>
        <template #item.lastPaymentAmount="{ item }">
          <span class="legal-cell-nowrap">{{ formattedNumber(item.lastPaymentAmount) }}</span>
        </template>
        <template #item.paidPercent="{ item }">
          <span class="legal-cell-nowrap">{{ item.paidPercent != null ? `${Number(item.paidPercent).toFixed(1)}%` : '—' }}</span>
        </template>
        <template #item.receiptRateDevice="{ item }">
          <span class="legal-cell-nowrap">{{ item.receiptRateDevice != null ? `${item.receiptRateDevice}%` : '—' }}</span>
        </template>
        <template #item.requestNote="{ item }">
          <span
            class="legal-cell-nowrap"
            :title="item.requestNote || ''"
          >{{ item.requestNote || '—' }}</span>
        </template>
        <template #item.reviewNote="{ item }">
          <span
            class="legal-cell-nowrap"
            :title="item.reviewNote || ''"
          >{{ item.reviewNote || '—' }}</span>
        </template>
        <template #item.address="{ item }">
          <span
            class="legal-cell-nowrap"
            :title="item.address || ''"
          >{{ item.address || '—' }}</span>
        </template>
        <template #item.itemsNames="{ item }">
          <span
            class="legal-cell-nowrap"
            :title="item.itemsNames || ''"
          >{{ item.itemsNames || '—' }}</span>
        </template>
        <template #item.notes="{ item }">
          <span
            class="legal-cell-nowrap"
            :title="item.notes || ''"
          >{{ item.notes || '—' }}</span>
        </template>
      </VDataTable>
    </div>
  </VCard>

  <VCard
    v-else-if="boardTab === 'waivers'"
    class="pa-10"
  >
    <div class="text-h6 mb-4">
      التنازلات — عرض فقط (الموافقة من المحاسب الرئيسي)
    </div>
    <VRow class="mb-4">
      <VCol
        cols="12"
        md="4"
      >
        <VSelect
          v-model="waiverStatusFilter"
          :items="waiverStatusItems"
          item-title="title"
          item-value="value"
          label="الحالة"
          @update:model-value="() => { waiverLoaded = false; fetchWaiverRows(true) }"
        />
      </VCol>
      <VCol
        cols="12"
        md="8"
        class="d-flex align-end justify-end"
      >
        <VBtn
          color="primary"
          :loading="waiverLoading"
          prepend-icon="tabler-refresh"
          @click="fetchWaiverRows(true)"
        >
          تحديث
        </VBtn>
      </VCol>
    </VRow>
    <VDataTable
      :headers="waiverHeaders"
      :items="waiverRows"
      :loading="waiverLoading"
      items-per-page="25"
      class="text-no-wrap"
    >
      <template #item.approvedAmount="{ item }">
        {{ formattedNumber(item.approvedAmount) }}
      </template>
      <template #item.status="{ item }">
        <VChip
          size="small"
          :color="waiverStatusColor(item.status)"
          label
        >
          {{ waiverStatusLabel(item.status) }}
        </VChip>
      </template>
      <template #item.requestedAt="{ item }">
        {{ item.requestedAt ? new Date(item.requestedAt).toLocaleString('ar-IQ') : '—' }}
      </template>
    </VDataTable>
  </VCard>

  <VDialog
    v-model="decideDialog"
    max-width="560"
  >
    <VCard>
      <VCardTitle class="d-flex align-center">
        اتخاذ قرار
        <VSpacer />
        <VBtn
          icon
          variant="text"
          @click="decideDialog = false"
        >
          <VIcon icon="tabler-x" />
        </VBtn>
      </VCardTitle>
      <VCardText>
        <div class="mb-4">
          الزبون: <strong>{{ selectedCustomer?.customerName }}</strong>
          — النسبة هذا الأسبوع:
          <strong>{{ selectedCustomer?.paidPercent ?? 0 }}%</strong>
        </div>
        <VRadioGroup v-model="decisionForm.decisionType">
          <VRadio
            v-for="opt in decisionOptions"
            :key="opt.value"
            :label="opt.title"
            :value="opt.value"
            :color="opt.color"
          />
        </VRadioGroup>
        <VTextarea
          v-model="decisionForm.note"
          label="ملاحظة (إجبارية)"
          rows="3"
          class="mt-4"
        />
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          @click="decideDialog = false"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="primary"
          :loading="submitting"
          :disabled="!decisionForm.decisionType"
          @click="submitDecision"
        >
          اعتماد القرار
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <VDialog
    v-model="noteRequiredDialog"
    max-width="420"
  >
    <VCard class="pa-4 text-center">
      <VCardTitle class="justify-center text-h6">
        الملاحظة إلزامية
      </VCardTitle>
      <VCardText>
        يجب كتابة ملاحظة قبل اعتماد القرار.
      </VCardText>
      <VCardActions class="justify-center">
        <VBtn
          color="primary"
          @click="noteRequiredDialog = false"
        >
          حسناً
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <VDialog
    v-model="legalReviewDialog"
    max-width="480"
  >
    <VCard>
      <VCardTitle>
        {{ legalBulkIds.length
          ? (legalReviewMode === 'approve' ? 'موافقة متعددة' : 'رفض متعدد')
          : (legalReviewMode === 'approve' ? 'موافقة تحويل قانونية' : 'رفض طلب قانونية') }}
      </VCardTitle>
      <VCardText>
        <div class="mb-3">
          <template v-if="legalBulkIds.length">
            عدد الطلبات المحددة: <strong>{{ legalBulkIds.length }}</strong>
          </template>
          <template v-else>
            الزبون: <strong>{{ legalSelected?.customerName }}</strong>
          </template>
        </div>
        <VTextarea
          v-model="legalReviewNote"
          :label="legalReviewMode === 'reject' ? 'ملاحظة الرفض (إلزامي)' : 'ملاحظة المراجعة (اختياري)'"
          :rules="legalReviewMode === 'reject' ? [v => !!String(v || '').trim() || 'ملاحظة الرفض مطلوبة'] : []"
          rows="3"
        />
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          :disabled="legalBulkActing || legalActingId != null"
          @click="legalReviewDialog = false"
        >
          إلغاء
        </VBtn>
        <VBtn
          :color="legalReviewMode === 'approve' ? 'success' : 'error'"
          :loading="legalBulkActing || legalActingId != null"
          @click="submitLegalReview"
        >
          {{ legalReviewMode === 'approve'
            ? (legalBulkIds.length ? `موافقة ${legalBulkIds.length}` : 'موافقة')
            : (legalBulkIds.length ? `رفض ${legalBulkIds.length}` : 'رفض') }}
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>

<style scoped>
.legal-table-hint {
  margin: 0 0 8px;
  font-size: 0.78rem;
  opacity: 0.8;
}
.legal-h-scroll {
  overflow-x: auto;
  overflow-y: hidden;
}
.legal-h-scroll--top {
  z-index: 6;
  height: 14px;
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.legal-h-scroll__spacer { height: 1px; }
.legal-table-host {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  max-height: calc(100vh - 260px);
  overflow-x: auto;
  overflow-y: auto;
  background: rgb(var(--v-theme-surface));
}
.legal-dfu-table :deep(table) {
  min-width: 2200px;
  border-collapse: separate;
  border-spacing: 0;
}
.legal-dfu-table :deep(.v-table__wrapper) {
  overflow: visible !important;
  max-height: none !important;
}
.legal-dfu-table :deep(thead) {
  position: sticky;
  top: 0;
  z-index: 5;
}
.legal-dfu-table :deep(thead th),
.legal-dfu-table :deep(.legal-dfu-th) {
  position: sticky !important;
  top: 0 !important;
  z-index: 5;
  background: rgb(var(--v-theme-surface)) !important;
  box-shadow: 0 1px 0 rgba(var(--v-border-color), var(--v-border-opacity));
  white-space: nowrap !important;
}
.legal-dfu-th {
  cursor: grab;
  user-select: none;
  white-space: nowrap;
  touch-action: none;
}
.legal-dfu-th--fixed {
  cursor: default;
  touch-action: auto;
}
.legal-dfu-th:active { cursor: grabbing; }
.legal-dfu-th--armed {
  outline: 2px solid rgba(var(--v-theme-primary), 0.55);
  background: rgba(var(--v-theme-primary), 0.12) !important;
}
.legal-dfu-th--over {
  box-shadow: inset 0 -3px 0 rgb(var(--v-theme-primary));
}
.legal-dfu-th__label { pointer-events: none; }
.legal-cell-nowrap {
  white-space: nowrap !important;
  display: inline-block;
  max-width: 22rem;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: middle;
}
.legal-customer-name {
  font-weight: 600;
  max-width: none;
}
.legal-dfu-table :deep(td) {
  white-space: nowrap !important;
  vertical-align: middle;
}
</style>
