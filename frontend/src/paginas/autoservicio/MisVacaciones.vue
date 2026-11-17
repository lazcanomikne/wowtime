<script setup lang="ts">
// Solicitar vacaciones (Solicitud_de_Vacaciones.png). Días hábiles, saldo posterior y ruta los calcula el servidor (POST /solicitudes/simular).
import { computed, defineAsyncComponent, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Calendario from '@/componentes/Calendario.vue'
import CampoFecha from '@/componentes/CampoFecha.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useEsMovil, debounce } from '@/utils/usos'
import { MESES, aFecha, iso, num, rangoLargo } from '@/utils/formato'

const SolicitarMovil = defineAsyncComponent(() => import('../movil/SolicitarVacacionesMovil.vue'))
const esMovil = useEsMovil()
const s = useSesion()
const ui = useUi()
const router = useRouter()
const saldo = ref<any>(null)
const mias = ref<any[]>([])
const mes = ref(new Date())
const inicio = ref<string | null>(null)
const fin = ref<string | null>(null)
const coberturaId = ref<number | null>(null)
const coberturaNota = ref('')
const comentario = ref('')
const sim = ref<any>(null)
const ruta = ref<any[]>([])
const enviando = ref(false)
const manana = computed(() => { const d = new Date(); d.setDate(d.getDate() + 1); return iso(d) })

async function cargar() {
  saldo.value = await api.get('/vacaciones/saldo')
  mias.value = (await api.get('/solicitudes', { mias: true, tipo: 'Vacaciones', tamano: 2 })).items
  // Ruta de aprobación vigente (se calcula en el servidor con la regla y el organigrama)
  try { ruta.value = (await api.post('/solicitudes/simular', { tipo: 'Vacaciones', fechaInicio: manana.value, fechaFin: manana.value })).ruta } catch { ruta.value = [] }
}
onMounted(cargar)

const simular = debounce(async () => {
  if (!inicio.value) { sim.value = null; return }
  try {
    sim.value = await api.post('/solicitudes/simular', { tipo: 'Vacaciones', fechaInicio: inicio.value, fechaFin: fin.value ?? inicio.value, coberturaColaboradorId: coberturaId.value, coberturaNota: coberturaNota.value, comentario: comentario.value })
    if (sim.value.ruta?.length) ruta.value = sim.value.ruta
  } catch (e) { ui.error(e) }
}, 200)
watch([inicio, fin, coberturaId], simular)

function elegirRango(a: string, b: string | null) { inicio.value = a; fin.value = b }
function cambiarInicio(v: string) { inicio.value = v || null; if (fin.value && v && fin.value < v) fin.value = v; if (v) mes.value = aFecha(v)! }
function cambiarFin(v: string) { fin.value = v || null }

const errores = computed(() => (sim.value?.validaciones ?? []).filter((v: any) => v.nivel === 'error'))
const avisos = computed(() => (sim.value?.validaciones ?? []).filter((v: any) => v.nivel === 'aviso'))
const sinSupervisor = computed(() => s.me?.colaborador?.sinSupervisor)
const venceMes = computed(() => saldo.value?.fechaProximoVencimiento ? MESES[aFecha(saldo.value.fechaProximoVencimiento)!.getMonth()] : null)
const puedeEnviar = computed(() => sim.value?.valida && !enviando.value)

async function enviar() {
  if (!sim.value?.valida) return
  enviando.value = true
  try {
    const r = await api.post('/solicitudes', { tipo: 'Vacaciones', fechaInicio: inicio.value, fechaFin: fin.value ?? inicio.value, coberturaColaboradorId: coberturaId.value, coberturaNota: coberturaNota.value || null, comentario: comentario.value || null })
    ui.avisar(`Solicitud ${r.folio} enviada. Te avisaremos cuando la revisen.`)
    inicio.value = null; fin.value = null; sim.value = null; coberturaId.value = null; coberturaNota.value = ''; comentario.value = ''
    await Promise.all([cargar(), s.cargar()])
  } catch (e) { ui.error(e) } finally { enviando.value = false }
}
const filaMia = (x: any) => ({ Aprobada: ['checkCirculo', 'ok', 'Vacaciones aprobadas'], Pendiente: ['reloj', 'pend', 'Solicitud enviada'], Rechazada: ['x', 'rech', 'Solicitud rechazada'], Cancelada: ['x', 'canc', 'Solicitud cancelada'] } as Record<string, string[]>)[x.estado]
</script>

<template>
  <SolicitarMovil v-if="esMovil" />
  <div v-else class="solicitar">
    <header>
      <h1 class="titulo-pagina">Solicitar vacaciones</h1>
      <p class="subtitulo-pagina">Planifica tu descanso y envía tu solicitud</p>
    </header>

    <div v-if="sinSupervisor" class="caja-alerta sin-sup">
      <span class="ico-alerta"><Icono n="alerta" :t="22" /></span>
      <div><b>No tienes un supervisor asignado</b><p>No puedes enviar solicitudes hasta que Recursos Humanos te asigne uno. Ya les avisamos.</p></div>
    </div>

    <div class="rejilla">
      <div class="col">
        <section class="tarjeta">
          <h2 class="titulo-tarjeta">Tu saldo disponible</h2>
          <div class="saldo">
            <div class="mitad">
              <span class="circ verde"><Icono n="calendario" :t="30" :g="1.9" /></span>
              <div><b class="grande verde-t">{{ num(saldo?.disponibles ?? s.me?.saldo?.disponibles, 1) }}</b><span class="verde-t">días disponibles</span></div>
            </div>
            <span class="div" />
            <div class="mitad">
              <span class="circ ambar"><Icono n="reloArena" :t="28" :g="1.9" /></span>
              <div v-if="venceMes"><b class="grande ambar-t">{{ num(saldo.diasProximoVencimiento, 1) }}</b><span>días vencen en {{ venceMes }}</span></div>
              <div v-else><b class="grande ambar-t">0</b><span>días por vencer</span></div>
            </div>
          </div>
        </section>

        <section class="tarjeta">
          <div class="tarjeta-cabecera">
            <h2 class="titulo-tarjeta">Selecciona tus fechas</h2>
            <span v-if="sim && sim.saldoSuficiente" class="caja-ok badge"><Icono n="checkCirculo" :t="22" />Tienes saldo suficiente</span>
            <span v-else-if="sim && !sim.saldoSuficiente" class="badge mal"><Icono n="alertaCirculo" :t="22" />Saldo insuficiente</span>
          </div>
          <div class="fechas">
            <div>
              <Calendario v-model:mes="mes" :inicio="inicio" :fin="fin" :descanso="sim?.diasDescanso ?? [6, 7]" :festivos="(sim?.festivos ?? []).map((f: any) => f.fecha)" :minimo="iso(new Date())" @rango="elegirRango" />
              <div class="leyenda">
                <span><i class="dot sel" />Días seleccionados</span><span><i class="dot fds" />Fin de semana</span><span><i class="dot otro" />Otro mes</span>
              </div>
              <p class="caja-info nota"><Icono n="info" :t="20" />Los días de descanso y festivos no se descuentan.</p>
            </div>
            <div class="campos">
              <div class="campo">
                <label for="fi">Fecha de inicio</label>
                <CampoFecha id="fi" :model-value="inicio" :min="iso(new Date())" @update:model-value="v => cambiarInicio(v ?? '')" />
              </div>
              <div class="campo">
                <label for="ff">Fecha de término</label>
                <CampoFecha id="ff" :model-value="fin ?? inicio" :min="inicio ?? iso(new Date())" @update:model-value="v => cambiarFin(v ?? '')" />
              </div>
              <div class="campo">
                <label>Días a utilizar</label>
                <div class="entrada dias" aria-readonly="true"><span>{{ sim ? sim.diasHabiles : '' }}</span><span class="muted">días hábiles</span></div>
              </div>
              <div class="campo">
                <label>Cobertura o entrega de actividades (opcional)</label>
                <SelectorColaborador v-model="coberturaId" placeholder="¿Quién cubre tus actividades?" :excluir="s.me?.colaborador ? [s.me.colaborador.id] : []" />
                <textarea v-model="coberturaNota" class="entrada" rows="2" placeholder="Ej. Entregar pendientes a mi responsable..." />
              </div>
              <div class="campo">
                <label for="com">Comentario (opcional)</label>
                <textarea id="com" v-model="comentario" class="entrada" rows="2" placeholder="Ej. Viaje familiar..." />
              </div>
            </div>
          </div>
          <ul v-if="errores.length || avisos.length" class="validaciones">
            <li v-for="v in errores" :key="v.mensaje" class="err"><Icono n="alertaCirculo" :t="18" />{{ v.mensaje }}</li>
            <li v-for="v in avisos" :key="v.mensaje" class="aviso"><Icono n="alerta" :t="18" />{{ v.mensaje }}</li>
          </ul>
        </section>
      </div>

      <div class="col">
        <section class="tarjeta">
          <h2 class="titulo-tarjeta">Resumen de solicitud</h2>
          <div class="res">
            <span class="circ verde g"><Icono n="vacaciones" :t="34" :g="1.9" /></span>
            <div v-if="sim"><b class="periodo">{{ sim.periodoTexto }}</b><span class="habiles">{{ sim.diasHabiles }} {{ sim.diasHabiles === 1 ? 'día hábil' : 'días hábiles' }}</span></div>
            <div v-else><b class="periodo muted">Selecciona tus fechas</b><span class="habiles">en el calendario</span></div>
          </div>
          <div class="separador" />
          <div class="fila-entre despues">
            <span class="fila"><Icono n="calendario" :t="24" :g="1.8" />Saldo después de esta solicitud:</span>
            <b :class="sim && sim.saldoPosterior < 0 ? 'neg' : 'verde-t'">{{ sim ? `${num(sim.saldoPosterior, 1)} ${Math.abs(sim.saldoPosterior) === 1 ? 'día' : 'días'}` : '—' }}</b>
          </div>
        </section>

        <section class="tarjeta">
          <h2 class="titulo-tarjeta">Ruta de aprobación</h2>
          <p class="muted rev">Tu solicitud será revisada por:</p>
          <div class="ruta">
            <template v-for="(p, k) in ruta" :key="p.orden">
              <Icono v-if="k > 0" n="flecha" :t="22" class="flecha" />
              <div class="paso">
                <template v-if="p.titular">
                  <Avatar :nombre="p.titular.nombre" :iniciales="p.titular.iniciales" :foto="p.titular.fotoUrl" :t="58" />
                  <div><b>{{ p.titular.nombre }}</b><small>{{ p.delegadoA ? `Delegado a ${p.delegadoA.nombre}` : p.titular.puesto ?? p.etiqueta }}</small></div>
                </template>
                <template v-else>
                  <span class="circ gris"><Icono n="personas" :t="28" /></span>
                  <div><b class="rh">Recursos Humanos</b></div>
                </template>
              </div>
            </template>
            <p v-if="!ruta.length" class="muted">La ruta se calculará al elegir tus fechas.</p>
          </div>
          <button class="btn btn-primario btn-g btn-bloque enviar" :disabled="!puedeEnviar" data-prueba="enviar-solicitud" @click="enviar">
            <Icono n="enviar" :t="22" />Enviar solicitud
          </button>
        </section>

        <section class="tarjeta">
          <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Mis solicitudes</h2><RouterLink to="/mis-solicitudes" class="ver-todo subr">Ver todas</RouterLink></div>
          <RouterLink v-for="x in mias" :key="x.id" :to="`/solicitudes/${x.id}`" class="mia">
            <span class="mia-ico" :class="filaMia(x)[1]"><Icono :n="filaMia(x)[0]" :t="22" /></span>
            <span class="crecer"><b>{{ filaMia(x)[2] }}</b><span>{{ rangoLargo(x.fechaInicio, x.fechaFin) }}</span></span>
            <Icono n="derecha" :t="22" />
          </RouterLink>
          <p v-if="!mias.length" class="vacio">Aún no tienes solicitudes.</p>
        </section>
      </div>
    </div>
  </div>
</template>

<style scoped>
.solicitar { display: flex; flex-direction: column; gap: 22px; }
.rejilla { display: grid; grid-template-columns: minmax(0, 1.7fr) minmax(360px, 1fr); gap: 20px; align-items: start; }
.col { display: flex; flex-direction: column; gap: 20px; }
.saldo { display: flex; align-items: center; background: var(--verde-suave); border-radius: 12px; padding: 18px 22px; margin-top: 16px; }
.mitad { display: flex; align-items: center; gap: 20px; flex: 1; }
.div { width: 1px; height: 64px; background: #cde7d7; margin: 0 26px; }
.circ { width: 72px; height: 72px; border-radius: 50%; display: grid; place-items: center; flex-shrink: 0; }
.circ.verde { background: #d8f3e4; color: var(--verde-texto); }
.circ.ambar { background: var(--ambar-circulo); color: #a8670c; }
.circ.gris { background: #eef2f6; color: var(--navy); width: 58px; height: 58px; }
.circ.g { width: 70px; height: 70px; }
.grande { display: block; font-size: 44px; font-weight: 800; line-height: 1; letter-spacing: -0.02em; }
.mitad span { font-size: 19px; color: var(--titulo); }
.verde-t { color: var(--verde-texto) !important; }
.ambar-t { color: #c98a14 !important; }
.badge { font-size: 15.5px; padding: 9px 14px; }
.badge.mal { display: flex; gap: 8px; align-items: center; background: var(--rojo-claro); color: var(--rojo-texto); border-radius: 8px; font-weight: 600; }
.fechas { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 0.9fr); gap: 34px; }
.leyenda { display: flex; gap: 18px; margin: 16px 4px 12px; font-size: 12.5px; color: var(--texto-2); flex-wrap: wrap; }
.leyenda span { display: flex; align-items: center; gap: 7px; white-space: nowrap; }
.dot { width: 18px; height: 18px; border-radius: 50%; display: inline-block; }
.dot.sel { background: var(--verde-osc); }
.dot.fds { background: var(--gris-100); }
.dot.otro { background: #f1f3f6; }
.nota { font-size: 13.5px; }
.campos { display: flex; flex-direction: column; gap: 16px; }
.fecha-i .entrada { font-size: 16.5px; }
.dias { display: flex; justify-content: space-between; align-items: center; background: var(--gris-100); color: var(--titulo); font-size: 16px; }
.validaciones { list-style: none; padding: 0; margin: 18px 0 0; display: flex; flex-direction: column; gap: 8px; }
.validaciones li { display: flex; gap: 8px; align-items: center; font-size: 14px; padding: 9px 12px; border-radius: 8px; }
.validaciones .err { background: var(--rojo-claro); color: var(--rojo-texto); }
.validaciones .aviso { background: var(--ambar-claro); color: #8a5a0b; }
.res { display: flex; align-items: center; gap: 22px; margin-top: 18px; }
.periodo { display: block; font-size: 20.5px; color: var(--titulo); font-weight: 700; }
.habiles { font-size: 17px; color: var(--texto-2); }
.despues { font-size: 16px; color: var(--titulo); }
.despues b { font-size: 26px; font-weight: 800; }
.neg { color: var(--rojo-texto); }
.rev { margin: 8px 0 16px; font-size: 15.5px; }
.ruta { display: flex; align-items: center; gap: 14px; flex-wrap: wrap; margin-bottom: 22px; }
.paso { display: flex; align-items: center; gap: 12px; }
.paso b { display: block; font-size: 15px; font-weight: 650; color: var(--titulo); }
.paso small { display: block; font-size: 13px; color: var(--texto-3); }
.rh { font-weight: 500 !important; }
.flecha { color: var(--texto-2); }
.enviar { font-size: 19px; }
.subr { text-decoration: underline; text-underline-offset: 3px; }
.mia { display: flex; align-items: center; gap: 16px; border: 1px solid var(--gris-200); border-radius: 10px; padding: 16px; margin-top: 12px; color: var(--titulo); text-decoration: none !important; }
.mia:hover { border-color: var(--gris-300); }
.mia b { display: block; font-size: 16px; font-weight: 650; }
.mia .crecer span { font-size: 15px; color: var(--texto-2); }
.mia-ico { width: 46px; height: 46px; border-radius: 50%; display: grid; place-items: center; flex-shrink: 0; }
.mia-ico.ok { background: #d8f3e4; color: var(--verde-texto); }
.mia-ico.pend { background: #eef2f6; color: var(--navy); }
.mia-ico.rech, .mia-ico.canc { background: var(--rojo-claro); color: var(--rojo-texto); }
.sin-sup { align-items: flex-start; }
.ico-alerta { width: 40px; height: 40px; border-radius: 50%; background: #fbd5d8; color: var(--rojo-texto); display: grid; place-items: center; flex-shrink: 0; }
.sin-sup b { color: var(--rojo-texto); }
.sin-sup p { color: #7a3a3c; font-size: 14px; }
@media (max-width: 1250px) { .rejilla { grid-template-columns: 1fr; } }
@media (max-width: 900px) { .fechas { grid-template-columns: 1fr; } .saldo { flex-direction: column; align-items: flex-start; gap: 16px; } .div { display: none; } }
</style>
