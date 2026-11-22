export const ROLE_DIRECTOR = 'المدير'
export const ROLE_SUPERVISOR = 'مسؤول المراقبين'
export const ROLE_MONITOR_LEGACY = 'المراقب'
export const ROLE_COMMITTEE_HEAD = 'مسؤول لجنة المراقبة'
export const ROLE_COMMITTEE_ASSISTANT = 'مساعد مسؤول لجنة المراقبة'
export const ROLE_ARCHIVE_MANAGER = 'مسؤول الارشيف'

/** محاسب القانونية — legal desk on this dashboard (all branches). */
export const ROLE_LEGAL_ACCOUNTANT = 'محاسب القانونية'
/** مدير القانونية — users of the separate legal app; they do not use this dashboard. */
export const ROLE_LEGAL_TEAM = 'مدير القانونية'

export function isLegalAccountantRole(userType) {
  return String(userType || '') === ROLE_LEGAL_ACCOUNTANT
}

export function isArchiveManagerRole(userType) {
  return String(userType || '') === ROLE_ARCHIVE_MANAGER
}

export function isSupervisorRole(userType) {
  const t = String(userType || '')
  return t === ROLE_SUPERVISOR || t === ROLE_MONITOR_LEGACY
}

export function isCommitteeHeadRole(userType) {
  return String(userType || '') === ROLE_COMMITTEE_HEAD
}

export function isCommitteeAssistantRole(userType) {
  return String(userType || '') === ROLE_COMMITTEE_ASSISTANT
}

export function isCommitteeRole(userType) {
  return isCommitteeHeadRole(userType) || isCommitteeAssistantRole(userType)
}

export function isOversightBoardRole(userType) {
  const t = String(userType || '')
  return t === ROLE_DIRECTOR || isSupervisorRole(t) || isCommitteeRole(t)
}

export function canManageOversightAccounts(userType) {
  const t = String(userType || '')
  return t === ROLE_DIRECTOR || isSupervisorRole(t)
}

export function readAllowedCities() {
  try {
    const raw = localStorage.getItem('OversightCities')
    const list = JSON.parse(raw || '[]')
    return Array.isArray(list) ? list.map(x => String(x || '').trim()).filter(Boolean) : []
  }
  catch {
    return []
  }
}

export function storeAllowedCities(cities) {
  const list = Array.isArray(cities) ? cities.map(x => String(x || '').trim()).filter(Boolean) : []
  if (list.length)
    localStorage.setItem('OversightCities', JSON.stringify(list))
  else
    localStorage.removeItem('OversightCities')
}
