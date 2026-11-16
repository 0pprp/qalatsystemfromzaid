import { defineStore } from 'pinia'
import { publicApi } from '@/api'

// Caches reference data (branches, categories, banners, app config) and product details.
export const useCatalogStore = defineStore('catalog', {
  state: () => ({
    branches: [],
    categories: [],
    banners: [],
    config: null,
    products: {}, // id -> ProductDetail
    _loading: {},
  }),
  getters: {
    branchName: (s) => (cv) => s.branches.find((b) => b.cityValue === cv)?.name ?? '',
    categoryName: (s) => (id) => s.categories.find((c) => c.id === Number(id))?.name ?? '',
    supportPhone: (s) => s.config?.supportPhone || '',
    supportWhatsapp: (s) => s.config?.supportWhatsapp || '',
    installmentsNotice: (s) => s.config?.installmentsNotice || 'الأقساط اليومية متاحة فقط لأصحاب المحلات التجارية',
    referralEnabled: (s) => s.config?.referralEnabled !== false,
  },
  actions: {
    async _once(key, loader) {
      if (this._loading[key]) return this._loading[key]
      this._loading[key] = loader().catch((e) => {
        delete this._loading[key]
        throw e
      })
      return this._loading[key]
    },
    loadBranches(force = false) {
      if (this.branches.length && !force) return Promise.resolve(this.branches)
      return this._once('branches', async () => (this.branches = await publicApi.branches()))
    },
    loadCategories(force = false) {
      if (this.categories.length && !force) return Promise.resolve(this.categories)
      return this._once('categories', async () =>
        (this.categories = (await publicApi.categories()).slice().sort((a, b) => a.sortOrder - b.sortOrder)))
    },
    loadBanners(force = false) {
      if (this.banners.length && !force) return Promise.resolve(this.banners)
      return this._once('banners', async () => (this.banners = await publicApi.banners()))
    },
    loadConfig() {
      if (this.config) return Promise.resolve(this.config)
      return this._once('config', async () => (this.config = await publicApi.appConfig()))
    },
    async getProduct(id, force = false) {
      id = Number(id)
      if (this.products[id] && !force) return this.products[id]
      const p = await publicApi.product(id)
      this.products[id] = p
      return p
    },
  },
})
