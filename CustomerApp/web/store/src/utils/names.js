// Each name part must be a single Arabic word (contract §5).
export const ARABIC_WORD_RE = /^[ء-يٱ-ۓۺ-ۿ]+$/

export function cleanName(v) {
  return String(v ?? '').trim().replace(/\s+/g, ' ')
}
export function isArabicSingleWord(v) {
  const s = cleanName(v)
  return !!s && !s.includes(' ') && ARABIC_WORD_RE.test(s)
}
export const nameRule = (label) => (v) =>
  !cleanName(v) ? `${label} مطلوب` : isArabicSingleWord(v) || `${label}: كلمة واحدة بالأحرف العربية`
export const optionalNameRule = (v) =>
  !cleanName(v) ? true : ARABIC_WORD_RE.test(cleanName(v).replace(/ /g, '')) || 'أحرف عربية فقط'
export const requiredRule = (label) => (v) =>
  (v !== null && v !== undefined && String(v).trim() !== '') || `${label} مطلوب`
