<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { dmLogin, dmSaveSession } from '@/composables/delegatedManagerApi'

defineOptions({ name: 'DmLogin' })
definePage({
  meta: {
    layout: 'blank',
    public: true,
    unauthenticatedOnly: true,
  },
})

const router = useRouter()
const userName = ref('')
const password = ref('')
const busy = ref(false)
const error = ref('')
const showPass = ref(false)

async function submit() {
  error.value = ''
  if (!userName.value.trim() || !password.value) {
    error.value = 'اسم المستخدم وكلمة المرور مطلوبان'
    return
  }
  busy.value = true
  try {
    const data = await dmLogin({
      userName: userName.value,
      password: password.value,
    })
    dmSaveSession(data)
    await router.replace('/dm-dashboard')
  }
  catch (e) {
    error.value = e?.response?.data?.message || e?.message || 'تعذر تسجيل الدخول'
  }
  finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="dm-login">
    <VCard
      max-width="420"
      class="pa-6"
      width="100%"
    >
      <div class="text-center mb-6">
        <VIcon
          icon="tabler-shield-check"
          size="56"
          color="primary"
          class="mb-3"
        />
        <h1 class="text-h5 font-weight-bold mb-1">
          تطبيق المدير المفوض — ويب
        </h1>
        <p class="text-medium-emphasis mb-0">
          نفس حساب التطبيق · بدون اختيار محافظة
        </p>
      </div>

      <VAlert
        v-if="error"
        type="error"
        variant="tonal"
        class="mb-4"
      >
        {{ error }}
      </VAlert>

      <VTextField
        v-model="userName"
        label="اسم المستخدم"
        density="comfortable"
        class="mb-3"
        autocomplete="username"
        @keyup.enter="submit"
      />
      <VTextField
        v-model="password"
        label="كلمة المرور"
        density="comfortable"
        class="mb-4"
        :type="showPass ? 'text' : 'password'"
        autocomplete="current-password"
        :append-inner-icon="showPass ? 'tabler-eye-off' : 'tabler-eye'"
        @click:append-inner="showPass = !showPass"
        @keyup.enter="submit"
      />
      <VBtn
        color="primary"
        block
        size="large"
        :loading="busy"
        @click="submit"
      >
        دخول
      </VBtn>
      <div class="text-center mt-4">
        <RouterLink
          to="/login"
          class="text-body-2"
        >
          العودة لدخول الإدارة
        </RouterLink>
      </div>
    </VCard>
  </div>
</template>

<style scoped>
.dm-login {
  min-block-size: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.5rem;
  background: rgb(var(--v-theme-background));
}
</style>
