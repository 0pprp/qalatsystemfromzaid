<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import {
  LEGAL_STATUS,
  legalError,
  legalMoney,
  legalPersons,
  legalStatus,
} from '@/composables/legalDeskApi'

const toast = useToast()
const route = useRoute()
const router = useRouter()

const TABS = [{ key: '', label: 'الكل' }, ...Object.entries(LEGAL_STATUS).map(([key, s]) => ({ key, label: s.label }))]

const status = ref(String(route.query.status || ''))
const q = ref('')
const loading = ref(false)
const all = ref([])

const rows = computed(() => all.value.filter(r => !status.value || legalStatus(r.stage, r.isAnnulled).key === status.value))

async function load() {
  loading.value = true
  try {
    all.value = await legalPersons({ q: q.value.trim() })
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل المدينين'))
  }
  finally {
    loading.value = false
  }
}

watch(status, value => router.replace({ query: value ? { status: value } : {} }))

const open = row => router.push({ path: '/legal-desk-debtor', query: { id: row.id } })

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <h1 class="text-h5 font-weight-bold mb-0">
        المدينون
      </h1>
      <VTextField
        v-model="q"
        placeholder="بحث بالاسم أو الهاتف أو كود الارشيف"
        prepend-inner-icon="tabler-search"
        density="compact"
        hide-details
        clearable
        style="max-inline-size: 300px;"
        @keyup.enter="load"
        @click:clear="q = ''; load()"
      />
    </div>

    <VCard>
      <VTabs
        v-model="status"
        show-arrows
      >
        <VTab
          v-for="t in TABS"
          :key="t.key"
          :value="t.key"
        >
          {{ t.label }}
        </VTab>
      </VTabs>
      <VDivider />
      <VProgressLinear
        v-if="loading"
        indeterminate
      />
      <VTable>
        <thead>
          <tr>
            <th>المدين</th>
            <th class="text-end">
              باقي الدين
            </th>
            <th class="text-end">
              الغرامات
            </th>
            <th>الحالة</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in rows"
            :key="row.id"
            style="cursor: pointer;"
            @click="open(row)"
          >
            <td>
              <div class="font-weight-medium">
                {{ row.fullName }}
              </div>
              <div
                class="text-caption text-medium-emphasis"
                dir="ltr"
              >
                {{ row.phone || '' }}
              </div>
              <div v-if="row.archiveCodes">
                <VChip
                  v-for="code in row.archiveCodes.split(' · ')"
                  :key="code"
                  size="x-small"
                  color="primary"
                  variant="tonal"
                  class="me-1"
                  style="direction: ltr; unicode-bidi: isolate; font-family: Consolas, monospace; font-weight: 700;"
                >
                  {{ code }}
                </VChip>
              </div>
            </td>
            <td class="text-end">
              {{ legalMoney(row.remainingSnapshot) }}
            </td>
            <td class="text-end">
              {{ legalMoney(row.penaltiesCharged - row.penaltiesPaid) }}
            </td>
            <td>
              <VChip
                size="small"
                :color="legalStatus(row.stage, row.isAnnulled).color"
                variant="tonal"
              >
                {{ legalStatus(row.stage, row.isAnnulled).label }}
              </VChip>
            </td>
          </tr>
          <tr v-if="!loading && !rows.length">
            <td
              colspan="4"
              class="text-center text-medium-emphasis py-8"
            >
              لا يوجد مدينون هنا
            </td>
          </tr>
        </tbody>
      </VTable>
    </VCard>
  </div>
</template>
