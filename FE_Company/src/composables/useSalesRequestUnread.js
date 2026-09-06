import { onMounted, onUnmounted, ref } from 'vue'
import { smGet, smPost, withCityQuery } from '@/composables/salesManagerApi'

export const salesRequestUnreadCount = ref(0)

export async function refreshSalesRequestUnread(cityValue = '') {
  try {
    const data = await smGet(withCityQuery('sales-requests/unread-count', cityValue))
    salesRequestUnreadCount.value = Number(data?.count ?? data?.Count ?? 0) || 0
  }
  catch {
    salesRequestUnreadCount.value = 0
  }
}

export async function markAllSalesRequestsRead(cityValue = '') {
  const data = await smPost(withCityQuery('sales-requests/mark-all-read', cityValue), {})
  await refreshSalesRequestUnread(cityValue)

  return Number(data?.marked ?? data?.Marked ?? 0) || 0
}

export function withRequestsUnreadBadge(items) {
  const count = salesRequestUnreadCount.value
  const apply = list => (list || []).map(item => {
    const children = item.children ? apply(item.children) : item.children
    const isRequests = item.to?.name === 'sales-manager-requests'
    return {
      ...item,
      children,
      badgeContent: isRequests && count > 0 ? String(count) : item.badgeContent,
      badgeClass: isRequests && count > 0 ? 'bg-error' : item.badgeClass,
    }
  })

  return apply(items)
}

export function useSalesRequestUnread(pollMs = 60000) {
  let timer
  onMounted(() => {
    refreshSalesRequestUnread()
    timer = window.setInterval(() => refreshSalesRequestUnread(), pollMs)
  })
  onUnmounted(() => {
    if (timer)
      window.clearInterval(timer)
  })

  return {
    unreadCount: salesRequestUnreadCount,
    refresh: refreshSalesRequestUnread,
    markAllRead: markAllSalesRequestsRead,
  }
}
