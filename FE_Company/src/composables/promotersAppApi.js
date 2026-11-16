/**
 * BE_PromotersApp (المروّجون أونلاين) — dashboard API helpers. Separate from تطبيق المسوّقين (customerAppApi.js):
 * own service, own base path `/promoters-app/api/v1/`. Auth: the central gateway JWT (المدير / المراقب).
 * Every helper is prefixed `pa` to avoid auto-import clashes.
 */
import axios from 'axios'
import { getToken } from '@/services/tokenService'
import { formatIraqClock, formatIraqDate } from '@/composables/iraqDate'

export function promotersAppBase() {
  const fromEnv = String(import.meta.env.VITE_PROMOTERS_APP_API_URL || '').trim()
  const base = fromEnv || '/promoters-app/api/v1/'

  return base.endsWith('/') ? base : `${base}/`
}

function paClean(params = {}) {
  const out = {}
  for (const [k, v] of Object.entries(params || {})) {
    if (v == null || v === '')
      continue
    out[k] = v
  }

  return out
}

async function paRequest(method, path, { params, data, timeoutMs, onUploadProgress } = {}) {
  const token = getToken()

  const res = await axios.request({
    method,
    url: `${promotersAppBase()}${String(path).replace(/^\/+/, '')}`,
    params: params ? paClean(params) : undefined,
    data,
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    timeout: timeoutMs ?? 60000,
    onUploadProgress,
  })

  return res.data
}

const paGet = (p, params, o = {}) => paRequest('get', p, { ...o, params })
const paPost = (p, data, o = {}) => paRequest('post', p, { ...o, data: data ?? {} })
const paPut = (p, data, o = {}) => paRequest('put', p, { ...o, data: data ?? {} })
const paDel = p => paRequest('delete', p)

/** Arabic message from `{ error, fields }`. */
export function paErrorMessage(err, fallback = 'حدث خطأ غير متوقع') {
  const status = err?.response?.status
  const data = err?.response?.data
  if (data && typeof data === 'object') {
    const fields = data.fields && typeof data.fields === 'object' ? Object.values(data.fields).filter(Boolean).join('، ') : ''
    const main = data.error || data.message || ''
    if (main && fields)
      return `${main}: ${fields}`
    if (main || fields)
      return main || fields
  }
  if (status === 401)
    return 'انتهت الجلسة — يرجى تسجيل الدخول من جديد'
  if (status === 403)
    return 'لا تملك صلاحية تنفيذ هذا الإجراء'
  if (status === 413)
    return 'الملف أكبر من الحجم المسموح'
  if (status === 502 || status === 503 || status === 504)
    return 'خدمة المروّجين غير متاحة حالياً — حاول لاحقاً'
  if (err?.code === 'ECONNABORTED')
    return 'انتهت مهلة الاتصال بالخادم'
  if (err?.message === 'Network Error')
    return 'تعذر الاتصال بخادم المروّجين'

  return err?.message || fallback
}

export function paFieldErrors(err) {
  const f = err?.response?.data?.fields

  return f && typeof f === 'object' ? f : {}
}

export function paPaged(data) {
  const items = Array.isArray(data?.items) ? data.items : (Array.isArray(data) ? data : [])

  return { items, total: Number(data?.total ?? items.length) || 0 }
}

// Overview
export const paOverview = (params = {}) => paGet('admin/overview', params)

// Users
export const paListUsers = (params = {}) => paGet('admin/users', params)
export const paGetUser = id => paGet(`admin/users/${id}`)
export const paCreateUser = body => paPost('admin/users', body)
export const paUpdateUser = (id, body) => paPut(`admin/users/${id}`, body)
export const paSetPassword = (id, password) => paPost(`admin/users/${id}/password`, { password })
export const paSuspendUser = (id, suspended, reason) => paPost(`admin/users/${id}/suspend`, { suspended, reason: reason || null })
export const paSetFull = (id, isFull) => paPost(`admin/users/${id}/full`, { isFull })
export const paSetCut = (id, cutPercent) => paPost(`admin/users/${id}/cut`, { cutPercent })
export const paUserWallet = id => paGet(`admin/users/${id}/wallet`)
export const paUserEntries = (id, params = {}) => paGet(`admin/users/${id}/wallet/entries`, params)
export const paAdjustWallet = (id, amount, note) => paPost(`admin/users/${id}/wallet/adjust`, { amount, note })

// Products
export const paListProducts = () => paGet('admin/products')
export const paCreateProduct = body => paPost('admin/products', body)
export const paUpdateProduct = (id, body) => paPut(`admin/products/${id}`, body)
export const paDeleteProduct = id => paDel(`admin/products/${id}`)

// Orders
export const paListOrders = (params = {}) => paGet('admin/orders', params)
export const paOrderCounts = () => paGet('admin/orders/counts')
export const paGetOrder = id => paGet(`admin/orders/${id}`)
export const paSyncOrders = orderId => paPost('admin/orders/sync', {}, { params: paClean({ orderId }), timeoutMs: 120000 })

// Payouts + chat
export const paListPayouts = (params = {}) => paGet('admin/payouts', params)
export const paPayoutCounts = () => paGet('admin/payouts/counts', undefined, { timeoutMs: 15000 })
export const paGetPayout = id => paGet(`admin/payouts/${id}`)
export const paPayoutMessages = (id, afterId = 0) => paGet(`admin/payouts/${id}/messages`, { afterId }, { timeoutMs: 15000 })
export const paSendPayoutMessage = (id, body) => paPost(`admin/payouts/${id}/messages`, { body })

/** «تم التحويل»: the picture of the transfer receipt is required. */
export function paMarkPaid(id, receiptFile, note) {
  const form = new FormData()

  form.append('receipt', receiptFile)
  if (note)
    form.append('note', note)

  return paRequest('post', `admin/payouts/${id}/paid`, { data: form, timeoutMs: 120000 })
}

/** A photo or document in the withdrawal chat (15 MB at most), with optional text. */
export function paSendPayoutAttachment(id, file, body) {
  const form = new FormData()

  form.append('file', file)
  if (body)
    form.append('body', body)

  return paRequest('post', `admin/payouts/${id}/messages/attachment`, { data: form, timeoutMs: 180000 })
}

/** Chat attachments are private too: fetched with the token, returned as an object URL (revoke it when done). */
export async function paMessageAttachmentUrl(payoutId, messageId) {
  const token = getToken()

  const res = await axios.get(`${promotersAppBase()}admin/payouts/${payoutId}/messages/${messageId}/attachment`, {
    responseType: 'blob',
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    timeout: 120000,
  })

  return URL.createObjectURL(res.data)
}

/** The receipt is not public: fetched with the dashboard token and shown through an object URL (revoke it when done). */
export async function paPayoutReceiptUrl(id) {
  const token = getToken()

  const res = await axios.get(`${promotersAppBase()}admin/payouts/${id}/receipt`, {
    responseType: 'blob',
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    timeout: 60000,
  })

  return URL.createObjectURL(res.data)
}
export const paRejectPayout = (id, note) => paPost(`admin/payouts/${id}/reject`, { note })

// Materials
export const paListMaterials = () => paGet('admin/materials')
export function paUploadMaterial(file, title, onProgress) {
  const form = new FormData()

  form.append('file', file)
  if (title)
    form.append('title', title)

  return paRequest('post', 'admin/materials', {
    data: form,
    timeoutMs: 30 * 60 * 1000,
    onUploadProgress: e => onProgress?.(e.total ? Math.round((100 * e.loaded) / e.total) : 0),
  })
}
export const paRenameMaterial = (id, title) => paPut(`admin/materials/${id}`, { title })
export const paDeleteMaterial = id => paDel(`admin/materials/${id}`)

// Settings + messages
export const paGetSettings = () => paGet('admin/settings')
export const paSaveSettings = body => paPut('admin/settings', body)
export const paBroadcast = body => paPost('admin/notifications/broadcast', body)
export const paListBroadcasts = (params = {}) => paGet('admin/notifications/broadcasts', params)

// Labels / formatting
export const PA_ORDER_STATUSES = [
  { value: 'New', title: 'قيد الإرسال', color: 'info' },
  { value: 'Transferred', title: 'في قسم المبيعات', color: 'primary' },
  { value: 'FieldInspection', title: 'قيد الكشف', color: 'secondary' },
  { value: 'Sold', title: 'تم البيع', color: 'success' },
  { value: 'NotSold', title: 'لم يتم البيع', color: 'error' },
  { value: 'Cancelled', title: 'ملغى', color: 'grey' },
]

export const PA_PAYOUT_STATUSES = [
  { value: 'Pending', title: 'قيد المعالجة', color: 'warning' },
  { value: 'Paid', title: 'تم التحويل', color: 'success' },
  { value: 'Rejected', title: 'مرفوض', color: 'error' },
]

export function paMeta(list, value) {
  return list.find(i => i.value === value) || { value, title: value || '—', color: 'default' }
}

export function paMoney(v) {
  const n = Number(v)
  if (v == null || v === '' || !Number.isFinite(n))
    return '—'

  return `${n.toLocaleString('en-US')} د.ع`
}

export function paDateTime(v) {
  if (!v)
    return '—'

  return [formatIraqDate(v), formatIraqClock(v)].filter(Boolean).join(' ') || '—'
}

export function paDate(v) {
  return v ? (formatIraqDate(v) || '—') : '—'
}

export function paSize(bytes) {
  const n = Number(bytes) || 0
  if (n < 1024 * 1024)
    return `${Math.max(1, Math.round(n / 1024))} KB`

  return `${(n / 1024 / 1024).toFixed(1)} MB`
}

/** 8 easy-to-read characters. */
export function paNewPassword() {
  const chars = 'abcdefghjkmnpqrstuvwxyz23456789'

  return Array.from(crypto.getRandomValues(new Uint32Array(8)), b => chars[b % chars.length]).join('')
}
