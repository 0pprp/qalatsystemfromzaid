import { describe, it, expect } from 'vitest'
import { normalizeIraqiPhone, isValidIraqiPhone, formatPhone, maskPhone, toLatinDigits, IRAQI_PHONE_RE } from '../src/utils/phone.js'

describe('Iraqi phone validation', () => {
  it('accepts canonical 07[3-9] numbers', () => {
    for (const p of ['07701234567', '07801234567', '07901234567', '07501234567', '07301234567', '07401234567', '07601234567']) {
      expect(isValidIraqiPhone(p)).toBe(true)
    }
  })

  it('rejects wrong prefixes and lengths', () => {
    for (const p of ['07201234567', '07001234567', '0770123456', '077012345678', '06701234567', '', 'abc', null, undefined]) {
      expect(isValidIraqiPhone(p)).toBe(false)
    }
  })

  it('normalises international and local variants', () => {
    expect(normalizeIraqiPhone('+9647701234567')).toBe('07701234567')
    expect(normalizeIraqiPhone('9647701234567')).toBe('07701234567')
    expect(normalizeIraqiPhone('009647701234567')).toBe('07701234567')
    expect(normalizeIraqiPhone('7701234567')).toBe('07701234567')
    expect(normalizeIraqiPhone('0770 123 4567')).toBe('07701234567')
    expect(normalizeIraqiPhone('0770-123-4567')).toBe('07701234567')
  })

  it('converts Arabic-Indic digits', () => {
    expect(toLatinDigits('٠٧٧٠١٢٣٤٥٦٧')).toBe('07701234567')
    expect(isValidIraqiPhone('٠٧٧٠١٢٣٤٥٦٧')).toBe(true)
  })

  it('canonical regex matches the contract', () => {
    expect(IRAQI_PHONE_RE.source).toBe('^07[3-9]\\d{8}$')
  })

  it('formats and masks', () => {
    expect(formatPhone('07701234567')).toBe('0770 123 4567')
    expect(maskPhone('+9647701234567')).toBe('0770 *** 4567')
    expect(formatPhone('bad')).toBe('bad')
  })
})
