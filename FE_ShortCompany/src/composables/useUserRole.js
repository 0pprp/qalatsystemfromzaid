import { computed } from 'vue'
import { isSummaryReadOnly } from '@/composables/useSummaryReadOnly'

export function useUserRole() {
  const userType = computed(() => localStorage.getItem('UserType') || '')
  const isAdmin = computed(() => userType.value === 'محاسب رئيسي')
  const isBranchManager = computed(() => userType.value === 'مدير فرع')
  const canMutate = computed(() => !isSummaryReadOnly)
  const canManageUsers = computed(() => canMutate.value && isAdmin.value)
  const canBackup = computed(() => canMutate.value && isAdmin.value)
  const canSwitchCity = computed(() => isSummaryReadOnly || !isBranchManager.value)
  const canViewDecisions = computed(() => isAdmin.value || isBranchManager.value)
  const canWriteNotes = computed(() => canMutate.value && (isAdmin.value || isBranchManager.value))
  const canDecide = computed(() => canMutate.value && isBranchManager.value)

  return {
    userType,
    isAdmin,
    isBranchManager,
    canMutate,
    canManageUsers,
    canBackup,
    canSwitchCity,
    canViewDecisions,
    canWriteNotes,
    canDecide,
  }
}
