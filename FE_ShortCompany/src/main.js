import App from '@/App.vue'
import { registerPlugins } from '@core/utils/plugins'
import { createApp } from 'vue'

// Styles
import '@/plugins/axios'
import { installSummaryReadOnlyGuard } from '@/plugins/summaryReadOnlyGuard'
import '@core/scss/template/index.scss'
import '@styles/styles.scss'

installSummaryReadOnlyGuard()

// Create vue app
const app = createApp(App)

// Register plugins
registerPlugins(app)

// Mount vue app
app.mount('#app')
