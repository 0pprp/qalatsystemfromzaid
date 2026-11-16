// In-memory (localStorage-persisted) fake implementation of API_CONTRACT §1–8.
// Enabled with VITE_USE_FAKE_API=true. Demo login: any valid Iraqi phone, OTP code 123456.
// The phone 07701234567 is a pre-seeded account with orders, installment accounts, earnings and a network.
import { AxiosError } from 'axios'
import { branches, categories, products, banners, appConfig, DEMO_PHONE, FAKE_OTP } from './data.js'
import { normalizeIraqiPhone, IRAQI_PHONE_RE } from '../../utils/phone.js'
import { isArabicSingleWord } from '../../utils/names.js'

const DB_KEY = 'qa.fakeDb.v1'
const LATENCY = [250, 650]
const OPEN = ['New', 'UnderReview', 'Transferred', 'FieldInspection']
const STATUS_LABEL = {
  New: 'طلب جديد', UnderReview: 'قيد المعالجة', Denied: 'مرفوض', Transferred: 'محوّل إلى مندوب المبيعات',
  FieldInspection: 'قيد الكشف الميداني', Sold: 'تم البيع', NotSold: 'لم يتم البيع', Cancelled: 'ملغى',
}
const COMMISSION_LABEL = { Pending: 'معلقة', Available: 'مستحقة للصرف', Paid: 'مصروفة', Reversed: 'ملغاة', Frozen: 'مجمدة' }
const PAYOUT_LABEL = { Requested: 'قيد الطلب', Approved: 'موافق عليه', Paid: 'مصروف', Rejected: 'مرفوض' }

const now = () => new Date().toISOString()
const daysAgo = (n, h = 10) => new Date(Date.now() - n * 86400000 - h * 3600000).toISOString()
const rid = () => Math.random().toString(36).slice(2) + Date.now().toString(36)
const cityName = (cv) => branches.find((b) => b.cityValue === cv)?.name ?? ''

// ------------------------------------------------------------------ seed
function seed() {
  const demo = {
    id: 42, phone: DEMO_PHONE, firstName: 'علي', fatherName: 'حسين', grandfatherName: 'محمد', surname: 'الكعبي',
    cityValue: 1, referralCode: 'QA7K2M', referredById: null, referredByName: 'حيدر', marketingOptIn: false,
    completedSalesCount: 6, createdAt: daysAgo(200), deleted: false,
  }
  const direct = ['حسين', 'زينب', 'مصطفى', 'فاطمة', 'كرار'].map((n, i) => ({
    id: 100 + i, phone: `0780000000${i}`, firstName: n, fatherName: 'عبد', grandfatherName: 'كاظم', surname: null,
    cityValue: 1 + (i % 3), referralCode: `QX${i}AB${i}`, referredById: 42, marketingOptIn: false,
    completedSalesCount: i, createdAt: daysAgo(150 - i * 20), deleted: false, networkSize: [3, 0, 7, 1, 0][i],
  }))
  const mkOrder = (id, status, daysBack, productId, opts = {}) => {
    const p = products.find((x) => x.id === productId)
    const pt = opts.purchaseType || 'Cash'
    const created = daysAgo(daysBack)
    const timelineStatuses = {
      New: ['New'], UnderReview: ['New', 'UnderReview'], Transferred: ['New', 'UnderReview', 'Transferred'],
      FieldInspection: ['New', 'UnderReview', 'Transferred', 'FieldInspection'],
      Sold: ['New', 'UnderReview', 'Transferred', 'FieldInspection', 'Sold'], Denied: ['New', 'UnderReview', 'Denied'],
      NotSold: ['New', 'UnderReview', 'Transferred', 'FieldInspection', 'NotSold'], Cancelled: ['New', 'Cancelled'],
    }[status]
    return {
      id, orderNumber: `QO-2611-${String(id).padStart(6, '0')}`, userId: 42, status, purchaseType: pt,
      forSelf: opts.forSelf ?? true, cityValue: opts.cityValue || 1,
      customer: opts.customer || { firstName: 'علي', fatherName: 'حسين', grandfatherName: 'محمد', surname: 'الكعبي', phone: DEMO_PHONE, secondaryPhone: null },
      address: { area: 'حي الأنصار', details: 'شارع 20 قرب جامع الأنصار', nearestLandmark: 'مدرسة الرافدين', latitude: null, longitude: null },
      shop: pt === 'DailyInstallments' ? { name: 'أسواق النور', businessType: 'مواد غذائية', address: 'السوق الكبير', ownsShop: true } : null,
      notes: opts.notes ?? null,
      items: [{ productId: p.id, quantity: 1 }],
      publicReason: status === 'Denied' ? 'تم رفض الطلب لعدم توفر المنتج في منطقتك حالياً' : status === 'NotSold' ? 'لم يتم إتمام البيع بعد الكشف الميداني' : null,
      timeline: timelineStatuses.map((s, i) => ({ status: s, at: daysAgo(daysBack - i * 0.6), note: s === 'Transferred' ? 'تم تحويل طلبك إلى مندوب المبيعات في فرعك' : null })),
      createdAt: created, updatedAt: daysAgo(Math.max(0, daysBack - (timelineStatuses.length - 1) * 0.6)),
    }
  }
  const orders = [
    mkOrder(77, 'UnderReview', 1, 1),
    mkOrder(76, 'New', 0.2, 7, { purchaseType: 'DailyInstallments' }),
    mkOrder(75, 'FieldInspection', 5, 10, { purchaseType: 'DailyInstallments', forSelf: false, customer: { firstName: 'حيدر', fatherName: 'جاسم', grandfatherName: 'علوان', surname: null, phone: '07812345678', secondaryPhone: null } }),
    mkOrder(70, 'Sold', 30, 14, { purchaseType: 'DailyInstallments' }),
    mkOrder(66, 'Denied', 40, 23),
    mkOrder(61, 'NotSold', 55, 3, { forSelf: false, customer: { firstName: 'سجاد', fatherName: 'كريم', grandfatherName: 'حمزة', surname: 'الحسناوي', phone: '07901112233', secondaryPhone: null } }),
    mkOrder(58, 'Cancelled', 70, 17),
  ]
  const entries = []
  let eid = 500
  const addE = (kind, level, amount, status, daysBack, order, product, note = null) =>
    entries.push({
      id: ++eid, userId: 42, kind, level, amount, status, orderNumber: order, productSummary: product,
      sourceLabel: kind === 'Direct' ? 'من مبيعاتك المباشرة' : kind === 'Network' ? `من شبكتك (المستوى ${level})` : kind === 'Clawback' ? 'استرجاع عمولة بيع ملغى' : 'تسوية من الإدارة',
      baseCommission: kind === 'Network' ? amount * 2 ** level : Math.abs(amount), createdAt: daysAgo(daysBack), availableAt: daysAgo(daysBack - 7), note,
    })
  addE('Direct', 0, 12000, 'Available', 30, 'QO-2611-000070', 'طقم قنفات 7 مقاعد ×1')
  addE('Direct', 0, 8000, 'Pending', 2, 'QO-2611-000081', 'ثلاجة 18 قدم نوفروست ×1')
  addE('Network', 1, 3000, 'Available', 20, 'QO-2611-000068', 'مكيف سبلت 1.5 طن انفيرتر ×1')
  addE('Network', 2, 1500, 'Pending', 3, 'QO-2611-000083', 'غسالة أوتوماتيك 9 كغم ×1')
  addE('Network', 1, 4000, 'Paid', 60, 'QO-2611-000040', 'شاشة تلفاز 55 بوصة 4K ×1')
  addE('Direct', 0, 6000, 'Paid', 80, 'QO-2611-000031', 'هاتف ذكي 256 كيكا ×1')
  addE('Direct', 0, 5000, 'Reversed', 50, 'QO-2611-000045', 'مكنسة كهربائية ×1', 'تم إلغاء البيع')
  addE('Network', 3, 500, 'Pending', 1, 'QO-2611-000085', 'مروحة سقفية ×1')
  addE('Adjustment', 0, 2000, 'Available', 10, null, null, 'مكافأة أفضل مسوّق لهذا الشهر')
  addE('Network', 2, 2500, 'Pending', 4, 'QO-2611-000079', 'ميز طعام 6 كراسي ×1')

  const notifications = [
    { id: 9, userId: 42, type: 'Order', title: 'تحديث على طلبك', body: 'طلبك QO-2611-000077 أصبح قيد المعالجة.', data: { orderId: 77 }, readAt: null, createdAt: daysAgo(0.9) },
    { id: 8, userId: 42, type: 'Commission', title: 'عمولة جديدة', body: 'حصلت على عمولة 8,000 د.ع من عملية بيع مباشرة.', data: {}, readAt: null, createdAt: daysAgo(2) },
    { id: 7, userId: 42, type: 'Network', title: 'انضم عضو جديد لشبكتك', body: 'انضم «كرار» إلى شبكتك عبر رابط الدعوة.', data: {}, readAt: null, createdAt: daysAgo(6) },
    { id: 6, userId: 42, type: 'Promotion', title: 'عرض الشتاء بدأ!', body: 'خصومات على أجهزة التدفئة والمطبخ.', data: { productId: 3 }, readAt: daysAgo(9), createdAt: daysAgo(10) },
    { id: 5, userId: 42, type: 'Order', title: 'تم رفض الطلب', body: 'تم رفض الطلب QO-2611-000066: تم رفض الطلب لعدم توفر المنتج في منطقتك حالياً', data: { orderId: 66 }, readAt: daysAgo(39), createdAt: daysAgo(40) },
    { id: 4, userId: 42, type: 'System', title: 'مرحباً بك في قلعة الضمان', body: 'شكراً لتسجيلك. تصفّح المنتجات واطلب بسهولة.', data: {}, readAt: daysAgo(199), createdAt: daysAgo(200) },
  ]
  return {
    seq: { user: 200, order: 90, notification: 20, payout: 5, entry: eid },
    users: [demo, ...direct],
    sessions: {},
    refreshTokens: {},
    otps: {},
    idempotency: {},
    orders,
    entries,
    payouts: [
      { id: 3, userId: 42, amount: 10000, status: 'Paid', method: 'zaincash', createdAt: daysAgo(58), processedAt: daysAgo(55), note: 'تم التحويل إلى محفظة زين كاش' },
      { id: 2, userId: 42, amount: 12000, status: 'Rejected', method: 'cash', createdAt: daysAgo(90), processedAt: daysAgo(88), note: 'الرصيد غير كافٍ وقت الطلب' },
    ],
    notifications,
  }
}

let db = null
function load() {
  if (db) return db
  try {
    const raw = localStorage.getItem(DB_KEY)
    db = raw ? JSON.parse(raw) : seed()
  } catch {
    db = seed()
  }
  return db
}
function save() {
  try { localStorage.setItem(DB_KEY, JSON.stringify(db)) } catch { /* ignore */ }
}
/** Reset fake data (exposed on window for demos). */
export function resetFakeDb() {
  db = seed()
  save()
}
if (typeof window !== 'undefined') window.__resetFakeDb = resetFakeDb

// ------------------------------------------------------------------ helpers
class HttpError {
  constructor(status, error, code, extra = {}) {
    Object.assign(this, { status, body: { error, code, ...extra } })
  }
}
const fail = (status, error, code, extra) => { throw new HttpError(status, error, code, extra) }
const unauthorized = () => fail(401, 'يرجى تسجيل الدخول', 'unauthorized')
const notFound = () => fail(404, 'غير موجود', 'not_found')

function paged(list, params) {
  const page = Math.max(1, parseInt(params.page || 1, 10))
  const pageSize = Math.min(100, Math.max(1, parseInt(params.pageSize || 20, 10)))
  return { items: list.slice((page - 1) * pageSize, page * pageSize), total: list.length, page, pageSize }
}

function card(p) {
  const { id, name, categoryId, categoryName, thumbnailUrl, cashPrice, installmentPrice, dailyInstallment, installmentDays, allowCash, allowInstallments, isFeatured, badge } = p
  return { id, name, categoryId, categoryName, thumbnailUrl, cashPrice, installmentPrice, dailyInstallment, installmentDays, allowCash, allowInstallments, isFeatured, badge }
}
function detail(p) {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://store.qalaa.iq'
  return { ...card(p), description: p.description, media: p.media, specs: p.specs, extraInfo: p.extraInfo, availableBranches: p.availableBranches, shareUrl: `${origin}/p/${p.id}` }
}

function fullName(u) {
  return [u.firstName, u.fatherName, u.grandfatherName, u.surname].filter(Boolean).join(' ')
}
function me(u) {
  // Invite links always show the production store domain, as the real API does.
  const origin = 'https://store.qalaa.iq'
  const referrer = u.referredById ? db.users.find((x) => x.id === u.referredById) : null
  return {
    id: u.id, phone: u.phone, firstName: u.firstName, fatherName: u.fatherName, grandfatherName: u.grandfatherName, surname: u.surname,
    fullName: fullName(u), cityValue: u.cityValue, cityName: cityName(u.cityValue),
    referralCode: u.referralCode, referralLink: `${origin}/r/${u.referralCode}`,
    referredBy: referrer ? { firstName: referrer.firstName } : u.referredByName ? { firstName: u.referredByName } : null,
    marketingOptIn: u.marketingOptIn, completedSalesCount: u.completedSalesCount,
    canCreateAccounts: u.completedSalesCount >= appConfig.createAccountsUnlockSales, createdAt: u.createdAt,
  }
}

function issueTokens(u) {
  const accessToken = 'fake-at-' + rid()
  const refreshToken = 'fake-rt-' + rid()
  const exp = Date.now() + 15 * 60 * 1000
  db.sessions[accessToken] = { userId: u.id, exp }
  db.refreshTokens[refreshToken] = u.id
  save()
  return { accessToken, accessTokenExpiresAt: new Date(exp).toISOString(), refreshToken, user: me(u) }
}

function authUser(config) {
  const h = config.headers?.Authorization || config.headers?.authorization || ''
  const token = String(h).replace(/^Bearer\s+/i, '')
  const s = db.sessions[token]
  if (!s || s.exp < Date.now()) unauthorized()
  const u = db.users.find((x) => x.id === s.userId && !x.deleted)
  if (!u) unauthorized()
  return u
}

function genCode() {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'
  let s = 'Q'
  for (let i = 0; i < 5; i++) s += chars[Math.floor(Math.random() * chars.length)]
  return s
}

function orderSummary(o) {
  const lines = o.items.map((i) => ({ i, p: products.find((x) => x.id === i.productId) }))
  return {
    id: o.id, orderNumber: o.orderNumber, status: o.status, statusLabel: STATUS_LABEL[o.status], purchaseType: o.purchaseType,
    forSelf: o.forSelf, customerName: [o.customer.firstName, o.customer.fatherName, o.customer.grandfatherName].join(' '),
    cityName: cityName(o.cityValue), itemsSummary: lines.map(({ i, p }) => `${p?.name ?? 'منتج'} ×${i.quantity}`).join('، '),
    estimatedTotal: lines.reduce((s, { i, p }) => s + (o.purchaseType === 'Cash' ? p.cashPrice : p.installmentPrice) * i.quantity, 0),
    createdAt: o.createdAt, updatedAt: o.updatedAt,
  }
}
function orderDetail(o) {
  return {
    ...orderSummary(o),
    customer: o.customer, address: o.address, shop: o.shop, notes: o.notes,
    items: o.items.map((i) => {
      const p = products.find((x) => x.id === i.productId)
      const inst = o.purchaseType === 'DailyInstallments'
      return { productId: p.id, name: p.name, thumbnailUrl: p.thumbnailUrl, quantity: i.quantity, unitPrice: inst ? p.installmentPrice : p.cashPrice, dailyInstallment: inst ? p.dailyInstallment : null, installmentDays: inst ? p.installmentDays : null }
    }),
    publicReason: ['Denied', 'NotSold'].includes(o.status) ? o.publicReason : null,
    timeline: o.timeline.map((t) => ({ ...t, statusLabel: STATUS_LABEL[t.status] })),
    canCancel: ['New', 'UnderReview'].includes(o.status),
  }
}

// ---- installment accounts (only for the demo phone) ----
function earningsSummary(u) {
  const list = db.entries.filter((e) => e.userId === u.id)
  const sum = (f) => list.filter(f).reduce((s, e) => s + e.amount, 0)
  const direct = sum((e) => e.kind === 'Direct' && e.status !== 'Reversed')
  const network = sum((e) => e.kind === 'Network' && e.status !== 'Reversed')
  const clawback = sum((e) => e.kind === 'Clawback')
  const adjustments = sum((e) => e.kind === 'Adjustment' && e.status !== 'Reversed')
  const available = sum((e) => e.status === 'Available')
  const open = db.payouts.find((p) => p.userId === u.id && ['Requested', 'Approved'].includes(p.status))
  return {
    directTotal: direct, networkTotal: network, grandTotal: direct + network + adjustments + clawback,
    pending: sum((e) => e.status === 'Pending'), available, paid: sum((e) => e.status === 'Paid'), frozen: sum((e) => e.status === 'Frozen'),
    balance: available - (open?.amount ?? 0),
    pendingPayoutRequest: open ? { id: open.id, amount: open.amount, status: open.status, statusLabel: PAYOUT_LABEL[open.status], method: open.method, createdAt: open.createdAt, processedAt: null, note: null } : null,
    minPayoutAmount: 10000,
  }
}

function networkSize(u, depth = 0) {
  if (depth > 20) return 0
  const kids = db.users.filter((x) => x.referredById === u.id && !x.deleted)
  return kids.reduce((s, k) => s + 1 + (k.networkSize ?? networkSize(k, depth + 1)), 0)
}

// ------------------------------------------------------------------ routes
const routes = []
const on = (method, pattern, handler) => routes.push({ method, re: new RegExp('^' + pattern + '$'), handler })

// §1
on('get', 'branches', () => branches)
on('get', 'catalog/categories', () => categories)
on('get', 'catalog/banners', () => banners)
on('get', 'app/config', () => appConfig)
on('get', 'catalog/products', ({ params }) => {
  let list = products.slice()
  const q = String(params.q || '').trim()
  if (q) list = list.filter((p) => p.name.includes(q) || p.categoryName.includes(q) || p.description.includes(q))
  if (params.categoryId) list = list.filter((p) => p.categoryId === Number(params.categoryId))
  if (params.cityValue) list = list.filter((p) => p.availableBranches.some((b) => b.cityValue === Number(params.cityValue)))
  if (params.purchaseType === 'Cash') list = list.filter((p) => p.allowCash)
  if (params.purchaseType === 'DailyInstallments') list = list.filter((p) => p.allowInstallments)
  const sort = params.sort || 'newest'
  if (sort === 'priceAsc') list.sort((a, b) => a.cashPrice - b.cashPrice)
  else if (sort === 'priceDesc') list.sort((a, b) => b.cashPrice - a.cashPrice)
  else if (sort === 'popular') list.sort((a, b) => b.popularity - a.popularity)
  else list.sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  return paged(list.map(card), params)
})
on('get', 'catalog/products/(\\d+)', ({ m }) => {
  const p = products.find((x) => x.id === Number(m[1]))
  if (!p) fail(404, 'المنتج غير موجود', 'not_found')
  return detail(p)
})

// §2
on('post', 'auth/otp/request', ({ body }) => {
  const phone = normalizeIraqiPhone(body.phone)
  if (!IRAQI_PHONE_RE.test(phone)) fail(400, 'رقم الهاتف غير صحيح، أدخل رقم موبايل عراقي', 'invalid_phone')
  const recent = Object.values(db.otps).filter((o) => o.phone === phone && Date.now() - o.createdAt < 10 * 60 * 1000)
  if (recent.length >= 5) fail(429, 'طلبت رموزاً كثيرة، حاول بعد قليل', 'otp_rate_limited', { retryAfterSeconds: 120 })
  const requestId = rid()
  const isRegistered = db.users.some((u) => u.phone === phone && !u.deleted)
  db.otps[requestId] = { phone, code: FAKE_OTP, exp: Date.now() + 300000, attempts: 0, createdAt: Date.now(), isRegistered }
  save()
  console.info(`[fake api] OTP for ${phone}: ${FAKE_OTP}`)
  return { requestId, expiresInSeconds: 300, resendAfterSeconds: 60, isRegistered }
})
on('post', 'auth/otp/verify', ({ body }) => {
  const otp = db.otps[body.requestId]
  const phone = normalizeIraqiPhone(body.phone)
  if (!otp || otp.phone !== phone) fail(400, 'انتهت صلاحية الرمز، اطلب رمزاً جديداً', 'otp_expired')
  if (otp.exp < Date.now()) fail(400, 'انتهت صلاحية الرمز، اطلب رمزاً جديداً', 'otp_expired')
  if (otp.attempts >= 5) fail(400, 'محاولات كثيرة خاطئة، اطلب رمزاً جديداً', 'otp_too_many_attempts')
  if (String(body.code) !== otp.code) {
    otp.attempts++
    save()
    fail(400, 'الرمز غير صحيح', 'otp_invalid', { attemptsLeft: 5 - otp.attempts })
  }
  let u = db.users.find((x) => x.phone === phone && !x.deleted)
  if (!u) {
    const r = body.registration
    if (!r) fail(400, 'يرجى إكمال بيانات التسجيل', 'validation', { fields: { firstName: 'مطلوب' } })
    const fields = {}
    for (const k of ['firstName', 'fatherName', 'grandfatherName']) if (!isArabicSingleWord(r[k])) fields[k] = 'مطلوب (كلمة واحدة بالعربية)'
    if (!r.cityValue) fields.cityValue = 'مطلوب'
    if (r.acceptTerms !== true) fields.acceptTerms = 'يجب الموافقة على الشروط'
    if (Object.keys(fields).length) fail(400, 'يرجى تصحيح الحقول المؤشرة', 'validation', { fields })
    let referrer = null
    if (r.referralCode) {
      referrer = db.users.find((x) => x.referralCode.toUpperCase() === String(r.referralCode).trim().toUpperCase() && !x.deleted)
      if (!referrer) fail(400, 'رمز الإحالة غير صحيح', 'invalid_referral_code')
    }
    u = {
      id: ++db.seq.user, phone, firstName: r.firstName.trim(), fatherName: r.fatherName.trim(), grandfatherName: r.grandfatherName.trim(),
      surname: r.surname?.trim() || null, cityValue: r.cityValue, referralCode: genCode(), referredById: referrer?.id ?? null,
      marketingOptIn: !!r.marketingOptIn, completedSalesCount: 0, createdAt: now(), deleted: false,
    }
    db.users.push(u)
    db.notifications.push({ id: ++db.seq.notification, userId: u.id, type: 'System', title: 'مرحباً بك في قلعة الضمان', body: 'تم إنشاء حسابك بنجاح. تصفّح المنتجات واطلب بسهولة.', data: {}, readAt: null, createdAt: now() })
  }
  delete db.otps[body.requestId]
  return issueTokens(u)
})
on('post', 'auth/refresh', ({ body }) => {
  const userId = db.refreshTokens[body.refreshToken]
  const u = db.users.find((x) => x.id === userId && !x.deleted)
  if (!u) fail(401, 'انتهت الجلسة، يرجى تسجيل الدخول', 'invalid_refresh_token')
  delete db.refreshTokens[body.refreshToken]
  return issueTokens(u)
})
on('post', 'auth/logout', ({ body, config }) => {
  delete db.refreshTokens[body.refreshToken]
  const token = String(config.headers?.Authorization || '').replace(/^Bearer\s+/i, '')
  delete db.sessions[token]
  return [204, null]
})

// §3
on('get', 'me', ({ u }) => me(u()))
on('put', 'me', ({ u, body }) => {
  const user = u()
  const fields = {}
  for (const k of ['firstName', 'fatherName', 'grandfatherName']) if (!isArabicSingleWord(body[k])) fields[k] = 'مطلوب (كلمة واحدة بالعربية)'
  if (!body.cityValue) fields.cityValue = 'مطلوب'
  if (Object.keys(fields).length) fail(400, 'يرجى تصحيح الحقول المؤشرة', 'validation', { fields })
  Object.assign(user, { firstName: body.firstName.trim(), fatherName: body.fatherName.trim(), grandfatherName: body.grandfatherName.trim(), surname: body.surname?.trim() || null, cityValue: body.cityValue })
  return me(user)
})
on('put', 'me/preferences', ({ u, body }) => {
  const user = u()
  user.marketingOptIn = !!body.marketingOptIn
  return me(user)
})
on('post', 'me/devices', ({ u }) => (u(), [204, null]))
on('delete', 'me/devices/(.+)', ({ u }) => (u(), [204, null]))
on('delete', 'me', ({ u, body }) => {
  const user = u()
  if (body?.confirm !== 'حذف') fail(400, 'اكتب كلمة «حذف» للتأكيد', 'validation', { fields: { confirm: 'مطلوب' } })
  user.deleted = true
  user.phone = 'deleted-' + user.id
  return [204, null]
})
on('get', 'me/product-commission/(\\d+)', ({ u, m }) => {
  u()
  const p = products.find((x) => x.id === Number(m[1]))
  if (!p) notFound()
  return { productId: p.id, baseCommission: Math.round((p.cashPrice * 0.02) / 250) * 250 }
})

// §5
on('post', 'orders', ({ u, body, config }) => {
  const user = u()
  const key = config.headers?.['Idempotency-Key']
  if (key && db.idempotency[key]) return [201, orderDetail(db.orders.find((o) => o.id === db.idempotency[key]))]
  const fields = {}
  if (!['Cash', 'DailyInstallments'].includes(body.purchaseType)) fields.purchaseType = 'مطلوب'
  if (!branches.some((b) => b.cityValue === body.cityValue)) fields.cityValue = 'مطلوب'
  const c = body.customer || {}
  for (const k of ['firstName', 'fatherName', 'grandfatherName']) if (!isArabicSingleWord(c[k])) fields[k] = 'مطلوب (كلمة واحدة بالعربية)'
  const phone = normalizeIraqiPhone(c.phone || (body.forSelf ? user.phone : ''))
  if (!IRAQI_PHONE_RE.test(phone)) fields.phone = 'رقم غير صحيح'
  if (!body.address?.area) fields.area = 'مطلوب'
  if (!body.address?.details) fields.details = 'مطلوب'
  if (body.purchaseType === 'DailyInstallments') {
    if (!body.shop?.name) fields.shopName = 'مطلوب'
    if (!body.shop?.businessType) fields.shopBusinessType = 'مطلوب'
  }
  const items = body.items || []
  if (items.length < 1 || items.length > 10) fields.items = 'من 1 إلى 10 منتجات'
  if (Object.keys(fields).length) fail(400, 'يرجى تصحيح الحقول المؤشرة', 'validation', { fields })
  for (const it of items) {
    const p = products.find((x) => x.id === it.productId)
    if (!p) fail(400, 'أحد المنتجات غير متوفر', 'product_unavailable')
    if (it.quantity < 1 || it.quantity > 5) fail(400, 'الكمية يجب أن تكون من 1 إلى 5', 'validation', { fields: { items: 'الكمية' } })
    if (!p.availableBranches.some((b) => b.cityValue === body.cityValue)) fail(400, `المنتج «${p.name}» غير متوفر في الفرع المختار`, 'product_not_in_branch')
    if (body.purchaseType === 'Cash' && !p.allowCash) fail(400, `المنتج «${p.name}» غير متاح بالدفع النقدي`, 'purchase_type_not_allowed')
    if (body.purchaseType === 'DailyInstallments' && !p.allowInstallments) fail(400, `المنتج «${p.name}» غير متاح بالأقساط`, 'purchase_type_not_allowed')
    const dup = db.orders.find((o) => o.customer.phone === phone && OPEN.includes(o.status) && o.items.some((i) => i.productId === p.id))
    if (dup) fail(409, `يوجد طلب مفتوح لنفس المنتج ولنفس رقم الهاتف (${dup.orderNumber})`, 'duplicate_open_order', { orderNumber: dup.orderNumber })
  }
  const id = ++db.seq.order
  const order = {
    id, orderNumber: `QO-2611-${String(id).padStart(6, '0')}`, userId: user.id, status: 'New', purchaseType: body.purchaseType,
    forSelf: !!body.forSelf, cityValue: body.cityValue,
    customer: { firstName: c.firstName, fatherName: c.fatherName, grandfatherName: c.grandfatherName, surname: c.surname || null, phone, secondaryPhone: c.secondaryPhone || null },
    address: body.address, shop: body.purchaseType === 'DailyInstallments' ? body.shop : null, notes: body.notes || null,
    items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })), publicReason: null,
    timeline: [{ status: 'New', at: now(), note: null }], createdAt: now(), updatedAt: now(),
  }
  db.orders.unshift(order)
  if (key) db.idempotency[key] = id
  db.notifications.push({ id: ++db.seq.notification, userId: user.id, type: 'Order', title: 'تم استلام طلبك', body: `طلبك ${order.orderNumber} قيد المراجعة وسنتواصل معك قريباً.`, data: { orderId: id }, readAt: null, createdAt: now() })
  return [201, orderDetail(order)]
})
on('get', 'orders', ({ u, params }) => {
  const user = u()
  let list = db.orders.filter((o) => o.userId === user.id)
  if (params.status) list = list.filter((o) => String(params.status).split(',').includes(o.status))
  list.sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  return paged(list.map(orderSummary), params)
})
on('get', 'orders/(\\d+)', ({ u, m }) => {
  const user = u()
  const o = db.orders.find((x) => x.id === Number(m[1]) && x.userId === user.id)
  if (!o) fail(404, 'الطلب غير موجود', 'not_found')
  return orderDetail(o)
})
on('post', 'orders/(\\d+)/cancel', ({ u, m, body }) => {
  const user = u()
  const o = db.orders.find((x) => x.id === Number(m[1]) && x.userId === user.id)
  if (!o) fail(404, 'الطلب غير موجود', 'not_found')
  if (!['New', 'UnderReview'].includes(o.status)) fail(400, 'لا يمكن إلغاء الطلب في حالته الحالية', 'cannot_cancel')
  o.status = 'Cancelled'
  o.updatedAt = now()
  o.timeline.push({ status: 'Cancelled', at: now(), note: body?.reason || null })
  return orderDetail(o)
})

// §6
on('get', 'earnings/summary', ({ u }) => earningsSummary(u()))
on('get', 'earnings/entries', ({ u, params }) => {
  const user = u()
  let list = db.entries.filter((e) => e.userId === user.id)
  if (params.kind) list = list.filter((e) => e.kind === params.kind)
  if (params.status) list = list.filter((e) => e.status === params.status)
  list.sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  return paged(list.map(({ userId, ...e }) => ({ ...e, statusLabel: COMMISSION_LABEL[e.status] })), params)
})
on('post', 'earnings/payout-requests', ({ u, body }) => {
  const user = u()
  const s = earningsSummary(user)
  if (s.pendingPayoutRequest) fail(409, 'لديك طلب صرف مفتوح بالفعل', 'payout_request_open')
  const amount = parseInt(body.amount, 10)
  if (!amount || amount < s.minPayoutAmount) fail(400, `أقل مبلغ للصرف هو ${s.minPayoutAmount.toLocaleString('en-US')} د.ع`, 'validation', { fields: { amount: 'أقل من الحد الأدنى' } })
  if (amount > s.balance) fail(400, 'المبلغ أكبر من الرصيد المتاح', 'insufficient_balance')
  if (!['cash', 'zaincash', 'qicard'].includes(body.method)) fail(400, 'اختر طريقة الصرف', 'validation', { fields: { method: 'مطلوب' } })
  const p = { id: ++db.seq.payout, userId: user.id, amount, status: 'Requested', method: body.method, accountDetails: body.accountDetails || null, createdAt: now(), processedAt: null, note: null }
  db.payouts.unshift(p)
  const { userId, accountDetails, ...out } = p
  return [201, { ...out, statusLabel: PAYOUT_LABEL[p.status] }]
})
on('get', 'earnings/payout-requests', ({ u }) => {
  const user = u()
  return db.payouts.filter((p) => p.userId === user.id).sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .map(({ userId, accountDetails, ...p }) => ({ ...p, statusLabel: PAYOUT_LABEL[p.status] }))
})

// §7
on('get', 'network/summary', ({ u }) => {
  const user = u()
  const m = me(user)
  const s = earningsSummary(user)
  return {
    referralCode: m.referralCode, referralLink: m.referralLink,
    directCount: db.users.filter((x) => x.referredById === user.id && !x.deleted).length,
    networkSize: networkSize(user), networkEarnings: s.networkTotal,
    completedSalesCount: user.completedSalesCount,
    salesToUnlockAccountCreation: Math.max(0, appConfig.createAccountsUnlockSales - user.completedSalesCount),
    canCreateAccounts: m.canCreateAccounts,
  }
})
on('get', 'network/direct', ({ u, params }) => {
  const user = u()
  const list = db.users.filter((x) => x.referredById === user.id && !x.deleted).sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  return paged(list.map((x) => ({ firstName: x.firstName, joinedAt: x.createdAt, networkSize: x.networkSize ?? networkSize(x) })), params)
})
on('post', 'network/accounts/otp', ({ u, body }) => {
  const user = u()
  if (user.completedSalesCount < appConfig.createAccountsUnlockSales) fail(403, 'هذه الميزة غير متاحة لك بعد', 'create_accounts_locked')
  const phone = normalizeIraqiPhone(body.phone)
  if (!IRAQI_PHONE_RE.test(phone)) fail(400, 'رقم الهاتف غير صحيح', 'invalid_phone')
  const fields = {}
  for (const k of ['firstName', 'fatherName', 'grandfatherName']) if (!isArabicSingleWord(body[k])) fields[k] = 'مطلوب (كلمة واحدة بالعربية)'
  if (!body.cityValue) fields.cityValue = 'مطلوب'
  if (Object.keys(fields).length) fail(400, 'يرجى تصحيح الحقول المؤشرة', 'validation', { fields })
  if (db.users.some((x) => x.phone === phone && !x.deleted)) fail(409, 'هذا الرقم مسجّل مسبقاً', 'phone_already_registered')
  const requestId = rid()
  db.otps[requestId] = { phone, code: FAKE_OTP, exp: Date.now() + 300000, attempts: 0, createdAt: Date.now(), createFor: { ...body, phone }, by: user.id }
  console.info(`[fake api] consent OTP sent to ${phone}: ${FAKE_OTP}`)
  return { requestId, expiresInSeconds: 300, resendAfterSeconds: 60 }
})
on('post', 'network/accounts/verify', ({ u, body }) => {
  const user = u()
  const otp = db.otps[body.requestId]
  if (!otp || otp.by !== user.id || otp.phone !== normalizeIraqiPhone(body.phone) || otp.exp < Date.now()) fail(400, 'انتهت صلاحية الرمز', 'otp_expired')
  if (String(body.code) !== otp.code) {
    otp.attempts++
    if (otp.attempts >= 5) fail(400, 'محاولات كثيرة خاطئة', 'otp_too_many_attempts')
    fail(400, 'الرمز غير صحيح', 'otp_invalid', { attemptsLeft: 5 - otp.attempts })
  }
  const f = otp.createFor
  db.users.push({
    id: ++db.seq.user, phone: f.phone, firstName: f.firstName, fatherName: f.fatherName, grandfatherName: f.grandfatherName, surname: f.surname || null,
    cityValue: f.cityValue, referralCode: genCode(), referredById: user.id, marketingOptIn: false, completedSalesCount: 0, createdAt: now(), deleted: false,
  })
  delete db.otps[body.requestId]
  return { created: true, firstName: f.firstName }
})

// §8
on('get', 'notifications', ({ u, params }) => {
  const user = u()
  const list = db.notifications.filter((n) => n.userId === user.id).sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  const { items, ...rest } = paged(list, params)
  return { items: items.map(({ userId, ...n }) => n), ...rest, unreadCount: list.filter((n) => !n.readAt).length }
})
on('post', 'notifications/read-all', ({ u }) => {
  const user = u()
  db.notifications.filter((n) => n.userId === user.id && !n.readAt).forEach((n) => (n.readAt = now()))
  return [204, null]
})
on('post', 'notifications/(\\d+)/read', ({ u, m }) => {
  const user = u()
  const n = db.notifications.find((x) => x.id === Number(m[1]) && x.userId === user.id)
  if (!n) notFound()
  n.readAt ||= now()
  return [204, null]
})

// ------------------------------------------------------------------ adapter
export async function fakeAdapter(config) {
  load()
  await new Promise((r) => setTimeout(r, LATENCY[0] + Math.random() * (LATENCY[1] - LATENCY[0])))
  let url = config.url || ''
  const base = config.baseURL || ''
  if (base && url.startsWith(base)) url = url.slice(base.length)
  url = url.replace(/^\/+/, '').replace(/^api\/v1\//, '').split('?')[0]
  const method = (config.method || 'get').toLowerCase()
  let body = {}
  try { body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data || {} } catch { body = {} }
  const params = config.params || {}

  const respond = (status, data) => {
    const response = { data, status, statusText: String(status), headers: {}, config, request: {} }
    if (status >= 200 && status < 300) return response
    throw new AxiosError(data?.error || 'Request failed', status >= 500 ? AxiosError.ERR_BAD_RESPONSE : AxiosError.ERR_BAD_REQUEST, config, {}, response)
  }

  const route = routes.find((r) => r.method === method && r.re.test(url))
  if (!route) return respond(404, { error: 'المسار غير موجود', code: 'not_found' })
  try {
    const result = route.handler({ m: url.match(route.re), params, body, config, u: () => authUser(config) })
    save()
    if (Array.isArray(result) && typeof result[0] === 'number' && result.length === 2) return respond(result[0], result[1])
    return respond(200, JSON.parse(JSON.stringify(result)))
  } catch (e) {
    if (e instanceof HttpError) return respond(e.status, e.body)
    if (e instanceof AxiosError) throw e
    console.error('[fake api]', e)
    return respond(500, { error: 'خطأ داخلي في الخادم التجريبي', code: 'server_error' })
  }
}
