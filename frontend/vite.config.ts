import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [
    vue(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg', 'favicon-32.png', 'apple-touch-icon.png'],
      manifest: {
        name: 'WOW! Time',
        short_name: 'WOW! Time',
        description: 'Asistencia, vacaciones, visitantes y expediente de tu equipo.',
        lang: 'es-MX',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        orientation: 'any',
        background_color: '#F8F9FC',
        theme_color: '#102848',
        categories: ['business', 'productivity'],
        icons: [
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          { src: 'maskable-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' }
        ],
        shortcuts: [
          { name: 'Registrar asistencia', url: '/m/asistencia', icons: [{ src: 'pwa-192x192.png', sizes: '192x192' }] },
          { name: 'Solicitar vacaciones', url: '/m/vacaciones', icons: [{ src: 'pwa-192x192.png', sizes: '192x192' }] }
        ]
      },
      workbox: {
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api\//],
        globPatterns: ['**/*.{js,css,html,svg,png,woff2}'],
        runtimeCaching: [
          {
            // Últimos datos para abrir sin conexión (solo lecturas)
            urlPattern: ({ url, request }) => url.pathname.startsWith('/api/v1/') && request.method === 'GET' && !url.pathname.includes('/eventos/'),
            handler: 'NetworkFirst',
            options: { cacheName: 'api-lecturas', networkTimeoutSeconds: 6, expiration: { maxEntries: 300, maxAgeSeconds: 60 * 60 * 24 * 3 } }
          },
          {
            urlPattern: ({ url }) => url.hostname.endsWith('tile.openstreetmap.org'),
            handler: 'CacheFirst',
            options: { cacheName: 'mapa', expiration: { maxEntries: 400, maxAgeSeconds: 60 * 60 * 24 * 14 } }
          }
        ]
      },
      devOptions: { enabled: false }
    })
  ],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: { port: 5173, host: true }
})
