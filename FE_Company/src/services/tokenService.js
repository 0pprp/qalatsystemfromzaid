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
  localStorage.removeItem('SalesManagerScope')
  localStorage.removeItem('LinkCity')
  localStorage.removeItem('CityName')
  localStorage.removeItem('Database')
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
