<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as XLSX from 'xlsx'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { formatIraqDate, formatIraqTime } from '@/composables/iraqDate'
import {
  branchRowKey,
  evaluationLabel,
  managerSalePath,
  requestHistoryLabel,
  requestStatusLabel,
  salesManagerBase,
  smGet,
  smGetBlob,
  smGetEmployees,
  smPost,
  withCityQuery,
  displayCityName,
} from '@/composables/salesManagerApi'
import { saleDisplayDocuments, saleDocumentTitle } from '@/composables/saleDocuments'
import ShopLocationLink from '@/components/ShopLocationLink.vue'
import { isDemo } from '@/composables/useCities'
import { useSalesBranches } from '@/composables/useSalesBranches'
import { useToast } from '@/composables/useToast'
import { markAllSalesRequestsRead, refreshSalesRequestUnread, salesRequestUnreadCount } from '@/composables/useSalesRequestUnread'
import { IRAQ_MOBILE_ERROR, iraqPhoneValidator, isIraqMobile, normalizeIraqPhone } from '@core/utils/validators'

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
const busy = ref(false)
const excelInput = ref(null)
const importOpen = ref(false)
const importBusy = ref(false)
const importPreview = ref(null)
const intakeQuery = ref('')
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
]

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

function requestSourceLabel(row) {
  if (isFollowerSubmitted(row))
    return 'المتابع'
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
  return pick(row, 'createdByName', 'CreatedByName') || 'موظف مبيعات'
}

function matchesTab(row, value = tab.value) {
  const s = requestStatus(row)
  switch (value) {
    case 'unassigned':
      return isUnassigned(row)
    case 'sent':
      // الطلبات المرسلة: متابع أو موظف مبيعات، والحالة New فقط (غير مسند).
      return (isEmployeeSubmitted(row) || isFollowerSubmitted(row)) && s === 'New'
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
  return rows.value.filter(row => matchesTab(row, value)).length
}

async function load() {
  rows.value = await smGet(withCityQuery('sales-requests', cityValue.value)) || []
  await refreshSalesRequestUnread(cityValue.value)
  if (selected.value)
    await openDetails(selected.value, false)
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

function headerKey(value) {
  const t = foldAr(value).replace(/[_\-]/g, ' ').toLowerCase()
  if (['اسم الزبون', 'اسم العميل', 'customername', 'fullname', 'name', 'الاسم'].includes(t))
    return 'name'
  if (['الهاتف', 'هاتف', 'phone', 'phonenumber', 'mobile'].includes(t))
    return 'phone'
  if (t === 'المحافظة' || t === 'المحافظه' || t === 'المدينة' || t === 'المدينه' || ['province', 'city', 'governorate'].includes(t))
    return 'province'
  if (['العنوان', 'address'].includes(t))
    return 'address'
  if (['نوع المبيع', 'نوع البيع', 'saletype', 'sale type'].includes(t))
    return 'saleType'

  return ''
}

function cellText(value) {
  if (value == null)
    return ''

  return String(value).trim()
}

function resolveCity(provinceText) {
  const text = foldAr(provinceText)
  if (text) {
    const match = branches.value.find(p => foldAr(p.name) === text || foldAr(p.value) === text)
    if (match)
      return { cityValue: String(match.value), cityName: match.name }

    return null
  }
  if (cityValue.value) {
    const selected = branches.value.find(p => String(p.value) === String(cityValue.value))

    return {
      cityValue: String(cityValue.value),
      cityName: selected?.name || '',
    }
  }

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
      importPreview.value = parseExcel(e.target.result)
      importOpen.value = true
    }
    catch (err) {
      toast.error(err?.message || 'تعذر قراءة ملف Excel')
    }
  }
  reader.readAsArrayBuffer(file)
}

function parseExcel(buffer) {
  const workbook = XLSX.read(buffer, { type: 'array' })
  const sheet = workbook.Sheets[workbook.SheetNames[0]]
  if (!sheet)
    throw new Error('الملف لا يحتوي على ورقة')
  const rows = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: '', raw: false })
  if (!rows.length)
    throw new Error('الملف فارغ')
  const map = {}
  ;(rows[0] || []).forEach((header, index) => {
    const key = headerKey(header)
    if (key && map[key] == null)
      map[key] = index
  })
  if (map.name == null)
    throw new Error('عمود اسم الزبون مطلوب في الصف الأول')
  const valid = []
  const errors = []
  let total = 0
  for (let i = 1; i < rows.length; i++) {
    const row = rows[i] || []
    const name = cellText(row[map.name])
    const phone = cellText(map.phone != null ? row[map.phone] : '')
    const province = cellText(map.province != null ? row[map.province] : '')
    const address = cellText(map.address != null ? row[map.address] : '')
    const saleType = cellText(map.saleType != null ? row[map.saleType] : '')
    if (![name, phone, province, address, saleType].some(Boolean))
      continue
    total++
    const excelRow = i + 1
    if (!name) {
      errors.push({ rowNumber: excelRow, message: 'اسم الزبون مطلوب' })
      continue
    }
    const city = resolveCity(province)
    if (!city) {
      errors.push({
        rowNumber: excelRow,
        message: province ? `المحافظة غير معروفة بعد التطبيع: ${province}` : 'المحافظة مطلوبة أو حددها من الفلتر',
      })
      continue
    }
    valid.push({
      rowNumber: excelRow,
      customerName: name,
      phone,
      province: province || city.cityName,
      address,
      saleType,
      cityValue: city.cityValue,
      cityName: city.cityName,
    })
  }

  return { total, valid, errors }
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
      const result = await smPost('sales-requests/import', {
        cityValue: city,
        rows: rows.map(r => ({
          rowNumber: r.rowNumber,
          customerName: r.customerName,
          phone: r.phone,
          province: r.province,
          address: r.address,
          saleType: r.saleType,
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

onMounted(load)
onUnmounted(() => {
  if (shopImageUrl.value)
    URL.revokeObjectURL(shopImageUrl.value)
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
          :to="{ name: 'sales-manager-excel-search' }"
        >
          بحث الزبائن من Excel
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
    <VRow class="mt-4">
      <VCol
        v-for="row in visibleRows"
        :key="branchRowKey(row, 'id')"
        cols="12"
        md="6"
      >
        <VCard
          :class="{
            'border-primary': selected?.id === row.id && selected?.cityValue === row.cityValue,
            'unread-request': isUnreadSent(row),
          }"
          @click="openDetails(row)"
        >
          <VCardText>
            <div class="d-flex align-center justify-space-between gap-2 mb-2">
              <div :class="{ 'font-weight-bold': isUnreadSent(row) }">
                طلب #{{ row.id }} — {{ row.branchName || row.cityName }}
                <VChip
                  v-if="isUnreadSent(row)"
                  size="x-small"
                  color="error"
                  class="ms-1"
                >
                  غير مقروء
                </VChip>
              </div>
              <VChip
                size="small"
                :color="statusColor(requestStatus(row))"
              >
                {{ statusText(row) }}
              </VChip>
            </div>
            <strong>{{ row.customerName }}</strong>
            <div>الهاتف: {{ row.customerPhone || row.CustomerPhone || '—' }}</div>
            <div>المحافظة: {{ displayCityName(row, branches) }}</div>
            <div>العنوان: {{ row.customerAddress || row.CustomerAddress || '—' }}</div>
            <div>الموظف: {{ isEmployeeSubmitted(row) ? submittedBy(row) : (row.targetEmployeeName || 'غير مسند') }}</div>
            <div v-if="isFollowerSubmitted(row) || isEmployeeSubmitted(row)">
              المصدر: {{ requestSourceLabel(row) }}
              <template v-if="isFollowerSubmitted(row)"> — أرسل بواسطة: {{ submittedBy(row) }}</template>
            </div>
            <div>التاريخ: {{ formatIraqDate(row.createdAtUtc || row.CreatedAtUtc) }}</div>
            <div>الحالة: {{ statusText(row) }}</div>
            <div v-if="lastNote(row)">
              آخر ملاحظة/سبب: {{ lastNote(row) }}
            </div>
            <div
              v-if="row.rejectionReason || row.RejectionReason"
              class="text-error"
            >
              سبب الرفض: {{ row.rejectionReason || row.RejectionReason }}
            </div>
            <VChip
              v-if="requestStatus(row) === 'Returned'"
              size="small"
              color="warning"
              class="mt-1"
            >
              معاد للموظف
            </VChip>
            <div class="text-medium-emphasis">
              آخر تحديث: {{ lastUpdated(row) }}
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
          <div>الهاتف: {{ detail.customerPhone || '—' }}</div>
          <div>العنوان: {{ detail.customerAddress || '—' }}</div>
          <div>الموظف: {{ isEmployeeSubmitted(detail) ? submittedBy(detail) : (detail.targetEmployeeName || 'غير مسند') }}</div>
          <div v-if="isFollowerSubmitted(detail) || isEmployeeSubmitted(detail)">
            المصدر: {{ requestSourceLabel(detail) }}
            <template v-if="isFollowerSubmitted(detail)"> — أرسل بواسطة: {{ submittedBy(detail) }}</template>
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
</style>
