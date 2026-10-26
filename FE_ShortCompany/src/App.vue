<script setup>
import GlobalMessageDialog from '@/components/GlobalMessageDialog.vue'
import { getToken, isTokenExpired, removeLocalStorage, removeToken } from "@/services/tokenService"
import ScrollToTop from '@core/components/ScrollToTop.vue'
import initCore from '@core/initCore'
import {
  initConfigStore,
  useConfigStore,
} from '@core/stores/config'
import { hexToRgb } from '@layouts/utils'
import { useTheme } from 'vuetify'
import { isSummaryReadOnly } from '@/composables/useSummaryReadOnly'

const { global } = useTheme()
const route = useRoute()

// ℹ️ Sync current theme with initial loader theme
initCore()
initConfigStore()

onMounted(async _ => {
  if (isSummaryReadOnly)
    document.body.classList.add('summary-readonly')

  setInterval(() => {
    const isExpired = isTokenExpired()
    const token = getToken()

    if(isExpired === true || !token){
      removeToken()
      removeLocalStorage()
      if (window.location.hash !== "#/login") {
        window.location.reload()
      }
    }
  }, 2000)
})

const configStore = useConfigStore()
</script>

<template>
  <VLocaleProvider :rtl="configStore.isAppRTL===false">
    <!-- ℹ️ This is required to set the background color of active nav link based on currently active global theme's primary -->
    <VApp :style="`--v-global-theme-primary: ${hexToRgb(global.current.value.colors.primary)}`">
      <VAlert
        v-if="isSummaryReadOnly && route.path !== '/login'"
        type="info"
        variant="tonal"
        density="compact"
        class="rounded-0 text-center"
        icon="tabler-eye"
      >
        رابط الملخص — عرض البيانات فقط (بدون تسديد، شراء، إضافة، أو حذف)
      </VAlert>
      <RouterView />

      <ScrollToTop />
      <GlobalSnackbar />
      <GlobalMessageDialog />
    </VApp>
  </VLocaleProvider>
</template>

<style lang="scss">
/* Summary portal: hide known mutation controls; search/export remain */
body.summary-readonly {
  .v-btn.bg-error,
  .v-btn.text-error,
  button.v-btn[class*='error'] {
    &:has(.tabler-trash),
    &:has(.tabler-plus),
    &:has(.v-icon[icon*='trash']),
    &:has(.v-icon[icon*='plus']) {
      display: none !important;
    }
  }
}
</style>
