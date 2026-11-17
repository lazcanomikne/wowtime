<script setup lang="ts">
// Bandeja de aprobaciones (supervisores, gerentes y RH) y delegaciones propias.
import { computed, nextTick, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { api, type Pagina, type Persona as TPersona } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion, useCarga, useEsMovil, useEventos } from '@/utils/usos'
import { dias, iso, momento, num, rango } from '@/utils/formato'
import Icono from '@/componentes/Icono.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'

interface Fila {
  id: number; folio: string; tipo: string; tipoTexto: string; colaborador: TPersona; fechaInicio: string; fechaFin: string; periodo: string
  diasHabiles: number; estado: string; pasoActual: number; pasoActualTexto: string | null; cobertura: TPersona | null; coberturaNota: string | null
  comentario: string | null; creadoEn: string; saldoDisponible: number | null; puedeDecidir: boolean
}
interface Delegacion {
  id: number; de: TPersona; hacia: TPersona; fechaInicio: string; fechaFin: string; periodo: string; tipos: string[]; tiposTexto: string; estado: string
}

const s = useSesion()
const ui = useUi()
const route = useRoute()
const esMovil = useEsMovil()
const { ocupado, ejecutar } = useAccion()
const conEncabezadoMovil = computed(() => esMovil.value && !s.esGestion)
const yo = computed(() => s.me?.colaborador?.id ?? null)

const TIPOS = [
  { v: '', t: 'Todas' }, { v: 'Vacaciones', t: 'Vacaciones' }, { v: 'Permiso', t: 'Permisos' }, { v: 'CanjeDias', t: 'Canjes de días' }, { v: 'IncidenciaAsistencia', t: 'Incidencias' }
]
const TIPOS_DELEGACION = [
  { v: 'Vacaciones', t: 'Vacaciones' }, { v: 'Permiso', t: 'Permisos' }, { v: 'CanjeDias', t: 'Canjes de días' }, { v: 'IncidenciaAsistencia', t: 'Incidencias de asistencia' }
]
const tipo = ref('')
const resaltada = computed(() => Number(route.query.solicitud) || null)

const bandeja = useCarga(() => api.get<{ total: number; items: Fila[] }>('/aprobaciones/bandeja', { tipo: tipo.value, sucursalId: s.esGestion ? s.sucursalId : undefined }), { porSucursal: s.esGestion })
watch(tipo, () => bandeja.cargar())
watch(() => bandeja.datos.value, async () => {
  if (!resaltada.value) return
  await nextTick()
  document.querySelector(`[data-solicitud="${resaltada.value}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
})

const puedeDelegar = computed(() => !!yo.value && s.tiene('delegaciones.propias', 'aprobaciones.configurar'))
const delegaciones = useCarga(() => api.get<Pagina<Delegacion>>('/aprobaciones/delegaciones', { tamano: 100 }))
const mias = computed(() => (delegaciones.datos.value?.items ?? []).filter(d => d.de.id === yo.value || d.hacia.id === yo.value))

useEventos(t => { if (t === 'solicitud') bandeja.cargar(true) })

async function aprobar(f: Fila) {
  const r = await ui.confirmar({
    titulo: 'Aprobar solicitud',
    mensaje: `${f.tipoTexto} de ${f.colaborador.nombre}: ${f.periodo} (${dias(f.diasHabiles)} hábiles).`,
    aceptar: 'Aprobar',
    pedirTexto: { etiqueta: 'Comentario (opcional)', placeholder: 'Ej. Aprobado, coordina la entrega de pendientes.' }
  })
  if (!r.ok) return
  const res = await ejecutar(() => api.post(`/solicitudes/${f.id}/aprobar`, { comentario: r.texto || null }), `Solicitud de ${f.colaborador.nombre} aprobada.`)
  if (res) bandeja.cargar(true)
}
async function rechazar(f: Fila) {
  const r = await ui.confirmar({
    titulo: 'Rechazar solicitud',
    mensaje: `${f.tipoTexto} de ${f.colaborador.nombre}: ${f.periodo}.`,
    aceptar: 'Rechazar', peligro: true,
    pedirTexto: { etiqueta: 'Motivo del rechazo', obligatorio: true, placeholder: 'Explica al colaborador por qué se rechaza.' }
  })
  if (!r.ok || !r.texto) return
  const res = await ejecutar(() => api.post(`/solicitudes/${f.id}/rechazar`, { motivo: r.texto }), `Solicitud de ${f.colaborador.nombre} rechazada.`)
  if (res) bandeja.cargar(true)
}

// ───── Delegaciones ─────
const modal = ref(false)
const intento = ref(false)
const nueva = reactive({ haciaColaboradorId: null as number | null, fechaInicio: iso(new Date()), fechaFin: '', tipos: ['Vacaciones', 'Permiso', 'CanjeDias', 'IncidenciaAsistencia'] as string[] })
const rutaDelegados = computed(() => (s.tiene('colaboradores.ver') ? '/colaboradores' : '/aprobaciones/delegados'))
const faltantes = computed(() => !nueva.haciaColaboradorId || !nueva.fechaInicio || !nueva.fechaFin || !nueva.tipos.length || nueva.fechaFin < nueva.fechaInicio)

function abrirModal() {
  Object.assign(nueva, { haciaColaboradorId: null, fechaInicio: iso(new Date()), fechaFin: '', tipos: ['Vacaciones', 'Permiso', 'CanjeDias', 'IncidenciaAsistencia'] })
  intento.value = false
  modal.value = true
}
async function guardarDelegacion() {
  intento.value = true
  if (faltantes.value || !yo.value) return
  const r = await ejecutar(() => api.post('/aprobaciones/delegaciones', {
    deColaboradorId: yo.value, haciaColaboradorId: nueva.haciaColaboradorId, fechaInicio: nueva.fechaInicio, fechaFin: nueva.fechaFin, tipos: nueva.tipos
  }), 'Delegación creada.')
  if (r) { modal.value = false; delegaciones.cargar(true) }
}
async function terminar(d: Delegacion) {
  const r = await ui.confirmar({
    titulo: 'Terminar delegación',
    mensaje: `Las aprobaciones pendientes regresarán a ti y ${d.hacia.nombre} dejará de recibirlas.`,
    aceptar: 'Terminar', peligro: true
  })
  if (!r.ok) return
  await ejecutar(() => api.del(`/aprobaciones/delegaciones/${d.id}`), 'Delegación terminada.')
  delegaciones.cargar(true)
}
</script>

<template>
  <div class="bandeja" :class="{ movil: conEncabezadoMovil }">
    <EncabezadoMovil v-if="conEncabezadoMovil" titulo="Aprobaciones" atras="/mis-solicitudes" />

    <div class="cuerpo">
      <div v-if="!conEncabezadoMovil" class="encabezado">
        <div>
          <h1 class="titulo-pagina">Aprobaciones</h1>
          <p class="subtitulo-pagina">Solicitudes que esperan tu decisión</p>
        </div>
        <button v-if="puedeDelegar" class="btn btn-contorno" data-prueba="delegar" @click="abrirModal"><Icono n="personaMas" :t="18" />Delegar mis aprobaciones</button>
      </div>

      <!-- Por aprobar -->
      <section class="tarjeta" :class="{ compacta: esMovil }">
        <div class="tarjeta-cabecera">
          <div class="izq">
            <h2 class="titulo-tarjeta">Por aprobar</h2>
            <span v-if="bandeja.datos.value" class="etiqueta" :class="bandeja.datos.value.total ? 'ambar' : 'verde'">{{ bandeja.datos.value.total }}</span>
          </div>
          <button class="btn-icono borde" aria-label="Actualizar" :disabled="bandeja.cargando.value" @click="bandeja.cargar()"><Icono n="actualizar" :t="18" /></button>
        </div>
        <div class="filtros" role="tablist" aria-label="Filtrar por tipo">
          <button v-for="t in TIPOS" :key="t.v" type="button" class="filtro" :class="{ activo: tipo === t.v }" role="tab" :aria-selected="tipo === t.v" @click="tipo = t.v">{{ t.t }}</button>
        </div>

        <div v-if="bandeja.cargando.value && !bandeja.datos.value" class="columna">
          <div v-for="i in 3" :key="i" class="esqueleto" style="height: 64px" />
        </div>
        <div v-else-if="!bandeja.datos.value?.items.length" class="vacio">
          <Icono n="checkCirculo" :t="36" style="color: var(--verde)" />
          <p style="margin-top: 8px">{{ tipo ? 'No hay solicitudes de este tipo por aprobar.' : 'No tienes solicitudes por aprobar. ¡Todo al día!' }}</p>
        </div>

        <!-- Tarjetas en teléfono -->
        <div v-else-if="esMovil" class="tarjetas">
          <article v-for="f in bandeja.datos.value.items" :key="f.id" class="solicitud" :class="{ resaltada: f.id === resaltada }" :data-solicitud="f.id">
            <div class="fila-entre" style="align-items: flex-start">
              <Persona :p="f.colaborador" :sub="f.colaborador.area ?? f.colaborador.puesto" />
              <span class="etiqueta gris cuadrada">{{ f.tipoTexto }}</span>
            </div>
            <div class="datos-mini">
              <div><small>Periodo</small><b>{{ rango(f.fechaInicio, f.fechaFin, true) }}</b></div>
              <div><small>Días</small><b>{{ num(f.diasHabiles, 1) }}</b></div>
              <div v-if="f.saldoDisponible !== null"><small>Saldo disp.</small><b>{{ num(f.saldoDisponible, 1) }}</b></div>
            </div>
            <div v-if="f.cobertura || f.coberturaNota" class="cobertura">
              <small class="muted">Cobertura</small>
              <Persona v-if="f.cobertura" :p="f.cobertura" :sub="f.cobertura.area ?? f.cobertura.puesto" :t="30" />
              <p v-if="f.coberturaNota" class="pequeno">{{ f.coberturaNota }}</p>
            </div>
            <p v-if="f.comentario" class="comentario">“{{ f.comentario }}”</p>
            <div class="fila-entre pequeno">
              <span v-if="f.pasoActualTexto" class="etiqueta ambar"><span class="punto" />Paso {{ f.pasoActual }}: {{ f.pasoActualTexto }}</span>
              <span class="muted">{{ momento(f.creadoEn) }}</span>
            </div>
            <div class="botones">
              <button class="btn btn-rechazar" :disabled="ocupado" @click="rechazar(f)">Rechazar</button>
              <button class="btn btn-primario" :disabled="ocupado" @click="aprobar(f)">Aprobar</button>
            </div>
            <RouterLink :to="`/solicitudes/${f.id}`" class="ver-todo centro">Ver detalle <Icono n="derecha" :t="16" /></RouterLink>
          </article>
        </div>

        <!-- Tabla en escritorio -->
        <div v-else class="tabla-envoltura">
          <table class="tabla">
            <thead>
              <tr>
                <th>Colaborador</th><th>Tipo</th><th>Periodo</th><th class="num">Días</th><th class="num">Saldo disp.</th><th>Cobertura</th><th>Paso actual</th><th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="f in bandeja.datos.value.items" :key="f.id" :class="{ resaltada: f.id === resaltada }" :data-solicitud="f.id">
                <td><Persona :p="f.colaborador" :sub="f.colaborador.area ?? f.colaborador.puesto" /></td>
                <td class="col-tipo">
                  <div class="negrita">{{ f.tipoTexto }}</div>
                  <div v-if="f.comentario" class="pequeno muted recorte" :title="f.comentario">“{{ f.comentario }}”</div>
                  <div class="pequeno muted">{{ f.folio }}</div>
                </td>
                <td class="nowrap">{{ rango(f.fechaInicio, f.fechaFin, true) }}</td>
                <td class="num">{{ num(f.diasHabiles, 1) }}</td>
                <td class="num">
                  <template v-if="f.saldoDisponible !== null">
                    <span :class="{ insuficiente: f.saldoDisponible < f.diasHabiles }">{{ num(f.saldoDisponible, 1) }}</span>
                  </template>
                  <span v-else class="muted">—</span>
                </td>
                <td class="col-cob">
                  <Persona v-if="f.cobertura" :p="f.cobertura" :sub="f.cobertura.area ?? f.cobertura.puesto" :t="32" />
                  <div v-if="f.coberturaNota" class="pequeno muted recorte" :title="f.coberturaNota">{{ f.coberturaNota }}</div>
                  <span v-if="!f.cobertura && !f.coberturaNota" class="muted">—</span>
                </td>
                <td><span v-if="f.pasoActualTexto" class="etiqueta ambar"><span class="punto" />{{ f.pasoActualTexto }}</span></td>
                <td>
                  <div class="fila acciones">
                    <button class="btn btn-primario btn-ch" :disabled="ocupado" :data-prueba="`aprobar-${f.id}`" @click="aprobar(f)">Aprobar</button>
                    <button class="btn btn-rechazar btn-ch" :disabled="ocupado" :data-prueba="`rechazar-${f.id}`" @click="rechazar(f)">Rechazar</button>
                    <RouterLink :to="`/solicitudes/${f.id}`" class="btn-icono borde" aria-label="Ver detalle" title="Ver detalle"><Icono n="ojo" :t="18" /></RouterLink>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- Delegaciones -->
      <section class="tarjeta" :class="{ compacta: esMovil }">
        <div class="tarjeta-cabecera">
          <div>
            <h2 class="titulo-tarjeta">Delegaciones</h2>
            <p class="subtitulo-tarjeta">Durante el periodo, tus pendientes llegan a la persona en quien delegas</p>
          </div>
          <button v-if="puedeDelegar && conEncabezadoMovil" class="btn btn-contorno btn-ch" @click="abrirModal"><Icono n="mas" :t="16" />Delegar</button>
        </div>
        <div v-if="delegaciones.cargando.value && !delegaciones.datos.value" class="esqueleto" style="height: 60px" />
        <div v-else-if="!mias.length" class="vacio">
          <p>No tienes delegaciones vigentes ni programadas.</p>
          <button v-if="puedeDelegar" class="btn btn-verde-contorno btn-ch" style="margin-top: 12px" @click="abrirModal"><Icono n="personaMas" :t="16" />Delegar mis aprobaciones</button>
        </div>
        <div v-else class="lista-delegaciones">
          <div v-for="d in mias" :key="d.id" class="delegacion">
            <span class="dir-icono" :class="d.de.id === yo ? 'salida' : 'entrada'"><Icono :n="d.de.id === yo ? 'flecha' : 'regresar'" :t="18" /></span>
            <div class="crecer">
              <div class="pequeno muted">{{ d.de.id === yo ? 'Delegaste en' : 'Te delegó' }}</div>
              <Persona :p="d.de.id === yo ? d.hacia : d.de" :t="34" />
            </div>
            <div class="delegacion-datos">
              <b>{{ d.periodo }}</b>
              <small class="muted">{{ d.tiposTexto }}</small>
            </div>
            <Estado :v="d.estado" />
            <button v-if="d.de.id === yo" class="btn btn-rechazar btn-ch" :disabled="ocupado" :data-prueba="`terminar-${d.id}`" @click="terminar(d)">Terminar</button>
          </div>
        </div>
      </section>
    </div>

    <Modal v-if="modal" titulo="Delegar mis aprobaciones" subtitulo="Tus solicitudes pendientes llegarán a esta persona durante el periodo." :ancho="520" @cerrar="modal = false">
      <div class="columna" style="gap: 16px">
        <div class="campo">
          <label>Delegar en</label>
          <SelectorColaborador v-model="nueva.haciaColaboradorId" :ruta="rutaDelegados" :excluir="yo ? [yo] : []" placeholder="Buscar persona…" />
          <span v-if="intento && !nueva.haciaColaboradorId" class="error-campo">Elige a la persona que aprobará por ti.</span>
        </div>
        <div class="rejilla-form">
          <div class="campo">
            <label for="del-ini">Fecha de inicio</label>
            <input id="del-ini" v-model="nueva.fechaInicio" type="date" class="entrada" :min="iso(new Date())" />
          </div>
          <div class="campo">
            <label for="del-fin">Fecha de término</label>
            <input id="del-fin" v-model="nueva.fechaFin" type="date" class="entrada" :min="nueva.fechaInicio" :class="{ error: intento && (!nueva.fechaFin || nueva.fechaFin < nueva.fechaInicio) }" />
            <span v-if="intento && !nueva.fechaFin" class="error-campo">Indica hasta cuándo.</span>
            <span v-else-if="intento && nueva.fechaFin < nueva.fechaInicio" class="error-campo">No puede ser anterior al inicio.</span>
          </div>
        </div>
        <div class="campo">
          <span class="etiqueta-campo">Tipos de solicitud</span>
          <div class="columna" style="gap: 8px">
            <label v-for="t in TIPOS_DELEGACION" :key="t.v" class="casilla"><input v-model="nueva.tipos" type="checkbox" :value="t.v" />{{ t.t }}</label>
          </div>
          <span v-if="intento && !nueva.tipos.length" class="error-campo">Selecciona al menos un tipo.</span>
        </div>
      </div>
      <template #pie>
        <button class="btn btn-contorno" @click="modal = false">Cancelar</button>
        <button class="btn btn-primario" :disabled="ocupado" data-prueba="guardar-delegacion" @click="guardarDelegacion">Delegar</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.cuerpo { display: flex; flex-direction: column; gap: 20px; }
.bandeja.movil .cuerpo { padding: 16px; gap: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; }
.filtros { display: flex; gap: 8px; overflow-x: auto; scrollbar-width: none; margin-bottom: 14px; padding-bottom: 2px; }
.filtros::-webkit-scrollbar { display: none; }
.filtro { height: 34px; padding: 0 14px; border-radius: 999px; border: 1px solid var(--gris-200); background: var(--gris-50); color: var(--texto-2); font-size: 14px; font-weight: 500; white-space: nowrap; }
.filtro:hover { border-color: var(--gris-300); }
.filtro.activo { background: var(--verde-claro); border-color: var(--verde-borde); color: var(--verde-texto); font-weight: 650; }

.tabla tr.resaltada td { background: var(--verde-suave); }
.tabla tr.resaltada td:first-child { box-shadow: inset 4px 0 0 var(--verde); }
.col-tipo { max-width: 220px; }
.col-cob { max-width: 220px; }
.recorte { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 210px; }
.nowrap { white-space: nowrap; }
.insuficiente { color: var(--rojo-texto); font-weight: 700; }
.acciones { gap: 8px; flex-wrap: nowrap; }

.tarjetas { display: flex; flex-direction: column; gap: 12px; }
.solicitud { border: 1px solid var(--gris-200); border-radius: var(--radio); padding: 14px; display: flex; flex-direction: column; gap: 12px; background: #fff; }
.solicitud.resaltada { border-color: var(--verde); box-shadow: 0 0 0 3px rgba(10, 170, 74, .14); }
.datos-mini { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; background: var(--gris-50); border-radius: var(--radio-s); padding: 10px 12px; }
.datos-mini small { display: block; font-size: 12px; color: var(--texto-3); }
.datos-mini b { font-size: 14px; color: var(--titulo); }
.cobertura { display: flex; flex-direction: column; gap: 6px; }
.comentario { font-size: 14px; color: var(--texto-2); font-style: italic; overflow-wrap: anywhere; }
.botones { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.centro { align-self: center; }

.lista-delegaciones { display: flex; flex-direction: column; gap: 10px; }
.delegacion { display: flex; align-items: center; gap: 14px; padding: 12px 14px; border: 1px solid var(--gris-200); border-radius: var(--radio); flex-wrap: wrap; }
.dir-icono { width: 36px; height: 36px; border-radius: 50%; display: grid; place-items: center; flex-shrink: 0; }
.dir-icono.salida { background: var(--azul-claro); color: var(--azul); }
.dir-icono.entrada { background: var(--verde-claro); color: var(--verde-texto); }
.delegacion-datos { display: flex; flex-direction: column; min-width: 150px; }
.delegacion-datos b { font-size: 14.5px; color: var(--titulo); }
.delegacion-datos small { font-size: 13px; }

@media (max-width: 760px) {
  .delegacion { gap: 10px; }
  .delegacion .crecer { flex-basis: calc(100% - 50px); }
  .delegacion-datos { flex: 1; min-width: 0; }
}
</style>
