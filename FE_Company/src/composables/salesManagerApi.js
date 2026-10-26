import axios from 'axios'
import { getAuthHeaders, getToken } from '@/services/tokenService'
import { DEMO_API, DEMO_BRANCH_VALUE, isDemo } from '@/composables/useCities'
import { isCentralSalesManager, salesGatewayBase } from '@/composables/useSalesBranches'

export function parseUtcMillis(value) {
  if (value == null || value === '')
    return Number.NaN
  if (typeof value === 'number')
    return value
  if (value instanceof Date)
    return value.getTime()

  const text = String(value).trim()
  const hasZone = /Z$|[+-]\d{2}:?\d{2}$/i.test(text)
  const normalized = hasZone ? text : `${text.replace(' ', 'T')}Z`

  return Date.parse(normalized)
}

export function shouldMoveMarker(currentCapturedAt, incomingCapturedAt) {
  const incoming = parseUtcMillis(incomingCapturedAt)
  if (Number.isNaN(incoming))
    return false

  const current = parseUtcMillis(currentCapturedAt)
  if (Number.isNaN(current))
    return true

  return incoming > current
}

export function salesManagerBase() {
  if (isDemo())
    return `${DEMO_API}sales-manager/`

  // المدير / المراقب / مدير المبيعات المركزي — دائماً عبر بوابة المبيعات
  // isCentralSalesManager() already includes المراقب + المدير
  if (isCentralSalesManager() || localStorage.getItem('UserType') === 'المدير' || localStorage.getItem('UserType') === 'المراقب') {
    const gw = salesGatewayBase()
    return `${gw}sales-manager/`
  }

  const link = localStorage.getItem('LinkCity') || ''
  return `${link}sales-manager/`
}

export function withCityQuery(path, cityValue) {
  if (!cityValue)
    return path
  const join = path.includes('?') ? '&' : '?'

  return `${path}${join}cityValue=${encodeURIComponent(cityValue)}`
}

export function employeeApiPath(cityValue, employeeId, suffix = '') {
  if (isDemo() || !isCentralSalesManager())
    return `employees/${employeeId}${suffix}`

  return `employees/${encodeURIComponent(cityValue)}/${employeeId}${suffix}`
}

/** Parallel to employeeApiPath — Sales Manager Followers tab (GPS via Delegate proxy). */
export function followerApiPath(cityValue, followerId, suffix = '') {
  if (isDemo() || !isCentralSalesManager())
    return `followers/${followerId}${suffix}`

  return `followers/${encodeURIComponent(cityValue)}/${followerId}${suffix}`
}

export function normalizeEmployeeRows(data) {
  const list = Array.isArray(data)
    ? data
    : Array.isArray(data?.data)
      ? data.data
      : Array.isArray(data?.employees)
        ? data.employees
        : Array.isArray(data?.Employees)
          ? data.Employees
          : []

  return list.map(row => ({
    ...row,
    employeeId: row.employeeId ?? row.EmployeeId,
    employeeName: row.employeeName || row.EmployeeName || '',
    cityValue: row.cityValue || row.CityValue || '',
    cityName: row.cityName || row.CityName || '',
    latestRating: row.latestRating ?? row.LatestRating ?? null,
    ratingLabel: row.ratingLabel || row.RatingLabel || '',
    ratedCustomerCount: row.ratedCustomerCount ?? row.RatedCustomerCount ?? null,
    phantomLatestRating: row.phantomLatestRating ?? row.PhantomLatestRating ?? null,
    phantomRatingLabel: row.phantomRatingLabel || row.PhantomRatingLabel || '',
  }))
}

export async function smGetEmployees(cityValue, extraQuery = '') {
  const extra = String(extraQuery || '').replace(/^\?/, '')
  const apply = path => {
    if (!extra)
      return path

    return `${path}${path.includes('?') ? '&' : '?'}${extra}`
  }

  // Production: one canonical cityValue → one gateway/branch call.
  // Demo may retry alternate keys when the catalog uses DatabaseCompanyNajaf_DEMO.
  const keys = []
  const add = value => {
    if (value == null)
      return
    const key = String(value).trim()
    if (key !== '' && !keys.includes(key))
      keys.push(key)
  }

  add(cityValue)
  if (isDemo()) {
    add(DEMO_BRANCH_VALUE)
    add('najaf-demo')
  }

  // Empty cityValue = fan-out all branches (central manager "الكل")
  if (!keys.length) {
    return normalizeEmployeeRows(await smGet(apply('employees')))
  }

  let last = []
  let lastError = null
  for (const key of keys) {
    try {
      const path = apply(withCityQuery('employees', key))
      last = normalizeEmployeeRows(await smGet(path))

      // Do not fall through to another key after a successful empty branch response
      // in production — that previously could surface Najaf via demo-style retries.
      if (!isDemo())
        return last

      if (last.length)
        return last
    }
    catch (err) {
      lastError = err
      if (!isDemo())
        throw err
    }
  }

  if (lastError)
    throw lastError

  return last
}

export async function smGetLiveLocations(cityValue) {
  const path = withCityQuery('live-locations', cityValue)
  const rows = await smGet(path) || []
  return (Array.isArray(rows) ? rows : []).map(row => ({
    ...row,
    employeeId: row.employeeId ?? row.EmployeeId,
    employeeName: row.employeeName || row.EmployeeName || '',
    cityValue: row.cityValue || row.CityValue || '',
    cityName: row.cityName || row.CityName || '',
    lastLatitude: row.latitude ?? row.Latitude ?? row.lastLatitude ?? row.LastLatitude,
    lastLongitude: row.longitude ?? row.Longitude ?? row.lastLongitude ?? row.LastLongitude,
    lastAccuracy: row.accuracy ?? row.Accuracy ?? row.lastAccuracy ?? row.LastAccuracy,
    lastLocationAt: row.capturedAt ?? row.CapturedAt ?? row.deviceTimestampUtc ?? row.DeviceTimestampUtc ?? row.lastLocationAt ?? row.LastLocationAt,
    locationStatus: row.locationStatus || row.LocationStatus || 'Live',
    shiftStatus: row.shiftStatus || row.ShiftStatus || 'Active',
    shiftId: row.shiftId ?? row.ShiftId,
  }))
}

/** Parallel to normalizeEmployeeRows — Sales Manager Followers tab. */
export function normalizeFollowerRows(data) {
  const list = Array.isArray(data)
    ? data
    : Array.isArray(data?.data)
      ? data.data
      : Array.isArray(data?.followers)
        ? data.followers
        : Array.isArray(data?.Followers)
          ? data.Followers
          : []

  return list.map(row => ({
    ...row,
    followerId: row.followerId ?? row.FollowerId,
    followerName: row.followerName || row.FollowerName || '',
    cityValue: row.cityValue || row.CityValue || '',
    cityName: row.cityName || row.CityName || '',
    latestRating: row.latestRating ?? row.LatestRating ?? null,
    ratingLabel: row.ratingLabel || row.RatingLabel || '',
    ratedCustomerCount: row.ratedCustomerCount ?? row.RatedCustomerCount ?? null,
    phantomLatestRating: row.phantomLatestRating ?? row.PhantomLatestRating ?? null,
    phantomRatingLabel: row.phantomRatingLabel || row.PhantomRatingLabel || '',
  }))
}

/** Parallel to smGetEmployees — Sales Manager Followers tab. */
export async function smGetFollowers(cityValue, extraQuery = '') {
  const extra = String(extraQuery || '').replace(/^\?/, '')
  const apply = path => {
    if (!extra)
      return path

    return `${path}${path.includes('?') ? '&' : '?'}${extra}`
  }

  // Production: one canonical cityValue → one gateway/branch call.
  // Demo may retry alternate keys when the catalog uses DatabaseCompanyNajaf_DEMO.
  const keys = []
  const add = value => {
    if (value == null)
      return
    const key = String(value).trim()
    if (key !== '' && !keys.includes(key))
      keys.push(key)
  }

  add(cityValue)
  if (isDemo()) {
    add(DEMO_BRANCH_VALUE)
    add('najaf-demo')
  }

  // Empty cityValue = fan-out all branches (central manager "الكل")
  if (!keys.length) {
    return normalizeFollowerRows(await smGet(apply('followers')))
  }

  let last = []
  let lastError = null
  for (const key of keys) {
    try {
      const path = apply(withCityQuery('followers', key))
      last = normalizeFollowerRows(await smGet(path))

      // Do not fall through to another key after a successful empty branch response
      // in production — that previously could surface Najaf via demo-style retries.
      if (!isDemo())
        return last

      if (last.length)
        return last
    }
    catch (err) {
      lastError = err
      if (!isDemo())
        throw err
    }
  }

  if (lastError)
    throw lastError

  return last
}

/** Parallel to smGetLiveLocations — Sales Manager Followers tab. */
export async function smGetFollowerLiveLocations(cityValue) {
  const path = withCityQuery('followers/live-locations', cityValue)
  const rows = await smGet(path) || []
  return (Array.isArray(rows) ? rows : []).map(row => ({
    ...row,
    employeeId: row.employeeId ?? row.EmployeeId,
    employeeName: row.employeeName || row.EmployeeName || '',
    cityValue: row.cityValue || row.CityValue || '',
    cityName: row.cityName || row.CityName || '',
    lastLatitude: row.latitude ?? row.Latitude ?? row.lastLatitude ?? row.LastLatitude,
    lastLongitude: row.longitude ?? row.Longitude ?? row.lastLongitude ?? row.LastLongitude,
    lastAccuracy: row.accuracy ?? row.Accuracy ?? row.lastAccuracy ?? row.LastAccuracy,
    lastLocationAt: row.capturedAt ?? row.CapturedAt ?? row.deviceTimestampUtc ?? row.DeviceTimestampUtc ?? row.lastLocationAt ?? row.LastLocationAt,
    locationStatus: row.locationStatus || row.LocationStatus || 'Live',
    shiftStatus: row.shiftStatus || row.ShiftStatus || 'Active',
    shiftId: row.shiftId ?? row.ShiftId,
  }))
}

export function branchRowKey(row, idField = 'employeeId') {
  return `${row.cityValue || row.branchName || ''}:${row[idField]}`
}

export async function smStatsCatalog(cityValue) {
  return smGet(withCityQuery('stats/catalog', cityValue), { timeoutMs: 60000 })
}

export async function smStatsTrend({ cityValue, scope, id, fromDate, toDate }) {
  const params = new URLSearchParams({
    scope: String(scope || 'branch'),
    id: String(id ?? 0),
  })
  if (fromDate)
    params.set('fromDate', fromDate)
  if (toDate)
    params.set('toDate', toDate)

  return smGet(withCityQuery(`stats/trend?${params}`, cityValue), { timeoutMs: 120000 })
}

export async function smStatsRatingCustomers({
  cityValue,
  scope,
  id,
  level,
  fromDate,
  toDate,
  take = 500,
  skip = 0,
  q,
} = {}) {
  const params = new URLSearchParams({
    scope: String(scope || 'branch'),
    id: String(id ?? 0),
    take: String(take),
    skip: String(skip),
  })
  if (level)
    params.set('level', level)
  if (fromDate)
    params.set('fromDate', fromDate)
  if (toDate)
    params.set('toDate', toDate)
  if (q)
    params.set('q', q)

  return smGet(withCityQuery(`stats/rating-customers?${params}`, cityValue), { timeoutMs: 120000 })
}

/** Paths المدير may mutate (educational videos + announcements). */
export function isDirectorWritePath(path) {
  const p = String(path || '').split('?')[0].replace(/^\/+|\/+$/g, '')
  if (p === 'tutorial-videos' || p === 'tutorial-videos/apps')
    return true
  // tutorial-videos/{id}
  if (/^tutorial-videos\/\d+$/.test(p))
    return true
  if (p === 'announcements' || p === 'announcements/apps' || p === 'announcements/upload-image')
    return true
  if (/^announcements\/\d+$/.test(p))
    return true
  if (p === 'committee-tasks')
    return true
  if (/^committee-tasks\/\d+$/.test(p))
    return true
  // assigned-tasks (+ assignee-options, {guid}, {guid}/attachments)
  if (p === 'assigned-tasks' || p === 'assigned-tasks/assignee-options')
    return true
  if (/^assigned-tasks\/[0-9a-fA-F-]{36}$/.test(p))
    return true
  if (/^assigned-tasks\/[0-9a-fA-F-]{36}\/attachments$/.test(p))
    return true

  return false
}

function assertSmWriteAllowed(path) {
  if (localStorage.getItem('UserType') !== 'المدير')
    return
  if (isDirectorWritePath(path))
    return
  const err = new Error('عرض فقط — لا يمكن تنفيذ الإجراء من حساب المدير')
  err.response = { status: 403, data: { message: err.message } }
  throw err
}

/** POST bodies that only read (evaluate / hits) — allowed for المدير. */
export function isDirectorReadPostPath(path) {
  const p = String(path || '').split('?')[0].replace(/^\/+|\/+$/g, '')
  if (p === 'sales-requests/evaluate' || p === 'sales-requests/evaluation-hits')
    return true
  // sales-requests/{cityValue}/evaluate
  if (/^sales-requests\/[^/]+\/evaluate$/.test(p))
    return true
  if (isDirectorWritePath(p))
    return true

  return false
}

export async function smGet(path, { timeoutMs } = {}) {
  const { data } = await axios.get(`${salesManagerBase()}${path}`, {
    headers: getAuthHeaders(),
    timeout: timeoutMs ?? 30000,
  })

  return data
}

export async function smPost(path, body, { timeoutMs } = {}) {
  if (!isDirectorReadPostPath(path))
    assertSmWriteAllowed(path)
  const { data } = await axios.post(`${salesManagerBase()}${path}`, body, {
    headers: getAuthHeaders(),
    timeout: timeoutMs ?? 60000,
  })

  return data
}

/** Bulk-assign unassigned requests to one employee (separate Assign per id). */
export async function assignSalesRequestsBatch({
  cityValue,
  employeeId,
  employeeName,
  requestIds,
  cityName,
} = {}) {
  const ids = (requestIds || []).map(Number).filter(id => id > 0)
  const body = {
    employeeId: Number(employeeId) || 0,
    employeeName,
    cityValue,
    cityName,
    requestIds: ids,
  }
  if (isDemo())
    return smPost('sales-requests/assign-batch', body)

  const city = String(cityValue || '').trim()
  if (!city)
    throw new Error('المحافظة مطلوبة للإسناد الجماعي')

  return smPost(`sales-requests/${encodeURIComponent(city)}/assign-batch`, body)
}
export async function createExceptionRequest({
  cityValue,
  cityName,
  customerId,
  customerName,
  customerPhone,
  salesRequestId,
  reason,
  targetApproverType = 'DelegatedManager',
} = {}) {
  return smPost('exceptions', {
    cityValue,
    cityName,
    customerId,
    customerName,
    customerPhone,
    salesRequestId,
    reason,
    targetApproverType,
  })
}

/** List exception requests for the authenticated sales manager. */
export async function listExceptionRequests({ status, cityValue, page, pageSize } = {}) {
  const params = new URLSearchParams()
  if (status)
    params.set('status', status)
  if (cityValue)
    params.set('cityValue', cityValue)
  if (page != null)
    params.set('page', String(page))
  if (pageSize != null)
    params.set('pageSize', String(pageSize))
  const q = params.toString()

  return smGet(q ? `exceptions?${q}` : 'exceptions')
}

/** Assign original SalesRequest after Approved exception (branch + consume). */
export async function assignExceptionRequest(exceptionId, { employeeId, employeeName } = {}) {
  return smPost(`exceptions/${exceptionId}/assign`, {
    employeeId,
    employeeName,
  })
}

export async function smPut(path, body) {
  assertSmWriteAllowed(path)
  const { data } = await axios.put(`${salesManagerBase()}${path}`, body, { headers: getAuthHeaders() })

  return data
}

export async function smPostForm(path, formData) {
  assertSmWriteAllowed(path)
  const token = getToken()
  const { data } = await axios.post(`${salesManagerBase()}${path}`, formData, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    timeout: 120000,
    maxContentLength: Infinity,
    maxBodyLength: Infinity,
  })

  return data
}

export function smErrorMessage(err, fallback) {
  const data = err?.response?.data
  if (data && typeof data === 'object') {
    if (data.message)
      return data.message
    if (data.title)
      return data.title
  }
  if (typeof data === 'string' && data.trim()) {
    try {
      const parsed = JSON.parse(data)

      return parsed.message || parsed.title || fallback
    }
    catch {
      return fallback
    }
  }
  if (err?.message && err.message !== 'Network Error')
    return err.message

  return fallback
}

export async function smDelete(path) {
  assertSmWriteAllowed(path)
  const { data } = await axios.delete(`${salesManagerBase()}${path}`, { headers: getAuthHeaders() })

  return data
}

export async function smGetBlob(path, { timeoutMs } = {}) {
  const response = await axios.get(`${salesManagerBase()}${path}`, {
    headers: getAuthHeaders(),
    responseType: 'blob',
    timeout: timeoutMs ?? 120000,
    validateStatus: () => true,
  })
  const data = response.data
  if (response.status < 200 || response.status >= 300) {
    let message = `HTTP ${response.status}`
    try {
      if (data instanceof Blob) {
        const text = await data.text()
        const parsed = JSON.parse(text)
        message = parsed?.message || parsed?.Message || message
      }
    }
    catch { /* keep status message */ }
    const err = new Error(message)
    err.response = { status: response.status, data: { message } }
    throw err
  }

  return data
}

export function evaluationLabel(level) {
  const map = {
    1: 'مرفوض',
    2: 'مقبول',
    3: 'جيد',
    4: 'جيد جداً',
    5: 'ممتاز',
  }

  return map[level] || map[String(level)] || level || ''
}

export const locationStatusLabel = {
  Live: 'مباشر',
  Stale: 'الموقع غير محدث',
  Offline: 'بدون اتصال',
  NoLocation: 'بدون موقع',
  NoShift: 'بدون دوام',
}

export const requestStatusLabel = {
  New: 'غير مسند',
  Assigned: 'طلبات البيع',
  Viewed: 'طلبات البيع',
  Pending: 'معلقة',
  PreparedForSale: 'جاهز للبيع',
  InProgress: 'جاهز للبيع',
  Returned: 'طلبات البيع',
  ConvertedToSale: 'جاهز للبيع',
  Inspected: 'تم الكشف',
  Completed: 'تم البيع',
  Rejected: 'تم الرفض',
  LegalFollowup: 'القانونية',
}

export function isInternalCityKey(value, cityValue = '') {
  const text = String(value || '').trim()
  if (!text)
    return true
  const key = String(cityValue || '').trim()
  if (key && text === key)
    return true
  if (/^Database/i.test(text))
    return true

  return /_DEMO$/i.test(text) && !/[\s\u0600-\u06FF]/.test(text)
}

export function displayCityName(row, branches = []) {
  if (!row || typeof row !== 'object')
    return ''
  const key = String(row.cityValue || row.CityValue || row.sourceCityValue || row.SourceCityValue || '').trim()
  const candidates = [
    row.cityName,
    row.CityName,
    row.branchName,
    row.BranchName,
    row.sourceCityName,
    row.SourceCityName,
    row.customerProvince,
    row.CustomerProvince,
    row.province,
    row.Province,
  ]
  for (const candidate of candidates) {
    const text = String(candidate || '').trim()
    if (text && !isInternalCityKey(text, key))
      return text
  }
  const match = (branches || []).find(item =>
    String(item.value) === key
    || String(item.database || item.Database || '') === key)
  return String(match?.name || match?.title || '').trim()
}

export const requestHistoryLabel = {
  Created: 'إنشاء',
  Assigned: 'إسناد',
  Viewed: 'مشاهدة',
  EmployeeSubmitted: 'إرسال من الموظف',
  ManagerViewed: 'مشاهدة المدير',
  Pending: 'تعليق',
  PendingNote: 'ملاحظة تعليق',
  PreparedForSale: 'تجهيز للبيع',
  Rejected: 'رفض',
  RejectionReason: 'سبب الرفض',
  Returned: 'إعادة للموظف',
  ReturnNote: 'ملاحظة الإعادة',
  ConvertedToSale: 'تحويل إلى بيع',
  Inspected: 'تم الكشف',
  Completed: 'اكتمال',
  LegalFollowupSent: 'إرسال للقانونية',
  CancelledToUnassigned: 'إلغاء → غير مسند',
  ManagerRequestDeleted: 'حذف',
}

export function customerProfileApiPath(city, { customerId, name, phone } = {}) {
  if (customerId) {
    if (isDemo() || !isCentralSalesManager())
      return `customers/${customerId}/profile`

    return `customers/${encodeURIComponent(city)}/${customerId}/profile`
  }

  const params = new URLSearchParams()
  if (name)
    params.set('name', name)
  if (phone)
    params.set('phone', phone)

  return isDemo() || !isCentralSalesManager()
    ? `customers/profile?${params}`
    : `customers/${encodeURIComponent(city)}/profile?${params}`
}

export function customerSharedNotesApiPath(city, customerId) {
  if (isDemo() || !isCentralSalesManager())
    return `customers/${customerId}/notes`

  return `customers/${encodeURIComponent(city)}/${customerId}/notes`
}

export function managerCustomerUpdatePath(city) {
  if (isDemo() || !isCentralSalesManager())
    return 'customers/profile'

  return `customers/${encodeURIComponent(city)}/profile`
}

export function managerSalePath(city, saleId, suffix = '') {
  if (isDemo() || !isCentralSalesManager())
    return `sales/${saleId}${suffix}`

  return `sales/${encodeURIComponent(city)}/${saleId}${suffix}`
}

export function managerCustomerDocumentFilePath(city, documentId) {
  if (isDemo() || !isCentralSalesManager())
    return `customer-documents/${documentId}/file`

  return `customer-documents/${encodeURIComponent(city)}/${documentId}/file`
}

export function managerCustomerDocumentUploadPath(city, { type, customerId, name, phone, saleId } = {}) {
  const params = new URLSearchParams()
  if (type)
    params.set('type', type)
  if (customerId)
    params.set('customerId', String(customerId))
  if (name)
    params.set('name', name)
  if (phone)
    params.set('phone', phone)
  if (saleId)
    params.set('saleId', String(saleId))
  const q = params.toString()
  if (isDemo() || !isCentralSalesManager())
    return q ? `customers/documents?${q}` : 'customers/documents'

  return q
    ? `customers/${encodeURIComponent(city)}/documents?${q}`
    : `customers/${encodeURIComponent(city)}/documents`
}

export function managerCustomerDocumentDeletePath(city, documentId) {
  if (isDemo() || !isCentralSalesManager())
    return `customer-documents/${documentId}`

  return `customer-documents/${encodeURIComponent(city)}/${documentId}`
}

export function overrideLabel(value) {
  if (value == null || value === '')
    return 'لم يتم التعديل'

  const n = Number(value)
  if (!Number.isFinite(n))
    return 'لم يتم التعديل'

  return `${n.toLocaleString('en-US')} د.ع`
}
