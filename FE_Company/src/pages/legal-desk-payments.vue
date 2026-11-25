<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import LegalDiscountDialog from '@/components/LegalDiscountDialog.vue'
import LegalPayDialog from '@/components/LegalPayDialog.vue'
import LegalReceiptDialog from '@/components/LegalReceiptDialog.vue'
import {
  LEGAL_STATUS,
  formatUtc,
  legalDay,
  legalError,
  legalMoney,
  legalOldReceipts,
  legalPayments,
  legalPersons,
  legalStatus,
} from '@/composables/legalDeskApi'

/**
 * التسديدات: everything محاسب القانونية does with debtors' money —
 * taking a تسديد or giving a خصم to any debtor in the legal app, the records of both,
 * and the receipts of the old legal system.
 */
const TABS = ['pay', 'payments', 'discounts', 'old']

const toast = useToast()
const route = useRoute()
const router = useRouter()

const tab = ref(TABS.includes(route.query.tab) ? route.query.tab : 'pay')

watch(tab, value => router.replace({ query: value === 'pay' ? {} : { tab: value } }))

// —— تسديد: every debtor with an open file in the legal app ——
const FILTERS = ['All', 'Waiting', 'Judicial', 'Execution']

const filter = ref('All')
const debtorQ = ref('')
const debtors = ref([])
const debtorsLoading = ref(false)

const due = d => Number(d.remainingSnapshot || 0) + Number(d.penaltiesCharged || 0) - Number(d.penaltiesPaid || 0)
const paid = d => Number(d.debtPaid || 0) + Number(d.penaltiesPaid || 0) - Number(d.discounts || 0)

const shownDebtors = computed(() => debtors.value.filter(d => {
  const status = legalStatus(d.stage).key
  if (status === 'NotSent' || status === 'Closed')
    return false

  return filter.value === 'All' || status === filter.value
}))

async function loadDebtors() {
  debtorsLoading.value = true
  try {
    debtors.value = await legalPersons({ q: (debtorQ.value || '').trim() })
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل المدينين'))
  }
  finally {
    debtorsLoading.value = false
  }
}

// —— حوار التسديد والخصم والوصل ——
const payDialog = ref(false)
const discountDialog = ref(false)
const current = ref(0)

function openPay(personId) {
  current.value = Number(personId)
  payDialog.value = true
}

function openDiscount(personId) {
  current.value = Number(personId)
  discountDialog.value = true
}

const receiptDialog = ref(false)
const receipt = ref({ id: 0, number: '' })

function openReceipt(paymentId, number) {
  receipt.value = { id: Number(paymentId), number }
  receiptDialog.value = true
}

async function afterPaid(res) {
  await Promise.all([loadDebtors(), loadPayments()])
  if (res?.paymentId && res.receiptNumber)
    openReceipt(res.paymentId, res.receiptNumber)
}

const afterDiscount = () => Promise.all([loadDebtors(), loadDiscounts()])

// —— سجل التسديدات ——
const payments = ref([])
const paymentsLoading = ref(false)
const paymentsQ = ref('')
const paymentsTotal = computed(() => payments.value.reduce((sum, p) => sum + Number(p.debtAmount) + Number(p.penaltyAmount), 0))

async function loadPayments() {
  paymentsLoading.value = true
  try {
    payments.value = await legalPayments((paymentsQ.value || '').trim())
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل التسديدات'))
  }
  finally {
    paymentsLoading.value = false
  }
}

// —— الخصومات ——
const discounts = ref([])
const discountsLoading = ref(false)
const discountsQ = ref('')
const discountsTotal = computed(() => discounts.value.reduce((sum, p) => sum + Number(p.debtAmount) + Number(p.penaltyAmount), 0))

async function loadDiscounts() {
  discountsLoading.value = true
  try {
    discounts.value = await legalPayments((discountsQ.value || '').trim(), 'Discount')
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل الخصومات'))
  }
  finally {
    discountsLoading.value = false
  }
}

// —— وصولات النظام القديم ——
const old = ref([])
const oldLoading = ref(false)
const oldQ = ref('')
const oldTotal = computed(() => old.value.reduce((sum, r) => sum + Number(r.amount || 0), 0))

async function loadOld() {
  oldLoading.value = true
  try {
    old.value = await legalOldReceipts((oldQ.value || '').trim())
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل الوصولات القديمة'))
  }
  finally {
    oldLoading.value = false
  }
}

onMounted(async () => {
  await Promise.all([loadDebtors(), loadPayments(), loadDiscounts(), loadOld()])

  // Arriving from a debtor's profile: open his تسديد straight away.
  if (route.query.pay)
    openPay(route.query.pay)
})
</script>

<template>
  <div>
    <h1 class="text-h5 font-weight-bold mb-4">
      التسديدات
    </h1>

    <VCard>
      <VTabs v-model="tab">
        <VTab value="pay">
          تسديد
        </VTab>
        <VTab value="payments">
          سجل التسديدات
        </VTab>
        <VTab value="discounts">
          الخصومات
        </VTab>
        <VTab value="old">
          وصولات النظام القديم
        </VTab>
      </VTabs>
      <VDivider />

      <VWindow v-model="tab">
        <!-- تسديد -->
        <VWindowItem value="pay">
          <VCardText class="d-flex flex-wrap align-center justify-space-between gap-3">
            <VChipGroup
              v-model="filter"
              mandatory
              selected-class="text-primary"
            >
              <VChip
                v-for="f in FILTERS"
                :key="f"
                :value="f"
                filter
                variant="outlined"
              >
                {{ f === 'All' ? 'كل المدينين' : LEGAL_STATUS[f].label }}
              </VChip>
            </VChipGroup>
            <VTextField
              v-model="debtorQ"
              placeholder="بحث بالاسم أو الهاتف أو كود الارشيف"
              prepend-inner-icon="tabler-search"
              density="compact"
              hide-details
              clearable
              style="max-inline-size: 320px;"
              @keyup.enter="loadDebtors"
              @click:clear="debtorQ = ''; loadDebtors()"
            />
          </VCardText>
          <VProgressLinear
            v-if="debtorsLoading"
            indeterminate
          />
          <VTable>
            <thead>
              <tr>
                <th>المدين</th>
                <th>الحالة</th>
                <th class="text-end">
                  باقي الدين
                </th>
                <th class="text-end">
                  الغرامات
                </th>
                <th class="text-end">
                  المطلوب
                </th>
                <th class="text-end">
                  سدّد حتى الآن
                </th>
                <th class="text-end">
                  خصومات
                </th>
                <th />
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="d in shownDebtors"
                :key="d.id"
              >
                <td>
                  <RouterLink :to="{ path: '/legal-desk-debtor', query: { id: d.id } }">
                    {{ d.fullName }}
                  </RouterLink>
                  <VIcon
                    v-if="d.isPaying"
                    icon="tabler-star-filled"
                    color="warning"
                    size="16"
                    class="ms-1"
                    title="مؤشَّر «تسديد» في تطبيق القانونية"
                  />
                  <div
                    class="text-caption text-medium-emphasis"
                    dir="ltr"
                  >
                    {{ d.archiveCodes || d.phone || '' }}
                  </div>
                </td>
                <td>
                  <VChip
                    size="small"
                    :color="legalStatus(d.stage).color"
                    variant="tonal"
                  >
                    {{ legalStatus(d.stage).label }}
                  </VChip>
                </td>
                <td class="text-end">
                  {{ legalMoney(d.remainingSnapshot) }}
                </td>
                <td class="text-end">
                  {{ legalMoney(d.penaltiesCharged - d.penaltiesPaid) }}
                </td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(due(d)) }}
                </td>
                <td class="text-end">
                  {{ legalMoney(paid(d)) }}
                </td>
                <td class="text-end">
                  {{ legalMoney(d.discounts) }}
                </td>
                <td class="text-end text-no-wrap">
                  <VBtn
                    size="small"
                    variant="text"
                    color="warning"
                    prepend-icon="tabler-discount"
                    class="me-2"
                    :disabled="due(d) <= 0"
                    @click="openDiscount(d.id)"
                  >
                    خصم
                  </VBtn>
                  <VBtn
                    size="small"
                    color="success"
                    prepend-icon="tabler-cash"
                    :disabled="due(d) <= 0"
                    @click="openPay(d.id)"
                  >
                    تسديد
                  </VBtn>
                </td>
              </tr>
              <tr v-if="!debtorsLoading && !shownDebtors.length">
                <td
                  colspan="8"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا يوجد مدينون هنا
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>

        <!-- سجل التسديدات -->
        <VWindowItem value="payments">
          <VCardText class="d-flex flex-wrap align-center justify-space-between gap-3">
            <div class="text-body-2">
              <strong>{{ payments.length }}</strong> تسديد · المجموع <strong>{{ legalMoney(paymentsTotal) }}</strong>
            </div>
            <VTextField
              v-model="paymentsQ"
              placeholder="بحث بالاسم أو رقم الوصل أو كود الارشيف"
              prepend-inner-icon="tabler-search"
              density="compact"
              hide-details
              clearable
              style="max-inline-size: 320px;"
              @keyup.enter="loadPayments"
              @click:clear="paymentsQ = ''; loadPayments()"
            />
          </VCardText>
          <VProgressLinear
            v-if="paymentsLoading"
            indeterminate
          />
          <VTable>
            <thead>
              <tr>
                <th>رقم الوصل</th>
                <th>التاريخ</th>
                <th>المدين</th>
                <th class="text-end">
                  من الدين
                </th>
                <th class="text-end">
                  من الغرامات
                </th>
                <th class="text-end">
                  المجموع
                </th>
                <th>بواسطة</th>
                <th />
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="p in payments"
                :key="p.id"
              >
                <td
                  class="font-weight-medium"
                  dir="ltr"
                >
                  {{ p.receiptNumber }}
                </td>
                <td class="text-no-wrap">
                  {{ formatUtc(p.atUtc, true) }}
                </td>
                <td>
                  <RouterLink :to="{ path: '/legal-desk-debtor', query: { id: p.personId } }">
                    {{ p.fullName }}
                  </RouterLink>
                  <div
                    v-if="p.note"
                    class="text-caption text-medium-emphasis"
                  >
                    {{ p.note }}
                  </div>
                </td>
                <td class="text-end">
                  {{ legalMoney(p.debtAmount) }}
                </td>
                <td class="text-end">
                  {{ legalMoney(p.penaltyAmount) }}
                </td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(Number(p.debtAmount) + Number(p.penaltyAmount)) }}
                </td>
                <td>{{ p.actor }}</td>
                <td class="text-end">
                  <VBtn
                    size="small"
                    variant="tonal"
                    prepend-icon="tabler-receipt"
                    @click="openReceipt(p.id, p.receiptNumber)"
                  >
                    الوصل
                  </VBtn>
                </td>
              </tr>
              <tr v-if="!paymentsLoading && !payments.length">
                <td
                  colspan="8"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا توجد تسديدات
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>

        <!-- الخصومات -->
        <VWindowItem value="discounts">
          <VCardText class="d-flex flex-wrap align-center justify-space-between gap-3">
            <div class="text-body-2">
              <strong>{{ discounts.length }}</strong> خصم · المجموع <strong>{{ legalMoney(discountsTotal) }}</strong>
            </div>
            <VTextField
              v-model="discountsQ"
              placeholder="بحث بالاسم أو كود الارشيف"
              prepend-inner-icon="tabler-search"
              density="compact"
              hide-details
              clearable
              style="max-inline-size: 320px;"
              @keyup.enter="loadDiscounts"
              @click:clear="discountsQ = ''; loadDiscounts()"
            />
          </VCardText>
          <VProgressLinear
            v-if="discountsLoading"
            indeterminate
          />
          <VTable>
            <thead>
              <tr>
                <th>التاريخ</th>
                <th>المدين</th>
                <th class="text-end">
                  من الدين
                </th>
                <th class="text-end">
                  من الغرامات
                </th>
                <th class="text-end">
                  مبلغ الخصم
                </th>
                <th>السبب</th>
                <th>بواسطة</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="p in discounts"
                :key="p.id"
              >
                <td class="text-no-wrap">
                  {{ formatUtc(p.atUtc, true) }}
                </td>
                <td>
                  <RouterLink :to="{ path: '/legal-desk-debtor', query: { id: p.personId } }">
                    {{ p.fullName }}
                  </RouterLink>
                </td>
                <td class="text-end">
                  {{ legalMoney(p.debtAmount) }}
                </td>
                <td class="text-end">
                  {{ legalMoney(p.penaltyAmount) }}
                </td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(Number(p.debtAmount) + Number(p.penaltyAmount)) }}
                </td>
                <td>{{ p.note || '—' }}</td>
                <td>{{ p.actor }}</td>
              </tr>
              <tr v-if="!discountsLoading && !discounts.length">
                <td
                  colspan="7"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا توجد خصومات
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>

        <!-- وصولات النظام القديم -->
        <VWindowItem value="old">
          <VCardText class="d-flex flex-wrap align-center justify-space-between gap-3">
            <div class="text-body-2">
              <strong>{{ old.length }}</strong> وصل من النظام القديم · المجموع <strong>{{ legalMoney(oldTotal) }}</strong>
              <span class="text-medium-emphasis"> — للبحث فقط، وأرقامها لا تُعطى لوصل جديد</span>
            </div>
            <VTextField
              v-model="oldQ"
              placeholder="بحث برقم الوصل أو الاسم أو الفرع"
              prepend-inner-icon="tabler-search"
              density="compact"
              hide-details
              clearable
              style="max-inline-size: 320px;"
              @keyup.enter="loadOld"
              @click:clear="oldQ = ''; loadOld()"
            />
          </VCardText>
          <VProgressLinear
            v-if="oldLoading"
            indeterminate
          />
          <VTable>
            <thead>
              <tr>
                <th>رقم الوصل</th>
                <th>التاريخ</th>
                <th>الاسم</th>
                <th>الفرع</th>
                <th class="text-end">
                  المبلغ
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="r in old"
                :key="r.receiptNumber"
              >
                <td
                  class="font-weight-medium"
                  dir="ltr"
                >
                  {{ r.receiptNumber }}
                </td>
                <td>{{ legalDay(r.paymentDate) }}</td>
                <td>{{ r.customer }}</td>
                <td>{{ r.branch }}</td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(r.amount) }}
                </td>
              </tr>
              <tr v-if="!oldLoading && !old.length">
                <td
                  colspan="5"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا توجد وصولات مطابقة
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>
      </VWindow>
    </VCard>

    <LegalPayDialog
      v-model="payDialog"
      :person-id="current"
      @paid="afterPaid"
    />
    <LegalDiscountDialog
      v-model="discountDialog"
      :person-id="current"
      @done="afterDiscount"
    />
    <LegalReceiptDialog
      v-model="receiptDialog"
      :payment-id="receipt.id"
      :receipt-number="receipt.number"
    />
  </div>
</template>
