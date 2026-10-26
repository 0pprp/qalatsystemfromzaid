/**
 * Web client for المدير المفوض — same paths as delegated_manager_application.
 * Source of truth for paths: Qalaa_ALL_APIs_Inventory.xlsx (read-only) + Flutter repos.
 * Base: Sales Gateway …/sales-gw/api/
 */
import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'
import { salesGatewayBase } from '@/composables/useSalesBranches'

export const DM_USER_TYPE = 'مدير مفوض'

function gw() {
  return salesGatewayBase()
}

function dm(path = '') {
  const p = String(path || '').replace(/^\/+/, '')
  return `${gw()}delegated-manager/${p}`
}

function authHeaders() {
  return getAuthHeaders()
}

async function get(path, { params, timeoutMs = 60000 } = {}) {
  const { data } = await axios.get(dm(path), {
    headers: authHeaders(),
    params: params || undefined,
    timeout: timeoutMs,
  })
  return data
}

async function post(path, body = {}, { timeoutMs = 60000 } = {}) {
  const { data } = await axios.post(dm(path), body ?? {}, {
    headers: authHeaders(),
    timeout: timeoutMs,
  })
  return data
}

async function put(path, body = {}, { timeoutMs = 60000 } = {}) {
  const { data } = await axios.put(dm(path), body ?? {}, {
    headers: authHeaders(),
    timeout: timeoutMs,
  })
  return data
}

async function del(path, { timeoutMs = 60000 } = {}) {
  const { data } = await axios.delete(dm(path), {
    headers: authHeaders(),
    timeout: timeoutMs,
  })
  return data
}

/** POST Auth/LoginDelegatedManager — central account, no province. */
export async function dmLogin({ userName, password } = {}) {
  const { data } = await axios.post(
    `${gw()}Auth/LoginDelegatedManager`,
    { userName: String(userName || '').trim(), password: String(password || '') },
    {
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      timeout: 45000,
    },
  )
  return data
}

export function dmSaveSession(login) {
  const token = login?.token
  if (!token)
    throw new Error('استجابة تسجيل الدخول غير مكتملة')
  const userType = login?.userType || login?.UserType || ''
  if (userType !== DM_USER_TYPE)
    throw new Error('هذا الحساب ليس حساب مدير مفوض')

  localStorage.setItem('Token', token)
  if (login.expiration || login.Expiration)
    localStorage.setItem('Expiration', login.expiration || login.Expiration)
  localStorage.setItem('UserID', String(login.userId ?? login.UserId ?? 0))
  localStorage.setItem('UserName', login.userName || login.UserName || '')
  localStorage.setItem('UserType', DM_USER_TYPE)
  localStorage.setItem('DirectorScope', '')
  localStorage.setItem('MonitorScope', '')
  localStorage.setItem('SalesManagerScope', '')
  localStorage.setItem('DmScope', 'central')
  localStorage.setItem('LinkCity', gw())
  localStorage.setItem('CityName', login.cityName || login.CityName || 'كل المحافظات')
  localStorage.removeItem('Database')
}

export function isDmSession() {
  return localStorage.getItem('UserType') === DM_USER_TYPE
}

// ── Dashboard ──────────────────────────────────────────────
export function dmDashboard() {
  return get('dashboard')
}

// ── Assigned tasks ─────────────────────────────────────────
export function dmAssignedTasksInbox() {
  return get('assigned-tasks/inbox')
}

export function dmAssignedTaskAccept(id) {
  return post(`assigned-tasks/${id}/accept`)
}

export function dmAssignedTaskReject(id, note) {
  return post(`assigned-tasks/${id}/reject`, { note: String(note || '').trim() })
}

export function dmAssignedTaskTransfer(id, body) {
  return post(`assigned-tasks/${id}/transfer`, body || {})
}

export function dmAssignedTaskComplete(id) {
  return post(`assigned-tasks/${id}/complete`)
}

export function dmAssignedTaskConfirm(id) {
  return post(`assigned-tasks/${id}/confirm`)
}

export function dmAssignedTaskAttachmentUrl(taskId, fileId) {
  return dm(`assigned-tasks/${taskId}/attachments/${fileId}`)
}

// ── Complaints ─────────────────────────────────────────────
export function dmComplaints(params = {}) {
  return get('complaints', { params })
}

export function dmComplaintsUnreadCount() {
  return get('complaints/unread-count')
}

export function dmComplaintDetail(id) {
  return get(`complaints/${id}`)
}

export function dmComplaintMarkRead(id) {
  return put(`complaints/${id}/read`)
}

export function dmComplaintsMarkAllRead() {
  return put('complaints/read-all')
}

// ── Exceptions / Wrong sales ───────────────────────────────
export function dmExceptions(params = {}) {
  return get('exceptions', { params })
}

export function dmExceptionDetail(id) {
  return get(`exceptions/${id}`)
}

export function dmExceptionMarkRead(id) {
  return post(`exceptions/${id}/read`)
}

export function dmExceptionDecide(id, { decision, note } = {}) {
  return put(`exceptions/${id}/decision`, {
    decision,
    ...(note ? { note: String(note).trim() } : {}),
  })
}

// ── Waivers ────────────────────────────────────────────────
export function dmWaivers(params = {}) {
  return get('waivers', { params })
}

export function dmWaiverDetail(id) {
  return get(`waivers/${id}`)
}

export function dmWaiverDecide(id, { decision, note, approvedAmount } = {}) {
  const body = { decision }
  if (note)
    body.note = String(note).trim()
  if (approvedAmount != null && approvedAmount !== '')
    body.approvedAmount = Number(approvedAmount)
  return put(`waivers/${id}/decision`, body)
}

// ── Stats / customer ───────────────────────────────────────
export function dmStatsCatalog(params = {}) {
  return get('stats/catalog', { params })
}

export function dmStatsTrend(params = {}) {
  return get('stats/trend', { params, timeoutMs: 120000 })
}

export function dmStatsRatingCustomers(params = {}) {
  return get('stats/rating-customers', { params, timeoutMs: 120000 })
}

// ── Follower ops board (same company paths as عمل المتابعين) ──
export function dmFollowerCards(cityValue) {
  return get('followers/ops/follower-cards', {
    params: { cityValue },
    timeoutMs: 180000,
  })
}

export function dmFollowerSummaries(cityValue) {
  return get('followers/ops/summaries', {
    params: { cityValue },
    timeoutMs: 180000,
  })
}

export function dmFollowerWorkBundle(cityValue, followerId) {
  return get(`followers/ops/${followerId}/work-bundle`, {
    params: { cityValue },
    timeoutMs: 180000,
  })
}

export function dmFollowerRemainingCustomers(cityValue, followerId, bucket) {
  return get(`followers/ops/${followerId}/remaining-customers`, {
    params: { cityValue, ...(bucket ? { bucket } : {}) },
    timeoutMs: 180000,
  })
}

export function dmFollowerCollection(cityValue, followerId) {
  return get(`followers/ops/${followerId}/collection`, {
    params: { cityValue },
    timeoutMs: 120000,
  })
}

export function dmFollowerLegalFollowups(cityValue, followerId) {
  return get(`followers/ops/${followerId}/legal-followups`, {
    params: { cityValue },
    timeoutMs: 120000,
  })
}

export function dmDelegateFollowupListSummary(cityValue, listId) {
  return get(`delegate-followup/lists/${listId}/summary`, {
    params: { cityValue },
    timeoutMs: 120000,
  })
}

export function dmDelegateFollowupListCustomers(cityValue, listId) {
  return get(`delegate-followup/lists/${listId}/customers`, {
    params: { cityValue },
    timeoutMs: 180000,
  })
}

export function dmCustomerProfile(cityValue, customerId) {
  return get(`customers/${encodeURIComponent(cityValue)}/${customerId}/profile`)
}

export function dmCustomerNotes(cityValue, customerId) {
  return get(`customers/${encodeURIComponent(cityValue)}/${customerId}/notes`)
}

export function dmAddCustomerNote(cityValue, body) {
  return post(`customers/${encodeURIComponent(cityValue)}/notes`, body || {})
}

export function dmSaleShopImageUrl(cityValue, saleId) {
  return dm(`sales/${encodeURIComponent(cityValue)}/${saleId}/shop-image`)
}

// ── Branch manager users ───────────────────────────────────
export function dmBmUsersCities() {
  return get('branch-manager-users/cities')
}

export function dmBmUsersList() {
  return get('branch-manager-users')
}

export function dmBmUserGet(id) {
  return get(`branch-manager-users/${id}`)
}

export function dmBmUserCreate(body) {
  return post('branch-manager-users', body || {})
}

export function dmBmUserUpdate(id, body) {
  return put(`branch-manager-users/${id}`, body || {})
}

export function dmBmUserDelete(id) {
  return del(`branch-manager-users/${id}`)
}

// ── Branch visits ──────────────────────────────────────────
export function dmBranchVisits(params = {}) {
  return get('branch-visits', { params })
}

export function dmBranchVisitDetail(id) {
  return get(`branch-visits/${id}`)
}

export function dmBranchVisitAttachmentUrl(visitId, fileId) {
  return dm(`branch-visits/${visitId}/attachments/${fileId}`)
}

// ── Committees ─────────────────────────────────────────────
export function dmCommitteesMainFollowers() {
  return get('committees/main-followers')
}

export function dmCommitteesMainFollowersLive() {
  return get('committees/main-followers/live')
}

export function dmCommitteesMainFollowerTrail(userName) {
  return get(`committees/main-followers/${encodeURIComponent(userName)}/trail`)
}

export function dmCommitteesMissions() {
  return get('committees/missions')
}

export function dmCommitteesMissionSummary(missionId) {
  return get(`committees/missions/${missionId}/summary`)
}

export function dmCommitteesCreate(body) {
  return post('committees', body || {})
}

export function dmCommitteesCreateMission(body) {
  return post('committees/missions', body || {})
}

export function isWrongSaleReason(reason) {
  const t = String(reason || '')
  return t.includes('مبيعات خاطئة') || t.toLowerCase().includes('wrongsale')
}
