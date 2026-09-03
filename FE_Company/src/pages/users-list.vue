<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import axios from 'axios'
import { computed, onMounted, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"
import * as XLSX from 'xlsx'

const apiUrl = localStorage.getItem('LinkCity')
const apiUrlImage = apiUrl.replace('api/', 'Images/')
const currentUserID = ref(0)
const usersData = ref([])
const loading = ref(false)
const userCount = ref(0)

const filters = ref({
  textSearch: '',
})

const cards = computed(() => [
  {
    icon: 'tabler-users',
    value: userCount.value,
    title: 'عدد المستخدمين',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
])

// Dialogs
const addDialog = ref(false)
const editDialog = ref(false)
const confirmDeleteDialog = ref(false)
const userIdToDelete = ref(null)
const imageDialog = ref(false)
const selectedImage = ref('')


// نموذج البيانات مع إضافة الخاصية الخاصة بالصورة
const formData = ref({
  userID: '',
  userName: '',
  email: '',
  phoneNumber: '',
  address: '',
  password: '',
  userType: '',
  userImage: null, // خاصية رفع الصورة
})

const selectedFile = ref(null)


// خاصية المعاينة للصورة
const imagePreview = computed(() => {
  if (selectedFile.value) {
    // Handle Vuetify 3 VFileInput which often returns an array
    const file = Array.isArray(selectedFile.value) ? selectedFile.value[0] : selectedFile.value
    if (file && file instanceof File) {
      return URL.createObjectURL(file)
    }
  }
  
  return ''
})

async function fetchUsers() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()
    const { textSearch } = filters.value

    const response = await axios.get(
      `${apiUrl}Users/Users_GetAll/${textSearch || 'null'}`,
      { headers: authHeader },
    )

    usersData.value = response.data
    userCount.value = usersData.value.length
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

function openAddDialog() {
  formData.value = {
    userID: '',
    userName: '',
    email: '',
    phoneNumber: '',
    address: '',
    password: '',
    userType: '',
    userImage: null, // إعادة تعيين الصورة عند فتح نافذة الإضافة
  }
  selectedFile.value = null
  currentUserID.value = null
  addDialog.value = true
}

function openEditDialog(userID) {
  console.log(userID)
  currentUserID.value = userID

  const user = usersData.value.find(item => item.userID === userID)
  if (user) {
    formData.value = { ...user }
    formData.value.password = ''
    selectedFile.value = null
    editDialog.value = true
  }
}

function openDeleteDialog(userID){
  console.log(userID)
  userIdToDelete.value = userID
  confirmDeleteDialog.value = true
}

async function addUser() {
  const authHeader = getAuthHeaders()
  const url = `${apiUrl}Users/Users_Create`
  const data = new FormData()

  Object.keys(formData.value).forEach(key => {
    if (key !== 'userImage') { // Skip userImage from formData, we use selectedFile
      data.append(key, formData.value[key])
    }
  })
  
  if (selectedFile.value) {
    const file = Array.isArray(selectedFile.value) ? selectedFile.value[0] : selectedFile.value
    if (file) {
      data.append('UserImage', file)
    }
  }

  try {
    const response = await axios.postForm(url, data, { headers: { 'Content-Type': 'multipart/form-data', ...authHeader } })

    usersData.value.push(response.data)
    userCount.value = usersData.value.length
    addDialog.value = false
  } catch (error) {
    console.error(error)
  }
}

async function updateUser() {
  const authHeader = getAuthHeaders()
  const url = `${apiUrl}Users/Users_Update/${currentUserID.value}`
  const data = new FormData()

  Object.keys(formData.value).forEach(key => {
    if (key !== 'userImage') {
      data.append(key, formData.value[key] || '')
    }
  })

  // Only append UserImage if a new file is selected
  if (selectedFile.value) {
    const file = Array.isArray(selectedFile.value) ? selectedFile.value[0] : selectedFile.value
    if (file) {
      data.append('UserImage', file)
    }
  }

  try {
    await axios.putForm(url, data, { headers: { 'Content-Type': 'multipart/form-data', ...authHeader } })
    await fetchUsers()
    editDialog.value = false
    selectedFile.value = null
  } catch (error) {
    console.error(error)
  }
}

async function deleteUser() {
  if (userIdToDelete.value) {
    try {
      const authHeader = getAuthHeaders()

      await axios.delete(`${apiUrl}Users/Users_Delete/${userIdToDelete.value}`, { headers: authHeader })

      const index = usersData.value.findIndex(user => user.userID === userIdToDelete.value)
      if (index !== -1) {
        usersData.value.splice(index, 1)
      }
      userCount.value = usersData.value.length
      confirmDeleteDialog.value = false
    } catch (error) {
      console.error(error)
    }
  }
}



function exportToExcel() {
  const dataToExport = usersData.value.map(user => ({
    'الاسم': user.userName || 'لا يوجد',
    'البريد الإلكتروني': user.email || 'لا يوجد',
    'الهاتف': user.phoneNumber || 'لا يوجد',
    'العنوان': user.address || 'لا يوجد',
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()

  XLSX.utils.book_append_sheet(workbook, worksheet, "Users")
  XLSX.writeFile(workbook, "users.xlsx")
}

function openImageDialog(imageUrl) {
  selectedImage.value = apiUrlImage+imageUrl
  imageDialog.value = true
}

const limitPhoneLength = event => {
  let value = event.target.value.toString()
  if (value.length > 11) {
    value = value.slice(0, 11)
    event.target.value = value
  }
  formData.value.phoneNumber = value
}

onMounted(() => {
  fetchUsers()
})
</script>

<template>
  <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, i) in cards"
      :key="i"
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
          cols="12"
          md="4"
        >
          <VLabel class="mb-2">
            بحث الاسم
          </VLabel>
          <AppTextField
            v-model="filters.textSearch"
            placeholder="أدخل اسم المستخدم"
            clearable
            clear-icon="tabler-x"
            prepend-inner-icon="tabler-user-search"
          />
        </VCol>
        <VCol
          cols="12"
          md="8"
          class="text-right d-flex align-center justify-end flex-wrap gap-2"
          style="margin-block-start: 20px;"
        >
          <VBtn
            color="primary"
            :loading="loading"
            prepend-icon="tabler-search"
            @click="fetchUsers"
          >
            بحث
          </VBtn>
          <VBtn
            color="success"
            prepend-icon="tabler-plus"
            @click="openAddDialog"
          >
            إضافة مستخدم
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

      <VRow class="d-flex flex-column">
        <VDataTable
          :headers="[
            { title: 'الصورة', key: 'userImage' },
            { title: 'اسم المستخدم', key: 'userName' },
            { title: 'البريد الإلكتروني', key: 'email' },
            { title: 'الهاتف', key: 'phoneNumber' },
            { title: 'العنوان', key: 'address' },
            { title: 'نوع المستخدم', key: 'userType' },
            { title: '', key: 'update' },
            { title: '', key: 'delete' }
          ]"
          :items="usersData"
          :items-per-page="10"
          items-per-page-text="العناصر في الصفحة"
          class="text-no-wrap custom-data-table"
        >
          <template #item.userImage="{ item }">
            <div @click="openImageDialog(item.userImage)">
              <img
                v-if="item.userImage && !item.userImage.includes(':')"
                :src="apiUrlImage + item.userImage"
                alt="user"
                style="border-radius: 50%; block-size: 40px; inline-size: 40px; margin-block-start: 10px;"
                @error="$event.target.src = 'https://www.w3schools.com/w3images/avatar2.png'"
              >
              <VAvatar
                v-else
                color="secondary"
                size="40"
                class="mt-2"
              >
                <VIcon icon="tabler-user" />
              </VAvatar>
            </div>
          </template>
          <template #item.update="{ item }">
            <VBtn
              color="primary"
              small
              prepend-icon="tabler-edit"
              @click="openEditDialog(item.userID)"
            >
              تعديل
            </VBtn>
          </template>
          <template #item.delete="{ item }">
            <VBtn
              color="error"
              small
              prepend-icon="tabler-trash"
              @click="openDeleteDialog(item.userID)"
            >
              حذف
            </VBtn>
          </template>
        </VDataTable>
      </VRow>
    </VForm>

    <!-- Confirm Delete -->
    <VDialog
      v-model="confirmDeleteDialog"
      max-width="400px"
      content-class="modern-dialog"
    >
      <VCard class="pa-4 text-center">
        <VCardText class="d-flex flex-column align-center justify-center">
          <VAvatar
            color="error"
            variant="tonal"
            size="80"
            class="mb-4"
          >
            <VIcon
              icon="tabler-alert-triangle"
              size="48"
            />
          </VAvatar>
          <h3 class="text-h5 font-weight-bold mb-2">
            تأكيد الحذف
          </h3>
          <p class="text-medium-emphasis">
            هل أنت متأكد من حذف هذا المستخدم؟ لا يمكن التراجع عن هذا الإجراء.
          </p>
        </VCardText>
        <VCardActions class="justify-center gap-3">
          <VBtn
            variant="tonal"
            color="secondary"
            class="action-btn"
            height="44"
            prepend-icon="tabler-x"
            @click="confirmDeleteDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="error"
            class="action-btn"
            height="44"
            elevation="2"
            prepend-icon="tabler-trash"
            @click="deleteUser"
          >
            حذف نهائي
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- Add/Edit Dialog -->
    <VDialog
      v-model="addDialog"
      max-width="700px"
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
                icon="tabler-user-plus"
                size="28"
              />
            </VAvatar>
            <div>
              <h4 class="text-h6 font-weight-bold">
                إضافة مستخدم جديد
              </h4>
              <span class="text-caption text-medium-emphasis">أدخل بيانات المستخدم الجديد</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="secondary"
            size="small"
            @click="addDialog = false"
          >
            <VIcon
              icon="tabler-x"
              size="24"
            />
          </VBtn>
        </div>

        <VCardText class="pa-4">
          <VForm @submit.prevent="addUser">
            <VRow>
              <VCol
                cols="12"
                sm="6"
              >
                <AppTextField
                  v-model="formData.userName"
                  placeholder="اسم المستخدم"
                  prepend-inner-icon="tabler-user"
                  label="اسم المستخدم"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <AppTextField
                  v-model="formData.email"
                  placeholder="البريد الإلكتروني"
                  prepend-inner-icon="tabler-mail"
                  label="البريد الإلكتروني"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <AppTextField
                  v-model="formData.phoneNumber"
                  placeholder="الهاتف"
                  prepend-inner-icon="tabler-phone"
                  label="الهاتف"
                  @input="limitPhoneLength"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <AppTextField
                  v-model="formData.address"
                  placeholder="العنوان"
                  prepend-inner-icon="tabler-map-pin"
                  label="العنوان"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <AppTextField
                  v-model="formData.password"
                  type="password"
                  placeholder="كلمة السر"
                  prepend-inner-icon="tabler-lock"
                  label="كلمة السر"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <VAutocomplete
                  v-model="formData.userType"
                  :items="['محاسب رئيسي', 'محاسب فرعي', 'مدير فرع', 'موظف مبيعات']"
                  placeholder="نوع المستخدم"
                  prepend-inner-icon="tabler-category"
                  label="نوع المستخدم"
                />
              </VCol>
              <!-- File Input for Image -->
              <VCol cols="12">
                <VFileInput
                  v-model="selectedFile"
                  label="صورة المستخدم"
                  accept="image/*"
                  prepend-icon="tabler-camera"
                />
                <!-- Preview -->
                <div
                  v-if="imagePreview"
                  class="mt-2 text-center"
                >
                  <img
                    :src="imagePreview"
                    style=" border-radius: 8px; max-block-size: 150px;max-inline-size: 150px;"
                  >
                </div>
              </VCol>
              
              <VDivider class="my-6" />

              <VCol
                cols="12"
                class="d-flex justify-end gap-3"
              >
                <VBtn
                  variant="outlined"
                  color="secondary"
                  class="action-btn"
                  height="44"
                  prepend-icon="tabler-x"
                  @click="addDialog = false"
                >
                  إلغاء
                </VBtn>
                <VBtn
                  color="primary"
                  class="action-btn"
                  height="44"
                  elevation="4"
                  prepend-icon="tabler-check"
                  type="submit"
                >
                  حفظ
                </VBtn>
              </VCol>
            </VRow>
          </VForm>
        </VCardText>
      </VCard>
    </VDialog>



    <VDialog
      v-model="editDialog"
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
                تعديل مستخدم
              </h4>
              <span class="text-caption text-medium-emphasis">تحديث تفاصيل المستخدم</span>
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            color="secondary"
            size="small"
            @click="editDialog = false"
          >
            <VIcon
              icon="tabler-x"
              size="24"
            />
          </VBtn>
        </div>
        <VCardText>
          <VForm @submit.prevent="updateUser">
            <VRow>
              <VCol
                cols="12"
                sm="6"
              >
                <AppTextField
                  v-model="formData.userName"
                  placeholder="اسم المستخدم"
                  prepend-inner-icon="tabler-user"
                  label="اسم المستخدم"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <AppTextField
                  v-model="formData.email"
                  placeholder="البريد الإلكتروني"
                  prepend-inner-icon="tabler-mail"
                  label="البريد الإلكتروني"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <AppTextField
                  v-model="formData.phoneNumber"
                  placeholder="الهاتف"
                  prepend-inner-icon="tabler-phone"
                  label="الهاتف"
                  @input="limitPhoneLength"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <AppTextField
                  v-model="formData.address"
                  placeholder="العنوان"
                  prepend-inner-icon="tabler-map-pin"
                  label="العنوان"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <VTextField
                  v-model="formData.password"
                  type="password"
                  placeholder="كلمة السر"
                  prepend-inner-icon="tabler-lock"
                  label="كلمة السر"
                />
              </VCol>
              <VCol
                cols="12"
                sm="6"
              >
                <VAutocomplete
                  v-model="formData.userType"
                  :items="['محاسب رئيسي', 'محاسب فرعي', 'مدير فرع', 'موظف مبيعات']"
                  placeholder="نوع المستخدم"
                  prepend-inner-icon="tabler-category"
                  label="نوع المستخدم"
                />
              </VCol>
              <!-- File Input for Image -->
              <VCol cols="12">
                <VFileInput
                  v-model="selectedFile"
                  label="تغيير صورة المستخدم"
                  accept="image/*"
                  prepend-icon="tabler-camera"
                />
                <!-- Preview New Image -->
                <div
                  v-if="imagePreview"
                  class="mt-2 text-center"
                >
                  <p>معاينة الصورة الجديدة:</p>
                  <img
                    :src="imagePreview"
                    style=" border-radius: 8px; max-block-size: 150px;max-inline-size: 150px;"
                  >
                </div>
                <!-- Current Image -->
                <div
                  v-else-if="formData.userImage"
                  class="mt-2 text-center"
                >
                  <p>الصورة الحالية:</p>
                  <img
                    :src="apiUrlImage + formData.userImage"
                    style=" border-radius: 8px; max-block-size: 150px;max-inline-size: 150px;"
                  >
                </div>
              </VCol>
              <VCol
                cols="12"
                class="d-flex justify-end gap-3"
              >
                <VBtn
                  variant="tonal"
                  color="secondary"
                  prepend-icon="tabler-x"
                  @click="editDialog = false"
                >
                  إلغاء
                </VBtn>
                <VBtn
                  color="primary"
                  prepend-icon="tabler-check"
                  type="submit"
                >
                  تعديل
                </VBtn>
              </VCol>
            </VRow>
          </VForm>
        </VCardText>
      </VCard>
    </VDialog>

    <!-- Image Preview Dialog -->
    <VDialog
      v-model="imageDialog"
      max-width="800px"
      content-class="modern-dialog"
    >
      <VCard>
        <div class="dialog-header pa-4 d-flex align-center justify-end">
          <VBtn
            icon
            variant="text"
            color="secondary"
            @click="imageDialog = false"
          >
            <VIcon
              icon="tabler-x"
              size="24"
            />
          </VBtn>
        </div>
        <VCardText class="d-flex justify-center align-center pa-4">
          <img
            :src="selectedImage"
            alt="large-image"
            style=" border-radius: 8px;max-block-size: 70vh; max-inline-size: 100%; object-fit: contain;"
          >
        </VCardText>
      </VCard>
    </VDialog>
  </VCard>
</template>


<style scoped>
.text-no-wrap { white-space: nowrap; }
.text-right { text-align: end; }
.d-flex { display: flex; }
.align-center { align-items: center; }
.justify-end { justify-content: flex-end; }
</style>
