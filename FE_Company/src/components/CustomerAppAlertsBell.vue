<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  caAge,
  caMinutes,
  caSeenAlert,
  caSeenAllAlerts,
  caUnseenAlerts,
  caYmd,
} from '@/composables/customerAppApi'
import { formatIraqClock } from '@/composables/gpsTrack'
import { useToast } from '@/composables/useToast'

/**
 * Navbar bell for المدير / المراقب: «بقي في نفس المكان» alerts from تطبيق المسوّقين.
 * Polls every minute; a new alert shows a toast, plays a short tone and (when allowed) a browser notification.
 */
const router = useRouter()
const toast = useToast()

const open = ref(false)
const unseen = ref(0)
const alerts = ref([])
const busy = ref(false)

const known = new Set()
let first = true
let timer

const hasAlerts = computed(() => unseen.value > 0)

function clock(value) {
  return value ? formatIraqClock(value) : '—'
}

function beep() {
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)()
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()

    osc.frequency.value = 880
    gain.gain.setValueAtTime(0.15, ctx.currentTime)
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.6)
    osc.connect(gain).connect(ctx.destination)
    osc.start()
    osc.stop(ctx.currentTime + 0.6)
  }
  catch {
    // no audio (autoplay policy) — the toast is enough
  }
}

function notifyBrowser(alert) {
  try {
    if (!('Notification' in window) || Notification.permission !== 'granted')
      return

    const n = new Notification('تنبيه دوام المسوّقين', {
      body: `${alert.name} في نفس المكان منذ ${caMinutes(alert.minutes)}`,
      tag: `ca-alert-${alert.id}`,
    })

    n.onclick = () => {
      window.focus()
      showOnMap(alert)
    }
  }
  catch {
    // ignore
  }
}

async function load() {
  if (document.hidden && !first)
    return
  try {
    const res = await caUnseenAlerts()
    const list = res?.latest || []
    const fresh = list.filter(a => !known.has(a.id))

    for (const a of list)
      known.add(a.id)
    unseen.value = res?.unseen || 0
    alerts.value = list
    if (!first && fresh.length) {
      const a = fresh[0]

      toast.warning(`تنبيه: ${a.name} بقي في نفس المكان منذ ${caMinutes(a.minutes)}`)
      beep()
      fresh.forEach(notifyBrowser)
    }
    first = false
  }
  catch {
    // BE_CustomerApp unreachable: keep the last state, try again next minute
  }
}

function askPermission() {
  try {
    if ('Notification' in window && Notification.permission === 'default')
      Notification.requestPermission()
  }
  catch {
    // ignore
  }
}

function showOnMap(alert) {
  open.value = false
  router.push({ path: '/customer-app-attendance', query: { userId: alert.userId, date: caYmd(new Date(alert.createdAt)) } })
}

async function markSeen(alert) {
  try {
    await caSeenAlert(alert.id)
    alerts.value = alerts.value.filter(a => a.id !== alert.id)
    unseen.value = Math.max(0, unseen.value - 1)
  }
  catch {
    toast.error('تعذر تحديث التنبيه')
  }
}

async function markAll() {
  busy.value = true
  try {
    await caSeenAllAlerts()
    alerts.value = []
    unseen.value = 0
  }
  catch {
    toast.error('تعذر تحديث التنبيهات')
  }
  finally {
    busy.value = false
  }
}

onMounted(() => {
  load()
  timer = setInterval(load, 60000)
})

onUnmounted(() => clearInterval(timer))
</script>

<template>
  <VMenu
    v-model="open"
    location="bottom end"
    offset="12"
    :close-on-content-click="false"
  >
    <template #activator="{ props }">
      <IconBtn
        v-bind="props"
        title="تنبيهات دوام المسوّقين"
        @click="askPermission"
      >
        <VBadge
          :model-value="hasAlerts"
          :content="unseen > 99 ? '99+' : unseen"
          color="error"
        >
          <VIcon
            icon="tabler-map-pin-exclamation"
            size="24"
            :color="hasAlerts ? 'error' : undefined"
          />
        </VBadge>
      </IconBtn>
    </template>

    <VCard
      width="380"
      max-height="520"
      class="d-flex flex-column"
    >
      <VCardItem class="py-3">
        <VCardTitle class="text-subtitle-1">
          تنبيهات دوام المسوّقين
        </VCardTitle>
        <template #append>
          <VBtn
            v-if="alerts.length"
            size="small"
            variant="text"
            :loading="busy"
            @click="markAll"
          >
            تعليم الكل كمقروء
          </VBtn>
        </template>
      </VCardItem>
      <VDivider />
      <div class="overflow-y-auto">
        <div
          v-if="!alerts.length"
          class="pa-6 text-center text-medium-emphasis"
        >
          لا توجد تنبيهات جديدة.
          <div class="text-caption mt-1">
            يصل تنبيه عندما يبقى مسوّق في الدوام في نفس المكان لمدة ساعة.
          </div>
        </div>
        <VList
          v-else
          density="compact"
          lines="three"
        >
          <VListItem
            v-for="a in alerts"
            :key="a.id"
          >
            <template #prepend>
              <VAvatar
                :color="a.isOpen ? 'error' : 'secondary'"
                variant="tonal"
                size="36"
              >
                <VIcon icon="tabler-clock-pause" />
              </VAvatar>
            </template>
            <VListItemTitle class="font-weight-medium">
              {{ a.name }}
            </VListItemTitle>
            <VListItemSubtitle>
              {{ a.isOpen ? 'ما زال' : 'كان' }} في نفس المكان {{ caMinutes(a.minutes) }}
              (منذ {{ clock(a.sinceAt) }})
              <span v-if="a.cityName"> · {{ a.cityName }}</span>
            </VListItemSubtitle>
            <div class="d-flex gap-2 mt-1">
              <VBtn
                size="x-small"
                variant="tonal"
                prepend-icon="tabler-map"
                @click="showOnMap(a)"
              >
                على الخريطة
              </VBtn>
              <VBtn
                size="x-small"
                variant="text"
                @click="markSeen(a)"
              >
                تم الاطلاع
              </VBtn>
              <VSpacer />
              <span class="text-caption text-medium-emphasis align-self-center">{{ caAge(a.createdAt) }}</span>
            </div>
          </VListItem>
        </VList>
      </div>
    </VCard>
  </VMenu>
</template>
