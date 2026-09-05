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
  return localStorage.getItem('UserType') === 'مدير مبيعات'
    ? '/sales-manager-dashboard'
    : '/'
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

  const adminOnlyPaths = ['/users-list', '/user-active', '/backup-database']
  if (adminOnlyPaths.includes(to.path) && userType !== 'محاسب رئيسي') {
    return next(homePath())
  }

  const decisionPaths = ['/decision-board', '/decisions-list']
  if (decisionPaths.includes(to.path) && userType !== 'مدير فرع' && userType !== 'محاسب رئيسي') {
    return next(homePath())
  }

  const salesManagerPaths = ['/sales-manager-dashboard', '/sales-manager-employees', '/sales-manager-map', '/sales-manager-routes', '/sales-manager-sales', '/sales-manager-requests', '/sales-manager-request-create']
  if (salesManagerPaths.includes(to.path) && userType !== 'مدير مبيعات') {
    return next(homePath())
  }

  next()
})
export { router }
export default function (app) {
  app.use(router)
}
