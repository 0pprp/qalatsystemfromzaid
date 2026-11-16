<template>
  <div class="qa-container py-8 pb-12">
    <v-card class="bg-brand-gradient pa-6 pa-md-12" rounded="xl" flat>
      <v-row align="center">
        <v-col cols="12" md="7">
          <h1 class="text-h4 font-weight-bold mb-3">تطبيق قلعة الضمان</h1>
          <p class="text-body-1 mb-6" style="opacity: 0.92; line-height: 2">
            اعرض المنتجات وقدّم الطلبات، واربح عمولة على كل بيع وحصة من كل بيع في شبكتك — وتابع محفظتك وشبكتك من هاتفك مع إشعارات فورية.
          </p>
          <AppStoreBadges dark />
          <p class="text-caption mt-4" style="opacity: 0.8">بعد التحميل، اسمح بتثبيت التطبيقات من هذا المصدر في إعدادات هاتفك ثم افتح الملف.</p>
        </v-col>
        <v-col cols="12" md="5" class="text-center">
          <div class="phone-mock mx-auto">
            <img src="/LogoCompany.png" alt="قلعة الضمان" />
          </div>
        </v-col>
      </v-row>
    </v-card>
    <v-row class="mt-6">
      <v-col v-for="f in features" :key="f.title" cols="12" sm="6" md="3">
        <v-card class="soft-card pa-5 h-100" rounded="xl">
          <v-avatar color="primary" variant="tonal" size="48" class="mb-3"><v-icon>{{ f.icon }}</v-icon></v-avatar>
          <div class="font-weight-bold mb-1">{{ f.title }}</div>
          <div class="text-body-2 text-medium-emphasis">{{ f.text }}</div>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup>
import AppStoreBadges from '@/components/AppStoreBadges.vue'
const features = [
  { icon: 'mdi-package-variant-closed', title: 'طلباتي', text: 'تابع حالة كل طلب من المراجعة حتى البيع.' },
  { icon: 'mdi-wallet-outline', title: 'محفظتي', text: 'رصيدك وأرباحك الشهرية وطلبات السحب.' },
  { icon: 'mdi-account-group-outline', title: 'شبكتي', text: 'أعضاء شبكتك في كل المستويات وأرباحك من كل فرع.' },
  { icon: 'mdi-bell-ring-outline', title: 'إشعارات فورية', text: 'تحديثات الطلبات والعروض الجديدة.' },
]
</script>

<style scoped>
.phone-mock {
  width: 220px; height: 420px; border-radius: 36px; background: #fff; border: 10px solid #0b2f2c;
  display: flex; align-items: center; justify-content: center; box-shadow: 0 20px 50px rgba(0, 0, 0, 0.3);
}
.phone-mock img { width: 70%; }
</style>
