<script setup>
import { onMounted, ref, watch } from 'vue'
import { paErrorMessage, paMoney, paOverview } from '@/composables/promotersAppApi'

definePage({ meta: { layout: 'default' } })

const router = useRouter()
const data = ref(null)
const loading = ref(false)
const error = ref('')
const from = ref('')
const to = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await paOverview({ from: from.value, to: to.value })
  }
  catch (err) {
    error.value = paErrorMessage(err, 'تعذر تحميل الأرقام')
  }
  finally {
    loading.value = false
  }
}

watch([from, to], load)

function thisMonth() {
  const d = new Date()
  const pad = n => String(n).padStart(2, '0')

  from.value = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-01`
  to.value = ''
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="text-h4 mb-1 d-flex align-center gap-2">
          <VIcon
            icon="tabler-world-share"
            color="primary"
          />
          المروّجون أونلاين — نظرة عامة
        </h4>
        <p class="text-medium-emphasis mb-0">
          الطلبات والمبيعات والعمولات للفترة المختارة (فارغة = كل الوقت).
        </p>
      </div>
      <div class="d-flex gap-2 align-center">
        <VTextField
          v-model="from"
          type="date"
          label="من"
          density="compact"
          hide-details
        />
        <VTextField
          v-model="to"
          type="date"
          label="إلى"
          density="compact"
          hide-details
        />
        <VBtn
          variant="tonal"
          @click="thisMonth"
        >
          هذا الشهر
        </VBtn>
        <VBtn
          variant="tonal"
          icon="tabler-refresh"
          :loading="loading"
          @click="load"
        />
      </div>
    </div>

    <VAlert
      v-if="error"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ error }}
    </VAlert>

    <template v-if="data">
      <VRow class="mb-2">
        <VCol
          v-for="c in [
            { t: 'المروّجون', v: data.users, s: `${data.activeUsers} نشط · ${data.subAccounts} فرعي`, i: 'tabler-users', color: 'primary', to: '/promoters-app-users' },
            { t: 'الطلبات', v: data.orders, s: `${data.open} قيد المتابعة الآن`, i: 'tabler-shopping-cart', color: 'info', to: '/promoters-app-orders' },
            { t: 'المبيعات', v: data.sold, s: `${data.notSold} لم يتم البيع`, i: 'tabler-circle-check', color: 'success', to: '/promoters-app-orders' },
            { t: 'العمولات المستحقة', v: paMoney(data.commissions), s: `أرصدة حالية: ${paMoney(data.balances)}`, i: 'tabler-coins', color: 'warning' },
            { t: 'المدفوع للمروّجين', v: paMoney(data.paidOut), s: 'طلبات سحب منفّذة', i: 'tabler-cash', color: 'secondary', to: '/promoters-app-payouts' },
            { t: 'طلبات سحب معلقة', v: data.pendingPayouts, s: paMoney(data.pendingPayoutAmount), i: 'tabler-hourglass', color: 'error', to: '/promoters-app-payouts' },
          ]"
          :key="c.t"
          cols="12"
          sm="6"
          md="4"
        >
          <VCard
            :class="c.to ? 'cursor-pointer' : ''"
            @click="c.to && router.push(c.to)"
          >
            <VCardText class="d-flex align-center gap-4">
              <VAvatar
                :color="c.color"
                variant="tonal"
                rounded
                size="48"
              >
                <VIcon
                  :icon="c.i"
                  size="28"
                />
              </VAvatar>
              <div>
                <div class="text-body-2 text-medium-emphasis">
                  {{ c.t }}
                </div>
                <div class="text-h5">
                  {{ c.v }}
                </div>
                <div class="text-caption text-medium-emphasis">
                  {{ c.s }}
                </div>
              </div>
            </VCardText>
          </VCard>
        </VCol>
      </VRow>

      <VRow>
        <VCol
          cols="12"
          md="6"
        >
          <VCard title="أفضل المروّجين">
            <VTable density="compact">
              <thead>
                <tr><th>المروّج</th><th>مبيعات</th><th>أرباح</th></tr>
              </thead>
              <tbody>
                <tr
                  v-for="p in data.topPromoters"
                  :key="p.id"
                >
                  <td><code dir="auto">{{ p.username }}</code></td>
                  <td>{{ p.sold }}</td>
                  <td>{{ paMoney(p.earned) }}</td>
                </tr>
                <tr v-if="!data.topPromoters.length">
                  <td
                    colspan="3"
                    class="text-center text-medium-emphasis py-4"
                  >
                    لا توجد مبيعات بعد
                  </td>
                </tr>
              </tbody>
            </VTable>
          </VCard>
        </VCol>
        <VCol
          cols="12"
          md="6"
        >
          <VCard title="أكثر المنتجات مبيعاً">
            <VTable density="compact">
              <thead>
                <tr><th>المنتج</th><th>القطع</th><th>العمولة</th></tr>
              </thead>
              <tbody>
                <tr
                  v-for="p in data.topProducts"
                  :key="p.productName"
                >
                  <td>{{ p.productName }}</td>
                  <td>{{ p.sold }}</td>
                  <td>{{ paMoney(p.commission) }}</td>
                </tr>
                <tr v-if="!data.topProducts.length">
                  <td
                    colspan="3"
                    class="text-center text-medium-emphasis py-4"
                  >
                    لا توجد مبيعات بعد
                  </td>
                </tr>
              </tbody>
            </VTable>
          </VCard>
        </VCol>
      </VRow>
    </template>
  </div>
</template>
