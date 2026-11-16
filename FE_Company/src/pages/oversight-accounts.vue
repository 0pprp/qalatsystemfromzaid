<script setup>
import { computed, onMounted, ref } from 'vue'
import {
  directorCities,
  oversightAccounts,
  oversightCommittees,
  oversightCreateAccount,
  oversightCreateCommittee,
  oversightDeleteAccount,
  oversightDeleteCommittee,
  oversightSetBranches,
} from '@/composables/directorApi'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'
import { ROLE_ARCHIVE_MANAGER, ROLE_COMMITTEE_ASSISTANT, ROLE_COMMITTEE_HEAD, ROLE_DIRECTOR, ROLE_SUPERVISOR } from '@/utils/oversightRoles'

definePage({
  meta: { layout: 'default' },
})

const toast = useToast()
const { isDirector, canManageOversight } = useUserRole()

const loading = ref(false)
const cities = ref([])
const committees = ref([])
const accounts = ref([])
const tab = ref('committees')
const search = ref('')

// The two member slots shown on every committee card.
const SLOTS = [
  { role: ROLE_COMMITTEE_HEAD, label: 'مسؤول اللجنة', add: 'إضافة مسؤول', icon: 'tabler-user-star' },
  { role: ROLE_COMMITTEE_ASSISTANT, label: 'المساعد', add: 'إضافة مساعد', icon: 'tabler-user' },
]
const MANAGEMENT_ROLES = [ROLE_DIRECTOR, ROLE_SUPERVISOR, ROLE_ARCHIVE_MANAGER]
const BRANCH_PREVIEW = 6

const errorMessage = e => e?.response?.data?.message || e?.message || ''

// ── data ─────────────────────────────────────────────────────────────

const cityItems = computed(() => (cities.value || [])
  .map(c => ({ title: c.name || c.Name || c.value, value: String(c.value || c.Value || '') }))
  .filter(c => c.value))

const cityName = value => cityItems.value.find(c => c.value === value)?.title || value

const membersOf = (committeeId, role) =>
  accounts.value.filter(a => a.committeeId === committeeId && a.role === role)

const committeeCards = computed(() => {
  const q = search.value.trim()
  return committees.value
    .map(c => {
      const branches = (c.cityValues || []).map(cityName)
      return {
        ...c,
        branches,
        slots: SLOTS.map(s => ({ ...s, members: membersOf(c.id, s.role) })),
        memberCount: accounts.value.filter(a => a.committeeId === c.id).length,
      }
    })
    .filter(c => !q || c.name.includes(q) || c.branches.some(b => b.includes(q)))
})

const managementAccounts = computed(() =>
  accounts.value.filter(a => MANAGEMENT_ROLES.includes(a.role)))

async function reload() {
  loading.value = true
  try {
    const [cityRows, committeeRows, accountRows] = await Promise.all([
      directorCities().catch(() => []),
      oversightCommittees(),
      oversightAccounts(),
    ])
    cities.value = Array.isArray(cityRows) ? cityRows : []
    committees.value = Array.isArray(committeeRows) ? committeeRows : []
    accounts.value = Array.isArray(accountRows) ? accountRows : []
  }
  catch (e) {
    toast.error(errorMessage(e) || 'تعذر تحميل اللجان والحسابات')
  }
  finally {
    loading.value = false
  }
}

// ── committee dialog (create / edit branches) ────────────────────────

const committeeDialog = ref({ open: false, id: null, name: '', cities: [], saving: false, error: '' })

function openCommitteeDialog(row = null) {
  committeeDialog.value = {
    open: true,
    id: row?.id ?? null,
    name: row?.name ?? '',
    cities: [...(row?.cityValues || [])],
    saving: false,
    error: '',
  }
}

const committeeDialogValid = computed(() =>
  (committeeDialog.value.id || committeeDialog.value.name.trim()) && committeeDialog.value.cities.length > 0)

function selectAllBranches() {
  committeeDialog.value.cities = cityItems.value.map(c => c.value)
}

async function saveCommittee() {
  const d = committeeDialog.value
  if (!committeeDialogValid.value || d.saving)
    return
  d.saving = true
  d.error = ''
  try {
    if (d.id) {
      await oversightSetBranches(d.id, d.cities)
      toast.success('تم حفظ فروع اللجنة')
    }
    else {
      await oversightCreateCommittee({ name: d.name.trim(), cityValues: d.cities })
      toast.success('تم إنشاء اللجنة — أضف الآن مسؤولها ومساعده')
    }
    d.open = false
    await reload()
  }
  catch (e) {
    d.error = errorMessage(e) || 'تعذر حفظ اللجنة'
  }
  finally {
    d.saving = false
  }
}

// ── account dialog ───────────────────────────────────────────────────

const accountDialog = ref({
  open: false, role: '', committeeId: null, committeeName: '', userName: '', password: '',
  showPassword: false, saving: false, error: '',
})

function openAccountDialog({ role, committee = null }) {
  accountDialog.value = {
    open: true,
    role,
    committeeId: committee?.id ?? null,
    committeeName: committee?.name ?? '',
    userName: '',
    password: '',
    showPassword: true,
    saving: false,
    error: '',
  }
}

const accountDialogIsCommittee = computed(() =>
  accountDialog.value.role === ROLE_COMMITTEE_HEAD || accountDialog.value.role === ROLE_COMMITTEE_ASSISTANT)

const accountDialogValid = computed(() =>
  accountDialog.value.userName.trim() && accountDialog.value.password.trim() && accountDialog.value.role)

async function saveAccount() {
  const d = accountDialog.value
  if (!accountDialogValid.value || d.saving)
    return
  d.saving = true
  d.error = ''
  try {
    await oversightCreateAccount({
      userName: d.userName.trim(),
      password: d.password,
      role: d.role,
      committeeId: accountDialogIsCommittee.value ? d.committeeId : null,
    })
    toast.success(`تم إنشاء حساب ${d.userName.trim()}`)
    d.open = false
    await reload()
  }
  catch (e) {
    d.error = errorMessage(e) || 'تعذر إنشاء الحساب'
  }
  finally {
    d.saving = false
  }
}

// ── confirm dialog (deletes) ─────────────────────────────────────────

const confirm = ref({ open: false, title: '', text: '', run: null, busy: false })

function askDeleteAccount(account) {
  confirm.value = {
    open: true,
    title: 'حذف الحساب',
    text: `سيتم حذف حساب «${account.userName}» ولن يتمكن من الدخول بعد الآن.`,
    run: async () => {
      await oversightDeleteAccount(account.id)
      toast.success('تم حذف الحساب')
    },
    busy: false,
  }
}

function askDeleteCommittee(committee) {
  confirm.value = {
    open: true,
    title: 'حذف اللجنة',
    text: `سيتم حذف لجنة «${committee.name}» وفروعها.`,
    run: async () => {
      await oversightDeleteCommittee(committee.id)
      toast.success('تم حذف اللجنة')
    },
    busy: false,
  }
}

async function runConfirm() {
  const c = confirm.value
  c.busy = true
  try {
    await c.run()
    c.open = false
    await reload()
  }
  catch (e) {
    toast.error(errorMessage(e) || 'تعذر الحذف')
  }
  finally {
    c.busy = false
  }
}

onMounted(() => {
  if (canManageOversight.value)
    reload()
})
</script>

<template>
  <div class="oversight-page">
    <VAlert
      v-if="!canManageOversight"
      type="info"
      variant="tonal"
    >
      هذه الصفحة للمدير ومسؤول المراقبين.
    </VAlert>

    <template v-else>
      <!-- Header -->
      <div class="d-flex flex-wrap align-center gap-3 mb-4">
        <div class="flex-grow-1">
          <h1 class="text-h5 mb-1">
            لجان المراقبة
          </h1>
          <div class="text-body-2 text-medium-emphasis">
            أنشئ لجنة واختر فروعها، ثم أضف مسؤول اللجنة ومساعده. كل لجنة ترى فروعها فقط.
          </div>
        </div>
        <VBtn
          variant="text"
          icon="tabler-refresh"
          :loading="loading"
          aria-label="تحديث"
          @click="reload"
        />
        <VBtn
          v-if="tab === 'committees'"
          color="primary"
          prepend-icon="tabler-plus"
          @click="openCommitteeDialog()"
        >
          لجنة جديدة
        </VBtn>
        <VBtn
          v-else
          color="primary"
          prepend-icon="tabler-plus"
          @click="openAccountDialog({ role: ROLE_SUPERVISOR })"
        >
          حساب إدارة جديد
        </VBtn>
      </div>

      <VTabs
        v-if="isDirector"
        v-model="tab"
        class="mb-4"
      >
        <VTab value="committees">
          اللجان
          <VChip
            size="x-small"
            class="ms-2"
          >
            {{ committees.length }}
          </VChip>
        </VTab>
        <VTab value="management">
          حسابات الإدارة
          <VChip
            size="x-small"
            class="ms-2"
          >
            {{ managementAccounts.length }}
          </VChip>
        </VTab>
      </VTabs>

      <!-- Committees -->
      <template v-if="tab === 'committees'">
        <VProgressLinear
          v-if="loading && !committees.length"
          indeterminate
          color="primary"
          class="mb-4"
        />

        <!-- Empty state: explain the two steps -->
        <VCard
          v-else-if="!committees.length"
          class="text-center pa-8"
        >
          <VIcon
            icon="tabler-users-group"
            size="48"
            class="text-medium-emphasis mb-3"
          />
          <div class="text-h6 mb-4">
            لا توجد لجان مراقبة بعد
          </div>
          <div class="steps mx-auto mb-6">
            <div class="step">
              <span class="step-num">1</span>
              أنشئ لجنة واختر الفروع التابعة لها
            </div>
            <div class="step">
              <span class="step-num">2</span>
              أضف مسؤول اللجنة ومساعده من بطاقة اللجنة
            </div>
          </div>
          <VBtn
            color="primary"
            prepend-icon="tabler-plus"
            @click="openCommitteeDialog()"
          >
            إنشاء أول لجنة
          </VBtn>
        </VCard>

        <template v-else>
          <VTextField
            v-if="committees.length > 3"
            v-model="search"
            prepend-inner-icon="tabler-search"
            placeholder="ابحث باسم اللجنة أو الفرع"
            density="compact"
            clearable
            class="mb-4 search-field"
          />

          <VRow>
            <VCol
              v-for="c in committeeCards"
              :key="c.id"
              cols="12"
              md="6"
              xl="4"
            >
              <VCard class="h-100 d-flex flex-column">
                <VCardItem>
                  <VCardTitle class="text-wrap">
                    {{ c.name }}
                  </VCardTitle>
                  <VCardSubtitle>
                    {{ c.branches.length }} {{ c.branches.length === 1 ? 'فرع' : 'فروع' }}
                  </VCardSubtitle>
                  <template #append>
                    <VMenu location="bottom end">
                      <template #activator="{ props }">
                        <VBtn
                          v-bind="props"
                          icon="tabler-dots-vertical"
                          variant="text"
                          size="small"
                          aria-label="خيارات اللجنة"
                        />
                      </template>
                      <VList density="compact">
                        <VListItem
                          prepend-icon="tabler-map-pin"
                          title="تعديل الفروع"
                          @click="openCommitteeDialog(c)"
                        />
                        <VListItem
                          prepend-icon="tabler-trash"
                          title="حذف اللجنة"
                          :subtitle="c.memberCount ? 'احذف حساباتها أولاً' : ''"
                          :disabled="c.memberCount > 0"
                          base-color="error"
                          @click="askDeleteCommittee(c)"
                        />
                      </VList>
                    </VMenu>
                  </template>
                </VCardItem>

                <VCardText class="pt-0">
                  <div class="d-flex flex-wrap gap-1">
                    <VChip
                      v-for="b in c.branches.slice(0, BRANCH_PREVIEW)"
                      :key="b"
                      size="small"
                      prepend-icon="tabler-map-pin"
                      variant="tonal"
                      color="primary"
                    >
                      {{ b }}
                    </VChip>
                    <VChip
                      v-if="c.branches.length > BRANCH_PREVIEW"
                      size="small"
                      variant="outlined"
                      @click="openCommitteeDialog(c)"
                    >
                      +{{ c.branches.length - BRANCH_PREVIEW }}
                    </VChip>
                  </div>
                </VCardText>

                <VDivider />

                <VCardText class="flex-grow-1 d-flex flex-column gap-4">
                  <div
                    v-for="slot in c.slots"
                    :key="slot.role"
                  >
                    <div class="d-flex align-center mb-2">
                      <VIcon
                        :icon="slot.icon"
                        size="18"
                        class="me-2 text-medium-emphasis"
                      />
                      <span class="text-body-2 font-weight-medium flex-grow-1">{{ slot.label }}</span>
                      <VBtn
                        v-if="slot.members.length"
                        size="x-small"
                        variant="text"
                        prepend-icon="tabler-plus"
                        @click="openAccountDialog({ role: slot.role, committee: c })"
                      >
                        إضافة
                      </VBtn>
                    </div>

                    <div
                      v-for="m in slot.members"
                      :key="m.id"
                      class="member d-flex align-center"
                    >
                      <VAvatar
                        size="28"
                        color="primary"
                        variant="tonal"
                        class="me-2"
                      >
                        {{ (m.userName || '?').charAt(0) }}
                      </VAvatar>
                      <span class="flex-grow-1 text-truncate">{{ m.userName }}</span>
                      <VBtn
                        icon="tabler-trash"
                        size="x-small"
                        variant="text"
                        color="error"
                        :aria-label="`حذف ${m.userName}`"
                        @click="askDeleteAccount(m)"
                      />
                    </div>

                    <VBtn
                      v-if="!slot.members.length"
                      block
                      variant="outlined"
                      class="add-slot"
                      prepend-icon="tabler-user-plus"
                      @click="openAccountDialog({ role: slot.role, committee: c })"
                    >
                      {{ slot.add }}
                    </VBtn>
                  </div>
                </VCardText>
              </VCard>
            </VCol>
          </VRow>

          <div
            v-if="!committeeCards.length"
            class="text-center text-medium-emphasis py-8"
          >
            لا توجد لجنة تطابق البحث.
          </div>
        </template>
      </template>

      <!-- Management accounts (director only) -->
      <VCard v-else>
        <VCardText
          v-if="!managementAccounts.length"
          class="text-center text-medium-emphasis py-8"
        >
          لا توجد حسابات إدارة مضافة من هنا بعد.
          <div class="text-caption mt-1">
            الحسابات الأصلية (مثل المدير الحالي ومسؤولي المراقبين) تبقى تعمل ولا تظهر هنا.
          </div>
        </VCardText>
        <VTable v-else>
          <thead>
            <tr>
              <th>اسم المستخدم</th>
              <th>النوع</th>
              <th class="text-end" />
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="a in managementAccounts"
              :key="a.id"
            >
              <td>{{ a.userName }}</td>
              <td>
                <VChip
                  size="small"
                  :color="a.role === ROLE_DIRECTOR ? 'error' : 'primary'"
                  variant="tonal"
                >
                  {{ a.role }}
                </VChip>
              </td>
              <td class="text-end">
                <VBtn
                  icon="tabler-trash"
                  size="small"
                  variant="text"
                  color="error"
                  :aria-label="`حذف ${a.userName}`"
                  @click="askDeleteAccount(a)"
                />
              </td>
            </tr>
          </tbody>
        </VTable>
      </VCard>
    </template>

    <!-- Committee dialog -->
    <VDialog
      v-model="committeeDialog.open"
      max-width="560"
    >
      <VCard>
        <VCardTitle class="pt-5 px-6">
          {{ committeeDialog.id ? `فروع لجنة «${committeeDialog.name}»` : 'لجنة مراقبة جديدة' }}
        </VCardTitle>
        <VCardText class="px-6 d-flex flex-column gap-4">
          <VTextField
            v-if="!committeeDialog.id"
            v-model="committeeDialog.name"
            label="اسم اللجنة"
            placeholder="مثال: لجنة الفرات الأوسط"
            autofocus
          />
          <div>
            <VAutocomplete
              v-model="committeeDialog.cities"
              :items="cityItems"
              label="الفروع التابعة للجنة"
              multiple
              chips
              closable-chips
              :hint="`${committeeDialog.cities.length} من ${cityItems.length} فرع`"
              persistent-hint
            />
            <div class="d-flex gap-2 mt-1">
              <VBtn
                size="small"
                variant="text"
                @click="selectAllBranches"
              >
                تحديد الكل
              </VBtn>
              <VBtn
                v-if="committeeDialog.cities.length"
                size="small"
                variant="text"
                @click="committeeDialog.cities = []"
              >
                مسح
              </VBtn>
            </div>
          </div>
          <VAlert
            v-if="committeeDialog.id"
            type="info"
            variant="tonal"
            density="compact"
          >
            يُطبَّق التغيير على حسابات اللجنة فوراً.
          </VAlert>
          <VAlert
            v-if="committeeDialog.error"
            type="error"
            variant="tonal"
            density="compact"
          >
            {{ committeeDialog.error }}
          </VAlert>
        </VCardText>
        <VCardActions class="px-6 pb-5">
          <VSpacer />
          <VBtn
            variant="text"
            @click="committeeDialog.open = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :disabled="!committeeDialogValid"
            :loading="committeeDialog.saving"
            @click="saveCommittee"
          >
            {{ committeeDialog.id ? 'حفظ الفروع' : 'إنشاء اللجنة' }}
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- Account dialog -->
    <VDialog
      v-model="accountDialog.open"
      max-width="480"
    >
      <VCard>
        <VCardTitle class="pt-5 px-6">
          حساب جديد
        </VCardTitle>
        <VCardText class="px-6">
          <form
            class="d-flex flex-column gap-4"
            @submit.prevent="saveAccount"
          >
            <div
              v-if="accountDialogIsCommittee"
              class="context-box"
            >
              <div class="text-caption text-medium-emphasis">
                الدور
              </div>
              <div class="font-weight-medium">
                {{ accountDialog.role }}
              </div>
              <div class="text-caption text-medium-emphasis mt-2">
                اللجنة
              </div>
              <div class="font-weight-medium">
                {{ accountDialog.committeeName }}
              </div>
            </div>
            <VBtnToggle
              v-else
              v-model="accountDialog.role"
              mandatory
              divided
              variant="outlined"
              color="primary"
              class="w-100"
            >
              <VBtn
                :value="ROLE_SUPERVISOR"
                class="flex-grow-1"
              >
                {{ ROLE_SUPERVISOR }}
              </VBtn>
              <VBtn
                :value="ROLE_DIRECTOR"
                class="flex-grow-1"
              >
                {{ ROLE_DIRECTOR }}
              </VBtn>
              <VBtn
                v-if="isDirector"
                :value="ROLE_ARCHIVE_MANAGER"
                class="flex-grow-1"
              >
                {{ ROLE_ARCHIVE_MANAGER }}
              </VBtn>
            </VBtnToggle>

            <VTextField
              v-model="accountDialog.userName"
              label="اسم المستخدم"
              hint="يمكن كتابته بالعربي"
              persistent-hint
              autofocus
              autocomplete="off"
            />
            <VTextField
              v-model="accountDialog.password"
              label="كلمة المرور"
              :type="accountDialog.showPassword ? 'text' : 'password'"
              :append-inner-icon="accountDialog.showPassword ? 'tabler-eye-off' : 'tabler-eye'"
              hint="أي كلمة مرور، بلا شروط"
              persistent-hint
              autocomplete="new-password"
              @click:append-inner="accountDialog.showPassword = !accountDialog.showPassword"
            />
            <VAlert
              v-if="accountDialog.error"
              type="error"
              variant="tonal"
              density="compact"
            >
              {{ accountDialog.error }}
            </VAlert>
            <!-- lets Enter submit -->
            <button
              type="submit"
              hidden
            />
          </form>
        </VCardText>
        <VCardActions class="px-6 pb-5">
          <VSpacer />
          <VBtn
            variant="text"
            @click="accountDialog.open = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :disabled="!accountDialogValid"
            :loading="accountDialog.saving"
            @click="saveAccount"
          >
            إنشاء الحساب
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- Confirm delete -->
    <VDialog
      v-model="confirm.open"
      max-width="420"
    >
      <VCard>
        <VCardTitle class="pt-5 px-6">
          {{ confirm.title }}
        </VCardTitle>
        <VCardText class="px-6">
          {{ confirm.text }}
        </VCardText>
        <VCardActions class="px-6 pb-5">
          <VSpacer />
          <VBtn
            variant="text"
            @click="confirm.open = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="error"
            variant="flat"
            :loading="confirm.busy"
            @click="runConfirm"
          >
            حذف
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.search-field {
  max-inline-size: 360px;
}

.steps {
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-inline-size: 380px;
  text-align: start;
}

.step {
  display: flex;
  align-items: center;
  gap: 10px;
}

.step-num {
  display: inline-flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: rgba(var(--v-theme-primary), 0.12);
  block-size: 26px;
  color: rgb(var(--v-theme-primary));
  font-weight: 600;
  inline-size: 26px;
}

.member {
  padding-block: 4px;
}

.add-slot {
  border-style: dashed;
  color: rgba(var(--v-theme-on-surface), 0.7);
}

.context-box {
  padding: 12px 14px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.04);
}
</style>
