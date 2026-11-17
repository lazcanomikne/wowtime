<script setup lang="ts">
// Solicitar permiso: tipo de permiso, fechas en el calendario único, cobertura y comentario.
// Días hábiles, validaciones y ruta de aprobación salen siempre de POST /solicitudes/simular.
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api, type Pagina, type Persona as TPersona } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useAccion, useCarga, useEsMovil, debounce } from '@/utils/usos'
import { aFecha, dias, fechaNum, iso } from '@/utils/formato'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Calendario from '@/componentes/Calendario.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'

interface TipoPermiso { id: number; nombre: string; conGoce: boolean; maximoDias: number | null; activo: boolean }
interface PasoRuta { orden: number; tipoAprobador: string; etiqueta: string; titular: TPersona | null; delegadoA: TPersona | null; descripcion: string }
interface Simulacion {
  fechaInicio: string; fechaFin: string; periodoTexto: string; diasNaturales: number; diasHabiles: number
  festivos: { fecha: string; nombre: string }[]; diasDescanso: number[]
  validaciones: { codigo: string; mensaje: string; nivel: 'error' | 'aviso' | 'ok' }[]; valida: boolean
  regla: string | null; ruta: PasoRuta[]; rutaTexto: string
}

const s = useSesion()
const router = useRouter()
const esMovil = useEsMovil()
const { ocupado, ejecutar } = useAccion()
const conEncabezadoMovil = computed(() => esMovil.value && !s.esGestion)
const hoy = iso(new Date())
const rutaPersonas = computed(() => (s.tiene('colaboradores.ver') ? '/colaboradores' : '/solicitudes/companeros'))

const tipos = useCarga(() => api.get<Pagina<TipoPermiso>>('/tipos-permiso', { activos: true, tamano: 100 }))
const tipoPermisoId = ref<number | null>(null)
const tipoSel = computed(() => tipos.datos.value?.items.find(t => t.id === tipoPermisoId.value) ?? null)

const mes = ref(new Date(new Date().getFullYear(), new Date().getMonth(), 1))
const inicio = ref<string | null>(null)
const fin = ref<string | null>(null)
const coberturaId = ref<number | null>(null)
const coberturaNota = ref('')
const comentario = ref('')

const sim = ref<Simulacion | null>(null)
const simulando = ref(false)
const errorSim = ref<string | null>(null)
let secuencia = 0
/** Firma de los datos simulados: el envío solo se habilita si coincide con lo que está en pantalla. */
const firmaActual = computed(() => JSON.stringify([tipoPermisoId.value, inicio.value, fin.value, coberturaId.value, coberturaNota.value.trim(), comentario.value.trim()]))
const firmaSimulada = ref('')

function cuerpo() {
  return {
    tipo: 'Permiso', fechaInicio: inicio.value, fechaFin: fin.value ?? inicio.value, tipoPermisoId: tipoPermisoId.value,
    coberturaColaboradorId: coberturaId.value, coberturaNota: coberturaNota.value.trim() || null, comentario: comentario.value.trim() || null
  }
}

async function simular() {
  if (!inicio.value) { sim.value = null; simulando.value = false; return }
  const n = ++secuencia
  const firma = firmaActual.value
  simulando.value = true
  try {
    const r = await api.post<Simulacion>('/solicitudes/simular', cuerpo())
    if (n !== secuencia) return
    sim.value = r; firmaSimulada.value = firma; errorSim.value = null
  } catch (e: any) {
    if (n !== secuencia) return
    sim.value = null; errorSim.value = e?.message ?? 'No se pudo validar la solicitud.'
  } finally { if (n === secuencia) simulando.value = false }
}
const simularLuego = debounce(simular, 350)
watch(firmaActual, () => {
  if (inicio.value) { simulando.value = true; simularLuego() } else { secuencia++; sim.value = null; simulando.value = false }
})

function alElegirRango(a: string, b: string | null) { inicio.value = a; fin.value = b ?? a }
function cambiarInicio(v: string) {
  if (!v) { inicio.value = null; fin.value = null; return }
  inicio.value = v
  if (!fin.value || fin.value < v) fin.value = v
  const d = aFecha(v)!; mes.value = new Date(d.getFullYear(), d.getMonth(), 1)
}
function cambiarFin(v: string) {
  if (!v) { fin.value = inicio.value; return }
  if (!inicio.value) inicio.value = v
  fin.value = v
}

const errores = computed(() => sim.value?.validaciones.filter(v => v.nivel === 'error') ?? [])
const avisos = computed(() => sim.value?.validaciones.filter(v => v.nivel === 'aviso') ?? [])
const puedeEnviar = computed(() => !!sim.value?.valida && !simulando.value && firmaSimulada.value === firmaActual.value && !ocupado.value)

async function enviar() {
  if (!puedeEnviar.value) return
  const r = await ejecutar(() => api.post<{ id: number }>('/solicitudes', cuerpo()), 'Solicitud enviada.')
  if (r?.id) router.push(`/solicitudes/${r.id}`)
}
</script>

<template>
  <div class="solicitar-permiso" :class="{ movil: conEncabezadoMovil }">
    <EncabezadoMovil v-if="conEncabezadoMovil" titulo="Solicitar permiso" atras="/mis-solicitudes" />

    <div class="cuerpo">
      <div v-if="!conEncabezadoMovil" class="encabezado">
        <div>
          <h1 class="titulo-pagina">Solicitar permiso</h1>
          <p class="subtitulo-pagina">Elige el tipo de permiso, las fechas y envía tu solicitud</p>
        </div>
        <RouterLink to="/mis-solicitudes" class="btn btn-contorno"><Icono n="lista" :t="18" />Mis solicitudes</RouterLink>
      </div>

      <div class="rejilla">
        <div class="columna-principal">
          <!-- Tipo de permiso -->
          <section class="tarjeta">
            <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Tipo de permiso</h2></div>
            <div v-if="tipos.cargando.value && !tipos.datos.value" class="tipos">
              <div v-for="i in 3" :key="i" class="esqueleto" style="height: 74px" />
            </div>
            <div v-else-if="!tipos.datos.value?.items.length" class="vacio">No hay tipos de permiso activos. Pide a Recursos Humanos que los configure.</div>
            <div v-else class="tipos" role="radiogroup" aria-label="Tipo de permiso">
              <button v-for="t in tipos.datos.value.items" :key="t.id" type="button" class="tipo" :class="{ sel: tipoPermisoId === t.id }"
                      role="radio" :aria-checked="tipoPermisoId === t.id" :data-prueba="`tipo-permiso-${t.id}`" @click="tipoPermisoId = t.id">
                <span class="radio"><span /></span>
                <span class="crecer">
                  <b>{{ t.nombre }}</b>
                  <span class="fila envolver meta">
                    <span class="etiqueta" :class="t.conGoce ? 'verde' : 'gris'">{{ t.conGoce ? 'Con goce' : 'Sin goce' }}</span>
                    <small v-if="t.maximoDias">Máximo {{ dias(t.maximoDias) }}</small>
                    <small v-else>Sin máximo de días</small>
                  </span>
                </span>
              </button>
            </div>
          </section>

          <!-- Fechas -->
          <section class="tarjeta">
            <div class="tarjeta-cabecera">
              <h2 class="titulo-tarjeta">Selecciona tus fechas</h2>
              <span v-if="sim && sim.valida && !simulando" class="caja-ok chico"><Icono n="checkCirculo" :t="18" />Fechas válidas</span>
            </div>
            <div class="fechas">
              <div class="cal-col">
                <Calendario v-model:mes="mes" :inicio="inicio" :fin="fin" :minimo="hoy" :variante="esMovil ? 'movil' : 'web'"
                            :descanso="sim?.diasDescanso ?? [6, 7]" :festivos="sim?.festivos.map(f => f.fecha) ?? []" @rango="alElegirRango" />
                <div class="leyenda">
                  <span><i class="l-sel" />Días seleccionados</span>
                  <span><i class="l-desc" />Descanso</span>
                  <span><i class="l-fest" />Festivo</span>
                </div>
                <p class="caja-info"><Icono n="info" :t="18" />Los días de descanso y festivos no cuentan como días hábiles.</p>
              </div>
              <div class="campos">
                <div class="campo">
                  <label for="f-inicio">Fecha de inicio</label>
                  <input id="f-inicio" type="date" class="entrada" :min="hoy" :value="inicio ?? ''" @change="cambiarInicio(($event.target as HTMLInputElement).value)" />
                </div>
                <div class="campo">
                  <label for="f-fin">Fecha de término</label>
                  <input id="f-fin" type="date" class="entrada" :min="inicio ?? hoy" :value="fin ?? ''" @change="cambiarFin(($event.target as HTMLInputElement).value)" />
                </div>
                <div class="campo">
                  <label>Días hábiles</label>
                  <div class="entrada solo-lectura">
                    <span v-if="simulando && inicio" class="spinner chico" />
                    <template v-else>{{ sim ? sim.diasHabiles : '—' }}</template>
                    <span class="muted">días hábiles</span>
                  </div>
                </div>
                <div class="campo">
                  <label>Cobertura o entrega de actividades (opcional)</label>
                  <SelectorColaborador v-model="coberturaId" :ruta="rutaPersonas" :excluir="s.me?.colaborador ? [s.me.colaborador.id] : []" placeholder="Buscar a quién te cubre…" />
                  <textarea v-model="coberturaNota" class="entrada" rows="2" maxlength="500" placeholder="Ej. Entregar pendientes a mi responsable…" />
                </div>
                <div class="campo">
                  <label for="comentario">Comentario (opcional)</label>
                  <textarea id="comentario" v-model="comentario" class="entrada" rows="3" maxlength="500" placeholder="Ej. Cita médica a las 10:00…" />
                </div>
              </div>
            </div>
          </section>
        </div>

        <!-- Resumen y ruta -->
        <div class="columna-lateral">
          <section class="tarjeta">
            <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Resumen de solicitud</h2></div>
            <div v-if="!inicio" class="vacio" style="padding: 18px 8px">
              <Icono n="calendario" :t="30" />
              <p style="margin-top: 8px">Elige las fechas en el calendario para ver el resumen.</p>
            </div>
            <template v-else>
              <div class="resumen">
                <span class="resumen-icono"><Icono n="calendario" :t="26" /></span>
                <div class="crecer">
                  <div class="resumen-periodo">{{ sim?.periodoTexto ?? `${fechaNum(inicio)} – ${fechaNum(fin)}` }}</div>
                  <div class="muted">
                    <template v-if="sim">{{ dias(sim.diasHabiles) }} hábiles<template v-if="sim.diasNaturales !== sim.diasHabiles"> · {{ dias(sim.diasNaturales) }} naturales</template></template>
                    <template v-else-if="simulando">Calculando…</template>
                  </div>
                </div>
              </div>
              <div v-if="tipoSel" class="fila-entre resumen-tipo">
                <span>{{ tipoSel.nombre }}</span>
                <span class="etiqueta" :class="tipoSel.conGoce ? 'verde' : 'gris'">{{ tipoSel.conGoce ? 'Con goce' : 'Sin goce' }}</span>
              </div>
              <div v-if="sim?.festivos.length" class="pequeno muted" style="margin-top: 10px">
                Festivos en el periodo: {{ sim.festivos.map(f => f.nombre).join(', ') }}
              </div>

              <div v-if="errorSim" class="validacion error"><Icono n="alertaCirculo" :t="18" />{{ errorSim }}</div>
              <div v-for="v in errores" :key="v.codigo + v.mensaje" class="validacion error" data-prueba="validacion-error"><Icono n="alertaCirculo" :t="18" />{{ v.mensaje }}</div>
              <div v-for="v in avisos" :key="v.codigo + v.mensaje" class="validacion aviso"><Icono n="alerta" :t="18" />{{ v.mensaje }}</div>
            </template>
          </section>

          <section class="tarjeta">
            <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Ruta de aprobación</h2></div>
            <template v-if="sim?.ruta.length">
              <p class="subtitulo-tarjeta" style="margin: -8px 0 14px">Tu solicitud será revisada por:</p>
              <div class="ruta">
                <template v-for="(p, i) in sim.ruta" :key="p.orden">
                  <div class="aprobador">
                    <Avatar v-if="p.titular" :nombre="p.titular.nombre" :iniciales="p.titular.iniciales" :foto="p.titular.fotoUrl" :t="42" />
                    <span v-else class="icono-rh"><Icono n="personas" :t="20" /></span>
                    <div class="aprobador-txt">
                      <b>{{ p.titular?.nombre ?? p.descripcion }}</b>
                      <small>{{ p.titular ? p.etiqueta : 'Paso final' }}</small>
                      <small v-if="p.delegadoA" class="delegado">Delegado a {{ p.delegadoA.nombre }}</small>
                    </div>
                  </div>
                  <Icono v-if="i < sim.ruta.length - 1" n="flecha" :t="18" class="ruta-flecha" />
                </template>
              </div>
            </template>
            <p v-else class="muted pequeno">{{ inicio ? (simulando ? 'Calculando la ruta…' : 'La ruta aparecerá al validar la solicitud.') : 'Elige las fechas para conocer quién revisará tu solicitud.' }}</p>

            <button class="btn btn-primario btn-g btn-bloque enviar" :disabled="!puedeEnviar" data-prueba="enviar-solicitud" @click="enviar">
              <span v-if="ocupado" class="spinner chico blanco" /><Icono v-else n="enviar" :t="20" />Enviar solicitud
            </button>
            <p v-if="!tipoPermisoId" class="ayuda centro">Selecciona el tipo de permiso para continuar.</p>
          </section>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.solicitar-permiso.movil .cuerpo { padding: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; margin-bottom: 20px; flex-wrap: wrap; }
.rejilla { display: grid; grid-template-columns: minmax(0, 2fr) minmax(320px, 1fr); gap: 20px; align-items: start; }
.columna-principal, .columna-lateral { display: flex; flex-direction: column; gap: 20px; min-width: 0; }

.tipos { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 12px; }
.tipo { display: flex; gap: 12px; align-items: flex-start; text-align: left; padding: 14px; border: 1px solid var(--gris-200); border-radius: var(--radio); background: #fff; color: var(--titulo); transition: border-color .15s, background .15s; }
.tipo:hover { border-color: var(--gris-300); }
.tipo.sel { border-color: var(--verde); background: var(--verde-suave); box-shadow: 0 0 0 3px rgba(10, 170, 74, .12); }
.tipo b { display: block; font-size: 15px; font-weight: 650; margin-bottom: 6px; }
.tipo .meta { gap: 8px; }
.tipo small { color: var(--texto-3); font-size: 13px; }
.radio { width: 20px; height: 20px; border-radius: 50%; border: 2px solid var(--gris-300); display: grid; place-items: center; flex-shrink: 0; margin-top: 1px; }
.tipo.sel .radio { border-color: var(--verde); }
.tipo.sel .radio span { width: 10px; height: 10px; border-radius: 50%; background: var(--verde); }

.fechas { display: grid; grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr); gap: 22px; }
.cal-col { display: flex; flex-direction: column; gap: 14px; min-width: 0; }
.campos { display: flex; flex-direction: column; gap: 14px; min-width: 0; }
.campos textarea { margin-top: 6px; }
.solo-lectura { display: flex; align-items: center; justify-content: space-between; background: var(--gris-50); color: var(--titulo); font-weight: 600; }
.solo-lectura .muted { font-weight: 400; }
.leyenda { display: flex; gap: 16px; flex-wrap: wrap; font-size: 13px; color: var(--texto-2); }
.leyenda span { display: inline-flex; align-items: center; gap: 6px; }
.leyenda i { width: 14px; height: 14px; border-radius: 50%; display: inline-block; }
.l-sel { background: var(--verde-osc); }
.l-desc { background: var(--gris-100); border: 1px solid var(--gris-200); }
.l-fest { background: var(--naranja); width: 8px !important; height: 8px !important; }
.caja-ok.chico { font-size: 13.5px; padding: 6px 12px; }

.resumen { display: flex; gap: 14px; align-items: center; }
.resumen-icono { width: 56px; height: 56px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-osc); display: grid; place-items: center; flex-shrink: 0; }
.resumen-periodo { font-size: 18px; font-weight: 750; color: var(--titulo); }
.resumen-tipo { margin-top: 14px; padding-top: 12px; border-top: 1px solid var(--gris-200); font-weight: 600; color: var(--titulo); }
.validacion { display: flex; gap: 8px; align-items: flex-start; margin-top: 10px; padding: 10px 12px; border-radius: var(--radio-s); font-size: 14px; }
.validacion .icono { margin-top: 1px; }
.validacion.error { background: var(--rojo-claro); color: var(--rojo-texto); }
.validacion.aviso { background: var(--ambar-claro); color: #8a5a0b; }

.ruta { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.aprobador { display: flex; align-items: center; gap: 10px; min-width: 0; }
.aprobador-txt { display: flex; flex-direction: column; min-width: 0; }
.aprobador-txt b { font-size: 14.5px; color: var(--titulo); }
.aprobador-txt small { font-size: 13px; color: var(--texto-3); }
.aprobador-txt .delegado { color: var(--azul); }
.icono-rh { width: 42px; height: 42px; border-radius: 50%; background: var(--azul-claro); color: var(--azul); display: grid; place-items: center; flex-shrink: 0; }
.ruta-flecha { color: var(--texto-3); }
.enviar { margin-top: 20px; }
.centro { text-align: center; margin-top: 8px; }
.spinner.chico { width: 18px; height: 18px; border-width: 2.5px; }
.spinner.blanco { border-color: rgba(255, 255, 255, .35); border-top-color: #fff; }

@media (max-width: 1200px) {
  .fechas { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 1000px) {
  .rejilla { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 760px) {
  .tipos { grid-template-columns: minmax(0, 1fr); }
  .tarjeta-cabecera { flex-wrap: wrap; }
}
</style>
