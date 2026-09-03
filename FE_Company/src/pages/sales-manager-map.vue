<script setup>
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { nextTick, onMounted, onUnmounted, ref } from 'vue'
import mapboxgl from 'mapbox-gl'
import 'mapbox-gl/dist/mapbox-gl.css'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { branchRowKey, locationStatusLabel, shouldMoveMarker, smGet, withCityQuery } from '@/composables/salesManagerApi'
import { isCentralSalesManager } from '@/composables/useSalesBranches'
import { MAPBOX_TOKEN } from '@/composables/mapboxToken'
import { getToken } from '@/services/tokenService'

const token = MAPBOX_TOKEN
const employees = ref([])
const cityValue = ref('')
const selected = ref(null)
const mapEl = ref(null)
let map
const markers = new Map()
const lastCaptured = new Map()
let poll
let connection

function hubUrl() {
  const link = localStorage.getItem('LinkCity') || ''
  const origin = link.replace(/\/api\/?$/i, '').replace(/\/+$/, '')

  return `${origin}/hubs/sales-tracking`
}

function liveEmployees(list) {
  return (list || []).filter(e => e.lastLatitude != null && e.lastLongitude != null)
}

function markerKey(row) {
  return branchRowKey(row)
}

function popupHtml(row) {
  return `<div dir="rtl">
    <b>${row.employeeName || ''}</b><br>
    الفرع: ${row.branchName || row.cityName || ''}<br>
    آخر تحديث: ${row.lastLocationAt || '—'}<br>
    الدوام: ${row.shiftStatus || '—'}<br>
    التتبع: ${locationStatusLabel[row.locationStatus] || row.locationStatus || '—'}
  </div>`
}

function upsertMarker(row) {
  if (!map || row.lastLatitude == null)
    return
  const key = markerKey(row)
  const prev = lastCaptured.get(key)
  if (!shouldMoveMarker(prev, row.lastLocationAt))
    return
  lastCaptured.set(key, row.lastLocationAt)

  const lngLat = [row.lastLongitude, row.lastLatitude]
  if (markers.has(key)) {
    markers.get(key).setLngLat(lngLat).getPopup()?.setHTML(popupHtml(row))

    return
  }

  const marker = new mapboxgl.Marker().setLngLat(lngLat).setPopup(
    new mapboxgl.Popup().setHTML(popupHtml(row)),
  ).addTo(map)

  marker.getElement().addEventListener('click', () => { selected.value = row })
  markers.set(key, marker)
}

function applyLiveUpdate(body) {
  if (!body?.employeeId)
    return

  const row = {
    employeeId: body.employeeId,
    employeeName: body.employeeName,
    cityName: body.cityName,
    cityValue: body.cityValue,
    branchName: body.cityName,
    shiftStatus: 'Active',
    lastLatitude: body.latitude,
    lastLongitude: body.longitude,
    lastLocationAt: body.capturedAt,
    locationStatus: 'Live',
  }

  const key = markerKey(row)
  if (!shouldMoveMarker(lastCaptured.get(key), body.capturedAt))
    return

  const idx = employees.value.findIndex(e => branchRowKey(e) === key)
  if (idx >= 0)
    employees.value.splice(idx, 1, { ...employees.value[idx], ...row })
  else
    employees.value = [...employees.value, row]

  upsertMarker(row)
}

async function load() {
  const latest = liveEmployees(await smGet(withCityQuery('employees', cityValue.value)))

  employees.value = latest
  latest.forEach(upsertMarker)
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
    map = new mapboxgl.Map({
      container: mapEl.value,
      style: 'mapbox://styles/mapbox/streets-v12',
      center: [44.33, 32.0],
      zoom: 11,
    })
    map.on('load', load)
  }
  else {
    await load()
  }

  await connectSignalR()
  poll = setInterval(load, 20000)
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
      الخريطة الحية
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
      style="height: 420px; border-radius: 12px;"
    />
    <VTable class="mt-4">
      <thead>
        <tr>
          <th>الموظف</th>
          <th>المحافظة</th>
          <th>آخر تحديث</th>
          <th>الدوام</th>
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
          <td>{{ row.lastLocationAt }}</td>
          <td>{{ row.shiftStatus }} / {{ locationStatusLabel[row.locationStatus] }}</td>
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
        <div>{{ selected.lastLocationAt }}</div>
        <div>{{ selected.shiftStatus }} / {{ locationStatusLabel[selected.locationStatus] }}</div>
      </VCardText>
    </VCard>
  </div>
</template>
