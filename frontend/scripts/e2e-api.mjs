// Levanta la API para pruebas o capturas contra la base de pruebas: usa ConnectionStrings__Pruebas del .env del back,
// recarga la semilla y escucha en el puerto indicado (5081 por omisión).
// Uso: node scripts/e2e-api.mjs [puerto] [HH:mm simulada] [origen del front]
import { spawn, spawnSync } from 'node:child_process'
import { readFileSync, existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const [puerto = '5081', horaSimulada, origen = 'http://localhost:5174'] = process.argv.slice(2)
const raiz = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../backend/WowTime.Api')
const env = { ...process.env }
const archivo = path.join(raiz, '.env')
if (existsSync(archivo))
  for (const l of readFileSync(archivo, 'utf8').split(/\r?\n/)) {
    const i = l.indexOf('=')
    if (i > 0 && !l.trim().startsWith('#')) env[l.slice(0, i).trim()] ??= l.slice(i + 1).trim()
  }
if (!env.ConnectionStrings__Pruebas) { console.error('Falta ConnectionStrings__Pruebas en backend/WowTime.Api/.env'); process.exit(1) }
env.ConnectionStrings__Default = env.ConnectionStrings__Pruebas
env.Cors__Origenes = origen
if (horaSimulada) { const d = new Date(); env.Reloj__Simular = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}T${horaSimulada}:00` }
env.Tareas__Deshabilitar = 'true'
env.ASPNETCORE_ENVIRONMENT = 'Development'
env.Archivos__Ruta = path.join(raiz, '..', '..', '..', 'wowtime-data', 'archivos-pruebas')

// Compila en una carpeta aparte para no chocar con la API de desarrollo en ejecución
const salida = path.join(raiz, 'bin', `e2e-${puerto}`)
const b = spawnSync('dotnet', ['build', '-c', 'Debug', '-o', salida], { cwd: raiz, env, stdio: 'inherit', shell: true })
if (b.status !== 0) process.exit(b.status ?? 1)
const dll = path.join(salida, 'WowTime.Api.dll')
const r = spawnSync('dotnet', [dll, 'semilla', '--reiniciar'], { cwd: raiz, env, stdio: 'inherit', shell: true })
if (r.status !== 0) process.exit(r.status ?? 1)
const api = spawn('dotnet', [dll, '--urls', `http://localhost:${puerto}`], { cwd: raiz, env, stdio: 'inherit', shell: true })
const fin = () => { api.kill(); process.exit(0) }
process.on('SIGINT', fin); process.on('SIGTERM', fin)
