import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ErrorApi } from '@/api'

export interface Aviso { id: number; tipo: 'ok' | 'error' | 'info'; texto: string }
export interface Confirmacion {
  titulo: string; mensaje?: string; aceptar?: string; cancelar?: string; peligro?: boolean
  /** Si se define, pide un texto (por ejemplo, el motivo del rechazo). */
  pedirTexto?: { etiqueta: string; obligatorio?: boolean; placeholder?: string }
  resolver: (r: { ok: boolean; texto?: string }) => void
}

export const useUi = defineStore('ui', () => {
  const avisos = ref<Aviso[]>([])
  const confirmacion = ref<Confirmacion | null>(null)
  const menuAbierto = ref(false)
  let n = 0

  function avisar(texto: string, tipo: Aviso['tipo'] = 'ok', ms = 4200) {
    const id = ++n
    avisos.value.push({ id, tipo, texto })
    setTimeout(() => (avisos.value = avisos.value.filter(a => a.id !== id)), ms)
  }
  function error(e: unknown) {
    const msg = e instanceof ErrorApi ? e.message : e instanceof Error ? e.message : 'Ocurrió un error inesperado.'
    avisar(msg, 'error', 6000)
  }
  function confirmar(c: Omit<Confirmacion, 'resolver'>) {
    return new Promise<{ ok: boolean; texto?: string }>(resolver => { confirmacion.value = { ...c, resolver } })
  }
  function cerrarConfirmacion(r: { ok: boolean; texto?: string }) {
    confirmacion.value?.resolver(r)
    confirmacion.value = null
  }
  return { avisos, confirmacion, menuAbierto, avisar, error, confirmar, cerrarConfirmacion }
})
