<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import {
  LEGAL_STATUS,
  formatUtc,
  legalError,
  legalMoney,
  legalOverview,
  legalRefreshBalances,
} from '@/composables/legalDeskApi'

const toast = useToast()
const router = useRouter()
const loading = ref(false)
const refreshing = ref(false)
const data = ref(null)

const m = computed(() => data.value?.money || {})
const r = computed(() => data.value?.registers || {})
const penaltiesDue = computed(() => Number(m.value.penaltiesCharged || 0) - Number(m.value.penaltiesPaid || 0))

/** Debtors per status, from the per-stage counts the server returns. */
const counts = computed(() => {
  const stage = key => data.value?.stages?.find(s => s.stage === key) || { count: 0, annulled: 0 }
  const open = ['Recent', 'Archive', 'Judicial', 'Decisions'].map(stage)
  const plain = s => Number(s.count || 0) - Number(s.annulled || 0)

  return {
    Waiting: plain(open[0]) + plain(open[1]),
    Judicial: plain(open[2]) + plain(open[3]),
    Execution: Number(stage('Execution').count || 0),
    Paying: open.reduce((sum, s) => sum + Number(s.annulled || 0), 0),
    Closed: Number(stage('Closed').count || 0),
  }
})

const TILES = ['Waiting', 'Judicial', 'Execution', 'Paying', 'Closed']

async function load() {
  loading.value = true
  try {
    data.value = await legalOverview()
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل النظرة العامة'))
  }
  finally {
    loading.value = false
  }
}

async function refreshBalances() {
  refreshing.value = true
  try {
    const res = await legalRefreshBalances()
    if (res.staleCities?.length)
      toast.warning(`تعذر تحديث: ${res.staleCities.join('، ')}`)
    else
      toast.success('تم تحديث الأرصدة')
    await load()
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحديث الأرصدة'))
  }
  finally {
    refreshing.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-4">
      <h1 class="text-h5 font-weight-bold mb-0">
        القانونية
      </h1>
      <VBtn
        variant="text"
        prepend-icon="tabler-refresh"
        :loading="refreshing"
        @click="refreshBalances"
      >
        تحديث الأرصدة
      </VBtn>
    </div>

    <VProgressLinear
      v-if="loading"
      indeterminate
      class="mb-4"
    />

    <VAlert
      v-if="m.pendingSettlements > 0"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-4"
    >
      {{ m.pendingSettlements }} طلب تسديد كامل بانتظارك.
      <VBtn
        size="small"
        variant="text"
        :to="{ path: '/legal-desk-payments', query: { tab: 'settlement' } }"
      >
        فتح الطلبات
      </VBtn>
    </VAlert>
    <VAlert
      v-if="m.pendingAnnuls > 0"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-4"
    >
      {{ m.pendingAnnuls }} طلب ابطال بانتظار قبولك.
      <VBtn
        size="small"
        variant="text"
        :to="{ path: '/legal-desk-payments', query: { tab: 'annul' } }"
      >
        فتح الطلبات
      </VBtn>
    </VAlert>

    <!-- المدينون حسب الحالة -->
    <VRow class="mb-1">
      <VCol
        v-for="key in TILES"
        :key="key"
        cols="6"
        sm="4"
        md
      >
        <VCard
          class="cursor-pointer"
          @click="router.push({ path: '/legal-desk-debtors', query: { status: key } })"
        >
          <VCardText>
            <div class="text-body-2 text-medium-emphasis">
              {{ LEGAL_STATUS[key].label }}
            </div>
            <div class="text-h4 font-weight-bold">
              {{ counts[key] }}
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <!-- المبالغ -->
    <VCard class="mb-4">
      <VCardText>
        <VRow>
          <VCol
            cols="12"
            sm="4"
          >
            <div class="text-body-2 text-medium-emphasis">
              المطلوب دفعه لفريق القانونية
            </div>
            <div
              class="text-h5 font-weight-bold"
              :class="r.dueToLegalTeam > 0 ? 'text-error' : ''"
            >
              {{ legalMoney(r.dueToLegalTeam) }}
            </div>
          </VCol>
          <VCol
            cols="12"
            sm="4"
          >
            <div class="text-body-2 text-medium-emphasis">
              باقي ديون المدينين
            </div>
            <div class="text-h5 font-weight-bold">
              {{ legalMoney(m.remainingSnapshot) }}
            </div>
          </VCol>
          <VCol
            cols="12"
            sm="4"
          >
            <div class="text-body-2 text-medium-emphasis">
              غرامات غير مسددة
            </div>
            <div class="text-h5 font-weight-bold">
              {{ legalMoney(penaltiesDue) }}
            </div>
          </VCol>
        </VRow>
      </VCardText>
    </VCard>

    <!-- القاصة: عرض فقط -->
    <VCard class="mb-4">
      <VCardText>
        <div class="text-subtitle-1 font-weight-bold mb-3">
          قاصة القانونية
          <span class="text-caption text-medium-emphasis">(عرض فقط)</span>
        </div>
        <VRow>
          <VCol
            cols="6"
            sm="4"
            md
          >
            <div class="text-body-2 text-medium-emphasis">
              رصيد القاصة
            </div>
            <div
              class="text-h6 font-weight-bold"
              :class="r.cash < 0 ? 'text-error' : ''"
            >
              {{ legalMoney(r.cash) }}
            </div>
          </VCol>
          <VCol
            cols="6"
            sm="4"
            md
          >
            <div class="text-body-2 text-medium-emphasis">
              محافظ المحامين
            </div>
            <div class="text-h6 font-weight-bold">
              {{ legalMoney(r.lawyerWallets) }}
            </div>
          </VCol>
          <VCol
            cols="6"
            sm="4"
            md
          >
            <div class="text-body-2 text-medium-emphasis">
              استلمت من قلعة الضمان
            </div>
            <div class="text-h6 font-weight-bold">
              {{ legalMoney(r.companyFunds) }}
            </div>
          </VCol>
          <VCol
            cols="6"
            sm="4"
            md
          >
            <div class="text-body-2 text-medium-emphasis">
              سدّدت لقلعة الضمان
            </div>
            <div class="text-h6 font-weight-bold">
              {{ legalMoney(r.paidBack) }}
            </div>
          </VCol>
          <VCol
            cols="6"
            sm="4"
            md
          >
            <div class="text-body-2 text-medium-emphasis">
              الباقي بذمة القانونية
            </div>
            <div class="text-h6 font-weight-bold">
              {{ legalMoney(Math.max(0, r.companyDebt || 0)) }}
            </div>
          </VCol>
        </VRow>
      </VCardText>
    </VCard>

    <!-- آخر التسديدات -->
    <VCard>
      <VCardText class="d-flex align-center justify-space-between pb-2">
        <div class="text-subtitle-1 font-weight-bold">
          آخر التسديدات
        </div>
        <div class="text-body-2 text-medium-emphasis">
          المجموع {{ legalMoney(Number(m.debtCollected || 0) + Number(m.penaltiesPaid || 0)) }}
        </div>
      </VCardText>
      <VTable density="compact">
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
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="p in data?.payments || []"
            :key="p.id"
          >
            <td>{{ formatUtc(p.atUtc) }}</td>
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
          </tr>
          <tr v-if="!(data?.payments || []).length">
            <td
              colspan="4"
              class="text-center text-medium-emphasis py-4"
            >
              لا توجد تسديدات بعد
            </td>
          </tr>
        </tbody>
      </VTable>
    </VCard>
  </div>
</template>
