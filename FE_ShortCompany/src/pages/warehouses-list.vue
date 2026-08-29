<script setup>
import ModernStatCard from "@/components/ModernStatCard.vue"
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'
import * as XLSX from 'xlsx'

import { getAuthHeaders } from '@/services/tokenService'

const apiUrl = localStorage.getItem('LinkCity')
const storesData = ref([])
const loading = ref(false)

const formattedNumber = num => (num ? num.toLocaleString() + "  دع  " : '0')

const filters = ref({
  textSearch: '',
})


const headers = [
  { title: 'اسم المخزن', key: 'storeName' },
  { title: 'الموقع', key: 'storePlace' },
  { title: 'المدينة', key: 'cityName' },
  { title: 'سعر البيع', key: 'totalPrice' },
  { title: 'سعر الشراء', key: 'totalCost' },
  { title: 'عدد الأنواع', key: 'numberOfTypes' },
  { title: 'عدد العناصر', key: 'numberOfItems' },
  { title: 'عدد المباع', key: 'numberOfItemsSales' },
  { title: 'عدد الشراء', key: 'numberOfItemBuy' },
  { title: 'كلفة البيع الحالية', key: 'costSalesItemsCurrent' },
  { title: 'كلفة الشراء الحالية', key: 'costBuyItemsCurrent' },
]


const totals = computed(() => [
  {
    icon: 'tabler-building',
    value: storesData.value.length,
    title: 'عدد المخازن',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-tag',
    value: formattedNumber(storesData.value.reduce((sum, store) => sum + store.totalPrice, 0)),
    title: 'سعر البيع',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
  {
    icon: 'tabler-shopping-cart',
    value: formattedNumber(storesData.value.reduce((sum, store) => sum + store.totalCost, 0)),
    title: 'سعر الشراء',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-archive',
    value: storesData.value.reduce((sum, store) => sum + store.numberOfTypes, 0),
    title: 'عدد الأنواع',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
  {
    icon: 'tabler-box',
    value: storesData.value.reduce((sum, store) => sum + store.numberOfItems, 0),
    title: 'عدد العناصر',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
  {
    icon: 'tabler-box',
    value: storesData.value.reduce((sum, store) => sum + store.numberOfItemsSales, 0),
    title: 'عدد المباع',
    color: "secondary",
    gradient: "linear-gradient(135deg, #667db6 0%, #0082c8 100%, #0082c8 100%, #667db6 100%)",
  },
  {
    icon: 'tabler-box',
    value: storesData.value.reduce((sum, store) => sum + store.numberOfItemBuy, 0),
    title: 'عدد الشراء',
    color: "warning",
    gradient: "linear-gradient(135deg, #F2994A 0%, #F2C94C 100%)",
  },
  {
    icon: 'tabler-archive',
    value: formattedNumber(storesData.value.reduce((sum, store) => sum + store.costSalesItemsCurrent, 0)),
    title: 'كلفة البيع الحالية',
    color: "success",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
  {
    icon: 'tabler-archive',
    value: formattedNumber(storesData.value.reduce((sum, store) => sum + store.costBuyItemsCurrent, 0)),
    title: 'كلفة الشراء الحالية',
    color: "error",
    gradient: "linear-gradient(135deg, #FF512F 0%, #DD2476 100%)",
  },
])



const formData = ref({
  storeID: '',
  storeName: '',
  storePlace: '',
  notes: '',
  cityName: '',
  totalPrice: '',
  totalCost: '',
  costSalesItemsCurrent: '',
  costBuyItemsCurrent: '',
  numberOfTypes: '',
  numberOfItems: '',
  numberOfItemsSales: '',
})

const addDialog = ref(false)
const editDialog = ref(false)
const confirmDeleteDialog = ref(false)
const storeIdToDelete = ref(null)
const currentStoreId = ref(null)

async function fetchStores() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const { textSearch } = filters.value

    const response = await axios.get(
      `${apiUrl}Stores/Stores_GetAll/${textSearch || 'null'}`,
      {
        headers: authHeader,
      },
    )

    storesData.value = response.data
  } catch (error) {
    console.log(error)
  } finally {
    loading.value = false
  }
}

onMounted(fetchStores)

function openAddDialog() {
  formData.value = {
    storeID: '',
    storeName: '',
    storePlace: '',
    notes: '',
    cityName: '',
    totalPrice: '',
    totalCost: '',
    costSalesItemsCurrent: '',
    costBuyItemsCurrent: '',
    numberOfTypes: '',
    numberOfItems: '',
    numberOfItemsSales: '',
  }
  currentStoreId.value = null
  addDialog.value = true
}

function openEditDialog(storeID) {
  const store = storesData.value.find(item => item.storeID === storeID)
  if (store) {
    formData.value = { ...store }
    currentStoreId.value = store.storeID
    editDialog.value = true
  }
}

function openDeleteDialog(storeID) {
  storeIdToDelete.value = storeID
  confirmDeleteDialog.value = true
}

async function addStore() {
  const authHeader = getAuthHeaders()
  const url = `${apiUrl}Stores/Stores_Create`
  const data = new FormData()

  Object.keys(formData.value).forEach(key => {
    data.append(key, formData.value[key])
  })
  try {
    const response = await axios.post(url, data, { headers: { 'Content-Type': 'multipart/form-data', ...authHeader } })

    storesData.value.push(response.data)
    addDialog.value = false
  } catch (error) {
    console.error(error)
  }
}

async function updateStore() {
  const authHeader = getAuthHeaders()
  const url = `${apiUrl}Stores/Stores_Update/${currentStoreId.value}`
  const data = new FormData()

  Object.keys(formData.value).forEach(key => {
    data.append(key, formData.value[key])
  })
  try {
    const response = await axios.put(url, data, { headers: { 'Content-Type': 'multipart/form-data', ...authHeader } })
    const index = storesData.value.findIndex(store => store.storeID === currentStoreId.value)
    if (index !== -1) {
      storesData.value[index] = response.data
    }
    editDialog.value = false
  } catch (error) {
    console.error(error)
  }
}

async function deleteStore() {
  if (storeIdToDelete.value) {
    try {
      const authHeader = getAuthHeaders()

      await axios.delete(`${apiUrl}Stores/Stores_Delete/${storeIdToDelete.value}`, { headers: authHeader })

      const index = storesData.value.findIndex(store => store.storeID === storeIdToDelete.value)
      if (index !== -1) {
        storesData.value.splice(index, 1)
      }
      confirmDeleteDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}

function exportToExcel() {
  const dataToExport = storesData.value.map(store => ({
    'اسم المخزن': store.storeName,
    'الموقع': store.storePlace,
    'المدينة': store.cityName,
    'سعر البيع': store.totalPrice,
    'سعر الشراء': store.totalCost,
    'عدد الأنواع': store.numberOfTypes,
    'عدد العناصر': store.numberOfItems,
    'عدد المباع': store.numberOfItemsSales,
    'كلفة البيع الحالية': store.costSalesItemsCurrent,
    'كلفة الشراء الحالية': store.costBuyItemsCurrent,
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Stores")
  XLSX.writeFile(workbook, "stores.xlsx")
}

const colSlots = {
  totalPrice: 'item.totalPrice',
  totalCost: 'item.totalCost',
  costSalesItemsCurrent: 'item.costSalesItemsCurrent',
  costBuyItemsCurrent: 'item.costBuyItemsCurrent',
  update: 'item.update',
  delete: 'item.delete',
}
</script>

<template>
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
          md="4"
          cols="12"
        >
          <VLabel class="mb-2">
            اسم المخزن
          </VLabel>
          <AppTextField
            v-model="filters.textSearch"
            placeholder="ابحث عن المخزن"
            clearable
            clear-icon="tabler-x"
            prepend-inner-icon="tabler-building-store"
          />
        </VCol>

        <VCol
          md="4"
          cols="12"
          class="text-right d-flex align-center justify-end"
          style="margin-block-start: 20px;"
        >
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            prepend-icon="tabler-search"
            @click="fetchStores"
          >
            بحث
          </VBtn>
          <!--
            <VBtn
            color="success"
            class="mx-2"
            prepend-icon="tabler-plus"
            @click="openAddDialog"
            >
            إضافة مخزن
            </VBtn> 
          -->
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

      <VRow class="d-flex flex-column">
        <VDataTable
          :headers="headers"
          :items="storesData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <template #[colSlots.totalPrice]="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.totalPrice) }}
            </div>
          </template>
          <template #[colSlots.totalCost]="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.totalCost) }}
            </div>
          </template>
          <template #[colSlots.costSalesItemsCurrent]="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.costSalesItemsCurrent) }}
            </div>
          </template>
          <template #[colSlots.costBuyItemsCurrent]="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.costBuyItemsCurrent) }}
            </div>
          </template>

          <!-- أزرار التعديل والحذف بألوان وخلفيات -->
          <template #[colSlots.update]>
            <!--
              <VBtn
              color="primary"
              small
              style="margin-block-end: 10px;"
              prepend-icon="tabler-edit"
              @click="openEditDialog(item.storeID)"
              >
              تعديل
              </VBtn> 
            -->
          </template>

          <template #[colSlots.delete]>
            <!--
              <VBtn
              color="error"
              small
              style="margin-block-end: 10px;"
              prepend-icon="tabler-trash"
              @click="openDeleteDialog(item.storeID)"
              >
              حذف
              </VBtn> 
            -->
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>

  <!-- إضافة مخزن -->
  <VDialog
    v-model="addDialog"
    max-width="700px"
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
              icon="tabler-building-warehouse"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              إضافة مخزن جديد
            </h4>
            <span class="text-caption text-medium-emphasis">أدخل بيانات المخزن الجديد أدناه</span>
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

      <VCardText class="pa-4">
        <VRow>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2 font-weight-medium">
              اسم المخزن
            </VLabel>
            <AppTextField
              v-model="formData.storeName"
              prepend-inner-icon="tabler-building-store"
              placeholder="اسم المخزن"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2 font-weight-medium">
              الموقع
            </VLabel>
            <AppTextField
              v-model="formData.storePlace"
              prepend-inner-icon="tabler-map-pin"
              placeholder="الموقع"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2 font-weight-medium">
              الملاحظات
            </VLabel>
            <AppTextField
              v-model="formData.notes"
              prepend-inner-icon="tabler-note"
              placeholder="الملاحظات"
            />
          </VCol>
        </VRow>

        <VDivider class="my-6" />

        <div class="d-flex justify-end gap-3">
          <VBtn
            variant="outlined"
            color="secondary"
            class="action-btn"
            height="44"
            prepend-icon="tabler-x"
            @click="addDialog = false"
          >
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            class="action-btn"
            height="44"
            elevation="4"
            prepend-icon="tabler-check"
            @click="addStore"
          >
            إضافة
          </VBtn>
        </div>
      </VCardText>
    </VCard>
  </VDialog>

  <!-- تعديل مخزن -->
  <VDialog
    v-model="editDialog"
    max-width="700px"
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
              تعديل بيانات المخزن
            </h4>
            <span class="text-caption text-medium-emphasis">تحديث بيانات المخزن المحدد</span>
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

      <VCardText class="pa-4">
        <VRow>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2 font-weight-medium">
              اسم المخزن
            </VLabel>
            <AppTextField
              v-model="formData.storeName"
              prepend-inner-icon="tabler-building-store"
              placeholder="اسم المخزن"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2 font-weight-medium">
              الموقع
            </VLabel>
            <AppTextField
              v-model="formData.storePlace"
              prepend-inner-icon="tabler-map-pin"
              placeholder="الموقع"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2 font-weight-medium">
              الملاحظات
            </VLabel>
            <AppTextField
              v-model="formData.notes"
              prepend-inner-icon="tabler-note"
              placeholder="الملاحظات"
            />
          </VCol>
        </VRow>

        <VDivider class="my-6" />

        <div class="d-flex justify-end gap-3">
          <VBtn
            variant="outlined"
            color="secondary"
            class="action-btn"
            height="44"
            prepend-icon="tabler-x"
            @click="editDialog = false"
          >
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            class="action-btn"
            height="44"
            elevation="4"
            prepend-icon="tabler-check"
            @click="updateStore"
          >
            تعديل
          </VBtn>
        </div>
      </VCardText>
    </VCard>
  </VDialog>

  <!-- تأكيد الحذف -->
  <VDialog
    v-model="confirmDeleteDialog"
    max-width="400px"
    content-class="modern-dialog"
  >
    <VCard class="pa-4 text-center">
      <VCardText class="d-flex flex-column align-center justify-center">
        <VAvatar
          color="error"
          variant="tonal"
          size="80"
          class="mb-4"
        >
          <VIcon
            icon="tabler-alert-triangle"
            size="48"
          />
        </VAvatar>
        <h3 class="text-h5 font-weight-bold mb-2">
          تأكيد الحذف
        </h3>
        <p class="text-medium-emphasis">
          هل أنت متأكد من أنك تريد حذف هذا المخزن؟ لا يمكن التراجع عن هذا الإجراء.
        </p>
      </VCardText>
      <VCardActions class="justify-center gap-3">
        <VBtn
          variant="tonal"
          color="secondary"
          class="action-btn"
          height="44"
          prepend-icon="tabler-x"
          @click="confirmDeleteDialog = false"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="error"
          class="action-btn"
          height="44"
          elevation="2"
          prepend-icon="tabler-trash"
          @click="deleteStore"
        >
          حذف نهائي
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>


<style scoped>
.v-btn { margin-block-start: 10px; }
.v-btn + .v-btn { margin-inline-start: 10px; }
.text-no-wrap { white-space: nowrap; }
.text-right { text-align: end; }
.d-flex { display: flex; }
.align-center { align-items: center; }
.justify-end { justify-content: flex-end; }

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
