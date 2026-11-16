<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import mapboxgl from '@/utils/mapbox'
import 'mapbox-gl/dist/mapbox-gl.css'
import {
  caAge,
  caAgentDay,
  caAgentHistory,
  caAttendance,
  caAttendanceAlerts,
  caDate,
  caErrorMessage,
  caMinutes,
  caSeenAlert,
  caYmd,
} from '@/composables/customerAppApi'
import { formatIraqClock } from '@/composables/gpsTrack'
import { MAPBOX_TOKEN } from '@/composables/mapboxToken'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()
const route = useRoute()

const token = MAPBOX_TOKEN
const date = ref(/^\d{4}-\d{2}-\d{2}$/.test(String(route.query.date || '')) ? String(route.query.date) : caYmd())
const rows = ref([])
const loading = ref(false)
const listError = ref('')
const search = ref('')

const selectedId = ref(route.query.userId ? Number(route.query.userId) : null)
const day = ref(null)
const dayLoading = ref(false)

// «بقي في نفس المكان» alerts of the selected date
const alerts = ref([])

/** userId → that agent's most relevant alert (open first, then newest). */
const alertByUser = computed(() => {
  const out = {}
  for (const a of alerts.value) {
    const cur = out[a.userId]
    if (!cur || (a.isOpen && !cur.isOpen))
      out[a.userId] = a
  }

  return out
})

const openAlerts = computed(() => alerts.value.filter(a => a.isOpen).length)
const selectedAlerts = computed(() => alerts.value.filter(a => a.userId === selectedId.value))

async function acknowledge(alert) {
  try {
    await caSeenAlert(alert.id)
    alert.seenAt = new Date().toISOString()
  }
  catch (err) {
    toast.error(caErrorMessage(err, 'تعذر تحديث التنبيه'))
  }
}

const historyOpen = ref(false)
const history = ref([])
const historyLoading = ref(false)

const isToday = computed(() => date.value === caYmd())
const selected = computed(() => rows.value.find(r => r.userId === selectedId.value) || day.value?.summary || null)

const filteredRows = computed(() => {
  const q = search.value.trim()
  if (!q)
    return rows.value

  return rows.value.filter(r => `${r.name} ${r.username || ''} ${r.phone}`.includes(q))
})

const totals = computed(() => ({
  agents: rows.value.length,
  working: rows.value.filter(r => r.isWorking).length,
  attended: rows.value.filter(r => r.sessionsCount > 0).length,
  minutes: rows.value.reduce((s, r) => s + (r.totalMinutes || 0), 0),
}))

function clock(value) {
  return value ? formatIraqClock(value) : '—'
}

// ---------------------------------------------------------------- data
let poll

async function loadDay() {
  loading.value = true
  listError.value = ''
  try {
    rows.value = await caAttendance({ date: date.value }) || []
    try {
      alerts.value = await caAttendanceAlerts({ date: date.value }) || []
    }
    catch {
      alerts.value = []
    }
    paint()
  }
  catch (err) {
    rows.value = []
    listError.value = caErrorMessage(err, 'تعذر تحميل الدوام')
  }
  finally {
    loading.value = false
  }
  if (selectedId.value)
    await loadAgentDay(selectedId.value, { quiet: true })
}

async function loadAgentDay(userId, { quiet = false } = {}) {
  if (!quiet)
    dayLoading.value = true
  try {
    day.value = await caAgentDay(userId, date.value)
    paint({ fit: !quiet })
  }
  catch (err) {
    day.value = null
    if (!quiet)
      toast.error(caErrorMessage(err, 'تعذر تحميل مسار المسوّق'))
  }
  finally {
    dayLoading.value = false
  }
}

function selectAgent(row) {
  if (selectedId.value === row.userId) {
    selectedId.value = null
    day.value = null
    paint({ fit: true })

    return
  }
  selectedId.value = row.userId
  loadAgentDay(row.userId)
}

async function openHistory() {
  if (!selectedId.value)
    return
  historyOpen.value = true
  historyLoading.value = true
  try {
    history.value = await caAgentHistory(selectedId.value) || []
  }
  catch (err) {
    history.value = []
    toast.error(caErrorMessage(err, 'تعذر تحميل سجل الدوام'))
  }
  finally {
    historyLoading.value = false
  }
}

function openHistoryDay(row) {
  historyOpen.value = false
  date.value = row.date
}

function startPolling() {
  clearInterval(poll)

  // Live view: refresh positions every 30 seconds while looking at today.
  poll = setInterval(() => {
    if (isToday.value && !document.hidden)
      loadDay()
  }, 30000)
}

watch(date, () => loadDay())

// ---------------------------------------------------------------- map
const mapEl = ref(null)
let map
let resizeObserver
let bleedEl
const markers = []

function clearMarkers() {
  for (const m of markers)
    m.remove()
  markers.length = 0
}

function whenMapReady(cb) {
  if (!map)
    return
  if (map.isStyleLoaded())
    cb()
  else
    map.once('load', cb)
}

function dotEl(color, label, big = false) {
  const el = document.createElement('div')

  el.className = 'ca-att-dot'
  el.style.background = color
  el.style.inlineSize = el.style.blockSize = big ? '18px' : '14px'
  if (label)
    el.title = label

  return el
}

function setRoute(coords) {
  const data = { type: 'Feature', geometry: { type: 'LineString', coordinates: coords } }
  if (map.getSource('ca-route')) {
    map.getSource('ca-route').setData(data)

    return
  }
  map.addSource('ca-route', { type: 'geojson', data })
  map.addLayer({
    id: 'ca-route-line',
    type: 'line',
    source: 'ca-route',
    layout: { 'line-join': 'round', 'line-cap': 'round' },
    paint: { 'line-color': '#0f766e', 'line-width': 5, 'line-opacity': 0.85 },
  })
}

function paint({ fit = false } = {}) {
  if (!map)
    return
  whenMapReady(() => {
    clearMarkers()

    const bounds = new mapboxgl.LngLatBounds()
    let any = false

    if (selectedId.value && day.value) {
      // One agent: the day's trail with start / current-or-end markers.
      const track = day.value.track || []
      const coords = track.map(p => [Number(p.lng), Number(p.lat)])

      setRoute(coords.length >= 2 ? coords : [])
      if (coords.length) {
        const first = track[0]
        const last = track[track.length - 1]

        markers.push(new mapboxgl.Marker({ color: '#16a34a' })
          .setLngLat(coords[0])
          .setPopup(new mapboxgl.Popup({ offset: 24, closeButton: false }).setHTML(`<div dir="rtl">أول نقطة ${clock(first.at)}</div>`))
          .addTo(map))

        const working = day.value.summary?.isWorking

        markers.push(new mapboxgl.Marker({ element: dotEl(working ? '#2563eb' : '#dc2626', '', true) })
          .setLngLat(coords[coords.length - 1])
          .setPopup(new mapboxgl.Popup({ offset: 14, closeButton: false })
            .setHTML(`<div dir="rtl">${working ? 'الموقع الحالي' : 'آخر نقطة'} ${clock(last.at)}</div>`))
          .addTo(map))
        for (const c of coords)
          bounds.extend(c)
        any = true
      }
      for (const a of selectedAlerts.value) {
        const el = dotEl('#f97316', 'بقي في نفس المكان', true)

        el.classList.add('is-alert')
        markers.push(new mapboxgl.Marker({ element: el })
          .setLngLat([Number(a.longitude), Number(a.latitude)])
          .setPopup(new mapboxgl.Popup({ offset: 14, closeButton: false })
            .setHTML(`<div dir="rtl"><b>بقي في نفس المكان ${caMinutes(a.minutes)}</b><br>${clock(a.sinceAt)} ← ${a.isOpen ? 'ما زال هناك' : clock(a.lastSeenAt)}</div>`))
          .addTo(map))
      }
    }
    else {
      // Everyone: last known position of each agent today (green = working now).
      setRoute([])
      for (const r of rows.value) {
        if (!r.lastPoint)
          continue
        const ll = [Number(r.lastPoint.lng), Number(r.lastPoint.lat)]
        const el = dotEl(r.isWorking ? '#16a34a' : '#94a3b8', r.name)
        if (alertByUser.value[r.userId]?.isOpen)
          el.classList.add('is-alert')

        el.addEventListener('click', () => selectAgent(r))
        markers.push(new mapboxgl.Marker({ element: el })
          .setLngLat(ll)
          .setPopup(new mapboxgl.Popup({ offset: 12, closeButton: false })
            .setHTML(`<div dir="rtl"><b>${r.name}</b><br>${r.isWorking ? 'في الدوام' : 'خارج الدوام'} · ${clock(r.lastPoint.at)}</div>`))
          .addTo(map))
        bounds.extend(ll)
        any = true
      }
    }

    if (fit && any)
      map.fitBounds(bounds, { padding: 60, maxZoom: 16, duration: 600 })
  })
}

async function ensureMap() {
  await nextTick()
  if (!token || !mapEl.value || map)
    return
  mapboxgl.accessToken = token
  mapEl.value.setAttribute('dir', 'ltr')
  map = new mapboxgl.Map({
    container: mapEl.value,
    style: 'mapbox://styles/mapbox/streets-v12',
    center: [44.36, 33.31],
    zoom: 6,
  })
  map.addControl(new mapboxgl.NavigationControl({ showCompass: false }), 'bottom-left')
  resizeObserver = new ResizeObserver(() => map?.resize())
  resizeObserver.observe(mapEl.value)
  map.on('load', () => paint({ fit: true }))
}

onMounted(async () => {
  bleedEl = document.querySelector('.layout-page-content')
  bleedEl?.classList.add('ca-att-bleed')
  await ensureMap()
  await loadDay()
  if (selectedId.value)
    paint({ fit: true })
  startPolling()
})

onUnmounted(() => {
  clearInterval(poll)
  bleedEl?.classList.remove('ca-att-bleed')
  resizeObserver?.disconnect()
  clearMarkers()
  map?.remove()
  map = null
})
</script>

<template>
  <div class="ca-att-page">
    <div class="ca-att-toolbar">
      <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-3">
        <h4 class="text-h4 d-flex align-center gap-2 mb-0">
          <VIcon
            icon="tabler-map-pin-check"
            color="primary"
          />
          دوام المسوّقين
        </h4>
        <div class="d-flex flex-wrap gap-2 align-center">
          <VChip
            v-if="alerts.length"
            :color="openAlerts ? 'error' : 'warning'"
            variant="tonal"
            prepend-icon="tabler-map-pin-exclamation"
            size="small"
          >
            {{ alerts.length }} تنبيه بقاء في نفس المكان<template v-if="openAlerts">
              ({{ openAlerts }} الآن)
            </template>
          </VChip>
          <VChip
            v-if="isToday"
            color="success"
            variant="tonal"
            prepend-icon="tabler-broadcast"
            size="small"
          >
            مباشر — يتحدث كل 30 ثانية
          </VChip>
          <VBtn
            variant="tonal"
            prepend-icon="tabler-refresh"
            :loading="loading"
            @click="loadDay"
          >
            تحديث
          </VBtn>
        </div>
      </div>
      <VRow dense>
        <VCol
          cols="12"
          md="3"
        >
          <VTextField
            v-model="date"
            type="date"
            label="اليوم"
            density="compact"
            hide-details
          />
        </VCol>
        <VCol
          cols="12"
          md="9"
        >
          <div class="ca-att-totals">
            <div><span>المسوّقون</span><b>{{ totals.agents }}</b></div>
            <div><span>داوموا</span><b>{{ totals.attended }}</b></div>
            <div class="text-success">
              <span>في الدوام الآن</span><b>{{ isToday ? totals.working : '—' }}</b>
            </div>
            <div><span>مجموع الساعات</span><b>{{ caMinutes(totals.minutes) }}</b></div>
          </div>
        </VCol>
      </VRow>
      <VAlert
        v-if="listError"
        type="error"
        variant="tonal"
        class="mt-3"
      >
        {{ listError }}
      </VAlert>
      <div
        v-if="!token"
        class="mt-2 text-error"
      >
        مفتاح الخريطة غير مهيأ. أضف VITE_MAPBOX_TOKEN.
      </div>
    </div>

    <div class="ca-att-body">
      <div class="ca-att-list">
        <VTextField
          v-model="search"
          placeholder="بحث بالاسم أو الهاتف"
          density="compact"
          prepend-inner-icon="tabler-search"
          hide-details
          clearable
          class="mb-2"
        />
        <div
          v-if="!loading && !filteredRows.length"
          class="text-medium-emphasis pa-4 text-center"
        >
          لا يوجد مسوّقون
        </div>
        <VCard
          v-for="r in filteredRows"
          :key="r.userId"
          class="ca-att-row mb-2"
          :class="{ 'is-selected': r.userId === selectedId }"
          variant="outlined"
          @click="selectAgent(r)"
        >
          <div class="d-flex align-center gap-3 pa-3">
            <VBadge
              dot
              :color="r.isWorking ? 'success' : (r.sessionsCount ? 'secondary' : 'grey')"
              location="bottom end"
              offset-x="3"
              offset-y="3"
            >
              <VAvatar
                color="primary"
                variant="tonal"
                size="38"
              >
                {{ String(r.name || '?').charAt(0) }}
              </VAvatar>
            </VBadge>
            <div class="flex-grow-1">
              <div class="font-weight-medium">
                {{ r.name }}
              </div>
              <div class="text-caption text-medium-emphasis">
                {{ r.phone }}
                <template v-if="r.lastPoint">
                  · آخر موقع {{ caAge(r.lastPoint.at) }}
                </template>
              </div>
            </div>
            <div class="text-end">
              <VChip
                v-if="r.isWorking"
                size="x-small"
                color="success"
              >
                في الدوام
              </VChip>
              <VChip
                v-else-if="r.sessionsCount"
                size="x-small"
                color="secondary"
                variant="tonal"
              >
                أنهى الدوام
              </VChip>
              <VChip
                v-else
                size="x-small"
                variant="tonal"
              >
                لم يداوم
              </VChip>
              <div class="font-weight-bold mt-1">
                {{ caMinutes(r.totalMinutes) }}
              </div>
            </div>
          </div>
          <div
            v-if="r.sessionsCount"
            class="ca-att-times px-3 pb-2"
          >
            <span>بدأ {{ clock(r.firstStartedAt) }}</span>
            <span>{{ r.isWorking ? 'مستمر' : `أنهى ${clock(r.lastEndedAt)}` }}</span>
            <span v-if="r.sessionsCount > 1">{{ r.sessionsCount }} فترات</span>
          </div>
          <div
            v-if="alertByUser[r.userId]"
            class="px-3 pb-2"
          >
            <VChip
              size="x-small"
              :color="alertByUser[r.userId].isOpen ? 'error' : 'warning'"
              variant="tonal"
              prepend-icon="tabler-clock-pause"
            >
              {{ alertByUser[r.userId].isOpen ? 'في نفس المكان منذ' : 'بقي في نفس المكان' }} {{ caMinutes(alertByUser[r.userId].minutes) }}
            </VChip>
          </div>
        </VCard>
      </div>

      <div class="ca-att-map-col">
        <div class="ca-att-map-wrap">
          <div
            ref="mapEl"
            class="ca-att-map"
          />
          <VProgressLinear
            v-if="dayLoading"
            indeterminate
            color="primary"
            class="ca-att-map-progress"
          />
        </div>

        <div
          v-if="selectedId && selected"
          class="ca-att-detail"
        >
          <div class="d-flex flex-wrap align-center gap-3 mb-2">
            <div class="text-h6">
              {{ selected.name }}
            </div>
            <span
              class="text-medium-emphasis"
              dir="ltr"
            >{{ selected.phone }}</span>
            <VSpacer />
            <VBtn
              size="small"
              variant="tonal"
              prepend-icon="tabler-calendar-stats"
              @click="openHistory"
            >
              سجل الأيام
            </VBtn>
            <VBtn
              size="small"
              variant="text"
              prepend-icon="tabler-x"
              @click="selectAgent({ userId: selectedId })"
            >
              عرض الكل
            </VBtn>
          </div>
          <div class="ca-att-totals mb-2">
            <div><span>بدء الدوام</span><b>{{ clock(day?.summary?.firstStartedAt) }}</b></div>
            <div><span>نهاية الدوام</span><b>{{ day?.summary?.isWorking ? 'مستمر' : clock(day?.summary?.lastEndedAt) }}</b></div>
            <div><span>ساعات العمل</span><b>{{ caMinutes(day?.summary?.totalMinutes) }}</b></div>
            <div><span>نقاط الموقع</span><b>{{ day?.track?.length ?? 0 }}</b></div>
          </div>
          <div
            v-if="day?.sessions?.length"
            class="d-flex flex-wrap gap-2"
          >
            <VChip
              v-for="s in day.sessions"
              :key="s.id"
              size="small"
              :color="s.endedAt ? 'primary' : 'success'"
              variant="tonal"
            >
              {{ clock(s.startedAt) }} ← {{ s.endedAt ? clock(s.endedAt) : 'مستمر' }} ({{ caMinutes(s.minutes) }})
              <template v-if="s.endedBy === 'auto'">
                · أُغلق تلقائياً
              </template>
            </VChip>
          </div>
          <div
            v-else-if="!dayLoading"
            class="text-medium-emphasis"
          >
            لم يبدأ الدوام في هذا اليوم.
          </div>
          <div
            v-if="selectedAlerts.length"
            class="mt-3"
          >
            <div class="text-subtitle-2 mb-1">
              تنبيهات البقاء في نفس المكان (البرتقالي على الخريطة)
            </div>
            <div
              v-for="a in selectedAlerts"
              :key="a.id"
              class="d-flex flex-wrap align-center gap-2 mb-1"
            >
              <VChip
                size="small"
                :color="a.isOpen ? 'error' : 'warning'"
                variant="tonal"
              >
                {{ clock(a.sinceAt) }} ← {{ a.isOpen ? 'ما زال هناك' : clock(a.lastSeenAt) }} ({{ caMinutes(a.minutes) }})
              </VChip>
              <span
                v-if="a.seenAt"
                class="text-caption text-medium-emphasis"
              >اطلع عليه {{ a.seenBy || '' }}</span>
              <VBtn
                v-else
                size="x-small"
                variant="text"
                @click="acknowledge(a)"
              >
                تم الاطلاع
              </VBtn>
            </div>
          </div>
        </div>
        <div
          v-else
          class="ca-att-detail text-medium-emphasis"
        >
          الخريطة تعرض آخر موقع لكل مسوّق في هذا اليوم (الأخضر في الدوام الآن). اختر مسوّقاً لعرض مساره الكامل وأوقات دوامه.
        </div>
      </div>
    </div>

    <VDialog
      v-model="historyOpen"
      max-width="640"
    >
      <VCard :title="`سجل دوام ${selected?.name || ''} — آخر 30 يوماً`">
        <VCardText>
          <VProgressLinear
            v-if="historyLoading"
            indeterminate
          />
          <VTable
            v-else-if="history.length"
            density="compact"
          >
            <thead>
              <tr>
                <th>اليوم</th>
                <th>البدء</th>
                <th>النهاية</th>
                <th>الفترات</th>
                <th>الساعات</th>
                <th />
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="h in history"
                :key="h.date"
              >
                <td>{{ caDate(h.date) }}</td>
                <td>{{ clock(h.firstStartedAt) }}</td>
                <td>{{ h.isWorking ? 'مستمر' : clock(h.lastEndedAt) }}</td>
                <td>{{ h.sessionsCount }}</td>
                <td class="font-weight-bold">
                  {{ caMinutes(h.totalMinutes) }}
                </td>
                <td>
                  <VBtn
                    size="x-small"
                    variant="text"
                    @click="openHistoryDay(h)"
                  >
                    المسار
                  </VBtn>
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr>
                <td colspan="4">
                  المجموع ({{ history.length }} يوم)
                </td>
                <td class="font-weight-bold">
                  {{ caMinutes(history.reduce((s, h) => s + (h.totalMinutes || 0), 0)) }}
                </td>
                <td />
              </tr>
            </tfoot>
          </VTable>
          <div
            v-else
            class="text-medium-emphasis"
          >
            لا يوجد دوام مسجل في آخر 30 يوماً.
          </div>
        </VCardText>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped lang="scss">
.ca-att-page {
  display: flex;
  flex-direction: column;
  min-block-size: calc(100dvh - 4.5rem);
}

.ca-att-toolbar {
  padding: 1rem 1.25rem 0.75rem;
}

.ca-att-totals {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 1.5rem;
  align-items: center;
  block-size: 100%;

  div {
    display: flex;
    flex-direction: column;
  }

  span {
    font-size: 0.75rem;
    opacity: 0.7;
  }

  b {
    font-size: 1.05rem;
  }
}

.ca-att-body {
  display: flex;
  flex: 1 1 auto;
  gap: 0;
  min-block-size: 0;

  @media (max-width: 960px) {
    flex-direction: column;
  }
}

.ca-att-list {
  flex: 0 0 360px;
  max-block-size: calc(100dvh - 13rem);
  padding: 0 1rem 1rem 1.25rem;
  overflow: auto;

  @media (max-width: 960px) {
    flex-basis: auto;
    max-block-size: 40dvh;
  }
}

.ca-att-row {
  cursor: pointer;
  transition: border-color 0.15s;

  &.is-selected {
    border-color: rgb(var(--v-theme-primary));
    background: rgba(var(--v-theme-primary), 0.06);
  }
}

.ca-att-times {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  font-size: 0.8rem;
  opacity: 0.8;
}

.ca-att-map-col {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-inline-size: 0;
}

.ca-att-map-wrap {
  position: relative;
  flex: 1 1 auto;
  min-block-size: 52dvh;
}

.ca-att-map {
  position: absolute;
  inset: 0;
  direction: ltr;
  /* html { zoom: 90% } in styles.scss desyncs Mapbox marker transforms on zoom */
  zoom: calc(10 / 9);

  :deep(.mapboxgl-marker) {
    inset: auto !important;
    right: auto !important;
  }

  :deep(.ca-att-dot) {
    border: 3px solid #fff;
    border-radius: 50%;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 35%);
    cursor: pointer;
  }

  :deep(.ca-att-dot.is-alert) {
    box-shadow: 0 0 0 4px rgba(249, 115, 22, 55%), 0 1px 4px rgba(0, 0, 0, 35%);
  }
}

.ca-att-map-progress {
  position: absolute;
  inset-block-start: 0;
  inset-inline: 0;
}

.ca-att-detail {
  padding: 0.75rem 1.25rem 1rem;
  background: rgb(var(--v-theme-surface));
  border-block-start: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>

<style lang="scss">
.layout-page-content.ca-att-bleed {
  inline-size: 100% !important;
  max-inline-size: none !important;
  margin-inline: 0 !important;
  padding-inline: 0 !important;
  padding-block: 0 !important;
}

.layout-page-content.ca-att-bleed .page-content-container {
  inline-size: 100% !important;
  max-inline-size: none !important;
  margin-inline: 0 !important;
  padding-inline: 0 !important;
}
</style>
