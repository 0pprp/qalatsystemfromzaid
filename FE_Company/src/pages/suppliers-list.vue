<script setup>
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'
import * as XLSX from 'xlsx'

import ModernStatCard from "@/components/ModernStatCard.vue"
import { getAuthHeaders } from '@/services/tokenService'

const apiUrl = localStorage.getItem('LinkCity')
const suppliersData = ref([])
const loading = ref(false)
const formattedNumber = num => (num ? num.toLocaleString() + "  دع  " : '0')

const filters = ref({
  textSearch: '',
})

const headers = [
  { title: 'اسم المورد', key: 'supplierName' },
  { title: 'العنوان', key: 'address' },
  { title: 'المدينة', key: 'cityName' },
  { title: 'رقم الهاتف', key: 'phoneNumber' },
  { title: 'مبلغ الحساب', key: 'amountAccount' },
  { title: 'الملاحظات', key: 'notes' },
  { title: '', key: 'update' },
  { title: '', key: 'delete' },
]

const totals = computed(() => [
  {
    icon: 'tabler-users', // عدد الموردين
    value: suppliersData.value.length,
    title: 'عدد الموردين',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-cash', // إجمالي مبلغ الحساب (يظهر فقط في الإجماليات)
    value: formattedNumber(suppliersData.value.reduce((sum, supplier) => sum + supplier.amountAccount, 0)),
    title: 'مجموع مبلغ الحساب',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
])

const formData = ref({
  supplierName: '',
  address: '',
  phoneNumber: '',
  notes: '',
})

const addDialog = ref(false)
const editDialog = ref(false)
const confirmDeleteDialog = ref(false)
const supplierIdToDelete = ref(null)
const currentSupplierId = ref(null)

async function fetchSuppliers() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const { textSearch } = filters.value

    const response = await axios.get(
      `${apiUrl}Suppliers/Suppliers_GetAll/${textSearch || 'null'}`,
      { headers: authHeader },
    )

    suppliersData.value = response.data
  } catch (error) {
    console.log(error)
  } finally {
    loading.value = false
  }
}

onMounted(fetchSuppliers)

function openAddDialog() {
  formData.value = {
    supplierName: '',
    address: '',
    phoneNumber: '',
    notes: '',
  }
  currentSupplierId.value = null
  addDialog.value = true
}

function openEditDialog(supplierID) {
  const supplier = suppliersData.value.find(item => item.supplierID === supplierID)
  if (supplier) {
    formData.value = { ...supplier }
    currentSupplierId.value = supplier.supplierID
    editDialog.value = true
  }
}

function openDeleteDialog(supplierID) {
  supplierIdToDelete.value = supplierID
  confirmDeleteDialog.value = true
}

async function addSupplier() {
  const authHeader = getAuthHeaders()
  const url = `${apiUrl}Suppliers/Suppliers_Create`
  const data = new FormData()

  Object.keys(formData.value).forEach(key => {
    data.append(key, formData.value[key])
  })
  try {
    const response = await axios.post(url, data, { headers: { 'Content-Type': 'multipart/form-data', ...authHeader } })

    suppliersData.value.push(response.data)
    addDialog.value = false
  } catch (error) {
    console.error(error)
  }
}

const limitPhoneLength = event => {
  let value = event.target.value.toString()
  if (value.length > 11) {
    value = value.slice(0, 11)
    event.target.value = value
  }
  formData.value.phoneNumber = value
}

async function updateSupplier() {
  const authHeader = getAuthHeaders()
  const url = `${apiUrl}Suppliers/Suppliers_Update/${currentSupplierId.value}`
  const data = new FormData()

  Object.keys(formData.value).forEach(key => {
    data.append(key, formData.value[key])
  })
  try {
    const response = await axios.put(url, data, { headers: { 'Content-Type': 'multipart/form-data', ...authHeader } })
    const index = suppliersData.value.findIndex(supplier => supplier.supplierID === currentSupplierId.value)
    if (index !== -1) {
      suppliersData.value[index] = response.data
    }
    editDialog.value = false
  } catch (error) {
    console.error(error)
  }
}

async function deleteSupplier() {
  if (supplierIdToDelete.value) {
    try {
      const authHeader = getAuthHeaders()

      await axios.delete(`${apiUrl}Suppliers/Suppliers_Delete/${supplierIdToDelete.value}`, { headers: authHeader })

      const index = suppliersData.value.findIndex(supplier => supplier.supplierID === supplierIdToDelete.value)
      if (index !== -1) {
        suppliersData.value.splice(index, 1)
      }
      confirmDeleteDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}

function exportToExcel() {
  const dataToExport = suppliersData.value.map(supplier => ({
    'اسم المورد': supplier.supplierName,
    'العنوان': supplier.address,
    'المدينة': supplier.cityName,
    'رقم الهاتف': supplier.phoneNumber,
    'مبلغ الحساب': supplier.amountAccount,
    'الملاحظات': supplier.notes,
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Suppliers")
  XLSX.writeFile(workbook, "suppliers.xlsx")
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
        <!-- حقل البحث عن المورد -->
        <VCol
          md="4"
          cols="12"
        >
          <VLabel class="mb-2">
            اسم المورد
          </VLabel>
          <AppTextField
            v-model="filters.textSearch"
            placeholder="ابحث عن المورد"
            clearable
            clear-icon="tabler-x"
            prepend-inner-icon="tabler-user-search"
          />
        </VCol>
        <!-- أزرار البحث والإضافة والتصدير -->
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
            @click="fetchSuppliers"
          >
            بحث
          </VBtn>
          <VBtn
            color="success"
            class="mx-4"
            prepend-icon="tabler-user-plus"
            @click="openAddDialog"
          >
            إضافة مورد
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

      <!-- جدول الموردين -->
      <VRow class="d-flex flex-column">
        <VDataTable
          :headers="headers"
          :items="suppliersData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <!-- تنسيق رصيد الحساب -->
          <template #item.amountAccount="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.amountAccount) }}
            </div>
          </template>
          <!-- أزرار التعديل والحذف -->
          <template #item.update="{ item }">
            <VBtn
              color="primary"
              style="margin-block-end: 10px;"
              small
              prepend-icon="tabler-edit"
              @click="openEditDialog(item.supplierID)"
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
              @click="openDeleteDialog(item.supplierID)"
            >
              حذف
            </VBtn>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>

  <!-- حوار إضافة مورد -->
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
              icon="tabler-user-plus"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              إضافة مورد جديد
            </h4>
            <span class="text-caption text-medium-emphasis">أدخل تفاصيل المورد الجديد</span>
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
              اسم المورد
            </VLabel>
            <AppTextField
              v-model="formData.supplierName"
              required
              prepend-inner-icon="tabler-user"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              العنوان
            </VLabel>
            <AppTextField
              v-model="formData.address"
              required
              prepend-inner-icon="tabler-map-pin"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              رقم الهاتف
            </VLabel>
            <AppTextField
              v-model="formData.phoneNumber"
              required
              prepend-inner-icon="tabler-phone"
              @input="limitPhoneLength"
              @keypress="e => !/[0-9]/.test(e.key) && e.preventDefault()"
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
          variant="tonal"
          color="secondary"
          @click="addDialog = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          @click="addSupplier"
        >
          إضافة
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- حوار تعديل مورد -->
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
              تعديل مورد
            </h4>
            <span class="text-caption text-medium-emphasis">تحديث بيانات المورد</span>
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
              اسم المورد
            </VLabel>
            <AppTextField
              v-model="formData.supplierName"
              required
              prepend-inner-icon="tabler-user"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              العنوان
            </VLabel>
            <AppTextField
              v-model="formData.address"
              required
              prepend-inner-icon="tabler-map-pin"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              رقم الهاتف
            </VLabel>
            <AppTextField
              v-model="formData.phoneNumber"
              required
              prepend-inner-icon="tabler-phone"
              @input="limitPhoneLength"
              @keypress="e => !/[0-9]/.test(e.key) && e.preventDefault()"
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
          variant="tonal"
          color="secondary"
          @click="editDialog = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          @click="updateSupplier"
        >
          تعديل
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- تأكيد الحذف -->
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
            <span class="text-caption text-medium-emphasis">هل أنت متأكد من حذف هذا المورد؟</span>
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
          @click="deleteSupplier"
        >
          حذف
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
