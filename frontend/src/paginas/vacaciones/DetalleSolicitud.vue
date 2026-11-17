<script setup lang="ts">
// Detalle de una solicitud: datos, ruta de aprobación con decisiones (y en nombre de quién), bitácora y acciones.
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { api, type Persona as TPersona } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion, useCarga, useEsMovil, useEventos } from '@/utils/usos'
import { dias, fechaLarga, momento, num } from '@/utils/formato'
import Icono from '@/componentes/Icono.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'

interface Paso {
  id: number; orden: number; tipoAprobador: string; etiqueta: string; titular: TPersona | null; delegadoA: string | null
  estado: string; decididoPorNombre: string | null; enNombreDeNombre: string | null; motivo: string | null; fecha: string | null; notaBitacora: string | null
}
interface Detalle {
  id: number; folio: string; tipo: string; tipoTexto: string; colaborador: TPersona; fechaInicio: string; fechaFin: string; periodo: string
  diasHabiles: number; estado: string; pasoActual: number; reglaNombre: string | null; cobertura: TPersona | null; coberturaNota: string | null
  comentario: string | null; motivoRechazo: string | null; creadoEn: string; resueltaEn: string | null; saldoDisponible: number | null
  pasos: Paso[]; bitacora: { fecha: string; titulo: string; detalle: string | null; autorNombre: string | null }[]
  puedeDecidir: boolean; motivoNoDecidir: string | null; puedeCancelar: boolean; esMia: boolean
}

const route = useRoute()
const s = useSesion()
const ui = useUi()
const esMovil = useEsMovil()
const { ocupado, ejecutar } = useAccion()
const id = computed(() => Number(route.params.id))
const conEncabezadoMovil = computed(() => esMovil.value && !s.esGestion)

const { datos: d, cargando, error, cargar } = useCarga(() => api.get<Detalle>(`/solicitudes/${id.value}`))
useEventos((tipo, x) => { if (tipo === 'solicitud' && (!x || x.id === id.value)) cargar(true) })

const regreso = computed(() => (d.value?.esMia ? '/mis-solicitudes' : s.tiene('aprobaciones.decidir') ? '/aprobaciones' : '/'))
const hayAcciones = computed(() => !!d.value && (d.value.puedeDecidir || d.value.puedeCancelar))

function iconoPaso(e: string) {
  return e === 'Aprobado' ? 'check' : e === 'Rechazado' ? 'x' : e === 'Pendiente' ? 'reloj' : e === 'Omitido' ? 'flecha' : ''
}

async function aprobar() {
  const x = d.value!
  const r = await ui.confirmar({
    titulo: 'Aprobar solicitud',
    mensaje: `${x.tipoTexto} de ${x.colaborador.nombre}: ${x.periodo} (${dias(x.diasHabiles)} hábiles).`,
    aceptar: 'Aprobar',
    pedirTexto: { etiqueta: 'Comentario (opcional)', placeholder: 'Ej. Aprobado, coordina la entrega de pendientes.' }
  })
  if (!r.ok) return
  const res = await ejecutar(() => api.post<Detalle>(`/solicitudes/${x.id}/aprobar`, { comentario: r.texto || null }), 'Solicitud aprobada.')
  if (res) d.value = res
}

async function rechazar() {
  const x = d.value!
  const r = await ui.confirmar({
    titulo: 'Rechazar solicitud',
    mensaje: `${x.tipoTexto} de ${x.colaborador.nombre}: ${x.periodo}.`,
    aceptar: 'Rechazar', peligro: true,
    pedirTexto: { etiqueta: 'Motivo del rechazo', obligatorio: true, placeholder: 'Explica al colaborador por qué se rechaza.' }
  })
  if (!r.ok || !r.texto) return
  const res = await ejecutar(() => api.post<Detalle>(`/solicitudes/${x.id}/rechazar`, { motivo: r.texto }), 'Solicitud rechazada.')
  if (res) d.value = res
}

async function cancelar() {
  const x = d.value!
  const r = await ui.confirmar({
    titulo: 'Cancelar solicitud',
    mensaje: `Se cancelará la solicitud ${x.folio} (${x.periodo}) y se liberarán los días comprometidos.`,
    aceptar: 'Cancelar solicitud', cancelar: 'Regresar', peligro: true
  })
  if (!r.ok) return
  const res = await ejecutar(() => api.post<Detalle>(`/solicitudes/${x.id}/cancelar`, {}), 'Solicitud cancelada.')
  if (res) d.value = res
}
</script>

<template>
  <div class="detalle-solicitud" :class="{ movil: conEncabezadoMovil }">
    <EncabezadoMovil v-if="conEncabezadoMovil" titulo="Detalle de solicitud" :atras="regreso" />

    <div class="cuerpo">
      <div v-if="cargando && !d" class="columna">
        <div class="esqueleto" style="height: 64px" />
        <div class="esqueleto" style="height: 260px" />
      </div>
      <div v-else-if="error && !d" class="tarjeta vacio">
        <Icono n="alertaCirculo" :t="30" />
        <p style="margin-top: 8px">{{ error }}</p>
        <RouterLink :to="regreso" class="btn btn-contorno btn-ch" style="margin-top: 14px">Regresar</RouterLink>
      </div>

      <template v-else-if="d">
        <!-- Encabezado -->
        <div class="encabezado">
          <div class="crecer">
            <RouterLink v-if="!conEncabezadoMovil" :to="regreso" class="ver-todo regresar"><Icono n="izquierda" :t="18" />Regresar</RouterLink>
            <div class="fila envolver titulo-fila">
              <h1 class="titulo-pagina">{{ d.tipoTexto }}</h1>
              <Estado :v="d.estado" />
            </div>
            <p class="subtitulo-pagina">Folio {{ d.folio }} · Enviada {{ momento(d.creadoEn).toLowerCase() }}<template v-if="d.resueltaEn"> · Resuelta {{ momento(d.resueltaEn).toLowerCase() }}</template></p>
          </div>
        </div>

        <div class="rejilla">
          <div class="columna-principal">
            <!-- Datos -->
            <section class="tarjeta">
              <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Datos de la solicitud</h2></div>
              <Persona :p="d.colaborador" :sub="[d.colaborador.puesto, d.colaborador.area].filter(Boolean).join(' · ')" :t="48" />

              <div class="datos">
                <div class="dato">
                  <span class="dato-icono verde"><Icono :n="d.tipo === 'Vacaciones' ? 'vacaciones' : 'calendario'" :t="20" /></span>
                  <div>
                    <div class="dato-etiqueta">Periodo</div>
                    <div class="dato-valor">{{ d.periodo }}</div>
                    <div class="pequeno muted">{{ fechaLarga(d.fechaInicio) }}<template v-if="d.fechaFin !== d.fechaInicio"> al {{ fechaLarga(d.fechaFin).toLowerCase() }}</template></div>
                  </div>
                </div>
                <div class="dato">
                  <span class="dato-icono azul"><Icono n="reloArena" :t="20" /></span>
                  <div>
                    <div class="dato-etiqueta">Días hábiles</div>
                    <div class="dato-valor">{{ dias(d.diasHabiles) }}</div>
                  </div>
                </div>
                <div v-if="d.saldoDisponible !== null" class="dato">
                  <span class="dato-icono ambar"><Icono n="palmera" :t="20" /></span>
                  <div>
                    <div class="dato-etiqueta">Saldo disponible</div>
                    <div class="dato-valor">{{ dias(d.saldoDisponible) }}</div>
                    <div v-if="d.estado === 'Pendiente'" class="pequeno muted">Saldo vigente del colaborador</div>
                  </div>
                </div>
                <div v-if="d.reglaNombre" class="dato">
                  <span class="dato-icono gris"><Icono n="organigrama" :t="20" /></span>
                  <div>
                    <div class="dato-etiqueta">Regla de aprobación</div>
                    <div class="dato-valor">{{ d.reglaNombre }}</div>
                  </div>
                </div>
              </div>

              <div class="separador" />
              <div class="bloques">
                <div>
                  <div class="dato-etiqueta">Cobertura o entrega de actividades</div>
                  <Persona v-if="d.cobertura" :p="d.cobertura" :sub="d.cobertura.area ?? d.cobertura.puesto" :t="36" style="margin-top: 8px" />
                  <p v-if="d.coberturaNota" class="texto-libre">{{ d.coberturaNota }}</p>
                  <p v-if="!d.cobertura && !d.coberturaNota" class="muted pequeno" style="margin-top: 4px">Sin cobertura indicada.</p>
                </div>
                <div>
                  <div class="dato-etiqueta">Comentario</div>
                  <p v-if="d.comentario" class="texto-libre">{{ d.comentario }}</p>
                  <p v-else class="muted pequeno" style="margin-top: 4px">Sin comentario.</p>
                </div>
              </div>

              <div v-if="d.motivoRechazo" class="caja-alerta rechazo">
                <Icono n="alertaCirculo" :t="22" style="color: var(--rojo)" />
                <div>
                  <div class="negrita" style="color: var(--rojo-texto)">Motivo del rechazo</div>
                  <div>{{ d.motivoRechazo }}</div>
                </div>
              </div>

              <template v-if="hayAcciones || (d.estado === 'Pendiente' && d.motivoNoDecidir)">
                <div class="separador" />
                <p v-if="!d.puedeDecidir && d.motivoNoDecidir && d.estado === 'Pendiente'" class="caja-info" style="margin-bottom: 12px">
                  <Icono n="info" :t="20" />{{ d.motivoNoDecidir }}
                </p>
                <div v-if="hayAcciones" class="acciones">
                  <button v-if="d.puedeCancelar" class="btn btn-contorno" :disabled="ocupado" data-prueba="cancelar-solicitud" @click="cancelar">
                    <Icono n="x" :t="18" />Cancelar solicitud
                  </button>
                  <span class="crecer" />
                  <template v-if="d.puedeDecidir">
                    <button class="btn btn-rechazar" :disabled="ocupado" data-prueba="rechazar" @click="rechazar">Rechazar</button>
                    <button class="btn btn-primario" :disabled="ocupado" data-prueba="aprobar" @click="aprobar"><Icono n="check" :t="18" />Aprobar</button>
                  </template>
                </div>
              </template>
            </section>

            <!-- Ruta de aprobación -->
            <section class="tarjeta">
              <div class="tarjeta-cabecera">
                <div>
                  <h2 class="titulo-tarjeta">Ruta de aprobación</h2>
                  <p class="subtitulo-tarjeta">{{ d.pasos.length }} {{ d.pasos.length === 1 ? 'paso' : 'pasos' }}<template v-if="d.reglaNombre"> · regla «{{ d.reglaNombre }}»</template></p>
                </div>
              </div>
              <div v-if="!d.pasos.length" class="vacio">Esta solicitud no tiene pasos de aprobación.</div>
              <ol v-else class="pasos">
                <li v-for="(p, i) in d.pasos" :key="p.id" class="paso" :class="[p.estado.toLowerCase(), { actual: d.estado === 'Pendiente' && p.orden === d.pasoActual }]">
                  <div class="paso-cab">
                    <span class="circulo" :class="p.estado.toLowerCase()">
                      <Icono v-if="iconoPaso(p.estado)" :n="iconoPaso(p.estado)" :t="16" :g="2.6" />
                      <template v-else>{{ p.orden }}</template>
                    </span>
                    <div class="crecer">
                      <div class="pequeno muted">Paso {{ p.orden }}</div>
                      <div class="negrita">{{ p.etiqueta }}</div>
                    </div>
                    <Estado :v="p.estado" />
                  </div>
                  <div class="paso-cuerpo">
                    <Persona v-if="p.titular" :p="p.titular" :t="34" />
                    <div v-else class="persona">
                      <span class="icono-rh"><Icono n="personas" :t="18" /></span>
                      <div class="textos"><div class="nombre">Recursos Humanos</div><div class="sub">Cualquier persona de RH de la sucursal</div></div>
                    </div>
                    <span v-if="p.delegadoA" class="etiqueta azul delegado"><Icono n="flecha" :t="14" />Delegado a {{ p.delegadoA }}</span>
                    <div v-if="p.decididoPorNombre" class="decision">
                      <span>{{ p.estado === 'Rechazado' ? 'Rechazó' : 'Decidió' }}: <b>{{ p.decididoPorNombre }}</b></span>
                      <span v-if="p.enNombreDeNombre" class="muted">en nombre de {{ p.enNombreDeNombre }}</span>
                      <span v-if="p.fecha" class="muted">{{ momento(p.fecha) }}</span>
                    </div>
                    <p v-if="p.motivo" class="motivo" :class="{ rojo: p.estado === 'Rechazado' }">“{{ p.motivo }}”</p>
                    <p v-if="p.notaBitacora" class="caja-aviso nota"><Icono n="escudo" :t="18" />{{ p.notaBitacora }}</p>
                  </div>
                  <span v-if="i < d.pasos.length - 1" class="conector" aria-hidden="true"><Icono n="flecha" :t="18" /></span>
                </li>
              </ol>
            </section>
          </div>

          <!-- Bitácora -->
          <section class="tarjeta bitacora">
            <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Bitácora</h2></div>
            <div v-if="!d.bitacora.length" class="vacio">Sin movimientos en la bitácora.</div>
            <ol v-else class="linea">
              <li v-for="(b, i) in [...d.bitacora].reverse()" :key="i" class="evento">
                <span class="punto-linea" />
                <div class="crecer">
                  <div class="negrita evento-titulo">{{ b.titulo }}</div>
                  <div v-if="b.detalle" class="evento-detalle">{{ b.detalle }}</div>
                  <div class="pequeno muted">{{ momento(b.fecha) }}<template v-if="b.autorNombre"> · {{ b.autorNombre }}</template></div>
                </div>
              </li>
            </ol>
            <div class="separador" />
            <div class="fila-entre pequeno"><span class="muted">Días hábiles</span><b>{{ num(d.diasHabiles, 1) }}</b></div>
            <div class="fila-entre pequeno" style="margin-top: 6px"><span class="muted">Creada</span><span>{{ momento(d.creadoEn) }}</span></div>
            <div v-if="d.resueltaEn" class="fila-entre pequeno" style="margin-top: 6px"><span class="muted">Resuelta</span><span>{{ momento(d.resueltaEn) }}</span></div>
          </section>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.detalle-solicitud.movil .cuerpo { padding: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; margin-bottom: 20px; }
.regresar { margin-bottom: 8px; }
.titulo-fila { gap: 14px; }
.rejilla { display: grid; grid-template-columns: minmax(0, 1.75fr) minmax(0, 1fr); gap: 20px; align-items: start; }
.columna-principal { display: flex; flex-direction: column; gap: 20px; min-width: 0; }
.datos { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px 20px; margin-top: 20px; }
.dato { display: flex; gap: 12px; align-items: flex-start; }
.dato-icono { width: 40px; height: 40px; border-radius: 50%; display: grid; place-items: center; flex-shrink: 0; }
.dato-icono.verde { background: var(--verde-claro); color: var(--verde-texto); }
.dato-icono.azul { background: var(--azul-claro); color: var(--azul); }
.dato-icono.ambar { background: var(--ambar-circulo); color: var(--ambar); }
.dato-icono.gris { background: var(--gris-100); color: var(--texto-2); }
.dato-etiqueta { font-size: 13px; color: var(--texto-3); }
.dato-valor { font-size: 16px; font-weight: 700; color: var(--titulo); }
.bloques { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; }
.texto-libre { margin-top: 6px; color: var(--texto); white-space: pre-line; overflow-wrap: anywhere; }
.rechazo { margin-top: 16px; align-items: flex-start; }
.acciones { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }

.pasos { list-style: none; margin: 0; padding: 0; display: grid; grid-auto-flow: column; grid-auto-columns: minmax(0, 1fr); gap: 28px; }
.paso { position: relative; border: 1px solid var(--gris-200); border-radius: var(--radio); padding: 14px; background: #fff; display: flex; flex-direction: column; gap: 12px; min-width: 0; }
.paso.actual { border-color: var(--verde-borde); box-shadow: 0 0 0 3px var(--verde-suave); }
.paso.enespera, .paso.omitido { background: var(--gris-50); }
.paso-cab { display: flex; align-items: center; gap: 10px; }
.circulo { width: 32px; height: 32px; border-radius: 50%; display: grid; place-items: center; font-weight: 700; font-size: 14px; flex-shrink: 0; background: var(--gris-100); color: var(--texto-2); }
.circulo.aprobado { background: var(--verde); color: #fff; }
.circulo.rechazado { background: var(--rojo); color: #fff; }
.circulo.pendiente { background: var(--ambar-circulo); color: var(--ambar); }
.paso-cuerpo { display: flex; flex-direction: column; gap: 8px; min-width: 0; }
.icono-rh { width: 34px; height: 34px; border-radius: 50%; background: var(--azul-claro); color: var(--azul); display: grid; place-items: center; flex-shrink: 0; }
.delegado { align-self: flex-start; height: auto; min-height: 26px; padding: 4px 10px; white-space: normal; }
.decision { display: flex; flex-direction: column; gap: 2px; font-size: 13.5px; }
.motivo { font-size: 14px; color: var(--texto-2); font-style: italic; overflow-wrap: anywhere; }
.motivo.rojo { color: var(--rojo-texto); }
.nota { font-size: 13px; align-items: flex-start; padding: 10px 12px; }
.conector { position: absolute; right: -24px; top: 22px; color: var(--gris-400); }

.linea { list-style: none; margin: 0; padding: 0; position: relative; }
.evento { display: flex; gap: 14px; position: relative; padding-bottom: 18px; }
.evento:last-child { padding-bottom: 0; }
.evento::before { content: ''; position: absolute; left: 6px; top: 16px; bottom: 0; width: 2px; background: var(--gris-200); }
.evento:last-child::before { display: none; }
.punto-linea { width: 14px; height: 14px; border-radius: 50%; background: #fff; border: 3px solid var(--verde); flex-shrink: 0; margin-top: 3px; position: relative; z-index: 1; }
.evento-titulo { font-size: 14.5px; color: var(--titulo); }
.evento-detalle { font-size: 14px; color: var(--texto-2); overflow-wrap: anywhere; }

@media (max-width: 1100px) {
  .rejilla { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 760px) {
  .pasos { grid-auto-flow: row; grid-auto-columns: auto; gap: 26px; }
  .conector { right: auto; left: 50%; top: auto; bottom: -24px; transform: translateX(-50%) rotate(90deg); }
  .bloques { grid-template-columns: minmax(0, 1fr); }
  .acciones .btn { flex: 1; }
  .acciones .crecer { display: none; }
  .detalle-solicitud.movil .titulo-pagina { font-size: 24px; }
  .detalle-solicitud.movil .subtitulo-pagina { font-size: 14px; }
}
</style>
