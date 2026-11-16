// Money is integer IQD. Displayed with Latin digits and comma thousands separators: "1,250,000 د.ع"
export const CURRENCY = 'د.ع'

export function formatNumber(value) {
  const n = Math.round(Number(value) || 0)
  const sign = n < 0 ? '-' : ''
  return sign + String(Math.abs(n)).replace(/\B(?=(\d{3})+(?!\d))/g, ',')
}

export function formatMoney(value, { currency = true } = {}) {
  if (value === null || value === undefined || value === '') return '—'
  const s = formatNumber(value)
  return currency ? `${s} ${CURRENCY}` : s
}

/** Parse a user-entered amount (accepts Arabic digits and separators). */
export function parseMoney(input) {
  const s = String(input ?? '')
    .replace(/[٠-٩]/g, (d) => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)))
    .replace(/[^\d]/g, '')
  return s ? parseInt(s, 10) : 0
}
