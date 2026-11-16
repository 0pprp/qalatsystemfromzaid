<template>
  <v-card class="product-card soft-card h-100 d-flex flex-column" :to="{ name: 'product', params: { id: product.id } }" hover>
    <div class="img-wrap">
      <v-img :src="product.thumbnailUrl" :alt="product.name" aspect-ratio="1" cover class="bg-grey-lighten-4">
        <template #placeholder><v-skeleton-loader type="image" class="h-100" /></template>
        <template #error>
          <div class="d-flex align-center justify-center h-100 bg-grey-lighten-3"><v-icon size="48" color="grey">mdi-image-off-outline</v-icon></div>
        </template>
      </v-img>
      <v-chip v-if="product.badge" class="badge" color="secondary" variant="flat" size="small" label>{{ product.badge }}</v-chip>
      <v-chip v-if="product.allowInstallments" class="inst-chip" color="primary" variant="flat" size="x-small">
        <v-icon start size="12">mdi-calendar-clock</v-icon>أقساط يومية
      </v-chip>
    </div>
    <v-card-text class="pa-3 d-flex flex-column flex-grow-1">
      <div class="text-caption text-medium-emphasis mb-1">{{ product.categoryName }}</div>
      <div class="name clamp-2 mb-2">{{ product.name }}</div>
      <v-spacer />
      <div v-if="product.allowCash" class="price num">{{ formatMoney(product.cashPrice) }}</div>
      <div v-else class="text-caption text-medium-emphasis">غير متاح نقداً</div>
      <div v-if="product.allowInstallments && product.dailyInstallment" class="daily num">
        أو <b>{{ formatMoney(product.dailyInstallment) }}</b> يومياً × {{ product.installmentDays }} يوم
      </div>
    </v-card-text>
    <v-card-actions class="pa-3 pt-0">
      <v-btn block color="primary" variant="tonal" size="small" prepend-icon="mdi-cart-plus" @click.prevent.stop="addToCart">
        {{ cart.has(product.id) ? 'في السلة' : 'أضف للسلة' }}
      </v-btn>
    </v-card-actions>
  </v-card>
</template>

<script setup>
import { formatMoney } from '@/utils/money'
import { useCartStore } from '@/stores/cart'
import { toast } from '@/composables/useToast'

const props = defineProps({ product: { type: Object, required: true } })
const cart = useCartStore()
function addToCart() {
  const r = cart.add(props.product, 1)
  toast(r.message, r.ok ? 'success' : 'error')
}
</script>

<style scoped>
.product-card { overflow: hidden; transition: transform 0.15s, box-shadow 0.15s; }
.product-card:hover { transform: translateY(-3px); }
.img-wrap { position: relative; }
.badge { position: absolute; top: 10px; inset-inline-start: 10px; font-weight: 700; }
.inst-chip { position: absolute; bottom: 8px; inset-inline-end: 8px; }
.name { font-weight: 700; font-size: 0.95rem; line-height: 1.5; min-height: 2.9em; color: #1d2b2a; }
.price { font-weight: 800; color: #006a64; font-size: 1.08rem; }
.daily { font-size: 0.78rem; color: #4a5e5c; margin-top: 2px; }
</style>
