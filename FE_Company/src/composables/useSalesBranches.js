import { computed, watch } from 'vue'
import { useCities, isDemo, isLocalLab } from '@/composables/useCities'

const EXCLUDED = ['قانونية الشركة', 'تجريبي', 'الشهري']

export function isExcludedSalesBranch(name) {
  return EXCLUDED.some(part => String(name || '').includes(part))
}

export function isCentralSalesManager() {
  return localStorage.getItem('UserType') === 'مدير مبيعات'
    && localStorage.getItem('SalesManagerScope') === 'central'
}

export function salesGatewayBase() {
  const fromEnv = String(import.meta.env.VITE_SALES_GATEWAY_URL || '').trim()
  if (fromEnv)
    return fromEnv.endsWith('/') ? fromEnv : `${fromEnv}/`

  return '/sales-gw/api/'
}

export function useSalesBranches() {
  const { provinces, isLoading, error, fetchCities } = useCities()

  const branches = computed(() => {
    if (isDemo() || isLocalLab())
      return provinces.value

    return provinces.value.filter(p => !isExcludedSalesBranch(p.name))
  })

  const items = computed(() => [
    { title: 'الكل', value: '' },
    ...branches.value.map(p => ({ title: p.name, value: String(p.value) })),
  ])

  watch(provinces, list => {
    if (!list.length)
      fetchCities()
  }, { immediate: true })

  return { branches, items, isLoading, error, fetchCities }
}
