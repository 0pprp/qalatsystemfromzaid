<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  customerProfileApiPath,
  customerSharedNotesApiPath,
  displayCityName,
  managerCustomerDocumentDeletePath,
  managerCustomerDocumentFilePath,
  managerCustomerDocumentUploadPath,
  managerCustomerUpdatePath,
  managerSalePath,
  overrideLabel,
  smDelete,
  smErrorMessage,
  smGet,
  smGetBlob,
  smPostForm,
  smPut,
} from '@/composables/salesManagerApi'
import {
  directorCustomerDocumentFilePath,
  directorCustomerNotesPath,
  directorCustomerProfilePath,
  directorGet,
  directorGetBlob,
  directorSaleShopImagePath,
} from '@/composables/directorApi'
import { loadCachedBlob, cachedBlobUrl } from '@/composables/useBlobUrlCache'
import { isDemo } from '@/composables/useCities'
import { isCentralSalesManager } from '@/composables/useSalesBranches'
import { useUserRole } from '@/composables/useUserRole'
import { shouldHidePaidRemaining } from '@/composables/legalFinancialVisibility'
import { formatIraqDate } from '@/composables/iraqDate'
import { saleDisplayDocuments, saleDocumentTitle } from '@/composables/saleDocuments'
import ShopLocationLink from '@/components/ShopLocationLink.vue'
import { useToast } from '@/composables/useToast'
import { IRAQ_MOBILE_ERROR, iraqPhoneValidator, isIraqMobile, normalizeIraqPhone } from '@core/utils/validators'

const toast = useToast()
const route = useRoute()
const router = useRouter()
const { isDirector, isAdmin, isMonitor } = useUserRole()
/** المدير عرض فقط؛ المراقب يعمل مثل مدير المبيعات */
const readOnly = computed(() => isDirector.value)
/** Accountant/director: hide الواصل/الباقي for legal customers (SM keeps full amounts). */
const hideLegalPaidRemaining = computed(() =>
  (isDirector.value || isAdmin.value)
  && shouldHidePaidRemaining(profile.value),
)
const loading = ref(false)
const profile = ref(null)
const sharedNotes = ref([])
const shopUrls = ref({})
const kycUrls = ref({})
const kycBusyMap = ref({})
const kycErrorMap = ref({})
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
  { value: 'ResidenceCardFront', label: 'بطاقة السكن - أمامية' },
  { value: 'ResidenceCardBack', label: 'بطاقة السكن - خلفية' },
  { value: 'ResidenceCertificate', label: 'تأييد السكن' },
]
const kycLabelFallback = {
  ...Object.fromEntries(kycTypes.map(t => [t.value, t.label])),
  ResidenceCard: 'بطاقة السكن - قديمة',
}

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
  return saleDisplayDocuments(sale)
}

function documentTitle(doc) {
  return saleDocumentTitle(doc)
}

async function load() {
  const city = cityValue.value
  if (!city && !customerId.value && !customerName.value && !customerPhone.value) {
    profile.value = null
    sharedNotes.value = []

    return
  }
  loading.value = true
  try {
    if (isDirector.value) {
      if (!city || !customerId.value) {
        throw new Error('المحافظة ومعرّف الزبون مطلوبان')
      }
      profile.value = await directorGet(directorCustomerProfilePath(city, customerId.value), {
        timeoutMs: 15000,
      })
    }
    else {
      profile.value = await smGet(customerProfileApiPath(city, {
        customerId: customerId.value,
        name: customerName.value,
        phone: customerPhone.value,
      }), { timeoutMs: 15000 })
    }
    // First paint immediately after JSON — images/notes load in background.
    loading.value = false
    void Promise.all([
      loadShopImages(),
      loadCustomerDocuments(),
      loadSharedNotes(),
    ])
  }
  catch (err) {
    profile.value = null
    sharedNotes.value = []
    toast.error(err?.response?.data?.message || err?.message || 'تعذر فتح بروفايل الزبون')
    loading.value = false
  }
}

async function loadSharedNotes() {
  sharedNotes.value = []
  const id = pick(profile.value, 'customerId', 'CustomerId') || customerId.value
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  // Prefer notes already on profile payload when present.
  const embedded = pick(profile.value, 'notes', 'Notes')
  if (Array.isArray(embedded) && embedded.length) {
    sharedNotes.value = embedded

    return
  }
  if (!id)
    return
  try {
    const rows = isDirector.value
      ? await directorGet(directorCustomerNotesPath(city, id))
      : await smGet(customerSharedNotesApiPath(city, id))
    sharedNotes.value = Array.isArray(rows) ? rows : []
  }
  catch {
    sharedNotes.value = []
  }
}

async function loadShopImages() {
  shopUrls.value = {}
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  if (!city && ((isCentralSalesManager() && !isDemo()) || isDirector.value)) {
    return
  }
  const tasks = []
  for (const sale of sales.value) {
    const id = pick(sale, 'saleId', 'SaleId')
    if (!id)
      continue
    if (!city && ((isCentralSalesManager() && !isDemo()) || isDirector.value))
      continue
    tasks.push((async () => {
      try {
        const cacheKey = `shop:${city}:${id}`
        const url = await loadCachedBlob(cacheKey, () => (
          isDirector.value
            ? directorGetBlob(directorSaleShopImagePath(city, id), { timeoutMs: 120000 })
            : smGetBlob(managerSalePath(city, id, '/shop-image'), { timeoutMs: 120000 })
        ))
        if (url)
          shopUrls.value = { ...shopUrls.value, [id]: url }
      }
      catch (e) {
        console.warn('shop-image failed', city, id, e)
      }
    })())
  }
  await Promise.all(tasks)
}

async function loadCustomerDocuments() {
  kycUrls.value = {}
  kycBusyMap.value = {}
  kycErrorMap.value = {}
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  const needsCity = (isCentralSalesManager() && !isDemo()) || isDirector.value
  if (!city && needsCity)
    return

  const docs = customerDocuments.value || []
  // Instant paint from session blob cache before any network.
  const seeded = {}
  for (const doc of docs) {
    const id = Number(pick(doc, 'id', 'Id', 'documentId', 'DocumentId') || 0)
    if (!(id > 0))
      continue
    const hit = cachedBlobUrl(`kyc:${city}:${id}`)
    if (hit)
      seeded[id] = hit
  }
  if (Object.keys(seeded).length)
    kycUrls.value = seeded

  await Promise.all(docs.map(async (doc) => {
    const id = Number(pick(doc, 'id', 'Id', 'documentId', 'DocumentId') || 0)
    if (!(id > 0) || (!city && needsCity))
      return
    const cacheKey = `kyc:${city}:${id}`
    kycBusyMap.value = { ...kycBusyMap.value, [id]: true }
    try {
      const url = await loadCachedBlob(cacheKey, () => (
        isDirector.value
          ? directorGetBlob(directorCustomerDocumentFilePath(city, id), { timeoutMs: 120000 })
          : smGetBlob(managerCustomerDocumentFilePath(city, id), { timeoutMs: 120000 })
      ))
      if (url)
        kycUrls.value = { ...kycUrls.value, [id]: url }
      else
        kycErrorMap.value = { ...kycErrorMap.value, [id]: 'الملف غير موجود على السيرفر' }
    }
    catch (e) {
      console.warn('kyc-doc failed', city, id, e)
      const msg = e?.response?.status === 404
        ? 'الملف غير موجود على السيرفر'
        : (e?.message || 'تعذر عرض الصورة')
      kycErrorMap.value = { ...kycErrorMap.value, [id]: msg }
    }
    finally {
      kycBusyMap.value = { ...kycBusyMap.value, [id]: false }
    }
  }))
}

function goBack() {
  const returnTo = String(route.query.returnTo || '').trim()
  if (returnTo === 'director-follower-work') {
    const city = String(route.query.cityValue || pick(profile.value, 'cityValue', 'CityValue') || '').trim()
    const followerId = String(route.query.followerId || '').trim()
    const tab = String(route.query.tab || 'sales').trim()
    const resumeCustomerId = String(
      route.query.resumeCustomerId || route.query.customerId || customerId.value || '',
    ).trim()
    router.push({
      path: '/director-follower-work',
      query: {
        ...(city ? { city } : {}),
        ...(followerId ? { followerId } : {}),
        ...(tab ? { tab } : {}),
        ...(resumeCustomerId ? { resumeCustomerId } : {}),
      },
    })

    return
  }
  if (returnTo === 'director-delegate-followup' || returnTo === 'director-delegate-followup-customers') {
    const city = String(route.query.cityValue || '').trim()
    const listId = String(route.query.listId || '').trim()
    const path = returnTo === 'director-delegate-followup-customers'
      ? '/director-delegate-followup-customers'
      : '/director-delegate-followup'
    router.push({
      path,
      query: {
        ...(city ? { city } : {}),
        ...(listId ? { listId } : {}),
      },
    })

    return
  }
  if (returnTo === 'director-sales-followup') {
    const city = String(route.query.cityValue || '').trim()
    const employeeId = String(route.query.employeeId || '').trim()
    router.push({
      path: '/director-sales-followup',
      query: {
        ...(city ? { city } : {}),
        ...(employeeId ? { employeeId } : {}),
      },
    })

    return
  }
  if (window.history.length > 1) {
    router.back()

    return
  }
  if (isMonitor.value) {
    router.push('/sales-manager-sales')

    return
  }
  router.push(isDirector.value ? '/director-ratings' : '/sales-manager-requests')
}

function shopFieldEval(source) {
  if (source === 'follower')
    return pick(profile.value, 'followerShopEval', 'FollowerShopEval') || null
  if (source === 'delegate')
    return pick(profile.value, 'delegateShopEval', 'DelegateShopEval') || null

  return null
}

function evalMoney(value) {
  const n = Number(value)
  if (!Number.isFinite(n) || n === 0)
    return '—'

  return money(n)
}

function evalDate(value) {
  if (!value)
    return '—'

  return formatIraqDate(value) || '—'
}

function docLabel(doc) {
  const type = pick(doc, 'documentType', 'DocumentType')

  return pick(doc, 'typeLabel', 'TypeLabel')
    || kycLabelFallback[type]
    || kycTypes.find(t => t.value === type)?.label
    || type
    || 'مستند'
}

function openKyc(doc) {
  const url = kycUrls.value[pick(doc, 'id', 'Id', 'documentId', 'DocumentId')]
  if (!url)
    return
  kycOpenUrl.value = url
  kycOpen.value = true
}

function openShopImage(sale) {
  const id = pick(sale, 'saleId', 'SaleId')
  const url = id != null ? shopUrls.value[id] : null
  if (!url)
    return
  kycOpenUrl.value = url
  kycOpen.value = true
}

function openEdit() {
  if (readOnly.value)
    return
  editForm.value = {
    name: pick(profile.value, 'customerName', 'CustomerName') || '',
    phone: pick(profile.value, 'phone', 'Phone') || '',
    province: displayCityName(profile.value) || '',
    address: pick(profile.value, 'address', 'Address') || '',
  }
  editOpen.value = true
}

async function saveCustomer() {
  if (readOnly.value)
    return
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
  editForm.value.phone = normalizeIraqPhone(editForm.value.phone)
  if (!isIraqMobile(editForm.value.phone)) {
    toast.error(IRAQ_MOBILE_ERROR)
    return
  }
  editBusy.value = true
  try {
    const payload = {
      customerId: pick(profile.value, 'customerId', 'CustomerId') || customerId.value || null,
      originalName: pick(profile.value, 'customerName', 'CustomerName') || customerName.value || '',
      originalPhone: pick(profile.value, 'phone', 'Phone') || customerPhone.value || '',
      customerName: name,
      phone: normalizeIraqPhone(editForm.value.phone),
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
  if (readOnly.value) {
    toast.error('عرض فقط — لا يمكن تعديل المستمسكات من حساب المدير')

    return false
  }
  const city = pick(profile.value, 'cityValue', 'CityValue') || cityValue.value
  if (!city) {
    toast.error('حدد المحافظة')

    return false
  }
  kycBusy.value = true
  try {
    const form = new FormData()
    form.append('file', file)
    const latestSale = sales.value[0]
    await smPostForm(managerCustomerDocumentUploadPath(city, {
      type,
      customerId: pick(profile.value, 'customerId', 'CustomerId') || customerId.value,
      name: pick(profile.value, 'customerName', 'CustomerName') || customerName.value,
      phone: pick(profile.value, 'phone', 'Phone') || customerPhone.value,
      saleId: pick(latestSale, 'saleId', 'SaleId'),
    }), form)
    await load()

    return true
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر رفع المستند'))

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
    const buffer = await blob.arrayBuffer()
    const header = new TextDecoder('latin1').decode(buffer.slice(0, 5))
    if (header !== '%PDF-') {
      toast.error('تعذر فتح المستند')
      return
    }
    const pdf = new Blob([buffer], { type: 'application/pdf' })
    const url = URL.createObjectURL(pdf)
    window.open(url, '_blank')
  }
  catch {
    toast.error('تعذر فتح المستند')
  }
}

watch(() => route.query, load, { deep: true })
onMounted(load)
onUnmounted(() => {
  // Blob URLs are owned by the session cache — do not revoke on leave.
  if (kycOpenUrl.value && !String(kycOpenUrl.value).startsWith('blob:'))
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
        @click="goBack"
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
            v-if="!readOnly"
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
            المحافظة: {{ displayCityName(profile) || '—' }}
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
          ملاحظات الزبون
        </h5>
        <div
          v-if="!sharedNotes.length"
          class="text-medium-emphasis"
        >
          لا توجد ملاحظات.
        </div>
        <div
          v-for="(note, idx) in sharedNotes"
          :key="pick(note, 'noteId', 'NoteId', 'noteID') || idx"
          class="mb-3 pa-3"
          style="border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity)); border-radius: 8px;"
        >
          <div class="mb-2">
            {{ pick(note, 'noteText', 'NoteText') || '—' }}
          </div>
          <div class="text-caption text-medium-emphasis">
            الكاتب: {{ pick(note, 'createdByName', 'CreatedByName', 'userName', 'UserName') || '—' }}
            —
            {{ formatIraqDate(pick(note, 'createdAtUtc', 'CreatedAtUtc', 'createdDate', 'CreatedDate')) }}
          </div>
        </div>
      </VCard>

      <VCard class="mb-6 pa-6">
        <h5 class="mb-4">
          مستندات الزبون
        </h5>
        <div
          v-if="!readOnly"
          class="d-flex flex-wrap align-center ga-3 mb-4"
        >
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
              v-if="kycUrls[pick(doc, 'id', 'Id', 'documentId', 'DocumentId')]"
              :src="kycUrls[pick(doc, 'id', 'Id', 'documentId', 'DocumentId')]"
              height="180"
              cover
              class="rounded cursor-pointer"
              @click="openKyc(doc)"
            />
            <div
              v-else-if="kycBusyMap[pick(doc, 'id', 'Id', 'documentId', 'DocumentId')]"
              class="text-medium-emphasis"
            >
              جاري تحميل الصورة…
            </div>
            <div
              v-else
              class="text-medium-emphasis"
            >
              {{ kycErrorMap[pick(doc, 'id', 'Id', 'documentId', 'DocumentId')] || 'تعذر عرض الصورة.' }}
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
                v-if="!readOnly"
                size="small"
                variant="text"
                :disabled="kycBusy"
                @click="startReplace(doc)"
              >
                استبدال
              </VBtn>
              <VBtn
                v-if="!readOnly"
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
            الواصل:
            {{ hideLegalPaidRemaining
              ? '—'
              : money(pick(paymentSummary, 'receiptsTotal', 'ReceiptsTotal')) }}
          </VCol>
          <VCol md="4">
            الباقي:
            {{ hideLegalPaidRemaining
              ? '—'
              : money(pick(paymentSummary, 'amountRemaining', 'AmountRemaining')) }}
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
              <td>{{ formatIraqDate(pick(row, 'paymentDate', 'PaymentDate')) || '—' }}</td>
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
            تاريخ البيع: {{ formatIraqDate(pick(sale, 'date', 'Date')) || '—' }}
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

        <h6 class="text-subtitle-1 font-weight-bold mb-3">
          بيانات موظف المبيعات
        </h6>
        <div
          v-if="!saleShop(sale)"
          class="text-medium-emphasis mb-6"
        >
          لا توجد بيانات محل من موظف المبيعات.
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
          <ShopLocationLink :shop="saleShop(sale)" />
          <button
            v-if="shopUrls[pick(sale, 'saleId', 'SaleId')]"
            type="button"
            class="shop-image-thumb mt-4"
            title="اضغط لعرض الصورة بحجم أكبر"
            @click="openShopImage(sale)"
          >
            <VImg
              :src="shopUrls[pick(sale, 'saleId', 'SaleId')]"
              max-height="280"
            />
            <span class="shop-image-thumb-hint">اضغط للتكبير</span>
          </button>
          <div
            v-else
            class="text-medium-emphasis mt-3"
          >
            لا توجد صورة محل.
          </div>
        </template>

        <VDivider class="my-4" />
        <h6 class="text-subtitle-1 font-weight-bold mb-3">
          تقييم المتابع
        </h6>
        <div
          v-if="!shopFieldEval('follower')"
          class="text-medium-emphasis mb-2"
        >
          لا يوجد تقييم متابع.
        </div>
        <VRow v-else>
          <VCol md="4">
            التقييم: {{ pick(shopFieldEval('follower'), 'rating', 'Rating') ?? '—' }}
          </VCol>
          <VCol md="4">
            المقيّم: {{ pick(shopFieldEval('follower'), 'evaluatorName', 'EvaluatorName') || '—' }}
          </VCol>
          <VCol md="4">
            وقت المصادقة: {{ evalDate(pick(shopFieldEval('follower'), 'evaluatedAtUtc', 'EvaluatedAtUtc')) }}
          </VCol>
          <VCol md="4">
            قيمة بضاعة المحل: {{ evalMoney(pick(shopFieldEval('follower'), 'shopStockEstimatedValue', 'ShopStockEstimatedValue')) }}
          </VCol>
          <VCol md="4">
            قيمة الوارد اليومي: {{ evalMoney(pick(shopFieldEval('follower'), 'estimatedDailyRevenue', 'EstimatedDailyRevenue')) }}
          </VCol>
          <VCol
            cols="12"
            class="pt-0"
          >
            الملاحظة: {{ pick(shopFieldEval('follower'), 'note', 'Note') || '—' }}
          </VCol>
        </VRow>

        <VDivider class="my-4" />
        <h6 class="text-subtitle-1 font-weight-bold mb-3">
          تقييم المندوب
        </h6>
        <div
          v-if="!shopFieldEval('delegate')"
          class="text-medium-emphasis mb-2"
        >
          لا يوجد تقييم مندوب.
        </div>
        <VRow v-else>
          <VCol md="4">
            التقييم: {{ pick(shopFieldEval('delegate'), 'rating', 'Rating') ?? '—' }}
          </VCol>
          <VCol md="4">
            المقيّم: {{ pick(shopFieldEval('delegate'), 'evaluatorName', 'EvaluatorName') || '—' }}
          </VCol>
          <VCol md="4">
            وقت التقييم: {{ evalDate(pick(shopFieldEval('delegate'), 'evaluatedAtUtc', 'EvaluatedAtUtc')) }}
          </VCol>
          <VCol md="4">
            قيمة بضاعة المحل: {{ evalMoney(pick(shopFieldEval('delegate'), 'shopStockEstimatedValue', 'ShopStockEstimatedValue')) }}
          </VCol>
          <VCol md="4">
            قيمة الوارد اليومي: {{ evalMoney(pick(shopFieldEval('delegate'), 'estimatedDailyRevenue', 'EstimatedDailyRevenue')) }}
          </VCol>
          <VCol
            cols="12"
            class="pt-0"
          >
            الملاحظة: {{ pick(shopFieldEval('delegate'), 'note', 'Note') || '—' }}
          </VCol>
        </VRow>

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
          <VBtn
            color="primary"
            variant="tonal"
            @click="openDocument(sale, doc)"
          >
            {{ documentTitle(doc) }}
          </VBtn>
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
              <th>الواصل</th>
              <th>الباقي</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="row in officialSales"
              :key="pick(row, 'customerSaleId', 'CustomerSaleId') || pick(row, 'dateCreate', 'DateCreate')"
            >
              <td>{{ formatIraqDate(pick(row, 'dateCreate', 'DateCreate')) || '—' }}</td>
              <td>{{ pick(row, 'itemsNames', 'ItemsNames') || '—' }}</td>
              <td>{{ money(pick(row, 'amountTotalSales', 'AmountTotalSales')) }}</td>
              <td>{{ hideLegalPaidRemaining ? '—' : money(pick(row, 'receiptsTotal', 'ReceiptsTotal')) }}</td>
              <td>{{ hideLegalPaidRemaining ? '—' : money(pick(row, 'amountRemaining', 'AmountRemaining')) }}</td>
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
                maxlength="11"
                inputmode="numeric"
                hint="11 رقم ويبدأ بـ 07"
                persistent-hint
                :rules="[iraqPhoneValidator]"
                @update:model-value="v => editForm.phone = normalizeIraqPhone(v)"
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
      max-width="960"
    >
      <VCard v-if="kycOpenUrl">
        <div class="d-flex align-center justify-space-between pa-3">
          <span class="text-subtitle-1 font-weight-bold">صورة المحل</span>
          <VBtn
            icon="tabler-x"
            variant="text"
            aria-label="إغلاق"
            @click="kycOpen = false"
          />
        </div>
        <VImg
          :src="kycOpenUrl"
          max-height="80vh"
          contain
        />
        <VCardActions>
          <VSpacer />
          <VBtn
            color="primary"
            variant="tonal"
            @click="kycOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </VContainer>
</template>

<style scoped>
.shop-image-thumb {
  display: block;
  width: 100%;
  max-width: 420px;
  margin-inline: auto;
  padding: 0;
  border: 1px solid rgba(15, 118, 110, 0.35);
  border-radius: 10px;
  background: #fff;
  cursor: zoom-in;
  overflow: hidden;
  text-align: center;
}

.shop-image-thumb:hover {
  border-color: #0f766e;
  box-shadow: 0 0 0 2px rgba(15, 118, 110, 0.12);
}

.shop-image-thumb-hint {
  display: block;
  padding: 6px 8px;
  background: #f0fdfa;
  color: #0f766e;
  font-size: 0.8rem;
  font-weight: 700;
}
</style>
