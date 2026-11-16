<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { directorCities, directorGlobalCustomerSearch } from '@/composables/directorApi'

/**
 * Navbar «بحث الزبائن» for المدير / مسؤول المراقبين / لجنة المراقبة.
 * Queries every branch the caller may see in parallel (the gateway scopes committee accounts)
 * and shows hits as each branch answers — no waiting for the slowest branch.
 */
const PAGE = 5
const FINISHED_KEY = 'global-customer-search-include-finished'

const router = useRouter()

const query = ref('')
const results = ref([])
const visibleCount = ref(PAGE)
const busy = ref(false)
const branchesTotal = ref(0)
const branchesDone = ref(0)
const branchesFailed = ref(0)
const error = ref('')
const menuOpen = ref(false)
const activeIndex = ref(-1)
const fieldRef = ref(null)
const rootRef = ref(null)
const panelRef = ref(null)

/** Off by default: finished (مصفر) customers stay out unless the user asks for them. */
function readIncludeFinished() {
  try {
    return localStorage.getItem(FINISHED_KEY) === '1'
  }
  catch {
    return false
  }
}
const includeFinished = ref(readIncludeFinished())

let timer = null
let controller = null
let lastQuery = ''
let runId = 0
let citiesPromise = null

const RATING_COLORS = {
  'ممتاز': 'success',
  'جيد': 'info',
  'ضعيف': 'warning',
  'متلكئ': 'deep-orange',
  'سيء': 'error',
}

function pick(obj, ...keys) {
  for (const k of keys) {
    if (obj?.[k] !== undefined && obj?.[k] !== null && obj?.[k] !== '')
      return obj[k]
  }

  return null
}

function normalizeArabic(v) {
  return String(v || '')
    .trim()
    .replace(/[أإآ]/g, 'ا')
    .replace(/ى/g, 'ي')
    .toLowerCase()
}

function normHit(row, branch) {
  const remaining = Number(pick(row, 'amountRemaining', 'AmountRemaining') ?? 0)

  return {
    customerId: Number(pick(row, 'customerID', 'customerId', 'CustomerID') ?? 0),
    name: String(pick(row, 'customerName', 'CustomerName') ?? ''),
    phone: String(pick(row, 'phoneNumber', 'PhoneNumber') ?? ''),
    listName: String(pick(row, 'listName', 'ListName') ?? ''),
    cityValue: String(pick(row, 'cityValue', 'CityValue') ?? branch?.value ?? ''),
    cityName: String(pick(row, 'cityName', 'branchName', 'CityName') ?? branch?.name ?? ''),
    remaining,
    finished: Boolean(pick(row, 'isFinished', 'IsFinished') ?? remaining <= 0),
    legal: Boolean(pick(row, 'isLegal', 'IsLegal') ?? false),
    rating: String(pick(row, 'ratingLabel', 'RatingLabel') ?? ''),
  }
}

const fmtNum = n => Number(n || 0).toLocaleString('en-US')

const showHint = computed(() => query.value.trim().length > 0 && query.value.trim().length < 2)
const visibleResults = computed(() => results.value.slice(0, visibleCount.value))
const hiddenCount = computed(() => Math.max(0, results.value.length - visibleCount.value))

/** Branches the caller may see (committee → its branches). Cached for the session. */
function loadBranches() {
  if (!citiesPromise) {
    citiesPromise = directorCities()
      .then(rows => (Array.isArray(rows) ? rows : [])
        .map(c => ({
          value: String(c.value ?? c.Value ?? '').trim(),
          name: String(c.name ?? c.Name ?? c.value ?? '').trim(),
        }))
        .filter(c => c.value))
      .catch(() => {
        citiesPromise = null

        return []
      })
  }

  return citiesPromise
}

function mergeHits(rows, branch, nq) {
  const seen = new Set(results.value.map(h => `${h.cityValue}:${h.customerId}`))
  const fresh = rows
    .map(r => normHit(r, branch))
    .filter(h => h.customerId > 0 && h.cityValue && !seen.has(`${h.cityValue}:${h.customerId}`))
  if (!fresh.length)
    return

  // Names starting with the query first; otherwise keep arrival order (stable sort).
  const merged = [...results.value, ...fresh]
  const rank = h => (normalizeArabic(h.name).startsWith(nq) ? 0 : 1)
  merged.sort((a, b) => rank(a) - rank(b))
  results.value = merged
  if (activeIndex.value < 0)
    activeIndex.value = 0
}

function isCancel(e) {
  return e?.name === 'CanceledError' || e?.code === 'ERR_CANCELED'
}

async function runSearch(q) {
  controller?.abort()
  controller = new AbortController()
  const signal = controller.signal
  const id = ++runId
  const nq = normalizeArabic(q)

  results.value = []
  visibleCount.value = PAGE
  activeIndex.value = -1
  error.value = ''
  branchesDone.value = 0
  branchesFailed.value = 0
  busy.value = true
  lastQuery = q

  const branches = await loadBranches()
  if (id !== runId)
    return

  // No branch list → one gateway-side fan-out (still scoped server-side).
  const targets = branches.length ? branches : [null]
  branchesTotal.value = branches.length

  await Promise.all(targets.map(async branch => {
    try {
      const rows = await directorGlobalCustomerSearch(q, {
        signal,
        cityValue: branch?.value,
        includeFinished: includeFinished.value,
      })
      if (id === runId)
        mergeHits(rows, branch, nq)
    }
    catch (e) {
      if (isCancel(e) || id !== runId)
        return
      branchesFailed.value++
      if (!branch)
        error.value = e?.response?.data?.message || 'تعذر البحث، حاول مرة أخرى'
    }
    finally {
      if (id === runId)
        branchesDone.value++
    }
  }))

  if (id === runId)
    busy.value = false
}

watch(query, val => {
  clearTimeout(timer)
  const q = String(val || '').trim()
  if (q.length < 2) {
    controller?.abort()
    runId++
    busy.value = false
    results.value = []
    error.value = ''
    lastQuery = ''
    if (!q)
      menuOpen.value = false

    return
  }
  menuOpen.value = true
  if (q === lastQuery)
    return
  busy.value = true
  timer = setTimeout(() => runSearch(q), 300)
})

watch(includeFinished, val => {
  try {
    localStorage.setItem(FINISHED_KEY, val ? '1' : '0')
  }
  catch { /* private mode */ }
  const q = query.value.trim()
  lastQuery = ''
  if (q.length >= 2) {
    clearTimeout(timer)
    runSearch(q)
  }
  fieldRef.value?.focus()
})

function showMore() {
  visibleCount.value += PAGE
  fieldRef.value?.focus()
}

function openHit(hit) {
  if (!hit)
    return
  menuOpen.value = false
  router.push({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: hit.cityValue,
      customerId: String(hit.customerId),
      name: hit.name || '',
      phone: hit.phone || '',
    },
  })
}

function onKeydown(e) {
  const n = visibleResults.value.length
  if (e.key === 'ArrowDown') {
    e.preventDefault()
    menuOpen.value = query.value.trim().length >= 2
    if (n)
      activeIndex.value = (activeIndex.value + 1) % n
  }
  else if (e.key === 'ArrowUp') {
    e.preventDefault()
    if (n)
      activeIndex.value = (activeIndex.value - 1 + n) % n
  }
  else if (e.key === 'Enter') {
    e.preventDefault()
    openHit(visibleResults.value[activeIndex.value] || visibleResults.value[0])
  }
  else if (e.key === 'Escape') {
    menuOpen.value = false
  }
}

watch(activeIndex, async i => {
  await nextTick()
  panelRef.value?.$el?.querySelectorAll('.gcs-item')?.[i]?.scrollIntoView({ block: 'nearest' })
})

function onDocPointer(e) {
  if (menuOpen.value && !rootRef.value?.contains(e.target))
    menuOpen.value = false
}

function onGlobalShortcut(e) {
  if ((e.ctrlKey || e.metaKey) && String(e.key).toLowerCase() === 'k') {
    e.preventDefault()
    fieldRef.value?.focus()
  }
}

onMounted(() => {
  window.addEventListener('keydown', onGlobalShortcut)
  document.addEventListener('mousedown', onDocPointer, true)
  loadBranches()
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onGlobalShortcut)
  document.removeEventListener('mousedown', onDocPointer, true)
  clearTimeout(timer)
  controller?.abort()
})
</script>

<template>
  <div
    ref="rootRef"
    class="gcs-root"
  >
    <VTextField
      ref="fieldRef"
      v-model="query"
      placeholder="بحث عن زبون بالاسم أو الهاتف… (Ctrl+K)"
      prepend-inner-icon="tabler-search"
      density="compact"
      variant="outlined"
      hide-details
      clearable
      autocomplete="off"
      class="gcs-field"
      @focus="menuOpen = query.trim().length >= 2"
      @keydown="onKeydown"
    />

    <VCard
      v-if="menuOpen"
      ref="panelRef"
      class="gcs-panel"
      elevation="12"
    >
      <div class="gcs-head">
        <span class="gcs-head__title">
          {{ results.length ? `${results.length} نتيجة` : 'النتائج' }}
        </span>
        <VSpacer />
        <VSwitch
          v-model="includeFinished"
          label="إظهار المصفرين"
          color="success"
          density="compact"
          hide-details
          inset
          class="gcs-switch"
          @mousedown.prevent
        />
      </div>
      <VProgressLinear
        v-if="busy && branchesTotal"
        :model-value="(branchesDone / branchesTotal) * 100"
        color="primary"
        height="2"
      />
      <VProgressLinear
        v-else-if="busy"
        indeterminate
        color="primary"
        height="2"
      />

      <div
        v-if="showHint"
        class="gcs-state"
      >
        اكتب حرفين على الأقل
      </div>
      <div
        v-else-if="error && !results.length"
        class="gcs-state text-error"
      >
        {{ error }}
      </div>
      <div
        v-else-if="!results.length"
        class="gcs-state"
      >
        {{ busy ? 'جاري البحث في الفروع…' : (includeFinished ? 'لا توجد نتائج' : 'لا توجد نتائج — فعّل «إظهار المصفرين» للبحث فيهم أيضاً') }}
      </div>

      <template v-else>
        <VList
          density="compact"
          class="py-1"
        >
          <VListItem
            v-for="(hit, i) in visibleResults"
            :key="`${hit.cityValue}:${hit.customerId}`"
            :active="i === activeIndex"
            class="gcs-item"
            @click="openHit(hit)"
            @mouseenter="activeIndex = i"
          >
            <div class="gcs-row">
              <span
                class="gcs-name"
                :title="hit.name"
              >{{ hit.name || '—' }}</span>
              <VChip
                v-if="hit.legal"
                size="x-small"
                color="error"
                variant="tonal"
                label
              >
                قانونية
              </VChip>
              <VChip
                v-else-if="hit.finished"
                size="x-small"
                color="success"
                variant="tonal"
                label
                prepend-icon="tabler-circle-check"
              >
                مصفر
              </VChip>
              <VChip
                v-else
                size="x-small"
                color="warning"
                variant="tonal"
                label
              >
                باقي {{ fmtNum(hit.remaining) }}
              </VChip>
            </div>
            <div class="gcs-row gcs-row--meta">
              <span
                class="gcs-meta"
                :title="`${hit.cityName} · ${hit.listName}`"
              >
                <VIcon
                  icon="tabler-map-pin"
                  size="13"
                />
                {{ hit.cityName || '—' }}
                <span class="gcs-dot">·</span>
                <VIcon
                  icon="tabler-list"
                  size="13"
                />
                {{ hit.listName || 'بدون قائمة' }}
              </span>
              <VChip
                v-if="hit.rating && !hit.finished"
                size="x-small"
                :color="RATING_COLORS[hit.rating] || 'secondary'"
                variant="outlined"
                class="gcs-rating"
              >
                {{ hit.rating }}
              </VChip>
            </div>
          </VListItem>
        </VList>

        <div class="gcs-foot">
          <VBtn
            v-if="hiddenCount"
            size="small"
            variant="tonal"
            color="primary"
            @mousedown.prevent
            @click="showMore"
          >
            عرض المزيد ({{ hiddenCount }})
          </VBtn>
          <VSpacer />
          <span class="gcs-progress">
            <template v-if="busy && branchesTotal">
              جاري البحث… {{ branchesDone }} من {{ branchesTotal }} فرع
            </template>
            <template v-else-if="branchesFailed">
              تعذر الوصول إلى {{ branchesFailed }} فرع
            </template>
          </span>
        </div>
      </template>
    </VCard>
  </div>
</template>

<style scoped>
.gcs-root {
  position: relative;
  flex: 1 1 auto;
  min-inline-size: 160px;
  max-inline-size: 560px;
}
/* Wide screens: sit in the exact centre of the navbar. */
@media (min-width: 1200px) {
  .gcs-root {
    position: absolute;
    inset-block-start: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    inline-size: clamp(340px, 32vw, 560px);
    margin: 0 !important;
  }
}
.gcs-field :deep(.v-field) {
  border-radius: 10px;
}

/* Anchored to the field itself (no viewport maths — the app runs under html zoom). */
.gcs-panel {
  position: absolute;
  inset-block-start: calc(100% + 6px);
  left: 50%;
  transform: translateX(-50%);
  inline-size: max(100%, 440px);
  max-inline-size: 94vw;
  max-block-size: min(70vh, 640px);
  overflow-y: auto;
  z-index: 30;
}
@media (max-width: 600px) {
  .gcs-panel {
    inline-size: 100%;
  }
}
.gcs-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 12px 4px 16px;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.gcs-head__title {
  font-size: 0.78rem;
  opacity: 0.7;
}
.gcs-switch {
  flex: 0 0 auto;
}
.gcs-switch :deep(.v-label) {
  font-size: 0.8rem;
  opacity: 0.85;
}
.gcs-state {
  padding: 14px 16px;
  font-size: 0.85rem;
  opacity: 0.75;
}
.gcs-item {
  padding-block: 6px !important;
}
.gcs-row {
  display: flex;
  align-items: center;
  gap: 8px;
  min-inline-size: 0;
}
.gcs-row--meta {
  margin-top: 3px;
}
.gcs-name {
  flex: 1 1 auto;
  min-inline-size: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 600;
}
.gcs-meta {
  flex: 1 1 auto;
  min-inline-size: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.78rem;
  opacity: 0.7;
}
.gcs-dot {
  margin-inline: 4px;
}
.gcs-foot {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px 10px;
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.gcs-progress {
  font-size: 0.75rem;
  opacity: 0.65;
}

/* Narrow panels: drop the rating chip so name/branch/list keep their room. */
@media (max-width: 480px) {
  .gcs-rating {
    display: none;
  }
}
</style>
