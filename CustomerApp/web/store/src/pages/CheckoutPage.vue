<template>
  <div class="qa-container py-4 pb-12 checkout">
    <h1 class="text-h5 font-weight-bold mb-4">إتمام الطلب</h1>

    <EmptyState v-if="cart.isEmpty" icon="mdi-cart-outline" title="سلتك فارغة" text="أضف منتجات إلى السلة أولاً.">
      <v-btn color="primary" :to="{ name: 'catalog' }">تصفّح المنتجات</v-btn>
    </EmptyState>

    <v-row v-else>
      <v-col cols="12" md="8">
        <!-- Stepper header -->
        <div class="steps hide-scrollbar mb-4">
          <button
            v-for="(s, i) in steps"
            :key="s.key"
            type="button"
            class="step"
            :class="{ active: i === stepIndex, done: i < stepIndex }"
            :disabled="i > stepIndex"
            @click="i < stepIndex && (stepIndex = i)"
          >
            <span class="dot">
              <v-icon v-if="i < stepIndex" size="16">mdi-check</v-icon><template v-else>{{ i + 1 }}</template>
            </span>
            <span class="label">{{ s.title }}</span>
          </button>
        </div>

        <v-card class="soft-card" rounded="xl">
          <v-card-text class="pa-4 pa-md-6">
            <!-- 1. Purchase type -->
            <v-form v-show="step === 'type'" ref="formType" @submit.prevent="next">
              <div class="text-h6 font-weight-bold mb-3">طريقة الشراء</div>
              <v-alert type="warning" variant="tonal" icon="mdi-store-alert-outline" class="mb-4 notice" border="start">
                <div class="text-subtitle-1 font-weight-bold">{{ INSTALLMENTS_NOTICE }}</div>
                <div class="text-body-2 mt-1">إذا لم يكن لديك محل تجاري، يرجى اختيار الشراء نقداً. سيتحقق المندوب من وجود المحل عند الكشف الميداني.</div>
              </v-alert>
              <div class="choice-grid">
                <button type="button" class="choice" :class="{ selected: form.purchaseType === 'Cash' }" :disabled="!cart.allowCash" @click="form.purchaseType = 'Cash'">
                  <v-icon size="32" color="primary">mdi-cash</v-icon>
                  <div class="font-weight-bold mt-1">نقداً</div>
                  <div class="text-body-2 text-medium-emphasis num">{{ cart.allowCash ? formatMoney(cart.cashTotal) : 'غير متاح لبعض منتجات السلة' }}</div>
                </button>
                <button type="button" class="choice" :class="{ selected: form.purchaseType === 'DailyInstallments' }" :disabled="!cart.allowInstallments" @click="form.purchaseType = 'DailyInstallments'">
                  <v-icon size="32" color="primary">mdi-calendar-clock</v-icon>
                  <div class="font-weight-bold mt-1">أقساط يومية <span class="text-caption">(للمحلات فقط)</span></div>
                  <div class="text-body-2 text-medium-emphasis num">
                    {{ cart.allowInstallments ? `${formatMoney(cart.dailyTotal)} يومياً تقريباً` : 'غير متاح لبعض منتجات السلة' }}
                  </div>
                </button>
              </div>
              <div v-if="errors.purchaseType" class="text-error text-body-2 mt-2">{{ errors.purchaseType }}</div>

              <div class="text-h6 font-weight-bold mt-6 mb-3">الطلب لمن؟</div>
              <div class="choice-grid">
                <button type="button" class="choice" :class="{ selected: form.forSelf }" @click="setForSelf(true)">
                  <v-icon size="28" color="primary">mdi-account</v-icon>
                  <div class="font-weight-bold mt-1">لنفسي</div>
                </button>
                <button type="button" class="choice" :class="{ selected: !form.forSelf }" @click="setForSelf(false)">
                  <v-icon size="28" color="primary">mdi-account-arrow-right</v-icon>
                  <div class="font-weight-bold mt-1">لشخص آخر</div>
                  <div class="text-caption text-medium-emphasis">صديق أو قريب أو زبون</div>
                </button>
              </div>
            </v-form>

            <!-- 2. Buyer -->
            <v-form v-show="step === 'buyer'" ref="formBuyer" @submit.prevent="next">
              <div class="text-h6 font-weight-bold mb-1">{{ form.forSelf ? 'بياناتك' : 'بيانات المشتري' }}</div>
              <p class="text-body-2 text-medium-emphasis mb-4">الاسم الثلاثي كما في الهوية، كل حقل كلمة واحدة.</p>
              <v-row dense>
                <v-col cols="12" sm="6"><v-text-field v-model="form.firstName" label="الاسم الأول *" :rules="[nameRule('الاسم الأول')]" :error-messages="errors.firstName" /></v-col>
                <v-col cols="12" sm="6"><v-text-field v-model="form.fatherName" label="اسم الأب *" :rules="[nameRule('اسم الأب')]" :error-messages="errors.fatherName" /></v-col>
                <v-col cols="12" sm="6"><v-text-field v-model="form.grandfatherName" label="اسم الجد *" :rules="[nameRule('اسم الجد')]" :error-messages="errors.grandfatherName" /></v-col>
                <v-col cols="12" sm="6"><v-text-field v-model="form.surname" label="اللقب (اختياري)" :rules="[optionalNameRule]" :error-messages="errors.surname" /></v-col>
                <v-col cols="12" sm="6">
                  <PhoneField v-model="form.phone" :label="form.forSelf ? 'رقم الهاتف *' : 'رقم هاتف المشتري *'" :error-messages="errors.phone"
                    :hint="form.forSelf ? 'افتراضياً رقم حسابك، يمكنك تغييره' : 'سيتصل المندوب بهذا الرقم'" />
                </v-col>
                <v-col cols="12" sm="6">
                  <PhoneField v-model="form.secondaryPhone" label="رقم هاتف ثانٍ (اختياري)" :required="false" :error-messages="errors.secondaryPhone" hint="رقم بديل للتواصل" />
                </v-col>
              </v-row>
            </v-form>

            <!-- 3. Address -->
            <v-form v-show="step === 'address'" ref="formAddress" @submit.prevent="next">
              <div class="text-h6 font-weight-bold mb-3">الفرع والعنوان</div>
              <v-select
                v-model="form.cityValue"
                :items="branchOptions"
                item-title="name"
                item-value="cityValue"
                label="الفرع / المحافظة *"
                prepend-inner-icon="mdi-map-marker-outline"
                :rules="[(v) => !!v || 'اختر الفرع']"
                :error-messages="errors.cityValue"
                :loading="branchesLoading"
                :hint="branchHint"
                persistent-hint
                no-data-text="لا يوجد فرع تتوفر فيه جميع منتجات السلة"
                class="mb-3"
              />
              <v-alert v-if="!branchesLoading && !branchOptions.length" type="error" variant="tonal" density="compact" class="mb-3">
                لا يوجد فرع واحد تتوفر فيه جميع منتجات السلة. يرجى حذف بعض المنتجات أو تقسيم الطلب.
              </v-alert>
              <v-row dense>
                <v-col cols="12" sm="6"><v-text-field v-model="form.area" label="المنطقة / الحي *" :rules="[requiredRule('المنطقة')]" :error-messages="errors.area" placeholder="مثال: حي الأنصار" /></v-col>
                <v-col cols="12" sm="6"><v-text-field v-model="form.nearestLandmark" label="أقرب نقطة دالة" placeholder="مثال: قرب مدرسة الرافدين" :error-messages="errors.nearestLandmark" /></v-col>
                <v-col cols="12"><v-textarea v-model="form.details" label="تفاصيل العنوان *" rows="2" auto-grow :rules="[requiredRule('تفاصيل العنوان')]" :error-messages="errors.details" placeholder="الشارع، الزقاق، رقم الدار..." /></v-col>
              </v-row>
              <div class="geo d-flex align-center flex-wrap ga-3">
                <v-btn variant="tonal" color="primary" prepend-icon="mdi-crosshairs-gps" :loading="geoLoading" @click="locate">
                  {{ hasCoords ? 'تحديث موقعي' : 'إرفاق موقعي الحالي (اختياري)' }}
                </v-btn>
                <template v-if="hasCoords">
                  <a :href="mapLink" target="_blank" rel="noopener" class="text-body-2 ltr num">{{ form.latitude.toFixed(5) }}, {{ form.longitude.toFixed(5) }}</a>
                  <v-btn size="small" variant="text" color="error" @click="clearCoords">إزالة</v-btn>
                </template>
                <span v-if="geoError" class="text-error text-body-2">{{ geoError }}</span>
              </div>
              <p class="text-caption text-medium-emphasis mt-2">يساعد الموقع المندوب في الوصول إليك بسرعة. لن نستخدمه لأي غرض آخر.</p>
            </v-form>

            <!-- 4. Shop + notes -->
            <v-form v-show="step === 'shop'" ref="formShop" @submit.prevent="next">
              <template v-if="isInstallments">
                <div class="text-h6 font-weight-bold mb-1">معلومات المحل التجاري</div>
                <v-alert type="warning" variant="tonal" density="compact" class="mb-4" icon="mdi-store-alert-outline">{{ INSTALLMENTS_NOTICE }}</v-alert>
                <v-row dense>
                  <v-col cols="12" sm="6"><v-text-field v-model="form.shopName" label="اسم المحل *" :rules="[requiredRule('اسم المحل')]" :error-messages="errors.shopName" /></v-col>
                  <v-col cols="12" sm="6">
                    <v-combobox v-model="form.shopBusinessType" :items="businessTypes" label="نوع النشاط *" :rules="[requiredRule('نوع النشاط')]" :error-messages="errors.shopBusinessType" variant="outlined" density="comfortable" />
                  </v-col>
                  <v-col cols="12"><v-text-field v-model="form.shopAddress" label="عنوان المحل (إذا كان مختلفاً)" :error-messages="errors.shopAddress" /></v-col>
                  <v-col cols="12">
                    <v-switch v-model="form.ownsShop" color="primary" inset hide-details :label="form.ownsShop ? 'المحل مُلك للمشتري' : 'المحل مستأجر'" />
                  </v-col>
                </v-row>
                <v-divider class="my-4" />
              </template>
              <div class="text-h6 font-weight-bold mb-3">ملاحظات</div>
              <v-textarea v-model="form.notes" label="ملاحظات للمندوب (اختياري)" rows="3" auto-grow counter="500" maxlength="500" placeholder="مثال: اتصلوا بعد الساعة 4 عصراً" />
            </v-form>

            <!-- 5. Review -->
            <div v-show="step === 'review'">
              <div class="text-h6 font-weight-bold mb-3">مراجعة الطلب</div>
              <div class="review-grid">
                <ReviewBlock title="طريقة الشراء" @edit="goStep('type')">
                  <b>{{ PURCHASE_TYPES[form.purchaseType] }}</b> — {{ form.forSelf ? 'لنفسي' : 'لشخص آخر' }}
                </ReviewBlock>
                <ReviewBlock title="المشتري" @edit="goStep('buyer')">
                  {{ fullName }}<br />
                  <span class="ltr num">{{ formatPhone(payload.customer.phone) }}</span>
                  <span v-if="payload.customer.secondaryPhone" class="ltr num"> / {{ formatPhone(payload.customer.secondaryPhone) }}</span>
                </ReviewBlock>
                <ReviewBlock title="العنوان" @edit="goStep('address')">
                  {{ catalog.branchName(form.cityValue) }} — {{ form.area }}<br />{{ form.details }}
                  <template v-if="form.nearestLandmark"><br />قرب: {{ form.nearestLandmark }}</template>
                  <template v-if="hasCoords"><br /><v-icon size="14">mdi-map-marker-check</v-icon> تم إرفاق الموقع</template>
                </ReviewBlock>
                <ReviewBlock v-if="isInstallments" title="المحل" @edit="goStep('shop')">
                  {{ form.shopName }} — {{ form.shopBusinessType }}<br />
                  {{ form.ownsShop ? 'مُلك' : 'مستأجر' }}<template v-if="form.shopAddress"> — {{ form.shopAddress }}</template>
                </ReviewBlock>
                <ReviewBlock v-if="form.notes" title="ملاحظات" @edit="goStep('shop')">{{ form.notes }}</ReviewBlock>
              </div>
              <v-alert v-if="Object.keys(reviewErrors).length" type="error" variant="tonal" density="compact" class="mt-4">
                <div v-for="(m, k) in reviewErrors" :key="k">• {{ m }}</div>
              </v-alert>
              <v-alert v-if="submitError" type="error" variant="tonal" density="compact" class="mt-4">{{ submitError }}</v-alert>
              <p class="text-caption text-medium-emphasis mt-4">
                بإرسال الطلب، سيقوم فريق قلعة الضمان بمراجعته ثم يتواصل معك مندوب المبيعات. الأسعار المعروضة تقديرية.
              </p>
            </div>
          </v-card-text>

          <v-divider />
          <v-card-actions class="pa-4 d-flex ga-2">
            <v-btn v-if="stepIndex > 0" variant="text" prepend-icon="mdi-arrow-right" @click="back">رجوع</v-btn>
            <v-spacer />
            <v-btn v-if="step !== 'review'" color="primary" variant="flat" size="large" append-icon="mdi-arrow-left" @click="next">التالي</v-btn>
            <v-btn v-else color="primary" variant="flat" size="large" prepend-icon="mdi-send" :loading="submitting" @click="submit">تأكيد وإرسال الطلب</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>

      <!-- Order summary -->
      <v-col cols="12" md="4">
        <v-card class="soft-card summary" rounded="xl">
          <v-card-title class="font-weight-bold pt-4">المنتجات ({{ cart.count }})</v-card-title>
          <v-list density="compact" class="py-0">
            <v-list-item v-for="l in cart.lines" :key="l.productId" class="py-2">
              <template #prepend><v-avatar rounded="lg" size="48" class="me-3"><v-img :src="l.product?.thumbnailUrl" cover /></v-avatar></template>
              <v-list-item-title class="font-weight-bold text-body-2" style="white-space: normal">{{ l.product?.name }}</v-list-item-title>
              <v-list-item-subtitle class="num">
                ×{{ l.quantity }} —
                <template v-if="isInstallments">{{ formatMoney(l.product?.dailyInstallment * l.quantity) }} يومياً</template>
                <template v-else>{{ formatMoney(l.product?.cashPrice * l.quantity) }}</template>
              </v-list-item-subtitle>
            </v-list-item>
          </v-list>
          <v-divider />
          <v-card-text>
            <template v-if="isInstallments">
              <div class="row"><span>القسط اليومي التقريبي</span><b class="num">{{ formatMoney(cart.dailyTotal) }}</b></div>
              <div class="row"><span>الإجمالي بالأقساط</span><b class="num text-primary">{{ formatMoney(cart.installmentTotal) }}</b></div>
            </template>
            <div v-else class="row"><span>الإجمالي نقداً</span><b class="num text-primary">{{ formatMoney(cart.cashTotal) }}</b></div>
            <v-btn variant="text" size="small" color="primary" class="px-0 mt-1" :to="{ name: 'cart' }">تعديل السلة</v-btn>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- duplicate open order -->
    <v-dialog v-model="dupDialog" max-width="440">
      <v-card rounded="xl" class="pa-2">
        <v-card-title class="font-weight-bold"><v-icon color="warning" class="me-1">mdi-alert</v-icon> يوجد طلب مفتوح</v-card-title>
        <v-card-text class="text-body-1" style="line-height: 1.9">
          {{ dupMessage }}
          <div v-if="dupOrderNumber" class="mt-2">رقم الطلب: <b class="ltr num">{{ dupOrderNumber }}</b></div>
          <div class="text-body-2 text-medium-emphasis mt-2">لا يمكن تقديم طلب جديد لنفس المنتج ولنفس رقم الهاتف حتى تتم معالجة الطلب الحالي.</div>
        </v-card-text>
        <v-card-actions>
          <v-btn variant="text" @click="dupDialog = false">إغلاق</v-btn>
          <v-spacer />
          <v-btn color="primary" variant="flat" :to="{ name: 'orders' }">عرض طلباتي</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import EmptyState from '@/components/EmptyState.vue'
import PhoneField from '@/components/PhoneField.vue'
import ReviewBlock from '@/components/ReviewBlock.vue'
import { useCartStore } from '@/stores/cart'
import { useAuthStore } from '@/stores/auth'
import { useCatalogStore } from '@/stores/catalog'
import { ordersApi } from '@/api'
import { emptyCheckoutForm, buildOrderPayload, validateOrderPayload, commonBranches } from '@/utils/orderPayload'
import { nameRule, optionalNameRule, requiredRule } from '@/utils/names'
import { formatMoney } from '@/utils/money'
import { formatPhone } from '@/utils/phone'
import { PURCHASE_TYPES, INSTALLMENTS_NOTICE } from '@/utils/enums'
import { apiErrorMessage, apiErrorCode, apiErrorFields } from '@/utils/errors'
import { uuid } from '@/utils/storage'
import { toastSuccess } from '@/composables/useToast'

const cart = useCartStore()
const auth = useAuthStore()
const catalog = useCatalogStore()
const router = useRouter()

const form = reactive(emptyCheckoutForm())
const errors = reactive({})
const stepIndex = ref(0)
const submitting = ref(false)
const submitError = ref('')
const dupDialog = ref(false)
const dupMessage = ref('')
const dupOrderNumber = ref('')
const branchOptions = ref([])
const branchesLoading = ref(true)
const productDetails = ref({})
const geoLoading = ref(false)
const geoError = ref('')
const formType = ref(), formBuyer = ref(), formAddress = ref(), formShop = ref()
let pendingIdempotencyKey = null

const businessTypes = ['مواد غذائية', 'ملابس', 'مواد إنشائية', 'كهربائيات', 'موبايلات وإكسسوارات', 'مطعم / كافيه', 'صيدلية', 'قرطاسية', 'حلاقة / تجميل', 'خضروات وفواكه', 'أخرى']

const isInstallments = computed(() => form.purchaseType === 'DailyInstallments')
const steps = computed(() => [
  { key: 'type', title: 'طريقة الشراء' },
  { key: 'buyer', title: 'المشتري' },
  { key: 'address', title: 'العنوان' },
  { key: 'shop', title: isInstallments.value ? 'المحل والملاحظات' : 'ملاحظات' },
  { key: 'review', title: 'المراجعة' },
])
const step = computed(() => steps.value[stepIndex.value].key)
const forms = { type: formType, buyer: formBuyer, address: formAddress, shop: formShop }

const hasCoords = computed(() => Number.isFinite(form.latitude) && Number.isFinite(form.longitude))
const mapLink = computed(() => `https://www.google.com/maps?q=${form.latitude},${form.longitude}`)
const payload = computed(() => buildOrderPayload(form, cart.lines, { accountPhone: auth.user?.phone }))
const fullName = computed(() => [payload.value.customer.firstName, payload.value.customer.fatherName, payload.value.customer.grandfatherName, payload.value.customer.surname].filter(Boolean).join(' '))
const productsCtx = computed(() =>
  Object.fromEntries(
    Object.values(productDetails.value).map((p) => [p.id, { allowCash: p.allowCash, allowInstallments: p.allowInstallments, availableCityValues: p.availableBranches?.map((b) => b.cityValue) }]),
  ),
)
const reviewErrors = computed(() => validateOrderPayload(payload.value, { products: productsCtx.value }))
const branchHint = computed(() => (branchOptions.value.length < catalog.branches.length ? 'تظهر فقط الفروع التي تتوفر فيها جميع منتجات السلة' : ''))

function prefillSelf() {
  const u = auth.user || {}
  Object.assign(form, {
    firstName: u.firstName || '', fatherName: u.fatherName || '', grandfatherName: u.grandfatherName || '',
    surname: u.surname || '', phone: u.phone || '',
  })
  if (!form.cityValue && u.cityValue && branchOptions.value.some((b) => b.cityValue === u.cityValue)) form.cityValue = u.cityValue
}
function setForSelf(v) {
  if (form.forSelf === v) return
  form.forSelf = v
  if (v) prefillSelf()
  else Object.assign(form, { firstName: '', fatherName: '', grandfatherName: '', surname: '', phone: '', secondaryPhone: '' })
}

async function loadBranches() {
  branchesLoading.value = true
  try {
    const details = await Promise.all(cart.lines.map((l) => catalog.getProduct(l.productId)))
    productDetails.value = Object.fromEntries(details.map((d) => [d.id, d]))
    branchOptions.value = commonBranches(details.map((d) => d.availableBranches || []))
    if (form.cityValue && !branchOptions.value.some((b) => b.cityValue === form.cityValue)) form.cityValue = null
    if (!form.cityValue && auth.user?.cityValue && branchOptions.value.some((b) => b.cityValue === auth.user.cityValue)) form.cityValue = auth.user.cityValue
  } catch (e) {
    submitError.value = apiErrorMessage(e)
  } finally {
    branchesLoading.value = false
  }
}

function clearErrors() {
  Object.keys(errors).forEach((k) => delete errors[k])
  submitError.value = ''
}

async function validateStep(key) {
  clearErrors()
  if (key === 'type') {
    if (!form.purchaseType) {
      errors.purchaseType = 'اختر طريقة الشراء'
      return false
    }
    return true
  }
  const f = forms[key]?.value
  if (!f) return true
  const { valid } = await f.validate()
  return valid
}
async function next() {
  if (!(await validateStep(step.value))) return
  stepIndex.value = Math.min(steps.value.length - 1, stepIndex.value + 1)
  window.scrollTo({ top: 0, behavior: 'smooth' })
}
function back() {
  stepIndex.value = Math.max(0, stepIndex.value - 1)
}
function goStep(key) {
  stepIndex.value = steps.value.findIndex((s) => s.key === key)
}

const FIELD_STEP = {
  purchaseType: 'type', firstName: 'buyer', fatherName: 'buyer', grandfatherName: 'buyer', surname: 'buyer', phone: 'buyer', secondaryPhone: 'buyer',
  cityValue: 'address', area: 'address', details: 'address', nearestLandmark: 'address',
  shopName: 'shop', shopBusinessType: 'shop', shopAddress: 'shop', shop: 'shop', notes: 'shop',
}
function normaliseFieldKey(k) {
  // server may send nested keys like "customer.firstName" / "shop.name"
  const last = k.split('.').pop()
  if (k.startsWith('shop.')) return { name: 'shopName', businessType: 'shopBusinessType', address: 'shopAddress' }[last] || 'shop'
  if (k.startsWith('address.')) return last
  return last
}

async function submit() {
  clearErrors()
  const errs = reviewErrors.value
  if (Object.keys(errs).length) {
    Object.assign(errors, errs)
    const first = Object.keys(errs).map((k) => FIELD_STEP[k]).find(Boolean)
    if (first) goStep(first)
    return
  }
  submitting.value = true
  // One key per submit attempt; reused only if the previous attempt got no response (network failure).
  const key = pendingIdempotencyKey || uuid()
  pendingIdempotencyKey = key
  try {
    const order = await ordersApi.create(payload.value, key)
    pendingIdempotencyKey = null
    cart.clear()
    toastSuccess('تم إرسال طلبك بنجاح')
    router.replace({ name: 'order-success', params: { id: order.id }, query: { n: order.orderNumber } })
  } catch (e) {
    if (e.response) pendingIdempotencyKey = null
    const code = apiErrorCode(e)
    if (code === 'duplicate_open_order') {
      dupMessage.value = apiErrorMessage(e)
      dupOrderNumber.value = e.response?.data?.orderNumber || ''
      dupDialog.value = true
    } else if (code === 'validation') {
      const fields = apiErrorFields(e)
      for (const [k, v] of Object.entries(fields)) errors[normaliseFieldKey(k)] = v
      submitError.value = apiErrorMessage(e)
      const first = Object.keys(fields).map((k) => FIELD_STEP[normaliseFieldKey(k)]).find(Boolean)
      if (first) goStep(first)
    } else {
      submitError.value = apiErrorMessage(e)
    }
  } finally {
    submitting.value = false
  }
}

function clearCoords() {
  form.latitude = null
  form.longitude = null
}
function locate() {
  geoError.value = ''
  if (!navigator.geolocation) {
    geoError.value = 'المتصفح لا يدعم تحديد الموقع'
    return
  }
  geoLoading.value = true
  navigator.geolocation.getCurrentPosition(
    (pos) => {
      form.latitude = Math.round(pos.coords.latitude * 1e6) / 1e6
      form.longitude = Math.round(pos.coords.longitude * 1e6) / 1e6
      geoLoading.value = false
    },
    (err) => {
      geoLoading.value = false
      geoError.value = err.code === 1 ? 'تم رفض إذن الموقع' : 'تعذّر تحديد الموقع'
    },
    { enableHighAccuracy: true, timeout: 15000, maximumAge: 60000 },
  )
}

// purchase type must be allowed by every product in the cart
watch(
  () => [cart.allowCash, cart.allowInstallments],
  ([c, i]) => {
    if (form.purchaseType === 'Cash' && !c) form.purchaseType = null
    if (form.purchaseType === 'DailyInstallments' && !i) form.purchaseType = null
    if (!form.purchaseType && c !== i) form.purchaseType = c ? 'Cash' : 'DailyInstallments'
  },
  { immediate: true },
)

onMounted(async () => {
  prefillSelf()
  if (!cart.isEmpty) await loadBranches()
})
</script>

<style scoped>
.steps { display: flex; gap: 6px; overflow-x: auto; }
.step {
  flex: 1 0 auto; display: flex; align-items: center; gap: 8px; padding: 8px 12px; border-radius: 999px;
  background: #fff; border: 1px solid rgba(0, 0, 0, 0.08); color: #667; font-weight: 600; font-size: 0.85rem; white-space: nowrap;
}
.step .dot {
  width: 24px; height: 24px; border-radius: 50%; display: inline-flex; align-items: center; justify-content: center;
  background: #e6eeed; color: #4a5e5c; font-size: 0.8rem;
}
.step.active { border-color: #006a64; color: #006a64; }
.step.active .dot, .step.done .dot { background: #006a64; color: #fff; }
.step.done { cursor: pointer; color: #1d2b2a; }
.notice { border-width: 2px; }
.choice-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
.choice {
  text-align: start; padding: 16px; border-radius: 16px; border: 2px solid rgba(0, 0, 0, 0.1); background: #fff; cursor: pointer;
  transition: border-color 0.15s, background 0.15s; font-family: inherit;
}
.choice:hover:not(:disabled) { border-color: #2e9c8f; }
.choice.selected { border-color: #006a64; background: #e8f4f3; }
.choice:disabled { opacity: 0.5; cursor: not-allowed; }
.review-grid { display: grid; gap: 12px; }
.row { display: flex; justify-content: space-between; padding: 4px 0; }
@media (min-width: 960px) { .summary { position: sticky; top: 92px; } }
</style>
