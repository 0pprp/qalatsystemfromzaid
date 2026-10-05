<script setup>
import { nextTick, onMounted, onUnmounted, ref } from 'vue'
import mapboxgl from 'mapbox-gl'
import 'mapbox-gl/dist/mapbox-gl.css'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { branchRowKey, locationStatusLabel, smGetFollowerLiveLocations } from '@/composables/salesManagerApi'
import { formatIraqTime } from '@/composables/gpsTrack'
import { MAPBOX_TOKEN } from '@/composables/mapboxToken'

const token = MAPBOX_TOKEN
const followers = ref([])
const cityValue = ref('')
const selected = ref(null)
const mapEl = ref(null)
let map
const markers = new Map()
const markerStatus = new Map()
let poll

function markerKey(row) {
  return branchRowKey(row)
}

function statusColor(status) {
  if (status === 'Live')
    return '#16a34a'
  if (status === 'Stale')
    return '#ea580c'

  return '#64748b'
}

function formatAccuracy(value) {
  const n = Number(value)
  if (!Number.isFinite(n))
    return '—'

  return `${Math.round(n)} م`
}

function formatLastUpdate(value) {
  return formatIraqTime(value) || '—'
}

function popupHtml(row) {
  const status = locationStatusLabel[row.locationStatus] || row.locationStatus || '—'

  return `<div dir="rtl">
    <b>${row.employeeName || ''}</b><br>
    الفرع: ${row.branchName || row.cityName || ''}<br>
    آخر تحديث: ${formatLastUpdate(row.lastLocationAt)}<br>
    الدقة: ${formatAccuracy(row.lastAccuracy)}<br>
    الحالة: ${status}
  </div>`
}

function upsertMarker(row) {
  if (!map || row.lastLatitude == null || row.lastLongitude == null)
    return
  const key = markerKey(row)
  const lngLat = [row.lastLongitude, row.lastLatitude]
  const color = statusColor(row.locationStatus)
  const existing = markers.get(key)
  if (existing && markerStatus.get(key) === row.locationStatus) {
    existing.setLngLat(lngLat).getPopup()?.setHTML(popupHtml(row))

    return
  }

  existing?.remove()
  const marker = new mapboxgl.Marker({ color, anchor: 'bottom' })
    .setLngLat(lngLat)
    .setPopup(new mapboxgl.Popup().setHTML(popupHtml(row)))
    .addTo(map)
  marker.getElement().addEventListener('click', () => { selected.value = row })
  markers.set(key, marker)
  markerStatus.set(key, row.locationStatus)
}

function pruneMarkers(list) {
  const keys = new Set(list.map(markerKey))
  for (const key of [...markers.keys()]) {
    if (keys.has(key))
      continue
    markers.get(key)?.remove()
    markers.delete(key)
    markerStatus.delete(key)
  }
}

function syncMarkers(list) {
  pruneMarkers(list)
  list.forEach(upsertMarker)
}

async function load() {
  const latest = (await smGetFollowerLiveLocations(cityValue.value))
    .filter(f => f.shiftStatus === 'Active' && f.lastLatitude != null && f.lastLongitude != null)

  followers.value = latest
  if (selected.value) {
    const next = latest.find(f => branchRowKey(f) === branchRowKey(selected.value))
    selected.value = next || null
  }
  syncMarkers(latest)
}

onMounted(async () => {
  await nextTick()
  if (token && mapEl.value) {
    mapboxgl.accessToken = token
    mapEl.value.setAttribute('dir', 'ltr')
    map = new mapboxgl.Map({
      container: mapEl.value,
      style: 'mapbox://styles/mapbox/streets-v12',
      center: [44.33, 32.0],
      zoom: 11,
    })
    map.getContainer().setAttribute('dir', 'ltr')
    map.on('load', load)
  }
  else {
    await load()
  }

  poll = setInterval(load, 15000)
})

onUnmounted(() => {
  clearInterval(poll)
  map?.remove()
})
</script>

<template>
  <div>
    <h4 class="mb-4">
      موقع المتابعين
    </h4>
    <VRow class="mb-3">
      <VCol
        cols="12"
        md="4"
      >
        <SalesBranchFilter
          v-model="cityValue"
          @change="load"
        />
      </VCol>
    </VRow>
    <div
      v-if="!token"
      class="mb-3 text-medium-emphasis"
    >
      مفتاح الخريطة غير مهيأ. أضف VITE_MAPBOX_TOKEN. القائمة أدناه تعرض آخر موقع معروف.
    </div>
    <div
      v-show="token"
      ref="mapEl"
      class="sales-live-map"
      dir="ltr"
    />
    <VTable class="mt-4">
      <thead>
        <tr>
          <th>المتابع</th>
          <th>المحافظة</th>
          <th>آخر تحديث</th>
          <th>الدقة</th>
          <th>الحالة</th>
          <th />
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in followers"
          :key="branchRowKey(row)"
        >
          <td>{{ row.employeeName }}</td>
          <td>{{ row.branchName || row.cityName }}</td>
          <td>{{ formatLastUpdate(row.lastLocationAt) }}</td>
          <td>{{ formatAccuracy(row.lastAccuracy) }}</td>
          <td>{{ locationStatusLabel[row.locationStatus] || row.locationStatus }}</td>
          <td>
            <VBtn
              size="small"
              variant="text"
              :to="{ name: 'sales-manager-followers' }"
            >
              عرض التفاصيل
            </VBtn>
          </td>
        </tr>
      </tbody>
    </VTable>
    <VCard
      v-if="selected"
      class="mt-3"
    >
      <VCardText>
        <div><strong>{{ selected.employeeName }}</strong></div>
        <div>{{ selected.branchName || selected.cityName }}</div>
        <div>آخر تحديث: {{ formatLastUpdate(selected.lastLocationAt) }}</div>
        <div>الدقة: {{ formatAccuracy(selected.lastAccuracy) }}</div>
        <div>{{ locationStatusLabel[selected.locationStatus] }}</div>
      </VCardText>
    </VCard>
  </div>
</template>

<style scoped lang="scss">
.sales-live-map {
  height: 420px;
  border-radius: 12px;
  direction: ltr;
  /* html { zoom: 90% } in styles.scss desyncs Mapbox marker transforms on zoom */
  zoom: calc(10 / 9);

  :deep(.mapboxgl-marker) {
    inset: auto !important;
    right: auto !important;
  }
}
</style>
