<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { formatIraqTime } from '@/composables/gpsTrack'
import {
  customerProfileApiPath,
  managerCustomerDocumentDeletePath,
  managerCustomerDocumentFilePath,
  managerCustomerDocumentUploadPath,
  managerCustomerUpdatePath,
  managerSalePath,
  overrideLabel,
  smDelete,
  smGet,
  smGetBlob,
  smPostForm,
  smPut,
} from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const route = useRoute()
const router = useRouter()
const loading = ref(false)
const profile = ref(null)
const shopUrls = ref({})
const kycUrls = ref({})
const kycOpen = ref(false)
const kycOpenUrl = ref('')
const kycBusy = ref(false)
const kycAddType = ref('NationalIdFront')
const kycAddInput = ref(null)
const kycReplaceInput = ref(null)
const kycReplaceDoc = ref(null)
const editOpen = ref(false)
const editBusy = ref(false)
const editForm = ref({
  name: '',
  phone: '',
  province: '',
  address: '',
})
const kycTypes = [
  { value: 'NationalIdFront', label: 'البطاقة الوطنية - أمامية' },
  { value: 'NationalIdBack', label: 'البطاقة الوطنية - خلفية' },
  { value: 'ResidenceCard', label: 'بطاقة السكن' },
  { value: 'ResidenceCertificate', label: 'تأييد السكن' },
]

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function money(value) {
  const n = Number(value || 0)
  if (!Number.isFinite(n))
    return '0 د.ع'

  return `${n.toLocaleString('en-US')} د.ع`
}

const cityValue = computed(() => String(route.query.cityValue || ''))
const customerId = computed(() => Number(route.query.customerId || 0) || null)
const customerName = computed(() => String(route.query.name || ''))
const customerPhone = computed(() => String(route.query.phone || ''))

const paymentSummary = computed(() => pick(profile.value, 'paymentSummary', 'PaymentSummary') || {})
const payments = computed(() => {
  const rows = pick(profile.value, 'payments', 'Payments') || []

  return Array.isArray(rows) ? rows : []
})
const officialSales = computed(() => {
  const rows = pick(profile.value, 'officialSales', 'OfficialSales') || []

  return Array.isArray(rows) ? rows : []
})
const sales = computed(() => {
  const rows = pick(profile.value, 'sales', 'Sales') || []

  return Array.isArray(rows) ? rows : []
})
const customerDocuments = computed(() => {
  const rows = pick(profile.value, 'customerDocuments', 'CustomerDocuments') || []

  return Array.isArray(rows) ? rows : []
})

function saleItems(sale) {
  const rows = pick(sale, 'items', 'Items') || []

  return Array.isArray(rows) ? rows : []
}

function saleShop(sale) {
  return pick(sale, 'shop', 'Shop') || pick(profile.value, 'latestShop', 'LatestShop') || null
}

function saleDocs(sale) {
  const rows = pick(sale, 'documents', 'Documents') || []
  const list = Array.isArray(rows) ? rows : []

  return list.filter(doc => {
    const type = String(pick(doc, 'type', 'Type') || '')

    return type === 'Contract' || type === 'PromissoryNote' || type === 'PreviewContract' || type === 'PreviewPromissoryNote'
  })
}

function isContract(doc) {
  const type = String(pick(doc, 'type', 'Type') || '')

  return type === 'Contract' || type === 'PreviewContract'
}

async function load() {
  const city = cityValue.value
  if (!city && !customerId.value && !customerName.value && !customerPhone.value) {
    profile.value = null

    return
  }
  loading.value = true
  try {
    profile.value = await smGet(customerProfileApiPath(city, {
      customerId: customerId.value,
      name: customerName.value,
      phone: customerPhone.value,
    }))
    await loadShopImages()
    await loadCustomerDocuments()
  }
  catch (err) {
    profile.value = null
    toast.error(err?.response?.data?.message || 'تعذر فتح بروفايل الزبون')
  }
  finally {
    loading.value = false
  }
}

async function loadShopImages() {
  Object.values(shopUrls.value).forEach(url => URL.revokeObjectURL(url))
  shopUrls.value = {}
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  for (const sale of sales.value) {
    const id = pick(sale, 'saleId', 'SaleId')
    if (!id || !city)
      continue
    try {
      const blob = await smGetBlob(managerSalePath(city, id, '/shop-image'))
      if (blob && blob.size)
        shopUrls.value[id] = URL.createObjectURL(blob)
    }
    catch {
      // no shop image for this sale
    }
  }
}

async function loadCustomerDocuments() {
  Object.values(kycUrls.value).forEach(url => URL.revokeObjectURL(url))
  kycUrls.value = {}
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  for (const doc of customerDocuments.value) {
    const id = pick(doc, 'id', 'Id')
    if (!id || !city)
      continue
    try {
      const blob = await smGetBlob(managerCustomerDocumentFilePath(city, id))
      if (blob && blob.size)
        kycUrls.value[id] = URL.createObjectURL(blob)
    }
    catch {
      // image missing
    }
  }
}

function docLabel(doc) {
  return pick(doc, 'typeLabel', 'TypeLabel')
    || kycTypes.find(t => t.value === pick(doc, 'documentType', 'DocumentType'))?.label
    || pick(doc, 'documentType', 'DocumentType')
    || 'مستند'
}

function openKyc(doc) {
  const url = kycUrls.value[pick(doc, 'id', 'Id')]
  if (!url)
    return
  kycOpenUrl.value = url
  kycOpen.value = true
}

function openEdit() {
  editForm.value = {
    name: pick(profile.value, 'customerName', 'CustomerName') || '',
    phone: pick(profile.value, 'phone', 'Phone') || '',
    province: pick(profile.value, 'province', 'Province', 'cityName', 'CityName') || '',
    address: pick(profile.value, 'address', 'Address') || '',
  }
  editOpen.value = true
}

async function saveCustomer() {
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  if (!city) {
    toast.error('حدد المحافظة أولاً')
    return
  }
  const name = String(editForm.value.name || '').trim()
  if (!name) {
    toast.error('اسم الزبون مطلوب')
    return
  }
  editBusy.value = true
  try {
    const payload = {
      customerId: pick(profile.value, 'customerId', 'CustomerId') || customerId.value || null,
      originalName: pick(profile.value, 'customerName', 'CustomerName') || customerName.value || '',
      originalPhone: pick(profile.value, 'phone', 'Phone') || customerPhone.value || '',
      customerName: name,
      phone: String(editForm.value.phone || '').trim(),
      province: String(editForm.value.province || '').trim(),
      address: String(editForm.value.address || '').trim(),
    }
    profile.value = await smPut(managerCustomerUpdatePath(city), payload)
    await router.replace({
      query: {
        ...route.query,
        customerId: payload.customerId || route.query.customerId,
        name: payload.customerName,
        phone: payload.phone,
      },
    })
    editOpen.value = false
    toast.success('تم حفظ بيانات الزبون')
    await loadShopImages()
    await loadCustomerDocuments()
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر حفظ بيانات الزبون')
  }
  finally {
    editBusy.value = false
  }
}

async function addCustomerDocument(event) {
  const file = event.target.files?.[0]
  event.target.value = ''
  if (!file)
    return
  await uploadCustomerDocument(kycAddType.value, file)
}

function startReplace(doc) {
  kycReplaceDoc.value = doc
  kycReplaceInput.value?.click()
}

async function onReplaceFile(event) {
  const file = event.target.files?.[0]
  event.target.value = ''
  const doc = kycReplaceDoc.value
  kycReplaceDoc.value = null
  if (!file || !doc)
    return
  const type = pick(doc, 'documentType', 'DocumentType') || kycAddType.value
  const uploaded = await uploadCustomerDocument(type, file)
  if (uploaded)
    await deleteCustomerDocument(doc, true)
}

async function uploadCustomerDocument(type, file) {
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  if (!city) {
    toast.error('حدد المحافظة')

    return false
  }
  kycBusy.value = true
  try {
    const form = new FormData()
    form.append('file', file)
    await smPostForm(managerCustomerDocumentUploadPath(city, {
      type,
      customerId: pick(profile.value, 'customerId', 'CustomerId') || customerId.value,
      name: pick(profile.value, 'customerName', 'CustomerName') || customerName.value,
      phone: pick(profile.value, 'phone', 'Phone') || customerPhone.value,
    }), form)
    await load()

    return true
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر رفع المستند')

    return false
  }
  finally {
    kycBusy.value = false
  }
}

async function deleteCustomerDocument(doc, silent = false) {
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  const id = pick(doc, 'id', 'Id')
  if (!city || !id)
    return
  kycBusy.value = true
  try {
    await smDelete(managerCustomerDocumentDeletePath(city, id))
    await load()
  }
  catch (err) {
    if (!silent)
      toast.error(err?.response?.data?.message || 'تعذر حذف المستند')
  }
  finally {
    kycBusy.value = false
  }
}

async function openDocument(sale, doc) {
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  const saleId = pick(sale, 'saleId', 'SaleId')
  const documentId = pick(doc, 'documentId', 'DocumentId')
  if (!city || !saleId || !documentId) {
    toast.error('المستند غير متوفر')

    return
  }
  try {
    const blob = await smGetBlob(managerSalePath(city, saleId, `/documents/${documentId}/download`))
    const url = URL.createObjectURL(blob)
    window.open(url, '_blank')
  }
  catch {
    toast.error('تعذر فتح المستند')
  }
}

watch(() => route.query, load, { deep: true })
onMounted(load)
onUnmounted(() => {
  Object.values(shopUrls.value).forEach(url => URL.revokeObjectURL(url))
  Object.values(kycUrls.value).forEach(url => URL.revokeObjectURL(url))
  if (kycOpenUrl.value)
    URL.revokeObjectURL(kycOpenUrl.value)
})
</script>

<template>
  <VContainer
    fluid
    dir="rtl"
    class="customer-profile-page py-6"
  >
    <div class="d-flex align-center justify-space-between mb-6">
      <h4 class="mb-0">
        بروفايل الزبون
      </h4>
      <VBtn
        variant="text"
        @click="router.back()"
      >
        رجوع
      </VBtn>
    </div>

    <div
      v-if="loading"
      class="text-medium-emphasis"
    >
      جاري التحميل...
    </div>

    <template v-else-if="profile">
      <VCard class="mb-6 pa-6">
        <div class="d-flex align-center justify-space-between mb-4">
          <h5 class="mb-0">
            معلومات الزبون
          </h5>
          <VBtn
            prepend-icon="tabler-edit"
            @click="openEdit"
          >
            تعديل
          </VBtn>
        </div>
        <VRow>
          <VCol md="4">
            الاسم: {{ pick(profile, 'customerName', 'CustomerName') || '—' }}
          </VCol>
          <VCol md="4">
            الهاتف: {{ pick(profile, 'phone', 'Phone') || '—' }}
          </VCol>
          <VCol md="4">
            المحافظة: {{ pick(profile, 'province', 'Province', 'cityName', 'CityName') || '—' }}
          </VCol>
          <VCol md="8">
            العنوان: {{ pick(profile, 'address', 'Address') || '—' }}
          </VCol>
          <VCol md="4">
            القائمة/المندوب: {{ pick(profile, 'delegateName', 'DelegateName', 'customerListName', 'CustomerListName') || '—' }}
          </VCol>
        </VRow>
      </VCard>

      <VCard class="mb-6 pa-6">
        <h5 class="mb-4">
          مستندات الزبون
        </h5>
        <div class="d-flex flex-wrap align-center ga-3 mb-4">
          <VSelect
            v-model="kycAddType"
            :items="kycTypes"
            item-title="label"
            item-value="value"
            density="compact"
            hide-details
            style="max-width: 280px"
          />
          <VBtn
            :disabled="kycBusy"
            prepend-icon="tabler-upload"
            @click="kycAddInput?.click()"
          >
            إضافة صورة
          </VBtn>
          <input
            ref="kycAddInput"
            type="file"
            accept="image/*"
            class="d-none"
            @change="addCustomerDocument"
          >
          <input
            ref="kycReplaceInput"
            type="file"
            accept="image/*"
            class="d-none"
            @change="onReplaceFile"
          >
        </div>
        <div
          v-if="!customerDocuments.length"
          class="text-medium-emphasis"
        >
          لا توجد مستندات مرفوعة.
        </div>
        <VRow>
          <VCol
            v-for="doc in customerDocuments"
            :key="pick(doc, 'id', 'Id')"
            cols="12"
            md="6"
            lg="3"
          >
            <div class="text-subtitle-2 mb-2">
              {{ docLabel(doc) }}
            </div>
            <VImg
              v-if="kycUrls[pick(doc, 'id', 'Id')]"
              :src="kycUrls[pick(doc, 'id', 'Id')]"
              height="180"
              cover
              class="rounded cursor-pointer"
              @click="openKyc(doc)"
            />
            <div
              v-else
              class="text-medium-emphasis"
            >
              تعذر عرض الصورة.
            </div>
            <div class="d-flex flex-wrap ga-2 mt-2">
              <VBtn
                size="small"
                variant="text"
                @click="openKyc(doc)"
              >
                فتح
              </VBtn>
              <VBtn
                size="small"
                variant="text"
                :disabled="kycBusy"
                @click="startReplace(doc)"
              >
                استبدال
              </VBtn>
              <VBtn
                size="small"
                variant="text"
                color="error"
                :disabled="kycBusy"
                @click="deleteCustomerDocument(doc)"
              >
                حذف
              </VBtn>
            </div>
          </VCol>
        </VRow>
      </VCard>

      <VCard class="mb-6 pa-6">
        <h5 class="mb-4">
          الحساب والتسديدات
        </h5>
        <VRow class="mb-4">
          <VCol md="4">
            إجمالي المبلغ: {{ money(pick(paymentSummary, 'amountTotalSales', 'AmountTotalSales')) }}
          </VCol>
          <VCol md="4">
            المستلم: {{ money(pick(paymentSummary, 'receiptsTotal', 'ReceiptsTotal')) }}
          </VCol>
          <VCol md="4">
            المتبقي / الرصيد: {{ money(pick(paymentSummary, 'amountRemaining', 'AmountRemaining')) }}
          </VCol>
        </VRow>
        <div
          v-if="!payments.length"
          class="text-medium-emphasis"
        >
          لا توجد تسديدات.
        </div>
        <VTable v-else>
          <thead>
            <tr>
              <th>التاريخ</th>
              <th>القيمة</th>
              <th>رقم الوصل</th>
              <th>ملاحظة</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="row in payments"
              :key="pick(row, 'customerPaymentId', 'CustomerPaymentId') || pick(row, 'paymentDate', 'PaymentDate')"
            >
              <td>{{ formatIraqTime(pick(row, 'paymentDate', 'PaymentDate')) || '—' }}</td>
              <td>{{ money(pick(row, 'amount', 'Amount')) }}</td>
              <td>{{ pick(row, 'boundNumber', 'BoundNumber') || '—' }}</td>
              <td>{{ pick(row, 'note', 'Note', 'itemsNames', 'ItemsNames') || '—' }}</td>
            </tr>
          </tbody>
        </VTable>
      </VCard>

      <VCard
        v-for="sale in sales"
        :key="pick(sale, 'saleId', 'SaleId')"
        class="mb-6 pa-6"
      >
        <h5 class="mb-4">
          تفاصيل البيع
        </h5>
        <VRow class="mb-4">
          <VCol md="4">
            تاريخ البيع: {{ formatIraqTime(pick(sale, 'date', 'Date')) || '—' }}
          </VCol>
          <VCol md="4">
            موظف المبيعات: {{ pick(sale, 'employeeName', 'EmployeeName') || '—' }}
          </VCol>
          <VCol md="4">
            الحالة: {{ pick(sale, 'status', 'Status') || '—' }}
          </VCol>
        </VRow>
        <div class="font-weight-bold mb-2">
          المنتجات
        </div>
        <div
          v-if="!saleItems(sale).length"
          class="text-medium-emphasis mb-4"
        >
          لا توجد منتجات.
        </div>
        <div
          v-for="item in saleItems(sale)"
          :key="pick(item, 'saleItemId', 'SaleItemId', 'productId', 'ProductId')"
          class="mb-2"
        >
          {{ pick(item, 'productName', 'ProductName') }} × {{ pick(item, 'quantity', 'Quantity') }}
          —
          {{ money(pick(item, 'lineSalePrice', 'LineSalePrice')) }}
        </div>

        <VDivider class="my-4" />
        <div class="font-weight-bold mb-3">
          السعر الإجمالي
        </div>
        <div class="mb-2">
          الافتراضي: {{ money(pick(sale, 'defaultTotalSalePrice', 'DefaultTotalSalePrice', 'baseSalePrice', 'BaseSalePrice')) }}
        </div>
        <div class="mb-2">
          تعديل الموظف: {{ overrideLabel(pick(sale, 'overrideTotalSalePrice', 'OverrideTotalSalePrice')) }}
        </div>
        <div class="mb-4">
          المعتمد: {{ money(pick(sale, 'finalSalePrice', 'FinalSalePrice')) }}
        </div>
        <div class="font-weight-bold mb-3">
          القسط اليومي
        </div>
        <div class="mb-2">
          الافتراضي: {{ money(pick(sale, 'defaultDailyInstallment', 'DefaultDailyInstallment')) }}
        </div>
        <div class="mb-2">
          تعديل الموظف: {{ overrideLabel(pick(sale, 'overrideDailyInstallment', 'OverrideDailyInstallment')) }}
        </div>
        <div class="mb-4">
          المعتمد: {{ money(pick(sale, 'dailyInstallment', 'DailyInstallment')) }}
        </div>
        <div class="font-weight-bold mb-3">
          المقدمة
        </div>
        <div class="mb-2">
          الافتراضي: {{ money(pick(sale, 'defaultDownPayment', 'DefaultDownPayment')) }}
        </div>
        <div class="mb-2">
          تعديل الموظف: {{ overrideLabel(pick(sale, 'overrideDownPayment', 'OverrideDownPayment')) }}
        </div>
        <div class="mb-4">
          المعتمد: {{ money(pick(sale, 'downPayment', 'DownPayment')) }}
        </div>

        <VDivider class="my-4" />
        <h5 class="mb-4">
          بيانات المحل
        </h5>
        <div
          v-if="!saleShop(sale)"
          class="text-medium-emphasis"
        >
          لا توجد بيانات محل.
        </div>
        <template v-else>
          <VRow>
            <VCol md="4">
              اسم المحل: {{ pick(saleShop(sale), 'shopName', 'ShopName') }}
            </VCol>
            <VCol md="4">
              طبيعة العمل: {{ pick(saleShop(sale), 'shopBusinessType', 'ShopBusinessType') }}
            </VCol>
            <VCol md="4">
              قيمة الموجودات: {{ money(pick(saleShop(sale), 'shopStockEstimatedValue', 'ShopStockEstimatedValue')) }}
            </VCol>
            <VCol md="4">
              الإيراد اليومي: {{ money(pick(saleShop(sale), 'estimatedDailyRevenue', 'EstimatedDailyRevenue')) }}
            </VCol>
            <VCol md="4">
              الطول: {{ pick(saleShop(sale), 'shopLength', 'ShopLength') }} م
            </VCol>
            <VCol md="4">
              العرض: {{ pick(saleShop(sale), 'shopWidth', 'ShopWidth') }} م
            </VCol>
            <VCol md="4">
              المساحة: {{ pick(saleShop(sale), 'shopArea', 'ShopArea') }} م²
            </VCol>
          </VRow>
          <VImg
            v-if="shopUrls[pick(sale, 'saleId', 'SaleId')]"
            :src="shopUrls[pick(sale, 'saleId', 'SaleId')]"
            max-height="280"
            class="mt-4"
          />
          <div
            v-else
            class="text-medium-emphasis mt-3"
          >
            لا توجد صورة محل.
          </div>
        </template>

        <VDivider class="my-4" />
        <h5 class="mb-4">
          المستندات
        </h5>
        <div
          v-if="!saleDocs(sale).length"
          class="text-medium-emphasis"
        >
          لا توجد مستندات.
        </div>
        <div
          v-for="doc in saleDocs(sale)"
          :key="pick(doc, 'documentId', 'DocumentId')"
          class="d-flex align-center justify-space-between mb-3"
        >
          <span>{{ isContract(doc) ? 'عقد البيع' : 'وصل الأمانة' }}</span>
          <div>
            <VBtn
              size="small"
              variant="text"
              class="me-2"
              @click="openDocument(sale, doc)"
            >
              فتح
            </VBtn>
            <VBtn
              size="small"
              variant="text"
              @click="openDocument(sale, doc)"
            >
              تنزيل
            </VBtn>
          </div>
        </div>
      </VCard>

      <VCard
        v-if="officialSales.length"
        class="mb-6 pa-6"
      >
        <h5 class="mb-4">
          المبيعات السابقة
        </h5>
        <VTable>
          <thead>
            <tr>
              <th>التاريخ</th>
              <th>المنتجات</th>
              <th>الإجمالي</th>
              <th>المستلم</th>
              <th>المتبقي</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="row in officialSales"
              :key="pick(row, 'customerSaleId', 'CustomerSaleId') || pick(row, 'dateCreate', 'DateCreate')"
            >
              <td>{{ formatIraqTime(pick(row, 'dateCreate', 'DateCreate')) || '—' }}</td>
              <td>{{ pick(row, 'itemsNames', 'ItemsNames') || '—' }}</td>
              <td>{{ money(pick(row, 'amountTotalSales', 'AmountTotalSales')) }}</td>
              <td>{{ money(pick(row, 'receiptsTotal', 'ReceiptsTotal')) }}</td>
              <td>{{ money(pick(row, 'amountRemaining', 'AmountRemaining')) }}</td>
            </tr>
          </tbody>
        </VTable>
      </VCard>
    </template>

    <div
      v-else
      class="text-medium-emphasis"
    >
      لا توجد بيانات لهذا الزبون في فرع البحث الحالي.
    </div>
    <VDialog
      v-model="editOpen"
      max-width="640"
    >
      <VCard>
        <VCardTitle>تعديل بيانات الزبون</VCardTitle>
        <VCardText>
          <VRow>
            <VCol cols="12">
              <VTextField
                v-model="editForm.name"
                label="اسم الزبون"
              />
            </VCol>
            <VCol
              cols="12"
              md="6"
            >
              <VTextField
                v-model="editForm.phone"
                label="رقم الهاتف"
              />
            </VCol>
            <VCol
              cols="12"
              md="6"
            >
              <VTextField
                v-model="editForm.province"
                label="المحافظة"
              />
            </VCol>
            <VCol cols="12">
              <VTextField
                v-model="editForm.address"
                label="العنوان"
              />
            </VCol>
          </VRow>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            :disabled="editBusy"
            @click="editOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="editBusy"
            @click="saveCustomer"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
    <VDialog
      v-model="kycOpen"
      max-width="900"
    >
      <VCard v-if="kycOpenUrl">
        <VImg :src="kycOpenUrl" />
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="kycOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </VContainer>
</template>
