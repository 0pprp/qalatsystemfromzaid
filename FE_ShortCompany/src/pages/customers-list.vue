<script setup>
import { getAuthHeaders } from '@/services/tokenService' // تأكد من استيراد المكونات حسب المكتبة المستخدمة
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')
const salesData = ref([])

// بيانات العملاء وقائمة المندوبين
const customersData = ref([])
const delegateList = ref([])
const addDialog = ref(false)
const storesOptions = ref([])
const delegateOption = ref([])
const itemsOptions = ref([])
const dialogEdit = ref(false)
const dialogMove = ref(false)

// حالة التحميل
const loading = ref(false)
const paymentDialog = ref(false)

const paymentDetails = ref({
  customerName: '',
  delegateName: '',
  amountTotalSales: 0,
  amountDaySales: 0,
  receiptsTotal: 0,
  amountRemaining: 0,
  paymentAmount: 0,
  paymentDate: new Date().toLocaleDateString('en-CA'),
})

const filterDateFrom = ref(null)
const filterDateTo = ref(null)

// حساب مجموع مبالغ التسديدات (عمود amountDenar)
const totalPaymentsSum = computed(() =>
  filteredPaymentsData.value.reduce((sum, payment) => sum + (Number(payment.amountDenar) || 0), 0),
)

// حساب عدد التسديدات (عدد السجلات)
const paymentsCount = computed(() => filteredPaymentsData.value.length)


const filteredPaymentsData = computed(() => {
  return paymentsData.value.filter(payment => {
    const date = new Date(payment.paymentDate)

    const from = filterDateFrom.value
      ? new Date(filterDateFrom.value) // بدون تعديل
      : null

    const to = filterDateTo.value
      ? new Date(filterDateTo.value)
      : null

    if (to) {
      to.setDate(to.getDate() + 1)
    }

    return (!from || date >= from) && (!to || date < to)
  })
})

function clearDateFilters() {
  filterDateFrom.value = null
  filterDateTo.value = null
}

// دالة فتح Dialog التسديد وملئ البيانات
function openPaymentDialog(item) {
  currentCustomerID.value = item.customerID
  paymentDetails.value.customerName = item.customerName
  paymentDetails.value.delegateName = item.delegateName
  paymentDetails.value.amountTotalSales = item.amountTotalSales || 0
  paymentDetails.value.amountDaySales = item.amountDaySales || 0
  paymentDetails.value.receiptsTotal = item.receiptsTotal || 0
  paymentDetails.value.amountRemaining = item.amountRemaining || 0
  paymentDetails.value.paymentAmount = item.amountDaySales || 0
  paymentDetails.value.paymentDate =new Date().toLocaleDateString('en-CA')
  paymentDialog.value = true
}

async function submitPayment() {
  try {
    if(paymentDetails.value.paymentAmount>=1000){
      if (paymentDetails.value.amountRemaining > 0) {
        if (paymentDetails.value.paymentAmount <= paymentDetails.value.amountRemaining) {
          const authHeader = getAuthHeaders()

          const paymentData = {
            customerID: currentCustomerID.value,
            paymentAmount: paymentDetails.value.paymentAmount,
            paymentDate: paymentDetails.value.paymentDate,
          }

          const response =  await axios.post(`${apiUrl}CustomersPayments/CustomersPayments_Create`, paymentData, { headers: authHeader })
          const customer = customersData.value.find(customer => customer.customerID === currentCustomerID.value)
          if (customer) {
            customer.receiptsTotal = parseFloat(customer.receiptsTotal) + parseFloat(paymentDetails.value.paymentAmount)
            customer.amountRemaining = parseFloat(customer.amountRemaining) - parseFloat(paymentDetails.value.paymentAmount)
            customersData.value = [...customersData.value]
          }
          if(response.data.amountDenar>0){
            paymentDialog.value = false
          }
        } else {
          alert('مبلغ التسديد أكبر من المتبقي.')
        }
      } else {
        alert('هذا العميل مصفر حسابه.')
      }
    }else {
      alert('يجب ان يكون مبلغ التسديد اكبر او يساوي 1000.')
    }
  } catch (error) {
    console.error('Error submitting payment:', error)
  }
}




// فلاتر البحث
const filters = ref({
  delegateID: null,      // 0 تعني "الجميع"
  textSearch: '',
  showType: 'الجميع', // الخيارات: 'الجميع'، 'الغير مصفرين'، 'المصفرين'، 'القانونية'
})

// دالة تنسيق الأرقام
const formattedNumber = num => {
  if (num === 0) return "0"
  if (num) return num.toLocaleString() + " دع "
  
  return "لا يوجد"
}

// تعريف عناوين جدول العملاء
const headers = [
  { title: 'التسديدات', key: 'showReceipt' },
  { title: 'العميل', key: 'customerName' },
  { title: 'الحالة', key: 'status' },
  { title: 'المندوب', key: 'delegateName' },
  { title: 'المباعة', key: 'itemsNames' },
  { title: 'التاريخ', key: 'dateSaleDevice' },
  { title: 'سعر البيع', key: 'amountTotalSales' },
  { title: 'سعر الشراء', key: 'costTotalSales' },
  { title: 'القسط', key: 'amountDaySales' },
  { title: 'الواصل', key: 'receiptsTotal' },
  { title: 'الباقي', key: 'amountRemaining' },
  { title: 'نسبة التسديد', key: 'receiptRateDevice' },
  { title: 'عدد التسديدات', key: 'countReceiptDevice' },
  { title: 'عدد الأيام', key: 'numberOfDayDevice' },
  { title: 'تاريخ اخر تسديد', key: 'lastPaymentDate' },
  { title: 'العنوان', key: 'address' },
  { title: 'الهاتف', key: 'phoneNumber' },
  { title: 'اسم المحل', key: 'shopName' },
  { title: 'اقرب نقطة', key: 'nearestFunctionPoint' },
  { title: 'اسم الجابي', key: 'receiptName' },
  { title: 'اسم البائع', key: 'saleName' },
  { title: 'الملاحظات', key: 'notes' },
]


// إجماليات بيانات العملاء
const totals = computed(() => [
  {
    icon: 'tabler-user', // عدد العملاء
    value: customersData.value.length,
    title: 'عدد العملاء',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
  {
    icon: 'tabler-wallet', // إجمالي سعر البيع
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.amountTotalSales || 0), 0),
    ),
    title: 'إجمالي سعر البيع',
    color: "success",
    gradient: "linear-gradient(135deg, #11998e 0%, #38ef7d 100%)",
  },
  {
    icon: 'tabler-wallet', // إجمالي سعر الشراء
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.costTotalSales || 0), 0),
    ),
    title: 'إجمالي سعر الشراء',
    color: "warning",
    gradient: "linear-gradient(135deg, #fce38a 0%, #f38181 100%)",
  },
  {
    icon: 'tabler-credit-card', // إجمالي القسط
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.amountDaySales || 0), 0),
    ),
    title: 'إجمالي القسط',
    color: "info",
    gradient: "linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)",
  },
  {
    icon: 'tabler-currency-dollar', // إجمالي الواصل
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.receiptsTotal || 0), 0),
    ),
    title: 'إجمالي الواصل',
    color: "error",
    gradient: "linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)",
  },
  {
    icon: 'tabler-currency-dollar', // إجمالي المبلغ المتبقي
    value: formattedNumber(
      customersData.value.reduce((sum, customer) => sum + (customer.amountRemaining || 0), 0),
    ),
    title: 'إجمالي المبلغ المتبقي',
    color: "secondary",
    gradient: "linear-gradient(135deg, #667db6 0%, #0082c8 100%, #0082c8 100%, #667db6 100%)",
  },

  // {
  //   icon: 'tabler-check', // إجمالي عدد التسديدات
  //   value: customersData.value.reduce((sum, customer) => sum + (customer.countReceiptDevice || 0), 0),
  //   title: 'إجمالي عدد التسديدات',
  //   bgColor: '#F1C1A6',
  //   iconColor: 'green',
  // },
])


// متغيرات Dialog التسديدات وبياناتها
const paymentsDialog = ref(false)
const paymentsData = ref([])
const currentCustomerID = ref(null)
const customerPaymentIDToDelete = ref(null)
const confirmDeletePaymentDialog = ref(false)



// دالة جلب قائمة المندوبين
async function fetchDelegates() {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}Delegates/Delegates_GetDataAll`, { headers: authHeader })

    delegateOption.value = response.data
    delegateList.value = response.data
  } catch (error) {
    console.error(error)
  }
}

// دالة جلب بيانات العملاء باستخدام الفلاتر
async function fetchCustomers() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const delegateID = filters.value.delegateID || 0
    const textSearch = filters.value.textSearch || 'null'
    const showType = filters.value.showType || 'الجميع'
    const response = await axios.get(`${apiUrl}Customers/Customers_GetAll/${delegateID}&&${textSearch}&&${showType}`, { headers: authHeader })

    customersData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// دالة تصدير بيانات العملاء إلى Excel
function exportToExcel() {
  const dataToExport = customersData.value.map(customer => ({
    'العميل': customer.customerName || 'لا يوجد',
    'المباعة': customer.itemsNames || 'لا يوجد',
    'التاريخ': customer.dateSaleDevice ? new Date(customer.dateSaleDevice).toLocaleDateString('en-CA') : 'لا يوجد',
    'سعر البيع': customer.amountTotalSales || 'لا يوجد',
    'سعر الشراء': customer.costTotalSales || 'لا يوجد',
    'القسط': customer.amountDaySales || 'لا يوجد',
    'الواصل': customer.receiptsTotal || 'لا يوجد',
    'الباقي': customer.amountRemaining || 'لا يوجد',
    'نسبة التسديد': customer.receiptRateDevice !== undefined && customer.receiptRateDevice !== null ? `${customer.receiptRateDevice}%` : '0%',
    'عدد التسديدات': customer.countReceiptDevice || 'لا يوجد',
    'عدد الأيام': customer.numberOfDayDevice || 'لا يوجد',
    'تاريخ اخر تسديد': customer.lastPaymentDate ? new Date(customer.lastPaymentDate).toLocaleDateString('en-CA')   : 'لا يوجد',
    'العنوان': customer.address || 'لا يوجد',
    'الهاتف': customer.phoneNumber || 'لا يوجد',
    'اسم المحل': customer.shopName || 'لا يوجد',
    'اقرب نقطة': customer.nearestFunctionPoint || 'لا يوجد',
    'المندوب': customer.delegateName || 'لا يوجد',
    'اسم الجابي': customer.receiptName || 'لا يوجد',
    'اسم البائع': customer.saleName || 'لا يوجد',
    'الملاحظات': customer.notes || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Customers")
  XLSX.writeFile(workbook, "customers.xlsx")
}



// دالة تصدير بيانات العملاء إلى Excel
function exportReceiptToExcel() {
  const dataToExport = paymentsData.value.map(payment => ({
    'العميل': payment.customerName || 'لا يوجد',
    'التاريخ': payment.paymentDate ? new Date(payment.paymentDate).toLocaleDateString('en-CA')   : 'لا يوجد',
    'التسديد': payment.amountDenar || 0,
    'المندوب': payment.delegateName || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "receipts")
  XLSX.writeFile(workbook, "receipts.xlsx")
}


// دالة جلب بيانات التسديدات لعميل معين
async function fetchPayments(customerID) {
  try {
    const authHeader = getAuthHeaders()
    const response = await axios.get(`${apiUrl}CustomersPayments/CustomersPayments_GetByCustomerID/${customerID}`, { headers: authHeader })

    paymentsData.value = response.data
  } catch (error) {
    console.error(error)
  }
}

// دالة فتح Dialog التسديدات
function openPaymentsDialog(customerID) {
  clearDateFilters()
  currentCustomerID.value = customerID
  fetchPayments(customerID)
  paymentsDialog.value = true
}

// دالة فتح Google Maps باستخدام الموقع
function openInGoogleMaps(location) {
  if (!location) return
  const url = `https://www.google.com/maps/search/?api=1&query=${location}`

  window.open(url, '_blank')
}

function deleteReceipt(customerPaymentID) {
  customerPaymentIDToDelete.value = customerPaymentID
  confirmDeletePaymentDialog.value = true
}


async function deleteReceiptData() {
  if (customerPaymentIDToDelete.value) {
    try {
      const authHeader = getAuthHeaders()

      await axios.delete(`${apiUrl}CustomersPayments/CustomersPayments_Delete/${customerPaymentIDToDelete.value}`, { headers: authHeader })
      paymentsData.value = paymentsData.value.filter(sale => sale.customerPaymentID !== customerPaymentIDToDelete.value)
      confirmDeletePaymentDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}



function addSaleSelect(item){
  formData.value.delegateID = item.delegateID
  formData.value.customerName = item.customerName
  formData.value.phoneNumber = item.phoneNumber  ||'078'
  formData.value.address = item.address ||'لا يوجد'
  formData.value.shopName = item.shopName ||'لا يوجد'
  formData.value.nearestFunctionPoint = item.nearestFunctionPoint  ||'لا يوجد'
  formData.value.saleName = item.saleName  ||'لا يوجد'
  formData.value.receiptName =delegateOption.value.find(c=>c.delegateID===item.delegateID).receiptName || ''
  formData.value.notes = item.notes  ||'لا يوجد'
  addDialog.value = true
}

function updateCustomer(item){
  formData.value.delegateID = item.delegateID
  formData.value.customerID = item.customerID
  formData.value.customerName = item.customerName
  formData.value.phoneNumber = item.phoneNumber  ||'078'
  formData.value.address = item.address ||'لا يوجد'
  formData.value.shopName = item.shopName ||'لا يوجد'
  formData.value.nearestFunctionPoint = item.nearestFunctionPoint  ||'لا يوجد'
  formData.value.saleName = item.saleName  ||'لا يوجد'
  formData.value.receiptName =delegateOption.value.find(c=>c.delegateID===item.delegateID).receiptName || ''
  formData.value.notes = item.notes  ||'لا يوجد'
  dialogEdit.value = true
}

function moveCustomerDialog(item){
  formData.value.customerID = item.customerID
  dialogMove.value = true
}

async function moveCustomerLegal(item) {
  try {
    const headers = getAuthHeaders()

    await axios.put(`${apiUrl}Customers/Customers_MoveLegal/${item.customerID}`, {}, {
      headers: {
        ...headers,
        'Content-Type': 'application/json',
      },
    })

    if (typeof item.isLegal === 'string') {
      item.isLegal = item.isLegal === 'true'
    }
    item.isLegal = !item.isLegal

    dialogMove.value = false
  } catch (error) {
    console.error(error)
  }
}



async function moveCustomer() {
  try {
    const headers = getAuthHeaders()

    const response = await axios.put(`${apiUrl}Customers/Customers_Move/${formData.value.customerID}&&${formData.value.moveDelegateID}`, {}, {
      headers: {
        ...headers,
        'Content-Type': 'application/json',
      },
    })

    const customerIndex = customersData.value.findIndex(
      customer => customer.customerID === formData.value.customerID,
    )

    if (customerIndex !== -1) {
      customersData.value[customerIndex] = { ...response.data }
    }
    dialogMove.value = false
  } catch (error) {
    console.error(error)
  }
}

async function editCustomer() {
  try {
    const headers = getAuthHeaders()

    const data = {
      customerID: formData.value.customerID,
      customerName: formData.value.customerName,
      phoneNumber: formData.value.phoneNumber,
      address: formData.value.address,
      shopName: formData.value.shopName,
      saleName: formData.value.saleName,
      notes: formData.value.notes,
    }

    const response = await axios.put(`${apiUrl}Customers/Customers_Update/${formData.value.customerID}`, data, {
      headers: {
        ...headers,
        'Content-Type': 'application/json',
      },
    })

    const customerIndex = customersData.value.findIndex(
      customer => customer.customerID === formData.value.customerID,
    )

    if (customerIndex !== -1) {
      customersData.value[customerIndex] = { ...response.data }
    }
    dialogEdit.value = false
  } catch (error) {
    console.error(error)
  }
}



const formData = ref({
  delegateID: '',
  moveDelegateID: '',
  customerID: '',
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


// إغلاق الـ Dialog
function closeDialog() {
  addDialog.value = false
}


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

async function changeDelegate(delegateID) {
  formData.value.receiptName=delegateOption.value.find(c=>c.delegateID===delegateID).receiptName || ''
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

    const response = await axios.post(`${apiUrl}Customers/CustomersSalesCustomer_Create`, dataToSubmit, {
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

    customersData.value.push(response.data)
    addDialog.value = false
  } catch (error) {
    console.error('حدث خطأ أثناء إضافة السند:', error)
    alert('حدث خطأ أثناء إضافة السند.')
  }
}


function addSales() {
  addDialog.value = true
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


function getLegalColor(isLegal) {
  return (isLegal === true || isLegal === 'true') ? 'error' : 'primary'
}

function getLegalText(isLegal) {
  return (isLegal === true || isLegal === 'true') ? 'قانونية' : 'بدون قانونية'
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


const formattedPaymentAmount = computed({
  get() {
    return paymentDetails.value.paymentAmount !== ''
      ? Number(paymentDetails.value.paymentAmount).toLocaleString() + ' دع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(value.replace(/[^\d.-]/g, '')) // إزالة كل شيء غير الأرقام والنقاط والشارحة

    paymentDetails.value.paymentAmount = isNaN(numeric) ? '' : numeric
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
  fetchDelegates()
  fetchStores()
})

const colSlots = {
  addSale: 'item.addSale',
  showReceipt: 'item.showReceipt',
  addReceipt: 'item.addReceipt',
  move: 'item.move',
  isLegal: 'item.isLegal',
  customerName: 'item.customerName',
  status: 'item.status',
  itemsNames: 'item.itemsNames',
  dateSaleDevice: 'item.dateSaleDevice',
  amountTotalSales: 'item.amountTotalSales',
  costTotalSales: 'item.costTotalSales',
  amountDaySales: 'item.amountDaySales',
  receiptsTotal: 'item.receiptsTotal',
  amountRemaining: 'item.amountRemaining',
  receiptRateDevice: 'item.receiptRateDevice',
  countReceiptDevice: 'item.countReceiptDevice',
  numberOfDayDevice: 'item.numberOfDayDevice',
  lastPaymentDate: 'item.lastPaymentDate',
  address: 'item.address',
  phoneNumber: 'item.phoneNumber',
  shopName: 'item.shopName',
  nearestFunctionPoint: 'item.nearestFunctionPoint',
  delegateName: 'item.delegateName',
  receiptName: 'item.receiptName',
  saleName: 'item.saleName',
  notes: 'item.notes',
  update: 'item.update',
  amountDenar: 'item.amountDenar',
  paymentDate: 'item.paymentDate',
  location: 'item.location',
  showMap: 'item.showMap',
  delete: 'item.delete',
}
</script>

<template>
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
      <!-- فلاتر البحث -->
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            بحث (العميل / الهاتف)
          </VLabel>
          <AppTextField
            v-model="filters.textSearch"
            placeholder="أدخل اسم العميل أو الهاتف"
            clearable
            prepend-inner-icon="tabler-search"
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
            :items="delegateList.map(delegate => ({ title: delegate.delegateName, value: delegate.delegateID }))"
            placeholder="اختر المندوب"
            clearable
            prepend-inner-icon="tabler-user"
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
            prepend-inner-icon="tabler-category"
            :items="['الجميع', 'الغير مصفرين', 'المصفرين', 'القانونية', 'المستمرين', 'المتوقفين']"
          />
        </VCol>
        <VCol
          cols="12"
          md="6"
          class="d-flex align-center justify-end flex-wrap gap-2"
          style="margin-block-start: 20px;"
        >
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            prepend-icon="tabler-search"
            @click="fetchCustomers"
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
            variant="tonal"
            color="success"
            prepend-icon="tabler-upload"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VCol>
      </VRow>
      <!-- عرض بيانات العملاء -->
      <VRow>
        <VDataTable
          :headers="headers"
          :items="customersData"
          :items-per-page="50"
          style="overflow: hidden; block-size: 100%;white-space: nowrap;"
          items-per-page-text="عدد السجل"
          class="text-no-wrap custom-data-table"
        >
          <!-- تنسيق الحقول داخل div بعرض 200px -->
          <template #[colSlots.customerName]="{ item }">
            <div>
              {{ item.customerName }}
            </div>
          </template>
          <template #[colSlots.status]="{ item }">
            <VChip
              :color="getStatus(item).color"
              class="font-weight-bold"
              size="small"
              label
            >
              {{ getStatus(item).text }}
            </VChip>
          </template>
          <template #[colSlots.itemsNames]="{ item }">
            <div>
              {{ item.itemsNames || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.dateSaleDevice]="{ item }">
            <div>
              {{ item.dateSaleDevice ? new Date(item.dateSaleDevice).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.amountTotalSales]="{ item }">
            <div class="premium-amount amt-installment">
              {{ formattedNumber(item.amountTotalSales) }}
            </div>
          </template>
          <template #[colSlots.costTotalSales]="{ item }">
            <div class="premium-amount amt-total-sales">
              {{ formattedNumber(item.costTotalSales) }}
            </div>
          </template>
          <template #[colSlots.amountDaySales]="{ item }">
            <div class="premium-amount amt-paid-yesterday">
              {{ formattedNumber(item.amountDaySales) }}
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
          <template #[colSlots.receiptRateDevice]="{ item }">
            <div>
              {{ item.receiptRateDevice !== undefined && item.receiptRateDevice !== null ? item.receiptRateDevice + '%' : '0%' }}
            </div>
          </template>
          <template #[colSlots.countReceiptDevice]="{ item }">
            <div>
              {{ item.countReceiptDevice !== undefined ? item.countReceiptDevice : 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.numberOfDayDevice]="{ item }">
            <div>
              {{ item.numberOfDayDevice !== undefined ? item.numberOfDayDevice : 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.lastPaymentDate]="{ item }">
            <div>
              {{ item.lastPaymentDate ? new Date(item.lastPaymentDate).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.address]="{ item }">
            <div>
              {{ item.address || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.phoneNumber]="{ item }">
            <div>
              {{ item.phoneNumber || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.shopName]="{ item }">
            <div>
              {{ item.shopName || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.nearestFunctionPoint]="{ item }">
            <div>
              {{ item.nearestFunctionPoint || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.delegateName]="{ item }">
            <div>
              {{ item.delegateName || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.receiptName]="{ item }">
            <div>
              {{ item.receiptName || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.saleName]="{ item }">
            <div>
              {{ item.saleName || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.notes]="{ item }">
            <div>
              {{ item.notes || 'لا يوجد' }}
            </div>
          </template>
          <template #[colSlots.showReceipt]="{ item }">
            <div class="d-flex gap-1">
              <VBtn
                color="primary"
                style="margin-block-end: 0;"
                prepend-icon="tabler-receipt"
                @click="openPaymentsDialog(item.customerID)"
              >
                التسديدات
              </VBtn>
            </div>
          </template>

          <template #[colSlots.addReceipt]>
            <!--
              <div class="d-flex gap-1">
              <VBtn
              color="primary"
              style="margin-block-end: 0;"
              prepend-icon="tabler-credit-card"
              @click="openPaymentDialog(item)"
              >
              تسديد
              </VBtn>
              </div> 
            -->
          </template>

          <template #[colSlots.addSale]>
            <!--
              <div class="d-flex gap-1">
              <VBtn
              color="primary"
              style="margin-block-end: 0;"
              prepend-icon="tabler-shopping-cart-plus"
              @click="addSaleSelect(item)"
              >
              إضافة مبيع
              </VBtn>
              </div> 
            -->
          </template>

          <template #[colSlots.update]>
            <!--
              <div class="d-flex gap-1">
              <VBtn
              color="primary"
              style="margin-block-end: 0;"
              prepend-icon="tabler-edit"
              @click="updateCustomer(item)"
              >
              تعديل
              </VBtn>
              </div> 
            -->
          </template>

          <template #[colSlots.move]>
            <!--
              <div class="d-flex gap-1">
              <VBtn
              color="primary"
              style="margin-block-end: 0;"
              prepend-icon="tabler-arrows-right-left"
              @click="moveCustomerDialog(item)"
              >
              نقل
              </VBtn>
              </div> 
            -->
          </template>

          <template #[colSlots.isLegal]>
            <!--
              <div class="d-flex gap-1">
              <VBtn
              :color="getLegalColor(item.isLegal)"
              class="mb-0"
              prepend-icon="tabler-shield-check"
              @click="moveCustomerLegal(item)"
              >
              {{ getLegalText(item.isLegal) }}
              </VBtn>
              </div> 
            -->
          </template>
        </VDataTable>
      </VRow>
    </VForm>

    <!-- Dialog لعرض التسديدات مع ملخص عدد ومجموع مبالغ التسديدات والموقع -->
    <!-- Dialog لعرض التسديدات مع إجماليات التسديدات -->
    <VDialog
      v-model="paymentsDialog"
      fullscreen
      hide-overlay
      transition="dialog-bottom-transition"
      content-class="modern-dialog-fullscreen"
    >
      <VCard>
        <div class="dialog-header pa-4 d-flex align-center justify-space-between bg-primary">
          <div class="d-flex align-center gap-3">
            <VIcon
              icon="tabler-receipt"
              color="white"
              size="28"
            />
            <div>
              <h4 class="text-h6 font-weight-bold text-white">
                التسديدات
              </h4>
              <span class="text-caption text-white">عرض وإدارة تسديدات العميل</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="white"
            @click="paymentsDialog = false"
          >
            <VIcon
              icon="tabler-x"
              size="24"
            />
          </VBtn>
        </div>
        
        <VCardText class="pa-6">
          <!-- إجماليات -->
          <VRow class="mb-6">
            <VCol
              md="6"
              cols="12"
            >
              <ModernStatCard
                title="عدد التسديدات"
                :value="paymentsCount"
                icon="tabler-hash"
                color="info"
                gradient="linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)"
              />
            </VCol>
            <VCol
              md="6"
              cols="12"
            >
              <ModernStatCard
                title="مجموع التسديدات"
                :value="formattedNumber(totalPaymentsSum)"
                icon="tabler-sum"
                color="success"
                gradient="linear-gradient(135deg, #11998e 0%, #38ef7d 100%)"
              />
            </VCol>
          </VRow>

          <!-- فلاتر التاريخ -->
          <VRow class="mb-4 align-end">
            <VCol
              cols="12"
              md="4"
            >
              <VLabel class="mb-2 font-weight-medium">
                من تاريخ
              </VLabel>
              <AppTextField
                v-model="filterDateFrom"
                prepend-inner-icon="tabler-calendar"
                type="date"
                clearable
              />
            </VCol>
            <VCol
              cols="12"
              md="4"
            >
              <VLabel class="mb-2 font-weight-medium">
                إلى تاريخ
              </VLabel>
              <AppTextField
                v-model="filterDateTo"
                prepend-inner-icon="tabler-calendar"
                type="date"
                clearable
              />
            </VCol>
            <VCol
              cols="12"
              md="4"
              class="d-flex gap-2 justify-end"
            >
              <VBtn
                color="primary"
                prepend-icon="tabler-refresh"
                @click="clearDateFilters"
              >
                عرض الجميع
              </VBtn>
              <VBtn
                color="success"
                prepend-icon="tabler-file-export"
                @click="exportReceiptToExcel"
              >
                تصدير إلى Excel
              </VBtn>
            </VCol>
          </VRow>

          <!-- جدول التسديدات -->
          <VDataTable
            :headers="[
              { title: 'العميل', key: 'customerName' },
              { title: 'مبلغ التسديد', key: 'amountDenar' },
              { title: 'تاريخ التسديد', key: 'paymentDate' },
              { title: 'المندوب', key: 'delegateName' },
              { title: 'الموقع', key: 'location' },
              { title: 'عرض على الخريطة', key: 'showMap' },
            ]"
            :items="filteredPaymentsData"
            :items-per-page="50"
            items-per-page-text="عدد السجل"
            class="text-no-wrap custom-data-table"
          >
            <template #[colSlots.customerName]="{ item }">
              <div class="font-weight-medium">
                {{ item.customerName }}
              </div>
            </template>
            <template #[colSlots.paymentDate]="{ item }">
              {{ item.paymentDate ? new Date(item.paymentDate).toLocaleDateString('en-CA') : 'لا يوجد' }}
            </template>
            <template #[colSlots.amountDenar]="{ item }">
              <VChip
                color="success"
                size="small"
                label
              >
                {{ formattedNumber(item.amountDenar) || 0 }}
              </VChip>
            </template>
            <template #[colSlots.location]="{ item }">
              <div
                class="text-truncate"
                style="max-inline-size: 200px;"
              >
                {{ item.location || 'لا يوجد' }}
              </div>
            </template>

            <template #[colSlots.showMap]="{ item }">
              <VBtn
                color="info"
                variant="tonal"
                size="small"
                prepend-icon="tabler-map-pin"
                @click="openInGoogleMaps(item.location)"
              >
                الخريطة
              </VBtn>
            </template>

            <template #[colSlots.delete]>
              <!--
                <VBtn
                color="error"
                variant="tonal"
                size="small"
                prepend-icon="tabler-trash"
                @click="deleteReceipt(item.customerPaymentID)"
                >
                حذف
                </VBtn> 
              -->
            </template>
          </VDataTable>
        </VCardText>
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

        <VCardText class="pa-4">
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

    <VDialog
      v-model="confirmDeletePaymentDialog"
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
              <span class="text-caption text-medium-emphasis">هل أنت متأكد من أنك تريد حذف هذا التسديد؟</span>
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
            @click="confirmDeletePaymentDialog = false"
          >
            <VIcon
              icon="tabler-x"
              class="me-2"
            />
            إلغاء
          </VBtn>
          <VBtn
            color="error"
            @click="deleteReceiptData"
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

    <VDialog
      v-model="paymentDialog"
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
                icon="tabler-credit-card"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                التسديد
              </h4>
              <span class="text-caption text-medium-emphasis">إضافة تسديد جديد</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="secondary"
            @click="paymentDialog = false"
          >
            <VIcon
              icon="tabler-x"
              size="24"
            />
          </VBtn>
        </div>

        <VCardText class="pa-4">
          <VForm>
            <VRow>
              <VCol cols="12">
                <VLabel class="mb-2">
                  اسم العميل
                </VLabel>
                <VTextField
                  v-model="paymentDetails.customerName"
                  readonly
                  prepend-inner-icon="tabler-user"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  اسم المندوب
                </VLabel>
                <VTextField
                  v-model="paymentDetails.delegateName"
                  readonly
                  prepend-inner-icon="tabler-briefcase"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  سعر البيع
                </VLabel>
                <VTextField
                  :value="formattedNumber(paymentDetails.amountTotalSales)"
                  readonly
                  prepend-inner-icon="tabler-currency-dollar"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  القسط
                </VLabel>
                <VTextField
                  :value="formattedNumber(paymentDetails.amountDaySales)"
                  readonly
                  prepend-inner-icon="tabler-currency-dollar"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  الواصل
                </VLabel>
                <VTextField
                  :value="formattedNumber(paymentDetails.receiptsTotal)"
                  readonly
                  prepend-inner-icon="tabler-currency-dollar"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  الباقي
                </VLabel>
                <VTextField
                  :value="formattedNumber(paymentDetails.amountRemaining)"
                  readonly
                  prepend-inner-icon="tabler-alert-circle"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  مبلغ التسديد
                </VLabel>
                <VTextField
                  v-model="formattedPaymentAmount"
                  type="text"
                  prepend-inner-icon="tabler-currency-dollar"
                  @keypress="onNumberInput"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  تاريخ التسديد
                </VLabel>
                <VTextField
                  v-model="paymentDetails.paymentDate"
                  type="date"
                  prepend-inner-icon="tabler-calendar"
                />
              </VCol>
            </VRow>
          </VForm>
        </VCardText>

        <VCardActions class="justify-end gap-3 pa-4">
          <VBtn
            variant="tonal"
            color="secondary"
            @click="paymentDialog = false"
          >
            <VIcon
              icon="tabler-x"
              class="me-2"
            />
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            @click="submitPayment"
          >
            <VIcon
              icon="tabler-check"
              class="me-2"
            />
            تسديد
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

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
                icon="tabler-edit"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                تعديل بيانات العميل
              </h4>
              <span class="text-caption text-medium-emphasis">تحديث المعلومات الخاصة بالعميل</span>
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

        <VCardText class="pa-4">
          <VForm>
            <VRow>
              <VCol cols="12">
                <VLabel class="mb-2">
                  العميل
                </VLabel>
                <VTextField
                  v-model="formData.customerName"
                  prepend-inner-icon="tabler-user"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  رقم الهاتف
                </VLabel>
                <VTextField
                  v-model="formData.phoneNumber"
                  prepend-inner-icon="tabler-phone"
                  @input="limitPhoneLength"
                  @keypress="e => !/[0-9]/.test(e.key) && e.preventDefault()"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  العنوان
                </VLabel>
                <VTextField
                  v-model="formData.address"
                  prepend-inner-icon="tabler-map-pin"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  اسم المحل
                </VLabel>
                <VTextField
                  v-model="formData.shopName"
                  prepend-inner-icon="tabler-building-store"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  اسم البائع
                </VLabel>
                <VTextField
                  v-model="formData.saleName"
                  prepend-inner-icon="tabler-user-circle"
                />
              </VCol>

              <VCol cols="12">
                <VLabel class="mb-2">
                  الملاحظات
                </VLabel>
                <VTextField
                  v-model="formData.notes"
                  prepend-inner-icon="tabler-file-text"
                />
              </VCol>
            </VRow>
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
            @click="editCustomer"
          >
            <VIcon
              icon="tabler-edit"
              class="me-2"
            />
            تعديل
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
    <VDialog
      v-model="dialogMove"
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
                icon="tabler-arrows-right-left"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                نقل إلى قائمة أخرى
              </h4>
              <span class="text-caption text-medium-emphasis">نقل العميل إلى مندوب آخر</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="secondary"
            size="small"
            @click="dialogMove = false"
          >
            <VIcon
              icon="tabler-x"
              size="24"
            />
          </VBtn>
        </div>

        <VCardText class="pa-4">
          <VForm>
            <VLabel class="mb-2">
              اختيار القائمة
            </VLabel>
            <VAutocomplete
              v-model="formData.moveDelegateID"
              :items="delegateOption.map(delegate => ({ title: delegate.delegateName, value: delegate.delegateID }))"
              prepend-inner-icon="tabler-list"
              clearable
              required
            />
          </VForm>
        </VCardText>

        <VCardActions class="justify-end gap-3 pa-4">
          <VBtn
            variant="tonal"
            color="secondary"
            @click="dialogMove = false"
          >
            <VIcon
              icon="tabler-x"
              class="me-2"
            />
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            @click="moveCustomer"
          >
            <VIcon
              icon="tabler-arrow-right"
              class="me-2"
            />
            نقل
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </VCard>
</template>

<style scoped>
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
