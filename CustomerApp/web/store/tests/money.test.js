import { describe, it, expect } from 'vitest'
import { formatMoney, formatNumber, parseMoney } from '../src/utils/money.js'

describe('money formatting (IQD, Latin digits)', () => {
  it('adds comma thousands separators and currency', () => {
    expect(formatMoney(123000)).toBe('123,000 د.ع')
    expect(formatMoney(1250000)).toBe('1,250,000 د.ع')
    expect(formatMoney(650)).toBe('650 د.ع')
    expect(formatMoney(0)).toBe('0 د.ع')
  })

  it('handles negatives (clawbacks) and rounding', () => {
    expect(formatMoney(-3000)).toBe('-3,000 د.ع')
    expect(formatNumber(1234.6)).toBe('1,235')
  })

  it('can omit the currency', () => {
    expect(formatMoney(15000, { currency: false })).toBe('15,000')
  })

  it('returns a dash for missing values', () => {
    expect(formatMoney(null)).toBe('—')
    expect(formatMoney(undefined)).toBe('—')
  })

  it('parses user input including Arabic digits', () => {
    expect(parseMoney('15,000')).toBe(15000)
    expect(parseMoney('١٥٠٠٠')).toBe(15000)
    expect(parseMoney('')).toBe(0)
  })
})
