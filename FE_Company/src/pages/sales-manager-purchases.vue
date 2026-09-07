<script setup>
import { computed, ref, watch } from 'vue'
import AppTextField from '@core/components/app-form-elements/AppTextField.vue'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { smErrorMessage, smGet, smPost, withCityQuery } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const cityValue = ref('')
const loading = ref(false)
const saving = ref(false)
const addDialog = ref(false)
const rows = ref([])
const suppliers = ref([])
const stores = ref([])
const boxes = ref([])
const itemsOptions = ref([])
const filters = ref({
  fromDate: '',
  toDate: '',
  textSearch: '',
})

const formData = ref(emptyForm())

function emptyForm() {
  return {
    supplierID: null,
    storeID: null,
    boxID: null,
    date: new Date().toLocaleDateString('en-CA'),
    notes: '',
    contents: [],
    totalAmountSpent: 0,
    finalTotalItemCostDenar: 0,
    amountTotalDenar: 0,
  }
}

const formattedNumber = num => (num ? Number(num).toLocaleString('en-US') + ' د.ع' : '0 د.ع')

function formatIq(num) {
  const n = Number(num)
  if (!Number.isFinite(n))
    return '0 د.ع'
  return `${Math.round(n).toLocaleString('en-US')} د.ع`
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

const remainingAmount = computed(() =>
  Number(formData.value.finalTotalItemCostDenar || 0) - Number(formData.value.totalAmountSpent || 0))

const selectedBox = computed(() =>
  boxes.value.find(b => pick(b, 'boxID', 'BoxID') === formData.value.boxID) || null)

const selectedBoxBalance = computed(() => {
  if (!selectedBox.value)
    return null
  const raw = pick(selectedBox.value, 'amountDenar', 'AmountDenar')
  const n = Number(raw)
  return Number.isFinite(n) ? n : 0
})

const isFormValid = computed(() => {
  const contentsValid = formData.value.contents.length > 0
    && formData.value.contents.every(content => content.itemID && content.quantity > 0)
  return formData.value.supplierID
    && formData.value.storeID
    && formData.value.boxID
    && formData.value.date
    && contentsValid
})

function requireCity() {
  if (cityValue.value)
    return true
  toast.error('حدد المحافظة أولاً')
  return false
}

async function loadLookups() {
  if (!cityValue.value)
    return
  try {
    const data = await smGet(withCityQuery('purchases/lookups', cityValue.value)) || {}
    suppliers.value = data.suppliers || data.Suppliers || []
    stores.value = data.stores || data.Stores || []
    boxes.value = (data.boxes || data.Boxes || []).slice().sort((a, b) =>
      (pick(a, 'boxID', 'BoxID') || 0) - (pick(b, 'boxID', 'BoxID') || 0))
  }
  catch (err) {
    suppliers.value = []
    stores.value = []
    boxes.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل الموردين والمخازن'))
  }
}

async function loadItems(storeId) {
  itemsOptions.value = []
  if (!cityValue.value || !storeId)
    return
  try {
    itemsOptions.value = await smGet(withCityQuery(`purchases/items?storeId=${storeId}`, cityValue.value)) || []
  }
  catch (err) {
    itemsOptions.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل مواد المخزن'))
  }
}

async function loadRows() {
  if (!requireCity()) {
    rows.value = []
    return
  }
  loading.value = true
  try {
    const q = []
    if (filters.value.fromDate)
      q.push(`fromDate=${filters.value.fromDate}`)
    if (filters.value.toDate)
      q.push(`toDate=${filters.value.toDate}`)
    if (filters.value.textSearch)
      q.push(`textSearch=${encodeURIComponent(filters.value.textSearch)}`)
    const path = q.length ? `purchases?${q.join('&')}` : 'purchases'
    rows.value = await smGet(withCityQuery(path, cityValue.value)) || []
  }
  catch (err) {
    rows.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل فواتير الشراء'))
  }
  finally {
    loading.value = false
  }
}

async function onCityChange() {
  formData.value = emptyForm()
  itemsOptions.value = []
  await loadLookups()
  await loadRows()
}

async function openAdd() {
  if (!requireCity())
    return
  formData.value = emptyForm()
  itemsOptions.value = []
  addDialog.value = true
  await loadLookups()
}

function addContent() {
  if (!formData.value.storeID) {
    toast.error('حدد المخزن أولاً')
    return
  }
  loadItems(formData.value.storeID)
  formData.value.contents.push({
    itemID: null,
    quantity: 1,
    itemCostDenar: 0,
    itemPriceDenar: null,
    quantityOnHand: null,
    totalItemCostDenar: 0,
  })
}

function itemLabel(item) {
  return pick(item, 'itemName', 'ItemName', 'displayName', 'DisplayName') || ''
}

function itemValue(item) {
  return pick(item, 'itemId', 'ItemId', 'itemID', 'ItemID')
}

function updateItemCost(index) {
  const content = formData.value.contents[index]
  const selected = itemsOptions.value.find(item => itemValue(item) === content.itemID)
  const duplicate = formData.value.contents.some((item, idx) => item.itemID === content.itemID && idx !== index)
  if (duplicate) {
    toast.error('تم اختيار هذا العنصر مسبقاً')
    content.itemID = null
    content.itemPriceDenar = null
    content.quantityOnHand = null
    return
  }
  if (selected) {
    content.itemCostDenar = Number(pick(selected, 'itemCostDenar', 'ItemCostDenar') || 0)
    content.itemPriceDenar = Number(pick(selected, 'itemPriceDenar', 'ItemPriceDenar') ?? 0)
    const qty = pick(selected, 'quantity', 'Quantity')
    content.quantityOnHand = qty == null || qty === '' ? null : Number(qty)
    calculateTotalPrice(index)
  }
}

function calculateTotalPrice(index) {
  const content = formData.value.contents[index]
  content.totalItemCostDenar = Number(content.quantity || 0) * Number(content.itemCostDenar || 0)
  calculateTotals()
}

function calculateTotals() {
  const itemsTotal = formData.value.contents.reduce((sum, content) => sum + Number(content.totalItemCostDenar || 0), 0)
  const supplierAccount = Number(pick(
    suppliers.value.find(s => pick(s, 'supplierID', 'SupplierID') === formData.value.supplierID) || {},
    'amountAccount',
    'AmountAccount',
  ) || 0)
  formData.value.amountTotalDenar = itemsTotal
  formData.value.finalTotalItemCostDenar = itemsTotal + supplierAccount
  formData.value.totalAmountSpent = itemsTotal + supplierAccount
}

watch(() => formData.value.contents, () => {
  formData.value.contents.forEach((_, index) => calculateTotalPrice(index))
}, { deep: true })

watch(() => formData.value.supplierID, calculateTotals)

const formattedTotalAmountSpent = computed({
  get() {
    return formData.value.totalAmountSpent !== ''
      ? Number(formData.value.totalAmountSpent).toLocaleString('en-US') + ' د.ع'
      : ''
  },
  set(value) {
    const numeric = parseFloat(String(value).replace(/[^\d.-]/g, ''))
    formData.value.totalAmountSpent = Number.isNaN(numeric) ? 0 : numeric
  },
})

async function submitForm() {
  if (!isFormValid.value) {
    toast.error('الرجاء ملء جميع الحقول المطلوبة')
    return
  }
  const boxAmount = Number(pick(
    boxes.value.find(b => pick(b, 'boxID', 'BoxID') === formData.value.boxID) || {},
    'amountDenar',
    'AmountDenar',
  ) || 0)
  if (boxAmount < Number(formData.value.totalAmountSpent || 0)) {
    toast.error('المبلغ المراد صرفه أكبر من الموجود في الخزينة.')
    return
  }

  saving.value = true
  try {
    await smPost('purchases', {
      cityValue: cityValue.value,
      supplierId: formData.value.supplierID,
      storeId: formData.value.storeID,
      boxId: formData.value.boxID,
      date: formData.value.date,
      supplierInvoiceNumber: null,
      notes: formData.value.notes || '',
      totalAmountSpent: formData.value.totalAmountSpent,
      amountTotalDenar: formData.value.amountTotalDenar,
      finalTotalItemCostDenar: formData.value.finalTotalItemCostDenar,
      remainingAmountDenar: remainingAmount.value,
      contents: formData.value.contents.map(content => ({
        itemId: content.itemID,
        quantity: Number(content.quantity),
        itemCostDenar: content.itemCostDenar,
        totalItemCostDenar: content.totalItemCostDenar,
      })),
    })
    addDialog.value = false
    toast.success('تم إدخال فاتورة الشراء وتحديث مخزون الفرع')
    await loadRows()
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'حدث خطأ أثناء إضافة فاتورة الشراء.'))
  }
  finally {
    saving.value = false
  }
}
</script>

<template>
  <div>
    <h4 class="mb-4">
      إدخال فاتورة شراء للمخزن
    </h4>
    <p class="text-medium-emphasis mb-4">
      تستخدم نفس مسار المحاسب الرئيسي لإضافة الكمية إلى مخزون هذا الفرع فوراً.
    </p>

    <VRow class="mb-3">
      <VCol
        md="4"
        cols="12"
      >
        <SalesBranchFilter
          v-model="cityValue"
          @change="onCityChange"
        />
      </VCol>
      <VCol
        md="2"
        cols="12"
      >
        <VTextField
          v-model="filters.fromDate"
          type="date"
          label="من تاريخ"
          hide-details
        />
      </VCol>
      <VCol
        md="2"
        cols="12"
      >
        <VTextField
          v-model="filters.toDate"
          type="date"
          label="إلى تاريخ"
          hide-details
        />
      </VCol>
      <VCol
        md="2"
        cols="12"
      >
        <VTextField
          v-model="filters.textSearch"
          label="بحث"
          hide-details
        />
      </VCol>
      <VCol
        md="2"
        cols="12"
        class="d-flex gap-2"
      >
        <VBtn
          :loading="loading"
          @click="loadRows"
        >
          بحث
        </VBtn>
        <VBtn
          color="success"
          prepend-icon="tabler-plus"
          @click="openAdd"
        >
          إضافة شراء
        </VBtn>
      </VCol>
    </VRow>

    <VTable>
      <thead>
        <tr>
          <th>رقم السند</th>
          <th>المورد</th>
          <th>المواد</th>
          <th>التاريخ</th>
          <th>الكمية</th>
          <th>أُدخلت بواسطة</th>
          <th>الفرع</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in rows"
          :key="pick(row, 'buyId', 'BuyId')"
        >
          <td>{{ pick(row, 'boundNumber', 'BoundNumber') || pick(row, 'buyId', 'BuyId') }}</td>
          <td>{{ pick(row, 'supplierName', 'SupplierName') }}</td>
          <td>{{ pick(row, 'itemsNames', 'ItemsNames') }}</td>
          <td>{{ pick(row, 'dateCreate', 'DateCreate') ? new Date(pick(row, 'dateCreate', 'DateCreate')).toLocaleDateString('en-CA') : '—' }}</td>
          <td>{{ pick(row, 'numberOfItemsBuys', 'NumberOfItemsBuys') || 0 }}</td>
          <td>{{ pick(row, 'createdByDisplayName', 'CreatedByDisplayName') || pick(row, 'createdByUserName', 'CreatedByUserName') || '—' }}</td>
          <td>{{ pick(row, 'createdByBranchName', 'CreatedByBranchName') || cityValue }}</td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="7"
            class="text-center text-medium-emphasis"
          >
            لا توجد فواتير شراء معروضة لهذه المحافظة.
          </td>
        </tr>
      </tbody>
    </VTable>

    <VDialog
      v-model="addDialog"
      max-width="1080"
      scrollable
    >
      <VCard class="purchase-invoice-card">
        <VCardTitle class="d-flex align-center justify-space-between px-6 pt-5">
          <span>إضافة تفاصيل فاتورة الشراء</span>
          <VBtn
            icon
            variant="text"
            @click="addDialog = false"
          >
            <VIcon icon="tabler-x" />
          </VBtn>
        </VCardTitle>
        <VCardText class="px-6 pb-2">
          <VRow>
            <VCol
              cols="12"
              sm="6"
              md="3"
            >
              <VLabel class="mb-2">
                المورد
              </VLabel>
              <VAutocomplete
                v-model="formData.supplierID"
                :items="suppliers.map(s => ({ title: pick(s, 'supplierName', 'SupplierName'), value: pick(s, 'supplierID', 'SupplierID') }))"
                clearable
                hide-details
              />
            </VCol>
            <VCol
              cols="12"
              sm="6"
              md="3"
            >
              <VLabel class="mb-2">
                المخزن
              </VLabel>
              <VAutocomplete
                v-model="formData.storeID"
                :items="stores.map(s => ({ title: pick(s, 'storeName', 'StoreName'), value: pick(s, 'storeID', 'StoreID') }))"
                clearable
                hide-details
                @update:model-value="loadItems"
              />
            </VCol>
            <VCol
              cols="12"
              sm="6"
              md="3"
            >
              <VLabel class="mb-2">
                الخزينة النقدية
              </VLabel>
              <VAutocomplete
                v-model="formData.boxID"
                :items="boxes.map(b => ({ title: pick(b, 'boxName', 'BoxName'), value: pick(b, 'boxID', 'BoxID') }))"
                clearable
                hide-details
              />
              <div
                v-if="formData.boxID && selectedBoxBalance != null"
                class="box-balance mt-2"
              >
                الرصيد الحالي: {{ formatIq(selectedBoxBalance) }}
              </div>
            </VCol>
            <VCol
              cols="12"
              sm="6"
              md="3"
            >
              <VLabel class="mb-2">
                التاريخ
              </VLabel>
              <AppTextField
                v-model="formData.date"
                type="date"
                hide-details
              />
            </VCol>
          </VRow>

          <VRow>
            <VCol cols="12">
              <VLabel class="mb-2">
                ملاحظات
              </VLabel>
              <AppTextField
                v-model="formData.notes"
                hide-details
              />
            </VCol>
          </VRow>

          <div class="items-section mt-6 pt-4">
            <div class="d-flex align-center justify-space-between flex-wrap gap-3 mb-1">
              <div class="text-subtitle-1 font-weight-medium">
                عناصر الفاتورة
              </div>
              <VBtn
                color="primary"
                class="add-item-btn"
                prepend-icon="tabler-plus"
                :disabled="!formData.storeID"
                @click="addContent"
              >
                إضافة عنصر
              </VBtn>
            </div>

            <VSheet
              v-for="(content, idx) in formData.contents"
              :key="idx"
              class="item-row pa-4 mt-4"
              border
              rounded
            >
              <VRow dense>
                <VCol
                  cols="12"
                  md="4"
                >
                  <VLabel class="mb-2">
                    المادة
                  </VLabel>
                  <VAutocomplete
                    v-model="content.itemID"
                    :items="itemsOptions.map(i => ({ title: itemLabel(i), value: itemValue(i) }))"
                    clearable
                    hide-details
                    @update:model-value="() => updateItemCost(idx)"
                  />
                  <div
                    v-if="content.itemID"
                    class="item-meta mt-3"
                  >
                    <div>سعر الشراء: {{ formatIq(content.itemCostDenar) }}</div>
                    <div>سعر البيع: {{ formatIq(content.itemPriceDenar) }}</div>
                    <div>المخزون الحالي: {{ content.quantityOnHand == null ? '—' : content.quantityOnHand }}</div>
                  </div>
                </VCol>
                <VCol
                  cols="6"
                  sm="4"
                  md="2"
                >
                  <VLabel class="mb-2">
                    الكمية
                  </VLabel>
                  <AppTextField
                    v-model="content.quantity"
                    type="number"
                    min="1"
                    hide-details
                    @input="() => calculateTotalPrice(idx)"
                  />
                </VCol>
                <VCol
                  cols="6"
                  sm="4"
                  md="2"
                >
                  <VLabel class="mb-2">
                    سعر الشراء
                  </VLabel>
                  <AppTextField
                    :model-value="formattedNumber(content.itemCostDenar)"
                    readonly
                    hide-details
                  />
                </VCol>
                <VCol
                  cols="6"
                  sm="4"
                  md="2"
                >
                  <VLabel class="mb-2">
                    سعر البيع الحالي
                  </VLabel>
                  <AppTextField
                    :model-value="content.itemID ? formattedNumber(content.itemPriceDenar) : ''"
                    readonly
                    hide-details
                  />
                </VCol>
                <VCol
                  cols="6"
                  sm="4"
                  md="2"
                >
                  <VLabel class="mb-2">
                    المخزون
                  </VLabel>
                  <AppTextField
                    :model-value="content.itemID && content.quantityOnHand != null ? String(content.quantityOnHand) : ''"
                    readonly
                    hide-details
                  />
                </VCol>
                <VCol
                  cols="6"
                  sm="4"
                  md="2"
                >
                  <VLabel class="mb-2">
                    الإجمالي
                  </VLabel>
                  <AppTextField
                    :model-value="formattedNumber(content.totalItemCostDenar)"
                    readonly
                    hide-details
                  />
                </VCol>
              </VRow>
              <div class="d-flex justify-start mt-3">
                <VBtn
                  color="error"
                  variant="tonal"
                  prepend-icon="tabler-trash"
                  @click="formData.contents.splice(idx, 1)"
                >
                  حذف
                </VBtn>
              </div>
            </VSheet>
          </div>

          <VRow class="mt-6">
            <VCol
              cols="12"
              sm="6"
              md="3"
            >
              <VLabel class="mb-2">
                إجمالي سعر الشراء الكلي
              </VLabel>
              <AppTextField
                :model-value="formattedNumber(formData.amountTotalDenar)"
                readonly
                hide-details
              />
            </VCol>
            <VCol
              cols="12"
              sm="6"
              md="3"
            >
              <VLabel class="mb-2">
                إجمالي سعر الشراء النهائي
              </VLabel>
              <AppTextField
                :model-value="formattedNumber(formData.finalTotalItemCostDenar)"
                readonly
                hide-details
              />
            </VCol>
            <VCol
              cols="12"
              sm="6"
              md="3"
            >
              <VLabel class="mb-2">
                المبلغ المصروف
              </VLabel>
              <AppTextField
                v-model="formattedTotalAmountSpent"
                hide-details
              />
            </VCol>
            <VCol
              cols="12"
              sm="6"
              md="3"
            >
              <VLabel class="mb-2">
                المبلغ المتبقي
              </VLabel>
              <AppTextField
                :model-value="formattedNumber(remainingAmount)"
                readonly
                hide-details
              />
            </VCol>
          </VRow>
        </VCardText>
        <VCardActions class="px-6 pb-5">
          <VSpacer />
          <VBtn @click="addDialog = false">
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="saving"
            :disabled="!isFormValid"
            @click="submitForm"
          >
            حفظ الفاتورة
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.purchase-invoice-card {
  overflow-x: hidden;
}
.box-balance {
  color: rgba(var(--v-theme-on-surface), 0.62);
  font-size: 0.875rem;
  line-height: 1.4;
}
.items-section {
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.add-item-btn {
  margin-block-start: 0.25rem;
}
.item-row {
  background: rgba(var(--v-theme-on-surface), 0.02);
}
.item-meta {
  color: rgba(var(--v-theme-on-surface), 0.68);
  font-size: 0.8125rem;
  line-height: 1.55;
}
</style>
