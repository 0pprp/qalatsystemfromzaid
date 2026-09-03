<script setup>
import { nextTick, onMounted, onUnmounted, ref } from 'vue'
import mapboxgl from 'mapbox-gl'
import 'mapbox-gl/dist/mapbox-gl.css'
import { smGet } from '@/composables/salesManagerApi'
import { MAPBOX_TOKEN } from '@/composables/mapboxToken'
import {
  dropBeforePermission,
  lineCollection,
  matchTrackToRoads,
  minuteIndex,
  normalizePoints,
  pointsCollection,
  segmentByTravel,
  thinByMeters,
} from '@/composables/gpsTrack'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const employees = ref([])
const employeeId = ref(null)
const date = ref(new Date().toISOString().slice(0, 10))
const route = ref(null)
const minutePoints = ref([])
const selectedIndex = ref(-1)
const loading = ref(false)
const token = MAPBOX_TOKEN
const mapEl = ref(null)
let map
let resizeObserver

onMounted(async () => {
  try {
    employees.value = await smGet('employees')
  }
  catch {
    toast.error('تعذر تحميل الموظفين')
  }
  await ensureMap()
})

onUnmounted(() => {
  resizeObserver?.disconnect()
  window.removeEventListener('resize', resizeMap)
  map?.remove()
  map = null
})

function resizeMap() {
  map?.resize()
}

function whenMapReady(cb) {
  if (!map)
    return
  if (map.isStyleLoaded())
    cb()
  else
    map.once('load', cb)
}

function fitToLines(lines) {
  const bounds = new mapboxgl.LngLatBounds()
  let any = false
  for (const line of lines) {
    for (const coord of line) {
      bounds.extend(coord)
      any = true
    }
  }
  if (any)
    map.fitBounds(bounds, { padding: 48, maxZoom: 16, duration: 600 })
}

function paintRoute(lines, points) {
  if (!map)
    return

  const routeData = lineCollection(lines)
  const stopsData = pointsCollection(points)

  if (map.getSource('route'))
    map.getSource('route').setData(routeData)
  else {
    map.addSource('route', { type: 'geojson', data: routeData })
    map.addLayer({
      id: 'route-line',
      type: 'line',
      source: 'route',
      layout: { 'line-join': 'round', 'line-cap': 'round' },
      paint: { 'line-color': '#16a34a', 'line-width': 6, 'line-opacity': 0.95 },
    })
  }

  if (map.getSource('stops'))
    map.getSource('stops').setData(stopsData)
  else {
    map.addSource('stops', { type: 'geojson', data: stopsData })
    map.addLayer({
      id: 'stops-circle',
      type: 'circle',
      source: 'stops',
      paint: {
        'circle-radius': 5,
        'circle-color': '#16a34a',
        'circle-stroke-width': 2,
        'circle-stroke-color': '#ffffff',
        'circle-pitch-alignment': 'map',
      },
    })
    map.on('click', 'stops-circle', e => {
      const id = e.features?.[0]?.properties?.id
      if (id == null)
        return
      focusPoint(Number(id), false)
    })
    map.on('mouseenter', 'stops-circle', () => { map.getCanvas().style.cursor = 'pointer' })
    map.on('mouseleave', 'stops-circle', () => { map.getCanvas().style.cursor = '' })
  }

  ensureHighlightLayer()
  clearHighlight()

  if (lines.length)
    fitToLines(lines)
  requestAnimationFrame(() => resizeMap())
}

function highlightCollection(point) {
  if (!point)
    return { type: 'FeatureCollection', features: [] }

  return {
    type: 'FeatureCollection',
    features: [{
      type: 'Feature',
      geometry: { type: 'Point', coordinates: [point.lng, point.lat] },
    }],
  }
}

function ensureHighlightLayer() {
  if (!map || map.getSource('highlight'))
    return

  map.addSource('highlight', { type: 'geojson', data: highlightCollection(null) })
  map.addLayer({
    id: 'highlight-halo',
    type: 'circle',
    source: 'highlight',
    paint: {
      'circle-radius': 14,
      'circle-color': '#16a34a',
      'circle-opacity': 0.28,
      'circle-pitch-alignment': 'map',
    },
  })
  map.addLayer({
    id: 'highlight-dot',
    type: 'circle',
    source: 'highlight',
    paint: {
      'circle-radius': 7,
      'circle-color': '#16a34a',
      'circle-stroke-width': 2,
      'circle-stroke-color': '#ffffff',
      'circle-pitch-alignment': 'map',
    },
  })
}

function clearHighlight() {
  map?.getSource('highlight')?.setData(highlightCollection(null))
}

function focusPoint(index, fly = true) {
  const point = minutePoints.value[index]
  if (!point || !map)
    return
  selectedIndex.value = index
  ensureHighlightLayer()
  map.getSource('highlight')?.setData(highlightCollection(point))
  if (fly)
    map.flyTo({ center: [point.lng, point.lat], zoom: 16, duration: 700 })
}

async function ensureMap() {
  await nextTick()
  if (!token || !mapEl.value)
    return false
  if (map) {
    resizeMap()

    return true
  }

  mapboxgl.accessToken = token
  mapEl.value.setAttribute('dir', 'ltr')
  map = new mapboxgl.Map({
    container: mapEl.value,
    style: 'mapbox://styles/mapbox/streets-v12',
    center: [44.33, 32.02],
    zoom: 12,
    attributionControl: true,
  })
  map.getContainer().setAttribute('dir', 'ltr')
  map.addControl(new mapboxgl.NavigationControl({ showCompass: false }), 'bottom-left')
  resizeObserver = new ResizeObserver(() => resizeMap())
  resizeObserver.observe(mapEl.value)
  window.addEventListener('resize', resizeMap)
  map.on('load', () => resizeMap())
  requestAnimationFrame(() => resizeMap())

  return true
}

async function loadRoute() {
  if (!employeeId.value) {
    toast.warning('اختر الموظف أولاً')

    return
  }
  loading.value = true
  selectedIndex.value = -1
  clearHighlight()
  try {
    route.value = await smGet(`employees/${employeeId.value}/route?date=${date.value}`)
    const events = await smGet(`employees/${employeeId.value}/tracking-events?date=${date.value}`)
    const raw = route.value?.points || []
    let usable = normalizePoints(dropBeforePermission(raw, events))
    if (!usable.length)
      usable = normalizePoints(raw)
    usable = thinByMeters(usable, 5)
    const times = minuteIndex(usable)
    minutePoints.value = times
    const segments = segmentByTravel(usable)
    const lines = await matchTrackToRoads(segments, token)

    if (!await ensureMap()) {
      toast.error('مفتاح الخريطة غير مهيأ')

      return
    }
    whenMapReady(() => {
      paintRoute(lines, minutePoints.value)
      if (!usable.length)
        toast.info('لا توجد نقاط مسار لهذا التاريخ')
    })
  }
  catch (err) {
    console.error(err)
    toast.error('تعذر تحميل المسار')
  }
  finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="sales-route-page">
    <div class="sales-route-toolbar">
      <h4 class="mb-3">
        المسارات
      </h4>
      <VRow dense>
        <VCol
          cols="12"
          md="4"
        >
          <VSelect
            v-model="employeeId"
            :items="employees"
            item-title="employeeName"
            item-value="employeeId"
            label="الموظف"
            hide-details
          />
        </VCol>
        <VCol
          cols="12"
          md="4"
        >
          <VTextField
            v-model="date"
            type="date"
            label="التاريخ"
            hide-details
          />
        </VCol>
        <VCol
          cols="12"
          md="4"
          class="d-flex align-center"
        >
          <VBtn
            color="primary"
            :loading="loading"
            block
            @click="loadRoute"
          >
            عرض المسار
          </VBtn>
        </VCol>
      </VRow>
      <div
        v-if="!token"
        class="mt-2 text-error"
      >
        مفتاح الخريطة غير مهيأ. أضف VITE_MAPBOX_TOKEN.
      </div>
    </div>

    <div class="sales-route-map-wrap">
      <div
        ref="mapEl"
        class="sales-route-map"
      />
    </div>

    <div
      v-if="minutePoints.length"
      class="sales-route-times"
    >
      <div class="sales-route-times-title">
        أحداث التتبع
      </div>
      <div class="sales-route-times-list">
        <button
          v-for="(point, index) in minutePoints"
          :key="point.t"
          type="button"
          class="sales-route-time"
          :class="{ 'is-active': selectedIndex === index }"
          @click="focusPoint(index)"
        >
          {{ point.timeLabel }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
.sales-route-page {
  display: flex;
  flex-direction: column;
  min-block-size: calc(100dvh - 4.5rem);
  margin-block: -1.5rem;
  margin-inline: -1.5rem;
}

.sales-route-toolbar {
  flex: 0 0 auto;
  padding: 1rem 1.25rem 0.75rem;
}

.sales-route-map-wrap {
  position: relative;
  flex: 1 1 auto;
  min-block-size: 58dvh;
  inline-size: 100%;
}

.sales-route-map {
  position: absolute;
  inset: 0;
  direction: ltr;
}

.sales-route-times {
  flex: 0 0 auto;
  max-block-size: 22dvh;
  padding: 0.75rem 1.25rem 1.1rem;
  overflow: auto;
  background: rgb(var(--v-theme-surface));
  border-block-start: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.sales-route-times-title {
  margin-block-end: 0.5rem;
  font-size: 0.875rem;
  font-weight: 600;
}

.sales-route-times-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.sales-route-time {
  padding: 0.3rem 0.65rem;
  border: 1px solid rgba(22, 163, 74, 35%);
  border-radius: 999px;
  background: transparent;
  color: inherit;
  cursor: pointer;
  font-size: 0.8125rem;
  font-variant-numeric: tabular-nums;
}

.sales-route-time.is-active {
  border-color: #16a34a;
  background: #16a34a;
  color: #fff;
}
</style>
