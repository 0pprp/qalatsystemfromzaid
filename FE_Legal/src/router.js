import { createRouter, createWebHashHistory } from 'vue-router'
import { session } from './api'
import BoardPage from './pages/BoardPage.vue'
import CasePage from './pages/CasePage.vue'
import LawyerPage from './pages/LawyerPage.vue'
import LawyersPage from './pages/LawyersPage.vue'
import LoginPage from './pages/LoginPage.vue'
import RegistersPage from './pages/RegistersPage.vue'
import SettingsPage from './pages/SettingsPage.vue'

// Pages are imported up front (the app is small). Loading them lazily made the dev server
// re-bundle dependencies after sign-in, which left the user stuck on the login screen.
export const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/login', component: LoginPage, meta: { public: true } },
    { path: '/', component: BoardPage },
    { path: '/cases/:id', component: CasePage, props: true },
    { path: '/lawyers', component: LawyersPage },
    { path: '/lawyers/:id', component: LawyerPage, props: true },
    { path: '/registers', component: RegistersPage },
    { path: '/settings', component: SettingsPage },
    { path: '/:rest(.*)*', redirect: '/' },
  ],
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach(to => {
  const signedIn = Boolean(session())
  if (!to.meta.public && !signedIn)
    return '/login'
  if (to.path === '/login' && signedIn)
    return '/'

  return true
})
