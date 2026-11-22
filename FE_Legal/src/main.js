import { createApp, h } from 'vue'
import { createVuetify } from 'vuetify'
import { ar } from 'vuetify/locale'
import 'vuetify/styles'
import './styles.css'
import App from './App.vue'
import { ICONS } from './icons'
import { router } from './router'

// Tabler outlines from the generated subset: `<VIcon icon="gavel" />`.
const tabler = {
  component: props => h(props.tag, { class: 'tb-icon' }, [
    h('svg', {
      'class': 'v-icon__svg',
      'viewBox': '0 0 24 24',
      'aria-hidden': 'true',
      'innerHTML': ICONS[props.icon] || '',
    }),
  ]),
}

const vuetify = createVuetify({
  locale: { locale: 'ar', messages: { ar }, rtl: { ar: true } },
  icons: {
    defaultSet: 'tabler',
    sets: { tabler },
    aliases: {
      collapse: 'chevron-up',
      complete: 'check',
      cancel: 'x',
      close: 'x',
      delete: 'x',
      clear: 'x',
      success: 'circle-check',
      info: 'info-circle',
      warning: 'alert-triangle',
      error: 'alert-circle',
      prev: 'chevron-right',
      next: 'chevron-left',
      checkboxOn: 'square-check',
      checkboxOff: 'square',
      checkboxIndeterminate: 'square-minus',
      delimiter: 'circle',
      sortAsc: 'arrow-up',
      sortDesc: 'arrow-down',
      expand: 'chevron-down',
      menu: 'menu-2',
      subgroup: 'caret-down',
      dropdown: 'chevron-down',
      radioOn: 'circle-dot',
      radioOff: 'circle',
      edit: 'edit',
      ratingEmpty: 'circle',
      ratingFull: 'circle-dot',
      ratingHalf: 'circle-dot',
      loading: 'refresh',
      first: 'chevrons-right',
      last: 'chevrons-left',
      unfold: 'selector',
      file: 'paperclip',
      plus: 'plus',
      minus: 'minus',
      calendar: 'calendar',
    },
  },
  theme: {
    defaultTheme: 'legal',
    themes: {
      legal: {
        dark: false,
        // The company dashboard's palette, so both apps read as one product.
        colors: {
          'background': '#F8F7FA',
          'surface': '#FFFFFF',
          'primary': '#128488',
          'secondary': '#808390',
          'accent': '#7367F0',
          'info': '#00BAD1',
          'success': '#28C76F',
          'warning': '#FF9F43',
          'error': '#FF4C51',
          'on-background': '#2F2B3D',
          'on-surface': '#2F2B3D',
        },
      },
    },
  },
  defaults: {
    VBtn: { rounded: 'md', class: 'text-none' },
    VCard: { rounded: 'md', elevation: 0 },
    VTextField: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VTextarea: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VSelect: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VAutocomplete: { variant: 'outlined', density: 'comfortable', color: 'primary' },
    VChip: { rounded: 'md' },
  },
})

createApp(App).use(router).use(vuetify).mount('#app')
