import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css'
import { createVuetify } from 'vuetify'
import { ar } from 'vuetify/locale'

export default createVuetify({
  locale: { locale: 'ar', fallback: 'ar', messages: { ar }, rtl: { ar: true } },
  theme: {
    defaultTheme: 'qalaa',
    themes: {
      qalaa: {
        dark: false,
        colors: {
          primary: '#006A64',
          'primary-darken-1': '#004D48',
          secondary: '#F2A93B',
          accent: '#2E9C8F',
          background: '#F5F7F7',
          surface: '#FFFFFF',
          'surface-variant': '#E8F1F0',
          'on-surface-variant': '#22413F',
          error: '#C62828',
          warning: '#E67E00',
          info: '#1565C0',
          success: '#2E7D32',
        },
      },
    },
  },
  defaults: {
    global: { ripple: true },
    VBtn: { rounded: 'lg', style: 'letter-spacing:0; text-transform:none; font-weight:700' },
    VCard: { rounded: 'xl' },
    VTextField: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VTextarea: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VSelect: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VAutocomplete: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VChip: { rounded: 'lg' },
    VAlert: { rounded: 'lg' },
  },
})
