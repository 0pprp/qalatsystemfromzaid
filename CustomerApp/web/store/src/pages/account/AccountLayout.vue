<template>
  <div class="qa-container py-4 pb-12">
    <v-row>
      <v-col v-if="mdAndUp" md="3">
        <v-card class="soft-card side" rounded="xl">
          <div class="pa-4 d-flex align-center">
            <v-avatar color="primary" size="48" class="me-3 text-h6">{{ auth.user?.firstName?.slice(0, 1) }}</v-avatar>
            <div class="min-w-0">
              <div class="font-weight-bold text-truncate">{{ auth.user?.fullName }}</div>
              <div class="text-caption text-medium-emphasis ltr">{{ formatPhone(auth.user?.phone) }}</div>
            </div>
          </div>
          <v-divider />
          <v-list nav density="comfortable" color="primary">
            <v-list-item v-for="l in links" :key="l.to" :to="{ name: l.to }" :prepend-icon="l.icon" :title="l.title" rounded="lg">
              <template v-if="l.to === 'notifications' && auth.unreadCount" #append>
                <v-badge :content="auth.unreadCount" color="error" inline />
              </template>
            </v-list-item>
          </v-list>
          <v-divider />
          <v-list nav density="comfortable">
            <v-list-item prepend-icon="mdi-logout" title="تسجيل الخروج" base-color="error" rounded="lg" @click="logout" />
          </v-list>
        </v-card>
      </v-col>
      <v-col cols="12" :md="mdAndUp ? 9 : 12">
        <v-tabs v-if="!mdAndUp" :model-value="activeTab" color="primary" show-arrows class="mb-4 tabs" density="comfortable">
          <v-tab v-for="l in links" :key="l.to" :value="l.to" :to="{ name: l.to }">
            <v-icon start size="18">{{ l.icon }}</v-icon>{{ l.title }}
            <v-badge v-if="l.to === 'notifications' && auth.unreadCount" :content="auth.unreadCount" color="error" inline />
          </v-tab>
        </v-tabs>
        <router-view v-slot="{ Component, route }">
          <component :is="Component" :key="route.path" />
        </router-view>
      </v-col>
    </v-row>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useDisplay } from 'vuetify'
import { ACCOUNT_LINKS } from './links'
import { useAuthStore } from '@/stores/auth'
import { useCatalogStore } from '@/stores/catalog'
import { formatPhone } from '@/utils/phone'
import { toast } from '@/composables/useToast'

const { mdAndUp } = useDisplay()
const auth = useAuthStore()
const catalog = useCatalogStore()
const route = useRoute()
const router = useRouter()
const links = computed(() => ACCOUNT_LINKS.filter((l) => !l.referral || catalog.referralEnabled))
const activeTab = computed(() => {
  const n = String(route.name)
  if (n === 'order') return 'orders'
  return n
})
async function logout() {
  await auth.logout()
  toast('تم تسجيل الخروج')
  router.push({ name: 'home' })
}
</script>

<style scoped>
.side { position: sticky; top: 92px; }
.min-w-0 { min-width: 0; }
.tabs { background: #fff; border-radius: 14px; border: 1px solid rgba(0, 0, 0, 0.06); }
</style>
