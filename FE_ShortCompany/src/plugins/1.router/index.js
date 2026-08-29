import { getToken, isTokenExpired, removeLocalStorage, removeToken } from "@/services/tokenService"
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


router.beforeEach((to, from, next) => {

  if (to.path === "/logout") {
    removeToken()
    removeLocalStorage()
  }

  const token = getToken()
  const isNotAuthenticated = !token && isTokenExpired()



  if (to.path === '/login') {
    if (!isNotAuthenticated)
      return next("/")
    else
      return next()
  }


  // إذا لم يكن المستخدم مصادقًا عليه، اسمح له بالوصول إلى صفحة تسجيل الدخول أو التسجيل فقط
  if (isNotAuthenticated) {
    return next('/login')
  }

  const userType = localStorage.getItem('UserType') || ''
  const decisionPaths = ['/decision-board', '/decisions-list']
  if (decisionPaths.includes(to.path) && userType !== 'مدير فرع' && userType !== 'محاسب رئيسي') {
    return next('/')
  }

  next()
})
export { router }
export default function (app) {
  app.use(router)
}
