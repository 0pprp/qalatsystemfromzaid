<script setup>
import AppAutocomplete from "@core/components/app-form-elements/AppAutocomplete.vue"
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import { jwtDecode } from 'jwt-decode'
import { useTheme } from 'vuetify'
import { useCities, isLocalLab, LOCAL_API } from '@/composables/useCities'

// Import assets
import bgImage from '@images/background.png'
import logo from '@images/logo.png'

definePage({ meta: { layout: 'blank' } })

const form = ref({
  userName: '',
  password: '',
})

const theme = useTheme()
const loginLoadingBtn = ref(false)
const errorMessage = ref("")
const router = useRouter()
const showAlert = ref(false)
const refForm = ref({})
const selectCity = ref(null)

const { provinces, isLoading: isLoadingCities, error: citiesError, fetchCities } = useCities()

const validateForm = async () => {
  return await refForm.value?.validate()
}

function loginBody() {
  const u = form.value.userName.trim()
  const p = form.value.password

  return {
    userName: u,
    password: p,
    UserName: u,
    Password: p,
  }
}

async function login() {
  showAlert.value = false
  loginLoadingBtn.value = false

  const validForm = await validateForm()
  if (validForm.valid === true) {
    loginLoadingBtn.value = true

    if (!selectCity.value) {
      showAlert.value = true
      errorMessage.value = "يجب اختيار المحافظة"
      loginLoadingBtn.value = false

      return
    }

    const selectedProvince = provinces.value.find(p => p.value === selectCity.value)
    const selectedApi = isLocalLab() ? LOCAL_API : selectedProvince?.link
    const cityName = selectedProvince?.name || ''
    const database = selectedProvince?.database || ''

    if (!selectedApi) {
      showAlert.value = true
      errorMessage.value = "لا يوجد رابط API لهذه المحافظة"
      loginLoadingBtn.value = false

      return
    }

    const postLogin = async endpoint => {
      let res
      try {
        res = await fetch(`${selectedApi}${endpoint}`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify(loginBody()),
        })
      }
      catch {
        throw new Error('تعذر الاتصال بخادم المحافظة')
      }

      let data = null
      try {
        data = await res.json()
      }
      catch {
        data = null
      }

      if (res.status >= 500)
        throw new Error('تعذر الاتصال بخادم المحافظة')

      return data || {}
    }

    try {
      // محاسب رئيسي / مدير فرع → Admin؛ محاسب فرعي → Employee (نفس FE_Company)
      let resData = await postLogin('Users/Users_LoginAdmin')
      if (resData.message)
        resData = await postLogin('Users/Users_LoginEmployee')

      if (resData.message) {
        loginLoadingBtn.value = false
        showAlert.value = true
        errorMessage.value = resData.message
      }
      else if (!resData.token) {
        loginLoadingBtn.value = false
        showAlert.value = true
        errorMessage.value = 'تعذر إكمال تسجيل الدخول. حاول مرة أخرى.'
      }
      else {
        loginLoadingBtn.value = false

        const user = jwtDecode(resData.token)
        const userType = user.UserType || user.userType || ''

        localStorage.setItem("Token", resData.token)
        localStorage.setItem('Expiration', resData.expiration)
        localStorage.setItem('UserID', user.UserID)
        localStorage.setItem('UserName', user.UserName)
        localStorage.setItem('UserImage', user.UserImage)
        localStorage.setItem('UserType', userType || 'محاسب فرعي')
        localStorage.setItem("LinkCity", selectedApi)
        localStorage.setItem("CityName", cityName)
        localStorage.setItem("Password", form.value.password)
        localStorage.setItem("Database", database)
        localStorage.setItem('SummaryReadOnly', '1')

        router.push('/')
      }
    }
    catch (err) {
      loginLoadingBtn.value = false
      showAlert.value = true
      errorMessage.value = err.message || 'تعذر الاتصال بالخادم. تأكد من الشبكة.'
    }
  }
  else {
    showAlert.value = true
    errorMessage.value = "يجب ادخال اسم المستخدم وكلمة المرور"
  }
}

const isPasswordVisible = ref(false)
</script>

<template>
  <div class="auth-wrapper d-flex align-center justify-center pa-4">
    <div
      class="auth-bg-layer"
      :style="{ backgroundImage: `url(${bgImage})` }"
    >
      <div class="auth-bg-overlay" />
    </div>

    <VCard
      class="auth-card elevation-24 rounded-xl"
      max-width="480"
      width="100%"
      color="surface"
    >
      <div class="pa-8 d-flex flex-column align-center text-center">
        <div class="mb-6">
          <VImg
            :src="logo"
            width="120"
            class="mx-auto"
          />
        </div>

        <h3 class="text-h4 font-weight-bold text-primary mb-2">
          رابط الملخص
        </h3>
        <p class="text-body-1 text-medium-emphasis mb-8">
          عرض البيانات فقط — محاسب رئيسي أو فرعي
        </p>

        <VForm
          ref="refForm"
          class="w-100"
          @submit.prevent="login"
        >
          <VAlert
            v-if="showAlert"
            color="error"
            variant="tonal"
            closable
            class="mb-6 text-start"
            density="compact"
            @click:close="showAlert = false"
          >
            {{ errorMessage }}
          </VAlert>

          <VRow>
            <VCol cols="12">
              <AppAutocomplete
                v-model="selectCity"
                :items="provinces"
                item-title="name"
                item-value="value"
                label="اختر المحافظة"
                placeholder="جميع فروع الشركة"
                :rules="[requiredValidator]"
                variant="outlined"
                density="comfortable"
                color="primary"
                cache-items
                prepend-inner-icon="tabler-map-pin"
              />
            </VCol>

            <VCol cols="12">
              <AppTextField
                v-model="form.userName"
                label="اسم المستخدم"
                placeholder="ادخل اسم المستخدم"
                type="text"
                autofocus
                :rules="[requiredValidator]"
                variant="outlined"
                density="comfortable"
                color="primary"
                prepend-inner-icon="tabler-user"
              />
            </VCol>

            <VCol cols="12">
              <AppTextField
                v-model="form.password"
                label="كلمة المرور"
                placeholder="············"
                :type="isPasswordVisible ? 'text' : 'password'"
                :rules="[requiredValidator]"
                :append-inner-icon="isPasswordVisible ? 'tabler-eye-off' : 'tabler-eye'"
                variant="outlined"
                density="comfortable"
                color="primary"
                prepend-inner-icon="tabler-lock"
                @click:append-inner="isPasswordVisible = !isPasswordVisible"
              />
            </VCol>

            <VCol
              cols="12"
              class="mt-2"
            >
              <VBtn
                block
                type="submit"
                size="large"
                :loading="loginLoadingBtn"
                color="primary"
                elevation="4"
                class="font-weight-bold"
                rounded="lg"
              >
                تسجيل الدخول
              </VBtn>
            </VCol>
          </VRow>
        </VForm>
      </div>

      <div class="auth-card-footer py-3 bg-grey-100 dark:bg-grey-900 text-center text-caption text-disabled">
        جميع الحقوق محفوظة &copy; {{ new Date().getFullYear() }} قلعة الضمان
      </div>
    </VCard>
  </div>
</template>

<style lang="scss" scoped>
@use "@core/scss/template/pages/page-auth.scss";

.auth-wrapper {
  position: relative;
  display: flex;
  overflow: hidden;
  align-items: center;
  justify-content: center;
  min-block-size: 100dvh;
  padding-block: 2rem;
}

.auth-bg-layer {
  position: fixed;
  z-index: 0;
  background-position: center;
  background-size: cover;
  block-size: 100%;
  inline-size: 100%;
  inset: 0;
  transform: scale(1.05);
}

.auth-bg-overlay {
  position: absolute;
  z-index: 1;
  backdrop-filter: blur(6px);
  background: linear-gradient(135deg, rgba(var(--v-theme-primary), 0.7), rgba(0, 0, 0, 85%));
  inset: 0;
}

.auth-card {
  position: relative;
  z-index: 2;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  box-shadow: 0 12px 40px rgba(0, 0, 0, 20%) !important;
}
</style>
