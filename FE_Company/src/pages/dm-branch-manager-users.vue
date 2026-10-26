<script setup>
import { onMounted, ref } from 'vue'
import {
  dmBmUserCreate,
  dmBmUserDelete,
  dmBmUsersCities,
  dmBmUsersList,
  dmBmUserUpdate,
} from '@/composables/delegatedManagerApi'
import { useToast } from '@/composables/useToast'

defineOptions({ name: 'DmBranchManagerUsers' })
definePage({ meta: { layout: 'dm' } })

const toast = useToast()
const busy = ref(false)
const users = ref([])
const cities = ref([])
const editorOpen = ref(false)
const saving = ref(false)
const editing = ref(null)
const form = ref({
  userName: '',
  password: '',
  displayName: '',
  isActive: true,
  cityValues: [],
})

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] != null && obj[k] !== '')
      return obj[k]
  }
  return undefined
}

function cityValueOf(c) {
  return String(pick(c, 'value', 'Value', 'cityValue', 'CityValue', 'database', 'Database') || '').trim()
}

function cityNameOf(c) {
  return String(pick(c, 'name', 'Name', 'cityName', 'CityName') || cityValueOf(c))
}

function userCityNames(u) {
  const list = pick(u, 'cities', 'Cities') || []
  if (!Array.isArray(list)) return ''
  return list
    .map(c => pick(c, 'cityName', 'CityName', 'cityValue', 'CityValue') || '')
    .filter(Boolean)
    .join(' · ')
}

async function load() {
  busy.value = true
  try {
    const [c, u] = await Promise.all([dmBmUsersCities(), dmBmUsersList()])
    cities.value = Array.isArray(c) ? c : []
    users.value = Array.isArray(u) ? u : []
  }
  catch (e) {
    users.value = []
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل الحسابات')
  }
  finally {
    busy.value = false
  }
}

function openCreate() {
  editing.value = null
  form.value = {
    userName: '',
    password: '',
    displayName: '',
    isActive: true,
    cityValues: [],
  }
  editorOpen.value = true
}

function openEdit(u) {
  editing.value = u
  const cityList = pick(u, 'cities', 'Cities') || []
  form.value = {
    userName: String(pick(u, 'userName', 'UserName') || ''),
    password: '',
    displayName: String(pick(u, 'displayName', 'DisplayName') || ''),
    isActive: pick(u, 'isActive', 'IsActive') !== false,
    cityValues: (Array.isArray(cityList) ? cityList : [])
      .map(c => cityValueOf(c) || String(pick(c, 'cityValue', 'CityValue') || ''))
      .filter(Boolean),
  }
  editorOpen.value = true
}

async function save() {
  const f = form.value
  if (!String(f.displayName || '').trim()) {
    toast.error('أدخل الاسم الظاهر')
    return
  }
  if (String(f.userName || '').trim().length < 3) {
    toast.error('اسم المستخدم 3 أحرف على الأقل')
    return
  }
  if (!editing.value && String(f.password || '').length < 4) {
    toast.error('كلمة المرور 4 أحرف على الأقل')
    return
  }
  if (!f.cityValues?.length) {
    toast.error('اختر محافظة واحدةً على الأقل')
    return
  }
  saving.value = true
  try {
    if (!editing.value) {
      await dmBmUserCreate({
        userName: f.userName.trim(),
        password: f.password,
        displayName: f.displayName.trim(),
        cityValues: f.cityValues,
      })
      toast.success('تم إنشاء مستخدم تطبيق إدارة الفرع')
    }
    else {
      const id = pick(editing.value, 'id', 'Id')
      const body = {
        userName: f.userName.trim(),
        displayName: f.displayName.trim(),
        isActive: f.isActive,
        cityValues: f.cityValues,
      }
      if (f.password)
        body.password = f.password
      await dmBmUserUpdate(id, body)
      toast.success('تم حفظ التعديلات')
    }
    editorOpen.value = false
    await load()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر الحفظ')
  }
  finally {
    saving.value = false
  }
}

async function removeUser(u) {
  const id = pick(u, 'id', 'Id')
  if (!id) return
  if (!confirm(`حذف «${pick(u, 'displayName', 'DisplayName', 'userName', 'UserName')}»؟`))
    return
  try {
    await dmBmUserDelete(id)
    toast.success('تم الحذف')
    await load()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر الحذف')
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-4">
      <h1 class="text-h5 mb-0">
        حسابات إدارة الفرع
      </h1>
      <div class="d-flex gap-2">
        <VBtn
          variant="tonal"
          :loading="busy"
          @click="load"
        >
          تحديث
        </VBtn>
        <VBtn
          color="primary"
          @click="openCreate"
        >
          إنشاء مستخدم
        </VBtn>
      </div>
    </div>

    <VAlert
      type="info"
      variant="tonal"
      class="mb-4"
    >
      من هنا فقط تُنشأ أسماء الدخول وكلمات المرور والمحافظات المسموحة لتطبيق إدارة الفرع.
    </VAlert>

    <VProgressLinear
      v-if="busy"
      indeterminate
      class="mb-3"
    />
    <VAlert
      v-if="!busy && !users.length"
      type="info"
      variant="tonal"
    >
      لا يوجد مستخدمون بعد — أنشئ أول حساب لمدير فرع.
    </VAlert>

    <VCard
      v-for="u in users"
      :key="pick(u, 'id', 'Id')"
      variant="outlined"
      class="mb-2"
    >
      <VCardText class="d-flex justify-space-between gap-3 flex-wrap">
        <div>
          <strong :class="{ 'text-medium-emphasis': pick(u, 'isActive', 'IsActive') === false }">
            {{ pick(u, 'displayName', 'DisplayName') || pick(u, 'userName', 'UserName') }}
          </strong>
          <div class="text-caption text-medium-emphasis mt-1">
            اسم الدخول: {{ pick(u, 'userName', 'UserName') }}
            <template v-if="pick(u, 'isActive', 'IsActive') === false">
              · الحساب موقوف
            </template>
          </div>
          <div
            v-if="userCityNames(u)"
            class="text-caption mt-1"
          >
            المحافظات: {{ userCityNames(u) }}
          </div>
        </div>
        <div class="d-flex gap-1 align-start">
          <VBtn
            size="small"
            variant="tonal"
            @click="openEdit(u)"
          >
            تعديل
          </VBtn>
          <VBtn
            size="small"
            color="error"
            variant="text"
            @click="removeUser(u)"
          >
            حذف
          </VBtn>
        </div>
      </VCardText>
    </VCard>

    <VDialog
      v-model="editorOpen"
      max-width="560"
      persistent
    >
      <VCard>
        <VCardTitle>
          {{ editing ? 'تعديل مستخدم إدارة الفرع' : 'إنشاء مستخدم إدارة الفرع' }}
        </VCardTitle>
        <VCardText>
          <VTextField
            v-model="form.displayName"
            label="الاسم الظاهر"
            class="mb-2"
          />
          <VTextField
            v-model="form.userName"
            label="اسم المستخدم *"
            class="mb-2"
          />
          <VTextField
            v-model="form.password"
            :label="editing ? 'كلمة مرور جديدة (اختياري)' : 'كلمة المرور *'"
            type="password"
            class="mb-2"
          />
          <VSwitch
            v-if="editing"
            v-model="form.isActive"
            label="الحساب مفعّل"
            class="mb-3"
          />
          <div class="text-subtitle-2 mb-1">
            المحافظات المسموحة
          </div>
          <div class="dm-cities">
            <VCheckbox
              v-for="c in cities"
              :key="cityValueOf(c)"
              v-model="form.cityValues"
              :label="cityNameOf(c)"
              :value="cityValueOf(c)"
              density="compact"
              hide-details
            />
          </div>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="editorOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="saving"
            @click="save"
          >
            {{ editing ? 'حفظ التعديلات' : 'إنشاء الحساب' }}
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.dm-cities {
  max-block-size: 240px;
  overflow: auto;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 10px;
  padding: 0.5rem 0.75rem;
}
</style>
