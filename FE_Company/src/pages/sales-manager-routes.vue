<script setup>
import { onMounted, ref } from 'vue'
import mapboxgl from 'mapbox-gl'
import 'mapbox-gl/dist/mapbox-gl.css'
import { smGet } from '@/composables/salesManagerApi'

const employees = ref([])
const employeeId = ref(null)
const date = ref(new Date().toISOString().slice(0, 10))
const route = ref(null)
const events = ref([])
const token = import.meta.env.VITE_MAPBOX_TOKEN || ''
const mapEl = ref(null)
let map

onMounted(async () => {
  employees.value = await smGet('employees')
})

async function loadRoute() {
  if (!employeeId.value) return
  route.value = await smGet(`employees/${employeeId.value}/route?date=${date.value}`)
  events.value = await smGet(`employees/${employeeId.value}/tracking-events?date=${date.value}`)
  if (token && mapEl.value && route.value?.points?.length) {
    if (!map) {
      mapboxgl.accessToken = token
      map = new mapboxgl.Map({
        container: mapEl.value,
        style: 'mapbox://styles/mapbox/streets-v12',
        center: [route.value.points[0].longitude, route.value.points[0].latitude],
        zoom: 12,
      })
    }
    const coords = route.value.points.map(p => [p.longitude, p.latitude])
    if (map.getSource('route')) {
      map.getSource('route').setData({ type: 'Feature', geometry: { type: 'LineString', coordinates: coords } })
    }
    else {
      map.on('load', () => {
        map.addSource('route', { type: 'geojson', data: { type: 'Feature', geometry: { type: 'LineString', coordinates: coords } } })
        map.addLayer({ id: 'route-line', type: 'line', source: 'route', paint: { 'line-color': '#0B6B3A', 'line-width': 4 } })
      })
    }
  }
}
</script>

<template>
  <div>
    <h4 class="mb-4">
      المسارات
    </h4>
    <VRow>
      <VCol md="4">
        <VSelect
          v-model="employeeId"
          :items="employees"
          item-title="employeeName"
          item-value="employeeId"
          label="الموظف"
        />
      </VCol>
      <VCol md="4">
        <VTextField
          v-model="date"
          type="date"
          label="التاريخ"
        />
      </VCol>
      <VCol md="4">
        <VBtn
          color="primary"
          class="mt-2"
          @click="loadRoute"
        >
          عرض المسار
        </VBtn>
      </VCol>
    </VRow>
    <div
      v-if="route"
      class="my-3"
    >
      بداية الدوام: {{ route.shift?.startedAt }} —
      نهاية / قطع: {{ route.shift?.cutoffAt }} —
      النقاط: {{ route.returnedPoints }} / {{ route.totalPoints }}
      <span v-if="route.isTruncated">(مختصر)</span>
    </div>
    <div
      v-show="token"
      ref="mapEl"
      style="height: 360px;"
    />
    <h6 class="mt-6">
      أحداث التتبع
    </h6>
    <VTimeline
      v-if="events.length"
      density="compact"
    >
      <VTimelineItem
        v-for="(ev, i) in events"
        :key="i"
        size="small"
      >
        {{ ev.eventType }} — {{ ev.occurredAt }}
      </VTimelineItem>
    </VTimeline>
  </div>
</template>
