<script setup lang="ts">
// Asistencia (RH): registros del día por jornada o por checada, incidencias y asignaciones temporales.
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import { api, urlArchivo } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { debounce, useAccion, useEventos } from '@/utils/usos'
import { fechaCorta, fechaLarga, fechaMedia, hora, iso, num, rango } from '@/utils/formato'

type Tab = 'registros' | 'incidencias' | 'asignaciones'
const TABS: { clave: Tab; texto: string }[] = [
  { clave: 'registros', texto: 'Registros' },
  { clave: 'incidencias', texto: 'Incidencias' },
  { clave: 'asignaciones', texto: 'Asignaciones temporales' },
]
const ORIGENES: Record<string, [string, string]> = {
  App: ['App móvil', 'celular'], Terminal: ['Terminal', 'terminal'], Kiosco: ['Quiosco', 'monitor'], Rh: ['Manual (RH)', 'fichaPersona'],
}
const MODALIDADES: Record<string, string> = { Presencial: 'Presencial', HomeOffice: 'Home office', Hibrido: 'Híbrido' }
const TIPOS_ASIG: Record<string, string> = { HomeOffice: 'casa', OtraSucursal: 'edificio', Comision: 'maletin' }
const TAM = 25

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()
const hoyIso = iso(new Date())

const q0 = route.query
const tab = ref<Tab>(TABS.some(t => t.clave === q0.tab) ? (q0.tab as Tab) : q0.id ? 'incidencias' : 'registros')
const sucursal = () => s.sucursalId ?? undefined

// ───────── Registros ─────────
const fecha = ref<string>(typeof q0.fecha === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(q0.fecha) ? q0.fecha : hoyIso)
const areaId = ref<number | ''>('')
const estado = ref<string>(tab.value === 'registros' && typeof q0.estado === 'string' ? q0.estado : '')
const origen = ref<string>('')
const fueraDeArea = ref(false)
const vista = ref<'jornadas' | 'checadas'>('jornadas')
const pagReg = ref(1)
const areas = ref<{ id: number; nombre: string }[]>([])
const resumen = ref<any>(null)
const jornadas = ref<any[]>([])
const checadas = ref<{ items: any[]; total: number; paginas: number }>({ items: [], total: 0, paginas: 0 })
const cargandoReg = ref(false)

const jornadasFiltradas = computed(() => (estado.value ? jornadas.value.filter(j => j.estado === estado.value) : jornadas.value))
const jornadasPagina = computed(() => jornadasFiltradas.value.slice((pagReg.value - 1) * TAM, pagReg.value * TAM))
const kpis = computed(() => {
  const r = resumen.value
  if (!r) return null
  const presentes = r.presentes.valor as number
  const enHorario = r.puntualidad.puntuales as number
  return { presentes, plantilla: r.presentes.plantilla as number, pct: r.presentes.porcentaje as number, enHorario, puntualidad: r.puntualidad.porcentaje as number, retardos: Math.max(0, presentes - enHorario), sinRegistro: Math.max(0, r.presentes.plantilla - presentes) }
})

async function cargarAreas() {
  try { areas.value = (await api.get('/areas', { sucursalId: sucursal(), activos: true, tamano: 200 })).items } catch { areas.value = [] }
}
async function cargarResumen() {
  try { resumen.value = await api.get('/dashboard/resumen', { sucursalId: sucursal() }) } catch { resumen.value = null }
}
function filtrosReg() {
  return { sucursalId: sucursal(), fecha: fecha.value || hoyIso, areaId: areaId.value || undefined, origen: origen.value || undefined, fueraDeArea: fueraDeArea.value || undefined }
}
async function cargarRegistros(silencioso = false) {
  if (!silencioso) cargandoReg.value = true
  try {
    if (vista.value === 'jornadas') {
      // Una fila por jornada del día; el estado se filtra aquí porque el de la jornada considera la salida.
      const todas: any[] = []
      for (let p = 1; p <= 10; p++) {
        const r = await api.get('/asistencia/registros', { ...filtrosReg(), agrupado: true, tamano: 500, pagina: p })
        todas.push(...r.items)
        if (todas.length >= r.total || !r.items.length) break
      }
      jornadas.value = todas
      const max = Math.max(1, Math.ceil(jornadasFiltradas.value.length / TAM))
      if (pagReg.value > max) pagReg.value = max
    } else {
      const r = await api.get('/asistencia/registros', { ...filtrosReg(), estado: estado.value || undefined, pagina: pagReg.value, tamano: TAM })
      checadas.value = { items: r.items, total: r.total, paginas: r.paginas }
    }
  } catch (e) { if (!silencioso) ui.error(e) } finally { cargandoReg.value = false }
}
function cambiarVista() {
  vista.value = vista.value === 'jornadas' ? 'checadas' : 'jornadas'
  pagReg.value = 1
  cargarRegistros()
}
function irPaginaReg(p: number) { pagReg.value = p; if (vista.value === 'checadas') cargarRegistros() }
watch([fecha, areaId, origen, fueraDeArea], () => { pagReg.value = 1; cargarRegistros(); sincronizar() })
watch(estado, () => { pagReg.value = 1; if (vista.value === 'checadas') cargarRegistros(); sincronizar() })
function filtrarEstado(v: string) { if (fecha.value !== hoyIso) fecha.value = hoyIso; estado.value = estado.value === v ? '' : v }
function limpiarReg() { fecha.value = hoyIso; areaId.value = ''; estado.value = ''; origen.value = ''; fueraDeArea.value = false }
const hayFiltrosReg = computed(() => fecha.value !== hoyIso || !!areaId.value || !!estado.value || !!origen.value || fueraDeArea.value)

const enlaceMapa = (c: any) => `https://www.openstreetmap.org/?mlat=${c.latitud}&mlon=${c.longitud}#map=18/${c.latitud}/${c.longitud}`
const horasTexto = (j: any) => (j.horas != null ? `${num(j.horas, 1)} h` : j.fecha === hoyIso ? 'En curso' : '—')

// Checada manual
const manual = ref<{ abierto: boolean; colaboradorId: number | null; tipo: 'Entrada' | 'Salida'; justificacion: string; intento: boolean }>({ abierto: false, colaboradorId: null, tipo: 'Entrada', justificacion: '', intento: false })
const { ocupado: guardandoManual, ejecutar } = useAccion()
function abrirManual() { manual.value = { abierto: true, colaboradorId: null, tipo: 'Entrada', justificacion: '', intento: false } }
async function guardarManual() {
  const m = manual.value
  m.intento = true
  if (!m.colaboradorId || !m.justificacion.trim()) return
  const r = await ejecutar(() => api.post('/asistencia/registros/manual', { colaboradorId: m.colaboradorId, tipo: m.tipo, justificacion: m.justificacion.trim() }))
  if (!r) return
  ui.avisar(`Se registró la ${m.tipo === 'Entrada' ? 'entrada' : 'salida'} de ${r.colaborador} a las ${hora(r.hora)} (${r.estadoTexto.toLowerCase()}).`)
  m.abierto = false
  cargarResumen(); cargarRegistros(true)
}

// ───────── Incidencias ─────────
const incEstado = ref<string>(tab.value === 'incidencias' && typeof q0.estado === 'string' ? q0.estado : '')
const incTipo = ref('')
const incDesde = ref('')
const incHasta = ref('')
const incPag = ref(1)
const incidencias = ref<{ items: any[]; total: number; paginas: number }>({ items: [], total: 0, paginas: 0 })
const cargandoInc = ref(false)
const resaltada = ref<number | null>(q0.id ? Number(q0.id) : null)
const resolviendo = ref<number | null>(null)
let buscoResaltada = false

async function cargarIncidencias(silencioso = false) {
  if (!silencioso) cargandoInc.value = true
  try {
    const est = incEstado.value
    const r = await api.get('/asistencia/incidencias', {
      sucursalId: sucursal(), abiertas: est === '' ? true : undefined, estado: est && est !== 'todas' ? est : undefined,
      tipo: incTipo.value || undefined, desde: incDesde.value || undefined, hasta: incHasta.value || undefined, pagina: incPag.value, tamano: TAM
    })
    incidencias.value = { items: r.items, total: r.total, paginas: r.paginas }
    if (resaltada.value && !buscoResaltada) {
      // Si la incidencia ya no está abierta, se busca entre todas (el cambio de filtro vuelve a cargar).
      if (!r.items.some((i: any) => i.id === resaltada.value) && est !== 'todas') { incEstado.value = 'todas'; return }
      buscoResaltada = true
      await nextTick()
      document.querySelector('tr.resaltada')?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    }
  } catch (e) { if (!silencioso) ui.error(e) } finally { cargandoInc.value = false }
}
watch([incEstado, incTipo, incDesde, incHasta], () => { incPag.value = 1; cargarIncidencias(); sincronizar() })
function irPaginaInc(p: number) { incPag.value = p; cargarIncidencias() }
const puedeActuar = (i: any) => i.puedeResolver && (i.estado === 'PorRevisar' || i.estado === 'EnRevision')

async function resolver(i: any, justificada: boolean) {
  const r = await ui.confirmar(justificada
    ? { titulo: 'Justificar incidencia', mensaje: `${i.tipoTexto} de ${i.colaborador.nombre} del ${fechaMedia(i.fecha, true)}. La incidencia no contará en su historial.`, aceptar: 'Justificar', pedirTexto: { etiqueta: 'Comentario (opcional)', placeholder: 'Ej. Presentó comprobante médico' } }
    : { titulo: 'Confirmar incidencia', mensaje: `${i.tipoTexto} de ${i.colaborador.nombre} del ${fechaMedia(i.fecha, true)}. Se avisará al colaborador.`, aceptar: 'Confirmar', peligro: true, pedirTexto: { etiqueta: 'Comentario', obligatorio: true, placeholder: 'Explica el motivo al colaborador' } })
  if (!r.ok) return
  resolviendo.value = i.id
  try {
    await api.post(`/asistencia/incidencias/${i.id}/resolver`, { justificada, comentario: r.texto || null })
    ui.avisar(justificada ? 'Incidencia justificada. Se avisó al colaborador.' : 'Incidencia confirmada. Se avisó al colaborador.')
    cargarIncidencias(true); cargarResumen()
  } catch (e) { ui.error(e) } finally { resolviendo.value = null }
}

// ───────── Asignaciones ─────────
const asigEstado = ref('')
const asigPag = ref(1)
const asignaciones = ref<{ items: any[]; total: number; paginas: number }>({ items: [], total: 0, paginas: 0 })
const cargandoAsig = ref(false)
async function cargarAsignaciones() {
  cargandoAsig.value = true
  try {
    const r = await api.get('/asignaciones', { sucursalId: sucursal(), estado: asigEstado.value || undefined, pagina: asigPag.value, tamano: TAM })
    asignaciones.value = { items: r.items, total: r.total, paginas: r.paginas }
  } catch (e) { ui.error(e) } finally { cargandoAsig.value = false }
}
watch(asigEstado, () => { asigPag.value = 1; cargarAsignaciones() })
function irPaginaAsig(p: number) { asigPag.value = p; cargarAsignaciones() }
const fichaAsignaciones = (a: any) => `/colaboradores/${a.colaborador.id}?tab=laborales&asignaciones=1`

// ───────── Pestañas, URL y tiempo real ─────────
const cargados = new Set<Tab>()
function cargarTab(t: Tab, forzar = false) {
  if (cargados.has(t) && !forzar) return
  cargados.add(t)
  if (t === 'registros') { cargarResumen(); cargarRegistros() }
  else if (t === 'incidencias') cargarIncidencias()
  else cargarAsignaciones()
}
function sincronizar() {
  const query: Record<string, string> = { tab: tab.value }
  if (tab.value === 'registros') { if (fecha.value !== hoyIso) query.fecha = fecha.value; if (estado.value) query.estado = estado.value }
  if (tab.value === 'incidencias') { if (incEstado.value) query.estado = incEstado.value; if (resaltada.value) query.id = String(resaltada.value) }
  router.replace({ query })
}
function irTab(t: Tab) { tab.value = t; sincronizar(); cargarTab(t) }
watch(() => route.query.tab, v => { if (typeof v === 'string' && v !== tab.value && TABS.some(t => t.clave === v)) { tab.value = v as Tab; cargarTab(tab.value) } })
watch(() => s.sucursalId, () => { cargados.clear(); cargarAreas(); areaId.value = ''; cargarTab(tab.value, true) })
onMounted(() => { cargarAreas(); cargarTab(tab.value) })

const enVivo = debounce(() => {
  cargarResumen()
  if (cargados.has('registros')) cargarRegistros(true)
}, 700)
const incEnVivo = debounce(() => { if (cargados.has('incidencias')) cargarIncidencias(true) }, 700)
useEventos(t => { if (t === 'asistencia') enVivo(); if (t === 'incidencia') incEnVivo() })

const subtitulo = computed(() => `${fechaLarga(new Date(), true)}${s.sucursal ? ` · ${s.sucursal.nombre}` : ''}`)
</script>

<template>
  <div class="pagina">
    <div class="encabezado">
      <div>
        <h1 class="titulo-pagina">Asistencia</h1>
        <p class="subtitulo-pagina">{{ subtitulo }}</p>
      </div>
      <button v-if="s.tiene('asistencia.gestionar')" class="btn btn-primario" data-prueba="checada-manual" @click="abrirManual">
        <Icono n="masCirculo" :t="20" />Registrar checada manual
      </button>
    </div>

    <nav class="pestanas" role="tablist">
      <button v-for="t in TABS" :key="t.clave" class="pestana" :class="{ activa: tab === t.clave }" role="tab" :aria-selected="tab === t.clave" @click="irTab(t.clave)">{{ t.texto }}</button>
    </nav>

    <!-- ───── Registros ───── -->
    <template v-if="tab === 'registros'">
      <section class="kpis" aria-label="Indicadores de hoy">
        <button class="tarjeta kpi-t" :class="{ sel: !estado && fecha === hoyIso }" @click="filtrarEstado('')">
          <div class="kpi">
            <span class="kpi-icono" style="background:#dff5e6;color:#0d8a49"><Icono n="personas" :t="28" :g="2.2" /></span>
            <div class="crecer">
              <p class="kpi-titulo">Presentes hoy</p>
              <p class="kpi-valor">{{ kpis?.presentes ?? '—' }}<span class="kpi-de">de {{ kpis?.plantilla ?? '—' }}</span></p>
              <div class="barra-pct"><div class="barra verde"><span :style="{ width: (kpis?.pct ?? 0) + '%' }" /></div><span>{{ kpis?.pct ?? 0 }}%</span></div>
            </div>
          </div>
        </button>
        <button class="tarjeta kpi-t" :class="{ sel: estado === 'EnHorario' }" @click="filtrarEstado('EnHorario')">
          <div class="kpi">
            <span class="kpi-icono" style="background:#d6eef0;color:#11937a"><Icono n="checkCirculo" :t="28" :g="2.2" /></span>
            <div class="crecer">
              <p class="kpi-titulo">En horario</p>
              <p class="kpi-valor">{{ kpis?.enHorario ?? '—' }}<span class="kpi-de">{{ kpis?.puntualidad ?? 0 }}% puntual</span></p>
              <div class="barra-pct"><div class="barra"><span :style="{ width: (kpis?.puntualidad ?? 0) + '%' }" /></div></div>
            </div>
          </div>
        </button>
        <button class="tarjeta kpi-t" :class="{ sel: estado === 'Retardo' }" @click="filtrarEstado('Retardo')">
          <div class="kpi">
            <span class="kpi-icono" style="background:#fde7d6;color:#f26913"><Icono n="reloj" :t="28" :g="2.2" /></span>
            <div class="crecer">
              <p class="kpi-titulo">Retardos</p>
              <p class="kpi-valor">{{ kpis?.retardos ?? '—' }}<span class="kpi-de">{{ resumen?.incidencias.abiertas ?? 0 }} incidencias abiertas</span></p>
            </div>
          </div>
        </button>
        <div class="tarjeta kpi-t estatico">
          <div class="kpi">
            <span class="kpi-icono" style="background:var(--gris-100);color:var(--gris-600)"><Icono n="persona" :t="28" :g="2.2" /></span>
            <div class="crecer">
              <p class="kpi-titulo">Sin registro</p>
              <p class="kpi-valor">{{ kpis?.sinRegistro ?? '—' }}<span class="kpi-de">sin entrada hoy</span></p>
            </div>
          </div>
        </div>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera envolver">
          <div>
            <h2 class="titulo-tarjeta">{{ vista === 'jornadas' ? 'Registros por jornada' : 'Checadas' }}</h2>
            <p class="subtitulo-tarjeta">{{ fecha === hoyIso ? `Hoy, ${fechaMedia(fecha)} · se actualiza en tiempo real` : fechaLarga(fecha, true) }}</p>
          </div>
          <button class="btn btn-contorno btn-ch" data-prueba="ver-checadas" @click="cambiarVista">
            <Icono :n="vista === 'jornadas' ? 'lista' : 'reloj'" :t="18" />{{ vista === 'jornadas' ? 'Ver checadas' : 'Ver jornadas' }}
          </button>
        </div>

        <div class="barra-filtros">
          <input v-model="fecha" type="date" class="entrada" :max="hoyIso" aria-label="Fecha" />
          <select v-model="areaId" class="entrada" aria-label="Área">
            <option value="">Todas las áreas</option>
            <option v-for="a in areas" :key="a.id" :value="a.id">{{ a.nombre }}</option>
          </select>
          <select v-model="estado" class="entrada" aria-label="Estado">
            <option value="">Todos los estados</option>
            <option value="EnHorario">En horario</option>
            <option value="Retardo">Retardo</option>
            <option value="SalidaRegistrada">Salida registrada</option>
          </select>
          <select v-model="origen" class="entrada" aria-label="Origen">
            <option value="">Todos los orígenes</option>
            <option v-for="(o, k) in ORIGENES" :key="k" :value="k">{{ o[0] }}</option>
          </select>
          <label class="casilla"><input v-model="fueraDeArea" type="checkbox" />Fuera de área</label>
          <button v-if="hayFiltrosReg" class="ver-todo" @click="limpiarReg">Limpiar filtros</button>
        </div>

        <!-- Vista por jornada -->
        <div v-if="vista === 'jornadas'" class="tabla-envoltura">
          <table class="tabla">
            <thead>
              <tr><th>Colaborador</th><th>Entrada</th><th>Salida</th><th>Horas</th><th>Modalidad</th><th>Origen / Dispositivo</th><th>Estado</th></tr>
            </thead>
            <tbody>
              <tr v-for="j in jornadasPagina" :key="j.id" class="clic" @click="router.push(`/colaboradores/${j.colaborador.id}?tab=asistencia`)">
                <td><Persona :p="j.colaborador" /></td>
                <td class="hora">{{ hora(j.entrada) }}</td>
                <td class="hora">{{ j.salida ? hora(j.salida) : '—' }}</td>
                <td class="nowrap">{{ horasTexto(j) }}</td>
                <td class="nowrap">{{ MODALIDADES[j.modalidad] ?? j.modalidad }}</td>
                <td>
                  <div class="origen"><Icono :n="ORIGENES[j.origen]?.[1] ?? 'reloj'" :t="18" /><div><b>{{ ORIGENES[j.origen]?.[0] ?? j.origen }}</b><small v-if="j.dispositivo || j.lugarAutorizado">{{ j.dispositivo ?? j.lugarAutorizado }}</small></div></div>
                </td>
                <td>
                  <div class="estados">
                    <Estado :v="j.estado" punto />
                    <span v-if="j.dentroGeocerca === false" class="etiqueta rojo cuadrada">Fuera de área</span>
                  </div>
                </td>
              </tr>
              <tr v-if="cargandoReg && !jornadas.length"><td colspan="7"><div class="vacio"><span class="spinner" /></div></td></tr>
              <tr v-else-if="!jornadasFiltradas.length"><td colspan="7"><div class="vacio">No hay registros {{ fecha === hoyIso ? 'hoy' : 'en esa fecha' }} con estos filtros.</div></td></tr>
            </tbody>
          </table>
        </div>
        <Paginacion v-if="vista === 'jornadas'" :pagina="pagReg" :paginas="Math.ceil(jornadasFiltradas.length / TAM)" :total="jornadasFiltradas.length" :tamano="TAM" @ir="irPaginaReg" />

        <!-- Vista por checada -->
        <template v-if="vista === 'checadas'">
          <div class="tabla-envoltura">
            <table class="tabla">
              <thead>
                <tr><th>Colaborador</th><th>Checada</th><th>Origen / Dispositivo</th><th>Ubicación</th><th>Distancia</th><th>Justificación</th><th>Evidencia</th><th>Estado</th></tr>
              </thead>
              <tbody>
                <tr v-for="c in checadas.items" :key="c.id">
                  <td><Persona :p="c.colaborador" /></td>
                  <td class="nowrap"><b class="hora">{{ hora(c.hora) }}</b><small class="bloque muted">{{ c.tipo === 'Entrada' ? 'Entrada' : 'Salida' }} · {{ fechaCorta(c.fechaLocal, false) }}</small></td>
                  <td>
                    <div class="origen"><Icono :n="ORIGENES[c.origen]?.[1] ?? 'reloj'" :t="18" /><div><b>{{ ORIGENES[c.origen]?.[0] ?? c.origen }}</b><small v-if="c.dispositivo">{{ c.dispositivo }}</small></div></div>
                  </td>
                  <td>
                    <a v-if="c.latitud != null && c.longitud != null" :href="enlaceMapa(c)" target="_blank" rel="noopener" class="mapa" @click.stop>
                      <Icono n="ubicacion" :t="17" />{{ c.lugarAutorizado ?? 'Ver en mapa' }}<Icono n="externo" :t="14" />
                    </a>
                    <span v-else class="muted">—</span>
                  </td>
                  <td class="nowrap">
                    <span v-if="c.dentroGeocerca === true" class="txt-verde">Dentro{{ c.distanciaMetros != null ? ` · ${num(c.distanciaMetros)} m` : '' }}</span>
                    <span v-else-if="c.dentroGeocerca === false" class="txt-rojo">A {{ num(c.distanciaMetros) }} m del área</span>
                    <span v-else class="muted">—</span>
                  </td>
                  <td class="justif">{{ c.justificacion ?? '—' }}</td>
                  <td>
                    <a v-if="c.fotoUrl" :href="urlArchivo(c.fotoUrl)" target="_blank" rel="noopener" class="foto" title="Ver foto de evidencia"><img :src="urlArchivo(c.fotoUrl)" alt="Foto de evidencia" loading="lazy" /></a>
                    <span v-else class="muted">—</span>
                  </td>
                  <td>
                    <div class="estados">
                      <Estado :v="c.estado" punto />
                      <span v-if="c.dentroGeocerca === false" class="etiqueta rojo cuadrada">Fuera de área</span>
                    </div>
                  </td>
                </tr>
                <tr v-if="cargandoReg && !checadas.items.length"><td colspan="8"><div class="vacio"><span class="spinner" /></div></td></tr>
                <tr v-else-if="!checadas.items.length"><td colspan="8"><div class="vacio">No hay checadas con estos filtros.</div></td></tr>
              </tbody>
            </table>
          </div>
          <Paginacion :pagina="pagReg" :paginas="checadas.paginas" :total="checadas.total" :tamano="TAM" @ir="irPaginaReg" />
        </template>
      </section>
    </template>

    <!-- ───── Incidencias ───── -->
    <section v-else-if="tab === 'incidencias'" class="tarjeta">
      <div class="tarjeta-cabecera">
        <div>
          <h2 class="titulo-tarjeta">Incidencias</h2>
          <p class="subtitulo-tarjeta">Retardos, faltas, salidas omitidas y registros fuera de área</p>
        </div>
      </div>
      <div class="barra-filtros">
        <select v-model="incEstado" class="entrada" aria-label="Estado">
          <option value="">Abiertas</option>
          <option value="PorRevisar">Por revisar</option>
          <option value="EnRevision">En revisión</option>
          <option value="Justificada">Justificadas</option>
          <option value="Confirmada">Confirmadas</option>
          <option value="todas">Todas</option>
        </select>
        <select v-model="incTipo" class="entrada" aria-label="Tipo">
          <option value="">Todos los tipos</option>
          <option value="Retardo">Retardo</option>
          <option value="Falta">Falta</option>
          <option value="SalidaOmitida">Salida omitida</option>
          <option value="FueraDeArea">Fuera de área</option>
        </select>
        <label class="rango-f"><span>Del</span><input v-model="incDesde" type="date" class="entrada" :max="incHasta || hoyIso" aria-label="Desde" /></label>
        <label class="rango-f"><span>al</span><input v-model="incHasta" type="date" class="entrada" :min="incDesde || undefined" :max="hoyIso" aria-label="Hasta" /></label>
      </div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead>
            <tr><th>Colaborador</th><th>Fecha</th><th>Tipo</th><th>Detalle</th><th>Justificación</th><th>Estado</th><th class="der">Acciones</th></tr>
          </thead>
          <tbody>
            <tr v-for="i in incidencias.items" :key="i.id" :class="{ resaltada: i.id === resaltada }" :data-id="i.id">
              <td><Persona :p="i.colaborador" enlace /></td>
              <td class="nowrap">{{ fechaCorta(i.fecha) }}</td>
              <td><Estado :v="i.tipo" :texto="i.tipoTexto" /></td>
              <td class="detalle">{{ i.detalle ?? '—' }}</td>
              <td class="justif">
                {{ i.justificacion ?? '—' }}
                <small v-if="i.comentarioResolucion" class="bloque muted">Resolución: {{ i.comentarioResolucion }}</small>
              </td>
              <td><Estado :v="i.estado" punto /></td>
              <td>
                <div v-if="puedeActuar(i)" class="acciones">
                  <button class="btn btn-verde-contorno btn-ch" :disabled="resolviendo === i.id" @click="resolver(i, true)">Justificar</button>
                  <button class="btn btn-rechazar btn-ch" :disabled="resolviendo === i.id" @click="resolver(i, false)">Confirmar</button>
                </div>
                <RouterLink v-else-if="i.solicitudId" :to="`/solicitudes/${i.solicitudId}`" class="ver-todo">Ver solicitud</RouterLink>
              </td>
            </tr>
            <tr v-if="cargandoInc && !incidencias.items.length"><td colspan="7"><div class="vacio"><span class="spinner" /></div></td></tr>
            <tr v-else-if="!incidencias.items.length"><td colspan="7"><div class="vacio">{{ incEstado === '' ? 'No hay incidencias abiertas. ¡Todo en orden!' : 'No hay incidencias con estos filtros.' }}</div></td></tr>
          </tbody>
        </table>
      </div>
      <Paginacion :pagina="incPag" :paginas="incidencias.paginas" :total="incidencias.total" :tamano="TAM" @ir="irPaginaInc" />
    </section>

    <!-- ───── Asignaciones temporales ───── -->
    <section v-else class="tarjeta">
      <div class="tarjeta-cabecera">
        <div>
          <h2 class="titulo-tarjeta">Asignaciones temporales</h2>
          <p class="subtitulo-tarjeta">Home office, apoyo en otra sucursal y comisiones</p>
        </div>
      </div>
      <div class="barra-filtros">
        <select v-model="asigEstado" class="entrada" aria-label="Estado">
          <option value="">Todos los estados</option>
          <option value="Programada">Programada</option>
          <option value="Vigente">Vigente</option>
          <option value="Concluida">Concluida</option>
          <option value="Cancelada">Cancelada</option>
        </select>
      </div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead>
            <tr><th>Colaborador</th><th>Tipo</th><th>Periodo</th><th>Motivo</th><th>Estado</th><th></th></tr>
          </thead>
          <tbody>
            <tr v-for="a in asignaciones.items" :key="a.id" class="clic" @click="router.push(fichaAsignaciones(a))">
              <td><Persona :p="a.colaborador" /></td>
              <td><span class="tipo-asig"><span class="icono-asig"><Icono :n="TIPOS_ASIG[a.tipo] ?? 'asignacion'" :t="18" /></span>{{ a.tipoTexto }}</span></td>
              <td class="nowrap">{{ rango(a.fechaInicio, a.fechaFin, true) }}</td>
              <td class="detalle">{{ a.motivo }}</td>
              <td><Estado :v="a.estado" punto /></td>
              <td class="der"><RouterLink :to="fichaAsignaciones(a)" class="ver-todo" @click.stop>Ver en ficha<Icono n="flecha" :t="16" /></RouterLink></td>
            </tr>
            <tr v-if="cargandoAsig && !asignaciones.items.length"><td colspan="6"><div class="vacio"><span class="spinner" /></div></td></tr>
            <tr v-else-if="!asignaciones.items.length"><td colspan="6"><div class="vacio">No hay asignaciones temporales{{ asigEstado ? ' con ese estado' : '' }}.</div></td></tr>
          </tbody>
        </table>
      </div>
      <Paginacion :pagina="asigPag" :paginas="asignaciones.paginas" :total="asignaciones.total" :tamano="TAM" @ir="irPaginaAsig" />
    </section>

    <!-- Checada manual -->
    <Modal v-if="manual.abierto" titulo="Registrar checada manual" subtitulo="Queda registrada con origen RH, tu nombre y la justificación." :ancho="520" @cerrar="manual.abierto = false">
      <form class="columna formulario" @submit.prevent="guardarManual">
        <div class="campo">
          <label>Colaborador</label>
          <SelectorColaborador v-model="manual.colaboradorId" :sucursal-id="s.sucursalId" />
          <span v-if="manual.intento && !manual.colaboradorId" class="error-campo">Elige al colaborador.</span>
        </div>
        <div class="campo">
          <label>Tipo de checada</label>
          <div class="segmentos">
            <button type="button" :class="{ sel: manual.tipo === 'Entrada' }" @click="manual.tipo = 'Entrada'"><Icono n="flecha" :t="18" />Entrada</button>
            <button type="button" :class="{ sel: manual.tipo === 'Salida' }" @click="manual.tipo = 'Salida'"><Icono n="salir" :t="18" />Salida</button>
          </div>
          <span class="ayuda">Se registra con la hora actual del servidor.</span>
        </div>
        <div class="campo">
          <label for="just-manual">Justificación</label>
          <textarea id="just-manual" v-model="manual.justificacion" class="entrada" :class="{ error: manual.intento && !manual.justificacion.trim() }" rows="3" placeholder="Ej. La terminal no respondió; el colaborador llegó a las 07:58" />
          <span v-if="manual.intento && !manual.justificacion.trim()" class="error-campo">La justificación es obligatoria.</span>
        </div>
      </form>
      <template #pie>
        <button class="btn btn-contorno" @click="manual.abierto = false">Cancelar</button>
        <button class="btn btn-primario" :disabled="guardandoManual" @click="guardarManual">Registrar {{ manual.tipo === 'Entrada' ? 'entrada' : 'salida' }}</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.pagina { display: flex; flex-direction: column; gap: 20px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; flex-wrap: wrap; }
.pestanas { margin-top: -4px; }
.kpis { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 20px; }
.kpi-t { text-align: left; font: inherit; color: inherit; padding: 18px 20px; transition: box-shadow .15s, border-color .15s; }
button.kpi-t:hover { border-color: var(--gris-300); box-shadow: var(--sombra-m); }
.kpi-t.sel { border-color: var(--verde); box-shadow: 0 0 0 1px var(--verde); }
.kpi-t.estatico { cursor: default; }
.kpi-valor { margin-top: 4px; display: flex; align-items: baseline; flex-wrap: wrap; }
.barra-pct { display: flex; align-items: center; gap: 12px; margin-top: 10px; font-size: 14px; color: var(--texto-2); }
.barra-pct .barra { flex: 1; max-width: 150px; }
.tarjeta-cabecera.envolver { flex-wrap: wrap; }
.hora { font-variant-numeric: tabular-nums; font-weight: 600; color: var(--titulo); white-space: nowrap; }
.nowrap { white-space: nowrap; }
.bloque { display: block; font-size: 12.5px; font-weight: 400; }
.origen { display: flex; align-items: center; gap: 10px; color: var(--texto-2); }
.origen b { display: block; font-weight: 550; color: var(--titulo); font-size: 14px; white-space: nowrap; }
.origen small { display: block; color: var(--texto-3); font-size: 12.5px; white-space: nowrap; }
.estados { display: flex; gap: 6px; align-items: center; flex-wrap: wrap; }
.mapa { display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; font-size: 14px; }
.txt-verde { color: var(--verde-texto); font-weight: 550; }
.txt-rojo { color: var(--rojo-texto); font-weight: 550; }
.justif { max-width: 260px; font-size: 14px; color: var(--texto-2); }
.detalle { max-width: 300px; font-size: 14px; color: var(--texto-2); }
.foto img { width: 44px; height: 44px; border-radius: 8px; object-fit: cover; display: block; border: 1px solid var(--gris-200); }
.der { text-align: right; }
.acciones { display: flex; gap: 8px; justify-content: flex-end; }
tr.resaltada td { background: #fff8e6; }
tr.resaltada td:first-child { box-shadow: inset 3px 0 0 var(--naranja); }
.rango-f { display: inline-flex; align-items: center; gap: 8px; font-size: 14px; color: var(--texto-2); }
.tipo-asig { display: inline-flex; align-items: center; gap: 10px; white-space: nowrap; font-weight: 500; color: var(--titulo); }
.icono-asig { width: 34px; height: 34px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-texto); display: grid; place-items: center; }
.formulario { gap: 16px; }
.segmentos { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.segmentos button { height: 46px; border-radius: var(--radio-s); border: 1px solid var(--gris-300); background: #fff; display: inline-flex; align-items: center; justify-content: center; gap: 8px; font-weight: 600; color: var(--texto-2); }
.segmentos button.sel { border-color: var(--verde); background: var(--verde-suave); color: var(--verde-texto); box-shadow: 0 0 0 1px var(--verde); }

@media (max-width: 1200px) { .kpis { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 760px) {
  .encabezado .btn { width: 100%; }
  .kpis { gap: 12px; }
  .kpi-t { padding: 14px; }
  .kpi { gap: 12px; }
  .kpi-icono { width: 44px; height: 44px; }
  .kpi-valor { font-size: 28px; }
  .rango-f { width: 100%; }
}
@media (max-width: 480px) { .kpis { grid-template-columns: minmax(0, 1fr); } }
</style>
