<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import LegalPayDialog from '@/components/LegalPayDialog.vue'
import LegalSettlementRequests from '@/components/LegalSettlementRequests.vue'
import {
  formatUtc,
  legalAnnulRequests,
  legalDecideAnnul,
  legalError,
  legalMoney,
  legalOverview,
  legalPayments,
  legalPersons,
  legalReturnFromAnnul,
  legalStatus,
} from '@/composables/legalDeskApi'

/**
 * التسديدات: everything محاسب القانونية does with debtors' money in one place —
 * the payments taken, ابطال requests from the legal app, and full-settlement requests.
 */
const toast = useToast()
const route = useRoute()
const router = useRouter()

const tab = ref(['pay', 'payments', 'annul', 'settlement'].includes(route.query.tab) ? route.query.tab : 'pay')
const pending = ref({ annul: 0, settlement: 0 })

async function loadPending() {
  try {
    const { money } = await legalOverview()

    pending.value = { annul: Number(money.pendingAnnuls || 0), settlement: Number(money.pendingSettlements || 0) }
  }
  catch { /* the badges are a convenience only */ }
}

watch(tab, value => router.replace({ query: value === 'pay' ? {} : { tab: value } }))

// —— تسديد: who is paying ——
const WHO = [
  { key: 'Paying', label: 'ابطال (التسديد)' },
  { key: 'Execution', label: 'التنفيذ' },
  { key: 'All', label: 'كل المدينين' },
]

const who = ref('Paying')
const debtorQ = ref('')
const debtors = ref([])
const debtorsLoading = ref(false)

const due = d => Number(d.remainingSnapshot || 0) + Number(d.penaltiesCharged || 0) - Number(d.penaltiesPaid || 0)

const shownDebtors = computed(() => debtors.value.filter(d => {
  const status = legalStatus(d.stage, d.isAnnulled).key
  if (status === 'Closed')
    return false

  return who.value === 'All' || status === who.value
}))

async function loadDebtors() {
  debtorsLoading.value = true
  try {
    debtors.value = await legalPersons({ q: debtorQ.value.trim() })
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل المدينين'))
  }
  finally {
    debtorsLoading.value = false
  }
}

// The one تسديد dialog of the dashboard.
const payDialog = ref(false)
const payPerson = ref(0)
const payFull = ref(false)

function openPay(personId, full = false) {
  payPerson.value = Number(personId)
  payFull.value = full
  payDialog.value = true
}

const afterPaid = () => Promise.all([loadDebtors(), loadPayments(), loadPending()])

// —— إرجاع: an ابطال debtor who stopped paying goes back to the legal app ——
const backDialog = ref(false)
const backDebtor = ref(null)
const backNote = ref('')
const backSaving = ref(false)

function openBack(debtor) {
  backDebtor.value = debtor
  backNote.value = ''
  backDialog.value = true
}

async function confirmBack() {
  backSaving.value = true
  try {
    await legalReturnFromAnnul(backDebtor.value.caseId, backNote.value)
    backDialog.value = false
    toast.success('أُعيد المدين إلى القانونية')
    await Promise.all([loadDebtors(), loadPending()])
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر إرجاع المدين'))
  }
  finally {
    backSaving.value = false
  }
}

// —— كل التسديدات ——
const q = ref('')
const payments = ref([])
const paymentsLoading = ref(false)

const totals = computed(() => ({
  debt: payments.value.reduce((sum, p) => sum + Number(p.debtAmount || 0), 0),
  penalty: payments.value.reduce((sum, p) => sum + Number(p.penaltyAmount || 0), 0),
}))

async function loadPayments() {
  paymentsLoading.value = true
  try {
    payments.value = await legalPayments(q.value.trim())
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل التسديدات'))
  }
  finally {
    paymentsLoading.value = false
  }
}

// —— طلبات الابطال ——
const ANNUL_STATUS = {
  Pending: { label: 'بانتظار القبول', color: 'warning' },
  Approved: { label: 'مقبول', color: 'success' },
  Rejected: { label: 'مرفوض', color: 'error' },
  Withdrawn: { label: 'سحبه الفريق', color: 'default' },
}

const annulStatus = ref('Pending')
const annulRows = ref([])
const annulLoading = ref(false)
const annulDialog = ref(false)
const annulCurrent = ref(null)
const annulNote = ref('')
const annulSaving = ref(false)

/** How مبلغ الابطال will be recorded: the debt first, anything left against the legal penalties. */
const annulSplit = computed(() => {
  const r = annulCurrent.value
  if (!r)
    return { debt: 0, penalty: 0, tooMuch: false }
  const debt = Math.min(Number(r.amount), Number(r.remainingSnapshot))
  const penalty = Number(r.amount) - debt

  return { debt, penalty, tooMuch: penalty > Number(r.penaltiesDue) }
})

async function loadAnnul() {
  annulLoading.value = true
  try {
    annulRows.value = await legalAnnulRequests(annulStatus.value)
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل طلبات الابطال'))
  }
  finally {
    annulLoading.value = false
  }
}

function openAnnul(row) {
  annulCurrent.value = row
  annulNote.value = ''
  annulDialog.value = true
}

async function decideAnnul(approve) {
  annulSaving.value = true
  try {
    await legalDecideAnnul(annulCurrent.value.id, approve, annulNote.value)
    annulDialog.value = false
    toast.success(approve ? 'قُبل الابطال وسُجّل المبلغ تسديداً' : 'رُفض طلب الابطال')
    await Promise.all([loadAnnul(), loadPayments(), loadPending(), loadDebtors()])
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر حفظ القرار'))
  }
  finally {
    annulSaving.value = false
  }
}

watch(annulStatus, loadAnnul)

onMounted(async () => {
  await Promise.all([loadDebtors(), loadPayments(), loadAnnul(), loadPending()])

  // Arriving from a debtor's profile: open his تسديد straight away.
  if (route.query.pay)
    openPay(route.query.pay, route.query.full === '1')
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
        <VTab value="annul">
          طلبات الابطال
          <VChip
            v-if="pending.annul"
            size="x-small"
            color="warning"
            class="ms-2"
          >
            {{ pending.annul }}
          </VChip>
        </VTab>
        <VTab value="settlement">
          طلبات التسديد الكامل
          <VChip
            v-if="pending.settlement"
            size="x-small"
            color="warning"
            class="ms-2"
          >
            {{ pending.settlement }}
          </VChip>
        </VTab>
      </VTabs>
      <VDivider />

      <VWindow v-model="tab">
        <!-- تسديد -->
        <VWindowItem value="pay">
          <VCardText class="d-flex flex-wrap align-center justify-space-between gap-3">
            <VChipGroup
              v-model="who"
              mandatory
              selected-class="text-primary"
            >
              <VChip
                v-for="w in WHO"
                :key="w.key"
                :value="w.key"
                filter
                variant="outlined"
              >
                {{ w.label }}
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
                    :color="legalStatus(d.stage, d.isAnnulled).color"
                    variant="tonal"
                  >
                    {{ legalStatus(d.stage, d.isAnnulled).label }}
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
                  {{ legalMoney(Number(d.debtPaid) + Number(d.penaltiesPaid)) }}
                </td>
                <td class="text-end text-no-wrap">
                  <VBtn
                    v-if="d.isAnnulled"
                    size="small"
                    variant="text"
                    color="secondary"
                    prepend-icon="tabler-arrow-back-up"
                    class="me-2"
                    @click="openBack(d)"
                  >
                    إرجاع
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
                  colspan="7"
                  class="text-center text-medium-emphasis py-8"
                >
                  {{ who === 'Paying' ? 'لا يوجد مدينون ابطال حالياً' : 'لا يوجد مدينون هنا' }}
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>

        <!-- سجل التسديدات -->
        <VWindowItem value="payments">
          <VCardText class="d-flex flex-wrap align-center justify-space-between gap-3">
            <VTextField
              v-model="q"
              placeholder="بحث بالاسم أو الهاتف أو كود الارشيف"
              prepend-inner-icon="tabler-search"
              density="compact"
              hide-details
              clearable
              style="max-inline-size: 320px;"
              @keyup.enter="loadPayments"
              @click:clear="q = ''; loadPayments()"
            />
            <div class="d-flex align-center gap-4">
              <div class="text-body-2">
                المجموع <strong>{{ legalMoney(totals.debt + totals.penalty) }}</strong>
                <span class="text-medium-emphasis"> (دين {{ legalMoney(totals.debt) }} · غرامات {{ legalMoney(totals.penalty) }})</span>
              </div>
            </div>
          </VCardText>
          <VProgressLinear
            v-if="paymentsLoading"
            indeterminate
          />
          <VTable>
            <thead>
              <tr>
                <th>التاريخ</th>
                <th>المدين</th>
                <th class="text-end">
                  دين
                </th>
                <th class="text-end">
                  غرامات
                </th>
                <th class="text-end">
                  المجموع
                </th>
                <th>بواسطة</th>
                <th>ملاحظة</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="p in payments"
                :key="p.id"
              >
                <td>{{ formatUtc(p.atUtc, true) }}</td>
                <td>
                  <RouterLink :to="{ path: '/legal-desk-debtor', query: { id: p.personId } }">
                    {{ p.fullName }}
                  </RouterLink>
                  <div
                    v-if="p.archiveCodes"
                    class="text-caption text-medium-emphasis"
                    dir="ltr"
                  >
                    {{ p.archiveCodes }}
                  </div>
                </td>
                <td class="text-end">
                  {{ legalMoney(p.debtAmount) }}
                </td>
                <td class="text-end">
                  {{ legalMoney(p.penaltyAmount) }}
                </td>
                <td class="text-end font-weight-medium">
                  {{ legalMoney(Number(p.debtAmount) + Number(p.penaltyAmount)) }}
                </td>
                <td>{{ p.actor }}</td>
                <td>
                  <VChip
                    v-if="p.isAnnul"
                    size="x-small"
                    color="info"
                    variant="tonal"
                    class="me-1"
                  >
                    ابطال
                  </VChip>
                  {{ p.note || '' }}
                </td>
              </tr>
              <tr v-if="!paymentsLoading && !payments.length">
                <td
                  colspan="7"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا توجد تسديدات
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>

        <!-- طلبات الابطال -->
        <VWindowItem value="annul">
          <VCardText class="d-flex flex-wrap align-center justify-space-between gap-3">
            <div class="text-body-2 text-medium-emphasis">
              فريق القانونية اتفق مع المدين وأرسل مبلغ الابطال. عند القبول يُسجَّل المبلغ تسديداً ويصبح المدين «التسديد».
            </div>
            <VChipGroup
              v-model="annulStatus"
              mandatory
              selected-class="text-primary"
            >
              <VChip
                v-for="(s, key) in { Pending: 'بانتظار القبول', Approved: 'المقبولة', Rejected: 'المرفوضة' }"
                :key="key"
                :value="key"
                filter
                variant="outlined"
              >
                {{ s }}
              </VChip>
            </VChipGroup>
          </VCardText>
          <VProgressLinear
            v-if="annulLoading"
            indeterminate
          />
          <VTable>
            <thead>
              <tr>
                <th>المدين</th>
                <th>الحالة</th>
                <th class="text-end">
                  مبلغ الابطال
                </th>
                <th class="text-end">
                  المطلوب منه
                </th>
                <th>أرسله</th>
                <th>تفاصيل الاتفاق</th>
                <th />
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="row in annulRows"
                :key="row.id"
              >
                <td>
                  <RouterLink :to="{ path: '/legal-desk-debtor', query: { id: row.personId } }">
                    {{ row.fullName }}
                  </RouterLink>
                  <div
                    class="text-caption text-medium-emphasis"
                    dir="ltr"
                  >
                    {{ row.archiveCodes || row.phone || '' }}
                  </div>
                </td>
                <td>
                  <VChip
                    size="small"
                    :color="legalStatus(row.stage).color"
                    variant="tonal"
                  >
                    {{ legalStatus(row.stage).label }}
                  </VChip>
                </td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(row.amount) }}
                </td>
                <td class="text-end">
                  {{ legalMoney(Number(row.remainingSnapshot) + Number(row.penaltiesDue)) }}
                </td>
                <td>
                  {{ row.requestedBy }}
                  <div class="text-caption text-medium-emphasis">
                    {{ formatUtc(row.requestedAtUtc, true) }}
                  </div>
                </td>
                <td>{{ row.note || '—' }}</td>
                <td class="text-end">
                  <VBtn
                    v-if="row.status === 'Pending'"
                    size="small"
                    color="primary"
                    @click="openAnnul(row)"
                  >
                    البت في الطلب
                  </VBtn>
                  <template v-else>
                    <VChip
                      size="small"
                      :color="ANNUL_STATUS[row.status]?.color"
                      variant="tonal"
                    >
                      {{ ANNUL_STATUS[row.status]?.label || row.status }}
                    </VChip>
                    <div
                      v-if="row.decidedBy"
                      class="text-caption text-medium-emphasis"
                    >
                      {{ row.decidedBy }} · {{ formatUtc(row.decidedAtUtc) }}
                    </div>
                  </template>
                </td>
              </tr>
              <tr v-if="!annulLoading && !annulRows.length">
                <td
                  colspan="7"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا توجد طلبات ابطال
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>

        <!-- طلبات التسديد الكامل -->
        <VWindowItem value="settlement">
          <LegalSettlementRequests
            @changed="loadPending"
            @pay="openPay($event, true)"
          />
        </VWindowItem>
      </VWindow>
    </VCard>

    <LegalPayDialog
      v-model="payDialog"
      :person-id="payPerson"
      :full="payFull"
      @paid="afterPaid"
    />

    <VDialog
      v-model="backDialog"
      max-width="480"
      :persistent="backSaving"
    >
      <VCard>
        <VCardTitle>إرجاع إلى القانونية — {{ backDebtor?.fullName }}</VCardTitle>
        <VCardText>
          <div class="mb-3">
            يُرفع الابطال عن المدين ويعود في تطبيق القانونية ملفاً عادياً في محطته الحالية. ما سدّده حتى الآن يبقى محسوباً.
          </div>
          <VTextField
            v-model="backNote"
            label="السبب (اختياري)"
            density="compact"
            hide-details
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            :disabled="backSaving"
            @click="backDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="backSaving"
            @click="confirmBack"
          >
            إرجاع
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="annulDialog"
      max-width="520"
      :persistent="annulSaving"
    >
      <VCard v-if="annulCurrent">
        <VCardTitle>طلب ابطال — {{ annulCurrent.fullName }}</VCardTitle>
        <VCardText>
          <VTable
            density="compact"
            class="mb-3"
          >
            <tbody>
              <tr>
                <td>مبلغ الابطال</td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(annulCurrent.amount) }}
                </td>
              </tr>
              <tr>
                <td>يُسجَّل على الدين</td>
                <td class="text-end">
                  {{ legalMoney(annulSplit.debt) }}
                </td>
              </tr>
              <tr>
                <td>يُسجَّل على غرامات القانونية</td>
                <td class="text-end">
                  {{ legalMoney(annulSplit.penalty) }}
                </td>
              </tr>
              <tr>
                <td>يبقى عليه بعد القبول</td>
                <td class="text-end">
                  {{ legalMoney(Math.max(0, Number(annulCurrent.remainingSnapshot) + Number(annulCurrent.penaltiesDue) - Number(annulCurrent.amount))) }}
                </td>
              </tr>
            </tbody>
          </VTable>
          <VAlert
            v-if="annulSplit.tooMuch"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            مبلغ الابطال أكبر من كل المطلوب من المدين. ارفض الطلب ليصحّحه الفريق.
          </VAlert>
          <div
            v-if="annulCurrent.note"
            class="text-body-2 mb-3"
          >
            <strong>الاتفاق:</strong> {{ annulCurrent.note }}
          </div>
          <VTextField
            v-model="annulNote"
            label="ملاحظة (اختياري)"
            density="compact"
            hide-details
          />
        </VCardText>
        <VCardActions>
          <VBtn
            color="error"
            variant="text"
            :disabled="annulSaving"
            @click="decideAnnul(false)"
          >
            رفض
          </VBtn>
          <VSpacer />
          <VBtn
            variant="text"
            @click="annulDialog = false"
          >
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="annulSaving"
            :disabled="annulSplit.tooMuch"
            @click="decideAnnul(true)"
          >
            قبول وتسجيل التسديد
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
