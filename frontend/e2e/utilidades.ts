import { expect, type Browser, type BrowserContextOptions, type Page } from '@playwright/test'

export const API = 'http://localhost:5081/api/v1'
export const PWD = 'WowTime2026!'
export const MONTERREY = { latitude: 25.6694, longitude: -100.3097 }
export const MOVIL: BrowserContextOptions = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 2 }

/** Abre un contexto nuevo con sesión iniciada por la pantalla de login. */
export async function entrar(browser: Browser, correo: string, opciones: BrowserContextOptions = {}): Promise<Page> {
  const ctx = await browser.newContext({ locale: 'es-MX', timezoneId: 'America/Monterrey', geolocation: MONTERREY, permissions: ['geolocation'], ...opciones })
  const p = await ctx.newPage()
  await p.goto('/login')
  await p.fill('#email', correo)
  await p.fill('#pwd', PWD)
  await p.click('[data-prueba="entrar"]')
  await p.waitForURL(u => !u.pathname.startsWith('/login'))
  return p
}

/** Llamada directa a la API (para preparar datos o leer ids). */
export async function api(correo: string, ruta: string, metodo = 'GET', cuerpo?: unknown) {
  const login = await fetch(`${API}/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: correo, password: PWD }) })
  const { tokenAcceso } = await login.json()
  const r = await fetch(`${API}${ruta}`, { method: metodo, headers: { Authorization: `Bearer ${tokenAcceso}`, 'Content-Type': 'application/json' }, body: cuerpo ? JSON.stringify(cuerpo) : undefined })
  return r.status === 204 ? null : r.json()
}

export function iso(d: Date) { return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` }
export function lunesEn(dias: number) { const d = new Date(); d.setDate(d.getDate() + dias); while (d.getDay() !== 1) d.setDate(d.getDate() + 1); return d }

/** Avanza el calendario único hasta que el día esté visible y lo selecciona. */
export async function elegirDia(p: Page, fecha: string) {
  for (let i = 0; i < 14 && !(await p.locator(`.cal [data-fecha="${fecha}"]:not(.otro)`).count()); i++) await p.getByLabel('Mes siguiente').first().click()
  await p.locator(`.cal [data-fecha="${fecha}"]`).first().click()
}

export async function confirmar(p: Page) {
  await p.locator('[data-prueba="confirmar"]').click()
}

export { expect }
