// Utilidades compartidas por las secciones de Configuración.
import { ref } from 'vue'
import { api, ErrorApi, type Pagina } from '@/api'
import { useUi } from '@/stores/ui'

/** Días de la semana ISO (1 = lunes … 7 = domingo). */
export const DIAS_SEMANA = [
  { n: 1, c: 'L', nombre: 'Lunes' }, { n: 2, c: 'M', nombre: 'Martes' }, { n: 3, c: 'X', nombre: 'Miércoles' }, { n: 4, c: 'J', nombre: 'Jueves' },
  { n: 5, c: 'V', nombre: 'Viernes' }, { n: 6, c: 'S', nombre: 'Sábado' }, { n: 7, c: 'D', nombre: 'Domingo' },
]

export const mensajeError = (e: unknown) => e instanceof ErrorApi || e instanceof Error ? e.message : 'Ocurrió un error inesperado.'

/** Envío de formularios en modal: guarda el error del servidor para mostrarlo dentro del modal. */
export function useEnvio() {
  const ui = useUi()
  const ocupado = ref(false)
  const error = ref<string | null>(null)
  async function enviar<T>(fn: () => Promise<T>, exito?: string): Promise<{ ok: true; valor: T } | { ok: false }> {
    ocupado.value = true
    error.value = null
    try {
      const valor = await fn()
      if (exito) ui.avisar(exito)
      return { ok: true, valor }
    } catch (e) {
      error.value = mensajeError(e)
      return { ok: false }
    } finally { ocupado.value = false }
  }
  return { ocupado, error, enviar }
}

/** Pide confirmación y ejecuta una acción destructiva; muestra el error del servidor si la rechaza. */
export async function confirmarYEjecutar(opciones: { titulo: string; mensaje?: string; aceptar?: string; peligro?: boolean }, fn: () => Promise<unknown>, exito?: string) {
  const ui = useUi()
  const r = await ui.confirmar({ peligro: true, aceptar: 'Eliminar', ...opciones })
  if (!r.ok) return false
  try { await fn(); if (exito) ui.avisar(exito); return true } catch (e) { ui.error(e); return false }
}

/** Trae todas las filas de un catálogo paginado (hasta 500). */
export async function todos<T = any>(ruta: string, p: Record<string, string | number | boolean | null | undefined> = {}) {
  const r = await api.get<Pagina<T>>(ruta, { tamano: 500, ...p })
  return r.items
}

/** Copia un texto al portapapeles (con alternativa para navegadores sin permiso). */
export async function copiar(texto: string) {
  const ui = useUi()
  try {
    await navigator.clipboard.writeText(texto)
  } catch {
    const t = document.createElement('textarea')
    t.value = texto; t.style.position = 'fixed'; t.style.opacity = '0'
    document.body.appendChild(t); t.select()
    try { document.execCommand('copy') } catch { /* sin portapapeles */ }
    t.remove()
  }
  ui.avisar('Copiado al portapapeles')
}

/** "HH:mm" → "HH:mm:ss" para TimeOnly. */
export const aHoraApi = (v: string) => (v.length === 5 ? `${v}:00` : v)
