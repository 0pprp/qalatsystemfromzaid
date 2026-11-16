// Token persistence (localStorage). Kept separate from the Pinia store so the axios client can use it.
const ACCESS = 'qa.accessToken'
const ACCESS_EXP = 'qa.accessTokenExpiresAt'
const REFRESH = 'qa.refreshToken'

const listeners = new Set()

export const tokens = {
  get access() {
    try { return localStorage.getItem(ACCESS) } catch { return null }
  },
  get refresh() {
    try { return localStorage.getItem(REFRESH) } catch { return null }
  },
  get accessExpiresAt() {
    try { return localStorage.getItem(ACCESS_EXP) } catch { return null }
  },
  set({ accessToken, refreshToken, accessTokenExpiresAt }) {
    try {
      if (accessToken) localStorage.setItem(ACCESS, accessToken)
      if (refreshToken) localStorage.setItem(REFRESH, refreshToken)
      if (accessTokenExpiresAt) localStorage.setItem(ACCESS_EXP, accessTokenExpiresAt)
    } catch { /* ignore */ }
  },
  clear() {
    try {
      localStorage.removeItem(ACCESS)
      localStorage.removeItem(REFRESH)
      localStorage.removeItem(ACCESS_EXP)
    } catch { /* ignore */ }
  },
  /** Subscribe to forced logouts (refresh failed). */
  onExpired(fn) {
    listeners.add(fn)
    return () => listeners.delete(fn)
  },
  emitExpired() {
    listeners.forEach((fn) => fn())
  },
}
