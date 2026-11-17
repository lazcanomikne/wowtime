// Captura rápida de una pantalla: node scripts/captura.mjs <correo> <ruta> <archivo.png> [ancho] [alto]
import { chromium } from '@playwright/test'

const [correo, ruta, archivo, ancho = '1536', alto = '1024'] = process.argv.slice(2)
const FRONT = process.env.FRONT_URL || 'http://localhost:5173'
const movil = Number(ancho) < 600
const navegador = await chromium.launch()
const pagina = await navegador.newPage({ viewport: { width: Number(ancho), height: Number(alto) }, deviceScaleFactor: movil ? 2 : 1, locale: 'es-MX', timezoneId: 'America/Monterrey', geolocation: { latitude: 25.68662, longitude: -100.31608 }, permissions: ['geolocation'] })
await pagina.goto(`${FRONT}/login`)
await pagina.fill('#email', correo)
await pagina.fill('#pwd', process.env.PWD_DEMO || 'WowTime2026!')
await pagina.click('[data-prueba="entrar"]')
await pagina.waitForURL(u => !u.pathname.startsWith('/login'))
await pagina.goto(`${FRONT}${ruta}`)
await pagina.waitForLoadState('networkidle')
await pagina.waitForTimeout(1200)
await pagina.screenshot({ path: archivo, fullPage: process.env.COMPLETA === '1' })
await navegador.close()
console.log('Captura:', archivo)
