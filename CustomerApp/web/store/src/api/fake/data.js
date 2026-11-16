// Realistic Arabic sample data for VITE_USE_FAKE_API=true. Images are demo stock photos in public/demo/ (Unsplash License).

export const branches = [
  { cityValue: 1, name: 'النجف' },
  { cityValue: 2, name: 'بغداد - الرصافة' },
  { cityValue: 3, name: 'بغداد - الكرخ' },
  { cityValue: 4, name: 'كربلاء' },
  { cityValue: 5, name: 'بابل' },
  { cityValue: 6, name: 'الديوانية' },
  { cityValue: 8, name: 'البصرة' },
  { cityValue: 9, name: 'ذي قار' },
  { cityValue: 10, name: 'المثنى' },
  { cityValue: 11, name: 'ديالى' },
  { cityValue: 12, name: 'الأنبار' },
  { cityValue: 13, name: 'صلاح الدين' },
  { cityValue: 14, name: 'كركوك' },
]

const catDefs = [
  { id: 1, name: 'أجهزة كهربائية', emoji: '🔌', from: '#006A64', to: '#2E9C8F' },
  { id: 2, name: 'تبريد وتكييف', emoji: '❄️', from: '#1565C0', to: '#4FC3F7' },
  { id: 3, name: 'هواتف وإلكترونيات', emoji: '📱', from: '#4527A0', to: '#7E57C2' },
  { id: 4, name: 'أثاث منزلي', emoji: '🛋️', from: '#6D4C41', to: '#A1887F' },
  { id: 5, name: 'أدوات المطبخ', emoji: '🍳', from: '#E65100', to: '#FFB74D' },
  { id: 6, name: 'مستلزمات المحلات', emoji: '🏪', from: '#2E7D32', to: '#81C784' },
]

const raw = [
  // [name, cat, emoji, cash, days, featured, badge, specs]
  ['ثلاجة 18 قدم نوفروست', 1, '🧊', 650000, 120, true, 'جديد', [['السعة', '18 قدم'], ['النوع', 'نوفروست'], ['اللون', 'فضي'], ['الضمان', 'سنتان']]],
  ['غسالة أوتوماتيك 9 كغم', 1, '🧺', 520000, 120, true, null, [['السعة', '9 كغم'], ['سرعة الدوران', '1400 دورة/دقيقة'], ['عدد البرامج', '15'], ['الضمان', 'سنتان']]],
  ['طباخ غازي 5 عيون', 1, '🔥', 380000, 90, false, null, [['عدد العيون', '5'], ['الفرن', 'غاز مع شواية'], ['الأبعاد', '90×60 سم']]],
  ['مكنسة كهربائية 2000 واط', 1, '🧹', 145000, 60, false, 'الأكثر طلباً', [['القدرة', '2000 واط'], ['سعة الخزان', '3 لتر'], ['طول السلك', '5 م']]],
  ['مجمدة عمودية 7 أدراج', 1, '🧊', 590000, 120, false, null, [['عدد الأدراج', '7'], ['السعة', '260 لتر']]],
  ['شاشة تلفاز 55 بوصة 4K', 3, '📺', 720000, 150, true, 'عرض خاص', [['الحجم', '55 بوصة'], ['الدقة', '4K UHD'], ['نظام التشغيل', 'أندرويد تي في'], ['منافذ HDMI', '3']]],
  ['هاتف ذكي 256 كيكا', 3, '📱', 480000, 120, true, 'جديد', [['الذاكرة', '256 كيكابايت'], ['الرام', '8 كيكابايت'], ['البطارية', '5000 ملي أمبير'], ['الكاميرا', '108 ميكابكسل']]],
  ['لابتوب للأعمال 15.6 بوصة', 3, '💻', 850000, 150, false, null, [['المعالج', 'Core i5 الجيل 12'], ['الرام', '16 كيكابايت'], ['التخزين', 'SSD 512']]],
  ['سماعات لاسلكية', 3, '🎧', 65000, 30, false, null, [['الاتصال', 'بلوتوث 5.3'], ['عمر البطارية', '30 ساعة']]],
  ['مكيف سبلت 1.5 طن انفيرتر', 2, '❄️', 690000, 150, true, 'وفّر الكهرباء', [['القدرة', '1.5 طن'], ['التقنية', 'انفيرتر'], ['الغاز', 'R410'], ['الضمان', '5 سنوات للكمبريسر']]],
  ['مكيف سبلت 2 طن', 2, '🌬️', 820000, 150, false, null, [['القدرة', '2 طن'], ['التقنية', 'عادي'], ['الضمان', '3 سنوات']]],
  ['مبردة هواء 6000 م³', 2, '💨', 260000, 90, false, null, [['التدفق', '6000 م³/ساعة'], ['الخزان', '60 لتر']]],
  ['مروحة سقفية', 2, '🌀', 75000, 30, false, null, [['عدد الريش', '5'], ['السرعات', '5']]],
  ['طقم قنفات 7 مقاعد', 4, '🛋️', 1250000, 200, true, 'الأكثر مبيعاً', [['عدد المقاعد', '7'], ['القماش', 'شنيل تركي'], ['الخشب', 'زان']]],
  ['غرفة نوم كاملة', 4, '🛏️', 1900000, 240, false, null, [['المحتويات', 'سرير، كنتور، تسريحة، كومدينو'], ['الخشب', 'MDF ماليزي']]],
  ['ميز طعام 6 كراسي', 4, '🪑', 560000, 120, false, null, [['عدد الكراسي', '6'], ['السطح', 'زجاج سيكوريت']]],
  ['خلاط ومحضّر طعام', 5, '🥤', 95000, 30, false, null, [['القدرة', '1000 واط'], ['السعة', '1.5 لتر']]],
  ['طقم قدور ستيل 12 قطعة', 5, '🍲', 135000, 45, false, 'جديد', [['عدد القطع', '12'], ['المادة', 'ستانلس ستيل']]],
  ['فرن كهربائي 45 لتر', 5, '🍕', 165000, 60, false, null, [['السعة', '45 لتر'], ['القدرة', '2000 واط']]],
  ['ثلاجة عرض مشروبات للمحلات', 6, '🥫', 890000, 180, true, 'للمحلات', [['السعة', '400 لتر'], ['الباب', 'زجاج مزدوج'], ['الإضاءة', 'LED']]],
  ['ميزان إلكتروني تجاري', 6, '⚖️', 120000, 45, false, null, [['أقصى وزن', '40 كغم'], ['الدقة', '5 غرام']]],
  ['جهاز كاشير مع طابعة', 6, '🧾', 450000, 120, false, null, [['الشاشة', 'لمس 15 بوصة'], ['الطابعة', 'حرارية 80 مم']]],
  ['مولدة كهرباء 5 كيلو', 6, '⚡', 980000, 180, false, null, [['القدرة', '5 كيلو واط'], ['الوقود', 'بنزين']]],
  ['ستاندات عرض معدنية (طقم 4)', 6, '🗄️', 240000, 90, false, null, [['عدد القطع', '4'], ['الارتفاع', '2 م']]],
]

// Photo key per row of `raw` (same order).
const productPhotos = [
  'fridge', 'washer', 'stove', 'vacuum', 'freezer', 'tv55', 'phone', 'laptop', 'headph', 'ac', 'ac', 'cooler', 'cfan',
  'sofa', 'bed', 'dining', 'blender', 'pots', 'oven', 'showcase', 'scale', 'pos', 'gen', 'shelving',
]
const categoryPhotos = { 1: 'washer', 2: 'ac', 3: 'phone', 4: 'sofa', 5: 'pots', 6: 'shelving' }

export const categories = catDefs.map((c, i) => ({
  id: c.id,
  name: c.name,
  iconUrl: `/demo/${categoryPhotos[c.id]}.jpg`,
  sortOrder: i + 1,
  productCount: raw.filter((r) => r[1] === c.id).length,
}))

const allCityValues = branches.map((b) => b.cityValue)

export const products = raw.map((r, idx) => {
  const id = idx + 1
  const [name, catId, , cash, days, featured, badge, specs] = r
  const cat = catDefs.find((c) => c.id === catId)
  const installmentPrice = Math.round((cash * 1.2) / 5000) * 5000
  const dailyInstallment = Math.ceil(installmentPrice / days / 250) * 250
  const cashOnly = id % 9 === 0
  const installmentsOnly = id === 20
  // availability: most products everywhere, some only in a subset
  const cities = id % 4 === 0 ? allCityValues.filter((c) => c <= 6) : id % 5 === 0 ? [1, 2, 3, 4] : allCityValues
  // Demo photos (Unsplash License) in public/demo/, cropped to squares; "-b" is a closer crop for the gallery.
  const photo = productPhotos[idx]
  const media = [
    { type: 'image', url: `/demo/${photo}.jpg` },
    { type: 'image', url: `/demo/${photo}-b.jpg` },
  ]
  if (id % 3 === 1) media.push({ type: 'video', url: 'https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4' })
  if (id % 4 === 1) media.push({ type: 'youtube', url: 'https://youtu.be/aqz-KE-bpKQ' })
  const createdAt = new Date(Date.UTC(2026, 9, 1 + idx)).toISOString()
  return {
    id,
    name,
    categoryId: catId,
    categoryName: cat.name,
    thumbnailUrl: media[0].url,
    cashPrice: cash,
    installmentPrice,
    dailyInstallment,
    installmentDays: days,
    allowCash: !installmentsOnly,
    allowInstallments: !cashOnly,
    isFeatured: featured,
    badge,
    description: `${name} من قلعة الضمان بضمان رسمي وتوصيل مجاني داخل المدينة.\nمتوفر بالدفع النقدي${cashOnly ? '' : ' أو بالأقساط اليومية لأصحاب المحلات التجارية'}.\nيقوم مندوب المبيعات بالتواصل معك بعد تقديم الطلب لإكمال الإجراءات.`,
    media,
    specs: specs.map(([n, v]) => ({ name: n, value: v })),
    extraInfo: 'التوصيل والتركيب مجاناً. الأسعار قد تتغير حسب الفرع والعروض الحالية.',
    availableBranches: branches.filter((b) => cities.includes(b.cityValue)),
    popularity: (id * 37) % 100,
    createdAt,
  }
})

export const banners = [
  { id: 1, title: 'عرض الشتاء', subtitle: 'خصومات على أجهزة التدفئة والمطبخ — قسّطها يومياً', imageUrl: '/demo/banner-winter.jpg', productId: null, categoryId: 1, externalUrl: null },
  { id: 2, title: 'شاشات 4K بأقساط يومية', subtitle: 'ابتداءً من 7,000 د.ع يومياً لأصحاب المحلات', imageUrl: '/demo/banner-tv.jpg', productId: 6, categoryId: null, externalUrl: null },
  { id: 3, title: 'جهّز محلك مع قلعة الضمان', subtitle: 'ثلاجات عرض، كاشير، موازين ومولدات', imageUrl: '/demo/banner-shop.jpg', productId: null, categoryId: 6, externalUrl: null },
  { id: 4, title: 'ادعُ أصدقاءك واربح', subtitle: 'عمولة على كل عملية بيع مكتملة عبر رابطك', imageUrl: '/demo/banner-invite.jpg', productId: null, categoryId: null, externalUrl: null },
]

export const appConfig = {
  minSupportedVersion: '1.0.0',
  latestVersion: '1.0.0',
  supportPhone: '07801234567',
  supportWhatsapp: '9647801234567',
  termsUrl: '/terms',
  privacyUrl: '/privacy',
  installmentsNotice: 'الشراء بالأقساط اليومية متاح لأصحاب المحلات التجارية فقط',
  referralEnabled: true,
  createAccountsUnlockSales: 5,
}

export const DEMO_PHONE = '07701234567'
export const FAKE_OTP = '123456'
