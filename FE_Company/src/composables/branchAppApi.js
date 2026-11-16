import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'
import { salesGatewayBase } from '@/composables/useSalesBranches'

/** «ادارة الفرع» — تطبيق إدارة الفرع tabs via the gateway (api/director/branch-app/*). */
function base() {
  return `${salesGatewayBase()}director/branch-app/`
}

function qs(params) {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(params || {})) {
    if (v !== undefined && v !== null && v !== '')
      p.append(k, String(v))
  }
  const s = p.toString()

  return s ? `?${s}` : ''
}

async function get(path, params, timeout = 120000) {
  const { data } = await axios.get(`${base()}${path}${qs(params)}`, { headers: getAuthHeaders(), timeout })

  return data
}

async function post(path, params, body) {
  const { data } = await axios.post(`${base()}${path}${qs(params)}`, body ?? {}, { headers: getAuthHeaders(), timeout: 60000 })

  return data
}

export const branchApp = {
  weakWeekPayers: cityValue => get('legal/weak-week-payers', { cityValue }),
  decide: (cityValue, body) => post('legal/decide', { cityValue }, body),
  declining: cityValue => get('declining', { cityValue }),
  wrongSales: (cityValue, { reviewed = false, page = 1, pageSize = 50 } = {}) =>
    get('wrong-sales', { cityValue, reviewed, page, pageSize }),
  wrongSale: id => get(`wrong-sales/${id}`),
  reviewWrongSale: (id, note) => post(`wrong-sales/${id}/review`, null, { note }),
  catalog: cityValue => get('catalog', { cityValue }),
  ratingTrend: ({ cityValue, scope, id, fromDate, toDate }) =>
    get('ratings/trend', { cityValue, scope, id, fromDate, toDate }),
  ratingCustomers: ({ cityValue, scope, id, level, fromDate, toDate }) =>
    get('ratings/customers', { cityValue, scope, id, level, fromDate, toDate }),
  waivers: (cityValue, { status, page = 1, pageSize = 100 } = {}) =>
    get('waivers', { cityValue, status, page, pageSize }),
  decisions: (cityValue, { fromDate, toDate } = {}) => get('decisions', { cityValue, fromDate, toDate }),
  sales: cityValue => get('sales', { cityValue }),
  sale: (cityValue, saleId) => get(`sales/${saleId}`, { cityValue }),
  payments: ({ cityValue, listId, fromDate, toDate, q }) =>
    get('payments', { cityValue, listId, fromDate, toDate, q }),
}

/** First non-empty value among camelCase / PascalCase keys. */
export function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const k of keys) {
    if (obj[k] !== undefined && obj[k] !== null && obj[k] !== '')
      return obj[k]
  }

  return undefined
}

export function asList(data) {
  if (Array.isArray(data))
    return data
  const inner = data?.items ?? data?.Items ?? data?.data ?? data?.Data

  return Array.isArray(inner) ? inner : []
}

export function money(n) {
  if (n === undefined || n === null || n === '' || Number.isNaN(Number(n)))
    return '—'

  return Number(n).toLocaleString('en-US', { maximumFractionDigits: 0 })
}

export function fmtDate(v, withTime = false) {
  if (!v)
    return '—'
  const d = new Date(v)
  if (Number.isNaN(d.getTime()))
    return String(v)
  const day = d.toLocaleDateString('en-GB')

  return withTime ? `${day} ${d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}` : day
}

export function isoDay(d) {
  const x = d instanceof Date ? d : new Date(d)
  const m = String(x.getMonth() + 1).padStart(2, '0')
  const day = String(x.getDate()).padStart(2, '0')

  return `${x.getFullYear()}-${m}-${day}`
}

export function daysAgo(n) {
  const d = new Date()
  d.setDate(d.getDate() - n)

  return isoDay(d)
}

export function errorText(e, fallback) {
  return e?.response?.data?.message || e?.response?.data?.Message || (typeof e?.response?.data === 'string' ? e.response.data : '') || fallback
}
