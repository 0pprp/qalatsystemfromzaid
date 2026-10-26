import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'
import { salesGatewayBase } from '@/composables/useSalesBranches'

function base() {
  return `${salesGatewayBase()}director/`
}

export async function directorGet(path, { timeoutMs } = {}) {
  const { data } = await axios.get(`${base()}${path}`, {
    headers: getAuthHeaders(),
    timeout: timeoutMs ?? 30000,
  })

  return data
}

export async function directorGetBlob(path, { timeoutMs } = {}) {
  const response = await axios.get(`${base()}${path}`, {
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

export async function directorCities() {
  return directorGet('cities')
}

/** Monitor «لوحة الأسعار» — CRUD on gateway. */
export async function directorPriceBoardList(cityValue) {
  const params = new URLSearchParams()
  if (cityValue)
    params.set('cityValue', String(cityValue))
  const q = params.toString()

  return directorGet(`price-board${q ? `?${q}` : ''}`, { timeoutMs: 60000 })
}

export async function directorPriceBoardCreate(body) {
  const { data } = await axios.post(`${base()}price-board`, body, {
    headers: getAuthHeaders(),
    timeout: 30000,
  })

  return data
}

export async function directorPriceBoardUpdate(id, body) {
  const { data } = await axios.put(`${base()}price-board/${id}`, body, {
    headers: getAuthHeaders(),
    timeout: 30000,
  })

  return data
}

export async function directorPriceBoardDelete(id) {
  const { data } = await axios.delete(`${base()}price-board/${id}`, {
    headers: getAuthHeaders(),
    timeout: 30000,
  })

  return data
}

/** Monitor/director view-only «أسماء قانونية» — per-city CustomerDecisions proxies. */
export async function directorLegalWeakWeekPayers(cityValue) {
  const q = encodeURIComponent(String(cityValue || ''))

  return directorGet(`customer-decisions/weak-week-payers?cityValue=${q}`, { timeoutMs: 60000 })
}

export async function directorLegalConversionRequests(cityValue, { status, take = 200, skip = 0 } = {}) {
  const params = new URLSearchParams({
    cityValue: String(cityValue || ''),
    take: String(take),
    skip: String(skip),
  })
  if (status)
    params.set('status', String(status))

  return directorGet(`customer-decisions/legal-conversion-requests?${params}`, { timeoutMs: 60000 })
}

export async function directorLegalConversionApprove(cityValue, requestId, note) {
  const q = encodeURIComponent(String(cityValue || ''))
  const { data } = await axios.put(
    `${base()}customer-decisions/legal-conversion-requests/${requestId}/approve?cityValue=${q}`,
    { note: note || null },
    { headers: getAuthHeaders(), timeout: 60000 },
  )

  return data
}

export async function directorLegalConversionReject(cityValue, requestId, note) {
  const q = encodeURIComponent(String(cityValue || ''))
  const { data } = await axios.put(
    `${base()}customer-decisions/legal-conversion-requests/${requestId}/reject?cityValue=${q}`,
    { note: note || null },
    { headers: getAuthHeaders(), timeout: 60000 },
  )

  return data
}

/** Director «التنازلات» — all statuses from gateway store. */
export async function directorWaiversList({ status, cityValue, page = 1, pageSize = 50 } = {}) {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  })
  if (status)
    params.set('status', String(status))
  if (cityValue)
    params.set('cityValue', String(cityValue))

  return directorGet(`waivers?${params}`, { timeoutMs: 60000 })
}

export async function directorLegalWaiverAccountantRequests(cityValue, { status, take = 200, skip = 0 } = {}) {
  const params = new URLSearchParams({
    cityValue: String(cityValue || ''),
    take: String(take),
    skip: String(skip),
  })
  if (status)
    params.set('status', String(status))

  return directorGet(`customer-decisions/waiver-accountant-requests?${params}`, { timeoutMs: 60000 })
}

export async function directorLegalDecisions(cityValue, { decisionType, fromDate, toDate, customerID } = {}) {
  const params = new URLSearchParams({ cityValue: String(cityValue || '') })
  if (decisionType)
    params.set('decisionType', String(decisionType))
  if (fromDate)
    params.set('fromDate', String(fromDate))
  if (toDate)
    params.set('toDate', String(toDate))
  if (customerID)
    params.set('customerID', String(customerID))

  return directorGet(`customer-decisions/decisions?${params}`, { timeoutMs: 60000 })
}

/** Director inbox: notes from field apps (مندوب / متابع / مدير فرع). */
export async function directorNotesList({ page = 1, pageSize = 50, unreadOnly = false, search } = {}) {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    unreadOnly: unreadOnly ? 'true' : 'false',
  })
  if (search)
    params.set('search', String(search))

  const { data } = await axios.get(`${salesGatewayBase()}director/notes?${params}`, {
    headers: getAuthHeaders(),
    timeout: 30000,
  })

  return data
}

export async function directorNotesUnreadCount() {
  const { data } = await axios.get(`${salesGatewayBase()}director/notes/unread-count`, {
    headers: getAuthHeaders(),
    timeout: 15000,
  })

  return data
}

export async function directorNotesGet(id) {
  const { data } = await axios.get(`${salesGatewayBase()}director/notes/${id}`, {
    headers: getAuthHeaders(),
    timeout: 15000,
  })

  return data
}

export async function directorNotesMarkRead(id) {
  const { data } = await axios.put(`${salesGatewayBase()}director/notes/${id}/read`, {}, {
    headers: getAuthHeaders(),
    timeout: 15000,
  })

  return data
}

export async function directorNotesMarkAllRead() {
  const { data } = await axios.put(`${salesGatewayBase()}director/notes/read-all`, {}, {
    headers: getAuthHeaders(),
    timeout: 15000,
  })

  return data
}

export async function directorStatsCatalog(cityValue) {
  const q = encodeURIComponent(cityValue)

  return directorGet(`stats/catalog?cityValue=${q}`)
}

/** ملخص قلعة الضمان: days×branches matrix (+ قاصة الكلية). */
export async function directorSummaryMatrix({ year, month, metric } = {}) {
  const params = new URLSearchParams({
    year: String(year),
    month: String(month),
    metric: String(metric || 'receipts'),
  })

  return directorGet(`summary/matrix?${params}`, { timeoutMs: 180000 })
}

export async function directorSummaryDrilldown({ cityValue, year, month, day, metric } = {}) {
  const params = new URLSearchParams({
    cityValue: String(cityValue || ''),
    year: String(year),
    month: String(month),
    day: String(day),
    metric: String(metric || 'receipts'),
  })

  return directorGet(`summary/drilldown?${params}`, { timeoutMs: 120000 })
}

/** Current + previous month new/zeroed/net for one province, or all when cityValue omitted. */
export async function directorStatsBranchMonth(cityValue) {
  const params = new URLSearchParams()
  if (cityValue)
    params.set('cityValue', String(cityValue))
  const qs = params.toString()

  return directorGet(`stats/branch-month${qs ? `?${qs}` : ''}`, { timeoutMs: 120000 })
}

export async function directorStatsListPaymentsSum({ cityValue, listId, fromDate, toDate }) {
  const params = new URLSearchParams()
  params.set('cityValue', cityValue)
  params.set('listId', String(listId))
  if (fromDate)
    params.set('fromDate', fromDate)
  if (toDate)
    params.set('toDate', toDate)

  return directorGet(`stats/list-payments-sum?${params}`, { timeoutMs: 60000 })
}

export async function directorStatsTrend({ cityValue, scope, id, fromDate, toDate }) {
  const params = new URLSearchParams({
    cityValue: String(cityValue || ''),
    scope: String(scope || 'branch'),
    id: String(id ?? 0),
  })
  if (fromDate)
    params.set('fromDate', fromDate)
  if (toDate)
    params.set('toDate', toDate)

  // Long periods (up to ~1 year daily points) need more than the default 30s.
  return directorGet(`stats/trend?${params}`, { timeoutMs: 120000 })
}

/** Page-capped bucket names for director تقييم cards (default take=500). */
export async function directorStatsRatingCustomers({
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
    cityValue: String(cityValue || ''),
    scope: String(scope || 'branch'),
    id: String(id ?? 0),
    level: String(level || ''),
    take: String(take),
    skip: String(skip),
  })
  if (fromDate)
    params.set('fromDate', fromDate)
  if (toDate)
    params.set('toDate', toDate)
  if (q)
    params.set('q', q)

  return directorGet(`stats/rating-customers?${params}`, { timeoutMs: 120000 })
}

export function directorCustomerProfilePath(city, customerId) {
  return `customers/${encodeURIComponent(city)}/${customerId}/profile`
}

export function directorCustomerNotesPath(city, customerId) {
  return `customers/${encodeURIComponent(city)}/${customerId}/notes`
}

export function directorSaleShopImagePath(city, saleId) {
  return `sales/${encodeURIComponent(city)}/${saleId}/shop-image`
}

export function directorCustomerDocumentFilePath(city, documentId) {
  return `customer-documents/${encodeURIComponent(city)}/${documentId}/file`
}

function opsCityParams(cityValue) {
  return new URLSearchParams({ cityValue: String(cityValue || '') })
}

/** عمل المتابعين — ملخص أكشنز (بدون فترة) */
export async function directorFollowerOpsSummaries({ cityValue } = {}) {
  return directorGet(`followers/ops/summaries?${opsCityParams(cityValue)}`, { timeoutMs: 120000 })
}

export async function directorFollowerOpsFollowerCards({ cityValue } = {}) {
  return directorGet(`followers/ops/follower-cards?${opsCityParams(cityValue)}`, { timeoutMs: 180000 })
}

export async function directorFollowerOpsWorkBundle({ cityValue, followerId } = {}) {
  return directorGet(
    `followers/ops/${followerId}/work-bundle?${opsCityParams(cityValue)}`,
    { timeoutMs: 180000 },
  )
}

export async function directorFollowerOpsSalesWorkspace({ cityValue, followerId } = {}) {
  return directorGet(
    `followers/ops/${followerId}/sales-workspace?${opsCityParams(cityValue)}`,
    { timeoutMs: 180000 },
  )
}

export async function directorFollowerOpsRemainingCustomers({ cityValue, followerId, bucket } = {}) {
  const params = opsCityParams(cityValue)
  if (bucket)
    params.set('bucket', String(bucket))

  return directorGet(
    `followers/ops/${followerId}/remaining-customers?${params}`,
    { timeoutMs: 180000 },
  )
}

export async function directorFollowerOpsSalesAuth({ cityValue, followerId } = {}) {
  return directorGet(
    `followers/ops/${followerId}/sales-auth?${opsCityParams(cityValue)}`,
    { timeoutMs: 180000 },
  )
}

export async function directorFollowerOpsDeclining({ cityValue, followerId } = {}) {
  return directorGet(`followers/ops/${followerId}/declining?${opsCityParams(cityValue)}`, { timeoutMs: 180000 })
}

export async function directorFollowerOpsNotes({ cityValue, followerId } = {}) {
  return directorGet(`followers/ops/${followerId}/notes?${opsCityParams(cityValue)}`, { timeoutMs: 120000 })
}

/** الزبائن القديمة = قانونية يرسلها مدير المبيعات للمتابع */
export async function directorFollowerOpsLegalFollowups({ cityValue, followerId } = {}) {
  return directorGet(`followers/ops/${followerId}/legal-followups?${opsCityParams(cityValue)}`, { timeoutMs: 120000 })
}

/** إلغاء إرسال زبون قديم بالغلط → غير مسند */
export async function directorFollowerOpsCancelLegalFollowup({ cityValue, caseId, reason } = {}) {
  const params = opsCityParams(cityValue)
  const { data } = await axios.post(
    `${base()}followers/ops/legal-followups/${caseId}/cancel?${params}`,
    { reason: reason || '' },
    { headers: getAuthHeaders(), timeout: 60000 },
  )

  return data
}

export async function directorFollowerOpsDelegateNotes({ cityValue, followerId } = {}) {
  return directorGet(`followers/ops/${followerId}/delegate-notes?${opsCityParams(cityValue)}`, { timeoutMs: 120000 })
}

export async function directorFollowerOpsCollection({ cityValue, followerId } = {}) {
  return directorGet(
    `followers/ops/${followerId}/collection?${opsCityParams(cityValue)}`,
    { timeoutMs: 120000 },
  )
}

export async function directorFollowerOpsSetCollectionEnabled({ cityValue, listId, enabled } = {}) {
  const params = opsCityParams(cityValue)
  const { data } = await axios.put(
    `${base()}followers/ops/lists/${listId}/collection-enabled?${params}`,
    { enabled: !!enabled },
    { headers: getAuthHeaders(), timeout: 30000 },
  )

  return data
}

/** متابعة المندوبين — قوائم نشطة (آخر تسديد ≤ 40 يوماً) */
export async function directorDelegateFollowupLists({ cityValue } = {}) {
  return directorGet(`delegate-followup/lists?${opsCityParams(cityValue)}`, { timeoutMs: 180000 })
}

export async function directorDelegateFollowupListSummary({ cityValue, listId } = {}) {
  return directorGet(
    `delegate-followup/lists/${listId}/summary?${opsCityParams(cityValue)}`,
    { timeoutMs: 120000 },
  )
}

export async function directorDelegateFollowupListCustomers({ cityValue, listId } = {}) {
  return directorGet(
    `delegate-followup/lists/${listId}/customers?${opsCityParams(cityValue)}`,
    { timeoutMs: 180000 },
  )
}

export async function directorDelegateFollowupPayments({ cityValue, customerId } = {}) {
  return directorGet(
    `delegate-followup/payments/${customerId}?${opsCityParams(cityValue)}`,
    { timeoutMs: 60000 },
  )
}

export async function directorDelegateFollowupCreatePayment({ cityValue, payload } = {}) {
  const { data } = await axios.post(
    `${base()}delegate-followup/payments?${opsCityParams(cityValue)}`,
    payload || {},
    { headers: getAuthHeaders(), timeout: 60000 },
  )

  return data
}

export async function directorDelegateFollowupGetBoardOperatorRating({ cityValue, listId } = {}) {
  return directorGet(
    `delegate-followup/lists/${listId}/board-operator-rating?${opsCityParams(cityValue)}`,
    { timeoutMs: 30000 },
  )
}

export async function directorDelegateFollowupUpsertBoardOperatorRating({
  cityValue,
  listId,
  score,
  note,
} = {}) {
  const { data } = await axios.post(
    `${base()}delegate-followup/lists/${listId}/board-operator-rating?${opsCityParams(cityValue)}`,
    { score, note: note || null },
    { headers: getAuthHeaders(), timeout: 30000 },
  )

  return data
}

/** متابعة المبيعات — كروت موظفي المبيعات */
export async function directorSalesFollowupEmployees({ cityValue } = {}) {
  return directorGet(`sales-followup/employees?${opsCityParams(cityValue)}`, { timeoutMs: 180000 })
}

export async function directorSalesFollowupDetail({ cityValue, employeeId } = {}) {
  return directorGet(
    `sales-followup/employees/${employeeId}?${opsCityParams(cityValue)}`,
    { timeoutMs: 120000 },
  )
}

export async function directorSalesFollowupCustomers({ cityValue, employeeId } = {}) {
  return directorGet(
    `sales-followup/employees/${employeeId}/customers?${opsCityParams(cityValue)}`,
    { timeoutMs: 180000 },
  )
}

export async function directorSalesFollowupUpsertManagerRating({
  cityValue,
  employeeId,
  score,
  note,
} = {}) {
  const { data } = await axios.post(
    `${base()}sales-followup/employees/${employeeId}/manager-rating?${opsCityParams(cityValue)}`,
    { score, note: note || null },
    { headers: getAuthHeaders(), timeout: 30000 },
  )

  return data
}

export async function directorSalesFollowupGetAppBlock({ cityValue, employeeId } = {}) {
  return directorGet(
    `sales-followup/employees/${employeeId}/app-block?${opsCityParams(cityValue)}`,
    { timeoutMs: 30000 },
  )
}

export async function directorSalesFollowupSetAppBlock({
  cityValue,
  employeeId,
  isBlocked,
} = {}) {
  const { data } = await axios.put(
    `${base()}sales-followup/employees/${employeeId}/app-block?${opsCityParams(cityValue)}`,
    { isBlocked: !!isBlocked },
    { headers: getAuthHeaders(), timeout: 30000 },
  )
  return data
}

/** إدارة حسابات المتابعين (مدير / مراقب) على فرع المحافظة المختارة */
export async function directorFollowerManageLists({ cityValue } = {}) {
  return directorGet(`followers/manage/lists?${opsCityParams(cityValue)}`, { timeoutMs: 60000 })
}

export async function directorFollowerManageGet({ cityValue, userId } = {}) {
  return directorGet(`followers/manage/${userId}?${opsCityParams(cityValue)}`, { timeoutMs: 60000 })
}

export async function directorFollowerManageCreate({ cityValue, payload } = {}) {
  const { data } = await axios.post(
    `${base()}followers/manage?${opsCityParams(cityValue)}`,
    payload || {},
    { headers: getAuthHeaders(), timeout: 60000 },
  )

  return data
}

export async function directorFollowerManageUpdate({ cityValue, userId, payload } = {}) {
  const { data } = await axios.put(
    `${base()}followers/manage/${userId}?${opsCityParams(cityValue)}`,
    payload || {},
    { headers: getAuthHeaders(), timeout: 60000 },
  )

  return data
}

export async function directorFollowerManageDelete({ cityValue, userId } = {}) {
  const { data } = await axios.delete(
    `${base()}followers/manage/${userId}?${opsCityParams(cityValue)}`,
    { headers: getAuthHeaders(), timeout: 60000 },
  )

  return data
}
