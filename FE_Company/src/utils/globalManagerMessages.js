import { USER_TYPE_SALES_MANAGER } from '../utils/userCreationRoles.js'

export function isGlobalSalesManagerType(userType) {
  return String(userType || '').trim() === USER_TYPE_SALES_MANAGER
}

export function globalManagerUserMessage(result) {
  const status = result?.data?.status || ''
  if (result?.ok && status === 'Success')
    return 'تم إنشاء مدير المبيعات في جميع الفروع بنجاح'
  if (status === 'PartialFailure')
    return 'تم التنفيذ جزئياً، تعذر تحديث بعض الفروع'
  if (status === 'Conflict') {
    const names = (result?.data?.failedBranches || [])
      .map(b => b.cityName || b.CityName)
      .filter(Boolean)

    return names.length
      ? `تعارض في المحافظات: ${names.join('، ')}`
      : 'تعارض في أحد الفروع'
  }
  if (result?.data?.message)
    return String(result.data.message)

  return 'تعذر إنشاء مدير المبيعات'
}
