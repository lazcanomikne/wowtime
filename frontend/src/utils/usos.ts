import { onBeforeUnmount, onMounted, ref, watch, type Ref } from 'vue'
import { API_URL, tokenAcceso } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'

/** true en pantallas de teléfono (< 768 px). */
export function useEsMovil() {
  const mq = typeof window !== 'undefined' ? window.matchMedia('(max-width: 767px)') : null
  const esMovil = ref(mq?.matches ?? false)
  const f = (e: MediaQueryListEvent) => (esMovil.value = e.matches)
  onMounted(() => mq?.addEventListener('change', f))
  onBeforeUnmount(() => mq?.removeEventListener('change', f))
  return esMovil
}

/** Carga datos con estado de carga y error. Se recarga al cambiar la sucursal si `porSucursal`. */
export function useCarga<T>(fn: () => Promise<T>, opciones: { porSucursal?: boolean; inmediato?: boolean } = {}) {
  const datos = ref<T | null>(null) as Ref<T | null>
  const cargando = ref(false)
  const error = ref<string | null>(null)
  const ui = useUi()
  async function cargar(silencioso = false) {
    if (!silencioso) cargando.value = true
    error.value = null
    try { datos.value = await fn() } catch (e: any) { error.value = e?.message ?? 'Error'; if (!silencioso) ui.error(e) } finally { cargando.value = false }
  }
  if (opciones.porSucursal) {
    const s = useSesion()
    watch(() => s.sucursalId, () => cargar())
  }
  if (opciones.inmediato !== false) onMounted(() => cargar())
  return { datos, cargando, error, cargar }
}

/** Escucha el canal de eventos del servidor (SSE) de la sucursal seleccionada. */
export function useEventos(manejar: (tipo: string, datos: any) => void) {
  const s = useSesion()
  let fuente: EventSource | null = null
  let reintento: ReturnType<typeof setTimeout> | null = null
  function conectar() {
    cerrar()
    if (!s.sucursalId || !tokenAcceso()) return
    fuente = new EventSource(`${API_URL}/api/v1/eventos/sucursal/${s.sucursalId}?token=${encodeURIComponent(tokenAcceso())}`)
    for (const t of ['asistencia', 'solicitud', 'visitante', 'incidencia', 'ausencia', 'notificacion'])
      fuente.addEventListener(t, (e: MessageEvent) => { try { manejar(t, JSON.parse(e.data)) } catch { manejar(t, null) } })
    fuente.onerror = () => { cerrar(); reintento = setTimeout(conectar, 15000) }
  }
  function cerrar() { fuente?.close(); fuente = null; if (reintento) clearTimeout(reintento) }
  onMounted(conectar)
  watch(() => s.sucursalId, conectar)
  onBeforeUnmount(cerrar)
}

/** Ejecuta una acción con aviso de éxito y manejo de error. */
export function useAccion() {
  const ui = useUi()
  const ocupado = ref(false)
  async function ejecutar<T>(fn: () => Promise<T>, exito?: string): Promise<T | undefined> {
    ocupado.value = true
    try { const r = await fn(); if (exito) ui.avisar(exito); return r } catch (e) { ui.error(e); return undefined } finally { ocupado.value = false }
  }
  return { ocupado, ejecutar }
}

export function debounce<A extends unknown[]>(fn: (...a: A) => void, ms = 300) {
  let t: ReturnType<typeof setTimeout> | undefined
  return (...a: A) => { if (t) clearTimeout(t); t = setTimeout(() => fn(...a), ms) }
}
