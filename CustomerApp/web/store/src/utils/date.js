const fmtDate = new Intl.DateTimeFormat('ar-IQ-u-nu-latn', { year: 'numeric', month: 'long', day: 'numeric' })
const fmtDateTime = new Intl.DateTimeFormat('ar-IQ-u-nu-latn', {
  year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit',
})
const fmtMonth = new Intl.DateTimeFormat('ar-IQ-u-nu-latn', { year: 'numeric', month: 'long', timeZone: 'UTC' })

export function formatDate(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  return isNaN(d) ? '—' : fmtDate.format(d)
}
export function formatDateTime(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  return isNaN(d) ? '—' : fmtDateTime.format(d)
}
/** "2026-11" -> month name + year */
export function formatMonth(ym) {
  if (!ym) return ''
  const [y, m] = ym.split('-').map(Number)
  return fmtMonth.format(new Date(Date.UTC(y, m - 1, 15)))
}
export function timeAgo(iso) {
  if (!iso) return ''
  const diff = (Date.now() - new Date(iso).getTime()) / 1000
  if (diff < 60) return 'الآن'
  if (diff < 3600) return `قبل ${Math.floor(diff / 60)} دقيقة`
  if (diff < 86400) return `قبل ${Math.floor(diff / 3600)} ساعة`
  if (diff < 86400 * 7) return `قبل ${Math.floor(diff / 86400)} يوم`
  return formatDate(iso)
}
