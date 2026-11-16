<template>
  <div class="qa-container py-4 pb-12">
    <!-- Loading -->
    <v-row v-if="loading">
      <v-col cols="12" md="6"><v-skeleton-loader type="image" class="rounded-xl" height="420" /></v-col>
      <v-col cols="12" md="6"><v-skeleton-loader type="heading, subtitle, paragraph, button, button, table-row-divider@3" /></v-col>
    </v-row>

    <EmptyState v-else-if="notFound" icon="mdi-package-variant-remove" title="المنتج غير موجود" text="ربما تمت إزالة هذا المنتج أو أن الرابط غير صحيح.">
      <v-btn color="primary" :to="{ name: 'catalog' }">تصفّح المنتجات</v-btn>
    </EmptyState>
    <ErrorState v-else-if="error" :message="error" @retry="load" />

    <template v-else-if="p">
      <v-breadcrumbs :items="crumbs" class="px-0 pt-0 text-body-2" divider="/" />
      <v-row>
        <v-col cols="12" md="6">
          <MediaGallery :media="gallery" :alt="p.name" class="gallery-sticky" />
        </v-col>
        <v-col cols="12" md="6">
          <div class="d-flex align-center flex-wrap ga-2 mb-2">
            <v-chip size="small" variant="tonal" color="primary" :to="{ name: 'catalog', query: { categoryId: p.categoryId } }">{{ p.categoryName }}</v-chip>
            <v-chip v-if="p.badge" size="small" color="secondary" variant="flat" class="text-black">{{ p.badge }}</v-chip>
          </div>
          <h1 class="text-h5 text-md-h4 font-weight-bold mb-4" style="line-height: 1.5">{{ p.name }}</h1>

          <!-- Prices -->
          <v-card class="soft-card mb-4" rounded="xl">
            <v-card-text class="pa-4">
              <div class="price-row" v-if="p.allowCash">
                <div>
                  <div class="text-caption text-medium-emphasis">السعر نقداً</div>
                  <div class="cash num">{{ formatMoney(p.cashPrice) }}</div>
                </div>
                <v-icon color="primary" size="32">mdi-cash</v-icon>
              </div>
              <v-alert v-else type="info" variant="tonal" density="compact" class="mb-2">هذا المنتج غير متاح بالدفع النقدي.</v-alert>

              <template v-if="p.allowInstallments">
                <v-divider class="my-3" />
                <div class="price-row">
                  <div>
                    <div class="text-caption text-medium-emphasis">بالأقساط اليومية</div>
                    <div class="daily num">
                      {{ formatMoney(p.dailyInstallment) }} <span class="text-body-2 text-medium-emphasis">يومياً × {{ p.installmentDays }} يوم</span>
                    </div>
                    <div class="text-body-2 text-medium-emphasis">السعر الكلي بالأقساط: <b class="num">{{ formatMoney(p.installmentPrice) }}</b></div>
                  </div>
                  <v-icon color="primary" size="32">mdi-calendar-clock</v-icon>
                </div>
                <v-alert type="warning" variant="tonal" density="compact" class="mt-3" icon="mdi-store-alert-outline">
                  <b>{{ catalog.installmentsNotice || INSTALLMENTS_NOTICE }}</b>
                </v-alert>
              </template>
              <v-alert v-else type="info" variant="tonal" density="compact" class="mt-3">هذا المنتج متاح بالدفع النقدي فقط.</v-alert>

              <div v-if="commission" class="commission mt-3">
                <v-icon color="success" size="18" class="me-1">mdi-cash-plus</v-icon>
                عمولتك عند إتمام البيع عبر رابطك: <b class="num">{{ formatMoney(commission) }}</b>
              </div>
            </v-card-text>
          </v-card>

          <!-- Actions -->
          <div class="d-flex align-center ga-3 mb-4 flex-wrap">
            <div class="qty">
              <v-btn icon="mdi-minus" size="small" variant="tonal" :disabled="qty <= 1" aria-label="إنقاص الكمية" @click="qty--" />
              <span class="num font-weight-bold mx-3" aria-live="polite">{{ qty }}</span>
              <v-btn icon="mdi-plus" size="small" variant="tonal" :disabled="qty >= MAX_LINE_QTY" aria-label="زيادة الكمية" @click="qty++" />
            </div>
            <v-btn color="primary" size="large" class="flex-grow-1" prepend-icon="mdi-flash" @click="orderNow">اطلب الآن</v-btn>
            <v-btn color="primary" variant="outlined" size="large" class="flex-grow-1" prepend-icon="mdi-cart-plus" @click="addToCart">أضف إلى السلة</v-btn>
          </div>

          <!-- Branches -->
          <div class="mb-4">
            <div class="font-weight-bold mb-2"><v-icon size="20" color="primary" class="me-1">mdi-map-marker-multiple-outline</v-icon>متوفر في الفروع</div>
            <div v-if="p.availableBranches?.length" class="d-flex flex-wrap ga-2">
              <v-chip v-for="b in p.availableBranches" :key="b.cityValue" size="small" variant="outlined">{{ b.name }}</v-chip>
            </div>
            <div v-else class="text-body-2 text-medium-emphasis">غير متوفر حالياً في أي فرع.</div>
          </div>

          <ShareButtons :url="shareUrl" :title="p.name" :text="shareText" label="شارك المنتج:" class="mb-2" />
        </v-col>
      </v-row>

      <!-- Details -->
      <v-row class="mt-4">
        <v-col cols="12" md="7">
          <v-card class="soft-card" rounded="xl">
            <v-card-title class="font-weight-bold pt-4">الوصف</v-card-title>
            <v-card-text class="text-body-1 text-pre" style="line-height: 2">{{ p.description || 'لا يوجد وصف.' }}</v-card-text>
            <template v-if="p.extraInfo">
              <v-divider />
              <v-card-title class="font-weight-bold text-subtitle-1">معلومات إضافية</v-card-title>
              <v-card-text class="text-body-2 text-pre" style="line-height: 2">{{ p.extraInfo }}</v-card-text>
            </template>
          </v-card>
        </v-col>
        <v-col cols="12" md="5">
          <v-card class="soft-card" rounded="xl">
            <v-card-title class="font-weight-bold pt-4">المواصفات</v-card-title>
            <v-table v-if="p.specs?.length" density="comfortable" class="specs">
              <tbody>
                <tr v-for="s in p.specs" :key="s.name">
                  <th class="text-medium-emphasis">{{ s.name }}</th>
                  <td class="font-weight-medium">{{ s.value }}</td>
                </tr>
              </tbody>
            </v-table>
            <v-card-text v-else class="text-medium-emphasis">لا توجد مواصفات مسجّلة.</v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <section v-if="related.length" class="mt-10">
        <h2 class="section-title mb-3">منتجات مشابهة</h2>
        <ProductGrid :items="related" />
      </section>
    </template>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import MediaGallery from '@/components/MediaGallery.vue'
import ShareButtons from '@/components/ShareButtons.vue'
import ProductGrid from '@/components/ProductGrid.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import { useCatalogStore } from '@/stores/catalog'
import { useCartStore } from '@/stores/cart'
import { useAuthStore } from '@/stores/auth'
import { publicApi, meApi } from '@/api'
import { formatMoney } from '@/utils/money'
import { apiErrorMessage } from '@/utils/errors'
import { INSTALLMENTS_NOTICE } from '@/utils/enums'
import { MAX_LINE_QTY } from '@/utils/orderPayload'
import { setHead } from '@/composables/useHead'
import { toast } from '@/composables/useToast'

const route = useRoute()
const router = useRouter()
const catalog = useCatalogStore()
const cart = useCartStore()
const auth = useAuthStore()

const p = ref(null)
const loading = ref(true)
const error = ref('')
const notFound = ref(false)
const qty = ref(1)
const related = ref([])
const commission = ref(null)

const gallery = computed(() => {
  const media = p.value?.media?.length ? p.value.media : p.value?.thumbnailUrl ? [{ type: 'image', url: p.value.thumbnailUrl }] : []
  return media
})
const shareUrl = computed(() => {
  if (p.value?.shareUrl) return p.value.shareUrl
  const base = window.location.origin
  return `${base}/p/${route.params.id}`
})
const shareText = computed(() => {
  if (!p.value) return ''
  const parts = [p.value.name]
  if (p.value.allowCash) parts.push(`نقداً ${formatMoney(p.value.cashPrice)}`)
  if (p.value.allowInstallments) parts.push(`أو ${formatMoney(p.value.dailyInstallment)} يومياً`)
  return parts.join(' — ') + ' | قلعة الضمان'
})
const crumbs = computed(() => [
  { title: 'الرئيسية', to: { name: 'home' } },
  { title: p.value?.categoryName || 'المنتجات', to: { name: 'catalog', query: p.value ? { categoryId: p.value.categoryId } : {} } },
  { title: p.value?.name || '', disabled: true },
])

async function load() {
  loading.value = true
  error.value = ''
  notFound.value = false
  try {
    p.value = await catalog.getProduct(route.params.id, true)
    const img = p.value.media?.find((m) => m.type === 'image')?.url || p.value.thumbnailUrl
    setHead({ title: p.value.name, description: `${p.value.name} — ${p.value.description || ''}`, image: img, url: shareUrl.value })
    loadRelated()
    loadCommission()
  } catch (e) {
    if (e.response?.status === 404) {
      notFound.value = true
      setHead({ title: 'المنتج غير موجود' })
    } else error.value = apiErrorMessage(e)
  } finally {
    loading.value = false
  }
}
async function loadRelated() {
  try {
    const res = await publicApi.products({ categoryId: p.value.categoryId, sort: 'popular', page: 1, pageSize: 9 })
    related.value = res.items.filter((x) => x.id !== p.value.id).slice(0, 4)
  } catch { /* optional */ }
}
async function loadCommission() {
  if (!auth.isLoggedIn || !catalog.referralEnabled) return
  try {
    const r = await meApi.productCommission(p.value.id)
    commission.value = r.baseCommission || null
  } catch { /* optional */ }
}

function addToCart() {
  const r = cart.add(p.value, qty.value)
  toast(r.message, r.ok ? 'success' : 'error')
  return r.ok
}
function orderNow() {
  if (!cart.has(p.value.id)) {
    if (!addToCart()) return
  }
  if (auth.isLoggedIn) router.push({ name: 'checkout' })
  else auth.openLogin({ name: 'checkout' }, 'سجّل دخولك لإكمال الطلب. سلتك محفوظة.')
}

onMounted(load)
</script>

<style scoped>
.price-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.cash { font-size: 1.7rem; font-weight: 800; color: #006a64; }
.daily { font-size: 1.35rem; font-weight: 800; color: #1d2b2a; }
.qty { display: flex; align-items: center; border: 1px solid rgba(0, 0, 0, 0.12); border-radius: 12px; padding: 4px 8px; }
.commission { background: #e8f5e9; border-radius: 10px; padding: 8px 12px; font-size: 0.9rem; }
.specs th { width: 40%; font-weight: 600; }
@media (min-width: 960px) { .gallery-sticky { position: sticky; top: 92px; } }
</style>
