export function getToken() {
  return localStorage.getItem('Token')
}

export function removeToken() {
  localStorage.removeItem('Token')
  localStorage.removeItem('Expiration')
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
  try {
    for (let i = sessionStorage.length - 1; i >= 0; i--) {
      const key = sessionStorage.key(i)
      if (key && (key.startsWith('director-ratings-cache') || key.startsWith('accountant-ratings-cache'))) {
        sessionStorage.removeItem(key)
      }
    }
  } catch {
    // ignore
  }
}

export function isTokenExpired() {
  const expiry = localStorage.getItem('Expiration')

  return !expiry || new Date().getTime() > new Date(expiry).getTime()
}

export function getAuthHeaders() {
  const token = getToken()

  return token ? {
    "Authorization": `Bearer ${token}`,
    "Content-Type": "application/json",
  } : {}
}
