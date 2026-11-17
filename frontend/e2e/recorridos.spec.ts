import { test } from '@playwright/test'
import { MOVIL, api, confirmar, elegirDia, entrar, expect, iso, lunesEn } from './utilidades'

test.describe.configure({ mode: 'serial' })

test('1. Un colaborador registra entrada y salida desde el teléfono', async ({ browser }) => {
  const p = await entrar(browser, 'maria.fernandez@wowtime.mx', MOVIL)
  await expect(p.locator('.inferior')).toBeVisible()
  await expect(p.getByText('Registrar entrada')).toBeVisible()
  await p.click('[data-prueba="boton-registrar"]')
  await expect(p.getByText('Ubicación validada')).toBeVisible()
  await expect(p.getByText('Tu ubicación está dentro del área autorizada')).toBeVisible()
  await p.click('[data-prueba="confirmar-registro"]')
  await expect(p.getByRole('heading', { name: 'Entrada registrada' })).toBeVisible()

  await p.goto('/')
  await expect(p.getByText('Registrar salida')).toBeVisible()
  await p.click('[data-prueba="boton-registrar"]')
  await expect(p.getByText('Ubicación validada')).toBeVisible()
  await p.click('[data-prueba="confirmar-registro"]')
  await expect(p.getByRole('heading', { name: 'Salida registrada' })).toBeVisible()
  await p.goto('/')
  await expect(p.locator('.registrar.hecho')).toContainText('Jornada concluida')
  await p.context().close()
})

test('2. Colaborador solicita vacaciones, su supervisora aprueba, RH aprueba y cambian saldo y calendario', async ({ browser }) => {
  const ini = lunesEn(70)
  const fin = new Date(ini); fin.setDate(ini.getDate() + 2)
  const saldoAntes = (await api('colaborador@wowtime.mx', '/vacaciones/saldo')).disponibles as number
  const sim = await api('colaborador@wowtime.mx', '/solicitudes/simular', 'POST', { tipo: 'Vacaciones', fechaInicio: iso(ini), fechaFin: iso(fin) })
  const habiles = sim.diasHabiles as number
  expect(habiles).toBeGreaterThan(0)

  // Sofía Herrera solicita desde el portal web
  const sofia = await entrar(browser, 'colaborador@wowtime.mx')
  await sofia.goto('/mis-vacaciones')
  await elegirDia(sofia, iso(ini))
  await elegirDia(sofia, iso(fin))
  await expect(sofia.getByText('Tienes saldo suficiente')).toBeVisible()
  await expect(sofia.locator('.habiles')).toContainText(`${habiles} ${habiles === 1 ? 'día hábil' : 'días hábiles'}`)
  const resp = sofia.waitForResponse(r => r.url().endsWith('/api/v1/solicitudes') && r.request().method() === 'POST')
  await sofia.click('[data-prueba="enviar-solicitud"]')
  const id = (await (await resp).json()).id as number
  await expect(sofia.locator('.aviso').filter({ hasText: /enviada/ })).toBeVisible()

  // La supervisora (Ana Lucía) aprueba desde su bandeja
  const ana = await entrar(browser, 'supervisor@wowtime.mx')
  await ana.goto('/aprobaciones')
  await ana.click(`[data-prueba="aprobar-${id}"]`)
  await confirmar(ana)
  await expect(ana.locator(`[data-prueba="aprobar-${id}"]`)).toHaveCount(0)

  // RH aprueba el paso final
  const rh = await entrar(browser, 'rh@wowtime.mx')
  await rh.goto(`/solicitudes/${id}`)
  await rh.click('[data-prueba="aprobar"]')
  await confirmar(rh)
  await expect(rh.getByText('Aprobada').first()).toBeVisible()

  // El saldo bajó 3 días y la ausencia aparece en el calendario de equipo
  const saldoDespues = (await api('colaborador@wowtime.mx', '/vacaciones/saldo')).disponibles as number
  expect(saldoDespues).toBe(saldoAntes - habiles)
  await sofia.goto('/mis-vacaciones')
  await expect(sofia.locator('.saldo .grande').first()).toHaveText(String(saldoDespues))
  await rh.goto(`/vacaciones?tab=calendario`)
  const hoy = new Date()
  const meses = (ini.getFullYear() - hoy.getFullYear()) * 12 + ini.getMonth() - hoy.getMonth()
  for (let i = 0; i < meses; i++) { await rh.getByLabel('Mes siguiente').first().click(); await rh.waitForLoadState('networkidle') }
  await expect(rh.locator('.fila-lt', { hasText: 'Sofía Herrera' }).locator('.barra-lt.apr')).toBeVisible()
  await Promise.all([sofia.context().close(), ana.context().close(), rh.context().close()])
})

test('3. Una supervisora solicita vacaciones y la ruta resulta Gerente y luego RH', async ({ browser }) => {
  const web = await entrar(browser, 'supervisor@wowtime.mx')
  await web.goto('/mis-vacaciones')
  const ruta = web.locator('.ruta')
  await expect(ruta.locator('.paso').nth(0)).toContainText('Carlos Méndez')
  await expect(ruta.locator('.paso').nth(1)).toContainText('Recursos Humanos')
  await web.context().close()

  const movil = await entrar(browser, 'supervisor@wowtime.mx', MOVIL)
  await movil.goto('/m/vacaciones')
  await expect(movil.getByText('Revisará: Gerente → RH')).toBeVisible()
  await movil.context().close()
})

test('4. Un anfitrión registra un invitado, Recepción lee el QR y RH lo ve Dentro', async ({ browser }) => {
  const nombre = `Invitado E2E ${Date.now() % 100000}`
  const anfitrion = await entrar(browser, 'supervisor@wowtime.mx', MOVIL)
  await anfitrion.goto('/m/invitado')
  await anfitrion.fill('#n', nombre)
  await anfitrion.fill('#e', 'Auditoría externa')
  const resp = anfitrion.waitForResponse(r => r.url().endsWith('/api/v1/visitantes/invitaciones') && r.request().method() === 'POST')
  await anfitrion.click('[data-prueba="generar-invitacion"]')
  const inv = await (await resp).json()
  await expect(anfitrion.getByText('Invitación generada')).toBeVisible()
  await expect(anfitrion.locator('canvas.qr')).toBeVisible()

  const recepcion = await entrar(browser, 'recepcion@wowtime.mx')
  await recepcion.goto('/control-acceso')
  await recepcion.fill('#codigo', inv.qr)
  await recepcion.click('[data-prueba="validar-codigo"]')
  await expect(recepcion.locator('[data-prueba="resultado-acceso"]')).toHaveText('Entrada registrada')

  const rh = await entrar(browser, 'rh@wowtime.mx')
  await rh.goto('/visitantes')
  await expect(rh.locator('tr', { hasText: nombre }).getByText('Dentro')).toBeVisible()
  await Promise.all([anfitrion.context().close(), recepcion.context().close(), rh.context().close()])
})

test('5. RH sube un documento con vigencia y aparece la alerta de vencimiento', async ({ browser }) => {
  const lista = await api('rh@wowtime.mx', '/colaboradores?q=Jorge%20L%C3%B3pez')
  const jorge = lista.items.find((c: any) => c.nombre === 'Jorge López')
  const vence = new Date(); vence.setDate(vence.getDate() + 5)

  const rh = await entrar(browser, 'rh@wowtime.mx')
  await rh.goto(`/colaboradores/${jorge.id}?tab=expediente`)
  const fila = rh.locator('.docs-lista li', { has: rh.locator('b', { hasText: /^INE$/ }) })
  await fila.getByRole('button', { name: /Subir|Reemplazar/ }).click()
  await rh.locator('.modal input[type="file"]').setInputFiles({ name: 'ine.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\n%prueba e2e\n') })
  await rh.fill('#sd-vence', iso(vence))
  await rh.locator('.modal').getByRole('button', { name: 'Subir documento' }).click()
  await expect(fila.getByText('Urgente')).toBeVisible()

  await rh.goto('/expedientes?tab=por-vencer')
  await expect(rh.locator('tr', { hasText: 'Jorge López' }).filter({ hasText: 'INE' }).getByText('Urgente')).toBeVisible()
  await rh.goto(`/colaboradores/${jorge.id}`)
  await expect(rh.locator('.alerta-doc')).toContainText('INE vence en 5 días')
  await rh.context().close()
})
