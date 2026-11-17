<script setup lang="ts">
// Pestaña "Datos laborales": puesto y organización, esquema de trabajo, ubicaciones autorizadas (geocercas) y asignaciones temporales.
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import Mapa from '@/componentes/Mapa.vue'
import { api } from '@/api'
import { useUi } from '@/stores/ui'
import { useAccion } from '@/utils/usos'
import { fechaMedia, hora, iso, rango, num } from '@/utils/formato'
import { TIPOS_ASIGNACION, antiguedad, listaDias, erroresPorCampo } from '../comun'

interface Ubicacion { id: number; tipo: 'Domicilio' | 'Otro'; nombre: string; latitud: number; longitud: number; radioMetros: number; activa: boolean }
interface Asignacion {
  id: number; tipo: 'HomeOffice' | 'OtraSucursal' | 'Comision'; tipoTexto: string; fechaInicio: string; fechaFin: string; motivo: string
  sucursalId?: number | null; sucursal?: string | null; ubicacionAutorizadaId?: number | null; cancelada: boolean; estado: string
}

const props = defineProps<{ ficha: any; id: number }>()
const emit = defineEmits<{ actualizar: [] }>()
const route = useRoute()
const ui = useUi()
const { ocupado, ejecutar } = useAccion()

const l = computed(() => props.ficha?.laborales ?? {})
const e = computed(() => props.ficha?.esquema ?? {})
const puedeEditar = computed(() => !!props.ficha?.puedeEditar)

const politicas = ref<{ id: number; nombre: string; porDefecto: boolean }[]>([])
const sucursales = ref<{ id: number; nombre: string; latitud: number; longitud: number }[]>([])
const ubicaciones = ref<Ubicacion[]>([])
const asignaciones = ref<Asignacion[]>([])
const cargandoUb = ref(true)
const cargandoAsig = ref(true)
const tarjetaAsig = ref<HTMLElement>()
const resaltarAsig = ref(false)

const politica = computed(() => {
  if (l.value.politicaVacacionesId) return politicas.value.find(p => p.id === l.value.politicaVacacionesId)?.nombre ?? '—'
  const d = politicas.value.find(p => p.porDefecto)
  return d ? `${d.nombre} (por defecto)` : 'Por defecto'
})
const DESCANSO = ['', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb', 'dom']
const turno = computed(() => {
  const t = l.value.turno
  if (!t) return null
  const desc = (t.diasDescanso as number[] | undefined)?.map(d => DESCANSO[d]).join(', ')
  return { nombre: t.nombre, horario: `${hora(t.horaEntrada)} a ${hora(t.horaSalida)}`, detalle: `Tolerancia ${t.toleranciaMinutos} min${desc ? ` · Descansa ${desc}` : ''}` }
})
const datos = computed(() => [
  { icono: 'fichaPersona', etiqueta: 'Número de colaborador', valor: props.ficha?.numero },
  { icono: 'maletin', etiqueta: 'Puesto', valor: props.ficha?.puesto },
  { icono: 'edificio', etiqueta: 'Sucursal', valor: l.value.sucursal },
  { icono: 'organigrama', etiqueta: 'Área', valor: l.value.area },
  { icono: 'personas', etiqueta: 'Equipo', valor: l.value.equipo },
  { icono: 'calendario', etiqueta: 'Fecha de ingreso', valor: l.value.fechaIngreso ? fechaMedia(l.value.fechaIngreso, true) : null, sub: antiguedad(l.value.fechaIngreso) },
  { icono: 'terminal', etiqueta: 'Dispositivo asignado', valor: l.value.dispositivo ?? 'Sin dispositivo' },
  { icono: 'vacaciones', etiqueta: 'Política de vacaciones', valor: politica.value },
  { icono: 'candado', etiqueta: 'PIN del quiosco', valor: l.value.tienePin ? 'Configurado' : 'Sin PIN' },
  { icono: 'colaboradores', etiqueta: 'Reportes directos', valor: num(l.value.reportesDirectos ?? 0) },
])

async function cargarUbicaciones() {
  cargandoUb.value = true
  try { ubicaciones.value = await api.get<Ubicacion[]>(`/colaboradores/${props.id}/ubicaciones`) } catch (x) { ui.error(x) } finally { cargandoUb.value = false }
}
async function cargarAsignaciones() {
  cargandoAsig.value = true
  try { asignaciones.value = await api.get<Asignacion[]>(`/colaboradores/${props.id}/asignaciones`) } catch (x) { ui.error(x) } finally { cargandoAsig.value = false }
}
async function irAsignaciones() {
  if (route.query.asignaciones !== '1') return
  await nextTick()
  tarjetaAsig.value?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  resaltarAsig.value = true
  setTimeout(() => (resaltarAsig.value = false), 2200)
}
onMounted(async () => {
  api.get('/politicas-vacaciones').then(r => (politicas.value = r)).catch(() => {})
  if (puedeEditar.value) api.get('/sucursales', { tamano: 100 }).then(r => (sucursales.value = r.items)).catch(() => {})
  await Promise.all([cargarUbicaciones(), cargarAsignaciones()])
  irAsignaciones()
})
watch(() => route.query.asignaciones, irAsignaciones)

// ───── Ubicaciones autorizadas ─────
const modalUb = ref(false)
const ub = reactive({ id: null as number | null, tipo: 'Domicilio' as 'Domicilio' | 'Otro', nombre: '', lat: null as number | null, lng: null as number | null, radio: 150, activa: true })
const errUb = ref<Record<string, string>>({})
const esCoord = (v: unknown, max: number): v is number => typeof v === 'number' && Number.isFinite(v) && Math.abs(v) <= max
const centroUb = computed<[number, number] | null>(() => (esCoord(ub.lat, 90) && esCoord(ub.lng, 180) ? [ub.lat, ub.lng] : null))
const localizando = ref(false)

function abrirUbicacion(u?: Ubicacion) {
  errUb.value = {}
  if (u) Object.assign(ub, { id: u.id, tipo: u.tipo, nombre: u.nombre, lat: u.latitud, lng: u.longitud, radio: u.radioMetros, activa: u.activa })
  else {
    const suc = sucursales.value.find(x => x.id === l.value.sucursalId)
    Object.assign(ub, { id: null, tipo: 'Domicilio', nombre: ubicaciones.value.length ? '' : 'Domicilio', lat: suc?.latitud ?? null, lng: suc?.longitud ?? null, radio: 150, activa: true })
  }
  modalUb.value = true
}
function mover(lat: number, lng: number) { ub.lat = Math.round(lat * 1e6) / 1e6; ub.lng = Math.round(lng * 1e6) / 1e6; delete errUb.value.ubicacion }
function miUbicacion() {
  if (!navigator.geolocation) { ui.avisar('Este navegador no permite obtener la ubicación.', 'error'); return }
  localizando.value = true
  navigator.geolocation.getCurrentPosition(
    p => { mover(p.coords.latitude, p.coords.longitude); localizando.value = false },
    () => { ui.avisar('No se pudo obtener tu ubicación. Revisa los permisos del navegador.', 'error'); localizando.value = false },
    { enableHighAccuracy: true, timeout: 10000 }
  )
}
async function guardarUbicacion() {
  const er: Record<string, string> = {}
  if (!ub.nombre.trim()) er.nombre = 'Escribe un nombre para identificar la ubicación.'
  if (!centroUb.value) er.ubicacion = 'Coloca el punto en el mapa.'
  if (!(ub.radio >= 20 && ub.radio <= 2000)) er.radioMetros = 'El radio debe estar entre 20 y 2000 metros.'
  errUb.value = er
  if (Object.keys(er).length) return
  const cuerpo = { tipo: ub.tipo, nombre: ub.nombre.trim(), latitud: ub.lat, longitud: ub.lng, radioMetros: Math.round(ub.radio), activa: ub.activa }
  await ejecutar(async () => {
    try {
      if (ub.id) await api.put(`/ubicaciones/${ub.id}`, cuerpo)
      else await api.post(`/colaboradores/${props.id}/ubicaciones`, cuerpo)
    } catch (x) { errUb.value = erroresPorCampo(x); throw x }
    modalUb.value = false
    await cargarUbicaciones()
    emit('actualizar')
  }, ub.id ? 'Ubicación actualizada' : 'Ubicación agregada')
}

// ───── Asignaciones temporales ─────
const modalAsig = ref(false)
const hoy = iso(new Date())
const asig = reactive({ id: null as number | null, tipo: 'HomeOffice' as Asignacion['tipo'], fechaInicio: hoy, fechaFin: hoy, motivo: '', sucursalId: null as number | null, ubicacionAutorizadaId: null as number | null })
const errAsig = ref<Record<string, string>>({})
const domicilios = computed(() => ubicaciones.value.filter(u => u.activa))
const otrasSucursales = computed(() => sucursales.value.filter(x => x.id !== l.value.sucursalId))
const puedeModificar = (a: Asignacion) => puedeEditar.value && !a.cancelada && a.estado !== 'Concluida'
const icoAsig = (t: string) => TIPOS_ASIGNACION.find(x => x.v === t)?.icono ?? 'asignacion'

function abrirAsignacion(a?: Asignacion) {
  errAsig.value = {}
  if (a) Object.assign(asig, { id: a.id, tipo: a.tipo, fechaInicio: a.fechaInicio, fechaFin: a.fechaFin, motivo: a.motivo, sucursalId: a.sucursalId ?? null, ubicacionAutorizadaId: a.ubicacionAutorizadaId ?? null })
  else Object.assign(asig, { id: null, tipo: 'HomeOffice', fechaInicio: hoy, fechaFin: hoy, motivo: '', sucursalId: null, ubicacionAutorizadaId: null })
  modalAsig.value = true
}
async function guardarAsignacion() {
  const er: Record<string, string> = {}
  if (!asig.fechaInicio) er.fechaInicio = 'Indica la fecha de inicio.'
  if (!asig.fechaFin) er.fechaFin = 'Indica la fecha de término.'
  else if (asig.fechaInicio && asig.fechaFin < asig.fechaInicio) er.fechaFin = 'La fecha de término no puede ser anterior a la de inicio.'
  if (!asig.motivo.trim()) er.motivo = 'El motivo es obligatorio.'
  if (asig.tipo === 'OtraSucursal' && !asig.sucursalId) er.sucursalId = 'Selecciona la sucursal de la asignación.'
  if (asig.tipo === 'HomeOffice' && !domicilios.value.length) er.ubicacionAutorizadaId = 'Primero registra un domicilio en Ubicaciones autorizadas.'
  errAsig.value = er
  if (Object.keys(er).length) return
  const cuerpo = {
    tipo: asig.tipo, fechaInicio: asig.fechaInicio, fechaFin: asig.fechaFin, motivo: asig.motivo.trim(),
    sucursalId: asig.tipo === 'OtraSucursal' ? asig.sucursalId : null,
    ubicacionAutorizadaId: asig.tipo === 'HomeOffice' ? asig.ubicacionAutorizadaId : null
  }
  await ejecutar(async () => {
    try {
      if (asig.id) await api.put(`/asignaciones/${asig.id}`, cuerpo)
      else await api.post(`/colaboradores/${props.id}/asignaciones`, cuerpo)
    } catch (x) { errAsig.value = erroresPorCampo(x); throw x }
    modalAsig.value = false
    await cargarAsignaciones()
    emit('actualizar')
  }, asig.id ? 'Asignación actualizada' : 'Asignación registrada')
}
async function cancelarAsignacion(a: Asignacion) {
  const r = await ui.confirmar({ titulo: 'Cancelar asignación', mensaje: `${a.tipoTexto} · ${rango(a.fechaInicio, a.fechaFin, true)}. El colaborador volverá a su esquema habitual.`, aceptar: 'Cancelar asignación', cancelar: 'Volver', peligro: true })
  if (!r.ok) return
  await ejecutar(async () => { await api.post(`/asignaciones/${a.id}/cancelar`); await cargarAsignaciones(); emit('actualizar') }, 'Asignación cancelada')
}
</script>

<template>
  <div class="laborales">
    <section class="tarjeta datos">
      <div class="tarjeta-cabecera">
        <div class="izq"><Icono n="maletin" :t="26" /><h2 class="titulo-tarjeta">Datos laborales</h2></div>
        <RouterLink v-if="puedeEditar" :to="`/colaboradores/${id}/editar`" class="btn btn-contorno btn-ch"><Icono n="lapiz" :t="17" />Editar</RouterLink>
      </div>
      <dl class="rejilla-datos">
        <div v-for="d in datos" :key="d.etiqueta" class="dato">
          <dt><Icono :n="d.icono" :t="18" />{{ d.etiqueta }}</dt>
          <dd>{{ d.valor || '—' }}<small v-if="d.sub" class="muted"> · {{ d.sub }}</small></dd>
        </div>
        <div class="dato">
          <dt><Icono n="sol" :t="18" />Turno</dt>
          <dd v-if="turno">{{ turno.nombre }} · {{ turno.horario }}<small class="muted bloque">{{ turno.detalle }}</small></dd>
          <dd v-else>Sin turno</dd>
        </div>
        <div class="dato">
          <dt><Icono n="persona" :t="18" />Supervisor</dt>
          <dd><Persona v-if="l.supervisor" :p="l.supervisor" :sub="l.supervisor.area ?? l.supervisor.numero" :t="32" enlace /><span v-else class="etiqueta ambar">Sin supervisor</span></dd>
        </div>
      </dl>
    </section>

    <section class="tarjeta esquema">
      <div class="tarjeta-cabecera"><div class="izq"><Icono n="edificioDoble" :t="26" /><h2 class="titulo-tarjeta">Esquema de trabajo</h2></div></div>
      <dl class="filas-esquema">
        <div><dt><Icono n="monitor" :t="20" />Modalidad</dt><dd><span class="etiqueta verde cuadrada">{{ e.modalidadTexto }}</span></dd></div>
        <div><dt><Icono n="ubicacion" :t="20" />Centro base</dt><dd>{{ e.centroBase ?? '—' }}</dd></div>
        <div><dt><Icono n="edificioDoble" :t="20" />Días en oficina</dt><dd>{{ e.diasOficinaTexto ?? listaDias(e.diasOficina) }}</dd></div>
        <div><dt><Icono n="casa" :t="20" />Días remotos</dt><dd>{{ e.diasRemotosTexto ?? listaDias(e.diasRemotos) }}</dd></div>
        <div><dt><Icono n="celular" :t="20" />Registro permitido</dt><dd>{{ e.registroTexto }}</dd></div>
        <div><dt><Icono n="camara" :t="20" />Foto de evidencia</dt><dd>{{ e.requiereFoto ? 'Requerida al registrar' : 'No requerida' }}</dd></div>
      </dl>
    </section>

    <section class="tarjeta ubicaciones">
      <div class="tarjeta-cabecera">
        <div class="izq"><Icono n="ubicacion" :t="26" /><div><h2 class="titulo-tarjeta">Ubicaciones autorizadas</h2><p class="subtitulo-tarjeta">Domicilios y lugares donde puede registrar asistencia fuera de la sucursal.</p></div></div>
        <button v-if="puedeEditar" class="btn btn-contorno btn-ch" @click="abrirUbicacion()"><Icono n="mas" :t="17" />Agregar</button>
      </div>
      <div v-if="cargandoUb" class="esqueleto" style="height: 64px" />
      <ul v-else-if="ubicaciones.length" class="lista-ub">
        <li v-for="u in ubicaciones" :key="u.id" :class="{ inactiva: !u.activa }">
          <span class="ub-ico"><Icono :n="u.tipo === 'Domicilio' ? 'casa' : 'ubicacion'" :t="22" /></span>
          <div class="crecer">
            <b>{{ u.nombre }}</b>
            <span class="muted pequeno">{{ u.tipo === 'Domicilio' ? 'Domicilio' : 'Otro lugar' }} · radio {{ num(u.radioMetros) }} m · {{ u.latitud.toFixed(5) }}, {{ u.longitud.toFixed(5) }}</span>
          </div>
          <Estado :v="u.activa ? 'Activa' : 'Inactiva'" />
          <button v-if="puedeEditar" class="btn-icono" aria-label="Editar ubicación" @click="abrirUbicacion(u)"><Icono n="lapiz" :t="18" /></button>
        </li>
      </ul>
      <p v-else class="vacio">Sin ubicaciones autorizadas. {{ puedeEditar ? 'Agrega su domicilio para habilitar home office.' : '' }}</p>
    </section>

    <section id="asignaciones" ref="tarjetaAsig" class="tarjeta asignaciones" :class="{ resaltada: resaltarAsig }">
      <div class="tarjeta-cabecera">
        <div class="izq"><Icono n="asignacion" :t="26" /><div><h2 class="titulo-tarjeta">Asignaciones temporales</h2><p class="subtitulo-tarjeta">Home office, cambio de sucursal o comisiones por periodo definido.</p></div></div>
        <button v-if="puedeEditar" class="btn btn-primario btn-ch" @click="abrirAsignacion()"><Icono n="mas" :t="17" />Nueva asignación</button>
      </div>
      <div v-if="cargandoAsig" class="esqueleto" style="height: 80px" />
      <ul v-else-if="asignaciones.length" class="lista-asig">
        <li v-for="a in asignaciones" :key="a.id" :class="{ apagada: a.cancelada || a.estado === 'Concluida' }">
          <span class="asig-ico"><Icono :n="icoAsig(a.tipo)" :t="24" /></span>
          <div class="crecer">
            <div class="fila envolver" style="gap: 10px"><b>{{ a.tipoTexto }}</b><Estado :v="a.estado" /></div>
            <span class="periodo">{{ rango(a.fechaInicio, a.fechaFin, true) }}<template v-if="a.sucursal"> · {{ a.sucursal }}</template></span>
            <span class="muted motivo"><Icono n="notas" :t="16" />{{ a.motivo }}</span>
          </div>
          <div v-if="puedeModificar(a)" class="fila acciones">
            <button class="btn btn-contorno btn-ch" @click="abrirAsignacion(a)"><Icono n="lapiz" :t="16" />Editar</button>
            <button class="btn btn-rechazar btn-ch" :disabled="ocupado" @click="cancelarAsignacion(a)">Cancelar</button>
          </div>
        </li>
      </ul>
      <p v-else class="vacio">Sin asignaciones temporales registradas.</p>
    </section>

    <!-- Modal: ubicación -->
    <Modal v-if="modalUb" :titulo="ub.id ? 'Editar ubicación' : 'Agregar ubicación'" subtitulo="Haz clic en el mapa o arrastra el marcador para ajustar el punto." :ancho="640" @cerrar="modalUb = false">
      <div class="columna" style="gap: 14px">
        <div class="rejilla-form">
          <div class="campo">
            <label for="ub-tipo">Tipo</label>
            <select id="ub-tipo" v-model="ub.tipo" class="entrada"><option value="Domicilio">Domicilio</option><option value="Otro">Otro lugar</option></select>
          </div>
          <div class="campo">
            <label for="ub-nombre">Nombre</label>
            <input id="ub-nombre" v-model="ub.nombre" class="entrada" :class="{ error: errUb.nombre }" placeholder="Ej. Domicilio particular" />
            <span v-if="errUb.nombre" class="error-campo">{{ errUb.nombre }}</span>
          </div>
        </div>
        <Mapa :centro="centroUb" :radio="ub.radio" editable :alto="300" @mover="mover" />
        <div class="fila-entre envolver">
          <span v-if="errUb.ubicacion" class="error-campo">{{ errUb.ubicacion }}</span>
          <span v-else class="ayuda">{{ centroUb ? `${centroUb[0].toFixed(6)}, ${centroUb[1].toFixed(6)}` : 'Sin punto seleccionado' }}</span>
          <button type="button" class="btn btn-contorno btn-ch" :disabled="localizando" @click="miUbicacion"><Icono n="navegar" :t="16" />{{ localizando ? 'Localizando…' : 'Usar mi ubicación actual' }}</button>
        </div>
        <div class="rejilla-form">
          <div class="campo">
            <label for="ub-lat">Latitud</label>
            <input id="ub-lat" v-model.number="ub.lat" type="number" step="0.000001" class="entrada" />
          </div>
          <div class="campo">
            <label for="ub-lng">Longitud</label>
            <input id="ub-lng" v-model.number="ub.lng" type="number" step="0.000001" class="entrada" />
          </div>
        </div>
        <div class="campo">
          <label for="ub-radio">Radio permitido: <b>{{ num(ub.radio) }} m</b></label>
          <div class="fila">
            <input v-model.number="ub.radio" type="range" min="20" max="2000" step="10" class="crecer rango" aria-label="Radio en metros" />
            <input id="ub-radio" v-model.number="ub.radio" type="number" min="20" max="2000" class="entrada radio-num" :class="{ error: errUb.radioMetros }" />
          </div>
          <span v-if="errUb.radioMetros" class="error-campo">{{ errUb.radioMetros }}</span>
          <span v-else class="ayuda">Entre 20 y 2000 metros. El registro fuera de este radio queda como "Fuera de área".</span>
        </div>
        <label class="casilla"><input v-model="ub.activa" type="checkbox" />Ubicación activa</label>
      </div>
      <template #pie>
        <button class="btn btn-contorno" @click="modalUb = false">Cancelar</button>
        <button class="btn btn-primario" :disabled="ocupado" @click="guardarUbicacion">Guardar ubicación</button>
      </template>
    </Modal>

    <!-- Modal: asignación temporal -->
    <Modal v-if="modalAsig" :titulo="asig.id ? 'Editar asignación' : 'Nueva asignación temporal'" :subtitulo="ficha.nombre" :ancho="560" @cerrar="modalAsig = false">
      <div class="columna" style="gap: 14px">
        <div class="campo">
          <span class="etiqueta-campo">Tipo</span>
          <div class="tipos">
            <button v-for="t in TIPOS_ASIGNACION" :key="t.v" type="button" class="tipo" :class="{ sel: asig.tipo === t.v }" @click="asig.tipo = t.v; errAsig = {}">
              <Icono :n="t.icono" :t="20" />{{ t.t }}
            </button>
          </div>
        </div>
        <div class="rejilla-form">
          <div class="campo">
            <label for="as-ini">Fecha de inicio</label>
            <input id="as-ini" v-model="asig.fechaInicio" type="date" class="entrada" :class="{ error: errAsig.fechaInicio }" />
            <span v-if="errAsig.fechaInicio" class="error-campo">{{ errAsig.fechaInicio }}</span>
          </div>
          <div class="campo">
            <label for="as-fin">Fecha de término</label>
            <input id="as-fin" v-model="asig.fechaFin" type="date" class="entrada" :class="{ error: errAsig.fechaFin }" :min="asig.fechaInicio" />
            <span v-if="errAsig.fechaFin" class="error-campo">{{ errAsig.fechaFin }}</span>
          </div>
        </div>
        <div v-if="asig.tipo === 'OtraSucursal'" class="campo">
          <label for="as-suc">Sucursal</label>
          <select id="as-suc" v-model="asig.sucursalId" class="entrada" :class="{ error: errAsig.sucursalId }">
            <option :value="null" disabled>Selecciona…</option>
            <option v-for="x in otrasSucursales" :key="x.id" :value="x.id">{{ x.nombre }}</option>
          </select>
          <span v-if="errAsig.sucursalId" class="error-campo">{{ errAsig.sucursalId }}</span>
          <span v-else-if="!otrasSucursales.length" class="ayuda">No tienes acceso a otras sucursales.</span>
        </div>
        <div v-if="asig.tipo === 'HomeOffice'" class="campo">
          <label for="as-ub">Domicilio autorizado</label>
          <select id="as-ub" v-model="asig.ubicacionAutorizadaId" class="entrada" :class="{ error: errAsig.ubicacionAutorizadaId }" :disabled="!domicilios.length">
            <option :value="null">{{ domicilios.length ? 'Cualquier domicilio activo' : 'Sin domicilios registrados' }}</option>
            <option v-for="u in domicilios" :key="u.id" :value="u.id">{{ u.nombre }} (radio {{ u.radioMetros }} m)</option>
          </select>
          <span v-if="errAsig.ubicacionAutorizadaId" class="error-campo">{{ errAsig.ubicacionAutorizadaId }}</span>
        </div>
        <div class="campo">
          <label for="as-motivo">Motivo</label>
          <textarea id="as-motivo" v-model="asig.motivo" class="entrada" :class="{ error: errAsig.motivo }" rows="3" placeholder="Ej. Apoyo por inventario en sucursal" />
          <span v-if="errAsig.motivo" class="error-campo">{{ errAsig.motivo }}</span>
        </div>
      </div>
      <template #pie>
        <button class="btn btn-contorno" @click="modalAsig = false">Cancelar</button>
        <button class="btn btn-primario" :disabled="ocupado" @click="guardarAsignacion">{{ asig.id ? 'Guardar cambios' : 'Registrar asignación' }}</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.laborales { display: grid; grid-template-columns: minmax(0, 3fr) minmax(0, 2fr); gap: 20px; align-items: start; }
.asignaciones { grid-column: 1 / -1; scroll-margin-top: 90px; transition: box-shadow .4s, border-color .4s; }
.asignaciones.resaltada { border-color: var(--verde); box-shadow: 0 0 0 3px rgba(10, 170, 74, .15); }
.tarjeta-cabecera .izq .icono { color: var(--titulo); }
.rejilla-datos { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 28px; margin: 0; }
.dato { padding: 12px 0; border-bottom: 1px solid var(--gris-100); min-width: 0; }
.dato dt { display: flex; align-items: center; gap: 8px; color: var(--texto-3); font-size: 13.5px; }
.dato dt .icono { color: var(--gris-500); }
.dato dd { margin: 4px 0 0 26px; color: var(--titulo); font-size: 15.5px; font-weight: 500; overflow-wrap: anywhere; }
.dato dd .persona { margin-top: 2px; }
.bloque { display: block; font-weight: 400; font-size: 13px; }
.filas-esquema { margin: 0; display: flex; flex-direction: column; gap: 14px; }
.filas-esquema div { display: grid; grid-template-columns: 170px 1fr; align-items: center; gap: 8px; }
.filas-esquema dt { display: flex; align-items: center; gap: 12px; color: var(--texto-2); font-size: 14.5px; }
.filas-esquema dd { margin: 0; color: var(--titulo); font-size: 15px; }
.lista-ub, .lista-asig { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
.lista-ub li { display: flex; align-items: center; gap: 14px; padding: 12px 14px; border: 1px solid var(--gris-200); border-radius: 10px; }
.lista-ub li.inactiva { background: var(--gris-50); }
.lista-ub b, .lista-asig b { color: var(--titulo); font-weight: 650; display: block; }
.lista-ub .crecer > span { display: block; }
.ub-ico { width: 44px; height: 44px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-texto); display: grid; place-items: center; flex-shrink: 0; }
.lista-asig li { display: flex; align-items: center; gap: 16px; padding: 14px 16px; border-radius: 10px; background: #f0faf7; }
.lista-asig li.apagada { background: var(--gris-50); }
.lista-asig li.apagada .asig-ico { background: var(--gris-200); color: var(--texto-2); }
.asig-ico { width: 52px; height: 52px; border-radius: 50%; background: #d8f3ea; color: #0c8e6c; display: grid; place-items: center; flex-shrink: 0; }
.periodo { display: block; font-size: 15px; color: var(--titulo); margin-top: 2px; }
.motivo { display: flex; align-items: center; gap: 6px; font-size: 14px; margin-top: 4px; }
.acciones { flex-shrink: 0; gap: 8px; }
.tipos { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; }
.tipo { display: flex; align-items: center; justify-content: center; gap: 8px; height: 46px; border-radius: var(--radio-s); border: 1px solid var(--gris-300); background: #fff; color: var(--titulo); font-weight: 500; font-size: 14.5px; }
.tipo.sel { border-color: var(--verde); background: var(--verde-suave); color: var(--verde-texto); font-weight: 650; box-shadow: 0 0 0 1px var(--verde) inset; }
.rango { accent-color: var(--verde); }
.radio-num { width: 110px; }
@media (max-width: 1100px) { .laborales { grid-template-columns: 1fr; } }
@media (max-width: 640px) {
  .rejilla-datos { grid-template-columns: 1fr; }
  .filas-esquema div { grid-template-columns: 1fr; gap: 2px; }
  .lista-asig li { flex-wrap: wrap; }
  .acciones { width: 100%; }
  .acciones .btn { flex: 1; }
  .tipos { grid-template-columns: 1fr; }
}
</style>
