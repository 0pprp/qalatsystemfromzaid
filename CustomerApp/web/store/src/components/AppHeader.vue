<template>
  <v-app-bar :elevation="scrolled ? 3 : 0" color="surface" class="qa-header" :height="mdAndUp ? 72 : 60" :extension-height="smAndDown ? 60 : 0">
    <div class="qa-container d-flex align-center ga-2 ga-md-4">
      <router-link :to="{ name: 'home' }" class="d-flex align-center text-decoration-none brand" aria-label="قلعة الضمان - الرئيسية">
        <img src="/LogoCompany.png" alt="قلعة الضمان" class="brand-logo" />
        <div class="d-flex flex-column ms-2">
          <span class="brand-name">قلعة الضمان</span>
          <span v-if="mdAndUp" class="brand-tag">نقداً أو بالأقساط اليومية</span>
        </div>
      </router-link>

      <nav v-if="mdAndUp" class="d-flex ga-1 ms-4">
        <v-btn variant="text" :to="{ name: 'home' }" exact>الرئيسية</v-btn>
        <v-btn variant="text" :to="{ name: 'catalog' }">المنتجات</v-btn>
      </nav>

      <form v-if="mdAndUp" class="flex-grow-1 mx-4" role="search" @submit.prevent="doSearch">
        <SearchField v-model="q" @submit="doSearch" />
      </form>
      <v-spacer v-else />

      <v-btn icon variant="text" :to="{ name: 'cart' }" aria-label="السلة">
        <v-badge :content="cart.count" :model-value="cart.count > 0" color="secondary" text-color="black">
          <v-icon>mdi-cart-outline</v-icon>
        </v-badge>
      </v-btn>

      <template v-if="auth.isLoggedIn">
        <v-btn icon variant="text" :to="{ name: 'notifications' }" aria-label="الإشعارات">
          <v-badge :content="auth.unreadCount > 99 ? '99+' : auth.unreadCount" :model-value="auth.unreadCount > 0" color="error">
            <v-icon>mdi-bell-outline</v-icon>
          </v-badge>
        </v-btn>
        <v-menu location="bottom end" min-width="240">
          <template #activator="{ props }">
            <v-btn v-bind="props" :icon="smAndDown" variant="tonal" color="primary" class="ms-1" aria-label="قائمة الحساب">
              <v-icon :start="!smAndDown">mdi-account-circle</v-icon>
              <span v-if="!smAndDown">{{ auth.user?.firstName }}</span>
            </v-btn>
          </template>
          <v-list density="comfortable" nav>
            <v-list-item :title="auth.user?.fullName" :subtitle="auth.user?.phone" class="mb-1">
              <template #prepend><v-avatar color="primary" size="36">{{ initials }}</v-avatar></template>
            </v-list-item>
            <v-divider class="mb-1" />
            <v-list-item v-for="i in accountLinks" :key="i.to" :to="{ name: i.to }" :prepend-icon="i.icon" :title="i.title" />
            <v-divider class="my-1" />
            <v-list-item prepend-icon="mdi-logout" title="تسجيل الخروج" base-color="error" @click="logout" />
          </v-list>
        </v-menu>
      </template>
      <v-btn v-else color="primary" variant="flat" :size="smAndDown ? 'small' : 'default'" @click="auth.openLogin(null)">
        <v-icon start>mdi-login</v-icon>دخول
      </v-btn>
    </div>

    <template v-if="smAndDown" #extension>
      <form class="qa-container pb-2" role="search" @submit.prevent="doSearch">
        <SearchField v-model="q" @submit="doSearch" />
      </form>
    </template>
  </v-app-bar>
</template>

<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useDisplay } from 'vuetify'
import SearchField from './SearchField.vue'
import { useAuthStore } from '@/stores/auth'
import { useCartStore } from '@/stores/cart'
import { toast } from '@/composables/useToast'
import { ACCOUNT_LINKS } from '@/pages/account/links'

const { mdAndUp, smAndDown } = useDisplay()
const auth = useAuthStore()
const cart = useCartStore()
const router = useRouter()
const route = useRoute()
const q = ref(route.query.q || '')
const scrolled = ref(false)
const accountLinks = ACCOUNT_LINKS

watch(() => route.query.q, (v) => (q.value = v || ''))

const initials = computed(() => auth.user?.firstName?.slice(0, 1) || '؟')

function doSearch() {
  const query = { ...(route.name === 'catalog' ? route.query : {}), q: q.value || undefined, page: undefined }
  router.push({ name: 'catalog', query })
}
async function logout() {
  await auth.logout()
  toast('تم تسجيل الخروج')
  router.push({ name: 'home' })
}
const onScroll = () => (scrolled.value = window.scrollY > 4)
onMounted(() => window.addEventListener('scroll', onScroll, { passive: true }))
onBeforeUnmount(() => window.removeEventListener('scroll', onScroll))
</script>

<style scoped>
.qa-header { border-bottom: 1px solid rgba(0, 0, 0, 0.06); }
.qa-header :deep(.v-toolbar__content), .qa-header :deep(.v-toolbar__extension) { padding: 0; }
.brand-logo { height: 46px; width: auto; object-fit: contain; }
.brand-name { font-weight: 800; color: #006a64; font-size: 1.15rem; line-height: 1.2; white-space: nowrap; }
.brand-tag { font-size: 0.72rem; color: #5f7a78; line-height: 1.1; white-space: nowrap; }
@media (max-width: 959px) {
  .brand-logo { height: 38px; }
  .brand-name { font-size: 1rem; }
}
</style>
