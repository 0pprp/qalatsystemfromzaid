import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'

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
  return `${localStorage.getItem('LinkCity') || ''}sales-manager/`
}

export async function smGet(path) {
  const { data } = await axios.get(`${salesManagerBase()}${path}`, { headers: getAuthHeaders() })

  return data
}

export async function smPost(path, body) {
  const { data } = await axios.post(`${salesManagerBase()}${path}`, body, { headers: getAuthHeaders() })

  return data
}

export const locationStatusLabel = {
  Live: 'مباشر',
  Stale: 'متأخر',
  Offline: 'بدون اتصال',
  NoLocation: 'بدون موقع',
  NoShift: 'بدون دوام',
}

export const requestStatusLabel = {
  New: 'جديد',
  Viewed: 'تمت المشاهدة',
  InProgress: 'قيد المعالجة',
  ConvertedToSale: 'تحول إلى بيع',
  Completed: 'مكتمل',
  Rejected: 'مرفوض',
}
