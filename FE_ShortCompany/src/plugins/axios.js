import { getToken, removeLocalStorage } from '@/services/tokenService'
import axios from 'axios'

const rawUrl = (import.meta.env.VITE_API_BASE_URL || "").replace(/\/+$/, '/')
const apiUrl = /^https?:\/\//i.test(rawUrl) ? rawUrl : `https://${rawUrl}`

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

axios.interceptors.response.use(
  response => response,
  async error => {
    const originalRequest = error.config

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
        
        // Call the refresh endpoint
        const { data } = await axios.post(`${apiUrl}Users/Users_RefreshToken`, {
          Token: oldToken,
        })

        if (data && data.token) {
          // Update Local Storage
          localStorage.setItem('Token', data.token)
          if (data.expiration) localStorage.setItem('Expiration', data.expiration)
          
          // Update Authorization header for future requests
          axios.defaults.headers.common['Authorization'] = 'Bearer ' + data.token
          
          processQueue(null, data.token)
          
          // Retry the original request
          originalRequest.headers['Authorization'] = 'Bearer ' + data.token
          
          return axios(originalRequest)
        } else {
          throw new Error("Invalid token refresh response")
        }
      } catch (err) {
        processQueue(err, null)

        // Logout user on failure
        removeLocalStorage()
        window.location.href = '/login' 
        
        return Promise.reject(err)
      } finally {
        isRefreshing = false
      }
    }

    return Promise.reject(error)
  },
)
