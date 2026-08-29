<template>
  <v-app>
    <router-view v-slot="{ Component }">
      <transition name="fade" mode="out-in">
        <component :is="Component" />
      </transition>
    </router-view>
  </v-app>
</template>

<script setup>
// ── Professional Portal App ───────────────────────────
</script>

<style>
/* ── Global Reset & Typography ──────────────────────── */
*, *::before, *::after {
  box-sizing: border-box;
}

html {
  direction: rtl;
  font-family: 'Cairo', 'Segoe UI', 'Arial', sans-serif;
}

body {
  font-family: 'Cairo', 'Segoe UI', 'Arial', sans-serif;
  margin: 0;
  padding: 0;
  background: #f0f4f8;
}

a { text-decoration: none; color: inherit; }

/* ── Vuetify Overrides ──────────────────────────────── */
.v-application {
  font-family: 'Cairo', 'Segoe UI', 'Arial', sans-serif !important;
}

/* Smooth page transitions */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

/* Custom scrollbar */
::-webkit-scrollbar { width: 8px; height: 8px; }
::-webkit-scrollbar-track { background: #f0f4f8; }
::-webkit-scrollbar-thumb { background: #94a3b8; border-radius: 8px; }
::-webkit-scrollbar-thumb:hover { background: #64748b; }
</style>
