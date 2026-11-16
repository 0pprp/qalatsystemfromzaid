/** Fully paid account with a single receipt >= 20% of the sale in the 30 days up to the last payment. */

export const CLOSING_LUMP_SHARE = 0.2
export const CLOSING_LUMP_LOOKBACK_DAYS = 30

export function paymentDayKey(value) {
  if (value == null || value === '')
    return ''
  if (value instanceof Date && !Number.isNaN(value.getTime())) {
    const y = value.getFullYear()
    const m = String(value.getMonth() + 1).padStart(2, '0')
    const d = String(value.getDate()).padStart(2, '0')

    return `${y}-${m}-${d}`
  }
  const match = String(value).trim().match(/^(\d{4})-(\d{2})-(\d{2})/)
  if (match)
    return `${match[1]}-${match[2]}-${match[3]}`
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime()))
    return ''
  const y = parsed.getFullYear()
  const m = String(parsed.getMonth() + 1).padStart(2, '0')
  const d = String(parsed.getDate()).padStart(2, '0')

  return `${y}-${m}-${d}`
}

function dayUtc(key) {
  const [y, m, d] = key.split('-').map(Number)

  return Date.UTC(y, m - 1, d)
}

export function isFinishedAccount(totalSale, remaining) {
  const total = Number(totalSale)
  const left = Number(remaining)

  return Number.isFinite(total) && total > 0 && Number.isFinite(left) && left >= 0 && left < 1
}

export function isLargeClosingAmount(amount, totalSale) {
  const paid = Number(amount)
  const total = Number(totalSale)
  if (!Number.isFinite(paid) || !Number.isFinite(total) || total <= 0 || paid <= 0)
    return false

  return paid + 0.5 >= total * CLOSING_LUMP_SHARE
}

export function formatClosingLumpNote(date, amount) {
  const key = paymentDayKey(date)
  if (!key)
    return ''
  const [, month, day] = key.split('-')
  const money = Math.round(Number(amount) || 0).toLocaleString('en-US')

  return `في يوم ${Number(day)}/${Number(month)} دفع ${money} دينار`
}

export function findClosingLumpPayments(payments, totalSale, finished) {
  const total = Number(totalSale)
  if (!finished || !(total > 0))
    return []
  const rows = []
  for (const pay of payments || []) {
    const day = paymentDayKey(pay?.date)
    const amount = Number(pay?.amount)
    if (!day || !(amount > 0))
      continue
    rows.push({ date: pay.date, day, amount, utc: dayUtc(day) })
  }
  if (!rows.length)
    return []
  const last = Math.max(...rows.map(row => row.utc))
  const start = last - CLOSING_LUMP_LOOKBACK_DAYS * 86400000

  return rows
    .filter(row => row.utc >= start && row.utc <= last && isLargeClosingAmount(row.amount, total))
    .sort((a, b) => b.utc - a.utc || b.amount - a.amount)
    .map(row => ({
      date: row.date,
      amount: row.amount,
      text: formatClosingLumpNote(row.day, row.amount),
    }))
}

export function notesFromHit(hit) {
  const list = hit?.largeClosingPayments || hit?.LargeClosingPayments
  if (!Array.isArray(list))
    return []

  return list.map((note) => {
    const date = note?.paymentDate || note?.PaymentDate
    const amount = Number(note?.amount ?? note?.Amount ?? 0)
    const text = String(note?.text || note?.Text || formatClosingLumpNote(date, amount) || '').trim()

    return { text, date, amount }
  }).filter(note => note.text)
}

export function isClosingLumpRow(payment, payments, totalSale, finished) {
  const key = paymentDayKey(payment?.date)
  const amount = Number(payment?.amount)
  if (!key || !Number.isFinite(amount))
    return false

  return findClosingLumpPayments(payments, totalSale, finished)
    .some(note => paymentDayKey(note.date) === key && Math.abs(Number(note.amount) - amount) < 0.5)
}
