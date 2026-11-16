<template>
  <div class="qa-container py-4 pb-10">
    <div class="d-flex align-center flex-wrap ga-2 mb-3">
      <h1 class="text-h6 text-md-h5 font-weight-bold">
        {{ heading }}
      </h1>
      <v-chip v-if="total !== null && !loading" size="small" variant="tonal" color="primary">{{ total }} منتج</v-chip>
      <v-spacer />
      <v-select
        v-model="filters.sort"
        :items="sortItems"
        density="compact"
        hide-details
        variant="outlined"
        class="sort-select"
        prepend-inner-icon="mdi-sort"
        aria-label="ترتيب"
      />
      <v-btn v-if="!mdAndUp" variant="tonal" color="primary" prepend-icon="mdi-filter-variant" @click="sheet = true">
        تصفية<v-badge v-if="activeFilterCount" :content="activeFilterCount" inline color="secondary" text-color="black" />
      </v-btn>
    </div>

    <!-- Category chips -->
    <div class="cat-chips hide-scrollbar mb-3">
      <v-chip :color="!filters.categoryId ? 'primary' : undefined" :variant="!filters.categoryId ? 'flat' : 'outlined'" @click="filters.categoryId = null">الكل</v-chip>
      <v-chip
        v-for="c in catalog.categories"
        :key="c.id"
        :color="filters.categoryId === c.id ? 'primary' : undefined"
        :variant="filters.categoryId === c.id ? 'flat' : 'outlined'"
        @click="filters.categoryId = filters.categoryId === c.id ? null : c.id"
      >
        {{ c.name }}
      </v-chip>
    </div>

    <v-row>
      <v-col v-if="mdAndUp" md="3">
        <v-card class="soft-card pa-4 filters-card" rounded="xl">
          <CatalogFilters :model-value="filters" @update:model-value="(v) => Object.assign(filters, v)" :branches="catalog.branches" @reset="resetFilters" />
        </v-card>
      </v-col>
      <v-col cols="12" :md="mdAndUp ? 9 : 12">
        <div v-if="filters.q" class="mb-3 text-body-2">
          نتائج البحث عن: <b>«{{ filters.q }}»</b>
          <v-btn size="x-small" variant="text" color="primary" @click="filters.q = ''">مسح البحث</v-btn>
        </div>
        <ErrorState v-if="error && !items.length" :message="error" @retry="load(true)" />
        <EmptyState
          v-else-if="!loading && !items.length"
          icon="mdi-magnify-close"
          title="لا توجد منتجات مطابقة"
          text="جرّب تغيير كلمات البحث أو إزالة بعض عوامل التصفية."
        >
          <v-btn color="primary" variant="tonal" @click="resetFilters">إزالة عوامل التصفية</v-btn>
        </EmptyState>
        <template v-else>
          <ProductGrid :items="items" :loading="loading && !items.length" :cols="{ cols: 6, sm: 4, md: 4, lg: 3 }" :skeletons="9" />
          <div ref="sentinel" class="py-6 d-flex justify-center">
            <v-progress-circular v-if="loading && items.length" indeterminate color="primary" />
            <v-btn v-else-if="hasMore" variant="tonal" color="primary" @click="loadMore">عرض المزيد</v-btn>
            <span v-else-if="items.length" class="text-caption text-medium-emphasis">وصلت إلى نهاية القائمة</span>
          </div>
          <v-alert v-if="error && items.length" type="error" variant="tonal" density="compact">{{ error }}</v-alert>
        </template>
      </v-col>
    </v-row>

    <v-bottom-sheet v-model="sheet" inset>
      <v-card class="pa-5" rounded="t-xl">
        <div class="d-flex align-center mb-3">
          <div class="text-h6 font-weight-bold">تصفية النتائج</div>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" aria-label="إغلاق" @click="sheet = false" />
        </div>
        <CatalogFilters :model-value="filters" @update:model-value="(v) => Object.assign(filters, v)" :branches="catalog.branches" @reset="resetFilters" />
        <v-btn block color="primary" class="mt-4" size="large" @click="sheet = false">عرض النتائج</v-btn>
      </v-card>
    </v-bottom-sheet>
  </div>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useDisplay } from 'vuetify'
import ProductGrid from '@/components/ProductGrid.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import CatalogFilters from '@/components/CatalogFilters.vue'
import { useCatalogStore } from '@/stores/catalog'
import { publicApi } from '@/api'
import { apiErrorMessage } from '@/utils/errors'

const PAGE_SIZE = 24
const { mdAndUp } = useDisplay()
const route = useRoute()
const router = useRouter()
const catalog = useCatalogStore()
catalog.loadCategories().catch(() => {})
catalog.loadBranches().catch(() => {})

const sortItems = [
  { title: 'الأحدث', value: 'newest' },
  { title: 'الأكثر طلباً', value: 'popular' },
  { title: 'السعر: من الأقل', value: 'priceAsc' },
  { title: 'السعر: من الأعلى', value: 'priceDesc' },
]

const fromQuery = (q) => ({
  q: q.q || '',
  categoryId: q.categoryId ? Number(q.categoryId) : null,
  cityValue: q.cityValue ? Number(q.cityValue) : null,
  purchaseType: q.purchaseType || null,
  sort: q.sort || 'newest',
})
const filters = reactive(fromQuery(route.query))
const items = ref([])
const total = ref(null)
const page = ref(0)
const loading = ref(false)
const error = ref('')
const sheet = ref(false)
const sentinel = ref()
let reqSeq = 0

const hasMore = computed(() => total.value !== null && items.value.length < total.value)
const activeFilterCount = computed(() => [filters.cityValue, filters.purchaseType].filter(Boolean).length)
const heading = computed(() => (filters.categoryId ? catalog.categoryName(filters.categoryId) || 'المنتجات' : 'جميع المنتجات'))

async function load(reset = false) {
  if (reset) {
    page.value = 0
    items.value = []
    total.value = null
  }
  const seq = ++reqSeq
  loading.value = true
  error.value = ''
  try {
    const res = await publicApi.products({
      q: filters.q?.trim(),
      categoryId: filters.categoryId,
      cityValue: filters.cityValue,
      purchaseType: filters.purchaseType,
      sort: filters.sort,
      page: page.value + 1,
      pageSize: PAGE_SIZE,
    })
    if (seq !== reqSeq) return
    page.value = res.page
    total.value = res.total
    const ids = new Set(items.value.map((i) => i.id))
    items.value = [...items.value, ...res.items.filter((i) => !ids.has(i.id))]
  } catch (e) {
    if (seq === reqSeq) error.value = apiErrorMessage(e)
  } finally {
    if (seq === reqSeq) loading.value = false
  }
}
function loadMore() {
  if (!loading.value && hasMore.value) load(false)
}
function resetFilters() {
  Object.assign(filters, { q: '', categoryId: null, cityValue: null, purchaseType: null, sort: 'newest' })
}

// filters → URL
watch(
  () => ({ ...filters }),
  (f) => {
    const query = {}
    if (f.q) query.q = f.q
    if (f.categoryId) query.categoryId = String(f.categoryId)
    if (f.cityValue) query.cityValue = String(f.cityValue)
    if (f.purchaseType) query.purchaseType = f.purchaseType
    if (f.sort && f.sort !== 'newest') query.sort = f.sort
    router.replace({ query })
  },
)
// URL → filters + reload
watch(
  () => route.query,
  (q) => {
    if (route.name !== 'catalog') return
    Object.assign(filters, fromQuery(q))
    load(true)
  },
)

// infinite scroll
let observer
onMounted(() => {
  load(true)
  observer = new IntersectionObserver((entries) => entries.some((e) => e.isIntersecting) && loadMore(), { rootMargin: '400px' })
  const stop = watch(sentinel, (el) => el && observer.observe(el), { immediate: true })
  onBeforeUnmount(stop)
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<style scoped>
.sort-select { max-width: 210px; min-width: 160px; }
.filters-card { position: sticky; top: 88px; }
.cat-chips { display: flex; gap: 8px; overflow-x: auto; padding-bottom: 4px; }
.cat-chips .v-chip { flex: 0 0 auto; }
</style>
