import { getToken, removeLocalStorage } from '@/services/tokenService'
import axios from 'axios'

const rawUrl = (import.meta.env.VITE_API_BASE_URL || "").replace(/\/+$/, '/')
const apiUrl = /^https?:\/\//i.test(rawUrl) ? rawUrl : `https://${rawUrl}`

const refreshClient = axios.create({ timeout: 20000 })

let isRefreshing = false
let failedQueue = []

const processQueue = (error, token = null) => {
  failedQueue.forEach(prom => {
    if (error) {
      prom.reject(error)
    } else {
      prom.resolve(token)
    }
  })
  failedQueue = []
}

function isRefreshRequest(config) {
  if (!config) return false
  if (config.__isRefreshRequest) return true
  const url = String(config.url || '')
  return url.includes('Users_RefreshToken')
}

function forceLogout() {
  removeLocalStorage()
  window.location.href = '/login'
}

axios.interceptors.response.use(
  response => response,
  async error => {
    const originalRequest = error.config

    if (!originalRequest || isRefreshRequest(originalRequest)) {
      return Promise.reject(error)
    }

    if (error.response?.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise(function(resolve, reject) {
          failedQueue.push({ resolve, reject })
        }).then(token => {
          originalRequest.headers['Authorization'] = 'Bearer ' + token
          return axios(originalRequest)
        }).catch(err => {
          return Promise.reject(err)
        })
      }

      originalRequest._retry = true
      isRefreshing = true

      try {
        const oldToken = getToken()
        const { data } = await refreshClient.post(
          `${apiUrl}Users/Users_RefreshToken`,
          { Token: oldToken },
          { __isRefreshRequest: true },
        )

        if (data && data.token) {
          localStorage.setItem('Token', data.token)
          if (data.expiration) localStorage.setItem('Expiration', data.expiration)
          axios.defaults.headers.common['Authorization'] = 'Bearer ' + data.token
          processQueue(null, data.token)
          originalRequest.headers['Authorization'] = 'Bearer ' + data.token
          return axios(originalRequest)
        }

        throw new Error("Invalid token refresh response")
      } catch (err) {
        processQueue(err, null)
        forceLogout()
        return Promise.reject(err)
      } finally {
        isRefreshing = false
      }
    }

    return Promise.reject(error)
  },
)

export { refreshClient, isRefreshRequest, apiUrl }
