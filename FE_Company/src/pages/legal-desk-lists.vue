<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import LegalAccountSearchDialog from '@/components/LegalAccountSearchDialog.vue'
import {
  accountKey,
  legalError,
  legalDay,
  legalCities,
  legalCreatePerson,
  legalCustomers,
  legalLists,
  legalSend,
  legalSimilar,
  legalMoney,
  readTray,
  legalStatus,
  writeTray,
} from '@/composables/legalDeskApi'

const BRANCH_KEY = 'legal-desk-branch'

const toast = useToast()
const router = useRouter()

const cities = ref([])
const cityValue = ref(localStorage.getItem(BRANCH_KEY) || '')
const lists = ref([])
const listsLoading = ref(false)
const activeList = ref(null)
const customers = ref([])
const customersLoading = ref(false)
const filter = ref('')
const onlyUngrouped = ref(true)

// الحسابات المختارة للتجميع — تبقى عند التنقل بين القوائم والفروع
const tray = ref(readTray())

watch(tray, rows => writeTray(rows), { deep: true })

const trayKeys = computed(() => tray.value.map(accountKey))
const inTray = row => trayKeys.value.includes(accountKey(row))
const trayRemaining = computed(() => tray.value.reduce((sum, r) => sum + Number(r.remaining || 0), 0))

const legalTotal = computed(() => lists.value.reduce((sum, l) => sum + Number(l.legalCustomers || 0), 0))

const shownCustomers = computed(() => {
  const text = filter.value.trim()

  return customers.value.filter(c => {
    if (onlyUngrouped.value && c.personId)
      return false
    if (!text)
      return true

    return String(c.customerName || '').includes(text) || String(c.phone || '').includes(text)
  })
})

const allShownPicked = computed(() => {
  const free = shownCustomers.value.filter(c => !c.personId)

  return free.length > 0 && free.every(inTray)
})

async function loadCities() {
  try {
    cities.value = await legalCities()
    if (!cities.value.some(c => c.value === cityValue.value))
      cityValue.value = cities.value[0]?.value || ''
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل الفروع'))
  }
}

async function loadLists() {
  if (!cityValue.value)
    return
  localStorage.setItem(BRANCH_KEY, cityValue.value)
  listsLoading.value = true
  activeList.value = null
  customers.value = []
  try {
    lists.value = await legalLists(cityValue.value)
  }
  catch (e) {
    lists.value = []
    toast.error(legalError(e, 'تعذر تحميل القوائم من الفرع'))
  }
  finally {
    listsLoading.value = false
  }
}

/** list = one of the branch lists, or null for every legal customer of the branch. */
async function openList(list) {
  activeList.value = list || { delegateId: 0, delegateName: 'كل زبائن القانونية في الفرع' }
  customersLoading.value = true
  customers.value = []
  try {
    customers.value = await legalCustomers(cityValue.value, { delegateId: list?.delegateId || undefined })
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل الزبائن'))
  }
  finally {
    customersLoading.value = false
  }
}

function toggle(row) {
  if (row.personId)
    return
  const key = accountKey(row)
  const i = tray.value.findIndex(r => accountKey(r) === key)
  if (i >= 0)
    tray.value.splice(i, 1)
  else
    tray.value.push(row)
}

function toggleAllShown() {
  const free = shownCustomers.value.filter(c => !c.personId)
  if (allShownPicked.value) {
    const keys = new Set(free.map(accountKey))

    tray.value = tray.value.filter(r => !keys.has(accountKey(r)))
  }
  else {
    for (const row of free) {
      if (!inTray(row))
        tray.value.push(row)
    }
  }
}

function addToTray(rows) {
  for (const row of rows) {
    if (!row.personId && !inTray(row))
      tray.value.push(row)
  }
}

// —— حسابات مشابهة ——
const similarDialog = ref(false)
const similarFor = ref(null)
const similarRows = ref([])
const similarLoading = ref(false)

async function openSimilar(row) {
  similarFor.value = row
  similarRows.value = []
  similarDialog.value = true
  similarLoading.value = true
  try {
    similarRows.value = await legalSimilar(row.cityValue, row.customerId)
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر جلب الحسابات المشابهة'))
  }
  finally {
    similarLoading.value = false
  }
}

function takeSimilar() {
  addToTray([similarFor.value, ...similarRows.value])
  similarDialog.value = false
}

// —— البحث في كل الفروع ——
const searchDialog = ref(false)
const searchQuery = ref('')

function openSearch(text) {
  searchQuery.value = text || ''
  searchDialog.value = true
}

// —— التجميع ——
const groupDialog = ref(false)
const groupForm = ref({ fullName: '', phone: '', address: '', notes: '', send: true })
const saving = ref(false)
/** One debtor per account: for the common case where each name in a list is its own person. */
const bulkDialog = ref(false)
const bulkProgress = ref({ done: 0, total: 0, failed: [] })

function openGroup() {
  const first = tray.value[0] || {}

  groupForm.value = {
    fullName: String(first.customerName || '').trim(),
    phone: first.phone || '',
    address: first.address || '',
    notes: '',
    send: true,
  }
  groupDialog.value = true
}

async function saveGroup() {
  if (!groupForm.value.fullName.trim()) {
    toast.error('اكتب اسم المدين')

    return
  }
  saving.value = true
  try {
    const { id } = await legalCreatePerson({
      fullName: groupForm.value.fullName,
      phone: groupForm.value.phone,
      address: groupForm.value.address,
      notes: groupForm.value.notes,
      accounts: tray.value.map(r => ({ cityValue: r.cityValue, customerId: r.customerId })),
    })

    if (groupForm.value.send)
      await legalSend(id)
    toast.success(groupForm.value.send ? 'تم التجميع والإرسال إلى القانونية' : 'تم تجميع الحسابات في مدين واحد')
    tray.value = []
    groupDialog.value = false
    router.push({ path: '/legal-desk-debtor', query: { id } })
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر إنشاء ملف المدين'))
  }
  finally {
    saving.value = false
  }
}

async function runBulk(send) {
  const rows = tray.value.slice()

  bulkProgress.value = { done: 0, total: rows.length, failed: [] }
  saving.value = true
  for (const row of rows) {
    try {
      const { id } = await legalCreatePerson({
        fullName: String(row.customerName || '').trim() || `زبون ${row.customerId}`,
        phone: row.phone,
        address: row.address,
        accounts: [{ cityValue: row.cityValue, customerId: row.customerId }],
      })

      if (send)
        await legalSend(id)
      tray.value = tray.value.filter(r => accountKey(r) !== accountKey(row))
    }
    catch (e) {
      bulkProgress.value.failed.push(`${row.customerName}: ${legalError(e, 'فشل')}`)
    }
    bulkProgress.value.done++
  }
  saving.value = false
  if (!bulkProgress.value.failed.length) {
    bulkDialog.value = false
    toast.success(`تم إنشاء ${rows.length} ملف مدين`)
  }
  if (activeList.value)
    openList(activeList.value.delegateId ? activeList.value : null)
}

watch(cityValue, loadLists)

onMounted(async () => {
  await loadCities()

  // The branch watcher already loads when loadCities had to pick a branch.
  if (!lists.value.length && !listsLoading.value)
    await loadLists()
})
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold mb-0">
          قوائم القانونية
        </h1>
      </div>
      <div class="d-flex align-center gap-2">
        <VSelect
          v-model="cityValue"
          :items="cities"
          item-title="name"
          item-value="value"
          label="الفرع"
          density="compact"
          hide-details
          style="min-inline-size: 200px;"
        />
        <VBtn
          variant="tonal"
          prepend-icon="tabler-search"
          @click="openSearch('')"
        >
          بحث في كل الفروع
        </VBtn>
      </div>
    </div>

    <VRow>
      <!-- القوائم -->
      <VCol
        cols="12"
        md="4"
        lg="3"
      >
        <VCard>
          <VCardText class="pb-2">
            <div class="text-subtitle-1 font-weight-bold">
              زبائن القانونية حسب القائمة
            </div>
            <div class="text-caption text-medium-emphasis">
              كل زبون مؤشَّر قانونية يظهر تحت قائمته الأصلية
            </div>
          </VCardText>
          <VProgressLinear
            v-if="listsLoading"
            indeterminate
          />
          <VList
            density="compact"
            class="py-0"
          >
            <VListItem
              :active="activeList?.delegateId === 0"
              @click="openList(null)"
            >
              <template #prepend>
                <VIcon
                  icon="tabler-users"
                  size="20"
                />
              </template>
              <VListItemTitle>كل زبائن القانونية في الفرع</VListItemTitle>
              <template #append>
                <VChip
                  size="small"
                  variant="tonal"
                >
                  {{ legalTotal }}
                </VChip>
              </template>
            </VListItem>
            <VDivider />
            <VListItem
              v-for="list in lists"
              :key="list.delegateId"
              :active="activeList?.delegateId === list.delegateId && list.delegateId !== 0"
              @click="openList(list)"
            >
              <template #prepend>
                <VIcon
                  icon="tabler-list"
                  size="20"
                />
              </template>
              <VListItemTitle>{{ list.delegateName }}</VListItemTitle>
              <template #append>
                <VChip
                  size="small"
                  variant="tonal"
                  color="warning"
                >
                  {{ list.legalCustomers }}
                </VChip>
              </template>
            </VListItem>
            <VListItem v-if="!listsLoading && !lists.length">
              <VListItemTitle class="text-medium-emphasis">
                لا يوجد زبائن قانونية في هذا الفرع
              </VListItemTitle>
            </VListItem>
          </VList>
        </VCard>
      </VCol>

      <!-- الزبائن -->
      <VCol
        cols="12"
        md="8"
        lg="9"
      >
        <VCard v-if="activeList">
          <VCardText class="d-flex flex-wrap align-center gap-3">
            <div class="text-subtitle-1 font-weight-bold">
              {{ activeList.delegateName }}
            </div>
            <VChip
              size="small"
              variant="tonal"
            >
              {{ shownCustomers.length }} من {{ customers.length }}
            </VChip>
            <VSpacer />
            <VSwitch
              v-model="onlyUngrouped"
              label="غير المجمّعين فقط"
              density="compact"
              hide-details
            />
            <VTextField
              v-model="filter"
              placeholder="تصفية بالاسم أو الهاتف"
              prepend-inner-icon="tabler-filter"
              density="compact"
              hide-details
              clearable
              style="max-inline-size: 260px;"
            />
          </VCardText>
          <VProgressLinear
            v-if="customersLoading"
            indeterminate
          />
          <VTable
            density="compact"
            fixed-header
            height="560"
          >
            <thead>
              <tr>
                <th style="inline-size: 44px;">
                  <VCheckboxBtn
                    :model-value="allShownPicked"
                    density="compact"
                    @click="toggleAllShown"
                  />
                </th>
                <th>الاسم</th>
                <th>الهاتف</th>
                <th v-if="!activeList.delegateId">
                  القائمة
                </th>
                <th>المواد</th>
                <th>آخر تسديد</th>
                <th class="text-end">
                  المبلغ
                </th>
                <th class="text-end">
                  الباقي
                </th>
                <th>الملف</th>
                <th />
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="row in shownCustomers"
                :key="row.customerId"
                :class="{ 'bg-primary-lighten': inTray(row) }"
              >
                <td>
                  <VCheckboxBtn
                    :model-value="inTray(row)"
                    :disabled="Boolean(row.personId)"
                    density="compact"
                    @click="toggle(row)"
                  />
                </td>
                <td class="font-weight-medium">
                  {{ row.customerName }}
                  <VChip
                    v-if="row.archiveCode"
                    size="x-small"
                    color="primary"
                    variant="tonal"
                    class="ms-1"
                    style="direction: ltr; unicode-bidi: isolate; font-family: Consolas, monospace; font-weight: 700;"
                  >
                    {{ row.archiveCode }}
                  </VChip>
                </td>
                <td dir="ltr">
                  {{ row.phone || '—' }}
                </td>
                <td v-if="!activeList.delegateId">
                  {{ row.listName || '—' }}
                </td>
                <td class="text-truncate" style="max-inline-size: 170px;">
                  {{ row.items || '—' }}
                </td>
                <td>{{ legalDay(row.lastPaymentDate) }}</td>
                <td class="text-end">
                  {{ legalMoney(row.total) }}
                </td>
                <td class="text-end font-weight-medium">
                  {{ legalMoney(row.remaining) }}
                </td>
                <td>
                  <VChip
                    v-if="row.personId"
                    size="x-small"
                    :color="legalStatus(row.personStage || 'Recent').color"
                    variant="tonal"
                    :to="{ path: '/legal-desk-debtor', query: { id: row.personId } }"
                  >
                    {{ row.personStage ? legalStatus(row.personStage).label : 'مجمّع' }}
                  </VChip>
                  <span
                    v-else
                    class="text-disabled"
                  >—</span>
                </td>
                <td class="text-no-wrap">
                  <VTooltip location="top">
                    <template #activator="{ props }">
                      <VBtn
                        v-bind="props"
                        icon="tabler-users-group"
                        size="x-small"
                        variant="text"
                        :disabled="Boolean(row.personId)"
                        @click="openSimilar(row)"
                      />
                    </template>
                    حسابات أخرى لنفس الشخص في هذا الفرع
                  </VTooltip>
                  <VTooltip location="top">
                    <template #activator="{ props }">
                      <VBtn
                        v-bind="props"
                        icon="tabler-world-search"
                        size="x-small"
                        variant="text"
                        @click="openSearch(String(row.customerName || '').trim())"
                      />
                    </template>
                    البحث عنه في كل الفروع
                  </VTooltip>
                </td>
              </tr>
              <tr v-if="!customersLoading && !shownCustomers.length">
                <td
                  colspan="10"
                  class="text-center text-medium-emphasis py-6"
                >
                  لا يوجد زبائن
                </td>
              </tr>
            </tbody>
          </VTable>
        </VCard>
        <VCard v-else>
          <VCardText class="text-center text-medium-emphasis py-12">
            اختر قائمة لعرض زبائنها
          </VCardText>
        </VCard>
      </VCol>
    </VRow>

    <!-- شريط الحسابات المختارة -->
    <div
      v-if="tray.length"
      class="legal-tray"
    >
      <div class="d-flex flex-wrap align-center gap-3">
        <div>
          <div class="font-weight-bold">
            {{ tray.length }} حساب مختار
          </div>
          <div class="text-caption">
            الباقي {{ legalMoney(trayRemaining) }}
          </div>
        </div>
        <div class="legal-tray__chips">
          <VChip
            v-for="row in tray"
            :key="accountKey(row)"
            size="small"
            closable
            @click:close="toggle(row)"
          >
            {{ row.customerName }} · {{ row.cityName }}
          </VChip>
        </div>
        <VSpacer />
        <VBtn
          variant="text"
          @click="tray = []"
        >
          إفراغ
        </VBtn>
        <VBtn
          v-if="tray.length > 1"
          variant="tonal"
          prepend-icon="tabler-users"
          @click="bulkDialog = true"
        >
          ملف مستقل لكل حساب
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-user-plus"
          @click="openGroup"
        >
          {{ tray.length > 1 ? 'تجميع في مدين واحد' : 'إنشاء ملف المدين' }}
        </VBtn>
      </div>
    </div>

    <!-- حوار التجميع -->
    <VDialog
      v-model="groupDialog"
      max-width="720"
    >
      <VCard>
        <VCardTitle>ملف مدين موحد</VCardTitle>
        <VCardText>
          <VRow>
            <VCol
              cols="12"
              sm="7"
            >
              <VTextField
                v-model="groupForm.fullName"
                label="اسم المدين"
                density="compact"
                hide-details
              />
            </VCol>
            <VCol
              cols="12"
              sm="5"
            >
              <VTextField
                v-model="groupForm.phone"
                label="الهاتف"
                density="compact"
                dir="ltr"
                hide-details
              />
            </VCol>
            <VCol cols="12">
              <VTextField
                v-model="groupForm.address"
                label="العنوان"
                density="compact"
                hide-details
              />
            </VCol>
            <VCol cols="12">
              <VTextarea
                v-model="groupForm.notes"
                label="ملاحظات"
                rows="2"
                density="compact"
                hide-details
              />
            </VCol>
          </VRow>
          <div class="text-body-2 font-weight-medium mt-4 mb-1">
            الحسابات ({{ tray.length }})
          </div>
          <VTable density="compact">
            <tbody>
              <tr
                v-for="row in tray"
                :key="accountKey(row)"
              >
                <td>{{ row.customerName }}</td>
                <td>{{ row.cityName }} · {{ row.listName }}</td>
                <td class="text-end">
                  {{ legalMoney(row.remaining) }}
                </td>
              </tr>
              <tr class="font-weight-bold">
                <td colspan="2">
                  مجموع الباقي
                </td>
                <td class="text-end">
                  {{ legalMoney(trayRemaining) }}
                </td>
              </tr>
            </tbody>
          </VTable>
          <VCheckbox
            v-model="groupForm.send"
            label="إرسال إلى تطبيق القانونية مباشرة (الاسماء المضافة مؤخرا)"
            hide-details
            class="mt-2"
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="groupDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="saving"
            @click="saveGroup"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- ملف لكل حساب -->
    <VDialog
      v-model="bulkDialog"
      max-width="560"
      :persistent="saving"
    >
      <VCard>
        <VCardTitle>ملف مستقل لكل حساب</VCardTitle>
        <VCardText>
          <p class="mb-2">
            سيُنشأ {{ tray.length }} ملف مدين، ملف لكل حساب مختار. استخدم هذا عندما يكون كل اسم شخصاً مختلفاً.
          </p>
          <VProgressLinear
            v-if="saving || bulkProgress.done"
            :model-value="bulkProgress.total ? bulkProgress.done * 100 / bulkProgress.total : 0"
            class="mb-2"
          />
          <VAlert
            v-if="bulkProgress.failed.length"
            type="error"
            variant="tonal"
            density="compact"
          >
            <div
              v-for="f in bulkProgress.failed"
              :key="f"
            >
              {{ f }}
            </div>
          </VAlert>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            :disabled="saving"
            @click="bulkDialog = false"
          >
            إغلاق
          </VBtn>
          <VBtn
            variant="tonal"
            :loading="saving"
            @click="runBulk(false)"
          >
            إنشاء فقط
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="saving"
            @click="runBulk(true)"
          >
            إنشاء وإرسال للقانونية
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- حسابات مشابهة -->
    <VDialog
      v-model="similarDialog"
      max-width="900"
    >
      <VCard>
        <VCardTitle>حسابات أخرى تبدو لنفس الشخص</VCardTitle>
        <VCardText>
          <div class="text-body-2 mb-2">
            {{ similarFor?.customerName }} — مطابقة بالاسم الكامل أو رقم الهاتف داخل فرع {{ similarFor?.cityName }}
          </div>
          <VProgressLinear
            v-if="similarLoading"
            indeterminate
          />
          <VTable
            v-else-if="similarRows.length"
            density="compact"
          >
            <thead>
              <tr>
                <th>الاسم</th>
                <th>الهاتف</th>
                <th>القائمة</th>
                <th>المواد</th>
                <th>تاريخ البيع</th>
                <th class="text-end">
                  الباقي
                </th>
                <th />
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="row in similarRows"
                :key="row.customerId"
              >
                <td>{{ row.customerName }}</td>
                <td dir="ltr">
                  {{ row.phone }}
                </td>
                <td>{{ row.listName }}</td>
                <td>{{ row.items }}</td>
                <td>{{ legalDay(row.saleDate) }}</td>
                <td class="text-end">
                  {{ legalMoney(row.remaining) }}
                </td>
                <td>
                  <VChip
                    v-if="row.personId"
                    size="x-small"
                    color="info"
                    variant="tonal"
                  >
                    ضمن: {{ row.personName }}
                  </VChip>
                  <VChip
                    v-else-if="row.remaining <= 0"
                    size="x-small"
                    color="success"
                    variant="tonal"
                  >
                    منتهي
                  </VChip>
                </td>
              </tr>
            </tbody>
          </VTable>
          <div
            v-else
            class="text-center text-medium-emphasis py-4"
          >
            لا توجد حسابات مشابهة في هذا الفرع
          </div>
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            prepend-icon="tabler-world-search"
            @click="similarDialog = false; openSearch(String(similarFor?.customerName || '').trim())"
          >
            بحث في كل الفروع
          </VBtn>
          <VSpacer />
          <VBtn
            variant="text"
            @click="similarDialog = false"
          >
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            @click="takeSimilar"
          >
            اختيارها كلها مع الحساب الأصلي
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <LegalAccountSearchDialog
      v-model="searchDialog"
      :initial-query="searchQuery"
      :taken="trayKeys"
      confirm-label="إضافة للمختارة"
      @pick="addToTray"
    />
  </div>
</template>

<style scoped>
/* Sticks to the bottom of the content area, so it never slides under the side menu. */
.legal-tray {
  position: sticky;
  z-index: 5;
  inset-block-end: 16px;
  margin-block-start: 16px;
  padding: 12px 16px;
  border: 1px solid rgb(var(--v-theme-primary));
  border-radius: 12px;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 6px 24px rgba(0, 0, 0, 18%);
}

.legal-tray__chips {
  display: flex;
  flex: 1 1 300px;
  flex-wrap: wrap;
  gap: 4px;
  max-block-size: 64px;
  overflow-y: auto;
}

.bg-primary-lighten {
  background: rgba(var(--v-theme-primary), 0.08);
}
</style>
