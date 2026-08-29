<script setup>
import { getAuthHeaders } from '@/services/tokenService' // استيراد مكتبة XLSX
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

const apiUrl = localStorage.getItem('LinkCity') // رابط الـ API
const buysData = ref([])
const buyIdToDelete = ref(null)
const confirmDeleteDialog = ref(false)
const updateDateDialog = ref(false)
const selectedBuy = ref(null)
const suppliersOptions = ref([])
const storesOptions = ref([])
const cashBoxesOptions = ref([])
const itemsOptions = ref([])

// البيانات المتعلقة بالسند الجديد
const formData = ref({
  supplierID: '',
  storeID: '',
  boxID: '',
  date: '',
  contents: [],
  totalAmountSpent: 0,
  finalTotalItemCostDenar: 0,
  amountTotalDenar: 0,
})



// فتح الـ Dialog وتعبئة البيانات
function openUpdateDateDialog(item) {
  selectedBuy.value = item

  const today = new Date(item.dateCreate)
  const yyyy = today.getFullYear()
  const mm = String(today.getMonth() + 1).padStart(2, '0')
  const dd = String(today.getDate()).padStart(2, '0')

  formData.value.date =  `${yyyy}-${mm}-${dd}`
  updateDateDialog.value = true
}



async function submitUpdateDate() {
  if (selectedBuy.value) {
    const updatedBuy = {
      buyID: selectedBuy.value.buyID,
      dateCreate: formData.value.date,
    }

    try {
      const authHeader = getAuthHeaders()

      await axios.put(
        `${apiUrl}Buys/Buys_UpdateDate/${selectedBuy.value.buyID}`,
        updatedBuy,
        { headers: authHeader },
      )
      updateDateDialog.value = false
      await fetchBuys()
    } catch (error) {
      console.error('حدث خطأ أثناء تعديل تاريخ الشراء:', error)
      alert('حدث خطأ أثناء تعديل  تاريخ الشراء.')
    }
  }
}

function handleDateCreate(event) {
  const rawDate = event.target.value

  formData.value.date = new Date(rawDate).toLocaleDateString('en-CA')
}

const addDialog = ref(false)
const loading = ref(false)

// جلب الموردين
async function fetchSuppliers() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Suppliers/Suppliers_GetAll/null`, { headers: authHeader })

    suppliersOptions.value = response.data
  } catch (error) {
    console.error("Error fetching suppliers", error)
  }
}

// جلب المخازن
async function fetchStores() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Stores/StoresData_GetAll`, { headers: authHeader })

    storesOptions.value = response.data
  } catch (error) {
    console.error("Error fetching stores", error)
  }
}



async function changeStore(storeID) {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Items/Items_GetByItemBuy/${storeID}`, { headers: authHeader })

    itemsOptions.value = response.data
  } catch (error) {
    console.error("Error fetching stores", error)
  }
}

// جلب الخزائن النقدية
async function fetchCashBoxes() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Accounts/Boxs_GetAll`, { headers: authHeader })

    cashBoxesOptions.value = response.data.sort((a, b) => a.boxID - b.boxID)
  } catch (error) {
    console.error("Error fetching cashboxes", error)
  }
}

// جلب البيانات عند تحميل الصفحة
onMounted(() => {
  fetchSuppliers()
  fetchStores()
  fetchCashBoxes()
})

// إضافة عنصر إلى القائمة مع حساب السعر الإجمالي
function addContent() {
  changeStore(formData.value.storeID)
  formData.value.contents.push({
    itemID: '',
    quantity: 1,
    itemCostDenar: 0,
    totalItemCostDenar: 0,
  })
}

// دالة لتحديث سعر الشراء بناءً على العنصر المختار
function updateItemCost(index) {
  const content = formData.value.contents[index]
  const selectedItem = itemsOptions.value.find(item => item.itemID === content.itemID)

  const isItemDuplicate = formData.value.contents.some((item, idx) => item.itemID === content.itemID && idx !== index)

  if (isItemDuplicate) {
    alert('تم اختيار هذا العنصر مسبقًا')
    content.itemID = ''
    
    return
  }

  if (selectedItem) {
    content.itemCostDenar = selectedItem.itemCostDenar
    calculateTotalPrice(index)
  }
}

// دالة لحساب السعر الكلي عند إدخال الكمية أو السعر
function calculateTotalPrice(index) {
  const content = formData.value.contents[index]

  content.totalItemCostDenar = content.quantity * content.itemCostDenar
  calculateTotalAmountSpent()
}


// دالة لحساب إجمالي المبلغ المصروف
function calculateTotalAmountSpent() {
  formData.value.amountTotalDenar = formData.value.contents.reduce((sum, content) => sum + content.totalItemCostDenar, 0)
  formData.value.totalAmountSpent = formData.value.contents.reduce((sum, content) => sum + content.totalItemCostDenar, 0)+(parseFloat(suppliersOptions.value?.find(c => c.supplierID === formData.value.supplierID)?.amountAccount) || 0)
  formData.value.finalTotalItemCostDenar = formData.value.contents.reduce((sum, content) => sum + content.totalItemCostDenar, 0)+(parseFloat(suppliersOptions.value?.find(c => c.supplierID === formData.value.supplierID)?.amountAccount) || 0)
}

// حساب المبلغ المتبقي بناءً على إجمالي المبلغ المصروف
const remainingAmount = computed(() => {
  const totalCost = formData.value.finalTotalItemCostDenar
  
  return totalCost - formData.value.totalAmountSpent
})

// مراقبة التغييرات على الكمية أو سعر الشراء
watch(() => formData.value.contents, () => {
  formData.value.contents.forEach((content, index) => {
    calculateTotalPrice(index)
  })
}, { deep: true })


const isFormValid = computed(() => {
  const contentsValid = formData.value.contents.every(content => content.itemID && content.quantity && content.itemCostDenar)
  
  return formData.value.supplierID && formData.value.storeID && formData.value.boxID && formData.value.date && contentsValid
})


// إغلاق Dialog
function closeDialog() {
  addDialog.value = false
}


const submitForm = async () => {
  // تحقق من أن جميع الحقول المطلوبة تم ملؤها
  if (formData.value.contents.some(content => !content.itemID || !content.quantity || !content.itemCostDenar)) {
    alert("الرجاء ملء جميع الحقول المطلوبة")
    
    return
  }

  // بناء البيانات لإرسالها
  const dataToSubmit = {
    contents: formData.value.contents.map(content => ({
      ItemID: content.itemID,
      Quantity: content.quantity,
      ItemCostDenar: content.itemCostDenar,
      TotalItemCostDenar: content.totalItemCostDenar,
    })),
    SupplierID: formData.value.supplierID,
    StoreID: formData.value.storeID,
    BoxID: formData.value.boxID,
    TotalAmountSpent: formData.value.totalAmountSpent,
    AmountTotalDenar: formData.value.amountTotalDenar,
    FinalTotalItemCostDenar: formData.value.finalTotalItemCostDenar,
    RemainingAmountDenar: remainingAmount.value,
    Date: new Date(formData.value.date).toLocaleDateString('en-CA'),
  }

  try {
    const authHeader = getAuthHeaders()
    let boxAmount = cashBoxesOptions.value.find(c => c.boxID === formData.value.boxID)?.amountDenar

    if (parseFloat(boxAmount) >= formData.value.totalAmountSpent) {
      // إرسال البيانات إلى API باستخدام POST مع Content-Type: application/json
      const response = await axios.post(`${apiUrl}Buys/Buys_Create`, dataToSubmit, {
        headers: {
          ...authHeader,
          'Content-Type': 'application/json', // تحديد نوع المحتوى إلى JSON
        },
      })

      // تنظيف الحقول بعد الإرسال بنجاح
      formData.value.contents = [{ itemID: '', quantity: 1, itemCostDenar: 0, totalItemCostDenar: 0 }]
      formData.value.totalAmountSpent = 0
      formData.value.amountTotalDenar = 0
      formData.value.finalTotalItemCostDenar = 0

      buysData.value.push(response.data)
      addDialog.value = false
    } else {
      alert('المبلغ المراد صرفه أكبر من الموجود في الخزينة.')
    }
  } catch (error) {
    console.error('حدث خطأ أثناء إضافة السند:', error)
    alert('حدث خطأ أثناء إضافة السند.')
  }
}


// حالة التحميل عند البحث
const filters = ref({
  textSearch: '',
  fromDate: '',
  toDate: '',
})

const formattedNumber = num => (num ? num.toLocaleString() + " دع " : '0')

// 🟢 تعريف عناوين الجدول (باستخدام أسماء الحقول بحرف صغير)
const headers = [
  { title: 'رقم السند', key: 'boundNumber' },
  { title: 'المورد', key: 'supplierName' },
  { title: 'أسماء المواد', key: 'itemsNames' },
  { title: 'تاريخ الشراء', key: 'dateCreate' },
  { title: 'عدد المواد', key: 'numberOfItemsBuys' },
  { title: 'المبلغ الكلي', key: 'totalAmountDenar' },
  { title: 'المبلغ المصروف', key: 'amountSpentDenar' },
  { title: 'الصندوق', key: 'boxName' },
  { title: 'تعديل التاريخ', key: 'updateDate' },
  { title: 'حذف', key: 'delete' },
]

const totals = computed(() => [
  {
    icon: 'tabler-file', // عدد السندات
    value: buysData.value.length,
    title: 'عدد السندات',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-wallet', // إجمالي مبلغ الحساب الكلي
    value: formattedNumber(
      buysData.value.reduce((sum, buy) => sum + buy.totalAmountDenar, 0),
    ),
    title: 'إجمالي المبلغ الكلي',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
  {
    icon: 'tabler-wallet', // إجمالي المبلغ المصروف
    value: formattedNumber(
      buysData.value.reduce((sum, buy) => sum + buy.amountSpentDenar, 0),
    ),
    title: 'إجمالي المبلغ المصروف',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-box', // إجمالي عدد المواد
    value: buysData.value.reduce((sum, buy) => sum + buy.numberOfItemsBuys, 0),
    title: 'إجمالي عدد المواد',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
])


// 🟢 دالة جلب بيانات المشتريات من API باستخدام الفلاتر مع تحديث حالة التحميل
async function fetchBuys() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const fromDate = filters.value.fromDate || 'null'
    const toDate = filters.value.toDate || 'null'
    const textSearch = filters.value.textSearch || 'null'

    const response = await axios.get(
      `${apiUrl}Buys/Buys_GetByDateByTextSearch/${fromDate}&&${toDate}&&${textSearch}`, { headers: authHeader },
    )

    buysData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير البيانات إلى Excel
function exportToExcel() {
  const dataToExport = buysData.value.map(buy => ({
    'رقم السند': buy.boundNumber,
    'المورد': buy.supplierName,
    'أسماء المواد': buy.itemsNames,
    'تاريخ الشراء': new Date(buy.dateCreate).toLocaleDateString('en-CA'),
    'عدد المواد': buy.numberOfItemsBuys,
    'المبلغ الكلي': buy.totalAmountDenar,
    'المبلغ المصروف': buy.amountSpentDenar,
    'الصندوق': buy.boxName,
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Buys")
  XLSX.writeFile(workbook, "buys.xlsx")
}

function addBuys() {
  addDialog.value = true
}


function handleDateChangeFromDate(event) {
  const rawDate = event.target.value

  filters.value.fromDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function handleDateChangeToDate(event) {
  const rawDate = event.target.value

  filters.value.toDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function openDeleteDialog(buyID) {
  buyIdToDelete.value = buyID
  confirmDeleteDialog.value = true
}


async function deleteBuy() {
  if (buyIdToDelete.value) {
    try {
      const authHeader = getAuthHeaders()

      await axios.delete(`${apiUrl}Buys/Buys_Delete/${buyIdToDelete.value}`, { headers: authHeader })

      const index = buysData.value.findIndex(buy => buy.buyID === buyIdToDelete.value)
      if (index !== -1) {
        buysData.value.splice(index, 1)
      }
      confirmDeleteDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}

const formattedTotalAmountSpent = computed({
  get() {
    return formData.value.totalAmountSpent !== ''
      ? Number(formData.value.totalAmountSpent).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    formData.value.totalAmountSpent = isNaN(numeric) ? '' : numeric
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
      <!-- 🔹 فلاتر البحث -->
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            من تاريخ
          </VLabel>
          <AppTextField
            v-model="filters.fromDate"
            type="date"
            placeholder="اختر تاريخ البدء"
            prepend-inner-icon="tabler-calendar"
            clearable
            clear-icon="tabler-x"
            @input="handleDateChangeFromDate"
          />
        </VCol>

        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            إلى تاريخ
          </VLabel>
          <AppTextField
            v-model="filters.toDate"
            type="date"
            placeholder="اختر تاريخ الانتهاء"
            prepend-inner-icon="tabler-calendar"
            clearable
            clear-icon="tabler-x"
            @input="handleDateChangeToDate"
          />
        </VCol>

        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            بحث (رقم السند / المورد / المواد)
          </VLabel>
          <AppTextField
            v-model="filters.textSearch"
            placeholder="أدخل نص البحث"
            prepend-inner-icon="tabler-search"
            clearable
            clear-icon="tabler-x"
          />
        </VCol>

        <VCol
          cols="6"
          class="text-right d-flex"
          style="margin-block-start: 21px;"
        >
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            prepend-icon="tabler-search"
            @click="fetchBuys"
          >
            بحث
          </VBtn>
          <VBtn
            color="success"
            class="mx-4"
            prepend-icon="tabler-plus"
            @click="addBuys"
          >
            إضافة شراء جديد
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

      <!-- 🔹 عرض بيانات المشتريات -->
      <VRow>
        <VDataTable
          :headers="headers"
          :items="buysData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <template #item.dateCreate="{ item }">
            {{ item.dateCreate ? new Date(item.dateCreate).toLocaleDateString('en-CA') : 'لا يوجد' }}
          </template>
          <template #item.totalAmountDenar="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.totalAmountDenar) }}
            </div>
          </template>
          <template #item.amountSpentDenar="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.amountSpentDenar) }}
            </div>
          </template>
          <template #item.updateDate="{ item }">
            <VBtn
              color="primary"
              style="margin-block-end: 10px;"
              small
              prepend-icon="tabler-calendar-event"
              @click="openUpdateDateDialog(item)"
            >
              تعديل التاريخ
            </VBtn>
          </template>
          <template #item.delete="{ item }">
            <VBtn
              color="error"
              style="margin-block-end: 10px;"
              small
              prepend-icon="tabler-trash"
              @click="openDeleteDialog(item.buyID)"
            >
              حذف
            </VBtn>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>

  <!-- إضافة شراء جديد -->
  <VDialog
    v-model="addDialog"
    max-width="1300px"
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
              icon="tabler-shopping-cart-plus"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              إضافة شراء جديد
            </h4>
            <span class="text-caption text-medium-emphasis">إضافة تفاصيل فاتورة الشراء</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          size="small"
          @click="closeDialog"
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
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              المورد
            </VLabel>
            <VAutocomplete
              v-model="formData.supplierID"
              :items="suppliersOptions.map(s => ({ title: s.supplierName, value: s.supplierID }))"
              required
              prepend-inner-icon="tabler-user"
              clearable
              clear-icon="tabler-x"
            />
          </VCol>
          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              المخزن
            </VLabel>
            <VAutocomplete
              v-model="formData.storeID"
              :items="storesOptions.map(s => ({ title: s.storeName, value: s.storeID }))"
              required
              prepend-inner-icon="tabler-building-store"
              clearable
              clear-icon="tabler-x"
              @update:model-value="changeStore"
            />
          </VCol>
          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              الخزينة النقدية
            </VLabel>
            <VAutocomplete
              v-model="formData.boxID"
              :items="cashBoxesOptions.map(b => ({ title: b.boxName, value: b.boxID }))"
              required
              prepend-inner-icon="tabler-cash"
              clearable
              clear-icon="tabler-x"
            />
          </VCol>
          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              التاريخ
            </VLabel>
            <AppTextField
              v-model="formData.date"
              type="date"
              required
              prepend-inner-icon="tabler-calendar"
            />
          </VCol>
        </VRow>

        <!-- قائمة المحتويات -->
        <VRow>
          <VCol
            v-for="(content, idx) in formData.contents"
            :key="idx"
            cols="12"
            class="mb-3"
          >
            <VRow>
              <VCol
                md="4"
                cols="12"
              >
                <VLabel class="mb-2">
                  العنصر
                </VLabel>
                <VAutocomplete
                  v-model="content.itemID"
                  :items="itemsOptions.map(i => ({ title: i.itemName, value: i.itemID }))"
                  required
                  prepend-inner-icon="tabler-box"
                  clearable
                  clear-icon="tabler-x"
                  @update:model-value="() => updateItemCost(idx)"
                />
              </VCol>
              <VCol
                md="2"
                cols="12"
              >
                <VLabel class="mb-2">
                  الكمية
                </VLabel>
                <AppTextField
                  v-model="content.quantity"
                  type="number"
                  min="1"
                  required
                  prepend-inner-icon="tabler-numbers"
                  @input="() => calculateTotalPrice(idx)"
                />
              </VCol>
              <VCol
                md="2"
                cols="12"
              >
                <VLabel class="mb-2">
                  سعر الشراء
                </VLabel>
                <AppTextField
                  readonly
                  :value="formattedNumber(content.itemCostDenar)"
                  prepend-inner-icon="tabler-coin"
                />
              </VCol>
              <VCol
                md="2"
                cols="12"
              >
                <VLabel class="mb-2">
                  سعر الشراء الكلي
                </VLabel>
                <AppTextField
                  readonly
                  :value="formattedNumber(content.totalItemCostDenar)"
                  prepend-inner-icon="tabler-coin"
                />
              </VCol>
              <VCol
                md="2"
                cols="12"
              >
                <VBtn
                  color="error"
                  small
                  prepend-icon="tabler-trash"
                  style="margin-block-start: 29px;"
                  @click="formData.contents.splice(idx, 1)"
                >
                  حذف العنصر
                </VBtn>
              </VCol>
            </VRow>
          </VCol>
        </VRow>

        <VRow>
          <VCol
            cols="12"
            class="text-right"
          >
            <VBtn
              color="primary"
              prepend-icon="tabler-plus"
              :disabled="!isFormValid"
              @click="addContent"
            >
              إضافة عنصر
            </VBtn>
          </VCol>
        </VRow>

        <!-- الإجماليات -->
        <VLabel
          class="mb-2"
          style="margin-block-start: 20px;"
        >
          الإجماليات
        </VLabel>
        <VRow>
          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              إجمالي سعر الشراء الكلي
            </VLabel>
            <AppTextField
              :value="formattedNumber(formData.amountTotalDenar)"
              readonly
              prepend-inner-icon="tabler-coin"
            />
          </VCol>
          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              إجمالي سعر الشراء النهائي
            </VLabel>
            <AppTextField
              :value="formattedNumber(formData.finalTotalItemCostDenar)"
              readonly
              prepend-inner-icon="tabler-coin"
            />
          </VCol>
          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              المبلغ المصروف
            </VLabel>
            <AppTextField
              v-model="formattedTotalAmountSpent"
              type="text"
              required
              prepend-inner-icon="tabler-coin"
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              المبلغ المتبقي
            </VLabel>
            <AppTextField
              :value="formattedNumber(remainingAmount)"
              readonly
              prepend-inner-icon="tabler-coin"
            />
          </VCol>
        </VRow>
      </VCardText>
      <VCardActions class="pa-4">
        <VSpacer />
        <VBtn
          variant="tonal"
          color="secondary"
          @click="closeDialog"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          @click="submitForm"
        >
          إضافة الشراء
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- حوار تأكيد الحذف -->
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
            <span class="text-caption text-medium-emphasis">هل أنت متأكد من حذف هذا السند؟</span>
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
          @click="deleteBuy"
        >
          حذف
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>

  <!-- حوار تعديل التاريخ -->
  <VDialog
    v-model="updateDateDialog"
    max-width="400px"
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
              icon="tabler-calendar-time"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              تعديل التاريخ
            </h4>
            <span class="text-caption text-medium-emphasis">تغيير تاريخ الشراء</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          size="small"
          @click="updateDateDialog = false"
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
            md="10"
            cols="12"
          >
            <VLabel class="mb-2">
              تاريخ الشراء
            </VLabel>
            <AppTextField
              v-model="formData.date"
              type="date"
              required
              prepend-inner-icon="tabler-calendar"
              @input="handleDateCreate"
            />
          </VCol>
        </VRow>
      </VCardText>
      <VCardActions class="pa-4">
        <VSpacer />
        <VBtn
          variant="tonal"
          color="secondary"
          @click="updateDateDialog = false"
        >
          إلغاء
        </VBtn>
        <VBtn
          color="primary"
          prepend-icon="tabler-check"
          @click="submitUpdateDate"
        >
          تعديل
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
