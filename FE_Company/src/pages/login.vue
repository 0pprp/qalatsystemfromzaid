<script setup>
import AppAutocomplete from "@core/components/app-form-elements/AppAutocomplete.vue"
import AppTextField from "@core/components/app-form-elements/AppTextField.vue"
import { jwtDecode } from 'jwt-decode'
import { useTheme } from 'vuetify'
import { useCities, isLocalLab, isDemo, LOCAL_API, DEMO_API } from '@/composables/useCities'
import { salesGatewayBase } from '@/composables/useSalesBranches'

// Import assets
import bgImage from '@images/background.png'
import logo from '@images/logo.png'

definePage({ meta: { layout: 'blank', public: true, unauthenticatedOnly: true } })

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

// جلب المدن من API ديناميكياً
const { provinces, isLoading: isLoadingCities, error: citiesError, fetchCities } = useCities()

watch(provinces, list => {
  if (!selectCity.value && list.length === 1)
    selectCity.value = list[0].value
}, { immediate: true })

const validateForm = async () => {
  return await refForm.value?.validate()
}


async function login() {
  showAlert.value = false
  loginLoadingBtn.value = false

  const validForm = await validateForm()
  if (validForm.valid === true) {
    loginLoadingBtn.value = true

    try {
      if (!isDemo()) {
        const gatewayRes = await fetch(`${salesGatewayBase()}Auth/LoginSalesManager`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(form.value),
        }).then(res => res.json()).catch(() => null)

        if (gatewayRes?.token && (gatewayRes.userType === 'مدير مبيعات' || gatewayRes.central)) {
          localStorage.setItem('Token', gatewayRes.token)
          localStorage.setItem('Expiration', gatewayRes.expiration)
          localStorage.setItem('UserID', String(gatewayRes.userId ?? 0))
          localStorage.setItem('UserName', gatewayRes.userName || form.value.userName)
          localStorage.setItem('UserType', 'مدير مبيعات')
          localStorage.setItem('SalesManagerScope', 'central')
          localStorage.setItem('LinkCity', salesGatewayBase())
          localStorage.setItem('CityName', 'كل المحافظات')
          localStorage.removeItem('Database')
          loginLoadingBtn.value = false
          router.push('/sales-manager-dashboard')

          return
        }
      }
    }
    catch {
      // fall through to city accountant login
    }

    if (!selectCity.value) {
      showAlert.value = true
      errorMessage.value = 'يجب اختيار المحافظة'
      loginLoadingBtn.value = false

      return
    }

    // البحث عن المحافظة المختارة في البيانات
    const selectedProvince = provinces.value.find(p => p.value === selectCity.value)

    const selectedApi = isDemo()
      ? DEMO_API
      : (isLocalLab() ? LOCAL_API : selectedProvince?.link)

    const cityName = selectedProvince?.name || ''
    const database = selectedProvince?.database || ''

    if (!selectedApi) {
      showAlert.value = true
      errorMessage.value = "لا يوجد رابط API لهذه المحافظة"
      loginLoadingBtn.value = false

      return
    }

    const postLogin = endpoint => fetch(`${selectedApi}${endpoint}`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(form.value),
    }).then(res => res.json())

    try {
      let resData = await postLogin('Users/Users_LoginAdmin')

      // على اللوكال والإنتاج: المحاسب الفرعي ومدير المبيعات يدخلون من نفس الصفحة
      if (resData.message)
        resData = await postLogin('Users/Users_LoginEmployee')

      if (resData.message) {
        loginLoadingBtn.value = false
        showAlert.value = true
        errorMessage.value = resData.message
      } else {
        loginLoadingBtn.value = false

        const user = jwtDecode(resData.token)
        const userType = user.UserType || user.userType || ''

        localStorage.setItem("Token", resData.token)
        localStorage.setItem('Expiration', resData.expiration)
        localStorage.setItem('UserID', user.UserID)
        localStorage.setItem('UserName', user.UserName)
        localStorage.setItem('UserImage', user.UserImage)
        localStorage.setItem('UserType', userType)
        localStorage.setItem("LinkCity", selectedApi)
        localStorage.setItem("CityName", cityName)
        localStorage.setItem("Password", form.value.password)
        localStorage.setItem("Database", database)
        localStorage.removeItem('SalesManagerScope')

        router.push(userType === 'مدير مبيعات' ? '/sales-manager-dashboard' : '/')
      }
    } catch (err) {
      loginLoadingBtn.value = false
      showAlert.value = true
      errorMessage.value = err.message || (isDemo()
        ? 'تعذر الاتصال بخادم Demo. تأكد أن BE_Company يعمل على المنفذ 5401.'
        : 'تعذر الاتصال بالخادم المحلي. تأكد أن الـ API يعمل على المنفذ 5180.')
    }
  } else {
    showAlert.value = true
    errorMessage.value = "يجب ادخال اسم المستخدم وكلمة المرور"
  }
}



const isPasswordVisible = ref(false)
</script>

<template>
  <div class="auth-wrapper d-flex align-center justify-center pa-4">
    <!-- Full Background with Fixed Position -->
    <div 
      class="auth-bg-layer"
      :style="{ backgroundImage: `url(${bgImage})` }"
    >
      <div class="auth-bg-overlay" />
    </div>

    <!-- Floating Professional Card -->
    <VCard
      class="auth-card elevation-24 rounded-xl"
      max-width="480"
      width="100%"
      color="surface"
    >
      <div class="pa-8 d-flex flex-column align-center text-center">
        <!-- Logo Area -->
        <div class="mb-6">
          <VImg
            :src="logo"
            width="120"
            class="mx-auto"
          />
        </div>

        <h3 class="text-h4 font-weight-bold text-primary mb-2">
          مرحباً بعودتك!
        </h3>
        <p class="text-body-1 text-medium-emphasis mb-8">
          شركة قلعة الضمان لأنظمة الإدارة
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
                placeholder="مطلوبة للمحاسب — اختيارية لمدير المبيعات"
                :rules="[]"
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
      
      <!-- Footer Decoration -->
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
}

.auth-bg-layer {
  position: fixed;
  z-index: 0;
  background-position: center;
  background-size: cover;
  block-size: 100%;
  inline-size: 100%;
  inset: 0;
  transform: scale(1.05); /* Slight zoom for premium feel */
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
  // Professional shadow
  box-shadow: 0 12px 40px rgba(0, 0, 0, 20%) !important;
}

// Ensure full height on mobile
.auth-wrapper {
  padding-block: 2rem;
}
</style>
