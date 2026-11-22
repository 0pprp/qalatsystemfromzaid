<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { errorMessage, signIn } from '../api'

const router = useRouter()
const userName = ref('')
const password = ref('')
const loading = ref(false)
const error = ref('')

async function submit() {
  if (!userName.value.trim() || !password.value) {
    error.value = 'أدخل اسم المستخدم وكلمة المرور'

    return
  }
  loading.value = true
  error.value = ''
  try {
    await signIn(userName.value.trim(), password.value)
    router.push('/')
  }
  catch (e) {
    error.value = errorMessage(e, 'تعذر تسجيل الدخول')
  }
  finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login">
    <VCard
      class="login__card"
      elevation="2"
    >
      <VCardText class="pa-8">
        <div class="text-center mb-6">
          <img
            src="/logo.png"
            alt=""
            width="84"
            height="84"
          >
          <h1 class="text-h6 font-weight-bold text-primary mt-2 mb-1">
            القانونية — شركة قلعة الضمان
          </h1>
          <div class="text-body-2 muted">
            متابعة ملفات المدينين من الاستلام حتى التنفيذ
          </div>
        </div>
        <VForm @submit.prevent="submit">
          <VTextField
            v-model="userName"
            label="اسم المستخدم"
            name="username"
            autocomplete="username"
            autofocus
            class="mb-3"
            hide-details
          />
          <VTextField
            v-model="password"
            label="كلمة المرور"
            name="password"
            type="password"
            autocomplete="current-password"
            class="mb-3"
            hide-details
          />
          <VAlert
            v-if="error"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ error }}
          </VAlert>
          <VBtn
            type="submit"
            color="primary"
            size="large"
            block
            :loading="loading"
          >
            دخول
          </VBtn>
        </VForm>
      </VCardText>
    </VCard>
  </div>
</template>

<style scoped>
.login {
  display: grid;
  min-block-size: 100vh;
  padding: 24px;
  background: linear-gradient(135deg, rgba(var(--v-theme-primary), 0.7), rgba(0, 0, 0, 85%)), #0b2f31;
  place-items: center;
}

.login__card {
  inline-size: min(420px, 100%);
}
</style>
