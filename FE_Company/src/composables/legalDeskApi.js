import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'
import { salesGatewayBase } from '@/composables/useSalesBranches'

/**
 * القانونية — company-dashboard side (محاسب القانونية + المدير), through the sales gateway.
 * Amounts are whole Iraqi dinars.
 */
async function call(method, path, { data, params } = {}) {
  const { data: body } = await axios({
    method,
    url: `${salesGatewayBase()}legal-desk/${path}`,
    data,
    params,
    headers: getAuthHeaders(),
    timeout: 120000,
  })

  return body
}

export const legalCities = () => call('get', 'cities')
export const legalLists = cityValue => call('get', 'lists', { params: { cityValue } })
/** scope: 'legal' (customers marked قانونية) or 'all' (any customer, finished sales included). */
export const legalCustomers = (cityValue, { delegateId, q, scope } = {}) =>
  call('get', 'customers', { params: { cityValue, delegateId: delegateId || undefined, q: q || undefined, scope: scope || 'legal' } })
export const legalSimilar = (cityValue, customerId) => call('get', 'customers/similar', { params: { cityValue, customerId } })
export const legalCustomerDetail = (cityValue, customerId) => call('get', 'customers/detail', { params: { cityValue, customerId } })
/** { rows, failedBranches } — every branch. */
export const legalSearch = q => call('get', 'search', { params: { q } })

export const legalPersons = ({ stage, q } = {}) => call('get', 'persons', { params: { stage: stage || undefined, q: q || undefined } })
export const legalPerson = id => call('get', `persons/${id}`)
export const legalCreatePerson = body => call('post', 'persons', { data: body })
export const legalUpdatePerson = (id, body) => call('put', `persons/${id}`, { data: body })
export const legalDeletePerson = id => call('delete', `persons/${id}`)
export const legalAddAccounts = (id, accounts) => call('post', `persons/${id}/accounts`, { data: { accounts } })
export const legalRemoveAccount = (id, accountId) => call('delete', `persons/${id}/accounts/${accountId}`)
export const legalSend = (id, note) => call('post', `persons/${id}/send`, { data: { note: note || null } })
/** lines: [{ accountId, amount }] */
export const legalPay = (id, lines, penaltyAmount, note) =>
  call('post', `persons/${id}/payments`, { data: { lines, penaltyAmount, note: note || null } })

/** خصم: one amount taken off what the debtor owes. No receipt. */
export const legalDiscount = (id, amount, note) =>
  call('post', `persons/${id}/discounts`, { data: { amount, note: note || null } })

export const legalOverview = () => call('get', 'overview')
/** المدير only. */
export const legalStalled = days => call('get', 'stalled', { params: { days } })
export const legalRegisters = () => call('get', 'registers')
/** kind: 'Payment' (default) or 'Discount'. */
export const legalPayments = (q, kind) => call('get', 'payments', { params: { q: q || undefined, kind: kind || undefined, take: 1000 } })
export const legalOldReceipts = q => call('get', 'old-receipts', { params: { q: q || undefined, take: 2000 } })

/** The receipt of one تسديد as a PDF blob. */
export async function legalReceiptPdf(paymentId) {
  const { data } = await axios({
    method: 'get',
    url: `${salesGatewayBase()}legal-desk/payments/${paymentId}/receipt`,
    headers: getAuthHeaders(),
    responseType: 'blob',
    timeout: 120000,
  })

  return data
}

export const legalCashbox = () => call('get', 'cashbox')
/** month: 'yyyy-MM' (Iraq time). */
export const legalLiability = month => call('get', 'liability', { params: { month: month || undefined } })
export const legalRefreshBalances = () => call('post', 'refresh-balances')

export function legalError(e, fallback) {
  return e?.response?.data?.message || e?.message || fallback
}

export function legalMoney(value) {
  return Math.round(Number(value || 0)).toLocaleString('en-US')
}

/** Server timestamps named ...AtUtc come without a zone marker. */
export function formatUtc(raw, withTime = false) {
  if (!raw)
    return '—'
  const text = String(raw)
  const date = new Date(/Z|[+-]\d\d:\d\d$/.test(text) ? text : `${text}Z`)
  if (Number.isNaN(date.getTime()))
    return text.slice(0, 10)
  const day = date.toLocaleDateString('en-GB', { timeZone: 'Asia/Baghdad' })

  return withTime
    ? `${day} ${date.toLocaleTimeString('en-GB', { timeZone: 'Asia/Baghdad', hour: '2-digit', minute: '2-digit' })}`
    : day
}

/** Plain branch dates (sale date, last payment). */
export function legalDay(raw) {
  const parts = String(raw || '').slice(0, 10).split('-')

  return parts.length === 3 ? `${parts[2]}/${parts[1]}/${parts[0]}` : '—'
}

/**
 * What the company side needs to know about a debtor. The stations of the legal app
 * (recently added, archive, decisions…) are the legal team's business and are not shown here.
 */
export const LEGAL_STATUS = {
  NotSent: { label: 'لم يُرسل', color: 'default' },
  Waiting: { label: 'قيد الانتظار', color: 'secondary' },
  Judicial: { label: 'دعوى قضائية', color: 'warning' },
  Execution: { label: 'التنفيذ', color: 'error' },
  Closed: { label: 'مغلق', color: 'success' },
}

/** stage = the debtor's station in the legal app (empty when not sent yet). */
export function legalStatus(stage) {
  let key = 'Waiting'
  if (!stage)
    key = 'NotSent'
  else if (stage === 'Closed')
    key = 'Closed'
  else if (stage === 'Execution')
    key = 'Execution'
  else if (stage === 'Judicial' || stage === 'Decisions')
    key = 'Judicial'

  return { key, ...LEGAL_STATUS[key] }
}

export const CHARGE_KIND = {
  Judicial: 'أجور القانونية',
  Execution: 'أجور التنفيذ',
  EarlySettlement: 'غرامة تسديد كامل',
}

export const LEGAL_DESK_NAV = [
  { title: 'نظرة عامة', to: { name: 'legal-desk-overview' }, icon: { icon: 'tabler-smart-home', size: '22' } },
  { title: 'قوائم القانونية', to: { name: 'legal-desk-lists' }, icon: { icon: 'tabler-list-details', size: '22' } },
  { title: 'المدينون', to: { name: 'legal-desk-debtors' }, icon: { icon: 'tabler-users', size: '22' } },
  { title: 'التسديدات', to: { name: 'legal-desk-payments' }, icon: { icon: 'tabler-cash', size: '22' } },
  { title: 'القاصة', to: { name: 'legal-desk-cashbox' }, icon: { icon: 'tabler-wallet', size: '22' } },
  { title: 'ذمة القانونية', to: { name: 'legal-desk-liability' }, icon: { icon: 'tabler-scale', size: '22' } },
]

/** Extra tab for المدير only. */
export const LEGAL_DESK_DIRECTOR_NAV = [
  { title: 'مدينون بلا تقدم', to: { name: 'legal-desk-stalled' }, icon: { icon: 'tabler-hourglass-off', size: '22' } },
]

export const LEGAL_DESK_NAV_GROUP = {
  title: 'القانونية',
  icon: { icon: 'tabler-gavel', size: '25' },
}

/** Accounts picked on the lists page wait here until they are grouped (kept across branches and reloads). */
const TRAY_KEY = 'legal-desk-tray'

export function readTray() {
  try {
    const rows = JSON.parse(sessionStorage.getItem(TRAY_KEY) || '[]')

    return Array.isArray(rows) ? rows : []
  }
  catch {
    return []
  }
}

export function writeTray(rows) {
  try {
    sessionStorage.setItem(TRAY_KEY, JSON.stringify(rows || []))
  }
  catch { /* ignore */ }
}

export const accountKey = row => `${row.cityValue}:${row.customerId}`
