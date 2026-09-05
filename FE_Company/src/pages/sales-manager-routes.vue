<script setup>
import { nextTick, onMounted, onUnmounted, ref } from 'vue'
import mapboxgl from 'mapbox-gl'
import 'mapbox-gl/dist/mapbox-gl.css'
import { smGet, employeeApiPath, smGetEmployees } from '@/composables/salesManagerApi'
import { MAPBOX_TOKEN } from '@/composables/mapboxToken'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import {
  formatIraqClock,
  normalizePoints,
  officialTenMinutePoints,
} from '@/composables/gpsTrack'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const employees = ref([])
const cityValue = ref('')
const employeeId = ref(null)
const date = ref(new Date().toISOString().slice(0, 10))
const points = ref([])
const selectedIndex = ref(-1)
const loading = ref(false)
const emptyMessage = ref('')
const token = MAPBOX_TOKEN
const mapEl = ref(null)
let map
let resizeObserver
let bleedEl
const markers = []

onMounted(async () => {
  bleedEl = document.querySelector('.layout-page-content')
  bleedEl?.classList.add('sales-route-bleed')
  await ensureMap()
})

async function loadEmployees() {
  employeeId.value = null
  if (!cityValue.value) {
    employees.value = []

    return
  }
  try {
    employees.value = await smGetEmployees(cityValue.value)
  }
  catch {
    toast.error('تعذر تحميل الموظفين')
  }
}

onUnmounted(() => {
  bleedEl?.classList.remove('sales-route-bleed')
  resizeObserver?.disconnect()
  window.removeEventListener('resize', resizeMap)
  clearMarkers()
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

function fitToPins(list) {
  if (!list.length)
    return
  if (list.length === 1) {
    map.flyTo({ center: [list[0].lng, list[0].lat], zoom: 15, duration: 500 })

    return
  }
  const bounds = new mapboxgl.LngLatBounds()
  for (const point of list)
    bounds.extend([point.lng, point.lat])
  map.fitBounds(bounds, { padding: 48, maxZoom: 16, duration: 600 })
}

function popupHtml(point) {
  const acc = Number.isFinite(point.acc)
    ? `الدقة: ${Math.round(point.acc)} متر`
    : ''

  return `<div dir="rtl" class="sales-route-popup">
    <div>${point.timeLabel || ''}</div>
    ${acc ? `<div>${acc}</div>` : ''}
  </div>`
}

function clearMarkers() {
  for (const marker of markers)
    marker.remove()
  markers.length = 0
}

function closePopups() {
  for (const marker of markers)
    marker.getPopup()?.remove()
}

function paintPoints(list) {
  if (!map)
    return

  if (map.getLayer('route-line'))
    map.removeLayer('route-line')
  if (map.getSource('route'))
    map.removeSource('route')
  if (map.getLayer('stops-circle'))
    map.removeLayer('stops-circle')
  if (map.getSource('stops'))
    map.removeSource('stops')

  clearMarkers()
  for (const [index, point] of list.entries()) {
    const marker = new mapboxgl.Marker({ color: '#16a34a', anchor: 'bottom' })
      .setLngLat([point.lng, point.lat])
      .setPopup(new mapboxgl.Popup({ offset: 18, closeButton: false }).setHTML(popupHtml(point)))
      .addTo(map)
    marker.getElement().addEventListener('click', () => {
      selectedIndex.value = index
    })
    markers.push(marker)
  }

  if (list.length)
    fitToPins(list)
  requestAnimationFrame(() => {
    resizeMap()
    if (list.length)
      fitToPins(list)
  })
}

function focusPoint(index, fly = true) {
  const point = points.value[index]
  if (!point || !map)
    return
  selectedIndex.value = index
  closePopups()
  markers[index]?.togglePopup()
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

function extractPoints(payload) {
  const body = payload?.data && typeof payload.data === 'object' && !Array.isArray(payload.data)
    ? payload.data
    : payload
  if (Array.isArray(body?.points))
    return body.points
  if (Array.isArray(body?.Points))
    return body.Points

  return []
}

function isHttpError(err) {
  return !!(err?.response || err?.request || err?.isAxiosError)
}

async function loadLocations() {
  if (!cityValue.value) {
    toast.warning('اختر المحافظة أولاً')

    return
  }
  if (!employeeId.value) {
    toast.warning('اختر الموظف أولاً')

    return
  }
  loading.value = true
  selectedIndex.value = -1
  emptyMessage.value = ''
  try {
    const payload = await smGet(`${employeeApiPath(cityValue.value, employeeId.value, `/route?date=${date.value}`)}`)
    const raw = extractPoints(payload)
    const usable = officialTenMinutePoints(normalizePoints(raw))

    points.value = usable.map(point => ({
      ...point,
      timeLabel: formatIraqClock(point.t),
    }))

    if (!points.value.length)
      emptyMessage.value = 'لا توجد نقاط موقع مسجلة لهذا اليوم'

    if (!await ensureMap()) {
      toast.error('مفتاح الخريطة غير مهيأ')

      return
    }
    whenMapReady(() => {
      paintPoints(points.value)
      requestAnimationFrame(() => {
        resizeMap()
        if (points.value.length)
          fitToPins(points.value)
      })
    })
  }
  catch (err) {
    console.error(err)
    points.value = []
    emptyMessage.value = ''
    if (isHttpError(err))
      toast.error('تعذر تحميل نقاط الموقع')
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
        سجل المواقع
      </h4>
      <VRow dense>
        <VCol
          cols="12"
          md="4"
        >
          <SalesBranchFilter
            v-model="cityValue"
            @change="loadEmployees"
          />
        </VCol>
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
            :disabled="!cityValue"
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
            @click="loadLocations"
          >
            عرض المواقع
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

    <div class="sales-route-times">
      <div
        v-if="emptyMessage"
        class="text-medium-emphasis"
      >
        {{ emptyMessage }}
      </div>
      <template v-else-if="points.length">
        <div class="sales-route-times-title">
          سجل المواقع
        </div>
        <div class="sales-route-times-list">
          <button
            v-for="(point, index) in points"
            :key="`${point.t}-${index}`"
            type="button"
            class="sales-route-time"
            :class="{ 'is-active': selectedIndex === index }"
            @click="focusPoint(index)"
          >
            {{ point.timeLabel }}
          </button>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped lang="scss">
.sales-route-page {
  display: flex;
  flex-direction: column;
  inline-size: 100%;
  max-inline-size: none;
  min-block-size: calc(100dvh - 4.5rem);
  margin: 0;
  padding: 0;
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
  max-inline-size: none;
  margin: 0;
  padding: 0;
}

.sales-route-map {
  position: absolute;
  inset: 0;
  inline-size: 100%;
  block-size: 100%;
  margin: 0;
  padding: 0;
  direction: ltr;
  /* html { zoom: 90% } in styles.scss desyncs Mapbox marker transforms on zoom */
  zoom: calc(10 / 9);

  :deep(.mapboxgl-map),
  :deep(.mapboxgl-canvas-container),
  :deep(.mapboxgl-canvas) {
    inline-size: 100% !important;
    block-size: 100% !important;
  }

  :deep(.mapboxgl-marker) {
    inset: auto !important;
    right: auto !important;
  }
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

<style lang="scss">
.layout-page-content.sales-route-bleed {
  inline-size: 100% !important;
  max-inline-size: none !important;
  margin-inline: 0 !important;
  padding-inline: 0 !important;
  padding-block: 0 !important;
}

.layout-page-content.sales-route-bleed .page-content-container {
  inline-size: 100% !important;
  max-inline-size: none !important;
  margin-inline: 0 !important;
  padding-inline: 0 !important;
}

.sales-route-popup {
  font-size: 0.875rem;
  line-height: 1.5;
}
</style>
