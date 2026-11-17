import { defineConfig } from '@playwright/test'

// Capturas de las 11 pantallas diseñadas para docs/capturas. API con hora simulada (12:15, Monterrey) sobre la base de pruebas.
export default defineConfig({
  testDir: './e2e-capturas',
  timeout: 120_000,
  workers: 1,
  reporter: 'list',
  use: { baseURL: 'http://localhost:5175', locale: 'es-MX', timezoneId: 'America/Monterrey' },
  webServer: [
    { command: 'node scripts/e2e-api.mjs 5082 12:15 http://localhost:5175', url: 'http://localhost:5082/api/v1/salud', timeout: 300_000, reuseExistingServer: false, stdout: 'pipe' },
    { command: 'npx vite --port 5175 --strictPort', url: 'http://localhost:5175', timeout: 120_000, reuseExistingServer: false, env: { VITE_API_URL: 'http://localhost:5082' } },
  ],
})
