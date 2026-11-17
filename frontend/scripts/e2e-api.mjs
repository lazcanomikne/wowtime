// Levanta la API para las pruebas de punta a punta contra la base de pruebas:
// usa ConnectionStrings__Pruebas del .env del back, recarga la semilla y escucha en el puerto 5081.
import { spawn, spawnSync } from 'node:child_process'
import { readFileSync, existsSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

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
env.Cors__Origenes = 'http://localhost:5174'
env.Tareas__Deshabilitar = 'true'
env.ASPNETCORE_ENVIRONMENT = 'Development'
env.Archivos__Ruta = path.join(raiz, '..', '..', '..', 'wowtime-data', 'archivos-pruebas')

// Compila en una carpeta aparte para no chocar con la API de desarrollo en ejecución
const salida = path.join(raiz, 'bin', 'e2e')
const b = spawnSync('dotnet', ['build', '-c', 'Debug', '-o', salida], { cwd: raiz, env, stdio: 'inherit', shell: true })
if (b.status !== 0) process.exit(b.status ?? 1)
const dll = path.join(salida, 'WowTime.Api.dll')
const r = spawnSync('dotnet', [dll, 'semilla', '--reiniciar'], { cwd: raiz, env, stdio: 'inherit', shell: true })
if (r.status !== 0) process.exit(r.status ?? 1)
const api = spawn('dotnet', [dll, '--urls', 'http://localhost:5081'], { cwd: raiz, env, stdio: 'inherit', shell: true })
const fin = () => { api.kill(); process.exit(0) }
process.on('SIGINT', fin); process.on('SIGTERM', fin)
