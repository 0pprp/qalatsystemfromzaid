<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import ModernStatCard from "@/components/ModernStatCard.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'

const apiUrl = localStorage.getItem('LinkCity')
const router = useRouter()
const toast = useToast()
const { canDecide } = useUserRole()

const payersData = ref([])
const loading = ref(false)
const decideDialog = ref(false)
const submitting = ref(false)
const selectedCustomer = ref(null)
const decisionForm = ref({
  decisionType: '',
  note: '',
})

const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "

  return "لا يوجد"
}

const formattedDate = date =>
  date ? new Date(date).toLocaleDateString('en-CA') : 'لا يوجد'

const headers = computed(() => [
  { title: canDecide.value ? 'اتخاذ قرار' : 'الملف', key: 'actions', sortable: false },
  { title: 'اسم العميل', key: 'customerName' },
  { title: 'رقم الهاتف', key: 'phoneNumber' },
  { title: 'القائمة', key: 'delegateName' },
  { title: 'المباع', key: 'itemsNames' },
  { title: 'سعر البيع', key: 'amountTotalSales' },
  { title: 'المدفوع هذا الأسبوع', key: 'weekPaid' },
  { title: 'النسبة %', key: 'paidPercent' },
  { title: 'الباقي', key: 'amountRemaining' },
  { title: 'تاريخ اخر تسديد', key: 'lastPaymentDate' },
])

const decisionOptions = [
  { title: 'متواصل معه', value: 'متواصل', color: 'info', icon: 'tabler-phone-call' },
  { title: 'إرسال للقانونية', value: 'قانونية', color: 'error', icon: 'tabler-scale' },
  { title: 'مبيع وهمي', value: 'وهمي', color: 'warning', icon: 'tabler-alert-octagon' },
]

const totals = computed(() => [
  {
    icon: 'tabler-user',
    value: payersData.value.length,
    title: 'عدد الزبائن',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-percentage',
    value: payersData.value.length
      ? (payersData.value.reduce((sum, row) => sum + (row.paidPercent || 0), 0) / payersData.value.length).toFixed(2) + ' %'
      : '0 %',
    title: 'متوسط النسبة',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-wallet',
    value: formattedNumber(
      payersData.value.reduce((sum, row) => sum + (row.amountTotalSales || 0), 0),
    ),
    title: 'سعر البيع',
    color: "success",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
  {
    icon: 'tabler-currency-dollar',
    value: formattedNumber(
      payersData.value.reduce((sum, row) => sum + (row.weekPaid || 0), 0),
    ),
    title: 'مدفوع الأسبوع',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
])

async function fetchWeakPayers() {
  try {
    loading.value = true
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}CustomerDecisions/WeakWeekPayers`, { headers: authHeader })
    payersData.value = response.data || []
  } catch (error) {
    console.error(error)
    toast.error('تعذر جلب لوحة ضعفاء التسديد')
  } finally {
    loading.value = false
  }
}

function openDecide(item) {
  if (!canDecide.value) {
    toast.warning('اتخاذ القرار من صلاحية مدير الفرع فقط')

    return
  }

  selectedCustomer.value = item
  decisionForm.value = { decisionType: '', note: '' }
  decideDialog.value = true
}

function openProfile(item) {
  router.push({ name: 'customer-profile', query: { customerID: item.customerID } })
}

async function submitDecision() {
  if (!canDecide.value) {
    toast.warning('اتخاذ القرار من صلاحية مدير الفرع فقط')

    return
  }

  if (!selectedCustomer.value || !decisionForm.value.decisionType) {
    toast.warning('اختر نوع القرار')

    return
  }

  try {
    submitting.value = true
    const authHeader = getAuthHeaders()
    await axios.post(`${apiUrl}CustomerDecisions/Decide`, {
      customerID: selectedCustomer.value.customerID,
      decisionType: decisionForm.value.decisionType,
      note: decisionForm.value.note || null,
    }, { headers: authHeader })

    decideDialog.value = false
    toast.success('تم حفظ القرار')
    const decidedId = selectedCustomer.value.customerID
    payersData.value = payersData.value.filter(row => row.customerID !== decidedId)
    await fetchWeakPayers()
  } catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'فشل حفظ القرار')
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  fetchWeakPayers()
})
</script>

<template>
  <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, index) in totals"
      :key="index"
      cols="12"
      sm="6"
      md="3"
    >
      <ModernStatCard
        :title="card.title"
        :value="card.value"
        :icon="card.icon"
        :color="card.color"
        :gradient="card.gradient"
      />
    </VCol>
  </VRow>

  <VCard class="pa-10">
    <VRow class="mb-4">
      <VCol cols="12">
        <div class="text-h6 mb-2">
          زبائن دفعوا 2% أو أقل من سعر البيع خلال آخر 7 أيام
        </div>
        <VAlert
          v-if="!canDecide"
          type="info"
          variant="tonal"
          class="mb-4"
        >
          اتخاذ القرار من صلاحية مدير الفرع فقط. يمكنك فتح الملف وكتابة ملاحظة.
        </VAlert>
        <VBtn
          color="primary"
          :loading="loading"
          prepend-icon="tabler-refresh"
          class="me-2"
          @click="fetchWeakPayers"
        >
          تحديث اللوحة
        </VBtn>
        <VBtn
          color="secondary"
          variant="tonal"
          prepend-icon="tabler-list-check"
          to="/decisions-list"
        >
          سجل القرارات
        </VBtn>
      </VCol>
    </VRow>

    <VDataTable
      :headers="headers"
      :items="payersData"
      :loading="loading"
      :items-per-page="50"
      style="overflow: hidden; block-size: 100%;white-space: nowrap;"
      items-per-page-text="عدد السجل"
      class="text-no-wrap custom-data-table"
    >
      <template #item.actions="{ item }">
        <div class="d-flex gap-1">
          <VBtn
            v-if="canDecide"
            color="primary"
            prepend-icon="tabler-gavel"
            @click="openDecide(item)"
          >
            اتخاذ قرار
          </VBtn>
          <VBtn
            color="secondary"
            variant="tonal"
            prepend-icon="tabler-user"
            @click="openProfile(item)"
          >
            الملف
          </VBtn>
        </div>
      </template>
      <template #item.customerName="{ item }">
        <div>{{ item.customerName }}</div>
      </template>
      <template #item.phoneNumber="{ item }">
        <div>{{ item.phoneNumber || 'لا يوجد' }}</div>
      </template>
      <template #item.delegateName="{ item }">
        <div>{{ item.delegateName || 'لا يوجد' }}</div>
      </template>
      <template #item.itemsNames="{ item }">
        <div>{{ item.itemsNames || 'لا يوجد' }}</div>
      </template>
      <template #item.amountTotalSales="{ item }">
        <div class="premium-amount amt-total-sales">
          {{ formattedNumber(item.amountTotalSales) }}
        </div>
      </template>
      <template #item.weekPaid="{ item }">
        <div class="premium-amount amt-total-receipts">
          {{ formattedNumber(item.weekPaid) }}
        </div>
      </template>
      <template #item.paidPercent="{ item }">
        <VChip
          color="error"
          size="small"
          label
        >
          {{ item.paidPercent ?? 0 }} %
        </VChip>
      </template>
      <template #item.amountRemaining="{ item }">
        <div class="premium-amount amt-remaining">
          {{ formattedNumber(item.amountRemaining) }}
        </div>
      </template>
      <template #item.lastPaymentDate="{ item }">
        <div>{{ item.lastPaymentDate ? formattedDate(item.lastPaymentDate) : 'لا يوجد' }}</div>
      </template>
    </VDataTable>
  </VCard>

  <VDialog
    v-model="decideDialog"
    max-width="560"
  >
    <VCard>
      <VCardTitle class="d-flex align-center">
        اتخاذ قرار
        <VSpacer />
        <VBtn
          icon
          variant="text"
          @click="decideDialog = false"
        >
          <VIcon icon="tabler-x" />
        </VBtn>
      </VCardTitle>
      <VCardText>
        <div class="mb-4">
          الزبون: <strong>{{ selectedCustomer?.customerName }}</strong>
          — النسبة هذا الأسبوع:
          <strong>{{ selectedCustomer?.paidPercent ?? 0 }}%</strong>
        </div>
        <VRadioGroup v-model="decisionForm.decisionType">
          <VRadio
            v-for="opt in decisionOptions"
            :key="opt.value"
            :label="opt.title"
            :value="opt.value"
            :color="opt.color"
          />
        </VRadioGroup>
        <VTextarea
          v-model="decisionForm.note"
          label="ملاحظة (اختياري)"
          rows="3"
          class="mt-4"
        />
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          @click="decideDialog = false"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="primary"
          :loading="submitting"
          :disabled="!decisionForm.decisionType"
          @click="submitDecision"
        >
          حفظ القرار
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
