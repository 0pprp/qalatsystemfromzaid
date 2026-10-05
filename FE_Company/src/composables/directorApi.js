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

export async function directorStatsCatalog(cityValue) {
  const q = encodeURIComponent(cityValue)

  return directorGet(`stats/catalog?cityValue=${q}`)
}

/** Current + previous month new/zeroed/net for one province, or all when cityValue omitted. */
export async function directorStatsBranchMonth(cityValue) {
  const params = new URLSearchParams()
  if (cityValue)
    params.set('cityValue', String(cityValue))
  const qs = params.toString()

  return directorGet(`stats/branch-month${qs ? `?${qs}` : ''}`, { timeoutMs: 120000 })
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
