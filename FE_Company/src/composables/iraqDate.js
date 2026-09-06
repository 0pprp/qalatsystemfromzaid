import { formatIraqClock, formatIraqTime as formatIraqDateTimeClock } from '@/composables/gpsTrack'
import { parseUtcMillis } from '@/composables/salesManagerApi'

const IRAQ_TZ = 'Asia/Baghdad'

function iraqYmdParts(ms) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: IRAQ_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date(ms))
  const pick = type => parts.find(p => p.type === type)?.value || ''

  return {
    year: pick('year'),
    month: pick('month'),
    day: pick('day'),
  }
}

export function formatIraqDate(value) {
  if (value == null || value === '')
    return ''

  const text = String(value).trim()
  const hasZone = /Z$|[+-]\d{2}:?\d{2}$/i.test(text)
  if (!hasZone) {
    const day = text.match(/^(\d{4})[-/](\d{2})[-/](\d{2})/)
    if (day)
      return `${day[1]}/${day[2]}/${day[3]}`
  }

  const ms = hasZone ? Date.parse(text) : parseUtcMillis(value)
  if (!Number.isFinite(ms))
    return ''

  const { year, month, day } = iraqYmdParts(ms)

  return `${year}/${month}/${day}`
}

export function formatIraqTime(value) {
  return formatIraqDateTimeClock(value)
}

export { formatIraqClock }
