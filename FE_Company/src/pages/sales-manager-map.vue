<script setup>
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { nextTick, onMounted, onUnmounted, ref } from 'vue'
import mapboxgl from 'mapbox-gl'
import 'mapbox-gl/dist/mapbox-gl.css'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { branchRowKey, locationStatusLabel, smGetLiveLocations } from '@/composables/salesManagerApi'
import { isCentralSalesManager } from '@/composables/useSalesBranches'
import { formatIraqTime } from '@/composables/gpsTrack'
import { MAPBOX_TOKEN } from '@/composables/mapboxToken'
import { getToken } from '@/services/tokenService'

const token = MAPBOX_TOKEN
const employees = ref([])
const cityValue = ref('')
const selected = ref(null)
const mapEl = ref(null)
let map
const markers = new Map()
const markerStatus = new Map()
let poll
let connection

function hubUrl() {
  const link = localStorage.getItem('LinkCity') || ''
  const origin = link.replace(/\/api\/?$/i, '').replace(/\/+$/, '')

  return `${origin}/hubs/sales-tracking`
}

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

function liveFromHub(body) {
  return {
    employeeId: body.employeeId ?? body.EmployeeId,
    employeeName: body.employeeName || body.EmployeeName || '',
    cityValue: body.cityValue || body.CityValue || '',
    cityName: body.cityName || body.CityName || '',
    branchName: body.cityName || body.CityName || '',
    shiftId: body.shiftId ?? body.ShiftId,
    shiftStatus: body.shiftStatus || body.ShiftStatus || 'Active',
    lastLatitude: body.latitude ?? body.Latitude ?? body.lastLatitude,
    lastLongitude: body.longitude ?? body.Longitude ?? body.lastLongitude,
    lastAccuracy: body.accuracy ?? body.Accuracy ?? body.lastAccuracy,
    lastLocationAt: body.capturedAt ?? body.CapturedAt ?? body.deviceTimestampUtc ?? body.DeviceTimestampUtc,
    locationStatus: body.locationStatus || body.LocationStatus || 'Live',
  }
}

function applyLiveUpdate(body) {
  if (!body)
    return
  const row = liveFromHub(body)
  if (!row.employeeId || row.lastLatitude == null || row.lastLongitude == null)
    return
  if (cityValue.value && row.cityValue && row.cityValue !== cityValue.value)
    return

  const key = markerKey(row)
  const idx = employees.value.findIndex(e => branchRowKey(e) === key)
  if (idx >= 0)
    employees.value.splice(idx, 1, { ...employees.value[idx], ...row })
  else
    employees.value = [...employees.value, row]

  upsertMarker(row)
}

async function load() {
  const latest = (await smGetLiveLocations(cityValue.value))
    .filter(e => e.shiftStatus === 'Active' && e.lastLatitude != null && e.lastLongitude != null)

  employees.value = latest
  if (selected.value) {
    const next = latest.find(e => branchRowKey(e) === branchRowKey(selected.value))
    selected.value = next || null
  }
  syncMarkers(latest)
}

async function connectSignalR() {
  if (isCentralSalesManager())
    return

  connection = new HubConnectionBuilder()
    .withUrl(hubUrl(), {
      accessTokenFactory: () => getToken() || '',
      withCredentials: false,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('locationUpdated', applyLiveUpdate)
  connection.onreconnected(() => {
    load()
  })

  try {
    await connection.start()
  }
  catch {
    connection = null
  }
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

  await connectSignalR()
  poll = setInterval(load, 15000)
})

onUnmounted(() => {
  clearInterval(poll)
  connection?.stop()
  map?.remove()
})
</script>

<template>
  <div>
    <h4 class="mb-4">
      الموقع المباشر
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
          <th>الموظف</th>
          <th>المحافظة</th>
          <th>آخر تحديث</th>
          <th>الدقة</th>
          <th>الحالة</th>
          <th />
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in employees"
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
              :to="{ name: 'sales-manager-employees' }"
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
