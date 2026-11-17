import { test, type Browser, type BrowserContextOptions, type Page } from '@playwright/test'
import path from 'node:path'

// Genera docs/capturas/<Mockup>.png para comparar cada pantalla diseñada con su imagen de docs/mockups.
const DESTINO = path.resolve(process.cwd(), '../docs/capturas')
const API = 'http://localhost:5082/api/v1'
const ESCRITORIO: BrowserContextOptions = { viewport: { width: 1536, height: 1024 } }
const MOVIL: BrowserContextOptions = { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 2 }
const hoy = new Date()
const mediodia = new Date(`${hoy.getFullYear()}-${String(hoy.getMonth() + 1).padStart(2, '0')}-${String(hoy.getDate()).padStart(2, '0')}T12:15:00-06:00`)

async function entrar(browser: Browser, correo: string, op: BrowserContextOptions, geo = { latitude: 25.68662, longitude: -100.31608 }): Promise<Page> {
  const ctx = await browser.newContext({ locale: 'es-MX', timezoneId: 'America/Monterrey', geolocation: geo, permissions: ['geolocation'], ...op })
  const p = await ctx.newPage()
  await p.clock.setFixedTime(mediodia)
  await p.goto('/login')
  await p.fill('#email', correo)
  await p.fill('#pwd', 'WowTime2026!')
  await p.click('[data-prueba="entrar"]')
  await p.waitForURL(u => !u.pathname.startsWith('/login'))
  return p
}
async function guardar(p: Page, nombre: string) {
  await p.waitForLoadState('networkidle')
  await p.waitForTimeout(1500)
  await p.screenshot({ path: path.join(DESTINO, `${nombre}.png`) })
}
async function idDe(correo: string, q: string) {
  const t = (await (await fetch(`${API}/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: correo, password: 'WowTime2026!' }) })).json()).tokenAcceso
  const r = await (await fetch(`${API}/colaboradores?q=${encodeURIComponent(q)}`, { headers: { Authorization: `Bearer ${t}` } })).json()
  return r.items.find((c: any) => c.nombre === q).id as number
}
function lunesProximo(semanas = 1) { const d = new Date(); d.setDate(d.getDate() + 7 * semanas); while (d.getDay() !== 1) d.setDate(d.getDate() + 1); return d }
const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
async function elegir(p: Page, f: string) {
  for (let i = 0; i < 6 && !(await p.locator(`.cal [data-fecha="${f}"]:not(.otro)`).count()); i++) await p.getByLabel('Mes siguiente').first().click()
  await p.locator(`.cal [data-fecha="${f}"]`).first().click()
}

test('Capturas de escritorio', async ({ browser }) => {
  const rh = await entrar(browser, 'rh@wowtime.mx', ESCRITORIO)
  await rh.goto('/')
  await guardar(rh, 'Dashboard')
  await rh.goto(`/colaboradores/${await idDe('rh@wowtime.mx', 'Ana Lucía Martínez')}`)
  await guardar(rh, 'Ficha_colaborador')
  await rh.goto('/vacaciones')
  await guardar(rh, 'Portal_Autorizar_Vacaciones')
  await rh.goto('/configuracion/organizacion')
  await guardar(rh, 'Organigrama_Autorizaciones')
  await rh.context().close()

  const ana = await entrar(browser, 'supervisor@wowtime.mx', ESCRITORIO)
  await ana.goto('/mis-vacaciones')
  const ini = lunesProximo(3)
  const fin = new Date(ini); fin.setDate(ini.getDate() + 4)
  await elegir(ana, iso(ini)); await elegir(ana, iso(fin))
  await ana.mouse.move(5, 5)
  await guardar(ana, 'Solicitud_de_Vacaciones')
  await ana.context().close()
})

test('Capturas móviles', async ({ browser }) => {
  const m = await entrar(browser, 'supervisor@wowtime.mx', MOVIL)
  await m.goto('/')
  await guardar(m, 'App_Movil_1_Inicio')
  await m.goto('/m/asistencia')
  await m.getByText('Ubicación validada').waitFor()
  await guardar(m, 'App_Movil_2_Registrar_asistencia')
  await m.goto('/m/invitado')
  await guardar(m, 'App_Movil_3_Registrar_invitado')
  await m.goto('/m/vacaciones')
  await guardar(m, 'App_Movil_4_Solicitar_vacaciones')
  const ini = lunesProximo(3)
  const fin = new Date(ini); fin.setDate(ini.getDate() + 4)
  await elegir(m, iso(ini)); await elegir(m, iso(fin))
  await m.getByText('Saldo después').waitFor()
  await guardar(m, 'Solicitar_vacaciones')
  await m.goto('/m/invitado')
  await m.fill('#n', 'Carlos Andrés Ramírez')
  await m.fill('#e', 'Innovación S.A. / Reunión de trabajo')
  await m.fill('#t', '8112345678')
  await m.click('[data-prueba="generar-invitacion"]')
  await m.getByText('Invitación generada').waitFor()
  await guardar(m, 'Registro_de_invitados')
  await m.context().close()
})
