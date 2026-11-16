<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import mapboxgl from '@/utils/mapbox'
import 'mapbox-gl/dist/mapbox-gl.css'
import { directorDelegateFollowupListLocations } from '@/composables/directorApi'
import { MAPBOX_TOKEN } from '@/composables/mapboxToken'

/**
 * متابعة المندوبين — خريطة محلات الزبائن غير المصفرين للقوائم المحددة.
 * مصدر الموقع: تطبيق المبيعات (دبوس مفرغ) أو المتابع (دبوس ممتلئ) أو كلاهما. كل قائمة بلونها.
 */
const props = defineProps({
  cityValue: { type: String, required: true },

  /** [{ listId, listName, color }] */
  lists: { type: Array, default: () => [] },
})

const router = useRouter()

const SOURCE_ID = 'dlsm-customers'
const LAYER_ID = 'dlsm-pins'
const MODE_KEY = 'dlsm-source-mode'

const SOURCE_LABEL = {
  follower: 'موقع المتابع (GPS عند المحل)',
  sales: 'موقع تطبيق المبيعات',
}

const mapEl = ref(null)
const mapWrapEl = ref(null)
const token = MAPBOX_TOKEN
const activeOnly = ref(false)
const loadingIds = ref(new Set())
const errorIds = ref(new Set())

/** sales | follower | both */
const mode = ref(readMode())

function readMode() {
  try {
    const v = localStorage.getItem(MODE_KEY)

    return ['sales', 'follower', 'both'].includes(v) ? v : 'both'
  }
  catch {
    return 'both'
  }
}

watch(mode, v => {
  try {
    localStorage.setItem(MODE_KEY, v)
  }
  catch { /* ignore */ }
})

/** key `${city}|${listId}` → response (per tab). */
const cache = new Map()
const dataVersion = ref(0)

let map = null
let mapReady = false
let resizeObserver = null
let popup = null
const icons = new Set()

function cacheKey(listId) {
  return `${props.cityValue}|${listId}`
}

function listData(listId) {
  void dataVersion.value

  return cache.get(cacheKey(listId)) || null
}

async function ensureLoaded(listId) {
  const key = cacheKey(listId)
  if (cache.has(key) || loadingIds.value.has(listId))
    return
  loadingIds.value = new Set([...loadingIds.value, listId])

  const err = new Set(errorIds.value)

  err.delete(listId)
  errorIds.value = err
  try {
    const data = await directorDelegateFollowupListLocations({ cityValue: props.cityValue, listId })

    cache.set(key, data || { customers: [] })
    dataVersion.value++
  }
  catch {
    errorIds.value = new Set([...errorIds.value, listId])
  }
  finally {
    const next = new Set(loadingIds.value)

    next.delete(listId)
    loadingIds.value = next
  }
}

function point(lat, lng) {
  return Number.isFinite(lat) && Number.isFinite(lng) ? { lat, lng } : null
}

/** Both points of a customer (older API: fall back to the single primary point). */
function pointsOf(c) {
  const hasSplit = 'followerLatitude' in c || 'salesLatitude' in c
  if (hasSplit) {
    return {
      follower: point(c.followerLatitude, c.followerLongitude),
      sales: point(c.salesLatitude, c.salesLongitude),
    }
  }

  return {
    follower: c.source === 'follower' ? point(c.latitude, c.longitude) : null,
    sales: c.source === 'gps' ? point(c.latitude, c.longitude) : null,
  }
}

/** Pins this customer gets under the selected source. */
function pinsFor(c) {
  const p = pointsOf(c)
  const out = []
  if (mode.value !== 'sales' && p.follower)
    out.push({ kind: 'follower', ...p.follower })
  if (mode.value !== 'follower' && p.sales)
    out.push({ kind: 'sales', ...p.sales })

  return out
}

function inScope(c) {
  return !activeOnly.value || c.isActive
}

const modeWithoutLabel = computed(() => ({
  sales: 'بدون موقع من المبيعات',
  follower: 'بدون موقع من المتابع',
  both: 'بدون أي موقع',
}[mode.value]))

const legend = computed(() => props.lists.map(l => {
  const d = listData(l.listId)
  const rows = (Array.isArray(d?.customers) ? d.customers : []).filter(inScope)
  const shown = rows.filter(c => pinsFor(c).length).length

  return {
    ...l,
    loading: loadingIds.value.has(l.listId),
    failed: errorIds.value.has(l.listId),
    loaded: !!d,
    total: rows.length,
    shown,
    without: rows.length - shown,
    avgDaily: Number(d?.avgDailyCollected30d ?? NaN),
    collected: Number(d?.collected30d ?? NaN),
    daily: Array.isArray(d?.daily30d) ? d.daily30d : [],
  }
}))

/** Day-by-day popup behind the 30-day average. */
const dailyListId = ref(null)
const dailyList = computed(() => legend.value.find(l => l.listId === dailyListId.value) || null)

const WEEKDAYS = ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت']

const dailyRows = computed(() => {
  const days = dailyList.value?.daily || []
  const max = Math.max(1, ...days.map(x => Number(x.amount) || 0))

  // Newest first — the most recent days are what people check.
  return days.map(x => {
    const key = String(x.date || '').slice(0, 10)
    const [y, m, dd] = key.split('-').map(Number)
    const dt = new Date(Date.UTC(y, (m || 1) - 1, dd || 1))
    const amount = Number(x.amount) || 0

    return {
      key,
      weekday: WEEKDAYS[dt.getUTCDay()],
      label: `${String(dd).padStart(2, '0')}/${String(m).padStart(2, '0')}`,
      amount,
      payments: Number(x.payments) || 0,
      pct: Math.round(amount / max * 100),
    }
  }).reverse()
})

const dailyActiveDays = computed(() => dailyRows.value.filter(r => r.amount > 0).length)

const totalShown = computed(() => legend.value.reduce((s, l) => s + l.shown, 0))

function iconName(color, kind) {
  return `dlsm-${kind}-${color.replace('#', '')}`
}

/** Pin images drawn on a canvas — solid for the follower, hollow for the sales app. */
function ensureIcon(color, kind) {
  const name = iconName(color, kind)
  if (!map || icons.has(name) || map.hasImage(name))
    return name
  const ratio = 2
  const w = 26, h = 36
  const canvas = document.createElement('canvas')

  canvas.width = w * ratio
  canvas.height = h * ratio

  const ctx = canvas.getContext('2d')

  ctx.scale(ratio, ratio)

  const cx = w / 2, cy = 12, r = 10

  ctx.beginPath()
  ctx.moveTo(cx, h - 2)
  ctx.bezierCurveTo(cx - 3, h - 10, cx - r, cy + 7, cx - r, cy)
  ctx.arc(cx, cy, r, Math.PI, 0)
  ctx.bezierCurveTo(cx + r, cy + 7, cx + 3, h - 10, cx, h - 2)
  ctx.closePath()
  if (kind === 'sales') {
    ctx.fillStyle = '#ffffff'
    ctx.fill()
    ctx.lineWidth = 2.6
    ctx.strokeStyle = color
    ctx.stroke()
    ctx.beginPath()
    ctx.arc(cx, cy, 4, 0, Math.PI * 2)
    ctx.fillStyle = color
    ctx.fill()
  }
  else {
    ctx.fillStyle = color
    ctx.fill()
    ctx.lineWidth = 1.6
    ctx.strokeStyle = '#ffffff'
    ctx.stroke()
    ctx.beginPath()
    ctx.arc(cx, cy, 4, 0, Math.PI * 2)
    ctx.fillStyle = '#ffffff'
    ctx.fill()
  }
  map.addImage(name, ctx.getImageData(0, 0, canvas.width, canvas.height), { pixelRatio: ratio })
  icons.add(name)

  return name
}

function featureCollection() {
  const features = []
  for (const l of props.lists) {
    const d = listData(l.listId)
    for (const c of (d?.customers || [])) {
      if (!inScope(c))
        continue
      for (const pin of pinsFor(c)) {
        features.push({
          type: 'Feature',
          geometry: { type: 'Point', coordinates: [pin.lng, pin.lat] },
          properties: {
            icon: ensureIcon(l.color, pin.kind),
            kind: pin.kind,
            order: pin.kind === 'follower' ? 1 : 0,
            listId: l.listId,
            customerId: c.customerId,
          },
        })
      }
    }
  }

  return { type: 'FeatureCollection', features }
}

function findCustomer(listId, customerId) {
  const d = listData(listId)

  return (d?.customers || []).find(c => c.customerId === customerId) || null
}

function esc(v) {
  return String(v ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', '\'': '&#39;' }[ch]))
}

function fmtNum(n) {
  const v = Number(n)

  return Number.isFinite(v) ? Math.round(v).toLocaleString('en-US') : '—'
}

function fmtDate(v) {
  if (!v)
    return '—'

  return String(v).slice(0, 10)
}

function distanceMeters(a, b) {
  const phi = (a.lat + b.lat) / 2 * Math.PI / 180
  const dx = (b.lng - a.lng) * Math.PI / 180 * Math.cos(phi)
  const dy = (b.lat - a.lat) * Math.PI / 180

  return Math.sqrt(dx * dx + dy * dy) * 6371000
}

/** Customer profile in a new tab (hash router → resolve gives the full #/ link). */
function profileHref(c) {
  return router.resolve({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: props.cityValue,
      customerId: String(c.customerId),
      name: c.customerName || '',
      phone: c.phoneNumber || '',
    },
  }).href
}

function popupHtml(list, c, kind) {
  const p = pointsOf(c)
  const here = p[kind]
  const other = kind === 'follower' ? p.sales : p.follower
  const gmaps = `https://www.google.com/maps?q=${here.lat},${here.lng}`
  const gap = other ? Math.round(distanceMeters(here, other)) : null

  return `<div dir="rtl" class="dlsm-pop">
    <a class="dlsm-pop__name" href="${esc(profileHref(c))}" target="_blank" rel="noopener noreferrer">${esc(c.customerName)}</a>
    ${c.shopName ? `<div>المحل: ${esc(c.shopName)}</div>` : ''}
    <div>القائمة: <span class="dlsm-pop__swatch" style="background:${esc(list.color)}"></span>${esc(list.listName)}</div>
    ${c.phoneNumber ? `<div>الهاتف: <span dir="ltr">${esc(c.phoneNumber)}</span></div>` : ''}
    ${c.address ? `<div>العنوان: ${esc(c.address)}</div>` : ''}
    <div>الباقي: ${fmtNum(c.amountRemaining)}</div>
    <div>آخر تسديد: ${esc(fmtDate(c.lastPaymentDate))}</div>
    ${c.ratingLabel ? `<div>التقييم: ${esc(c.ratingLabel)}</div>` : ''}
    <div class="dlsm-pop__where"><b>هذا الدبوس:</b> ${esc(SOURCE_LABEL[kind])}</div>
    ${gap != null ? `<div>يبعد عن ${kind === 'follower' ? 'موقع المبيعات' : 'موقع المتابع'}: ${fmtNum(gap)} م</div>` : ''}
    <a href="${gmaps}" target="_blank" rel="noopener noreferrer">فتح في خرائط Google</a>
  </div>`
}

let lastFitKey = ''

function render({ fit = false } = {}) {
  if (!map || !mapReady)
    return
  const data = featureCollection()
  const src = map.getSource(SOURCE_ID)
  if (src) {
    src.setData(data)
  }
  else {
    map.addSource(SOURCE_ID, { type: 'geojson', data })
    map.addLayer({
      id: LAYER_ID,
      type: 'symbol',
      source: SOURCE_ID,
      layout: {
        'icon-image': ['get', 'icon'],
        'icon-anchor': 'bottom',
        'icon-allow-overlap': true,
        'icon-ignore-placement': true,
        'symbol-sort-key': ['get', 'order'],
      },
    })
    map.on('click', LAYER_ID, e => {
      const f = e.features?.[0]
      if (!f)
        return
      const list = props.lists.find(l => l.listId === f.properties.listId)
      const c = findCustomer(f.properties.listId, f.properties.customerId)
      if (!list || !c)
        return
      popup?.remove()
      popup = new mapboxgl.Popup({ offset: 30, maxWidth: '300px' })
        .setLngLat(f.geometry.coordinates)
        .setHTML(popupHtml(list, c, f.properties.kind))
        .addTo(map)
    })
    map.on('mouseenter', LAYER_ID, () => { map.getCanvas().style.cursor = 'pointer' })
    map.on('mouseleave', LAYER_ID, () => { map.getCanvas().style.cursor = '' })
  }

  // Re-fit when the set of loaded selected lists changes (not on filter toggles).
  const fitKey = props.lists.filter(l => listData(l.listId)).map(l => l.listId).join(',')
  if ((fit || fitKey !== lastFitKey) && data.features.length) {
    lastFitKey = fitKey

    const bounds = new mapboxgl.LngLatBounds()

    data.features.forEach(f => bounds.extend(f.geometry.coordinates))
    map.fitBounds(bounds, { padding: 50, maxZoom: 15, duration: 600 })
  }
}

watch(
  () => [props.cityValue, props.lists.map(l => `${l.listId}:${l.color}`).join(',')],
  () => {
    props.lists.forEach(l => { void ensureLoaded(l.listId) })
    render()
  },
  { immediate: true },
)

watch([dataVersion, activeOnly, mode], () => {
  popup?.remove()
  render()
})

watch(() => props.cityValue, () => {
  popup?.remove()
  lastFitKey = ''
})

function retry(listId) {
  void ensureLoaded(listId)
}

onMounted(async () => {
  await nextTick()
  if (!token || !mapEl.value)
    return
  mapboxgl.accessToken = token
  mapEl.value.setAttribute('dir', 'ltr')
  map = new mapboxgl.Map({
    container: mapEl.value,
    style: 'mapbox://styles/mapbox/streets-v12',
    center: [44.36, 33.31],
    zoom: 10,
  })
  map.getContainer().setAttribute('dir', 'ltr')
  map.addControl(new mapboxgl.NavigationControl({ showCompass: false }), 'top-left')
  map.addControl(new mapboxgl.FullscreenControl({ container: mapWrapEl.value }), 'top-left')
  map.on('load', () => {
    mapReady = true
    map.resize()
    render({ fit: true })
  })

  // Panel width settles after the cards render (and when the kept-alive page is shown again).
  resizeObserver = new ResizeObserver(() => map?.resize())
  resizeObserver.observe(mapEl.value)
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  resizeObserver = null
  popup?.remove()
  map?.remove()
  map = null
  mapReady = false
  icons.clear()
})
</script>

<template>
  <section class="dlsm">
    <div class="dlsm__head">
      <h3 class="dlsm__title">
        خريطة محلات الزبائن
      </h3>
      <div class="dlsm__toggles">
        <VBtnToggle
          v-model="mode"
          mandatory
          density="compact"
          color="primary"
          variant="outlined"
          divided
        >
          <VBtn value="sales">
            المبيعات
          </VBtn>
          <VBtn value="follower">
            المتابع
          </VBtn>
          <VBtn value="both">
            الاثنين
          </VBtn>
        </VBtnToggle>
        <VCheckbox
          v-model="activeOnly"
          density="compact"
          hide-details
          label="النشطين فقط (تسديد خلال 30 يوماً)"
        />
      </div>
    </div>

    <div class="dlsm__keys muted">
      <span
        v-if="mode !== 'sales'"
        class="dlsm__key"
      ><span class="dlsm__pin dlsm__pin--solid" /> موقع المتابع</span>
      <span
        v-if="mode !== 'follower'"
        class="dlsm__key"
      ><span class="dlsm__pin dlsm__pin--hollow" /> موقع تطبيق المبيعات</span>
      <span class="dlsm__key">اضغط اسم الزبون في الدبوس لفتح بروفايله</span>
    </div>

    <div
      v-if="lists.length"
      class="dlsm__legend"
    >
      <div
        v-for="l in legend"
        :key="l.listId"
        class="dlsm__legend-row"
      >
        <span
          class="dlsm__swatch"
          :style="{ background: l.color }"
        />
        <strong>{{ l.listName }}</strong>
        <span
          v-if="l.loading"
          class="muted"
        >جاري التحميل…</span>
        <span
          v-else-if="l.failed"
          class="text-error"
        >
          تعذر التحميل
          <VBtn
            size="x-small"
            variant="text"
            color="error"
            @click="retry(l.listId)"
          >إعادة</VBtn>
        </span>
        <template v-else-if="l.loaded">
          <span class="dlsm__stat">الزبائن <b>{{ fmtNum(l.total) }}</b></span>
          <span class="dlsm__stat">على الخريطة <b>{{ fmtNum(l.shown) }}</b></span>
          <span class="dlsm__stat">{{ modeWithoutLabel }} <b>{{ fmtNum(l.without) }}</b></span>
          <button
            v-if="Number.isFinite(l.avgDaily)"
            type="button"
            class="dlsm__stat dlsm__stat--link"
            :disabled="!l.daily.length"
            title="عرض الاستحصال يوماً بيوم"
            @click="dailyListId = l.listId"
          >
            معدل الاستحصال اليومي (30 يوماً) <b>{{ fmtNum(l.avgDaily) }}</b>
          </button>
        </template>
      </div>
    </div>

    <div
      v-if="!token"
      class="dlsm__empty muted"
    >
      مفتاح الخريطة (Mapbox) غير مضبوط.
    </div>
    <div
      v-else
      ref="mapWrapEl"
      class="dlsm__map-wrap"
    >
      <div
        ref="mapEl"
        class="dlsm__map"
      />
      <div
        v-if="!lists.length"
        class="dlsm__overlay"
      >
        حدّد قائمة أو أكثر من المربعات أعلاه لعرض زبائنها على الخريطة
      </div>
      <div
        v-else-if="!totalShown && !legend.some(l => l.loading)"
        class="dlsm__overlay"
      >
        لا توجد مواقع لعرضها
      </div>
    </div>

    <VDialog
      :model-value="!!dailyList"
      max-width="520"
      scrollable
      @update:model-value="v => { if (!v) dailyListId = null }"
    >
      <VCard v-if="dailyList">
        <VCardTitle class="dlsm-daily__title">
          <span>
            الاستحصال اليومي — {{ dailyList.listName }}
          </span>
          <VBtn
            icon="tabler-x"
            size="small"
            variant="text"
            @click="dailyListId = null"
          />
        </VCardTitle>
        <div class="dlsm-daily__sum">
          <span>المجموع <b>{{ fmtNum(dailyList.collected) }}</b></span>
          <span>المعدل اليومي <b>{{ fmtNum(dailyList.avgDaily) }}</b></span>
          <span>أيام فيها استحصال <b>{{ dailyActiveDays }} / {{ dailyRows.length }}</b></span>
        </div>
        <VCardText class="dlsm-daily__body">
          <div
            v-for="r in dailyRows"
            :key="r.key"
            class="dlsm-daily__row"
            :class="{ 'dlsm-daily__row--zero': !r.amount }"
          >
            <span class="dlsm-daily__day">{{ r.weekday }} <span class="muted">{{ r.label }}</span></span>
            <span class="dlsm-daily__bar">
              <span :style="{ width: `${r.pct}%`, background: dailyList.color }" />
            </span>
            <span class="dlsm-daily__amt">{{ r.amount ? fmtNum(r.amount) : '—' }}</span>
            <span class="dlsm-daily__cnt muted">{{ r.payments ? `${r.payments} دفعة` : '' }}</span>
          </div>
        </VCardText>
      </VCard>
    </VDialog>
  </section>
</template>

<style scoped>
.dlsm {
  margin-top: 20px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 14px;
  padding: 14px 16px;
  background: rgb(var(--v-theme-surface));
}
.dlsm__head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.dlsm__title { font-size: 1.05rem; margin: 0; }
.dlsm__toggles { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 16px; }
.dlsm__keys {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 18px;
  font-size: 0.85rem;
  margin: 8px 0 10px;
}
.dlsm__key { display: inline-flex; align-items: center; gap: 6px; }
.dlsm__pin {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 50% 50% 50% 0;
  transform: rotate(-45deg);
}
.dlsm__pin--solid { background: rgb(var(--v-theme-on-surface)); }
.dlsm__pin--hollow { border: 2.5px solid rgb(var(--v-theme-on-surface)); background: #fff; }
.dlsm__legend {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 10px;
  font-size: 0.9rem;
}
.dlsm__legend-row { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 14px; }
.dlsm__stat { opacity: 0.85; }
.dlsm__stat b { opacity: 1; }
.dlsm__stat--link {
  padding: 0;
  border: 0;
  background: none;
  color: inherit;
  font: inherit;
  cursor: pointer;
  text-decoration: underline dotted;
  text-underline-offset: 3px;
}
.dlsm__stat--link:disabled { cursor: default; text-decoration: none; }
.dlsm__stat--link:not(:disabled):hover b { color: rgb(var(--v-theme-primary)); }
.dlsm-daily__title { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.dlsm-daily__sum {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 16px;
  padding: 0 24px 10px;
  font-size: 0.9rem;
  opacity: 0.9;
}
.dlsm-daily__body { padding-top: 4px !important; }
.dlsm-daily__row {
  display: grid;
  grid-template-columns: 110px 1fr 90px 60px;
  align-items: center;
  gap: 10px;
  padding: 5px 0;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.5);
  font-size: 0.88rem;
}
.dlsm-daily__row--zero { opacity: 0.5; }
.dlsm-daily__bar {
  height: 8px;
  border-radius: 4px;
  background: rgba(var(--v-border-color), var(--v-border-opacity));
  overflow: hidden;
}
.dlsm-daily__bar span { display: block; height: 100%; border-radius: 4px; }
.dlsm-daily__amt { text-align: left; font-variant-numeric: tabular-nums; font-weight: 600; }
.dlsm-daily__cnt { font-size: 0.78rem; }
.dlsm__swatch {
  width: 14px;
  height: 14px;
  border-radius: 4px;
  flex: none;
}
.dlsm__map-wrap { position: relative; background: rgb(var(--v-theme-surface)); }
.dlsm__map-wrap:fullscreen .dlsm__map { height: 100vh; border-radius: 0; }
.dlsm__map {
  width: 100%;
  height: max(560px, 72vh);
  border-radius: 10px;
  overflow: hidden;
  /* html { zoom: 90% } in styles.scss desyncs Mapbox sizing and clicks — same fix as the other maps. */
  zoom: calc(10 / 9);
}
.dlsm__overlay {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 16px;
  background: rgba(var(--v-theme-surface), 0.7);
  border-radius: 10px;
  font-weight: 600;
  pointer-events: none;
}
.dlsm__empty { padding: 24px; text-align: center; }
:deep(.dlsm-pop) { font-size: 0.85rem; line-height: 1.6; color: #222; }
:deep(.dlsm-pop__name) {
  display: inline-block;
  font-weight: 800;
  font-size: 0.95rem;
  color: rgb(var(--v-theme-primary));
  text-decoration: underline;
}
:deep(.dlsm-pop__where) { margin-top: 4px; padding-top: 4px; border-top: 1px solid #ddd; }
:deep(.dlsm-pop__swatch) {
  display: inline-block;
  width: 10px;
  height: 10px;
  border-radius: 3px;
  margin-inline-end: 4px;
  vertical-align: middle;
}
@media (max-width: 600px) {
  .dlsm__map { height: max(440px, 65vh); }
}
</style>
