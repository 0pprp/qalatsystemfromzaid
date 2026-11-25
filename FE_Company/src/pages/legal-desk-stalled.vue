<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import {
  formatUtc,
  legalError,
  legalMoney,
  legalStalled,
  legalStatus,
} from '@/composables/legalDeskApi'

/** المدير only: debtors sent to the legal app with no progress for the chosen number of days. */
const DAYS_KEY = 'legal-desk-stalled-days'

const toast = useToast()
const router = useRouter()
const days = ref(Number(localStorage.getItem(DAYS_KEY)) || 30)
const shownDays = ref(days.value)
const loading = ref(false)
const rows = ref([])

const totalRemaining = computed(() => rows.value.reduce((sum, r) => sum + Number(r.remainingSnapshot || 0), 0))

async function load() {
  const value = Math.max(0, Math.floor(Number(days.value) || 0))

  days.value = value
  localStorage.setItem(DAYS_KEY, String(value))
  loading.value = true
  try {
    rows.value = await legalStalled(value)
    shownDays.value = value
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل القائمة'))
  }
  finally {
    loading.value = false
  }
}

const open = row => router.push({ path: '/legal-desk-debtor', query: { id: row.personId } })

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <h1 class="text-h5 font-weight-bold mb-0">
        مدينون بلا تقدم
      </h1>
      <div class="d-flex align-center gap-2">
        <span class="text-body-2">بلا تقدم منذ</span>
        <VTextField
          v-model.number="days"
          type="number"
          min="0"
          density="compact"
          hide-details
          dir="ltr"
          style="inline-size: 100px;"
          @keyup.enter="load"
        />
        <span class="text-body-2">يوم أو أكثر</span>
        <VBtn
          color="primary"
          :loading="loading"
          @click="load"
        >
          عرض
        </VBtn>
      </div>
    </div>

    <VCard>
      <VCardText class="d-flex flex-wrap align-center justify-space-between gap-2 pb-2">
        <div class="text-body-2 text-medium-emphasis">
          أُرسلوا إلى القانونية ولم تُسجَّل عليهم خطوة ولم ينتقلوا إلى مرحلة جديدة منذ {{ shownDays }} يوم أو أكثر (لا يشمل من وصلوا التنفيذ)
        </div>
        <div class="text-body-2">
          <strong>{{ rows.length }}</strong> مدين · باقي ديونهم <strong>{{ legalMoney(totalRemaining) }}</strong>
        </div>
      </VCardText>
      <VProgressLinear
        v-if="loading"
        indeterminate
      />
      <VTable>
        <thead>
          <tr>
            <th>المدين</th>
            <th>الحالة</th>
            <th>المحامي</th>
            <th>آخر تقدم</th>
            <th class="text-end">
              أيام بلا تقدم
            </th>
            <th class="text-end">
              باقي الدين
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in rows"
            :key="row.caseId"
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
            <td>{{ row.lawyerName || '—' }}</td>
            <td>
              {{ formatUtc(row.lastProgressUtc) }}
              <div class="text-caption text-medium-emphasis">
                {{ row.lastStepName ? `آخر خطوة: ${row.lastStepName}` : 'لا توجد خطوات' }}
              </div>
            </td>
            <td class="text-end">
              <VChip
                size="small"
                :color="row.idleDays >= shownDays * 2 && shownDays > 0 ? 'error' : 'warning'"
                variant="tonal"
              >
                {{ row.idleDays }}
              </VChip>
            </td>
            <td class="text-end">
              {{ legalMoney(row.remainingSnapshot) }}
            </td>
          </tr>
          <tr v-if="!loading && !rows.length">
            <td
              colspan="6"
              class="text-center text-medium-emphasis py-8"
            >
              لا يوجد مدينون متوقفون بهذه المدة
            </td>
          </tr>
        </tbody>
      </VTable>
    </VCard>
  </div>
</template>
