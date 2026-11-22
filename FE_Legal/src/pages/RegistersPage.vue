<script setup>
import { computed, onMounted, ref } from 'vue'
import { api, errorMessage } from '../api'
import AmountDialog from '../components/AmountDialog.vue'
import { REGISTER_KIND, fromUtc, money, toast } from '../shared'

const loading = ref(false)
const data = ref(null)
const filter = ref('all')
const fundsDialog = ref(false)
const expenseDialog = ref(false)

const r = computed(() => data.value || {})

/** all | cash (movements of the box itself) | company (the account with قلعة الضمان). */
const rows = computed(() => (r.value.transactions || []).filter(t =>
  filter.value === 'all'
  || (filter.value === 'cash' && t.cashAmount != null)
  || (filter.value === 'company' && t.companyAmount != null)))

/** How the team paid قلعة الضمان back, line by line. */
const paidBackLines = computed(() => [
  { label: 'دعاوى قضائية', count: r.value.judicialCount, amount: r.value.judicialFees },
  { label: 'تنفيذ', count: r.value.executionCount, amount: r.value.executionFees },
  { label: 'تسديد قبل الدعوى', count: r.value.earlyCount, amount: r.value.earlyFees },
])

async function load() {
  loading.value = true
  try {
    data.value = await api.registers()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل القاصة'))
  }
  finally {
    loading.value = false
  }
}

const signed = value => `${value > 0 ? '+' : ''}${money(value)}`

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="d-flex flex-wrap align-center justify-space-between ga-3 mb-4">
      <div>
        <h1 class="page-title">
          القاصة
        </h1>
        <div class="text-body-2 muted">
          نقد الفريق، وحسابه مع قلعة الضمان: ما استلمه منها وما سدّده لها
        </div>
      </div>
      <div class="d-flex ga-2">
        <VBtn
          variant="tonal"
          prepend-icon="report-money"
          @click="expenseDialog = true"
        >
          صرف من القاصة
        </VBtn>
        <VBtn
          color="primary"
          variant="flat"
          prepend-icon="cash"
          @click="fundsDialog = true"
        >
          استلام مبلغ من قلعة الضمان
        </VBtn>
      </div>
    </div>

    <VProgressLinear
      v-if="loading"
      indeterminate
      color="primary"
      class="mb-3"
    />

    <VCard class="mb-4">
      <VRow no-gutters>
        <!-- النقد -->
        <VCol
          cols="12"
          md="5"
          class="pa-5"
        >
          <div class="d-flex align-center ga-3 mb-2">
            <VAvatar
              color="primary"
              variant="tonal"
              rounded="lg"
            >
              <VIcon icon="wallet" />
            </VAvatar>
            <div class="section-title">
              رصيد القاصة
            </div>
          </div>
          <div
            class="register-value num"
            :class="r.cash < 0 ? 'text-error' : ''"
          >
            {{ money(r.cash) }}
          </div>
          <div class="text-body-2 muted mb-3">
            {{ r.cash < 0 ? 'القاصة بالسالب: صُرف أكثر مما استُلم' : 'النقد الموجود فعلياً عند الفريق' }}
          </div>
          <div class="line">
            <span>في محافظ المحامين</span>
            <span class="num">{{ money(r.lawyerWallets) }}</span>
          </div>
          <div class="line">
            <span>مصروفات أخرى</span>
            <span class="num">{{ money(r.expenses) }}</span>
          </div>
        </VCol>

        <VDivider
          vertical
          class="d-none d-md-block"
        />

        <!-- الحساب مع قلعة الضمان -->
        <VCol
          cols="12"
          md
          class="pa-5"
        >
          <div class="d-flex align-center ga-3 mb-3">
            <VAvatar
              color="accent"
              variant="tonal"
              rounded="lg"
            >
              <VIcon icon="building-bank" />
            </VAvatar>
            <div class="section-title">
              الحساب مع قلعة الضمان
            </div>
          </div>
          <div class="line line--strong">
            <span>استلمت القاصة من قلعة الضمان</span>
            <span class="num">{{ money(r.companyFunds) }}</span>
          </div>
          <div class="line line--strong">
            <span>سدّدت لقلعة الضمان</span>
            <span class="num">{{ money(r.paidBack) }}</span>
          </div>
          <div
            v-for="item in paidBackLines"
            :key="item.label"
            class="line line--sub"
          >
            <span>{{ item.label }} <span class="muted">({{ item.count ?? 0 }})</span></span>
            <span class="num">{{ money(item.amount) }}</span>
          </div>
          <VDivider class="my-2" />
          <div class="line line--strong">
            <span>{{ r.companyDebt < 0 ? 'قلعة الضمان مدينة للقانونية بـ' : 'الباقي بذمة القانونية' }}</span>
            <span
              class="num text-h6 font-weight-bold"
              :class="r.companyDebt < 0 ? 'text-success' : ''"
            >{{ money(Math.abs(r.companyDebt || 0)) }}</span>
          </div>
        </VCol>
      </VRow>
    </VCard>

    <VCard>
      <div class="d-flex flex-wrap align-center justify-space-between ga-3 pa-4">
        <div class="section-title">
          حركات القاصة
        </div>
        <VBtnToggle
          v-model="filter"
          mandatory
          density="compact"
          variant="outlined"
          color="primary"
        >
          <VBtn value="all">
            الكل
          </VBtn>
          <VBtn value="cash">
            النقد
          </VBtn>
          <VBtn value="company">
            حساب قلعة الضمان
          </VBtn>
        </VBtnToggle>
      </div>
      <VDivider />
      <VTable>
        <thead>
          <tr>
            <th>التاريخ</th>
            <th>الحركة</th>
            <th>البيان</th>
            <th>بواسطة</th>
            <th class="text-end">
              النقد
            </th>
            <th class="text-end">
              حساب قلعة الضمان
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="t in rows"
            :key="t.id"
          >
            <td class="num">
              {{ fromUtc(t.atUtc, true) }}
            </td>
            <td>{{ REGISTER_KIND[t.kind] || t.kind }}</td>
            <td>
              <RouterLink
                v-if="t.caseId"
                :to="`/cases/${t.caseId}`"
              >
                {{ t.debtorName }}
              </RouterLink>
              <RouterLink
                v-else-if="t.lawyerId"
                :to="`/lawyers/${t.lawyerId}`"
              >
                {{ t.lawyerName }}
              </RouterLink>
              <span v-if="t.note">{{ t.caseId || t.lawyerId ? ' — ' : '' }}{{ t.note }}</span>
            </td>
            <td>{{ t.actor }}</td>
            <td
              class="text-end num font-weight-medium"
              :class="t.cashAmount < 0 ? 'text-error' : 'text-success'"
            >
              {{ t.cashAmount == null ? '' : signed(t.cashAmount) }}
            </td>
            <td class="text-end num">
              {{ t.companyAmount == null ? '' : signed(t.companyAmount) }}
            </td>
          </tr>
          <tr v-if="!loading && !rows.length">
            <td
              colspan="6"
              class="text-center muted py-10"
            >
              لا توجد حركات بعد
            </td>
          </tr>
        </tbody>
      </VTable>
      <div class="text-caption muted pa-4 pt-2">
        عمود «حساب قلعة الضمان»: الموجب مبلغ استلمته القاصة منها، والسالب تسديد لها (200,000 عن كل دعوى، 150,000 عن كل تنفيذ، ومبلغ التسديد قبل الدعوى).
      </div>
    </VCard>

    <AmountDialog
      v-model="fundsDialog"
      title="استلام مبلغ من قلعة الضمان"
      description="يدخل المبلغ القاصة ويُسجَّل على حسابها مع قلعة الضمان."
      confirm-label="تسجيل الاستلام"
      :action="api.companyFunds"
      success-text="سُجّل المبلغ المستلم"
      @done="load"
    />
    <AmountDialog
      v-model="expenseDialog"
      title="صرف من القاصة"
      description="لأي مصروف آخر غير رسوم المدينين (تلك تُدفع من محافظ المحامين)."
      confirm-label="تسجيل الصرف"
      note-label="سبب الصرف"
      note-required
      :action="api.expense"
      success-text="سُجّل الصرف"
      @done="load"
    />
  </div>
</template>

<style scoped>
.register-value {
  font-size: 2.1rem;
  font-weight: 700;
  line-height: 1.4;
}

.line {
  display: flex;
  justify-content: space-between;
  padding-block: 3px;
  font-size: 0.9rem;
  gap: 16px;
}

.line--strong {
  font-size: 0.98rem;
  font-weight: 500;
}

.line--sub {
  color: rgba(var(--v-theme-on-surface), 0.75);
  padding-inline-start: 18px;
}
</style>
