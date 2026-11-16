<template>
  <div>
    <h1 class="text-h6 font-weight-bold mb-3">شبكتي</h1>
    <template v-if="loading">
      <v-skeleton-loader type="card" class="rounded-xl mb-3" />
      <v-row dense><v-col v-for="n in 3" :key="n" cols="4"><v-skeleton-loader type="list-item-two-line" class="rounded-xl" /></v-col></v-row>
    </template>
    <ErrorState v-else-if="error" :message="error" @retry="load" />
    <template v-else-if="s">
      <!-- referral link -->
      <v-card class="bg-brand-gradient mb-4" rounded="xl" flat>
        <v-card-text class="pa-5">
          <div class="text-body-2 mb-1" style="opacity: 0.9">رمز الإحالة الخاص بك</div>
          <div class="d-flex align-center ga-2 mb-3">
            <span class="code ltr">{{ s.referralCode }}</span>
            <v-btn icon="mdi-content-copy" size="small" variant="tonal" color="white" aria-label="نسخ الرمز" @click="copy(s.referralCode, 'تم نسخ الرمز')" />
          </div>
          <div class="link-box d-flex align-center ga-2 mb-3">
            <span class="ltr text-truncate flex-grow-1">{{ s.referralLink }}</span>
            <v-btn size="small" color="secondary" class="text-black" prepend-icon="mdi-content-copy" @click="copy(s.referralLink, 'تم نسخ الرابط')">نسخ الرابط</v-btn>
          </div>
          <ShareButtons :url="s.referralLink" title="قلعة الضمان" :text="shareText" class="share-light" />
        </v-card-text>
      </v-card>

      <v-row dense class="mb-4">
        <v-col cols="4"><StatCard icon="mdi-account-plus-outline" label="دعوات مباشرة" :value="String(s.directCount)" /></v-col>
        <v-col cols="4"><StatCard icon="mdi-account-group-outline" label="حجم الشبكة" :value="String(s.networkSize)" color="indigo" /></v-col>
        <v-col cols="4"><StatCard icon="mdi-cash-multiple" label="أرباح الشبكة" :value="formatMoney(s.networkEarnings)" color="success" /></v-col>
      </v-row>

      <!-- unlock progress -->
      <v-card class="soft-card mb-4" rounded="xl">
        <v-card-text class="pa-5">
          <div class="d-flex align-center flex-wrap ga-2 mb-2">
            <v-icon :color="s.canCreateAccounts ? 'success' : 'grey'">{{ s.canCreateAccounts ? 'mdi-lock-open-variant' : 'mdi-lock' }}</v-icon>
            <span class="font-weight-bold">إنشاء حسابات للآخرين</span>
            <v-spacer />
            <v-btn v-if="s.canCreateAccounts" color="primary" prepend-icon="mdi-account-plus" @click="openCreate">إنشاء حساب لشخص</v-btn>
          </div>
          <template v-if="!s.canCreateAccounts">
            <p class="text-body-2 text-medium-emphasis mb-3">
              أكمل <b class="num">{{ s.salesToUnlockAccountCreation }}</b> عملية بيع إضافية لتتمكن من إنشاء حسابات لأشخاص ليس لديهم هواتف ذكية وضمّهم إلى شبكتك.
            </p>
            <v-progress-linear :model-value="unlockPercent" color="primary" height="12" rounded bg-color="grey-lighten-2" />
            <div class="text-caption text-medium-emphasis mt-1 num">{{ s.completedSalesCount }} / {{ unlockTarget }} مبيعات مكتملة</div>
          </template>
          <p v-else class="text-body-2 text-medium-emphasis mb-0">
            يمكنك إنشاء حساب لشخص آخر برقم هاتفه. سيصله رمز موافقة برسالة نصية، ومشاركته للرمز معك تعني موافقته.
          </p>
        </v-card-text>
      </v-card>

      <!-- direct referrals -->
      <v-card class="soft-card" rounded="xl">
        <v-card-title class="font-weight-bold pt-4">دعواتك المباشرة</v-card-title>
        <v-card-subtitle class="pb-2">نعرض الاسم الأول فقط حفاظاً على الخصوصية.</v-card-subtitle>
        <v-skeleton-loader v-if="directLoading && !direct.length" type="list-item-avatar@3" />
        <EmptyState v-else-if="!direct.length" icon="mdi-account-multiple-plus-outline" title="لم ينضم أحد بعد" text="شارك رابطك مع أصدقائك وعائلتك ليبدأوا التسوّق." />
        <v-list v-else class="py-0">
          <template v-for="(d, i) in direct" :key="i">
            <v-divider v-if="i" />
            <v-list-item class="py-2">
              <template #prepend><v-avatar color="primary" variant="tonal">{{ d.firstName?.slice(0, 1) }}</v-avatar></template>
              <v-list-item-title class="font-weight-bold">{{ d.firstName }}</v-list-item-title>
              <v-list-item-subtitle>انضم {{ formatDate(d.joinedAt) }}</v-list-item-subtitle>
              <template #append><v-chip size="small" variant="tonal" prepend-icon="mdi-account-group-outline" class="num">{{ d.networkSize }}</v-chip></template>
            </v-list-item>
          </template>
        </v-list>
        <div v-if="direct.length < directTotal" class="d-flex justify-center pa-3">
          <v-btn variant="tonal" color="primary" :loading="directLoading" @click="loadDirect(false)">عرض المزيد</v-btn>
        </div>
      </v-card>
    </template>

    <!-- create account for another -->
    <v-dialog v-model="createDialog" max-width="520" :fullscreen="xs" scrollable persistent>
      <v-card :rounded="xs ? 0 : 'xl'">
        <v-card-title class="d-flex align-center pt-4">
          <span class="font-weight-bold">إنشاء حساب لشخص آخر</span>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" aria-label="إغلاق" @click="createDialog = false" />
        </v-card-title>
        <v-card-text>
          <v-form v-if="createStep === 'form'" ref="createForm" @submit.prevent="requestCreateOtp">
            <v-alert type="info" variant="tonal" density="compact" class="mb-4">
              سيصل رمز موافقة إلى هاتف الشخص الجديد. اطلب منه الرمز بعد أن يوافق على إنشاء الحساب.
            </v-alert>
            <PhoneField v-model="cf.phone" label="رقم هاتف الشخص الجديد *" :error-messages="cfErrors.phone" class="mb-2" />
            <v-row dense>
              <v-col cols="12" sm="6"><v-text-field v-model="cf.firstName" label="الاسم الأول *" :rules="[nameRule('الاسم الأول')]" :error-messages="cfErrors.firstName" /></v-col>
              <v-col cols="12" sm="6"><v-text-field v-model="cf.fatherName" label="اسم الأب *" :rules="[nameRule('اسم الأب')]" :error-messages="cfErrors.fatherName" /></v-col>
              <v-col cols="12" sm="6"><v-text-field v-model="cf.grandfatherName" label="اسم الجد *" :rules="[nameRule('اسم الجد')]" :error-messages="cfErrors.grandfatherName" /></v-col>
              <v-col cols="12" sm="6"><v-text-field v-model="cf.surname" label="اللقب (اختياري)" :rules="[optionalNameRule]" /></v-col>
              <v-col cols="12">
                <v-select v-model="cf.cityValue" :items="catalog.branches" item-title="name" item-value="cityValue" label="الفرع *" :rules="[(v) => !!v || 'اختر الفرع']" :error-messages="cfErrors.cityValue" />
              </v-col>
            </v-row>
            <v-btn type="submit" block color="primary" size="large" :loading="createLoading">إرسال رمز الموافقة</v-btn>
          </v-form>

          <v-form v-else-if="createStep === 'otp'" @submit.prevent="verifyCreate">
            <p class="text-body-2 mb-4">أرسلنا رمز موافقة إلى <b class="ltr">{{ formatPhone(cf.phone) }}</b>. اطلب من {{ cf.firstName }} مشاركة الرمز معك.</p>
            <OtpField
              v-model="createCode"
              :expires-in-seconds="createOtp.expiresInSeconds"
              :resend-after-seconds="createOtp.resendAfterSeconds"
              :started-at="createOtp.sentAt"
              :error-messages="createCodeError"
              :resending="createLoading"
              @complete="verifyCreate"
              @resend="requestCreateOtp"
            />
            <v-btn type="submit" block color="primary" size="large" :loading="createLoading" :disabled="createCode.length !== 6">تأكيد وإنشاء الحساب</v-btn>
            <v-btn block variant="text" class="mt-2" @click="createStep = 'form'">تعديل البيانات</v-btn>
          </v-form>

          <div v-else class="text-center py-6">
            <v-avatar color="success" variant="tonal" size="80" class="mb-3"><v-icon size="44">mdi-account-check</v-icon></v-avatar>
            <div class="text-h6 font-weight-bold mb-1">تم إنشاء الحساب بنجاح</div>
            <p class="text-body-2 text-medium-emphasis">أصبح {{ createdName }} ضمن شبكتك. يمكنه الدخول برقم هاتفه في أي وقت.</p>
            <v-btn color="primary" @click="createDialog = false">تم</v-btn>
          </div>
          <v-alert v-if="createError" type="error" variant="tonal" density="compact" class="mt-3">{{ createError }}</v-alert>
        </v-card-text>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useDisplay } from 'vuetify'
import StatCard from '@/components/StatCard.vue'
import ShareButtons from '@/components/ShareButtons.vue'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import PhoneField from '@/components/PhoneField.vue'
import OtpField from '@/components/OtpField.vue'
import { networkApi } from '@/api'
import { useCatalogStore } from '@/stores/catalog'
import { useAuthStore } from '@/stores/auth'
import { formatMoney } from '@/utils/money'
import { formatDate } from '@/utils/date'
import { formatPhone, normalizeIraqiPhone } from '@/utils/phone'
import { nameRule, optionalNameRule, cleanName } from '@/utils/names'
import { apiErrorMessage, apiErrorCode, apiErrorFields } from '@/utils/errors'
import { copyText } from '@/utils/clipboard'
import { toast, toastSuccess } from '@/composables/useToast'

const { xs } = useDisplay()
const catalog = useCatalogStore()
const auth = useAuthStore()
catalog.loadBranches().catch(() => {})

const s = ref(null)
const loading = ref(true)
const error = ref('')
const direct = ref([])
const directTotal = ref(0)
const directPage = ref(0)
const directLoading = ref(false)

const unlockTarget = computed(() => (s.value ? s.value.completedSalesCount + s.value.salesToUnlockAccountCreation : 0))
const unlockPercent = computed(() => (unlockTarget.value ? Math.round((s.value.completedSalesCount / unlockTarget.value) * 100) : 0))
const shareText = computed(() => `تسوّق من قلعة الضمان نقداً أو بالأقساط اليومية لأصحاب المحلات. سجّل عبر رابطي:`)

async function load() {
  loading.value = true
  error.value = ''
  try {
    s.value = await networkApi.summary()
    loadDirect(true)
  } catch (e) {
    error.value = apiErrorMessage(e)
  } finally {
    loading.value = false
  }
}
async function loadDirect(reset) {
  if (reset) {
    directPage.value = 0
    direct.value = []
  }
  directLoading.value = true
  try {
    const res = await networkApi.direct(directPage.value + 1)
    directPage.value = res.page
    directTotal.value = res.total
    direct.value = [...direct.value, ...res.items]
  } catch { /* ignore */ } finally {
    directLoading.value = false
  }
}
async function copy(text, msg) {
  if (await copyText(text)) toast(msg, 'success')
}

// ---- create account for another person ----
const createDialog = ref(false)
const createStep = ref('form')
const createForm = ref()
const cf = reactive({ phone: '', firstName: '', fatherName: '', grandfatherName: '', surname: '', cityValue: null })
const cfErrors = reactive({})
const createOtp = reactive({ requestId: null, expiresInSeconds: 300, resendAfterSeconds: 60, sentAt: 0 })
const createCode = ref('')
const createCodeError = ref('')
const createLoading = ref(false)
const createError = ref('')
const createdName = ref('')

function openCreate() {
  Object.assign(cf, { phone: '', firstName: '', fatherName: '', grandfatherName: '', surname: '', cityValue: auth.user?.cityValue ?? null })
  Object.keys(cfErrors).forEach((k) => delete cfErrors[k])
  createStep.value = 'form'
  createError.value = ''
  createCode.value = ''
  createDialog.value = true
}
async function requestCreateOtp() {
  createError.value = ''
  Object.keys(cfErrors).forEach((k) => delete cfErrors[k])
  if (createStep.value === 'form') {
    const { valid } = await createForm.value.validate()
    if (!valid) return
  }
  createLoading.value = true
  try {
    const res = await networkApi.requestAccountOtp({
      phone: normalizeIraqiPhone(cf.phone),
      firstName: cleanName(cf.firstName),
      fatherName: cleanName(cf.fatherName),
      grandfatherName: cleanName(cf.grandfatherName),
      surname: cleanName(cf.surname) || null,
      cityValue: cf.cityValue,
    })
    Object.assign(createOtp, res, { sentAt: Date.now() })
    createCode.value = ''
    createCodeError.value = ''
    createStep.value = 'otp'
  } catch (e) {
    const code = apiErrorCode(e)
    if (code === 'phone_already_registered' || code === 'invalid_phone') {
      cfErrors.phone = apiErrorMessage(e)
      createStep.value = 'form'
    } else if (code === 'validation') {
      Object.assign(cfErrors, apiErrorFields(e))
      createStep.value = 'form'
    } else createError.value = apiErrorMessage(e)
  } finally {
    createLoading.value = false
  }
}
async function verifyCreate() {
  if (createCode.value.length !== 6 || createLoading.value) return
  createLoading.value = true
  createCodeError.value = ''
  createError.value = ''
  try {
    const res = await networkApi.verifyAccount({ requestId: createOtp.requestId, phone: normalizeIraqiPhone(cf.phone), code: createCode.value })
    createdName.value = res.firstName || cf.firstName
    createStep.value = 'done'
    toastSuccess('تم إنشاء الحساب')
    load()
  } catch (e) {
    const code = apiErrorCode(e)
    if (code?.startsWith('otp_')) {
      const left = e.response?.data?.attemptsLeft
      createCodeError.value = `${apiErrorMessage(e)}${left !== undefined ? ` — المحاولات المتبقية: ${left}` : ''}`
    } else createError.value = apiErrorMessage(e)
  } finally {
    createLoading.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.code { font-size: 1.8rem; font-weight: 800; letter-spacing: 3px; }
.link-box { background: rgba(255, 255, 255, 0.15); border-radius: 12px; padding: 8px 8px 8px 12px; }
.share-light :deep(.v-btn) { background: rgba(255, 255, 255, 0.92) !important; }
</style>
