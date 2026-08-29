import { createRouter, createWebHistory } from 'vue-router'
import PortalView from '../views/PortalView.vue'

const routes = [
  {
    path: '/',
    name: 'portal',
    component: PortalView,
  },
  // صفحة 404
  {
    path: '/:pathMatch(.*)*',
    redirect: '/',
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

export default router
