import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'
import { salesGatewayBase } from '@/composables/useSalesBranches'
import { isCommitteeRole, readAllowedCities } from '@/utils/oversightRoles'

function base() {
  return `${salesGatewayBase()}director/`
}

/** المحاسب الرئيسي: متابعة المندوبين على فرعه المحدد فقط (LinkCity + توكن الفرع، بدون البوابة). */
export function isMainAccountantUser() {
  return String(localStorage.getItem('UserType') || '').trim() === 'محاسب رئيسي'
}

function accountantBranchRoot() {
  const link = String(localStorage.getItem('LinkCity') || '').trim()

  return link.endsWith('/') ? link : `${link}/`
}

async function accountantFollowupGet(path, { timeoutMs } = {}) {
  const { data } = await axios.get(`${accountantBranchRoot()}${path}`, {
    headers: getAuthHeaders(),
    timeout: timeoutMs ?? 30000,
  })

  return data
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

const CITIES_CACHE_KEY = 'director-gateway-cities-cache'

function citiesCacheKey() {
  return `${CITIES_CACHE_KEY}:${localStorage.getItem('UserType') || ''}`
}

/** Last gateway branch list for this role (instant first paint; committee re-filtered by its branches). */
export function directorCitiesCached() {
  try {
    const rows = JSON.parse(localStorage.getItem(citiesCacheKey()) || '[]')
    if (!Array.isArray(rows))
      return []
    if (!isCommitteeRole(localStorage.getItem('UserType')))
      return rows
    const allowed = new Set(readAllowedCities())

    return rows.filter(r => allowed.has(String(r?.value ?? r?.Value ?? '').trim()))
  }
  catch {
    return []
  }
}

let citiesInflight = null

export async function directorCities() {
  // Both متابعة pages (and nav switches) share one in-flight request.
  if (citiesInflight)
    return citiesInflight
  citiesInflight = directorGet('cities')
    .then(rows => {
      try {
        if (Array.isArray(rows) && rows.length)
          localStorage.setItem(citiesCacheKey(), JSON.stringify(rows))
      }
      catch { /* ignore quota */ }

      return rows
    })
    .finally(() => { citiesInflight = null })

  return citiesInflight
}

/** Monitor «لوحة الأسعار» — CRUD on gateway. */
export async function oversightCommittees() {
  return directorGet('oversight/committees')
}

export async function oversightCreateCommittee(body) {
  const { data } = await axios.post(`${base()}oversight/committees`, body, {
    headers: getAuthHeaders(),
    timeout: 30000,
  })
  return data
}

export async function oversightSetBranches(id, cityValues) {
  const { data } = await axios.put(`${base()}oversight/committees/${id}/branches`, { cityValues }, {
    headers: getAuthHeaders(),
    timeout: 30000,
  })
  return data
}

export async function oversightDeleteCommittee(id) {
  const { data } = await axios.delete(`${base()}oversight/committees/${id}`, {
    headers: getAuthHeaders(),
  })

  return data
}

export async function oversightAccounts() {
  return directorGet('oversight/accounts')
}

export async function oversightCreateAccount(body) {
  const { data } = await axios.post(`${base()}oversight/accounts`, body, {
    headers: getAuthHeaders(),
    timeout: 30000,
  })
  return data
}

export async function oversightDeleteAccount(id) {
  const { data } = await axios.delete(`${base()}oversight/accounts/${id}`, {
    headers: getAuthHeaders(),
    timeout: 30000,
  })
  return data
}

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
  if (isMainAccountantUser()) {
    const p = new URLSearchParams({ listId: String(listId) })
    if (fromDate)
      p.set('fromDate', fromDate)
    if (toDate)
      p.set('toDate', toDate)

    return accountantFollowupGet(`accountant/delegate-followup/list-payments-sum?${p}`, { timeoutMs: 60000 })
  }
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

/** مسؤول الارشيف reads the same read-only profile through api/archive-flow (the director routes refuse it). */
function profileBase() {
  return String(localStorage.getItem('UserType') || '').trim() === 'مسؤول الارشيف' ? '../archive-flow/' : ''
}

export function directorCustomerProfilePath(city, customerId) {
  return `${profileBase()}customers/${encodeURIComponent(city)}/${customerId}/profile`
}

export function directorCustomerNotesPath(city, customerId) {
  return `${profileBase()}customers/${encodeURIComponent(city)}/${customerId}/notes`
}

export function directorSaleShopImagePath(city, saleId) {
  return `${profileBase()}sales/${encodeURIComponent(city)}/${saleId}/shop-image`
}

export function directorCustomerDocumentFilePath(city, documentId) {
  return `${profileBase()}customer-documents/${encodeURIComponent(city)}/${documentId}/file`
}

function opsCityParams(cityValue) {
  return new URLSearchParams({ cityValue: String(cityValue || '') })
}

/** عمل المتابعين — مواقع وصور المحلات: تقدم كل قائمة مسندة لمتابع. */
export async function directorFollowerOpsShopCapture({ cityValue } = {}) {
  return directorGet(`followers/ops/shop-capture?${opsCityParams(cityValue)}`, { timeoutMs: 180000 })
}

export async function directorFollowerOpsShopCaptureList({ cityValue, listId } = {}) {
  return directorGet(`followers/ops/shop-capture/lists/${listId}/customers?${opsCityParams(cityValue)}`, { timeoutMs: 180000 })
}

/** موقع وصورة محل زبون واحد (بروفايل الزبون). */
export async function directorFollowerOpsShopCaptureCustomer({ cityValue, customerId } = {}) {
  return directorGet(`followers/ops/shop-capture/customers/${customerId}?${opsCityParams(cityValue)}`, { timeoutMs: 60000 })
}

export async function directorFollowerOpsShopCapturePhoto({ cityValue, customerId } = {}) {
  return directorGetBlob(`followers/ops/shop-capture/customers/${customerId}/photo?${opsCityParams(cityValue)}`, { timeoutMs: 120000 })
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

/** In-memory (per tab) last lists per branch — shown instantly while a fresh copy loads. */
const delegateListsCache = new Map()

/** متابعة المندوبين — قوائم نشطة (آخر تسديد ≤ 40 يوماً) */
export async function directorDelegateFollowupLists({ cityValue } = {}) {
  const rows = isMainAccountantUser()
    ? await accountantFollowupGet('accountant/delegate-followup/lists', { timeoutMs: 180000 })
    : await directorGet(`delegate-followup/lists?${opsCityParams(cityValue)}`, { timeoutMs: 180000 })
  if (Array.isArray(rows))
    delegateListsCache.set(String(cityValue || ''), rows)

  return rows
}


export function directorDelegateFollowupListsCached(cityValue) {
  return delegateListsCache.get(String(cityValue || '')) || null
}

export async function directorDelegateFollowupListSummary({ cityValue, listId } = {}) {
  if (isMainAccountantUser())
    return accountantFollowupGet(`accountant/delegate-followup/lists/${listId}/summary`, { timeoutMs: 120000 })
  return directorGet(
    `delegate-followup/lists/${listId}/summary?${opsCityParams(cityValue)}`,
    { timeoutMs: 120000 },
  )
}

/** Navbar «بحث الزبائن» — across every branch the caller may see (committee: its branches). */
export async function directorGlobalCustomerSearch(q, { signal, cityValue, includeFinished = false } = {}) {
  const params = { q, includeFinished }
  if (cityValue)
    params.cityValue = cityValue
  const { data } = await axios.get(`${base()}delegate-followup/customers/search`, {
    params,
    headers: getAuthHeaders(),
    timeout: 30000,
    signal,
  })

  return Array.isArray(data) ? data : []
}

export async function directorDelegateFollowupListCustomers({ cityValue, listId } = {}) {
  if (isMainAccountantUser())
    return accountantFollowupGet(`accountant/delegate-followup/lists/${listId}/customers`, { timeoutMs: 180000 })
  return directorGet(
    `delegate-followup/lists/${listId}/customers?${opsCityParams(cityValue)}`,
    { timeoutMs: 180000 },
  )
}

/** طلبات بيع مندوب القائمة — كل الطلبات + المبيعات/المرفوضة/التقييمات/المبيعات الجديدة */
export async function directorDelegateFollowupListSalesOrders({ cityValue, listId } = {}) {
  if (isMainAccountantUser())
    return accountantFollowupGet(`accountant/delegate-followup/lists/${listId}/sales-orders`, { timeoutMs: 180000 })
  return directorGet(
    `delegate-followup/lists/${listId}/sales-orders?${opsCityParams(cityValue)}`,
    { timeoutMs: 180000 },
  )
}

/** خريطة القوائم — موقع محل كل زبون غير مصفر (GPS المبيعات أو تقديري من مواقع التسديد) */
export async function directorDelegateFollowupListLocations({ cityValue, listId } = {}) {
  if (isMainAccountantUser())
    return accountantFollowupGet(`accountant/delegate-followup/lists/${listId}/locations`, { timeoutMs: 180000 })
  return directorGet(
    `delegate-followup/lists/${listId}/locations?${opsCityParams(cityValue)}`,
    { timeoutMs: 180000 },
  )
}

/** Every recent payment GPS point of one customer (kind: shop | office | other). */
export async function directorDelegateFollowupCustomerPaymentLocations({ cityValue, listId, customerId } = {}) {
  if (isMainAccountantUser())
    return accountantFollowupGet(`accountant/delegate-followup/lists/${listId}/customers/${customerId}/payment-locations`, { timeoutMs: 180000 })
  return directorGet(
    `delegate-followup/lists/${listId}/customers/${customerId}/payment-locations?${opsCityParams(cityValue)}`,
    { timeoutMs: 180000 },
  )
}

export async function directorDelegateFollowupPayments({ cityValue, customerId } = {}) {
  if (isMainAccountantUser())
    return accountantFollowupGet(`CustomersPayments/CustomersPayments_GetByCustomerID/${customerId}`, { timeoutMs: 60000 })
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

export async function directorDelegateFollowupGetBoardOperatorRating({ cityValue, listId, slot } = {}) {
  if (isMainAccountantUser())
    return accountantFollowupGet(`accountant/delegate-followup/lists/${listId}/board-operator-rating?slot=accountant`, { timeoutMs: 30000 })
  const slotParam = slot === 'accountant' ? '&slot=accountant' : ''
  return directorGet(
    `delegate-followup/lists/${listId}/board-operator-rating?${opsCityParams(cityValue)}${slotParam}`,
    { timeoutMs: 30000 },
  )
}

export async function directorDelegateFollowupUpsertBoardOperatorRating({
  cityValue,
  listId,
  score,
  note,
} = {}) {
  // المحاسب الرئيسي → «تقييم المحاسب الرئيسي» on his branch; everyone else → «تقييم لجنة المراقبة».
  if (isMainAccountantUser()) {
    const { data } = await axios.post(
      `${accountantBranchRoot()}accountant/delegate-followup/lists/${listId}/board-operator-rating`,
      { score, note: note || null, slot: 'accountant' },
      { headers: getAuthHeaders(), timeout: 30000 },
    )

    return data
  }
  const { data } = await axios.post(
    `${base()}delegate-followup/lists/${listId}/board-operator-rating?${opsCityParams(cityValue)}`,
    { score, note: note || null, slot: 'head' },
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
