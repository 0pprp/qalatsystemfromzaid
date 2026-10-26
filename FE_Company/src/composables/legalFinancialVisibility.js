/**
 * Legal (IsLegal) customers: الواصل / الباقي are shown (no longer masked).
 * Helpers kept so call sites stay stable; hide always returns false.
 */

function truthyFlag(v) {
  if (v === true || v === 1 || v === '1')
    return true
  if (typeof v === 'string') {
    const s = v.trim().toLowerCase()

    return s === 'true' || s === 'yes' || s === 'قانونية' || s === '1'
  }

  return false
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
 * @param {{ showType?: string } | null} [ctx]
 * @returns {boolean} always false — amounts stay visible for accountant/director
 */
export function shouldHidePaidRemaining(row, ctx = null) {
  return false
}

/** @returns {number|null} null when missing */
export function visibleReceiptsTotal(row, ctx = null) {
  if (shouldHidePaidRemaining(row, ctx))
    return null
  const v = row?.receiptsTotal ?? row?.ReceiptsTotal ?? row?.amountPaid ?? row?.AmountPaid

  return v == null || v === '' ? null : Number(v)
}

/** @returns {number|null} null when missing */
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

/** Sum receipts/remaining for widgets — includes legal customers. */
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
