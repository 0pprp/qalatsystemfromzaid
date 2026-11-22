import { fileURLToPath } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'
import vuetify from 'vite-plugin-vuetify'

// The legal app (فريق القانونية). Talks to the sales gateway only: api/legal-app/*.
export default defineConfig({
  base: './',
  plugins: [vue(), vuetify({ autoImport: true })],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  // Scan every component at startup so dependencies are bundled once, not mid-session.
  optimizeDeps: { entries: ['index.html', 'src/**/*.vue'] },
  server: { port: 5190 },
})
