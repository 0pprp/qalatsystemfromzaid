<script setup>
import allNavItems from '@/navigation/vertical'
import { themeConfig } from '@themeConfig'
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useUserRole } from '@/composables/useUserRole'

// Components
import Footer from '@/layouts/components/Footer.vue'
import NavbarThemeSwitcher from '@/layouts/components/NavbarThemeSwitcher.vue'
import NavBarNotifications from '@/layouts/components/NavBarNotifications.vue'
import UserProfile from '@/layouts/components/UserProfile.vue'
import NavBarI18n from '@core/components/I18n.vue'

// @layouts plugin
import AppLoadingIndicator from "@/components/AppLoadingIndicator.vue"
import TheCustomizer from "@core/components/TheCustomizer.vue"
import { Icon } from "@iconify/vue"
import { VerticalNavLayout } from '@layouts'

// SECTION: Loading Indicator
const isFallbackStateActive = ref(false)
const refLoadingIndicator = ref(null)

watch([isFallbackStateActive, refLoadingIndicator], () => {
  if (isFallbackStateActive.value && refLoadingIndicator.value) refLoadingIndicator.value.fallbackHandle()
  if (!isFallbackStateActive.value && refLoadingIndicator.value) refLoadingIndicator.value.resolveHandle()
}, { immediate: true })

// === NEW: احصل على عنصر القائمة المطابق للراوت الحالي ===
const route = useRoute()

function findByRouteName(items, name) {
  for (const it of items) {
    if (it?.to?.name === name) return it
    if (Array.isArray(it?.children) && it.children.length) {
      const found = findByRouteName(it.children, name)
      if (found) return found
    }
  }
  
  return null
}

const { canManageUsers, canBackup, canViewDecisions } = useUserRole()

const navItems = computed(() => {
  return allNavItems.filter(item => {
    if (item.title === 'المستخدمين' && !canManageUsers.value) return false
    if (item.title === 'النسخ الاحتياطي' && !canBackup.value) return false
    if (item.title === 'القرارات' && !canViewDecisions.value) return false

    return true
  })
})

const activeNavItem = computed(() => findByRouteName(navItems.value, route.name) ?? null)
const cityName = ref(localStorage.getItem('CityName'))
</script>

<template>
  <VerticalNavLayout :nav-items="navItems">
    <!-- 👉 navbar -->
    <template #navbar="{ toggleVerticalOverlayNavActive }">
      <div class="d-flex h-100 align-center">
        <IconBtn
          id="vertical-nav-toggle-btn"
          class="ms-n3 d-lg-none"
          @click="toggleVerticalOverlayNavActive(true)"
        >
          <VIcon
            size="26"
            icon="tabler-menu-2"
          />
        </IconBtn>

        <div
          v-if="cityName"
          class="d-flex align-center ms-4"
        >
          <VIcon
            icon="tabler-map-pin"
            size="20"
            class="me-2 text-primary"
          />
          <span class="text-subtitle-2 font-weight-bold text-primary">{{ cityName }}</span>
          <span class="mx-3 text-medium-emphasis">/</span>
        </div>
        
        <Icon
          v-if="activeNavItem?.icon?.icon"
          :icon="activeNavItem.icon.icon"
          class="ms-2"
          style="block-size: 20px; inline-size: 20px;"
        />
        <VListItemSubtitle
          class="text-truncate ms-2"
          style="font-size: 16px;"
        >
          {{ activeNavItem?.title || '' }}
        </VListItemSubtitle>

        <VSpacer />

        <NavBarI18n
          v-if="themeConfig.app.i18n.enable && themeConfig.app.i18n.langConfig?.length"
          :languages="themeConfig.app.i18n.langConfig"
          class="d-none d-md-block"
        />
        <NavbarThemeSwitcher class="me-2" />
        <NavBarNotifications
          v-if="isAdmin"
          class="me-1"
        />
        <UserProfile />
      </div>
    </template>

    <AppLoadingIndicator ref="refLoadingIndicator" />

    <!-- 👉 Pages -->
    <RouterView v-slot="{ Component }">
      <Suspense
        :timeout="0"
        @fallback="isFallbackStateActive = true"
        @resolve="isFallbackStateActive = false"
      >
        <Component :is="Component" />
      </Suspense>
    </RouterView>

    <!-- 👉 Footer -->
    <template #footer>
      <Footer />
    </template>

    <!-- 👉 Customizer -->
    <TheCustomizer />
  </VerticalNavLayout>
</template>
