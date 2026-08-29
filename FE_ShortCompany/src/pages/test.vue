<script setup>
import { ref, computed, onMounted } from 'vue'
import axios from 'axios'
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import * as XLSX from 'xlsx'  // استيراد مكتبة XLSX

const apiUrl = localStorage.getItem('LinkCity') // رابط الـ API
const itemsData = ref([])
const storesList = ref([]) // قائمة المخازن

// حالة التحميل عند البحث
const loading = ref(false)

const filters = ref({
  storeID: null,
  itemName: '',
  showType: 'الجميع', // الافتراضي: عرض جميع المواد
})

// دالة تنسيق الأرقام بحيث تُعيد "0" إذا كانت القيمة صفر، وتُنسّق الرقم مع إضافة " دع " إذا كانت القيمة موجودة، وإلا تُعيد "0"
const formattedNumber = num => (num === 0 ? "0" : num ? num.toLocaleString() + " دع " : '0')

// 🟢 تعريف عناوين الجدول
const headers = [
  { title: 'اسم المادة', key: 'itemName' },
  { title: 'المخزن', key: 'storeName' },
  { title: 'سعر البيع', key: 'itemPriceDenar' },
  { title: 'سعر الشراء', key: 'itemCostDenar' },
  { title: 'القسط', key: 'amountDayDenar' },
  { title: 'الكمية', key: 'quantity' },
  { title: 'عدد المبيعات', key: 'numberOfItemsSales' },
  { title: 'عدد المشتريات', key: 'numberOfItemsBuys' },
  { title: 'مبلغ البيع الكلي', key: 'priceTotalItem' },
  { title: 'مبلغ الشراء الكلي', key: 'costTotalItem' },
]

// 🟢 حساب الإجماليات
const totals = computed(() => ({
  NumberOfItems: itemsData.value.length,
  TotalPrice: formattedNumber(itemsData.value.reduce((sum, item) => sum + item.itemPriceDenar, 0)),
  TotalCost: formattedNumber(itemsData.value.reduce((sum, item) => sum + item.itemCostDenar, 0)),
  AmountDay: formattedNumber(itemsData.value.reduce((sum, item) => sum + item.amountDayDenar, 0)),
  PriceTotalItem: formattedNumber(itemsData.value.reduce((sum, item) => sum + item.priceTotalItem, 0)),
  CostTotalItem: formattedNumber(itemsData.value.reduce((sum, item) => sum + item.costTotalItem, 0)),
  TotalSales: itemsData.value.reduce((sum, item) => sum + item.numberOfItemsSales, 0),
  TotalBuys: itemsData.value.reduce((sum, item) => sum + item.numberOfItemsBuys, 0),
  TotalQuantity: itemsData.value.reduce((sum, item) => sum + item.quantity, 0),
}))

// 🟢 عناوين الإجماليات
const titles = {
  NumberOfItems: 'عدد المواد',
  TotalPrice: 'إجمالي السعر',
  TotalCost: 'عدد المشتريات',
  AmountDay: 'القسط',
  PriceTotalItem: 'مبلغ البيع الكلي',
  CostTotalItem: 'مبلغ الشراء الكلي',
  TotalSales: 'عدد المبيعات',
  TotalBuys: 'إجمالي المشتريات',
  TotalQuantity: 'إجمالي الكمية',
}

// 🟢 دالة جلب البيانات من API مع الفلترة في الـ Backend مع تحديث حالة التحميل
async function fetchItems() {
  try {
    loading.value = true

    const storeID = filters.value.storeID ?? 'null'
    const itemName = filters.value.itemName || 'null'
    const showType = filters.value.showType || 'null'
    const response = await axios.get(`${apiUrl}Items/Items_GetAll/${storeID}&&${itemName}&&${showType}`)

    itemsData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// 🟢 جلب قائمة المخازن من API
async function fetchStores() {
  try {
    const response = await axios.get(`${apiUrl}Stores/Stores_GetAll`)

    storesList.value = response.data
    if (storesList.value.length > 0) {
      filters.value.storeID = storesList.value[0].storeID
    }
  } catch (error) {
    console.error(error)
  }
}

// جلب بيانات المخازن عند تحميل الصفحة
onMounted(() => {
  fetchStores()
})

// دالة تصدير البيانات إلى Excel
function exportToExcel() {
  const dataToExport = itemsData.value.map(item => ({
    'اسم المادة': item.itemName,
    'المخزن': item.storeName,
    'سعر البيع': item.itemPriceDenar,
    'سعر الشراء': item.itemCostDenar,
    'القسط': item.amountDayDenar,
    'الكمية': item.quantity,
    'عدد المبيعات': item.numberOfItemsSales,
    'عدد المشتريات': item.numberOfItemsBuys,
    'مبلغ البيع الكلي': item.priceTotalItem,
    'مبلغ الشراء الكلي': item.costTotalItem,
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Items")
  XLSX.writeFile(workbook, "items.xlsx")
}
</script>

<template>
  <VCard class="pa-10">
    <VForm>
      <!-- 🔹 فلاتر البحث -->
      <VRow>
        <VCol
          md="4"
          cols="12"
        >
          <VLabel class="mb-2">
            اسم المادة
          </VLabel>
          <AppTextField
            v-model="filters.itemName"
            placeholder="ابحث عن المادة"
            clearable
          />
        </VCol>

        <VCol
          md="4"
          cols="12"
        >
          <VLabel class="mb-2">
            المخزن
          </VLabel>
          <VAutocomplete
            v-model="filters.storeID"
            :items="storesList.map(store => ({ title: store.storeName, value: store.storeID }))"
            placeholder="اختر المخزن"
            clearable
          />
        </VCol>

        <VCol
          md="4"
          cols="12"
        >
          <VLabel class="mb-2">
            نوع العرض
          </VLabel>
          <VAutocomplete
            v-model="filters.showType"
            :items="['الجميع', 'المواد الحالية', 'المواد النافذة', 'الاكثر مبيعا', 'الاقل مبيعا', 'غير مباع', 'على وشك النفاذ']"
          />
        </VCol>
      </VRow>

      <VRow class="mb-4">
        <VCol
          cols="12"
          class="text-right"
        >
          <VBtn
            color="primary"
            :loading="loading"
            :disabled="loading"
            @click="fetchItems"
          >
            بحث
          </VBtn>
          <VBtn
            color="success"
            style="margin-right: 10px"
            @click="exportToExcel"
          >
            تصدير إلى Excel
          </VBtn>
        </VCol>
      </VRow>

      <!-- 🔹 عرض البيانات -->
      <VRow>
        <VDataTable
          :headers="headers"
          :items="itemsData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
        >
          <template #item.itemPriceDenar="{ item }">
            {{ formattedNumber(item.itemPriceDenar) }}
          </template>
          <template #item.itemCostDenar="{ item }">
            {{ formattedNumber(item.itemCostDenar) }}
          </template>
          <template #item.amountDayDenar="{ item }">
            {{ formattedNumber(item.amountDayDenar) }}
          </template>
          <template #item.priceTotalItem="{ item }">
            {{ formattedNumber(item.priceTotalItem) }}
          </template>
          <template #item.costTotalItem="{ item }">
            {{ formattedNumber(item.costTotalItem) }}
          </template>
        </VDataTable>
      </VRow>

      <!-- 🔹 عرض الإجماليات -->
      <VRow class="mt-4">
        <VCol
          v-for="(value, key) in totals"
          :key="key"
          md="2"
          cols="12"
        >
          <VCard class="text-center elevation-1">
            <VLabel
              class="mb-2"
              style="font-size: 14px; margin-top: 20px"
            >
              {{ titles[key] }}
            </VLabel>
            <VCardText class="text-h6 font-weight-bold">
              {{ value }}
            </VCardText>
          </VCard>
        </VCol>
      </VRow>
    </VForm>
  </VCard>
</template>
