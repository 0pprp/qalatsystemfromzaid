<template>
  <div class="qa-container py-8 pb-12">
    <v-card class="soft-card mx-auto text-center pa-6 pa-md-10" max-width="620" rounded="xl">
      <v-avatar color="success" variant="tonal" size="96" class="mb-4"><v-icon size="56">mdi-check-circle</v-icon></v-avatar>
      <h1 class="text-h5 font-weight-bold mb-2">تم إرسال طلبك بنجاح!</h1>
      <p class="text-body-1 text-medium-emphasis mb-4">شكراً لك. سيراجع فريقنا طلبك ويتواصل معك مندوب المبيعات قريباً.</p>
      <div class="order-no mb-5">
        <div class="text-caption text-medium-emphasis">رقم الطلب</div>
        <div class="d-flex align-center justify-center ga-2">
          <span class="text-h5 font-weight-bold ltr num">{{ orderNumber || '...' }}</span>
          <v-btn v-if="orderNumber" icon="mdi-content-copy" size="small" variant="text" aria-label="نسخ رقم الطلب" @click="copy" />
        </div>
      </div>
      <v-skeleton-loader v-if="loading" type="list-item-two-line" />
      <div v-else-if="order" class="text-start mb-5">
        <div class="d-flex justify-space-between py-1"><span>الحالة</span><StatusChip :status="order.status" :label="order.statusLabel" /></div>
        <div class="d-flex justify-space-between py-1"><span>طريقة الشراء</span><b>{{ PURCHASE_TYPES[order.purchaseType] }}</b></div>
        <div class="d-flex justify-space-between py-1"><span>الفرع</span><b>{{ order.cityName }}</b></div>
        <div class="d-flex justify-space-between py-1"><span>المنتجات</span><b class="text-end">{{ order.itemsSummary }}</b></div>
        <div class="d-flex justify-space-between py-1"><span>الإجمالي التقديري</span><b class="num text-primary">{{ formatMoney(order.estimatedTotal) }}</b></div>
      </div>
      <div class="d-flex flex-wrap justify-center ga-3">
        <v-btn color="primary" size="large" :to="{ name: 'order', params: { id } }" prepend-icon="mdi-file-document-outline">متابعة الطلب</v-btn>
        <v-btn variant="outlined" color="primary" size="large" :to="{ name: 'home' }">العودة للتسوّق</v-btn>
      </div>
    </v-card>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import StatusChip from '@/components/StatusChip.vue'
import { ordersApi } from '@/api'
import { formatMoney } from '@/utils/money'
import { PURCHASE_TYPES } from '@/utils/enums'
import { copyText } from '@/utils/clipboard'
import { toast } from '@/composables/useToast'

const route = useRoute()
const id = route.params.id
const order = ref(null)
const loading = ref(true)
const orderNumber = computed(() => order.value?.orderNumber || route.query.n || '')

async function copy() {
  if (await copyText(orderNumber.value)) toast('تم نسخ رقم الطلب', 'success')
}
onMounted(async () => {
  try {
    order.value = await ordersApi.get(id)
  } catch { /* keep number from query */ } finally {
    loading.value = false
  }
})
</script>

<style scoped>
.order-no { background: #e8f4f3; border-radius: 16px; padding: 12px; }
</style>
