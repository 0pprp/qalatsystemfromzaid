// Iraqi mobile numbers only. Canonical form: 07XXXXXXXXX (regex ^07[3-9]\d{8}$).
export const IRAQI_PHONE_RE = /^07[3-9]\d{8}$/

const AR_DIGITS = '٠١٢٣٤٥٦٧٨٩'
const FA_DIGITS = '۰۱۲۳۴۵۶۷۸۹'

/** Convert Arabic-Indic / Persian digits to Latin. */
export function toLatinDigits(value) {
  return String(value ?? '').replace(/[٠-٩۰-۹]/g, (d) => {
    const i = AR_DIGITS.indexOf(d)
    return String(i >= 0 ? i : FA_DIGITS.indexOf(d))
  })
}

/**
 * Normalise user input to canonical 07XXXXXXXXX when possible.
 * Accepts +9647..., 009647..., 9647..., 7XXXXXXXXX, 07XXXXXXXXX (spaces/dashes allowed).
 * Returns the cleaned string even if invalid (use isValidIraqiPhone to check).
 */
export function normalizeIraqiPhone(input) {
  let s = toLatinDigits(input).replace(/[\s\-().]/g, '')
  if (s.startsWith('+')) s = s.slice(1)
  if (s.startsWith('00964')) s = s.slice(5)
  else if (s.startsWith('964')) s = s.slice(3)
  if (/^7\d{9}$/.test(s)) s = '0' + s
  return s
}

export function isValidIraqiPhone(input) {
  if (input === null || input === undefined) return false
  return IRAQI_PHONE_RE.test(normalizeIraqiPhone(input))
}

/** Vuetify rules */
export const phoneRule = (v) =>
  !v ? 'رقم الهاتف مطلوب' : isValidIraqiPhone(v) || 'أدخل رقم موبايل عراقي صحيح يبدأ بـ 07 (11 رقماً)'
export const optionalPhoneRule = (v) => (!v ? true : isValidIraqiPhone(v) || 'رقم موبايل عراقي غير صحيح')

/** 07701234567 -> 0770 123 4567 */
export function formatPhone(phone) {
  const p = normalizeIraqiPhone(phone)
  if (!IRAQI_PHONE_RE.test(p)) return phone ?? ''
  return `${p.slice(0, 4)} ${p.slice(4, 7)} ${p.slice(7)}`
}

/** 07701234567 -> 0770 *** 4567 */
export function maskPhone(phone) {
  const p = normalizeIraqiPhone(phone)
  if (!IRAQI_PHONE_RE.test(p)) return phone ?? ''
  return `${p.slice(0, 4)} *** ${p.slice(7)}`
}
