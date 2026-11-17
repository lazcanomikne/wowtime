<script setup lang="ts">
// Vacaciones del portal RH (Portal_Autorizar_Vacaciones.png): Resumen, Solicitudes, Calendario de equipo, Políticas y saldos.
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import Calendario from '@/componentes/Calendario.vue'
import LineaTiempo from '@/componentes/LineaTiempo.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import { api, type Pagina } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion, useEventos, debounce } from '@/utils/usos'
import { MESES, aFecha, cap, fechaCorta, iso, mesAnio, num, rango } from '@/utils/formato'

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()
const { ejecutar, ocupado } = useAccion()
const tab = computed(() => (route.query.tab as string) || 'resumen')
const irTab = (k: string, extra: Record<string, string> = {}) => router.replace({ query: { ...(k === 'resumen' ? {} : { tab: k }), ...extra } })
const hoy = new Date()
const mes = ref(new Date(hoy.getFullYear(), hoy.getMonth(), 1))
const mesTxt = computed(() => `${mes.value.getFullYear()}-${String(mes.value.getMonth() + 1).padStart(2, '0')}`)
const areaId = ref<number | ''>('')
const areas = ref<any[]>([])
const equipos = ref<any[]>([])
const selMes = ref(false)

// Resumen
const kpi = ref<any>(null)
const porResolver = ref<any[]>([])
const cal = ref<any>(null)
const saldosTop = ref<any[]>([])
const vencimientos = ref<any[]>([])
// Solicitudes
const filtroSol = ref({ estado: 'Pendiente', tipo: '', q: '', soloRh: true, pagina: 1 })
const solicitudes = ref<Pagina<any> | null>(null)
// Calendario
const filtroCal = ref({ equipoId: '' as number | '', todos: false })
// Políticas y saldos
const politicas = ref<any[]>([])
const saldos = ref<Pagina<any> | null>(null)
const filtroSaldos = ref({ q: '', orden: (route.query.orden as string) || 'nombre', dir: 'asc', pagina: 1 })

const base = () => ({ sucursalId: s.sucursalId ?? undefined, areaId: areaId.value || undefined })
const finMes = computed(() => iso(new Date(mes.value.getFullYear(), mes.value.getMonth() + 1, 0)))

async function cargarCatalogos() {
  areas.value = (await api.get('/areas', { sucursalId: s.sucursalId ?? undefined, activos: true, tamano: 100 })).items
  equipos.value = (await api.get('/equipos', { sucursalId: s.sucursalId ?? undefined, activos: true, tamano: 100 })).items
}
async function cargarResumen() {
  const [k, p, c, sa, v] = await Promise.allSettled([
    api.get('/vacaciones/resumen', { ...base(), mes: mesTxt.value }),
    api.get('/solicitudes', { ...base(), pasoRh: true, tamano: 6, orden: 'FechaInicio', dir: 'asc', tipo: 'Vacaciones' }),
    api.get('/ausencias/calendario', { ...base(), mes: mesTxt.value }),
    api.get('/vacaciones/saldos', { ...base(), orden: 'porVencer', dir: 'desc', tamano: 3 }),
    api.get('/vacaciones/vencimientos', { ...base(), tamano: 3, dias: 120 }),
  ])
  if (k.status === 'fulfilled') kpi.value = k.value
  if (p.status === 'fulfilled') porResolver.value = p.value.items
  if (c.status === 'fulfilled') cal.value = c.value
  if (sa.status === 'fulfilled') saldosTop.value = sa.value.items
  if (v.status === 'fulfilled') vencimientos.value = v.value.items
}
async function cargarSolicitudes() {
  const f = filtroSol.value
  solicitudes.value = await api.get('/solicitudes', { ...base(), estado: f.estado || undefined, tipo: f.tipo || undefined, q: f.q || undefined, pasoRh: f.estado === 'Pendiente' && f.soloRh ? true : undefined, desde: iso(mes.value), pagina: f.pagina, tamano: 15, orden: 'FechaInicio', dir: 'asc' })
}
async function cargarCalendario() {
  cal.value = await api.get('/ausencias/calendario', { ...base(), mes: mesTxt.value, equipoId: filtroCal.value.equipoId || undefined, todos: filtroCal.value.todos || undefined })
}
async function cargarSaldos() {
  const f = filtroSaldos.value
  saldos.value = await api.get('/vacaciones/saldos', { ...base(), q: f.q || undefined, orden: f.orden, dir: f.dir, pagina: f.pagina, tamano: 20 })
  politicas.value = await api.get('/politicas-vacaciones')
}
async function cargarTab() {
  try {
    if (tab.value === 'resumen') await cargarResumen()
    else if (tab.value === 'solicitudes') await cargarSolicitudes()
    else if (tab.value === 'calendario') await cargarCalendario()
    else await cargarSaldos()
  } catch (e) { ui.error(e) }
}
onMounted(() => { cargarCatalogos(); cargarTab() })
watch(() => s.sucursalId, () => { areaId.value = ''; cargarCatalogos(); cargarTab() })
watch([tab, mesTxt, areaId], cargarTab)
watch(() => [filtroSol.value.estado, filtroSol.value.tipo, filtroSol.value.soloRh], () => { filtroSol.value.pagina = 1; cargarSolicitudes() })
watch(() => [filtroCal.value.equipoId, filtroCal.value.todos], cargarCalendario)
const buscarSol = debounce(() => { filtroSol.value.pagina = 1; cargarSolicitudes() }, 300)
const buscarSaldos = debounce(() => { filtroSaldos.value.pagina = 1; cargarSaldos() }, 300)
useEventos(t => { if (t === 'solicitud' || t === 'ausencia') cargarTab() })

function moverMes(n: number) { mes.value = new Date(mes.value.getFullYear(), mes.value.getMonth() + n, 1) }
function ordenarSaldos(campo: string) { const f = filtroSaldos.value; f.dir = f.orden === campo && f.dir === 'asc' ? 'desc' : 'asc'; f.orden = campo; cargarSaldos() }

async function aprobar(x: any) {
  const r = await ui.confirmar({ titulo: 'Aprobar solicitud', mensaje: `¿Aprobar ${x.tipoTexto.toLowerCase()} de ${x.colaborador.nombre} (${x.periodo})? Se revalidan saldo y traslapes.`, aceptar: 'Aprobar' })
  if (!r.ok) return
  await ejecutar(async () => { await api.post(`/solicitudes/${x.id}/aprobar`, {}); await cargarTab() }, 'Solicitud aprobada')
}
async function rechazar(x: any) {
  const r = await ui.confirmar({ titulo: 'Rechazar solicitud', mensaje: `${x.colaborador.nombre} · ${x.periodo}`, aceptar: 'Rechazar', peligro: true, pedirTexto: { etiqueta: 'Motivo del rechazo', obligatorio: true } })
  if (!r.ok) return
  await ejecutar(async () => { await api.post(`/solicitudes/${x.id}/rechazar`, { motivo: r.texto }); await cargarTab() }, 'Solicitud rechazada')
}
function abrirAusencia(a: any) {
  if (a.solicitudId) router.push(`/solicitudes/${a.solicitudId}`)
  else if (a.incapacidadId) ui.avisar(`Incapacidad del ${fechaCorta(a.inicio)} al ${fechaCorta(a.fin)}`, 'info')
}

// Detalle de cobertura baja
const cobertura = ref<any>(null)
async function verCobertura(al: any) {
  const r = await ejecutar(() => api.get('/ausencias/cobertura', { equipoId: al.equipoId, desde: al.desde, hasta: al.hasta }))
  if (r) cobertura.value = r
}

// Nueva solicitud capturada por RH (sigue la misma regla de aprobación)
const nueva = ref<any>(null)
const tiposPermiso = ref<any[]>([])
const simNueva = ref<any>(null)
async function abrirNueva() {
  nueva.value = { colaboradorId: null, tipo: 'Vacaciones', tipoPermisoId: null, inicio: null, fin: null, mes: new Date(), coberturaColaboradorId: null, comentario: '' }
  simNueva.value = null
  if (!tiposPermiso.value.length) tiposPermiso.value = (await api.get('/tipos-permiso', { activos: true })).items
}
const simularNueva = debounce(async () => {
  const n = nueva.value
  if (!n?.colaboradorId || !n.inicio) { simNueva.value = null; return }
  try { simNueva.value = await api.post('/solicitudes/simular', { colaboradorId: n.colaboradorId, tipo: n.tipo, tipoPermisoId: n.tipoPermisoId, fechaInicio: n.inicio, fechaFin: n.fin ?? n.inicio, coberturaColaboradorId: n.coberturaColaboradorId }) } catch (e) { ui.error(e) }
}, 250)
watch(() => nueva.value && [nueva.value.colaboradorId, nueva.value.tipo, nueva.value.tipoPermisoId, nueva.value.inicio, nueva.value.fin, nueva.value.coberturaColaboradorId], simularNueva, { deep: true })
async function guardarNueva() {
  const n = nueva.value
  const r = await ejecutar(() => api.post('/solicitudes', { colaboradorId: n.colaboradorId, tipo: n.tipo, tipoPermisoId: n.tipoPermisoId, fechaInicio: n.inicio, fechaFin: n.fin ?? n.inicio, coberturaColaboradorId: n.coberturaColaboradorId, comentario: n.comentario || null }), 'Solicitud registrada; sigue la ruta de aprobación')
  if (r) { nueva.value = null; cargarTab() }
}

// Incapacidad
const incap = ref<any>(null)
async function guardarIncap() {
  const r = await ejecutar(() => api.post('/incapacidades', incap.value), 'Incapacidad registrada')
  if (r) { incap.value = null; cargarTab() }
}
// Ajuste de saldo
const ajuste = ref<any>(null)
async function guardarAjuste() {
  const r = await ejecutar(() => api.post(`/vacaciones/saldos/${ajuste.value.colaboradorId}/ajuste`, { dias: Number(ajuste.value.dias), motivo: ajuste.value.motivo }), 'Saldo ajustado')
  if (r) { ajuste.value = null; cargarSaldos() }
}
const textoVence = (d: number) => d === 0 ? 'Vence hoy' : `Vence en ${d} ${d === 1 ? 'día' : 'días'}`
const otorgadosRef = (x: any) => Math.max(Number(x.otorgados) || 0, 1)
</script>

<template>
  <div class="vac">
    <div class="encabezado">
      <div>
        <h1 class="titulo-pagina">Vacaciones</h1>
        <p class="subtitulo-pagina">Autoriza solicitudes, vigila la cobertura y da seguimiento a saldos</p>
      </div>
      <div class="fila envolver filtros">
        <div class="mes-sel">
          <button class="btn btn-contorno" @click="selMes = !selMes"><Icono n="calendario" :t="19" />{{ mesAnio(mes) }}<Icono n="abajo" :t="16" /></button>
          <div v-if="selMes" class="pop">
            <div class="fila-entre"><button class="btn-icono" @click="moverMes(-12)"><Icono n="izquierda" :t="18" /></button><b>{{ mes.getFullYear() }}</b><button class="btn-icono" @click="moverMes(12)"><Icono n="derecha" :t="18" /></button></div>
            <div class="meses"><button v-for="(m, k) in MESES" :key="m" :class="{ sel: k === mes.getMonth() }" @click="mes = new Date(mes.getFullYear(), k, 1); selMes = false">{{ cap(m.slice(0, 3)) }}</button></div>
          </div>
        </div>
        <select v-model="areaId" class="entrada area" aria-label="Área"><option value="">Área: Todas</option><option v-for="a in areas" :key="a.id" :value="a.id">Área: {{ a.nombre }}</option></select>
        <button class="btn btn-primario" data-prueba="nueva-solicitud-rh" @click="abrirNueva"><Icono n="mas" :t="19" />Nueva solicitud</button>
      </div>
    </div>

    <nav class="pestanas">
      <button class="pestana" :class="{ activa: tab === 'resumen' }" @click="irTab('resumen')">Resumen</button>
      <button class="pestana" :class="{ activa: tab === 'solicitudes' }" @click="irTab('solicitudes')">Solicitudes</button>
      <button class="pestana" :class="{ activa: tab === 'calendario' }" @click="irTab('calendario')">Calendario de equipo</button>
      <button class="pestana" :class="{ activa: tab === 'politicas' }" @click="irTab('politicas')">Políticas y saldos</button>
    </nav>

    <!-- ───── Resumen ───── -->
    <template v-if="tab === 'resumen'">
      <section class="kpis">
        <button class="tarjeta kpi-t" @click="irTab('solicitudes')"><div class="kpi"><span class="kpi-icono" style="background:#fdf0cf;color:#c27a12"><Icono n="documento" :t="28" /></span><div><p class="kpi-titulo">Solicitudes pendientes</p><p class="kpi-valor">{{ kpi?.solicitudesPendientes ?? '—' }}</p></div></div></button>
        <button class="tarjeta kpi-t" @click="irTab('calendario')"><div class="kpi"><span class="kpi-icono" style="background:#dff5e6;color:#0d8a49"><Icono n="palmera" :t="28" /></span><div><p class="kpi-titulo">Personas de vacaciones hoy</p><p class="kpi-valor">{{ kpi?.vacacionesHoy ?? '—' }}</p></div></div></button>
        <button class="tarjeta kpi-t" @click="irTab('politicas', { orden: 'fechaLimite' })"><div class="kpi"><span class="kpi-icono" style="background:#fde7d6;color:#f26913"><Icono n="reloArena" :t="28" /></span><div><p class="kpi-titulo">Días por vencer</p><p class="kpi-valor">{{ kpi ? num(kpi.diasPorVencer, 1) : '—' }}</p></div></div></button>
        <button class="tarjeta kpi-t" @click="irTab('calendario')"><div class="kpi"><span class="kpi-icono" style="background:#dcecfb;color:#2f74d6"><Icono n="personas" :t="28" /></span><div class="crecer"><p class="kpi-titulo">Cobertura de equipo</p><p class="kpi-valor">{{ kpi?.cobertura ?? '—' }}%</p><div class="barra" style="max-width:150px;margin-top:8px"><span :style="{ width: (kpi?.cobertura ?? 0) + '%' }" /></div></div></div></button>
      </section>

      <div class="rejilla">
        <section class="tarjeta">
          <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Solicitudes por resolver</h2><button class="ver-todo" @click="irTab('solicitudes')">Ver todas</button></div>
          <div class="tabla-envoltura">
            <table class="tabla">
              <thead><tr><th>Colaborador</th><th>Periodo</th><th class="num">Días</th><th class="num">Saldo disp.</th><th>Cobertura</th><th>Acciones</th></tr></thead>
              <tbody>
                <tr v-for="x in porResolver" :key="x.id" data-prueba="fila-por-resolver">
                  <td><Persona :p="x.colaborador" :sub="x.colaborador.area" :t="38" enlace /></td>
                  <td class="nw"><RouterLink :to="`/solicitudes/${x.id}`">{{ rango(x.fechaInicio, x.fechaFin, true) }}</RouterLink></td>
                  <td class="num">{{ num(x.diasHabiles) }}</td>
                  <td class="num" :class="{ bajo: x.saldoDisponible !== null && x.saldoDisponible < 0 }">{{ num(x.saldoDisponible, 1) }}</td>
                  <td><Persona v-if="x.cobertura" :p="x.cobertura" :sub="x.cobertura.area" :t="30" /><span v-else class="muted">{{ x.coberturaNota || '—' }}</span></td>
                  <td><div class="acc"><button class="btn btn-primario btn-ch" :disabled="!x.puedeDecidir || ocupado" :title="x.puedeDecidir ? '' : 'Otro usuario de RH debe decidir este paso'" @click="aprobar(x)">Aprobar</button><button class="btn btn-rechazar btn-ch" :disabled="!x.puedeDecidir || ocupado" @click="rechazar(x)">Rechazar</button></div></td>
                </tr>
                <tr v-if="!porResolver.length"><td colspan="6" class="vacio">No hay solicitudes por resolver.</td></tr>
              </tbody>
            </table>
          </div>
        </section>

        <section class="tarjeta">
          <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Saldos y alertas</h2><button class="ver-todo" @click="irTab('politicas', { orden: 'porVencer' })">Ver todos</button></div>
          <div class="saldos-al">
            <div v-for="x in saldosTop" :key="x.colaborador.id" class="saldo-c">
              <div class="fila-entre"><Persona :p="x.colaborador" :sub="x.colaborador.area" :t="38" enlace /><Icono :n="x.porVencer > 0 ? 'alerta' : 'palmera'" :t="24" :class="x.porVencer > 0 ? 'naranja' : 'verde'" /></div>
              <p class="txt">{{ x.porVencer > 0 ? `${num(x.porVencer, 1)} días por vencer` : `${num(x.disponibles, 1)} días disponibles` }}</p>
              <div class="fila"><div class="barra crecer" :class="x.porVencer > 0 ? 'ambar' : 'verde'"><span :style="{ width: Math.min(100, ((x.porVencer > 0 ? x.porVencer : x.disponibles) / otorgadosRef(x)) * 100) + '%' }" /></div><small class="muted">{{ num(x.porVencer > 0 ? x.porVencer : x.disponibles, 1) }} / {{ num(x.otorgados) }} días</small></div>
            </div>
            <p v-if="!saldosTop.length" class="vacio">Sin datos de saldos.</p>
          </div>
        </section>

        <section class="tarjeta">
          <div class="tarjeta-cabecera">
            <h2 class="titulo-tarjeta">Calendario de equipo</h2>
            <div class="fila"><button class="btn-icono" aria-label="Mes anterior" @click="moverMes(-1)"><Icono n="izquierda" :t="18" /></button><b>{{ mesAnio(mes) }}</b><button class="btn-icono" aria-label="Mes siguiente" @click="moverMes(1)"><Icono n="derecha" :t="18" /></button></div>
          </div>
          <div class="leyenda"><span><i class="apr" />Aprobadas</span><span><i class="pen" />Pendientes</span><span><i class="per" />Permiso</span><span><i class="inc" />Incapacidad</span></div>
          <div v-for="al in (cal?.alertas ?? []).slice(0, 2)" :key="al.equipoId + al.desde" class="alerta-cob">
            <Icono n="alerta" :t="20" />
            <span class="crecer"><b>Cobertura baja</b> · {{ al.equipo }} · {{ al.ausentes }} {{ al.ausentes === 1 ? 'persona ausente' : 'personas ausentes' }} <span class="muted">({{ rango(al.desde, al.hasta) }}, {{ al.cobertura }}% vs. {{ al.minimo }}%)</span></span>
            <button class="ver-todo" @click="verCobertura(al)">Ver detalle</button>
          </div>
          <LineaTiempo v-if="cal" :filas="cal.filas.slice(0, 7)" :desde="cal.desde" :hasta="cal.hasta" :festivos="cal.festivos" compacto @abrir="abrirAusencia" />
          <button v-if="cal && cal.filas.length > 7" class="ver-todo" style="margin-top: 10px" @click="irTab('calendario')">Ver las {{ cal.filas.length }} personas con ausencias</button>
        </section>

        <section class="tarjeta">
          <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Próximos vencimientos</h2><button class="ver-todo" @click="irTab('politicas', { orden: 'fechaLimite' })">Ver todos</button></div>
          <table class="tabla">
            <thead><tr><th>Colaborador</th><th class="num">Días por vencer</th><th>Fecha límite</th></tr></thead>
            <tbody>
              <tr v-for="v in vencimientos" :key="v.colaboradorId">
                <td><Persona :p="v.colaborador" :sub="v.colaborador.area" :t="34" enlace /></td>
                <td class="num">{{ num(v.diasPorVencer, 1) }} días</td>
                <td><span class="nw">{{ fechaCorta(v.fechaLimite) }}</span><br><span v-if="v.alerta" class="etiqueta ambar">{{ textoVence(v.diasRestantes) }}</span></td>
              </tr>
              <tr v-if="!vencimientos.length"><td colspan="3" class="vacio">Sin días por vencer.</td></tr>
            </tbody>
          </table>
        </section>
      </div>
    </template>

    <!-- ───── Solicitudes ───── -->
    <section v-else-if="tab === 'solicitudes'" class="tarjeta">
      <div class="barra-filtros">
        <div class="con-icono buscar"><Icono n="buscar" :t="18" /><input v-model="filtroSol.q" class="entrada" placeholder="Buscar por nombre o folio" @input="buscarSol" /></div>
        <select v-model="filtroSol.estado" class="entrada"><option value="">Todos los estados</option><option value="Pendiente">Pendientes</option><option value="Aprobada">Aprobadas</option><option value="Rechazada">Rechazadas</option><option value="Cancelada">Canceladas</option></select>
        <select v-model="filtroSol.tipo" class="entrada"><option value="">Vacaciones y permisos</option><option value="Vacaciones">Vacaciones</option><option value="Permiso">Permisos</option></select>
        <label v-if="filtroSol.estado === 'Pendiente'" class="casilla"><input v-model="filtroSol.soloRh" type="checkbox" />Solo en paso de RH</label>
        <span class="muted pequeno">Desde {{ fechaCorta(iso(mes)) }}</span>
      </div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead><tr><th>Folio</th><th>Colaborador</th><th>Tipo</th><th>Periodo</th><th class="num">Días</th><th class="num">Saldo</th><th>Cobertura</th><th>Estado</th><th>Acciones</th></tr></thead>
          <tbody>
            <tr v-for="x in solicitudes?.items" :key="x.id" class="clic" @click="router.push(`/solicitudes/${x.id}`)">
              <td class="muted">{{ x.folio }}</td>
              <td><Persona :p="x.colaborador" :sub="x.colaborador.area" :t="34" /></td>
              <td>{{ x.tipoTexto }}</td>
              <td class="nw">{{ rango(x.fechaInicio, x.fechaFin, true) }}</td>
              <td class="num">{{ num(x.diasHabiles) }}</td>
              <td class="num">{{ x.saldoDisponible === null ? '—' : num(x.saldoDisponible, 1) }}</td>
              <td><span v-if="x.cobertura">{{ x.cobertura.nombre }}</span><span v-else class="muted">—</span></td>
              <td><Estado :v="x.estado" /><br><small v-if="x.pasoActualTexto" class="muted">Paso: {{ x.pasoActualTexto }}</small></td>
              <td @click.stop><div v-if="x.puedeDecidir" class="acc"><button class="btn btn-primario btn-ch" :disabled="ocupado" @click="aprobar(x)">Aprobar</button><button class="btn btn-rechazar btn-ch" :disabled="ocupado" @click="rechazar(x)">Rechazar</button></div></td>
            </tr>
            <tr v-if="solicitudes && !solicitudes.items.length"><td colspan="9" class="vacio">No hay solicitudes con estos filtros.</td></tr>
          </tbody>
        </table>
      </div>
      <Paginacion v-if="solicitudes" :pagina="solicitudes.paginaActual" :paginas="solicitudes.paginas" :total="solicitudes.total" :tamano="solicitudes.tamano" @ir="p => { filtroSol.pagina = p; cargarSolicitudes() }" />
    </section>

    <!-- ───── Calendario de equipo ───── -->
    <section v-else-if="tab === 'calendario'" class="tarjeta">
      <div class="tarjeta-cabecera envolver">
        <div class="fila"><button class="btn-icono borde" aria-label="Mes anterior" @click="moverMes(-1)"><Icono n="izquierda" :t="18" /></button><b class="mes-t">{{ mesAnio(mes) }}</b><button class="btn-icono borde" aria-label="Mes siguiente" @click="moverMes(1)"><Icono n="derecha" :t="18" /></button></div>
        <div class="fila envolver">
          <select v-model="filtroCal.equipoId" class="entrada" style="width:auto"><option value="">Todos los equipos</option><option v-for="e in equipos" :key="e.id" :value="e.id">{{ e.nombre }} ({{ e.area }})</option></select>
          <label class="casilla"><input v-model="filtroCal.todos" type="checkbox" />Mostrar a todo el personal</label>
          <button class="btn btn-contorno btn-ch" @click="incap = { colaboradorId: null, fechaInicio: iso(new Date()), fechaFin: iso(new Date()), folio: '', tipo: 'Enfermedad general', nota: '' }"><Icono n="mas" :t="16" />Registrar incapacidad</button>
        </div>
      </div>
      <div class="leyenda"><span><i class="apr" />Aprobadas</span><span><i class="pen" />Pendientes</span><span><i class="per" />Permiso</span><span><i class="inc" />Incapacidad</span></div>
      <div v-for="al in cal?.alertas ?? []" :key="al.equipoId + al.desde" class="alerta-cob">
        <Icono n="alerta" :t="20" />
        <span class="crecer"><b>Cobertura baja</b> · {{ al.equipo }} · {{ al.ausentes }} {{ al.ausentes === 1 ? 'persona ausente' : 'personas ausentes' }} <span class="muted">({{ rango(al.desde, al.hasta) }}, {{ al.cobertura }}% de {{ al.miembros }}; mínimo {{ al.minimo }}%)</span></span>
        <button class="ver-todo" @click="verCobertura(al)">Ver detalle</button>
      </div>
      <LineaTiempo v-if="cal" :filas="cal.filas" :desde="cal.desde" :hasta="cal.hasta" :festivos="cal.festivos" @abrir="abrirAusencia" />
    </section>

    <!-- ───── Políticas y saldos ───── -->
    <template v-else>
      <section class="politicas">
        <div v-for="p in politicas" :key="p.id" class="tarjeta compacta">
          <div class="fila-entre"><h3 class="titulo-tarjeta" style="font-size:17px">{{ p.nombre }}</h3><span v-if="p.porDefecto" class="etiqueta verde">Por omisión</span></div>
          <p class="muted pequeno" style="margin: 6px 0 10px">Vencimiento a {{ p.plazoVencimientoMeses }} meses · alerta {{ p.diasAlertaVencimiento }} días antes{{ p.anticipacionMinimaDias ? ` · anticipación mínima ${p.anticipacionMinimaDias} días` : '' }}</p>
          <div class="fila envolver" style="gap:6px"><span v-for="t in p.tabla" :key="t.id" class="chip pequeno" style="height:30px">{{ t.aniosDesde }}–{{ t.aniosHasta > 50 ? '+' : t.aniosHasta }} años: <b>{{ t.dias }} días</b></span></div>
          <RouterLink v-if="s.tiene('vacaciones.gestionar')" to="/configuracion/politicas" class="ver-todo" style="margin-top: 10px">Editar política <Icono n="flecha" :t="15" /></RouterLink>
        </div>
      </section>
      <section class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Saldos por colaborador</h2><div class="con-icono" style="width: 300px"><Icono n="buscar" :t="18" /><input v-model="filtroSaldos.q" class="entrada" placeholder="Buscar colaborador" @input="buscarSaldos" /></div></div>
        <div class="tabla-envoltura">
          <table class="tabla">
            <thead><tr>
              <th class="ordenable" @click="ordenarSaldos('nombre')">Colaborador</th><th class="num">Otorgados</th><th class="num">Usados</th><th class="num">Comprometidos</th>
              <th class="num ordenable" @click="ordenarSaldos('disponibles')">Disponibles</th><th class="num ordenable" @click="ordenarSaldos('porVencer')">Por vencer</th>
              <th class="ordenable" @click="ordenarSaldos('fechaLimite')">Fecha límite</th><th />
            </tr></thead>
            <tbody>
              <tr v-for="x in saldos?.items" :key="x.colaborador.id">
                <td><Persona :p="x.colaborador" :sub="x.colaborador.area" :t="34" enlace /></td>
                <td class="num">{{ num(x.otorgados, 1) }}</td><td class="num">{{ num(x.usados, 1) }}</td><td class="num">{{ num(x.comprometidos, 1) }}</td>
                <td class="num negrita">{{ num(x.disponibles, 1) }}</td>
                <td class="num"><span :class="{ naranja: x.porVencer > 0 }">{{ num(x.porVencer, 1) }}</span></td>
                <td class="nw">{{ x.fechaLimite ? fechaCorta(x.fechaLimite) : '—' }}</td>
                <td><button class="btn btn-contorno btn-ch" @click="ajuste = { colaboradorId: x.colaborador.id, nombre: x.colaborador.nombre, dias: 1, motivo: '' }">Ajustar</button></td>
              </tr>
            </tbody>
          </table>
        </div>
        <Paginacion v-if="saldos" :pagina="saldos.paginaActual" :paginas="saldos.paginas" :total="saldos.total" :tamano="saldos.tamano" @ir="p => { filtroSaldos.pagina = p; cargarSaldos() }" />
      </section>
    </template>

    <!-- Diálogos -->
    <Modal v-if="cobertura" :titulo="`Cobertura · ${cobertura.equipo.nombre}`" :subtitulo="`${cobertura.equipo.area} · ${cobertura.miembros} integrantes · mínimo ${cobertura.equipo.coberturaMinima}%`" :ancho="720" @cerrar="cobertura = null">
      <table class="tabla">
        <thead><tr><th>Día</th><th class="num">Programados</th><th>Ausentes</th><th class="num">Cobertura</th></tr></thead>
        <tbody>
          <tr v-for="d in cobertura.dias" :key="d.fecha" :class="{ critico: d.bajoMinimo }">
            <td class="nw">{{ fechaCorta(d.fecha, false) }}</td>
            <td class="num">{{ d.programados }}</td>
            <td><span v-for="a in d.ausentes" :key="a.colaborador.id" class="chip pequeno" style="height:28px;margin:2px">{{ a.colaborador.nombre }} · {{ a.texto }}</span><span v-if="d.pendientesPorAprobar" class="etiqueta ambar">+{{ d.pendientesPorAprobar }} pendientes</span></td>
            <td class="num"><span class="etiqueta" :class="d.bajoMinimo ? 'rojo' : 'verde'">{{ d.cobertura }}%</span></td>
          </tr>
        </tbody>
      </table>
    </Modal>

    <Modal v-if="nueva" titulo="Nueva solicitud" subtitulo="Se captura a nombre del colaborador y sigue la misma regla de aprobación." :ancho="760" @cerrar="nueva = null">
      <div class="nueva">
        <div class="columna">
          <div class="campo"><label>Colaborador</label><SelectorColaborador v-model="nueva.colaboradorId" :sucursal-id="s.sucursalId" /></div>
          <div class="campo"><label>Tipo</label><select v-model="nueva.tipo" class="entrada"><option value="Vacaciones">Vacaciones</option><option value="Permiso">Permiso</option></select></div>
          <div v-if="nueva.tipo === 'Permiso'" class="campo"><label>Tipo de permiso</label><select v-model="nueva.tipoPermisoId" class="entrada"><option :value="null">Selecciona…</option><option v-for="t in tiposPermiso" :key="t.id" :value="t.id">{{ t.nombre }}</option></select></div>
          <div class="campo"><label>Cobertura (opcional)</label><SelectorColaborador v-model="nueva.coberturaColaboradorId" :excluir="nueva.colaboradorId ? [nueva.colaboradorId] : []" /></div>
          <div class="campo"><label>Comentario</label><textarea v-model="nueva.comentario" class="entrada" rows="2" /></div>
        </div>
        <div class="columna">
          <Calendario v-model:mes="nueva.mes" :inicio="nueva.inicio" :fin="nueva.fin" :descanso="simNueva?.diasDescanso ?? [6, 7]" :festivos="(simNueva?.festivos ?? []).map((f: any) => f.fecha)" @rango="(a, b) => { nueva.inicio = a; nueva.fin = b }" />
          <div v-if="simNueva" class="sim">
            <p><b>{{ simNueva.periodoTexto }}</b> · {{ simNueva.diasHabiles }} días hábiles<template v-if="simNueva.saldoPosterior !== null"> · saldo después: {{ num(simNueva.saldoPosterior, 1) }}</template></p>
            <p class="muted pequeno">Ruta: {{ simNueva.ruta.map((p: any) => p.titular?.nombre ?? 'Recursos Humanos').join(' → ') || '—' }}</p>
            <p v-for="v in simNueva.validaciones.filter((x: any) => x.nivel !== 'ok')" :key="v.mensaje" :class="v.nivel === 'error' ? 'err' : 'avi'">{{ v.mensaje }}</p>
          </div>
        </div>
      </div>
      <template #pie><button class="btn btn-contorno" @click="nueva = null">Cancelar</button><button class="btn btn-primario" :disabled="!simNueva?.valida || ocupado" @click="guardarNueva">Registrar solicitud</button></template>
    </Modal>

    <Modal v-if="incap" titulo="Registrar incapacidad" @cerrar="incap = null">
      <div class="columna">
        <div class="campo"><label>Colaborador</label><SelectorColaborador v-model="incap.colaboradorId" :sucursal-id="s.sucursalId" /></div>
        <div class="rejilla-form">
          <div class="campo"><label>Desde</label><input v-model="incap.fechaInicio" type="date" class="entrada" /></div>
          <div class="campo"><label>Hasta</label><input v-model="incap.fechaFin" type="date" class="entrada" :min="incap.fechaInicio" /></div>
          <div class="campo"><label>Folio IMSS</label><input v-model="incap.folio" class="entrada" /></div>
          <div class="campo"><label>Tipo</label><select v-model="incap.tipo" class="entrada"><option>Enfermedad general</option><option>Riesgo de trabajo</option><option>Maternidad</option></select></div>
        </div>
        <div class="campo"><label>Nota</label><textarea v-model="incap.nota" class="entrada" rows="2" /></div>
      </div>
      <template #pie><button class="btn btn-contorno" @click="incap = null">Cancelar</button><button class="btn btn-primario" :disabled="!incap.colaboradorId || ocupado" @click="guardarIncap">Registrar</button></template>
    </Modal>

    <Modal v-if="ajuste" titulo="Ajustar saldo" :subtitulo="ajuste.nombre" @cerrar="ajuste = null">
      <div class="columna">
        <div class="campo"><label>Días (positivo suma, negativo resta)</label><input v-model="ajuste.dias" type="number" step="0.5" class="entrada" /></div>
        <div class="campo"><label>Motivo (queda en la bitácora)</label><textarea v-model="ajuste.motivo" class="entrada" rows="2" /></div>
      </div>
      <template #pie><button class="btn btn-contorno" @click="ajuste = null">Cancelar</button><button class="btn btn-primario" :disabled="!ajuste.motivo.trim() || !Number(ajuste.dias) || ocupado" @click="guardarAjuste">Guardar ajuste</button></template>
    </Modal>
  </div>
</template>

<style scoped>
.vac { display: flex; flex-direction: column; gap: 20px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; }
.filtros { gap: 10px; }
.area { width: auto; min-width: 170px; }
.mes-sel { position: relative; }
.pop { position: absolute; top: calc(100% + 6px); left: 0; z-index: 40; background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; box-shadow: var(--sombra-m); padding: 10px; width: 260px; }
.meses { display: grid; grid-template-columns: repeat(4, 1fr); gap: 6px; margin-top: 8px; }
.meses button { border: 0; background: var(--gris-50); border-radius: 8px; padding: 8px 0; font-size: 13.5px; }
.meses button.sel { background: var(--verde); color: #fff; font-weight: 600; }
.kpis { display: grid; grid-template-columns: repeat(4, 1fr); gap: 20px; }
.kpi-t { text-align: left; font: inherit; cursor: pointer; }
.kpi-t:hover { box-shadow: var(--sombra-m); }
.rejilla { display: grid; grid-template-columns: minmax(0, 1.9fr) minmax(320px, 1fr); gap: 20px; align-items: start; }
.nw { white-space: nowrap; }
.acc { display: flex; gap: 8px; }
.bajo { color: var(--rojo-texto); font-weight: 700; }
.saldos-al { display: flex; flex-direction: column; gap: 12px; }
.saldo-c { border: 1px solid var(--gris-200); border-radius: 10px; padding: 12px 14px; }
.saldo-c .txt { margin: 8px 0 6px; font-weight: 600; color: var(--titulo); font-size: 15px; }
.verde { color: var(--verde-osc); }
.naranja { color: var(--naranja); font-weight: 600; }
.leyenda { display: flex; gap: 18px; flex-wrap: wrap; font-size: 13px; color: var(--texto-2); margin-bottom: 12px; }
.leyenda span { display: flex; align-items: center; gap: 7px; }
.leyenda i { width: 14px; height: 14px; border-radius: 4px; display: inline-block; }
.leyenda .apr { background: var(--verde); }
.leyenda .pen { background: #f3a51c; }
.leyenda .per { background: var(--azul-punto); }
.leyenda .inc { background: var(--rojo); }
.alerta-cob { display: flex; align-items: center; gap: 10px; background: var(--rojo-claro); color: var(--rojo-texto); border-radius: 10px; padding: 10px 14px; margin-bottom: 10px; font-size: 14px; }
.alerta-cob b { color: var(--rojo-texto); }
.mes-t { min-width: 150px; text-align: center; font-size: 17px; }
.politicas { display: grid; grid-template-columns: repeat(auto-fill, minmax(340px, 1fr)); gap: 16px; }
.critico td { background: #fff7f8; }
.nueva { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
.sim { margin-top: 12px; font-size: 14px; display: flex; flex-direction: column; gap: 6px; }
.sim .err { color: var(--rojo-texto); background: var(--rojo-claro); padding: 6px 10px; border-radius: 6px; }
.sim .avi { color: #8a5a0b; background: var(--ambar-claro); padding: 6px 10px; border-radius: 6px; }
@media (max-width: 1250px) { .rejilla { grid-template-columns: 1fr; } .kpis { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 760px) { .kpis, .nueva { grid-template-columns: 1fr; } }
</style>
