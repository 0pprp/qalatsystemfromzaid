<template>
  <div>
    <div class="d-flex align-center flex-wrap ga-2 mb-3">
      <h1 class="text-h6 font-weight-bold">الإشعارات</h1>
      <v-chip v-if="unread" size="small" color="error" variant="tonal">{{ unread }} غير مقروء</v-chip>
      <v-spacer />
      <v-btn variant="tonal" color="primary" size="small" prepend-icon="mdi-check-all" :disabled="!unread" :loading="markingAll" @click="markAll">تعليم الكل كمقروء</v-btn>
    </div>
    <template v-if="loading && !items.length">
      <v-skeleton-loader v-for="n in 4" :key="n" type="list-item-avatar-two-line" class="mb-2 rounded-xl" />
    </template>
    <ErrorState v-else-if="error && !items.length" :message="error" @retry="load(true)" />
    <EmptyState v-else-if="!items.length" icon="mdi-bell-off-outline" title="لا توجد إشعارات" text="ستظهر هنا تحديثات طلباتك وأرباحك والعروض." />
    <v-card v-else class="soft-card" rounded="xl">
      <template v-for="(n, i) in items" :key="n.id">
        <v-divider v-if="i" />
        <div class="notif pa-4" :class="{ unread: !n.readAt }" role="button" tabindex="0" @click="open(n)" @keydown.enter="open(n)">
          <v-avatar :color="meta(n).color" variant="tonal" size="42"><v-icon>{{ meta(n).icon }}</v-icon></v-avatar>
          <div class="flex-grow-1 min-w-0">
            <div class="d-flex align-center ga-2">
              <span class="font-weight-bold">{{ n.title }}</span>
              <span v-if="!n.readAt" class="dot" aria-label="غير مقروء" />
              <v-spacer />
              <span class="text-caption text-medium-emphasis text-no-wrap">{{ timeAgo(n.createdAt) }}</span>
            </div>
            <div class="text-body-2 text-medium-emphasis mt-1">{{ n.body }}</div>
            <div v-if="link(n)" class="text-caption text-primary mt-1">عرض التفاصيل ←</div>
          </div>
        </div>
      </template>
      <div v-if="items.length < total" class="d-flex justify-center pa-3">
        <v-btn variant="tonal" color="primary" :loading="loading" @click="load(false)">عرض المزيد</v-btn>
      </div>
    </v-card>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import { notificationsApi } from '@/api'
import { useAuthStore } from '@/stores/auth'
import { NOTIFICATION_TYPE } from '@/utils/enums'
import { timeAgo } from '@/utils/date'
import { apiErrorMessage } from '@/utils/errors'

const auth = useAuthStore()
const router = useRouter()
const items = ref([])
const total = ref(0)
const page = ref(0)
const unread = ref(0)
const loading = ref(false)
const error = ref('')
const markingAll = ref(false)

const meta = (n) => NOTIFICATION_TYPE[n.type] || NOTIFICATION_TYPE.System
function link(n) {
  if (n.data?.orderId) return { name: 'order', params: { id: n.data.orderId } }
  if (n.data?.productId) return { name: 'product', params: { id: n.data.productId } }
  if (n.type === 'Commission') return { name: 'earnings' }
  if (n.type === 'Network') return { name: 'network' }
  return null
}

async function load(reset) {
  if (reset) {
    page.value = 0
    items.value = []
  }
  loading.value = true
  error.value = ''
  try {
    const res = await notificationsApi.list(page.value + 1)
    page.value = res.page
    total.value = res.total
    unread.value = res.unreadCount ?? 0
    auth.unreadCount = unread.value
    items.value = [...items.value, ...res.items]
  } catch (e) {
    error.value = apiErrorMessage(e)
  } finally {
    loading.value = false
  }
}
async function open(n) {
  if (!n.readAt) {
    n.readAt = new Date().toISOString()
    unread.value = Math.max(0, unread.value - 1)
    auth.unreadCount = unread.value
    notificationsApi.markRead(n.id).catch(() => {})
  }
  const to = link(n)
  if (to) router.push(to)
}
async function markAll() {
  markingAll.value = true
  try {
    await notificationsApi.markAllRead()
    const now = new Date().toISOString()
    items.value.forEach((n) => (n.readAt ||= now))
    unread.value = 0
    auth.unreadCount = 0
  } catch { /* ignore */ } finally {
    markingAll.value = false
  }
}
onMounted(() => load(true))
</script>

<style scoped>
.notif { display: flex; gap: 14px; align-items: flex-start; cursor: pointer; transition: background 0.15s; }
.notif:hover { background: #f5f9f9; }
.notif.unread { background: #eef7f6; }
.dot { width: 8px; height: 8px; border-radius: 50%; background: #c62828; display: inline-block; }
.min-w-0 { min-width: 0; }
</style>
