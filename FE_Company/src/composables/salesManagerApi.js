import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'
import { DEMO_API, DEMO_BRANCH_VALUE, isDemo } from '@/composables/useCities'
import { isCentralSalesManager, salesGatewayBase } from '@/composables/useSalesBranches'

export function parseUtcMillis(value) {
  if (value == null || value === '')
    return Number.NaN
  if (typeof value === 'number')
    return value
  if (value instanceof Date)
    return value.getTime()

  const text = String(value).trim()
  const hasZone = /Z$|[+-]\d{2}:?\d{2}$/i.test(text)
  const normalized = hasZone ? text : `${text.replace(' ', 'T')}Z`

  return Date.parse(normalized)
}

export function shouldMoveMarker(currentCapturedAt, incomingCapturedAt) {
  const incoming = parseUtcMillis(incomingCapturedAt)
  if (Number.isNaN(incoming))
    return false

  const current = parseUtcMillis(currentCapturedAt)
  if (Number.isNaN(current))
    return true

  return incoming > current
}

export function salesManagerBase() {
  if (isDemo())
    return `${DEMO_API}sales-manager/`

  if (isCentralSalesManager())
    return `${salesGatewayBase()}sales-manager/`

  return `${localStorage.getItem('LinkCity') || ''}sales-manager/`
}

export function withCityQuery(path, cityValue) {
  if (!cityValue)
    return path
  const join = path.includes('?') ? '&' : '?'

  return `${path}${join}cityValue=${encodeURIComponent(cityValue)}`
}

export function employeeApiPath(cityValue, employeeId, suffix = '') {
  if (isDemo() || !isCentralSalesManager())
    return `employees/${employeeId}${suffix}`

  return `employees/${encodeURIComponent(cityValue)}/${employeeId}${suffix}`
}

export function normalizeEmployeeRows(data) {
  const list = Array.isArray(data)
    ? data
    : Array.isArray(data?.data)
      ? data.data
      : Array.isArray(data?.employees)
        ? data.employees
        : Array.isArray(data?.Employees)
          ? data.Employees
          : []

  return list.map(row => ({
    ...row,
    employeeId: row.employeeId ?? row.EmployeeId,
    employeeName: row.employeeName || row.EmployeeName || '',
    cityValue: row.cityValue || row.CityValue || '',
    cityName: row.cityName || row.CityName || '',
  }))
}

export async function smGetEmployees(cityValue, extraQuery = '') {
  const extra = String(extraQuery || '').replace(/^\?/, '')
  const apply = path => {
    if (!extra)
      return path

    return `${path}${path.includes('?') ? '&' : '?'}${extra}`
  }

  const keys = []
  const add = value => {
    if (value == null)
      return
    const key = String(value)
    if (!keys.includes(key))
      keys.push(key)
  }

  add(cityValue || '')
  if (isDemo()) {
    add(DEMO_BRANCH_VALUE)
    add('najaf-demo')
    add('')
  }

  let last = []
  let lastError = null
  let gotOk = false
  for (const key of keys) {
    try {
      const path = apply(key ? withCityQuery('employees', key) : 'employees')
      last = normalizeEmployeeRows(await smGet(path))
      gotOk = true
      if (last.length)
        return last
    }
    catch (err) {
      lastError = err
      if (!isDemo())
        throw err
    }
  }

  if (!gotOk && lastError)
    throw lastError

  return last
}

export function branchRowKey(row, idField = 'employeeId') {
  return `${row.cityValue || row.branchName || ''}:${row[idField]}`
}

export async function smGet(path) {
  const { data } = await axios.get(`${salesManagerBase()}${path}`, { headers: getAuthHeaders() })

  return data
}

export async function smPost(path, body) {
  const { data } = await axios.post(`${salesManagerBase()}${path}`, body, { headers: getAuthHeaders() })

  return data
}

export async function smGetBlob(path) {
  const { data } = await axios.get(`${salesManagerBase()}${path}`, {
    headers: getAuthHeaders(),
    responseType: 'blob',
  })

  return data
}

export function evaluationLabel(level) {
  const map = {
    1: 'مرفوض',
    2: 'مقبول',
    3: 'جيد',
    4: 'جيد جداً',
    5: 'ممتاز',
  }

  return map[level] || map[String(level)] || level || ''
}

export const locationStatusLabel = {
  Live: 'مباشر',
  Stale: 'متأخر',
  Offline: 'بدون اتصال',
  NoLocation: 'بدون موقع',
  NoShift: 'بدون دوام',
}

export const requestStatusLabel = {
  New: 'جديدة / غير مسندة',
  Assigned: 'قيد المعالجة',
  Viewed: 'قيد المعالجة',
  Pending: 'معلقة',
  PreparedForSale: 'مجهز للبيع',
  InProgress: 'مجهز للبيع',
  Returned: 'معاد للموظف',
  ConvertedToSale: 'مجهز للبيع',
  Completed: 'مكتمل',
  Rejected: 'مرفوض',
}

export const requestHistoryLabel = {
  Created: 'إنشاء',
  Assigned: 'إسناد',
  Viewed: 'مشاهدة',
  Pending: 'تعليق',
  PendingNote: 'ملاحظة تعليق',
  PreparedForSale: 'تجهيز للبيع',
  Rejected: 'رفض',
  RejectionReason: 'سبب الرفض',
  Returned: 'إعادة للموظف',
  ReturnNote: 'ملاحظة الإعادة',
  ConvertedToSale: 'تحويل إلى بيع',
  Completed: 'اكتمال',
}
