// Endpoint modules — one function per endpoint in API_CONTRACT §1–8.
import http from './client.js'

const d = (p) => p.then((r) => r.data)
const clean = (params) =>
  Object.fromEntries(Object.entries(params || {}).filter(([, v]) => v !== null && v !== undefined && v !== ''))

// §1 Public
export const publicApi = {
  branches: () => d(http.get('branches')),
  categories: () => d(http.get('catalog/categories')),
  banners: () => d(http.get('catalog/banners')),
  /** { q, categoryId, cityValue, purchaseType, sort, page, pageSize } */
  products: (params) => d(http.get('catalog/products', { params: clean(params) })),
  product: (id) => d(http.get(`catalog/products/${id}`)),
  appConfig: () => d(http.get('app/config')),
}

// §2 Auth
export const authApi = {
  requestOtp: (phone) => d(http.post('auth/otp/request', { phone, purpose: 'login' })),
  /** body: { requestId, phone, code, registration?, device? } */
  verifyOtp: (body) => d(http.post('auth/otp/verify', body)),
  refresh: (refreshToken) => d(http.post('auth/refresh', { refreshToken }, { _skipAuth: true, _isRefresh: true })),
  logout: (refreshToken) => d(http.post('auth/logout', { refreshToken })),
}

// §3 Account
export const meApi = {
  get: () => d(http.get('me')),
  update: (body) => d(http.put('me', body)),
  preferences: (marketingOptIn) => d(http.put('me/preferences', { marketingOptIn })),
  registerDevice: (body) => d(http.post('me/devices', body)),
  removeDevice: (pushToken) => d(http.delete(`me/devices/${encodeURIComponent(pushToken)}`)),
  deleteAccount: () => d(http.delete('me', { data: { confirm: 'حذف' } })),
  productCommission: (productId) => d(http.get(`me/product-commission/${productId}`)),
}

// §5 Orders
export const ordersApi = {
  create: (payload, idempotencyKey) =>
    d(http.post('orders', payload, { headers: { 'Idempotency-Key': idempotencyKey } })),
  list: (params) => d(http.get('orders', { params: clean(params) })),
  get: (id) => d(http.get(`orders/${id}`)),
  cancel: (id, reason) => d(http.post(`orders/${id}/cancel`, { reason: reason || null })),
}

// §6 Earnings
export const earningsApi = {
  summary: () => d(http.get('earnings/summary')),
  entries: (params) => d(http.get('earnings/entries', { params: clean(params) })),
  requestPayout: (body) => d(http.post('earnings/payout-requests', body)),
  payouts: () => d(http.get('earnings/payout-requests')),
}

// §7 Network
export const networkApi = {
  summary: () => d(http.get('network/summary')),
  direct: (page = 1) => d(http.get('network/direct', { params: { page } })),
  /** { phone, firstName, fatherName, grandfatherName, surname, cityValue } */
  requestAccountOtp: (body) => d(http.post('network/accounts/otp', body)),
  verifyAccount: (body) => d(http.post('network/accounts/verify', body)),
}

// §8 Notifications
export const notificationsApi = {
  list: (page = 1) => d(http.get('notifications', { params: { page } })),
  markRead: (id) => d(http.post(`notifications/${id}/read`)),
  markAllRead: () => d(http.post('notifications/read-all')),
}
