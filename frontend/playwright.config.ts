import { defineConfig, devices } from '@playwright/test'

// Pruebas de punta a punta: API de pruebas en 5081 (base wowtime_test recargada) y front en 5174.
export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'http://localhost:5174',
    locale: 'es-MX',
    timezoneId: 'America/Monterrey',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 960 } } }],
  webServer: [
    { command: 'node scripts/e2e-api.mjs', url: 'http://localhost:5081/api/v1/salud', timeout: 300_000, reuseExistingServer: false, stdout: 'pipe' },
    { command: 'npx vite --port 5174 --strictPort', url: 'http://localhost:5174', timeout: 120_000, reuseExistingServer: false, env: { VITE_API_URL: 'http://localhost:5081' } },
  ],
})
