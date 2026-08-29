<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from '@core/components/app-form-elements/AppTextField.vue'
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات بنود الصرف
const exchangeItemsData = ref([])

// حالة التحميل
const loading = ref(false)

// نص البحث المحلي لتصفية البنود حسب اسم البند
const searchText = ref('')

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  
  return "لا يوجد"
}

// تعريف أعمدة جدول بنود الصرف (أسماء الحقول تبدأ بحرف صغير)
const headers = [
  { title: 'البند', key: 'exchangeItemName' },
  { title: 'المحافظة', key: 'cityName' },
  { title: 'الحد', key: 'limitAmount' },
  { title: 'الحساب', key: 'amountAccount' },
  { title: 'تعديل', key: 'update' },
  { title: 'حذف', key: 'delete' },
]

const totals = computed(() => [
  {
    icon: 'tabler-list',  // أيقونة تمثل قائمة البنود
    value: exchangeItemsData.value.length,
    title: 'عدد البنود الكلي',
    bgColor: '#ECE8FC',
    iconColor: 'blue',
  },
  {
    icon: 'tabler-credit-card',  // أيقونة تمثل الحد أو القياس
    value: formattedNumber(
      exchangeItemsData.value.reduce((sum, item) => sum + (item.limitAmount || 0), 0),
    ),
    title: 'الحد الكلي',
    bgColor: '#F1C1A6',
    iconColor: 'green',
  },
  {
    icon: 'tabler-credit-card',  // أيقونة تمثل المال أو الحساب
    value: formattedNumber(
      exchangeItemsData.value.reduce((sum, item) => sum + (item.amountAccount || 0), 0),
    ),
    title: 'مبلغ الحساب الكلي',
    bgColor: '#E5E5E5',
    iconColor: 'purple',
  },
])


// دالة تحويل البيانات من PascalCase إلى camelCase (اختياري إذا كانت البيانات ليست بالحالة المطلوبة)
function toCamelCase(item) {
  return {
    exchangeItemID: item.exchangeItemID,
    exchangeItemName: item.exchangeItemName,
    limitAmount: item.limitAmount,
    exchangeItemsState: item.exchangeItemsState,
    cityName: item.cityName,
    amountAccount: item.amountAccount,
  }
}

// دالة جلب بيانات بنود الصرف من API
async function fetchExchangeItems() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}ExchangesItems/ExchangesItems_GetAll`, { headers: authHeader })

    exchangeItemsData.value = response.data.map(item => toCamelCase(item))
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير بيانات بنود الصرف إلى Excel
function exportToExcel() {
  const dataToExport = filteredExchangeItems.value.map(item => ({
    'البند': item.exchangeItemName || 'لا يوجد',
    'المحافظة': item.cityName || 'لا يوجد',
    'اسم المستخدم': item.userName || 'لا يوجد',
    'الحد': item.limitAmount || 'لا يوجد',
    'الحساب': item.amountAccount || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "ExchangeItems")
  XLSX.writeFile(workbook, "ExchangeItems.xlsx")
}

// فلترة البنود محلياً حسب اسم البند
const filteredExchangeItems = computed(() => {
  if (!searchText.value) return exchangeItemsData.value
  
  return exchangeItemsData.value.filter(item =>
    item.exchangeItemName &&
    item.exchangeItemName.toLowerCase().includes(searchText.value.toLowerCase()),
  )
})

// بيانات البند للإضافة أو التعديل
const newExchangeItem = ref({
  exchangeItemName: '',
  limitAmount: '',
})

// حالة الـ Dialogs
const dialogAdd = ref(false)
const dialogEdit = ref(false)
const dialogDeleteConfirm = ref(false)

// بيانات البند المختار للتعديل أو الحذف
const selectedExchangeItem = ref({})

// دالة إضافة البند
async function addExchangeItem() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.post(`${apiUrl}ExchangesItems/ExchangesItems_Create`, newExchangeItem.value, { headers: authHeader })

    await fetchExchangeItems()  // تحديث قائمة البنود بعد الإضافة
    dialogAdd.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}

// دالة تعديل البند
async function editExchangeItem() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.put(`${apiUrl}ExchangesItems/ExchangesItems_Update/${selectedExchangeItem.value.exchangeItemID}`, { ...selectedExchangeItem.value, ...newExchangeItem.value }, { headers: authHeader })

    await fetchExchangeItems()  // تحديث قائمة البنود بعد التعديل
    dialogEdit.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}

// دالة حذف البند
async function deleteExchangeItem() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.delete(`${apiUrl}ExchangesItems/ExchangesItems_Delete/${selectedExchangeItem.value.exchangeItemID}`, { headers: authHeader })

    await fetchExchangeItems()  // تحديث قائمة البنود بعد الحذف
    dialogDeleteConfirm.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}

function openDeleteDialog(item) {
  selectedExchangeItem.value = item
  dialogDeleteConfirm.value = true
}

function openEditDialog(item) {
  selectedExchangeItem.value = item
  newExchangeItem.value = { ...item }
  dialogEdit.value = true
}

function openAddDialog(item) {
  dialogAdd.value = true
}

onMounted(() => {
  fetchExchangeItems()
})
</script>

<template>
  <VRow
    class="pa-5"
    style=" margin-block-start: -33px;margin-inline-start: -30px;"
  >
    <VCol
      v-for="(card, i) in totals"
      :key="i"
      cols="12"
      sm="6"
      md="2"
    >
      <VCard
        class="pa-5 d-flex flex-column align-center text-center"
        elevation="2"
      >
        <VRow class="d-flex align-center justify-center">
          <VIcon
            :icon="card.icon"
            size="50"
            :color="card.iconColor"
          />
        </VRow>
        <h2
          class="mt-3"
          style="font-size: 18px;"
        >
          {{ card.value }}
        </h2>
        <VLabel class="text-subtitle-1">
          {{ card.title }}
        </VLabel>
      </VCard>
    </VCol>
  </VRow>
  <VCard class="pa-10">
    <VForm>
      <!-- فلترة محلية حسب اسم البند -->
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            بحث بالبند
          </VLabel>
          <AppTextField
            v-model="searchText"
            placeholder="ادخل اسم البند"
            clearable
            prepend-inner-icon="tabler-search"
          />
        </VCol>
        <VRow style="margin-block-start: 32px;margin-inline-end: 10px;">
          <VBtn
            color="primary"
            style="margin-inline-start: 20px;"
            prepend-icon="tabler-plus"
            @click="openAddDialog"
          >
            إضافة بند صرف
          </VBtn>
          <VBtn
            color="success"
            style="margin-inline-start: 20px;"
            prepend-icon="tabler-upload"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VRow>
      </VRow>

      <!-- عرض بيانات بنود الصرف -->
      <VRow>
        <VDataTable
          :headers="headers"
          :items="filteredExchangeItems"
          :items-per-page="50"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          items-per-page-text="عدد السجل"
        >
          <template #item.exchangeItemName="{ item }">
            <div>
              {{ item.exchangeItemName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.cityName="{ item }">
            <div>
              {{ item.cityName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.userName="{ item }">
            <div>
              {{ item.userName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.limitAmount="{ item }">
            <div>
              {{ formattedNumber(item.limitAmount) }}
            </div>
          </template>
          <template #item.amountAccount="{ item }">
            <div>
              {{ formattedNumber(item.amountAccount) }}
            </div>
          </template>
          <template #item.update="{ item }">
            <VBtn
              style="margin-block-end: 10px;"
              color="primary"
              prepend-icon="tabler-edit"
              @click="openEditDialog(item)"
            >
              تعديل
            </VBtn>
          </template>
          <template #item.delete="{ item }">
            <VBtn
              style="margin-block-end: 10px;"
              color="error"
              prepend-icon="tabler-trash"
              @click="openDeleteDialog(item)"
            >
              حذف
            </VBtn>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>

  <!-- Dialog إضافة البند -->
  <VDialog
    v-model="dialogAdd"
    max-width="500px"
  >
    <VCard>
      <VCardTitle>إضافة بند جديد</VCardTitle>
      <VCardText>
        <VForm>
          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            اسم البند
          </VLabel>
          <VTextField
            v-model="newExchangeItem.exchangeItemName"
            required
            prepend-inner-icon="tabler-tag"
          />

          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            الحد
          </VLabel>
          <VTextField
            v-model="newExchangeItem.limitAmount"
            prepend-inner-icon="tabler-credit-card"
          />
        </VForm>
      </VCardText>
      <VCardActions>
        <VBtn
          prepend-icon="tabler-x"
          @click="dialogAdd = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          prepend-icon="tabler-check"
          color="primary"
          @click="addExchangeItem"
        >
          إضافة
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- Dialog تعديل البند -->
  <VDialog
    v-model="dialogEdit"
    max-width="500px"
  >
    <VCard>
      <VCardTitle>تعديل بيانات البند</VCardTitle>
      <VCardText>
        <VForm>
          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            اسم البند
          </VLabel>
          <VTextField
            v-model="newExchangeItem.exchangeItemName"
            required
            prepend-inner-icon="tabler-tag"
          />

          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            الحد
          </VLabel>
          <VTextField
            v-model="newExchangeItem.limitAmount"
            prepend-inner-icon="tabler-credit-card"
          />
        </VForm>
      </VCardText>
      <VCardActions>
        <VBtn
          prepend-icon="tabler-x"
          @click="dialogEdit = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          prepend-icon="tabler-check"
          color="primary"
          @click="editExchangeItem"
        >
          تعديل
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>


  <!-- Dialog تأكيد الحذف -->
  <VDialog
    v-model="dialogDeleteConfirm"
    max-width="400px"
  >
    <VCard>
      <VCardTitle>تأكيد الحذف</VCardTitle>
      <VCardText>هل أنت متأكد من أنك تريد حذف هذا البند؟</VCardText>
      <VCardActions>
        <VBtn
          prepend-icon="tabler-x"
          @click="dialogDeleteConfirm = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="red"
          prepend-icon="tabler-trash" 
          @click="deleteExchangeItem"
        >
          حذف
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
</style>
