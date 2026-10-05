import { computed } from 'vue'

export function useUserRole() {
  const userType = computed(() => String(localStorage.getItem('UserType') || '').trim())
  const isAdmin = computed(() => userType.value === 'محاسب رئيسي')
  const isBranchManager = computed(() => userType.value === 'مدير فرع')
  const canManageUsers = computed(() => isAdmin.value)
  const canBackup = computed(() => isAdmin.value)
  const canSwitchCity = computed(() => isAdmin.value)
  const isSalesManager = computed(() => userType.value === 'مدير مبيعات')
  const isDirector = computed(() => userType.value === 'المدير')
  const canViewDecisions = computed(() => isAdmin.value || isBranchManager.value)
  const canWriteNotes = computed(() => isAdmin.value || isBranchManager.value)
  const canDecide = computed(() => isBranchManager.value)
  const canViewFollowerGps = computed(() => isAdmin.value)
  const canViewRatings = computed(() => isAdmin.value || isDirector.value || isSalesManager.value)

  return {
    userType,
    isAdmin,
    isBranchManager,
    canManageUsers,
    canBackup,
    canSwitchCity,
    canViewDecisions,
    canWriteNotes,
    canDecide,
    isSalesManager,
    isDirector,
    canViewFollowerGps,
    canViewRatings,
  }
}
