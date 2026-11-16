<template>
  <div>
    <div class="d-flex align-center flex-wrap ga-2 mb-3">
      <h1 class="text-h6 font-weight-bold">طلباتي</h1>
      <v-spacer />
      <v-btn icon="mdi-refresh" variant="text" size="small" :loading="loading" aria-label="تحديث" @click="load(true)" />
    </div>
    <div class="chips hide-scrollbar mb-4">
      <v-chip v-for="f in statusFilters" :key="f.value" :color="status === f.value ? 'primary' : undefined" :variant="status === f.value ? 'flat' : 'outlined'" @click="setStatus(f.value)">
        {{ f.title }}
      </v-chip>
    </div>

    <ErrorState v-if="error && !items.length" :message="error" @retry="load(true)" />
    <template v-else-if="loading && !items.length">
      <v-skeleton-loader v-for="n in 4" :key="n" type="list-item-three-line" class="mb-3 rounded-xl" />
    </template>
    <EmptyState v-else-if="!items.length" icon="mdi-package-variant" title="لا توجد طلبات" :text="status ? 'لا توجد طلبات بهذه الحالة.' : 'لم تقدّم أي طلب بعد. تصفّح المنتجات واطلب بسهولة.'">
      <v-btn color="primary" :to="{ name: 'catalog' }">تصفّح المنتجات</v-btn>
    </EmptyState>
    <template v-else>
      <v-card v-for="o in items" :key="o.id" class="soft-card mb-3 order-card" rounded="xl" :to="{ name: 'order', params: { id: o.id } }">
        <v-card-text class="pa-4">
          <div class="d-flex align-center flex-wrap ga-2 mb-2">
            <span class="font-weight-bold ltr num">{{ o.orderNumber }}</span>
            <StatusChip :status="o.status" :label="o.statusLabel" />
            <v-spacer />
            <span class="text-caption text-medium-emphasis">{{ formatDate(o.createdAt) }}</span>
          </div>
          <div class="font-weight-medium mb-1 clamp-2">{{ o.itemsSummary }}</div>
          <div class="d-flex flex-wrap ga-3 text-body-2 text-medium-emphasis">
            <span><v-icon size="16">mdi-tag-outline</v-icon> {{ PURCHASE_TYPES[o.purchaseType] }}</span>
            <span><v-icon size="16">mdi-map-marker-outline</v-icon> {{ o.cityName }}</span>
            <span v-if="!o.forSelf"><v-icon size="16">mdi-account-arrow-right</v-icon> لـ {{ o.customerName }}</span>
            <v-spacer />
            <b class="text-primary num">{{ formatMoney(o.estimatedTotal) }}</b>
          </div>
        </v-card-text>
      </v-card>
      <div class="d-flex justify-center py-3">
        <v-btn v-if="hasMore" variant="tonal" color="primary" :loading="loading" @click="load(false)">عرض المزيد</v-btn>
      </div>
    </template>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import StatusChip from '@/components/StatusChip.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import { ordersApi } from '@/api'
import { formatMoney } from '@/utils/money'
import { formatDate } from '@/utils/date'
import { PURCHASE_TYPES, ORDER_STATUS } from '@/utils/enums'
import { apiErrorMessage } from '@/utils/errors'

const route = useRoute()
const router = useRouter()
const statusFilters = [{ title: 'الكل', value: '' }, ...Object.entries(ORDER_STATUS).map(([value, m]) => ({ title: m.label, value }))]
const status = ref(route.query.status || '')
const items = ref([])
const total = ref(0)
const page = ref(0)
const loading = ref(false)
const error = ref('')
const hasMore = computed(() => items.value.length < total.value)

async function load(reset) {
  if (reset) {
    page.value = 0
    items.value = []
  }
  loading.value = true
  error.value = ''
  try {
    const res = await ordersApi.list({ status: status.value, page: page.value + 1 })
    page.value = res.page
    total.value = res.total
    items.value = reset ? res.items : [...items.value, ...res.items]
  } catch (e) {
    error.value = apiErrorMessage(e)
  } finally {
    loading.value = false
  }
}
function setStatus(v) {
  status.value = v
  router.replace({ query: v ? { status: v } : {} })
  load(true)
}
onMounted(() => load(true))
</script>

<style scoped>
.chips { display: flex; gap: 8px; overflow-x: auto; }
.chips .v-chip { flex: 0 0 auto; }
</style>
