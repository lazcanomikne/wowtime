<script setup lang="ts">
// Reportes: catálogo (GET /reportes), filtros, vista previa en tabla y exportación a CSV o Excel.
import { computed, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useCarga } from '@/utils/usos'
import { iso, num } from '@/utils/formato'

interface ReporteInfo { clave: string; nombre: string; descripcion: string }
interface Columna { clave: string; titulo: string }
interface Resultado { titulo: string; columnas: Columna[]; filas: Record<string, unknown>[]; totales: Record<string, unknown> }

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()
const ICONOS: Record<string, string> = { asistencia: 'asistencia', 'retardos-faltas': 'alerta', 'vacaciones-saldos': 'vacaciones', visitantes: 'visitantes', documentos: 'documentoAlerta' }
const TIPOS_INCIDENCIA = [
  { v: 'Retardo', t: 'Retardos' }, { v: 'Falta', t: 'Faltas' }, { v: 'SalidaOmitida', t: 'Salidas omitidas' }, { v: 'FueraDeArea', t: 'Fuera de área' },
]

const { datos: catalogo, cargando: cargandoCat } = useCarga(() => api.get<ReporteInfo[]>('/reportes'))
const clave = computed(() => (route.query.tipo ? String(route.query.tipo) : ''))
const actual = computed(() => catalogo.value?.find(r => r.clave === clave.value) ?? null)
function elegir(r: ReporteInfo) { router.replace({ query: { ...route.query, tipo: r.clave } }) }

const hoy = new Date()
const f = reactive({
  desde: iso(new Date(hoy.getFullYear(), hoy.getMonth(), 1)), hasta: iso(hoy),
  areaId: '' as number | '', equipoId: '' as number | '', colaboradorId: null as number | null, tipo: '',
})
const areas = ref<{ id: number; nombre: string }[]>([])
const equipos = ref<{ id: number; nombre: string; areaId: number }[]>([])
async function cargarAreas() {
  try {
    const [a, e] = await Promise.all([
      api.get<Pagina<{ id: number; nombre: string }>>('/areas', { sucursalId: s.sucursalId, tamano: 500, activos: true }),
      api.get<Pagina<{ id: number; nombre: string; areaId: number }>>('/equipos', { sucursalId: s.sucursalId, tamano: 500, activos: true }),
    ])
    areas.value = a.items; equipos.value = e.items
  } catch { areas.value = []; equipos.value = [] }
}
cargarAreas()
watch(() => s.sucursalId, () => { f.areaId = ''; f.equipoId = ''; f.colaboradorId = null; resultado.value = null; cargarAreas() })
const equiposArea = computed(() => equipos.value.filter(e => f.areaId === '' || e.areaId === f.areaId))
watch(() => f.areaId, () => { if (f.equipoId !== '' && !equiposArea.value.some(e => e.id === f.equipoId)) f.equipoId = '' })

const errorFechas = computed(() => {
  if (!f.desde || !f.hasta) return 'Elige el periodo.'
  if (f.hasta < f.desde) return 'La fecha final no puede ser anterior a la inicial.'
  return ''
})
const params = () => ({
  sucursalId: s.sucursalId, desde: f.desde, hasta: f.hasta, areaId: f.areaId, equipoId: f.equipoId, colaboradorId: f.colaboradorId,
  tipo: clave.value === 'retardos-faltas' ? f.tipo : undefined,
})

const resultado = ref<Resultado | null>(null)
const generando = ref(false)
const errorGen = ref<string | null>(null)
const exportando = ref<'' | 'csv' | 'xlsx'>('')
watch(clave, () => { resultado.value = null; errorGen.value = null; f.tipo = '' })

async function generar() {
  if (!actual.value || errorFechas.value) return
  generando.value = true; errorGen.value = null
  try { resultado.value = await api.get<Resultado>(`/reportes/${actual.value.clave}`, { ...params(), formato: 'json' }) }
  catch (e) { resultado.value = null; errorGen.value = e instanceof Error ? e.message : 'No se pudo generar el reporte.' }
  finally { generando.value = false }
}
async function exportar(formato: 'csv' | 'xlsx') {
  if (!actual.value || errorFechas.value) return
  exportando.value = formato
  try { await api.descargar(`/reportes/${actual.value.clave}`, { ...params(), formato }) }
  catch (e) { ui.error(e) }
  finally { exportando.value = '' }
}

const esNumero = (v: unknown) => typeof v === 'number'
const celda = (v: unknown) => (v === null || v === undefined || v === '' ? '—' : typeof v === 'number' ? num(v, 1) : typeof v === 'boolean' ? (v ? 'Sí' : 'No') : String(v))
const numericas = computed(() => new Set((resultado.value?.columnas ?? []).filter(c => resultado.value!.filas.some(fl => esNumero(fl[c.clave]))).map(c => c.clave)))
/** Totales que no corresponden a una columna (p. ej. "total" en retardos y faltas) se muestran como resumen. */
const totalesSueltos = computed(() => {
  const r = resultado.value; if (!r) return []
  const cols = new Set(r.columnas.map(c => c.clave))
  return Object.entries(r.totales ?? {}).filter(([k]) => !cols.has(k)).map(([k, v]) => ({ k, titulo: k.charAt(0).toUpperCase() + k.slice(1).replace(/([A-Z])/g, ' $1').toLowerCase(), v }))
})
const hayTotalesEnColumnas = computed(() => !!resultado.value && resultado.value.columnas.some(c => c.clave in (resultado.value!.totales ?? {})))
</script>

<template>
  <div class="pagina">
    <div class="encabezado">
      <div>
        <h1 class="titulo-pagina">Reportes</h1>
        <p class="subtitulo-pagina">Consulta y exporta la información de {{ s.sucursal?.nombre ?? 'tu sucursal' }}.</p>
      </div>
    </div>

    <div class="catalogo">
      <div v-if="cargandoCat && !catalogo" class="tarjeta esqueleto" style="height: 96px" />
      <button v-for="r in catalogo" :key="r.clave" type="button" class="tarjeta reporte" :class="{ sel: r.clave === clave }" :aria-pressed="r.clave === clave" :data-prueba="`reporte-${r.clave}`" @click="elegir(r)">
        <span class="ico"><Icono :n="ICONOS[r.clave] ?? 'reportes'" :t="22" /></span>
        <span class="textos"><span class="titulo">{{ r.nombre }}</span><span class="desc">{{ r.descripcion }}</span></span>
        <Icono v-if="r.clave === clave" n="checkCirculo" :t="22" class="ok" />
      </button>
    </div>

    <section v-if="actual" class="tarjeta">
      <div class="tarjeta-cabecera">
        <div>
          <h2 class="titulo-tarjeta">{{ actual.nombre }}</h2>
          <p class="subtitulo-tarjeta">{{ actual.descripcion }}</p>
        </div>
      </div>
      <form class="filtros" novalidate @submit.prevent="generar">
        <div class="campo">
          <label for="rp-desde">Desde</label>
          <input id="rp-desde" v-model="f.desde" type="date" class="entrada" :class="{ error: errorFechas }" />
        </div>
        <div class="campo">
          <label for="rp-hasta">Hasta</label>
          <input id="rp-hasta" v-model="f.hasta" type="date" class="entrada" :class="{ error: errorFechas }" />
        </div>
        <div class="campo">
          <label for="rp-area">Área</label>
          <select id="rp-area" v-model="f.areaId" class="entrada">
            <option value="">Todas las áreas</option>
            <option v-for="a in areas" :key="a.id" :value="a.id">{{ a.nombre }}</option>
          </select>
        </div>
        <div class="campo">
          <label for="rp-equipo">Equipo</label>
          <select id="rp-equipo" v-model="f.equipoId" class="entrada">
            <option value="">Todos los equipos</option>
            <option v-for="e in equiposArea" :key="e.id" :value="e.id">{{ e.nombre }}</option>
          </select>
        </div>
        <div class="campo colab">
          <span class="etiqueta-campo">Colaborador</span>
          <SelectorColaborador v-model="f.colaboradorId" :sucursal-id="s.sucursalId" placeholder="Todos los colaboradores" />
        </div>
        <div v-if="actual.clave === 'retardos-faltas'" class="campo">
          <label for="rp-tipo">Tipo de incidencia</label>
          <select id="rp-tipo" v-model="f.tipo" class="entrada">
            <option value="">Todas</option>
            <option v-for="t in TIPOS_INCIDENCIA" :key="t.v" :value="t.v">{{ t.t }}</option>
          </select>
        </div>
        <p v-if="errorFechas" class="error-campo completo">{{ errorFechas }}</p>
        <div class="acciones completo">
          <button type="submit" class="btn btn-primario" :disabled="generando || !!errorFechas" data-prueba="generar">
            <span v-if="generando" class="spinner" style="width: 16px; height: 16px; border-top-color: #fff" /><Icono v-else n="grafica" :t="18" />Generar
          </button>
          <button type="button" class="btn btn-contorno" :disabled="!!exportando || !!errorFechas" data-prueba="exportar-csv" @click="exportar('csv')">
            <span v-if="exportando === 'csv'" class="spinner" style="width: 16px; height: 16px" /><Icono v-else n="descargar" :t="18" />Exportar CSV
          </button>
          <button type="button" class="btn btn-verde-contorno" :disabled="!!exportando || !!errorFechas" data-prueba="exportar-xlsx" @click="exportar('xlsx')">
            <span v-if="exportando === 'xlsx'" class="spinner" style="width: 16px; height: 16px" /><Icono v-else n="descargar" :t="18" />Exportar Excel
          </button>
        </div>
      </form>
    </section>
    <section v-else-if="catalogo" class="tarjeta vacio">
      <Icono n="reportes" :t="32" />
      <p>Elige un reporte para ver sus filtros.</p>
    </section>

    <p v-if="errorGen" class="caja-alerta err" role="alert"><Icono n="alertaCirculo" :t="20" />{{ errorGen }}</p>

    <section v-if="resultado" class="tarjeta">
      <div class="tarjeta-cabecera">
        <div>
          <h2 class="titulo-tarjeta">{{ resultado.titulo }}</h2>
          <p class="subtitulo-tarjeta">{{ resultado.filas.length }} {{ resultado.filas.length === 1 ? 'fila' : 'filas' }}</p>
        </div>
        <div v-if="totalesSueltos.length" class="resumen">
          <span v-for="t in totalesSueltos" :key="t.k" class="chip"><span class="muted">{{ t.titulo }}</span><b>{{ celda(t.v) }}</b></span>
        </div>
      </div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead><tr><th v-for="c in resultado.columnas" :key="c.clave" :class="{ num: numericas.has(c.clave) }">{{ c.titulo }}</th></tr></thead>
          <tbody>
            <tr v-for="(fl, i) in resultado.filas" :key="i">
              <td v-for="c in resultado.columnas" :key="c.clave" :class="{ num: numericas.has(c.clave) }">{{ celda(fl[c.clave]) }}</td>
            </tr>
            <tr v-if="!resultado.filas.length"><td :colspan="resultado.columnas.length" class="vacio">No hay datos para estos filtros.</td></tr>
          </tbody>
          <tfoot v-if="hayTotalesEnColumnas && resultado.filas.length">
            <tr>
              <td v-for="(c, i) in resultado.columnas" :key="c.clave" :class="{ num: numericas.has(c.clave) }">
                {{ c.clave in resultado.totales ? celda(resultado.totales[c.clave]) : i === 0 ? 'Totales' : '' }}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </section>
  </div>
</template>

<style scoped>
.pagina { display: flex; flex-direction: column; gap: 20px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-start; gap: 20px; }
.catalogo { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 20px; }
.reporte { display: flex; align-items: flex-start; gap: 14px; text-align: left; padding: 18px 20px; cursor: pointer; font: inherit; color: inherit; transition: border-color .15s, box-shadow .15s; }
.reporte:hover { border-color: var(--verde-borde); }
.reporte.sel { border-color: var(--verde); box-shadow: 0 0 0 1px var(--verde), var(--sombra); background: var(--verde-suave); }
.ico { width: 46px; height: 46px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-texto); display: grid; place-items: center; flex-shrink: 0; }
.textos { display: flex; flex-direction: column; gap: 3px; flex: 1; min-width: 0; }
.titulo { font-weight: 700; color: var(--titulo); font-size: 16px; }
.desc { font-size: 13.5px; color: var(--texto-3); line-height: 1.35; }
.ok { color: var(--verde); flex-shrink: 0; }
.filtros { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 16px 18px; align-items: end; }
.filtros .colab { grid-column: span 2; }
.filtros .completo { grid-column: 1 / -1; }
.acciones { display: flex; flex-wrap: wrap; gap: 10px; }
.vacio { display: flex; flex-direction: column; align-items: center; gap: 8px; }
.resumen { display: flex; flex-wrap: wrap; gap: 8px; }
.resumen .chip { gap: 6px; }
tfoot td { font-weight: 700; color: var(--titulo); background: var(--gris-50); border-top: 1px solid var(--gris-200); }
.tabla td { white-space: nowrap; }
.err { font-size: 14px; color: var(--rojo-texto); }
@media (max-width: 1000px) { .filtros { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 600px) { .filtros { grid-template-columns: 1fr; } .filtros .colab { grid-column: auto; } .acciones .btn { flex: 1; } }
</style>
