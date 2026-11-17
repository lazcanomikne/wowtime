<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useCarga } from '@/utils/usos'
import { fechaCorta, momento } from '@/utils/formato'
import { mensajeError } from './comun'

interface Ejecucion { id: number; tarea: string; fecha: string; inicio: string; fin: string | null; resultado: string | null }

const s = useSesion()
const ui = useUi()
const pagina = ref(1)
const { datos, cargando, cargar } = useCarga(() => api.get<{ nombres: string[]; ejecuciones: Pagina<Ejecucion> }>('/tareas', { pagina: pagina.value, tamano: 20 }))
/** Última ejecución de cada tarea (se consulta aparte para no depender de la página del historial). */
const ultimas = ref<Record<string, Ejecucion>>({})
async function cargarUltimas() {
  try {
    const r = await api.get<{ ejecuciones: Pagina<Ejecucion> }>('/tareas', { tamano: 200 })
    const m: Record<string, Ejecucion> = {}
    for (const e of r.ejecuciones.items) if (!m[e.tarea]) m[e.tarea] = e
    ultimas.value = m
  } catch { /* se muestra sin última ejecución */ }
}
cargarUltimas()
function ir(p: number) { pagina.value = p; cargar() }

const NOMBRES: Record<string, string> = {
  asignaciones: 'Asignaciones temporales', qr: 'Vigencia de QR de visitantes', saldos: 'Otorgamiento de saldos de vacaciones', vencimiento_dias: 'Vencimiento de días de vacaciones',
  documentos: 'Documentos por vencer', cumpleanos: 'Cumpleaños', cierre_asistencia: 'Cierre diario de asistencia',
}
const nombre = (t: string) => NOMBRES[t] ?? t
const items = computed(() => datos.value?.ejecuciones.items ?? [])
const duracion = (e: Ejecucion) => {
  if (!e.fin) return 'En curso'
  const ms = new Date(e.fin).getTime() - new Date(e.inicio).getTime()
  return ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1)} s`
}

const ejecutando = reactive<Record<string, boolean>>({})
const resultados = ref<Record<string, string> | null>(null)
const puedeEjecutar = computed(() => s.tiene('sistema.configurar'))
async function ejecutar(tarea?: string) {
  const k = tarea ?? '*'
  ejecutando[k] = true
  try {
    const r = await api.post<Record<string, string>>(`/tareas/ejecutar${tarea ? `?tarea=${encodeURIComponent(tarea)}` : ''}`)
    resultados.value = r
    ui.avisar(tarea ? `${nombre(tarea)}: ${r[tarea] ?? 'ejecutada'}` : 'Tareas ejecutadas')
    pagina.value = 1
    await Promise.all([cargar(true), cargarUltimas()])
  } catch (e) {
    ui.avisar(mensajeError(e), 'error', 6000)
  } finally { ejecutando[k] = false }
}
</script>

<template>
  <div class="columna" style="gap: 20px">
    <section class="tarjeta">
      <div class="tarjeta-cabecera">
        <div>
          <h2 class="titulo-tarjeta">Tareas</h2>
          <p class="subtitulo-tarjeta">Corren al arrancar la API y cada hora. Todas son seguras de repetir.</p>
        </div>
        <button v-if="puedeEjecutar" class="btn btn-primario" :disabled="ejecutando['*']" data-prueba="ejecutar-todas" @click="ejecutar()">
          <span v-if="ejecutando['*']" class="spinner" style="width: 16px; height: 16px; border-top-color: #fff" /><Icono v-else n="actualizar" :t="18" />Ejecutar todas
        </button>
      </div>
      <div class="lista">
        <div v-for="t in datos?.nombres" :key="t" class="tarea">
          <span class="ico"><Icono n="reloj" :t="20" /></span>
          <div class="crecer">
            <div class="negrita">{{ nombre(t) }}</div>
            <div class="muted pequeno">
              <code>{{ t }}</code>
              <template v-if="ultimas[t]"> · Última vez {{ momento(ultimas[t].inicio) }} · {{ ultimas[t].resultado }}</template>
              <template v-else> · Sin ejecuciones registradas</template>
            </div>
            <div v-if="resultados?.[t]" class="resultado"><Icono n="checkCirculo" :t="15" />{{ resultados[t] }}</div>
          </div>
          <button v-if="puedeEjecutar" class="btn btn-contorno btn-ch" :disabled="ejecutando[t] || ejecutando['*']" :data-prueba="`ejecutar-${t}`" @click="ejecutar(t)">
            <span v-if="ejecutando[t]" class="spinner" style="width: 14px; height: 14px" />Ejecutar ahora
          </button>
        </div>
        <div v-if="cargando && !datos" class="esqueleto" style="height: 120px" />
      </div>
    </section>

    <section class="tarjeta">
      <div class="tarjeta-cabecera">
        <h2 class="titulo-tarjeta">Historial de ejecuciones</h2>
        <span class="muted pequeno">Más recientes primero</span>
      </div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead><tr><th>Tarea</th><th>Día</th><th>Inicio</th><th>Duración</th><th>Resultado</th></tr></thead>
          <tbody>
            <tr v-for="e in items" :key="e.id">
              <td class="negrita">{{ nombre(e.tarea) }}</td>
              <td class="nowrap">{{ fechaCorta(e.fecha) }}</td>
              <td class="nowrap">{{ momento(e.inicio) }}</td>
              <td class="nowrap">{{ duracion(e) }}</td>
              <td>{{ e.resultado ?? '—' }}</td>
            </tr>
            <tr v-if="datos && !items.length"><td colspan="5" class="vacio">Todavía no hay ejecuciones registradas.</td></tr>
          </tbody>
        </table>
      </div>
      <Paginacion v-if="datos" :pagina="datos.ejecuciones.paginaActual" :paginas="datos.ejecuciones.paginas" :total="datos.ejecuciones.total" :tamano="datos.ejecuciones.tamano" @ir="ir" />
    </section>
  </div>
</template>

<style scoped>
.lista { display: flex; flex-direction: column; }
.tarea { display: flex; align-items: center; gap: 14px; padding: 14px 0; border-top: 1px solid var(--gris-200); }
.tarea:first-child { border-top: 0; padding-top: 0; }
.ico { width: 40px; height: 40px; border-radius: 50%; display: grid; place-items: center; background: var(--azul-claro); color: var(--azul); flex-shrink: 0; }
.resultado { display: inline-flex; align-items: center; gap: 5px; color: var(--verde-texto); font-size: 13px; font-weight: 600; margin-top: 4px; }
.nowrap { white-space: nowrap; }
@media (max-width: 600px) { .tarea { flex-wrap: wrap; } .tarea .btn { margin-left: 54px; } }
</style>
