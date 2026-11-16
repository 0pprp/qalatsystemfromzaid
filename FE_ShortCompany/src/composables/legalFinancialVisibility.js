/**
 * Legal (IsLegal) customers: الواصل / الباقي stay hidden for every user
 * except المحاسب الرئيسي. Sale orders are not covered here.
 */

const MAIN_ACCOUNTANT = 'محاسب رئيسي'

function truthyFlag(v) {
  if (v === true || v === 1 || v === '1')
    return true
  if (typeof v === 'string') {
    const s = v.trim().toLowerCase()

    return s === 'true' || s === 'yes' || s === 'قانونية' || s === '1'
  }

  return false
}

function viewerUserType(ctx) {
  if (ctx && Object.prototype.hasOwnProperty.call(ctx, 'userType'))
    return String(ctx.userType ?? '').trim()

  try {
    if (typeof localStorage === 'undefined' || !localStorage)
      return ''

    return String(localStorage.getItem('UserType') || '').trim()
  }
  catch {
    return ''
  }
}

export function isMainAccountantViewer(ctx = null) {
  return viewerUserType(ctx) === MAIN_ACCOUNTANT
}

export function isLegalCustomer(row) {
  if (!row || typeof row !== 'object')
    return false

  return truthyFlag(row.isLegal)
    || truthyFlag(row.IsLegal)
    || truthyFlag(row.hidePaidRemaining)
    || truthyFlag(row.HidePaidRemaining)
}

/**
 * @param {object} row
 * @param {{ showType?: string, userType?: string } | null} [ctx]
 * @returns {boolean}
 */
export function shouldHidePaidRemaining(row, ctx = null) {
  if (isMainAccountantViewer(ctx))
    return false
  if (ctx?.showType === 'القانونية')
    return true

  return isLegalCustomer(row)
}

/** @returns {number|null} null when hidden or missing */
export function visibleReceiptsTotal(row, ctx = null) {
  if (shouldHidePaidRemaining(row, ctx))
    return null
  const v = row?.receiptsTotal ?? row?.ReceiptsTotal ?? row?.amountPaid ?? row?.AmountPaid

  return v == null || v === '' ? null : Number(v)
}

/** @returns {number|null} null when hidden or missing */
export function visibleAmountRemaining(row, ctx = null) {
  if (shouldHidePaidRemaining(row, ctx))
    return null
  const v = row?.amountRemaining ?? row?.AmountRemaining

  return v == null || v === '' ? null : Number(v)
}

export function formatHiddenFinancial(value, formatFn) {
  if (value == null || Number.isNaN(value))
    return '—'

  return typeof formatFn === 'function' ? formatFn(value) : String(value)
}

/** Sum receipts/remaining for widgets — skips rows whose amounts are hidden. */
export function sumVisibleFinancial(rows, picker, ctx = null) {
  let sum = 0
  for (const row of rows || []) {
    if (shouldHidePaidRemaining(row, ctx))
      continue
    const n = Number(picker(row) || 0)
    if (Number.isFinite(n))
      sum += n
  }

  return sum
}
