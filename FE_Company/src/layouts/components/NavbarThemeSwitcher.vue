<script setup>
const themes = [
  {
    name: 'light',
    icon: 'tabler-sun-high',
    text: 'فاتح',
  },
  {
    name: 'dark',
    icon: 'tabler-moon-stars',
    text: 'داكن',
  },
  {
    name: 'system',
    icon: 'tabler-device-desktop-analytics',
    text: 'حسب النظام',
  },
]
</script>

<template>
  <ThemeSwitcher :themes="themes"/>
</template>
