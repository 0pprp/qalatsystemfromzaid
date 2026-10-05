/**
 * Client-side duplicate search over sales-request rows (لوحة طلبات البيع).
 * Mirrors BE SalesRequestNameSimilarity + SalesPhoneNormalizer.ForMatch for FE filtering.
 */

const DIACRITICS = new Set([
  '\u064B', '\u064C', '\u064D', '\u064E', '\u064F', '\u0650', '\u0651', '\u0652',
  '\u0670', '\u0653', '\u0654', '\u0655',
])

function pick(row, ...keys) {
  if (!row)
    return ''
  for (const k of keys) {
    const v = row[k]
    if (v != null && String(v).trim() !== '')
      return String(v)
  }
  return ''
}

export function requestCustomerName(row) {
  return pick(row, 'customerName', 'CustomerName')
}

export function requestCustomerPhone(row) {
  return pick(row, 'customerPhone', 'CustomerPhone')
}

export function requestRowKey(row) {
  const id = pick(row, 'id', 'Id') || '0'
  const city = pick(row, 'cityValue', 'CityValue', 'sourceCityValue', 'SourceCityValue')
  return `${city}:${id}`
}

export function normalizeName(value) {
  if (!value || !String(value).trim())
    return ''
  const chars = []
  for (const c of String(value).trim()) {
    if (DIACRITICS.has(c) || c === 'ـ')
      continue
    if (/[\p{P}\p{S}]/u.test(c))
      continue
    chars.push(
      c === 'أ' || c === 'إ' || c === 'آ' || c === 'ٱ' ? 'ا'
        : c === 'ى' ? 'ي'
          : c === 'ة' ? 'ه'
            : c,
    )
  }
  return chars.join('').split(/\s+/).filter(Boolean).join(' ')
}

export function matchingTokens(value) {
  const normalized = normalizeName(value)
  if (!normalized)
    return []
  const parts = normalized.split(' ').filter(Boolean)
  while (parts.length > 0 && /^\d+$/.test(parts[parts.length - 1]))
    parts.pop()
  return parts
}

export function tripleParts(value) {
  const tokens = matchingTokens(value)
  return {
    first: tokens[0] || '',
    father: tokens[1] || '',
    grandfather: tokens[2] || '',
  }
}

export function isTripleNameMatch(queryName, candidateName) {
  const q = matchingTokens(queryName)
  const c = matchingTokens(candidateName)
  if (q.length < 3 || c.length < 3)
    return false
  return q[0] === c[0] && q[1] === c[1] && q[2] === c[2]
}

export function isFatherGrandfatherMatch(queryName, candidateName) {
  const { father, grandfather } = tripleParts(queryName)
  if (!father || !grandfather)
    return false
  const c = matchingTokens(candidateName)
  if (c.length < 3)
    return false
  return father === c[1] && grandfather === c[2]
}

function digitsOnly(value) {
  return String(value || '').replace(/\D+/g, '')
}

/** Canonical 10-digit local mobile without leading zero (e.g. 7800337759). */
export function phoneForMatch(value) {
  let digits = digitsOnly(value)
  if (!digits)
    return ''
  if (digits.startsWith('00964') && digits.length >= 15)
    digits = digits.slice(5)
  else if (digits.startsWith('964') && digits.length >= 13)
    digits = digits.slice(3)
  if (digits.startsWith('0') && digits.length === 11)
    digits = digits.slice(1)
  if (digits.length === 10 && digits.startsWith('7'))
    return digits
  return ''
}

export function isPhoneMatch(a, b) {
  const left = phoneForMatch(a)
  const right = phoneForMatch(b)
  return !!left && left === right
}

/**
 * Free-text filter: name substring (normalized) or phone digits / exact match form.
 * Empty query returns all rows.
 */
export function filterRequestsByQuery(rows, query) {
  const list = Array.isArray(rows) ? rows : []
  const q = String(query || '').trim()
  if (!q)
    return list

  const qName = normalizeName(q)
  const qPhone = phoneForMatch(q)
  const qDigits = digitsOnly(q)

  return list.filter((row) => {
    const name = normalizeName(requestCustomerName(row))
    const phone = requestCustomerPhone(row)
    const phoneCanon = phoneForMatch(phone)
    const phoneDigits = digitsOnly(phone)

    if (qPhone && (qPhone === phoneCanon || phoneDigits.includes(qDigits)))
      return true
    if (!qPhone && qDigits.length >= 4 && phoneDigits.includes(qDigits))
      return true
    if (qName && name.includes(qName))
      return true
    return false
  })
}

function isSameRequest(a, b) {
  return requestRowKey(a) === requestRowKey(b)
}

/**
 * Find other sales-request rows matching the given criterion.
 * @param {'tripleName'|'phone'|'fatherGrandfather'} category
 */
export function findDuplicateRequests(self, rows, category) {
  const list = Array.isArray(rows) ? rows : []
  if (!self)
    return []
  const selfName = requestCustomerName(self)
  const selfPhone = requestCustomerPhone(self)

  return list.filter((row) => {
    if (isSameRequest(self, row))
      return false
    const name = requestCustomerName(row)
    const phone = requestCustomerPhone(row)
    if (category === 'tripleName')
      return isTripleNameMatch(selfName, name)
    if (category === 'phone')
      return isPhoneMatch(selfPhone, phone)
    if (category === 'fatherGrandfather')
      return isFatherGrandfatherMatch(selfName, name)
    return false
  })
}

export function countDuplicateRequests(self, rows, category) {
  return findDuplicateRequests(self, rows, category).length
}
