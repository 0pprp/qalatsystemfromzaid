<template>
  <v-dialog v-model="auth.loginDialog" max-width="480" :fullscreen="xs" scrollable>
    <v-card :rounded="xs ? 0 : 'xl'">
      <v-toolbar color="transparent" density="compact">
        <img src="/LogoCompany.png" alt="" height="32" class="ms-4" />
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" aria-label="إغلاق" @click="auth.loginDialog = false" />
      </v-toolbar>
      <v-card-text class="pt-0 pb-6 px-5">
        <v-alert v-if="auth.loginReason" type="info" variant="tonal" density="compact" class="mb-4">{{ auth.loginReason }}</v-alert>
        <LoginForm v-if="auth.loginDialog" @success="onSuccess" />
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup>
import { useDisplay } from 'vuetify'
import { useRouter } from 'vue-router'
import LoginForm from './LoginForm.vue'
import { useAuthStore } from '@/stores/auth'
import { toastSuccess } from '@/composables/useToast'

const { xs } = useDisplay()
const auth = useAuthStore()
const router = useRouter()

function onSuccess(res) {
  auth.loginDialog = false
  toastSuccess(`أهلاً ${res.user?.firstName || ''}! تم تسجيل الدخول`)
  if (auth.loginRedirect) router.push(auth.loginRedirect)
  auth.loginRedirect = null
  auth.loginReason = ''
}
</script>
