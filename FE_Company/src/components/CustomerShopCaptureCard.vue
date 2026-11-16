<script setup>
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import mapboxgl from '@/utils/mapbox'
import 'mapbox-gl/dist/mapbox-gl.css'
import { directorFollowerOpsShopCaptureCustomer, directorFollowerOpsShopCapturePhoto } from '@/composables/directorApi'
import { MAPBOX_TOKEN } from '@/composables/mapboxToken'

/**
 * بروفايل الزبون — موقع المحل وصورته من المتابع ومن تطبيق المبيعات معاً:
 * خريطة واحدة بدبوسين، وصورتان جنباً إلى جنب.
 */
const props = defineProps({
  cityValue: { type: String, default: '' },
  customerId: { type: [Number, String], default: null },

  /** Latest sale's shop photo (blob URL) already loaded by the profile page. */
  salesPhotoUrl: { type: String, default: '' },
})

const FOLLOWER_COLOR = '#00796B'
const SALES_COLOR = '#F57C00'

const loading = ref(false)
const data = ref(null)
const followerPhotoUrl = ref('')
const viewerUrl = ref('')
const mapEl = ref(null)

let map = null

const locations = computed(() => {
  const d = data.value
  if (!d)
    return []
  const out = []
  if (d.followerLatitude != null && d.followerLongitude != null) {
    out.push({
      key: 'follower',
      title: 'موقع المتابع',
      color: FOLLOWER_COLOR,
      lat: d.followerLatitude,
      lng: d.followerLongitude,
      sub: [d.locationBy, fmtDate(d.followerLocationAtUtc), d.followerAccuracyM != null ? `دقة ${Math.round(d.followerAccuracyM)} م` : '']
        .filter(Boolean).join(' — '),
    })
  }
  if (d.salesLatitude != null && d.salesLongitude != null)
    out.push({ key: 'sales', title: 'موقع تطبيق المبيعات', color: SALES_COLOR, lat: d.salesLatitude, lng: d.salesLongitude, sub: '' })

  return out
})

const distanceM = computed(() => {
  const [a, b] = locations.value
  if (!a || !b)
    return null
  const phi = (a.lat + b.lat) / 2 * Math.PI / 180
  const dx = (b.lng - a.lng) * Math.PI / 180 * Math.cos(phi)
  const dy = (b.lat - a.lat) * Math.PI / 180

  return Math.round(Math.sqrt(dx * dx + dy * dy) * 6371000)
})

const photos = computed(() => [
  { key: 'follower', title: 'صورة المتابع', url: followerPhotoUrl.value, sub: data.value?.photoBy ? `${data.value.photoBy} — ${fmtDate(data.value.photoAtUtc)}` : '' },
  { key: 'sales', title: 'صورة تطبيق المبيعات', url: props.salesPhotoUrl, sub: '' },
])

function fmtDate(v) {
  return v ? String(v).slice(0, 10) : ''
}

function mapsUrl(l) {
  return `https://www.google.com/maps?q=${l.lat},${l.lng}`
}

function clearPhoto() {
  if (followerPhotoUrl.value)
    URL.revokeObjectURL(followerPhotoUrl.value)
  followerPhotoUrl.value = ''
}

function destroyMap() {
  map?.remove()
  map = null
}

async function drawMap() {
  destroyMap()
  await nextTick()

  const locs = locations.value
  if (!locs.length || !mapEl.value || !MAPBOX_TOKEN)
    return

  mapboxgl.accessToken = MAPBOX_TOKEN
  mapEl.value.setAttribute('dir', 'ltr')
  map = new mapboxgl.Map({
    container: mapEl.value,
    style: 'mapbox://styles/mapbox/streets-v12',
    center: [locs[0].lng, locs[0].lat],
    zoom: 16,
  })
  map.getContainer().setAttribute('dir', 'ltr')
  map.addControl(new mapboxgl.NavigationControl({ showCompass: false }), 'top-left')
  for (const l of locs) {
    new mapboxgl.Marker({ color: l.color })
      .setLngLat([l.lng, l.lat])
      .setPopup(new mapboxgl.Popup({ offset: 28 }).setText(l.title))
      .addTo(map)
  }
  if (locs.length > 1) {
    const bounds = new mapboxgl.LngLatBounds()

    locs.forEach(l => bounds.extend([l.lng, l.lat]))
    map.fitBounds(bounds, { padding: 60, maxZoom: 17, duration: 0 })
  }
  map.on('load', () => map?.resize())
}

async function load() {
  data.value = null
  clearPhoto()
  destroyMap()
  if (!props.cityValue || !props.customerId)
    return
  loading.value = true
  try {
    data.value = await directorFollowerOpsShopCaptureCustomer({ cityValue: props.cityValue, customerId: props.customerId })
  }
  catch {
    // Section stays empty — the rest of the profile is unaffected.
  }
  finally {
    loading.value = false
  }
  await drawMap()
  if (data.value?.hasPhoto) {
    try {
      const blob = await directorFollowerOpsShopCapturePhoto({ cityValue: props.cityValue, customerId: props.customerId })

      followerPhotoUrl.value = URL.createObjectURL(blob)
    }
    catch { /* no photo */ }
  }
}

watch(() => [props.cityValue, props.customerId], load, { immediate: true })
onBeforeUnmount(() => {
  destroyMap()
  clearPhoto()
})
</script>

<template>
  <VCard class="mb-6 pa-6">
    <h5 class="mb-4">
      موقع وصورة المحل
    </h5>
    <div
      v-if="loading"
      class="text-medium-emphasis"
    >
      جاري التحميل...
    </div>
    <div
      v-else
      class="cscc"
    >
      <div class="cscc-loc">
        <div
          v-if="!locations.length"
          class="text-medium-emphasis"
        >
          لا يوجد موقع للمحل.
        </div>
        <template v-else>
          <div
            v-if="MAPBOX_TOKEN"
            ref="mapEl"
            class="cscc-map"
          />
          <div
            v-for="l in locations"
            :key="l.key"
            class="cscc-row"
          >
            <span
              class="cscc-dot"
              :style="{ background: l.color }"
            />
            <div class="cscc-row-text">
              <b>{{ l.title }}</b>
              <span
                v-if="l.sub"
                class="text-caption text-medium-emphasis"
              >{{ l.sub }}</span>
            </div>
            <a
              :href="mapsUrl(l)"
              target="_blank"
              rel="noopener noreferrer"
            >فتح في الخرائط</a>
          </div>
          <div
            v-if="distanceM != null"
            class="text-caption text-medium-emphasis"
          >
            المسافة بين الموقعين: {{ distanceM.toLocaleString('en-US') }} م
          </div>
          <div
            v-else
            class="text-caption text-medium-emphasis"
          >
            {{ locations[0].key === 'follower' ? 'لا يوجد موقع من تطبيق المبيعات' : 'لم يضف المتابع موقعاً بعد' }}
          </div>
        </template>
      </div>

      <div class="cscc-photos">
        <div
          v-for="p in photos"
          :key="p.key"
          class="cscc-photo"
        >
          <div class="text-caption font-weight-bold">
            {{ p.title }}
          </div>
          <button
            v-if="p.url"
            type="button"
            class="cscc-thumb"
            @click="viewerUrl = p.url"
          >
            <img
              :src="p.url"
              :alt="p.title"
            >
          </button>
          <div
            v-else
            class="cscc-thumb-empty text-medium-emphasis"
          >
            لا توجد صورة
          </div>
          <div
            v-if="p.url && p.sub"
            class="text-caption text-medium-emphasis"
          >
            {{ p.sub }}
          </div>
        </div>
      </div>
    </div>

    <VDialog
      :model-value="!!viewerUrl"
      max-width="760"
      @update:model-value="v => { if (!v) viewerUrl = '' }"
    >
      <VCard>
        <VCardText class="cscc-full">
          <img
            :src="viewerUrl"
            alt="صورة المحل"
          >
        </VCardText>
      </VCard>
    </VDialog>
  </VCard>
</template>

<style scoped>
.cscc { display: flex; flex-wrap: wrap; gap: 20px; align-items: flex-start; }
.cscc-loc { flex: 1 1 380px; display: flex; flex-direction: column; gap: 6px; min-width: 0; }
.cscc-map {
  width: 100%;
  height: 300px;
  border-radius: 10px;
  overflow: hidden;
  margin-bottom: 4px;
  /* html { zoom: 90% } in styles.scss desyncs Mapbox sizing and clicks — same fix as the other maps. */
  zoom: calc(10 / 9);
}
.cscc-row { display: flex; align-items: center; gap: 10px; }
.cscc-row-text { display: flex; flex-direction: column; flex: 1; }
.cscc-dot { width: 12px; height: 12px; border-radius: 50%; flex: none; }
.cscc-photos { flex: 0 1 430px; display: flex; flex-wrap: wrap; gap: 12px; }
.cscc-photo { display: flex; flex-direction: column; gap: 4px; width: 200px; }
.cscc-thumb { padding: 0; border: 0; background: none; cursor: zoom-in; }
.cscc-thumb img { width: 200px; height: 150px; object-fit: cover; border-radius: 10px; display: block; }
.cscc-thumb-empty {
  width: 200px;
  height: 150px;
  border-radius: 10px;
  border: 1px dashed rgba(var(--v-border-color), var(--v-border-opacity));
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.85rem;
}
.cscc-full { display: flex; justify-content: center; }
.cscc-full img { max-width: 100%; max-height: 75vh; border-radius: 8px; }
</style>
