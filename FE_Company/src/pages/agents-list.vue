<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from '@core/components/app-form-elements/AppTextField.vue'
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات المندوبين
const delegatesData = ref([])

// حالة التحميل
const loading = ref(false)

// نص البحث للفلترة المحلية (حسب اسم المندوب)
const filters = ref({
  textSearch: '',
})

// حالات الـ Dialog
const dialogAdd = ref(false)
const dialogFollower = ref(false)
const dialogEdit = ref(false)
const dialogDeleteConfirm = ref(false)
const dialogPermissions = ref(false)
const followerSaving = ref(false)
const followerError = ref('')
const selectedListIds = ref([])

// بيانات المندوب المختار (للتعديل والحذف والصلاحيات)
const selectedDelegate = ref(null)

// بيانات المندوب للإضافة أو التعديل
const newDelegate = ref({
  delegateName: '',
  address: '',
  phoneNumber: '',
  receiptName: '',
  asyncID: '',
  notes: '',
})

// المتغيرات الخاصة بإدارة صلاحيات المندوب
const availableDelegates = ref([])           // لجلب قائمة المندوبين من API لإضافتهم كصلاحية
const currentPermissions = ref([])             // لتخزين الصلاحيات الحالية للمندوب
const selectedDelegateForPermission = ref(null) // لتحديد المندوب المراد إضافته كصلاحية
const followerListOptions = computed(() =>
  availableDelegates.value.filter(item => {
    const name = item.delegateName || ''

    return !String(name).startsWith('متابع')
  }),
)

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  
  return "لا يوجد"
}

// تعريف أعمدة جدول المندوبين
const headers = [
  { title: 'المندوب', key: 'delegateName' },
  { title: 'كلمة السر', key: 'asyncID' },
  { title: 'عدد العملاء', key: 'numberOfCustomer' },
  { title: 'عدد المصفرين', key: 'numberOfCustomerIsZero' },
  { title: 'عدد الغير مصفرين', key: 'numberOfCustomerIsNotZero' },
  { title: 'عدد العملاء القانونية', key: 'numberOfCustomerIsLegal' },
  { title: 'سعر البيع', key: 'amountTotal' },
  { title: 'سعر الشراء', key: 'amountCost' },
  { title: 'القسط', key: 'amountDay' },
  { title: 'الواصل', key: 'amountRecever' },
  { title: 'الباقي', key: 'amountRemaining' },
  { title: 'المحافظة', key: 'cityName' },
  { title: 'العنوان', key: 'address' },
  { title: 'رقم الهاتف', key: 'phoneNumber' },
  { title: 'اسم الجابي', key: 'receiptName' },
  { title: 'الملاحظات', key: 'notes' },
  { title: 'تعديل', key: 'update' },
  { title: 'الصلاحيات', key: 'permissions' },
  { title: 'حذف', key: 'delete' },
]

// دالة جلب بيانات المندوبين من API
async function fetchDelegates() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const { textSearch } = filters.value
    const response = await axios.get(`${apiUrl}Delegates/Delegates_GetAll/${textSearch || 'null'}`, { headers: authHeader })

    delegatesData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة إضافة المندوب
async function addDelegate() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.post(`${apiUrl}Delegates/Delegates_Create`, newDelegate.value, { headers: authHeader })

    await fetchDelegates()
    dialogAdd.value = false
  } catch (error) {
    console.error(error)
  }
}

// دالة تعديل المندوب
async function editDelegate() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.put(`${apiUrl}Delegates/Delegates_Update`, { ...selectedDelegate.value, ...newDelegate.value }, { headers: authHeader })
    const index = delegatesData.value.findIndex(delegate => delegate.delegateID === selectedDelegate.value.delegateID)
    if (index !== -1) {
      delegatesData.value[index] = response.data
    }
    dialogEdit.value = false
  } catch (error) {
    console.error(error)
  }
}

// دالة حذف المندوب
async function deleteDelegate() {
  try {
    const authHeader = getAuthHeaders()

    await axios.delete(`${apiUrl}Delegates/Delegates_Delete/${selectedDelegate.value.delegateID}`, { headers: authHeader })

    const index = delegatesData.value.findIndex(delegate => delegate.delegateID === selectedDelegate.value.delegateID)
    if (index !== -1) {
      delegatesData.value.splice(index, 1)
    }
    dialogDeleteConfirm.value = false
  } catch (error) {
    console.error(error)
  }
}

// دالة تصدير بيانات المندوبين إلى Excel
function exportToExcel() {
  const dataToExport = delegatesData.value.map(d => ({
    'المندوب': d.delegateName || 'لا يوجد',
    'عدد العملاء': d.numberOfCustomer || 'لا يوجد',
    'عدد العملاء المصفرين': d.numberOfCustomerIsZero || 'لا يوجد',
    'عدد العملاء الغير مصفرين': d.numberOfCustomerIsNotZero || 'لا يوجد',
    'عدد العملاء القانونية': d.numberOfCustomerIsLegal || 'لا يوجد',
    'سعر البيع': d.amountTotal || 'لا يوجد',
    'سعر الشراء': d.amountCost || 'لا يوجد',
    'القسط': d.amountDay || 'لا يوجد',
    'الواصل': d.amountRecever || 'لا يوجد',
    'الباقي': d.amountRemaining || 'لا يوجد',
    'المحافظة': d.cityName || 'لا يوجد',
    'العنوان': d.address || 'لا يوجد',
    'رقم الهاتف': d.phoneNumber || 'لا يوجد',
    'اسم الجابي': d.receiptName || 'لا يوجد',
    'الملاحظات': d.notes || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Delegates")
  XLSX.writeFile(workbook, "Delegates.xlsx")
}

// إجماليات البيانات
const totals = computed(() => [
  {
    icon: 'tabler-users-group',
    value: delegatesData.value.length,
    title: 'عدد المندوبين',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-users',
    value: delegatesData.value.reduce((sum, d) => sum + (d.numberOfCustomer || 0), 0),
    title: 'عدد العملاء الكلي',
    color: "primary",
    gradient: "linear-gradient(135deg, #12c2e9 0%, #c471ed 50%, #f64f59 100%)",
  },
  {
    icon: 'tabler-user-check',
    value: delegatesData.value.reduce((sum, d) => sum + (d.numberOfCustomerIsZero || 0), 0),
    title: 'عدد المصفرين',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
  {
    icon: 'tabler-user-x',
    value: delegatesData.value.reduce((sum, d) => sum + (d.numberOfCustomerIsNotZero || 0), 0),
    title: 'عدد الغير مصفرين',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
  {
    icon: 'tabler-gavel',
    value: delegatesData.value.reduce((sum, d) => sum + (d.numberOfCustomerIsLegal || 0), 0),
    title: 'عدد العملاء القانونية',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
  {
    icon: 'tabler-cash',
    value: formattedNumber(delegatesData.value.reduce((sum, d) => sum + (d.amountTotal || 0), 0)),
    title: 'سعر البيع الكلي',
    color: "success",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
  {
    icon: 'tabler-cash',
    value: formattedNumber(delegatesData.value.reduce((sum, d) => sum + (d.amountCost || 0), 0)),
    title: 'سعر الشراء الكلي',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-calendar',
    value: formattedNumber(delegatesData.value.reduce((sum, d) => sum + (d.amountDay || 0), 0)),
    title: 'القسط الكلي',
    color: "secondary",
    gradient: "linear-gradient(135deg, #667db6 0%, #0082c8 100%, #0082c8 100%, #667db6 100%)",
  },
  {
    icon: 'tabler-credit-card',
    value: formattedNumber(delegatesData.value.reduce((sum, d) => sum + (d.amountRecever || 0), 0)),
    title: 'الواصل الكلي',
    color: "success",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-archive',
    value: formattedNumber(delegatesData.value.reduce((sum, d) => sum + (d.amountRemaining || 0), 0)),
    title: 'المتبقي الكلي',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
])

// دالة فتح Dialog التعديل
function openEditDialog(delegateID) {
  const delegate = delegatesData.value.find(item => item.delegateID === delegateID)
  if (delegate) {
    newDelegate.value = { ...delegate }
    selectedDelegate.value = delegate
    dialogEdit.value = true
  }
}


function resetDelegateForm() {
  newDelegate.value = {
    delegateName: '',
    address: '',
    phoneNumber: '',
    receiptName: '',
    asyncID: '',
    notes: '',
  }
  selectedListIds.value = []
  followerError.value = ''
}

function openAddDialog() {
  resetDelegateForm()
  dialogAdd.value = true
}

async function openAddFollowerDialog() {
  resetDelegateForm()
  await fetchAvailableDelegates()
  dialogFollower.value = true
}

async function addFollower() {
  if (!newDelegate.value.delegateName || !newDelegate.value.asyncID) {
    followerError.value = 'اسم المتابع وكلمة المرور مطلوبان'

    return
  }
  if (!selectedListIds.value.length) {
    followerError.value = 'اختر قائمة واحدة على الأقل لربطها بالمتابع'

    return
  }

  try {
    followerSaving.value = true
    followerError.value = ''
    const authHeader = getAuthHeaders()
    const response = await axios.post(`${apiUrl}Delegates/Delegates_Create`, newDelegate.value, { headers: authHeader })
    const fatherId = response.data?.delegateID ?? response.data?.delegateId

    for (const childId of selectedListIds.value) {
      await axios.post(`${apiUrl}Delegates/SelectDelegate_Create`, {
        delegateFatherID: fatherId,
        delegateChildID: childId,
      }, { headers: authHeader })
    }

    await fetchDelegates()
    dialogFollower.value = false
  } catch (error) {
    console.error(error)
    followerError.value = 'تعذر إضافة المتابع. تأكد أن الـ API المحلي يعمل.'
  } finally {
    followerSaving.value = false
  }
}

// دالة فتح Dialog الحذف
function openDeleteDialog(delegateID) {
  const delegate = delegatesData.value.find(item => item.delegateID === delegateID)
  if (delegate) {
    selectedDelegate.value = delegate
    dialogDeleteConfirm.value = true
  }
}

// دوال إدارة الصلاحيات

// جلب قائمة المندوبين لإضافتهم كصلاحيات
async function fetchAvailableDelegates() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Delegates/Delegates_GetDataAll`, { headers: authHeader })

    availableDelegates.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// جلب الصلاحيات الحالية للمندوب المحدد
async function fetchCurrentPermissions() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Delegates/SelectDelegate_GetByDelegateID/${selectedDelegate.value.delegateID}`, { headers: authHeader })

    currentPermissions.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// فتح Dialog الصلاحيات للمندوب
function openPermissionsDialog(delegateID) {
  const delegate = delegatesData.value.find(item => item.delegateID === delegateID)
  if (delegate) {
    selectedDelegate.value = delegate
    fetchAvailableDelegates()
    fetchCurrentPermissions()
    dialogPermissions.value = true
  }
}

// إضافة صلاحية (مندوب جديد) للمندوب المحدد
async function addPermission() {
  if (!selectedDelegateForPermission.value) return
  try {
    const authHeader = getAuthHeaders()

    const response = await axios.post(`${apiUrl}Delegates/SelectDelegate_Create`, {
      delegateFatherID: selectedDelegate.value.delegateID,
      delegateChildID: selectedDelegateForPermission.value,
    }, { headers: authHeader })

    if (response.data.selectDelegateID !== 0) {
      // التحقق إذا كان delegateName موجوداً في currentPermissions
      const isDelegateExists = currentPermissions.value.some(permission => permission.delegateName === response.data.delegateName)
      if (!isDelegateExists) {
        currentPermissions.value.push(response.data)
      }
    }

    selectedDelegateForPermission.value = null
  } catch (error) {
    console.error(error)
  }
}


// إزالة صلاحية من القائمة
async function removePermission(selectDelegateID) {
  try {
    const authHeader = getAuthHeaders()

    await axios.delete(`${apiUrl}Delegates/SelectDelegate_Delete/${selectDelegateID}`, { headers: authHeader })

    const index = currentPermissions.value.findIndex(item => item.selectDelegateID === selectDelegateID)
    if (index !== -1) {
      currentPermissions.value.splice(index, 1)
    }
  } catch (error) {
    console.error(error)
  }
}

onMounted(() => {
  fetchDelegates()
})
</script>

<template>
  <!-- عرض إجماليات البيانات -->
  <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, index) in totals"
      :key="index"
      cols="12"
      sm="6"
      md="2"
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
    <VForm>
      <!-- فلترة محلية حسب اسم المندوب -->
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            بحث بالمندوب
          </VLabel>
          <AppTextField
            v-model="filters.textSearch"
            prepend-inner-icon="tabler-search"
            placeholder="ادخل اسم المندوب"
            clearable
          />
        </VCol>
        <VRow style="margin-block-start: 31px;margin-inline: 5px 10px;">
          <VBtn
            color="primary"
            style="margin-inline-start: 10px;"
            prepend-icon="tabler-search"
            :loading="loading"
            :disabled="loading"
            @click="fetchDelegates"
          >
            بحث
          </VBtn>
          <VBtn
            color="primary"
            style="margin-inline-start: 10px;"
            prepend-icon="tabler-user-plus"
            @click="openAddFollowerDialog"
          >
            إضافة متابع
          </VBtn>
          <VBtn
            color="primary"
            variant="tonal"
            style="margin-inline-start: 10px;"
            prepend-icon="tabler-users-plus"
            @click="openAddDialog"
          >
            إضافة مندوب
          </VBtn>
          <VBtn
            color="success"
            prepend-icon="tabler-file-export"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VRow>
      </VRow>

      <!-- عرض بيانات المندوبين -->
      <VRow>
        <VDataTable
          :headers="headers"
          :items="delegatesData"
          :items-per-page="50"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <template #item.delegateName="{ item }">
            <div class="font-weight-medium">
              {{ item.delegateName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.numberOfCustomer="{ item }">
            <VChip
              color="primary"
              size="small"
              class="font-weight-bold"
            >
              {{ item.numberOfCustomer || 0 }}
            </VChip>
          </template>
          <template #item.numberOfCustomerIsZero="{ item }">
            <VChip
              color="success"
              size="small"
              class="font-weight-bold"
            >
              {{ item.numberOfCustomerIsZero || 0 }}
            </VChip>
          </template>
          <template #item.numberOfCustomerIsNotZero="{ item }">
            <VChip
              color="error"
              size="small"
              class="font-weight-bold"
            >
              {{ item.numberOfCustomerIsNotZero || 0 }}
            </VChip>
          </template>
          <template #item.numberOfCustomerIsLegal="{ item }">
            <VChip
              color="warning"
              size="small"
              class="font-weight-bold"
            >
              {{ item.numberOfCustomerIsLegal || 0 }}
            </VChip>
          </template>
          <template #item.amountTotal="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.amountTotal) }}
            </div>
          </template>
          <template #item.amountCost="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.amountCost) }}
            </div>
          </template>
          <template #item.amountDay="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.amountDay) }}
            </div>
          </template>
          <template #item.amountRecever="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.amountRecever) }}
            </div>
          </template>
          <template #item.amountRemaining="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.amountRemaining) }}
            </div>
          </template>
          <template #item.cityName="{ item }">
            <div>
              {{ item.cityName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.address="{ item }">
            <div>
              {{ item.address || 'لا يوجد' }}
            </div>
          </template>
          <template #item.phoneNumber="{ item }">
            <div>
              {{ item.phoneNumber || 'لا يوجد' }}
            </div>
          </template>
          <template #item.notes="{ item }">
            <div>
              {{ item.notes || 'لا يوجد' }}
            </div>
          </template>
          <template #item.permissions="{ item }">
            <VBtn
              color="primary"
              style="margin-block-end: 10px;"
              prepend-icon="tabler-lock"
              @click="openPermissionsDialog(item.delegateID)"
            >
              الصلاحيات
            </VBtn>
          </template>

          <template #item.update="{ item }">
            <VBtn
              color="primary"
              style="margin-block-end: 10px;"
              prepend-icon="tabler-edit"
              @click="openEditDialog(item.delegateID)"
            >
              تعديل
            </VBtn>
          </template>

          <template #item.delete="{ item }">
            <VBtn
              color="error"
              style="margin-block-end: 10px;"
              prepend-icon="tabler-trash"
              @click="openDeleteDialog(item.delegateID)"
            >
              حذف
            </VBtn>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>

  <!-- Dialog: إضافة مندوب جديد -->
  <VDialog
    v-model="dialogAdd"
    max-width="600px"
    content-class="modern-dialog"
    transition="dialog-bottom-transition"
  >
    <VCard class="pa-2">
      <div class="dialog-header pa-4 d-flex align-center justify-space-between">
        <div class="d-flex align-center gap-3">
          <VAvatar
            color="primary"
            variant="tonal"
            rounded
            size="48"
          >
            <VIcon
              icon="tabler-user-plus"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              إضافة مندوب / قائمة
            </h4>
            <span class="text-caption text-medium-emphasis">أدخل بيانات القائمة الجديدة</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          size="small"
          @click="dialogAdd = false"
        >
          <VIcon
            icon="tabler-x"
            size="24"
          />
        </VBtn>
      </div>
      <VCardText>
        <VForm>
          <VLabel class="mb-2">
            اسم المندوب
          </VLabel>
          <AppTextField
            v-model="newDelegate.delegateName"
            required
            prepend-inner-icon="tabler-user"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            العنوان
          </VLabel>
          <AppTextField
            v-model="newDelegate.address"
            prepend-inner-icon="tabler-map-pin"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            رقم الهاتف
          </VLabel>
          <AppTextField
            v-model="newDelegate.phoneNumber"
            prepend-inner-icon="tabler-phone"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            كلمة المرور
          </VLabel>
          <AppTextField
            v-model="newDelegate.asyncID"
            type="password"
            required
            :rules="[v => !!v || 'كلمة المرور مطلوبة']"
            prepend-inner-icon="tabler-lock"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            اسم الجابي
          </VLabel>
          <AppTextField
            v-model="newDelegate.receiptName"
            prepend-inner-icon="tabler-user-check"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            الملاحظات
          </VLabel>
          <AppTextField
            v-model="newDelegate.notes"
            prepend-inner-icon="tabler-file-text"
          />
        </VForm>
      </VCardText>
      <VCardActions>
        <VBtn
          variant="tonal"
          color="secondary"
          @click="dialogAdd = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          :disabled="!newDelegate.asyncID || !newDelegate.delegateName"
          @click="addDelegate"
        >
          إضافة
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- Dialog: إضافة متابع -->
  <VDialog
    v-model="dialogFollower"
    max-width="640px"
    content-class="modern-dialog"
    transition="dialog-bottom-transition"
  >
    <VCard class="pa-2">
      <div class="dialog-header pa-4 d-flex align-center justify-space-between">
        <div class="d-flex align-center gap-3">
          <VAvatar
            color="primary"
            variant="tonal"
            rounded
            size="48"
          >
            <VIcon
              icon="tabler-user-plus"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              إضافة متابع
            </h4>
            <span class="text-caption text-medium-emphasis">أدخل بيانات المتابع واختر القوائم التي يراها</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          size="small"
          @click="dialogFollower = false"
        >
          <VIcon
            icon="tabler-x"
            size="24"
          />
        </VBtn>
      </div>
      <VCardText>
        <VAlert
          v-if="followerError"
          color="error"
          variant="tonal"
          class="mb-4"
          density="compact"
        >
          {{ followerError }}
        </VAlert>
        <VForm>
          <VLabel class="mb-2">
            اسم المتابع
          </VLabel>
          <AppTextField
            v-model="newDelegate.delegateName"
            required
            prepend-inner-icon="tabler-user"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            العنوان
          </VLabel>
          <AppTextField
            v-model="newDelegate.address"
            prepend-inner-icon="tabler-map-pin"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            رقم الهاتف
          </VLabel>
          <AppTextField
            v-model="newDelegate.phoneNumber"
            prepend-inner-icon="tabler-phone"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            كلمة المرور (لتطبيق المتابع)
          </VLabel>
          <AppTextField
            v-model="newDelegate.asyncID"
            type="password"
            required
            prepend-inner-icon="tabler-lock"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            اسم الجابي
          </VLabel>
          <AppTextField
            v-model="newDelegate.receiptName"
            prepend-inner-icon="tabler-user-check"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            الملاحظات
          </VLabel>
          <AppTextField
            v-model="newDelegate.notes"
            prepend-inner-icon="tabler-file-text"
          />

          <VLabel
            class="mb-2 font-weight-bold"
            style="margin-block-start: 16px;"
          >
            اختر القوائم المرتبطة
          </VLabel>
          <div class="rounded-lg pa-3 lists-checkboxes">
            <div
              v-if="!followerListOptions.length"
              class="text-medium-emphasis"
            >
              لا توجد قوائم. أضف مندوب أولاً من زر إضافة مندوب.
            </div>
            <VCheckbox
              v-for="item in followerListOptions"
              :key="item.delegateID || item.delegateId"
              v-model="selectedListIds"
              :label="item.delegateName"
              :value="item.delegateID || item.delegateId"
              color="primary"
              density="compact"
              hide-details
            />
          </div>
        </VForm>
      </VCardText>
      <VCardActions>
        <VBtn
          variant="tonal"
          color="secondary"
          @click="dialogFollower = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          :loading="followerSaving"
          :disabled="!newDelegate.asyncID || !newDelegate.delegateName || !selectedListIds.length"
          @click="addFollower"
        >
          إضافة
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- Dialog: تعديل بيانات المندوب -->
  <VDialog
    v-model="dialogEdit"
    max-width="600px"
    content-class="modern-dialog"
    transition="dialog-bottom-transition"
  >
    <VCard class="pa-2">
      <div class="dialog-header pa-4 d-flex align-center justify-space-between">
        <div class="d-flex align-center gap-3">
          <VAvatar
            color="primary"
            variant="tonal"
            rounded
            size="48"
          >
            <VIcon
              icon="tabler-edit"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              تعديل بيانات المندوب
            </h4>
            <span class="text-caption text-medium-emphasis">تحديث تفاصيل المندوب</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          size="small"
          @click="dialogEdit = false"
        >
          <VIcon
            icon="tabler-x"
            size="24"
          />
        </VBtn>
      </div>
      <VCardText>
        <VForm>
          <VLabel class="mb-2">
            اسم المندوب
          </VLabel>
          <AppTextField
            v-model="newDelegate.delegateName"
            required
            prepend-inner-icon="tabler-user"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            العنوان
          </VLabel>
          <AppTextField
            v-model="newDelegate.address"
            prepend-inner-icon="tabler-map-pin"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            رقم الهاتف
          </VLabel>
          <AppTextField
            v-model="newDelegate.phoneNumber"
            prepend-inner-icon="tabler-phone"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            كلمة المرور
          </VLabel>
          <AppTextField
            v-model="newDelegate.asyncID"
            type="password"
            prepend-inner-icon="tabler-lock"
            :hint="selectedDelegate?.asyncID ? 'اتركه فارغاً للإبقاء على كلمة السر الحالية' : 'مطلوب'"
            persistent-hint
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            اسم الجابي
          </VLabel>
          <AppTextField
            v-model="newDelegate.receiptName"
            prepend-inner-icon="tabler-user-check"
          />

          <VLabel
            class="mb-2"
            style="margin-block-start: 10px;"
          >
            الملاحظات
          </VLabel>
          <AppTextField
            v-model="newDelegate.notes"
            prepend-inner-icon="tabler-file-text"
          />
        </VForm>
      </VCardText>
      <VCardActions>
        <VBtn
          variant="tonal"
          color="secondary"
          @click="dialogEdit = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          @click="editDelegate"
        >
          تعديل
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- Dialog: تأكيد الحذف -->
  <VDialog
    v-model="dialogDeleteConfirm"
    max-width="400px"
    content-class="modern-dialog"
  >
    <VCard>
      <div class="dialog-header pa-4 d-flex align-center justify-space-between">
        <div class="d-flex align-center gap-3">
          <VAvatar
            color="error"
            variant="tonal"
            rounded
            size="48"
          >
            <VIcon
              icon="tabler-alert-triangle"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              تأكيد الحذف
            </h4>
            <span class="text-caption text-medium-emphasis">إجراء لا يمكن التراجع عنه</span>
          </div>
        </div>
      </div>
      <VCardText>
        <p>هل أنت متأكد من رغبتك في حذف المندوب <strong>{{ selectedDelegate?.delegateName }}</strong>؟</p>
      </VCardText>
      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="dialogDeleteConfirm = false"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إلغاء
        </VBtn>
        <VBtn
          color="error"
          @click="deleteDelegate"
        >
          <VIcon
            icon="tabler-trash"
            class="me-2"
          />
          حذف
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- Dialog: إدارة صلاحيات المندوب -->
  <VDialog
    v-model="dialogPermissions"
    max-width="500px"
    content-class="modern-dialog"
  >
    <VCard>
      <div class="dialog-header pa-4 d-flex align-center justify-space-between">
        <div class="d-flex align-center gap-3">
          <VAvatar
            color="primary"
            variant="tonal"
            rounded
            size="48"
          >
            <VIcon
              icon="tabler-lock"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              صلاحيات المندوب: {{ selectedDelegate?.delegateName }}
            </h4>
            <span class="text-caption text-medium-emphasis">إدارة صلاحيات رؤية المندوبين الآخرين</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          size="small"
          @click="dialogPermissions = false"
        >
          <VIcon
            icon="tabler-x"
            size="24"
          />
        </VBtn>
      </div>
      <VCardText>
        <div v-if="currentPermissions.length">
          <VLabel class="mb-2 font-weight-bold">
            المندوبين المضافين:
          </VLabel>
          <VList class="rounded-lg border mb-4">
            <VListItem
              v-for="permission in currentPermissions"
              :key="permission.delegateID"
              prepend-icon="tabler-user"
            >
              <div style="display: flex; align-items: center; justify-content: space-between; inline-size: 100%;">
                <span>{{ permission.delegateName }}</span>
                <VBtn
                  color="error"
                  variant="text"
                  size="small"
                  icon
                  @click="removePermission(permission.selectDelegateID)"
                >
                  <VIcon icon="tabler-trash" />
                </VBtn>
              </div>
            </VListItem>
          </VList>
        </div>
        <div
          v-else
          style="margin-block-end: 10px;"
        >
          <VLabel>لا توجد صلاحيات مضافة حالياً.</VLabel>
        </div>
        <VLabel style="margin-block-end: 5px;">
          اختر مندوب لإضافة صلاحية
        </VLabel>
        <VAutocomplete
          v-model="selectedDelegateForPermission"
          prepend-inner-icon="tabler-user"
          :items="availableDelegates"
          item-value="delegateID"
          item-title="delegateName"
        />
      </VCardText>
      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="dialogPermissions = false"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          @click="addPermission"
        >
          <VIcon
            icon="tabler-check"
            class="me-2"
          />
          إضافة
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>

<style scoped>
.v-btn {
  margin-block-start: 10px;
}

.v-btn + .v-btn {
  margin-inline-start: 10px;
}

.text-right {
  text-align: end;
}

.d-flex {
  display: flex;
}

.align-center {
  align-items: center;
}

.justify-end {
  justify-content: flex-end;
}

.lists-checkboxes {
  max-block-size: 240px;
  overflow: auto;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
}

/* Premium numeric badges style */
:deep(.premium-amount) {
  display: inline-block !important;
  min-width: 100px !important;
  padding: 6px 12px !important;
  border-radius: 8px !important;
  font-weight: 700 !important;
  font-size: 0.95rem !important;
  text-align: center !important;
  font-family: 'Cairo', sans-serif !important;
  letter-spacing: 0.5px !important;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.04) !important;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1) !important;
  border: 1px solid transparent !important;
}

:deep(.premium-amount:hover) {
  transform: translateY(-1px) !important;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.08) !important;
}

/* Light Theme Styling */
.v-theme--light :deep(.amt-installment) {
  background-color: #f0f7ff !important;
  color: #0284c7 !important;
  border-color: #e0f2fe !important;
}
.v-theme--light :deep(.amt-installment:hover) {
  background-color: #e0f2fe !important;
}

.v-theme--light :deep(.amt-paid-yesterday) {
  background-color: #ecfdf5 !important;
  color: #059669 !important;
  border-color: #d1fae5 !important;
}
.v-theme--light :deep(.amt-paid-yesterday:hover) {
  background-color: #d1fae5 !important;
}

.v-theme--light :deep(.amt-total-sales) {
  background-color: #f8fafc !important;
  color: #475569 !important;
  border-color: #e2e8f0 !important;
}
.v-theme--light :deep(.amt-total-sales:hover) {
  background-color: #f1f5f9 !important;
}

.v-theme--light :deep(.amt-total-receipts) {
  background-color: #f0fdf4 !important;
  color: #16a34a !important;
  border-color: #dcfce7 !important;
}
.v-theme--light :deep(.amt-total-receipts:hover) {
  background-color: #dcfce7 !important;
}

.v-theme--light :deep(.amt-remaining) {
  background-color: #fff1f2 !important;
  color: #e11d48 !important;
  border-color: #ffe4e6 !important;
  font-weight: 800 !important;
}
.v-theme--light :deep(.amt-remaining:hover) {
  background-color: #ffe4e6 !important;
}

/* Dark Theme Styling */
.v-theme--dark :deep(.amt-installment) {
  background-color: rgba(2, 132, 199, 0.15) !important;
  color: #38bdf8 !important;
  border-color: rgba(56, 189, 248, 0.3) !important;
}
.v-theme--dark :deep(.amt-installment:hover) {
  background-color: rgba(2, 132, 199, 0.25) !important;
}

.v-theme--dark :deep(.amt-paid-yesterday) {
  background-color: rgba(5, 150, 105, 0.15) !important;
  color: #34d399 !important;
  border-color: rgba(52, 211, 153, 0.3) !important;
}
.v-theme--dark :deep(.amt-paid-yesterday:hover) {
  background-color: rgba(5, 150, 105, 0.25) !important;
}

.v-theme--dark :deep(.amt-total-sales) {
  background-color: rgba(71, 85, 105, 0.15) !important;
  color: #cbd5e1 !important;
  border-color: rgba(203, 213, 225, 0.2) !important;
}
.v-theme--dark :deep(.amt-total-sales:hover) {
  background-color: rgba(71, 85, 105, 0.25) !important;
}

.v-theme--dark :deep(.amt-total-receipts) {
  background-color: rgba(22, 163, 74, 0.15) !important;
  color: #4ade80 !important;
  border-color: rgba(74, 222, 128, 0.3) !important;
}
.v-theme--dark :deep(.amt-total-receipts:hover) {
  background-color: rgba(22, 163, 74, 0.25) !important;
}

.v-theme--dark :deep(.amt-remaining) {
  background-color: rgba(225, 29, 72, 0.15) !important;
  color: #fb7185 !important;
  border-color: rgba(251, 113, 133, 0.3) !important;
  font-weight: 800 !important;
}
.v-theme--dark :deep(.amt-remaining:hover) {
  background-color: rgba(225, 29, 72, 0.25) !important;
}
</style>
