// Genera el favicon y los íconos de la PWA a partir del logo SVG (src/assets/marca).
// Uso: npm run iconos
import sharp from 'sharp'
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs'

const NAVY = '#102848'
const marca = new URL('../src/assets/marca/', import.meta.url)
const pub = new URL('../public/', import.meta.url)
mkdirSync(pub, { recursive: true })

const wow = readFileSync(new URL('wow-blanco.svg', marca))

async function icono(tam, archivo, { margen = 0.16, radio = 0.22, fondoCompleto = false } = {}) {
  const ancho = Math.round(tam * (1 - margen * 2))
  const marcaPng = await sharp(wow, { density: 600 }).resize({ width: ancho }).png().toBuffer()
  const r = fondoCompleto ? 0 : Math.round(tam * radio)
  const fondo = Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${tam}" height="${tam}"><rect width="${tam}" height="${tam}" rx="${r}" fill="${NAVY}"/></svg>`)
  await sharp(fondo).composite([{ input: marcaPng, gravity: 'center' }]).png().toFile(new URL(archivo, pub).pathname.replace(/^\/([A-Z]:)/, '$1'))
}

await icono(192, 'pwa-192x192.png')
await icono(512, 'pwa-512x512.png')
await icono(512, 'maskable-512x512.png', { margen: 0.22, fondoCompleto: true })
await icono(180, 'apple-touch-icon.png', { margen: 0.14, fondoCompleto: true })
await icono(64, 'favicon-64.png', { margen: 0.08, radio: 0.2 })
await icono(32, 'favicon-32.png', { margen: 0.06, radio: 0.2 })

// Favicon SVG: la marca "WOW!" sobre fondo azul marino
const wowSvg = wow.toString()
const vb = /viewBox="([^"]+)"/.exec(wowSvg)[1].split(' ').map(Number)
const contenido = wowSvg.replace(/^<svg[^>]*>/, '').replace(/<\/svg>\s*$/, '')
const lado = vb[2] * 1.16
const fav = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${lado} ${lado}"><rect width="${lado}" height="${lado}" rx="${lado * 0.2}" fill="${NAVY}"/><g transform="translate(${(lado - vb[2]) / 2 - vb[0]} ${(lado - vb[3]) / 2 - vb[1]})">${contenido}</g></svg>`
writeFileSync(new URL('favicon.svg', pub), fav)
console.log('Íconos generados en public/')
