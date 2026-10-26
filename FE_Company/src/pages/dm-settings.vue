<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { DM_USER_TYPE } from '@/composables/delegatedManagerApi'
import { removeLocalStorage, removeToken } from '@/services/tokenService'

defineOptions({ name: 'DmSettings' })
definePage({ meta: { layout: 'dm' } })

const router = useRouter()
const confirmOpen = ref(false)

const userName = computed(() => localStorage.getItem('UserName') || '')
const cityName = computed(() => localStorage.getItem('CityName') || 'كل المحافظات')
const userType = computed(() => localStorage.getItem('UserType') || DM_USER_TYPE)

function logout() {
  removeToken()
  removeLocalStorage()
  router.replace('/login')
}
</script>

<template>
  <div>
    <h1 class="text-h5 mb-4">
      الإعدادات
    </h1>

    <VCard
      variant="outlined"
      class="mb-4"
    >
      <VCardTitle>الحساب</VCardTitle>
      <VCardText>
        <div class="dm-kv">
          <span>الاسم</span>
          <strong>{{ userName || '—' }}</strong>
        </div>
        <div class="dm-kv">
          <span>النوع</span>
          <strong>{{ userType }}</strong>
        </div>
        <div class="dm-kv">
          <span>النطاق</span>
          <strong>{{ cityName }}</strong>
        </div>
      </VCardText>
    </VCard>

    <VCard variant="outlined">
      <VCardTitle>الجلسة</VCardTitle>
      <VCardText>
        <p class="text-body-2 text-medium-emphasis mb-4">
          ويب المدير المفوض يستخدم نفس حساب التطبيق ونفس بوابة المبيعات. لا حاجة لاختيار محافظة.
        </p>
        <VBtn
          color="error"
          variant="tonal"
          @click="confirmOpen = true"
        >
          تسجيل الخروج
        </VBtn>
      </VCardText>
    </VCard>

    <VDialog
      v-model="confirmOpen"
      max-width="400"
    >
      <VCard>
        <VCardTitle>تسجيل الخروج</VCardTitle>
        <VCardText>
          هل تريد تسجيل الخروج؟
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="confirmOpen = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="error"
            @click="logout"
          >
            خروج
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.dm-kv {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.45rem 0;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
