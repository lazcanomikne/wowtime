<script setup lang="ts">
// Mi asistencia (colaborador): jornada de hoy, resumen del mes, días registrados e incidencias propias.
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Estado from '@/componentes/Estado.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useEsMovil } from '@/utils/usos'
import { DIAS, MESES_CORTOS, aFecha, cap, fechaCorta, fechaLarga, fechaMedia, hora, iso, mesAnio, num } from '@/utils/formato'

const s = useSesion()
const ui = useUi()
const router = useRouter()
const esMovil = useEsMovil()
const colaboradorId = computed(() => s.me?.colaborador?.id ?? null)

// ───── Hoy ─────
const hoy = ref<any>(null)
const cargandoHoy = ref(true)
async function cargarHoy() {
  cargandoHoy.value = true
  try { hoy.value = await api.get('/asistencia/jornada-hoy') } catch (e) { ui.error(e) } finally { cargandoHoy.value = false }
}
const jornada = computed(() => hoy.value?.jornada)
const estadoHoy = computed(() => {
  const h = hoy.value
  if (!h) return { v: '', texto: '' }
  if (h.salida) return { v: 'SalidaRegistrada', texto: 'Salida registrada' }
  if (h.estadoEntrada) return { v: h.estadoEntrada as string, texto: null }
  return { v: h.jornada?.ausencia ? 'Programada' : 'SinRegistro', texto: h.estado as string }
})
const horario = computed(() => (jornada.value?.horaEntrada ? `${hora(jornada.value.horaEntrada)} – ${hora(jornada.value.horaSalida)}` : 'Sin horario asignado'))
const sinJornada = computed(() => {
  const j = jornada.value
  if (!j) return null
  if (j.ausencia) return `Hoy tienes ${j.ausencia.toLowerCase()} registrada.`
  if (j.festivo) return `Hoy es día festivo: ${j.festivo}.`
  if (j.diaDescanso) return 'Hoy es tu día de descanso.'
  return null
})
const iconoModalidad = computed(() => (jornada.value?.modalidad === 'HomeOffice' ? 'casa' : 'edificio'))

// ───── Mes ─────
const inicioMes = (d: Date) => new Date(d.getFullYear(), d.getMonth(), 1)
const mes = ref(inicioMes(new Date()))
const esMesActual = computed(() => mes.value.getFullYear() === new Date().getFullYear() && mes.value.getMonth() === new Date().getMonth())
const resumen = ref<any>(null)
const cargandoMes = ref(false)
async function cargarMes() {
  cargandoMes.value = true
  const fin = new Date(mes.value.getFullYear(), mes.value.getMonth() + 1, 0)
  const hasta = esMesActual.value ? new Date() : fin
  try { resumen.value = await api.get('/asistencia/resumen-colaborador', { desde: iso(mes.value), hasta: iso(hasta) }) } catch (e) { ui.error(e); resumen.value = null } finally { cargandoMes.value = false }
}
function moverMes(n: number) {
  const m = new Date(mes.value.getFullYear(), mes.value.getMonth() + n, 1)
  if (m > new Date()) return
  mes.value = m
}
watch(mes, cargarMes)

function estadoDia(d: any): { v: string; texto: string | null } {
  if (d.estado) return { v: d.estado, texto: null }
  if (d.estadoTexto === 'Falta') return { v: 'Falta', texto: null }
  if (d.estadoTexto === 'Sin registro') return { v: 'SinRegistro', texto: null }
  if (['Vacaciones', 'Permiso', 'Incapacidad'].some(x => d.estadoTexto?.startsWith(x))) return { v: 'Programada', texto: d.estadoTexto }
  return { v: 'gris', texto: d.estadoTexto }
}
const diaCorto = (f: string) => { const d = aFecha(f)!; return { dia: d.getDate(), sem: cap(DIAS[d.getDay()]).slice(0, 3), mes: MESES_CORTOS[d.getMonth()] } }

// ───── Incidencias ─────
const incidencias = ref<{ items: any[]; total: number; paginas: number }>({ items: [], total: 0, paginas: 0 })
const pagInc = ref(1)
const enviando = ref<number | null>(null)
async function cargarIncidencias() {
  if (!colaboradorId.value) return
  try {
    const r = await api.get('/asistencia/incidencias', { colaboradorId: colaboradorId.value, pagina: pagInc.value, tamano: 6 })
    incidencias.value = { items: r.items, total: r.total, paginas: r.paginas }
  } catch (e) { ui.error(e) }
}
function irPagina(p: number) { pagInc.value = p; cargarIncidencias() }
async function justificar(i: any) {
  const r = await ui.confirmar({
    titulo: 'Justificar incidencia', mensaje: `${i.tipoTexto} del ${fechaMedia(i.fecha, true)}. Tu supervisor revisará la justificación.`, aceptar: 'Enviar justificación',
    pedirTexto: { etiqueta: 'Justificación', obligatorio: true, placeholder: 'Ej. Tuve una cita médica; adjunto comprobante con RH' }
  })
  if (!r.ok) return
  enviando.value = i.id
  try {
    await api.post(`/asistencia/incidencias/${i.id}/justificar`, { justificacion: r.texto })
    ui.avisar('Enviamos tu justificación a tu supervisor.')
    cargarIncidencias(); cargarMes()
  } catch (e) { ui.error(e) } finally { enviando.value = null }
}

onMounted(() => { cargarHoy(); cargarMes(); cargarIncidencias() })
</script>

<template>
  <div class="mi-asistencia" :class="{ movil: esMovil }">
    <EncabezadoMovil v-if="esMovil" titulo="Mi asistencia" />
    <div class="contenido-pag">
      <div v-if="!esMovil" class="encabezado">
        <div>
          <h1 class="titulo-pagina">Mi asistencia</h1>
          <p class="subtitulo-pagina">{{ fechaLarga(new Date(), true) }}</p>
        </div>
      </div>

      <p v-if="!colaboradorId" class="caja-info"><Icono n="info" :t="20" />Tu usuario no está vinculado a un colaborador, por lo que no tiene registros de asistencia.</p>

      <template v-else>
        <!-- Hoy -->
        <section class="tarjeta hoy">
          <div v-if="cargandoHoy && !hoy" class="vacio"><span class="spinner" /></div>
          <template v-else-if="hoy">
            <div class="hoy-cab">
              <div>
                <h2 class="titulo-tarjeta">Hoy</h2>
                <p class="subtitulo-tarjeta">{{ fechaLarga(hoy.jornada.fecha) }}</p>
              </div>
              <Estado :v="estadoHoy.v" :texto="estadoHoy.texto" punto />
            </div>
            <div class="hoy-cuerpo">
              <div class="lugar">
                <span class="icono-c"><Icono :n="iconoModalidad" :t="26" /></span>
                <div>
                  <p class="muted pequeno">Hoy trabajas</p>
                  <p class="lugar-txt"><b>{{ jornada.modalidadTexto }}</b> · {{ horario }}</p>
                  <p class="muted pequeno">{{ jornada.area?.nombre ?? jornada.lugar }}<template v-if="jornada.turno"> · {{ jornada.turno }}</template></p>
                  <p v-if="jornada.asignacion" class="asig"><Icono n="asignacion" :t="16" />{{ jornada.asignacion }}</p>
                </div>
              </div>
              <div class="tiempos">
                <div><p class="muted pequeno">Entrada</p><p class="t-valor">{{ hoy.entrada ? hora(hoy.entrada) : '—' }}</p></div>
                <span class="sep" />
                <div><p class="muted pequeno">Salida</p><p class="t-valor">{{ hoy.salida ? hora(hoy.salida) : '—' }}</p></div>
                <span class="sep" />
                <div><p class="muted pequeno">Horas</p><p class="t-valor">{{ hoy.entrada ? num(hoy.horas, 1) : '—' }}</p></div>
              </div>
              <div class="accion">
                <template v-if="hoy.siguiente && jornada.permiteApp">
                  <button class="btn btn-primario btn-g btn-bloque" data-prueba="ir-registrar" @click="router.push('/m/asistencia')">
                    <Icono n="reloj" :t="22" />Registrar {{ hoy.siguiente === 'Entrada' ? 'entrada' : 'salida' }}
                  </button>
                  <p class="muted pequeno centro">{{ hoy.mensaje }}</p>
                </template>
                <p v-else-if="hoy.siguiente" class="caja-info"><Icono n="terminal" :t="20" />Registra tu {{ hoy.siguiente === 'Entrada' ? 'entrada' : 'salida' }} en la terminal o el quiosco de tu sucursal.</p>
                <p v-else class="caja-ok"><Icono n="checkCirculo" :t="20" />{{ hoy.mensaje }}</p>
              </div>
            </div>
            <p v-if="sinJornada" class="caja-aviso nota"><Icono n="calendario" :t="20" />{{ sinJornada }}</p>
          </template>
        </section>

        <div class="rejilla">
          <!-- Mes -->
          <section class="tarjeta">
            <div class="tarjeta-cabecera">
              <h2 class="titulo-tarjeta">Mi mes</h2>
              <div class="sel-mes">
                <button class="btn-icono borde" aria-label="Mes anterior" @click="moverMes(-1)"><Icono n="izquierda" :t="18" /></button>
                <span>{{ mesAnio(mes) }}</span>
                <button class="btn-icono borde" aria-label="Mes siguiente" :disabled="esMesActual" @click="moverMes(1)"><Icono n="derecha" :t="18" /></button>
              </div>
            </div>

            <div class="indicadores">
              <div class="ind"><span class="ind-ic" style="background:#dff5e6;color:#0d8a49"><Icono n="calendario" :t="20" /></span><div><b>{{ resumen?.diasTrabajados ?? '—' }}</b><small>Días trabajados</small></div></div>
              <div class="ind"><span class="ind-ic" style="background:#d6eef0;color:#11937a"><Icono n="checkCirculo" :t="20" /></span><div><b>{{ resumen ? resumen.puntualidad + '%' : '—' }}</b><small>Puntualidad</small></div></div>
              <div class="ind"><span class="ind-ic" style="background:#fde7d6;color:#f26913"><Icono n="reloj" :t="20" /></span><div><b>{{ resumen?.retardos ?? '—' }}</b><small>Retardos</small></div></div>
              <div class="ind"><span class="ind-ic" style="background:var(--rojo-claro);color:var(--rojo)"><Icono n="alertaCirculo" :t="20" /></span><div><b>{{ resumen?.faltas ?? '—' }}</b><small>Faltas</small></div></div>
            </div>

            <div v-if="cargandoMes && !resumen" class="vacio"><span class="spinner" /></div>
            <ul v-else-if="resumen?.dias.length" class="dias">
              <li v-for="d in resumen.dias" :key="d.fecha" class="dia">
                <div class="fecha-c"><small>{{ diaCorto(d.fecha).sem }}</small><b>{{ diaCorto(d.fecha).dia }}</b></div>
                <div class="crecer">
                  <p class="horas-dia">
                    <template v-if="d.entrada">{{ hora(d.entrada) }} – {{ d.salida ? hora(d.salida) : 'sin salida' }}</template>
                    <template v-else>{{ d.estadoTexto }}</template>
                  </p>
                  <p class="muted pequeno">
                    <template v-if="d.horas != null">{{ num(d.horas, 1) }} h</template>
                    <template v-if="d.horas != null && d.modalidad"> · </template>{{ d.modalidad ?? '' }}
                    <template v-for="i in d.incidencias" :key="i.id"> · {{ i.tipoTexto }}</template>
                  </p>
                </div>
                <Estado :v="estadoDia(d).v" :texto="estadoDia(d).texto" punto />
              </li>
            </ul>
            <p v-else class="vacio">No hay registros en {{ mesAnio(mes).toLowerCase() }}.</p>
          </section>

          <!-- Incidencias -->
          <section class="tarjeta">
            <div class="tarjeta-cabecera">
              <div>
                <h2 class="titulo-tarjeta">Mis incidencias</h2>
                <p class="subtitulo-tarjeta">Justifica las que estén por revisar; tu supervisor las revisará.</p>
              </div>
            </div>
            <ul v-if="incidencias.items.length" class="incs">
              <li v-for="i in incidencias.items" :key="i.id" class="inc">
                <div class="inc-cab">
                  <Estado :v="i.tipo" :texto="i.tipoTexto" />
                  <span class="muted pequeno">{{ fechaCorta(i.fecha) }}</span>
                  <span class="crecer" />
                  <Estado :v="i.estado" punto />
                </div>
                <p v-if="i.detalle" class="inc-det">{{ i.detalle }}</p>
                <p v-if="i.justificacion" class="inc-just"><b>Tu justificación:</b> {{ i.justificacion }}</p>
                <p v-if="i.comentarioResolucion" class="inc-just"><b>Comentario:</b> {{ i.comentarioResolucion }}</p>
                <button v-if="i.estado === 'PorRevisar' && i.esMia" class="btn btn-verde-contorno btn-ch" :disabled="enviando === i.id" @click="justificar(i)">
                  <Icono n="lapiz" :t="16" />Justificar
                </button>
              </li>
            </ul>
            <p v-else class="vacio"><Icono n="checkCirculo" :t="22" class="ok" /><br>No tienes incidencias. ¡Sigue así!</p>
            <Paginacion :pagina="pagInc" :paginas="incidencias.paginas" :total="incidencias.total" :tamano="6" @ir="irPagina" />
          </section>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.contenido-pag { display: flex; flex-direction: column; gap: 20px; }
.movil .contenido-pag { padding: 16px; gap: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
.hoy-cab { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; margin-bottom: 16px; }
.hoy-cuerpo { display: grid; grid-template-columns: minmax(0, 1.3fr) minmax(0, 1fr) minmax(220px, .8fr); gap: 24px; align-items: center; }
.lugar { display: flex; gap: 14px; align-items: flex-start; }
.icono-c { width: 52px; height: 52px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-texto); display: grid; place-items: center; flex-shrink: 0; }
.lugar-txt { font-size: 16px; color: var(--titulo); margin: 2px 0; }
.asig { display: inline-flex; align-items: center; gap: 6px; margin-top: 6px; font-size: 13px; color: var(--azul); background: var(--azul-claro); padding: 4px 10px; border-radius: 999px; }
.tiempos { display: flex; gap: 18px; align-items: center; }
.sep { width: 1px; align-self: stretch; background: var(--gris-200); }
.t-valor { font-size: 30px; font-weight: 800; color: var(--titulo); letter-spacing: -.02em; font-variant-numeric: tabular-nums; line-height: 1.15; }
.accion { display: flex; flex-direction: column; gap: 8px; }
.centro { text-align: center; }
.nota { margin-top: 16px; }
.rejilla { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(0, 1fr); gap: 20px; align-items: start; }
.sel-mes { display: flex; align-items: center; gap: 10px; font-weight: 600; color: var(--titulo); white-space: nowrap; }
.sel-mes span { min-width: 120px; text-align: center; }
.sel-mes .btn-icono:disabled { opacity: .4; cursor: not-allowed; }
.indicadores { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; margin-bottom: 16px; }
.ind { display: flex; align-items: center; gap: 10px; padding: 12px; border: 1px solid var(--gris-200); border-radius: 10px; min-width: 0; }
.ind-ic { width: 38px; height: 38px; border-radius: 50%; display: grid; place-items: center; flex-shrink: 0; }
.ind b { display: block; font-size: 20px; font-weight: 800; color: var(--titulo); line-height: 1.1; }
.ind small { display: block; font-size: 12.5px; color: var(--texto-3); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.dias { list-style: none; margin: 0; padding: 0; max-height: 560px; overflow-y: auto; }
.dia { display: flex; align-items: center; gap: 14px; padding: 10px 2px; border-bottom: 1px solid var(--gris-100); }
.dia:last-child { border-bottom: 0; }
.fecha-c { width: 46px; height: 46px; border-radius: 10px; background: var(--gris-50); border: 1px solid var(--gris-200); display: flex; flex-direction: column; align-items: center; justify-content: center; flex-shrink: 0; line-height: 1.1; }
.fecha-c small { font-size: 11px; color: var(--texto-3); text-transform: uppercase; }
.fecha-c b { font-size: 17px; color: var(--titulo); }
.horas-dia { font-weight: 600; color: var(--titulo); font-variant-numeric: tabular-nums; }
.incs { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 12px; }
.inc { border: 1px solid var(--gris-200); border-radius: 10px; padding: 14px; display: flex; flex-direction: column; gap: 8px; align-items: flex-start; }
.inc-cab { display: flex; align-items: center; gap: 10px; width: 100%; flex-wrap: wrap; }
.inc-det { font-size: 14px; color: var(--texto-2); }
.inc-just { font-size: 13.5px; color: var(--texto-2); background: var(--gris-50); border-radius: 8px; padding: 8px 10px; width: 100%; }
.inc-just b { color: var(--titulo); font-weight: 600; }
.ok { color: var(--verde); }

@media (max-width: 1100px) {
  .hoy-cuerpo { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); }
  .accion { grid-column: 1 / -1; }
  .rejilla { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 760px) {
  .hoy-cuerpo { grid-template-columns: minmax(0, 1fr); gap: 16px; }
  .tiempos { justify-content: space-between; background: var(--gris-50); border-radius: 10px; padding: 12px 16px; }
  .t-valor { font-size: 24px; }
  .indicadores { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .tarjeta-cabecera { flex-wrap: wrap; }
}
</style>
