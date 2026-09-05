import vue from '@vitejs/plugin-vue'
import vueJsx from '@vitejs/plugin-vue-jsx'
import http from 'node:http'
import { fileURLToPath } from 'node:url'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { VueRouterAutoImports, getPascalCaseRouteName } from 'unplugin-vue-router'
import VueRouter from 'unplugin-vue-router/vite'
import { defineConfig } from 'vite'
import Layouts from 'vite-plugin-vue-layouts'
import vuetify from 'vite-plugin-vuetify'
import svgLoader from 'vite-svg-loader'

/** Same key the working Demo BE_Company curl uses. Dev-server only — not shipped in the browser bundle. */
const DEMO_COMPANY_GATEWAY_KEY = 'SalesEmployee-Gateway-2026'
const DEMO_COMPANY_TARGETS = [
  { host: '127.0.0.1', port: 5401 },
  { host: '169.58.236.52', port: 8080 },
]

function demoCompanyProxy() {
  return {
    name: 'demo-company-proxy',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        if (!req.url?.startsWith('/demo-api'))
          return next()

        let destPath = req.url.replace(/^\/demo-api(?=\/|$)/, '') || '/'
        if (!destPath.startsWith('/api'))
          destPath = `/api${destPath.startsWith('/') ? destPath : `/${destPath}`}`
        while (destPath.includes('/api/api'))
          destPath = destPath.replace('/api/api', '/api')

        const chunks = []
        req.on('data', chunk => chunks.push(chunk))
        req.on('error', () => {
          if (res.headersSent || res.writableEnded)
            return
          res.statusCode = 502
          res.setHeader('Content-Type', 'application/json; charset=utf-8')
          res.end(JSON.stringify({ message: 'تعذر قراءة الطلب.' }))
        })
        req.on('end', () => {
          const body = Buffer.concat(chunks)
          let settled = false

          const fail = () => {
            if (settled || res.headersSent || res.writableEnded)
              return
            settled = true
            res.statusCode = 502
            res.setHeader('Content-Type', 'application/json; charset=utf-8')
            res.end(JSON.stringify({
              message: 'تعذر الاتصال بـ Demo BE_Company على المنفذ 5401.',
            }))
          }

          const tryTarget = index => {
            if (settled || res.headersSent || res.writableEnded)
              return
            if (index >= DEMO_COMPANY_TARGETS.length) {
              fail()

              return
            }

            const target = DEMO_COMPANY_TARGETS[index]
            const headers = { ...req.headers }
            delete headers.host
            delete headers.connection
            headers.host = `${target.host}:${target.port}`
            if (target.port === 5401)
              headers['x-sales-gateway-key'] = DEMO_COMPANY_GATEWAY_KEY
            else
              delete headers['x-sales-gateway-key']
            headers['content-length'] = String(body.length)

            const proxyReq = http.request({
              host: target.host,
              port: target.port,
              path: destPath,
              method: req.method,
              headers,
              timeout: 8000,
            }, proxyRes => {
              const canRetry = index + 1 < DEMO_COMPANY_TARGETS.length
                && proxyRes.statusCode === 404
              if (canRetry) {
                proxyRes.resume()
                tryTarget(index + 1)

                return
              }
              if (settled || res.headersSent || res.writableEnded) {
                proxyRes.resume()

                return
              }
              settled = true
              res.statusCode = proxyRes.statusCode || 502
              for (const [name, value] of Object.entries(proxyRes.headers)) {
                if (value == null || name.toLowerCase() === 'transfer-encoding')
                  continue
                res.setHeader(name, value)
              }
              proxyRes.pipe(res)
            })

            proxyReq.on('error', () => {
              if (settled || res.headersSent || res.writableEnded)
                return
              tryTarget(index + 1)
            })
            proxyReq.on('timeout', () => {
              if (settled || res.headersSent || res.writableEnded)
                return
              proxyReq.destroy()
              tryTarget(index + 1)
            })
            proxyReq.end(body)
          }

          tryTarget(0)
        })
      })
    },
  }
}

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    // Docs: https://github.com/posva/unplugin-vue-router
    // ℹ️ This plugin should be placed before vue plugin
    demoCompanyProxy(),
    VueRouter({
      getRouteName: routeNode => {
        // Convert pascal case to kebab case
        return getPascalCaseRouteName(routeNode)
          .replace(/([a-z\d])([A-Z])/g, '$1-$2')
          .toLowerCase()
      },
      dts: false,
    }),
    vue({
      template: {
        compilerOptions: {
          isCustomElement: tag => tag === 'swiper-container' || tag === 'swiper-slide',
        },
      },
    }),

    // VueDevTools(),
    vueJsx(),

    // Docs: https://github.com/vuetifyjs/vuetify-loader/tree/master/packages/vite-plugin
    vuetify({
      styles: {
        configFile: 'src/assets/styles/variables/_vuetify.scss',
      },
    }),

    // Docs: https://github.com/johncampionjr/vite-plugin-vue-layouts#vite-plugin-vue-layouts
    Layouts({
      layoutsDirs: './src/layouts/',
    }),

    // Docs: https://github.com/antfu/unplugin-vue-components#unplugin-vue-components
    Components({
      dirs: ['src/@core/components', 'src/views/demos', 'src/components'],
      dts: false,
      resolvers: [
        componentName => {
          // Auto import `VueApexCharts`
          if (componentName === 'VueApexCharts')
            return { name: 'default', from: 'vue3-apexcharts', as: 'VueApexCharts' }
        },
      ],
    }),

    // Docs: https://github.com/antfu/unplugin-auto-import#unplugin-auto-import
    AutoImport({
      imports: ['vue', VueRouterAutoImports, '@vueuse/core', '@vueuse/math', 'vue-i18n', 'pinia'],
      dirs: [
        './src/@core/utils',
        './src/@core/composable/',
        './src/composables/',
        './src/utils/',
        './src/plugins/*/composables/*',
      ],
      vueTemplate: true,

      // ℹ️ Disabled to avoid confusion & accidental usage
      ignore: ['useCookies', 'useStorage'],
      eslintrc: {
        enabled: false,
        filepath: './.eslintrc-auto-import.json',
      },
      dts: false,
    }),
    svgLoader(),
  ],
  define: { 'process.env': {} },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@themeConfig': fileURLToPath(new URL('./themeConfig.js', import.meta.url)),
      '@core': fileURLToPath(new URL('./src/@core', import.meta.url)),
      '@layouts': fileURLToPath(new URL('./src/@layouts', import.meta.url)),
      '@images': fileURLToPath(new URL('./src/assets/images/', import.meta.url)),
      '@styles': fileURLToPath(new URL('./src/assets/styles/', import.meta.url)),
      '@configured-variables': fileURLToPath(new URL('./src/assets/styles/variables/_template.scss', import.meta.url)),
      '@db': fileURLToPath(new URL('./src/plugins/fake-api/handlers/', import.meta.url)),
      '@api-utils': fileURLToPath(new URL('./src/plugins/fake-api/utils/', import.meta.url)),
    },
  },
  build: {
    chunkSizeWarningLimit: 5000,
  },
  optimizeDeps: {
    exclude: ['vuetify'],
    entries: [
      './src/**/*.vue',
    ],
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api-defaultdata': {
        target: 'http://defaultdata.alsaaeidy.com',
        changeOrigin: true,
        rewrite: path => path.replace(/^\/api-defaultdata/, ''),
      },
      '/sales-gw': {
        target: 'http://127.0.0.1:5280',
        changeOrigin: true,
        rewrite: path => path.replace(/^\/sales-gw/, ''),
      },
    },
    watch: {
      ignored: ['**/typed-router.d.ts', '**/auto-imports.d.ts', '**/components.d.ts', '**/.eslintrc-auto-import.json'],
    },
  },
})
