import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'
import { salesGatewayBase } from '@/composables/useSalesBranches'

/**
 * Paper archive chain through the sales gateway:
 * موظف المبيعات → مسؤول/مساعد مسؤول لجنة المراقبة → مسؤول الارشيف,
 * and follower archive requests: مسؤول الارشيف → اللجنة → المتابع.
 */
function url(path) {
  return `${salesGatewayBase()}archive-flow/${path}`
}

async function call(method, path, { cityValue, data, params, responseType } = {}) {
  const { data: body } = await axios({
    method,
    url: url(path),
    data,
    params: { ...(params || {}), ...(cityValue ? { cityValue } : {}) },
    headers: getAuthHeaders(),
    responseType,
    timeout: 120000,
  })

  return body
}

export const ARCHIVE_BRANCH_KEY = 'archive-flow-branch'

export function rememberedBranch() {
  try {
    return localStorage.getItem(ARCHIVE_BRANCH_KEY) || ''
  }
  catch {
    return ''
  }
}

export function rememberBranch(value) {
  try {
    localStorage.setItem(ARCHIVE_BRANCH_KEY, value || '')
  }
  catch { /* ignore */ }
}

/** [{ value, name }] — committee sees only its branches. */
export const archiveCities = () => call('get', 'cities')

/** [{ name, role, committeeName }] */
export const archiveRecipients = cityValue => call('get', 'recipients', { cityValue })

// —— مسؤول الارشيف ——
export const archiveBoard = cityValue => call('get', 'archive/board', { cityValue })
export const archiveBundle = (cityValue, bundleId) => call('get', `archive/bundles/${encodeURIComponent(bundleId)}`, { cityValue })
export function archivePhoto(cityValue, bundleId, photoId) {
  const id = encodeURIComponent(bundleId)
  const path = photoId ? `archive/bundles/${id}/photos/${encodeURIComponent(photoId)}` : `archive/bundles/${id}/photo`

  return call('get', path, { cityValue, responseType: 'blob' })
}
export const archiveDecide = (cityValue, archiveIds, accept, note) =>
  call('post', 'archive/decide', { cityValue, data: { archiveIds, accept, note: note || null } })
export const archiveRequestsBoard = cityValue => call('get', 'archive/requests', { cityValue })
/** { branches: [{ cityValue, cityName, ok, data: { counts, atArchive, newRequests } }] } */
export const archiveOverview = () => call('get', 'archive/overview')
export const archiveAssignRequest = (cityValue, id, committeeUser, note) =>
  call('post', `archive/requests/${encodeURIComponent(id)}/assign`, { cityValue, data: { committeeUser, note: note || null } })
export const archiveFinishedCustomers = (cityValue, days = 30) =>
  call('get', 'archive/finished-customers', { cityValue, params: { days } })

// —— لجنة المراقبة ——
export const committeeItems = cityValue => call('get', 'committee/items', { cityValue })
export const committeeReceive = (cityValue, archiveIds, accept, note) =>
  call('post', 'committee/receive', { cityValue, data: { archiveIds, accept, note: note || null } })
export const committeeToArchive = (cityValue, archiveIds) =>
  call('post', 'committee/to-archive', { cityValue, data: { archiveIds } })
export const committeeReturn = (cityValue, archiveIds, note) =>
  call('post', 'committee/return', { cityValue, data: { archiveIds, note } })
export const committeeRequests = cityValue => call('get', 'committee/requests', { cityValue })
export const committeeAcceptRequest = (cityValue, id) => call('post', `committee/requests/${encodeURIComponent(id)}/accept`, { cityValue })
export const committeeGiveRequest = (cityValue, id) => call('post', `committee/requests/${encodeURIComponent(id)}/give`, { cityValue })

export function errorMessage(e, fallback) {
  return e?.response?.data?.message || e?.message || fallback
}

export function formatDay(raw) {
  const text = String(raw || '')
  if (!text)
    return '—'
  const date = new Date(text.endsWith('Z') || text.includes('+') || text.length <= 10 ? text : `${text}Z`)
  if (Number.isNaN(date.getTime()))
    return text.slice(0, 10)

  return date.toLocaleDateString('en-GB', { timeZone: 'Asia/Baghdad' })
}

export const ITEM_STATUS = {
  Pending: { label: 'عند موظف المبيعات', color: 'default' },
  Refused: { label: 'أُعيد لموظف المبيعات', color: 'error' },
  ToCommittee: { label: 'بانتظار استلام اللجنة', color: 'warning' },
  WithCommittee: { label: 'عند اللجنة', color: 'info' },
  ArchiveRefused: { label: 'رفضه الأرشيف', color: 'error' },
  ToArchive: { label: 'بانتظار استلام الأرشيف', color: 'warning' },
  Approved: { label: 'مؤرشف', color: 'success' },
}

export const REQUEST_STATUS = {
  Requested: { label: 'بانتظار الإسناد', color: 'warning' },
  ToCommittee: { label: 'بانتظار استلام اللجنة', color: 'warning' },
  WithCommittee: { label: 'عند اللجنة', color: 'info' },
  Sent: { label: 'سُلّم للمتابع', color: 'info' },
  Received: { label: 'استلمه المتابع', color: 'success' },
}

export function customerProfileRoute(cityValue, row) {
  return {
    path: '/sales-manager-customer-profile',
    query: { cityValue, customerId: row.customerId, name: row.customerName || '' },
  }
}

export function money(value) {
  return `${Math.round(Number(value || 0)).toLocaleString('en-US')} د.ع`
}

export const ARCHIVE_MANAGER_NAV = [
  { title: 'الرئيسية', to: { name: 'archive-overview' }, icon: { icon: 'tabler-smart-home', size: '25' } },
  { title: 'أرشيف المبيعات', to: { name: 'archive-sales' }, icon: { icon: 'tabler-file-stack', size: '25' } },
  { title: 'طلبات المتابعين', to: { name: 'archive-requests' }, icon: { icon: 'tabler-inbox', size: '25' } },
  { title: 'المنتهون (30 يوم)', to: { name: 'archive-finished-customers' }, icon: { icon: 'tabler-user-check', size: '25' } },
]

export const COMMITTEE_ARCHIVE_NAV = {
  title: 'الأرشيف',
  to: { name: 'committee-archive' },
  icon: { icon: 'tabler-archive', size: '25' },
}
