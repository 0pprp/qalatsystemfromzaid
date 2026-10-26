import { isAuthenticated, removeLocalStorage, removeToken } from "@/services/tokenService"
import { setupLayouts } from 'virtual:generated-layouts'
import { createRouter, createWebHashHistory } from 'vue-router/auto'

function recursiveLayouts(route) {
  if (route.children) {
    for (let i = 0; i < route.children.length; i++) {
      route.children[i] = recursiveLayouts(route.children[i])
    }


    return route
  }

  return setupLayouts([route])[0]
}

const router = createRouter({
  history: createWebHashHistory(import.meta.env.BASE_URL),
  scrollBehavior(to) {
    if (to.hash)
      return { el: to.hash, behavior: 'smooth', top: 0 }

    return { top: 0 }
  },
  extendRoutes: pages => [
    ...[...pages].map(route => recursiveLayouts(route)),
  ],
})


const DM_USER_TYPE = 'مدير مفوض'
const dmPaths = [
  '/dm-dashboard',
  '/dm-assigned-tasks',
  '/dm-stats',
  '/dm-customer-profile',
  '/dm-complaints',
  '/dm-exceptions',
  '/dm-wrong-sales',
  '/dm-waivers',
  '/dm-branch-manager-users',
  '/dm-branch-visits',
  '/dm-settings',
]

function isDmPath(path) {
  return path === '/dm-login' || dmPaths.includes(path) || String(path || '').startsWith('/dm-')
}

function homePath() {
  const t = localStorage.getItem('UserType')
  if (t === DM_USER_TYPE)
    return '/dm-dashboard'
  if (t === 'المدير')
    return '/director-ratings'
  if (t === 'المراقب')
    return { path: '/director-ratings', query: {} }
  if (t === 'مدير مبيعات')
    return '/sales-manager-dashboard'

  return '/'
}

router.beforeEach((to, from, next) => {

  if (to.path === "/logout") {
    removeToken()
    removeLocalStorage()
  }

  const isNotAuthenticated = !isAuthenticated()



  if (to.path === '/login' || to.path === '/dm-login') {
    if (!isNotAuthenticated)
      return next(homePath())
    else
      return next()
  }


  // إذا لم يكن المستخدم مصادقًا عليه، اسمح له بالوصول إلى صفحة تسجيل الدخول أو التسجيل فقط
  if (isNotAuthenticated) {
    return next('/login')
  }

  if (to.path === '/support')
    return next(homePath())

  const userType = localStorage.getItem('UserType') || ''
  if (userType === DM_USER_TYPE) {
    if (!isDmPath(to.path) && to.path !== '/logout')
      return next('/dm-dashboard')
    if (to.path === '/')
      return next('/dm-dashboard')
  }
  if (isDmPath(to.path) && to.path !== '/dm-login' && userType !== DM_USER_TYPE)
    return next(homePath())

  if (userType === 'مدير مبيعات' && to.path === '/')
    return next('/sales-manager-dashboard')
  if (userType === 'المدير' && to.path === '/')
    return next('/director-ratings')
  if (userType === 'المراقب' && to.path === '/')
    return next({ path: '/director-ratings', query: {} })

  const adminOnlyPaths = [
    '/users-list',
    '/user-active',
    '/backup-database',
    '/accountant-legal-search',
    '/accountant-tutorial-video',
    '/accountant-wrong-sales',
  ]
  if (adminOnlyPaths.includes(to.path) && userType !== 'محاسب رئيسي') {
    return next(homePath())
  }

  // أسماء قانونية: مدير فرع + محاسب رئيسي + المراقب (عرض فقط)
  if (to.path === '/decision-board'
    && userType !== 'مدير فرع'
    && userType !== 'محاسب رئيسي'
    && userType !== 'المراقب') {
    return next(homePath())
  }

  // legacy legal-conversion → decision-board?tab=legal
  if (to.path === '/legal-conversion-requests') {
    return next({ path: '/decision-board', query: { ...to.query, tab: 'legal' } })
  }

  const decisionPaths = ['/decisions-list', '/waiver-accountant-requests']
  if (decisionPaths.includes(to.path)
    && userType !== 'مدير فرع'
    && userType !== 'محاسب رئيسي'
    && userType !== 'المراقب') {
    return next(homePath())
  }

  if (to.path === '/price-board'
    && userType !== 'محاسب رئيسي'
    && userType !== 'المراقب') {
    return next(homePath())
  }

  const salesManagerPaths = [
    '/sales-manager-dashboard',
    '/sales-manager-employees',
    '/sales-manager-map',
    '/sales-manager-routes',
    '/sales-manager-sales',
    '/sales-manager-legal-followups',
    '/sales-manager-request-create',
    '/sales-manager-excel-search',
  ]
  // مدير المبيعات + المراقب (كامل إدارة المبيعات)
  if (salesManagerPaths.includes(to.path)
    && userType !== 'مدير مبيعات'
    && userType !== 'المراقب') {
    return next(homePath())
  }

  // ملخص المبيعات: المراقب فقط
  if (to.path === '/sales-manager-sales-summary' && userType !== 'المراقب') {
    return next(homePath())
  }

  // تبويبات أُزيلت من قائمة مدير المبيعات — منع الدخول المباشر
  // map/routes stay open for المراقب + المدير under عمل المتابعين
  const followerRoutesInMonitorWorkMarker = 'FollowerRoutesInMonitorWork_V1'
  void followerRoutesInMonitorWorkMarker
  const salesManagerHiddenPaths = [
    '/sales-manager-ratings',
    '/sales-manager-followers',
    '/sales-manager-purchases',
    '/sales-manager-tutorial-links',
  ]
  if (userType === 'مدير مبيعات' && salesManagerHiddenPaths.includes(to.path)) {
    return next('/sales-manager-dashboard')
  }
  if (userType === 'مدير مبيعات'
    && (to.path === '/sales-manager-followers-map'
      || to.path === '/sales-manager-followers-routes')) {
    return next('/sales-manager-dashboard')
  }

  const followerGpsPaths = [
    '/sales-manager-followers-map',
    '/sales-manager-followers-routes',
  ]
  if (followerGpsPaths.includes(to.path)
    && userType !== 'المراقب'
    && userType !== 'المدير') {
    return next(homePath())
  }

  // طلبات البيع: مدير المبيعات + المدير + المراقب
  if (to.path === '/sales-manager-requests'
    && userType !== 'مدير مبيعات'
    && userType !== 'المدير'
    && userType !== 'المراقب') {
    return next(homePath())
  }

  if (to.path === '/director-ratings'
    && userType !== 'المدير'
    && userType !== 'المراقب') {
    return next(homePath())
  }

  if (to.path === '/accountant-ratings' && userType !== 'محاسب رئيسي') {
    return next(homePath())
  }

  const directorAllowed = [
    '/director-ratings',
    '/director-summary',
    '/director-delegate-followup',
    '/director-delegate-followup-customers',
    '/director-sales-followup',
    '/director-follower-work',
    '/director-tutorial-videos',
    '/director-announcements',
    '/director-assigned-tasks',
    '/director-branch-visits',
    '/director-committee-tasks',
    '/director-notes',
    '/director-waivers',
    '/sales-manager-customer-profile',
    '/sales-manager-requests',
    '/sales-manager-followers-map',
    '/sales-manager-followers-routes',
  ]
  // Director: ratings + tutorial videos + notes + sales requests + customer profile
  if (userType === 'المدير' && !directorAllowed.includes(to.path) && to.path !== '/logout') {
    return next('/director-ratings')
  }

  // Monitor: التقييمات + عمل المتابعين + كامل إدارة المبيعات (+ بروفايل الزبون)
  const monitorAllowed = [
    '/director-ratings',
    '/director-delegate-followup',
    '/director-delegate-followup-customers',
    '/director-sales-followup',
    '/director-follower-work',
    '/sales-manager-dashboard',
    '/sales-manager-employees',
    '/sales-manager-map',
    '/sales-manager-routes',
    '/sales-manager-sales',
    '/sales-manager-sales-summary',
    '/sales-manager-legal-followups',
    '/sales-manager-requests',
    '/sales-manager-request-create',
    '/sales-manager-excel-search',
    '/sales-manager-customer-profile',
    '/sales-manager-followers-map',
    '/sales-manager-followers-routes',
    '/decision-board',
    '/waiver-accountant-requests',
    '/decisions-list',
    '/price-board',
  ]
  if (userType === 'المراقب' && !monitorAllowed.includes(to.path) && to.path !== '/logout') {
    return next({ path: '/director-ratings', query: {} })
  }

  if ((to.path === '/director-tutorial-videos'
    || to.path === '/director-announcements'
    || to.path === '/director-assigned-tasks'
    || to.path === '/director-branch-visits'
    || to.path === '/director-committee-tasks'
    || to.path === '/director-notes'
    || to.path === '/director-waivers')
    && userType !== 'المدير') {
    return next(homePath())
  }

  if (to.path === '/director-summary' && userType !== 'المدير') {
    return next(homePath())
  }

  if (to.path === '/sales-manager-customer-profile'
    && userType !== 'مدير مبيعات'
    && userType !== 'المدير'
    && userType !== 'المراقب') {
    return next(homePath())
  }

  // Accountant follower-tracking pages retired from nav (FollowerRoutesInMonitorWork_V1).
  const followerTrackingPaths = ['/follower-tracking-map', '/follower-tracking-routes']
  if (followerTrackingPaths.includes(to.path)) {
    return next(homePath())
  }

  next()
})
export { router }
export default function (app) {
  app.use(router)
}
