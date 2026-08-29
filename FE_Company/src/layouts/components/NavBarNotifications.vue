<script setup>
import { getAuthHeaders } from "@/services/tokenService"
import axios from "axios"
import { onMounted, onUnmounted, ref } from "vue"
import { useRouter } from "vue-router"

const notifications = ref([])
const router = useRouter()
const apiUrl = localStorage.getItem('LinkCity')

let pollingInterval = null

function camelizeKey(k) { return k ? k[0].toLowerCase() + k.slice(1) : k }
function camelizeDeep(input) {
  if (Array.isArray(input)) return input.map(camelizeDeep)
  if (input && typeof input === "object") {
    const out = {}
    for (const [k, v] of Object.entries(input)) { out[camelizeKey(k)] = camelizeDeep(v) }

    return out
  }

  return input
}

function fmtDate(d) {
  if (!d) return 'الآن'
  const date = new Date(d)
  const now = new Date()
  const diffInSeconds = Math.floor((now - date) / 1000)

  if (diffInSeconds < 60) return 'الآن'
  if (diffInSeconds < 3600) return `منذ ${Math.floor(diffInSeconds / 60)} دقيقة`
  if (diffInSeconds < 86400) return `منذ ${Math.floor(diffInSeconds / 3600)} ساعة`

  return date.toLocaleDateString('ar-EG')
}

function typeColor(type) {
  if (type === 'قانونية') return 'error'
  if (type === 'وهمي') return 'warning'
  if (type === 'متواصل') return 'info'

  return 'primary'
}

async function fetchNotifications() {
  if (!apiUrl) return

  try {
    const headers = getAuthHeaders()
    const { data } = await axios.get(`${apiUrl}CustomerDecisions/Notifications`, { headers })
    const rawData = camelizeDeep(data) || []

    notifications.value = rawData.map(item => ({
      id: item.notificationID,
      title: `${item.decisionType || 'قرار'}: ${item.customerName || 'زبون'}`,
      subtitle: `${item.userName || ''} | النسبة ${item.paidPercent ?? 0}%`,
      time: fmtDate(item.createdDate),
      isSeen: item.isRead === true,
      color: typeColor(item.decisionType),
      icon: 'tabler-gavel',
      originalItem: item,
    }))
  } catch (e) {
    console.error("Failed to fetch notifications:", e)
  }
}

const removeNotification = notificationId => {
  notifications.value = notifications.value.filter(item => item.id !== notificationId)
}

const markRead = async notificationIds => {
  const ids = Array.isArray(notificationIds) ? notificationIds : [notificationIds]
  const headers = getAuthHeaders()

  try {
    if (ids.length > 1) {
      await axios.put(`${apiUrl}CustomerDecisions/Notifications/ReadAll`, {}, { headers })
    } else if (ids[0]) {
      await axios.put(`${apiUrl}CustomerDecisions/Notifications/${ids[0]}/Read`, {}, { headers })
    }
  } catch (e) {
    console.error(e)
  }

  notifications.value.forEach(item => {
    if (ids.includes(item.id)) item.isSeen = true
  })
}

const markUnRead = notificationIds => {
  const ids = Array.isArray(notificationIds) ? notificationIds : [notificationIds]
  notifications.value.forEach(item => {
    if (ids.includes(item.id)) item.isSeen = false
  })
}

const handleNotificationClick = async notification => {
  const customerID = notification.originalItem?.customerID
  if (customerID) {
    router.push({ name: 'customer-profile', query: { customerID } })
  } else {
    router.push({ name: 'decisions-list' })
  }

  if (!notification.isSeen) {
    await markRead([notification.id])
  }
}

onMounted(() => {
  fetchNotifications()
  pollingInterval = setInterval(fetchNotifications, 60000)
})

onUnmounted(() => {
  if (pollingInterval) clearInterval(pollingInterval)
})
</script>

<template>
  <Notifications
    :notifications="notifications"
    @remove="removeNotification"
    @read="markRead"
    @unread="markUnRead"
    @click:notification="handleNotificationClick"
  />
</template>
