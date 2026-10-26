import axios from 'axios'
import { isSummaryReadOnly } from '@/composables/useSummaryReadOnly'

const ALLOW_MUTATION = /Users_(LoginAdmin|LoginEmployee|RefreshToken)|\/login/i

function isAllowedMutationUrl(url, method) {
  const m = String(method || 'GET').toUpperCase()
  if (m === 'GET' || m === 'HEAD' || m === 'OPTIONS')
    return true

  return ALLOW_MUTATION.test(String(url || ''))
}

function readOnlyError() {
  return Object.assign(new Error('SUMMARY_READONLY'), {
    summaryReadOnly: true,
    message: 'رابط الملخص للعرض فقط — لا يمكن تنفيذ هذا الإجراء',
  })
}

export function installSummaryReadOnlyGuard() {
  if (!isSummaryReadOnly)
    return

  axios.interceptors.request.use(
    config => {
      const url = `${config.baseURL || ''}${config.url || ''}`
      if (isAllowedMutationUrl(url, config.method))
        return config

      return Promise.reject(readOnlyError())
    },
    err => Promise.reject(err),
  )

  const nativeFetch = window.fetch.bind(window)
  window.fetch = (input, init = {}) => {
    const url = typeof input === 'string' ? input : (input?.url || '')
    const method = init.method || (typeof input !== 'string' && input?.method) || 'GET'
    if (isAllowedMutationUrl(url, method))
      return nativeFetch(input, init)

    return Promise.reject(readOnlyError())
  }
}
