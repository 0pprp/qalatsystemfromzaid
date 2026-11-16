import { computed, watch } from 'vue'
import { useCities, isDemo, isLocalLab } from '@/composables/useCities'
import { isCommitteeRole, isOversightBoardRole, readAllowedCities } from '@/utils/oversightRoles'

const EXCLUDED = ['قانونية الشركة', 'تجريبي', 'الشهري']

export function isExcludedSalesBranch(name) {
  return EXCLUDED.some(part => String(name || '').includes(part))
}

export function isCentralSalesManager() {
  const userType = localStorage.getItem('UserType')
  // المدير / مسؤول المراقبين / لجنة المراقبة يستعملون بوابة المبيعات المركزية
  if (isOversightBoardRole(userType))
    return true

  return userType === 'مدير مبيعات'
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
    const base = (isDemo() || isLocalLab())
      ? provinces.value
      : provinces.value.filter(p => !isExcludedSalesBranch(p.name))
    if (!isCommitteeRole(localStorage.getItem('UserType')))
      return base
    // Committee with no stored branches sees none (the API refuses them anyway).
    const allowed = new Set(readAllowedCities())
    return base.filter(p => allowed.has(String(p.value || '').trim()))
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
