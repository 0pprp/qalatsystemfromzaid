<template>
  <div class="qa-container py-6 py-md-12 pb-12">
    <v-row justify="center" align="stretch" class="login-wrap">
      <v-col v-if="mdAndUp" md="5">
        <v-card class="bg-brand-gradient h-100 pa-8 d-flex flex-column justify-center" rounded="xl" flat>
          <div class="logo-box mb-6"><img src="/LogoCompany.png" alt="قلعة الضمان" height="90" /></div>
          <div class="text-h5 font-weight-bold mb-3">أهلاً بك في قلعة الضمان</div>
          <div v-for="b in benefits" :key="b" class="d-flex align-center mb-3">
            <v-icon class="me-2">mdi-check-circle</v-icon><span>{{ b }}</span>
          </div>
        </v-card>
      </v-col>
      <v-col cols="12" sm="9" md="5">
        <v-card class="soft-card pa-5 pa-md-8" rounded="xl">
          <LoginForm @success="onSuccess" />
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup>
import { useRoute, useRouter } from 'vue-router'
import { useDisplay } from 'vuetify'
import LoginForm from '@/components/LoginForm.vue'
import { toastSuccess } from '@/composables/useToast'

const { mdAndUp } = useDisplay()
const route = useRoute()
const router = useRouter()
const benefits = ['تابع طلباتك وحالتها لحظة بلحظة', 'اطّلع على أقساطك وكل دفعاتك', 'اربح عمولة عند دعوة أصدقائك', 'استلم إشعارات العروض والطلبات']

function onSuccess(res) {
  toastSuccess(`أهلاً ${res.user?.firstName || ''}! تم تسجيل الدخول`)
  const redirect = typeof route.query.redirect === 'string' && route.query.redirect.startsWith('/') ? route.query.redirect : '/'
  router.replace(redirect)
}
</script>

<style scoped>
.logo-box { background: #fff; border-radius: 20px; padding: 10px; display: inline-flex; align-self: flex-start; }
</style>
