<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import axios from 'axios'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'

const apiUrl = localStorage.getItem('LinkCity')
const route = useRoute()
const toast = useToast()
const { canWriteNotes } = useUserRole()

const customerID = computed(() => Number(route.query.customerID) || 0)
const customer = ref(null)
const notes = ref([])
const decisions = ref([])
const loading = ref(false)
const savingNote = ref(false)
const newNote = ref('')

const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "

  return "لا يوجد"
}

const formattedDateTime = date => {
  if (!date) return ''

  return new Date(date).toLocaleString('ar-IQ', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

const isAutoDecisionNote = text => {
  const value = text || ''

  return value.startsWith('تم تصنيف بيع الزبون')
    || value.startsWith('تم إرسال الزبون')
    || value.startsWith('مدير الفرع متواصل مع الزبون')
}

const typeColor = type => {
  if (type === 'قانونية') return 'error'
  if (type === 'وهمي') return 'warning'
  if (type === 'متواصل') return 'info'

  return 'primary'
}

const timeline = computed(() => {
  const noteItems = (notes.value || [])
    .filter(n => !isAutoDecisionNote(n.noteText))
    .map(n => ({
      kind: 'note',
      date: n.createdDate,
      userName: n.userName,
      userType: n.userType,
      text: n.noteText,
    }))
  const decisionItems = (decisions.value || []).map(d => ({
    kind: 'decision',
    date: d.createdDate,
    userName: d.userName,
    userType: d.userType,
    text: d.decisionType,
    extra: d.note,
    paidPercent: d.paidPercent,
    decisionType: d.decisionType,
  }))

  return [...noteItems, ...decisionItems].sort((a, b) => new Date(b.date) - new Date(a.date))
})

async function fetchProfile() {
  if (!customerID.value) return

  try {
    loading.value = true
    const authHeader = getAuthHeaders()

    const [infoSettled, notesRes, decisionsRes, payersRes] = await Promise.allSettled([
      axios.get(`${apiUrl}Customers/Customers_InfoSimple/${customerID.value}`, { headers: authHeader }),
      axios.get(`${apiUrl}CustomerDecisions/Notes/${customerID.value}`, { headers: authHeader }),
      axios.get(`${apiUrl}CustomerDecisions/Decisions`, {
        headers: authHeader,
        params: { customerID: customerID.value },
      }),
      axios.get(`${apiUrl}CustomerDecisions/WeakWeekPayers`, { headers: authHeader }),
    ])

    const info = infoSettled.status === 'fulfilled' ? infoSettled.value.data : null
    const payers = payersRes.status === 'fulfilled' ? (payersRes.value.data || []) : []
    const fromBoard = payers.find(p => Number(p.customerID) === customerID.value)

    const hasInfo = info && (info.customerName || info.amountTotalSales != null)
    customer.value = hasInfo ? { ...fromBoard, ...info } : (fromBoard || info)

    notes.value = notesRes.status === 'fulfilled' ? (notesRes.value.data || []) : []
    decisions.value = decisionsRes.status === 'fulfilled' ? (decisionsRes.value.data || []) : []
  } catch (error) {
    console.error(error)
    toast.error('تعذر جلب ملف الزبون')
  } finally {
    loading.value = false
  }
}

async function submitNote() {
  if (!newNote.value.trim()) {
    toast.warning('اكتب الملاحظة أولاً')

    return
  }

  try {
    savingNote.value = true
    const authHeader = getAuthHeaders()
    await axios.post(`${apiUrl}CustomerDecisions/Notes`, {
      customerID: customerID.value,
      noteText: newNote.value.trim(),
    }, { headers: authHeader })
    newNote.value = ''
    toast.success('تمت إضافة الملاحظة')
    await fetchProfile()
  } catch (error) {
    console.error(error)
    toast.error(error.response?.data?.message || 'فشل حفظ الملاحظة')
  } finally {
    savingNote.value = false
  }
}

watch(customerID, fetchProfile)
onMounted(fetchProfile)
</script>

<template>
  <VCard class="pa-8">
    <div
      v-if="!customerID"
      class="text-medium-emphasis"
    >
      اختر زبوناً من لوحة اتخاذ القرار أو من قائمة العملاء.
    </div>

    <div v-else>
      <div class="d-flex align-center mb-6">
        <VIcon
          icon="tabler-user-circle"
          size="32"
          class="me-3"
        />
        <div>
          <div class="text-h5">
            {{ customer?.customerName || 'ملف الزبون' }}
          </div>
          <div class="text-medium-emphasis">
            المندوب: {{ customer?.delegateName || '—' }}
          </div>
        </div>
      </div>

      <VRow class="mb-6">
        <VCol cols="12" sm="6" md="3">
          <VSheet
            border
            rounded
            class="pa-4"
          >
            <div class="text-caption">سعر البيع</div>
            <div class="text-h6">{{ formattedNumber(customer?.amountTotalSales) }}</div>
          </VSheet>
        </VCol>
        <VCol cols="12" sm="6" md="3">
          <VSheet
            border
            rounded
            class="pa-4"
          >
            <div class="text-caption">الواصل</div>
            <div class="text-h6">{{ formattedNumber(customer?.receiptsTotal) }}</div>
          </VSheet>
        </VCol>
        <VCol cols="12" sm="6" md="3">
          <VSheet
            border
            rounded
            class="pa-4"
          >
            <div class="text-caption">الباقي</div>
            <div class="text-h6">{{ formattedNumber(customer?.amountRemaining) }}</div>
          </VSheet>
        </VCol>
        <VCol cols="12" sm="6" md="3">
          <VSheet
            border
            rounded
            class="pa-4"
          >
            <div class="text-caption">القسط</div>
            <div class="text-h6">{{ formattedNumber(customer?.amountDaySales) }}</div>
          </VSheet>
        </VCol>
      </VRow>

      <VCard
        v-if="canWriteNotes"
        variant="tonal"
        class="pa-4 mb-6"
      >
        <VTextarea
          v-model="newNote"
          label="إضافة ملاحظة"
          rows="3"
        />
        <VBtn
          color="primary"
          class="mt-2"
          :loading="savingNote"
          prepend-icon="tabler-send"
          @click="submitNote"
        >
          إضافة
        </VBtn>
      </VCard>

      <div class="text-h6 mb-4">السجل الزمني</div>
      <div
        v-if="loading"
        class="text-medium-emphasis"
      >
        جاري التحميل...
      </div>
      <div
        v-else-if="!timeline.length"
        class="text-medium-emphasis"
      >
        لا توجد ملاحظات أو قرارات بعد.
      </div>
      <VTimeline
        v-else
        side="end"
        density="compact"
        align="start"
      >
        <VTimelineItem
          v-for="(item, index) in timeline"
          :key="index"
          :dot-color="item.kind === 'decision' ? typeColor(item.decisionType) : 'primary'"
          size="small"
        >
          <div class="text-caption text-medium-emphasis">
            {{ formattedDateTime(item.date) }}
            — {{ item.userName }}
            <span v-if="item.userType"> ({{ item.userType }})</span>
          </div>
          <div v-if="item.kind === 'decision'">
            <VChip
              :color="typeColor(item.decisionType)"
              size="small"
              label
              class="me-2"
            >
              {{ item.decisionType }}
            </VChip>
            <span v-if="item.paidPercent != null">النسبة: {{ item.paidPercent }}%</span>
            <div
              v-if="item.extra"
              class="mt-1"
            >
              {{ item.extra }}
            </div>
          </div>
          <div
            v-else
            class="text-body-1"
          >
            {{ item.text }}
          </div>
        </VTimelineItem>
      </VTimeline>
    </div>
  </VCard>
</template>
