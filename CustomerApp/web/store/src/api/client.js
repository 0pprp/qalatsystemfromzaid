import axios from 'axios'
import { tokens } from './tokens.js'

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api/v1/'
export const USE_FAKE_API = import.meta.env.VITE_USE_FAKE_API === 'true'

export const http = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
})

// Fake API mode: plug an in-memory adapter (lazy-loaded so it is not shipped in real builds' main chunk).
if (import.meta.env.VITE_USE_FAKE_API === 'true') {
  let adapterPromise
  http.defaults.adapter = async (config) => {
    adapterPromise ||= import('./fake/adapter.js')
    const { fakeAdapter } = await adapterPromise
    return fakeAdapter(config)
  }
}

http.interceptors.request.use((config) => {
  const token = tokens.access
  if (token && !config.headers.Authorization && !config._skipAuth) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// ---- refresh-on-401 (single flight) ----
let refreshPromise = null

async function refreshTokens() {
  const refreshToken = tokens.refresh
  if (!refreshToken) throw new Error('no refresh token')
  const { data } = await http.post('auth/refresh', { refreshToken }, { _skipAuth: true, _isRefresh: true })
  tokens.set(data)
  return data
}

http.interceptors.response.use(
  (res) => res,
  async (error) => {
    const { config, response } = error
    if (!config || !response) return Promise.reject(error)
    const isAuthCall = config._isRefresh || /auth\/(otp|refresh)/.test(config.url || '')
    if (response.status === 401 && !config._retried && !isAuthCall && tokens.refresh) {
      config._retried = true
      try {
        refreshPromise ||= refreshTokens().finally(() => {
          refreshPromise = null
        })
        await refreshPromise
        config.headers.Authorization = `Bearer ${tokens.access}`
        return http(config)
      } catch (e) {
        tokens.clear()
        tokens.emitExpired()
        return Promise.reject(error)
      }
    }
    if (response.status === 401 && !isAuthCall && !tokens.refresh && config.headers?.Authorization) {
      tokens.clear()
      tokens.emitExpired()
    }
    return Promise.reject(error)
  },
)

export default http
