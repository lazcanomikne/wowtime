// Cliente de la API pública /api/v1. El front no tiene rutas privilegiadas: todo pasa por aquí.
// Vacío = mismo origen (en local, Vite reenvía /api a la API). En Vercel apunta a la API publicada.
export const API_URL = (import.meta.env.VITE_API_URL || '').replace(/\/$/, '')
const BASE = `${API_URL}/api/v1`

export class ErrorApi extends Error {
  constructor(public status: number, public codigo: string, mensaje: string, public detalles?: any) { super(mensaje) }
}

interface Tokens { acceso: string; renovacion: string; expira: string }
const CLAVE = 'wt.sesion'

export const tokens = {
  leer(): Tokens | null {
    try { return JSON.parse(localStorage.getItem(CLAVE) || 'null') } catch { return null }
  },
  guardar(t: Tokens | null) {
    try { t ? localStorage.setItem(CLAVE, JSON.stringify(t)) : localStorage.removeItem(CLAVE) } catch { /* almacenamiento no disponible */ }
  }
}

let renovando: Promise<boolean> | null = null
export let alExpirar: () => void = () => {}
export function configurarExpiracion(fn: () => void) { alExpirar = fn }

async function renovar(): Promise<boolean> {
  const t = tokens.leer()
  if (!t?.renovacion) return false
  renovando ??= (async () => {
    try {
      const r = await fetch(`${BASE}/auth/refresh`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ tokenRenovacion: t.renovacion }) })
      if (!r.ok) return false
      const s = await r.json()
      tokens.guardar({ acceso: s.tokenAcceso, renovacion: s.tokenRenovacion, expira: s.expira })
      return true
    } catch { return false } finally { setTimeout(() => (renovando = null), 0) }
  })()
  return renovando
}

export function tokenAcceso() { return tokens.leer()?.acceso ?? '' }

type Params = Record<string, string | number | boolean | null | undefined>
export function qs(p?: Params) {
  if (!p) return ''
  const s = new URLSearchParams()
  for (const [k, v] of Object.entries(p)) if (v !== undefined && v !== null && v !== '') s.set(k, String(v))
  const r = s.toString()
  return r ? `?${r}` : ''
}

async function solicitud<T>(metodo: string, ruta: string, cuerpo?: unknown, opciones: { reintento?: boolean; crudo?: boolean } = {}): Promise<T> {
  const headers: Record<string, string> = {}
  const t = tokens.leer()
  if (t?.acceso) headers.Authorization = `Bearer ${t.acceso}`
  let body: BodyInit | undefined
  if (cuerpo instanceof FormData) body = cuerpo
  else if (cuerpo !== undefined) { headers['Content-Type'] = 'application/json'; body = JSON.stringify(cuerpo) }
  let r: Response
  try {
    r = await fetch(`${BASE}${ruta}`, { method: metodo, headers, body })
  } catch {
    throw new ErrorApi(0, 'sin_conexion', 'No se pudo conectar con el servidor. Revisa tu conexión e intenta de nuevo.')
  }
  if (r.status === 401 && opciones.reintento !== false && t?.renovacion && !ruta.startsWith('/auth/')) {
    if (await renovar()) return solicitud<T>(metodo, ruta, cuerpo, { ...opciones, reintento: false })
    tokens.guardar(null)
    alExpirar()
  }
  if (opciones.crudo) {
    if (!r.ok) await lanzar(r)
    return r as unknown as T
  }
  if (r.status === 204) return undefined as T
  if (!r.ok) await lanzar(r)
  const tipo = r.headers.get('content-type') || ''
  return (tipo.includes('json') ? await r.json() : await r.text()) as T
}

async function lanzar(r: Response): Promise<never> {
  let e: any = null
  try { e = (await r.json())?.error } catch { /* sin cuerpo */ }
  throw new ErrorApi(r.status, e?.codigo ?? 'error', e?.mensaje ?? `Error ${r.status}`, e?.detalles)
}

export const api = {
  get: <T = any>(ruta: string, p?: Params) => solicitud<T>('GET', ruta + qs(p)),
  post: <T = any>(ruta: string, cuerpo?: unknown) => solicitud<T>('POST', ruta, cuerpo ?? {}),
  put: <T = any>(ruta: string, cuerpo?: unknown) => solicitud<T>('PUT', ruta, cuerpo ?? {}),
  del: <T = any>(ruta: string) => solicitud<T>('DELETE', ruta),
  subir: <T = any>(ruta: string, datos: FormData) => solicitud<T>('POST', ruta, datos),
  /** Descarga un archivo (CSV, Excel) respetando el nombre que manda el servidor. */
  async descargar(ruta: string, p?: Params, nombre?: string) {
    const r = await solicitud<Response>('GET', ruta + qs(p), undefined, { crudo: true })
    const blob = await r.blob()
    const cd = r.headers.get('content-disposition') || ''
    const m = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(cd)
    const archivo = nombre || decodeURIComponent(m?.[1] || m?.[2] || 'descarga')
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = archivo; document.body.appendChild(a); a.click(); a.remove()
    setTimeout(() => URL.revokeObjectURL(url), 2000)
  }
}

/** URL absoluta para archivos firmados que devuelve la API (/api/v1/archivos/...). */
export function urlArchivo(ruta?: string | null) {
  if (!ruta) return undefined
  return ruta.startsWith('http') ? ruta : `${API_URL}${ruta}`
}

export interface Pagina<T> { items: T[]; total: number; paginaActual: number; tamano: number; paginas: number }
export interface Persona { id: number; numero: string; nombre: string; puesto?: string | null; area?: string | null; iniciales: string; fotoUrl?: string | null }
