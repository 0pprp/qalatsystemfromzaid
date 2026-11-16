<template>
  <v-app>
    <AppHeader />
    <v-main>
      <router-view v-slot="{ Component, route }">
        <component :is="Component" :key="route.matched[0]?.path === '/account' ? 'account' : route.fullPath.split('?')[0]" />
      </router-view>
    </v-main>
    <AppFooter />
    <BottomNav v-if="smAndDown" />
    <LoginDialog />
    <v-snackbar v-model="toastState.show" :color="toastState.color" :timeout="toastState.timeout" location="top" rounded="lg">
      {{ toastState.text }}
      <template #actions>
        <v-btn variant="text" icon="mdi-close" size="small" aria-label="إغلاق" @click="toastState.show = false" />
      </template>
    </v-snackbar>
  </v-app>
</template>

<script setup>
import { onMounted } from 'vue'
import { tokens } from '@/api/tokens'
import { useRouter, useRoute } from 'vue-router'
import { useDisplay } from 'vuetify'
import AppHeader from '@/components/AppHeader.vue'
import AppFooter from '@/components/AppFooter.vue'
import BottomNav from '@/components/BottomNav.vue'
import LoginDialog from '@/components/LoginDialog.vue'
import { toastState, toast } from '@/composables/useToast'
import { useCatalogStore } from '@/stores/catalog'

const { smAndDown } = useDisplay()
const catalog = useCatalogStore()
const router = useRouter()
const route = useRoute()

onMounted(() => {
  catalog.loadConfig().catch(() => {})
  catalog.loadBranches().catch(() => {})
  catalog.loadCategories().catch(() => {})
})

// Session expired (refresh failed) while on a protected page → go to login.
tokens.onExpired(() => {
  if (route.matched.some((r) => r.meta.requiresAuth)) {
    toast('انتهت الجلسة، يرجى تسجيل الدخول مرة أخرى', 'warning')
    router.replace({ name: 'login', query: { redirect: route.fullPath } })
  }
})
</script>
