import { describe, it, expect } from 'vitest'
import { emptyCheckoutForm, buildOrderPayload, validateOrderPayload, commonBranches } from '../src/utils/orderPayload.js'

const items = [{ productId: 10, quantity: 1, product: { name: 'x' } }]

function validForm(overrides = {}) {
  return {
    ...emptyCheckoutForm(),
    purchaseType: 'Cash',
    forSelf: true,
    firstName: ' علي ',
    fatherName: 'حسين',
    grandfatherName: 'محمد',
    surname: '',
    phone: '+9647701234567',
    cityValue: 1,
    area: 'حي الأنصار',
    details: 'شارع 20',
    ...overrides,
  }
}

describe('buildOrderPayload', () => {
  it('produces the exact contract shape for cash orders', () => {
    const p = buildOrderPayload(validForm(), items)
    expect(p).toEqual({
      cityValue: 1,
      purchaseType: 'Cash',
      forSelf: true,
      customer: { firstName: 'علي', fatherName: 'حسين', grandfatherName: 'محمد', surname: null, phone: '07701234567', secondaryPhone: null },
      address: { area: 'حي الأنصار', details: 'شارع 20', nearestLandmark: null, latitude: null, longitude: null },
      shop: null,
      items: [{ productId: 10, quantity: 1 }],
      notes: null,
    })
  })

  it('includes shop only for daily installments', () => {
    const p = buildOrderPayload(
      validForm({ purchaseType: 'DailyInstallments', shopName: 'أسواق النور', shopBusinessType: 'مواد غذائية', ownsShop: false }),
      items,
    )
    expect(p.shop).toEqual({ name: 'أسواق النور', businessType: 'مواد غذائية', address: null, ownsShop: false })
  })

  it('falls back to the account phone when buying for self', () => {
    const p = buildOrderPayload(validForm({ phone: '' }), items, { accountPhone: '07801234567' })
    expect(p.customer.phone).toBe('07801234567')
    const other = buildOrderPayload(validForm({ phone: '', forSelf: false }), items, { accountPhone: '07801234567' })
    expect(other.customer.phone).toBe('')
  })

  it('keeps coordinates only when both are numbers', () => {
    expect(buildOrderPayload(validForm({ latitude: 32.1, longitude: 44.3 }), items).address).toMatchObject({ latitude: 32.1, longitude: 44.3 })
    expect(buildOrderPayload(validForm({ latitude: 32.1, longitude: null }), items).address).toMatchObject({ latitude: null, longitude: null })
  })
})

describe('validateOrderPayload', () => {
  it('accepts a valid payload', () => {
    expect(validateOrderPayload(buildOrderPayload(validForm(), items))).toEqual({})
  })

  it('requires each name part to be one Arabic word', () => {
    const e = validateOrderPayload(buildOrderPayload(validForm({ firstName: 'علي حسن', fatherName: '', grandfatherName: 'Ali' }), items))
    expect(Object.keys(e).sort()).toEqual(['fatherName', 'firstName', 'grandfatherName'])
  })

  it('requires shop name and business type for installments', () => {
    const e = validateOrderPayload(buildOrderPayload(validForm({ purchaseType: 'DailyInstallments' }), items))
    expect(e.shopName).toBeTruthy()
    expect(e.shopBusinessType).toBeTruthy()
  })

  it('validates phone, address, branch and purchase type', () => {
    const e = validateOrderPayload(buildOrderPayload(validForm({ phone: '0612', area: ' ', details: '', cityValue: null, purchaseType: null }), items))
    expect(e).toHaveProperty('phone')
    expect(e).toHaveProperty('area')
    expect(e).toHaveProperty('details')
    expect(e).toHaveProperty('cityValue')
    expect(e).toHaveProperty('purchaseType')
  })

  it('enforces 1–10 lines and quantity 1–5', () => {
    expect(validateOrderPayload(buildOrderPayload(validForm(), []))).toHaveProperty('items')
    const eleven = Array.from({ length: 11 }, (_, i) => ({ productId: i + 1, quantity: 1 }))
    expect(validateOrderPayload(buildOrderPayload(validForm(), eleven))).toHaveProperty('items')
    expect(validateOrderPayload(buildOrderPayload(validForm(), [{ productId: 1, quantity: 6 }]))).toHaveProperty('items')
  })

  it('checks purchase type and branch availability per product', () => {
    const ctx = { products: { 10: { allowCash: false, allowInstallments: true, availableCityValues: [2] } } }
    const e = validateOrderPayload(buildOrderPayload(validForm(), items), ctx)
    expect(e.items).toBeTruthy()
    expect(e.cityValue).toBeTruthy()
  })
})

describe('commonBranches', () => {
  it('returns branches where all items are available', () => {
    const a = [{ cityValue: 1, name: 'النجف' }, { cityValue: 2, name: 'بغداد' }, { cityValue: 3, name: 'كربلاء' }]
    const b = [{ cityValue: 2, name: 'بغداد' }, { cityValue: 3, name: 'كربلاء' }]
    const c = [{ cityValue: 3, name: 'كربلاء' }]
    expect(commonBranches([a, b, c])).toEqual([{ cityValue: 3, name: 'كربلاء' }])
    expect(commonBranches([])).toEqual([])
  })
})
