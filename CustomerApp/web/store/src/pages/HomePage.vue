<template>
  <div class="pb-10">
    <!-- Hero banners -->
    <section class="qa-container pt-4">
      <v-skeleton-loader v-if="bannersLoading" type="image" class="hero-skeleton rounded-xl" />
      <v-carousel
        v-else-if="catalog.banners.length"
        :height="mdAndUp ? 380 : 200"
        cycle
        interval="6000"
        hide-delimiter-background
        :show-arrows="mdAndUp ? 'hover' : false"
        delimiter-icon="mdi-circle"
        class="hero rounded-xl"
      >
        <v-carousel-item v-for="b in catalog.banners" :key="b.id" :src="b.imageUrl" cover @click="openBanner(b)" :class="{ clickable: bannerHasLink(b) }">
          <div class="hero-overlay d-flex flex-column justify-center">
            <div class="hero-text">
              <h2 class="hero-title">{{ b.title }}</h2>
              <p v-if="b.subtitle" class="hero-sub">{{ b.subtitle }}</p>
              <v-btn v-if="bannerHasLink(b)" color="secondary" variant="flat" :size="mdAndUp ? 'large' : 'small'" class="mt-2 text-black" append-icon="mdi-arrow-left">تسوّق الآن</v-btn>
            </div>
          </div>
        </v-carousel-item>
      </v-carousel>
      <div v-else class="hero-fallback rounded-xl bg-brand-gradient d-flex flex-column justify-center pa-6 pa-md-12">
        <h1 class="text-h5 text-md-h3 font-weight-bold mb-2">تسوّق نقداً أو بالأقساط اليومية</h1>
        <p class="text-body-1 mb-4">أجهزة كهربائية وأثاث ومستلزمات المحلات من قلعة الضمان.</p>
        <div><v-btn color="secondary" class="text-black" :to="{ name: 'catalog' }">تصفّح المنتجات</v-btn></div>
      </div>
    </section>

    <!-- Feature strip -->
    <section class="qa-container mt-4">
      <div class="features hide-scrollbar">
        <div v-for="f in features" :key="f.title" class="feature soft-card">
          <v-avatar color="primary" variant="tonal" size="40"><v-icon>{{ f.icon }}</v-icon></v-avatar>
          <div>
            <div class="font-weight-bold text-body-2">{{ f.title }}</div>
            <div class="text-caption text-medium-emphasis">{{ f.text }}</div>
          </div>
        </div>
      </div>
    </section>

    <!-- Categories -->
    <section class="qa-container mt-8">
      <div class="d-flex align-center justify-space-between mb-3">
        <h2 class="section-title">الأقسام</h2>
        <v-btn variant="text" color="primary" :to="{ name: 'catalog' }" append-icon="mdi-chevron-left">عرض الكل</v-btn>
      </div>
      <div class="cats hide-scrollbar">
        <template v-if="!catalog.categories.length && catsLoading">
          <v-skeleton-loader v-for="n in 6" :key="n" type="avatar, text" class="cat-skel" />
        </template>
        <router-link v-for="c in catalog.categories" :key="c.id" :to="{ name: 'catalog', query: { categoryId: c.id } }" class="cat">
          <v-avatar size="64" class="cat-icon" rounded="xl">
            <v-img v-if="c.iconUrl" :src="c.iconUrl" :alt="c.name" cover />
            <v-icon v-else color="primary">mdi-shape-outline</v-icon>
          </v-avatar>
          <span class="cat-name">{{ c.name }}</span>
          <span class="text-caption text-medium-emphasis">{{ c.productCount }} منتج</span>
        </router-link>
      </div>
    </section>

    <!-- Installments notice -->
    <section class="qa-container mt-6">
      <v-alert color="primary" variant="tonal" icon="mdi-store-outline" class="font-weight-medium" border="start">
        <b>{{ INSTALLMENTS_NOTICE }}</b>
        <div class="text-body-2 mt-1">يمكن لغير أصحاب المحلات الشراء نقداً. يتواصل معك مندوب المبيعات بعد تقديم الطلب.</div>
      </v-alert>
    </section>

    <!-- Featured -->
    <section class="qa-container mt-8">
      <div class="d-flex align-center justify-space-between mb-3">
        <h2 class="section-title">منتجات مميزة</h2>
        <v-btn variant="text" color="primary" :to="{ name: 'catalog', query: { sort: 'popular' } }" append-icon="mdi-chevron-left">المزيد</v-btn>
      </div>
      <ErrorState v-if="featuredError" :message="featuredError" @retry="loadFeatured" />
      <ProductGrid v-else :items="featured" :loading="featuredLoading" :skeletons="4" />
    </section>

    <!-- Referral promo -->
    <section v-if="catalog.referralEnabled" class="qa-container mt-10">
      <v-card class="bg-brand-gradient pa-6 pa-md-8" rounded="xl" flat>
        <v-row align="center">
          <v-col cols="12" md="8">
            <div class="text-h6 text-md-h5 font-weight-bold mb-2"><v-icon class="me-1">mdi-gift-outline</v-icon> ادعُ أصدقاءك واربح عمولة</div>
            <p class="text-body-2 text-md-body-1 mb-0" style="opacity: 0.9">
              شارك رابطك الخاص، واحصل على عمولة عند إتمام عمليات بيع حقيقية عبر رابطك وشبكتك. الانضمام مجاني بالكامل.
            </p>
          </v-col>
          <v-col cols="12" md="4" class="text-md-end">
            <v-btn color="secondary" class="text-black" size="large" @click="goNetwork">ابدأ الآن</v-btn>
            <div><router-link :to="{ name: 'terms', hash: '#referral' }" class="text-white text-caption">شروط برنامج الإحالة</router-link></div>
          </v-col>
        </v-row>
      </v-card>
    </section>

    <!-- Newest -->
    <section class="qa-container mt-10">
      <div class="d-flex align-center justify-space-between mb-3">
        <h2 class="section-title">وصل حديثاً</h2>
        <v-btn variant="text" color="primary" :to="{ name: 'catalog', query: { sort: 'newest' } }" append-icon="mdi-chevron-left">المزيد</v-btn>
      </div>
      <ErrorState v-if="newestError" :message="newestError" @retry="loadNewest" />
      <ProductGrid v-else :items="newest" :loading="newestLoading" :skeletons="8" />
    </section>

    <!-- App download -->
    <section class="qa-container mt-10">
      <v-card class="soft-card pa-6" rounded="xl">
        <v-row align="center">
          <v-col cols="12" md="7">
            <div class="text-h6 font-weight-bold mb-1">حمّل تطبيق قلعة الضمان</div>
            <div class="text-body-2 text-medium-emphasis">تابع طلباتك وأقساطك وأرباحك واستلم الإشعارات أولاً بأول.</div>
          </v-col>
          <v-col cols="12" md="5" class="d-flex justify-md-end"><AppStoreBadges /></v-col>
        </v-row>
      </v-card>
    </section>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useDisplay } from 'vuetify'
import ProductGrid from '@/components/ProductGrid.vue'
import ErrorState from '@/components/ErrorState.vue'
import AppStoreBadges from '@/components/AppStoreBadges.vue'
import { useCatalogStore } from '@/stores/catalog'
import { useAuthStore } from '@/stores/auth'
import { publicApi } from '@/api'
import { apiErrorMessage } from '@/utils/errors'
import { INSTALLMENTS_NOTICE } from '@/utils/enums'
import { toast } from '@/composables/useToast'

const { mdAndUp } = useDisplay()
const catalog = useCatalogStore()
const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const bannersLoading = ref(!catalog.banners.length)
const catsLoading = ref(!catalog.categories.length)
const featured = ref([])
const featuredLoading = ref(true)
const featuredError = ref('')
const newest = ref([])
const newestLoading = ref(true)
const newestError = ref('')

const features = [
  { icon: 'mdi-cash', title: 'الدفع نقداً', text: 'بأفضل الأسعار' },
  { icon: 'mdi-calendar-clock', title: 'أقساط يومية', text: 'لأصحاب المحلات التجارية' },
  { icon: 'mdi-truck-delivery-outline', title: 'توصيل وتركيب', text: 'داخل المحافظة' },
  { icon: 'mdi-shield-check-outline', title: 'ضمان رسمي', text: 'على جميع الأجهزة' },
]

async function loadFeatured() {
  featuredLoading.value = true
  featuredError.value = ''
  try {
    // The contract has no "featured" filter: take popular products and prefer isFeatured ones.
    const res = await publicApi.products({ sort: 'popular', page: 1, pageSize: 40 })
    const f = res.items.filter((p) => p.isFeatured)
    featured.value = (f.length >= 4 ? f : [...f, ...res.items.filter((p) => !p.isFeatured)]).slice(0, 8)
  } catch (e) {
    featuredError.value = apiErrorMessage(e)
  } finally {
    featuredLoading.value = false
  }
}
async function loadNewest() {
  newestLoading.value = true
  newestError.value = ''
  try {
    newest.value = (await publicApi.products({ sort: 'newest', page: 1, pageSize: 8 })).items
  } catch (e) {
    newestError.value = apiErrorMessage(e)
  } finally {
    newestLoading.value = false
  }
}

const bannerHasLink = (b) => !!(b.productId || b.categoryId || b.externalUrl)
function openBanner(b) {
  if (b.productId) router.push({ name: 'product', params: { id: b.productId } })
  else if (b.categoryId) router.push({ name: 'catalog', query: { categoryId: b.categoryId } })
  else if (b.externalUrl) window.open(b.externalUrl, '_blank', 'noopener')
}
function goNetwork() {
  if (auth.isLoggedIn) router.push({ name: 'network' })
  else auth.openLogin({ name: 'network' }, 'سجّل دخولك للحصول على رابط الإحالة الخاص بك')
}

onMounted(() => {
  if (route.query.ref === 'already') toast('أنت مسجّل بالفعل، لا حاجة لرمز الإحالة', 'info')
  catalog.loadBanners().catch(() => {}).finally(() => (bannersLoading.value = false))
  catalog.loadCategories().catch(() => {}).finally(() => (catsLoading.value = false))
  loadFeatured()
  loadNewest()
})
</script>

<style scoped>
.hero { overflow: hidden; }
.hero-skeleton :deep(.v-skeleton-loader__image) { height: 380px; }
@media (max-width: 959px) { .hero-skeleton :deep(.v-skeleton-loader__image) { height: 200px; } }
.hero-fallback { min-height: 220px; }
.clickable { cursor: pointer; }
.hero-overlay {
  position: absolute; inset: 0;
  background: linear-gradient(to left, rgba(0, 0, 0, 0.45) 0%, rgba(0, 0, 0, 0.15) 55%, rgba(0, 0, 0, 0) 100%);
}
.hero-text { padding: 0 6%; max-width: 640px; color: #fff; }
.hero-title { font-size: clamp(1.3rem, 3.6vw, 2.6rem); font-weight: 800; line-height: 1.3; text-shadow: 0 2px 10px rgba(0, 0, 0, 0.3); }
.hero-sub { font-size: clamp(0.85rem, 1.6vw, 1.15rem); opacity: 0.95; margin-top: 4px; }
.features { display: grid; grid-auto-flow: column; grid-auto-columns: minmax(200px, 1fr); gap: 12px; overflow-x: auto; }
.feature { display: flex; align-items: center; gap: 12px; padding: 12px 14px; border-radius: 16px; background: #fff; }
.cats { display: flex; gap: 12px; overflow-x: auto; padding-bottom: 6px; }
.cat {
  flex: 0 0 auto; width: 112px; display: flex; flex-direction: column; align-items: center; gap: 6px;
  text-decoration: none; color: inherit; padding: 12px 6px; border-radius: 18px; background: #fff;
  border: 1px solid rgba(0, 0, 0, 0.06); transition: border-color 0.15s, transform 0.15s;
}
.cat:hover { border-color: #006a64; transform: translateY(-2px); }
.cat-name { font-weight: 700; font-size: 0.82rem; text-align: center; line-height: 1.4; }
.cat-skel { width: 112px; flex: 0 0 auto; }
</style>
