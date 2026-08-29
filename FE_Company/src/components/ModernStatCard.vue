<script setup>
import { computed } from 'vue'

const props = defineProps({
  title: { type: String, required: true },
  value: { type: [String, Number], required: true },
  icon: { type: String, default: 'tabler-circle' },
  color: { type: String, default: 'primary' },

  // Optional gradient override. If not provided, we generate one based on color
  gradient: { type: String, default: '' },
})

const finalGradient = computed(() => {
  if (props.gradient) return props.gradient
  
  // Default gradients map based on color prop
  const map = {
    primary: 'linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)',
    success: 'linear-gradient(135deg, #11998e 0%, #38ef7d 100%)',
    warning: 'linear-gradient(135deg, #F2994A 0%, #F2C94C 100%)',
    error: 'linear-gradient(135deg, #FF416C 0%, #FF4B2B 100%)',
    info: 'linear-gradient(135deg, #36D1DC 0%, #5B86E5 100%)',
    secondary: 'linear-gradient(135deg, #667db6 0%, #0082c8 100%, #0082c8 100%, #667db6 100%)',
    indigo: 'linear-gradient(135deg, #667db6 0%, #0082c8 100%)',
    teal: 'linear-gradient(135deg, #11998e 0%, #38ef7d 100%)',
    purple: 'linear-gradient(135deg, #da22ff 0%, #9733ee 100%)',
    orange: 'linear-gradient(135deg, #fce38a 0%, #f38181 100%)',
    amber: 'linear-gradient(135deg, #f6d365 0%, #fda085 100%)',
    pink: 'linear-gradient(135deg, #ec008c 0%, #fc6767 100%)',
    deepOrange: 'linear-gradient(135deg, #FF512F 0%, #DD2476 100%)',
    'deep-purple': 'linear-gradient(135deg, #654ea3 0%, #eaafc8 100%)',
  }
  
  // Fallback to primary if color not found or generic
  return map[props.color] || map['primary']
})

const cardVars = computed(() => {
  return {
    '--card-gradient': finalGradient.value,
  }
})
</script>

<template>
  <VCard
    class="modern-card"
    :style="cardVars"
    variant="elevated"
  >
    <div class="card-content">
      <div class="card-header">
        <span class="card-title">{{ title }}</span>
        <VIcon
          :icon="icon"
          class="header-icon"
        />
      </div>
      <div class="card-body">
        <h3 class="card-value">
          {{ value }}
        </h3>
      </div>
    </div>
    
    <!-- Decorative Elements -->
    <div class="card-watermark">
      <VIcon :icon="icon" />
    </div>
    <div class="card-gradient-bar" />
  </VCard>
</template>

<style scoped lang="scss">
/* Modern Card Styles extracted here */
.modern-card {
  position: relative;
  overflow: hidden;
  border-radius: 20px !important; /* Force radius override if needed */
  min-block-size: 140px;
  padding-block: 24px;
  padding-inline: 24px;
  transition: all 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275);

  /* Remove manual background/border/shadow overrides to let VCard handle theme */
}

.modern-card:hover {
  box-shadow: 0 20px 40px -10px rgb(0 0 0 / 20%);
  transform: translateY(-8px) scale(1.02);
}

.card-content {
  position: relative;
  z-index: 2;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-block-end: 12px;
}

.card-title {
  color: rgb(var(--v-theme-on-surface));
  font-size: 0.9rem;
  font-weight: 600;
  letter-spacing: 0.5px;
  opacity: 0.8;
  text-transform: uppercase;
}

.header-icon {
  color: rgb(var(--v-theme-on-surface));
  font-size: 2.5rem;
  opacity: 0.5;
}

.card-value {
  background: var(--card-gradient);
  background-clip: text;
  filter: drop-shadow(0 2px 4px rgb(0 0 0 / 10%));
  font-size: 1.8rem;
  font-weight: 800;
  line-height: 1.2;
  -webkit-text-fill-color: transparent;
}

/* Watermark Icon */
.card-watermark {
  position: absolute;
  z-index: 1;
  inset-block-end: -20px;
  inset-inline-end: -20px;
  opacity: 0.08;
  transform: rotate(-15deg);
}

.card-watermark .v-icon {
  color: rgb(var(--v-theme-on-surface));
  font-size: 10rem;
}

/* Gradient Bar */
.card-gradient-bar {
  position: absolute;
  z-index: 2;
  background: var(--card-gradient);
  block-size: 4px;
  inline-size: 100%;
  inset-block-end: 0;
  inset-inline-start: 0;
  opacity: 0.8;
}
</style>
