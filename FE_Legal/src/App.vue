<script setup>
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useDisplay } from 'vuetify'
import { session, signOut } from './api'
import { toast } from './shared'

const route = useRoute()
const router = useRouter()
const { mdAndUp } = useDisplay()
const drawer = ref(false)

// Re-read on every navigation: the session lives in localStorage, not in reactive state.
const user = computed(() => route.fullPath && session())

const NAV = [
  { to: '/', label: 'مسار المدينين', icon: 'route', match: p => p === '/' || p.startsWith('/cases') },
  { to: '/lawyers', label: 'المحامون', icon: 'briefcase', match: p => p.startsWith('/lawyers') },
  { to: '/registers', label: 'القاصة', icon: 'wallet', match: p => p.startsWith('/registers') },
  { to: '/settings', label: 'الإعدادات', icon: 'settings', match: p => p.startsWith('/settings') },
]

const current = computed(() => NAV.find(item => item.match(route.path)))

function logout() {
  signOut()
  router.push('/login')
}
</script>

<template>
  <VApp>
    <template v-if="!route.meta.public">
      <!-- Same shell as the company dashboard: dark side menu, floating top bar. -->
      <VNavigationDrawer
        :model-value="mdAndUp || drawer"
        :permanent="mdAndUp"
        :temporary="!mdAndUp"
        width="260"
        class="side"
        @update:model-value="drawer = $event"
      >
        <div class="side__brand">
          <img
            src="/logo.png"
            alt=""
            width="40"
            height="40"
          >
          <div>
            <div class="side__title">
              شركة قلعة الضمان
            </div>
            <div class="side__sub">
              القانونية
            </div>
          </div>
        </div>
        <nav class="side__nav">
          <RouterLink
            v-for="item in NAV"
            :key="item.to"
            :to="item.to"
            class="side__item"
            :class="{ 'side__item--active': item.match(route.path) }"
            @click="drawer = false"
          >
            <VIcon
              :icon="item.icon"
              size="22"
            />
            {{ item.label }}
          </RouterLink>
        </nav>
      </VNavigationDrawer>

      <VAppBar
        flat
        color="transparent"
        height="76"
      >
        <div class="topbar">
          <VBtn
            v-if="!mdAndUp"
            icon="menu-2"
            variant="text"
            size="small"
            @click="drawer = !drawer"
          />
          <VIcon
            v-if="current"
            :icon="current.icon"
            size="20"
            class="muted"
          />
          <span class="topbar__title">{{ current?.label }}</span>
          <VSpacer />
          <template v-if="user">
            <div class="text-end">
              <div class="text-body-2 font-weight-medium">
                {{ user.userName }}
              </div>
              <div class="text-caption muted">
                {{ user.userType }}
              </div>
            </div>
            <VBtn
              icon="logout"
              variant="text"
              size="small"
              title="تسجيل الخروج"
              @click="logout"
            />
          </template>
        </div>
      </VAppBar>
    </template>

    <VMain>
      <RouterView :key="route.path" />
    </VMain>

    <VSnackbar
      v-model="toast.visible"
      :color="toast.color"
      location="bottom"
      timeout="4000"
    >
      {{ toast.text }}
    </VSnackbar>
  </VApp>
</template>

<style scoped>
.side {
  border: 0 !important;
  background: #2f3349 !important;
  color: #e1def5 !important;
}

.side__brand {
  display: flex;
  align-items: center;
  padding: 20px 20px 16px;
  gap: 12px;
}

.side__title {
  color: #fff;
  font-size: 1rem;
  font-weight: 700;
  line-height: 1.4;
}

.side__sub {
  color: rgba(225, 222, 245, 70%);
  font-size: 0.8rem;
}

.side__nav {
  display: flex;
  flex-direction: column;
  padding: 4px 12px;
  gap: 4px;
}

.side__item {
  display: flex;
  align-items: center;
  padding: 9px 14px;
  border-radius: 6px;
  color: rgba(225, 222, 245, 90%);
  font-size: 0.95rem;
  gap: 10px;
  text-decoration: none;
  transition: background 0.15s;
}

.side__item:hover {
  background: rgba(225, 222, 245, 6%);
}

.side__item--active,
.side__item--active:hover {
  background: rgb(var(--v-theme-primary));
  box-shadow: 0 2px 6px rgba(var(--v-theme-primary), 0.3);
  color: #fff;
  font-weight: 500;
}

.topbar {
  display: flex;
  flex: 1 1 auto;
  align-items: center;
  padding: 10px 16px;
  border-radius: 6px;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 3px 12px rgba(47, 43, 61, 14%);
  gap: 10px;
  margin-block-start: 12px;
  margin-inline: 24px;
}

.topbar__title {
  font-size: 0.95rem;
}
</style>
