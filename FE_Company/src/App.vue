<script setup>
import GlobalMessageDialog from '@/components/GlobalMessageDialog.vue'
import { isAuthenticated, removeLocalStorage, removeToken } from "@/services/tokenService"
import ScrollToTop from '@core/components/ScrollToTop.vue'
import initCore from '@core/initCore'
import {
  initConfigStore,
  useConfigStore,
} from '@core/stores/config'
import { hexToRgb } from '@layouts/utils'
import { useTheme } from 'vuetify'

const { global } = useTheme()

// ℹ️ Sync current theme with initial loader theme
initCore()
initConfigStore()

const router = useRouter()

onMounted(() => {
  const sendToLoginIfSessionEnded = () => {
    if (isAuthenticated())
      return

    removeToken()
    removeLocalStorage()
    if (router.currentRoute.value.path !== '/login')
      router.replace('/login')
  }

  sendToLoginIfSessionEnded()
  const timer = setInterval(sendToLoginIfSessionEnded, 2000)
  onUnmounted(() => clearInterval(timer))
})

const configStore = useConfigStore()
</script>

<template>
  <VLocaleProvider :rtl="configStore.isAppRTL===false">
    <!-- ℹ️ This is required to set the background color of active nav link based on currently active global theme's primary -->
    <VApp :style="`--v-global-theme-primary: ${hexToRgb(global.current.value.colors.primary)}`">
      <RouterView />

      <ScrollToTop />
      <GlobalSnackbar />
      <GlobalMessageDialog />
    </VApp>
  </VLocaleProvider>
</template>
