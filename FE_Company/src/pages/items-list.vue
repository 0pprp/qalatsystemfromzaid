<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

const apiUrl = localStorage.getItem('LinkCity')
const itemsData = ref([])
const loading = ref(false)
const formattedNumber = num => (num ? num.toLocaleString() + " دع" : '0')

const filters = ref({
  textSearch: '',
  storeSearch: '',
  showType: 'الجميع',
})

const storeOptions = ref([])

const headers = [
  { title: 'اسم المادة', key: 'itemName' },
  { title: 'المخزن', key: 'storeName' },
  { title: 'سعر البيع', key: 'itemPriceDenar' },
  { title: 'سعر الشراء', key: 'itemCostDenar' },
  { title: 'القسط', key: 'amountDayDenar' },
  { title: 'الكمية', key: 'quantity' },
  { title: 'عدد المبيعات', key: 'numberOfItemsSales' },
  { title: 'عدد المشتريات', key: 'numberOfItemsBuys' },
  { title: 'مبلغ البيع الكلي', key: 'priceTotalItem' },
  { title: 'مبلغ الشراء الكلي', key: 'costTotalItem' },
  { title: 'سعر البيع للمباع', key: 'itemPriceSales' },
  { title: 'سعر الشراء للمباع', key: 'itemCostSales' },
  { title: 'الملاحظات', key: 'notes' },
  { title: '', key: 'update' },
  { title: '', key: 'delete' },
]

const totals = computed(() => [
  {
    icon: 'tabler-cube', // عدد المواد
    value: itemsData.value.length,
    title: 'عدد انواع المواد',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-cube', // عدد المواد
    value: itemsData.value.reduce((sum, item) => sum + item.quantity, 0),
    title: 'عدد المواد',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
  {
    icon: 'tabler-tag', // سعر البيع
    value: formattedNumber(itemsData.value.reduce((sum, item) => sum + item.itemPriceDenar, 0)),
    title: 'سعر البيع',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
  {
    icon: 'tabler-tag', // سعر الشراء
    value: formattedNumber(itemsData.value.reduce((sum, item) => sum + item.itemCostDenar, 0)),
    title: 'سعر الشراء',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-calendar', // القسط اليومي
    value: formattedNumber(itemsData.value.reduce((sum, item) => sum + item.amountDayDenar, 0)),
    title: 'القسط اليومي',
    color: "secondary",
    gradient: "linear-gradient(135deg, #667db6 0%, #0082c8 100%, #0082c8 100%, #667db6 100%)",
  },
  {
    icon: 'tabler-box', // عدد المواد المباعة
    value: itemsData.value.reduce((sum, item) => sum + item.numberOfItemsSales, 0),
    title: 'عدد المواد المباعة',
    color: "success",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
  {
    icon: 'tabler-box', // عدد المواد المشترية
    value: itemsData.value.reduce((sum, item) => sum + item.numberOfItemsBuys, 0),
    title: 'عدد المواد المشترية',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
  {
    icon: 'tabler-box', // سعر البيع الإجمالي
    value: formattedNumber(itemsData.value.reduce((sum, item) => sum + (item.itemPriceDenar * item.quantity), 0)),
    title: 'سعر البيع الإجمالي',
    color: "info",
    gradient: "linear-gradient(135deg, #a8ff78 0%, #78ffd6 100%)",
  },
  {
    icon: 'tabler-box', // سعر الشراء الإجمالي
    value: formattedNumber(itemsData.value.reduce((sum, item) => sum + (item.itemCostDenar * item.quantity), 0)),
    title: 'سعر الشراء الإجمالي',
    color: "warning",
    gradient: "linear-gradient(135deg, #FF8008 0%, #FFC837 100%)",
  },
  {
    icon: 'tabler-cash', // إجمالي سعر البيع للمباع
    value: formattedNumber(itemsData.value.reduce((sum, item) => sum + (item.itemPriceDenar * item.numberOfItemsSales), 0)),
    title: 'سعر البيع للمباع',
    color: "primary",
    gradient: "linear-gradient(135deg, #12c2e9 0%, #c471ed 50%, #f64f59 100%)",
  },
  {
    icon: 'tabler-cash', // إجمالي سعر الشراء للمباع
    value: formattedNumber(itemsData.value.reduce((sum, item) => sum + (item.itemCostDenar * item.numberOfItemsSales), 0)),
    title: 'سعر الشراء للمباع',
    color: "success",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
])

const formData = ref({
  storeID: '',
  itemName: '',
  itemPriceDenar: '',
  itemCostDenar: '',
  amountDayDenar: '',
  quantity: '',
  notificationNumber: '',
  notes: '',
})

const addDialog = ref(false)
const editDialog = ref(false)
const confirmDeleteDialog = ref(false)
const itemIdToDelete = ref(null)
const currentItemId = ref(null)

async function fetchItems() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const storeID = filters.value.storeSearch ?? 'null'
    const itemName = filters.value.textSearch || 'null'
    const showType = filters.value.showType || 'null'

    const response = await axios.get(`${apiUrl}Items/Items_GetAll/${storeID}&&${itemName}&&${showType}`,
      {
        headers: authHeader,
      },
    )

    itemsData.value = response.data
  } catch (error) {
    console.log(error)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchStores()
})

async function fetchStores() {
  try {
    const authHeader = getAuthHeaders()

    const response = await axios.get(
      `${apiUrl}Stores/StoresData_GetAll`,
      { headers: authHeader },
    )

    storeOptions.value = response.data
  } catch (error) {
    console.log(error)
  }
}

function openAddDialog() {
  formData.value = {
    storeID: '',
    itemName: '',
    itemPriceDenar: '',
    itemCostDenar: '',
    amountDayDenar: '',
    quantity: '',
    notificationNumber: '',
    notes: '',
  }
  currentItemId.value = null
  addDialog.value = true
}

function openEditDialog(itemID) {
  const item = itemsData.value.find(item => item.itemID === itemID)
  if (item) {
    formData.value = { ...item }
    currentItemId.value = item.itemID
    editDialog.value = true
  }
}

function openDeleteDialog(itemID) {
  itemIdToDelete.value = itemID
  confirmDeleteDialog.value = true
}

async function addItem() {
  if(formData.value.itemPriceDenar>=1000){
    if(formData.value.itemCostDenar>=1000){
      if(formData.value.amountDayDenar>=1000){
        const authHeader = getAuthHeaders()
        const url = `${apiUrl}Items/Items_Create`
        const data = new FormData()

        Object.keys(formData.value).forEach(key => {
          data.append(key, formData.value[key])
        })
        try {
          const response = await axios.post(url, data, { headers: { 'Content-Type': 'multipart/form-data', ...authHeader } })

          itemsData.value.push(response.data)
          addDialog.value = false
        } catch (error) {
          console.error(error)
        }
      }else {
        alert('يجب ان يكون القسط اكبر او يساوي 1000.')
      }
    }else {
      alert('يجب ان يكون سعر الشراء اكبر او يساوي 1000.')
    }
  }else {
    alert('يجب ان يكون سعر البيع اكبر او يساوي 1000.')
  }
}

async function updateItem() {
  if(formData.value.itemPriceDenar>=1000){
    if(formData.value.itemCostDenar>=1000){
      if(formData.value.amountDayDenar>=1000){
        const authHeader = getAuthHeaders()
        const url = `${apiUrl}Items/Items_Update/${currentItemId.value}`
        const data = new FormData()

        Object.keys(formData.value).forEach(key => {
          data.append(key, formData.value[key])
        })
        try {
          const response = await axios.put(url, data, { headers: { 'Content-Type': 'multipart/form-data', ...authHeader } })
          const index = itemsData.value.findIndex(item => item.itemID === currentItemId.value)
          if (index !== -1) {
            itemsData.value[index] = response.data
          }
          editDialog.value = false
        } catch (error) {
          console.error(error)
        }
      }else {
        alert('يجب ان يكون القسط اكبر او يساوي 1000.')
      }
    }else {
      alert('يجب ان يكون سعر الشراء اكبر او يساوي 1000.')
    }
  }else {
    alert('يجب ان يكون سعر البيع اكبر او يساوي 1000.')
  }
}

async function deleteItem() {
  if (itemIdToDelete.value) {
    try {
      const authHeader = getAuthHeaders()

      await axios.delete(`${apiUrl}Items/Items_Delete/${itemIdToDelete.value}`, { headers: authHeader })

      const index = itemsData.value.findIndex(item => item.itemID === itemIdToDelete.value)
      if (index !== -1) {
        itemsData.value.splice(index, 1)
      }
      confirmDeleteDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}

function exportToExcel() {
  const dataToExport = itemsData.value.map(item => ({
    'اسم المادة': item.itemName,
    'سعر البيع': item.itemPriceDenar,
    'سعر الشراء': item.itemCostDenar,
    'الكمية': item.quantity,
    'القسط': item.amountDayDenar,
    'عدد التنبية': item.notificationNumber,
    'الملاحظات': item.notes,
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Items")
  XLSX.writeFile(workbook, "items.xlsx")
}

// خصائص مُنسقة مع فواصل الآلاف وعرض العملة
const formattedItemPrice = computed({
  get() {
    return formData.value.itemPriceDenar !== ''
      ? Number(formData.value.itemPriceDenar).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, ''))

    formData.value.itemPriceDenar = isNaN(numeric) ? '' : numeric
  },
})

const formattedItemCost = computed({
  get() {
    return formData.value.itemCostDenar !== ''
      ? Number(formData.value.itemCostDenar).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, ''))

    formData.value.itemCostDenar = isNaN(numeric) ? '' : numeric
  },
})

const formattedAmountDay = computed({
  get() {
    return formData.value.amountDayDenar !== ''
      ? Number(formData.value.amountDayDenar).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, ''))

    formData.value.amountDayDenar = isNaN(numeric) ? '' : numeric
  },
})

const onNumberInput = e => {
  const key = e.key
  const value = e.target.value
  const cursorPosition = e.target.selectionStart

  const isNumber = /\d/.test(key)
  const isMinus = key === '-' && cursorPosition === 0 && !value.includes('-')

  if (!isNumber && !isMinus) {
    e.preventDefault()
  }
}
</script>

<template>
  <!-- عرض الإجماليات -->
  <!-- عرض الإجماليات -->
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
    <VForm>
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            اسم المادة
          </VLabel>
          <AppTextField
            v-model="filters.textSearch"
            placeholder="ابحث عن المادة"
            clearable
            clear-icon="tabler-x"
            prepend-inner-icon="tabler-box"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            المخزن
          </VLabel>
          <VAutocomplete
            v-model="filters.storeSearch"
            :items="storeOptions.map(s => ({ title: s.storeName, value: s.storeID }))"
            placeholder="اختر المخزن"
            clearable
            clear-icon="tabler-x"
            prepend-inner-icon="tabler-building-store"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            نوع العرض
          </VLabel>
          <VAutocomplete
            v-model="filters.showType"
            :items="['الجميع','المواد الحالية','المواد النافذة','الاكثر مبيعا','الاقل مبيعا','غير مباع','على وشك النفاذ']"
            clearable
            clear-icon="tabler-x"
            prepend-inner-icon="tabler-eye"
          />
        </VCol>
        <VCol
          cols="12"
          md="4"
          class="text-right d-flex align-center justify-end"
          style="margin-block-start: 21px;"
        >
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            prepend-icon="tabler-search"
            @click="fetchItems"
          >
            بحث
          </VBtn>
          <VBtn
            color="success"
            class="mx-4"
            prepend-icon="tabler-plus"
            @click="openAddDialog"
          >
            إضافة مادة
          </VBtn>
          <VBtn
            variant="tonal"
            color="success"
            prepend-icon="tabler-upload"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VCol>
      </VRow>

      <VRow>
        <VDataTable
          :headers="headers"
          :items="itemsData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <!-- تنسيق الأرقام -->
          <template #item.itemPriceDenar="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.itemPriceDenar) }}
            </div>
          </template>
          <template #item.itemCostDenar="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.itemCostDenar) }}
            </div>
          </template>
          <template #item.amountDayDenar="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.amountDayDenar) }}
            </div>
          </template>
          <template #item.priceTotalItem="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.priceTotalItem) }}
            </div>
          </template>
          <template #item.costTotalItem="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.costTotalItem) }}
            </div>
          </template>
          <template #item.itemPriceSales="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.numberOfItemsSales * item.itemPriceDenar) }}
            </div>
          </template>
          <template #item.itemCostSales="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.numberOfItemsSales * item.itemCostDenar) }}
            </div>
          </template>

          <!-- أزرار التعديل والحذف -->
          <template #item.update="{ item }">
            <VBtn
              color="primary"
              style="margin-block-end: 10px;"
              small
              prepend-icon="tabler-edit"
              @click="openEditDialog(item.itemID)"
            >
              تعديل
            </VBtn>
          </template>
          <template #item.delete="{ item }">
            <VBtn
              color="error"
              style="margin-block-end: 10px;"
              small
              prepend-icon="tabler-trash"
              @click="openDeleteDialog(item.itemID)"
            >
              حذف
            </VBtn>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>

  <!-- نافذة إضافة مادة -->
  <VDialog
    v-model="addDialog"
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
              icon="tabler-box-seam"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              إضافة مادة جديدة
            </h4>
            <span class="text-caption text-medium-emphasis">أدخل تفاصيل المادة الجديدة</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          size="small"
          @click="addDialog = false"
        >
          <VIcon
            icon="tabler-x"
            size="24"
          />
        </VBtn>
      </div>
      <VCardText>
        <VRow>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              اسم المادة
            </VLabel>
            <AppTextField
              v-model="formData.itemName"
              required
              prepend-inner-icon="tabler-box"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              المخزن
            </VLabel>
            <VAutocomplete
              v-model="formData.storeID"
              :items="storeOptions.map(s => ({ title: s.storeName, value: s.storeID }))"
              required
              prepend-inner-icon="tabler-building-store"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              سعر البيع
            </VLabel>
            <AppTextField
              v-model="formattedItemPrice"
              type="text"
              required
              prepend-inner-icon="tabler-coin"
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              سعر الشراء
            </VLabel>
            <AppTextField
              v-model="formattedItemCost"
              type="text"
              required
              prepend-inner-icon="tabler-coin"
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              القسط اليومي
            </VLabel>
            <AppTextField
              v-model="formattedAmountDay"
              type="text"
              required
              prepend-inner-icon="tabler-hourglass"
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              الكمية
            </VLabel>
            <AppTextField
              v-model="formData.quantity"
              type="number"
              required
              prepend-inner-icon="tabler-numbers"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              عدد التنبية
            </VLabel>
            <AppTextField
              v-model="formData.notificationNumber"
              type="number"
              required
              prepend-inner-icon="tabler-bell"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              الملاحظات
            </VLabel>
            <AppTextField
              v-model="formData.notes"
              prepend-inner-icon="tabler-note"
            />
          </VCol>
        </VRow>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          color="success"
          prepend-icon="tabler-x"
          @click="addDialog = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          @click="addItem"
        >
          إضافة
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- نافذة تعديل مادة -->
  <VDialog
    v-model="editDialog"
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
              تعديل مادة
            </h4>
            <span class="text-caption text-medium-emphasis">تحديث تفاصيل المادة</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          size="small"
          @click="editDialog = false"
        >
          <VIcon
            icon="tabler-x"
            size="24"
          />
        </VBtn>
      </div>
      <VCardText>
        <VRow>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              اسم المادة
            </VLabel>
            <AppTextField
              v-model="formData.itemName"
              required
              prepend-inner-icon="tabler-box"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              المخزن
            </VLabel>
            <VAutocomplete
              v-model="formData.storeID"
              :items="storeOptions.map(s => ({ title: s.storeName, value: s.storeID }))"
              required
              prepend-inner-icon="tabler-building-store"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              سعر البيع
            </VLabel>
            <AppTextField
              v-model="formattedItemPrice"
              type="text"
              required
              prepend-inner-icon="tabler-coin"
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              سعر الشراء
            </VLabel>
            <AppTextField
              v-model="formattedItemCost"
              type="text"
              required
              prepend-inner-icon="tabler-coin"
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              القسط اليومي
            </VLabel>
            <AppTextField
              v-model="formattedAmountDay"
              type="text"
              required
              prepend-inner-icon="tabler-hourglass"
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              الكمية
            </VLabel>
            <AppTextField
              v-model="formData.quantity"
              type="number"
              required
              prepend-inner-icon="tabler-numbers"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              عدد التنبية
            </VLabel>
            <AppTextField
              v-model="formData.notificationNumber"
              type="number"
              required
              prepend-inner-icon="tabler-bell"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              الملاحظات
            </VLabel>
            <AppTextField
              v-model="formData.notes"
              prepend-inner-icon="tabler-note"
            />
          </VCol>
        </VRow>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          color="success"
          prepend-icon="tabler-x"
          @click="editDialog = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          @click="updateItem"
        >
          تعديل
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- Dialog تأكيد الحذف -->
  <VDialog
    v-model="confirmDeleteDialog"
    max-width="400px"
    content-class="modern-dialog"
  >
    <VCard class="pa-2">
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
            <span class="text-caption text-medium-emphasis">هل أنت متأكد من أنك تريد حذف هذا العنصر؟</span>
          </div>
        </div>
      </div>
      <VCardText>
        <p class="mb-0">
          لا يمكن التراجع عن هذا الإجراء.
        </p>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="tonal"
          color="secondary"
          @click="confirmDeleteDialog = false"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="error"
          @click="deleteItem"
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

.text-no-wrap {
  white-space: nowrap;
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
