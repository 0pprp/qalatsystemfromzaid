import { defineStore } from 'pinia'
import { readJson, writeJson } from '@/utils/storage'
import { MAX_ORDER_LINES, MAX_LINE_QTY } from '@/utils/orderPayload'

const KEY = 'qa.cart'

/** Cart line: { productId, quantity, product: ProductCard snapshot } */
export const useCartStore = defineStore('cart', {
  state: () => ({
    lines: readJson(KEY, []),
    drawer: false,
  }),
  getters: {
    count: (s) => s.lines.reduce((n, l) => n + l.quantity, 0),
    isEmpty: (s) => s.lines.length === 0,
    cashTotal: (s) => s.lines.reduce((n, l) => n + (l.product?.cashPrice || 0) * l.quantity, 0),
    installmentTotal: (s) => s.lines.reduce((n, l) => n + (l.product?.installmentPrice || 0) * l.quantity, 0),
    dailyTotal: (s) => s.lines.reduce((n, l) => n + (l.product?.dailyInstallment || 0) * l.quantity, 0),
    allowCash: (s) => s.lines.every((l) => l.product?.allowCash !== false),
    allowInstallments: (s) => s.lines.every((l) => l.product?.allowInstallments !== false),
    has: (s) => (id) => s.lines.some((l) => l.productId === id),
  },
  actions: {
    persist() {
      writeJson(KEY, this.lines)
    },
    /** returns { ok, message } */
    add(product, quantity = 1) {
      const existing = this.lines.find((l) => l.productId === product.id)
      if (existing) {
        existing.quantity = Math.min(MAX_LINE_QTY, existing.quantity + quantity)
        existing.product = snapshot(product)
      } else {
        if (this.lines.length >= MAX_ORDER_LINES)
          return { ok: false, message: `لا يمكن إضافة أكثر من ${MAX_ORDER_LINES} منتجات مختلفة في الطلب الواحد` }
        this.lines.push({ productId: product.id, quantity: Math.min(MAX_LINE_QTY, quantity), product: snapshot(product) })
      }
      this.persist()
      return { ok: true, message: 'تمت الإضافة إلى السلة' }
    },
    setQuantity(productId, q) {
      const l = this.lines.find((x) => x.productId === productId)
      if (!l) return
      l.quantity = Math.max(1, Math.min(MAX_LINE_QTY, q))
      this.persist()
    },
    remove(productId) {
      this.lines = this.lines.filter((l) => l.productId !== productId)
      this.persist()
    },
    clear() {
      this.lines = []
      this.persist()
    },
  },
})

function snapshot(p) {
  const { id, name, thumbnailUrl, cashPrice, installmentPrice, dailyInstallment, installmentDays, allowCash, allowInstallments, categoryName } = p
  const thumb = thumbnailUrl || p.media?.find((m) => m.type === 'image')?.url || null
  return { id, name, thumbnailUrl: thumb, cashPrice, installmentPrice, dailyInstallment, installmentDays, allowCash, allowInstallments, categoryName }
}
