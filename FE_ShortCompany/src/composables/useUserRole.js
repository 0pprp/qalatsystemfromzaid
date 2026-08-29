import { computed } from 'vue'

export function useUserRole() {
  const userType = computed(() => localStorage.getItem('UserType') || '')
  const isAdmin = computed(() => userType.value === 'محاسب رئيسي')
  const isBranchManager = computed(() => userType.value === 'مدير فرع')
  const canManageUsers = computed(() => isAdmin.value)
  const canBackup = computed(() => isAdmin.value)
  const canSwitchCity = computed(() => !isBranchManager.value)
  const canViewDecisions = computed(() => isAdmin.value || isBranchManager.value)
  const canWriteNotes = computed(() => isAdmin.value || isBranchManager.value)
  const canDecide = computed(() => isBranchManager.value)

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
  }
}
