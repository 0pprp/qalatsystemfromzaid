import { ref } from 'vue'

const LOCAL_API = 'http://localhost:5180/api/'
const DEMO_API = import.meta.env.DEV
  ? '/demo-api/'
  : 'http://169.58.236.52:8080/api/'
/** Demo BE_Company branch key — matches employee.cityValue and Gateway Database mapping. Not GetAdmin "1". */
export const DEMO_BRANCH_VALUE = 'DatabaseCompanyNajaf_DEMO'
const CACHE_KEY = 'cached_admin_city_data'

function appEnv() {
  return String(import.meta.env.VITE_APP_ENV || '').toLowerCase()
}

/** Docker المحلي فقط عندما يُطلب صراحة */
export function isLocalLab() {
  return appEnv() === 'local'
}

/** فرع Demo النجف — DatabaseCompanyNajaf_DEMO */
export function isDemo() {
  const env = appEnv()
  if (env === 'local')
    return false
  if (env === 'demo')
    return true

  return import.meta.env.DEV
}

function cityListUrl() {
  return import.meta.env.DEV
    ? '/api-defaultdata/GetAdmin'
    : 'http://defaultdata.alsaaeidy.com/GetAdmin'
}

function resolveCityLink(itemLink) {
  if (isDemo())
    return DEMO_API
  if (isLocalLab())
    return LOCAL_API

  return itemLink
}

function localProvinces() {
  return [{
    value: 'local',
    name: 'محلي',
    database: 'DatabaseCompany',
    link: LOCAL_API,
  }]
}

function demoProvinces() {
  return [{
    value: DEMO_BRANCH_VALUE,
    name: 'النجف - DEMO',
    database: DEMO_BRANCH_VALUE,
    link: DEMO_API,
  }]
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
  if (isDemo()) {
    provinces.value = demoProvinces()
    isLoading.value = false
    error.value = null

    return provinces.value
  }

  if (isLocalLab()) {
    provinces.value = localProvinces()
    isLoading.value = false
    error.value = null

    return provinces.value
  }

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
  if (isDemo()) {
    provinces.value = demoProvinces()
  } else if (isLocalLab()) {
    provinces.value = localProvinces()
  } else if (provinces.value.length === 0) {
    const cached = loadFromCache()
    if (cached.length > 0) {
      provinces.value = cached
    }
  }

  if (!isDemo() && !isLocalLab() && (provinces.value.length === 0 || !fetchPromise)) {
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

export { fetchCities, getCityLink, getCityName, getCityDatabase, loadFromCache, LOCAL_API, DEMO_API }
