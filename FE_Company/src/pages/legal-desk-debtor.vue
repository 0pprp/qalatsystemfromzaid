<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import LegalAccountSearchDialog from '@/components/LegalAccountSearchDialog.vue'
import {
  CHARGE_KIND,
  accountKey,
  legalError,
  legalDay,
  formatUtc,
  legalAddAccounts,
  legalDeletePerson,
  legalPerson,
  legalRemoveAccount,
  legalSend,
  legalUpdatePerson,
  legalMoney,
  legalStatus,
} from '@/composables/legalDeskApi'

const toast = useToast()
const route = useRoute()
const router = useRouter()

const id = computed(() => Number(route.query.id || 0))
const loading = ref(false)
const data = ref(null)

const person = computed(() => data.value?.person || {})
const accounts = computed(() => data.value?.accounts || [])
const totals = computed(() => data.value?.totals || { remaining: 0, penaltiesDue: 0, totalDue: 0 })
const stage = computed(() => person.value.stage || '')
const status = computed(() => legalStatus(person.value.stage, person.value.isAnnulled))
const pendingRequest = computed(() => (data.value?.requests || []).find(r => r.status === 'Pending'))

async function load() {
  if (!id.value)
    return
  loading.value = true
  try {
    data.value = await legalPerson(id.value)
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل ملف المدين'))
  }
  finally {
    loading.value = false
  }
}

// —— إرسال ——
const sending = ref(false)

async function send() {
  sending.value = true
  try {
    await legalSend(id.value)
    toast.success('أُرسل إلى تطبيق القانونية (الاسماء المضافة مؤخرا)')
    await load()
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر الإرسال'))
  }
  finally {
    sending.value = false
  }
}

// —— تعديل البيانات ——
const editDialog = ref(false)
const editForm = ref({})
const saving = ref(false)

function openEdit() {
  editForm.value = {
    fullName: person.value.fullName,
    phone: person.value.phone,
    address: person.value.address,
    notes: person.value.notes,
  }
  editDialog.value = true
}

async function saveEdit() {
  saving.value = true
  try {
    await legalUpdatePerson(id.value, editForm.value)
    editDialog.value = false
    await load()
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر الحفظ'))
  }
  finally {
    saving.value = false
  }
}

async function removePerson() {
  if (!window.confirm('حذف ملف المدين وفك تجميع حساباته؟'))
    return
  try {
    await legalDeletePerson(id.value)
    toast.success('تم فك التجميع')
    router.push('/legal-desk-debtors')
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر الحذف'))
  }
}

// —— الحسابات ——
const searchDialog = ref(false)
const takenKeys = computed(() => accounts.value.map(accountKey))

async function addAccounts(rows) {
  if (!rows.length)
    return
  try {
    await legalAddAccounts(id.value, rows.map(r => ({ cityValue: r.cityValue, customerId: r.customerId })))
    toast.success('أُضيفت الحسابات إلى الملف')
    await load()
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر إضافة الحسابات'))
  }
}

async function removeAccount(account) {
  if (!window.confirm(`إزالة الحساب «${account.customerName}» من ملف المدين؟`))
    return
  try {
    await legalRemoveAccount(id.value, account.id)
    await load()
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر إزالة الحساب'))
  }
}

// التسديد itself is taken in the «التسديدات» tab; this opens its dialog for this debtor.
const openPay = () => router.push({ path: '/legal-desk-payments', query: { pay: id.value } })

watch(id, load)

onMounted(load)
</script>

<template>
  <div>
    <VProgressLinear
      v-if="loading && !data"
      indeterminate
    />
    <template v-if="data">
      <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
        <div>
          <VBtn
            variant="text"
            size="small"
            prepend-icon="tabler-arrow-right"
            class="mb-1 px-1"
            to="/legal-desk-debtors"
          >
            المدينون
          </VBtn>
          <h1 class="text-h5 font-weight-bold mb-1">
            {{ person.fullName }}
            <VChip
              size="small"
              :color="status.color"
              variant="tonal"
              class="ms-2"
            >
              {{ status.label }}
            </VChip>
          </h1>
          <div class="text-body-2 text-medium-emphasis">
            <span dir="ltr">{{ person.phone || 'بلا هاتف' }}</span>
            <span v-if="person.address"> · {{ person.address }}</span>
            <span v-if="stage === 'Closed' && person.closeReason"> · {{ person.closeReason }}</span>
          </div>
        </div>
        <div class="d-flex flex-wrap gap-2">
          <VBtn
            variant="tonal"
            prepend-icon="tabler-edit"
            @click="openEdit"
          >
            تعديل
          </VBtn>
          <VBtn
            v-if="!person.caseId"
            variant="tonal"
            color="error"
            prepend-icon="tabler-trash"
            @click="removePerson"
          >
            فك التجميع
          </VBtn>
          <VBtn
            v-if="!person.caseId || stage === 'Closed'"
            color="primary"
            prepend-icon="tabler-send"
            :loading="sending"
            @click="send"
          >
            {{ stage === 'Closed' ? 'إعادة الإرسال للقانونية' : 'إرسال إلى القانونية' }}
          </VBtn>
          <VBtn
            color="success"
            prepend-icon="tabler-cash"
            :disabled="totals.totalDue <= 0"
            @click="openPay"
          >
            تسديد
          </VBtn>
        </div>
      </div>

      <VAlert
        v-if="data.staleCities?.length"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-3"
      >
        تعذر الاتصال بـ {{ data.staleCities.join('، ') }} — أرصدة حساباتها من آخر تحديث وقد تكون قديمة.
      </VAlert>
      <VAlert
        v-if="pendingRequest"
        type="info"
        variant="tonal"
        density="compact"
        class="mb-3"
      >
        يوجد طلب تسديد كامل من فريق القانونية بانتظار قرارك.
        <VBtn
          size="small"
          variant="text"
          :to="{ path: '/legal-desk-payments', query: { tab: 'settlement' } }"
        >
          فتح الطلبات
        </VBtn>
      </VAlert>

      <VRow class="mb-1">
        <VCol
          cols="12"
          sm="4"
        >
          <VCard>
            <VCardText>
              <div class="text-body-2 text-medium-emphasis">
                باقي الدين
              </div>
              <div class="text-h5 font-weight-bold">
                {{ legalMoney(totals.remaining) }}
              </div>
            </VCardText>
          </VCard>
        </VCol>
        <VCol
          cols="12"
          sm="4"
        >
          <VCard>
            <VCardText>
              <div class="text-body-2 text-medium-emphasis">
                غرامات القانونية المستحقة
              </div>
              <div class="text-h5 font-weight-bold">
                {{ legalMoney(totals.penaltiesDue) }}
              </div>
            </VCardText>
          </VCard>
        </VCol>
        <VCol
          cols="12"
          sm="4"
        >
          <VCard
            color="primary"
            variant="tonal"
          >
            <VCardText>
              <div class="text-body-2">
                المطلوب من المدين
              </div>
              <div class="text-h5 font-weight-bold">
                {{ legalMoney(totals.totalDue) }}
              </div>
            </VCardText>
          </VCard>
        </VCol>
      </VRow>

      <!-- الحسابات -->
      <VCard class="mb-4">
        <VCardText class="d-flex align-center justify-space-between pb-2">
          <div class="text-subtitle-1 font-weight-bold">
            حسابات المدين ({{ accounts.length }})
          </div>
          <VBtn
            size="small"
            variant="tonal"
            prepend-icon="tabler-plus"
            @click="searchDialog = true"
          >
            إضافة حساب
          </VBtn>
        </VCardText>
        <VTable density="compact">
          <thead>
            <tr>
              <th>كود ارشيف القانونية</th>
              <th>الاسم في الفرع</th>
              <th>الفرع</th>
              <th>القائمة</th>
              <th>المواد</th>
              <th>تاريخ البيع</th>
              <th>آخر تسديد</th>
              <th class="text-end">
                المبلغ
              </th>
              <th class="text-end">
                الواصل
              </th>
              <th class="text-end">
                الباقي
              </th>
              <th />
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="a in accounts"
              :key="a.id"
            >
              <td>
                <VChip
                  v-if="a.archiveCode"
                  size="small"
                  color="primary"
                  variant="tonal"
                  style="direction: ltr; unicode-bidi: isolate; font-family: Consolas, monospace; font-weight: 700;"
                >
                  {{ a.archiveCode }}
                </VChip>
                <span
                  v-else
                  class="text-disabled"
                >—</span>
              </td>
              <td>{{ a.customerName }}</td>
              <td>{{ a.cityName }}</td>
              <td>{{ a.listName || '—' }}</td>
              <td>{{ a.items || '—' }}</td>
              <td>{{ legalDay(a.saleDate) }}</td>
              <td>{{ legalDay(a.lastPaymentDate) }}</td>
              <td class="text-end">
                {{ legalMoney(a.total) }}
              </td>
              <td class="text-end">
                {{ legalMoney(a.paid) }}
              </td>
              <td class="text-end font-weight-medium">
                {{ legalMoney(a.remaining) }}
                <VChip
                  v-if="a.remaining <= 0"
                  size="x-small"
                  color="success"
                  variant="tonal"
                  class="ms-1"
                >
                  منتهي
                </VChip>
              </td>
              <td>
                <VBtn
                  icon="tabler-x"
                  size="x-small"
                  variant="text"
                  @click="removeAccount(a)"
                />
              </td>
            </tr>
          </tbody>
        </VTable>
      </VCard>

      <VRow>
        <!-- الغرامات -->
        <VCol
          cols="12"
          md="5"
        >
          <VCard class="h-100">
            <VCardText class="pb-2">
              <div class="text-subtitle-1 font-weight-bold">
                غرامات القانونية
              </div>
            </VCardText>
            <VTable density="compact">
              <tbody>
                <tr
                  v-for="c in data.charges"
                  :key="c.id"
                >
                  <td>{{ CHARGE_KIND[c.kind] || c.kind }}</td>
                  <td>{{ formatUtc(c.atUtc) }}</td>
                  <td class="text-end">
                    {{ legalMoney(c.amount) }}
                  </td>
                </tr>
                <tr v-if="!data.charges.length">
                  <td class="text-medium-emphasis py-3">
                    لا توجد غرامات. تُضاف 200,000 عند اقامة الدعوى و150,000 عند التنفيذ.
                  </td>
                </tr>
              </tbody>
            </VTable>
          </VCard>
        </VCol>

        <!-- التسديدات -->
        <VCol
          cols="12"
          md="7"
        >
          <VCard class="h-100">
            <VCardText class="pb-2">
              <div class="text-subtitle-1 font-weight-bold">
                تسديدات القانونية
              </div>
            </VCardText>
            <VTable density="compact">
              <thead>
                <tr>
                  <th>التاريخ</th>
                  <th class="text-end">
                    دين
                  </th>
                  <th class="text-end">
                    غرامات
                  </th>
                  <th>بواسطة</th>
                  <th>ملاحظة</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="p in data.payments"
                  :key="p.id"
                >
                  <td>{{ formatUtc(p.atUtc, true) }}</td>
                  <td class="text-end">
                    {{ legalMoney(p.debtAmount) }}
                  </td>
                  <td class="text-end">
                    {{ legalMoney(p.penaltyAmount) }}
                  </td>
                  <td>{{ p.actor }}</td>
                  <td>
                    {{ p.note || '' }}
                  </td>
                </tr>
                <tr v-if="!data.payments.length">
                  <td
                    colspan="5"
                    class="text-medium-emphasis py-3"
                  >
                    لا توجد تسديدات من القانونية بعد
                  </td>
                </tr>
              </tbody>
            </VTable>
          </VCard>
        </VCol>
      </VRow>
    </template>

    <!-- تعديل -->
    <VDialog
      v-model="editDialog"
      max-width="560"
    >
      <VCard>
        <VCardTitle>بيانات المدين</VCardTitle>
        <VCardText>
          <VTextField
            v-model="editForm.fullName"
            label="الاسم"
            density="compact"
            class="mb-3"
          />
          <VTextField
            v-model="editForm.phone"
            label="الهاتف"
            density="compact"
            dir="ltr"
            class="mb-3"
          />
          <VTextField
            v-model="editForm.address"
            label="العنوان"
            density="compact"
            class="mb-3"
          />
          <VTextarea
            v-model="editForm.notes"
            label="ملاحظات"
            rows="2"
            density="compact"
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="editDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="saving"
            @click="saveEdit"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <LegalAccountSearchDialog
      v-model="searchDialog"
      :initial-query="String(person.fullName || '').trim()"
      :taken="takenKeys"
      confirm-label="إضافة إلى الملف"
      @pick="addAccounts"
    />
  </div>
</template>

