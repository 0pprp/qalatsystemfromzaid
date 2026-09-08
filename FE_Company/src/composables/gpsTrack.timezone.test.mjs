import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

/** Mirror of formatIraqClock Asia/Baghdad conversion — DB stays UTC. */
function formatIraqClock(ms) {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Baghdad',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).formatToParts(new Date(ms))
  const pick = type => parts.find(p => p.type === type)?.value || ''
  const hour = String(pick('hour')).padStart(2, '0')
  const minute = pick('minute').padStart(2, '0')
  const suffix = /p/i.test(pick('dayPeriod')) ? 'مساءً' : 'صباحاً'

  return `${hour}:${minute} ${suffix}`
}

describe('Iraq timezone display', () => {
  it('UTC 18:40 -> Iraq 21:40 (09:40 مساءً)', () => {
    const utc = Date.UTC(2026, 8, 2, 18, 40, 0)
    assert.equal(formatIraqClock(utc), '09:40 مساءً')
    assert.equal(new Date(utc).getUTCHours() + 3, 21)
  })
})
