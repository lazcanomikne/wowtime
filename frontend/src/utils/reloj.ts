import { onBeforeUnmount, onMounted, ref } from 'vue'
import { aFecha } from './formato'

/** Reloj de referencia sincronizado con la hora del servidor (la hora oficial de la checada la pone el servidor). */
export function useRelojServidor() {
  const ahora = ref(new Date())
  let desfase = 0
  let t: ReturnType<typeof setInterval>
  function sincronizar(horaServidorLocal?: string | null) {
    const d = aFecha(horaServidorLocal ?? null)
    if (d && !isNaN(d.getTime())) desfase = d.getTime() - Date.now()
    ahora.value = new Date(Date.now() + desfase)
  }
  onMounted(() => { t = setInterval(() => (ahora.value = new Date(Date.now() + desfase)), 1000) })
  onBeforeUnmount(() => clearInterval(t))
  return { ahora, sincronizar }
}
