<script setup>
import allNavItems from '@/navigation/horizontal'
import { themeConfig } from '@themeConfig'
import { useUserRole } from '@/composables/useUserRole'
import { useSalesRequestUnread, withRequestsUnreadBadge } from '@/composables/useSalesRequestUnread'
import { ARCHIVE_MANAGER_NAV, COMMITTEE_ARCHIVE_NAV } from '@/composables/archiveFlowApi'
import { LEGAL_DESK_DIRECTOR_NAV, LEGAL_DESK_NAV, LEGAL_DESK_NAV_GROUP } from '@/composables/legalDeskApi'

// Components
import AppLoadingIndicator from "@/components/AppLoadingIndicator.vue"
import NavbarThemeSwitcher from '@/layouts/components/NavbarThemeSwitcher.vue'
import NavBarNotifications from '@/layouts/components/NavBarNotifications.vue'
import CustomerAppAlertsBell from '@/components/CustomerAppAlertsBell.vue'
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
const { canManageUsers, canBackup, canViewDecisions, isSalesManager, isDirector, isMonitor, isCommittee, isArchiveManager, isLegalAccountant, canManageOversight, canViewFollowerGps, isAdmin } = useUserRole()

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
  if (isArchiveManager.value)
    return ARCHIVE_MANAGER_NAV.map(i => ({ ...i }))
  if (isLegalAccountant.value)
    return LEGAL_DESK_NAV.map(i => ({ ...i }))

  const items = allNavItems.filter(item => {
    if (isCommittee.value)
      return item.title === 'متابعة المندوبين'
        || item.title === 'متابعة السيئين'
        || item.title === 'متابعة المبيعات'
        || item.title === 'عمل المتابعين'

    if (isMonitor.value)
      return item.title === 'التقييمات'
        || item.title === 'متابعة المندوبين'
        || item.title === 'متابعة السيئين'
        || item.title === 'متابعة المبيعات'
        || item.title === 'عمل المتابعين'
        || item.title === 'إدارة المبيعات'
        || item.title === 'أسماء قانونية'
        || item.title === 'لوحة الأسعار'
        || item.title === 'تطبيق المسوّقين'
        || item.title === 'المروّجون أونلاين'
        || item.title === 'الإعلانات'

    if (isDirector.value)
      return item.title === 'التقييمات'
        || item.title === 'الملخص'
        || item.title === 'ملخص'
        || item.title === 'متابعة المندوبين'
        || item.title === 'متابعة السيئين'
        || item.title === 'متابعة المبيعات'
        || item.title === 'عمل المتابعين'
        || item.title === 'طلبات البيع'
        || item.title === 'الفيديوهات التعليمية'
        || item.title === 'الإعلانات'
        || item.title === 'المهام'
        || item.title === 'مهام اللجنة'
        || item.title === 'ملاحظات التطبيقات'
        || item.title === 'التنازلات'
        || item.title === 'تطبيق المسوّقين'
        || item.title === 'المروّجون أونلاين'

    if (isSalesManager.value)
      return item.title === 'إدارة المبيعات'

    if (item.title === 'الدعم الفني') return false
    if (item.title === 'تطبيق المسوّقين') return false // المدير + المراقب فقط
    if (item.title === 'المروّجون أونلاين') return false // المدير + المراقب فقط
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
    if (item.title === 'متابعة المندوبين' && !isDirector.value && !isMonitor.value && !isCommittee.value && !isAdmin.value) return false // المحاسب الرئيسي: فرعه فقط
    if (item.title === 'متابعة السيئين' && !isDirector.value && !isMonitor.value && !isCommittee.value && !isAdmin.value) return false
    if (item.title === 'لوحة الأسعار' && !isAdmin.value && !isMonitor.value) return false

    return true
  }).map(item => {
    // لجنة المراقبة: no الجباية under عمل المتابعين
    if (item.title === 'عمل المتابعين' && item.children && isCommittee.value) {
      return {
        ...item,
        children: item.children.filter(c => c.title !== 'الجباية'),
      }
    }
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

    if (!items.some(i => i?.to?.name === 'sales-manager-dashboard')) {
      items.unshift({
        title: 'نظرة عامة',
        to: { name: 'sales-manager-dashboard' },
        icon: { icon: 'tabler-layout-dashboard', size: '25' },
      })
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

  if (isCommittee.value)
    items.push({ ...COMMITTEE_ARCHIVE_NAV })

  if (isDirector.value)
    items.push({ ...LEGAL_DESK_NAV_GROUP, children: [...LEGAL_DESK_NAV, ...LEGAL_DESK_DIRECTOR_NAV].map(i => ({ ...i })) })

  if (canManageOversight.value && !items.some(i => i?.to?.name === 'oversight-accounts')) {
    items.push({
      title: 'لجان المراقبة',
      to: { name: 'oversight-accounts' },
      icon: { icon: 'tabler-users-group', size: '25' },
    })
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
      <CustomerAppAlertsBell
        v-if="isDirector || isMonitor"
        class="me-1"
      />
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
        <KeepAlive :include="['DirectorRatings', 'AccountantRatings', 'DirectorFollowerWork', 'DirectorDelegateFollowup', 'DirectorDelegateFollowupCustomers', 'DirectorBadCustomers']">
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
