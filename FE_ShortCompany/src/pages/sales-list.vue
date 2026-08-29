<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

const apiUrl = localStorage.getItem('LinkCity') // رابط الـ API
const salesData = ref([])
const customerSaleIDToDelete = ref(null)
const confirmDeleteDialog = ref(false)
const delegateOption = ref([])
const storesOptions = ref([])
const itemsOptions = ref([])

const formData = ref({
  delegateID: '',
  storeID: '',
  customerName: '',
  phoneNumber: '',
  address: '',
  shopName: '',
  nearestFunctionPoint: 'لا يوجد',
  saleName: '',
  receiptName: '',
  notes: '',
  dateCreate: '',
  amountPriceTotal: 0,
  amountPriceTotalFinal: 0,
  amountDayTotal: 0,
  amountDayTotalFinal: 0,
  discountAmountTotal: 0,
  discountAmountDay: 0,
  contents: [],
})

const addDialog = ref(false)
const loading = ref(false)

async function fetchDelegate() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Delegates/Delegates_GetDataAll`, { headers: authHeader })

    delegateOption.value = response.data
  } catch (error) {
    console.error("Error fetching Delegates", error)
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

// تحديث محتويات المخزن بناءً على المعرف
async function changeStore(storeID) {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Items/Items_GetByItemSale/${storeID}`, { headers: authHeader })

    itemsOptions.value = response.data
  } catch (error) {
    console.error("Error fetching store items", error)
  }
}


async function changeDelegate(delegateID) {
  formData.value.receiptName=delegateOption.value.find(c=>c.delegateID===delegateID).receiptName || ''
}

// جلب البيانات عند تحميل الصفحة
onMounted(() => {
  fetchDelegate()
  fetchStores()
})

function addContent() {
  formData.value.contents.push({
    itemID: '',
    quantity: 1,
    itemPriceDenar: 0,
    amountDayDenar: 0,
    totalItemPriceDenar: 0,
    totalAmountDayDenar: 0,
  })
}

// تحديث سعر العنصر بناءً على اختياره
function updateItemPrice(index) {
  const content = formData.value.contents[index]
  const selectedItem = itemsOptions.value.find(item => item.itemID === content.itemID)

  const isItemDuplicate = formData.value.contents.some((item, idx) => item.itemID === content.itemID && idx !== index)

  if (isItemDuplicate) {
    alert('تم اختيار هذا العنصر مسبقًا')
    content.itemID = ''
    
    return
  }

  if (selectedItem) {
    content.itemPriceDenar = selectedItem.itemPriceDenar
    calculateTotalPrice(index)
  }
}

function calculateTotalPrice(index) {
  const content = formData.value.contents[index]
  const selectedItem = itemsOptions.value.find(item => item.itemID === content.itemID)
  if (selectedItem) {
    if(content.quantity<=parseInt(selectedItem.quantity)){
      content.itemPriceDenar = selectedItem.itemPriceDenar
      content.amountDayDenar = selectedItem.amountDayDenar
      content.totalItemPriceDenar = content.quantity * content.itemPriceDenar
      content.totalAmountDayDenar = content.quantity * content.amountDayDenar
      calculateTotalAmount()
    } else {
      alert("لا يمكن السحب كمية اكبر من الموجود بالمخزن لهذا العنصر")
      content.quantity=0
    }
  }
}

// حساب إجمالي المبالغ
function calculateTotalAmount() {
  formData.value.amountPriceTotal = formData.value.contents.reduce((sum, content) => sum + content.totalItemPriceDenar, 0)
  formData.value.amountPriceTotalFinal = formData.value.contents.reduce((sum, content) => sum + content.totalItemPriceDenar, 0) - parseFloat(formData.value.discountAmountTotal)
  formData.value.amountDayTotal = formData.value.contents.reduce((sum, content) => sum + content.totalAmountDayDenar, 0)
  formData.value.amountDayTotalFinal =  formData.value.contents.reduce((sum, content) => sum + content.totalAmountDayDenar, 0) - parseFloat(formData.value.discountAmountDay)
}

watch(() => formData.value.discountAmountTotal, () => {
  calculateTotalAmount()
})

watch(() => formData.value.discountAmountDay, () => {
  calculateTotalAmount()
})

// مراقبة التغييرات على محتويات المبيعات
watch(() => formData.value.contents, () => {
  formData.value.contents.forEach((content, index) => {
    calculateTotalPrice(index)
  })
}, { deep: true })

const isFormValid = computed(() => {
  const contentsValid = formData.value.contents.every(content => content.itemID && content.quantity && content.itemPriceDenar && content.amountDayDenar)
  
  return formData.value.delegateID && formData.value.storeID && formData.value.customerName && formData.value.phoneNumber && formData.value.address && formData.value.shopName && formData.value.nearestFunctionPoint && formData.value.saleName && formData.value.receiptName && formData.value.dateCreate && contentsValid
})

// إغلاق الـ Dialog
function closeDialog() {
  addDialog.value = false
}

// دالة إرسال النموذج
const submitForm = async () => {
  if (formData.value.contents.some(content => !content.itemID || !content.quantity || !content.itemPriceDenar || !content.amountDayDenar)) {
    alert("الرجاء ملء جميع الحقول المطلوبة")
    
    return
  }

  const dataToSubmit = {
    contents: formData.value.contents.map(content => ({
      ItemID: content.itemID,
      Quantity: content.quantity,
      itemPriceDenar: content.itemPriceDenar,
      amountDayDenar: content.amountDayDenar,
      totalItemPriceDenar: content.totalItemPriceDenar,
      totalAmountDayDenar: content.totalAmountDayDenar,
    })),
    DelegateID: formData.value.delegateID,
    StoreID: formData.value.storeID,
    AmountPriceTotal: formData.value.amountPriceTotal,
    AmountPriceTotalFinal: formData.value.amountPriceTotalFinal,
    AmountDayTotal: formData.value.amountDayTotal,
    AmountDayTotalFinal: formData.value.amountDayTotalFinal,
    DiscountAmountTotal: formData.value.discountAmountTotal,
    DiscountAmountDay: formData.value.discountAmountDay,
    DateCreate: new Date(formData.value.dateCreate).toLocaleDateString('en-CA'),
    CustomerName: formData.value.customerName,
    PhoneNumber: formData.value.phoneNumber,
    Address: formData.value.address,
    ShopName: formData.value.shopName,
    NearestFunctionPoint: formData.value.nearestFunctionPoint,
    SaleName: formData.value.saleName,
    ReceiptName: formData.value.receiptName,
    Notes: formData.value.notes,
  }

  try {
    const authHeader = getAuthHeaders()

    const response = await axios.post(`${apiUrl}CustomersSales/CustomersSales_Create`, dataToSubmit, {
      headers: {
        ...authHeader,
        'Content-Type': 'application/json',
      },
    })

    // تنظيف الحقول بعد الإرسال بنجاح
    formData.value.contents = [{ itemID: '', quantity: 1, itemPriceDenar: 0, amountDayDenar: 0, totalItemPriceDenar: 0, totalAmountDayDenar: 0 }]
    formData.value.amountPriceTotal = 0
    formData.value.amountPriceTotalFinal = 0
    formData.value.amountDayTotal = 0
    formData.value.amountDayTotalFinal = 0
    formData.value.discountAmountTotal = 0
    formData.value.discountAmountDay = 0
    formData.value.storeID = null
    formData.value.delegateID = null
    formData.value.customerName=''
    formData.value.phoneNumber=''
    formData.value.address=''
    formData.value.shopName=''
    formData.value.nearestFunctionPoint=''
    formData.value.saleName=''
    formData.value.receiptName=''
    formData.value.notes=''

    salesData.value.push(response.data)
    addDialog.value = false
  } catch (error) {
    console.error('حدث خطأ أثناء إضافة السند:', error)
    alert('حدث خطأ أثناء إضافة السند.')
  }
}

// الفلاتر
const filters = ref({
  fromDate: '',
  toDate: '',
  delegateID: null,
  customerName: '',
  itemName: '',
  saleName: '',
})

// تنسيق الأرقام
const formattedNumber = num => (num ? num.toLocaleString() + " دع " : '0')

// رؤوس الأعمدة
const headers = [
  { title: 'رقم السند', key: 'boundNumber' },
  { title: 'المباع', key: 'itemsNames' },
  { title: 'عدد المباع', key: 'numberOfItemsSales' },
  { title: 'اسم العميل', key: 'customerName' },
  { title: 'الحالة', key: 'status' },
  { title: 'رقم الهاتف', key: 'phoneNumber' },
  { title: 'المخزن', key: 'storeName' },
  { title: 'تاريخ البيع', key: 'dateCreate' },
  { title: 'المندوب', key: 'delegateName' },
  { title: 'سعر الشراء', key: 'amountTotalCostDenar' },
  { title: 'سعر البيع', key: 'amountTotalDenar' },
  { title: 'خصم سعر البيع', key: 'discountAmountTotalDenar' },
  { title: 'سعر البيع النهائي', key: 'amountTotalSalesDenar' },
  { title: 'القسط', key: 'amountTotalDayDenar' },
  { title: 'خصم القسط', key: 'discountAmountTotalDayDenar' },
  { title: 'القسط النهائي', key: 'amountDaySalesDenar' },
  { title: 'الواصل', key: 'receiptsTotal' },
  { title: 'الباقي', key: 'amountRemaining' },
  { title: 'عدد التسديدات', key: 'countReceiptDevice' },
  { title: 'تاريخ اخر تسديد', key: 'lastPaymentDate' },
  { title: 'اسم البائع', key: 'saleName' },
  { title: 'اسم الجابي', key: 'receiptName' },
]

// إجماليات المبيعات
const totals = computed(() => [
  {
    icon: 'tabler-file', // عدد المبيعات
    value: salesData.value.length,
    title: 'عدد المبيعات',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-box', // إجمالي عدد المواد
    value: salesData.value.reduce((sum, sale) => sum + (sale.numberOfItemsSales || 0), 0),
    title: 'إجمالي عدد المواد',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
  {
    icon: 'tabler-wallet', // إجمالي سعر الشراء الكلي
    value: formattedNumber(
      salesData.value.reduce((sum, sale) => sum + (sale.amountTotalCostDenar || 0), 0),
    ),
    title: 'إجمالي سعر الشراء الكلي',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-wallet', // إجمالي سعر البيع الكلي
    value: formattedNumber(
      salesData.value.reduce((sum, sale) => sum + (sale.amountTotalDenar || 0), 0),
    ),
    title: 'إجمالي سعر البيع الكلي',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
  {
    icon: 'tabler-wallet', // إجمالي سعر البيع النهائي
    value: formattedNumber(
      salesData.value.reduce((sum, sale) => sum + (sale.amountTotalSalesDenar || 0), 0),
    ),
    title: 'إجمالي سعر البيع النهائي',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
  {
    icon: 'tabler-credit-card', // إجمالي القسط الكلي
    value: formattedNumber(
      salesData.value.reduce((sum, sale) => sum + (sale.amountTotalDayDenar || 0), 0),
    ),
    title: 'إجمالي القسط الكلي',
    color: "secondary",
    gradient: "linear-gradient(135deg, #667db6 0%, #0082c8 100%, #0082c8 100%, #667db6 100%)",
  },
  {
    icon: 'tabler-credit-card', // إجمالي القسط النهائي
    value: formattedNumber(
      salesData.value.reduce((sum, sale) => sum + (sale.amountDaySalesDenar || 0), 0),
    ),
    title: 'إجمالي القسط النهائي',
    color: "secondary",
    gradient: "linear-gradient(135deg, #667db6 0%, #0082c8 100%, #0082c8 100%, #667db6 100%)",
  },
  {
    icon: 'tabler-currency-dollar', // إجمالي الواصل
    value: formattedNumber(
      salesData.value.reduce((sum, sale) => sum + (sale.receiptsTotal || 0), 0),
    ),
    title: 'إجمالي الواصل',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
  {
    icon: 'tabler-currency-dollar', // إجمالي المبلغ المتبقي
    value: formattedNumber(
      salesData.value.reduce((sum, sale) => sum + (sale.amountRemaining || 0), 0),
    ),
    title: 'إجمالي المبلغ المتبقي',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
])

// دالة جلب بيانات المبيعات من API باستخدام الفلاتر مع تحديث حالة التحميل
async function fetchSales() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()

    // استخراج القيم من الفلاتر مع توفير القيم الافتراضية إذا كانت فارغة
    const fromDate = filters.value.fromDate || 'null'
    const toDate = filters.value.toDate || 'null'
    const delegateID = filters.value.delegateID || 0
    const customerName = filters.value.customerName || 'null'
    const itemName = filters.value.itemName || 'null'
    const saleName = filters.value.saleName || 'null'

    const response = await axios.get(
      `${apiUrl}CustomersSales/CustomersSales_GetAll/${fromDate}&&${toDate}&&${delegateID}&&${customerName}&&${itemName}&&${saleName}`, { headers: authHeader },
    )

    salesData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير البيانات إلى Excel
function exportToExcel() {
  const dataToExport = salesData.value.map(sale => ({
    'رقم السند': sale.boundNumber || 'لا يوجد',
    'المباع': sale.itemsNames || 'لا يوجد',
    'عدد المباع': sale.numberOfItemsSales || 'لا يوجد',
    'اسم العميل': sale.customerName || 'لا يوجد',
    'رقم الهاتف': sale.phoneNumber || 'لا يوجد',
    'المخزن': sale.storeName || 'لا يوجد',
    'تاريخ البيع': sale.dateCreate ? new Date(sale.dateCreate).toLocaleDateString('en-CA') : 'لا يوجد',
    'المندوب': sale.delegateName || 'لا يوجد',
    'سعر الشراء': sale.amountTotalCostDenar || 'لا يوجد',
    'سعر البيع': sale.amountTotalDenar || 'لا يوجد',
    'خصم سعر البيع': sale.discountAmountTotalDenar || 'لا يوجد',
    'سعر البيع النهائي': sale.amountTotalSalesDenar || 'لا يوجد',
    'القسط': sale.amountTotalDayDenar || 'لا يوجد',
    'خصم القسط': sale.discountAmountTotalDayDenar || 'لا يوجد',
    'القسط النهائي': sale.amountDaySalesDenar || 'لا يوجد',
    'الواصل': sale.receiptsTotal || 'لا يوجد',
    'الباقي': sale.amountRemaining || 'لا يوجد',
    'عدد التسديدات': sale.countReceiptDevice || 'لا يوجد',
    'تاريخ اخر تسديد': sale.lastPaymentDate ? new Date(sale.lastPaymentDate).toLocaleDateString('en-CA') : 'لا يوجد',
    'اسم الجابي': sale.receiptName || 'لا يوجد',
    'اسم البائع': sale.saleName || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Sales")
  XLSX.writeFile(workbook, "sales.xlsx")
}

// إضافة مبيعات جديدة
function addSales() {
  addDialog.value = true
}

// التعامل مع تغيير التاريخ من الفلاتر
function handleDateChangeFromDate(event) {
  const rawDate = event.target.value

  filters.value.fromDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function handleDateChangeToDate(event) {
  const rawDate = event.target.value

  filters.value.toDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function handleDateCreate(event) {
  const rawDate = event.target.value

  formData.value.dateCreate = new Date(rawDate).toLocaleDateString('en-CA')
}

// فتح حوار التأكيد لحذف السند
function openDeleteDialog(customerSaleID) {
  customerSaleIDToDelete.value = customerSaleID
  confirmDeleteDialog.value = true
}

// دالة لحذف السند
async function deleteBuy() {
  if (customerSaleIDToDelete.value) {
    try {
      const authHeader = getAuthHeaders()

      await axios.delete(`${apiUrl}CustomersSales/CustomersSales_Delete/${customerSaleIDToDelete.value}`, { headers: authHeader })

      const index = salesData.value.findIndex(sale => sale.customerSaleID === customerSaleIDToDelete.value)
      if (index !== -1) {
        salesData.value.splice(index, 1)
      }
      confirmDeleteDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}

const discountDialog = ref(false)
const selectedSale = ref(null) // لتخزين السند المحدد

// فتح الـ Dialog وتعبئة البيانات
function openDiscountDialog(item) {
  selectedSale.value = item
  formData.value.discountAmountTotal = item.discountAmountTotalDenar || 0
  formData.value.discountAmountDay = item.discountAmountTotalDayDenar || 0

  const today = new Date(item.dateCreate)
  const yyyy = today.getFullYear()
  const mm = String(today.getMonth() + 1).padStart(2, '0')
  const dd = String(today.getDate()).padStart(2, '0')

  formData.value.dateCreate =  `${yyyy}-${mm}-${dd}`
  discountDialog.value = true
}

function getStatus(item) {
  // 1. Zeroed (Amount Remaining = 0)
  if (item.amountRemaining === 0) {
    return { text: 'مصفر', color: 'secondary' }
  }

  // 2. Legal (isLegal = true)
  if (item.isLegal === true || item.isLegal === 'true') {
    return { text: 'قانونية', color: 'error' }
  }

  // 3. Date Check
  if (!item.lastPaymentDate) {
    // Treat no date as Stopped (or should we handle differently? Assuming stopped/old)
    return { text: 'متوقف', color: 'warning' }
  }

  const lastPayment = new Date(item.lastPaymentDate)
  const today = new Date()
  const diffTime = Math.abs(today - lastPayment)
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24))

  // User: "if lastPaymentDate <= before 365 days" (older than a year)
  // Logic: payment date is in the past.
  // If payment date is 2024-01-01 and today is 2025-01-09. Diff is > 365.
  // "Last payment <= 1 year ago" means the DATE is older (smaller value).
  
  const oneYearAgo = new Date()

  oneYearAgo.setDate(oneYearAgo.getDate() - 365)

  if (lastPayment <= oneYearAgo) {
    return { text: 'متوقف', color: 'warning' }
  } else {
    return { text: 'مستمر', color: 'success' }
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

// دالة لإرسال التعديل إلى الـ API
async function submitDiscount() {
  if (selectedSale.value) {
    const updatedSale = {
      customerSaleID: selectedSale.value.customerSaleID,
      discountAmountTotalDenar: formData.value.discountAmountTotal,
      discountAmountTotalDayDenar: formData.value.discountAmountDay,
      dateCreate: formData.value.dateCreate,
    }

    try {
      const authHeader = getAuthHeaders()

      const response = await axios.put(
        `${apiUrl}CustomersSales/CustomersSales_UpdateDiscount/${selectedSale.value.customerSaleID}`,
        updatedSale,
        { headers: authHeader },
      )

      // تحديث البيانات في الـ VDataTable بعد نجاح التعديل
      const index = salesData.value.findIndex(sale => sale.customerSaleID === selectedSale.value.customerSaleID)
      if (index !== -1) {
        salesData.value[index].discountAmountTotalDenar = formData.value.discountAmountTotal
        salesData.value[index].discountAmountTotalDayDenar = formData.value.discountAmountDay
      }
      discountDialog.value = false
    } catch (error) {
      console.error('حدث خطأ أثناء تعديل المبيع:', error)
      alert('حدث خطأ أثناء تعديل المبيع.')
    }
  }
}


const formattedDiscountAmountTotal = computed({
  get() {
    return formData.value.discountAmountTotal !== ''
      ? Number(formData.value.discountAmountTotal).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    formData.value.discountAmountTotal = isNaN(numeric) ? '' : numeric
  },
})

const formattedAmountDayTotal = computed({
  get() {
    return formData.value.amountDayTotal !== ''
      ? Number(formData.value.amountDayTotal).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    formData.value.amountDayTotal = isNaN(numeric) ? '' : numeric
  },
})



const formattedDiscountAmountDay = computed({
  get() {
    return formData.value.discountAmountDay !== ''
      ? Number(formData.value.discountAmountDay).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    formData.value.discountAmountDay = isNaN(numeric) ? '' : numeric
  },
})


const formattedDiscountAmountTotalSale = computed({
  get() {
    return formData.value.discountAmountTotal !== ''
      ? Number(formData.value.discountAmountTotal).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    formData.value.discountAmountTotal = isNaN(numeric) ? '' : numeric
  },
})


const formattedAmountDayTotalFinal = computed({
  get() {
    return formData.value.amountDayTotalFinal !== ''
      ? Number(formData.value.amountDayTotalFinal).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    formData.value.amountDayTotalFinal = isNaN(numeric) ? '' : numeric
  },
})


const formattedDiscountAmountDaySale = computed({
  get() {
    return formData.value.discountAmountDay !== ''
      ? Number(formData.value.discountAmountDay).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    formData.value.discountAmountDay = isNaN(numeric) ? '' : numeric
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

const colSlots = {
  status: 'item.status',
  boundNumber: 'item.boundNumber',
  itemsNames: 'item.itemsNames',
  numberOfItemsSales: 'item.numberOfItemsSales',
  customerName: 'item.customerName',
  phoneNumber: 'item.phoneNumber',
  storeName: 'item.storeName',
  dateCreate: 'item.dateCreate',
  delegateName: 'item.delegateName',
  amountTotalCostDenar: 'item.amountTotalCostDenar',
  amountTotalDenar: 'item.amountTotalDenar',
  discountAmountTotalDenar: 'item.discountAmountTotalDenar',
  amountTotalSalesDenar: 'item.amountTotalSalesDenar',
  amountTotalDayDenar: 'item.amountTotalDayDenar',
  discountAmountTotalDayDenar: 'item.discountAmountTotalDayDenar',
  amountDaySalesDenar: 'item.amountDaySalesDenar',
  receiptsTotal: 'item.receiptsTotal',
  amountRemaining: 'item.amountRemaining',
  countReceiptDevice: 'item.countReceiptDevice',
  lastPaymentDate: 'item.lastPaymentDate',
  saleName: 'item.saleName',
  receiptName: 'item.receiptName',
  updateDiscount: 'item.updateDiscount',
  delete: 'item.delete',
}
</script>

<template>
  <!-- عرض الإحصائيات الإجمالية -->
  <!-- عرض الإحصائيات الإجمالية -->
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
            من التاريخ
          </VLabel>
          <AppTextField
            v-model="filters.fromDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            @input="handleDateChangeFromDate"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            إلى التاريخ
          </VLabel>
          <AppTextField
            v-model="filters.toDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            @input="handleDateChangeToDate"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            المندوب
          </VLabel>
          <VAutocomplete
            v-model="filters.delegateID"
            prepend-inner-icon="tabler-user"
            :items="delegateOption.map(d => ({ title: d.delegateName, value: d.delegateID }))"
            placeholder="اختر المندوب"
            clearable
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            اسم العميل
          </VLabel>
          <AppTextField
            v-model="filters.customerName"
            placeholder="ادخل اسم العميل"
            clearable
            prepend-inner-icon="tabler-user"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            المباع
          </VLabel>
          <AppTextField
            v-model="filters.itemName"
            placeholder="ادخل اسم المنتج"
            clearable
            prepend-inner-icon="tabler-box"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            اسم البائع
          </VLabel>
          <AppTextField
            v-model="filters.saleName"
            placeholder="ادخل اسم البائع"
            clearable
            prepend-inner-icon="tabler-cash-banknote"
          />
        </VCol>
      </VRow>

      <VRow class="mb-4">
        <VCol
          cols="12"
          class="d-flex flex-wrap justify-end gap-2"
        >
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            prepend-icon="tabler-search"
            @click="fetchSales"
          >
            بحث
          </VBtn>
          <!--
            <VBtn
            color="success"
            prepend-icon="tabler-plus"
            @click="addSales"
            >
            اضافة مبيع جديد
            </VBtn> 
          -->
          <VBtn
            color="success"
            prepend-icon="tabler-file-export"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VCol>
      </VRow>

      <VRow>
        <VDataTable
          class="text-no-wrap custom-data-table"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          :headers="headers"
          :items="salesData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
        >
          <template #[colSlots.customerName]="{ item }">
            <div>
              {{ item.customerName }}
            </div>
          </template>
          <template #[colSlots.boundNumber]="{ item }">
            <div>
              {{ item.boundNumber || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.itemsNames]="{ item }">
            <div>
              {{ item.itemsNames || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.numberOfItemsSales]="{ item }">
            <div>
              {{ item.numberOfItemsSales || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.phoneNumber]="{ item }">
            <div>
              {{ item.phoneNumber || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.storeName]="{ item }">
            <div>
              {{ item.storeName || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.dateCreate]="{ item }">
            <div>
              {{ item.dateCreate ? new Date(item.dateCreate).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.delegateName]="{ item }">
            <div>
              {{ item.delegateName || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.amountTotalCostDenar]="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.amountTotalCostDenar) }}
            </div>
          </template>
          <template #[colSlots.amountTotalDenar]="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.amountTotalDenar) }}
            </div>
          </template>
          <template #[colSlots.discountAmountTotalDenar]="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.discountAmountTotalDenar) }}
            </div>
          </template>
          <template #[colSlots.amountTotalSalesDenar]="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.amountTotalSalesDenar) }}
            </div>
          </template>
          <template #[colSlots.amountTotalDayDenar]="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.amountTotalDayDenar) }}
            </div>
          </template>
          <template #[colSlots.discountAmountTotalDayDenar]="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.discountAmountTotalDayDenar) }}
            </div>
          </template>
          <template #[colSlots.amountDaySalesDenar]="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.amountDaySalesDenar) }}
            </div>
          </template>
          <template #[colSlots.receiptsTotal]="{ item }">
            <div class="premium-amount amt-total-receipts">
              {{ formattedNumber(item.receiptsTotal) }}
            </div>
          </template>
          <template #[colSlots.amountRemaining]="{ item }">
            <div class="premium-amount amt-remaining">
              {{ formattedNumber(item.amountRemaining) }}
            </div>
          </template>
          <template #[colSlots.countReceiptDevice]="{ item }">
            <div>
              {{ item.countReceiptDevice || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.lastPaymentDate]="{ item }">
            <div>
              {{ item.lastPaymentDate ? new Date(item.lastPaymentDate).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.updateDiscount]>
            <!--
              <div class="d-flex gap-1">
              <VBtn
              color="primary"
              style="margin-block-end: 10px;"
              prepend-icon="tabler-pencil"
              @click="openDiscountDialog(item)"
              >
              تعديل المبيع
              </VBtn>
              </div> 
            -->
          </template>
          <template #[colSlots.status]="{ item }">
            <VChip
              :color="getStatus(item).color"
              size="small"
              class="font-weight-medium"
            >
              {{ getStatus(item).text }}
            </VChip>
          </template>
          <template #[colSlots.delete]>
            <!--
              <div class="d-flex gap-1">
              <VBtn
              color="error"
              style="margin-block-end: 10px;"
              prepend-icon="tabler-trash"
              @click="openDeleteDialog(item.customerSaleID)"
              >
              حذف
              </VBtn>
              </div> 
            -->
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>
  <VDialog
    v-model="discountDialog"
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
              icon="tabler-pencil"
              size="28"
            />
          </VAvatar>
          <div>
            <h4 class="text-h6 font-weight-bold">
              تعديل المبيع
            </h4>
            <span class="text-caption text-medium-emphasis">تعديل تفاصيل البيع والخصومات</span>
          </div>
        </div>
        <VBtn
          icon
          variant="text"
          color="secondary"
          size="small"
          @click="discountDialog = false"
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
            <VLabel class="mb-2">
              خصم المبلغ الكلي
            </VLabel>
            <AppTextField
              v-model="formattedDiscountAmountTotalSale"
              type="text"
              required
              prepend-inner-icon="tabler-currency-dollar"
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              خصم المبلغ اليومي
            </VLabel>
            <AppTextField
              v-model="formattedDiscountAmountDaySale"
              type="text"
              required
              prepend-inner-icon="tabler-currency-dollar"
              @keypress="onNumberInput"
            />
          </VCol>
          <VCol
            md="6"
            cols="12"
          >
            <VLabel class="mb-2">
              تاريخ المبيع
            </VLabel>
            <AppTextField
              v-model="formData.dateCreate"
              type="date"
              required
              prepend-inner-icon="tabler-calendar"
              @input="handleDateCreate"
            />
          </VCol>
        </VRow>
      </VCardText>
      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="discountDialog = false"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إلغاء
        </VBtn>
        <VBtn
          color="primary"
          @click="submitDiscount"
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

  <!-- Dialog لإضافة السند الجديد -->

  <VDialog
    v-model="addDialog"
    max-width="1600px"
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
              إضافة مبيع جديد
            </h4>
            <span class="text-caption text-medium-emphasis">أدخل تفاصيل عملية البيع الجديدة</span>
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
              المندوب
            </VLabel>
            <AppAutocomplete
              v-model="formData.delegateID"
              :items="delegateOption.map(d => ({ title: d.delegateName, value: d.delegateID }))"
              placeholder="اختر المندوب"
              required
              clearable
              clear-icon="tabler-x"
              prepend-inner-icon="tabler-user"
              @update:model-value="changeDelegate"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              المخزن
            </VLabel>
            <AppAutocomplete
              v-model="formData.storeID"
              :items="storesOptions.map(s => ({ title: s.storeName, value: s.storeID }))"
              placeholder="اختر المخزن"
              required
              clearable
              clear-icon="tabler-x"
              prepend-inner-icon="tabler-building"
              @update:model-value="changeStore"
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
              v-model="formData.dateCreate"
              type="date"
              required
              prepend-inner-icon="tabler-calendar"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              العميل
            </VLabel>
            <AppTextField
              v-model="formData.customerName"
              placeholder="اسم العميل"
              required
              prepend-inner-icon="tabler-user"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              رقم الهاتف
            </VLabel>
            <AppTextField
              v-model="formData.phoneNumber"
              type="number"
              required
              prepend-inner-icon="tabler-phone"
              @input="limitPhoneLength"
              @keypress="e => !/[0-9]/.test(e.key) && e.preventDefault()"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              العنوان
            </VLabel>
            <AppTextField
              v-model="formData.address"
              placeholder="العنوان"
              required
              prepend-inner-icon="tabler-map-pin"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              اسم المحل
            </VLabel>
            <AppTextField
              v-model="formData.shopName"
              placeholder="اسم المحل"
              required
              prepend-inner-icon="tabler-building"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              اسم البائع
            </VLabel>
            <AppTextField
              v-model="formData.saleName"
              placeholder="اسم البائع"
              required
              prepend-inner-icon="tabler-clipboard"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              اسم الجابي
            </VLabel>
            <AppTextField
              v-model="formData.receiptName"
              placeholder="اسم الجابي"
              required
              prepend-inner-icon="tabler-clipboard-check"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              الملاحظات
            </VLabel>
            <AppTextField
              v-model="formData.notes"
              placeholder="أية ملاحظات"
              required
              prepend-inner-icon="tabler-note"
            />
          </VCol>
        </VRow>

        <!-- قائمة المحتويات -->
        <VRow>
          <VCol
            v-for="(content, index) in formData.contents"
            :key="index"
            class="mb-3"
            cols="12"
          >
            <VRow>
              <VCol
                md="2"
                cols="12"
              >
                <VLabel class="mb-2">
                  العنصر
                </VLabel>
                <AppAutocomplete
                  v-model="content.itemID"
                  :items="itemsOptions.map(i => ({ title: i.itemName, value: i.itemID }))"
                  placeholder="اختر العنصر"
                  required
                  clearable
                  clear-icon="tabler-x"
                  prepend-inner-icon="tabler-box"
                  @update:model-value="() => updateItemPrice(index)"
                />
              </VCol>

              <VCol
                md="1"
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
                  prepend-inner-icon="tabler-list-numbers"
                  @input="() => calculateTotalPrice(index)"
                />
              </VCol>

              <VCol
                md="2"
                cols="12"
              >
                <VLabel class="mb-2">
                  سعر البيع
                </VLabel>
                <AppTextField
                  :value="formattedNumber(content.itemPriceDenar)"
                  readonly
                  prepend-inner-icon="tabler-currency-dollar"
                />
              </VCol>

              <VCol
                md="2"
                cols="12"
              >
                <VLabel class="mb-2">
                  القسط
                </VLabel>
                <AppTextField
                  :value="formattedNumber(content.amountDayDenar)"
                  readonly
                  prepend-inner-icon="tabler-credit-card"
                />
              </VCol>

              <VCol
                md="2"
                cols="12"
              >
                <VLabel class="mb-2">
                  سعر البيع الكلي
                </VLabel>
                <AppTextField
                  :value="formattedNumber(content.totalItemPriceDenar)"
                  readonly
                  prepend-inner-icon="tabler-currency-dollar"
                />
              </VCol>

              <VCol
                md="2"
                cols="12"
              >
                <VLabel class="mb-2">
                  القسط الكلي
                </VLabel>
                <AppTextField
                  :value="formattedNumber(content.totalAmountDayDenar)"
                  readonly
                  prepend-inner-icon="tabler-credit-card"
                />
              </VCol>

              <VCol
                md="1"
                cols="12"
                class="d-flex align-end"
              >
                <VBtn
                  color="error"
                  prepend-icon="tabler-trash"
                  @click="formData.contents.splice(index, 1)"
                >
                  حذف
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
        <VLabel class="mb-2 mt-6">
          الإجماليات
        </VLabel>
        <VRow>
          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              إجمالي سعر البيع الكلي
            </VLabel>
            <AppTextField
              :value="formattedNumber(formData.amountPriceTotal)"
              readonly
              prepend-inner-icon="tabler-currency-dollar"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              خصم سعر البيع
            </VLabel>
            <AppTextField
              v-model="formattedDiscountAmountTotal"
              type="text"
              required
              prepend-inner-icon="tabler-currency-dollar"
              @keypress="onNumberInput"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              إجمالي سعر البيع النهائي
            </VLabel>
            <AppTextField
              :value="formattedNumber(formData.amountPriceTotalFinal)"
              readonly
              prepend-inner-icon="tabler-currency-dollar"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              القسط الكلي
            </VLabel>
            <AppTextField
              v-model="formattedAmountDayTotal"
              type="text"
              required
              prepend-inner-icon="tabler-credit-card"
              @keypress="onNumberInput"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              خصم القسط
            </VLabel>
            <AppTextField
              v-model="formattedDiscountAmountDay"
              type="text"
              required
              prepend-inner-icon="tabler-currency-dollar"
              @keypress="onNumberInput"
            />
          </VCol>

          <VCol
            md="3"
            cols="12"
          >
            <VLabel class="mb-2">
              القسط النهائي
            </VLabel>
            <AppTextField
              v-model="formattedAmountDayTotalFinal"
              type="text"
              required
              prepend-inner-icon="tabler-credit-card"
              @keypress="onNumberInput"
            />
          </VCol>
        </VRow>
      </VCardText>

      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="closeDialog"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          @click="submitForm"
        >
          <VIcon
            icon="tabler-check"
            class="me-2"
          />
          إضافة المبيع
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>


  <!-- Dialog تأكيد الحذف -->
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
      <VCardActions class="justify-end gap-3 pa-4">
        <VBtn
          variant="tonal"
          color="secondary"
          @click="confirmDeleteDialog = false"
        >
          <VIcon
            icon="tabler-x"
            class="me-2"
          />
          إلغاء
        </VBtn>
        <VBtn
          color="error"
          @click="deleteBuy"
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
