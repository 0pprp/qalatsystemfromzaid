import { createVuetify } from 'vuetify'
import { aliases, mdi } from 'vuetify/iconsets/mdi'

// ── هوية بصرية احترافية ──────────────────────────────
const brandColors = {
  dark:      '#0a1929',   // أزرق داكن (خلفيات)
  primary:   '#1565C0',   // أزرق أساسي
  primaryLight: '#1E88E5',
  secondary: '#F59E0B',   // ذهبي
  accent:    '#06B6D4',   // سماوي
  success:   '#10B981',   // أخضر
  info:      '#3B82F6',   // أزرق فاتح
  warning:   '#F59E0B',   // كهرماني
  error:     '#EF4444',   // أحمر
  surface:   '#F8FAFC',   // خلفية فاتحة
}

export default createVuetify({
  defaults: {
    global: {
      ripple: true,
    },
    VCard: {
      elevation: 0,
      rounded: 'xl',
    },
    VBtn: {
      rounded: 'lg',
      elevation: 0,
    },
    VTextField: {
      variant: 'outlined',
      density: 'comfortable',
      rounded: 'lg',
    },
    VDataTable: {
      hover: true,
    },
    VChip: {
      rounded: 'lg',
    },
    VSheet: {
      rounded: 'xl',
    },
  },
  icons: {
    defaultSet: 'mdi',
    aliases,
    sets: { mdi },
  },
  theme: {
    defaultTheme: 'light',
    themes: {
      light: {
        dark: false,
        colors: brandColors,
      },
    },
  },
})
