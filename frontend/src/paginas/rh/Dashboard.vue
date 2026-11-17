<script setup lang="ts">
// Resumen de hoy (Dashboard.png): 4 indicadores y 6 paneles, en tiempo real.
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import GraficaAsistencia from '@/componentes/GraficaAsistencia.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useEventos } from '@/utils/usos'
import { fechaMedia, hora, iso, num } from '@/utils/formato'

const s = useSesion()
const ui = useUi()
const router = useRouter()
const resumen = ref<any>(null)
const serie = ref<any[]>([])
const solicitudes = ref<any[]>([])
const registros = ref<any[]>([])
const cumples = ref<any[]>([])
const documentos = ref<any[]>([])
const visitantes = ref<any[]>([])
const ordenVis = ref<{ campo: string; dir: 'asc' | 'desc' }>({ campo: 'llegada', dir: 'asc' })
const ocupado = ref<number | null>(null)
const hoyIso = () => iso(new Date())
let intervalo: ReturnType<typeof setInterval>

const suc = () => ({ sucursalId: s.sucursalId ?? undefined })
async function cargarResumen() { resumen.value = await api.get('/dashboard/resumen', suc()) }
async function cargarSerie() { serie.value = (await api.get('/dashboard/asistencia-por-hora', suc())).puntos }
async function cargarSolicitudes() { solicitudes.value = (await api.get('/solicitudes', { ...suc(), pasoRh: true, tamano: 3, orden: 'FechaInicio', dir: 'asc' })).items }
async function cargarRegistros() { registros.value = (await api.get('/asistencia/registros', { ...suc(), agrupado: true, fecha: hoyIso(), tamano: 6 })).items }
async function cargarCumples() { cumples.value = (await api.get('/colaboradores/cumpleanos', suc())).slice(0, 3) }
async function cargarDocs() { documentos.value = (await api.get('/expedientes/por-vencer', { ...suc(), tamano: 3, incluirVencidos: false })).items }
async function cargarVisitantes() {
  visitantes.value = (await api.get('/visitantes/invitaciones', { ...suc(), fecha: hoyIso(), tamano: 5, orden: ordenVis.value.campo, dir: ordenVis.value.dir, estado: 'Dentro' })).items
}
async function todo() {
  await Promise.allSettled([cargarResumen(), cargarSerie(), cargarSolicitudes(), cargarRegistros(), cargarCumples(), cargarDocs(), cargarVisitantes()])
}
onMounted(() => { todo(); intervalo = setInterval(() => { cargarResumen(); cargarSerie() }, 60000) })
onBeforeUnmount(() => clearInterval(intervalo))
watch(() => s.sucursalId, todo)
useEventos(tipo => {
  if (tipo === 'asistencia') { cargarResumen(); cargarSerie(); cargarRegistros() }
  if (tipo === 'incidencia') cargarResumen()
  if (tipo === 'solicitud') cargarSolicitudes()
  if (tipo === 'visitante') { cargarResumen(); cargarVisitantes() }
})

function ordenar(campo: string) {
  ordenVis.value = { campo, dir: ordenVis.value.campo === campo && ordenVis.value.dir === 'asc' ? 'desc' : 'asc' }
  cargarVisitantes()
}

async function aprobar(sol: any) {
  const r = await ui.confirmar({ titulo: 'Aprobar solicitud', mensaje: `¿Aprobar ${sol.tipoTexto.toLowerCase()} de ${sol.colaborador.nombre} (${sol.periodo}, ${num(sol.diasHabiles)} días hábiles)?`, aceptar: 'Aprobar' })
  if (!r.ok) return
  ocupado.value = sol.id
  try { await api.post(`/solicitudes/${sol.id}/aprobar`, {}); ui.avisar('Solicitud aprobada. Se avisó al colaborador.'); await Promise.all([cargarSolicitudes(), cargarResumen()]) } catch (e) { ui.error(e) } finally { ocupado.value = null }
}
async function rechazar(sol: any) {
  const r = await ui.confirmar({ titulo: 'Rechazar solicitud', mensaje: `${sol.colaborador.nombre} · ${sol.periodo}`, aceptar: 'Rechazar', peligro: true, pedirTexto: { etiqueta: 'Motivo del rechazo', obligatorio: true, placeholder: 'Explica el motivo al colaborador' } })
  if (!r.ok) return
  ocupado.value = sol.id
  try { await api.post(`/solicitudes/${sol.id}/rechazar`, { motivo: r.texto }); ui.avisar('Solicitud rechazada.'); await cargarSolicitudes() } catch (e) { ui.error(e) } finally { ocupado.value = null }
}

const fechaHoy = computed(() => fechaMedia(new Date()))
const r = computed(() => resumen.value)
const docTexto = (d: any) => d.dias === 0 ? 'Vence hoy' : `Vence en ${d.dias} ${d.dias === 1 ? 'día' : 'días'}`
</script>

<template>
  <div class="dashboard">
    <div class="encabezado">
      <div>
        <h1 class="titulo-pagina">Resumen de hoy</h1>
        <p class="subtitulo-pagina sub">{{ r?.subtitulo || s.frase('dashboard_subtitulo') }} <Icono n="hoja" :t="20" class="hoja" /></p>
      </div>
      <div v-if="s.frase('dashboard_banner')" class="banner">
        <Icono n="destellos" :t="38" :g="1.6" class="dest" />
        <p>{{ s.frase('dashboard_banner') }}</p>
      </div>
    </div>

    <!-- Indicadores -->
    <section class="kpis">
      <RouterLink to="/asistencia?tab=registros" class="tarjeta kpi-t" data-prueba="kpi-presentes">
        <div class="kpi">
          <span class="kpi-icono" style="background:#dff5e6;color:#0d8a49"><Icono n="personas" :t="30" :g="2.2" /></span>
          <div class="crecer">
            <p class="kpi-titulo">Presentes hoy</p>
            <p class="kpi-valor">{{ r?.presentes.valor ?? '—' }}<span class="kpi-de">de {{ r?.presentes.plantilla ?? '—' }}</span></p>
            <div class="barra-pct"><div class="barra"><span :style="{ width: (r?.presentes.porcentaje ?? 0) + '%' }" /></div><span>{{ r?.presentes.porcentaje ?? 0 }}%</span></div>
          </div>
        </div>
      </RouterLink>
      <RouterLink to="/asistencia?tab=registros&estado=EnHorario" class="tarjeta kpi-t">
        <div class="kpi">
          <span class="kpi-icono" style="background:#d6eef0"><span class="reloj-lleno"><Icono n="reloj" :t="22" :g="2.6" /></span></span>
          <div class="crecer">
            <p class="kpi-titulo">Asistencia puntual</p>
            <p class="kpi-valor">{{ r?.puntualidad.porcentaje ?? 0 }}%<span class="kpi-de">{{ r?.puntualidad.puntuales ?? 0 }} de {{ r?.puntualidad.entradas ?? 0 }}</span></p>
            <div class="barra-pct"><div class="barra"><span :style="{ width: (r?.puntualidad.porcentaje ?? 0) + '%' }" /></div></div>
          </div>
        </div>
      </RouterLink>
      <RouterLink to="/asistencia?tab=incidencias" class="tarjeta kpi-t">
        <div class="kpi">
          <span class="kpi-icono" style="background:#fde7d6"><span class="alerta-llena"><Icono n="alerta" :t="22" :g="2.4" /></span></span>
          <div class="crecer">
            <p class="kpi-titulo">Incidencias por revisar</p>
            <p class="kpi-valor">{{ r?.incidencias.porRevisar ?? 0 }}
              <span v-if="r && r.incidencias.variacion !== 0" class="variacion" :class="r.incidencias.variacion > 0 ? 'sube' : 'baja'">{{ r.incidencias.variacion > 0 ? '▲' : '▼' }} {{ Math.abs(r.incidencias.variacion) }}</span>
            </p>
            <p class="kpi-nota">vs. día anterior</p>
          </div>
        </div>
      </RouterLink>
      <RouterLink to="/visitantes?estado=Dentro" class="tarjeta kpi-t">
        <div class="kpi">
          <span class="kpi-icono" style="background:#dcecfb;color:#2f74d6"><Icono n="persona" :t="30" :g="2.2" /></span>
          <div class="crecer">
            <p class="kpi-titulo">Visitantes dentro</p>
            <p class="kpi-valor">{{ r?.visitantes.dentro ?? 0 }}<span class="kpi-de">de {{ r?.visitantes.registradosHoy ?? 0 }} registrados hoy</span></p>
          </div>
        </div>
      </RouterLink>
    </section>

    <div class="rejilla">
      <!-- Asistencia en tiempo real -->
      <section class="tarjeta grafica-t">
        <div class="tarjeta-cabecera">
          <div class="izq"><Icono n="tendencia" :t="24" class="verde" /><h2 class="titulo-tarjeta t-s">Asistencia en tiempo real</h2></div>
          <span class="muted pequeno">Hoy, {{ fechaHoy }}</span>
        </div>
        <GraficaAsistencia :puntos="serie" />
      </section>

      <!-- Solicitudes pendientes -->
      <section class="tarjeta solicitudes-t">
        <div class="tarjeta-cabecera">
          <div class="izq"><Icono n="documento" :t="24" class="verde" /><h2 class="titulo-tarjeta t-s">Solicitudes pendientes</h2></div>
          <RouterLink to="/vacaciones?tab=solicitudes" class="ver-todo">Ver todas</RouterLink>
        </div>
        <div class="tabla-envoltura">
          <table class="tabla">
            <thead><tr><th>Colaborador</th><th>Tipo</th><th>Fechas</th><th>Acciones</th></tr></thead>
            <tbody>
              <tr v-for="x in solicitudes" :key="x.id" data-prueba="fila-solicitud">
                <td><Persona :p="x.colaborador" :t="40" enlace /></td>
                <td><span class="tipo"><Icono :n="x.tipo === 'Vacaciones' ? 'palmera' : 'calendario'" :t="22" :class="x.tipo === 'Vacaciones' ? 'verde' : 'gris'" />{{ x.tipoTexto }}</span></td>
                <td class="fechas">{{ x.periodo.replace(/ de \d{4}$/, '') }}<br><span>({{ num(x.diasHabiles) }} {{ x.diasHabiles == 1 ? 'día' : 'días' }})</span></td>
                <td>
                  <div class="acciones">
                    <button class="btn btn-primario btn-ch" :disabled="ocupado === x.id" @click="aprobar(x)">Aprobar</button>
                    <button class="btn btn-rechazar btn-ch" :disabled="ocupado === x.id" @click="rechazar(x)">Rechazar</button>
                  </div>
                </td>
              </tr>
              <tr v-if="!solicitudes.length"><td colspan="4" class="vacio">No hay solicitudes pendientes para Recursos Humanos.</td></tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- Últimos registros -->
      <section class="tarjeta registros-t">
        <div class="tarjeta-cabecera">
          <div class="izq"><Icono n="reloj" :t="24" class="verde" /><h2 class="titulo-tarjeta t-s">Últimos registros de asistencia</h2></div>
          <RouterLink to="/asistencia?tab=registros" class="ver-todo">Ver todos</RouterLink>
        </div>
        <div class="tabla-envoltura">
          <table class="tabla">
            <thead><tr><th>Colaborador</th><th>Entrada</th><th>Salida</th><th>Estado</th></tr></thead>
            <tbody>
              <tr v-for="x in registros" :key="x.id" class="clic" @click="router.push(`/colaboradores/${x.colaborador.id}?tab=asistencia`)">
                <td><Persona :p="x.colaborador" :t="40" /></td>
                <td>{{ hora(x.entrada) || '—' }}</td>
                <td>{{ hora(x.salida) || '—' }}</td>
                <td><Estado :v="x.estado" /></td>
              </tr>
              <tr v-if="!registros.length"><td colspan="4" class="vacio">Aún no hay registros hoy.</td></tr>
            </tbody>
          </table>
        </div>
      </section>

      <div class="columna-der">
        <!-- Próximos cumpleaños -->
        <section class="tarjeta">
          <div class="tarjeta-cabecera">
            <div class="izq"><Icono n="regalo" :t="24" class="rojo" /><h2 class="titulo-tarjeta t-s">Próximos cumpleaños</h2></div>
            <RouterLink to="/colaboradores?orden=nombre" class="ver-todo">Ver todos</RouterLink>
          </div>
          <div class="cumples">
            <RouterLink v-for="c in cumples" :key="c.colaborador.id" :to="`/colaboradores/${c.colaborador.id}`" class="cumple">
              <div class="fila">
                <Avatar :nombre="c.colaborador.nombre" :iniciales="c.colaborador.iniciales" :foto="c.colaborador.fotoUrl" :t="46" />
                <div class="crecer">
                  <b>{{ c.colaborador.nombre }}</b>
                  <small>{{ c.colaborador.puesto }}</small>
                  <span class="fecha"><Icono n="calendario" :t="15" />{{ c.esHoy ? 'Hoy' : fechaMedia(c.fecha) }}</span>
                </div>
              </div>
              <span class="mensaje"><Icono n="pastel" :t="18" />{{ c.mensaje }}</span>
            </RouterLink>
            <p v-if="!cumples.length" class="vacio">No hay cumpleaños en los próximos días.</p>
          </div>
        </section>

        <div class="doble">
          <!-- Documentos por vencer -->
          <section class="tarjeta">
            <div class="tarjeta-cabecera">
              <div class="izq"><Icono n="documento" :t="24" class="rojo" /><h2 class="titulo-tarjeta t-s">Documentos por vencer</h2></div>
              <RouterLink to="/expedientes?tab=por-vencer" class="ver-todo">Ver todos</RouterLink>
            </div>
            <RouterLink v-for="d in documentos" :key="d.id" :to="`/colaboradores/${d.colaborador.id}?tab=expediente&documento=${d.id}`" class="doc">
              <Icono n="documento" :t="22" class="gris" />
              <span class="crecer"><b>{{ d.documento }} - {{ d.colaborador.nombre }}</b><span class="vence"><Icono n="reloj" :t="14" />{{ docTexto(d) }}</span></span>
              <span class="etiqueta cuadrada" :class="d.estado === 'Urgente' ? 'rojo' : 'ambar'">{{ d.estado === 'Urgente' ? 'Urgente' : 'Próximo' }}</span>
            </RouterLink>
            <p v-if="!documentos.length" class="vacio">Sin documentos por vencer.</p>
          </section>

          <!-- Visitantes de hoy -->
          <section class="tarjeta">
            <div class="tarjeta-cabecera">
              <div class="izq"><Icono n="personas" :t="24" class="verde" /><h2 class="titulo-tarjeta t-s">Visitantes de hoy</h2></div>
              <RouterLink to="/visitantes" class="ver-todo">Ver todos</RouterLink>
            </div>
            <div class="tabla-envoltura">
              <table class="tabla compacta">
                <thead><tr>
                  <th class="ordenable sub" @click="ordenar('nombre')">Nombre</th>
                  <th class="ordenable"><span class="sub" @click="ordenar('empresa')">Empresa</span> / Motivo</th>
                  <th>Entrada</th><th>Estado</th>
                </tr></thead>
                <tbody>
                  <tr v-for="v in visitantes" :key="v.id" class="clic" @click="router.push(`/visitantes/${v.id}`)">
                    <td class="negrita">{{ v.nombre }}</td>
                    <td class="muted">{{ v.empresaMotivo }}</td>
                    <td>{{ hora(v.entrada) || hora(v.llegadaLocal) }}</td>
                    <td><Estado :v="v.estado" /></td>
                  </tr>
                  <tr v-if="!visitantes.length"><td colspan="4" class="vacio">Sin visitantes dentro.</td></tr>
                </tbody>
              </table>
            </div>
          </section>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.dashboard { display: flex; flex-direction: column; gap: 20px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-start; gap: 20px; }
.sub { display: flex; align-items: center; gap: 8px; }
.hoja { color: #3fbf6f; }
.banner { display: flex; align-items: center; gap: 18px; background: #dcf4f0; border-radius: 12px; padding: 18px 26px 18px 20px; min-width: 272px; }
.banner p { font-size: 17px; color: #0b4d45; line-height: 1.35; font-weight: 500; max-width: 190px; }
.dest { color: #0c9b78; }
.kpis { display: grid; grid-template-columns: repeat(4, 1fr); gap: 20px; }
.kpi-t { color: inherit; text-decoration: none !important; transition: box-shadow .15s, border-color .15s; padding: 18px 20px 20px; }
.kpi-t:hover { border-color: var(--gris-300); box-shadow: var(--sombra-m); }
.barra-pct { display: flex; align-items: center; gap: 14px; margin-top: 10px; font-size: 14px; color: var(--texto-2); }
.barra-pct .barra { flex: 1; max-width: 148px; }
.kpi-valor { margin-top: 6px; display: flex; align-items: baseline; }
.kpi-nota { font-size: 14px; color: var(--texto-2); margin-top: 4px; }
.reloj-lleno { width: 36px; height: 36px; border-radius: 50%; background: #11937a; color: #fff; display: grid; place-items: center; }
.alerta-llena { width: 40px; height: 40px; border-radius: 50%; background: #f26913; color: #fff; display: grid; place-items: center; padding-bottom: 2px; }
.variacion { font-size: 15px; font-weight: 600; margin-left: 14px; letter-spacing: 0; }
.variacion.sube { color: #e23b3b; }
.variacion.baja { color: var(--verde-texto); }
.rejilla { display: grid; grid-template-columns: minmax(0, 7fr) minmax(0, 9fr); gap: 20px; align-items: start; }
.t-s { font-size: 19px; }
.verde { color: var(--teal); }
.rojo { color: #e5484d; }
.gris { color: var(--texto-3); }
.tipo { display: inline-flex; align-items: center; gap: 8px; white-space: nowrap; }
.fechas { white-space: nowrap; }
.fechas span { color: var(--texto-2); }
.acciones { display: flex; gap: 10px; }
.acciones .btn-ch { height: 36px; padding: 0 22px; }
.columna-der { display: flex; flex-direction: column; gap: 20px; }
.cumples { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; }
.cumple { border: 1px solid var(--gris-200); border-radius: 10px; padding: 12px; display: flex; flex-direction: column; gap: 12px; color: inherit; text-decoration: none !important; }
.cumple:hover { border-color: var(--gris-300); }
.cumple b { display: block; font-size: 14.5px; color: var(--titulo); }
.cumple small { display: block; color: var(--texto-3); font-size: 13px; }
.cumple .fecha { display: flex; align-items: center; gap: 6px; font-size: 13px; color: var(--texto-2); margin-top: 6px; }
.mensaje { display: flex; align-items: center; gap: 8px; justify-content: center; background: #e8f7ef; color: #0b6b44; border-radius: 8px; padding: 8px 6px; font-size: 13.5px; font-weight: 500; text-align: center; }
.mensaje .icono { color: #e5484d; }
.doble { display: grid; grid-template-columns: minmax(0, 0.79fr) minmax(0, 1fr); gap: 20px; }
.doc { display: flex; align-items: center; gap: 12px; padding: 11px 0; border-bottom: 1px solid var(--gris-100); color: inherit; text-decoration: none !important; }
.doc:last-of-type { border-bottom: 0; }
.doc b { display: block; font-size: 13.5px; font-weight: 600; color: var(--titulo); }
.vence { display: flex; align-items: center; gap: 4px; color: #e23b3b; font-size: 12.5px; margin-top: 2px; }
.compacta { font-size: 13px; }
.compacta th, .compacta td { padding: 8px 8px; }
.sub { text-decoration: underline; text-underline-offset: 2px; }
th .sub { cursor: pointer; }
@media (max-width: 1350px) {
  .rejilla { grid-template-columns: 1fr; }
  .doble { grid-template-columns: 1fr 1fr; }
}
@media (max-width: 1100px) { .kpis { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 760px) {
  .encabezado { flex-direction: column; }
  .banner { min-width: 0; width: 100%; }
  .kpis, .doble, .cumples { grid-template-columns: 1fr; }
}
</style>
