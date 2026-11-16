<template>
  <div>
    <div class="d-flex align-center flex-wrap ga-2 mb-3">
      <h1 class="text-h6 font-weight-bold">أرباحي</h1>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-cash-fast" :disabled="!canRequestPayout" @click="openPayout">طلب صرف</v-btn>
    </div>

    <template v-if="summaryLoading">
      <v-row dense><v-col v-for="n in 6" :key="n" cols="6" md="4"><v-skeleton-loader type="list-item-two-line" class="rounded-xl" /></v-col></v-row>
    </template>
    <ErrorState v-else-if="summaryError" :message="summaryError" @retry="loadSummary" />
    <template v-else-if="s">
      <v-row dense class="mb-2">
        <v-col cols="12" md="4"><StatCard highlight icon="mdi-wallet-outline" label="الرصيد القابل للصرف" :value="formatMoney(s.balance)" :sub="`الحد الأدنى للصرف ${formatMoney(s.minPayoutAmount)}`" /></v-col>
        <v-col cols="6" md="4"><StatCard icon="mdi-sigma" label="إجمالي الأرباح" :value="formatMoney(s.grandTotal)" /></v-col>
        <v-col cols="6" md="4"><StatCard icon="mdi-account-cash-outline" label="من مبيعاتك المباشرة" :value="formatMoney(s.directTotal)" /></v-col>
        <v-col cols="6" md="3"><StatCard icon="mdi-account-group-outline" label="من شبكتك" :value="formatMoney(s.networkTotal)" color="indigo" /></v-col>
        <v-col cols="6" md="3"><StatCard icon="mdi-timer-sand" label="معلقة" :value="formatMoney(s.pending)" color="warning" /></v-col>
        <v-col cols="6" md="3"><StatCard icon="mdi-check-circle-outline" label="مستحقة للصرف" :value="formatMoney(s.available)" color="success" /></v-col>
        <v-col cols="6" md="3"><StatCard icon="mdi-bank-transfer-out" label="مصروفة" :value="formatMoney(s.paid)" /></v-col>
      </v-row>
      <v-alert v-if="s.frozen > 0" type="info" variant="tonal" density="compact" class="mb-3" icon="mdi-snowflake">
        لديك أرباح مجمدة بقيمة <b class="num">{{ formatMoney(s.frozen) }}</b>. تواصل مع خدمة العملاء للاستفسار.
      </v-alert>
      <v-alert v-if="s.pendingPayoutRequest" type="info" variant="tonal" class="mb-3" icon="mdi-progress-clock">
        لديك طلب صرف مفتوح بمبلغ <b class="num">{{ formatMoney(s.pendingPayoutRequest.amount) }}</b>
        ({{ s.pendingPayoutRequest.statusLabel || PAYOUT_STATUS[s.pendingPayoutRequest.status]?.label }}). يمكنك تقديم طلب جديد بعد معالجته.
      </v-alert>
      <v-alert type="info" variant="text" density="compact" class="mb-4 text-body-2 px-0" icon="mdi-information-outline">
        تُحتسب العمولات فقط على المبيعات المكتملة، وتبقى «معلقة» لفترة قبل أن تصبح مستحقة. قد تُلغى العمولة إذا أُلغي البيع أو أُرجع المنتج.
        <router-link :to="{ name: 'terms', hash: '#referral' }">الشروط</router-link>
      </v-alert>
    </template>

    <!-- Entries -->
    <v-card class="soft-card mb-4" rounded="xl">
      <v-card-title class="d-flex align-center flex-wrap ga-2 pt-4">
        <span class="font-weight-bold">سجل العمولات</span>
        <v-spacer />
        <v-select v-model="kind" :items="kindItems" density="compact" hide-details class="filter" label="النوع" />
        <v-select v-model="status" :items="statusItems" density="compact" hide-details class="filter" label="الحالة" />
      </v-card-title>
      <v-skeleton-loader v-if="entriesLoading" type="table-row@5" />
      <ErrorState v-else-if="entriesError" :message="entriesError" @retry="loadEntries" />
      <EmptyState v-else-if="!entries.items.length" icon="mdi-cash-remove" title="لا توجد عمولات" text="شارك رابطك أو اطلب منتجات لأصدقائك لتبدأ بالربح." />
      <template v-else>
        <!-- desktop table -->
        <v-table v-if="mdAndUp" density="comfortable">
          <thead>
            <tr><th>التاريخ</th><th>المصدر</th><th>الطلب</th><th>الحالة</th><th class="text-end">المبلغ</th></tr>
          </thead>
          <tbody>
            <tr v-for="e in entries.items" :key="e.id">
              <td class="text-no-wrap">{{ formatDate(e.createdAt) }}</td>
              <td>
                <div class="font-weight-medium">{{ e.sourceLabel || COMMISSION_KIND[e.kind] }}</div>
                <div class="text-caption text-medium-emphasis">{{ e.productSummary }}<template v-if="e.note"> · {{ e.note }}</template></div>
              </td>
              <td class="ltr num text-no-wrap">{{ e.orderNumber || '—' }}</td>
              <td><v-chip size="small" variant="tonal" :color="COMMISSION_STATUS[e.status]?.color">{{ e.statusLabel || COMMISSION_STATUS[e.status]?.label }}</v-chip></td>
              <td class="text-end num font-weight-bold" :class="e.amount < 0 ? 'text-error' : 'text-success'">{{ formatMoney(e.amount) }}</td>
            </tr>
          </tbody>
        </v-table>
        <!-- mobile list -->
        <v-list v-else class="py-0">
          <template v-for="(e, i) in entries.items" :key="e.id">
            <v-divider v-if="i" />
            <v-list-item class="py-3">
              <v-list-item-title class="font-weight-bold text-body-2">{{ e.sourceLabel || COMMISSION_KIND[e.kind] }}</v-list-item-title>
              <v-list-item-subtitle class="text-caption" style="white-space: normal">
                {{ e.productSummary }} · {{ formatDate(e.createdAt) }}
                <span v-if="e.orderNumber" class="ltr"> · {{ e.orderNumber }}</span>
              </v-list-item-subtitle>
              <template #append>
                <div class="text-end">
                  <div class="num font-weight-bold" :class="e.amount < 0 ? 'text-error' : 'text-success'">{{ formatMoney(e.amount) }}</div>
                  <v-chip size="x-small" variant="tonal" :color="COMMISSION_STATUS[e.status]?.color">{{ e.statusLabel || COMMISSION_STATUS[e.status]?.label }}</v-chip>
                </div>
              </template>
            </v-list-item>
          </template>
        </v-list>
        <div v-if="pageCount > 1" class="pa-2"><v-pagination v-model="page" :length="pageCount" density="comfortable" rounded="circle" :total-visible="5" /></div>
      </template>
    </v-card>

    <!-- Payout history -->
    <v-card class="soft-card" rounded="xl">
      <v-card-title class="font-weight-bold pt-4">طلبات الصرف</v-card-title>
      <v-skeleton-loader v-if="payoutsLoading" type="list-item-two-line@2" />
      <EmptyState v-else-if="!payouts.length" icon="mdi-bank-off-outline" title="لا توجد طلبات صرف بعد" />
      <v-list v-else class="py-0">
        <template v-for="(p, i) in payouts" :key="p.id">
          <v-divider v-if="i" />
          <v-list-item class="py-3">
            <template #prepend><v-avatar :color="PAYOUT_STATUS[p.status]?.color" variant="tonal"><v-icon>mdi-cash-fast</v-icon></v-avatar></template>
            <v-list-item-title class="num font-weight-bold">{{ formatMoney(p.amount) }}</v-list-item-title>
            <v-list-item-subtitle style="white-space: normal">
              {{ methodLabel(p.method) }} · {{ formatDate(p.createdAt) }}
              <template v-if="p.processedAt"> · عولج {{ formatDate(p.processedAt) }}</template>
              <div v-if="p.note" class="mt-1">{{ p.note }}</div>
            </v-list-item-subtitle>
            <template #append><v-chip size="small" variant="tonal" :color="PAYOUT_STATUS[p.status]?.color">{{ p.statusLabel || PAYOUT_STATUS[p.status]?.label }}</v-chip></template>
          </v-list-item>
        </template>
      </v-list>
    </v-card>

    <!-- Payout dialog -->
    <v-dialog v-model="payoutDialog" max-width="460">
      <v-card rounded="xl">
        <v-card-title class="font-weight-bold pt-4">طلب صرف الأرباح</v-card-title>
        <v-form ref="payoutForm" @submit.prevent="submitPayout">
          <v-card-text>
            <div class="mb-4 text-body-2">الرصيد القابل للصرف: <b class="num text-primary">{{ formatMoney(s?.balance) }}</b></div>
            <v-text-field
              v-model="payout.amount"
              label="المبلغ (د.ع)"
              inputmode="numeric"
              dir="ltr"
              :rules="[amountRule]"
              :hint="`من ${formatMoney(s?.minPayoutAmount)} إلى ${formatMoney(s?.balance)}`"
              persistent-hint
              class="mb-3"
            />
            <v-select v-model="payout.method" :items="PAYOUT_METHODS" label="طريقة الصرف" :rules="[(v) => !!v || 'اختر طريقة الصرف']" />
            <v-text-field
              v-if="payout.method && payout.method !== 'cash'"
              v-model="payout.accountDetails"
              :label="payout.method === 'zaincash' ? 'رقم محفظة زين كاش' : 'رقم بطاقة كي كارد / اسم صاحبها'"
              hint="اختياري — يساعد في سرعة التحويل"
              persistent-hint
            />
            <v-alert v-if="payoutError" type="error" variant="tonal" density="compact" class="mt-3">{{ payoutError }}</v-alert>
          </v-card-text>
          <v-card-actions class="pa-4 pt-0">
            <v-btn variant="text" @click="payoutDialog = false">إلغاء</v-btn>
            <v-spacer />
            <v-btn type="submit" color="primary" variant="flat" :loading="payoutSubmitting">إرسال الطلب</v-btn>
          </v-card-actions>
        </v-form>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useDisplay } from 'vuetify'
import StatCard from '@/components/StatCard.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import { earningsApi } from '@/api'
import { formatMoney, parseMoney, formatNumber } from '@/utils/money'
import { formatDate } from '@/utils/date'
import { COMMISSION_STATUS, COMMISSION_KIND, PAYOUT_STATUS, PAYOUT_METHODS } from '@/utils/enums'
import { apiErrorMessage } from '@/utils/errors'
import { toastSuccess } from '@/composables/useToast'

const { mdAndUp } = useDisplay()
const s = ref(null)
const summaryLoading = ref(true)
const summaryError = ref('')
const entries = ref({ items: [], total: 0, page: 1, pageSize: 20 })
const entriesLoading = ref(true)
const entriesError = ref('')
const page = ref(1)
const kind = ref(null)
const status = ref(null)
const payouts = ref([])
const payoutsLoading = ref(true)
const payoutDialog = ref(false)
const payoutForm = ref()
const payout = reactive({ amount: '', method: null, accountDetails: '' })
const payoutError = ref('')
const payoutSubmitting = ref(false)

const kindItems = [{ title: 'الكل', value: null }, ...Object.entries(COMMISSION_KIND).map(([value, title]) => ({ title, value }))]
const statusItems = [{ title: 'الكل', value: null }, ...Object.entries(COMMISSION_STATUS).map(([value, m]) => ({ title: m.label, value }))]
const pageCount = computed(() => Math.ceil((entries.value.total || 0) / (entries.value.pageSize || 20)))
const canRequestPayout = computed(() => s.value && !s.value.pendingPayoutRequest && s.value.balance >= s.value.minPayoutAmount)
const methodLabel = (m) => PAYOUT_METHODS.find((x) => x.value === m)?.title || m

const amountRule = (v) => {
  const n = parseMoney(v)
  if (!n) return 'أدخل المبلغ'
  if (n < s.value.minPayoutAmount) return `أقل مبلغ ${formatMoney(s.value.minPayoutAmount)}`
  if (n > s.value.balance) return 'المبلغ أكبر من رصيدك'
  return true
}

async function loadSummary() {
  summaryLoading.value = true
  summaryError.value = ''
  try {
    s.value = await earningsApi.summary()
  } catch (e) {
    summaryError.value = apiErrorMessage(e)
  } finally {
    summaryLoading.value = false
  }
}
async function loadEntries() {
  entriesLoading.value = true
  entriesError.value = ''
  try {
    entries.value = await earningsApi.entries({ kind: kind.value, status: status.value, page: page.value })
  } catch (e) {
    entriesError.value = apiErrorMessage(e)
  } finally {
    entriesLoading.value = false
  }
}
async function loadPayouts() {
  payoutsLoading.value = true
  try {
    payouts.value = await earningsApi.payouts()
  } catch { payouts.value = [] } finally {
    payoutsLoading.value = false
  }
}
watch([kind, status], () => {
  page.value = 1
  loadEntries()
})
watch(page, loadEntries)

function openPayout() {
  payout.amount = formatNumber(s.value.balance)
  payout.method = null
  payout.accountDetails = ''
  payoutError.value = ''
  payoutDialog.value = true
}
async function submitPayout() {
  const { valid } = await payoutForm.value.validate()
  if (!valid) return
  payoutSubmitting.value = true
  payoutError.value = ''
  try {
    await earningsApi.requestPayout({ amount: parseMoney(payout.amount), method: payout.method, accountDetails: payout.accountDetails?.trim() || null })
    payoutDialog.value = false
    toastSuccess('تم إرسال طلب الصرف')
    loadSummary()
    loadPayouts()
  } catch (e) {
    payoutError.value = apiErrorMessage(e)
  } finally {
    payoutSubmitting.value = false
  }
}

onMounted(() => {
  loadSummary()
  loadEntries()
  loadPayouts()
})
</script>

<style scoped>
.filter { max-width: 170px; min-width: 130px; }
</style>
