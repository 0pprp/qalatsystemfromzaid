import { onMounted, onUnmounted, ref } from 'vue'
import { smGet, smPost, withCityQuery } from '@/composables/salesManagerApi'

export const salesRequestUnreadCount = ref(0)

export async function refreshSalesRequestUnread(cityValue = '') {
  try {
    const data = await smGet(withCityQuery('sales-requests/unread-count', cityValue))
    const next = Number(data?.count ?? data?.Count ?? 0)
    salesRequestUnreadCount.value = Number.isFinite(next) && next > 0 ? next : 0
  }
  catch {
    salesRequestUnreadCount.value = 0
  }
}

export async function markAllSalesRequestsRead(cityValue = '') {
  try {
    const data = await smPost(withCityQuery('sales-requests/mark-all-read', cityValue), {})
    await refreshSalesRequestUnread(cityValue)

    return Number(data?.marked ?? data?.Marked ?? 0) || 0
  }
  catch {
    salesRequestUnreadCount.value = 0

    return 0
  }
}

function isSalesRequestsItem(item) {
  return item?.to?.name === 'sales-manager-requests'
}

export function withRequestsUnreadBadge(items) {
  if (!Array.isArray(items))
    return items

  try {
    const count = Number(salesRequestUnreadCount.value) || 0

    return items.map(item => {
      const next = { ...item }

      if (Array.isArray(item.children))
        next.children = withRequestsUnreadBadge(item.children)
      else
        delete next.children

      if (isSalesRequestsItem(item)) {
        if (count > 0) {
          next.badgeContent = String(count)
          next.badgeClass = 'bg-error'
        }
        else {
          delete next.badgeContent
          delete next.badgeClass
        }
      }

      return next
    })
  }
  catch {
    return items
  }
}

export function useSalesRequestUnread(pollMs = 60000) {
  let timer
  onMounted(() => {
    refreshSalesRequestUnread().catch(() => {
      salesRequestUnreadCount.value = 0
    })
    timer = window.setInterval(() => {
      refreshSalesRequestUnread().catch(() => {
        salesRequestUnreadCount.value = 0
      })
    }, pollMs)
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
