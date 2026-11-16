<script setup>
import { onMounted, ref } from 'vue'
import {
  paCreateProduct,
  paDeleteProduct,
  paErrorMessage,
  paFieldErrors,
  paListProducts,
  paMoney,
  paUpdateProduct,
} from '@/composables/promotersAppApi'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()
const rows = ref([])
const loading = ref(false)
const listError = ref('')

const headers = [
  { title: 'المنتج', key: 'name' },
  { title: 'السعر', key: 'price' },
  { title: 'عمولة المروّج', key: 'commission' },
  { title: 'يظهر في التطبيق', key: 'isActive', sortable: false },
  { title: 'الترتيب', key: 'sortOrder' },
  { title: '', key: 'actions', sortable: false, align: 'end' },
]

async function load() {
  loading.value = true
  listError.value = ''
  try {
    rows.value = await paListProducts()
  }
  catch (err) {
    listError.value = paErrorMessage(err, 'تعذر تحميل المنتجات')
  }
  finally {
    loading.value = false
  }
}

const formOpen = ref(false)
const formBusy = ref(false)
const formError = ref('')
const fieldErrors = ref({})
const editingId = ref(null)
const form = ref({})

function fe(k) {
  return fieldErrors.value?.[k] ? [fieldErrors.value[k]] : []
}

function openForm(row) {
  editingId.value = row?.id ?? null
  form.value = row
    ? { name: row.name, price: row.price, commission: row.commission, isActive: row.isActive, sortOrder: row.sortOrder }
    : { name: '', price: null, commission: null, isActive: true, sortOrder: rows.value.length + 1 }
  formError.value = ''
  fieldErrors.value = {}
  formOpen.value = true
}

async function save() {
  formBusy.value = true
  formError.value = ''
  fieldErrors.value = {}

  const body = { ...form.value, price: Number(form.value.price), commission: Number(form.value.commission), sortOrder: Number(form.value.sortOrder) || 0 }
  try {
    if (editingId.value)
      await paUpdateProduct(editingId.value, body)
    else
      await paCreateProduct(body)
    toast.success('تم حفظ المنتج')
    formOpen.value = false
    load()
  }
  catch (err) {
    fieldErrors.value = paFieldErrors(err)
    formError.value = paErrorMessage(err, 'تعذر الحفظ')
  }
  finally {
    formBusy.value = false
  }
}

async function toggleActive(row) {
  try {
    await paUpdateProduct(row.id, { ...row, isActive: !row.isActive })
    row.isActive = !row.isActive
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
}

const deleteRow = ref(null)

async function confirmDelete() {
  try {
    await paDeleteProduct(deleteRow.value.id)
    toast.success('تم حذف المنتج')
    deleteRow.value = null
    load()
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="text-h4 mb-1 d-flex align-center gap-2">
          <VIcon
            icon="tabler-package"
            color="primary"
          />
          المنتجات والعمولات
        </h4>
        <p class="text-medium-emphasis mb-0">
          ما يمكن للمروّجين بيعه: الاسم والسعر وعمولة المروّج للقطعة الواحدة. أي تعديل يطبق على الطلبات الجديدة فقط.
        </p>
      </div>
      <VBtn
        color="primary"
        prepend-icon="tabler-plus"
        @click="openForm"
      >
        إضافة منتج
      </VBtn>
    </div>

    <VAlert
      v-if="listError"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ listError }}
    </VAlert>

    <VCard>
      <VDataTable
        :headers="headers"
        :items="rows"
        :loading="loading"
        :items-per-page="-1"
        hide-default-footer
        no-data-text="لا توجد منتجات بعد"
      >
        <template #item.price="{ item }">
          {{ paMoney(item.price) }}
        </template>
        <template #item.commission="{ item }">
          <strong class="text-success">{{ paMoney(item.commission) }}</strong>
        </template>
        <template #item.isActive="{ item }">
          <VSwitch
            :model-value="item.isActive"
            color="success"
            hide-details
            density="compact"
            @update:model-value="toggleActive(item)"
          />
        </template>
        <template #item.actions="{ item }">
          <IconBtn @click="openForm(item)">
            <VIcon icon="tabler-edit" />
          </IconBtn>
          <IconBtn
            color="error"
            @click="deleteRow = item"
          >
            <VIcon icon="tabler-trash" />
          </IconBtn>
        </template>
      </VDataTable>
    </VCard>

    <VDialog
      v-model="formOpen"
      max-width="440"
    >
      <VCard :title="editingId ? 'تعديل المنتج' : 'إضافة منتج'">
        <VCardText>
          <VAlert
            v-if="formError"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ formError }}
          </VAlert>
          <VTextField
            v-model="form.name"
            label="اسم المنتج"
            :error-messages="fe('name')"
            class="mb-3"
            autofocus
          />
          <VTextField
            v-model="form.price"
            type="number"
            label="السعر (د.ع)"
            :error-messages="fe('price')"
            class="mb-3"
            dir="ltr"
          />
          <VTextField
            v-model="form.commission"
            type="number"
            label="عمولة المروّج للقطعة (د.ع)"
            :error-messages="fe('commission')"
            class="mb-3"
            dir="ltr"
          />
          <VTextField
            v-model="form.sortOrder"
            type="number"
            label="الترتيب"
            class="mb-3"
            dir="ltr"
          />
          <VSwitch
            v-model="form.isActive"
            color="success"
            label="يظهر في التطبيق"
            hide-details
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="formOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="formBusy"
            @click="save"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      :model-value="!!deleteRow"
      max-width="400"
      @update:model-value="v => { if (!v) deleteRow = null }"
    >
      <VCard title="حذف المنتج">
        <VCardText>
          سيختفي «{{ deleteRow?.name }}» من التطبيق. الطلبات السابقة لا تتأثر.
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="deleteRow = null"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="error"
            @click="confirmDelete"
          >
            حذف
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
