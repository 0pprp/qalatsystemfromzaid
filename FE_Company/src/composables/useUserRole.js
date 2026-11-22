import { computed } from 'vue'
import {
  canManageOversightAccounts,
  isArchiveManagerRole,
  isLegalAccountantRole,
  isCommitteeAssistantRole,
  isCommitteeHeadRole,
  isCommitteeRole,
  isOversightBoardRole,
  isSupervisorRole,
  ROLE_DIRECTOR,
} from '@/utils/oversightRoles'

export function useUserRole() {
  const userType = computed(() => String(localStorage.getItem('UserType') || '').trim())
  const isAdmin = computed(() => userType.value === 'محاسب رئيسي')
  const isBranchManager = computed(() => userType.value === 'مدير فرع')
  const canManageUsers = computed(() => isAdmin.value)
  const canBackup = computed(() => isAdmin.value)
  const canSwitchCity = computed(() => isAdmin.value)
  const isSalesManager = computed(() => userType.value === 'مدير مبيعات')
  const isDirector = computed(() => userType.value === ROLE_DIRECTOR)
  const isMonitor = computed(() => isSupervisorRole(userType.value))
  const isCommitteeHead = computed(() => isCommitteeHeadRole(userType.value))
  const isCommitteeAssistant = computed(() => isCommitteeAssistantRole(userType.value))
  const isCommittee = computed(() => isCommitteeRole(userType.value))
  const isArchiveManager = computed(() => isArchiveManagerRole(userType.value))
  const isLegalAccountant = computed(() => isLegalAccountantRole(userType.value))
  const isDirectorOrMonitor = computed(() => isDirector.value || isMonitor.value)
  const canOpenOversightBoards = computed(() => isOversightBoardRole(userType.value))
  const canManageOversight = computed(() => canManageOversightAccounts(userType.value))
  const canViewDecisions = computed(() => isAdmin.value || isBranchManager.value || isMonitor.value)
  const canWriteNotes = computed(() => isAdmin.value || isBranchManager.value)
  const canDecide = computed(() => isBranchManager.value)
  /** Follower GPS lives under عمل المتابعين for المراقب/المدير (not accountant). */
  const canViewFollowerGps = computed(() => isMonitor.value || isDirector.value || isCommittee.value)
  const canViewRatings = computed(() => isAdmin.value || isDirector.value || isSalesManager.value)
  /** «تقييم لجنة المراقبة» — both committee members (+ المدير / المراقب). */
  const canRateHeadSlot = computed(() => isDirector.value || isMonitor.value || isCommittee.value)
  /** «تقييم المحاسب الرئيسي» — the branch main accountant only. */
  const canRateAccountantSlot = computed(() => isAdmin.value)

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
    isCommitteeHead,
    isCommitteeAssistant,
    isCommittee,
    isArchiveManager,
    isLegalAccountant,
    isDirectorOrMonitor,
    canOpenOversightBoards,
    canManageOversight,
    canViewFollowerGps,
    canViewRatings,
    canRateHeadSlot,
    canRateAccountantSlot,
  }
}
