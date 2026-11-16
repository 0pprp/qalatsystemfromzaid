<template>
  <div class="qa-container py-4 pb-12">
    <h1 class="text-h5 font-weight-bold mb-4">سلة المشتريات</h1>

    <EmptyState v-if="cart.isEmpty" icon="mdi-cart-outline" title="سلتك فارغة" text="تصفّح منتجاتنا وأضف ما يعجبك إلى السلة.">
      <v-btn color="primary" :to="{ name: 'catalog' }">تصفّح المنتجات</v-btn>
    </EmptyState>

    <v-row v-else>
      <v-col cols="12" md="8">
        <v-card class="soft-card" rounded="xl">
          <template v-for="(l, i) in cart.lines" :key="l.productId">
            <v-divider v-if="i" />
            <div class="line pa-3 pa-sm-4">
              <router-link :to="{ name: 'product', params: { id: l.productId } }" class="thumb">
                <v-img :src="l.product?.thumbnailUrl" :alt="l.product?.name" aspect-ratio="1" cover class="rounded-lg bg-grey-lighten-4" />
              </router-link>
              <div class="flex-grow-1 min-w-0">
                <router-link :to="{ name: 'product', params: { id: l.productId } }" class="name clamp-2">{{ l.product?.name }}</router-link>
                <div class="text-caption text-medium-emphasis">{{ l.product?.categoryName }}</div>
                <div class="mt-1 text-body-2 num">
                  <span v-if="l.product?.allowCash" class="text-primary font-weight-bold">{{ formatMoney(l.product.cashPrice) }}</span>
                  <span v-if="l.product?.allowInstallments" class="text-medium-emphasis ms-2">أو {{ formatMoney(l.product.dailyInstallment) }} يومياً × {{ l.product.installmentDays }}</span>
                </div>
                <div class="d-flex flex-wrap ga-1 mt-1">
                  <v-chip v-if="l.product?.allowCash === false" size="x-small" color="warning" variant="tonal">أقساط فقط</v-chip>
                  <v-chip v-if="l.product?.allowInstallments === false" size="x-small" color="info" variant="tonal">نقد فقط</v-chip>
                  <v-chip v-if="unavailable[l.productId]" size="x-small" color="error" variant="tonal">لم يعد متوفراً</v-chip>
                </div>
              </div>
              <div class="d-flex flex-column align-end ga-2">
                <div class="qty">
                  <v-btn icon="mdi-minus" size="x-small" variant="text" :disabled="l.quantity <= 1" aria-label="إنقاص" @click="cart.setQuantity(l.productId, l.quantity - 1)" />
                  <span class="num font-weight-bold mx-2">{{ l.quantity }}</span>
                  <v-btn icon="mdi-plus" size="x-small" variant="text" :disabled="l.quantity >= MAX_LINE_QTY" aria-label="زيادة" @click="cart.setQuantity(l.productId, l.quantity + 1)" />
                </div>
                <v-btn size="small" variant="text" color="error" prepend-icon="mdi-delete-outline" @click="cart.remove(l.productId)">حذف</v-btn>
              </div>
            </div>
          </template>
        </v-card>
        <div class="text-caption text-medium-emphasis mt-2">
          الحد الأقصى {{ MAX_ORDER_LINES }} منتجات مختلفة في الطلب، و{{ MAX_LINE_QTY }} قطع من كل منتج.
        </div>
      </v-col>

      <v-col cols="12" md="4">
        <v-card class="soft-card summary" rounded="xl">
          <v-card-title class="font-weight-bold pt-4">ملخص السلة</v-card-title>
          <v-card-text>
            <div class="row"><span>عدد القطع</span><b class="num">{{ cart.count }}</b></div>
            <div v-if="cart.allowCash" class="row"><span>الإجمالي نقداً</span><b class="num text-primary">{{ formatMoney(cart.cashTotal) }}</b></div>
            <template v-if="cart.allowInstallments">
              <v-divider class="my-2" />
              <div class="row"><span>القسط اليومي التقريبي</span><b class="num">{{ formatMoney(cart.dailyTotal) }}</b></div>
              <div class="row"><span>الإجمالي بالأقساط</span><b class="num">{{ formatMoney(cart.installmentTotal) }}</b></div>
            </template>
            <v-alert v-if="!cart.allowCash && !cart.allowInstallments" type="error" variant="tonal" density="compact" class="mt-3">
              السلة تحتوي منتجات نقد فقط ومنتجات أقساط فقط. يرجى تقسيمها إلى طلبين.
            </v-alert>
            <v-alert v-else-if="!cart.allowCash" type="info" variant="tonal" density="compact" class="mt-3">بعض المنتجات متاحة بالأقساط فقط، لذا سيكون الطلب بالأقساط اليومية.</v-alert>
            <v-alert v-else-if="!cart.allowInstallments" type="info" variant="tonal" density="compact" class="mt-3">بعض المنتجات متاحة نقداً فقط، لذا سيكون الطلب نقداً.</v-alert>
            <v-alert type="warning" variant="tonal" density="compact" class="mt-3" icon="mdi-store-alert-outline">{{ INSTALLMENTS_NOTICE }}</v-alert>
            <p class="text-caption text-medium-emphasis mt-3">الأسعار تقديرية؛ يؤكد مندوب المبيعات السعر النهائي عند التواصل معك.</p>
          </v-card-text>
          <v-card-actions class="pa-4 pt-0 flex-column ga-2">
            <v-btn block color="primary" size="large" :disabled="!cart.allowCash && !cart.allowInstallments" @click="checkout">متابعة الطلب</v-btn>
            <v-btn block variant="text" :to="{ name: 'catalog' }">متابعة التسوّق</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup>
import { reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import EmptyState from '@/components/EmptyState.vue'
import { useCartStore } from '@/stores/cart'
import { useAuthStore } from '@/stores/auth'
import { useCatalogStore } from '@/stores/catalog'
import { formatMoney } from '@/utils/money'
import { INSTALLMENTS_NOTICE } from '@/utils/enums'
import { MAX_LINE_QTY, MAX_ORDER_LINES } from '@/utils/orderPayload'

const cart = useCartStore()
const auth = useAuthStore()
const catalog = useCatalogStore()
const router = useRouter()
const unavailable = reactive({})

function checkout() {
  if (auth.isLoggedIn) router.push({ name: 'checkout' })
  else auth.openLogin({ name: 'checkout' }, 'سجّل دخولك لإكمال الطلب. سلتك محفوظة.')
}

// Refresh cached product snapshots (prices/availability may have changed).
onMounted(async () => {
  await Promise.all(
    cart.lines.map(async (l) => {
      try {
        const p = await catalog.getProduct(l.productId, true)
        l.product = { ...l.product, ...pick(p) }
      } catch (e) {
        if (e.response?.status === 404) unavailable[l.productId] = true
      }
    }),
  )
  cart.persist()
})
function pick(p) {
  const { name, thumbnailUrl, cashPrice, installmentPrice, dailyInstallment, installmentDays, allowCash, allowInstallments, categoryName } = p
  return { name, thumbnailUrl: thumbnailUrl || p.media?.find((m) => m.type === 'image')?.url, cashPrice, installmentPrice, dailyInstallment, installmentDays, allowCash, allowInstallments, categoryName }
}
</script>

<style scoped>
.line { display: flex; gap: 12px; align-items: flex-start; }
.thumb { flex: 0 0 84px; width: 84px; }
.name { font-weight: 700; color: inherit; text-decoration: none; }
.min-w-0 { min-width: 0; }
.qty { display: flex; align-items: center; border: 1px solid rgba(0, 0, 0, 0.12); border-radius: 10px; padding: 2px 4px; }
.row { display: flex; justify-content: space-between; padding: 6px 0; }
@media (min-width: 960px) { .summary { position: sticky; top: 92px; } }
</style>
