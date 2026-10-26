import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { directorCities } from '@/composables/directorApi'
import { useUserRole } from '@/composables/useUserRole'

const STORAGE_KEY = 'MonitorLegalCity'

/**
 * Monitor (central) needs an explicit province before reading أسماء قانونية queues.
 * Prefer route query `city`; fall back to sessionStorage so sub-tabs keep the selection.
 */
export function useMonitorLegalCity() {
  const { isMonitor } = useUserRole()
  const route = useRoute()
  const router = useRouter()
  const cities = ref([])
  const citiesLoading = ref(false)
  const citiesError = ref('')

  const cityValue = computed(() => {
    const fromQuery = String(route.query.city || '').trim()
    if (fromQuery)
      return fromQuery
    try {
      return String(sessionStorage.getItem(STORAGE_KEY) || '').trim()
    }
    catch {
      return ''
    }
  })

  const cityItems = computed(() =>
    (cities.value || []).map(c => ({
      title: c.name || c.Name || c.value || c.Value,
      value: String(c.value || c.Value || ''),
    })).filter(i => i.value),
  )

  async function loadCities() {
    if (!isMonitor.value)
      return
    citiesLoading.value = true
    citiesError.value = ''
    try {
      const raw = await directorCities()
      cities.value = Array.isArray(raw) ? raw : []
    }
    catch (e) {
      cities.value = []
      citiesError.value = e?.response?.data?.message || e?.message || 'تعذر تحميل المحافظات'
    }
    finally {
      citiesLoading.value = false
    }
  }

  function setCity(value) {
    const next = String(value || '').trim()
    try {
      if (next)
        sessionStorage.setItem(STORAGE_KEY, next)
      else
        sessionStorage.removeItem(STORAGE_KEY)
    }
    catch { /* ignore */ }
    const q = { ...route.query }
    if (next)
      q.city = next
    else
      delete q.city
    router.replace({ path: route.path, query: q })
  }

  onMounted(() => {
    loadCities()
    // Sync query from session when landing without ?city=
    if (isMonitor.value && !String(route.query.city || '').trim() && cityValue.value)
      setCity(cityValue.value)
  })

  return {
    isMonitor,
    cities,
    cityItems,
    cityValue,
    citiesLoading,
    citiesError,
    loadCities,
    setCity,
  }
}

/** Optional: reload when monitor city query changes. */
export function watchMonitorLegalCity(cityValue, onCity, { immediate = true } = {}) {
  return watch(cityValue, (v, prev) => {
    if (v === prev && !immediate)
      return
    onCity(v)
  }, { immediate })
}
