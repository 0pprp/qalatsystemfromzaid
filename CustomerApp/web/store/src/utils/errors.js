/** Extract an Arabic user-facing message from an API error body { error, code, fields }. */
export function apiErrorMessage(err, fallback = 'حدث خطأ غير متوقع، يرجى المحاولة مرة أخرى') {
  if (!err) return fallback
  const data = err.response?.data
  if (data?.error) return data.error
  if (err.code === 'ERR_NETWORK' || (!err.response && err.request)) return 'تعذّر الاتصال بالخادم، تحقّق من اتصالك بالإنترنت'
  if (err.response?.status === 429) return 'محاولات كثيرة، يرجى الانتظار قليلاً'
  if (err.response?.status === 404) return 'العنصر المطلوب غير موجود'
  return fallback
}
export function apiErrorCode(err) {
  return err?.response?.data?.code ?? null
}
export function apiErrorFields(err) {
  return err?.response?.data?.fields ?? {}
}
