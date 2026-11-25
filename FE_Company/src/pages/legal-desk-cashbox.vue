<script setup>
import { computed, onMounted, ref } from 'vue'
import { useToast } from '@/composables/useToast'
import { formatUtc, legalCashbox, legalError, legalMoney } from '@/composables/legalDeskApi'

/**
 * القاصة (read-only): what was collected from debtors through التسديدات, the discounts given,
 * and what the company put into the legal app's قاصة (دين قلعة الضمان).
 */
const toast = useToast()
const loading = ref(false)
const data = ref(null)
const tab = ref('payments')

const c = computed(() => data.value?.collected || {})
const funds = computed(() => data.value?.funds || { total: 0, count: 0, records: [] })
const back = computed(() => data.value?.paidBack || {})
const payments = computed(() => data.value?.payments || [])
const d = computed(() => data.value?.discounted || {})
const discounts = computed(() => data.value?.discounts || [])

/** Discounts count into the register's total together with the money received. */
const grandTotal = computed(() => Number(c.value.total || 0) + Number(d.value.total || 0))

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
        md="4"
      >
        <VCard class="h-100">
          <VCardText>
            <div class="text-body-2 text-medium-emphasis">
              مجموع التسديدات والخصومات
            </div>
            <div class="text-h4 font-weight-bold text-success my-1">
              {{ legalMoney(grandTotal) }}
            </div>
            <div class="text-body-2 mb-3">
              <strong>{{ c.payments || 0 }}</strong> تسديد · <strong>{{ d.count || 0 }}</strong> خصم
            </div>
            <VTable density="compact">
              <tbody>
                <tr>
                  <td>تسديدات</td>
                  <td class="text-medium-emphasis">
                    {{ c.customers || 0 }} مدين
                  </td>
                  <td class="text-end font-weight-medium">
                    {{ legalMoney(c.total) }}
                  </td>
                </tr>
                <tr>
                  <td>خصومات</td>
                  <td class="text-medium-emphasis">
                    {{ d.customers || 0 }} مدين
                  </td>
                  <td class="text-end font-weight-medium">
                    {{ legalMoney(d.total) }}
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
                    {{ legalMoney(Number(c.debt || 0) + Number(d.debt || 0)) }}
                  </td>
                </tr>
                <tr>
                  <td colspan="2">
                    من غرامات القانونية
                  </td>
                  <td class="text-end">
                    {{ legalMoney(Number(c.penalties || 0) + Number(d.penalties || 0)) }}
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
        md="4"
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
                  <td>أجور القانونية</td>
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
                <tr v-if="back.earlyCount">
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
      <!-- 3) الخصومات -->
      <VCol
        cols="12"
        md="4"
      >
        <VCard class="h-100">
          <VCardText>
            <div class="text-body-2 text-medium-emphasis">
              الخصومات
            </div>
            <div class="text-h4 font-weight-bold text-warning my-1">
              {{ legalMoney(d.total) }}
            </div>
            <div class="text-body-2 mb-3">
              لـ <strong>{{ d.customers || 0 }}</strong> مدين · <strong>{{ d.count || 0 }}</strong> خصم
            </div>
            <VTable density="compact">
              <tbody>
                <tr>
                  <td>من أصل الدين</td>
                  <td class="text-end">
                    {{ legalMoney(d.debt) }}
                  </td>
                </tr>
                <tr>
                  <td>من غرامات القانونية</td>
                  <td class="text-end">
                    {{ legalMoney(d.penalties) }}
                  </td>
                </tr>
                <tr>
                  <td
                    colspan="2"
                    class="text-caption text-medium-emphasis pt-3"
                  >
                    الخصومات محسوبة ضمن مجموع القاصة مع التسديدات.
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
        <VTab value="discounts">
          سجل الخصومات ({{ discounts.length }})
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
                <th>رقم الوصل</th>
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
                <td dir="ltr">
                  {{ p.receiptNumber }}
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

        <VWindowItem value="discounts">
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
                  {{ formatUtc(p.atUtc) }}
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
              <tr v-if="!loading && !discounts.length">
                <td
                  colspan="7"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا توجد خصومات بعد
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
