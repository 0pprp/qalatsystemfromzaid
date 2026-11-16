import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { setHead } from '@/composables/useHead'

const routes = [
  { path: '/', name: 'home', component: () => import('@/pages/HomePage.vue'), meta: { title: null } },
  { path: '/products', name: 'catalog', component: () => import('@/pages/CatalogPage.vue'), meta: { title: 'المنتجات' } },
  { path: '/c/:id', redirect: (to) => ({ name: 'catalog', query: { categoryId: to.params.id } }) },
  { path: '/p/:id(\\d+)', name: 'product', component: () => import('@/pages/ProductPage.vue'), meta: { title: 'المنتج', dynamicHead: true } },
  { path: '/cart', name: 'cart', component: () => import('@/pages/CartPage.vue'), meta: { title: 'سلة المشتريات' } },
  { path: '/checkout', name: 'checkout', component: () => import('@/pages/CheckoutPage.vue'), meta: { title: 'إتمام الطلب', requiresAuth: true } },
  { path: '/order-success/:id', name: 'order-success', component: () => import('@/pages/OrderSuccessPage.vue'), meta: { title: 'تم إرسال الطلب', requiresAuth: true } },
  { path: '/login', name: 'login', component: () => import('@/pages/LoginPage.vue'), meta: { title: 'تسجيل الدخول', guestOnly: true } },
  {
    path: '/r/:code',
    name: 'referral',
    beforeEnter: (to) => {
      const auth = useAuthStore()
      const code = String(to.params.code || '').trim()
      if (auth.isLoggedIn) return { name: 'home', query: { ref: 'already' } }
      if (code) auth.storeReferralCode(code)
      return { name: 'login', query: { register: '1' } }
    },
    component: () => import('@/pages/HomePage.vue'),
  },
  { path: '/terms', name: 'terms', component: () => import('@/pages/TermsPage.vue'), meta: { title: 'الشروط والأحكام' } },
  { path: '/privacy', name: 'privacy', component: () => import('@/pages/PrivacyPage.vue'), meta: { title: 'سياسة الخصوصية' } },
  { path: '/download', name: 'download', component: () => import('@/pages/DownloadPage.vue'), meta: { title: 'حمّل التطبيق' } },
  {
    path: '/account',
    component: () => import('@/pages/account/AccountLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      { path: '', name: 'account', redirect: { name: 'orders' } },
      { path: 'orders', name: 'orders', component: () => import('@/pages/account/OrdersPage.vue'), meta: { title: 'طلباتي' } },
      { path: 'orders/:id(\\d+)', name: 'order', component: () => import('@/pages/account/OrderDetailPage.vue'), meta: { title: 'تفاصيل الطلب' } },
      { path: 'earnings', name: 'earnings', component: () => import('@/pages/account/EarningsPage.vue'), meta: { title: 'أرباحي' } },
      { path: 'network', name: 'network', component: () => import('@/pages/account/NetworkPage.vue'), meta: { title: 'شبكتي' } },
      { path: 'notifications', name: 'notifications', component: () => import('@/pages/account/NotificationsPage.vue'), meta: { title: 'الإشعارات' } },
      { path: 'profile', name: 'profile', component: () => import('@/pages/account/ProfilePage.vue'), meta: { title: 'حسابي' } },
    ],
  },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/pages/NotFoundPage.vue'), meta: { title: 'الصفحة غير موجودة' } },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
  scrollBehavior(to, from, saved) {
    if (saved) return saved
    if (to.hash) return { el: to.hash, behavior: 'smooth' }
    if (to.name === from.name && to.name === 'catalog') return false
    return { top: 0 }
  },
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.matched.some((r) => r.meta.requiresAuth) && !auth.isLoggedIn) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  if (to.meta.guestOnly && auth.isLoggedIn) {
    return typeof to.query.redirect === 'string' ? to.query.redirect : { name: 'home' }
  }
})

router.afterEach((to) => {
  if (!to.meta.dynamicHead) setHead({ title: to.meta.title || null })
})

export default router
