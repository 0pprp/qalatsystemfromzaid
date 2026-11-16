<script setup>
import { onMounted, ref, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import {
  paAdjustWallet,
  paCreateUser,
  paDateTime,
  paErrorMessage,
  paFieldErrors,
  paGetUser,
  paListUsers,
  paMoney,
  paNewPassword,
  paPaged,
  paSetCut,
  paSetFull,
  paSetPassword,
  paSuspendUser,
  paUpdateUser,
  paUserEntries,
} from '@/composables/promotersAppApi'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()
const router = useRouter()

// ---------------------------------------------------------------- list
const rows = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)
const listError = ref('')
const search = ref('')
const searchApplied = ref('')
const kind = ref(null)
const status = ref(null)
let gen = 0

const KIND_ITEMS = [
  { title: 'كل الحسابات', value: null },
  { title: 'حسابات الإدارة', value: 'main' },
  { title: 'حسابات فرعية', value: 'sub' },
  { title: 'حسابات كاملة', value: 'full' },
]

const STATUS_ITEMS = [
  { title: 'الكل', value: null },
  { title: 'نشط', value: 'active' },
  { title: 'موقوف', value: 'suspended' },
]

const headers = [
  { title: 'المروّج', key: 'username', sortable: false },
  { title: 'النوع', key: 'type', sortable: false },
  { title: 'الطلبات', key: 'orders', sortable: false },
  { title: 'الرصيد', key: 'balance', sortable: false },
  { title: 'الأرباح / المسحوب', key: 'earned', sortable: false },
  { title: 'الحالة', key: 'status', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end' },
]

const applySearch = useDebounceFn(v => { searchApplied.value = String(v || '').trim() }, 400)

watch(search, v => applySearch(v))

async function load() {
  const g = ++gen

  loading.value = true
  listError.value = ''
  try {
    const res = paPaged(await paListUsers({ q: searchApplied.value, kind: kind.value, status: status.value, page: page.value, pageSize: pageSize.value }))
    if (g !== gen)
      return
    rows.value = res.items
    total.value = res.total
  }
  catch (err) {
    if (g === gen) {
      rows.value = []
      listError.value = paErrorMessage(err, 'تعذر تحميل المروّجين')
    }
  }
  finally {
    if (g === gen)
      loading.value = false
  }
}

watch([searchApplied, kind, status], () => { if (page.value !== 1) page.value = 1; else load() })
watch([page, pageSize], () => load())

// ---------------------------------------------------------------- create / edit
const formOpen = ref(false)
const formBusy = ref(false)
const formError = ref('')
const fieldErrors = ref({})
const editingId = ref(null)
const form = ref({ username: '', password: '', phone: '', isFull: true })

function fe(key) {
  return fieldErrors.value?.[key] ? [fieldErrors.value[key]] : []
}

function openCreate() {
  editingId.value = null
  form.value = { username: '', password: paNewPassword(), phone: '', isFull: true }
  formError.value = ''
  fieldErrors.value = {}
  formOpen.value = true
}

function openEdit(row) {
  editingId.value = row.id
  form.value = { username: row.username, password: '', phone: row.phone, isFull: row.isFull }
  formError.value = ''
  fieldErrors.value = {}
  formOpen.value = true
}

async function saveForm() {
  formBusy.value = true
  formError.value = ''
  fieldErrors.value = {}
  try {
    if (editingId.value) {
      await paUpdateUser(editingId.value, { username: form.value.username, phone: form.value.phone })
      toast.success('تم حفظ البيانات')
    }
    else {
      await paCreateUser(form.value)
      toast.success(`تم إنشاء الحساب: ${form.value.username.trim()} / ${form.value.password}`)
    }
    formOpen.value = false
    load()
    if (detail.value && editingId.value === detail.value.user.id)
      openDetail(detail.value.user)
  }
  catch (err) {
    fieldErrors.value = paFieldErrors(err)
    formError.value = paErrorMessage(err, 'تعذر الحفظ')
  }
  finally {
    formBusy.value = false
  }
}

// ---------------------------------------------------------------- password
const pwOpen = ref(false)
const pwBusy = ref(false)
const pwValue = ref('')
const pwError = ref('')
const pwUser = ref(null)

function openPassword(row) {
  pwUser.value = row
  pwValue.value = paNewPassword()
  pwError.value = ''
  pwOpen.value = true
}

async function savePassword() {
  pwBusy.value = true
  pwError.value = ''
  try {
    await paSetPassword(pwUser.value.id, pwValue.value)
    toast.success(`كلمة المرور الجديدة: ${pwValue.value} — تم تسجيل خروج المروّج من أجهزته`)
    pwOpen.value = false
  }
  catch (err) {
    pwError.value = paErrorMessage(err, 'تعذر تغيير كلمة المرور')
  }
  finally {
    pwBusy.value = false
  }
}

// ---------------------------------------------------------------- suspend
const suspendOpen = ref(false)
const suspendUser = ref(null)
const suspendReason = ref('')
const suspendBusy = ref(false)

function openSuspend(row) {
  suspendUser.value = row
  suspendReason.value = ''
  suspendOpen.value = true
}

async function saveSuspend() {
  suspendBusy.value = true
  try {
    const u = suspendUser.value

    await paSuspendUser(u.id, !u.isSuspended, suspendReason.value)
    toast.success(u.isSuspended ? 'تم تفعيل الحساب' : 'تم إيقاف الحساب')
    suspendOpen.value = false
    load()
    if (detail.value?.user.id === u.id)
      openDetail(u)
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
  finally {
    suspendBusy.value = false
  }
}

// ---------------------------------------------------------------- detail
const detailOpen = ref(false)
const detail = ref(null)
const detailLoading = ref(false)
const entries = ref([])
const entriesTotal = ref(0)
const entriesPage = ref(1)
const detailTab = ref('wallet')

async function openDetail(row) {
  detailOpen.value = true
  detailLoading.value = true
  try {
    detail.value = await paGetUser(row.id)
    entriesPage.value = 1
    await loadEntries()
    cutValue.value = Number(detail.value.user.parentCutPercent) || 0
  }
  catch (err) {
    toast.error(paErrorMessage(err, 'تعذر تحميل الحساب'))
    detailOpen.value = false
  }
  finally {
    detailLoading.value = false
  }
}

async function loadEntries() {
  if (!detail.value)
    return
  const res = paPaged(await paUserEntries(detail.value.user.id, { page: entriesPage.value, pageSize: 15 }))

  entries.value = res.items
  entriesTotal.value = res.total
}

watch(entriesPage, () => loadEntries().catch(() => {}))

const fullBusy = ref(false)

async function toggleFull() {
  const u = detail.value.user

  fullBusy.value = true
  try {
    detail.value = await paSetFull(u.id, !u.isFull)
    toast.success(u.isFull ? 'لم يعد حساباً كاملاً' : 'أصبح حساباً كاملاً: يأخذ عمولته كاملة ويمكنه إنشاء حسابات')
    load()
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
  finally {
    fullBusy.value = false
  }
}

const cutValue = ref(0)
const cutBusy = ref(false)

async function saveCut() {
  cutBusy.value = true
  try {
    detail.value = await paSetCut(detail.value.user.id, Number(cutValue.value))
    toast.success('تم حفظ النسبة (تطبق على الطلبات الجديدة)')
    load()
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
  finally {
    cutBusy.value = false
  }
}

const adjustOpen = ref(false)
const adjustAmount = ref(null)
const adjustNote = ref('')
const adjustBusy = ref(false)
const adjustError = ref('')

async function saveAdjust() {
  adjustBusy.value = true
  adjustError.value = ''
  try {
    await paAdjustWallet(detail.value.user.id, Number(adjustAmount.value), adjustNote.value)
    toast.success('تم تعديل الرصيد')
    adjustOpen.value = false
    adjustAmount.value = null
    adjustNote.value = ''
    await openDetail(detail.value.user)
    load()
  }
  catch (err) {
    adjustError.value = paErrorMessage(err)
  }
  finally {
    adjustBusy.value = false
  }
}

const ENTRY_COLORS = { Sale: 'success', Team: 'success', SaleReversal: 'error', TeamReversal: 'error', Payout: 'warning', PayoutReturned: 'info', Adjustment: 'secondary' }

function openOrders(u) {
  router.push({ path: '/promoters-app-orders', query: { userId: String(u.id), name: u.username } })
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="text-h4 mb-1 d-flex align-center gap-2">
          <VIcon
            icon="tabler-users-group"
            color="primary"
          />
          المروّجون أونلاين
        </h4>
        <p class="text-medium-emphasis mb-0">
          الحسابات التي تُنشأ هنا حسابات كاملة: تأخذ العمولة كاملة ويمكنها إنشاء حسابات فرعية تقتطع منها نسبة.
        </p>
      </div>
      <div class="d-flex gap-2">
        <VBtn
          variant="tonal"
          prepend-icon="tabler-refresh"
          :loading="loading"
          @click="load"
        >
          تحديث
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-user-plus"
          @click="openCreate"
        >
          إضافة مروّج
        </VBtn>
      </div>
    </div>

    <VCard class="mb-4">
      <VCardText>
        <VRow dense>
          <VCol
            cols="12"
            md="6"
          >
            <VTextField
              v-model="search"
              label="بحث (اسم المستخدم، الهاتف، المنشئ)"
              density="compact"
              clearable
              prepend-inner-icon="tabler-search"
            />
          </VCol>
          <VCol
            cols="6"
            md="3"
          >
            <VSelect
              v-model="kind"
              :items="KIND_ITEMS"
              label="النوع"
              density="compact"
            />
          </VCol>
          <VCol
            cols="6"
            md="3"
          >
            <VSelect
              v-model="status"
              :items="STATUS_ITEMS"
              label="الحالة"
              density="compact"
            />
          </VCol>
        </VRow>
      </VCardText>
    </VCard>

    <VAlert
      v-if="listError"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ listError }}
    </VAlert>

    <VCard>
      <VDataTableServer
        v-model:page="page"
        v-model:items-per-page="pageSize"
        :headers="headers"
        :items="rows"
        :items-length="total"
        :loading="loading"
        item-value="id"
        hover
        no-data-text="لا يوجد مروّجون بعد — أضف أول حساب"
        loading-text="جاري التحميل..."
        @click:row="(_, { item }) => openDetail(item)"
      >
        <template #item.username="{ item }">
          <div class="py-2">
            <div class="font-weight-medium">
              <code dir="auto">{{ item.username }}</code>
            </div>
            <div
              class="text-caption text-medium-emphasis"
              dir="ltr"
              style="text-align: end;"
            >
              {{ item.phone }}
            </div>
          </div>
        </template>
        <template #item.type="{ item }">
          <VChip
            v-if="item.isFull"
            color="primary"
            size="small"
            variant="tonal"
          >
            حساب كامل
          </VChip>
          <VChip
            v-else
            size="small"
            variant="tonal"
          >
            حساب فرعي
          </VChip>
          <div
            v-if="item.parentUsername"
            class="text-caption text-medium-emphasis mt-1"
          >
            أنشأه {{ item.parentUsername }}<span v-if="!item.isFull"> · يقتطع {{ Number(item.parentCutPercent) }}%</span>
          </div>
          <div
            v-if="item.teamCount"
            class="text-caption text-medium-emphasis"
          >
            فريقه: {{ item.teamCount }}
          </div>
        </template>
        <template #item.orders="{ item }">
          <div class="text-no-wrap">
            الكل: <strong>{{ item.ordersCount }}</strong> · مفتوحة: {{ item.openCount }}
          </div>
          <div class="text-caption text-success">
            مبيعات: {{ item.soldCount }}
          </div>
        </template>
        <template #item.balance="{ item }">
          <strong :class="item.balance < 0 ? 'text-error' : ''">{{ paMoney(item.balance) }}</strong>
        </template>
        <template #item.earned="{ item }">
          <div class="text-no-wrap">
            {{ paMoney(item.totalEarned) }}
          </div>
          <div class="text-caption text-medium-emphasis text-no-wrap">
            مسحوب: {{ paMoney(item.totalWithdrawn) }}
          </div>
        </template>
        <template #item.status="{ item }">
          <VChip
            :color="item.isSuspended ? 'error' : 'success'"
            size="small"
            variant="tonal"
          >
            {{ item.isSuspended ? 'موقوف' : 'نشط' }}
          </VChip>
        </template>
        <template #item.actions="{ item }">
          <div
            class="d-flex justify-end"
            @click.stop
          >
            <IconBtn @click="openEdit(item)">
              <VIcon icon="tabler-edit" />
              <VTooltip activator="parent">
                تعديل
              </VTooltip>
            </IconBtn>
            <IconBtn @click="openPassword(item)">
              <VIcon icon="tabler-key" />
              <VTooltip activator="parent">
                كلمة مرور جديدة
              </VTooltip>
            </IconBtn>
            <IconBtn
              :color="item.isSuspended ? 'success' : 'error'"
              @click="openSuspend(item)"
            >
              <VIcon :icon="item.isSuspended ? 'tabler-player-play' : 'tabler-ban'" />
              <VTooltip activator="parent">
                {{ item.isSuspended ? 'تفعيل' : 'إيقاف' }}
              </VTooltip>
            </IconBtn>
          </div>
        </template>
      </VDataTableServer>
    </VCard>

    <!-- create / edit -->
    <VDialog
      v-model="formOpen"
      max-width="480"
    >
      <VCard :title="editingId ? 'تعديل الحساب' : 'إضافة مروّج'">
        <VCardText>
          <VAlert
            v-if="formError"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ formError }}
          </VAlert>
          <VTextField
            v-model="form.username"
            label="اسم المستخدم"
            :error-messages="fe('username')"
            class="mb-3"
            autofocus
          />
          <VTextField
            v-if="!editingId"
            v-model="form.password"
            label="كلمة المرور"
            :error-messages="fe('password')"
            class="mb-3"
            append-inner-icon="tabler-refresh"
            @click:append-inner="form.password = paNewPassword()"
          />
          <VTextField
            v-model="form.phone"
            label="رقم الهاتف"
            dir="ltr"
            placeholder="07XXXXXXXXX"
            :error-messages="fe('phone')"
            class="mb-3"
          />
          <VSwitch
            v-if="!editingId"
            v-model="form.isFull"
            color="primary"
            label="حساب كامل (يمكنه إنشاء حسابات فرعية)"
            hide-details
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="formOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="formBusy"
            @click="saveForm"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- password -->
    <VDialog
      v-model="pwOpen"
      max-width="420"
    >
      <VCard :title="`كلمة مرور جديدة — ${pwUser?.username || ''}`">
        <VCardText>
          <VAlert
            v-if="pwError"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ pwError }}
          </VAlert>
          <VTextField
            v-model="pwValue"
            label="كلمة المرور"
            append-inner-icon="tabler-refresh"
            @click:append-inner="pwValue = paNewPassword()"
          />
          <div class="text-caption text-medium-emphasis mt-2">
            سيتم تسجيل خروج المروّج من كل أجهزته.
          </div>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="pwOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="pwBusy"
            @click="savePassword"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- suspend -->
    <VDialog
      v-model="suspendOpen"
      max-width="420"
    >
      <VCard :title="suspendUser?.isSuspended ? 'تفعيل الحساب' : 'إيقاف الحساب'">
        <VCardText>
          <p
            v-if="suspendUser?.isSuspended"
            class="mb-0"
          >
            سيتمكن {{ suspendUser?.username }} من تسجيل الدخول مجدداً.
          </p>
          <VTextField
            v-else
            v-model="suspendReason"
            label="السبب (اختياري)"
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="suspendOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            :color="suspendUser?.isSuspended ? 'success' : 'error'"
            :loading="suspendBusy"
            @click="saveSuspend"
          >
            تأكيد
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- detail -->
    <VDialog
      v-model="detailOpen"
      max-width="900"
      scrollable
    >
      <VCard
        v-if="detail"
        :loading="detailLoading"
      >
        <VCardTitle class="d-flex align-center gap-2 flex-wrap pt-4">
          <code dir="auto">{{ detail.user.username }}</code>
          <VChip
            v-if="detail.user.isFull"
            color="primary"
            size="small"
            variant="tonal"
          >
            حساب كامل
          </VChip>
          <VChip
            v-else
            size="small"
            variant="tonal"
          >
            حساب فرعي
          </VChip>
          <VChip
            v-if="detail.user.isSuspended"
            color="error"
            size="small"
            variant="tonal"
          >
            موقوف
          </VChip>
          <VSpacer />
          <IconBtn @click="detailOpen = false">
            <VIcon icon="tabler-x" />
          </IconBtn>
        </VCardTitle>
        <VCardText>
          <VRow
            dense
            class="mb-2"
          >
            <VCol
              cols="6"
              md="3"
            >
              <VCard
                variant="tonal"
                color="primary"
                class="pa-3"
              >
                <div class="text-caption">
                  الرصيد
                </div>
                <div class="text-h6">
                  {{ paMoney(detail.user.balance) }}
                </div>
              </VCard>
            </VCol>
            <VCol
              cols="6"
              md="3"
            >
              <VCard
                variant="tonal"
                color="success"
                class="pa-3"
              >
                <div class="text-caption">
                  مجموع الأرباح
                </div>
                <div class="text-h6">
                  {{ paMoney(detail.user.totalEarned) }}
                </div>
              </VCard>
            </VCol>
            <VCol
              cols="6"
              md="3"
            >
              <VCard
                variant="tonal"
                class="pa-3"
              >
                <div class="text-caption">
                  المسحوب
                </div>
                <div class="text-h6">
                  {{ paMoney(detail.user.totalWithdrawn) }}
                </div>
              </VCard>
            </VCol>
            <VCol
              cols="6"
              md="3"
            >
              <VCard
                variant="tonal"
                class="pa-3 cursor-pointer"
                @click="openOrders(detail.user)"
              >
                <div class="text-caption">
                  الطلبات / المبيعات
                </div>
                <div class="text-h6">
                  {{ detail.user.ordersCount }} / {{ detail.user.soldCount }}
                </div>
              </VCard>
            </VCol>
          </VRow>

          <div class="d-flex flex-wrap gap-4 text-body-2 mb-3">
            <span>الهاتف: <span dir="ltr">{{ detail.user.phone }}</span></span>
            <span>أُنشئ: {{ paDateTime(detail.user.createdAt) }}<span v-if="detail.user.createdByAdmin"> بواسطة {{ detail.user.createdByAdmin }}</span></span>
            <span>آخر دخول: {{ paDateTime(detail.user.lastLoginAt) }}</span>
          </div>

          <VCard
            variant="outlined"
            class="pa-3 mb-4"
          >
            <div class="d-flex flex-wrap align-center gap-3">
              <div class="flex-grow-1">
                <div class="font-weight-medium">
                  {{ detail.user.isFull ? 'حساب كامل' : 'حساب فرعي' }}
                </div>
                <div class="text-caption text-medium-emphasis">
                  <template v-if="detail.user.isFull">
                    يأخذ العمولة كاملة ويمكنه إنشاء حسابات فرعية.
                  </template>
                  <template v-else-if="detail.user.parentUsername">
                    أنشأه {{ detail.user.parentUsername }} ويقتطع {{ Number(detail.user.parentCutPercent) }}% من عمولته. لا يمكنه إنشاء حسابات.
                  </template>
                  <template v-else>
                    يأخذ العمولة كاملة لكنه لا يمكنه إنشاء حسابات.
                  </template>
                </div>
              </div>
              <VBtn
                :color="detail.user.isFull ? 'default' : 'primary'"
                variant="tonal"
                :loading="fullBusy"
                @click="toggleFull"
              >
                {{ detail.user.isFull ? 'إلغاء الحساب الكامل' : 'جعله حساباً كاملاً' }}
              </VBtn>
            </div>
            <div
              v-if="detail.user.parentUserId && !detail.user.isFull"
              class="d-flex align-center gap-2 mt-3"
            >
              <VTextField
                v-model="cutValue"
                type="number"
                min="0"
                max="100"
                label="نسبة المنشئ %"
                density="compact"
                style="max-width: 180px;"
                hide-details
              />
              <VBtn
                variant="tonal"
                :loading="cutBusy"
                @click="saveCut"
              >
                حفظ النسبة
              </VBtn>
              <span class="text-caption text-medium-emphasis">للطلبات الجديدة فقط</span>
            </div>
          </VCard>

          <VTabs
            v-model="detailTab"
            class="mb-3"
          >
            <VTab value="wallet">
              سجل المحفظة
            </VTab>
            <VTab value="team">
              فريقه ({{ detail.team.length }})
            </VTab>
          </VTabs>

          <VWindow v-model="detailTab">
            <VWindowItem value="wallet">
              <div class="d-flex justify-end mb-2">
                <VBtn
                  size="small"
                  variant="tonal"
                  prepend-icon="tabler-adjustments"
                  @click="adjustOpen = true"
                >
                  تعديل الرصيد
                </VBtn>
              </div>
              <VTable density="compact">
                <thead>
                  <tr>
                    <th>التاريخ</th>
                    <th>الحركة</th>
                    <th>التفاصيل</th>
                    <th class="text-end">
                      المبلغ
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="e in entries"
                    :key="e.id"
                  >
                    <td class="text-no-wrap">
                      {{ paDateTime(e.createdAt) }}
                    </td>
                    <td>
                      <VChip
                        :color="ENTRY_COLORS[e.type] || 'default'"
                        size="x-small"
                        variant="tonal"
                      >
                        {{ e.typeLabel }}
                      </VChip>
                    </td>
                    <td class="text-caption">
                      <span v-if="e.orderNumber">{{ e.orderNumber }} · {{ e.productName }}</span>
                      <span v-if="e.fromUsername"> · من {{ e.fromUsername }}</span>
                      <span v-if="e.note"> · {{ e.note }}</span>
                    </td>
                    <td
                      class="text-end text-no-wrap"
                      :class="e.amount < 0 ? 'text-error' : 'text-success'"
                      dir="ltr"
                    >
                      {{ e.amount > 0 ? '+' : '' }}{{ Number(e.amount).toLocaleString('en-US') }}
                    </td>
                  </tr>
                  <tr v-if="!entries.length">
                    <td
                      colspan="4"
                      class="text-center text-medium-emphasis py-4"
                    >
                      لا توجد حركات بعد
                    </td>
                  </tr>
                </tbody>
              </VTable>
              <VPagination
                v-if="entriesTotal > 15"
                v-model="entriesPage"
                :length="Math.ceil(entriesTotal / 15)"
                density="compact"
                class="mt-2"
              />
            </VWindowItem>
            <VWindowItem value="team">
              <VTable density="compact">
                <thead>
                  <tr>
                    <th>الحساب</th>
                    <th>النسبة</th>
                    <th>الطلبات / المبيعات</th>
                    <th>الرصيد</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="m in detail.team"
                    :key="m.id"
                    class="cursor-pointer"
                    @click="openDetail(m)"
                  >
                    <td>
                      <code dir="auto">{{ m.username }}</code> <span
                        class="text-caption text-medium-emphasis"
                        dir="ltr"
                      >{{ m.phone }}</span>
                    </td>
                    <td>{{ m.isFull ? 'كامل' : `${Number(m.parentCutPercent)}%` }}</td>
                    <td>{{ m.ordersCount }} / {{ m.soldCount }}</td>
                    <td>{{ paMoney(m.balance) }}</td>
                  </tr>
                  <tr v-if="!detail.team.length">
                    <td
                      colspan="4"
                      class="text-center text-medium-emphasis py-4"
                    >
                      لم ينشئ حسابات بعد
                    </td>
                  </tr>
                </tbody>
              </VTable>
            </VWindowItem>
          </VWindow>
        </VCardText>
      </VCard>
    </VDialog>

    <!-- adjust -->
    <VDialog
      v-model="adjustOpen"
      max-width="420"
    >
      <VCard title="تعديل الرصيد">
        <VCardText>
          <VAlert
            v-if="adjustError"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ adjustError }}
          </VAlert>
          <VTextField
            v-model="adjustAmount"
            type="number"
            label="المبلغ (سالب للخصم)"
            class="mb-3"
            dir="ltr"
          />
          <VTextField
            v-model="adjustNote"
            label="السبب (يظهر للمروّج)"
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="adjustOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="adjustBusy"
            @click="saveAdjust"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
