<script setup>
import allNavItems from '@/navigation/horizontal'
import { themeConfig } from '@themeConfig'
import { useUserRole } from '@/composables/useUserRole'
import { useSalesRequestUnread, withRequestsUnreadBadge } from '@/composables/useSalesRequestUnread'

// Components
import AppLoadingIndicator from "@/components/AppLoadingIndicator.vue"
import NavbarThemeSwitcher from '@/layouts/components/NavbarThemeSwitcher.vue'
import NavBarNotifications from '@/layouts/components/NavBarNotifications.vue'
import UserProfile from '@/layouts/components/UserProfile.vue'
import NavBarI18n from '@core/components/I18n.vue'
import TheCustomizer from "@core/components/TheCustomizer.vue"
import { HorizontalNavLayout } from '@layouts'
import { VNodeRenderer } from '@layouts/components/VNodeRenderer'

// SECTION: Loading Indicator
const isFallbackStateActive = ref(false)
const refLoadingIndicator = ref(null)

watch([
  isFallbackStateActive,
  refLoadingIndicator,
], () => {
  if (isFallbackStateActive.value && refLoadingIndicator.value)
    refLoadingIndicator.value.fallbackHandle()
  if (!isFallbackStateActive.value && refLoadingIndicator.value)
    refLoadingIndicator.value.resolveHandle()
}, { immediate: true })
// !SECTION

const cityName = ref(localStorage.getItem('CityName'))
const { canManageUsers, canBackup, canViewDecisions, isSalesManager, isDirector, canViewFollowerGps, isAdmin } = useUserRole()
useSalesRequestUnread()

const ratingsNavItem = {
  title: 'التقييمات',
  to: { name: 'accountant-ratings' },
  icon: { icon: 'tabler-chart-histogram', size: '25' },
}

const tutorialVideoNavItem = {
  title: 'مشاهدة الفيديو التعليمي',
  to: { name: 'accountant-tutorial-video' },
  icon: { icon: 'tabler-video', size: '25' },
}

const navItems = computed(() => {
  const items = allNavItems.filter(item => {
    if (isDirector.value)
      return item.title === 'التقييمات' || item.title === 'طلبات البيع'

    if (isSalesManager.value)
      return item.title === 'إدارة المبيعات'

    if (item.title === 'الدعم الفني') return false
    if (item.title === 'طلبات البيع') return false
    if (item.title === 'التقييمات') return false
    if (item.title === 'مشاهدة الفيديو التعليمي') return false

    if (item.title === 'المستخدمين' && !canManageUsers.value) return false
    if (item.title === 'النسخ الاحتياطي' && !canBackup.value) return false
    if (item.title === 'القرارات' && !canViewDecisions.value) return false
    if (item.title === 'إدارة المبيعات' && !isSalesManager.value) return false
    if (item.title === 'متابعة المتابعين' && !canViewFollowerGps.value) return false

    return true
  }).map(item => {
    if (item.title === 'القرارات' && item.children && isAdmin.value) {
      return {
        ...item,
        children: item.children.filter(c => c.title !== 'لوحة ضعفاء التسديد'),
      }
    }

    if (item.title !== 'العملاء' || !item.children || isAdmin.value)
      return item

    return {
      ...item,
      children: item.children.filter(c => c.title !== 'بحث القانونية'),
    }
  })

  if (isAdmin.value) {
    const homeIdx = items.findIndex(i => i.title === 'الرئيسية')
    const insertAt = homeIdx >= 0 ? homeIdx + 1 : 0
    items.splice(insertAt, 0, { ...ratingsNavItem }, { ...tutorialVideoNavItem })
  }

  try {
    return withRequestsUnreadBadge(items)
  }
  catch {
    return items
  }
})
</script>

<template>
  <HorizontalNavLayout :nav-items="navItems">
    <!-- 👉 navbar -->
    <template #navbar>
      <RouterLink
        to="/"
        class="app-logo d-flex align-center gap-x-3"
      >
        <VNodeRenderer :nodes="themeConfig.app.logo" />

        <h1 class="app-title font-weight-bold leading-normal text-xl text-capitalize">
          {{ themeConfig.app.title }}
        </h1>
      </RouterLink>

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
      </div>
      <VSpacer />

      <NavBarI18n
        v-if="themeConfig.app.i18n.enable && themeConfig.app.i18n.langConfig?.length"
        :languages="themeConfig.app.i18n.langConfig"
      />

      <NavbarThemeSwitcher class="me-2" />
      <NavBarNotifications
        v-if="isAdmin"
        class="me-1"
      />
      <UserProfile />
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


    <!-- 👉 Customizer -->
    <TheCustomizer />
  </HorizontalNavLayout>
</template>
