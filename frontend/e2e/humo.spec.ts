import { test } from '@playwright/test'
import { MOVIL, api, entrar, expect } from './utilidades'

// Recorre todas las pantallas con cada rol: sin errores de JavaScript, sin respuestas 5xx y sin respuestas 4xx inesperadas.
test.describe.configure({ mode: 'serial' })

async function recorrer(browser: any, correo: string, rutas: string[], opciones = {}) {
  const p = await entrar(browser, correo, opciones)
  const fallas: string[] = []
  p.on('pageerror', (e: Error) => fallas.push(`JS en ${p.url()}: ${e.message}`))
  p.on('response', (r: any) => {
    const u = r.url()
    if (!u.includes('/api/v1/')) return
    if (r.status() >= 500) fallas.push(`${r.status()} ${r.request().method()} ${u}`)
    else if (r.status() >= 400 && r.status() !== 401) fallas.push(`${r.status()} ${r.request().method()} ${u} (en ${p.url()})`)
  })
  for (const ruta of rutas) {
    await p.goto(ruta)
    await p.waitForLoadState('networkidle')
    await expect(p.locator('body')).not.toContainText('Falta la página')
  }
  await p.context().close()
  expect(fallas, fallas.join('\n')).toEqual([])
}

test('Humo: Recursos Humanos', async ({ browser }) => {
  const ana = (await api('rh@wowtime.mx', '/colaboradores?q=Ana%20Luc%C3%ADa')).items[0].id
  const inv = (await api('rh@wowtime.mx', '/visitantes/invitaciones?tamano=1')).items[0]?.id
  const sol = (await api('rh@wowtime.mx', '/solicitudes?tamano=1')).items[0]?.id
  const fichas = ['', '?tab=personales', '?tab=laborales', '?tab=asistencia', '?tab=vacaciones', '?tab=expediente', '?tab=historial'].map(t => `/colaboradores/${ana}${t}`)
  await recorrer(browser, 'rh@wowtime.mx', [
    '/', '/colaboradores', '/colaboradores?sinSupervisor=1', '/colaboradores/nuevo', ...fichas, `/colaboradores/${ana}/editar`,
    '/asistencia', '/asistencia?tab=incidencias', '/asistencia?tab=asignaciones',
    '/vacaciones', '/vacaciones?tab=solicitudes', '/vacaciones?tab=calendario', '/vacaciones?tab=politicas', `/solicitudes/${sol}`,
    '/visitantes', `/visitantes/${inv}`, '/control-acceso', '/expedientes', '/expedientes?tab=por-vencer',
    '/reportes', '/reportes?tipo=asistencia', '/configuracion', '/configuracion/organizacion', '/configuracion/organizacion?tab=flujos',
    ...['sucursales', 'areas', 'equipos', 'puestos', 'turnos', 'festivos', 'tipos-permiso', 'tipos-documento', 'dispositivos', 'mensajes', 'parametros', 'politicas'].map(x => `/configuracion/${x}`),
    '/aprobaciones', '/perfil', '/notificaciones', '/buscar?q=ana',
    '/mi-asistencia', '/mis-vacaciones', '/mi-expediente', '/mis-solicitudes', '/solicitar-permiso', '/mis-invitados', '/ruta-que-no-existe'
  ])
})

test('Humo: Administrador', async ({ browser }) => {
  await recorrer(browser, 'admin@wowtime.mx', ['/', ...['usuarios', 'roles', 'llaves-api', 'correos', 'tareas', 'bitacora', 'parametros'].map(x => `/configuracion/${x}`)])
})

test('Humo: Supervisora (web y teléfono)', async ({ browser }) => {
  const rutas = ['/', '/mi-asistencia', '/mis-vacaciones', '/mi-expediente', '/aprobaciones', '/mi-equipo', '/reportes', '/mis-solicitudes', '/solicitar-permiso', '/mis-invitados', '/perfil', '/notificaciones']
  await recorrer(browser, 'supervisor@wowtime.mx', rutas)
  await recorrer(browser, 'supervisor@wowtime.mx', [...rutas, '/m/asistencia', '/m/vacaciones', '/m/invitado'], MOVIL)
})

test('Humo: Colaborador y Recepción', async ({ browser }) => {
  await recorrer(browser, 'colaborador@wowtime.mx', ['/', '/mi-asistencia', '/mis-vacaciones', '/mi-expediente', '/mis-solicitudes', '/solicitar-permiso', '/perfil', '/m/asistencia', '/m/vacaciones', '/m/invitado'], MOVIL)
  await recorrer(browser, 'recepcion@wowtime.mx', ['/', '/control-acceso', '/visitantes', '/mis-invitados', '/perfil'])
})

test('Humo: páginas públicas', async ({ page }) => {
  const inv = (await api('rh@wowtime.mx', '/visitantes/invitaciones?tamano=1')).items[0]
  const det = await api('rh@wowtime.mx', `/visitantes/invitaciones/${inv.id}`)
  const errores: string[] = []
  page.on('pageerror', e => errores.push(e.message))
  for (const r of ['/login', '/recuperar', '/restablecer?token=x', '/quiosco', `/qr/${det.qr.split('.')[1]}`]) {
    await page.goto(r)
    await page.waitForLoadState('networkidle')
  }
  await expect(page.getByText(inv.nombre.split(' ')[0])).toBeVisible()
  expect(errores).toEqual([])
})
