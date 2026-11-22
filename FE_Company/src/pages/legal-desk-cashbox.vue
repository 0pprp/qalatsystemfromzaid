<script setup>
import { computed, onMounted, ref } from 'vue'
import { useToast } from '@/composables/useToast'
import { formatUtc, legalCashbox, legalError, legalMoney } from '@/composables/legalDeskApi'

/**
 * القاصة (read-only): what was collected from debtors through التسديدات, and what the company
 * put into the legal app's قاصة (دين قلعة الضمان).
 */
const toast = useToast()
const loading = ref(false)
const data = ref(null)
const tab = ref('payments')

const c = computed(() => data.value?.collected || {})
const funds = computed(() => data.value?.funds || { total: 0, count: 0, records: [] })
const back = computed(() => data.value?.paidBack || {})
const payments = computed(() => data.value?.payments || [])

async function load() {
  loading.value = true
  try {
    data.value = await legalCashbox()
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل القاصة'))
  }
  finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-4">
      <h1 class="text-h5 font-weight-bold mb-0">
        القاصة
      </h1>
      <VBtn
        variant="tonal"
        prepend-icon="tabler-refresh"
        :loading="loading"
        @click="load"
      >
        تحديث
      </VBtn>
    </div>

    <VRow>
      <!-- 1) المقبوض من التسديدات -->
      <VCol
        cols="12"
        md="6"
      >
        <VCard class="h-100">
          <VCardText>
            <div class="text-body-2 text-medium-emphasis">
              المقبوض من التسديدات
            </div>
            <div class="text-h4 font-weight-bold text-success my-1">
              {{ legalMoney(c.total) }}
            </div>
            <div class="text-body-2 mb-3">
              من <strong>{{ c.customers || 0 }}</strong> مدين · <strong>{{ c.payments || 0 }}</strong> تسديد
            </div>
            <VTable density="compact">
              <tbody>
                <tr>
                  <td>مبالغ الابطال</td>
                  <td class="text-medium-emphasis">
                    {{ c.annulCustomers || 0 }} مدين · {{ c.annulPayments || 0 }} تسديد
                  </td>
                  <td class="text-end font-weight-medium">
                    {{ legalMoney(c.annulTotal) }}
                  </td>
                </tr>
                <tr>
                  <td>تسديدات مباشرة</td>
                  <td class="text-medium-emphasis">
                    {{ c.directCustomers || 0 }} مدين · {{ c.directPayments || 0 }} تسديد
                  </td>
                  <td class="text-end font-weight-medium">
                    {{ legalMoney(c.directTotal) }}
                  </td>
                </tr>
                <tr>
                  <td
                    colspan="3"
                    class="text-caption text-medium-emphasis pt-3"
                  >
                    ومن المجموع
                  </td>
                </tr>
                <tr>
                  <td colspan="2">
                    من أصل الدين
                  </td>
                  <td class="text-end">
                    {{ legalMoney(c.debt) }}
                  </td>
                </tr>
                <tr>
                  <td colspan="2">
                    من غرامات القانونية
                  </td>
                  <td class="text-end">
                    {{ legalMoney(c.penalties) }}
                  </td>
                </tr>
              </tbody>
            </VTable>
          </VCardText>
        </VCard>
      </VCol>

      <!-- 2) المضاف إلى قاصة تطبيق القانونية -->
      <VCol
        cols="12"
        md="6"
      >
        <VCard class="h-100">
          <VCardText>
            <div class="text-body-2 text-medium-emphasis">
              المضاف إلى قاصة تطبيق القانونية (من قلعة الضمان)
            </div>
            <div class="text-h4 font-weight-bold text-primary my-1">
              {{ legalMoney(funds.total) }}
            </div>
            <div class="text-body-2 mb-3">
              على <strong>{{ funds.count }}</strong> دفعة
            </div>
            <VTable density="compact">
              <tbody>
                <tr>
                  <td>أجور الدعوى القضائية</td>
                  <td class="text-medium-emphasis">
                    {{ back.judicialCount || 0 }} مدين
                  </td>
                  <td class="text-end">
                    {{ legalMoney(back.judicialFees) }}
                  </td>
                </tr>
                <tr>
                  <td>أجور التنفيذ</td>
                  <td class="text-medium-emphasis">
                    {{ back.executionCount || 0 }} مدين
                  </td>
                  <td class="text-end">
                    {{ legalMoney(back.executionFees) }}
                  </td>
                </tr>
                <tr>
                  <td>غرامة التسديد الكامل</td>
                  <td class="text-medium-emphasis">
                    {{ back.earlyCount || 0 }} مدين
                  </td>
                  <td class="text-end">
                    {{ legalMoney(back.earlyFees) }}
                  </td>
                </tr>
                <tr class="font-weight-medium">
                  <td colspan="2">
                    سُدّد منه
                  </td>
                  <td class="text-end">
                    {{ legalMoney(back.total) }}
                  </td>
                </tr>
                <tr class="font-weight-bold">
                  <td colspan="2">
                    {{ (data?.companyDebt || 0) < 0 ? 'لصالح فريق القانونية' : 'دين قلعة الضمان المتبقي' }}
                  </td>
                  <td
                    class="text-end"
                    :class="(data?.companyDebt || 0) > 0 ? 'text-error' : ''"
                  >
                    {{ legalMoney(Math.abs(data?.companyDebt || 0)) }}
                  </td>
                </tr>
              </tbody>
            </VTable>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <VCard class="mt-6">
      <VTabs v-model="tab">
        <VTab value="payments">
          سجل التسديدات ({{ payments.length }})
        </VTab>
        <VTab value="funds">
          سجل دين قلعة الضمان ({{ funds.count }})
        </VTab>
      </VTabs>
      <VDivider />
      <VProgressLinear
        v-if="loading"
        indeterminate
      />
      <VWindow v-model="tab">
        <VWindowItem value="payments">
          <VTable>
            <thead>
              <tr>
                <th>التاريخ</th>
                <th>المدين</th>
                <th>النوع</th>
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
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="p in payments"
                :key="p.id"
              >
                <td class="text-no-wrap">
                  {{ formatUtc(p.atUtc) }}
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
                <td>
                  <VChip
                    size="small"
                    variant="tonal"
                    :color="p.isAnnul ? 'info' : 'success'"
                  >
                    {{ p.isAnnul ? 'مبلغ ابطال' : 'تسديد' }}
                  </VChip>
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
              </tr>
              <tr v-if="!loading && !payments.length">
                <td
                  colspan="7"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا توجد تسديدات بعد
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>

        <VWindowItem value="funds">
          <VTable>
            <thead>
              <tr>
                <th>التاريخ</th>
                <th>الملاحظة</th>
                <th class="text-end">
                  المبلغ
                </th>
                <th>سجّله</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="f in funds.records"
                :key="f.id"
              >
                <td class="text-no-wrap">
                  {{ formatUtc(f.atUtc) }}
                </td>
                <td>{{ f.note || '—' }}</td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(f.amount) }}
                </td>
                <td>{{ f.actor }}</td>
              </tr>
              <tr v-if="!loading && !funds.records.length">
                <td
                  colspan="4"
                  class="text-center text-medium-emphasis py-8"
                >
                  لم يُضف شيء إلى قاصة القانونية بعد
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>
      </VWindow>
    </VCard>
  </div>
</template>
