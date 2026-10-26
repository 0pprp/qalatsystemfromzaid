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
const { canManageUsers, canBackup, canViewDecisions, isSalesManager, isDirector, isMonitor, canViewFollowerGps, isAdmin } = useUserRole()
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
    if (isMonitor.value)
      return item.title === 'التقييمات'
        || item.title === 'متابعة المندوبين'
        || item.title === 'متابعة المبيعات'
        || item.title === 'عمل المتابعين'
        || item.title === 'إدارة المبيعات'
        || item.title === 'أسماء قانونية'
        || item.title === 'لوحة الأسعار'

    if (isDirector.value)
      return item.title === 'التقييمات'
        || item.title === 'الملخص'
        || item.title === 'ملخص'
        || item.title === 'متابعة المندوبين'
        || item.title === 'متابعة المبيعات'
        || item.title === 'عمل المتابعين'
        || item.title === 'طلبات البيع'
        || item.title === 'الفيديوهات التعليمية'
        || item.title === 'الإعلانات'
        || item.title === 'المهام'
        || item.title === 'مهام اللجنة'
        || item.title === 'ملاحظات التطبيقات'
        || item.title === 'التنازلات'

    if (isSalesManager.value)
      return item.title === 'إدارة المبيعات'

    if (item.title === 'الدعم الفني') return false
    if (item.title === 'طلبات البيع') return false
    if (item.title === 'التقييمات') return false
    if (item.title === 'ملخص' || item.title === 'الملخص') return false
    if (item.title === 'الفيديوهات التعليمية') return false
    if (item.title === 'الإعلانات') return false
    if (item.title === 'المهام') return false
    if (item.title === 'مهام اللجنة') return false
    if (item.title === 'ملاحظات التطبيقات') return false
    if (item.title === 'التنازلات') return false
    if (item.title === 'مشاهدة الفيديو التعليمي') return false

    if (item.title === 'المستخدمين' && !canManageUsers.value) return false
    if (item.title === 'النسخ الاحتياطي' && !canBackup.value) return false
    if (item.title === 'القرارات' && !canViewDecisions.value) return false
    if (item.title === 'إدارة المبيعات' && !isSalesManager.value && !isMonitor.value) return false
    if (item.title === 'متابعة المتابعين' && !canViewFollowerGps.value) return false
    if (item.title === 'لوحة الأسعار' && !isAdmin.value && !isMonitor.value) return false

    return true
  }).map(item => {
    if (item.title === 'القرارات' && item.children && isAdmin.value) {
      return {
        ...item,
        children: item.children.filter(c => c.title !== 'لوحة ضعفاء التسديد'),
      }
    }

    if (item.title === 'إدارة المبيعات' && item.children && !isMonitor.value) {
      return {
        ...item,
        children: item.children.filter(c => c.title !== 'ملخص المبيعات'),
      }
    }

    if (item.title !== 'العملاء' || !item.children || isAdmin.value)
      return item

    return {
      ...item,
      children: item.children.filter(c => c.title !== 'بحث القانونية'),
    }
  })

  if (isDirector.value) {
    const hasSummary = items.some(i => i.title === 'الملخص' || i.title === 'ملخص' || i?.to?.name === 'director-summary')
    if (!hasSummary) {
      const ratingsIdx = items.findIndex(i => i.title === 'التقييمات')
      const insertAt = ratingsIdx >= 0 ? ratingsIdx + 1 : 0
      items.splice(insertAt, 0, {
        title: 'الملخص',
        to: { name: 'director-summary' },
        icon: { icon: 'tabler-table', size: '25' },
      })
    }
    else {
      for (const it of items) {
        if (it?.to?.name === 'director-summary' || it.title === 'ملخص')
          it.title = 'الملخص'
      }
    }
  }

  if (isAdmin.value) {
    const homeIdx = items.findIndex(i => i.title === 'الرئيسية')
    const insertAt = homeIdx >= 0 ? homeIdx + 1 : 0
    items.splice(insertAt, 0, { ...ratingsNavItem }, { ...tutorialVideoNavItem })

    const salesIdx = items.findIndex(i => i.title === 'المبيعات')
    if (salesIdx >= 0) {
      items[salesIdx] = {
        title: 'المبيعات',
        icon: { icon: 'tabler-cash', size: '25' },
        children: [
          {
            title: 'قائمة المبيعات',
            to: { name: 'sales-list' },
            icon: { icon: 'tabler-list', size: '22' },
          },
          {
            title: 'المبيعات الخاطئة',
            to: { name: 'accountant-wrong-sales' },
            icon: { icon: 'tabler-alert-triangle', size: '22' },
          },
        ],
      }
    }
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

    <!-- 👉 Pages: keepAlive pages use stable route.name so query changes do not remount -->
    <RouterView v-slot="{ Component, route: viewRoute }">
      <Suspense
        :timeout="0"
        @fallback="isFallbackStateActive = true"
        @resolve="isFallbackStateActive = false"
      >
        <KeepAlive :include="['DirectorRatings', 'AccountantRatings', 'DirectorFollowerWork', 'DirectorDelegateFollowup', 'DirectorDelegateFollowupCustomers']">
          <Component
            :is="Component"
            :key="viewRoute.meta?.keepAlive ? String(viewRoute.name || 'keep') : viewRoute.fullPath"
          />
        </KeepAlive>
      </Suspense>
    </RouterView>


    <!-- 👉 Customizer -->
    <TheCustomizer />
  </HorizontalNavLayout>
</template>
