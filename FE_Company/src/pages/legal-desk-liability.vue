<script setup>
import { computed, onMounted, ref } from 'vue'
import { useToast } from '@/composables/useToast'
import { formatUtc, legalError, legalLiability, legalMoney, legalStatus } from '@/composables/legalDeskApi'

/**
 * ذمة القانونية (read-only):
 *  - ذمة القانونية: what the debtors in the legal app owe now, leaving out those who reached القرارات. Not tied to the month.
 *  - تسديدات الشهر: payments and discounts of the chosen month, leaving out what a debtor paid after reaching القرارات.
 *  - قرارات الشهر: what each debtor owed when he reached القرارات, for those who reached it in the chosen month.
 */
function thisMonth() {
  // The month as it is in Iraq now.
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Baghdad', year: 'numeric', month: '2-digit' }).formatToParts(new Date())

  return `${parts.find(p => p.type === 'year').value}-${parts.find(p => p.type === 'month').value}`
}

const toast = useToast()
const month = ref(thisMonth())
const loading = ref(false)
const data = ref(null)
const tab = ref('liability')

const liability = computed(() => data.value?.liability || { total: 0, count: 0, debtors: [] })
const payments = computed(() => data.value?.monthPayments || { total: 0, fromPayments: 0, paymentsCount: 0, fromDiscounts: 0, discountsCount: 0, rows: [] })
const decisions = computed(() => data.value?.monthDecisions || { total: 0, count: 0, rows: [] })

const monthLabel = computed(() => {
  const [year, number] = String(month.value || '').split('-')

  return year && number ? `${number}/${year}` : ''
})

async function load() {
  if (!month.value)
    return
  loading.value = true
  try {
    data.value = await legalLiability(month.value)
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل ذمة القانونية'))
  }
  finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <h1 class="text-h5 font-weight-bold mb-0">
        ذمة القانونية
      </h1>
      <div class="d-flex align-center gap-2">
        <span class="text-body-2">الشهر</span>
        <VTextField
          v-model="month"
          type="month"
          density="compact"
          hide-details
          dir="ltr"
          style="inline-size: 180px;"
          @update:model-value="load"
        />
        <VBtn
          variant="tonal"
          prepend-icon="tabler-refresh"
          :loading="loading"
          @click="load"
        >
          تحديث
        </VBtn>
      </div>
    </div>

    <VRow>
      <VCol
        cols="12"
        md="4"
      >
        <VCard
          class="h-100"
          style="cursor: pointer;"
          @click="tab = 'liability'"
        >
          <VCardText>
            <div class="text-body-2 text-medium-emphasis">
              ذمة القانونية
            </div>
            <div class="text-h4 font-weight-bold text-primary my-1">
              {{ legalMoney(liability.total) }}
            </div>
            <div class="text-body-2">
              المطلوب حالياً من <strong>{{ liability.count }}</strong> مدين لم يصل القرارات
            </div>
            <div class="text-caption text-medium-emphasis mt-2">
              لا تتأثر باختيار الشهر.
            </div>
          </VCardText>
        </VCard>
      </VCol>

      <VCol
        cols="12"
        md="4"
      >
        <VCard
          class="h-100"
          style="cursor: pointer;"
          @click="tab = 'payments'"
        >
          <VCardText>
            <div class="text-body-2 text-medium-emphasis">
              تسديدات الشهر <span dir="ltr">{{ monthLabel }}</span>
            </div>
            <div class="text-h4 font-weight-bold text-success my-1">
              {{ legalMoney(payments.total) }}
            </div>
            <VTable density="compact">
              <tbody>
                <tr>
                  <td>من التسديدات</td>
                  <td class="text-medium-emphasis">
                    {{ payments.paymentsCount }} تسديد
                  </td>
                  <td class="text-end font-weight-medium">
                    {{ legalMoney(payments.fromPayments) }}
                  </td>
                </tr>
                <tr>
                  <td>من الخصومات</td>
                  <td class="text-medium-emphasis">
                    {{ payments.discountsCount }} خصم
                  </td>
                  <td class="text-end font-weight-medium">
                    {{ legalMoney(payments.fromDiscounts) }}
                  </td>
                </tr>
              </tbody>
            </VTable>
            <div class="text-caption text-medium-emphasis mt-2">
              لا تشمل ما سدّده المدين بعد وصوله القرارات.
            </div>
          </VCardText>
        </VCard>
      </VCol>

      <VCol
        cols="12"
        md="4"
      >
        <VCard
          class="h-100"
          style="cursor: pointer;"
          @click="tab = 'decisions'"
        >
          <VCardText>
            <div class="text-body-2 text-medium-emphasis">
              قرارات الشهر <span dir="ltr">{{ monthLabel }}</span>
            </div>
            <div class="text-h4 font-weight-bold text-warning my-1">
              {{ legalMoney(decisions.total) }}
            </div>
            <div class="text-body-2">
              المطلوب من <strong>{{ decisions.count }}</strong> مدين وصل القرارات هذا الشهر
            </div>
            <div class="text-caption text-medium-emphasis mt-2">
              يُحسب ما بقي على المدين لحظة وصوله القرارات، ويخرج من ذمة القانونية.
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <VCard class="mt-6">
      <VTabs v-model="tab">
        <VTab value="liability">
          ذمة القانونية ({{ liability.count }})
        </VTab>
        <VTab value="payments">
          تسديدات الشهر ({{ payments.rows.length }})
        </VTab>
        <VTab value="decisions">
          قرارات الشهر ({{ decisions.count }})
        </VTab>
      </VTabs>
      <VDivider />
      <VProgressLinear
        v-if="loading"
        indeterminate
      />
      <VWindow v-model="tab">
        <!-- ذمة القانونية -->
        <VWindowItem value="liability">
          <VTable>
            <thead>
              <tr>
                <th>المدين</th>
                <th>الحالة</th>
                <th>أُرسل في</th>
                <th class="text-end">
                  باقي الدين
                </th>
                <th class="text-end">
                  الغرامات
                </th>
                <th class="text-end">
                  المطلوب
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="d in liability.debtors"
                :key="d.caseId"
              >
                <td>
                  <RouterLink :to="{ path: '/legal-desk-debtor', query: { id: d.personId } }">
                    {{ d.fullName }}
                  </RouterLink>
                  <div
                    v-if="d.archiveCodes"
                    class="text-caption text-medium-emphasis"
                    dir="ltr"
                  >
                    {{ d.archiveCodes }}
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
                <td>{{ formatUtc(d.sentAtUtc) }}</td>
                <td class="text-end">
                  {{ legalMoney(d.remaining) }}
                </td>
                <td class="text-end">
                  {{ legalMoney(d.penaltiesDue) }}
                </td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(d.due) }}
                </td>
              </tr>
              <tr v-if="!loading && !liability.debtors.length">
                <td
                  colspan="6"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا يوجد مدينون ضمن ذمة القانونية
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>

        <!-- تسديدات الشهر -->
        <VWindowItem value="payments">
          <VTable>
            <thead>
              <tr>
                <th>التاريخ</th>
                <th>المدين</th>
                <th>النوع</th>
                <th>رقم الوصل</th>
                <th class="text-end">
                  من الدين
                </th>
                <th class="text-end">
                  من الغرامات
                </th>
                <th class="text-end">
                  المبلغ
                </th>
                <th>بواسطة</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="p in payments.rows"
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
                <td>
                  <VChip
                    size="small"
                    variant="tonal"
                    :color="p.kind === 'Discount' ? 'warning' : 'success'"
                  >
                    {{ p.kind === 'Discount' ? 'خصم' : 'تسديد' }}
                  </VChip>
                </td>
                <td dir="ltr">
                  {{ p.receiptNumber || '—' }}
                </td>
                <td class="text-end">
                  {{ legalMoney(p.debtAmount) }}
                </td>
                <td class="text-end">
                  {{ legalMoney(p.penaltyAmount) }}
                </td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(p.amount) }}
                </td>
                <td>{{ p.actor }}</td>
              </tr>
              <tr v-if="!loading && !payments.rows.length">
                <td
                  colspan="8"
                  class="text-center text-medium-emphasis py-8"
                >
                  لا توجد تسديدات أو خصومات محسوبة في هذا الشهر
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>

        <!-- قرارات الشهر -->
        <VWindowItem value="decisions">
          <VTable>
            <thead>
              <tr>
                <th>المدين</th>
                <th>وصل القرارات في</th>
                <th>الحالة الآن</th>
                <th class="text-end">
                  المطلوب لحظة الوصول
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="d in decisions.rows"
                :key="d.caseId"
              >
                <td>
                  <RouterLink :to="{ path: '/legal-desk-debtor', query: { id: d.personId } }">
                    {{ d.fullName }}
                  </RouterLink>
                  <div
                    v-if="d.archiveCodes"
                    class="text-caption text-medium-emphasis"
                    dir="ltr"
                  >
                    {{ d.archiveCodes }}
                  </div>
                </td>
                <td>{{ formatUtc(d.reachedAtUtc, true) }}</td>
                <td>
                  <VChip
                    size="small"
                    :color="legalStatus(d.stage).color"
                    variant="tonal"
                  >
                    {{ legalStatus(d.stage).label }}
                  </VChip>
                </td>
                <td class="text-end font-weight-bold">
                  {{ legalMoney(d.amount) }}
                </td>
              </tr>
              <tr v-if="!loading && !decisions.rows.length">
                <td
                  colspan="4"
                  class="text-center text-medium-emphasis py-8"
                >
                  لم يصل أحد إلى القرارات في هذا الشهر
                </td>
              </tr>
            </tbody>
          </VTable>
        </VWindowItem>
      </VWindow>
    </VCard>
  </div>
</template>
