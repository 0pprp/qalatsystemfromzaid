import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'

/** Current-branch accountant ratings (LinkCity) — fast, no gateway fan-out. */
function base() {
  const link = String(localStorage.getItem('LinkCity') || '').trim()
  const root = link.endsWith('/') ? link : `${link}/`

  return `${root}accountant/`
}

export async function accountantGet(path, { timeoutMs } = {}) {
  const { data } = await axios.get(`${base()}${path}`, {
    headers: getAuthHeaders(),
    timeout: timeoutMs ?? 30000,
  })

  return data
}

export async function accountantGetBlob(path, { timeoutMs } = {}) {
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
    catch { /* keep */ }
    const err = new Error(message)
    err.response = { status: response.status, data: { message } }
    throw err
  }

  return data
}

/** Current province only (LinkCity). */
export async function accountantStatsCatalog() {
  return accountantGet('stats/catalog', { timeoutMs: 60000 })
}

export async function accountantStatsBranchMonth() {
  return accountantGet('stats/branch-month', { timeoutMs: 60000 })
}

export async function accountantStatsTrend({ scope, id, fromDate, toDate }) {
  const params = new URLSearchParams({
    scope: String(scope || 'branch'),
    id: String(id ?? 0),
  })
  if (fromDate)
    params.set('fromDate', fromDate)
  if (toDate)
    params.set('toDate', toDate)

  return accountantGet(`stats/trend?${params}`, { timeoutMs: 120000 })
}

export async function accountantStatsRatingCustomers({
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

  return accountantGet(`stats/rating-customers?${params}`, { timeoutMs: 120000 })
}

export function accountantCustomerProfilePath(customerId) {
  return `customers/${customerId}/profile`
}

export function accountantCustomerNotesPath(customerId) {
  return `customers/${customerId}/notes`
}

export async function accountantGetRatingEquation(customerId) {
  return accountantGet(`customers/${customerId}/rating-equation`)
}

export async function accountantSetRatingEquation(customerId, factor) {
  const { data } = await axios.put(
    `${base()}customers/${customerId}/rating-equation`,
    { factor: Number(factor) },
    { headers: getAuthHeaders(), timeout: 30000 },
  )

  return data
}

export function accountantSaleShopImagePath(saleId) {
  return `sales/${saleId}/shop-image`
}

export function accountantCustomerDocumentFilePath(documentId) {
  return `customer-documents/${documentId}/file`
}
