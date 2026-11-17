<script setup lang="ts">
// Expedientes (RH): avance del expediente por colaborador y documentos por vencer.
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { debounce } from '@/utils/usos'
import { fechaCorta, num } from '@/utils/formato'

type Tab = 'avance' | 'por-vencer'
const TABS: { clave: Tab; texto: string }[] = [
  { clave: 'avance', texto: 'Avance por colaborador' },
  { clave: 'por-vencer', texto: 'Documentos por vencer' },
]
const TAM = 25

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()

// Enlaces de avisos (/expedientes?colaborador=ID) van directo a la ficha.
const redirigir = typeof route.query.colaborador === 'string' && /^\d+$/.test(route.query.colaborador) ? route.query.colaborador : null
if (redirigir) router.replace(`/colaboradores/${redirigir}?tab=expediente`)

const tab = ref<Tab>(TABS.some(t => t.clave === route.query.tab) ? (route.query.tab as Tab) : 'avance')
const sucursal = () => s.sucursalId ?? undefined

// ───── Indicadores ─────
const resumen = ref<any>(null)
async function cargarResumen() {
  try { resumen.value = (await api.get('/expedientes', { sucursalId: sucursal(), tamano: 1 })).resumen } catch { resumen.value = null }
}
const pctCompletos = computed(() => (resumen.value?.colaboradores ? Math.round(resumen.value.completos * 100 / resumen.value.colaboradores) : 0))

// ───── Avance ─────
const q = ref('')
const areaId = ref<number | ''>('')
const soloIncompletos = ref(false)
const orden = ref<'porcentaje' | 'alertas' | 'nombre'>('porcentaje')
const pagAv = ref(1)
const avance = ref<{ items: any[]; total: number }>({ items: [], total: 0 })
const cargandoAv = ref(false)
const areas = ref<{ id: number; nombre: string }[]>([])

async function cargarAreas() {
  try { areas.value = (await api.get('/areas', { sucursalId: sucursal(), activos: true, tamano: 200 })).items } catch { areas.value = [] }
}
async function cargarAvance() {
  cargandoAv.value = true
  try {
    const r = await api.get('/expedientes', {
      sucursalId: sucursal(), q: q.value.trim() || undefined, areaId: areaId.value || undefined, soloIncompletos: soloIncompletos.value || undefined,
      orden: orden.value, dir: orden.value === 'porcentaje' ? 'asc' : undefined, pagina: pagAv.value, tamano: TAM
    })
    avance.value = { items: r.items, total: r.total }
  } catch (e) { ui.error(e) } finally { cargandoAv.value = false }
}
const buscar = debounce(() => { pagAv.value = 1; cargarAvance() }, 300)
watch(q, buscar)
watch([areaId, soloIncompletos, orden], () => { pagAv.value = 1; cargarAvance() })
function irPagAv(p: number) { pagAv.value = p; cargarAvance() }
const colorBarra = (p: number) => (p >= 100 ? 'verde' : p >= 70 ? '' : p >= 40 ? 'ambar' : 'roja')

// ───── Por vencer ─────
const tipoDocumentoId = ref<number | ''>('')
const dias = ref(30)
const incluirVencidos = ref(true)
const pagPv = ref(1)
const porVencer = ref<{ items: any[]; total: number; paginas: number }>({ items: [], total: 0, paginas: 0 })
const cargandoPv = ref(false)
const tipos = ref<{ id: number; nombre: string; conVigencia: boolean }[]>([])

async function cargarTipos() {
  try { tipos.value = (await api.get('/tipos-documento', { activos: true, tamano: 200 })).items.filter((t: any) => t.conVigencia) } catch { tipos.value = [] }
}
async function cargarPorVencer() {
  cargandoPv.value = true
  try {
    const r = await api.get('/expedientes/por-vencer', {
      sucursalId: sucursal(), tipoDocumentoId: tipoDocumentoId.value || undefined, dias: dias.value, incluirVencidos: incluirVencidos.value, pagina: pagPv.value, tamano: TAM
    })
    porVencer.value = { items: r.items, total: r.total, paginas: r.paginas }
  } catch (e) { ui.error(e) } finally { cargandoPv.value = false }
}
watch([tipoDocumentoId, dias, incluirVencidos], () => { pagPv.value = 1; cargarPorVencer() })
function irPagPv(p: number) { pagPv.value = p; cargarPorVencer() }
const plazo = (d: any) => (d.dias < 0 ? `Venció hace ${-d.dias} ${d.dias === -1 ? 'día' : 'días'}` : d.dias === 0 ? 'Vence hoy' : `Vence en ${d.dias} ${d.dias === 1 ? 'día' : 'días'}`)

// ───── Pestañas ─────
const cargados = new Set<Tab>()
function cargarTab(t: Tab, forzar = false) {
  if (cargados.has(t) && !forzar) return
  cargados.add(t)
  if (t === 'avance') cargarAvance()
  else { if (!tipos.value.length) cargarTipos(); cargarPorVencer() }
}
function irTab(t: Tab) { tab.value = t; router.replace({ query: { tab: t } }); cargarTab(t) }
watch(() => route.query.tab, v => { if (typeof v === 'string' && v !== tab.value && TABS.some(t => t.clave === v)) { tab.value = v as Tab; cargarTab(tab.value) } })
watch(() => s.sucursalId, () => { cargados.clear(); areaId.value = ''; cargarAreas(); cargarResumen(); cargarTab(tab.value, true) })
onMounted(() => { if (redirigir) return; cargarResumen(); cargarAreas(); cargarTab(tab.value) })
</script>

<template>
  <div class="pagina">
    <div class="encabezado">
      <div>
        <h1 class="titulo-pagina">Expedientes</h1>
        <p class="subtitulo-pagina">Documentos de los colaboradores{{ s.sucursal ? ` · ${s.sucursal.nombre}` : '' }}</p>
      </div>
    </div>

    <section class="kpis" aria-label="Indicadores">
      <div class="tarjeta kpi-t">
        <div class="kpi">
          <span class="kpi-icono" style="background:#dff5e6;color:#0d8a49"><Icono n="expedientes" :t="28" :g="2.1" /></span>
          <div class="crecer">
            <p class="kpi-titulo">Expedientes completos</p>
            <p class="kpi-valor">{{ resumen?.completos ?? '—' }}<span class="kpi-de">de {{ resumen?.colaboradores ?? '—' }} colaboradores</span></p>
            <div class="barra-pct"><div class="barra verde"><span :style="{ width: pctCompletos + '%' }" /></div><span>{{ pctCompletos }}%</span></div>
          </div>
        </div>
      </div>
      <div class="tarjeta kpi-t">
        <div class="kpi">
          <span class="kpi-icono" style="background:#d6eef0;color:#11937a"><Icono n="documento" :t="28" :g="2.1" /></span>
          <div class="crecer">
            <p class="kpi-titulo">Avance promedio</p>
            <p class="kpi-valor">{{ resumen ? resumen.porcentaje + '%' : '—' }}</p>
            <div class="barra-pct"><div class="barra"><span :style="{ width: (resumen?.porcentaje ?? 0) + '%' }" /></div></div>
          </div>
        </div>
      </div>
      <button class="tarjeta kpi-t clic" @click="irTab('por-vencer')">
        <div class="kpi">
          <span class="kpi-icono" style="background:var(--rojo-claro);color:var(--rojo)"><Icono n="documentoAlerta" :t="28" :g="2.1" /></span>
          <div class="crecer">
            <p class="kpi-titulo">Documentos vencidos</p>
            <p class="kpi-valor" :class="{ rojo: resumen?.vencidos }">{{ resumen?.vencidos ?? '—' }}</p>
            <p class="kpi-nota">Requieren renovación</p>
          </div>
        </div>
      </button>
      <button class="tarjeta kpi-t clic" @click="irTab('por-vencer')">
        <div class="kpi">
          <span class="kpi-icono" style="background:#fde7d6;color:#f26913"><Icono n="alerta" :t="28" :g="2.1" /></span>
          <div class="crecer">
            <p class="kpi-titulo">Por vencer</p>
            <p class="kpi-valor">{{ resumen ? resumen.urgentes + resumen.proximos : '—' }}</p>
            <p class="kpi-nota"><span class="txt-rojo">{{ resumen?.urgentes ?? 0 }} urgentes</span> · <span class="txt-ambar">{{ resumen?.proximos ?? 0 }} próximos</span></p>
          </div>
        </div>
      </button>
    </section>

    <nav class="pestanas" role="tablist">
      <button v-for="t in TABS" :key="t.clave" class="pestana" :class="{ activa: tab === t.clave }" role="tab" :aria-selected="tab === t.clave" @click="irTab(t.clave)">{{ t.texto }}</button>
    </nav>

    <!-- Avance por colaborador -->
    <section v-if="tab === 'avance'" class="tarjeta">
      <div class="barra-filtros">
        <div class="con-icono buscar"><Icono n="buscar" :t="18" /><input v-model="q" class="entrada" placeholder="Buscar por nombre o número" aria-label="Buscar colaborador" /></div>
        <select v-model="areaId" class="entrada" aria-label="Área">
          <option value="">Todas las áreas</option>
          <option v-for="a in areas" :key="a.id" :value="a.id">{{ a.nombre }}</option>
        </select>
        <select v-model="orden" class="entrada" aria-label="Ordenar">
          <option value="porcentaje">Menor avance primero</option>
          <option value="alertas">Más alertas primero</option>
          <option value="nombre">Nombre (A–Z)</option>
        </select>
        <label class="casilla"><input v-model="soloIncompletos" type="checkbox" />Solo incompletos o con vencidos</label>
      </div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead>
            <tr><th>Colaborador</th><th class="col-avance">Avance</th><th>Alertas</th><th></th></tr>
          </thead>
          <tbody>
            <tr v-for="f in avance.items" :key="f.colaborador.id" class="clic" @click="router.push(`/colaboradores/${f.colaborador.id}?tab=expediente`)">
              <td><Persona :p="f.colaborador" :sub="[f.colaborador.puesto, f.colaborador.numero].filter(Boolean).join(' · ')" /></td>
              <td>
                <div class="avance">
                  <span class="avance-txt"><b>{{ f.entregados }}</b> de {{ f.requeridos }}</span>
                  <div class="barra" :class="colorBarra(f.porcentaje)"><span :style="{ width: f.porcentaje + '%' }" /></div>
                  <span class="pct">{{ f.porcentaje }}%</span>
                </div>
              </td>
              <td>
                <div class="alertas">
                  <span v-if="f.vencidos" class="etiqueta rojo cuadrada">{{ f.vencidos }} {{ f.vencidos === 1 ? 'vencido' : 'vencidos' }}</span>
                  <span v-if="f.urgentes" class="etiqueta naranja cuadrada">{{ f.urgentes }} {{ f.urgentes === 1 ? 'urgente' : 'urgentes' }}</span>
                  <span v-if="f.proximos" class="etiqueta ambar cuadrada">{{ f.proximos }} {{ f.proximos === 1 ? 'próximo' : 'próximos' }}</span>
                  <span v-if="!f.vencidos && !f.urgentes && !f.proximos" class="muted pequeno">Sin alertas</span>
                </div>
              </td>
              <td class="der"><Icono n="derecha" :t="18" class="muted" /></td>
            </tr>
            <tr v-if="cargandoAv && !avance.items.length"><td colspan="4"><div class="vacio"><span class="spinner" /></div></td></tr>
            <tr v-else-if="!avance.items.length"><td colspan="4"><div class="vacio">No hay colaboradores con estos filtros.</div></td></tr>
          </tbody>
        </table>
      </div>
      <Paginacion :pagina="pagAv" :paginas="Math.ceil(avance.total / TAM)" :total="avance.total" :tamano="TAM" @ir="irPagAv" />
    </section>

    <!-- Documentos por vencer -->
    <section v-else class="tarjeta">
      <div class="barra-filtros">
        <select v-model="tipoDocumentoId" class="entrada" aria-label="Tipo de documento">
          <option value="">Todos los documentos</option>
          <option v-for="t in tipos" :key="t.id" :value="t.id">{{ t.nombre }}</option>
        </select>
        <select v-model.number="dias" class="entrada" aria-label="Plazo">
          <option :value="7">Próximos 7 días</option>
          <option :value="15">Próximos 15 días</option>
          <option :value="30">Próximos 30 días</option>
          <option :value="60">Próximos 60 días</option>
          <option :value="90">Próximos 90 días</option>
        </select>
        <label class="casilla"><input v-model="incluirVencidos" type="checkbox" />Incluir vencidos</label>
      </div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead>
            <tr><th>Documento</th><th>Colaborador</th><th>Vence</th><th>Plazo</th><th>Estado</th></tr>
          </thead>
          <tbody>
            <tr v-for="d in porVencer.items" :key="d.id" class="clic" @click="router.push(`/colaboradores/${d.colaborador.id}?tab=expediente&documento=${d.id}`)">
              <td><span class="doc"><span class="doc-ic"><Icono n="documento" :t="20" /></span><b>{{ d.documento }}</b></span></td>
              <td><Persona :p="d.colaborador" :t="36" /></td>
              <td class="nowrap">{{ fechaCorta(d.fechaVencimiento) }}</td>
              <td class="nowrap"><span class="plazo"><Icono n="reloj" :t="16" />{{ plazo(d) }}</span></td>
              <td><Estado :v="d.estado" punto /></td>
            </tr>
            <tr v-if="cargandoPv && !porVencer.items.length"><td colspan="5"><div class="vacio"><span class="spinner" /></div></td></tr>
            <tr v-else-if="!porVencer.items.length"><td colspan="5"><div class="vacio">No hay documentos por vencer en los próximos {{ num(dias) }} días.</div></td></tr>
          </tbody>
        </table>
      </div>
      <Paginacion :pagina="pagPv" :paginas="porVencer.paginas" :total="porVencer.total" :tamano="TAM" @ir="irPagPv" />
    </section>
  </div>
</template>

<style scoped>
.pagina { display: flex; flex-direction: column; gap: 20px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
.kpis { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 20px; }
.kpi-t { text-align: left; font: inherit; color: inherit; padding: 18px 20px; }
.kpi-t.clic { transition: box-shadow .15s, border-color .15s; }
.kpi-t.clic:hover { border-color: var(--gris-300); box-shadow: var(--sombra-m); }
.kpi-valor { margin-top: 4px; display: flex; align-items: baseline; flex-wrap: wrap; }
.kpi-valor.rojo { color: var(--rojo); }
.kpi-nota { font-size: 14px; color: var(--texto-2); margin-top: 6px; }
.barra-pct { display: flex; align-items: center; gap: 12px; margin-top: 10px; font-size: 14px; color: var(--texto-2); }
.barra-pct .barra { flex: 1; max-width: 150px; }
.txt-rojo { color: var(--rojo-texto); font-weight: 550; }
.txt-ambar { color: var(--ambar); font-weight: 550; }
.pestanas { margin-top: -4px; }
.col-avance { min-width: 260px; }
.avance { display: flex; align-items: center; gap: 12px; }
.avance-txt { white-space: nowrap; font-size: 14px; color: var(--texto-2); min-width: 54px; }
.avance-txt b { color: var(--titulo); }
.avance .barra { flex: 1; min-width: 100px; max-width: 220px; }
.pct { font-weight: 650; color: var(--titulo); font-size: 14px; min-width: 40px; text-align: right; font-variant-numeric: tabular-nums; }
.alertas { display: flex; gap: 6px; flex-wrap: wrap; }
.der { text-align: right; width: 40px; }
.nowrap { white-space: nowrap; }
.doc { display: inline-flex; align-items: center; gap: 12px; }
.doc b { color: var(--titulo); font-weight: 600; }
.doc-ic { width: 36px; height: 36px; border-radius: 8px; background: var(--gris-100); color: var(--texto-2); display: grid; place-items: center; flex-shrink: 0; }
.plazo { display: inline-flex; align-items: center; gap: 6px; color: var(--rojo); font-weight: 550; font-size: 14px; }

@media (max-width: 1200px) { .kpis { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 760px) {
  .kpis { gap: 12px; }
  .kpi-t { padding: 14px; }
  .kpi { gap: 12px; }
  .kpi-icono { width: 44px; height: 44px; }
  .kpi-valor { font-size: 28px; }
}
@media (max-width: 480px) { .kpis { grid-template-columns: minmax(0, 1fr); } }
</style>
