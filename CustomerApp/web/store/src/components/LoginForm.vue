<template>
  <div>
    <!-- STEP 1: phone -->
    <v-form v-if="step === 'phone'" ref="phoneForm" @submit.prevent="sendOtp">
      <div class="text-h6 font-weight-bold mb-1">تسجيل الدخول أو إنشاء حساب</div>
      <p class="text-body-2 text-medium-emphasis mb-5">أدخل رقم موبايلك العراقي وسنرسل لك رمز تحقق برسالة SMS.</p>
      <v-alert v-if="referralCode" type="success" variant="tonal" density="compact" class="mb-4" icon="mdi-gift-outline">
        تمت دعوتك برمز الإحالة <b class="ltr">{{ referralCode }}</b>
      </v-alert>
      <PhoneField v-model="phone" :error-messages="phoneError" autofocus />
      <v-btn type="submit" color="primary" size="large" block class="mt-4" :loading="loading">إرسال رمز التحقق</v-btn>
      <p class="text-caption text-medium-emphasis mt-4 text-center">
        بالمتابعة فإنك توافق على
        <router-link :to="{ name: 'terms' }" target="_blank">الشروط والأحكام</router-link> و
        <router-link :to="{ name: 'privacy' }" target="_blank">سياسة الخصوصية</router-link>.
      </p>
    </v-form>

    <!-- STEP 2: OTP -->
    <v-form v-else-if="step === 'otp'" @submit.prevent="onOtpSubmit">
      <v-btn variant="text" size="small" prepend-icon="mdi-arrow-right" class="mb-2 px-1" @click="step = 'phone'">تغيير الرقم</v-btn>
      <div class="text-h6 font-weight-bold mb-1">أدخل رمز التحقق</div>
      <p class="text-body-2 text-medium-emphasis mb-5">
        أرسلنا رمزاً من 6 أرقام إلى <b class="ltr">{{ formatPhone(normPhone) }}</b>
      </p>
      <OtpField
        v-model="code"
        :expires-in-seconds="otp.expiresInSeconds"
        :resend-after-seconds="otp.resendAfterSeconds"
        :started-at="otp.sentAt"
        :error-messages="codeError"
        :resending="resending"
        @complete="onOtpSubmit"
        @resend="resend"
      />
      <v-alert v-if="isFake" type="info" variant="tonal" density="compact" class="mb-3">وضع العرض التجريبي: الرمز هو <b class="ltr">123456</b></v-alert>
      <v-btn type="submit" color="primary" size="large" block :loading="loading" :disabled="code.length !== 6">
        {{ otp.isRegistered ? 'تأكيد ودخول' : 'متابعة' }}
      </v-btn>
    </v-form>

    <!-- STEP 3: first-time registration -->
    <v-form v-else-if="step === 'register'" ref="regForm" @submit.prevent="register">
      <v-btn variant="text" size="small" prepend-icon="mdi-arrow-right" class="mb-2 px-1" @click="step = 'otp'">رجوع</v-btn>
      <div class="text-h6 font-weight-bold mb-1">أهلاً بك! أكمل بياناتك</div>
      <p class="text-body-2 text-medium-emphasis mb-4">هذه أول مرة تستخدم فيها هذا الرقم. يرجى كتابة اسمك الثلاثي كما في الهوية.</p>
      <v-row dense>
        <v-col cols="12" sm="6"><v-text-field v-model="reg.firstName" label="الاسم الأول *" :rules="[nameRule('الاسم الأول')]" :error-messages="fieldErrors.firstName" autocomplete="given-name" /></v-col>
        <v-col cols="12" sm="6"><v-text-field v-model="reg.fatherName" label="اسم الأب *" :rules="[nameRule('اسم الأب')]" :error-messages="fieldErrors.fatherName" /></v-col>
        <v-col cols="12" sm="6"><v-text-field v-model="reg.grandfatherName" label="اسم الجد *" :rules="[nameRule('اسم الجد')]" :error-messages="fieldErrors.grandfatherName" /></v-col>
        <v-col cols="12" sm="6"><v-text-field v-model="reg.surname" label="اللقب (اختياري)" :rules="[optionalNameRule]" :error-messages="fieldErrors.surname" autocomplete="family-name" /></v-col>
        <v-col cols="12">
          <v-select
            v-model="reg.cityValue"
            :items="catalog.branches"
            item-title="name"
            item-value="cityValue"
            label="الفرع / المحافظة *"
            :rules="[(v) => !!v || 'اختر الفرع']"
            :error-messages="fieldErrors.cityValue"
            prepend-inner-icon="mdi-map-marker-outline"
            :loading="!catalog.branches.length"
          />
        </v-col>
        <v-col cols="12">
          <v-text-field
            v-model="reg.referralCode"
            label="رمز الإحالة (اختياري)"
            dir="ltr"
            prepend-inner-icon="mdi-gift-outline"
            :error-messages="fieldErrors.referralCode"
            clearable
            hint="إذا دعاك أحد، اكتب رمزه هنا"
            @update:model-value="fieldErrors.referralCode = ''"
          />
        </v-col>
      </v-row>
      <v-checkbox v-model="reg.acceptTerms" :rules="[(v) => !!v || 'يجب الموافقة على الشروط للمتابعة']" :error-messages="fieldErrors.acceptTerms" density="compact" color="primary" hide-details="auto">
        <template #label>
          <span class="text-body-2">أوافق على <router-link :to="{ name: 'terms' }" target="_blank" @click.stop>الشروط والأحكام</router-link> و<router-link :to="{ name: 'privacy' }" target="_blank" @click.stop>سياسة الخصوصية</router-link> *</span>
        </template>
      </v-checkbox>
      <v-checkbox v-model="reg.marketingOptIn" density="compact" color="primary" hide-details label="أرغب باستلام العروض والتخفيضات (اختياري)" />
      <v-btn type="submit" color="primary" size="large" block class="mt-4" :loading="loading">إنشاء الحساب</v-btn>
    </v-form>

    <v-alert v-if="generalError" type="error" variant="tonal" density="compact" class="mt-4">{{ generalError }}</v-alert>
  </div>
</template>

<script setup>
import { ref, reactive, computed } from 'vue'
import PhoneField from './PhoneField.vue'
import OtpField from './OtpField.vue'
import { useAuthStore } from '@/stores/auth'
import { useCatalogStore } from '@/stores/catalog'
import { normalizeIraqiPhone, isValidIraqiPhone, formatPhone } from '@/utils/phone'
import { nameRule, optionalNameRule, cleanName } from '@/utils/names'
import { apiErrorMessage, apiErrorCode, apiErrorFields } from '@/utils/errors'
import { USE_FAKE_API } from '@/api/client'

const emit = defineEmits(['success'])
const auth = useAuthStore()
const catalog = useCatalogStore()
catalog.loadBranches().catch(() => {})
const isFake = USE_FAKE_API

const step = ref('phone')
const phone = ref('')
const code = ref('')
const loading = ref(false)
const resending = ref(false)
const phoneError = ref('')
const codeError = ref('')
const generalError = ref('')
const fieldErrors = reactive({})
const phoneForm = ref()
const regForm = ref()
const otp = reactive({ requestId: null, expiresInSeconds: 300, resendAfterSeconds: 60, isRegistered: true, sentAt: 0 })
const referralCode = ref(auth.pendingReferralCode)
const reg = reactive({
  firstName: '', fatherName: '', grandfatherName: '', surname: '', cityValue: null,
  referralCode: referralCode.value, acceptTerms: false, marketingOptIn: false,
})
const normPhone = computed(() => normalizeIraqiPhone(phone.value))

function resetErrors() {
  phoneError.value = ''
  codeError.value = ''
  generalError.value = ''
  Object.keys(fieldErrors).forEach((k) => delete fieldErrors[k])
}

async function requestCode() {
  const res = await auth.requestOtp(normPhone.value)
  Object.assign(otp, res, { sentAt: Date.now() })
  code.value = ''
}

function handleRequestError(e) {
  const c = apiErrorCode(e)
  if (c === 'invalid_phone') phoneError.value = apiErrorMessage(e)
  else if (c === 'otp_rate_limited') {
    const s = e.response?.data?.retryAfterSeconds
    generalError.value = `${apiErrorMessage(e)}${s ? ` (بعد ${s} ثانية)` : ''}`
  } else generalError.value = apiErrorMessage(e)
}

async function sendOtp() {
  resetErrors()
  const { valid } = await phoneForm.value.validate()
  if (!valid || !isValidIraqiPhone(phone.value)) return
  loading.value = true
  try {
    await requestCode()
    step.value = 'otp'
  } catch (e) {
    handleRequestError(e)
  } finally {
    loading.value = false
  }
}

async function resend() {
  resetErrors()
  resending.value = true
  try {
    await requestCode()
  } catch (e) {
    handleRequestError(e)
  } finally {
    resending.value = false
  }
}

async function onOtpSubmit() {
  if (code.value.length !== 6 || loading.value) return
  resetErrors()
  if (!otp.isRegistered) {
    step.value = 'register'
    return
  }
  await verify()
}

async function register() {
  resetErrors()
  const { valid } = await regForm.value.validate()
  if (!valid) return
  await verify({
    firstName: cleanName(reg.firstName),
    fatherName: cleanName(reg.fatherName),
    grandfatherName: cleanName(reg.grandfatherName),
    surname: cleanName(reg.surname) || null,
    cityValue: reg.cityValue,
    referralCode: reg.referralCode?.trim() || null,
    acceptTerms: reg.acceptTerms === true,
    marketingOptIn: reg.marketingOptIn === true,
  })
}

async function verify(registration) {
  loading.value = true
  try {
    const body = { requestId: otp.requestId, phone: normPhone.value, code: code.value }
    if (registration) body.registration = registration
    const res = await auth.verifyOtp(body)
    emit('success', res)
  } catch (e) {
    const c = apiErrorCode(e)
    if (c === 'otp_invalid') {
      const left = e.response?.data?.attemptsLeft
      codeError.value = `${apiErrorMessage(e)}${left !== undefined ? ` — المحاولات المتبقية: ${left}` : ''}`
      step.value = 'otp'
    } else if (c === 'otp_expired' || c === 'otp_too_many_attempts') {
      codeError.value = apiErrorMessage(e)
      step.value = 'otp'
    } else if (c === 'invalid_referral_code') {
      fieldErrors.referralCode = apiErrorMessage(e, 'رمز الإحالة غير صحيح') + ' — يمكنك حذفه والمتابعة'
    } else if (c === 'validation') {
      Object.assign(fieldErrors, apiErrorFields(e))
      generalError.value = apiErrorMessage(e)
    } else {
      generalError.value = apiErrorMessage(e)
    }
  } finally {
    loading.value = false
  }
}
</script>
