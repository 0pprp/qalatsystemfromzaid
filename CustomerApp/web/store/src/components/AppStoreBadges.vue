<template>
  <div class="d-flex flex-wrap ga-3">
    <component
      :is="b.url ? 'a' : 'div'"
      v-for="b in badges"
      :key="b.key"
      :href="b.url || undefined"
      target="_blank"
      rel="noopener"
      class="store-badge"
      :class="{ dark, disabled: !b.url }"
      :aria-label="b.label"
    >
      <v-icon size="30">{{ b.icon }}</v-icon>
      <div class="d-flex flex-column text-start">
        <span class="small">{{ b.url ? b.pre : 'قريباً على' }}</span>
        <span class="big">{{ b.name }}</span>
      </div>
    </component>
  </div>
</template>

<script setup>
defineProps({ dark: Boolean })
// The app is distributed as an APK (not through the stores).
const badges = [
  { key: 'apk', icon: 'mdi-android', pre: 'حمّل تطبيق', name: 'أندرويد (APK)', label: 'تحميل تطبيق أندرويد', url: import.meta.env.VITE_APK_URL || '' },
]
</script>

<style scoped>
.store-badge {
  display: inline-flex; align-items: center; gap: 10px; min-width: 160px;
  padding: 8px 14px; border-radius: 12px; background: #111; color: #fff; text-decoration: none;
  border: 1px solid #111; transition: transform 0.15s;
}
.store-badge.dark { background: rgba(0, 0, 0, 0.35); border-color: rgba(255, 255, 255, 0.35); }
.store-badge:hover { transform: translateY(-2px); }
.store-badge.disabled { cursor: default; opacity: 0.85; }
.store-badge.disabled:hover { transform: none; }
.small { font-size: 0.68rem; opacity: 0.8; line-height: 1.2; }
.big { font-size: 1.05rem; font-weight: 700; line-height: 1.2; direction: ltr; text-align: start; }
</style>
