/**
 * Creatable UserType options for the users-list dialog.
 * Must stay aligned with BE UserCreationAuthorization (Arabic UserType literals).
 */

export const USER_TYPE_SALES_MANAGER = 'مدير مبيعات'
export const USER_TYPE_MAIN_ACCOUNTANT = 'محاسب رئيسي'

const MAIN_ACCOUNTANT_CREATABLE_TYPES = Object.freeze([
  'محاسب رئيسي',
  'محاسب فرعي',
  'مدير فرع',
  'مدير مبيعات',
  'موظف مبيعات',
  'موظف فلترة المبيعات',
  'متابع',
])

/**
 * @param {string} actorUserType
 * @returns {readonly string[]}
 */
export function creatableUserTypesFor(actorUserType) {
  if (actorUserType === USER_TYPE_MAIN_ACCOUNTANT) {
    return MAIN_ACCOUNTANT_CREATABLE_TYPES
  }

  return []
}

/**
 * Extract Arabic error message from axios error / backend payload.
 * @param {unknown} error
 * @returns {string}
 */
export function userFormErrorMessage(error) {
  const data = error && typeof error === 'object' && 'response' in error
    ? error.response?.data
    : null

  if (data && typeof data === 'object' && data.message) {
    return String(data.message)
  }

  if (typeof data === 'string' && data.trim()) {
    return data
  }

  return 'تعذر حفظ المستخدم'
}
