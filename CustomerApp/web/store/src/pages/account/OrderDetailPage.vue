<template>
  <div>
    <v-btn variant="text" prepend-icon="mdi-arrow-right" class="mb-2 px-1" :to="{ name: 'orders' }">طلباتي</v-btn>
    <v-skeleton-loader v-if="loading" type="heading, list-item-three-line, list-item-three-line, table" class="rounded-xl" />
    <EmptyState v-else-if="notFound" icon="mdi-file-question-outline" title="الطلب غير موجود" />
    <ErrorState v-else-if="error" :message="error" @retry="load" />
    <template v-else-if="o">
      <v-card class="soft-card mb-4" rounded="xl">
        <v-card-text class="pa-4 pa-md-5">
          <div class="d-flex align-center flex-wrap ga-2">
            <div>
              <div class="text-caption text-medium-emphasis">رقم الطلب</div>
              <div class="text-h6 font-weight-bold ltr num">{{ o.orderNumber }}</div>
            </div>
            <v-spacer />
            <StatusChip :status="o.status" :label="o.statusLabel" />
          </div>
          <div class="text-body-2 text-medium-emphasis mt-2">
            قُدّم في {{ formatDateTime(o.createdAt) }} · آخر تحديث {{ formatDateTime(o.updatedAt) }}
          </div>
          <v-alert v-if="o.publicReason" type="error" variant="tonal" class="mt-4" icon="mdi-information-outline">
            <b>السبب:</b> {{ o.publicReason }}
          </v-alert>
          <div v-if="o.canCancel" class="mt-4">
            <v-btn color="error" variant="tonal" prepend-icon="mdi-close-circle-outline" @click="cancelDialog = true">إلغاء الطلب</v-btn>
          </div>
        </v-card-text>
      </v-card>

      <v-row>
        <v-col cols="12" md="7">
          <v-card class="soft-card mb-4" rounded="xl">
            <v-card-title class="font-weight-bold pt-4">المنتجات</v-card-title>
            <v-list class="py-0">
              <v-list-item v-for="it in o.items" :key="it.productId" :to="{ name: 'product', params: { id: it.productId } }" class="py-2">
                <template #prepend><v-avatar rounded="lg" size="56" class="me-3"><v-img :src="it.thumbnailUrl" cover /></v-avatar></template>
                <v-list-item-title class="font-weight-bold" style="white-space: normal">{{ it.name }}</v-list-item-title>
                <v-list-item-subtitle class="num">
                  الكمية {{ it.quantity }} ·
                  <template v-if="it.dailyInstallment">{{ formatMoney(it.dailyInstallment) }} يومياً × {{ it.installmentDays }} يوم</template>
                  <template v-else>{{ formatMoney(it.unitPrice) }}</template>
                </v-list-item-subtitle>
              </v-list-item>
            </v-list>
            <v-divider />
            <v-card-text class="d-flex justify-space-between">
              <span>الإجمالي التقديري ({{ PURCHASE_TYPES[o.purchaseType] }})</span>
              <b class="text-primary num">{{ formatMoney(o.estimatedTotal) }}</b>
            </v-card-text>
          </v-card>

          <v-card class="soft-card mb-4" rounded="xl">
            <v-card-title class="font-weight-bold pt-4">تفاصيل الطلب</v-card-title>
            <v-card-text>
              <dl class="kv">
                <dt>المشتري</dt>
                <dd>{{ customerName }} <v-chip v-if="!o.forSelf" size="x-small" variant="tonal" class="ms-1">لشخص آخر</v-chip></dd>
                <dt>الهاتف</dt>
                <dd class="ltr num">{{ formatPhone(o.customer?.phone) }}<template v-if="o.customer?.secondaryPhone"> / {{ formatPhone(o.customer.secondaryPhone) }}</template></dd>
                <dt>الفرع</dt><dd>{{ o.cityName }}</dd>
                <dt>العنوان</dt>
                <dd>{{ o.address?.area }} — {{ o.address?.details }}<template v-if="o.address?.nearestLandmark"> (قرب {{ o.address.nearestLandmark }})</template></dd>
                <template v-if="o.shop">
                  <dt>المحل</dt>
                  <dd>{{ o.shop.name }} — {{ o.shop.businessType }} · {{ o.shop.ownsShop ? 'مُلك' : 'مستأجر' }}<template v-if="o.shop.address"> · {{ o.shop.address }}</template></dd>
                </template>
                <template v-if="o.notes"><dt>ملاحظات</dt><dd>{{ o.notes }}</dd></template>
              </dl>
            </v-card-text>
          </v-card>
        </v-col>

        <v-col cols="12" md="5">
          <v-card class="soft-card" rounded="xl">
            <v-card-title class="font-weight-bold pt-4">مراحل الطلب</v-card-title>
            <v-card-text>
              <v-timeline side="end" density="compact" align="start" truncate-line="both">
                <v-timeline-item
                  v-for="(t, i) in timeline"
                  :key="i"
                  :dot-color="statusMeta(t.status).color"
                  :icon="statusMeta(t.status).icon"
                  size="small"
                  fill-dot
                >
                  <div class="font-weight-bold">{{ t.statusLabel || statusMeta(t.status).label }}</div>
                  <div class="text-caption text-medium-emphasis">{{ formatDateTime(t.at) }}</div>
                  <div v-if="t.note" class="text-body-2 mt-1">{{ t.note }}</div>
                </v-timeline-item>
              </v-timeline>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </template>

    <v-dialog v-model="cancelDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="font-weight-bold pt-4">إلغاء الطلب</v-card-title>
        <v-card-text>
          <p class="mb-3">هل أنت متأكد من إلغاء الطلب <b class="ltr">{{ o?.orderNumber }}</b>؟ لا يمكن التراجع عن الإلغاء.</p>
          <v-textarea v-model="cancelReason" label="سبب الإلغاء (اختياري)" rows="2" auto-grow maxlength="300" />
          <v-alert v-if="cancelError" type="error" variant="tonal" density="compact">{{ cancelError }}</v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-btn variant="text" @click="cancelDialog = false">تراجع</v-btn>
          <v-spacer />
          <v-btn color="error" variant="flat" :loading="cancelling" @click="cancel">تأكيد الإلغاء</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import StatusChip from '@/components/StatusChip.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import { ordersApi } from '@/api'
import { formatMoney } from '@/utils/money'
import { formatDateTime } from '@/utils/date'
import { formatPhone } from '@/utils/phone'
import { PURCHASE_TYPES, ORDER_STATUS } from '@/utils/enums'
import { apiErrorMessage } from '@/utils/errors'
import { toastSuccess } from '@/composables/useToast'

const route = useRoute()
const o = ref(null)
const loading = ref(true)
const error = ref('')
const notFound = ref(false)
const cancelDialog = ref(false)
const cancelReason = ref('')
const cancelling = ref(false)
const cancelError = ref('')

const statusMeta = (s) => ORDER_STATUS[s] || { color: 'grey', icon: 'mdi-circle-small', label: s }
const timeline = computed(() => (o.value?.timeline || []).slice().sort((a, b) => new Date(b.at) - new Date(a.at)))
const customerName = computed(() => {
  const c = o.value?.customer
  return c ? [c.firstName, c.fatherName, c.grandfatherName, c.surname].filter(Boolean).join(' ') : o.value?.customerName
})

async function load() {
  loading.value = true
  error.value = ''
  try {
    o.value = await ordersApi.get(route.params.id)
  } catch (e) {
    if (e.response?.status === 404) notFound.value = true
    else error.value = apiErrorMessage(e)
  } finally {
    loading.value = false
  }
}
async function cancel() {
  cancelling.value = true
  cancelError.value = ''
  try {
    o.value = await ordersApi.cancel(o.value.id, cancelReason.value.trim())
    cancelDialog.value = false
    toastSuccess('تم إلغاء الطلب')
  } catch (e) {
    cancelError.value = apiErrorMessage(e)
  } finally {
    cancelling.value = false
  }
}
onMounted(load)
</script>

<style scoped>
.kv { display: grid; grid-template-columns: max-content 1fr; gap: 8px 16px; }
.kv dt { color: rgba(0, 0, 0, 0.6); font-size: 0.9rem; }
.kv dd { font-weight: 500; }
</style>
