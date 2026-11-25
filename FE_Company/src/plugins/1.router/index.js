import { isAuthenticated, removeLocalStorage, removeToken } from "@/services/tokenService"
import { isArchiveManagerRole, isCommitteeRole, isLegalAccountantRole, isOversightBoardRole, isSupervisorRole } from "@/utils/oversightRoles"
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

/** تطبيق المسوّقين (BE_CustomerApp) — المدير + المراقب فقط */
const customerAppPaths = [
  '/customer-app-overview',
  '/customer-app-agents',
  '/customer-app-attendance',
  '/customer-app-prospects',
  '/customer-app-targets',
  '/customer-app-settings',
]

/** المروّجون أونلاين (BE_PromotersApp) — المدير + المراقب فقط، منفصل عن تطبيق المسوّقين */
const promotersAppPaths = [
  '/promoters-app-overview',
  '/promoters-app-users',
  '/promoters-app-products',
  '/promoters-app-orders',
  '/promoters-app-payouts',
  '/promoters-app-materials',
  '/promoters-app-settings',
]

function isPromotersAppPath(path) {
  return promotersAppPaths.includes(path) || String(path || '').startsWith('/promoters-app-')
}

function isCustomerAppPath(path) {
  return customerAppPaths.includes(path) || String(path || '').startsWith('/customer-app-')
}

function homePath() {
  const t = localStorage.getItem('UserType')
  if (t === DM_USER_TYPE)
    return '/dm-dashboard'
  if (t === 'المدير')
    return '/director-ratings'
  if (isSupervisorRole(t))
    return { path: '/director-ratings', query: {} }
  if (isCommitteeRole(t))
    return '/director-delegate-followup'
  if (isArchiveManagerRole(t))
    return '/archive-overview'
  if (isLegalAccountantRole(t))
    return '/legal-desk-overview'
  if (t === 'مدير مبيعات')
    return '/sales-manager-employees'

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
    return next('/sales-manager-employees')
  if (userType === 'المدير' && to.path === '/')
    return next('/director-ratings')
  if (isSupervisorRole(userType) && to.path === '/')
    return next({ path: '/director-ratings', query: {} })
  if (isCommitteeRole(userType) && to.path === '/')
    return next('/director-delegate-followup')

  // القانونية (لوحة الشركة): محاسب القانونية + المدير فقط.
  const legalDeskPaths = ['/legal-desk-overview', '/legal-desk-lists', '/legal-desk-debtors', '/legal-desk-debtor', '/legal-desk-payments', '/legal-desk-cashbox', '/legal-desk-liability']
  if (isLegalAccountantRole(userType)) {
    if (!legalDeskPaths.includes(to.path) && to.path !== '/logout')
      return next('/legal-desk-overview')

    return next()
  }
  if (legalDeskPaths.includes(to.path) && userType !== 'المدير')
    return next(homePath())

  // مدينون بلا تقدم: المدير فقط (محاسب القانونية is already kept to legalDeskPaths above).
  if (to.path === '/legal-desk-stalled' && userType !== 'المدير')
    return next(homePath())

  // مسؤول الارشيف: the three archive pages only.
  const archiveManagerPaths = ['/archive-overview', '/archive-sales', '/archive-requests', '/archive-finished-customers']
  if (isArchiveManagerRole(userType)) {
    if (!archiveManagerPaths.includes(to.path) && to.path !== '/sales-manager-customer-profile' && to.path !== '/logout')
      return next('/archive-overview')

    return next()
  }
  if (archiveManagerPaths.includes(to.path))
    return next(homePath())
  if (to.path === '/committee-archive' && !isCommitteeRole(userType))
    return next(homePath())

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
    && !isSupervisorRole(userType)) {
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
    && !isSupervisorRole(userType)) {
    return next(homePath())
  }

  if (to.path === '/price-board'
    && userType !== 'محاسب رئيسي'
    && !isSupervisorRole(userType)) {
    return next(homePath())
  }

  if (to.path === '/sales-manager-dashboard' && userType !== 'المدير') {
    return next(homePath())
  }

  const salesManagerPaths = [
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
    && !isSupervisorRole(userType)) {
    return next(homePath())
  }

  // ملخص المبيعات: المراقب فقط
  if (to.path === '/sales-manager-sales-summary' && !isSupervisorRole(userType)) {
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
    return next('/sales-manager-employees')
  }
  if (userType === 'مدير مبيعات'
    && (to.path === '/sales-manager-followers-map'
      || to.path === '/sales-manager-followers-routes')) {
    return next('/sales-manager-employees')
  }

  const followerGpsPaths = [
    '/sales-manager-followers-map',
    '/sales-manager-followers-routes',
  ]

  if (followerGpsPaths.includes(to.path)
    && !isSupervisorRole(userType)
    && userType !== 'المدير'
    && !isCommitteeRole(userType)) {
    return next(homePath())
  }

  // طلبات البيع: مدير المبيعات + المدير + المراقب
  if (to.path === '/sales-manager-requests'
    && userType !== 'مدير مبيعات'
    && userType !== 'المدير'
    && !isSupervisorRole(userType)) {
    return next(homePath())
  }

  if (to.path === '/director-ratings'
    && userType !== 'المدير'
    && !isSupervisorRole(userType)) {
    return next(homePath())
  }

  if (to.path === '/accountant-ratings' && userType !== 'محاسب رئيسي') {
    return next(homePath())
  }

  // تطبيق المسوّقين: المدير + المراقب فقط
  if ((isCustomerAppPath(to.path) || isPromotersAppPath(to.path))
    && userType !== 'المدير'
    && !isSupervisorRole(userType)) {
    return next(homePath())
  }

  const directorAllowed = [
    '/director-ratings',
    '/director-summary',
    '/director-delegate-followup',
    '/director-delegate-followup-customers',
    '/director-bad-customers',
    '/director-sales-followup',
    '/director-follower-work',
    '/director-tutorial-videos',
    '/director-announcements',
    '/director-assigned-tasks',
    '/director-branch-visits',
    '/director-committee-tasks',
    '/director-notes',
    '/director-waivers',
    '/branch-management',
    '/sales-manager-dashboard',
    '/sales-manager-customer-profile',
    '/sales-manager-requests',
    '/sales-manager-followers-map',
    '/sales-manager-followers-routes',
    '/oversight-accounts',
    '/legal-desk-overview',
    '/legal-desk-lists',
    '/legal-desk-debtors',
    '/legal-desk-debtor',
    '/legal-desk-payments',
    '/legal-desk-cashbox',
    '/legal-desk-liability',
    '/legal-desk-stalled',

    // تطبيق المسوّقين (BE_CustomerApp) — كامل الصلاحيات للمدير
    ...customerAppPaths,
    ...promotersAppPaths,
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
    '/director-bad-customers',
    '/director-sales-followup',
    '/director-follower-work',
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
    '/director-announcements',
    '/branch-management',

    // تطبيق المسوّقين (BE_CustomerApp)
    ...customerAppPaths,
    ...promotersAppPaths,
  ]

  if (isSupervisorRole(userType) && !monitorAllowed.includes(to.path) && to.path !== '/oversight-accounts' && to.path !== '/logout') {
    return next({ path: '/director-ratings', query: {} })
  }

  const committeeAllowed = [
    '/director-delegate-followup',
    '/director-delegate-followup-customers',
    '/director-bad-customers',
    '/director-sales-followup',
    '/director-follower-work',
    '/sales-manager-followers-map',
    '/sales-manager-followers-routes',
    '/committee-archive',
    '/branch-management',
  ]

  if (isCommitteeRole(userType) && to.path === '/director-follower-work' && to.query?.tab === 'collection')
    return next({ path: '/director-follower-work', query: { ...to.query, tab: 'sales' } })

  if (isCommitteeRole(userType) && !committeeAllowed.includes(to.path) && to.path !== '/logout') {
    return next('/director-delegate-followup')
  }

  if (to.path === '/director-announcements' && userType !== 'المدير' && !isSupervisorRole(userType))
    return next(homePath())

  if ((to.path === '/director-tutorial-videos'
    || to.path === '/director-assigned-tasks'
    || to.path === '/director-branch-visits'
    || to.path === '/director-committee-tasks'
    || to.path === '/director-notes'
    || to.path === '/director-waivers')
    && userType !== 'المدير') {
    return next(homePath())
  }

  // ادارة الفرع: المدير + مسؤول المراقبين + لجنة المراقبة (رئيس + مساعد) فقط
  if (to.path === '/branch-management' && !isOversightBoardRole(userType))
    return next(homePath())

  if (to.path === '/director-summary' && userType !== 'المدير') {
    return next(homePath())
  }

  if (to.path === '/sales-manager-customer-profile'
    && userType !== 'مدير مبيعات'
    && userType !== 'المدير'
    && !isSupervisorRole(userType)) {
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
