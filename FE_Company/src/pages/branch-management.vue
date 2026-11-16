<script setup>
import { onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { directorCities, directorCitiesCached } from '@/composables/directorApi'
import BaLegalNames from '@/components/branch-app/BaLegalNames.vue'
import BaDeclining from '@/components/branch-app/BaDeclining.vue'
import BaWrongSales from '@/components/branch-app/BaWrongSales.vue'
import BaRatings from '@/components/branch-app/BaRatings.vue'
import BaWaivers from '@/components/branch-app/BaWaivers.vue'
import BaDecisions from '@/components/branch-app/BaDecisions.vue'
import BaSales from '@/components/branch-app/BaSales.vue'
import BaPayments from '@/components/branch-app/BaPayments.vue'

defineOptions({ name: 'BranchManagement' })
definePage({ meta: { layout: 'default' } })

const route = useRoute()
const router = useRouter()

const tabs = [
  { value: 'legal', title: 'أسماء قانونية', icon: 'tabler-gavel', comp: BaLegalNames },
  { value: 'declining', title: 'الزبائن المتدنية', icon: 'tabler-trending-down', comp: BaDeclining },
  { value: 'wrong', title: 'المبيعات الخاطئة', icon: 'tabler-alert-triangle', comp: BaWrongSales },
  { value: 'ratings', title: 'التقييم', icon: 'tabler-chart-bar', comp: BaRatings },
  { value: 'waivers', title: 'طلبات التنازل', icon: 'tabler-heart-handshake', comp: BaWaivers },
  { value: 'decisions', title: 'سجل القرارات', icon: 'tabler-history', comp: BaDecisions },
  { value: 'sales', title: 'المبيعات', icon: 'tabler-receipt', comp: BaSales },
  { value: 'payments', title: 'التسديدات', icon: 'tabler-cash', comp: BaPayments },
]

const cities = ref(directorCitiesCached())
const city = ref('')
const tab = ref(tabs.some(t => t.value === route.query.tab) ? String(route.query.tab) : 'legal')

function cityValueOf(c) {
  return c?.value ?? c?.Value ?? ''
}

function cityNameOf(c) {
  return c?.name ?? c?.Name ?? cityValueOf(c)
}

function pickInitialCity() {
  const wanted = String(route.query.city || localStorage.getItem('branch-management-city') || '')
  const values = cities.value.map(cityValueOf)
  city.value = values.includes(wanted) ? wanted : (values[0] || '')
}

watch([city, tab], ([c, t]) => {
  if (c)
    localStorage.setItem('branch-management-city', c)
  if (route.query.city !== c || route.query.tab !== t)
    router.replace({ query: { ...route.query, city: c || undefined, tab: t } })
})

onMounted(async () => {
  pickInitialCity()
  try {
    const rows = await directorCities()
    if (Array.isArray(rows) && rows.length) {
      cities.value = rows
      if (!rows.some(r => cityValueOf(r) === city.value))
        pickInitialCity()
    }
  }
  catch { /* keep cached list */ }
})
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between ga-3 mb-4">
      <h4 class="text-h5 mb-0">
        ادارة الفرع
      </h4>
      <VSelect
        v-model="city"
        :items="cities"
        :item-title="cityNameOf"
        :item-value="cityValueOf"
        label="المحافظة"
        density="compact"
        hide-details
        style="max-inline-size: 260px; min-inline-size: 200px"
      />
    </div>

    <VCard>
      <VTabs
        v-model="tab"
        show-arrows
        density="comfortable"
      >
        <VTab
          v-for="t in tabs"
          :key="t.value"
          :value="t.value"
        >
          <VIcon
            :icon="t.icon"
            size="18"
            class="me-1"
          />
          {{ t.title }}
        </VTab>
      </VTabs>
      <VDivider />
      <VCardText>
        <div
          v-if="!city"
          class="text-medium-emphasis text-center py-8"
        >
          اختر المحافظة
        </div>
        <VWindow
          v-else
          v-model="tab"
          :touch="false"
        >
          <VWindowItem
            v-for="t in tabs"
            :key="t.value"
            :value="t.value"
          >
            <div class="pt-3 branch-app-panel">
              <component
                :is="t.comp"
                :city="city"
              />
            </div>
          </VWindowItem>
        </VWindow>
      </VCardText>
    </VCard>
  </div>
</template>

<style>
/* keep table cells on one line (horizontal scroll on phones); notes wrap inside a fixed min width */
.branch-app-panel .v-table th,
.branch-app-panel .v-table td {
  white-space: nowrap;
}

.branch-app-panel .v-table td .ba-wrap {
  display: inline-block;
  min-inline-size: 220px;
  max-inline-size: 360px;
  white-space: normal;
}
</style>
