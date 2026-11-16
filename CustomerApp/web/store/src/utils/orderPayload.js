// Builds and validates the POST /orders body exactly as defined in API_CONTRACT §5.
import { normalizeIraqiPhone, isValidIraqiPhone } from './phone.js'
import { cleanName, isArabicSingleWord } from './names.js'

export const MAX_ORDER_LINES = 10
export const MAX_LINE_QTY = 5

export function emptyCheckoutForm() {
  return {
    purchaseType: null, // 'Cash' | 'DailyInstallments'
    forSelf: true,
    firstName: '',
    fatherName: '',
    grandfatherName: '',
    surname: '',
    phone: '',
    secondaryPhone: '',
    cityValue: null,
    area: '',
    details: '',
    nearestLandmark: '',
    latitude: null,
    longitude: null,
    shopName: '',
    shopBusinessType: '',
    shopAddress: '',
    ownsShop: true,
    notes: '',
  }
}

const trimOrNull = (v) => {
  const s = String(v ?? '').trim()
  return s ? s : null
}

/**
 * @param {object} form  checkout form (see emptyCheckoutForm)
 * @param {Array<{productId:number, quantity:number}>} items
 * @param {{accountPhone?:string}} [opts]  when forSelf and phone empty, the account phone is used
 */
export function buildOrderPayload(form, items, opts = {}) {
  const phoneInput = form.phone || (form.forSelf ? opts.accountPhone : '')
  const hasCoords = Number.isFinite(form.latitude) && Number.isFinite(form.longitude)
  const payload = {
    cityValue: form.cityValue,
    purchaseType: form.purchaseType,
    forSelf: !!form.forSelf,
    customer: {
      firstName: cleanName(form.firstName),
      fatherName: cleanName(form.fatherName),
      grandfatherName: cleanName(form.grandfatherName),
      surname: trimOrNull(cleanName(form.surname)),
      phone: normalizeIraqiPhone(phoneInput),
      secondaryPhone: form.secondaryPhone ? normalizeIraqiPhone(form.secondaryPhone) : null,
    },
    address: {
      area: String(form.area ?? '').trim(),
      details: String(form.details ?? '').trim(),
      nearestLandmark: trimOrNull(form.nearestLandmark),
      latitude: hasCoords ? form.latitude : null,
      longitude: hasCoords ? form.longitude : null,
    },
    shop:
      form.purchaseType === 'DailyInstallments'
        ? {
            name: String(form.shopName ?? '').trim(),
            businessType: String(form.shopBusinessType ?? '').trim(),
            address: trimOrNull(form.shopAddress),
            ownsShop: !!form.ownsShop,
          }
        : null,
    items: (items || []).map((i) => ({ productId: i.productId, quantity: i.quantity })),
    notes: trimOrNull(form.notes),
  }
  return payload
}

/**
 * Validates a built payload against the contract rules.
 * @param {object} payload result of buildOrderPayload
 * @param {{products?: Record<number, {allowCash:boolean, allowInstallments:boolean, availableCityValues?:number[]}>}} [ctx]
 * @returns {Record<string,string>} field -> Arabic message (empty object = valid)
 */
export function validateOrderPayload(payload, ctx = {}) {
  const e = {}
  if (!payload.purchaseType || !['Cash', 'DailyInstallments'].includes(payload.purchaseType))
    e.purchaseType = 'اختر طريقة الشراء'
  if (!payload.cityValue) e.cityValue = 'اختر الفرع'
  const c = payload.customer || {}
  if (!isArabicSingleWord(c.firstName)) e.firstName = 'الاسم الأول مطلوب (كلمة واحدة بالعربية)'
  if (!isArabicSingleWord(c.fatherName)) e.fatherName = 'اسم الأب مطلوب (كلمة واحدة بالعربية)'
  if (!isArabicSingleWord(c.grandfatherName)) e.grandfatherName = 'اسم الجد مطلوب (كلمة واحدة بالعربية)'
  if (!isValidIraqiPhone(c.phone)) e.phone = 'رقم الهاتف غير صحيح'
  if (c.secondaryPhone && !isValidIraqiPhone(c.secondaryPhone)) e.secondaryPhone = 'رقم الهاتف الثاني غير صحيح'
  const a = payload.address || {}
  if (!a.area) e.area = 'المنطقة مطلوبة'
  if (!a.details) e.details = 'تفاصيل العنوان مطلوبة'
  if (payload.purchaseType === 'DailyInstallments') {
    if (!payload.shop) e.shop = 'معلومات المحل مطلوبة للأقساط اليومية'
    else {
      if (!payload.shop.name) e.shopName = 'اسم المحل مطلوب'
      if (!payload.shop.businessType) e.shopBusinessType = 'نوع النشاط مطلوب'
    }
  }
  const items = payload.items || []
  if (items.length < 1) e.items = 'السلة فارغة'
  else if (items.length > MAX_ORDER_LINES) e.items = `الحد الأقصى ${MAX_ORDER_LINES} منتجات في الطلب الواحد`
  else if (items.some((i) => !Number.isInteger(i.quantity) || i.quantity < 1 || i.quantity > MAX_LINE_QTY))
    e.items = `الكمية لكل منتج من 1 إلى ${MAX_LINE_QTY}`
  else if (ctx.products) {
    for (const i of items) {
      const p = ctx.products[i.productId]
      if (!p) continue
      if (payload.purchaseType === 'Cash' && p.allowCash === false) e.items = 'بعض المنتجات لا تتوفر بالدفع النقدي'
      if (payload.purchaseType === 'DailyInstallments' && p.allowInstallments === false)
        e.items = 'بعض المنتجات لا تتوفر بالأقساط اليومية'
      if (payload.cityValue && p.availableCityValues && !p.availableCityValues.includes(payload.cityValue))
        e.cityValue = 'بعض المنتجات غير متوفرة في هذا الفرع'
    }
  }
  return e
}

/** Branches where ALL items are available (intersection). */
export function commonBranches(productBranchLists) {
  if (!productBranchLists.length) return []
  const [first, ...rest] = productBranchLists
  return first.filter((b) => rest.every((list) => list.some((x) => x.cityValue === b.cityValue)))
}
