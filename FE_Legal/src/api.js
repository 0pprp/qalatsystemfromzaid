import axios from 'axios'

/** Gateway root. Production serves the app next to the gateway, so the relative default works there. */
const BASE = (() => {
  const fromEnv = String(import.meta.env.VITE_LEGAL_API_URL || '').trim()
  if (!fromEnv)
    return '/sales-gw/api/'

  return fromEnv.endsWith('/') ? fromEnv : `${fromEnv}/`
})()

const TOKEN_KEY = 'legal-app.token'
const USER_KEY = 'legal-app.user'
export const ALLOWED_ROLES = ['مدير القانونية', 'المدير']

export function session() {
  try {
    const user = JSON.parse(localStorage.getItem(USER_KEY) || 'null')
    const token = localStorage.getItem(TOKEN_KEY)
    if (!token || !user || new Date(user.expiration).getTime() < Date.now())
      return null

    return { token, ...user }
  }
  catch {
    return null
  }
}

export function signOut() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
}

export async function signIn(userName, password) {
  const { data } = await axios.post(`${BASE}Auth/LoginDirector`, { userName, password })
  if (!data?.token || !ALLOWED_ROLES.includes(data.userType))
    throw new Error('هذا الحساب ليس حساب مدير القانونية')
  localStorage.setItem(TOKEN_KEY, data.token)
  localStorage.setItem(USER_KEY, JSON.stringify({ userName: data.userName, userType: data.userType, expiration: data.expiration }))
}

const http = axios.create({ baseURL: `${BASE}legal-app/`, timeout: 120000 })

http.interceptors.request.use(config => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token)
    config.headers.Authorization = `Bearer ${token}`

  return config
})

http.interceptors.response.use(r => r, error => {
  if (error?.response?.status === 401) {
    signOut()
    if (!location.hash.startsWith('#/login'))
      location.hash = '#/login'
  }

  return Promise.reject(error)
})

const get = (path, params) => http.get(path, { params }).then(r => r.data)
const post = (path, data) => http.post(path, data ?? {}).then(r => r.data)

export const api = {
  board: () => get('board'),
  cases: ({ stage, q, lawyerId } = {}) => get('cases', { stage: stage || undefined, q: q || undefined, lawyerId: lawyerId || undefined }),
  legalCase: id => get(`cases/${id}`),
  archiveCodes: id => get(`cases/${id}/archive-codes`),
  move: (id, to, note, lawyerId) => post(`cases/${id}/move`, { to, note: note || null, lawyerId: lawyerId || null }),
  addStep: (id, stepTypeId, note, date, lawyerId) =>
    post(`cases/${id}/steps`, { stepTypeId, note: note || null, date: date || null, lawyerId: lawyerId || null }),
  stepDone: (id, stepId, done) => post(`cases/${id}/steps/${stepId}/done`, { done }),
  upcomingSteps: ({ lawyerId, caseId } = {}) => get('upcoming-steps', { lawyerId: lawyerId || undefined, caseId: caseId || undefined }),
  deleteStep: (id, stepId) => http.delete(`cases/${id}/steps/${stepId}`).then(r => r.data),
  assignLawyer: (id, lawyerId) => post(`cases/${id}/lawyer`, { lawyerId: lawyerId || null }),
  decision: (id, text, date) => post(`cases/${id}/decision`, { text: text || null, date: date || null }),
  note: (id, note) => post(`cases/${id}/note`, { note: note || null }),
  close: (id, note) => post(`cases/${id}/close`, { note: note || null }),

  stepTypes: () => get('step-types'),
  saveStepType: body => post('step-types', body),
  deleteStepType: id => http.delete(`step-types/${id}`).then(r => r.data),

  lawyers: () => get('lawyers'),
  lawyer: id => get(`lawyers/${id}`),
  saveLawyer: body => post('lawyers', body),
  transfer: (id, amount, note) => post(`lawyers/${id}/transfer`, { amount, note: note || null }),
  giveBack: (id, amount, note) => post(`lawyers/${id}/return`, { amount, note: note || null }),
  addFee(id, { caseId, amount, note, outsideAssignment, image }) {
    const form = new FormData()

    form.append('caseId', caseId)
    form.append('amount', amount)
    form.append('note', note || '')
    form.append('outsideAssignment', outsideAssignment ? 'true' : 'false')
    if (image)
      form.append('image', image)

    return http.post(`lawyers/${id}/fees`, form).then(r => r.data)
  },
  feeImage: txId => http.get(`fees/${txId}/image`, { responseType: 'blob' }).then(r => r.data),

  registers: () => get('registers'),
  companyFunds: (amount, note) => post('registers/company-funds', { amount, note: note || null }),
  expense: (amount, note) => post('registers/expenses', { amount, note: note || null }),

  settings: () => get('settings'),
  setSetting: (key, value) => http.put('settings', { key, value }).then(r => r.data),
}

export function errorMessage(e, fallback) {
  return e?.response?.data?.message || e?.message || fallback
}
