<template>
  <v-bottom-navigation grow color="primary" height="62" class="qa-bottom-nav" :elevation="8" mode="shift" :model-value="active">
    <v-btn value="home" :to="{ name: 'home' }" exact>
      <v-icon>mdi-home-outline</v-icon><span>الرئيسية</span>
    </v-btn>
    <v-btn value="catalog" :to="{ name: 'catalog' }">
      <v-icon>mdi-view-grid-outline</v-icon><span>المنتجات</span>
    </v-btn>
    <v-btn value="cart" :to="{ name: 'cart' }">
      <v-badge :content="cart.count" :model-value="cart.count > 0" color="secondary" text-color="black">
        <v-icon>mdi-cart-outline</v-icon>
      </v-badge>
      <span>السلة</span>
    </v-btn>
    <v-btn value="account" @click="goAccount">
      <v-icon>mdi-account-outline</v-icon><span>حسابي</span>
    </v-btn>
  </v-bottom-navigation>
</template>

<script setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCartStore } from '@/stores/cart'
import { useAuthStore } from '@/stores/auth'

const cart = useCartStore()
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const active = computed(() => {
  if (route.matched[0]?.path === '/account') return 'account'
  return ['home', 'catalog', 'cart'].includes(route.name) ? route.name : null
})
function goAccount() {
  if (auth.isLoggedIn) router.push({ name: 'orders' })
  else auth.openLogin({ name: 'orders' })
}
</script>

<style scoped>
.qa-bottom-nav { padding-bottom: env(safe-area-inset-bottom); }
.qa-bottom-nav :deep(.v-btn__content span) { font-size: 0.72rem; font-weight: 600; }
</style>
