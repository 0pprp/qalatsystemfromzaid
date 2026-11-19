/**
 * BE_CustomerApp (تطبيق المسوّقين) — admin API helpers for the dashboard.
 * Backend: PromoterApps/FieldPromoterApp/backend/BE_CustomerApp (تطبيق المروج الميداني).
 * Auth: the same gateway JWT stored in localStorage.Token after central login
 * (accepted roles: المدير / المراقب). All helpers are prefixed `ca` to avoid
 * auto-import name clashes with other composables.
 */
import axios from 'axios'
import { getToken } from '@/services/tokenService'
import { formatIraqClock, formatIraqDate } from '@/composables/iraqDate'

// ---------------------------------------------------------------------------
// Base + transport
// ---------------------------------------------------------------------------

export function customerAppBase() {
  const fromEnv = String(import.meta.env.VITE_CUSTOMER_APP_API_URL || '').trim()
  const base = fromEnv || '/customer-app/api/v1/'

  return base.endsWith('/') ? base : `${base}/`
}

function caHeaders(extra = {}) {
  const token = getToken()

  return token ? { Authorization: `Bearer ${token}`, ...extra } : { ...extra }
}

/** Drop empty query values so the API sees only real filters. */
export function caCleanParams(params = {}) {
  const out = {}
  for (const [key, value] of Object.entries(params || {})) {
    if (value == null || value === '' || (Array.isArray(value) && !value.length))
      continue
    out[key] = value
  }

  return out
}

async function caRequest(method, path, { params, data, timeoutMs, headers, responseType, onUploadProgress } = {}) {
  const response = await axios.request({
    method,
    url: `${customerAppBase()}${String(path).replace(/^\/+/, '')}`,
    params: params ? caCleanParams(params) : undefined,
    data,
    headers: caHeaders(headers),
    timeout: timeoutMs ?? 60000,
    responseType,
    onUploadProgress,
  })

  return response.data
}

const caGet = (path, params, opts = {}) => caRequest('get', path, { ...opts, params })
const caPost = (path, data, opts = {}) => caRequest('post', path, { ...opts, data: data ?? {} })
const caPut = (path, data, opts = {}) => caRequest('put', path, { ...opts, data: data ?? {} })
const caDel = (path, opts = {}) => caRequest('delete', path, opts)

/**
 * Arabic user-facing error from BE_CustomerApp `{ error, code, fields }`
 * (falls back to `message`/`title` used by other dashboard APIs).
 */
export function caErrorMessage(err, fallback = 'حدث خطأ غير متوقع') {
  const status = err?.response?.status
  let data = err?.response?.data
  if (typeof data === 'string' && data.trim()) {
    try {
      data = JSON.parse(data)
    }
    catch {
      data = { error: data.length < 300 ? data : '' }
    }
  }
  if (data && typeof data === 'object') {
    const fields = data.fields && typeof data.fields === 'object'
      ? Object.values(data.fields).filter(Boolean).join('، ')
      : ''

    const main = data.error || data.message || data.title || ''
    if (main && fields)
      return `${main}: ${fields}`
    if (main)
      return main
    if (fields)
      return fields
  }
  if (status === 401)
    return 'انتهت الجلسة — يرجى تسجيل الدخول من جديد'
  if (status === 403)
    return 'لا تملك صلاحية تنفيذ هذا الإجراء'
  if (status === 404)
    return 'العنصر غير موجود'
  if (status === 502 || status === 503 || status === 504)
    return 'خدمة تطبيق المسوّقين غير متاحة حالياً — حاول لاحقاً'
  if (err?.code === 'ECONNABORTED')
    return 'انتهت مهلة الاتصال بالخادم'
  if (err?.message === 'Network Error')
    return 'تعذر الاتصال بخادم تطبيق المسوّقين'
  if (err?.message)
    return err.message

  return fallback
}

/** Runs `fn` and rethrows a plain Error carrying the Arabic API message (for dialogs). */
export async function caTry(fn, fallback) {
  try {
    return await fn()
  }
  catch (err) {
    const wrapped = new Error(caErrorMessage(err, fallback))

    wrapped.code = caErrorCode(err)
    wrapped.fields = caFieldErrors(err)
    wrapped.cause = err
    throw wrapped
  }
}

/** Validation `fields` map (`{ "cashPrice": "مطلوب" }`) or `{}`. */
export function caFieldErrors(err) {
  const fields = err?.response?.data?.fields

  return fields && typeof fields === 'object' ? fields : {}
}

export function caErrorCode(err) {
  return err?.response?.data?.code || ''
}

/** Normalises `{ items, total, page, pageSize }` (and plain arrays). */
export function caPaged(data) {
  if (Array.isArray(data))
    return { items: data, total: data.length, page: 1, pageSize: data.length }
  const items = Array.isArray(data?.items) ? data.items : []

  return {
    items,
    total: Number(data?.total ?? items.length) || 0,
    page: Number(data?.page ?? 1) || 1,
    pageSize: Number(data?.pageSize ?? items.length) || 20,
    raw: data,
  }
}

// ---------------------------------------------------------------------------
// Public lookups
// ---------------------------------------------------------------------------

export const caBranches = () => caGet('branches')
export const caPublicAppConfig = () => caGet('app/config')

// ---------------------------------------------------------------------------
// Agents (المسوّقون) — accounts made from the dashboard
// ---------------------------------------------------------------------------

export const caListAgents = (params = {}) => caGet('admin/agents', params)
export const caGetAgent = id => caGet(`admin/agents/${id}`)

/** body: { username, password, firstName, fatherName, grandfatherName, surname, phone, cityValue } */
export const caCreateAgent = body => caPost('admin/agents', body)

/** Same body without password (the password is changed with caSetAgentPassword). */
export const caUpdateAgent = (id, body) => caPut(`admin/agents/${id}`, body)
export const caSetAgentPassword = (id, password) => caPost(`admin/agents/${id}/password`, { password })
export const caSuspendAgent = (id, suspended, reason) =>
  caPost(`admin/agents/${id}/suspend`, { suspended, reason: reason || null })

// ---------------------------------------------------------------------------
// Attendance (دوام المسوّقين) — work sessions + live location
// ---------------------------------------------------------------------------

/** Every agent on a day: { userId, name, firstStartedAt, lastEndedAt, totalMinutes, sessionsCount, isWorking, lastPoint, pointsCount } */
export const caAttendance = (params = {}) => caGet('admin/attendance', params)

/**
 * «بقي في نفس المكان» alerts: a working agent stayed within ~150 m of one place for an hour.
 * { id, userId, name, phone, cityName, latitude, longitude, sinceAt, lastSeenAt, minutes, isOpen, seenBy, seenAt }
 */
export const caAttendanceAlerts = (params = {}) => caGet('admin/attendance/alerts', params)

/** { unseen, latest: [...] } — unacknowledged alerts of the last 24 h (for the navbar bell). */
export const caUnseenAlerts = () => caGet('admin/attendance/alerts/unseen', undefined, { timeoutMs: 15000 })
export const caSeenAlert = id => caPost(`admin/attendance/alerts/${id}/seen`)
export const caSeenAllAlerts = () => caPost('admin/attendance/alerts/seen-all')

// ---------------------------------------------------------------------------
// Prospects (المحلات المثبّتة) — shops pinned by agents
// ---------------------------------------------------------------------------

/** Paged: { items: [{ id, latitude, longitude, shopName, description, interest, interestLabel, phone, goodsValue, lengthM, widthM, areaM2, createdByName, cityName, createdAt }], total } */
export const caListProspects = (params = {}) => caGet('admin/prospects', params)
export const caDeleteProspect = id => caDel(`admin/prospects/${id}`)

/**
 * «المحلات المثبّتة»: one agent's pins on an Iraq day, oldest first, grouped in periods.
 * { workMinutes, targetMinutes, met, quickPins, quickGapMinutes, breakGapMinutes, firstPinAt, lastPinAt,
 *   periods: [{ index, startAt, endAt, seconds, pins, quickPins, open }],
 *   pins: [{ pin, periodIndex, startsPeriod, endsPeriod, secondsOnShop, quick, breakSecondsBefore }] }
 */
export const caPinTimeline = (userId, date) => caGet('admin/prospects/timeline', { userId, date })

/** Metres between two points. */
export function caDistanceM(lat1, lng1, lat2, lng2) {
  const r = 6371000
  const rad = d => (Number(d) * Math.PI) / 180
  const dLat = rad(lat2 - lat1)
  const dLng = rad(lng2 - lng1)
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(rad(lat1)) * Math.cos(rad(lat2)) * Math.sin(dLng / 2) ** 2

  return 2 * r * Math.asin(Math.sqrt(a))
}

export const CA_INTEREST_LEVELS = [
  { value: 1, title: 'غير مهتم إطلاقاً', color: '#dc2626' },
  { value: 2, title: 'قليل الاهتمام', color: '#f97316' },
  { value: 3, title: 'مهتم نوعاً ما', color: '#eab308' },
  { value: 4, title: 'مهتم', color: '#22c55e' },
  { value: 5, title: 'مهتم جداً', color: '#15803d' },
]

export function caInterestMeta(level) {
  return CA_INTEREST_LEVELS.find(l => l.value === Number(level)) || { value: level, title: '—', color: '#94a3b8' }
}

/** Agents working right now with their latest point. */
export const caAttendanceLive = (params = {}) => caGet('admin/attendance/live', params)

/** { summary, sessions: [{ startedAt, endedAt, minutes, endedBy }], track: [{ lat, lng, at, accuracy }] } */
export const caAgentDay = (userId, date) => caGet(`admin/attendance/${userId}/day`, { date })
export const caAgentHistory = (userId, { from, to } = {}) => caGet(`admin/attendance/${userId}/history`, { from, to })

/** 425 → "7 س 05 د". */
export function caMinutes(value) {
  const total = Math.max(0, Math.trunc(Number(value) || 0))
  const h = Math.floor(total / 60)
  const m = total % 60

  return h ? `${h} س ${String(m).padStart(2, '0')} د` : `${m} د`
}

// ---------------------------------------------------------------------------
// Settings & notifications to agents
// ---------------------------------------------------------------------------

// «الهدف اليومي» — minutes of work per day, counted only inside pin periods (agents never see it).
// status: met | missed | off (did not work) | none (no target)
export const caTargetsDay = date => caGet('admin/targets', date ? { date } : {})
export const caAgentTargets = (userId, { from, to } = {}) => caGet(`admin/agents/${userId}/targets`, { from, to })

/** minutes: the agent's own target, or null to use the default from «إعدادات التطبيق». */
export const caSetAgentTarget = (userId, minutes) => caPut(`admin/agents/${userId}/target`, { dailyWorkMinutesTarget: minutes })

export const CA_TARGET_STATUS = {
  met: { title: 'حقق الهدف', color: 'success', icon: 'tabler-circle-check' },
  missed: { title: 'لم يحقق الهدف', color: 'error', icon: 'tabler-circle-x' },
  off: { title: 'لم يعمل', color: 'secondary', icon: 'tabler-moon' },
  none: { title: 'بدون هدف', color: 'default', icon: 'tabler-minus' },
}

/** Minutes ↔ hours for the target inputs (hours may be fractional: 4.5 = 4 س 30 د). */
export const caMinutesToHours = m => (m == null ? null : Math.round((Number(m) / 60) * 100) / 100)
export const caHoursToMinutes = h => (h == null || h === '' ? 0 : Math.round(Number(h) * 60))

/** Seconds → "12 د" / "1 س 05 د" / "45 ث". */
export function caSeconds(value) {
  const s = Math.max(0, Math.trunc(Number(value) || 0))

  return s < 60 ? `${s} ث` : caMinutes(Math.floor(s / 60))
}

/** Iraq clock (HH:mm) of an ISO UTC string. */
export function caClock(value) {
  return value ? formatIraqClock(value) || '—' : '—'
}

/** Today as `YYYY-MM-DD` in Iraq time (UTC+3). */
export function caIraqToday() {
  return new Date(Date.now() + 3 * 3600000).toISOString().slice(0, 10)
}

export const caGetSettings = () => caGet('admin/settings')
export const caUpdateSettings = body => caPut('admin/settings', body)

export const caBroadcast = body => caPost('admin/notifications/broadcast', body)
export const caListBroadcasts = (params = {}) => caGet('admin/notifications/broadcasts', params)

// ---------------------------------------------------------------------------
// Reports & audit
// ---------------------------------------------------------------------------

export const caReportsOverview = (params = {}) => caGet('admin/reports/overview', params)
/** Names behind one number of the «أداء المسوّقين» table. params: { userId, bucket, from, to } */
export const caReportsAgentDetails = (params = {}) => caGet('admin/reports/agent-details', params)
export const caListAudit =(params = {}) => caGet('admin/audit', params)

/** Downloads `admin/reports/export` CSV (UTF-8 BOM) and saves it in the browser. */
export async function caExportReport(type, { from, to } = {}) {
  let blob
  try {
    blob = await caRequest('get', 'admin/reports/export', {
      params: { type, from, to },
      responseType: 'blob',
      timeoutMs: 5 * 60 * 1000,
    })
  }
  catch (err) {
    // Error bodies arrive as Blob — decode for the Arabic message.
    const data = err?.response?.data
    if (data instanceof Blob) {
      try {
        err.response.data = JSON.parse(await data.text())
      }
      catch { /* keep original */ }
    }
    throw err
  }
  const stamp = new Date().toISOString().slice(0, 10)
  const name = `customer-app-${type}-${from || 'all'}-${to || stamp}.csv`
  const url = URL.createObjectURL(blob instanceof Blob ? blob : new Blob([blob], { type: 'text/csv;charset=utf-8' }))
  const a = document.createElement('a')

  a.href = url
  a.download = name
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 2000)

  return name
}

// ---------------------------------------------------------------------------
// Labels / formatting
// ---------------------------------------------------------------------------

export const CA_ORDER_STATUSES = [
  { value: 'New', title: 'جديد', long: 'طلب جديد', color: 'info', icon: 'tabler-sparkles' },
  { value: 'UnderReview', title: 'قيد المعالجة', long: 'قيد المعالجة', color: 'warning', icon: 'tabler-hourglass' },
  { value: 'Transferred', title: 'محوّل', long: 'في قسم المبيعات', color: 'primary', icon: 'tabler-send' },
  { value: 'FieldInspection', title: 'قيد الكشف', long: 'قيد الكشف الميداني', color: 'secondary', icon: 'tabler-map-search' },
  { value: 'Sold', title: 'قيد التسديد', long: 'تم البيع - قيد التسديد', color: 'success', icon: 'tabler-circle-check' },
  { value: 'Finished', title: 'مكتمل التسديد', long: 'مكتمل التسديد', color: 'success', icon: 'tabler-checks' },
  { value: 'NotSold', title: 'لم يتم البيع', long: 'لم يتم البيع', color: 'error', icon: 'tabler-circle-x' },
  { value: 'Denied', title: 'مرفوض', long: 'مرفوض', color: 'error', icon: 'tabler-ban' },
  { value: 'Cancelled', title: 'ملغى', long: 'ملغى', color: 'grey', icon: 'tabler-x' },
]

export function caOrderStatusMeta(status) {
  return CA_ORDER_STATUSES.find(s => s.value === status)
    || { value: status, title: status || '—', long: status || '—', color: 'default', icon: 'tabler-circle' }
}

export function caMetaFrom(list, value) {
  return list.find(i => i.value === value) || { value, title: value || '—', color: 'default' }
}

export function caMoney(value) {
  if (value == null || value === '')
    return '—'
  const n = Number(value)
  if (!Number.isFinite(n))
    return '—'

  return `${n.toLocaleString('en-US')} د.ع`
}

export function caDate(value) {
  if (!value)
    return '—'

  return formatIraqDate(value) || '—'
}

export function caDateTime(value) {
  if (!value)
    return '—'
  const d = formatIraqDate(value)
  const t = formatIraqClock(value)

  return [d, t].filter(Boolean).join(' ') || '—'
}

/** "منذ 3 ساعات" style age from an ISO UTC string. */
export function caAge(value) {
  if (!value)
    return '—'
  const ms = Date.parse(value)
  if (Number.isNaN(ms))
    return '—'
  const diff = Math.max(0, Date.now() - ms)
  const minutes = Math.floor(diff / 60000)
  if (minutes < 1)
    return 'الآن'
  if (minutes < 60)
    return `منذ ${minutes} دقيقة`
  const hours = Math.floor(minutes / 60)
  if (hours < 24)
    return `منذ ${hours} ساعة`
  const days = Math.floor(hours / 24)
  if (days < 30)
    return `منذ ${days} يوم`
  const months = Math.floor(days / 30)

  return `منذ ${months} شهر`
}

export function caMapLink(lat, lng) {
  const la = Number(lat)
  const ln = Number(lng)
  if (!Number.isFinite(la) || !Number.isFinite(ln) || (la === 0 && ln === 0))
    return ''

  return `https://www.google.com/maps?q=${la},${ln}`
}

/** `YYYY-MM-DD` for date inputs (local). */
export function caYmd(date = new Date()) {
  const d = new Date(date)
  const pad = n => String(n).padStart(2, '0')

  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
