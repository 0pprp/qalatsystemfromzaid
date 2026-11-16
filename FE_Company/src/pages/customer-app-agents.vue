<script setup>
import { onMounted, ref, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import {
  caAge,
  caCreateAgent,
  caDate,
  caDateTime,
  caErrorMessage,
  caFieldErrors,
  caGetAgent,
  caListAgents,
  caPaged,
  caSetAgentPassword,
  caSuspendAgent,
  caTry,
  caUpdateAgent,
} from '@/composables/customerAppApi'
import { useToast } from '@/composables/useToast'
import CaReasonDialog from '@/components/customer-app/CaReasonDialog.vue'
import CaTargetHistoryDialog from '@/components/customer-app/CaTargetHistoryDialog.vue'

// «سجل الهدف اليومي» of the agent picked from the row menu.
const targetAgent = ref(null)

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
const statusFilter = ref(null)
let loadGen = 0

const STATUS_ITEMS = [
  { title: 'الكل', value: null },
  { title: 'في الدوام الآن', value: 'working' },
  { title: 'نشط', value: 'active' },
  { title: 'موقوف', value: 'suspended' },
]

const headers = [
  { title: 'المسوّق', key: 'fullName', sortable: false },
  { title: 'اسم المستخدم', key: 'username', sortable: false },
  { title: 'الدوام', key: 'isWorking', sortable: false },
  { title: 'الطلبات', key: 'orders', sortable: false },
  { title: 'آخر دخول', key: 'lastLoginAt', sortable: false },
  { title: 'الحالة', key: 'status', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end' },
]

const applySearch = useDebounceFn(v => {
  searchApplied.value = String(v || '').trim()
}, 400)

watch(search, v => applySearch(v))

async function loadAgents() {
  const gen = ++loadGen

  loading.value = true
  listError.value = ''
  try {
    const res = caPaged(await caListAgents({
      q: searchApplied.value,
      status: statusFilter.value,
      page: page.value,
      pageSize: pageSize.value,
    }))

    if (gen !== loadGen)
      return
    rows.value = res.items
    total.value = res.total
  }
  catch (err) {
    if (gen === loadGen) {
      rows.value = []
      total.value = 0
      listError.value = caErrorMessage(err, 'تعذر تحميل المسوّقين')
    }
  }
  finally {
    if (gen === loadGen)
      loading.value = false
  }
}

watch([searchApplied, statusFilter], () => {
  if (page.value !== 1)
    page.value = 1
  else
    loadAgents()
})
watch([page, pageSize], () => loadAgents())

// ---------------------------------------------------------------- create / edit
const formOpen = ref(false)
const formBusy = ref(false)
const formError = ref('')
const fieldErrors = ref({})
const editingId = ref(null)
const showPassword = ref(false)

const emptyForm = () => ({
  username: '',
  password: '',
  firstName: '',
  fatherName: '',
  grandfatherName: '',
  surname: '',
  phone: '',
})

const form = ref(emptyForm())

function openCreate() {
  editingId.value = null
  form.value = emptyForm()
  formError.value = ''
  fieldErrors.value = {}
  showPassword.value = false
  formOpen.value = true
}

async function openEdit(row) {
  formError.value = ''
  fieldErrors.value = {}
  try {
    const d = await caGetAgent(row.id)

    editingId.value = row.id
    form.value = {
      username: d.agent.username || '',
      password: '',
      firstName: d.firstName,
      fatherName: d.fatherName,
      grandfatherName: d.grandfatherName,
      surname: d.surname || '',
      phone: d.agent.phone,
    }
    formOpen.value = true
  }
  catch (err) {
    toast.error(caErrorMessage(err, 'تعذر تحميل بيانات المسوّق'))
  }
}

async function saveForm() {
  formBusy.value = true
  formError.value = ''
  fieldErrors.value = {}

  const body = { ...form.value, surname: form.value.surname || null }
  try {
    if (editingId.value) {
      delete body.password
      await caUpdateAgent(editingId.value, body)
      toast.success('تم حفظ بيانات المسوّق')
    }
    else {
      const created = await caCreateAgent(body)

      toast.success(`تم إنشاء الحساب. اسم المستخدم: ${created?.agent?.username || body.username.trim()}`)
    }
    formOpen.value = false
    loadAgents()
  }
  catch (err) {
    fieldErrors.value = caFieldErrors(err) || {}
    formError.value = caErrorMessage(err, 'تعذر الحفظ')
  }
  finally {
    formBusy.value = false
  }
}

function fieldError(key) {
  return fieldErrors.value?.[key] ? [fieldErrors.value[key]] : []
}

/** 8 easy-to-read characters (no 0/O, 1/l/i). */
function newPassword() {
  const chars = 'abcdefghjkmnpqrstuvwxyz23456789'
  const bytes = crypto.getRandomValues(new Uint32Array(8))

  return Array.from(bytes, b => chars[b % chars.length]).join('')
}

function randomPassword() {
  form.value.password = newPassword()
  showPassword.value = true
}

// ---------------------------------------------------------------- password
const passwordOpen = ref(false)
const passwordBusy = ref(false)
const passwordError = ref('')
const passwordValue = ref('')
const passwordAgent = ref(null)

function openPassword(row) {
  passwordAgent.value = row
  passwordValue.value = ''
  passwordError.value = ''
  passwordOpen.value = true
}

function generatePassword() {
  passwordValue.value = newPassword()
}

async function savePassword() {
  passwordBusy.value = true
  passwordError.value = ''
  try {
    await caSetAgentPassword(passwordAgent.value.id, passwordValue.value)
    toast.success('تم تعيين كلمة المرور الجديدة وتسجيل خروج المسوّق من أجهزته')
    passwordOpen.value = false
  }
  catch (err) {
    passwordError.value = caErrorMessage(err, 'تعذر تغيير كلمة المرور')
  }
  finally {
    passwordBusy.value = false
  }
}

// ---------------------------------------------------------------- suspend
const suspendOpen = ref(false)
const actionAgent = ref(null)

function startSuspend(row) {
  actionAgent.value = row
  suspendOpen.value = true
}

function submitSuspend(reason) {
  const a = actionAgent.value

  return caTry(() => caSuspendAgent(a.id, !a.isSuspended, reason), 'تعذر تنفيذ العملية')
}

function afterSuspend() {
  toast.success(actionAgent.value?.isSuspended ? 'تم تفعيل الحساب' : 'تم إيقاف الحساب')
  loadAgents()
}

function openAttendance(row) {
  router.push({ path: '/customer-app-attendance', query: { userId: String(row.id) } })
}

onMounted(() => {
  loadAgents()
})
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="text-h4 mb-1 d-flex align-center gap-2">
          <VIcon
            icon="tabler-user-check"
            color="primary"
          />
          المسوّقون
        </h4>
        <p class="text-medium-emphasis mb-0">
          حسابات المسوّقين الميدانيين التابعين للمقر الرئيسي (غير مرتبطين بفرع). التطبيق لا يسمح بإنشاء الحسابات، تُنشأ هنا باسم مستخدم وكلمة مرور.
        </p>
      </div>
      <div class="d-flex gap-2">
        <VBtn
          variant="tonal"
          prepend-icon="tabler-refresh"
          :loading="loading"
          @click="loadAgents"
        >
          تحديث
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-user-plus"
          @click="openCreate"
        >
          إضافة مسوّق
        </VBtn>
      </div>
    </div>

    <VCard class="mb-4">
      <VCardText>
        <VRow dense>
          <VCol
            cols="12"
            md="9"
          >
            <VTextField
              v-model="search"
              label="بحث (الاسم، الهاتف، اسم المستخدم)"
              density="compact"
              clearable
              prepend-inner-icon="tabler-search"
            />
          </VCol>
          <VCol
            cols="12"
            md="3"
          >
            <VSelect
              v-model="statusFilter"
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
        density="comfortable"
        hover
        no-data-text="لا يوجد مسوّقون بعد — أضف أول حساب"
        loading-text="جاري التحميل..."
      >
        <template #item.fullName="{ item }">
          <div class="d-flex align-center gap-3 py-2">
            <VAvatar
              color="primary"
              variant="tonal"
              size="36"
            >
              {{ String(item.fullName || '?').charAt(0) }}
            </VAvatar>
            <div>
              <div class="font-weight-medium">
                {{ item.fullName }}
              </div>
              <div
                class="text-caption text-medium-emphasis"
                dir="ltr"
                style="text-align: end;"
              >
                {{ item.phone }}
              </div>
            </div>
          </div>
        </template>
        <template #item.username="{ item }">
          <code dir="auto">{{ item.username }}</code>
        </template>
        <template #item.isWorking="{ item }">
          <VChip
            :color="item.isWorking ? 'success' : 'default'"
            size="small"
            variant="tonal"
            :prepend-icon="item.isWorking ? 'tabler-map-pin' : 'tabler-clock-pause'"
            class="cursor-pointer"
            @click="openAttendance(item)"
          >
            {{ item.isWorking ? 'في الدوام' : 'خارج الدوام' }}
          </VChip>
          <div
            v-if="item.lastSeenAt"
            class="text-caption text-medium-emphasis mt-1"
          >
            آخر موقع {{ caAge(item.lastSeenAt) }}
          </div>
        </template>
        <template #item.orders="{ item }">
          <div class="text-no-wrap">
            الكل: <strong>{{ item.ordersCount }}</strong> · مفتوحة: {{ item.openOrdersCount }}
          </div>
          <div class="text-caption text-success text-no-wrap">
            مبيعات: {{ item.soldCount }} · مكتملة التسديد: {{ item.finishedCount }}
          </div>
        </template>
        <template #item.lastLoginAt="{ item }">
          <span class="text-no-wrap">{{ item.lastLoginAt ? caDateTime(item.lastLoginAt) : 'لم يدخل بعد' }}</span>
        </template>
        <template #item.status="{ item }">
          <VChip
            v-if="item.isSuspended"
            size="x-small"
            color="error"
            :title="item.suspendReason || ''"
          >
            موقوف
          </VChip>
          <VChip
            v-else
            size="x-small"
            color="success"
            variant="tonal"
          >
            نشط
          </VChip>
        </template>
        <template #item.actions="{ item }">
          <VMenu location="bottom end">
            <template #activator="{ props: menuProps }">
              <IconBtn v-bind="menuProps">
                <VIcon icon="tabler-dots-vertical" />
              </IconBtn>
            </template>
            <VList density="compact">
              <VListItem
                prepend-icon="tabler-edit"
                title="تعديل البيانات"
                @click="openEdit(item)"
              />
              <VListItem
                prepend-icon="tabler-key"
                title="تعيين كلمة مرور جديدة"
                @click="openPassword(item)"
              />
              <VListItem
                prepend-icon="tabler-map-2"
                title="الدوام والمسار"
                @click="openAttendance(item)"
              />
              <VListItem
                prepend-icon="tabler-target-arrow"
                title="سجل الهدف اليومي"
                @click="targetAgent = { id: item.id, fullName: item.fullName }"
              />
              <VDivider />
              <VListItem
                :prepend-icon="item.isSuspended ? 'tabler-player-play' : 'tabler-ban'"
                :title="item.isSuspended ? 'تفعيل الحساب' : 'إيقاف الحساب'"
                :base-color="item.isSuspended ? 'success' : 'error'"
                @click="startSuspend(item)"
              />
            </VList>
          </VMenu>
        </template>
      </VDataTableServer>
    </VCard>

    <CaTargetHistoryDialog
      :agent="targetAgent"
      @close="targetAgent = null"
      @changed="loadAgents"
    />

    <!-- Create / edit -->
    <VDialog
      v-model="formOpen"
      max-width="620"
      persistent
    >
      <VCard :title="editingId ? 'تعديل بيانات المسوّق' : 'إضافة مسوّق جديد'">
        <VCardText>
          <VAlert
            v-if="formError"
            type="error"
            variant="tonal"
            class="mb-4"
          >
            {{ formError }}
          </VAlert>
          <VRow dense>
            <VCol
              cols="12"
              md="6"
            >
              <VTextField
                v-model="form.username"
                label="اسم المستخدم"
                hint="عربي أو إنكليزي، بدون مسافات (2–30)"
                persistent-hint
                dir="auto"
                autocomplete="off"
                :error-messages="fieldError('username')"
              />
            </VCol>
            <VCol
              v-if="!editingId"
              cols="12"
              md="6"
            >
              <VTextField
                v-model="form.password"
                label="كلمة المرور"
                :type="showPassword ? 'text' : 'password'"
                dir="ltr"
                autocomplete="new-password"
                hint="أي كلمة مرور — سلّمها للمسوّق"
                persistent-hint
                :append-inner-icon="showPassword ? 'tabler-eye-off' : 'tabler-eye'"
                :error-messages="fieldError('password')"
                @click:append-inner="showPassword = !showPassword"
              >
                <template #append>
                  <IconBtn
                    title="توليد كلمة مرور"
                    @click="randomPassword"
                  >
                    <VIcon icon="tabler-wand" />
                  </IconBtn>
                </template>
              </VTextField>
            </VCol>
            <VCol
              cols="12"
              md="4"
            >
              <VTextField
                v-model="form.firstName"
                label="الاسم"
                :error-messages="fieldError('firstName')"
              />
            </VCol>
            <VCol
              cols="12"
              md="4"
            >
              <VTextField
                v-model="form.fatherName"
                label="اسم الأب"
                :error-messages="fieldError('fatherName')"
              />
            </VCol>
            <VCol
              cols="12"
              md="4"
            >
              <VTextField
                v-model="form.grandfatherName"
                label="اسم الجد"
                :error-messages="fieldError('grandfatherName')"
              />
            </VCol>
            <VCol
              cols="12"
              md="4"
            >
              <VTextField
                v-model="form.surname"
                label="اللقب (اختياري)"
                :error-messages="fieldError('surname')"
              />
            </VCol>
            <VCol
              cols="12"
              md="4"
            >
              <VTextField
                v-model="form.phone"
                label="رقم الهاتف"
                dir="ltr"
                placeholder="07XXXXXXXXX"
                :error-messages="fieldError('phone')"
              />
            </VCol>
          </VRow>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            :disabled="formBusy"
            @click="formOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="formBusy"
            @click="saveForm"
          >
            {{ editingId ? 'حفظ' : 'إنشاء الحساب' }}
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- Password -->
    <VDialog
      v-model="passwordOpen"
      max-width="460"
    >
      <VCard title="تعيين كلمة مرور جديدة">
        <VCardText>
          <p class="mb-4">
            {{ passwordAgent?.fullName }} — <code dir="auto">{{ passwordAgent?.username }}</code>
          </p>
          <VAlert
            v-if="passwordError"
            type="error"
            variant="tonal"
            class="mb-3"
          >
            {{ passwordError }}
          </VAlert>
          <VTextField
            v-model="passwordValue"
            label="كلمة المرور الجديدة"
            dir="ltr"
            autocomplete="new-password"
            hint="سيُسجَّل خروج المسوّق من كل أجهزته"
            persistent-hint
          >
            <template #append>
              <IconBtn
                title="توليد كلمة مرور"
                @click="generatePassword"
              >
                <VIcon icon="tabler-wand" />
              </IconBtn>
            </template>
          </VTextField>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="passwordOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="passwordBusy"
            @click="savePassword"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <CaReasonDialog
      v-model="suspendOpen"
      :title="actionAgent?.isSuspended ? 'تفعيل حساب المسوّق' : 'إيقاف حساب المسوّق'"
      :icon="actionAgent?.isSuspended ? 'tabler-player-play' : 'tabler-ban'"
      :description="actionAgent?.isSuspended ? 'سيتمكن المسوّق من تسجيل الدخول مجدداً.' : 'سيُسجَّل خروج المسوّق ولن يتمكن من الدخول حتى إعادة التفعيل.'"
      :alert-type="actionAgent?.isSuspended ? 'info' : 'warning'"
      :required="!actionAgent?.isSuspended"
      :confirm-text="actionAgent?.isSuspended ? 'تفعيل' : 'إيقاف'"
      :confirm-color="actionAgent?.isSuspended ? 'success' : 'error'"
      :submit="submitSuspend"
      @done="afterSuspend"
    />
  </div>
</template>
