<template>
  <v-footer class="qa-footer bg-brand-gradient pt-10 pb-6 d-block" :class="{ 'mb-15': smAndDown }">
    <div class="qa-container">
      <v-row>
        <v-col cols="12" md="4">
          <div class="d-flex align-center mb-3">
            <div class="logo-wrap"><img src="/LogoCompany.png" alt="قلعة الضمان" height="54" /></div>
            <div class="ms-3">
              <div class="text-h6 font-weight-bold">قلعة الضمان</div>
              <div class="text-body-2 opacity-80">تسوّق نقداً أو بالأقساط اليومية</div>
            </div>
          </div>
          <p class="text-body-2 opacity-80" style="line-height: 1.9">
            أجهزة كهربائية وأثاث ومستلزمات منزلية وتجارية في فروعنا داخل العراق. الأقساط اليومية متاحة فقط لأصحاب المحلات التجارية.
          </p>
        </v-col>
        <v-col cols="6" md="2">
          <div class="footer-title">روابط</div>
          <router-link class="footer-link" :to="{ name: 'home' }">الرئيسية</router-link>
          <router-link class="footer-link" :to="{ name: 'catalog' }">المنتجات</router-link>
          <router-link class="footer-link" :to="{ name: 'orders' }">طلباتي</router-link>
          <router-link class="footer-link" :to="{ name: 'download' }">حمّل التطبيق</router-link>
        </v-col>
        <v-col cols="6" md="2">
          <div class="footer-title">قانوني</div>
          <router-link class="footer-link" :to="{ name: 'terms' }">الشروط والأحكام</router-link>
          <router-link class="footer-link" :to="{ name: 'privacy' }">سياسة الخصوصية</router-link>
          <router-link class="footer-link" :to="{ name: 'terms', hash: '#referral' }">شروط برنامج الإحالة</router-link>
        </v-col>
        <v-col cols="12" md="4">
          <div class="footer-title">الدعم والتواصل</div>
          <a v-if="catalog.supportPhone" class="footer-link d-flex align-center" :href="`tel:${catalog.supportPhone}`">
            <v-icon size="18" class="me-2">mdi-phone</v-icon><span class="ltr">{{ formatPhone(catalog.supportPhone) }}</span>
          </a>
          <a v-if="catalog.supportWhatsapp" class="footer-link d-flex align-center" :href="`https://wa.me/${catalog.supportWhatsapp}`" target="_blank" rel="noopener">
            <v-icon size="18" class="me-2">mdi-whatsapp</v-icon>واتساب الدعم
          </a>
          <div v-if="!catalog.config" class="text-body-2 opacity-70">جارٍ تحميل معلومات التواصل...</div>
          <div class="footer-title mt-4">حمّل التطبيق</div>
          <AppStoreBadges dark />
        </v-col>
      </v-row>
      <v-divider class="my-5 opacity-30" />
      <div class="d-flex flex-wrap justify-space-between text-caption opacity-80 ga-2">
        <span>© {{ year }} قلعة الضمان. جميع الحقوق محفوظة.</span>
        <span>الأسعار بالدينار العراقي وقد تختلف حسب الفرع.</span>
      </div>
    </div>
  </v-footer>
</template>

<script setup>
import { useDisplay } from 'vuetify'
import AppStoreBadges from './AppStoreBadges.vue'
import { useCatalogStore } from '@/stores/catalog'
import { formatPhone } from '@/utils/phone'

const { smAndDown } = useDisplay()
const catalog = useCatalogStore()
const year = new Date().getFullYear()
</script>

<style scoped>
.logo-wrap { background: #fff; border-radius: 14px; padding: 4px 8px; display: flex; }
.footer-title { font-weight: 800; margin-bottom: 10px; }
.footer-link { display: block; color: rgba(255, 255, 255, 0.85); text-decoration: none; padding: 4px 0; font-size: 0.92rem; }
.footer-link:hover { color: #fff; text-decoration: underline; }
.opacity-80 { opacity: 0.8; }
.opacity-70 { opacity: 0.7; }
</style>
