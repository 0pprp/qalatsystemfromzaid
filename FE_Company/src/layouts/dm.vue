<script setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { removeLocalStorage, removeToken } from '@/services/tokenService'
import { DM_USER_TYPE, isDmSession } from '@/composables/delegatedManagerApi'

defineOptions({ name: 'DmLayout' })

const route = useRoute()
const router = useRouter()

const menu = [
  { title: 'الرئيسية', to: '/dm-dashboard', icon: 'tabler-layout-dashboard' },
  { title: 'مهامي', to: '/dm-assigned-tasks', icon: 'tabler-checkbox' },
  { title: 'التقييمات', to: '/dm-stats', icon: 'tabler-chart-bar' },
  { title: 'الرسائل والشكاوى', to: '/dm-complaints', icon: 'tabler-mail' },
  { title: 'طلبات الاستثناء', to: '/dm-exceptions', icon: 'tabler-scale' },
  { title: 'المبيعات الخاطئة', to: '/dm-wrong-sales', icon: 'tabler-alert-triangle' },
  { title: 'طلبات التنازل', to: '/dm-waivers', icon: 'tabler-handshake' },
  { title: 'حسابات إدارة الفرع', to: '/dm-branch-manager-users', icon: 'tabler-users' },
  { title: 'أعمال إدارة الفرع', to: '/dm-branch-visits', icon: 'tabler-building' },
  { title: 'الإعدادات', to: '/dm-settings', icon: 'tabler-settings' },
]

const userName = computed(() => localStorage.getItem('UserName') || DM_USER_TYPE)

function isActive(path) {
  return route.path === path || route.path.startsWith(`${path}/`)
}

function logout() {
  removeToken()
  removeLocalStorage()
  router.replace('/login')
}

if (!isDmSession())
  router.replace('/login')
</script>

<template>
  <div class="dm-shell">
    <aside class="dm-nav">
      <div class="dm-nav__brand">
        <div class="dm-nav__title">
          المدير المفوض
        </div>
        <div class="dm-nav__user">
          {{ userName }}
        </div>
      </div>
      <nav class="dm-nav__list">
        <RouterLink
          v-for="item in menu"
          :key="item.to"
          :to="item.to"
          class="dm-nav__item"
          :class="{ 'dm-nav__item--active': isActive(item.to) }"
        >
          <VIcon
            :icon="item.icon"
            size="20"
            class="me-2"
          />
          <span>{{ item.title }}</span>
        </RouterLink>
      </nav>
      <button
        type="button"
        class="dm-nav__logout"
        @click="logout"
      >
        تسجيل الخروج
      </button>
    </aside>
    <main class="dm-main">
      <RouterView v-slot="{ Component, route: viewRoute }">
        <KeepAlive :include="['DmStats']">
          <Component
            :is="Component"
            :key="viewRoute.meta?.keepAlive ? String(viewRoute.name || 'dm-keep') : viewRoute.fullPath"
          />
        </KeepAlive>
      </RouterView>
    </main>
  </div>
</template>

<style scoped>
.dm-shell {
  --dm-nav-w: 268px;
  display: block;
  min-block-size: 100vh;
  background:
    radial-gradient(70% 50% at 100% 0%, rgba(13, 115, 119, 0.07), transparent 50%),
    #f2f8f8;
}

.dm-nav {
  position: fixed;
  inset-block: 0;
  inset-inline-start: 0;
  inline-size: var(--dm-nav-w);
  z-index: 40;
  display: flex;
  flex-direction: column;
  border-inline-end: 1px solid rgba(13, 115, 119, 0.12);
  background: #fff;
  padding: 1rem 0.75rem;
  overflow: auto;
  box-shadow: 6px 0 24px rgba(9, 92, 95, 0.04);
}

.dm-nav__brand {
  padding: 0.7rem 0.85rem 1.1rem;
  margin-block-end: 0.4rem;
  border-radius: 14px;
  background: linear-gradient(145deg, rgba(13, 115, 119, 0.12), rgba(13, 115, 119, 0.02));
  flex-shrink: 0;
}

.dm-nav__title {
  font-weight: 800;
  font-size: 1.12rem;
  color: #0c2f30;
}

.dm-nav__user {
  font-size: 0.85rem;
  margin-block-start: 0.3rem;
  color: #5a7273;
}

.dm-nav__list {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  flex: 1 1 auto;
  min-block-size: 0;
}

.dm-nav__item {
  display: flex;
  align-items: center;
  padding: 0.7rem 0.8rem;
  border-radius: 12px;
  color: #0c2f30;
  text-decoration: none;
  font-size: 0.92rem;
  transition: background 0.18s ease, color 0.18s ease;
}

.dm-nav__item:hover {
  background: rgba(13, 115, 119, 0.07);
}

.dm-nav__item--active {
  background: rgba(13, 115, 119, 0.14);
  color: #0d7377;
  font-weight: 700;
}

.dm-nav__logout {
  margin-block-start: 0.75rem;
  padding: 0.7rem 0.8rem;
  border-radius: 12px;
  border: 1px solid rgba(15, 39, 68, 0.12);
  background: transparent;
  cursor: pointer;
  text-align: start;
  color: #0c2f30;
  font: inherit;
  flex-shrink: 0;
  transition: background 0.18s ease, border-color 0.18s ease;
}

.dm-nav__logout:hover {
  background: rgba(220, 38, 38, 0.06);
  border-color: rgba(220, 38, 38, 0.25);
}

.dm-main {
  padding: 1.35rem 1.6rem 2rem;
  margin-inline-start: var(--dm-nav-w);
  min-inline-size: 0;
  min-block-size: 100vh;
}

@media (prefers-reduced-motion: reduce) {
  .dm-nav__item { transition: none; }
}

@media (max-width: 960px) {
  .dm-nav {
    position: relative;
    inset: auto;
    inline-size: auto;
    block-size: auto;
    max-block-size: none;
    border-inline-start: 0;
    border-block-end: 1px solid rgba(13, 115, 119, 0.12);
    box-shadow: none;
  }
  .dm-main {
    margin-inline-start: 0;
    min-block-size: 0;
  }
}
</style>
