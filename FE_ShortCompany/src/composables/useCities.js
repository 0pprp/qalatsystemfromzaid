import { ref } from 'vue'

const LOCAL_API = 'http://localhost:5180/api/'
const CACHE_KEY = 'cached_employee_city_data'

/** Vite DEV أو أي بناء يفتح من localhost — الحسابات التجريبية على Docker فقط */
export function isLocalLab() {
  if (import.meta.env.DEV)
    return true
  if (typeof window === 'undefined')
    return false
  const host = window.location.hostname

  return host === 'localhost' || host === '127.0.0.1'
}

function cityListUrl() {
  return import.meta.env.DEV
    ? '/api-defaultdata/GetEmployee'
    : 'http://defaultdata.alsaaeidy.com/GetEmployee'
}

function resolveCityLink(itemLink) {
  return isLocalLab() ? LOCAL_API : itemLink
}

// حالة مشتركة (singleton) على مستوى التطبيق
const provinces = ref([])
const isLoading = ref(false)
const error = ref(null)
let fetchPromise = null

/**
 * جلب قائمة المدن من الـ API مع تخزين مؤقت في localStorage
 */
async function fetchCities() {
  // تجنب تكرار الطلب
  if (fetchPromise) return fetchPromise

  fetchPromise = (async () => {
    isLoading.value = true
    error.value = null

    try {
      const response = await fetch(cityListUrl(), {
        headers: { 'Accept': 'application/json' },
      })

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
      }

      const data = await response.json()

      // تخزين الـ JSON الخام في localStorage
      const rawJson = JSON.stringify(data)
      localStorage.setItem(CACHE_KEY, rawJson)

      provinces.value = data.map(item => ({
        value: item.value,
        name: item.name,
        database: item.database,
        link: resolveCityLink(item.link),
      }))

      isLoading.value = false
      return provinces.value
    } catch (err) {
      console.warn('تعذر جلب المدن من API، جاري تحميل البيانات المخزنة...', err)

      // محاولة تحميل البيانات المخزنة
      const cached = loadFromCache()
      if (cached.length > 0) {
        provinces.value = cached
        error.value = null
      } else {
        error.value = 'تعذر تحميل الفروع. تأكد من اتصالك بالإنترنت.'
      }

      isLoading.value = false
      return provinces.value
    } finally {
      fetchPromise = null
    }
  })()

  return fetchPromise
}

/**
 * تحميل البيانات المخزنة مسبقاً من localStorage
 */
function loadFromCache() {
  try {
    const cachedJson = localStorage.getItem(CACHE_KEY)
    if (cachedJson) {
      const data = JSON.parse(cachedJson)
      if (Array.isArray(data) && data.length > 0) {
        return data.map(item => ({
          value: item.value,
          name: item.name,
          database: item.database,
          link: resolveCityLink(item.link),
        }))
      }
    }
  } catch (e) {
    console.warn('فشل تحميل بيانات المدن المخزنة:', e)
  }
  return []
}

/**
 * الحصول على رابط API لمحافظة معينة
 */
function getCityLink(cityValue) {
  const city = provinces.value.find(p => p.value === cityValue)
  return city?.link || null
}

/**
 * الحصول على اسم المحافظة
 */
function getCityName(cityValue) {
  const city = provinces.value.find(p => p.value === cityValue)
  return city?.name || ''
}

/**
 * الحصول على اسم قاعدة البيانات
 */
function getCityDatabase(cityValue) {
  const city = provinces.value.find(p => p.value === cityValue)
  return city?.database || ''
}

/**
 * Composable للاستخدام داخل Vue components
 * يضمن جلب البيانات مرة واحدة فقط عبر التطبيق
 */
export function useCities() {
  // تحميل من الكاش فوراً للعرض السريع
  if (provinces.value.length === 0) {
    const cached = loadFromCache()
    if (cached.length > 0) {
      provinces.value = cached
    }
  }

  // بدء الجلب من API عند أول استخدام
  if (provinces.value.length === 0 || !fetchPromise) {
    fetchCities()
  }

  return {
    provinces,
    isLoading,
    error,
    fetchCities,
    getCityLink,
    getCityName,
    getCityDatabase,
  }
}

export { fetchCities, getCityLink, getCityName, getCityDatabase, loadFromCache, LOCAL_API }
