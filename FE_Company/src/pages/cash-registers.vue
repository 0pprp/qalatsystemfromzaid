<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from '@core/components/app-form-elements/AppTextField.vue'
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات الخزائن النقدية
const boxData = ref([])
const selectedBoxes = ref([])
const totalAmount = ref('')


// بيانات إضافة المبالغ وعمليات الصرف والنقل
const newTransaction = ref({
  amount: 0,
  boxID: null,
  notes: '',
  type: '', // "add", "withdraw", or "transfer"
  destinationBoxID: null, // Only used for transfers
})

// حالة التحميل
const loading = ref(false)

// نص البحث المحلي لتصفية الخزائن حسب اسم الخزينة
const searchText = ref('')
const viewMode = ref('grid')

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "

  return "لا يوجد"
}

// تعريف أعمدة الجدول (أسماء الحقول تبدأ بحرف صغير)
const headers = [
  { title: 'الخزينة', key: 'boxName' },
  { title: 'المبلغ', key: 'amountDenar' },
  { title: 'اضافة', key: 'add' },
  { title: 'سحب', key: 'withdraw' },
  { title: 'نقل', key: 'move' },
  { title: 'التعديل', key: 'update' },
  { title: 'الحذف', key: 'delete' },
]


// إجماليات البيانات
const totals = computed(() => [
  {
    icon: 'tabler-cash',
    value: boxData.value.length,
    title: 'عدد الخزائن',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-cash',
    value: formattedNumber(
      boxData.value.reduce((sum, box) => sum + (box.amountDenar || 0), 0),
    ),
    title: 'المبلغ الكلي',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
])

// دالة جلب بيانات الخزائن من API
async function fetchBoxes() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Accounts/Boxs_GetAll`, { headers: authHeader })

    boxData.value = response.data.map(item => ({
      boxID: item.boxID,
      boxName: item.boxName,
      boxState: item.boxState,
      amountDenar: item.amountDenar,
    }))
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير بيانات الخزائن إلى Excel
function exportToExcel() {
  const dataToExport = filteredBoxes.value.map(box => ({
    'الخزينة': box.boxName || 'لا يوجد',
    'المبلغ': box.amountDenar || 'لا يوجد',
    'الحالة': box.boxState ? 'مفعل' : 'غير مفعل',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Box")
  XLSX.writeFile(workbook, "Box.xlsx")
}

// فلترة الخزائن محلياً حسب اسم الخزينة
const filteredBoxes = computed(() => {
  let result = boxData.value
  
  if (searchText.value) {
    result = result.filter(box =>
      box.boxName && box.boxName.toLowerCase().includes(searchText.value.toLowerCase()),
    )
  }
  
  // ترتيب الخزائن حسب boxID تصاعدياً
  return result.sort((a, b) => a.boxID - b.boxID)
})

// فتح Dialog لإضافة خزينة نقدية
const dialogAdd = ref(false)

// فتح Dialog لتعديل خزينة نقدية
const dialogEdit = ref(false)

// فتح Dialog لتأكيد الحذف
const dialogDeleteConfirm = ref(false)

// فتح Dialog لإضافة مبلغ إلى خزينة
const dialogAddMoney = ref(false)

// فتح Dialog لسحب مبلغ من خزينة
const dialogWithdrawMoney = ref(false)

// فتح Dialog لنقل المبلغ بين الخزائن
const dialogTransferMoney = ref(false)

const dialogTransferSelectBox = ref(false)

// بيانات الموظف المختار للتعديل أو الحذف
const selectedBox = ref({})

// دالة إضافة خزينة نقدية
async function addBox() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.post(`${apiUrl}Accounts/Boxs_Create`, newTransaction.value, { headers: authHeader })

    await fetchBoxes()
    dialogAdd.value = false
  } catch (error) {
    console.error(error)
  }
}

// دالة تعديل خزينة نقدية
async function editBox() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.put(`${apiUrl}Accounts/Boxs_Update/${selectedBox.value.boxID}`, newTransaction.value, { headers: authHeader })

    await fetchBoxes()  // تحديث قائمة الخزائن بعد التعديل
    dialogEdit.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}

// دالة حذف خزينة نقدية
async function deleteBox() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.delete(`${apiUrl}Accounts/Boxs_Delete/${selectedBox.value.boxID}`, { headers: authHeader })

    await fetchBoxes()  // تحديث قائمة الخزائن بعد الحذف
    dialogDeleteConfirm.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}

// دالة إضافة مبلغ إلى خزينة
async function addToBox_Create() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.post(`${apiUrl}Accounts/AddToBox_Create`, newTransaction.value, { headers: authHeader })

    await fetchBoxes()  // تحديث قائمة الخزائن بعد إضافة المبلغ
    dialogAddMoney.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}


// دالة سحب مبلغ من خزينة
async function withdrawalFromBox_Create() {
  try {
    if (newTransaction.value.amount > selectedBox.value.amountDenar) {
      alert('المبلغ المطلوب سحبه أكبر من المبلغ الموجود في الخزينة!')
      
      return
    }

    const authHeader = getAuthHeaders()
    const response = await axios.post(`${apiUrl}Accounts/WithdrawalFromBox_Create`, newTransaction.value, { headers: authHeader })

    await fetchBoxes()  // تحديث قائمة الخزائن بعد سحب المبلغ
    dialogWithdrawMoney.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}


async function transferBoxSelect_Create() {
  try {
    const destinationBoxID = newTransaction.value.destinationBoxID
    if (destinationBoxID === null || destinationBoxID === undefined) {
      alert('لم يتم تحديد الخزينة الوجهة بشكل صحيح.')
      
      return
    }

    for (const box of selectedBoxes.value.filter(c => c.amountDenar > 0)) {
      const data = {
        destinationBoxID: newTransaction.value.destinationBoxID,
        amount: box.amountDenar,
        boxID: box.boxID,
        notes: newTransaction.value.notes,
        type: 'transfer',
      }

      const authHeader = getAuthHeaders()
      const response = await axios.post(`${apiUrl}Accounts/TransferBoxs_Create`, data, { headers: authHeader })
    }

    await fetchBoxes()
    dialogTransferSelectBox.value = false
  } catch (error) {
    alert('حدث خطأ غير متوقع!')
    console.error(error)
  }
}


// دالة نقل مبلغ بين الخزائن
async function transferBoxs_Create() {
  try {
    if (newTransaction.value.amount > selectedBox.value.amountDenar) {
      alert('المبلغ المطلوب نقله أكبر من المبلغ الموجود في الخزينة المصدر!')
      
      return
    }

    const authHeader = getAuthHeaders()
    const response = await axios.post(`${apiUrl}Accounts/TransferBoxs_Create`, newTransaction.value, { headers: authHeader })

    await fetchBoxes()  // تحديث قائمة الخزائن بعد النقل
    dialogTransferMoney.value = false  // إغلاق الـ Dialog
  } catch (error) {
    console.error(error)
  }
}


// فتح Dialog إضافة خزينة نقدية
function openAddDialog() {
  dialogAdd.value = true
}


function moveBox(){
  totalAmount.value =formattedNumber(selectedBoxes.value .filter(c => c.amountDenar > 0) .reduce((sum, c) => sum + c.amountDenar, 0)) 
  dialogTransferSelectBox.value = true
}

// فتح Dialog لتعديل الخزينة
function openEditDialog(item) {
  selectedBox.value = item
  newTransaction.value = { ...item }
  dialogEdit.value = true
}

// فتح Dialog تأكيد الحذف
function openDeleteDialog(item) {
  selectedBox.value = item
  dialogDeleteConfirm.value = true
}

// فتح Dialog إضافة مبلغ إلى الخزينة
function openAddMoneyDialog(item) {
  selectedBox.value = item
  newTransaction.value = { boxID: item.boxID, type: 'add' }
  dialogAddMoney.value = true
}

// فتح Dialog سحب مبلغ من الخزينة
function openWithdrawMoneyDialog(item) {
  selectedBox.value = item
  newTransaction.value = { boxID: item.boxID, type: 'withdraw' }
  dialogWithdrawMoney.value = true
}

// فتح Dialog لنقل المبلغ بين الخزائن
function openTransferMoneyDialog(item) {
  selectedBox.value = item
  newTransaction.value = { boxID: item.boxID, type: 'transfer' }
  dialogTransferMoney.value = true
}

 


const formattedAmount = computed({
  get() {
    const value = Number(newTransaction.value.amount)

    return !isNaN(value)
      ? value.toLocaleString() + ' دع'
      : '0 دع'
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, ''))

    newTransaction.value.amount = isNaN(numeric) ? 0 : numeric
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

onMounted(() => {
  fetchBoxes()
})
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
      <!-- فلترة محلية حسب اسم الخزينة -->
      <VRow
        align="end"
        class="mb-4"
      >
        <VCol
          cols="12"
          md="3"
        >
          <VLabel class="mb-2">
            بحث بالخزينة
          </VLabel>
          <AppTextField
            v-model="searchText"
            placeholder="ادخل اسم الخزينة"
            clearable
            prepend-inner-icon="tabler-search"
          />
        </VCol>
        
        <VCol
          cols="12"
          md="9"
          class="d-flex flex-wrap gap-2 align-end justify-md-end"
        >
          <VBtn
            color="primary"
            @click="openAddDialog"
          >
            <VIcon
              icon="tabler-plus"
              class="me-2"
            />
            إضافة خزينة
          </VBtn>
          <VBtn
            color="primary"
            @click="moveBox"
          >
            <VIcon
              icon="tabler-arrow-right"
              class="me-2"
            />
            نقل مبالغ
          </VBtn>
          <VBtn
            color="success"
            @click="exportToExcel"
          >
            <VIcon
              icon="tabler-file-export"
              class="me-2"
            />
            تصدير Excel
          </VBtn>

          <div class="ms-2 d-flex align-center">
            <VBtnToggle
              v-model="viewMode"
              mandatory
              rounded="lg"
              color="primary"
              variant="outlined"
              density="comfortable"
              class="view-toggle"
            >
              <VBtn value="grid">
                <VIcon icon="tabler-layout-grid" />
              </VBtn>
              <VBtn value="table">
                <VIcon icon="tabler-list" />
              </VBtn>
            </VBtnToggle>
          </div>
        </VCol>
      </VRow>

      <!-- عرض بيانات الخزائن -->
      <VRow class="mt-4">
        <!-- Grid View -->
        <template v-if="viewMode === 'grid'">
          <VCol
            v-for="box in filteredBoxes"
            :key="box.boxID"
            cols="12"
            sm="6"
            md="4"
            lg="3"
            xl="2"
          >
            <VCard
              class="cash-register-card h-100 position-relative border-0"
              variant="elevated"
              elevation="6"
            >
              <!-- Selection Checkbox -->
              <div
                class="position-absolute top-0 left-0 pa-2"
                style="z-index: 5; inset-inline-start: 0;"
              >
                <VCheckbox
                  v-model="selectedBoxes"
                  :value="box"
                  density="compact"
                  hide-details
                  color="primary"
                />
              </div>

              <!-- Card Header Background -->
              <div 
                class="position-absolute top-0 start-0 end-0"
                style=" background: linear-gradient(135deg, rgb(var(--v-theme-primary)) 0%, rgb(var(--v-theme-primary), 0.7) 100%);block-size: 120px; clip-path: polygon(0 0, 100% 0, 100% 70%, 0 100%); opacity: 0.1;"
              />

              <VCardText
                class="d-flex flex-column align-center text-center pt-8 pb-4 position-relative"
                style="z-index: 1;"
              >
                <!-- Icon with Glow -->
                <div class="mb-4 position-relative">
                  <VAvatar
                    size="80"
                    color="primary"
                    variant="tonal"
                    style="border: 2px solid rgb(var(--v-theme-primary)); background-color: rgb(var(--v-theme-surface));"
                  >
                    <VIcon
                      icon="tabler-wallet"
                      size="40"
                      color="primary"
                    />
                  </VAvatar>
                </div>
                
                <h3 class="text-h6 font-weight-bold mb-1 text-truncate w-100">
                  {{ box.boxName || 'بدون اسم' }}
                </h3>
                
                <VChip
                  size="x-small"
                  :color="box.boxState ? 'success' : 'error'"
                  variant="flat"
                  class="mb-4 font-weight-bold"
                >
                  {{ box.boxState ? 'مفعل' : 'غير مفعل' }}
                </VChip>

                <div class="py-4 w-100 mb-2 rounded-lg bg-grey-100 d-flex justify-center align-center">
                  <h2 class="text-h4 font-weight-black text-primary mb-0 balance-text">
                    {{ formattedNumber(box.amountDenar) }}
                  </h2>
                </div>

                <VDivider class="w-100 mb-4" />

                <!-- Actions -->
                <div class="d-flex gap-2 justify-center flex-wrap w-100 mt-auto">
                  <VTooltip
                    text="إضافة مبلغ"
                    location="top"
                  >
                    <template #activator="{ props }">
                      <VBtn
                        v-bind="props"
                        icon
                        size="small"
                        color="success"
                        variant="tonal"
                        @click="openAddMoneyDialog(box)"
                      >
                        <VIcon icon="tabler-plus" />
                      </VBtn>
                    </template>
                  </VTooltip>

                  <VTooltip
                    text="سحب مبلغ"
                    location="top"
                  >
                    <template #activator="{ props }">
                      <VBtn
                        v-bind="props"
                        icon
                        size="small"
                        color="warning"
                        variant="tonal"
                        @click="openWithdrawMoneyDialog(box)"
                      >
                        <VIcon icon="tabler-cash-banknote-off" />
                      </VBtn>
                    </template>
                  </VTooltip>

                  <VTooltip
                    text="نقل مبلغ"
                    location="top"
                  >
                    <template #activator="{ props }">
                      <VBtn
                        v-bind="props"
                        icon
                        size="small"
                        color="info"
                        variant="tonal"
                        @click="openTransferMoneyDialog(box)"
                      >
                        <VIcon icon="tabler-arrow-right" />
                      </VBtn>
                    </template>
                  </VTooltip>

                  <VMenu location="bottom end">
                    <template #activator="{ props }">
                      <VBtn
                        v-bind="props"
                        icon
                        size="small"
                        variant="text"
                        color="medium-emphasis"
                      >
                        <VIcon icon="tabler-dots-vertical" />
                      </VBtn>
                    </template>
                    <VList
                      density="compact"
                      elevation="10"
                      rounded="lg"
                    >
                      <VListItem @click="openEditDialog(box)">
                        <template #prepend>
                          <VIcon
                            icon="tabler-edit"
                            size="20"
                            class="me-2"
                          />
                        </template>
                        <VListItemTitle>تعديل</VListItemTitle>
                      </VListItem>
                      <VDivider />
                      <VListItem
                        class="text-error"
                        @click="openDeleteDialog(box)"
                      >
                        <template #prepend>
                          <VIcon
                            icon="tabler-trash"
                            size="20"
                            class="me-2"
                            color="error"
                          />
                        </template>
                        <VListItemTitle>حذف</VListItemTitle>
                      </VListItem>
                    </VList>
                  </VMenu>
                </div>
              </VCardText>
            </VCard>
          </VCol>
        </template>
        
        <!-- Table View -->
        <VDataTable
          v-else
          v-model="selectedBoxes"
          class="text-no-wrap custom-data-table w-100"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          :headers="headers"
          :items="filteredBoxes"
          :items-per-page="50"
          show-select
          return-object
          items-per-page-text="عدد السجل"
        >
          <template #item.boxName="{ item }">
            <div style="inline-size: 200px;">
              {{ item.boxName || 'لا يوجد' }}
            </div>
          </template>
          <template #item.boxState="{ item }">
            <div style="inline-size: 200px;">
              {{ item.boxState ? 'مفعل' : 'غير مفعل' }}
            </div>
          </template>
          <template #item.amountDenar="{ item }">
            <div style="inline-size: 200px;">
              {{ formattedNumber(item.amountDenar) }}
            </div>
          </template>
          <template #item.add="{ item }">
            <VBtn
              style="margin-block-end: 10px;"
              color="primary"
              @click="openAddMoneyDialog(item)"
            >
              <VIcon
                icon="tabler-coins"
                class="me-2"
              />
              اضافة
            </VBtn>
          </template>
          <template #item.withdraw="{ item }">
            <VBtn
              style="margin-block-end: 10px;"
              color="primary"
              @click="openWithdrawMoneyDialog(item)"
            >
              <VIcon
                icon="tabler-cash-banknote-off"
                class="me-2"
              />
              سحب
            </VBtn>
          </template>
          <template #item.move="{ item }">
            <VBtn
              style="margin-block-end: 10px;"
              color="primary"
              @click="openTransferMoneyDialog(item)"
            >
              <VIcon
                icon="tabler-arrow-right"
                class="me-2"
              />
              نقل
            </VBtn>
          </template>
          <template #item.update="{ item }">
            <VBtn
              style="margin-block-end: 10px;"
              color="primary"
              @click="openEditDialog(item)"
            >
              <VIcon
                icon="tabler-edit"
                class="me-2"
              />
              تعديل
            </VBtn>
          </template>
          <template #item.delete="{ item }">
            <VBtn
              style="margin-block-end: 10px;"
              color="error"
              @click="openDeleteDialog(item)"
            >
              <VIcon
                icon="tabler-trash"
                class="me-2"
              />
              حذف
            </VBtn>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>

  
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
            <span class="text-caption text-medium-emphasis">هل أنت متأكد من حذف هذه الخزينة؟</span>
          </div>
        </div>
      </div>
      <VCardText>
        <p class="mb-0">
          لا يمكن التراجع عن هذا الإجراء وسيتم حذف الخزينة نهائياً.
        </p>
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
          إغلاق
        </VBtn>
        <VBtn
          color="error"
          @click="deleteBox"
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

  <template>
    <!-- Dialog إضافة خزينة -->
    <VDialog
      v-model="dialogAdd"
      max-width="500px"
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
                icon="tabler-wallet"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                إضافة خزينة جديدة
              </h4>
              <span class="text-caption text-medium-emphasis">أدخل اسم الخزينة الجديدة</span>
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
              اسم الخزينة
            </VLabel>
            <AppTextField
              v-model="newTransaction.boxName"
              required
              prepend-inner-icon="tabler-wallet"
            />
          </VForm>
        </VCardText>
        <VCardActions class="justify-end gap-3 pa-4">
          <VBtn
            variant="tonal"
            color="secondary"
            @click="dialogAdd = false"
          >
            <VIcon
              icon="tabler-x"
              class="me-2"
            />
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            @click="addBox"
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

    <!-- Dialog تعديل الخزينة -->
    <VDialog
      v-model="dialogEdit"
      max-width="500px"
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
                icon="tabler-pencil"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                تعديل بيانات الخزينة
              </h4>
              <span class="text-caption text-medium-emphasis">تحديث اسم الخزينة المحددة</span>
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
              اسم الخزينة
            </VLabel>
            <AppTextField
              v-model="newTransaction.boxName"
              prepend-inner-icon="tabler-wallet"
              required
            />
          </VForm>
        </VCardText>
        <VCardActions class="justify-end gap-3 pa-4">
          <VBtn
            variant="tonal"
            color="secondary"
            @click="dialogEdit = false"
          >
            <VIcon
              icon="tabler-x"
              class="me-2"
            />
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            @click="editBox"
          >
            <VIcon
              icon="tabler-check"
              class="me-2"
            />
            تعديل
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- Dialog إضافة مبلغ إلى الخزينة -->
    <VDialog
      v-model="dialogAddMoney"
      max-width="500px"
      content-class="modern-dialog"
    >
      <VCard class="pa-2">
        <div class="dialog-header pa-4 d-flex align-center justify-space-between">
          <div class="d-flex align-center gap-3">
            <VAvatar
              color="success"
              variant="tonal"
              rounded
              size="48"
            >
              <VIcon
                icon="tabler-currency-dollar"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                إضافة مبلغ
              </h4>
              <span class="text-caption text-medium-emphasis">أدخل المبلغ ليتم إضافته للخزينة</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="secondary"
            size="small"
            @click="dialogAddMoney = false"
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
              المبلغ
            </VLabel>
            <AppTextField
              v-model="formattedAmount"
              type="text"
              required
              prepend-inner-icon="tabler-cash"
              @keypress="onNumberInput"
            />
            <VLabel
              class="mb-2"
              style="margin-block-start: 10px;"
            >
              الملاحظات
            </VLabel>
            <AppTextField
              v-model="newTransaction.notes"
              prepend-inner-icon="tabler-notes"
            />
          </VForm>
        </VCardText>
        <VCardActions class="justify-end gap-3 pa-4">
          <VBtn
            variant="tonal"
            color="secondary"
            @click="dialogAddMoney = false"
          >
            <VIcon
              icon="tabler-x"
              class="me-2"
            />
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            @click="addToBox_Create"
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

    <!-- Dialog سحب مبلغ من الخزينة -->
    <VDialog
      v-model="dialogWithdrawMoney"
      max-width="500px"
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
                icon="tabler-cash-banknote-off"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                سحب مبلغ
              </h4>
              <span class="text-caption text-medium-emphasis">أدخل المبلغ المراد سحبه من الخزينة</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="secondary"
            size="small"
            @click="dialogWithdrawMoney = false"
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
              المبلغ
            </VLabel>
            <AppTextField
              v-model="formattedAmount"
              type="text"
              required
              prepend-inner-icon="tabler-minus"
              @keypress="onNumberInput"
            />
            <VLabel
              class="mb-2"
              style="margin-block-start: 10px;"
            >
              الغرض
            </VLabel>
            <AppTextField
              v-model="newTransaction.notes"
              prepend-inner-icon="tabler-clipboard-text"
            />
          </VForm>
        </VCardText>
        <VCardActions class="justify-end gap-3 pa-4">
          <VBtn
            variant="tonal"
            color="secondary"
            @click="dialogWithdrawMoney = false"
          >
            <VIcon
              icon="tabler-x"
              class="me-2"
            />
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            @click="withdrawalFromBox_Create"
          >
            <VIcon
              icon="tabler-check"
              class="me-2"
            />
            سحب
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- Dialog نقل المبلغ بين الخزائن -->
    <VDialog
      v-model="dialogTransferMoney"
      max-width="500px"
      content-class="modern-dialog"
    >
      <VCard class="pa-2">
        <div class="dialog-header pa-4 d-flex align-center justify-space-between">
          <div class="d-flex align-center gap-3">
            <VAvatar
              color="info"
              variant="tonal"
              rounded
              size="48"
            >
              <VIcon
                icon="tabler-arrows-left-right"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                نقل مبلغ
              </h4>
              <span class="text-caption text-medium-emphasis">نقل مبلغ إلى خزينة أخرى</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="secondary"
            size="small"
            @click="dialogTransferMoney = false"
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
              المبلغ
            </VLabel>
            <AppTextField
              v-model="formattedAmount"
              type="text"
              required
              prepend-inner-icon="tabler-transfer"
              @keypress="onNumberInput"
            />

            <VLabel
              class="mb-2"
              style="margin-block-start: 10px;"
            >
              إلى الخزينة
            </VLabel>
            <VAutocomplete
              v-model="newTransaction.destinationBoxID"
              :items="boxData"
              item-value="boxID"
              prepend-inner-icon="tabler-building-bank"
              item-title="boxName"
              required
            />

            <VLabel
              class="mb-2"
              style="margin-block-start: 10px;"
            >
              الملاحظات
            </VLabel>
            <AppTextField
              v-model="newTransaction.notes"
              prepend-inner-icon="tabler-notes"
            />
          </VForm>
        </VCardText>
        <VCardActions class="justify-end gap-3 pa-4">
          <VBtn
            variant="tonal"
            color="secondary"
            @click="dialogTransferMoney = false"
          >
            <VIcon
              icon="tabler-x"
              class="me-2"
            />
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            @click="transferBoxs_Create"
          >
            <VIcon
              icon="tabler-check"
              class="me-2"
            />
            نقل
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="dialogTransferSelectBox"
      max-width="500px"
      content-class="modern-dialog"
    >
      <VCard class="pa-2">
        <div class="dialog-header pa-4 d-flex align-center justify-space-between">
          <div class="d-flex align-center gap-3">
            <VAvatar
              color="info"
              variant="tonal"
              rounded
              size="48"
            >
              <VIcon
                icon="tabler-arrows-join-2"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                نقل متعدد
              </h4>
              <span class="text-caption text-medium-emphasis">نقل مبالغ من عدة خزائن</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="secondary"
            size="small"
            @click="dialogTransferSelectBox = false"
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
              المبلغ الكلي
            </VLabel>
            <AppTextField
              v-model="totalAmount"
              readonly
              prepend-inner-icon="tabler-cash"
            />
            <VLabel
              class="mb-2"
              style="margin-block-start: 10px;"
            >
              إلى الخزينة
            </VLabel>
            <VAutocomplete
              v-model="newTransaction.destinationBoxID"
              :items="boxData"
              item-value="boxID"
              prepend-inner-icon="tabler-building-bank"
              item-title="boxName"
              required
            />
            <VLabel
              class="mb-2"
              style="margin-block-start: 10px;"
            >
              الملاحظات
            </VLabel>
            <AppTextField
              v-model="newTransaction.notes"
              prepend-inner-icon="tabler-notes"
            />
          </VForm>
        </VCardText>
        <VCardActions class="justify-end gap-3 pa-4">
          <VBtn
            variant="tonal"
            color="secondary"
            @click="dialogTransferSelectBox = false"
          >
            <VIcon
              icon="tabler-x"
              class="me-2"
            />
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            @click="transferBoxSelect_Create"
          >
            <VIcon
              icon="tabler-check"
              class="me-2"
            />
            نقل
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </template>
</template>

<style scoped>
.cash-register-card {
  overflow: hidden;
  border-radius: 16px;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.cash-register-card:hover {
  box-shadow: 0 12px 24px -10px rgba(var(--v-theme-primary), 0.3) !important;
  transform: translateY(-5px);
}

.balance-text {
  font-feature-settings: "tnum";
  font-variant-numeric: tabular-nums;
  letter-spacing: -0.5px;
}

.dialog-header {
  background-color: rgb(var(--v-theme-surface));
  border-block-end: 1px solid rgba(var(--v-border-color), 0.12);
}

.modern-dialog .v-card {
  box-shadow: 0 24px 48px -12px rgba(0, 0, 0, 18%) !important;
}

/* Helper classes */
.gap-2 {
  gap: 8px;
}

.gap-3 {
  gap: 12px;
}
</style>


