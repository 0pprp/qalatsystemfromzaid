<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import { useRouter } from 'vue-router'
import * as XLSX from 'xlsx'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { formatIraqDate, formatIraqTime } from '@/composables/iraqDate'
import {
  assignExceptionRequest,
  assignSalesRequestsBatch,
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
  withCityQuery,
  displayCityName,
} from '@/composables/salesManagerApi'
import { saleDisplayDocuments, saleDocumentTitle } from '@/composables/saleDocuments'
import { canManagerRejectRequest } from '@/utils/salesManagerRejectRules'
import {
  countDuplicateRequests,
  findDuplicateRequests,
} from '@/utils/salesRequestDuplicateSearch'
import { capEvaluationRows, createCityListCache } from '@/utils/salesRequestsListCache'
import ShopLocationLink from '@/components/ShopLocationLink.vue'
import { isDemo } from '@/composables/useCities'
import { useSalesBranches } from '@/composables/useSalesBranches'
import { useUserRole } from '@/composables/useUserRole'
import { useToast } from '@/composables/useToast'
import { markAllSalesRequestsRead, refreshSalesRequestUnread, salesRequestUnreadCount } from '@/composables/useSalesRequestUnread'
import { findProvince } from '@/utils/provinceCatalog'
import {
  buildEvaluationKey,
  categoryBlock,
  categoryResultCount,
  categoryWorstLabel,
  categoryWorstScore,
  evaluationKeyFromRow,
  indexEvaluationItems,
  lookupEvaluation,
  patchCategoryFromHits,
  receiptCountDisplay,
  requestCityOf,
  requestIdOf,
  settlementDaysDisplay,
} from '@/composables/salesRequestEvaluationMap'
import { parseSalesRequestExcel } from '@/composables/salesRequestExcelImport'
import { normalizeIraqPhone } from '@core/utils/validators'

const toast = useToast()
const router = useRouter()
const { isDirector } = useUserRole()
/** المدير: اطلاع فقط — بدون أكشنات مدير المبيعات */
const readOnly = computed(() => isDirector.value)
const { branches } = useSalesBranches()
const rows = ref([])
const tab = ref('all')
const cityValue = ref('')
const CITY_PREF_KEY = 'sm-requests-city-v1'
const RETURN_KEY = 'sm-requests-return-v1'
try {
  const saved = sessionStorage.getItem(CITY_PREF_KEY)
  if (saved != null)
    cityValue.value = saved
}
catch {
  /* private mode */
}
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
const legalFollowers = ref([])
const legalFollowerUserId = ref(null)
const legalNote = ref('')
const legalBusy = ref(false)
const returnOpen = ref(false)
const returnNote = ref('')
const rejectOpen = ref(false)
const rejectReason = ref('')
const rejectTarget = ref(null)
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
const bulkSelectedKeys = ref([])
const bulkEmployeeId = ref(null)
const bulkBusy = ref(false)
const bulkReport = ref(null)
const bulkEmployees = ref([])
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
/** Search within sales-request rows (not ERP catalog). */
const REQ_DUP_CRITERIA = [
  { key: 'tripleName', title: 'تشابه الاسم الثلاثي' },
  { key: 'phone', title: 'تشابه رقم الهاتف' },
  { key: 'fatherGrandfather', title: 'تشابه اسم الأب والجد' },
]
const listSearchQuery = ref('')
/** Debounced search text — keeps typing responsive on large lists. */
const listSearchApplied = ref('')
const applyListSearch = useDebounceFn((value) => {
  listSearchApplied.value = String(value || '')
}, 300)
watch(listSearchQuery, value => applyListSearch(value))
/** Per-request panel: { criterion, title, hits } */
const requestDupPanels = ref({})
const listLoading = ref(false)
const loadingMore = ref(false)
const listPage = ref(1)
const listPageSize = 40
const listTotal = ref(0)
const hasMore = ref(false)
const serverTabCounts = ref({})
const cityListCache = createCityListCache(90_000)
let loadGeneration = 0
let evalGeneration = 0
let deletedLoadGeneration = 0
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
const deleteReason = ref('')
const deleteImpact = ref(null)
const deleteImpactBusy = ref(false)
const deleteConfirmPayments = ref(false)
const deletedRows = ref([])
const intakeCustomers = ref([])
const intakeSelected = ref(null)
const intakeForm = ref({
  fullName: '',
  phone: '',
  province: '',
  address: '',
  notes: '',
  maxSalePrice: '',
})

const tabs = [
  { value: 'all', title: 'الكل' },
  { value: 'unassigned', title: 'غير مسند' },
  { value: 'legal', title: 'القانونية' },
  { value: 'sent', title: 'الطلبات المرسلة' },
  { value: 'incoming', title: 'طلبات البيع' },
  { value: 'prepared', title: 'جاهز للبيع' },
  { value: 'inspected', title: 'تم الكشف' },
  { value: 'pending', title: 'معلقة' },
  { value: 'rejected', title: 'مرفوض' },
  { value: 'sold', title: 'تم البيع' },
  { value: 'deleted', title: 'المحذوفين' },
  { value: 'exceptions', title: 'طلبات الاستثناء' },
]

const exceptionStatusTabs = [
  { value: 'Pending', title: 'قيد الانتظار' },
  { value: 'Approved', title: 'موافق عليه' },
  { value: 'Rejected', title: 'مرفوض' },
]

function employeeIdOf(row) {
  return Number(row?.targetEmployeeId || row?.TargetEmployeeId || row?.employeeId || row?.EmployeeId || 0)
}

/** Employee to return a rejected request to (assigned target, else employee submitter). */
function returnEmployeeIdOf(row) {
  const assigned = employeeIdOf(row)
  if (assigned > 0)
    return assigned
  if (isEmployeeSubmitted(row))
    return Number(pick(row, 'createdByUserId', 'CreatedByUserId') || 0)

  return 0
}

function returnEmployeeNameOf(row) {
  return pick(row, 'targetEmployeeName', 'TargetEmployeeName')
    || (isEmployeeSubmitted(row) ? submittedBy(row) : '')
    || '—'
}

function isUnassigned(row) {
  const s = requestStatus(row)
  if (s === 'Rejected' || s === 'Completed' || s === 'LegalFollowup')
    return false
  const hold = String(pick(row, 'exceptionHoldStatus', 'ExceptionHoldStatus') || '').trim().toLowerCase()
  if (hold === 'pending' || hold === 'approved' || hold === 'rejected')
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
  const who = (submittedBy(row) || '').trim()
  if (isFollowerSubmitted(row))
    return who && who !== 'متابع' ? `المتابع ${who}` : 'المتابع'
  if (isDelegateSubmitted(row))
    return who && who !== 'مندوب' ? `المندوب ${who}` : 'المندوب'
  if (isEmployeeSubmitted(row))
    return who && who !== 'موظف مبيعات' ? `موظف المبيعات ${who}` : 'موظف المبيعات'
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
    case 'legal':
      return s === 'LegalFollowup'
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

/** True when branch already has a denormalized exception hold. */
function hasExceptionHold(row) {
  const hold = String(pick(row, 'exceptionHoldStatus', 'ExceptionHoldStatus') || '').trim().toLowerCase()
  return hold === 'pending' || hold === 'approved' || hold === 'rejected'
}

function hasActiveExceptionInList(row) {
  const rid = Number(requestId(row) || 0)
  if (rid <= 0)
    return false
  return (exceptionRows.value || []).some((e) => {
    const sid = Number(pick(e, 'salesRequestId', 'SalesRequestId') || 0)
    const st = String(pick(e, 'status', 'Status') || '')
    return sid === rid && (st === 'Pending' || st === 'Approved')
  })
}

/** MVP: show exception CTA for unassigned / assigned-flow / inspected — never when hold/active exists. */
function canRequestException(row) {
  if (readOnly.value)
    return false
  if (!row)
    return false
  if (hasExceptionHold(row) || hasActiveExceptionInList(row))
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
  if (readOnly.value)
    return false
  const status = String(item?.status || item?.Status || '')
  const consumed = !!(item?.assignmentConsumed ?? item?.AssignmentConsumed)

  return status === 'Approved' && !consumed
}

/** Detail assign panel: normal unassigned OR approved-exception flow (hold still Approved). */
function canShowAssignPanel(row) {
  if (readOnly.value)
    return false
  if (exceptionAssignId.value)
    return requestStatus(row) === 'New' || employeeIdOf(row) <= 0

  return isUnassigned(row)
}

function exceptionDecisionNote(item) {
  return (item?.decisionNote || item?.DecisionNote || '').trim()
}

function exceptionAssignedLabel(item) {
  const name = item?.assignedEmployeeName || item?.AssignedEmployeeName
  if (!name)
    return ''
  const at = item?.assignedAtUtc || item?.AssignedAtUtc

  return at ? `${name} — ${formatIraqDate(at)}` : name
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

const showingExceptions = computed(() => tab.value === 'exceptions')
const showingDeleted = computed(() => tab.value === 'deleted')

const isListSearching = computed(() => !!String(listSearchApplied.value || '').trim())

const visibleRows = computed(() => {
  // Server already filters by tab + q; keep a light client pass for responsiveness.
  if (showingDeleted.value)
    return deletedRows.value

  return rows.value
})

const duplicateSearchPoolMemo = computed(() => [...(rows.value || []), ...(deletedRows.value || [])])

function duplicateSearchPool() {
  return duplicateSearchPoolMemo.value
}

function requestDupKey(row) {
  return branchRowKey(row, 'id')
}

function requestDupPanelOf(row) {
  return requestDupPanels.value[requestDupKey(row)] || null
}

/** One-pass memo: avoid O(cards × criteria × pool) on every keystroke/render. */
const requestDupCountsByKey = computed(() => {
  const pool = duplicateSearchPoolMemo.value
  const map = Object.create(null)
  const list = visibleRows.value || []
  // Keep first paint fast; skip full scans on huge filtered sets.
  if (list.length > 80)
    return map
  for (const row of list) {
    const id = requestDupKey(row)
    if (!id || map[id])
      continue
    const entry = Object.create(null)
    for (const item of REQ_DUP_CRITERIA)
      entry[item.key] = countDuplicateRequests(row, pool, item.key)
    map[id] = entry
  }

  return map
})

function requestDupCount(row, category) {
  const id = requestDupKey(row)
  const cached = requestDupCountsByKey.value[id]

  return cached?.[category] ?? 0
}

function openRequestDuplicates(row, category, title) {
  const hits = findDuplicateRequests(row, duplicateSearchPool(), category)
  requestDupPanels.value = {
    ...requestDupPanels.value,
    [requestDupKey(row)]: { criterion: category, title, hits },
  }
}

function closeRequestDuplicates(row) {
  const next = { ...requestDupPanels.value }
  delete next[requestDupKey(row)]
  requestDupPanels.value = next
}

async function jumpToDuplicateRequest(hit) {
  if (!hit)
    return
  tab.value = 'all'
  listSearchQuery.value = ''
  await nextTick()
  scrollToDomId(requestDomId(hit))
}

function tabCount(value) {
  return tabCounts.value[value] ?? 0
}

const tabCounts = computed(() => {
  const counts = Object.create(null)
  for (const item of tabs)
    counts[item.value] = Number(serverTabCounts.value?.[item.value] ?? 0)
  counts.exceptions = exceptionRows.value.length
  if (serverTabCounts.value?.deleted != null)
    counts.deleted = Number(serverTabCounts.value.deleted)

  return counts
})

function unwrapPage(data) {
  if (Array.isArray(data)) {
    return {
      items: data,
      total: data.length,
      page: 1,
      pageSize: data.length,
      hasMore: false,
      tabCounts: {},
    }
  }
  const items = Array.isArray(data?.items)
    ? data.items
    : (Array.isArray(data?.Items) ? data.Items : [])
  const tabCountsRaw = data?.tabCounts || data?.TabCounts || {}
  const normalizedCounts = Object.create(null)
  for (const [k, v] of Object.entries(tabCountsRaw))
    normalizedCounts[String(k).toLowerCase()] = Number(v) || 0

  return {
    items,
    total: Number(data?.total ?? data?.Total ?? items.length) || 0,
    page: Number(data?.page ?? data?.Page ?? 1) || 1,
    pageSize: Number(data?.pageSize ?? data?.PageSize ?? listPageSize) || listPageSize,
    hasMore: !!(data?.hasMore ?? data?.HasMore),
    tabCounts: normalizedCounts,
  }
}

function buildRequestsQuery({ page, tabKey, q }) {
  const params = new URLSearchParams()
  params.set('page', String(page))
  params.set('pageSize', String(listPageSize))
  if (tabKey && tabKey !== 'deleted' && tabKey !== 'exceptions')
    params.set('tab', tabKey)
  if (q)
    params.set('q', q)
  const city = String(cityValue.value || '').trim()
  if (city)
    params.set('cityValue', city)

  return params.toString()
}

async function load(options = {}) {
  const force = !!options.force
  const append = !!options.append
  const gen = ++loadGeneration
  const city = String(cityValue.value || '').trim()

  if (showingExceptions.value) {
    listLoading.value = true
    try {
      await loadExceptions()
      // Still refresh tab badges cheaply via page=1 all (small).
      const badge = unwrapPage(await smGet(`sales-requests?${buildRequestsQuery({ page: 1, tabKey: 'all', q: '' })}`))
      if (gen === loadGeneration)
        serverTabCounts.value = { ...serverTabCounts.value, ...badge.tabCounts }
    }
    finally {
      if (gen === loadGeneration)
        listLoading.value = false
    }

    return
  }

  if (!append) {
    listPage.value = 1
    listLoading.value = true
  }
  else {
    loadingMore.value = true
  }

  try {
    const page = append ? listPage.value + 1 : 1
    const q = String(listSearchApplied.value || '').trim()
    // Search spans all statuses (same as previous client behavior).
    const tabKey = showingDeleted.value
      ? 'deleted'
      : (isListSearching.value ? 'all' : tab.value)
    const path = showingDeleted.value
      ? `sales-requests/deleted?${buildRequestsQuery({ page, tabKey: 'deleted', q })}`
      : `sales-requests?${buildRequestsQuery({ page, tabKey, q })}`

    // الكل (كل المحافظات): fan-out يحتاج وقت أطول من محافظة واحدة
    const timeoutMs = city ? 45000 : 120000
    const data = await smGet(path, { timeoutMs })
    if (gen !== loadGeneration)
      return

    const pageData = unwrapPage(data)
    if (showingDeleted.value) {
      deletedRows.value = append
        ? [...deletedRows.value, ...pageData.items]
        : pageData.items
    }
    else {
      rows.value = append
        ? [...rows.value, ...pageData.items]
        : pageData.items
    }

    listPage.value = pageData.page
    listTotal.value = pageData.total
    hasMore.value = pageData.hasMore
      || (pageData.page * pageData.pageSize < pageData.total)
    if (Object.keys(pageData.tabCounts).length)
      serverTabCounts.value = { ...serverTabCounts.value, ...pageData.tabCounts }

    // Store under the city captured at request start — never the UI city after a switch.
    cityListCache.set(city, rows.value, deletedRows.value)
    void refreshSalesRequestUnread(city)
    // تقييم البرنامج والبحث قراءة فقط — المدير يرى التطابقات مثل مدير المبيعات
    if (!showingDeleted.value)
      scheduleEvaluations()
    pruneBulkSelection()
  }
  catch (err) {
    if (gen !== loadGeneration)
      return
    if (!append) {
      rows.value = []
      if (showingDeleted.value)
        deletedRows.value = []
    }
    toast.error(smErrorMessage(err, 'تعذر تحميل طلبات البيع'))
  }
  finally {
    if (gen === loadGeneration) {
      listLoading.value = false
      loadingMore.value = false
    }
  }
}

async function loadMore() {
  if (!hasMore.value || loadingMore.value || listLoading.value)
    return
  await load({ append: true })
}

function scheduleEvaluations() {
  if (showingDeleted.value || showingExceptions.value)
    return
  const list = visibleRows.value
  // Only evaluate the currently loaded page window.
  void loadEvaluations(capEvaluationRows(list, 24))
}

function invalidateListCache(city = cityValue.value) {
  cityListCache.invalidate(city)
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
  const target = row || detail.value || selected.value
  if (hasExceptionHold(target) || hasActiveExceptionInList(target)) {
    toast.error('يوجد طلب استثناء نشط لهذا الطلب')
    exceptionStatus.value = 'Pending'
    tab.value = 'exceptions'
    loadExceptions()
    return
  }
  exceptionTarget.value = target
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
    // Optimistic: hide from «غير مسند» immediately (hold Pending).
    const rid = Number(requestId(row) || 0)
    if (rid > 0) {
      rows.value = rows.value.map(r => (
        Number(requestId(r)) === rid
          ? { ...r, exceptionHoldStatus: 'Pending', ExceptionHoldStatus: 'Pending' }
          : r
      ))
    }
    exceptionOpen.value = false
    exceptionReason.value = ''
    exceptionTarget.value = null
    toast.success('تم إرسال طلب الاستثناء — اختفى من غير مسند وينتظر موافقة المدير المفوض')
    exceptionStatus.value = 'Pending'
    tab.value = 'exceptions'
    await Promise.all([load({ force: true }), loadExceptions()])
  }
  catch (err) {
    const status = err?.response?.status
    const data = err?.response?.data || {}
    const existingId = data.existingExceptionId || data.ExistingExceptionId
    if (status === 409) {
      const rid = Number(requestId(row) || 0)
      if (rid > 0) {
        rows.value = rows.value.map(r => (
          Number(requestId(r)) === rid
            ? {
                ...r,
                exceptionHoldStatus: 'Pending',
                ExceptionHoldStatus: 'Pending',
                activeExceptionId: existingId || pick(r, 'activeExceptionId', 'ActiveExceptionId'),
                ActiveExceptionId: existingId || pick(r, 'activeExceptionId', 'ActiveExceptionId'),
              }
            : r
        ))
      }
      exceptionOpen.value = false
      exceptionReason.value = ''
      exceptionTarget.value = null
      exceptionStatus.value = 'Pending'
      tab.value = 'exceptions'
      toast.error(smErrorMessage(err, 'يوجد طلب استثناء نشط لهذا الطلب'))
      await Promise.all([load({ force: true }), loadExceptions()])
      return
    }
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
  requestDupPanels.value = {}
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
  // Prefer gateway DatabaseCompany* so chips match evaluate/hits payloads.
  return buildEvaluationKey(gatewayCityKey(row) || requestCityOf(row), requestIdOf(row))
}

function evaluationOf(row) {
  return lookupEvaluation(evaluations.value, row)
}

function ratingColor(label) {
  const t = String(label || '')
  if (t.includes('سيء'))
    return 'error'
  if (t.includes('متلكئ') || t.includes('مرفوض'))
    return 'warning'
  if (t.includes('ضعيف'))
    return 'grey'
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
      sourceCityValue: gatewayCityKey(row) || requestCityOf(row),
      customerName: pick(row, 'customerName', 'CustomerName') || '',
      customerPhone: pick(row, 'customerPhone', 'CustomerPhone') || '',
      category,
      page: 1,
      pageSize: 40,
    }, { timeoutMs: 120000 })
    const items = res?.items || res?.Items || []
    const total = res?.total ?? res?.Total ?? items.length
    hitsCache.value = { ...hitsCache.value, [cacheKey]: { items, total } }
    evalPanels.value = {
      ...evalPanels.value,
      [key]: { ...panel, hits: items, total, loading: false, error: '' },
    }
    // Keep metric chips in sync with the hits list (fixes stale "0 نتيجة").
    const prev = evaluationOf(row) || {
      requestId: requestIdOf(row),
      RequestId: requestIdOf(row),
      sourceCityValue: gatewayCityKey(row) || requestCityOf(row),
      key,
    }
    const patched = patchCategoryFromHits(prev, category, total, items[0])
    evaluations.value = {
      ...evaluations.value,
      ...indexEvaluationItems([patched], [row]),
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
  const gen = ++evalGeneration
  const cityAtStart = String(cityValue.value || '').trim()
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
        sourceCityValue: gatewayCityKey(row) || requestCityOf(row),
        customerName: pick(row, 'customerName', 'CustomerName') || '',
        customerPhone: pick(row, 'customerPhone', 'CustomerPhone') || '',
        key: buildEvaluationKey(gatewayCityKey(row) || requestCityOf(row), id),
      })
    }
    if (!payloadItems.length) {
      if (gen === evalGeneration)
        evaluations.value = {}

      return
    }
    const res = await smPost('sales-requests/evaluate', { items: payloadItems }, { timeoutMs: 120000 })
    if (gen !== evalGeneration || String(cityValue.value || '').trim() !== cityAtStart)
      return
    const evalItems = res?.items || res?.Items || []
    // Replace for this province load — never merge leftovers from another city.
    evaluations.value = indexEvaluationItems(evalItems, list)
  }
  catch (err) {
    if (gen !== evalGeneration)
      return
    evaluations.value = {}
    evaluationsError.value = err?.response?.data?.message || 'تعذر تحميل تقييم البرنامج والبحث'
  }
  finally {
    if (gen === evalGeneration)
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

function profilePayments(profile) {
  const list = profile?.payments || profile?.Payments
  if (!Array.isArray(list))
    return []

  return list
}

function paymentAmount(pay) {
  const n = Number(
    pick(pay, 'amount', 'Amount', 'amountDenar', 'AmountDenar') ?? 0,
  )
  return Number.isFinite(n) ? n : 0
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
    await load({ force: true })
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر التعديل')
  }
  finally {
    editBusy.value = false
  }
}

async function confirmDelete(row) {
  deleteTarget.value = row
  deleteReason.value = ''
  deleteImpact.value = null
  deleteConfirmPayments.value = false
  deleteOpen.value = true
  deleteImpactBusy.value = true
  try {
    const city = gatewayCityKey(row)
    const id = requestId(row)
    const path = isDemo() || !city
      ? `sales-requests/${id}/soft-delete-impact`
      : `sales-requests/${encodeURIComponent(city)}/${id}/soft-delete-impact`
    deleteImpact.value = await smGet(path)
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر تحميل تفاصيل الحذف'))
  }
  finally {
    deleteImpactBusy.value = false
  }
}

function deleteModalMessage() {
  const impact = deleteImpact.value
  if (!impact)
    return 'سيتم نقل الطلب إلى تبويب المحذوفين مع الاحتفاظ بكل البيانات.'
  return impact.warningAr
    || impact.WarningAr
    || 'سيتم نقل الطلب إلى تبويب المحذوفين مع الاحتفاظ بكل البيانات.'
}

function deleteRequiresPaymentConfirm() {
  const impact = deleteImpact.value
  return !!(impact?.requiresPaymentConfirm || impact?.RequiresPaymentConfirm)
}

function deleteCanSubmit() {
  if (!(deleteReason.value || '').trim())
    return false
  if (deleteRequiresPaymentConfirm() && !deleteConfirmPayments.value)
    return false

  return true
}

async function doDelete() {
  const row = deleteTarget.value
  if (!row)
    return
  const reason = (deleteReason.value || '').trim()
  if (!reason) {
    toast.error('سبب الحذف مطلوب')

    return
  }
  if (deleteRequiresPaymentConfirm() && !deleteConfirmPayments.value) {
    toast.error('يجب تأكيد حذف التسديدات والمبيع أولاً')

    return
  }
  deleteBusy.value = true
  try {
    const city = gatewayCityKey(row)
    const id = requestId(row)
    const path = isDemo() || !city
      ? `sales-requests/${id}/soft-delete`
      : `sales-requests/${encodeURIComponent(city)}/${id}/soft-delete`
    await smPost(path, {
      reason,
      cityValue: city,
      confirmCascadePayments: !!deleteConfirmPayments.value,
    })
    clearEvaluationCachesForRow(row)
    deleteOpen.value = false
    deleteTarget.value = null
    deleteReason.value = ''
    deleteImpact.value = null
    deleteConfirmPayments.value = false
    if (detail.value?.id === id)
      detail.value = null
    toast.success('تم حذف الطلب')
    await load({ force: true })
  }
  catch (err) {
    const code = err?.response?.data?.code || err?.response?.data?.Code
    if (code === 'payments_confirm_required') {
      deleteConfirmPayments.value = false
      toast.error(smErrorMessage(err, 'هذا المبيع فيه تسديدات — أكّد الحذف'))
    }
    else {
      toast.error(smErrorMessage(err, 'تعذر الحذف'))
    }
  }
  finally {
    deleteBusy.value = false
  }
}

async function openDetails(row, resetAssign = true, options = {}) {
  const keepExceptionAssign = !!options.keepExceptionAssign
  selected.value = row
  const city = gatewayCityKey(row)
  try {
    detail.value = city
      ? await smGet(`sales-requests/${encodeURIComponent(city)}/${row.id || row.Id}`)
      : row
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
    if (!keepExceptionAssign)
      exceptionAssignId.value = null
    assignCityValue.value = gatewayCityKey(detail.value) || city || ''
    assignEmployeeId.value = null
    legalFollowerUserId.value = null
    legalNote.value = ''
    returnNote.value = ''
    returnOpen.value = false
    managerNote.value = ''
    resetIntake(detail.value)
  }
  await loadEmployees()
  if (canShowAssignPanel(detail.value) || requestStatus(detail.value) === 'Rejected')
    await loadLegalFollowers(assignCityValue.value || city)
  void refreshSalesRequestUnread(cityValue.value)
  // Profile/sale/docs are heavy — load after paint so the dialog opens instantly.
  void loadProfile(row, updated)
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
    maxSalePrice: pick(d, 'maxSalePrice', 'MaxSalePrice') != null
      ? String(pick(d, 'maxSalePrice', 'MaxSalePrice'))
      : '',
  }
}

function requestCity(row = detail.value || selected.value) {
  return pick(row, 'cityValue', 'CityValue') || ''
}

/** Gateway routing key (DatabaseCompany*) — Arabic/legacy cityValue on rows must be resolved. */
function gatewayCityKey(row = detail.value || selected.value) {
  const raw = requestCity(row)
    || pick(row, 'cityName', 'CityName')
    || pick(row, 'customerProvince', 'CustomerProvince')
    || cityValue.value
    || ''
  const hit = findProvince(branches.value, raw)
    || findProvince(branches.value, pick(row, 'cityName', 'CityName'))
    || findProvince(branches.value, pick(row, 'customerProvince', 'CustomerProvince'))
  return hit ? String(hit.value) : String(raw || '')
}

function requestId(row = detail.value || selected.value) {
  return pick(row, 'id', 'Id')
}

function requestActionPath(row, suffix) {
  const id = requestId(row)
  const city = gatewayCityKey(row)
  if (isDemo() || !city)
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
    maxSalePrice: intakeForm.value.maxSalePrice,
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

const suppressListReload = ref(false)

function saveBrowseReturnState(extra = {}) {
  try {
    sessionStorage.setItem(RETURN_KEY, JSON.stringify({
      tab: tab.value,
      city: cityValue.value,
      page: listPage.value,
      q: listSearchQuery.value,
      scrollY: window.scrollY || 0,
      ...extra,
    }))
  }
  catch { /* ignore */ }
}

async function restoreBrowseReturnState() {
  let raw = null
  try {
    raw = sessionStorage.getItem(RETURN_KEY)
    if (raw)
      sessionStorage.removeItem(RETURN_KEY)
  }
  catch { /* ignore */ }
  if (!raw)
    return false
  try {
    const st = JSON.parse(raw)
    suppressListReload.value = true
    try {
      if (st.city != null)
        cityValue.value = String(st.city || '')
      if (st.tab)
        tab.value = st.tab
      if (st.q != null) {
        listSearchQuery.value = String(st.q || '')
        listSearchApplied.value = String(st.q || '')
      }
    }
    finally {
      await nextTick()
      suppressListReload.value = false
    }
    await load({ force: true })
    const targetPage = Math.max(1, Number(st.page) || 1)
    while (listPage.value < targetPage && hasMore.value)
      await loadMore()
    await nextTick()
    if (st.requestId) {
      scrollToDomId(requestDomId({
        id: st.requestId,
        Id: st.requestId,
        cityValue: st.city,
        CityValue: st.city,
      }))
    }
    else if (st.scrollY != null) {
      window.scrollTo(0, Number(st.scrollY) || 0)
    }

    return true
  }
  catch {
    suppressListReload.value = false

    return false
  }
}

async function openCustomerProfile(row, d = null) {
  const source = d || detail.value || row
  const city = requestCity(source) || requestCity(row)
  if (!city) {
    toast.error('حدد المحافظة أولاً')

    return
  }
  saveBrowseReturnState({
    requestId: requestIdOf(source) || requestIdOf(row) || null,
    focusId: pick(source, 'existingCustomerId', 'ExistingCustomerId', 'customerId', 'CustomerId') || null,
  })
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
  saveBrowseReturnState({
    focusId: pick(c, 'customerId', 'CustomerId') || null,
  })
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
  const city = gatewayCityKey(detail.value || selected.value)
    || assignCityValue.value
    || requestCity()
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
  if (!d)
    return
  // Exception assign: request stays New with Approved hold — still assignable via exceptions API.
  if (!exceptionAssignId.value && d.status !== 'New')
    return
  if (exceptionAssignId.value && d.status !== 'New' && employeeIdOf(d) > 0)
    return
  const city = gatewayCityKey(d) || requestCity(d)
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
      await load({ force: true })
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
  const maxRaw = String(intakeForm.value.maxSalePrice || '').replace(/[^\d]/g, '')
  const maxSalePrice = maxRaw ? Number(maxRaw) : null
  if (maxSalePrice != null && (!Number.isFinite(maxSalePrice) || maxSalePrice <= 0)) {
    toast.error('الحد الأقصى لسعر البيع غير صالح')

    return
  }
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
    maxSalePrice,
  }
  busy.value = true
  try {
    detail.value = await smPost(path, payload)
    toast.success('تم إرسال الطلب للموظف')
    await load({ force: true })
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

async function loadLegalFollowers(city) {
  if (!city) {
    legalFollowers.value = []

    return
  }
  try {
    legalFollowers.value = (await smGet(withCityQuery('legal-followups/followers', city)) || []).map(f => ({
      userId: f.userId ?? f.UserId ?? f.followerId ?? f.FollowerId,
      followerName: f.followerName || f.FollowerName || '',
    }))
  }
  catch {
    legalFollowers.value = []
  }
}

async function sendToLegal() {
  const d = detail.value
  if (!d || (!isUnassigned(d) && requestStatus(d) !== 'Rejected'))
    return
  const city = gatewayCityKey(d)
  const id = requestId(d)
  const followerId = Number(legalFollowerUserId.value || 0)
  if (!city || !id) {
    toast.error('حدد المحافظة أولاً')

    return
  }
  if (!followerId) {
    toast.error('اختر المتابع')

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
  if (!(intakeForm.value.address || '').trim()) {
    toast.error('العنوان مطلوب')

    return
  }

  const selectedCustomer = intakeSelected.value
  const existingId = Number(selectedCustomer?.customerId || selectedCustomer?.CustomerId || d.existingCustomerId || d.ExistingCustomerId || 0)

  legalBusy.value = true
  try {
    await smPost('legal-followups', {
      salesRequestId: id,
      followerUserId: followerId,
      note: legalNote.value || null,
      cityValue: city,
      customerName: name,
      customerPhone: intakeForm.value.phone,
      customerAddress: (intakeForm.value.address || '').trim(),
      existingCustomerId: existingId > 0 ? existingId : null,
    })
    toast.success('تم الإرسال للمتابعة القانونية')
    detail.value = null
    tab.value = 'legal'
    await load({ force: true })
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر الإرسال للقانونية'))
  }
  finally {
    legalBusy.value = false
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
  const city = gatewayCityKey({
    cityValue: item.cityValue || item.CityValue,
    cityName: item.cityName || item.CityName,
    customerProvince: item.customerProvince || item.CustomerProvince,
  }) || cityValue.value || ''
  let row = rows.value.find(r => Number(requestId(r)) === salesRequestId
    && String(gatewayCityKey(r) || requestCity(r)) === String(city))
  if (!row) {
    row = rows.value.find(r => Number(requestId(r)) === salesRequestId)
  }
  if (!row) {
    try {
      row = await smGet(`sales-requests/${encodeURIComponent(city)}/${salesRequestId}`)
    }
    catch {
      try {
        row = await smGet(withCityQuery(`sales-requests/${salesRequestId}`, city))
      }
      catch {
        row = {
          id: salesRequestId,
          Id: salesRequestId,
          cityValue: city,
          CityValue: city,
          cityName: item.cityName || item.CityName,
          customerName: item.customerName || item.CustomerName,
          customerPhone: item.customerPhone || item.CustomerPhone,
          status: 'New',
          Status: 'New',
          targetEmployeeId: 0,
          // Keep hold marker so list filters stay correct; assign panel uses exceptionAssignId.
          exceptionHoldStatus: 'Approved',
          ExceptionHoldStatus: 'Approved',
        }
      }
    }
  }
  await openDetails(row, true, { keepExceptionAssign: true })
}

async function sendReturn() {
  const note = returnNote.value.trim()
  if (!note) {
    toast.error('ملاحظة الإعادة مطلوبة')

    return
  }
  const d = detail.value
  if (!d) {
    toast.error('افتح تفاصيل الطلب أولاً')

    return
  }
  if (requestStatus(d) !== 'Rejected') {
    toast.error('يمكن إعادة الطلب المرفوض فقط')

    return
  }
  if (returnEmployeeIdOf(d) <= 0) {
    toast.error('لا يوجد موظف لإعادة الطلب إليه')

    return
  }
  const returnPath = requestActionPath(d, 'return')
  busy.value = true
  try {
    const updated = await smPost(returnPath, { note, reason: note })
    applyRequestRow(updated)
    returnOpen.value = false
    returnNote.value = ''
    detail.value = null
    tab.value = 'incoming'
    toast.success('تمت إعادة الطلب لنفس الموظف — يظهر في الوارد')
    await load({ force: true })
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
  const d = rejectTarget.value || detail.value
  if (!d)
    return
  busy.value = true
  try {
    const updated = await smPost(requestActionPath(d, 'reject'), { reason })
    applyRequestRow(updated)
    rejectOpen.value = false
    rejectReason.value = ''
    rejectTarget.value = null
    toast.success('تم نقل الطلب إلى المحذوفين مع سبب الرفض')
    await load({ force: true })
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
    await load({ force: true })
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر تحديث حالة القراءة')
  }
  finally {
    busy.value = false
  }
}

function canManagerReject(row) {
  if (readOnly.value)
    return false

  return canManagerRejectRequest(row, {
    status: requestStatus(row),
    isEmployeeSubmitted: isEmployeeSubmitted(row),
    isFollowerSubmitted: isFollowerSubmitted(row),
    isDelegateSubmitted: isDelegateSubmitted(row),
  })
}

/** Card CTA: load row for reject API without opening the full detail dialog. */
async function openReject(row) {
  if (!row || !canManagerReject(row))
    return
  selected.value = row
  const city = gatewayCityKey(row)
  try {
    rejectTarget.value = city
      ? await smGet(`sales-requests/${encodeURIComponent(city)}/${row.id || row.Id}`)
      : row
  }
  catch {
    rejectTarget.value = row
  }
  rejectReason.value = ''
  rejectOpen.value = true
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
    await load({ force: true })
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
    await load({ force: true })
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

const canExportRejectDeleteTab = computed(() =>
  tab.value === 'rejected' || tab.value === 'deleted')

const exportRejectDeleteBusy = ref(false)

function rejectOrDeleteActor(row) {
  if (showingDeleted.value)
    return pick(row, 'deletedByName', 'DeletedByName') || '—'

  return pick(row, 'targetEmployeeName', 'TargetEmployeeName')
    || pick(row, 'createdByName', 'CreatedByName')
    || 'موظف المبيعات'
}

function rejectOrDeleteReason(row) {
  return pick(row, 'deleteReason', 'DeleteReason', 'rejectionReason', 'RejectionReason') || '—'
}

function rejectOrDeleteDate(row) {
  return formatIraqDate(
    pick(row, 'deletedAtUtc', 'DeletedAtUtc', 'rejectedAtUtc', 'RejectedAtUtc'),
  ) || '—'
}

async function fetchAllRejectDeleteRows() {
  const q = String(listSearchApplied.value || '').trim()
  const city = String(cityValue.value || '').trim()
  const pageSize = 200
  const all = []
  let page = 1
  let more = true
  while (more) {
    const params = new URLSearchParams()
    params.set('page', String(page))
    params.set('pageSize', String(pageSize))
    if (q)
      params.set('q', q)
    if (city)
      params.set('cityValue', city)
    if (tab.value === 'rejected')
      params.set('tab', 'rejected')

    const path = tab.value === 'deleted'
      ? `sales-requests/deleted?${params}`
      : `sales-requests?${params}`
    const data = await smGet(path, { timeoutMs: 60000 })
    const pageData = unwrapPage(data)
    all.push(...(pageData.items || []))
    more = !!(pageData.hasMore)
      || (page * pageSize < pageData.total)
    page += 1
    if (page > 500)
      break
  }

  return all
}

async function exportRejectDeleteExcel() {
  if (!canExportRejectDeleteTab.value) {
    toast.error('اختر تبويب مرفوض أو المحذوفين أولاً')

    return
  }
  exportRejectDeleteBusy.value = true
  try {
    const list = await fetchAllRejectDeleteRows()
    if (!list.length) {
      toast.error('لا توجد صفوف للتصدير في هذا التبويب')

      return
    }

    const header = ['الاسم', 'المصدر', 'الذي حذف أو رفض', 'رقم الهاتف', 'السبب', 'التاريخ']
    const body = list.map(row => [
      pick(row, 'customerName', 'CustomerName') || '—',
      requestSourceLabel(row),
      rejectOrDeleteActor(row),
      pick(row, 'customerPhone', 'CustomerPhone') || '—',
      rejectOrDeleteReason(row),
      rejectOrDeleteDate(row),
    ])
    const sheet = XLSX.utils.aoa_to_sheet([header, ...body])
    sheet['!cols'] = [
      { wch: 24 },
      { wch: 22 },
      { wch: 20 },
      { wch: 16 },
      { wch: 40 },
      { wch: 14 },
    ]
    const book = XLSX.utils.book_new()
    const sheetName = showingDeleted.value ? 'المحذوفين' : 'المرفوض'
    XLSX.utils.book_append_sheet(book, sheet, sheetName)
    const stamp = formatIraqDate(new Date().toISOString()).replace(/\//g, '-')
    const file = showingDeleted.value
      ? `طلبات_محذوفة_${stamp}.xlsx`
      : `طلبات_مرفوضة_${stamp}.xlsx`
    XLSX.writeFile(book, file)
    toast.success(`تم تصدير ${list.length} صفاً`)
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر تصدير Excel'))
  }
  finally {
    exportRejectDeleteBusy.value = false
  }
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
    await load({ force: true })
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر استيراد الطلبات')
  }
  finally {
    importBusy.value = false
  }
}

onMounted(async () => {
  window.addEventListener('popstate', onEvalPopState)
  // المدير: نفس «الكل» لمدير المبيعات — نفرّغ تفضيل محافظة قديم من جلسة أخرى
  if (readOnly.value) {
    try {
      sessionStorage.removeItem(CITY_PREF_KEY)
    }
    catch { /* ignore */ }
    cityValue.value = ''
  }
  const restored = await restoreBrowseReturnState()
  if (!restored)
    await load({ force: true })
  else if (readOnly.value && !String(cityValue.value || '').trim())
    await load({ force: true })
})
onUnmounted(() => {
  window.removeEventListener('popstate', onEvalPopState)
  if (shopImageUrl.value)
    URL.revokeObjectURL(shopImageUrl.value)
})

watch(tab, value => {
  if (suppressListReload.value)
    return
  if (value !== 'unassigned') {
    bulkSelectedKeys.value = []
    bulkReport.value = null
  }
  else if (!readOnly.value) {
    void ensureBulkEmployees()
  }
  listPage.value = 1
  void load({ force: true })
})

watch(listSearchApplied, () => {
  if (suppressListReload.value)
    return
  listPage.value = 1
  void load({ force: true })
})

watch(exceptionStatus, () => {
  if (showingExceptions.value)
    loadExceptions()
})

watch(cityValue, () => {
  if (suppressListReload.value)
    return
  try {
    sessionStorage.setItem(CITY_PREF_KEY, String(cityValue.value || ''))
  }
  catch {
    /* ignore */
  }
  // Drop evaluation/hits from the previous province before the new list paints.
  clearAllEvaluationCaches()
  evalGeneration += 1
  bulkSelectedKeys.value = []
  bulkReport.value = null
  listPage.value = 1
  bulkEmployees.value = []
  rows.value = []
  deletedRows.value = []
  selected.value = null
  detail.value = null
  if (tab.value === 'unassigned' && !readOnly.value)
    void ensureBulkEmployees()
  void load({ force: true })
})

function rowSelectKey(row) {
  return branchRowKey(row, 'id')
}

function isBulkSelected(row) {
  return bulkSelectedKeys.value.includes(rowSelectKey(row))
}

function pruneBulkSelection() {
  const allowed = new Set(
    rows.value.filter(r => isUnassigned(r)).map(rowSelectKey),
  )
  bulkSelectedKeys.value = bulkSelectedKeys.value.filter(k => allowed.has(k))
}

function toggleBulkSelect(row) {
  if (tab.value !== 'unassigned' || !isUnassigned(row))
    return
  const key = rowSelectKey(row)
  if (bulkSelectedKeys.value.includes(key))
    bulkSelectedKeys.value = bulkSelectedKeys.value.filter(k => k !== key)
  else
    bulkSelectedKeys.value = [...bulkSelectedKeys.value, key]
}

function selectAllVisibleUnassigned() {
  const keys = visibleRows.value.filter(r => isUnassigned(r)).map(rowSelectKey)
  bulkSelectedKeys.value = [...new Set([...bulkSelectedKeys.value, ...keys])]
}

function clearBulkSelection() {
  bulkSelectedKeys.value = []
}

const bulkSelectedRows = computed(() =>
  rows.value.filter(r => isBulkSelected(r) && isUnassigned(r)))

const showingBulkBar = computed(() =>
  tab.value === 'unassigned' && bulkSelectedKeys.value.length > 0)

async function ensureBulkEmployees() {
  const city = cityValue.value
    || requestCity(bulkSelectedRows.value[0])
    || ''
  if (!city) {
    bulkEmployees.value = []

    return
  }
  try {
    bulkEmployees.value = (await smGetEmployees(city)).map(row => ({
      ...row,
      employeeId: Number(row.employeeId || row.EmployeeId || 0),
      employeeName: row.employeeName || row.EmployeeName || '',
    }))
  }
  catch {
    bulkEmployees.value = []
  }
}

async function assignBulk() {
  if (bulkBusy.value)
    return
  const selectedRows = bulkSelectedRows.value
  if (!selectedRows.length) {
    toast.error('اختر طلباً واحداً على الأقل')

    return
  }
  const employeeId = Number(bulkEmployeeId.value || 0)
  if (!employeeId) {
    toast.error('اختر موظف المبيعات')

    return
  }
  const cities = [...new Set(selectedRows.map(r => requestCity(r)).filter(Boolean))]
  if (cities.length !== 1) {
    toast.error('الإسناد الجماعي لمحافظة واحدة فقط — صفِّ المحافظة أولاً')

    return
  }
  const city = cities[0]
  const employee = bulkEmployees.value.find(e => Number(e.employeeId) === employeeId)
  bulkBusy.value = true
  bulkReport.value = null
  try {
    const res = await assignSalesRequestsBatch({
      cityValue: city,
      cityName: employee?.cityName || employee?.branchName || '',
      employeeId,
      employeeName: employee?.employeeName,
      requestIds: selectedRows.map(r => Number(requestId(r))),
    })
    const succeeded = res?.succeeded || res?.Succeeded || []
    const failed = res?.failed || res?.Failed || []
    bulkReport.value = {
      succeeded: succeeded.map(Number),
      failed: failed.map(f => ({
        id: Number(f.id ?? f.Id),
        message: f.message || f.Message || 'فشل',
      })),
    }
    if (succeeded.length && !failed.length)
      toast.success(`تم إسناد ${succeeded.length} طلباً`)
    else if (succeeded.length)
      toast.success(`نجح ${succeeded.length} — فشل ${failed.length}`)
    else
      toast.error('تعذر إسناد أي طلب')
    clearBulkSelection()
    await load({ force: true })
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر الإسناد الجماعي'))
  }
  finally {
    bulkBusy.value = false
  }
}
</script>

<template>
  <div>
    <div class="d-flex justify-space-between mb-4 flex-wrap gap-2">
      <div>
        <h4 class="mb-1">طلبات البيع</h4>
        <VChip
          v-if="readOnly"
          size="small"
          color="info"
          variant="tonal"
        >
          عرض فقط — بدون تعديل أو إسناد
        </VChip>
      </div>
      <div
        v-if="!readOnly"
        class="d-flex gap-2 flex-wrap"
      >
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
    <div class="sales-requests-sticky-filters">
      <VRow class="mb-3 align-center">
        <VCol
          cols="12"
          md="3"
        >
          <SalesBranchFilter
            v-model="cityValue"
            @change="load"
          />
        </VCol>
        <VCol
          cols="12"
          md="5"
        >
          <VTextField
            v-model="listSearchQuery"
            label="بحث في طلبات البيع (اسم أو رقم هاتف)"
            placeholder="مثال: علي حسن أو 0780…"
            clearable
            hide-details
            density="comfortable"
            prepend-inner-icon="tabler-search"
          />
        </VCol>
        <VCol
          cols="12"
          md="4"
        >
          <VBtn
            block
            size="x-large"
            color="success"
            height="48"
            class="text-h6 font-weight-bold"
            :loading="exportRejectDeleteBusy"
            @click="exportRejectDeleteExcel"
          >
            تصدير إلى Excel
          </VBtn>
          <div
            v-if="!canExportRejectDeleteTab"
            class="text-caption text-medium-emphasis mt-1"
          >
            اختر تبويب مرفوض أو المحذوفين ثم صدّر
          </div>
        </VCol>
      </VRow>
      <VChipGroup
        v-model="tab"
        column
        class="sales-requests-status-chips"
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
    </div>
    <VAlert
      v-if="listLoading && !visibleRows.length"
      class="mb-3"
      type="info"
      variant="tonal"
      density="compact"
    >
      جاري تحميل طلبات البيع{{ cityValue ? '' : ' (كل المحافظات)' }}…
    </VAlert>
    <VAlert
      v-if="isListSearching"
      class="mb-3"
      type="info"
      variant="tonal"
      density="compact"
    >
      نتائج البحث: {{ listTotal }} طلب — تشمل كل الحالات.
    </VAlert>
    <p
      v-if="!showingExceptions && listTotal > 0"
      class="text-medium-emphasis text-caption mb-2"
    >
      عرض {{ visibleRows.length }} من {{ listTotal }} طلب
      <span v-if="!cityValue"> — كل المحافظات</span>
    </p>

    <VAlert
      v-if="tab === 'unassigned' && !readOnly"
      class="mt-3"
      type="info"
      variant="tonal"
      density="compact"
    >
      يمكنك تحديد عدة طلبات غير مسندة وإسنادها لنفس الموظف دفعة واحدة — كل طلب يبقى منفصلاً.
      <VBtn
        class="ms-2"
        size="small"
        variant="text"
        @click="selectAllVisibleUnassigned"
      >
        تحديد الكل الظاهر
      </VBtn>
    </VAlert>

    <VCard
      v-if="showingBulkBar && !readOnly"
      class="mt-3 sticky-bulk-bar"
      variant="outlined"
    >
      <VCardText class="d-flex flex-wrap align-center gap-3 py-3">
        <strong>محدد: {{ bulkSelectedRows.length }}</strong>
        <VSelect
          v-model="bulkEmployeeId"
          :items="bulkEmployees"
          item-title="employeeName"
          item-value="employeeId"
          label="موظف المبيعات"
          density="compact"
          hide-details
          style="min-inline-size: 220px; max-inline-size: 320px;"
          @focus="ensureBulkEmployees"
        />
        <VBtn
          color="primary"
          :loading="bulkBusy"
          :disabled="!bulkEmployeeId"
          @click="assignBulk"
        >
          إسناد جماعي
        </VBtn>
        <VBtn
          variant="text"
          :disabled="bulkBusy"
          @click="clearBulkSelection"
        >
          إلغاء التحديد
        </VBtn>
      </VCardText>
    </VCard>

    <VAlert
      v-if="bulkReport"
      class="mt-3"
      :type="bulkReport.failed.length ? 'warning' : 'success'"
      variant="tonal"
      closable
      @click:close="bulkReport = null"
    >
      نجح {{ bulkReport.succeeded.length }} —
      فشل {{ bulkReport.failed.length }}
      <ul
        v-if="bulkReport.failed.length"
        class="mt-2 mb-0"
      >
        <li
          v-for="f in bulkReport.failed"
          :key="f.id"
        >
          طلب #{{ f.id }}: {{ f.message }}
        </li>
      </ul>
    </VAlert>

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
                مرفوض — مغلق ولا يعود إلى غير مسند ولا يمكن الإسناد
              </VAlert>
              <VAlert
                v-else-if="String(item.status || item.Status) === 'Pending'"
                class="mt-3"
                type="warning"
                variant="tonal"
                density="compact"
              >
                بانتظار موافقة المدير المفوض — لا يمكن تحديد موظف الآن
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
            'bulk-selected': tab === 'unassigned' && isBulkSelected(row),
          }"
          @click="showingDeleted ? undefined : openDetails(row)"
        >
          <VCardText>
            <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-3">
              <div class="d-flex flex-wrap align-center gap-2">
                <VCheckbox
                  v-if="!readOnly && tab === 'unassigned' && isUnassigned(row)"
                  :model-value="isBulkSelected(row)"
                  hide-details
                  density="compact"
                  class="flex-grow-0"
                  @click.stop.prevent="toggleBulkSelect(row)"
                />
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
              <div
                v-if="!showingDeleted && !readOnly"
                class="d-flex flex-wrap gap-1"
                @click.stop
              >
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
                <VBtn
                  v-if="canManagerReject(row)"
                  size="small"
                  variant="tonal"
                  color="error"
                  @click="openReject(row)"
                >
                  رفض
                </VBtn>
                <VBtn size="small" variant="tonal" color="error" @click="confirmDelete(row)">حذف</VBtn>
              </div>
              <div
                v-else-if="!showingDeleted && readOnly"
                class="d-flex flex-wrap gap-1"
                @click.stop
              >
                <VBtn size="small" variant="tonal" color="primary" @click="openDetails(row)">عرض التفاصيل</VBtn>
              </div>
            </div>

            <VRow dense class="mb-2">
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">اسم الزبون</div><strong>{{ row.customerName || row.CustomerName || '—' }}</strong></VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">الهاتف</div>{{ row.customerPhone || row.CustomerPhone || '—' }}</VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">العنوان</div>{{ row.customerAddress || row.CustomerAddress || '—' }}</VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">المحافظة</div>{{ displayCityName(row, branches) || row.customerProvince || row.CustomerProvince || '—' }}</VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">نوع المبيع</div>{{ saleRequestTypeLabel(row) }}</VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">المصدر</div>{{ requestSourceLabel(row) }}</VCol>
              <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">تاريخ الطلب</div>{{ formatIraqDate(row.createdAtUtc || row.CreatedAtUtc) }} {{ formatIraqTime(row.createdAtUtc || row.CreatedAtUtc) }}</VCol>
              <VCol v-if="lastNote(row)" cols="12"><div class="text-medium-emphasis text-caption">الملاحظة / ماذا يريد</div>{{ lastNote(row) }}</VCol>
              <template v-if="showingDeleted">
                <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">تاريخ الحذف</div>{{ formatIraqDate(row.deletedAtUtc || row.DeletedAtUtc) }} {{ formatIraqTime(row.deletedAtUtc || row.DeletedAtUtc) }}</VCol>
                <VCol cols="12" sm="6"><div class="text-medium-emphasis text-caption">حُذف بواسطة</div>{{ row.deletedByName || row.DeletedByName || '—' }}</VCol>
                <VCol cols="12"><div class="text-medium-emphasis text-caption">{{ String(row.status || row.Status) === 'Rejected' ? 'سبب الرفض' : 'سبب الحذف' }}</div><strong class="text-error">{{ row.deleteReason || row.DeleteReason || row.rejectionReason || row.RejectionReason || '—' }}</strong></VCol>
              </template>
            </VRow>

            <template v-if="!showingDeleted">
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
                      <div class="mt-3 font-weight-bold">
                        تفاصيل التسديدات
                      </div>
                      <div
                        v-if="!profilePayments(panelOf(row).profile).length"
                        class="text-medium-emphasis"
                      >
                        لا توجد تسديدات مسجلة لهذا الزبون.
                      </div>
                      <VTable
                        v-else
                        density="compact"
                        class="mt-1"
                      >
                        <thead>
                          <tr>
                            <th>التاريخ</th>
                            <th>مبلغ التسديد</th>
                            <th>رقم الوصل</th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr
                            v-for="(pay, idx) in profilePayments(panelOf(row).profile)"
                            :key="pick(pay, 'customerPaymentId', 'CustomerPaymentId') || idx"
                          >
                            <td>{{ formatSaleDate(pick(pay, 'paymentDate', 'PaymentDate')) }}</td>
                            <td>{{ moneyShort(paymentAmount(pay)) }}</td>
                            <td>{{ pick(pay, 'boundNumber', 'BoundNumber') || '—' }}</td>
                          </tr>
                        </tbody>
                      </VTable>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <VDivider class="my-3" />
            <div class="text-subtitle-1 font-weight-bold mb-2">بحث داخل طلبات البيع</div>
            <div class="text-caption text-medium-emphasis mb-2">
              يبحث في الطلبات المضافة عندك (مرفوض، غير مسند، تم البيع…) لكشف التكرار.
            </div>
            <div class="eval-metrics">
              <button
                v-for="item in REQ_DUP_CRITERIA"
                :key="`req-dup-${item.key}`"
                type="button"
                class="eval-metric"
                :class="{ 'eval-metric--active': requestDupPanelOf(row)?.criterion === item.key }"
                @click.stop="openRequestDuplicates(row, item.key, item.title)"
              >
                <div class="text-caption text-medium-emphasis">{{ item.title }}</div>
                <div class="eval-metric__count text-primary">
                  {{ requestDupCount(row, item.key) }}
                  نتيجة
                </div>
                <VChip
                  size="x-small"
                  class="mt-1"
                  :color="requestDupCount(row, item.key) > 0 ? 'warning' : 'secondary'"
                >
                  {{ requestDupCount(row, item.key) > 0 ? 'يوجد تطابق' : 'لا توجد نتائج' }}
                </VChip>
              </button>
            </div>

            <div
              v-if="requestDupPanelOf(row)"
              class="eval-results mt-3"
              @click.stop
            >
              <div class="d-flex align-center justify-space-between gap-2 mb-2">
                <div class="text-subtitle-2 mb-0">
                  نتائج {{ requestDupPanelOf(row).title }}
                  ({{ (requestDupPanelOf(row).hits || []).length }})
                </div>
                <VBtn
                  size="small"
                  variant="text"
                  @click="closeRequestDuplicates(row)"
                >
                  إغلاق
                </VBtn>
              </div>
              <div
                v-if="!(requestDupPanelOf(row).hits || []).length"
                class="text-medium-emphasis text-body-2"
              >
                لا توجد طلبات مطابقة
              </div>
              <div
                v-else
                class="eval-hit-list"
              >
                <div
                  v-for="hit in requestDupPanelOf(row).hits"
                  :key="requestDupKey(hit)"
                  class="eval-hit-card"
                >
                  <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-2">
                    <div class="font-weight-medium">
                      طلب #{{ hit.id || hit.Id }} —
                      {{ hit.customerName || hit.CustomerName || '—' }}
                    </div>
                    <div class="d-flex flex-wrap gap-1">
                      <VChip
                        size="small"
                        :color="statusColor(requestStatus(hit))"
                      >
                        {{ statusText(hit) }}
                      </VChip>
                      <VChip
                        size="small"
                        color="primary"
                        variant="tonal"
                      >
                        {{ displayCityName(hit, branches) }}
                      </VChip>
                    </div>
                  </div>
                  <div class="text-body-2">
                    <div>الهاتف: {{ hit.customerPhone || hit.CustomerPhone || '—' }}</div>
                    <div>العنوان: {{ hit.customerAddress || hit.CustomerAddress || '—' }}</div>
                    <div>المصدر: {{ requestSourceLabel(hit) }}</div>
                    <div>نوع المبيع: {{ saleRequestTypeLabel(hit) }}</div>
                    <div>
                      تاريخ الطلب:
                      {{ formatIraqDate(hit.createdAtUtc || hit.CreatedAtUtc) }}
                      {{ formatIraqTime(hit.createdAtUtc || hit.CreatedAtUtc) }}
                    </div>
                    <div v-if="lastNote(hit)">الملاحظة: {{ lastNote(hit) }}</div>
                    <div v-if="hit.deleteReason || hit.DeleteReason">
                      سبب الحذف: {{ hit.deleteReason || hit.DeleteReason }}
                    </div>
                  </div>
                  <div class="d-flex gap-2 mt-2">
                    <VBtn
                      size="small"
                      color="primary"
                      variant="tonal"
                      @click="jumpToDuplicateRequest(hit)"
                    >
                      الانتقال للكارت
                    </VBtn>
                  </div>
                </div>
              </div>
            </div>

            <VBtn
              v-if="!showingDeleted && (requestStatus(row) === 'Completed' || requestStatus(row) === 'Inspected')"
              class="mt-3"
              color="primary"
              size="small"
              @click.stop="openCustomerProfile(row)"
            >
              فتح بروفايل الزبون
            </VBtn>
            </template>
          </VCardText>
        </VCard>
      </VCol>
      <VCol
        v-if="!showingExceptions && hasMore"
        cols="12"
        class="text-center py-4"
      >
        <VBtn
          color="primary"
          variant="tonal"
          size="large"
          :loading="loadingMore"
          @click="loadMore"
        >
          تحميل المزيد ({{ visibleRows.length }} / {{ listTotal }})
        </VBtn>
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

          <template v-if="canShowAssignPanel(detail)">
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
            <VTextField
              v-model="intakeForm.maxSalePrice"
              class="mb-4"
              label="الحد الأقصى لسعر البيع (اختياري)"
              variant="outlined"
              hide-details="auto"
              density="comfortable"
              inputmode="numeric"
              hint="موظف المبيعات لا يستطيع تجاوز هذا السعر"
              persistent-hint
              suffix="د.ع"
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
            <VDivider class="my-4" />
            <div class="font-weight-bold mb-4">
              أو إرسال للمتابعة القانونية
            </div>
            <VSelect
              v-model="legalFollowerUserId"
              class="mb-4"
              :items="legalFollowers"
              item-title="followerName"
              item-value="userId"
              label="المتابع"
              variant="outlined"
              hide-details="auto"
              density="comfortable"
            />
            <VTextarea
              v-model="legalNote"
              class="mb-4"
              label="ملاحظة للمتابع (اختياري)"
              variant="outlined"
              rows="2"
              hide-details="auto"
            />
            <VBtn
              class="mt-2"
              color="secondary"
              :loading="legalBusy"
              :disabled="!legalFollowerUserId"
              @click="sendToLegal"
            >
              إرسال للقانونية
            </VBtn>
            <VBtn
              v-if="canManagerReject(detail)"
              class="mt-2 ms-2"
              color="error"
              variant="tonal"
              @click="rejectTarget = detail; rejectOpen = true"
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
              @click="rejectTarget = detail; rejectOpen = true"
            >
              رفض الطلب
            </VBtn>
          </template>

          <template v-if="requestStatus(detail) === 'Rejected'">
            <VDivider class="my-4" />
            <div class="font-weight-bold mb-2">
              سبب الرفض: {{ detail.rejectionReason || detail.RejectionReason || '—' }}
            </div>
            <VBtn
              color="warning"
              :disabled="returnEmployeeIdOf(detail) <= 0"
              :title="returnEmployeeIdOf(detail) <= 0 ? 'لا يوجد موظف لإعادة الطلب إليه' : undefined"
              @click="returnOpen = true"
            >
              إرجاع للموظف
            </VBtn>
            <div class="text-medium-emphasis mt-2 mb-4">
              تُعاد لنفس الموظف: {{ returnEmployeeNameOf(detail) }}
            </div>
            <VDivider class="my-4" />
            <div class="font-weight-bold mb-4">
              أو إرسال للمتابعة القانونية
            </div>
            <VSelect
              v-model="legalFollowerUserId"
              class="mb-4"
              :items="legalFollowers"
              item-title="followerName"
              item-value="userId"
              label="المتابع"
              variant="outlined"
              hide-details="auto"
              density="comfortable"
            />
            <VTextarea
              v-model="legalNote"
              class="mb-4"
              label="ملاحظة للمتابع (اختياري)"
              variant="outlined"
              rows="2"
              hide-details="auto"
            />
            <VBtn
              color="secondary"
              :loading="legalBusy"
              :disabled="!legalFollowerUserId"
              @click="sendToLegal"
            >
              إرسال للقانونية
            </VBtn>
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
                @click="rejectTarget = detail; rejectOpen = true"
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
            @click="detail = null; selected = null; exceptionAssignId = null"
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
          <p class="text-medium-emphasis text-body-2 mb-3">
            سيُنقل الطلب إلى تبويب المحذوفين مع سبب الرفض. تبويب المرفوض يبقى لرفض موظف المبيعات فقط.
          </p>
          <VTextarea
            v-model="rejectReason"
            label="سبب الرفض *"
            auto-grow
          />
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="rejectOpen = false; rejectTarget = null"
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
      max-width="520"
    >
      <VCard>
        <VCardTitle>تأكيد الحذف</VCardTitle>
        <VCardText>
          <div
            v-if="deleteImpactBusy"
            class="mb-3 text-medium-emphasis"
          >
            جاري تحميل تفاصيل الحذف…
          </div>
          <VAlert
            v-else-if="deleteRequiresPaymentConfirm()"
            type="warning"
            variant="tonal"
            class="mb-3"
            density="compact"
          >
            {{ deleteModalMessage() }}
          </VAlert>
          <div
            v-else
            class="mb-3"
          >
            {{ deleteModalMessage() }}
          </div>
          <VCheckbox
            v-if="deleteRequiresPaymentConfirm()"
            v-model="deleteConfirmPayments"
            class="mb-3"
            color="error"
            hide-details
            label="أؤكد حذف التسديدات والمبيع وإرجاع الكميات للمخزن"
          />
          <VTextarea
            v-model="deleteReason"
            label="سبب الحذف"
            variant="outlined"
            rows="3"
            auto-grow
            hide-details="auto"
            :rules="[v => !!(v && String(v).trim()) || 'سبب الحذف مطلوب']"
          />
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
            :disabled="!deleteCanSubmit() || deleteImpactBusy"
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
.sales-request-card.bulk-selected {
  border: 2px solid rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 1px rgba(var(--v-theme-primary), 0.2);
}
.sticky-bulk-bar {
  position: sticky;
  /* Sit below the sticky search/province/status strip when both are visible. */
  top: 7.5rem;
  z-index: 5;
  background: rgb(var(--v-theme-surface));
}
.sales-requests-sticky-filters {
  position: sticky;
  top: 0;
  z-index: 6;
  margin-block-end: 0.75rem;
  padding-block: 0.5rem 0.65rem;
  background: rgb(var(--v-theme-surface));
  border-block-end: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  box-shadow: 0 6px 12px -10px rgba(0, 0, 0, 0.35);
}
.sales-requests-sticky-filters .sales-requests-status-chips {
  margin-block-end: 0;
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
