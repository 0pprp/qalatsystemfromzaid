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
  const isMonitor = computed(() => userType.value === 'المراقب')
  const isDirectorOrMonitor = computed(() => isDirector.value || isMonitor.value)
  const canViewDecisions = computed(() => isAdmin.value || isBranchManager.value || isMonitor.value)
  const canWriteNotes = computed(() => isAdmin.value || isBranchManager.value)
  const canDecide = computed(() => isBranchManager.value)
  /** Follower GPS lives under عمل المتابعين for المراقب/المدير (not accountant). */
  const canViewFollowerGps = computed(() => isMonitor.value || isDirector.value)
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
    isMonitor,
    isDirectorOrMonitor,
    canViewFollowerGps,
    canViewRatings,
  }
}
