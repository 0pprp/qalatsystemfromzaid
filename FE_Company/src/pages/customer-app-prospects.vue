<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import mapboxgl from '@/utils/mapbox'
import 'mapbox-gl/dist/mapbox-gl.css'
import {
  caClock,
  caDate,
  caDeleteProspect,
  caDistanceM,
  caErrorMessage,
  caInterestMeta,
  caIraqToday,
  caListAgents,
  caMapLink,
  caMinutes,
  caPaged,
  caPinTimeline,
  caSeconds,
  caYmd,
} from '@/composables/customerAppApi'
import { MAPBOX_TOKEN } from '@/composables/mapboxToken'
import { useToast } from '@/composables/useToast'

/**
 * «المحلات المثبّتة»: one agent's pinned shops on one day, as a timeline of work periods.
 * A shop's time is the time since the previous pin; a pin under 3 minutes after the previous one is flagged; 15 minutes
 * without a pin closes a period. Work time = the sum of the periods (the agent never sees any of this).
 */
definePage({ meta: { layout: 'default' } })

const route = useRoute()
const router = useRouter()
const toast = useToast()
const token = MAPBOX_TOKEN

/** The agent's phone position is shown only when the pin was moved further than this from it. */
const SAME_PLACE_M = 25

const agents = ref([])
const userId = ref(route.query.userId ? Number(route.query.userId) : null)
const date = ref(/^\d{4}-\d{2}-\d{2}$/.test(String(route.query.date || '')) ? String(route.query.date) : caIraqToday())

const data = ref(null)
const loading = ref(false)
const error = ref('')
const selectedId = ref(null)
const confirmDelete = ref(null)
const deleting = ref(false)

const isToday = computed(() => date.value === caIraqToday())
const items = computed(() => data.value?.pins || [])
const selected = computed(() => items.value.find(i => i.pin.id === selectedId.value) || null)

/** The list grouped by period, oldest first. */
const groups = computed(() => (data.value?.periods || []).map(p => ({
  period: p,
  items: items.value.filter(i => i.periodIndex === p.index),
  breakBefore: items.value.find(i => i.periodIndex === p.index)?.breakSecondsBefore ?? null,
})))

const progress = computed(() => {
  const t = data.value?.targetMinutes || 0

  return t > 0 ? Math.min(100, Math.round(((data.value?.workMinutes || 0) / t) * 100)) : 0
})

/** Positions (0–100 %) on the day bar, from the first pin to the last. */
const bar = computed(() => {
  const d = data.value
  if (!d?.firstPinAt)
    return null
  const start = Date.parse(d.firstPinAt)
  const end = Math.max(Date.parse(d.lastPinAt), start + 10 * 60000)
  const pos = v => ((Date.parse(v) - start) / (end - start)) * 100

  return {
    periods: d.periods.map(p => ({ ...p, from: pos(p.startAt), width: Math.max(0.6, pos(p.endAt) - pos(p.startAt)) })),
    ticks: d.pins.map(i => ({ id: i.pin.id, at: pos(i.pin.createdAt), quick: i.quick })),
  }
})

function orderOf(id) {
  return items.value.findIndex(i => i.pin.id === id) + 1
}

/** Metres between where the agent stood and the pin (null when the app did not send its position). */
function movedMeters(p) {
  if (p?.agentLatitude == null || p?.agentLongitude == null)
    return null

  return caDistanceM(p.latitude, p.longitude, p.agentLatitude, p.agentLongitude)
}

function shiftDay(n) {
  const d = new Date(`${date.value}T12:00:00`)

  d.setDate(d.getDate() + n)
  date.value = caYmd(d)
}

let loadGen = 0

async function load({ quiet = false } = {}) {
  const gen = ++loadGen
  if (!userId.value) {
    data.value = null
    selectedId.value = null
    paint()

    return
  }
  if (!quiet)
    loading.value = true
  error.value = ''
  try {
    const res = await caPinTimeline(userId.value, date.value)
    if (gen !== loadGen)
      return
    const keep = quiet && selectedId.value && res.pins.some(i => i.pin.id === selectedId.value)

    data.value = res
    if (!keep)
      selectedId.value = null
    paint({ fit: !keep })
  }
  catch (err) {
    if (gen === loadGen) {
      data.value = null
      error.value = caErrorMessage(err, 'تعذر تحميل المحلات')
      paint()
    }
  }
  finally {
    if (gen === loadGen)
      loading.value = false
  }
}

watch([userId, date], ([u, d]) => {
  router.replace({ query: { ...route.query, userId: u || undefined, date: d } })
  load()
})

watch(selectedId, () => paint({ fit: !selectedId.value }))

function select(id) {
  selectedId.value = selectedId.value === id ? null : id
}

async function remove() {
  const row = confirmDelete.value
  if (!row)
    return
  deleting.value = true
  try {
    await caDeleteProspect(row.id)
    confirmDelete.value = null
    selectedId.value = null
    toast.success('تم حذف المحل')
    await load()
  }
  catch (err) {
    toast.error(caErrorMessage(err, 'تعذر الحذف'))
  }
  finally {
    deleting.value = false
  }
}

// ---------------------------------------------------------------- map
const mapEl = ref(null)
let map
let resizeObserver
let bleedEl
let refreshTimer
const markers = []

function clearMarkers() {
  for (const m of markers)
    m.remove()
  markers.length = 0
}

function setLink(coords) {
  const src = map?.getSource('agent-link')
  if (src)
    src.setData({ type: 'FeatureCollection', features: coords ? [{ type: 'Feature', geometry: { type: 'LineString', coordinates: coords } }] : [] })
}

function paint({ fit = false } = {}) {
  if (!map)
    return

  const draw = () => {
    clearMarkers()
    setLink(null)

    // A selected shop is shown alone, with the agent's own position when the pin was moved away from it.
    const focus = selected.value
    const shown = focus ? [focus] : items.value
    const bounds = new mapboxgl.LngLatBounds()

    for (const i of shown) {
      const p = i.pin
      const el = document.createElement('div')

      el.className = `ca-pin${i.quick ? ' is-quick' : ''}${focus ? ' is-focus' : ''}`
      el.style.background = caInterestMeta(p.interest).color
      el.textContent = String(orderOf(p.id))
      el.title = p.shopName
      el.addEventListener('click', e => {
        e.stopPropagation()
        select(p.id)
      })

      const ll = [Number(p.longitude), Number(p.latitude)]

      markers.push(new mapboxgl.Marker({ element: el }).setLngLat(ll).addTo(map))
      bounds.extend(ll)
    }

    if (focus) {
      const p = focus.pin
      const ll = [Number(p.longitude), Number(p.latitude)]
      const moved = movedMeters(p)

      if (moved != null && moved > SAME_PLACE_M) {
        const me = [Number(p.agentLongitude), Number(p.agentLatitude)]
        const el = document.createElement('div')

        el.className = 'ca-agent'
        el.innerHTML = '<div class="ca-agent-label">موقع المسوّق عند تثبيت هذا المحل</div><div class="ca-agent-dot"></div>'
        markers.push(new mapboxgl.Marker({ element: el, anchor: 'bottom' }).setLngLat(me).addTo(map))
        setLink([me, ll])
        map.fitBounds(new mapboxgl.LngLatBounds(ll, ll).extend(me), { padding: 120, maxZoom: 17, duration: 600 })
      }
      else {
        map.flyTo({ center: ll, zoom: Math.max(map.getZoom(), 17), duration: 600 })
      }
    }
    else if (fit && shown.length) {
      map.fitBounds(bounds, { padding: 60, maxZoom: 16, duration: 600 })
    }
  }

  if (map.isStyleLoaded() && map.getSource('agent-link'))
    draw()
  else
    map.once('idle', draw)
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
  map.on('load', () => {
    map.addSource('agent-link', { type: 'geojson', data: { type: 'FeatureCollection', features: [] } })
    map.addLayer({
      id: 'agent-link',
      type: 'line',
      source: 'agent-link',
      paint: { 'line-color': '#2563eb', 'line-width': 2, 'line-dasharray': [2, 2] },
    })
  })
  resizeObserver = new ResizeObserver(() => map?.resize())
  resizeObserver.observe(mapEl.value)
}

onMounted(async () => {
  bleedEl = document.querySelector('.layout-page-content')
  bleedEl?.classList.add('ca-att-bleed')
  await ensureMap()
  try {
    agents.value = caPaged(await caListAgents({ pageSize: 100 })).items
  }
  catch {
    agents.value = []
  }
  await load()

  // Today keeps updating while the page is open.
  refreshTimer = setInterval(() => {
    if (isToday.value && userId.value && !document.hidden)
      load({ quiet: true })
  }, 60000)
})

onUnmounted(() => {
  clearInterval(refreshTimer)
  bleedEl?.classList.remove('ca-att-bleed')
  resizeObserver?.disconnect()
  clearMarkers()
  map?.remove()
  map = null
})
</script>

<template>
  <div class="ca-pp">
    <!-- Toolbar -->
    <div class="ca-pp-toolbar">
      <h4 class="text-h4 d-flex align-center gap-2 mb-0 me-auto">
        <VIcon
          icon="tabler-map-pin-plus"
          color="primary"
        />
        المحلات المثبّتة
      </h4>
      <VAutocomplete
        v-model="userId"
        :items="agents"
        :item-title="a => a.fullName || a.username"
        item-value="id"
        placeholder="اختر المسوّق"
        prepend-inner-icon="tabler-user"
        density="compact"
        hide-details
        clearable
        style="min-inline-size: 240px; max-inline-size: 320px;"
      />
      <div class="d-flex align-center">
        <IconBtn @click="shiftDay(-1)">
          <VIcon icon="tabler-chevron-right" />
        </IconBtn>
        <VTextField
          v-model="date"
          type="date"
          density="compact"
          hide-details
          style="min-inline-size: 160px;"
        />
        <IconBtn
          :disabled="isToday"
          @click="shiftDay(1)"
        >
          <VIcon icon="tabler-chevron-left" />
        </IconBtn>
      </div>
      <IconBtn
        :disabled="!userId"
        title="تحديث"
        @click="load"
      >
        <VIcon icon="tabler-refresh" />
      </IconBtn>
    </div>

    <VAlert
      v-if="error"
      type="error"
      variant="tonal"
      class="mx-5 mb-3"
    >
      {{ error }}
    </VAlert>
    <div
      v-if="!token"
      class="mx-5 mb-2 text-error"
    >
      مفتاح الخريطة غير مهيأ. أضف VITE_MAPBOX_TOKEN.
    </div>

    <!-- Summary + day bar -->
    <div
      v-if="data && items.length"
      class="ca-pp-summary"
    >
      <div class="ca-pp-stats">
        <div class="ca-pp-stat is-main">
          <span>وقت العمل</span>
          <b>{{ caMinutes(data.workMinutes) }}</b>
          <small v-if="data.targetMinutes > 0">من هدف {{ caMinutes(data.targetMinutes) }}</small>
        </div>
        <div class="ca-pp-stat">
          <span>المحلات</span><b>{{ items.length }}</b>
        </div>
        <div class="ca-pp-stat">
          <span>الفترات</span><b>{{ data.periods.length }}</b>
        </div>
        <div class="ca-pp-stat">
          <span>أول محل</span><b>{{ caClock(data.firstPinAt) }}</b>
        </div>
        <div
          class="ca-pp-stat"
          :class="{ 'is-warn': data.quickPins > 0 }"
        >
          <span>أقل من {{ data.quickGapMinutes }} د</span><b>{{ data.quickPins }}</b>
        </div>
      </div>
      <VProgressLinear
        v-if="data.targetMinutes > 0"
        :model-value="progress"
        :color="data.met ? 'success' : 'primary'"
        height="6"
        rounded
        class="mt-3"
      />

      <div
        v-if="bar"
        class="ca-pp-bar-wrap"
      >
        <div class="ca-pp-bar">
          <div
            v-for="p in bar.periods"
            :key="p.index"
            class="ca-pp-bar-period"
            :class="{ 'is-open': p.open }"
            :style="{ insetInlineStart: `${p.from}%`, inlineSize: `${p.width}%` }"
            :title="`الفترة ${p.index + 1}: ${caClock(p.startAt)} – ${caClock(p.endAt)} (${caSeconds(p.seconds)})`"
          />
          <div
            v-for="t in bar.ticks"
            :key="t.id"
            class="ca-pp-bar-tick"
            :class="{ 'is-quick': t.quick, 'is-selected': t.id === selectedId }"
            :style="{ insetInlineStart: `${t.at}%` }"
            @click="select(t.id)"
          />
        </div>
        <div class="ca-pp-bar-labels">
          <span
            v-for="p in bar.periods"
            :key="p.index"
            :style="{ insetInlineStart: `${p.from}%` }"
          >{{ caClock(p.startAt) }}</span>
        </div>
      </div>
    </div>

    <!-- Body -->
    <div class="ca-pp-body">
      <div class="ca-pp-list">
        <VProgressLinear
          v-if="loading"
          indeterminate
          color="primary"
          class="mb-2"
        />
        <div
          v-if="!userId"
          class="ca-pp-empty"
        >
          <VIcon
            icon="tabler-user-search"
            size="40"
          />
          <div>اختر مسوّقاً لعرض محلاته المثبّتة</div>
        </div>
        <div
          v-else-if="data && !items.length"
          class="ca-pp-empty"
        >
          <VIcon
            icon="tabler-map-pin-off"
            size="40"
          />
          <div>لم يثبّت أي محل في {{ caDate(date) }}</div>
        </div>

        <template
          v-for="g in groups"
          :key="g.period.index"
        >
          <div
            v-if="g.breakBefore != null"
            class="ca-pp-break"
          >
            <span>انقطاع {{ caSeconds(g.breakBefore) }} — لا يُحتسب</span>
          </div>
          <div class="ca-pp-period">
            <div class="ca-pp-period-head">
              <b>الفترة {{ g.period.index + 1 }}</b>
              <span dir="ltr">{{ caClock(g.period.startAt) }} – {{ caClock(g.period.endAt) }}</span>
              <VSpacer />
              <VChip
                v-if="g.period.open"
                size="x-small"
                color="success"
                variant="tonal"
              >
                جارية
              </VChip>
              <b>{{ caSeconds(g.period.seconds) }}</b>
            </div>
            <div
              v-for="i in g.items"
              :key="i.pin.id"
              class="ca-pp-row"
              :class="{ 'is-quick': i.quick, 'is-selected': i.pin.id === selectedId }"
              @click="select(i.pin.id)"
            >
              <span
                class="ca-pp-num"
                :style="{ background: caInterestMeta(i.pin.interest).color }"
              >{{ orderOf(i.pin.id) }}</span>
              <span class="ca-pp-time">{{ caClock(i.pin.createdAt) }}</span>
              <span class="ca-pp-name">{{ i.pin.shopName }}</span>
              <span
                v-if="i.startsPeriod"
                class="ca-pp-spent text-medium-emphasis"
              >بداية الفترة</span>
              <span
                v-else
                class="ca-pp-spent"
                :title="i.quick ? `أقل من ${data.quickGapMinutes} دقائق بعد المحل السابق` : 'الوقت منذ المحل السابق'"
              >
                <VIcon
                  v-if="i.quick"
                  icon="tabler-alert-triangle"
                  size="14"
                />
                {{ caSeconds(i.secondsOnShop) }}
              </span>
            </div>
          </div>
        </template>
      </div>

      <div class="ca-pp-map-col">
        <div class="ca-pp-map-wrap">
          <div
            ref="mapEl"
            class="ca-pp-map"
          />
          <VBtn
            v-if="selected"
            class="ca-pp-map-back"
            size="small"
            prepend-icon="tabler-arrow-back-up"
            @click="selectedId = null"
          >
            كل المحلات
          </VBtn>
        </div>
        <div
          v-if="selected"
          class="ca-pp-detail"
        >
          <div class="d-flex flex-wrap align-center gap-2 mb-1">
            <span
              class="ca-pp-num"
              :style="{ background: caInterestMeta(selected.pin.interest).color }"
            >{{ orderOf(selected.pin.id) }}</span>
            <div class="text-h6">
              {{ selected.pin.shopName }}
            </div>
            <VChip
              size="small"
              variant="tonal"
              :style="{ color: caInterestMeta(selected.pin.interest).color }"
            >
              {{ caInterestMeta(selected.pin.interest).title }}
            </VChip>
            <VSpacer />
            <IconBtn
              :href="caMapLink(selected.pin.latitude, selected.pin.longitude)"
              target="_blank"
              rel="noopener noreferrer"
              title="خرائط Google"
            >
              <VIcon icon="tabler-brand-google-maps" />
            </IconBtn>
            <IconBtn
              color="error"
              title="حذف"
              @click="confirmDelete = selected.pin"
            >
              <VIcon icon="tabler-trash" />
            </IconBtn>
          </div>
          <div class="text-body-2 text-medium-emphasis">
            ثُبّت الساعة {{ caClock(selected.pin.createdAt) }}
            · {{ selected.startsPeriod ? 'بداية الفترة' : `${caSeconds(selected.secondsOnShop)} منذ المحل السابق` }}
            <span
              v-if="selected.quick"
              class="text-warning"
            > (أقل من {{ data.quickGapMinutes }} دقائق)</span>
            <template v-if="movedMeters(selected.pin) != null && movedMeters(selected.pin) > SAME_PLACE_M">
              · يبعد {{ Math.round(movedMeters(selected.pin)) }} م عن موقع المسوّق
            </template>
          </div>
        </div>
      </div>
    </div>

    <VDialog
      :model-value="!!confirmDelete"
      max-width="420"
      @update:model-value="v => { if (!v) confirmDelete = null }"
    >
      <VCard title="حذف المحل">
        <VCardText>
          سيختفي «{{ confirmDelete?.shopName }}» من خريطة كل المسوّقين ومن هذه الصفحة.
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="confirmDelete = null"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="error"
            :loading="deleting"
            @click="remove"
          >
            حذف
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped lang="scss">
.ca-pp {
  display: flex;
  flex-direction: column;
  min-block-size: calc(100dvh - 4.5rem);
}

.ca-pp-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem 1.25rem 0.75rem;
}

.ca-pp-summary {
  padding: 0 1.25rem 0.75rem;
}

.ca-pp-stats {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 2rem;
}

.ca-pp-stat {
  display: flex;
  flex-direction: column;

  span,
  small {
    font-size: 0.75rem;
    opacity: 0.7;
  }

  b {
    font-size: 1.15rem;
  }

  &.is-main b {
    color: rgb(var(--v-theme-primary));
    font-size: 1.5rem;
  }

  &.is-warn b {
    color: rgb(var(--v-theme-warning));
  }
}

.ca-pp-bar-wrap {
  margin-block-start: 0.75rem;
}

.ca-pp-bar {
  position: relative;
  border-radius: 6px;
  background: rgba(var(--v-theme-on-surface), 0.06);
  block-size: 14px;
}

.ca-pp-bar-period {
  position: absolute;
  border-radius: 6px;
  background: rgba(var(--v-theme-primary), 0.35);
  inset-block: 0;

  &.is-open {
    background: rgba(var(--v-theme-success), 0.4);
  }
}

.ca-pp-bar-tick {
  position: absolute;
  border-radius: 2px;
  background: rgb(var(--v-theme-primary));
  block-size: 22px;
  cursor: pointer;
  inline-size: 3px;
  inset-block-start: -4px;
  transform: translateX(50%);

  &.is-quick {
    background: rgb(var(--v-theme-warning));
  }

  &.is-selected {
    background: rgb(var(--v-theme-on-surface));
    inline-size: 5px;
  }
}

.ca-pp-bar-labels {
  position: relative;
  block-size: 1.1rem;
  font-size: 0.7rem;
  margin-block-start: 6px;
  opacity: 0.7;

  span {
    position: absolute;
    transform: translateX(50%);
  }
}

.ca-pp-body {
  display: flex;
  flex: 1 1 auto;
  border-block-start: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  min-block-size: 0;

  @media (max-width: 960px) {
    flex-direction: column;
  }
}

.ca-pp-list {
  flex: 0 0 380px;
  max-block-size: calc(100dvh - 16rem);
  padding: 0.75rem 1rem 1rem 1.25rem;
  overflow: auto;

  @media (max-width: 960px) {
    flex-basis: auto;
    max-block-size: 45dvh;
  }
}

.ca-pp-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  opacity: 0.6;
  padding-block: 3rem;
  text-align: center;
}

.ca-pp-period + .ca-pp-period {
  margin-block-start: 0.5rem;
}

.ca-pp-period-head {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  padding-block: 0.25rem 0.4rem;
}

.ca-pp-row {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  border-radius: 8px;
  cursor: pointer;
  padding-block: 0.4rem;
  padding-inline: 0.5rem;

  &:hover {
    background: rgba(var(--v-theme-on-surface), 0.04);
  }

  &.is-quick {
    background: rgba(var(--v-theme-warning), 0.12);
  }

  &.is-selected {
    outline: 2px solid rgb(var(--v-theme-primary));
  }
}

.ca-pp-num {
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  block-size: 22px;
  color: #fff;
  font-size: 0.7rem;
  font-weight: 700;
  inline-size: 22px;
}

.ca-pp-time {
  font-size: 0.8rem;
  font-variant-numeric: tabular-nums;
  opacity: 0.7;
}

.ca-pp-name {
  overflow: hidden;
  flex: 1 1 auto;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ca-pp-spent {
  display: inline-flex;
  align-items: center;
  font-size: 0.8rem;
  font-weight: 600;
  gap: 2px;
  white-space: nowrap;
}

.ca-pp-row.is-quick .ca-pp-spent {
  color: rgb(var(--v-theme-warning));
}

.ca-pp-break {
  display: flex;
  align-items: center;
  font-size: 0.75rem;
  gap: 0.5rem;
  margin-block: 0.5rem;
  opacity: 0.65;

  &::before,
  &::after {
    flex: 1;
    border-block-start: 1px dashed currentcolor;
    content: "";
  }
}

.ca-pp-map-col {
  display: flex;
  flex: 1 1 auto;
  flex-direction: column;
  min-inline-size: 0;
}

.ca-pp-map-wrap {
  position: relative;
  flex: 1 1 auto;
  min-block-size: 52dvh;
}

.ca-pp-map-back {
  position: absolute;
  z-index: 2;
  inset-block-start: 12px;
  inset-inline-start: 12px;
}

.ca-pp-map {
  position: absolute;
  inset: 0;
  direction: ltr;
  /* html { zoom: 90% } in styles.scss desyncs Mapbox marker transforms on zoom */
  zoom: calc(10 / 9);

  :deep(.mapboxgl-marker) {
    inset: auto !important;
    right: auto !important;
  }

  :deep(.ca-pin) {
    display: flex;
    align-items: center;
    justify-content: center;
    border: 2px solid #fff;
    border-radius: 50%;
    block-size: 24px;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 40%);
    color: #fff;
    cursor: pointer;
    font: 700 11px/1 sans-serif;
    inline-size: 24px;
  }

  :deep(.ca-pin.is-quick) {
    box-shadow: 0 0 0 3px #f59e0b, 0 1px 4px rgba(0, 0, 0, 40%);
  }

  :deep(.ca-pin.is-focus) {
    block-size: 32px;
    font-size: 13px;
    inline-size: 32px;
  }

  :deep(.ca-agent) {
    display: flex;
    flex-direction: column;
    align-items: center;
    pointer-events: none;
  }

  :deep(.ca-agent-label) {
    position: relative;
    padding: 4px 8px;
    border-radius: 6px;
    background: #fff;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 30%);
    color: #1e3a8a;
    direction: rtl;
    font: 600 12px/1.4 sans-serif;
    margin-block-end: 8px;
    white-space: nowrap;
  }

  :deep(.ca-agent-label::after) {
    position: absolute;
    border: 6px solid transparent;
    border-top-color: #fff;
    content: "";
    left: 50%;
    top: 100%;
    transform: translateX(-50%);
  }

  :deep(.ca-agent-dot) {
    border: 3px solid #fff;
    border-radius: 50%;
    background: #2563eb;
    block-size: 18px;
    box-shadow: 0 0 0 6px rgba(37, 99, 235, 25%);
    inline-size: 18px;
  }
}

.ca-pp-detail {
  padding: 0.75rem 1.25rem 1rem;
  background: rgb(var(--v-theme-surface));
  border-block-start: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
