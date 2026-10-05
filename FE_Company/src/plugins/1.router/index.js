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


function homePath() {
  const t = localStorage.getItem('UserType')
  if (t === 'المدير')
    return '/director-ratings'
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



  if (to.path === '/login') {
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
  if (userType === 'مدير مبيعات' && to.path === '/')
    return next('/sales-manager-dashboard')
  if (userType === 'المدير' && to.path === '/')
    return next('/director-ratings')

  const adminOnlyPaths = [
    '/users-list',
    '/user-active',
    '/backup-database',
    '/accountant-legal-search',
    '/accountant-tutorial-video',
  ]
  if (adminOnlyPaths.includes(to.path) && userType !== 'محاسب رئيسي') {
    return next(homePath())
  }

  // لوحة ضعفاء التسديد لمدير الفرع فقط — مش للمحاسب
  if (to.path === '/decision-board' && userType !== 'مدير فرع') {
    return next(homePath())
  }

  const decisionPaths = ['/decisions-list', '/legal-conversion-requests']
  if (decisionPaths.includes(to.path) && userType !== 'مدير فرع' && userType !== 'محاسب رئيسي') {
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
  if (salesManagerPaths.includes(to.path) && userType !== 'مدير مبيعات') {
    return next(homePath())
  }

  // تبويبات أُزيلت من قائمة مدير المبيعات — منع الدخول المباشر
  const salesManagerHiddenPaths = [
    '/sales-manager-ratings',
    '/sales-manager-followers',
    '/sales-manager-followers-map',
    '/sales-manager-followers-routes',
    '/sales-manager-purchases',
    '/sales-manager-tutorial-links',
  ]
  if (userType === 'مدير مبيعات' && salesManagerHiddenPaths.includes(to.path)) {
    return next('/sales-manager-dashboard')
  }

  // طلبات البيع: مدير المبيعات + المدير (نفس الصفحة)
  if (to.path === '/sales-manager-requests'
    && userType !== 'مدير مبيعات'
    && userType !== 'المدير') {
    return next(homePath())
  }

  if (to.path === '/director-ratings' && userType !== 'المدير') {
    return next(homePath())
  }

  if (to.path === '/accountant-ratings' && userType !== 'محاسب رئيسي') {
    return next(homePath())
  }

  const directorAllowed = ['/director-ratings', '/sales-manager-customer-profile', '/sales-manager-requests']
  // Director: ratings + sales requests + customer profile drill-down
  if (userType === 'المدير' && !directorAllowed.includes(to.path) && to.path !== '/logout') {
    return next('/director-ratings')
  }

  if (to.path === '/sales-manager-customer-profile'
    && userType !== 'مدير مبيعات'
    && userType !== 'المدير') {
    return next(homePath())
  }

  const followerTrackingPaths = ['/follower-tracking-map', '/follower-tracking-routes']
  if (followerTrackingPaths.includes(to.path) && userType !== 'محاسب رئيسي') {
    return next(homePath())
  }

  next()
})
export { router }
export default function (app) {
  app.use(router)
}
