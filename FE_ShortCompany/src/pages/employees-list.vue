<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from '@core/components/app-form-elements/AppTextField.vue'
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات الموظفين
const employeesData = ref([])

// حالة التحميل
const loading = ref(false)

// نص البحث المحلي لتصفية الموظفين حسب الاسم
const searchText = ref('')

// بيانات الموظف للإضافة أو التعديل
const newEmployee = ref({
  employeeName: '',
  address: '',
  phoneNumber: '',
  notes: '',
})

// حالة الـ Dialogs
const dialogAdd = ref(false)
const dialogEdit = ref(false)
const dialogDeleteConfirm = ref(false)

// بيانات الموظف المختار للتعديل أو الحذف
const selectedEmployee = ref({})

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  
  return "لا يوجد"
}

// تعريف أعمدة جدول الموظفين المطلوبة فقط
const headers = [
  { title: 'الموظف', key: 'employeeName' },
  { title: 'العنوان', key: 'address' },
  { title: 'المحافظة', key: 'cityName' },
  { title: 'رقم الهاتف', key: 'phoneNumber' },
  { title: 'الحساب', key: 'amountAccount' },
]



// حساب إجماليات بيانات الموظفين
const totals = computed(() => [
  {
    icon: 'tabler-users', // عدد الموظفين الكلي
    value: employeesData.value.length,
    title: 'عدد الموظفين الكلي',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-cash', // مبلغ الحساب الكلي
    value: formattedNumber(
      employeesData.value.reduce((sum, emp) => sum + (emp.amountAccount || 0), 0),
    ),
    title: 'مبلغ الحساب الكلي',
    color: "info",
    gradient: "linear-gradient(135deg, #00b09b 0%, #96c93d 100%)",
  },
])

// فلترة الموظفين محلياً حسب اسم الموظف
const filteredEmployees = computed(() => {
  if (!searchText.value) return employeesData.value
  
  return employeesData.value.filter(emp =>
    emp.employeeName && emp.employeeName.toLowerCase().includes(searchText.value.toLowerCase()),
  )
})

// دالة جلب بيانات الموظفين من API
async function fetchEmployees() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Employees/Employees_GetAll`, { headers: authHeader })

    employeesData.value = response.data.map(emp => ({
      employeeID: emp.employeeID,
      employeeName: emp.employeeName,
      address: emp.address,
      phoneNumber: emp.phoneNumber,
      cityName: emp.cityName,
      amountAccount: emp.amountAccount,
    }))
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير بيانات الموظفين إلى Excel
function exportToExcel() {
  const dataToExport = filteredEmployees.value.map(emp => ({
    'الموظف': emp.employeeName || 'لا يوجد',
    'العنوان': emp.address || 'لا يوجد',
    'المحافظة': emp.cityName || 'لا يوجد',
    'رقم الهاتف': emp.phoneNumber || 'لا يوجد',
    'الحساب': emp.amountAccount || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Employees")
  XLSX.writeFile(workbook, "Employees.xlsx")
}

// دالة إضافة الموظف
async function addEmployee() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.post(`${apiUrl}Employees/Employees_Create`, newEmployee.value, { headers: authHeader })

    await fetchEmployees()  // تحديث قائمة الموظفين بعد الإضافة
    dialogAdd.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}

// دالة تعديل الموظف
async function editEmployee() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.put(`${apiUrl}Employees/Employees_Update/${selectedEmployee.value}`, { ...selectedEmployee.value, ...newEmployee.value }, { headers: authHeader })

    await fetchEmployees()  // تحديث قائمة الموظفين بعد التعديل
    dialogEdit.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}

// دالة حذف الموظف
async function deleteEmployee() {
  try {
    console.log(selectedEmployee.value.employeeID)

    const authHeader = getAuthHeaders()
    const response = await axios.delete(`${apiUrl}Employees/Employees_Delete/${selectedEmployee.value}`, { headers: authHeader })

    await fetchEmployees()  // تحديث قائمة الموظفين بعد الحذف
    dialogDeleteConfirm.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}


function openDeleteDialog(item) {
  console.log(item)
  selectedEmployee.value = item.employeeID
  dialogDeleteConfirm.value= true
}


function openEditDialog(item) {
  selectedEmployee.value = item.employeeID
  newEmployee.value = { ...item }
  dialogEdit.value = true
}





onMounted(() => {
  fetchEmployees()
})

const colSlots = {
  employeeName: 'item.employeeName',
  address: 'item.address',
  cityName: 'item.cityName',
  phoneNumber: 'item.phoneNumber',
  amountAccount: 'item.amountAccount',
  update: 'item.update',
  delete: 'item.delete',
}
</script>

<template>
  <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, i) in totals"
      :key="i"
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
      <!-- فلترة محلية حسب اسم الموظف -->
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            بحث بالموظف
          </VLabel>
          <AppTextField
            v-model="searchText"
            placeholder="ادخل اسم الموظف"
            clearable
            prepend-inner-icon="tabler-user-search"
          />
        </VCol>
        <VRow style="margin-block-start: 32px;margin-inline-end: 10px;">
          <VBtn
            color="success"
            style="margin-inline-start: 20px;"
            prepend-icon="tabler-upload"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
          <!--
            <VBtn
            color="primary"
            style="margin-inline-start: 20px;"
            prepend-icon="tabler-plus"
            @click="dialogAdd = true"
            >
            إضافة موظف
            </VBtn> 
          -->
        </VRow>
      </VRow>


      <!-- عرض بيانات الموظفين -->
      <VRow>
        <VDataTable
          :headers="headers"
          :items="filteredEmployees"
          :items-per-page="50"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <template #[colSlots.employeeName]="{ item }">
            <div>
              {{ item.employeeName || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.address]="{ item }">
            <div>
              {{ item.address || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.cityName]="{ item }">
            <div>
              {{ item.cityName || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.phoneNumber]="{ item }">
            <div>
              {{ item.phoneNumber || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.amountAccount]="{ item }">
            <div>
              {{ formattedNumber(item.amountAccount) }}
            </div>
          </template>
          <!-- أزرار التعديل والحذف داخل كل صف -->
          <template #[colSlots.update]>
            <!--
              <VBtn
              style="margin-block-end: 10px;"
              color="primary"
              small
              prepend-icon="tabler-edit"
              @click="openEditDialog(item)"
              >
              تعديل
              </VBtn> 
            -->
          </template>
          <template #[colSlots.delete]>
            <!--
              <VBtn
              style="margin-block-end: 10px;"
              color="error"
              small
              prepend-icon="tabler-trash"
              @click="openDeleteDialog(item)"
              >
              حذف
              </VBtn> 
            -->
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>

  <VDialog
    v-model="dialogAdd"
    max-width="600px"
    content-class="modern-dialog"
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
              إضافة موظف جديد
            </h4>
            <span class="text-caption text-medium-emphasis">أدخل بيانات الموظف الجديد</span>
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
            اسم الموظف
          </VLabel>
          <AppTextField
            v-model="newEmployee.employeeName"
            prepend-inner-icon="tabler-user"
            required
          />
          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            العنوان
          </VLabel>
          <AppTextField
            v-model="newEmployee.address"
            prepend-inner-icon="tabler-home"
          />
          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            رقم الهاتف
          </VLabel>
          <AppTextField
            v-model="newEmployee.phoneNumber"
            prepend-inner-icon="tabler-phone"
          />
          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            الملاحظات
          </VLabel>
          <AppTextField
            v-model="newEmployee.notes"
            prepend-inner-icon="tabler-note"
          />
        </VForm>
      </VCardText>
      <VCardActions>
        <VBtn
          variant="tonal"
          color="secondary"
          prepend-icon="tabler-x"
          @click="dialogAdd = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          prepend-icon="tabler-check"
          color="primary"
          @click="addEmployee"
        >
          إضافة
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <VDialog
    v-model="dialogEdit"
    max-width="600px"
    content-class="modern-dialog"
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
              تعديل بيانات الموظف
            </h4>
            <span class="text-caption text-medium-emphasis">تحديث تفاصيل الموظف</span>
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
            اسم الموظف
          </VLabel>
          <AppTextField
            v-model="newEmployee.employeeName"
            prepend-inner-icon="tabler-user"
            required
          />
          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            العنوان
          </VLabel>
          <AppTextField
            v-model="newEmployee.address"
            prepend-inner-icon="tabler-home"
          />
          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            رقم الهاتف
          </VLabel>
          <AppTextField
            v-model="newEmployee.phoneNumber"
            prepend-inner-icon="tabler-phone"
          />
          <VLabel
            style="margin-block-start: 10px;"
            class="mb-2"
          >
            الملاحظات
          </VLabel>
          <AppTextField
            v-model="newEmployee.notes"
            prepend-inner-icon="tabler-note"
          />
        </VForm>
      </VCardText>
      <VCardActions>
        <VBtn
          variant="tonal"
          color="secondary"
          prepend-icon="tabler-x"
          @click="dialogEdit = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          @click="editEmployee"
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
            <span class="text-caption text-medium-emphasis">هل أنت متأكد من أنك تريد حذف هذا الموظف؟</span>
          </div>
        </div>
      </div>
      <VCardText>
        <p class="mb-0">
          لا يمكن التراجع عن هذا الإجراء.
        </p>
      </VCardText>
      <VCardActions>
        <VBtn
          variant="tonal"
          color="secondary"
          prepend-icon="tabler-x"
          @click="dialogDeleteConfirm = false"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="error"
          prepend-icon="tabler-trash" 
          @click="deleteEmployee"
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
