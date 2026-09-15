<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import * as XLSX from 'xlsx'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { formatIraqDate, formatIraqTime } from '@/composables/iraqDate'
import {
  assignExceptionRequest,
  branchRowKey,
  createExceptionRequest,
  evaluationLabel,
  listExceptionRequests,
  managerSalePath,
  requestHistoryLabel,
  requestStatusLabel,
  salesManagerBase,
  smErrorMessage,
  smGet,
  smGetBlob,
  smGetEmployees,
  smPost,
  smPut,
  smDelete,
  withCityQuery,
  displayCityName,
} from '@/composables/salesManagerApi'
import { saleDisplayDocuments, saleDocumentTitle } from '@/composables/saleDocuments'
import ShopLocationLink from '@/components/ShopLocationLink.vue'
import { isDemo } from '@/composables/useCities'
import { useSalesBranches } from '@/composables/useSalesBranches'
import { useToast } from '@/composables/useToast'
import { markAllSalesRequestsRead, refreshSalesRequestUnread, salesRequestUnreadCount } from '@/composables/useSalesRequestUnread'
import {
  buildEvaluationKey,
  categoryBlock,
  categoryResultCount,
  categoryWorstLabel,
  categoryWorstScore,
  evaluationKeyFromRow,
  indexEvaluationItems,
  lookupEvaluation,
  receiptCountDisplay,
  requestCityOf,
  requestIdOf,
  settlementDaysDisplay,
} from '@/composables/salesRequestEvaluationMap'
import { parseSalesRequestExcel } from '@/composables/salesRequestExcelImport'
import { normalizeIraqPhone } from '@core/utils/validators'

const toast = useToast()
const router = useRouter()
const { branches } = useSalesBranches()
const rows = ref([])
const tab = ref('all')
const cityValue = ref('')
const selected = ref(null)
const detail = ref(null)
const profile = ref(null)
const profileOpen = ref(false)
const saleDetail = ref(null)
const shopImageUrl = ref('')
const shopImageOpen = ref(false)
const managerNote = ref('')
const employees = ref([])
const assignEmployeeId = ref(null)
const assignCityValue = ref('')
const returnOpen = ref(false)
const returnNote = ref('')
const rejectOpen = ref(false)
const rejectReason = ref('')
const pendOpen = ref(false)
const pendNote = ref('')
const exceptionOpen = ref(false)
const exceptionReason = ref('')
const exceptionBusy = ref(false)
const exceptionTarget = ref(null)
const exceptionRows = ref([])
const exceptionStatus = ref('Pending')
const exceptionListBusy = ref(false)
const exceptionAssignId = ref(null)
const busy = ref(false)
const excelInput = ref(null)
const importOpen = ref(false)
const importBusy = ref(false)
const importPreview = ref(null)
const intakeQuery = ref('')
const evaluations = ref({})
const evaluationsBusy = ref(false)
const evaluationsError = ref('')
/** Per-request inline evaluation UI: { criterion, title, hits, loading, error, expandedHitKey, profile, profileLoading } */
const evalPanels = ref({})
const hitsCache = ref({})
const EVAL_CRITERIA = [
  { key: 'tripleName', title: 'تشابه الاسم الثلاثي' },
  { key: 'phone', title: 'تشابه رقم الهاتف' },
  { key: 'fatherGrandfather', title: 'تشابه اسم الأب والجد' },
]
const editOpen = ref(false)
const editBusy = ref(false)
const editForm = ref({
  customerName: '',
  phone: '',
  province: '',
  cityValue: '',
  toEmployeeId: null,
  address: '',
  notes: '',
})
const editOriginalCity = ref('')
const editTransferBlockedReason = ref('')
const editEmployees = ref([])
const editCityChanged = computed(() =>
  !!editForm.value.cityValue
  && String(editForm.value.cityValue) !== String(editOriginalCity.value || ''))
const deleteOpen = ref(false)
const deleteBusy = ref(false)
const deleteTarget = ref(null)
const intakeCustomers = ref([])
const intakeSelected = ref(null)
const intakeForm = ref({
  fullName: '',
  phone: '',
  province: '',
  address: '',
  notes: '',
})

const tabs = [
  { value: 'all', title: 'الكل' },
  { value: 'unassigned', title: 'غير مسند' },
  { value: 'sent', title: 'الطلبات المرسلة' },
  { value: 'incoming', title: 'طلبات البيع' },
  { value: 'prepared', title: 'جاهز للبيع' },
  { value: 'inspected', title: 'تم الكشف' },
  { value: 'pending', title: 'معلقة' },
  { value: 'rejected', title: 'مرفوض' },
  { value: 'sold', title: 'تم البيع' },
  { value: 'exceptions', title: 'طلبات الاستثناء' },
]

const exceptionStatusTabs = [
  { value: 'Pending', title: 'قيد الانتظار' },
  { value: 'Approved', title: 'موافق عليه' },
  { value: 'Rejected', title: 'مرفوض' },
]

const showingExceptions = computed(() => tab.value === 'exceptions')

function employeeIdOf(row) {
  return Number(row?.targetEmployeeId || row?.TargetEmployeeId || row?.employeeId || row?.EmployeeId || 0)
}

function isUnassigned(row) {
  const s = requestStatus(row)
  if (s === 'Rejected' || s === 'Completed')
    return false
  return s === 'New' || employeeIdOf(row) <= 0
}

function requestStatus(row) {
  if (row == null)
    return ''
  if (typeof row === 'string')
    return row

  return String(pick(row, 'status', 'Status') || '')
}

function isEmployeeSubmitted(row) {
  return pick(row, 'customerSourceType', 'CustomerSourceType') === 'EmployeeSubmitted'
}

function isFollowerSubmitted(row) {
  const source = pick(row, 'customerSourceType', 'CustomerSourceType')
  const role = pick(row, 'createdByUserType', 'CreatedByUserType')
  return source === 'Follower' || role === 'Follower' || role === 'متابع'
}

function isDelegateSubmitted(row) {
  const source = pick(row, 'customerSourceType', 'CustomerSourceType')
  const role = pick(row, 'createdByUserType', 'CreatedByUserType')
  return source === 'Delegate' || role === 'مندوب' || role === 'Delegate'
}

function saleRequestTypeLabel(row) {
  const t = pick(row, 'saleRequestType', 'SaleRequestType')
  if (t === 'Old')
    return 'مبيع قديم'
  if (t === 'New')
    return 'مبيع جديد'

  // Fallback for older rows: existing customer id ⇒ قديم
  const existingId = pick(row, 'existingCustomerId', 'ExistingCustomerId')
  return existingId ? 'مبيع قديم' : 'مبيع جديد'
}

function requestSourceLabel(row) {
  if (isFollowerSubmitted(row))
    return 'المتابع'
  if (isDelegateSubmitted(row))
    return 'المندوب'
  if (isEmployeeSubmitted(row))
    return 'موظف المبيعات'
  return 'مدير المبيعات'
}

function isUnreadSent(row) {
  if (!isEmployeeSubmitted(row))
    return false
  if (pick(row, 'isManagerRead', 'IsManagerRead') === true)
    return false

  return !pick(row, 'managerReadAtUtc', 'ManagerReadAtUtc')
}

function submittedBy(row) {
  if (isFollowerSubmitted(row))
    return pick(row, 'createdByName', 'CreatedByName') || 'متابع'
  if (isDelegateSubmitted(row))
    return pick(row, 'createdByName', 'CreatedByName') || 'مندوب'
  return pick(row, 'createdByName', 'CreatedByName') || 'موظف مبيعات'
}

function matchesTab(row, value = tab.value) {
  const s = requestStatus(row)
  switch (value) {
    case 'exceptions':
      return false
    case 'unassigned':
      return isUnassigned(row)
    case 'sent':
      // الطلبات المرسلة: متابع أو مندوب أو موظف مبيعات، والحالة New فقط (غير مسند).
      return (isEmployeeSubmitted(row) || isFollowerSubmitted(row) || isDelegateSubmitted(row)) && s === 'New'
    case 'incoming':
      return s === 'Assigned' || s === 'Viewed' || s === 'Returned'
    case 'prepared':
      return s === 'PreparedForSale' || s === 'InProgress' || s === 'ConvertedToSale'
    case 'inspected':
      return s === 'Inspected'
    case 'pending':
      return s === 'Pending'
    case 'rejected':
      return s === 'Rejected'
    case 'sold':
      return s === 'Completed'
    default:
      return true
  }
}

/** MVP: show exception CTA for unassigned / assigned-flow / inspected. */
function canRequestException(row) {
  if (!row)
    return false
  const s = requestStatus(row)
  if (s === 'Completed' || s === 'Rejected' || s === 'PreparedForSale' || s === 'InProgress' || s === 'ConvertedToSale')
    return false

  return isUnassigned(row)
    || s === 'Assigned'
    || s === 'Viewed'
    || s === 'Returned'
    || s === 'Inspected'
    || s === 'Pending'
    || s === 'New'
}

function exceptionStatusColor(status) {
  switch (String(status || '')) {
    case 'Approved':
      return 'success'
    case 'Rejected':
      return 'error'
    case 'Cancelled':
      return 'secondary'
    case 'Pending':
    default:
      return 'warning'
  }
}

function exceptionStatusText(status) {
  switch (String(status || '')) {
    case 'Approved':
      return 'تمت الموافقة'
    case 'Rejected':
      return 'مرفوض'
    case 'Cancelled':
      return 'ملغى'
    case 'Pending':
      return 'بانتظار الموافقة'
    default:
      return status || '—'
  }
}

function canAssignException(item) {
  const status = String(item?.status || item?.Status || '')
  const consumed = !!(item?.assignmentConsumed ?? item?.AssignmentConsumed)

  return status === 'Approved' && !consumed
}

function exceptionDecisionNote(item) {
  return (item?.decisionNote || item?.DecisionNote || '').trim()
}

function exceptionAssignedLabel(item) {
  const name = item?.assignedEmployeeName || item?.AssignedEmployeeName
  if (!name)
    return ''
  const at = item?.assignedAtUtc || item?.AssignedAtUtc

  return at ? `${name} — ${formatIraqDate(at)} ${formatIraqTime(at)}` : name
}

function statusColor(status) {
  switch (String(status || '')) {
    case 'Completed':
      return 'success'
    case 'Pending':
      return 'warning'
    case 'PreparedForSale':
    case 'InProgress':
    case 'ConvertedToSale':
      return 'info'
    case 'Inspected':
      return 'primary'
    case 'Rejected':
      return 'error'
    case 'Returned':
      return 'warning'
    default:
      return 'secondary'
  }
}

function isPrepared(row) {
  const status = requestStatus(row)
  return status === 'PreparedForSale' || status === 'InProgress' || status === 'ConvertedToSale'
}

function statusText(row) {
  const s = requestStatus(row)
  return requestStatusLabel[s] || s
}

function lastNote(row) {
  const ret = (row?.returnNote || row?.ReturnNote || '').trim()
  const pending = (row?.pendingNote || row?.PendingNote || '').trim()
  const rejected = (row?.rejectionReason || row?.RejectionReason || '').trim()
  const notes = (row?.notes || row?.Notes || '').trim()
  if (ret)
    return ret
  if (pending)
    return pending
  if (rejected)
    return rejected

  return notes
}

function lastUpdated(row) {
  const times = [
    row?.completedAtUtc || row?.CompletedAtUtc,
    row?.rejectedAtUtc || row?.RejectedAtUtc,
    row?.processingAtUtc || row?.ProcessingAtUtc,
    row?.viewedAtUtc || row?.ViewedAtUtc,
    row?.assignedAtUtc || row?.AssignedAtUtc,
    row?.createdAtUtc || row?.CreatedAtUtc,
  ].filter(Boolean)
  if (!times.length)
    return ''
  times.sort((a, b) => new Date(b) - new Date(a))

  return formatIraqTime(times[0])
}

const visibleRows = computed(() => rows.value.filter(row => matchesTab(row)))

function tabCount(value) {
  if (value === 'exceptions')
    return exceptionRows.value.length

  return rows.value.filter(row => matchesTab(row, value)).length
}

async function load() {
  clearAllEvaluationCaches()
  rows.value = await smGet(withCityQuery('sales-requests', cityValue.value)) || []
  await refreshSalesRequestUnread(cityValue.value)
  await loadEvaluations(rows.value)
  if (selected.value)
    await openDetails(selected.value, false)
  if (showingExceptions.value)
    await loadExceptions()
}

async function loadExceptions() {
  exceptionListBusy.value = true
  try {
    const res = await listExceptionRequests({
      status: exceptionStatus.value || undefined,
      cityValue: cityValue.value || undefined,
    })
    exceptionRows.value = res?.items || res?.Items || []
  }
  catch (err) {
    exceptionRows.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل طلبات الاستثناء'))
  }
  finally {
    exceptionListBusy.value = false
  }
}

function openExceptionRequest(row) {
  exceptionTarget.value = row || detail.value || selected.value
  exceptionReason.value = ''
  exceptionOpen.value = true
}

async function submitExceptionRequest() {
  if (exceptionBusy.value)
    return
  const row = exceptionTarget.value
  const reason = exceptionReason.value.trim()
  if (!row) {
    toast.error('اختر طلب البيع أولاً')

    return
  }
  if (!reason) {
    toast.error('سبب الاستثناء مطلوب')

    return
  }
  const city = requestCity(row) || pick(row, 'cityValue', 'CityValue') || cityValue.value
  if (!city) {
    toast.error('المحافظة مطلوبة')

    return
  }
  exceptionBusy.value = true
  try {
    const customerIdRaw = pick(row, 'existingCustomerId', 'ExistingCustomerId', 'customerId', 'CustomerId')
    const customerId = customerIdRaw != null && customerIdRaw !== '' ? Number(customerIdRaw) : null
    await createExceptionRequest({
      cityValue: city,
      cityName: displayCityName(row, branches.value) || pick(row, 'cityName', 'CityName') || '',
      customerId: Number.isFinite(customerId) && customerId > 0 ? customerId : null,
      customerName: pick(row, 'customerName', 'CustomerName') || '',
      customerPhone: pick(row, 'customerPhone', 'CustomerPhone') || '',
      salesRequestId: requestId(row) || null,
      reason,
      targetApproverType: 'DelegatedManager',
    })
    exceptionOpen.value = false
    exceptionReason.value = ''
    exceptionTarget.value = null
    toast.success('تم إرسال طلب الاستثناء — الموافقة تُعلمك فقط ولا تغني عن الإسناد أو التجهيز')
    if (showingExceptions.value)
      await loadExceptions()
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر إرسال طلب الاستثناء'))
  }
  finally {
    exceptionBusy.value = false
  }
}

function clearAllEvaluationCaches() {
  evaluations.value = {}
  hitsCache.value = {}
  evalPanels.value = {}
  evaluationsError.value = ''
}

function clearEvaluationCachesForRow(row) {
  const key = evaluationKey(row)
  if (!key)
    return
  const nextEvals = { ...evaluations.value }
  delete nextEvals[key]
  evaluations.value = nextEvals
  const nextPanels = { ...evalPanels.value }
  delete nextPanels[key]
  evalPanels.value = nextPanels
  const nextHits = { ...hitsCache.value }
  for (const category of ['tripleName', 'phone', 'fatherGrandfather']) {
    delete nextHits[hitCacheKey(row, category)]
  }
  hitsCache.value = nextHits
}

function evaluationKey(row) {
  return evaluationKeyFromRow(row)
}

function evaluationOf(row) {
  return lookupEvaluation(evaluations.value, row)
}

function ratingColor(label) {
  const t = String(label || '')
  if (t.includes('قانونية'))
    return 'error'
  if (t.includes('مرفوض'))
    return 'warning'
  if (t.includes('ضعيف'))
    return 'secondary'
  if (t.includes('جيد'))
    return 'info'
  if (t.includes('ممتاز'))
    return 'success'

  return 'default'
}

function categoryDto(evalRow, key) {
  return categoryBlock(evalRow, key)
}

function categoryCountText(evalRow, key) {
  const n = categoryResultCount(categoryDto(evalRow, key))
  if (n == null)
    return evaluationsBusy.value ? '...' : '0'

  return String(n)
}

function categoryLabelText(evalRow, key) {
  const cat = categoryDto(evalRow, key)
  const count = categoryResultCount(cat)
  if (count == null)
    return evaluationsBusy.value ? '...' : 'لا توجد نتائج'
  if (count <= 0)
    return 'لا توجد نتائج'

  return categoryWorstLabel(cat) || '—'
}

function requestDomId(row) {
  return `sales-request-${requestCityOf(row) || 'x'}-${requestIdOf(row)}`
}

function hitDomId(hit) {
  const city = hit?.cityValue || hit?.CityValue || 'x'
  const id = hit?.customerId || hit?.CustomerId || '0'

  return `customer-hit-${city}-${id}`
}

function hitCacheKey(row, category) {
  return `${evaluationKey(row)}:${category}`
}

function panelOf(row) {
  return evalPanels.value[evaluationKey(row)] || null
}

function scrollToDomId(domId) {
  nextTick(() => {
    const el = document.getElementById(domId)
    if (el)
      el.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  })
}

async function openEvaluationHits(row, category, title) {
  const key = evaluationKey(row)
  const existing = evalPanels.value[key]
  // Toggle off same criterion
  if (existing?.criterion === category && !existing.expandedHitKey) {
    closeEvalResults(row)

    return
  }

  const cacheKey = hitCacheKey(row, category)
  const panel = {
    criterion: category,
    title,
    hits: hitsCache.value[cacheKey]?.items || [],
    total: hitsCache.value[cacheKey]?.total ?? null,
    loading: !hitsCache.value[cacheKey],
    error: '',
    expandedHitKey: null,
    profile: null,
    profileLoading: false,
  }
  evalPanels.value = { ...evalPanels.value, [key]: panel }
  if (history.state?.salesEvalRequestKey !== key || history.state?.salesEvalLayer !== 'results')
    history.pushState({ salesEvalLayer: 'results', salesEvalRequestKey: key }, '')
  scrollToDomId(requestDomId(row))

  if (hitsCache.value[cacheKey])
    return

  try {
    const res = await smPost('sales-requests/evaluation-hits', {
      requestId: requestIdOf(row),
      sourceCityValue: requestCityOf(row),
      customerName: pick(row, 'customerName', 'CustomerName') || '',
      customerPhone: pick(row, 'customerPhone', 'CustomerPhone') || '',
      category,
      page: 1,
      pageSize: 40,
    })
    const items = res?.items || res?.Items || []
    const total = res?.total ?? res?.Total ?? items.length
    hitsCache.value = { ...hitsCache.value, [cacheKey]: { items, total } }
    evalPanels.value = {
      ...evalPanels.value,
      [key]: { ...panel, hits: items, total, loading: false, error: '' },
    }
  }
  catch (err) {
    evalPanels.value = {
      ...evalPanels.value,
      [key]: {
        ...panel,
        loading: false,
        error: err?.response?.data?.message || 'تعذر تحميل النتائج',
      },
    }
  }
}

function closeEvalProfile(row, { skipHistory } = {}) {
  const key = evaluationKey(row)
  const panel = evalPanels.value[key]
  if (!panel)
    return
  const hitKey = panel.expandedHitKey
  panel.expandedHitKey = null
  panel.profile = null
  panel.profileLoading = false
  evalPanels.value = { ...evalPanels.value, [key]: { ...panel } }
  if (!skipHistory && history.state?.salesEvalLayer === 'profile')
    history.pushState({ salesEvalLayer: 'results', salesEvalRequestKey: key }, '')
  if (hitKey) {
    const [city, id] = String(hitKey).split(':')
    scrollToDomId(hitDomId({ cityValue: city, customerId: id }))
  }
  else {
    scrollToDomId(requestDomId(row))
  }
}

function closeEvalResults(row, { skipHistory } = {}) {
  const key = evaluationKey(row)
  if (!evalPanels.value[key])
    return
  const next = { ...evalPanels.value }
  delete next[key]
  evalPanels.value = next
  if (!skipHistory && history.state?.salesEvalRequestKey === key)
    history.pushState({ salesEvalLayer: null }, '')
  scrollToDomId(requestDomId(row))
}

function onEvalPopState(event) {
  const layer = event.state?.salesEvalLayer
  const reqKey = event.state?.salesEvalRequestKey
  if (!reqKey) {
    // Browser back left eval stack — close any open panels for scroll safety.
    const openKey = Object.keys(evalPanels.value)[0]
    if (!openKey)
      return
    const row = rows.value.find(r => evaluationKey(r) === openKey)
    if (row)
      closeEvalResults(row, { skipHistory: true })

    return
  }
  const row = rows.value.find(r => evaluationKey(r) === reqKey)
  if (!row)
    return
  const panel = evalPanels.value[reqKey]
  if (layer === 'results') {
    if (panel?.expandedHitKey)
      closeEvalProfile(row, { skipHistory: true })
    scrollToDomId(requestDomId(row))

    return
  }
  if (layer === 'profile') {
    scrollToDomId(requestDomId(row))

    return
  }
  if (panel)
    closeEvalResults(row, { skipHistory: true })
}

async function loadEvaluations(list) {
  evaluationsBusy.value = true
  evaluationsError.value = ''
  try {
    const payloadItems = []
    for (const row of list || []) {
      const id = requestIdOf(row)
      if (!id)
        continue
      payloadItems.push({
        requestId: id,
        sourceCityValue: requestCityOf(row),
        customerName: pick(row, 'customerName', 'CustomerName') || '',
        customerPhone: pick(row, 'customerPhone', 'CustomerPhone') || '',
        key: buildEvaluationKey(requestCityOf(row), id),
      })
    }
    if (!payloadItems.length) {
      evaluations.value = {}

      return
    }
    const res = await smPost('sales-requests/evaluate', { items: payloadItems })
    const evalItems = res?.items || res?.Items || []
    evaluations.value = indexEvaluationItems(evalItems, list)
  }
  catch (err) {
    evaluations.value = {}
    evaluationsError.value = err?.response?.data?.message || 'تعذر تحميل تقييم البرنامج والبحث'
  }
  finally {
    evaluationsBusy.value = false
  }
}

async function retryEvaluationHits(row) {
  const panel = panelOf(row)
  if (!panel?.criterion)
    return
  const cacheKey = hitCacheKey(row, panel.criterion)
  const nextCache = { ...hitsCache.value }
  delete nextCache[cacheKey]
  hitsCache.value = nextCache
  await openEvaluationHits(row, panel.criterion, panel.title)
}

async function openInlineHitProfile(row, hit) {
  const key = evaluationKey(row)
  const panel = evalPanels.value[key]
  if (!panel)
    return
  const city = String(hit.cityValue || hit.CityValue || '')
  const customerId = Number(hit.customerId || hit.CustomerId || 0)
  const hitKey = `${city}:${customerId}`
  panel.expandedHitKey = hitKey
  panel.profileLoading = true
  panel.profile = null
  evalPanels.value = { ...evalPanels.value, [key]: { ...panel } }
  history.pushState({ salesEvalLayer: 'profile', salesEvalRequestKey: key, salesEvalHitKey: hitKey }, '')
  scrollToDomId(hitDomId(hit))

  // Prefer hit DTO facts; lazy-load full profile when possible.
  const q = new URLSearchParams()
  if (customerId)
    q.set('customerId', String(customerId))
  if (hit.fullName || hit.FullName)
    q.set('name', hit.fullName || hit.FullName)
  if (hit.phone || hit.Phone)
    q.set('phone', hit.phone || hit.Phone)
  try {
    let profile = null
    if (city) {
      profile = await smGet(customerProfilePath(city, q.toString()))
    }
    evalPanels.value = {
      ...evalPanels.value,
      [key]: {
        ...panel,
        expandedHitKey: hitKey,
        profile: profile || { fromHit: true, hit },
        profileLoading: false,
      },
    }
  }
  catch {
    evalPanels.value = {
      ...evalPanels.value,
      [key]: {
        ...panel,
        expandedHitKey: hitKey,
        profile: { fromHit: true, hit },
        profileLoading: false,
      },
    }
  }
  scrollToDomId(hitDomId(hit))
}

function evalBack(row) {
  const panel = panelOf(row)
  if (!panel)
    return
  if (panel.expandedHitKey) {
    closeEvalProfile(row)

    return
  }
  closeEvalResults(row)
}

function formatSaleDate(value) {
  if (!value)
    return '—'
  try {
    return formatIraqDate(value) || '—'
  }
  catch {
    return '—'
  }
}

function moneyShort(value) {
  const n = Number(value || 0)
  if (!Number.isFinite(n))
    return '—'

  return `${n.toLocaleString('en-US')} د.ع`
}

function isProvinceTransferBlocked(row) {
  const status = requestStatus(row)
  const converted = Number(pick(row, 'convertedToSaleId', 'ConvertedToSaleId') || 0)
  if (status === 'Completed' || converted > 0)
    return 'لا يمكن نقل المحافظة لأن الطلب مكتمل أو مرتبط بمبيع.'
  if (status === 'ConvertedToSale' || status === 'Inspected')
    return 'لا يمكن نقل المحافظة لأن الطلب مرتبط بمسار البيع/الكشف.'

  return ''
}

async function openEdit(row) {
  editForm.value = {
    customerName: pick(row, 'customerName', 'CustomerName') || '',
    phone: pick(row, 'customerPhone', 'CustomerPhone') || '',
    province: pick(row, 'customerProvince', 'CustomerProvince') || displayCityName(row, branches.value),
    cityValue: String(pick(row, 'cityValue', 'CityValue') || ''),
    address: pick(row, 'customerAddress', 'CustomerAddress') || '',
    notes: pick(row, 'notes', 'Notes') || '',
  }
  editOriginalCity.value = String(pick(row, 'cityValue', 'CityValue') || '')
  editTransferBlockedReason.value = isProvinceTransferBlocked(row)
  editEmployees.value = []
  selected.value = row
  editOpen.value = true
  if (editForm.value.cityValue && editForm.value.cityValue !== editOriginalCity.value)
    await loadEditEmployees(editForm.value.cityValue)
}

async function loadEditEmployees(city) {
  if (!city || isDemo()) {
    editEmployees.value = []

    return
  }
  try {
    editEmployees.value = await smGetEmployees(city) || []
  }
  catch {
    editEmployees.value = []
  }
}

async function onEditCityChanged(city) {
  editForm.value.cityValue = city || ''
  const match = branches.value.find(b => String(b.value) === String(city))
  if (match)
    editForm.value.province = match.name || editForm.value.province
  editForm.value.toEmployeeId = null
  if (city && String(city) !== String(editOriginalCity.value))
    await loadEditEmployees(city)
  else
    editEmployees.value = []
}

async function saveEdit() {
  const row = selected.value
  if (!row)
    return
  editBusy.value = true
  try {
    const city = requestCity(row)
    const id = requestId(row)
    const phone = editForm.value.phone ? normalizeIraqPhone(editForm.value.phone) : ''
    const newCity = String(editForm.value.cityValue || '')
    const cityChanged = newCity && newCity !== String(editOriginalCity.value || '')

    if (cityChanged) {
      if (editTransferBlockedReason.value) {
        toast.error(editTransferBlockedReason.value)

        return
      }
      const path = isDemo() || !city
        ? `sales-requests/${id}/transfer-province`
        : `sales-requests/${encodeURIComponent(city)}/${id}/transfer-province`
      const match = branches.value.find(b => String(b.value) === newCity)
      await smPost(path, {
        toCityValue: newCity,
        toCityName: match?.name || editForm.value.province,
        toEmployeeId: editForm.value.toEmployeeId || null,
        customerName: editForm.value.customerName,
        customerPhone: phone,
        customerAddress: editForm.value.address,
        notes: editForm.value.notes,
      })
      toast.success('تم نقل الطلب إلى المحافظة الجديدة')
    }
    else {
      const path = isDemo() || !city
        ? `sales-requests/${id}`
        : `sales-requests/${encodeURIComponent(city)}/${id}`
      await smPut(path, {
        customerName: editForm.value.customerName,
        phone,
        province: editForm.value.province,
        address: editForm.value.address,
        notes: editForm.value.notes,
      })
      toast.success('تم تعديل الطلب')
    }
    clearEvaluationCachesForRow(row)
    editOpen.value = false
    await load()
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر التعديل')
  }
  finally {
    editBusy.value = false
  }
}

function confirmDelete(row) {
  deleteTarget.value = row
  deleteOpen.value = true
}

async function doDelete() {
  const row = deleteTarget.value
  if (!row)
    return
  deleteBusy.value = true
  try {
    const city = requestCity(row)
    const id = requestId(row)
    const path = isDemo() || !city
      ? `sales-requests/${id}`
      : `sales-requests/${encodeURIComponent(city)}/${id}`
    await smDelete(path)
    clearEvaluationCachesForRow(row)
    deleteOpen.value = false
    deleteTarget.value = null
    if (detail.value?.id === id)
      detail.value = null
    toast.success('تم حذف الطلب')
    await load()
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر الحذف')
  }
  finally {
    deleteBusy.value = false
  }
}

async function openDetails(row, resetAssign = true) {
  selected.value = row
  const city = row.cityValue
  try {
    detail.value = await smGet(`sales-requests/${encodeURIComponent(city)}/${row.id}`)
  }
  catch {
    detail.value = row
  }
  const updated = detail.value
  if (updated?.id) {
    rows.value = rows.value.map(r => (
      r.id === updated.id && (r.cityValue || '') === (updated.cityValue || city || '')
        ? { ...r, ...updated }
        : r
    ))
  }
  if (resetAssign) {
    assignCityValue.value = detail.value?.cityValue || city || ''
    assignEmployeeId.value = null
    returnNote.value = ''
    returnOpen.value = false
    managerNote.value = ''
    resetIntake(detail.value)
  }
  await loadEmployees()
  await refreshSalesRequestUnread(cityValue.value)
  await loadProfile(row, updated)
}

function resetIntake(d) {
  intakeQuery.value = pick(d, 'customerName', 'CustomerName') || ''
  intakeCustomers.value = []
  intakeSelected.value = null
  intakeForm.value = {
    fullName: pick(d, 'customerName', 'CustomerName') || '',
    phone: pick(d, 'customerPhone', 'CustomerPhone') || '',
    province: displayCityName(d, branches.value),
    address: pick(d, 'customerAddress', 'CustomerAddress') || '',
    notes: pick(d, 'notes', 'Notes') || '',
  }
}

function requestCity(row = detail.value || selected.value) {
  return pick(row, 'cityValue', 'CityValue') || ''
}

function requestId(row = detail.value || selected.value) {
  return pick(row, 'id', 'Id')
}

function requestActionPath(row, suffix) {
  const id = requestId(row)
  const city = requestCity(row)
  if (isDemo())
    return `sales-requests/${id}/${suffix}`

  return `sales-requests/${encodeURIComponent(city)}/${id}/${suffix}`
}

async function searchIntakeCustomers() {
  const q = intakeQuery.value.trim()
  if (q.length < 2) {
    toast.error('اكتب حرفين على الأقل للبحث')

    return
  }
  const city = requestCity()
  if (!city) {
    toast.error('لا يمكن البحث بدون محافظة الطلب')

    return
  }
  try {
    intakeCustomers.value = await smGet(`customers/search?q=${encodeURIComponent(q)}&cityValue=${encodeURIComponent(city)}`) || []
  }
  catch (err) {
    intakeCustomers.value = []
    toast.error(err?.response?.data?.message || 'تعذر البحث عن الزبون')
  }
}

function selectIntakeCustomer(c) {
  const requestBranch = String(requestCity() || '')
  const customerBranch = String(c.cityValue || c.CityValue || c.sourceCityValue || c.SourceCityValue || requestBranch)
  if (customerBranch && requestBranch && customerBranch !== requestBranch) {
    toast.error('لا يمكن ربط زبون من محافظة أخرى')

    return
  }
  intakeSelected.value = c
  intakeForm.value = {
    fullName: c.fullName || c.customerName || intakeForm.value.fullName,
    phone: c.phone || c.Phone || intakeForm.value.phone,
    province: displayCityName(c, branches.value) || intakeForm.value.province,
    address: c.address || c.Address || intakeForm.value.address,
    notes: intakeForm.value.notes,
  }
}

function customerKey(c) {
  return c.branchKey || `${c.cityValue || c.CityValue || ''}:${c.customerId || c.CustomerId}`
}

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function money(value) {
  const n = Number(value || 0)
  if (!Number.isFinite(n))
    return '0 د.ع'

  return `${n.toLocaleString('en-US')} د.ع`
}

function areaText(shop) {
  const area = Number(pick(shop, 'shopArea', 'ShopArea') ?? 0)

  return `${area} م²`
}

function saleDocs(sale) {
  return saleDisplayDocuments(sale)
}

function currentCity(row, d) {
  return cityValue.value
    || pick(d, 'cityValue', 'CityValue')
    || pick(row, 'cityValue', 'CityValue')
    || pick(profile.value, 'cityValue', 'CityValue')
    || pick(saleDetail.value, 'cityValue', 'CityValue')
    || ''
}

async function openSaleDocument(sale, doc) {
  const city = currentCity(selected.value, detail.value)
  const saleId = pick(sale, 'saleId', 'SaleId')
  const documentId = pick(doc, 'documentId', 'DocumentId')
  if ((!city && !isDemo()) || !saleId || !documentId) {
    toast.error('المستند غير متوفر')

    return
  }
  try {
    const blob = await smGetBlob(managerSalePath(city, saleId, `/documents/${documentId}/download`))
    const buffer = await blob.arrayBuffer()
    const header = new TextDecoder('latin1').decode(buffer.slice(0, 5))
    if (header !== '%PDF-') {
      toast.error('تعذر فتح المستند')

      return
    }
    const pdf = new Blob([buffer], { type: 'application/pdf' })
    const url = URL.createObjectURL(pdf)
    window.open(url, '_blank')
  }
  catch {
    toast.error('تعذر فتح المستند')
  }
}

const latestShop = computed(() =>
  profile.value?.latestShop
  || profile.value?.LatestShop
  || pick(saleDetail.value, 'shop', 'Shop')
  || null)
const profileSales = computed(() => profile.value?.sales || profile.value?.Sales || [])
const profileNotes = computed(() => profile.value?.notes || profile.value?.Notes || [])
const profileEvaluations = computed(() => profile.value?.evaluations || profile.value?.Evaluations || [])
const profileRequests = computed(() => profile.value?.salesRequests || profile.value?.SalesRequests || [])
const profileHistory = computed(() => profile.value?.history || profile.value?.History || [])

function customerProfilePath(city, params) {
  return isDemo()
    ? `customers/profile?${params}`
    : `customers/${encodeURIComponent(city)}/profile?${params}`
}

function customerNotesPath(city) {
  return isDemo()
    ? 'customers/notes'
    : `customers/${encodeURIComponent(city)}/notes`
}

function shopImagePath(city, saleId) {
  return isDemo()
    ? `sales/${saleId}/shop-image`
    : `sales/${encodeURIComponent(city)}/${saleId}/shop-image`
}

function saleDetailPath(city, saleId) {
  return isDemo()
    ? `sales/${saleId}`
    : `sales/${encodeURIComponent(city)}/${saleId}`
}

function currentSaleId(row, d) {
  return pick(d, 'convertedToSaleId', 'ConvertedToSaleId')
    || pick(row, 'convertedToSaleId', 'ConvertedToSaleId')
    || pick(d, 'saleId', 'SaleId')
    || pick(row, 'saleId', 'SaleId')
}

async function loadShopImage(city, saleId) {
  if (shopImageUrl.value) {
    URL.revokeObjectURL(shopImageUrl.value)
    shopImageUrl.value = ''
  }
  if (!saleId)
    return
  try {
    const blob = await smGetBlob(shopImagePath(city, saleId))
    if (blob && blob.size)
      shopImageUrl.value = URL.createObjectURL(blob)
  }
  catch {
    shopImageUrl.value = ''
  }
}

async function openCustomerProfile(row, d = null) {
  const source = d || detail.value || row
  const city = requestCity(source) || requestCity(row)
  if (!city) {
    toast.error('حدد المحافظة أولاً')

    return
  }
  router.push({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: city,
      customerId: pick(source, 'existingCustomerId', 'ExistingCustomerId', 'customerId', 'CustomerId') || '',
      name: pick(source, 'customerName', 'CustomerName', 'fullName') || intakeForm.value.fullName || '',
      phone: pick(source, 'customerPhone', 'CustomerPhone', 'phone') || intakeForm.value.phone || '',
    },
  })
}

function openIntakeCustomerProfile(c) {
  const city = String(c.cityValue || c.CityValue || c.sourceCityValue || c.SourceCityValue || requestCity() || '')
  if (!city) {
    toast.error('حدد المحافظة أولاً')

    return
  }
  router.push({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: city,
      customerId: pick(c, 'customerId', 'CustomerId') || '',
      name: pick(c, 'fullName', 'customerName', 'CustomerName') || '',
      phone: pick(c, 'phone', 'Phone') || '',
    },
  })
}

async function loadProfile(row, d) {
  profile.value = null
  saleDetail.value = null
  await loadShopImage('', null)
  const city = d?.cityValue || row?.cityValue
  if (!city)
    return
  const linkedSaleId = currentSaleId(row, d)
  if (linkedSaleId) {
    try {
      saleDetail.value = await smGet(saleDetailPath(city, linkedSaleId))
      await loadShopImage(city, linkedSaleId)
    }
    catch {
      saleDetail.value = null
    }
  }
  const params = new URLSearchParams()
  const customerId = pick(d, 'existingCustomerId', 'ExistingCustomerId') || pick(row, 'existingCustomerId', 'ExistingCustomerId')
  const name = pick(d, 'customerName', 'CustomerName') || pick(row, 'customerName', 'CustomerName')
  const phone = pick(d, 'customerPhone', 'CustomerPhone') || pick(row, 'customerPhone', 'CustomerPhone')
  if (customerId)
    params.set('customerId', String(customerId))
  if (name)
    params.set('name', name)
  if (phone)
    params.set('phone', phone)
  if (![...params.keys()].length)
    return
  try {
    profile.value = await smGet(customerProfilePath(city, params))
    const shop = profile.value?.latestShop || profile.value?.LatestShop
    const saleId = currentSaleId(row, d) || pick(shop, 'saleId', 'SaleId')
    if (saleId) {
      await loadShopImage(city, saleId)
      try {
        saleDetail.value = await smGet(saleDetailPath(city, saleId))
      }
      catch {
        saleDetail.value = null
      }
    }
  }
  catch {
    profile.value = null
  }
}

const profileName = computed(() =>
  pick(profile.value, 'customerName', 'CustomerName')
  || pick(saleDetail.value, 'fullName', 'FullName')
  || pick(detail.value, 'customerName', 'CustomerName')
  || pick(selected.value, 'customerName', 'CustomerName')
  || '')
const profilePhone = computed(() =>
  pick(profile.value, 'phone', 'Phone')
  || pick(saleDetail.value, 'phone', 'Phone')
  || pick(detail.value, 'customerPhone', 'CustomerPhone')
  || pick(selected.value, 'customerPhone', 'CustomerPhone')
  || '')
const profileProvince = computed(() =>
  displayCityName({
    ...profile.value,
    ...saleDetail.value,
    ...detail.value,
    ...selected.value,
    cityName: pick(profile.value, 'cityName', 'CityName')
      || pick(saleDetail.value, 'cityName', 'CityName')
      || pick(detail.value, 'cityName', 'CityName')
      || pick(selected.value, 'cityName', 'CityName'),
    customerProvince: pick(detail.value, 'customerProvince', 'CustomerProvince')
      || pick(selected.value, 'customerProvince', 'CustomerProvince'),
    province: pick(saleDetail.value, 'province', 'Province')
      || pick(profile.value, 'province', 'Province'),
    cityValue: pick(profile.value, 'cityValue', 'CityValue')
      || pick(detail.value, 'cityValue', 'CityValue')
      || pick(selected.value, 'cityValue', 'CityValue'),
  }, branches.value)
  || '')
const profileAddress = computed(() =>
  pick(saleDetail.value, 'address', 'Address')
  || pick(detail.value, 'customerAddress', 'CustomerAddress')
  || '')
const profileNationalId = computed(() =>
  pick(saleDetail.value, 'nationalCardNumber', 'NationalCardNumber')
  || '')

async function addManagerNote() {
  const note = managerNote.value.trim()
  if (!note) {
    toast.error('الملاحظة مطلوبة')

    return
  }
  const d = detail.value || selected.value
  const city = d?.cityValue
  if (!city)
    return
  busy.value = true
  try {
    await smPost(customerNotesPath(city), {
      customerId: pick(d, 'existingCustomerId', 'ExistingCustomerId'),
      customerName: pick(d, 'customerName', 'CustomerName'),
      customerPhone: pick(d, 'customerPhone', 'CustomerPhone'),
      note,
    })
    managerNote.value = ''
    toast.success('تمت إضافة الملاحظة')
    await loadProfile(selected.value, d)
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر حفظ الملاحظة')
  }
  finally {
    busy.value = false
  }
}

async function loadEmployees() {
  const city = requestCity() || assignCityValue.value || detail.value?.cityValue
  assignCityValue.value = city || ''
  if (!city) {
    employees.value = []

    return
  }
  try {
    employees.value = (await smGetEmployees(city)).map(row => ({
      ...row,
      employeeId: Number(row.employeeId || row.EmployeeId || 0),
    }))
  }
  catch {
    employees.value = []
  }
}

const historyRows = computed(() => {
  const d = detail.value
  if (!d)
    return []
  if (Array.isArray(d.history) && d.history.length)
    return d.history
  if (Array.isArray(d.History) && d.History.length)
    return d.History
  if (Array.isArray(d.timeline) && d.timeline.length) {
    return d.timeline.map(item => ({
      createdAtUtc: item.atUtc || item.AtUtc,
      event: item.event || item.Event,
      actorName: '',
      note: item.detail || item.Detail,
    }))
  }

  return []
})

function historyTime(row) {
  return formatIraqTime(row.createdAtUtc || row.CreatedAtUtc)
}

function historyEvent(row) {
  const ev = row.event || row.Event || ''

  return requestHistoryLabel[ev] || ev
}

function historyActor(row) {
  return row.actorName || row.ActorName || row.actorType || row.ActorType || ''
}

function historyNote(row) {
  return row.note || row.Note || row.detail || row.Detail || ''
}

async function assign() {
  const d = detail.value
  if (!d || d.status !== 'New')
    return
  const city = requestCity(d)
  const id = requestId(d)
  const employeeId = Number(assignEmployeeId.value || 0)
  if (!city || !employeeId) {
    toast.error('اختر موظف المبيعات')

    return
  }
  const name = (intakeForm.value.fullName || '').trim()
  if (!name) {
    toast.error('اسم الزبون مطلوب')

    return
  }
  intakeForm.value.phone = normalizeIraqPhone(intakeForm.value.phone)
  if (!isIraqMobile(intakeForm.value.phone)) {
    toast.error(IRAQ_MOBILE_ERROR)

    return
  }
  const employee = employees.value.find(e => Number(e.employeeId) === employeeId)

  if (exceptionAssignId.value) {
    busy.value = true
    try {
      await assignExceptionRequest(exceptionAssignId.value, {
        employeeId,
        employeeName: employee?.employeeName,
      })
      toast.success('تم إسناد الطلب من الاستثناء')
      exceptionAssignId.value = null
      await loadExceptions()
      await load()
      detail.value = null
    }
    catch (err) {
      toast.error(smErrorMessage(err, 'تعذر إسناد الاستثناء'))
    }
    finally {
      busy.value = false
    }

    return
  }

  const selectedCustomer = intakeSelected.value
  const sourceCity = selectedCustomer
    ? String(selectedCustomer.cityValue || selectedCustomer.CityValue || selectedCustomer.sourceCityValue || selectedCustomer.SourceCityValue || city)
    : ''
  const sameBranch = !!selectedCustomer && String(sourceCity) === String(city)
  const submitted = isEmployeeSubmitted(d)
  const existingId = submitted
    ? 0
    : (sameBranch ? Number(selectedCustomer.customerId || selectedCustomer.CustomerId || 0) : 0)
  const path = requestActionPath(d, 'assign')
  const payload = {
    employeeId,
    employeeName: employee?.employeeName,
    cityValue: city,
    cityName: employee?.cityName || employee?.branchName || d.cityName,
    existingCustomerId: existingId > 0 ? existingId : null,
    customerSourceCityValue: submitted ? null : (sameBranch ? sourceCity : null),
    customerName: name,
    customerPhone: intakeForm.value.phone,
    customerProvince: intakeForm.value.province,
    customerAddress: intakeForm.value.address,
    notes: intakeForm.value.notes,
  }
  busy.value = true
  try {
    detail.value = await smPost(path, payload)
    toast.success('تم إرسال الطلب للموظف')
    await load()
  }
  catch (err) {
    const status = err?.response?.status
    const backend = err?.response?.data
    const message = backend?.message || backend?.Message || err?.message || 'تعذر إسناد الطلب'
    console.error('assign request failed', {
      url: `${salesManagerBase()}${path}`,
      requestId: id,
      cityValue: city,
      employeeId,
      payload,
      status,
      body: backend,
      message,
    })
    toast.error(status ? `${message} (${status})` : message)
  }
  finally {
    busy.value = false
  }
}

async function openExceptionAssign(item) {
  const salesRequestId = Number(item?.salesRequestId || item?.SalesRequestId || 0)
  const exceptionId = item?.id || item?.Id
  if (!salesRequestId || !exceptionId) {
    toast.error('طلب المبيع المرتبط بالاستثناء غير موجود')

    return
  }
  exceptionAssignId.value = exceptionId
  const city = item.cityValue || item.CityValue || cityValue.value || ''
  let row = rows.value.find(r => Number(requestId(r)) === salesRequestId)
  if (!row) {
    try {
      row = await smGet(withCityQuery(`sales-requests/${salesRequestId}`, city))
    }
    catch {
      row = {
        id: salesRequestId,
        cityValue: city,
        cityName: item.cityName || item.CityName,
        customerName: item.customerName || item.CustomerName,
        customerPhone: item.customerPhone || item.CustomerPhone,
        status: 'New',
        targetEmployeeId: 0,
      }
    }
  }
  await openDetails(row, true)
}

async function sendReturn() {
  const note = returnNote.value.trim()
  if (!note) {
    toast.error('ملاحظة الإعادة مطلوبة')

    return
  }
  const d = detail.value
  const requestId = d.id ?? d.Id
  const returnPath = isDemo()
    ? `sales-requests/${requestId}/return`
    : `sales-requests/${encodeURIComponent(d.cityValue)}/${requestId}/return`
  busy.value = true
  try {
    detail.value = await smPost(returnPath, { note })
    returnOpen.value = false
    returnNote.value = ''
    toast.success('تمت إعادة الطلب لنفس الموظف')
    await load()
  }
  catch (err) {
    const status = err?.response?.status
    const backend = err?.response?.data
    const message = backend?.message || err?.message || 'تعذر إعادة الطلب'
    console.error('return request failed', {
      url: `${salesManagerBase()}${returnPath}`,
      status,
      body: backend,
      message,
    })
    toast.error(status ? `${message} (${status})` : message)
  }
  finally {
    busy.value = false
  }
}

function applyRequestRow(updated) {
  if (!updated)
    return
  const id = pick(updated, 'id', 'Id')
  const city = String(pick(updated, 'cityValue', 'CityValue') || '')
  const status = requestStatus(updated)
  const merged = { ...updated, status }
  rows.value = rows.value.map(r => (
    pick(r, 'id', 'Id') === id && String(pick(r, 'cityValue', 'CityValue') || '') === (city || String(pick(r, 'cityValue', 'CityValue') || ''))
      ? { ...r, ...merged }
      : r
  ))
  if (pick(detail.value, 'id', 'Id') === id)
    detail.value = { ...detail.value, ...merged }
  if (pick(selected.value, 'id', 'Id') === id)
    selected.value = { ...selected.value, ...merged }
}

async function rejectSubmitted() {
  const reason = rejectReason.value.trim()
  if (!reason) {
    toast.error('سبب الرفض مطلوب')

    return
  }
  const d = detail.value
  if (!d)
    return
  busy.value = true
  try {
    const updated = await smPost(requestActionPath(d, 'reject'), { reason })
    applyRequestRow(updated)
    rejectOpen.value = false
    rejectReason.value = ''
    toast.success('تم رفض الطلب')
    await load()
  }
  catch (err) {
    toast.error(err?.response?.data?.message || err?.message || 'تعذر رفض الطلب')
  }
  finally {
    busy.value = false
  }
}

async function markAllRead() {
  busy.value = true
  try {
    await markAllSalesRequestsRead(cityValue.value)
    toast.success('تم جعل الطلبات المرسلة مقروءة')
    await load()
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر تحديث حالة القراءة')
  }
  finally {
    busy.value = false
  }
}

function canManagerReject(row) {
  const s = requestStatus(row)
  if (s === 'Completed' || s === 'Rejected')
    return false
  if (s === 'Inspected')
    return true

  return isEmployeeSubmitted(row)
}

function canManagerInspectedActions(row) {
  return requestStatus(row) === 'Inspected'
}

async function prepareInspected() {
  const d = detail.value
  if (!d)
    return
  busy.value = true
  try {
    const updated = await smPost(requestActionPath(d, 'prepare'), {})
    applyRequestRow(updated)
    toast.success('تم تجهيز الطلب للبيع')
    await load()
  }
  catch (err) {
    toast.error(err?.response?.data?.message || err?.message || 'تعذر تجهيز الطلب')
  }
  finally {
    busy.value = false
  }
}

async function pendInspected() {
  const note = pendNote.value.trim()
  if (!note) {
    toast.error('ملاحظة التعليق مطلوبة')

    return
  }
  const d = detail.value
  if (!d)
    return
  busy.value = true
  try {
    const updated = await smPost(requestActionPath(d, 'pending'), { note })
    applyRequestRow(updated)
    pendOpen.value = false
    pendNote.value = ''
    toast.success('تم تعليق الطلب')
    await load()
  }
  catch (err) {
    toast.error(err?.response?.data?.message || err?.message || 'تعذر تعليق الطلب')
  }
  finally {
    busy.value = false
  }
}

function foldAr(value) {
  return String(value || '')
    .normalize('NFC')
    .replace(/[\u0640\u200B\u200C\u200D\uFEFF]/g, '')
    .replace(/[\u00A0\u202F\u2007]/g, ' ')
    .trim()
    .replace(/[أإآٱ]/g, 'ا')
    .replace(/ة/g, 'ه')
    .replace(/ى/g, 'ي')
    .replace(/\s+/g, ' ')
}

function resolveCity(provinceText) {
  // Each Excel row must resolve its own province — never fall back to the UI filter
  // (that previously stamped the selected/Najaf branch onto every blank/unknown row).
  const text = foldAr(provinceText)
  if (!text)
    return null
  const match = branches.value.find(p => foldAr(p.name) === text || foldAr(p.value) === text)
  if (match)
    return { cityValue: String(match.value), cityName: match.name }

  return null
}

function downloadTemplate() {
  const sheet = XLSX.utils.aoa_to_sheet([
    ['اسم الزبون', 'الهاتف', 'المحافظة', 'العنوان', 'نوع المبيع'],
    ['أحمد علي', '07700000000', 'النجف', 'الكوفة', 'ثلاجة'],
  ])
  const book = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(book, sheet, 'طلبات البيع')
  XLSX.writeFile(book, 'نموذج_طلبات_البيع.xlsx')
}

function openExcelPicker() {
  excelInput.value?.click()
}

function onExcelPicked(event) {
  const file = event.target.files?.[0]
  event.target.value = ''
  if (!file)
    return
  const reader = new FileReader()
  reader.onload = e => {
    try {
      importPreview.value = parseSalesRequestExcel(e.target.result, {
        XLSX,
        resolveCity,
      })
      importOpen.value = true
    }
    catch (err) {
      toast.error(err?.message || 'تعذر قراءة ملف Excel')
    }
  }
  reader.readAsArrayBuffer(file)
}

async function confirmImport() {
  const preview = importPreview.value
  if (!preview?.valid?.length) {
    toast.error('لا توجد صفوف صحيحة للحفظ')

    return
  }
  importBusy.value = true
  try {
    const groups = new Map()
    for (const row of preview.valid) {
      const key = row.cityValue
      if (!groups.has(key))
        groups.set(key, [])
      groups.get(key).push(row)
    }
    let saved = 0
    const failed = []
    for (const [city, rows] of groups.entries()) {
      const cityName = rows[0]?.cityName || ''
      const result = await smPost('sales-requests/import', {
        cityValue: city,
        cityName,
        rows: rows.map(r => ({
          rowNumber: r.rowNumber,
          customerName: r.customerName,
          phone: r.phone,
          province: r.province,
          address: r.address,
          saleType: r.saleType,
          cityValue: r.cityValue || city,
          cityName: r.cityName || cityName,
        })),
      })
      saved += Number(result?.saved || result?.Saved || 0)
      const errs = result?.errors || result?.Errors || []
      for (const err of errs) {
        failed.push({
          rowNumber: err.rowNumber || err.RowNumber,
          message: err.message || err.Message,
        })
      }
    }
    toast.success(`تم حفظ ${saved} طلب غير مسند`)
    if (failed.length)
      toast.error(`فشل ${failed.length} صف أثناء الحفظ`)
    importOpen.value = false
    importPreview.value = null
    tab.value = 'unassigned'
    await load()
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر استيراد الطلبات')
  }
  finally {
    importBusy.value = false
  }
}

onMounted(() => {
  window.addEventListener('popstate', onEvalPopState)
  load()
})
onUnmounted(() => {
  window.removeEventListener('popstate', onEvalPopState)
  if (shopImageUrl.value)
    URL.revokeObjectURL(shopImageUrl.value)
})

watch(tab, value => {
  if (value === 'exceptions')
    loadExceptions()
})

watch(exceptionStatus, () => {
  if (showingExceptions.value)
    loadExceptions()
})
</script>

<template>
  <div>
    <div class="d-flex justify-space-between mb-4 flex-wrap gap-2">
      <h4>طلبات البيع</h4>
      <div class="d-flex gap-2 flex-wrap">
        <VBtn
          variant="outlined"
          @click="downloadTemplate"
        >
          تحميل نموذج Excel
        </VBtn>
        <VBtn
          color="secondary"
          @click="openExcelPicker"
        >
          استيراد Excel
        </VBtn>
        <VBtn
          variant="tonal"
          :loading="busy"
          @click="markAllRead"
        >
          جعل الجميع مقروءة
        </VBtn>
        <VBtn
          color="primary"
          :to="{ name: 'sales-manager-request-create' }"
        >
          + طلب مبيع جديد
        </VBtn>
      </div>
    </div>
    <input
      ref="excelInput"
      type="file"
      accept=".xlsx,.xls"
      hidden
      @change="onExcelPicked"
    >
    <VRow class="mb-3">
      <VCol
        cols="12"
        md="4"
      >
        <SalesBranchFilter
          v-model="cityValue"
          @change="load"
        />
      </VCol>
    </VRow>
    <VChipGroup
      v-model="tab"
      column
    >
      <VChip
        v-for="item in tabs"
        :key="item.value"
        :value="item.value"
        filter
      >
        {{ item.title }}
        ({{ tabCount(item.value) }})
        <VBadge
          v-if="item.value === 'sent' && salesRequestUnreadCount > 0"
          :content="salesRequestUnreadCount"
          color="error"
          inline
          class="ms-1"
        />
      </VChip>
    </VChipGroup>

    <template v-if="showingExceptions">
      <VAlert
        class="mt-4"
        type="info"
        variant="tonal"
        density="compact"
      >
        طلبات الاستثناء تُعلم مدير المبيعات عند الموافقة فقط — ما زال إسناد الطلب أو تجهيزه للبيع مطلوبًا كالمعتاد.
      </VAlert>
      <VChipGroup
        v-model="exceptionStatus"
        class="mt-3"
        column
      >
        <VChip
          v-for="item in exceptionStatusTabs"
          :key="item.value"
          :value="item.value"
          filter
        >
          {{ item.title }}
        </VChip>
      </VChipGroup>
      <div
        v-if="exceptionListBusy"
        class="text-medium-emphasis mt-4"
      >
        جاري تحميل طلبات الاستثناء...
      </div>
      <div
        v-else-if="!exceptionRows.length"
        class="text-medium-emphasis mt-4"
      >
        لا توجد طلبات استثناء في هذا الفلتر.
      </div>
      <VRow
        v-else
        class="mt-4"
      >
        <VCol
          v-for="item in exceptionRows"
          :key="item.id || item.Id"
          cols="12"
          md="6"
        >
          <VCard class="sales-request-card">
            <VCardText>
              <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-3">
                <div class="d-flex flex-wrap align-center gap-2">
                  <span class="text-h6 mb-0">
                    استثناء
                    <template v-if="item.salesRequestId || item.SalesRequestId">
                      — طلب #{{ item.salesRequestId || item.SalesRequestId }}
                    </template>
                  </span>
                  <VChip
                    size="small"
                    color="primary"
                    variant="tonal"
                  >
                    {{ displayCityName(item, branches) || item.cityName || item.CityName || '—' }}
                  </VChip>
                  <VChip
                    size="small"
                    :color="exceptionStatusColor(item.status || item.Status)"
                  >
                    {{ exceptionStatusText(item.status || item.Status) }}
                  </VChip>
                </div>
              </div>
              <VRow dense>
                <VCol
                  cols="12"
                  sm="6"
                >
                  <div class="text-medium-emphasis text-caption">
                    اسم الزبون
                  </div>
                  <strong>{{ item.customerName || item.CustomerName || '—' }}</strong>
                </VCol>
                <VCol
                  cols="12"
                  sm="6"
                >
                  <div class="text-medium-emphasis text-caption">
                    الهاتف
                  </div>
                  {{ item.customerPhone || item.CustomerPhone || '—' }}
                </VCol>
                <VCol
                  cols="12"
                  sm="6"
                >
                  <div class="text-medium-emphasis text-caption">
                    سبب الاستثناء
                  </div>
                  {{ item.reason || item.Reason || '—' }}
                </VCol>
                <VCol
                  cols="12"
                  sm="6"
                >
                  <div class="text-medium-emphasis text-caption">
                    تاريخ الطلب
                  </div>
                  {{ formatIraqDate(item.requestedAtUtc || item.RequestedAtUtc) }}
                  {{ formatIraqTime(item.requestedAtUtc || item.RequestedAtUtc) }}
                </VCol>
                <VCol
                  v-if="item.decidedAtUtc || item.DecidedAtUtc"
                  cols="12"
                  sm="6"
                >
                  <div class="text-medium-emphasis text-caption">
                    تاريخ القرار
                  </div>
                  {{ formatIraqDate(item.decidedAtUtc || item.DecidedAtUtc) }}
                  {{ formatIraqTime(item.decidedAtUtc || item.DecidedAtUtc) }}
                </VCol>
                <VCol
                  v-if="exceptionDecisionNote(item)"
                  cols="12"
                >
                  <div class="text-medium-emphasis text-caption">
                    ملاحظة القرار
                  </div>
                  {{ exceptionDecisionNote(item) }}
                </VCol>
                <VCol
                  v-if="exceptionAssignedLabel(item)"
                  cols="12"
                >
                  <div class="text-medium-emphasis text-caption">
                    الموظف المسند
                  </div>
                  {{ exceptionAssignedLabel(item) }}
                </VCol>
              </VRow>
              <VAlert
                v-if="String(item.status || item.Status) === 'Approved' && !(item.assignmentConsumed || item.AssignmentConsumed)"
                class="mt-3"
                type="success"
                variant="tonal"
                density="compact"
              >
                تمت الموافقة على الاستثناء — يمكنك تحديد الموظف الآن
              </VAlert>
              <VAlert
                v-else-if="String(item.status || item.Status) === 'Approved'"
                class="mt-3"
                type="success"
                variant="tonal"
                density="compact"
              >
                تمت الموافقة وتم إسناد الطلب
              </VAlert>
              <VAlert
                v-else-if="String(item.status || item.Status) === 'Rejected'"
                class="mt-3"
                type="error"
                variant="tonal"
                density="compact"
              >
                تم رفض طلب الاستثناء
              </VAlert>
              <div
                v-if="canAssignException(item)"
                class="mt-3"
              >
                <VBtn
                  color="primary"
                  @click="openExceptionAssign(item)"
                >
                  تحديد الموظف
                </VBtn>
              </div>
            </VCardText>
          </VCard>
        </VCol>
      </VRow>
    </template>

    <VRow
      v-else
      class="mt-4"
    >
      <VCol
        v-for="row in visibleRows"
        :key="branchRowKey(row, 'id')"
        cols="12"
        md="6"
      >
        <VCard
          :id="requestDomId(row)"
          :class="{
            'border-primary': selected?.id === row.id && selected?.cityValue === row.cityValue,
            'unread-request': isUnreadSent(row),
            'sales-request-card': true,
          }"
          @click="openDetails(row)"
        >
          <VCardText>
            <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-3">
              <div class="d-flex flex-wrap align-center gap-2">
                <span class="text-h6 mb-0">طلب #{{ row.id }}</span>
                <VChip size="small" color="primary" variant="tonal">{{ displayCityName(row, branches) }}</VChip>
                <VChip size="small" :color="statusColor(requestStatus(row))">{{ statusText(row) }}</VChip>
                <VChip
                  size="small"
                  :color="saleRequestTypeLabel(row) === 'مبيع قديم' ? 'secondary' : 'info'"
                  variant="tonal"
                >
                  {{ saleRequestTypeLabel(row) }}
                </VChip>
                <VChip size="small" variant="outlined">
                  {{ isUnassigned(row) ? 'غير مسند' : (row.targetEmployeeName || row.TargetEmployeeName || '—') }}
                </VChip>
                <VChip
                  v-if="isUnreadSent(row)"
                  size="x-small"
                  color="error"
                >
                  غير مقروء
                </VChip>
              </div>
              <div class="d-flex flex-wrap gap-1" @click.stop>
                <VBtn size="small" variant="tonal" @click="openEdit(row)">تعديل</VBtn>
                <VBtn size="small" variant="tonal" color="primary" @click="openDetails(row)">تغيير الموظف</VBtn>
                <VBtn
                  v-if="canRequestException(row)"
                  size="small"
                  variant="tonal"
                  color="warning"
                  @click="openExceptionRequest(row)"
                >
                  طلب استثناء
                </VBtn>
                <VBtn size="small" variant="tonal" color="error" @click="confirmDelete(row)">حذف</VBtn>
              </div>
            </div>

            <VRow dense class="mb-2">
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">اسم الزبون</div><strong>{{ row.customerName }}</strong></VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">الهاتف</div>{{ row.customerPhone || row.CustomerPhone || '—' }}</VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">العنوان</div>{{ row.customerAddress || row.CustomerAddress || '—' }}</VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">المصدر</div>{{ requestSourceLabel(row) }}</VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">تاريخ الطلب</div>{{ formatIraqDate(row.createdAtUtc || row.CreatedAtUtc) }} {{ formatIraqTime(row.createdAtUtc || row.CreatedAtUtc) }}</VCol>
              <VCol v-if="lastNote(row)" cols="12"><div class="text-medium-emphasis text-caption">ملاحظة</div>{{ lastNote(row) }}</VCol>
            </VRow>

            <VDivider class="my-3" />

            <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-2">
              <div class="text-subtitle-1 font-weight-bold mb-0">تقييم البرنامج والبحث</div>
              <VChip
                size="small"
                :color="ratingColor(evaluationOf(row)?.overallRatingLabel || evaluationOf(row)?.OverallRatingLabel)"
              >
                التقييم النهائي:
                {{ evaluationOf(row)?.overallRatingLabel || evaluationOf(row)?.OverallRatingLabel || (evaluationsBusy ? '...' : 'لا يوجد تطابق') }}
                <template v-if="(evaluationOf(row)?.overallScore ?? evaluationOf(row)?.OverallScore) != null">
                  ({{ evaluationOf(row)?.overallScore ?? evaluationOf(row)?.OverallScore }})
                </template>
              </VChip>
            </div>
            <VAlert
              v-if="evaluationsError"
              type="error"
              variant="tonal"
              density="compact"
              class="mb-2"
            >
              {{ evaluationsError }}
            </VAlert>

            <div class="eval-metrics">
              <button
                v-for="item in EVAL_CRITERIA"
                :key="item.key"
                type="button"
                class="eval-metric"
                :class="{ 'eval-metric--active': panelOf(row)?.criterion === item.key }"
                @click.stop="openEvaluationHits(row, item.key, item.title)"
              >
                <div class="text-caption text-medium-emphasis">{{ item.title }}</div>
                <div class="eval-metric__count text-primary">
                  {{ categoryCountText(evaluationOf(row), item.key) }}
                  نتيجة
                </div>
                <VChip
                  size="x-small"
                  class="mt-1"
                  :color="ratingColor(categoryLabelText(evaluationOf(row), item.key))"
                >
                  {{ categoryLabelText(evaluationOf(row), item.key) }}
                  <template v-if="categoryWorstScore(categoryDto(evaluationOf(row), item.key)) != null">
                    ({{ categoryWorstScore(categoryDto(evaluationOf(row), item.key)) }})
                  </template>
                </VChip>
              </button>
            </div>

            <div
              v-if="panelOf(row)"
              class="eval-results mt-3"
              @click.stop
            >
              <div class="d-flex align-center justify-space-between gap-2 mb-2">
                <div class="text-subtitle-2 mb-0">
                  نتائج {{ panelOf(row).title }}
                  <span
                    v-if="panelOf(row).total != null"
                    class="text-medium-emphasis text-body-2"
                  >({{ panelOf(row).total }})</span>
                </div>
                <VBtn
                  size="small"
                  variant="text"
                  @click="evalBack(row)"
                >
                  رجوع
                </VBtn>
              </div>

              <div
                v-if="panelOf(row).loading"
                class="eval-skeleton"
              >
                <div
                  v-for="n in 3"
                  :key="n"
                  class="eval-skeleton__card"
                />
              </div>
              <VAlert
                v-else-if="panelOf(row).error"
                type="warning"
                variant="tonal"
                density="compact"
              >
                {{ panelOf(row).error }}
                <VBtn
                  size="small"
                  class="ms-2"
                  variant="text"
                  @click="retryEvaluationHits(row)"
                >
                  إعادة المحاولة
                </VBtn>
              </VAlert>
              <div
                v-else-if="!(panelOf(row).hits || []).length"
                class="text-medium-emphasis text-body-2"
              >
                لا توجد نتائج
              </div>
              <div
                v-else
                class="eval-hit-list"
              >
                <div
                  v-for="hit in panelOf(row).hits"
                  :id="hitDomId(hit)"
                  :key="hitDomId(hit)"
                  class="eval-hit-card"
                >
                  <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-2">
                    <div class="font-weight-medium">{{ hit.fullName || hit.FullName }}</div>
                    <VChip
                      size="small"
                      :color="ratingColor(hit.ratingLabel || hit.RatingLabel)"
                    >
                      {{ hit.ratingLabel || hit.RatingLabel || '—' }}
                      <template v-if="(hit.score ?? hit.Score) != null">({{ hit.score ?? hit.Score }})</template>
                    </VChip>
                  </div>
                  <div class="text-caption text-medium-emphasis mb-2">
                    {{ hit.cityName || hit.CityName || hit.province || hit.Province || '—' }}
                    ·
                    {{ hit.matchReason || hit.MatchReason || '—' }}
                  </div>
                  <div class="eval-mini-stats mb-2">
                    <div class="eval-mini-stat">
                      <span class="text-caption">عدد التسديدات</span>
                      <strong>{{ receiptCountDisplay(hit) ?? '—' }}</strong>
                    </div>
                    <div class="eval-mini-stat">
                      <span class="text-caption">أيام التسديد</span>
                      <strong>{{ settlementDaysDisplay(hit) != null ? `${settlementDaysDisplay(hit)} يوم` : '—' }}</strong>
                    </div>
                    <div class="eval-mini-stat">
                      <span class="text-caption">تاريخ المبيع</span>
                      <strong>{{ formatSaleDate(hit.dateSaleDevice || hit.DateSaleDevice) }}</strong>
                    </div>
                  </div>
                  <div class="text-body-2">
                    <div>الهاتف: {{ hit.phone || hit.Phone || '—' }}</div>
                    <div>المبلغ الكلي: {{ moneyShort(hit.amountTotalSales ?? hit.AmountTotalSales) }}</div>
                    <div>المستلم: {{ moneyShort(hit.receiptsTotal ?? hit.ReceiptsTotal) }}</div>
                    <div>المتبقي: {{ moneyShort(hit.amountRemaining ?? hit.AmountRemaining) }}</div>
                    <div v-if="hit.isLegal || hit.IsLegal">حالة: قانونية</div>
                    <div v-if="hit.delegateName || hit.DelegateName">المندوب: {{ hit.delegateName || hit.DelegateName }}</div>
                  </div>
                  <div class="d-flex gap-2 mt-2">
                    <VBtn
                      size="small"
                      color="primary"
                      variant="tonal"
                      @click="openInlineHitProfile(row, hit)"
                    >
                      عرض التفاصيل
                    </VBtn>
                  </div>
                  <div
                    v-if="panelOf(row).expandedHitKey === `${hit.cityValue || hit.CityValue || ''}:${hit.customerId || hit.CustomerId || ''}`"
                    class="eval-inline-profile mt-3"
                  >
                    <div class="d-flex justify-space-between align-center mb-2">
                      <div class="text-subtitle-2 mb-0">تفاصيل الزبون</div>
                      <VBtn
                        size="small"
                        variant="text"
                        @click="closeEvalProfile(row)"
                      >
                        إغلاق التفاصيل
                      </VBtn>
                    </div>
                    <div
                      v-if="panelOf(row).profileLoading"
                      class="text-medium-emphasis"
                    >
                      جاري تحميل التفاصيل...
                    </div>
                    <div
                      v-else
                      class="text-body-2"
                    >
                      <div>الاسم: {{ hit.fullName || hit.FullName }}</div>
                      <div>الهاتف: {{ hit.phone || hit.Phone || '—' }}</div>
                      <div>المحافظة: {{ hit.cityName || hit.CityName || hit.province || hit.Province || '—' }}</div>
                      <div>العنوان: {{ hit.address || hit.Address || pick(panelOf(row).profile, 'address', 'Address') || '—' }}</div>
                      <div>التقييم: {{ hit.ratingLabel || hit.RatingLabel || '—' }}</div>
                      <div>عدد التسديدات: {{ receiptCountDisplay(hit) ?? '—' }}</div>
                      <div>أيام التسديد: {{ settlementDaysDisplay(hit) != null ? `${settlementDaysDisplay(hit)} يوم` : '—' }}</div>
                      <div>تاريخ المبيع: {{ formatSaleDate(hit.dateSaleDevice || hit.DateSaleDevice) }}</div>
                      <div>المبلغ الكلي: {{ moneyShort(hit.amountTotalSales ?? hit.AmountTotalSales) }}</div>
                      <div>المستلم: {{ moneyShort(hit.receiptsTotal ?? hit.ReceiptsTotal) }}</div>
                      <div>المتبقي: {{ moneyShort(hit.amountRemaining ?? hit.AmountRemaining) }}</div>
                      <div v-if="hit.isLegal || hit.IsLegal">قانونية: نعم</div>
                      <div v-if="hit.delegateName || hit.DelegateName || pick(panelOf(row).profile, 'delegateName', 'DelegateName')">
                        المندوب: {{ hit.delegateName || hit.DelegateName || pick(panelOf(row).profile, 'delegateName', 'DelegateName') }}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <VBtn
              v-if="requestStatus(row) === 'Completed' || requestStatus(row) === 'Inspected'"
              class="mt-3"
              color="primary"
              size="small"
              @click.stop="openCustomerProfile(row)"
            >
              فتح بروفايل الزبون
            </VBtn>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <VDialog
      :model-value="!!detail"
      max-width="840"
      @update:model-value="v => { if (!v) { detail = null; if (!profileOpen) selected = null } }"
    >
      <VCard v-if="detail">
        <VCardTitle>طلب #{{ detail.id }}</VCardTitle>
        <VCardText>
          <div>الزبون: {{ detail.customerName }}</div>
          <div>
            نوع الطلب:
            <VChip
              size="x-small"
              class="ms-1"
              :color="saleRequestTypeLabel(detail) === 'مبيع قديم' ? 'secondary' : 'primary'"
            >
              {{ saleRequestTypeLabel(detail) }}
            </VChip>
          </div>
          <div v-if="pick(detail, 'existingCustomerId', 'ExistingCustomerId')">
            رقم الزبون: {{ pick(detail, 'existingCustomerId', 'ExistingCustomerId') }}
          </div>
          <div>الهاتف: {{ detail.customerPhone || '—' }}</div>
          <div>العنوان: {{ detail.customerAddress || '—' }}</div>
          <div>الموظف: {{ isEmployeeSubmitted(detail) ? submittedBy(detail) : (detail.targetEmployeeName || 'غير مسند') }}</div>
          <div v-if="isFollowerSubmitted(detail) || isDelegateSubmitted(detail) || isEmployeeSubmitted(detail)">
            المصدر: {{ requestSourceLabel(detail) }}
            <template v-if="isFollowerSubmitted(detail) || isDelegateSubmitted(detail)"> — أرسل بواسطة: {{ submittedBy(detail) }}</template>
          </div>
          <div>المحافظة: {{ displayCityName(detail, branches) }}</div>
          <div class="d-flex align-center gap-2 mt-1">
            <span>الحالة:</span>
            <VChip
              size="small"
              :color="statusColor(requestStatus(detail))"
            >
              {{ statusText(detail) }}
            </VChip>
            <VChip
              v-if="requestStatus(detail) === 'Returned'"
              size="small"
              color="warning"
            >
              معاد للموظف
            </VChip>
          </div>
          <div v-if="isPrepared(detail)" class="mt-1">
            مجهز للبيع — الموظف هو من يكمل البيع.
          </div>
          <div v-if="requestStatus(detail) === 'Inspected'" class="mt-1">
            تم الكشف — الزيارة محفوظة ولم يكتمل البيع بعد.
          </div>
          <div
            v-if="canRequestException(detail)"
            class="mt-3"
          >
            <VBtn
              color="warning"
              variant="tonal"
              @click="openExceptionRequest(detail)"
            >
              طلب استثناء
            </VBtn>
            <div class="text-caption text-medium-emphasis mt-1">
              الموافقة تُعلمك فقط ولا تستبدل الإسناد أو التجهيز للبيع.
            </div>
          </div>
          <div v-if="saleDetail" class="mt-3">
            <div class="font-weight-bold mb-1">بيانات المسودة حتى الآن</div>
            <div>الزبون: {{ pick(saleDetail, 'fullName', 'FullName') || '—' }}</div>
            <div>الهاتف: {{ pick(saleDetail, 'phone', 'Phone') || '—' }}</div>
            <div>العنوان: {{ pick(saleDetail, 'address', 'Address') || '—' }}</div>
            <div v-if="pick(saleDetail, 'shop', 'Shop')">
              المحل: {{ pick(pick(saleDetail, 'shop', 'Shop'), 'shopName', 'ShopName') || '—' }}
              <ShopLocationLink :shop="pick(saleDetail, 'shop', 'Shop')" />
            </div>
            <div
              v-if="saleDocs(saleDetail).length"
              class="mt-2"
            >
              <div class="font-weight-bold mb-1">
                المستندات
              </div>
              <div
                v-for="doc in saleDocs(saleDetail)"
                :key="pick(doc, 'documentId', 'DocumentId')"
                class="d-flex align-center flex-wrap ga-2 mb-2"
              >
                <span>{{ saleDocumentTitle(doc) }}</span>
                <VBtn
                  size="small"
                  color="primary"
                  variant="tonal"
                  @click="openSaleDocument(saleDetail, doc)"
                >
                  فتح
                </VBtn>
              </div>
            </div>
          </div>
          <div v-if="requestStatus(detail) === 'Pending' && (detail.pendingNote || detail.PendingNote)">
            ملاحظة التعليق: {{ detail.pendingNote || detail.PendingNote }}
          </div>
          <div v-if="detail.rejectionReason || detail.RejectionReason">
            سبب الرفض: {{ detail.rejectionReason || detail.RejectionReason }}
          </div>
          <div
            v-if="detail.returnNote || detail.ReturnNote"
            class="text-error font-weight-bold"
          >
            ملاحظة الإعادة: {{ detail.returnNote || detail.ReturnNote }}
          </div>
          <div class="text-medium-emphasis mt-2">
            الإنشاء: {{ formatIraqTime(detail.createdAtUtc) }}
          </div>
          <div
            v-if="detail.assignedAtUtc"
            class="text-medium-emphasis"
          >
            الإسناد: {{ formatIraqTime(detail.assignedAtUtc) }}
          </div>
          <div
            v-if="detail.viewedAtUtc"
            class="text-medium-emphasis"
          >
            المشاهدة: {{ formatIraqTime(detail.viewedAtUtc) }}
          </div>
          <div
            v-if="detail.rejectedAtUtc"
            class="text-medium-emphasis"
          >
            الرفض: {{ formatIraqTime(detail.rejectedAtUtc) }}
          </div>
          <div
            v-if="detail.completedAtUtc"
            class="text-medium-emphasis"
          >
            الاكتمال: {{ formatIraqTime(detail.completedAtUtc) }}
          </div>

          <template v-if="isUnassigned(detail)">
            <VDivider class="my-4" />
            <div class="font-weight-bold mb-4">
              بحث زبون
            </div>
            <VTextField
              v-model="intakeQuery"
              class="mb-4"
              label="بحث عن زبون"
              variant="outlined"
              hide-details="auto"
              density="comfortable"
              @keyup.enter="searchIntakeCustomers"
            />
            <VBtn
              class="mb-4"
              @click="searchIntakeCustomers"
            >
              بحث
            </VBtn>
            <div
              v-if="!intakeCustomers.length"
              class="text-medium-emphasis mb-4"
            >
              البحث داخل محافظة الطلب فقط. اختيار نتيجة اختياري.
            </div>
            <VCard
              v-for="c in intakeCustomers"
              :key="customerKey(c)"
              class="mb-3 intake-result"
              variant="outlined"
              :class="{ 'border-primary': intakeSelected && customerKey(intakeSelected) === customerKey(c) }"
              @click="selectIntakeCustomer(c)"
            >
              <VCardText>
                <div class="font-weight-bold mb-2">
                  {{ c.fullName || c.customerName }}
                </div>
                <div class="mb-1">
                  الهاتف: {{ c.phone || '—' }}
                </div>
                <div class="mb-1">
                  العنوان: {{ c.address || '—' }}
                </div>
                <div>
                  المحافظة: {{ displayCityName(c, branches) || '—' }}
                </div>
                <VBtn
                  class="mt-3"
                  size="small"
                  color="primary"
                  variant="tonal"
                  @click.stop="openIntakeCustomerProfile(c)"
                >
                  عرض بروفايل الزبون
                </VBtn>
              </VCardText>
            </VCard>
            <div class="font-weight-bold mb-4 mt-6">
              بيانات الطلب قبل الإرسال
            </div>
            <VTextField
              v-model="intakeForm.fullName"
              class="mb-4"
              label="الاسم"
              variant="outlined"
              hide-details="auto"
              density="comfortable"
            />
            <VTextField
              v-model="intakeForm.phone"
              class="mb-4"
              label="الهاتف"
              variant="outlined"
              hide-details="auto"
              density="comfortable"
              maxlength="11"
              inputmode="numeric"
              hint="11 رقم ويبدأ بـ 07"
              persistent-hint
              :rules="[iraqPhoneValidator]"
              @update:model-value="v => intakeForm.phone = normalizeIraqPhone(v)"
            />
            <VTextField
              v-model="intakeForm.province"
              class="mb-4"
              label="المحافظة"
              variant="outlined"
              hide-details="auto"
              density="comfortable"
            />
            <VTextField
              v-model="intakeForm.address"
              class="mb-4"
              label="العنوان"
              variant="outlined"
              hide-details="auto"
              density="comfortable"
            />
            <VTextarea
              v-model="intakeForm.notes"
              class="mb-4"
              label="الملاحظات"
              variant="outlined"
              auto-grow
              hide-details="auto"
            />
            <div class="mb-4">
              محافظة الإسناد: {{ displayCityName(detail, branches) }}
            </div>
            <VSelect
              v-model="assignEmployeeId"
              class="mb-4"
              :items="employees"
              item-title="employeeName"
              item-value="employeeId"
              label="موظف المبيعات"
              variant="outlined"
              hide-details="auto"
              density="comfortable"
              :disabled="!assignCityValue"
            />
            <VBtn
              class="mt-2"
              color="primary"
              :loading="busy"
              :disabled="!assignEmployeeId"
              @click="assign"
            >
              إرسال للموظف
            </VBtn>
            <VBtn
              v-if="canManagerReject(detail)"
              class="mt-2 ms-2"
              color="error"
              variant="tonal"
              @click="rejectOpen = true"
            >
              رفض الطلب
            </VBtn>
          </template>

          <template v-if="requestStatus(detail) === 'Assigned' || requestStatus(detail) === 'Viewed'">
            <VDivider class="my-4" />
            <div>الموظف الحالي: {{ detail.targetEmployeeName || 'غير مسند' }}</div>
            <div>الحالة: {{ statusText(detail) }}</div>
            <VBtn
              v-if="canManagerReject(detail)"
              class="mt-3"
              color="error"
              variant="tonal"
              @click="rejectOpen = true"
            >
              رفض الطلب
            </VBtn>
          </template>

          <template v-if="requestStatus(detail) === 'Rejected'">
            <VDivider class="my-4" />
            <VBtn
              color="warning"
              :disabled="!(detail.targetEmployeeId || detail.employeeId)"
              @click="returnOpen = true"
            >
              إرجاع للموظف
            </VBtn>
            <div class="text-medium-emphasis mt-2">
              تُعاد لنفس الموظف: {{ detail.targetEmployeeName }}
            </div>
          </template>

          <template v-if="canManagerInspectedActions(detail)">
            <VDivider class="my-4" />
            <div class="d-flex flex-wrap ga-2">
              <VBtn
                color="primary"
                :loading="busy"
                @click="prepareInspected"
              >
                جاهز للبيع
              </VBtn>
              <VBtn
                color="warning"
                variant="tonal"
                :loading="busy"
                @click="pendOpen = true"
              >
                معلّق
              </VBtn>
              <VBtn
                color="error"
                variant="tonal"
                :loading="busy"
                @click="rejectOpen = true"
              >
                مرفوض
              </VBtn>
            </div>
          </template>

          <template v-if="requestStatus(detail) === 'Completed'">
            <VDivider class="my-4" />
            <VBtn
              color="primary"
              @click="openCustomerProfile(selected, detail)"
            >
              فتح بروفايل الزبون
            </VBtn>
          </template>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            السجل
          </div>
          <div
            v-if="!historyRows.length"
            class="text-medium-emphasis"
          >
            لا يوجد سجل بعد.
          </div>
          <VTimeline
            v-else
            density="compact"
            side="end"
          >
            <VTimelineItem
              v-for="(item, i) in historyRows"
              :key="item.id || item.Id || i"
              size="x-small"
            >
              <div>{{ historyTime(item) }}</div>
              <div>{{ historyEvent(item) }}</div>
              <div v-if="historyActor(item)">
                {{ historyActor(item) }}
              </div>
              <div v-if="historyNote(item)">
                {{ historyNote(item) }}
              </div>
            </VTimelineItem>
          </VTimeline>
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="detail = null; selected = null"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="profileOpen"
      max-width="920"
      scrollable
    >
      <VCard>
        <VCardTitle>بروفايل الزبون</VCardTitle>
        <VCardText>
          <div class="font-weight-bold mb-2">
            بيانات الزبون
          </div>
          <div>الاسم: {{ profileName }}</div>
          <div>الهاتف: {{ profilePhone }}</div>
          <div>المحافظة: {{ profileProvince }}</div>
          <div>العنوان: {{ profileAddress || '—' }}</div>
          <div>رقم البطاقة: {{ profileNationalId || '—' }}</div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            تفاصيل المحل
          </div>
          <div v-if="!latestShop" class="text-medium-emphasis">
            لا توجد بيانات محل بعد.
          </div>
          <div v-else>
            <div>اسم المحل: {{ pick(latestShop, 'shopName', 'ShopName') }}</div>
            <div>نوع النشاط: {{ pick(latestShop, 'shopBusinessType', 'ShopBusinessType') }}</div>
            <div>قيمة البضاعة التقديرية: {{ money(pick(latestShop, 'shopStockEstimatedValue', 'ShopStockEstimatedValue')) }}</div>
            <div>الإيراد اليومي التقديري: {{ money(pick(latestShop, 'estimatedDailyRevenue', 'EstimatedDailyRevenue')) }}</div>
            <div>الطول: {{ pick(latestShop, 'shopLength', 'ShopLength') }} م</div>
            <div>العرض: {{ pick(latestShop, 'shopWidth', 'ShopWidth') }} م</div>
            <div>المساحة: {{ areaText(latestShop) }}</div>
            <ShopLocationLink :shop="latestShop" />
            <VImg
              v-if="shopImageUrl"
              :src="shopImageUrl"
              max-height="180"
              class="mt-3"
              style="cursor: pointer"
              @click="shopImageOpen = true"
            />
            <div v-else class="text-medium-emphasis mt-2">
              لا توجد صورة محل.
            </div>
          </div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            التقييم
          </div>
          <div v-if="!profileEvaluations.length" class="text-medium-emphasis">
            لا يوجد تقييم بعد.
          </div>
          <div
            v-for="item in profileEvaluations"
            :key="pick(item, 'saleId', 'SaleId')"
            class="mb-2"
          >
            <div>{{ pick(item, 'evaluationName', 'EvaluationName') || evaluationLabel(pick(item, 'evaluationLevel', 'EvaluationLevel')) }}</div>
            <div v-if="pick(item, 'evaluationNote', 'EvaluationNote')">
              ملاحظة التقييم: {{ pick(item, 'evaluationNote', 'EvaluationNote') }}
            </div>
          </div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            المبيعات
          </div>
          <div v-if="!profileSales.length" class="text-medium-emphasis">
            لا توجد عمليات بيع بعد.
          </div>
          <div
            v-for="item in profileSales"
            :key="pick(item, 'saleId', 'SaleId')"
            class="mb-2"
          >
            <div>
              <strong v-if="Number(pick(item, 'saleId', 'SaleId')) === Number(currentSaleId(selected, detail))">البيع الحالي</strong>
              <span v-else>مبيعات سابقة</span>
              — بيع #{{ pick(item, 'saleId', 'SaleId') }}
            </div>
            <div>المبلغ: {{ money(pick(item, 'finalSalePrice', 'FinalSalePrice')) }}</div>
            <div v-if="pick(item, 'dailyInstallment', 'DailyInstallment')">
              القسط اليومي: {{ money(pick(item, 'dailyInstallment', 'DailyInstallment')) }}
            </div>
            <div class="text-medium-emphasis">
              التاريخ: {{ formatIraqDate(pick(item, 'date', 'Date')) }}
            </div>
            <div
              v-for="doc in saleDocs(item)"
              :key="pick(doc, 'documentId', 'DocumentId')"
              class="d-flex align-center flex-wrap ga-2 mt-1"
            >
              <span>{{ saleDocumentTitle(doc) }}</span>
              <VBtn
                size="small"
                color="primary"
                variant="tonal"
                @click="openSaleDocument(item, doc)"
              >
                فتح
              </VBtn>
            </div>
          </div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            الطلبات
          </div>
          <div v-if="!profileRequests.length" class="text-medium-emphasis">
            لا توجد طلبات سابقة.
          </div>
          <div
            v-for="item in profileRequests"
            :key="pick(item, 'id', 'Id')"
            class="mb-2"
          >
            <div>طلب #{{ pick(item, 'id', 'Id') }} — {{ statusText(pick(item, 'status', 'Status')) }}</div>
            <div v-if="pick(item, 'rejectionReason', 'RejectionReason')">
              سبب الرفض: {{ pick(item, 'rejectionReason', 'RejectionReason') }}
            </div>
            <div v-if="pick(item, 'pendingNote', 'PendingNote')">
              ملاحظة التعليق: {{ pick(item, 'pendingNote', 'PendingNote') }}
            </div>
            <div v-if="pick(item, 'returnNote', 'ReturnNote')">
              ملاحظة الإعادة: {{ pick(item, 'returnNote', 'ReturnNote') }}
            </div>
          </div>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            السجل
          </div>
          <div
            v-if="!profileHistory.length"
            class="text-medium-emphasis"
          >
            لا يوجد سجل بعد.
          </div>
          <VTimeline
            v-else
            density="compact"
            side="end"
          >
            <VTimelineItem
              v-for="(item, i) in profileHistory"
              :key="item.id || item.Id || `p-${i}`"
              size="x-small"
            >
              <div>{{ historyTime(item) }}</div>
              <div>{{ historyEvent(item) }}</div>
              <div v-if="historyActor(item)">
                {{ historyActor(item) }}
              </div>
              <div v-if="historyNote(item)">
                {{ historyNote(item) }}
              </div>
            </VTimelineItem>
          </VTimeline>

          <VDivider class="my-4" />
          <div class="font-weight-bold mb-2">
            الملاحظات
          </div>
          <div v-if="!profileNotes.length" class="text-medium-emphasis">
            لا توجد ملاحظات بعد.
          </div>
          <div
            v-for="(item, i) in profileNotes"
            :key="pick(item, 'id', 'Id') || i"
            class="mb-2"
          >
            <div>{{ pick(item, 'authorRole', 'AuthorRole') }} — {{ pick(item, 'authorName', 'AuthorName') }}</div>
            <div>{{ pick(item, 'note', 'Note') }}</div>
            <div class="text-medium-emphasis">
              {{ formatIraqDate(pick(item, 'createdAtUtc', 'CreatedAtUtc')) }}
            </div>
          </div>
          <VTextarea
            v-model="managerNote"
            class="mt-2"
            label="ملاحظة مسؤول المبيعات"
            auto-grow
          />
          <VBtn
            class="mt-2"
            color="primary"
            :loading="busy"
            :disabled="!managerNote.trim()"
            @click="addManagerNote"
          >
            إضافة ملاحظة
          </VBtn>
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="profileOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="shopImageOpen"
      max-width="720"
    >
      <VCard v-if="shopImageUrl">
        <VImg :src="shopImageUrl" />
        <VCardActions>
          <VBtn
            variant="text"
            @click="shopImageOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="returnOpen"
      max-width="420"
    >
      <VCard>
        <VCardTitle>إرجاع للموظف</VCardTitle>
        <VCardText>
          <VTextarea
            v-model="returnNote"
            label="ملاحظة الإعادة *"
            auto-grow
          />
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="returnOpen = false"
          >
            رجوع
          </VBtn>
          <VBtn
            color="warning"
            :loading="busy"
            :disabled="!returnNote.trim()"
            @click="sendReturn"
          >
            إرجاع
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="pendOpen"
      max-width="420"
    >
      <VCard>
        <VCardTitle>تعليق الطلب</VCardTitle>
        <VCardText>
          <VTextarea
            v-model="pendNote"
            label="الملاحظة *"
            auto-grow
          />
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="pendOpen = false"
          >
            رجوع
          </VBtn>
          <VBtn
            color="warning"
            :loading="busy"
            :disabled="!pendNote.trim()"
            @click="pendInspected"
          >
            تعليق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="exceptionOpen"
      max-width="480"
    >
      <VCard>
        <VCardTitle>طلب استثناء</VCardTitle>
        <VCardText>
          <VAlert
            class="mb-3"
            type="info"
            variant="tonal"
            density="compact"
          >
            بعد الموافقة يبقى إسناد الطلب أو تجهيزه للبيع مطلوبًا كالمعتاد.
          </VAlert>
          <div
            v-if="exceptionTarget"
            class="mb-3 text-body-2"
          >
            <div>الزبون: {{ pick(exceptionTarget, 'customerName', 'CustomerName') || '—' }}</div>
            <div>
              طلب #
              {{ requestId(exceptionTarget) || '—' }}
              —
              {{ displayCityName(exceptionTarget, branches) || '—' }}
            </div>
          </div>
          <VTextarea
            v-model="exceptionReason"
            label="سبب الاستثناء *"
            auto-grow
            :disabled="exceptionBusy"
          />
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            :disabled="exceptionBusy"
            @click="exceptionOpen = false"
          >
            رجوع
          </VBtn>
          <VBtn
            color="warning"
            :loading="exceptionBusy"
            :disabled="exceptionBusy || !exceptionReason.trim()"
            @click="submitExceptionRequest"
          >
            إرسال
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="rejectOpen"
      max-width="420"
    >
      <VCard>
        <VCardTitle>رفض الطلب</VCardTitle>
        <VCardText>
          <VTextarea
            v-model="rejectReason"
            label="سبب الرفض *"
            auto-grow
          />
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="rejectOpen = false"
          >
            رجوع
          </VBtn>
          <VBtn
            color="error"
            :loading="busy"
            :disabled="!rejectReason.trim()"
            @click="rejectSubmitted"
          >
            رفض
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="importOpen"
      max-width="720"
    >
      <VCard>
        <VCardTitle>معاينة استيراد Excel</VCardTitle>
        <VCardText v-if="importPreview">
          <div>عدد الصفوف: {{ importPreview.total }}</div>
          <div>الصحيح: {{ importPreview.valid.length }}</div>
          <div>الخطأ: {{ importPreview.errors.length }}</div>
          <div
            v-if="importPreview.errors.length"
            class="mt-3"
          >
            <div class="font-weight-bold mb-1">
              أسباب الخطأ
            </div>
            <div
              v-for="err in importPreview.errors"
              :key="err.rowNumber"
              class="text-error"
            >
              صف {{ err.rowNumber }}: {{ err.message }}
            </div>
          </div>
          <div
            v-else
            class="text-medium-emphasis mt-2"
          >
            الصفوف الصحيحة تُحفظ كطلبات غير مسندة ولا تُرسل لأي موظف.
          </div>
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="importOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="importBusy"
            :disabled="!importPreview?.valid?.length"
            @click="confirmImport"
          >
            حفظ الطلبات غير المسندة
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="editOpen"
      max-width="560"
    >
      <VCard>
        <VCardTitle>تعديل طلب البيع</VCardTitle>
        <VCardText>
          <VTextField
            v-model="editForm.customerName"
            class="mb-3"
            label="اسم الزبون"
          />
          <VTextField
            v-model="editForm.phone"
            class="mb-3"
            label="الهاتف"
            :rules="[iraqPhoneValidator]"
            @update:model-value="v => editForm.phone = normalizeIraqPhone(v)"
          />
          <VSelect
            :model-value="editForm.cityValue"
            class="mb-2"
            label="المحافظة (نقل حقيقي بين الفروع)"
            :items="branches"
            item-title="name"
            item-value="value"
            :disabled="!!editTransferBlockedReason || isDemo()"
            @update:model-value="onEditCityChanged"
          />
          <VAlert
            v-if="editTransferBlockedReason"
            class="mb-3"
            type="warning"
            variant="tonal"
            density="compact"
          >
            {{ editTransferBlockedReason }}
          </VAlert>
          <VAlert
            v-else-if="editCityChanged"
            class="mb-3"
            type="warning"
            variant="tonal"
            density="compact"
          >
            سيتم نقل الطلب إلى محافظة أخرى وقد يتم إلغاء إسناد الموظف الحالي.
          </VAlert>
          <VSelect
            v-if="editCityChanged && !isDemo()"
            v-model="editForm.toEmployeeId"
            class="mb-3"
            label="موظف الفرع الهدف (اختياري)"
            :items="editEmployees"
            item-title="employeeName"
            item-value="employeeId"
            clearable
            hint="إذا لم تُختر، يُلغى الإسناد تلقائياً لأن موظف المصدر لا يعمل في الفرع الهدف"
            persistent-hint
          />
          <VTextField
            v-model="editForm.province"
            class="mb-3"
            label="اسم المحافظة للعرض"
          />
          <VTextField
            v-model="editForm.address"
            class="mb-3"
            label="العنوان"
          />
          <VTextarea
            v-model="editForm.notes"
            label="الملاحظات"
            auto-grow
          />
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="editOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="editBusy"
            :disabled="!!editTransferBlockedReason && editCityChanged"
            @click="saveEdit"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="deleteOpen"
      max-width="420"
    >
      <VCard>
        <VCardTitle>تأكيد الحذف</VCardTitle>
        <VCardText>
          سيتم إخفاء الطلب من القوائم مع الاحتفاظ بسجل التدقيق. لن يُحذف أي بيع أو تسديد مرتبط.
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="deleteOpen = false"
          >
            رجوع
          </VBtn>
          <VBtn
            color="error"
            :loading="deleteBusy"
            @click="doDelete"
          >
            حذف
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.intake-result {
  cursor: pointer;
}
.intake-result.border-primary {
  border-width: 2px;
}
.unread-request {
  border: 2px solid rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.08);
}
.sales-request-card {
  border-radius: 14px;
}
.eval-metrics {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.5rem;
}
@media (max-width: 960px) {
  .eval-metrics {
    grid-template-columns: 1fr;
  }
}
.eval-metric {
  background: rgba(var(--v-theme-on-surface), 0.02);
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 10px;
  padding: 0.65rem 0.75rem;
  text-align: start;
  cursor: pointer;
  min-height: auto;
}
.eval-metric--active {
  border-color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.06);
}
.eval-metric__count {
  font-size: 1.05rem;
  font-weight: 700;
  margin-top: 0.15rem;
}
.eval-results {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 12px;
  padding: 0.75rem;
  background: rgba(var(--v-theme-surface), 1);
}
.eval-hit-list {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}
.eval-hit-card {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 10px;
  padding: 0.75rem;
  overflow-wrap: anywhere;
}
.eval-mini-stats {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.4rem;
}
@media (max-width: 600px) {
  .eval-mini-stats {
    grid-template-columns: 1fr;
  }
}
.eval-mini-stat {
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.03);
  padding: 0.4rem 0.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}
.eval-inline-profile {
  border-top: 1px dashed rgba(var(--v-theme-on-surface), 0.2);
  padding-top: 0.75rem;
}
.eval-skeleton {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.eval-skeleton__card {
  height: 72px;
  border-radius: 10px;
  background: linear-gradient(90deg, rgba(var(--v-theme-on-surface), 0.06), rgba(var(--v-theme-on-surface), 0.12), rgba(var(--v-theme-on-surface), 0.06));
  background-size: 200% 100%;
  animation: evalPulse 1.2s ease-in-out infinite;
}
@keyframes evalPulse {
  0% { background-position: 100% 0; }
  100% { background-position: -100% 0; }
}
.cursor-pointer {
  cursor: pointer;
}
</style>
