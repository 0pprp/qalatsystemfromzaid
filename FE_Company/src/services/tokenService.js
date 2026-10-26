import { clearDirectorRatingsCache } from '@/composables/useDirectorRatingsCache'
import { clearAccountantRatingsCache } from '@/composables/useAccountantRatingsCache'

export function getToken() {
  return localStorage.getItem('Token')
}

export function removeToken() {
  localStorage.removeItem('Token')
  localStorage.removeItem('Expiration')
}

function clearSessionCaches() {
  try {
    clearDirectorRatingsCache()
  } catch { /* ignore */ }
  try {
    clearAccountantRatingsCache()
  } catch { /* ignore */ }
  try {
    sessionStorage.removeItem('director-ratings-cache-v8')
    sessionStorage.removeItem('accountant-ratings-cache-v2')
    for (let i = sessionStorage.length - 1; i >= 0; i--) {
      const key = sessionStorage.key(i)
      if (key && (key.startsWith('director-ratings-cache') || key.startsWith('accountant-ratings-cache'))) {
        sessionStorage.removeItem(key)
      }
    }
  } catch {
    // ignore storage errors
  }
}

export function removeLocalStorage() {
  localStorage.removeItem('Token')
  localStorage.removeItem('Expiration')
  localStorage.removeItem('UserName')
  localStorage.removeItem('UserPermissionsTabs')
  localStorage.removeItem('UserID')
  localStorage.removeItem('UserType')
  localStorage.removeItem('Image')
  localStorage.removeItem('FullName')
  localStorage.removeItem('SalesManagerScope')
  localStorage.removeItem('LinkCity')
  localStorage.removeItem('CityName')
  localStorage.removeItem('Database')
  clearSessionCaches()
}

export function isTokenExpired() {
  const expiry = localStorage.getItem('Expiration')

  return !expiry || new Date().getTime() > new Date(expiry).getTime()
}

export function isAuthenticated() {
  return Boolean(getToken()) && !isTokenExpired()
}

export function getAuthHeaders() {
  const token = getToken()

  return token ? {
    "Authorization": `Bearer ${token}`,
    "Content-Type": "application/json",
  } : {}
}
