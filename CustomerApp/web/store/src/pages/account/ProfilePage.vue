<template>
  <div>
    <h1 class="text-h6 font-weight-bold mb-3">حسابي</h1>

    <v-card class="soft-card mb-4" rounded="xl">
      <v-card-title class="font-weight-bold pt-4">البيانات الشخصية</v-card-title>
      <v-card-text>
        <v-form ref="form" @submit.prevent="save">
          <v-row dense>
            <v-col cols="12" sm="6"><v-text-field v-model="f.firstName" label="الاسم الأول *" :rules="[nameRule('الاسم الأول')]" :error-messages="errors.firstName" /></v-col>
            <v-col cols="12" sm="6"><v-text-field v-model="f.fatherName" label="اسم الأب *" :rules="[nameRule('اسم الأب')]" :error-messages="errors.fatherName" /></v-col>
            <v-col cols="12" sm="6"><v-text-field v-model="f.grandfatherName" label="اسم الجد *" :rules="[nameRule('اسم الجد')]" :error-messages="errors.grandfatherName" /></v-col>
            <v-col cols="12" sm="6"><v-text-field v-model="f.surname" label="اللقب (اختياري)" :rules="[optionalNameRule]" :error-messages="errors.surname" /></v-col>
            <v-col cols="12" sm="6">
              <v-select v-model="f.cityValue" :items="catalog.branches" item-title="name" item-value="cityValue" label="الفرع *" :rules="[(v) => !!v || 'اختر الفرع']" :error-messages="errors.cityValue" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field :model-value="formatPhone(auth.user?.phone)" label="رقم الهاتف" readonly dir="ltr" prepend-inner-icon="mdi-lock-outline" hint="لا يمكن تغيير رقم الهاتف" persistent-hint />
            </v-col>
          </v-row>
          <div class="d-flex flex-wrap ga-3 mt-3 text-body-2 text-medium-emphasis">
            <span v-if="auth.user?.referredBy">دعاك: <b>{{ auth.user.referredBy.firstName }}</b></span>
            <span>عضو منذ: {{ formatDate(auth.user?.createdAt) }}</span>
          </div>
          <v-alert v-if="saveError" type="error" variant="tonal" density="compact" class="mt-3">{{ saveError }}</v-alert>
          <v-btn type="submit" color="primary" class="mt-4" :loading="saving" :disabled="!dirty">حفظ التغييرات</v-btn>
        </v-form>
      </v-card-text>
    </v-card>

    <v-card class="soft-card mb-4" rounded="xl">
      <v-card-title class="font-weight-bold pt-4">الإشعارات التسويقية</v-card-title>
      <v-card-text>
        <v-switch
          :model-value="auth.user?.marketingOptIn"
          color="primary"
          inset
          hide-details
          :loading="prefSaving"
          :disabled="prefSaving"
          label="أرغب باستلام العروض والتخفيضات"
          @update:model-value="togglePref"
        />
        <p class="text-caption text-medium-emphasis mt-2">إشعارات الطلبات والأرباح المهمة تصلك دائماً بغض النظر عن هذا الخيار.</p>
      </v-card-text>
    </v-card>

    <v-card class="soft-card danger" rounded="xl">
      <v-card-title class="font-weight-bold pt-4 text-error">حذف الحساب</v-card-title>
      <v-card-text>
        <p class="text-body-2 mb-3">حذف الحساب نهائي ولا يمكن التراجع عنه.</p>
        <v-btn color="error" variant="outlined" prepend-icon="mdi-delete-forever-outline" @click="openDelete">حذف حسابي</v-btn>
      </v-card-text>
    </v-card>

    <v-dialog v-model="deleteDialog" max-width="480" persistent>
      <v-card rounded="xl">
        <v-card-title class="font-weight-bold pt-4 text-error"><v-icon class="me-1">mdi-alert</v-icon> تأكيد حذف الحساب</v-card-title>
        <v-card-text>
          <v-alert type="error" variant="tonal" class="mb-4">
            <ul class="ps-4" style="line-height: 2">
              <li>ستُحذف بياناتك الشخصية ولن تتمكن من الدخول بهذا الحساب.</li>
              <li><b>سيسقط أي رصيد أرباح غير مصروف<template v-if="balance !== null"> ({{ formatMoney(balance) }})</template> ولن يمكن استرجاعه.</b></li>
              <li>تبقى السجلات المالية وروابط الإحالة بشكل غير معرِّف لأغراض محاسبية.</li>
              <li>لن تتأثر حسابات الأقساط المسجّلة في فروع الشركة.</li>
            </ul>
          </v-alert>
          <v-text-field v-model="confirmText" label='اكتب كلمة «حذف» للتأكيد' autocomplete="off" :error-messages="deleteError" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-btn variant="text" @click="deleteDialog = false">تراجع</v-btn>
          <v-spacer />
          <v-btn color="error" variant="flat" :disabled="confirmText.trim() !== 'حذف'" :loading="deleting" @click="doDelete">حذف الحساب نهائياً</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed } from 'vue'
import { useRouter } from 'vue-router'
import { meApi, earningsApi } from '@/api'
import { useAuthStore } from '@/stores/auth'
import { useCatalogStore } from '@/stores/catalog'
import { nameRule, optionalNameRule, cleanName } from '@/utils/names'
import { formatPhone } from '@/utils/phone'
import { formatDate } from '@/utils/date'
import { formatMoney } from '@/utils/money'
import { apiErrorMessage, apiErrorFields } from '@/utils/errors'
import { toast, toastSuccess } from '@/composables/useToast'

const auth = useAuthStore()
const catalog = useCatalogStore()
const router = useRouter()
catalog.loadBranches().catch(() => {})

const form = ref()
const pick = (u) => ({ firstName: u?.firstName || '', fatherName: u?.fatherName || '', grandfatherName: u?.grandfatherName || '', surname: u?.surname || '', cityValue: u?.cityValue ?? null })
const f = reactive(pick(auth.user))
const errors = reactive({})
const saving = ref(false)
const saveError = ref('')
const prefSaving = ref(false)
const dirty = computed(() => JSON.stringify(pick(f)) !== JSON.stringify(pick(auth.user)))

async function save() {
  const { valid } = await form.value.validate()
  if (!valid) return
  saving.value = true
  saveError.value = ''
  Object.keys(errors).forEach((k) => delete errors[k])
  try {
    const me = await meApi.update({
      firstName: cleanName(f.firstName), fatherName: cleanName(f.fatherName), grandfatherName: cleanName(f.grandfatherName),
      surname: cleanName(f.surname) || null, cityValue: f.cityValue,
    })
    auth.setUser(me)
    Object.assign(f, pick(me))
    toastSuccess('تم حفظ البيانات')
  } catch (e) {
    Object.assign(errors, apiErrorFields(e))
    saveError.value = apiErrorMessage(e)
  } finally {
    saving.value = false
  }
}
async function togglePref(v) {
  prefSaving.value = true
  try {
    auth.setUser(await meApi.preferences(!!v))
    toast(v ? 'سنرسل لك العروض الجديدة' : 'تم إيقاف الإشعارات التسويقية', 'success')
  } catch (e) {
    toast(apiErrorMessage(e), 'error')
  } finally {
    prefSaving.value = false
  }
}

const deleteDialog = ref(false)
const confirmText = ref('')
const deleting = ref(false)
const deleteError = ref('')
const balance = ref(null)
async function openDelete() {
  confirmText.value = ''
  deleteError.value = ''
  deleteDialog.value = true
  try {
    const s = await earningsApi.summary()
    balance.value = (s.available || 0) + (s.pending || 0)
  } catch { balance.value = null }
}
async function doDelete() {
  deleting.value = true
  deleteError.value = ''
  try {
    await meApi.deleteAccount()
    deleteDialog.value = false
    auth.clearSession()
    toast('تم حذف حسابك', 'info')
    router.replace({ name: 'home' })
  } catch (e) {
    deleteError.value = apiErrorMessage(e)
  } finally {
    deleting.value = false
  }
}
</script>

<style scoped>
.danger { border-color: rgba(198, 40, 40, 0.25) !important; }
</style>
