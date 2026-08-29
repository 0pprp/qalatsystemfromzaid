<script setup>
const socialLinks = [
  {
    platform: 'facebook',
    icon: 'tabler-brand-facebook',
    title: 'فيسبوك',
    description: 'تواصل معنا عبر صفحتنا الرسمية',
    url: 'https://www.facebook.com/zaidaladly32101994',
    color: '#1877F2',
    gradient: 'linear-gradient(135deg, #1877F2 0%, #0C5DC7 100%)',
  },
  {
    platform: 'instagram',
    icon: 'tabler-brand-instagram',
    title: 'انستجرام',
    description: 'تابعنا وتواصل معنا عبر انستجرام',
    url: 'https://www.instagram.com/zaid.alshammary/',
    color: '#E4405F',
    gradient: 'linear-gradient(135deg, #833AB4 0%, #FD1D1D 50%, #FCB045 100%)',
  },
  {
    platform: 'telegram',
    icon: 'tabler-brand-telegram',
    title: 'تليجرام',
    description: 'قناتنا الرسمية للدعم المباشر',
    url: 'https://t.me/zaidalshammery',
    color: '#24A1DE',
    gradient: 'linear-gradient(135deg, #24A1DE 0%, #1A8CC4 100%)',
  },
  {
    platform: 'whatsapp',
    icon: 'tabler-brand-whatsapp',
    title: 'واتساب',
    description: 'محادثة فورية وسريعة',
    url: 'https://wa.me/9647831848785',
    color: '#25D366',
    gradient: 'linear-gradient(135deg, #25D366 0%, #128C7E 100%)',
  },
]

const openLink = url => {
  window.open(url, '_blank')
}
</script>

<template>
  <div class="support-page-wrapper">
    <!-- Header Section -->
    <VRow
      justify="center"
      class="text-center mb-8"
    >
      <VCol
        cols="12"
        md="8"
      >
        <VAvatar
          color="primary"
          variant="tonal"
          size="80"
          class="mb-6 elevation-2"
        >
          <VIcon
            icon="tabler-headset"
            size="48"
          />
        </VAvatar>
        <h1 class="text-h3 font-weight-bold mb-3 text-primary">
          مركز الدعم الفني
        </h1>
        <p class="text-h6 text-medium-emphasis">
          نحن هنا لمساعدتك! اختر وسيلة التواصل المفضلة لديك للحصول على الدعم الفوري.
        </p>
      </VCol>
    </VRow>

    <!-- Social Links Grid -->
    <VRow class="mb-8">
      <VCol
        v-for="(item, index) in socialLinks"
        :key="index"
        cols="12"
        sm="6"
        md="3"
      >
        <VCard
          class="social-card text-center h-100 pa-6 d-flex flex-column align-center"
          elevation="2"
          hover
          @click="openLink(item.url)"
        >
          <!-- Icon Circle with Gradient -->
          <div
            class="icon-wrapper mb-4 elevation-4 d-flex align-center justify-center"
            :style="{ background: item.gradient }"
          >
            <VIcon
              :icon="item.icon"
              size="42"
              color="white"
            />
          </div>

          <h3 class="text-h5 font-weight-bold mb-2">
            {{ item.title }}
          </h3>
          <p class="text-body-2 text-medium-emphasis mb-6 flex-grow-1">
            {{ item.description }}
          </p>

          <VBtn
            block
            variant="tonal"
            :color="item.platform === 'instagram' ? 'error' : 'primary'"
            class="font-weight-bold"
          >
            تواصل الآن
            <VIcon
              end
              icon="tabler-arrow-left"
              class="flip-icon-rtl"
            />
          </VBtn>
        </VCard>
      </VCol>
    </VRow>

    <!-- Additional Contact Info -->
    <VRow>
      <VCol cols="12">
        <VCard
          class="pa-6"
          elevation="1"
        >
          <div class="d-flex flex-wrap align-center justify-space-around text-center gap-6">
            <div class="contact-item d-flex align-center gap-3">
              <VAvatar
                color="primary"
                variant="tonal"
                size="48"
              >
                <VIcon
                  icon="tabler-phone"
                  size="24"
                />
              </VAvatar>
              <div class="text-start">
                <div class="text-caption text-medium-emphasis">
                  رقم الهاتف
                </div>
                <div
                  class="text-body-1 font-weight-bold"
                  dir="ltr"
                >
                  +964 783 184 8785
                </div>
              </div>
            </div>

            <VDivider
              vertical
              class="hidden-sm-and-down"
            />

            <div class="contact-item d-flex align-center gap-3">
              <VAvatar
                color="info"
                variant="tonal"
                size="48"
              >
                <VIcon
                  icon="tabler-mail"
                  size="24"
                />
              </VAvatar>
              <div class="text-start">
                <div class="text-caption text-medium-emphasis">
                  البريد الإلكتروني
                </div>
                <div class="text-body-1 font-weight-bold">
                  zaidkareemalshammery@gmail.com
                </div>
                <div class="text-body-2 text-medium-emphasis">
                  zaidkareemalshammery@hotmail.com
                </div>
              </div>
            </div>

            <VDivider
              vertical
              class="hidden-sm-and-down"
            />

            <div class="contact-item d-flex align-center gap-3">
              <VAvatar
                color="success"
                variant="tonal"
                size="48"
              >
                <VIcon
                  icon="tabler-clock"
                  size="24"
                />
              </VAvatar>
              <div class="text-start">
                <div class="text-caption text-medium-emphasis">
                  ساعات العمل
                </div>
                <div class="text-body-1 font-weight-bold">
                  متاح على مدار الساعة
                </div>
              </div>
            </div>
          </div>
        </VCard>
      </VCol>
    </VRow>
  </div>
</template>

<style scoped>
.support-page-wrapper {
  padding-block: 2rem;
}

.social-card {
  border-radius: 16px;
  transition: transform 0.3s ease, box-shadow 0.3s ease;
}

.social-card:hover {
  box-shadow: 0 12px 30px rgba(var(--v-theme-primary), 0.15) !important;
  transform: translateY(-8px);
}

.icon-wrapper {
  border-radius: 50%;
  block-size: 80px;
  inline-size: 80px;
  transition: transform 0.3s ease;
}

.social-card:hover .icon-wrapper {
  transform: scale(1.1) rotate(5deg);
}

.contact-item {
  min-inline-size: 200px;
}

/* Flip arrow for RTL */
.flip-icon-rtl {
  transform: scaleY(-1); /* Just in case arrow-left/right needs flipping based on context,
                            but tabler icons usually match direction.
                            tabler-arrow-left in RTL usually points left.
                            If we want it to point 'forward' in RTL (which is Left), it's correct.
                            */
}
</style>
