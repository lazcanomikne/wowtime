<script setup lang="ts">
// Alta y edición de colaborador: datos personales, laborales, esquema de trabajo y acceso.
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import { api, ErrorApi, type Persona } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { iso } from '@/utils/formato'
import { DIAS_SEMANA, GENEROS, MODALIDADES, REGISTROS, erroresPorCampo } from './comun'

interface Opcion { id: number; nombre: string }
type Modalidad = 'Presencial' | 'Hibrido' | 'HomeOffice'
type Registro = 'Terminal' | 'App' | 'TerminalYApp' | 'Ninguno'

const route = useRoute()
const router = useRouter()
const s = useSesion()
const ui = useUi()

const id = computed(() => (route.params.id ? Number(route.params.id) : null))
const esEdicion = computed(() => id.value !== null)

const f = reactive({
  nombres: '', apellidos: '', email: '', telefono: '', fechaNacimiento: '', genero: '', curp: '', rfc: '', nss: '', direccion: '',
  contactoEmergenciaNombre: '', contactoEmergenciaTelefono: '',
  sucursalId: (s.sucursalId ?? s.me?.sucursales[0]?.id ?? null) as number | null,
  areaId: null as number | null, equipoId: null as number | null, puestoId: null as number | null, turnoId: null as number | null,
  supervisorId: null as number | null, fechaIngreso: iso(new Date()), dispositivoId: null as number | null, politicaVacacionesId: null as number | null,
  pin: '', crearUsuario: true,
  esquema: {
    modalidad: 'Presencial' as Modalidad, centroBaseSucursalId: (s.sucursalId ?? null) as number | null,
    diasOficina: [1, 2, 3, 4, 5] as number[], diasRemotos: [] as number[], registroPermitido: 'TerminalYApp' as Registro, requiereFoto: false
  }
})
const nombreActual = ref('')
const tienePin = ref(false)
const supervisorInicial = ref<Persona | null>(null)
const cargandoFicha = ref(false)
const guardando = ref(false)
const errores = ref<Record<string, string>>({})
const errorGeneral = ref('')
const resultado = ref<{ id: number; numero: string; passwordTemporal: string; email: string } | null>(null)
let sucursalPrevia = f.sucursalId
let areaPrevia: number | null = null

// ───── Catálogos ─────
const sucursales = computed(() => s.me?.sucursales ?? [])
const areas = ref<Opcion[]>([])
const equipos = ref<(Opcion & { areaId: number })[]>([])
const puestos = ref<(Opcion & { nivel: number })[]>([])
const turnos = ref<(Opcion & { horaEntrada: string; horaSalida: string })[]>([])
const dispositivos = ref<(Opcion & { tipo: string })[]>([])
const politicas = ref<(Opcion & { porDefecto: boolean })[]>([])
const equiposDelArea = computed(() => f.areaId ? equipos.value.filter(e => e.areaId === f.areaId) : [])
const esRaiz = computed(() => puestos.value.find(p => p.id === f.puestoId)?.nivel === 1)
const politicaDefecto = computed(() => politicas.value.find(p => p.porDefecto)?.nombre)

const items = async <T,>(p: Promise<any>): Promise<T[]> => { try { const r = await p; return (Array.isArray(r) ? r : r.items) as T[] } catch { return [] } }

async function cargarPorSucursal() {
  const suc = f.sucursalId ?? undefined
  const [a, e, d] = await Promise.all([
    items<Opcion>(api.get('/areas', { sucursalId: suc, tamano: 500, activos: true })),
    items<Opcion & { areaId: number }>(api.get('/equipos', { sucursalId: suc, tamano: 500, activos: true })),
    items<Opcion & { tipo: string }>(api.get('/dispositivos', { sucursalId: suc, tamano: 500, activos: true })),
  ])
  areas.value = a; equipos.value = e; dispositivos.value = d.filter(x => x.tipo !== 'LectorQr')
}
async function cargarGenerales() {
  const [p, t, po] = await Promise.all([
    items<Opcion & { nivel: number }>(api.get('/puestos', { tamano: 500, activos: true, orden: 'nombre' })),
    items<Opcion & { horaEntrada: string; horaSalida: string }>(api.get('/turnos', { tamano: 500, activos: true })),
    items<Opcion & { porDefecto: boolean }>(api.get('/politicas-vacaciones')),
  ])
  puestos.value = p; turnos.value = t; politicas.value = po
}

async function cargarFicha() {
  if (!id.value) return
  cargandoFicha.value = true
  try {
    const c = await api.get(`/colaboradores/${id.value}`)
    const p = c.personales, l = c.laborales, e = c.esquema
    Object.assign(f, {
      nombres: c.nombres ?? '', apellidos: c.apellidos ?? '', email: p.email ?? '', telefono: p.telefono ?? '', fechaNacimiento: p.fechaNacimiento ?? '',
      genero: p.genero ?? '', curp: p.curp ?? '', rfc: p.rfc ?? '', nss: p.nss ?? '', direccion: p.direccion ?? '',
      contactoEmergenciaNombre: p.contactoEmergenciaNombre ?? '', contactoEmergenciaTelefono: p.contactoEmergenciaTelefono ?? '',
      sucursalId: l.sucursalId, areaId: l.areaId, equipoId: l.equipoId, puestoId: l.puestoId, turnoId: l.turnoId, supervisorId: l.supervisorId,
      fechaIngreso: l.fechaIngreso, dispositivoId: l.dispositivoId, politicaVacacionesId: l.politicaVacacionesId, pin: '', crearUsuario: false
    })
    f.esquema = {
      modalidad: e.modalidad, centroBaseSucursalId: e.centroBaseSucursalId ?? l.sucursalId, diasOficina: [...(e.diasOficina ?? [])],
      diasRemotos: [...(e.diasRemotos ?? [])], registroPermitido: e.registroPermitido, requiereFoto: !!e.requiereFoto
    }
    sucursalPrevia = l.sucursalId; areaPrevia = l.areaId
    nombreActual.value = c.nombre
    tienePin.value = !!l.tienePin
    supervisorInicial.value = l.supervisor ?? null
  } catch (e) {
    ui.error(e)
    router.replace('/colaboradores')
  } finally { cargandoFicha.value = false }
}

async function preseleccionarSupervisor() {
  const sup = Number(route.query.supervisorId)
  if (!sup || esEdicion.value) return
  try {
    const c = await api.get(`/colaboradores/${sup}`)
    supervisorInicial.value = { id: c.id, numero: c.numero, nombre: c.nombre, puesto: c.puesto, area: c.laborales?.area, iniciales: c.iniciales, fotoUrl: c.fotoUrl }
    f.supervisorId = c.id
    if (c.laborales?.areaId && !f.areaId) f.areaId = c.laborales.areaId
    if (c.laborales?.equipoId && !f.equipoId) f.equipoId = c.laborales.equipoId
  } catch { /* si no es visible, se elige a mano */ }
}

onMounted(async () => {
  await cargarFicha()
  await Promise.all([cargarGenerales(), cargarPorSucursal()])
  await preseleccionarSupervisor()
})

// Cambios hechos por el usuario (los valores cargados de la ficha actualizan los "previos" para no limpiar nada)
watch(() => f.sucursalId, v => {
  if (v === sucursalPrevia) return
  if (f.esquema.centroBaseSucursalId === sucursalPrevia || !f.esquema.centroBaseSucursalId) f.esquema.centroBaseSucursalId = v
  sucursalPrevia = v
  f.areaId = null; f.equipoId = null; f.dispositivoId = null
  cargarPorSucursal()
})
watch(() => f.areaId, v => {
  if (v === areaPrevia) return
  areaPrevia = v
  if (f.equipoId && !equiposDelArea.value.some(e => e.id === f.equipoId)) f.equipoId = null
})

// ───── Esquema de trabajo ─────
function elegirModalidad(m: Modalidad) {
  const e = f.esquema
  e.modalidad = m
  if (m === 'Presencial') { e.diasOficina = [...new Set([...e.diasOficina, ...e.diasRemotos])].sort(); e.diasRemotos = [] }
  else if (m === 'HomeOffice') { e.diasRemotos = [...new Set([...e.diasOficina, ...e.diasRemotos])].sort(); e.diasOficina = [] }
  else if (!e.diasRemotos.length) {
    const lab = e.diasOficina.length ? e.diasOficina : [1, 2, 3, 4, 5]
    e.diasRemotos = lab.filter(d => d === 2 || d === 4)
    e.diasOficina = lab.filter(d => !e.diasRemotos.includes(d))
  }
  if (m === 'Presencial' && !e.diasOficina.length) e.diasOficina = [1, 2, 3, 4, 5]
  if (m === 'HomeOffice' && !e.diasRemotos.length) e.diasRemotos = [1, 2, 3, 4, 5]
  limpiarError('diasRemotos'); limpiarError('diasOficina')
}
function alternarDia(lista: 'diasOficina' | 'diasRemotos', d: number) {
  const e = f.esquema
  const otra = lista === 'diasOficina' ? 'diasRemotos' : 'diasOficina'
  if (e[lista].includes(d)) e[lista] = e[lista].filter(x => x !== d)
  else { e[lista] = [...e[lista], d].sort(); e[otra] = e[otra].filter(x => x !== d) }
  limpiarError(lista)
}
const remotosHabilitados = computed(() => f.esquema.modalidad !== 'Presencial')
const oficinaHabilitada = computed(() => f.esquema.modalidad !== 'HomeOffice')

// ───── Validación y guardado ─────
function limpiarError(k: string) { if (errores.value[k]) { const { [k]: _, ...resto } = errores.value; errores.value = resto } }
function validar() {
  const e: Record<string, string> = {}
  if (!f.nombres.trim()) e.nombres = 'El nombre es obligatorio.'
  if (!f.apellidos.trim()) e.apellidos = 'Los apellidos son obligatorios.'
  if (f.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email.trim())) e.email = 'El correo no es válido.'
  if (!f.sucursalId) e.sucursalId = 'Selecciona la sucursal.'
  if (!f.fechaIngreso) e.fechaIngreso = 'La fecha de ingreso es obligatoria.'
  if (f.pin && !/^\d{4,8}$/.test(f.pin)) e.pin = 'El PIN debe tener de 4 a 8 dígitos.'
  if (!esEdicion.value && !f.supervisorId && !esRaiz.value) e.supervisorId = 'Selecciona un supervisor.'
  if (f.esquema.modalidad === 'Hibrido' && !f.esquema.diasRemotos.length) e.diasRemotos = 'El esquema híbrido requiere al menos un día remoto.'
  if (f.esquema.modalidad !== 'HomeOffice' && !f.esquema.diasOficina.length) e.diasOficina = 'Selecciona al menos un día en oficina.'
  if (!esEdicion.value && f.crearUsuario && !f.email.trim()) e.email = 'Para crear el usuario de acceso se necesita el correo.'
  return e
}
const nulo = (v: string) => (v.trim() ? v.trim() : null)
function cuerpo() {
  return {
    nombres: f.nombres.trim(), apellidos: f.apellidos.trim(), email: nulo(f.email), telefono: nulo(f.telefono), fechaNacimiento: f.fechaNacimiento || null,
    genero: nulo(f.genero), curp: nulo(f.curp), rfc: nulo(f.rfc), nss: nulo(f.nss), direccion: nulo(f.direccion),
    contactoEmergenciaNombre: nulo(f.contactoEmergenciaNombre), contactoEmergenciaTelefono: nulo(f.contactoEmergenciaTelefono),
    sucursalId: f.sucursalId, areaId: f.areaId, equipoId: f.equipoId, puestoId: f.puestoId, turnoId: f.turnoId, supervisorId: f.supervisorId,
    fechaIngreso: f.fechaIngreso, dispositivoId: f.dispositivoId, politicaVacacionesId: f.politicaVacacionesId, pin: nulo(f.pin),
    crearUsuario: !esEdicion.value && f.crearUsuario,
    esquema: { ...f.esquema, centroBaseSucursalId: f.esquema.centroBaseSucursalId ?? f.sucursalId }
  }
}
async function enfocarError() {
  await nextTick()
  document.querySelector('.formulario .entrada.error, .formulario .con-error')?.scrollIntoView({ behavior: 'smooth', block: 'center' })
}
async function guardar() {
  errorGeneral.value = ''
  errores.value = validar()
  if (Object.keys(errores.value).length) { ui.avisar('Revisa los campos marcados.', 'error'); enfocarError(); return }
  guardando.value = true
  try {
    if (esEdicion.value) {
      await api.put(`/colaboradores/${id.value}`, cuerpo())
      ui.avisar('Cambios guardados')
      router.push(`/colaboradores/${id.value}`)
    } else {
      const r = await api.post<{ id: number; numero: string; passwordTemporal: string | null }>('/colaboradores', cuerpo())
      ui.avisar(`Colaborador ${r.numero} agregado`)
      if (r.passwordTemporal) resultado.value = { id: r.id, numero: r.numero, passwordTemporal: r.passwordTemporal, email: f.email.trim().toLowerCase() }
      else router.push(`/colaboradores/${r.id}`)
    }
  } catch (e) {
    const porCampo = erroresPorCampo(e)
    errores.value = porCampo
    errorGeneral.value = e instanceof ErrorApi ? e.message : 'No se pudo guardar.'
    ui.error(e)
    if (Object.keys(porCampo).length) enfocarError()
  } finally { guardando.value = false }
}
function copiar(t: string) {
  navigator.clipboard?.writeText(t).then(() => ui.avisar('Contraseña copiada'), () => ui.avisar('No se pudo copiar; selecciónala y cópiala a mano.', 'info'))
}
function terminar() { if (resultado.value) router.push(`/colaboradores/${resultado.value.id}`) }
function cancelar() { router.push(esEdicion.value ? `/colaboradores/${id.value}` : '/colaboradores') }

const err = (k: string) => errores.value[k]
</script>

<template>
  <div class="formulario">
    <nav class="migas">
      <RouterLink to="/colaboradores">Colaboradores</RouterLink><span>/</span>
      <template v-if="esEdicion"><RouterLink :to="`/colaboradores/${id}`">{{ nombreActual || '…' }}</RouterLink><span>/</span><span>Editar</span></template>
      <span v-else>Agregar colaborador</span>
    </nav>
    <div class="encabezado">
      <div>
        <h1 class="titulo-pagina">{{ esEdicion ? 'Editar colaborador' : 'Agregar colaborador' }}</h1>
        <p class="subtitulo-pagina">{{ esEdicion ? nombreActual : 'Registra sus datos, su puesto y cómo registrará su asistencia.' }}</p>
      </div>
    </div>

    <div v-if="cargandoFicha" class="vacio"><span class="spinner" style="display: inline-block" /></div>
    <form v-else class="secciones" novalidate @submit.prevent="guardar">
      <!-- Datos personales -->
      <section class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><span class="ico-sec"><Icono n="persona" :t="22" /></span><h2 class="titulo-tarjeta">Datos personales</h2></div></div>
        <div class="rejilla-form">
          <div class="campo">
            <label for="c-nombres">Nombre(s) <span class="req">*</span></label>
            <input id="c-nombres" v-model="f.nombres" class="entrada" :class="{ error: err('nombres') }" autocomplete="off" @input="limpiarError('nombres')" />
            <span v-if="err('nombres')" class="error-campo">{{ err('nombres') }}</span>
          </div>
          <div class="campo">
            <label for="c-apellidos">Apellidos <span class="req">*</span></label>
            <input id="c-apellidos" v-model="f.apellidos" class="entrada" :class="{ error: err('apellidos') }" autocomplete="off" @input="limpiarError('apellidos')" />
            <span v-if="err('apellidos')" class="error-campo">{{ err('apellidos') }}</span>
          </div>
          <div class="campo">
            <label for="c-email">Correo electrónico</label>
            <input id="c-email" v-model="f.email" type="email" class="entrada" :class="{ error: err('email') }" placeholder="nombre@empresa.mx" autocomplete="off" @input="limpiarError('email')" />
            <span v-if="err('email')" class="error-campo">{{ err('email') }}</span>
          </div>
          <div class="campo">
            <label for="c-tel">Teléfono</label>
            <input id="c-tel" v-model="f.telefono" type="tel" class="entrada" :class="{ error: err('telefono') }" placeholder="10 dígitos" @input="limpiarError('telefono')" />
            <span v-if="err('telefono')" class="error-campo">{{ err('telefono') }}</span>
          </div>
          <div class="campo">
            <label for="c-nac">Fecha de nacimiento</label>
            <input id="c-nac" v-model="f.fechaNacimiento" type="date" class="entrada" :class="{ error: err('fechaNacimiento') }" :max="iso(new Date())" @input="limpiarError('fechaNacimiento')" />
            <span v-if="err('fechaNacimiento')" class="error-campo">{{ err('fechaNacimiento') }}</span>
          </div>
          <div class="campo">
            <label for="c-genero">Género</label>
            <select id="c-genero" v-model="f.genero" class="entrada">
              <option value="">Sin especificar</option>
              <option v-for="g in GENEROS" :key="g" :value="g">{{ g }}</option>
              <option v-if="f.genero && !GENEROS.includes(f.genero)" :value="f.genero">{{ f.genero }}</option>
            </select>
          </div>
          <div class="campo">
            <label for="c-curp">CURP</label>
            <input id="c-curp" v-model="f.curp" class="entrada mayus" :class="{ error: err('curp') }" maxlength="18" placeholder="18 caracteres" @input="limpiarError('curp')" />
            <span v-if="err('curp')" class="error-campo">{{ err('curp') }}</span>
          </div>
          <div class="campo">
            <label for="c-rfc">RFC</label>
            <input id="c-rfc" v-model="f.rfc" class="entrada mayus" :class="{ error: err('rfc') }" maxlength="13" placeholder="Con homoclave" @input="limpiarError('rfc')" />
            <span v-if="err('rfc')" class="error-campo">{{ err('rfc') }}</span>
          </div>
          <div class="campo">
            <label for="c-nss">NSS</label>
            <input id="c-nss" v-model="f.nss" class="entrada" :class="{ error: err('nss') }" inputmode="numeric" maxlength="11" placeholder="11 dígitos" @input="limpiarError('nss')" />
            <span v-if="err('nss')" class="error-campo">{{ err('nss') }}</span>
          </div>
          <div class="campo completo">
            <label for="c-dir">Dirección</label>
            <textarea id="c-dir" v-model="f.direccion" class="entrada" rows="2" placeholder="Calle, número, colonia, municipio y estado" />
          </div>
          <div class="campo">
            <label for="c-ce-nombre">Contacto de emergencia</label>
            <input id="c-ce-nombre" v-model="f.contactoEmergenciaNombre" class="entrada" placeholder="Nombre completo" />
          </div>
          <div class="campo">
            <label for="c-ce-tel">Teléfono de emergencia</label>
            <input id="c-ce-tel" v-model="f.contactoEmergenciaTelefono" type="tel" class="entrada" placeholder="10 dígitos" />
          </div>
        </div>
      </section>

      <!-- Datos laborales -->
      <section class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><span class="ico-sec"><Icono n="maletin" :t="22" /></span><h2 class="titulo-tarjeta">Datos laborales</h2></div></div>
        <div class="rejilla-form">
          <div class="campo">
            <label for="c-suc">Sucursal <span class="req">*</span></label>
            <select id="c-suc" v-model="f.sucursalId" class="entrada" :class="{ error: err('sucursalId') }" @change="limpiarError('sucursalId')">
              <option :value="null" disabled>Selecciona…</option>
              <option v-for="x in sucursales" :key="x.id" :value="x.id">{{ x.nombre }}</option>
            </select>
            <span v-if="err('sucursalId')" class="error-campo">{{ err('sucursalId') }}</span>
          </div>
          <div class="campo">
            <label for="c-ingreso">Fecha de ingreso <span class="req">*</span></label>
            <input id="c-ingreso" v-model="f.fechaIngreso" type="date" class="entrada" :class="{ error: err('fechaIngreso') }" @input="limpiarError('fechaIngreso')" />
            <span v-if="err('fechaIngreso')" class="error-campo">{{ err('fechaIngreso') }}</span>
          </div>
          <div class="campo">
            <label for="c-area">Área</label>
            <select id="c-area" v-model="f.areaId" class="entrada" :class="{ error: err('areaId') }" @change="limpiarError('areaId')">
              <option :value="null">Sin área</option>
              <option v-for="a in areas" :key="a.id" :value="a.id">{{ a.nombre }}</option>
            </select>
            <span v-if="err('areaId')" class="error-campo">{{ err('areaId') }}</span>
          </div>
          <div class="campo">
            <label for="c-equipo">Equipo</label>
            <select id="c-equipo" v-model="f.equipoId" class="entrada" :class="{ error: err('equipoId') }" :disabled="!f.areaId" @change="limpiarError('equipoId')">
              <option :value="null">{{ f.areaId ? (equiposDelArea.length ? 'Sin equipo' : 'El área no tiene equipos') : 'Elige primero el área' }}</option>
              <option v-for="e in equiposDelArea" :key="e.id" :value="e.id">{{ e.nombre }}</option>
            </select>
            <span v-if="err('equipoId')" class="error-campo">{{ err('equipoId') }}</span>
          </div>
          <div class="campo">
            <label for="c-puesto">Puesto</label>
            <select id="c-puesto" v-model="f.puestoId" class="entrada" :class="{ error: err('puestoId') }" @change="limpiarError('puestoId'); limpiarError('supervisorId')">
              <option :value="null">Sin puesto</option>
              <option v-for="p in puestos" :key="p.id" :value="p.id">{{ p.nombre }}</option>
            </select>
            <span v-if="err('puestoId')" class="error-campo">{{ err('puestoId') }}</span>
          </div>
          <div class="campo">
            <label for="c-turno">Turno</label>
            <select id="c-turno" v-model="f.turnoId" class="entrada" :class="{ error: err('turnoId') }" @change="limpiarError('turnoId')">
              <option :value="null">Sin turno</option>
              <option v-for="t in turnos" :key="t.id" :value="t.id">{{ t.nombre }} · {{ t.horaEntrada.slice(0, 5) }} a {{ t.horaSalida.slice(0, 5) }}</option>
            </select>
            <span v-if="err('turnoId')" class="error-campo">{{ err('turnoId') }}</span>
          </div>
          <div class="campo completo" :class="{ 'con-error': err('supervisorId') }">
            <label>Supervisor <span v-if="!esRaiz && !esEdicion" class="req">*</span></label>
            <SelectorColaborador v-model="f.supervisorId" :inicial="supervisorInicial" :sucursal-id="f.sucursalId" :excluir="id ? [id] : []"
                                 placeholder="Buscar supervisor por nombre o número…" @update:model-value="limpiarError('supervisorId')" />
            <span v-if="err('supervisorId')" class="error-campo">{{ err('supervisorId') }}</span>
            <span v-else class="ayuda">{{ esRaiz ? 'Este puesto es la raíz del organigrama; puede quedar sin supervisor.' : 'Es quien aprueba sus solicitudes y resuelve sus incidencias.' }}</span>
          </div>
          <div class="campo">
            <label for="c-disp">Dispositivo asignado</label>
            <select id="c-disp" v-model="f.dispositivoId" class="entrada" :class="{ error: err('dispositivoId') }" @change="limpiarError('dispositivoId')">
              <option :value="null">Sin dispositivo</option>
              <option v-for="d in dispositivos" :key="d.id" :value="d.id">{{ d.nombre }}</option>
            </select>
            <span v-if="err('dispositivoId')" class="error-campo">{{ err('dispositivoId') }}</span>
          </div>
          <div class="campo">
            <label for="c-pol">Política de vacaciones</label>
            <select id="c-pol" v-model="f.politicaVacacionesId" class="entrada" :class="{ error: err('politicaVacacionesId') }" @change="limpiarError('politicaVacacionesId')">
              <option :value="null">Por defecto{{ politicaDefecto ? ` (${politicaDefecto})` : '' }}</option>
              <option v-for="p in politicas" :key="p.id" :value="p.id">{{ p.nombre }}</option>
            </select>
            <span v-if="err('politicaVacacionesId')" class="error-campo">{{ err('politicaVacacionesId') }}</span>
          </div>
          <div class="campo">
            <label for="c-pin">PIN del quiosco</label>
            <input id="c-pin" v-model="f.pin" class="entrada" :class="{ error: err('pin') }" type="password" inputmode="numeric" maxlength="8" autocomplete="new-password"
                   :placeholder="tienePin ? '••••  (sin cambios)' : '4 a 8 dígitos'" @input="limpiarError('pin')" />
            <span v-if="err('pin')" class="error-campo">{{ err('pin') }}</span>
            <span v-else class="ayuda">{{ tienePin ? 'Ya tiene PIN. Escribe uno nuevo solo si quieres cambiarlo.' : 'Lo usa para registrar asistencia en el quiosco.' }}</span>
          </div>
        </div>
      </section>

      <!-- Esquema de trabajo -->
      <section class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><span class="ico-sec"><Icono n="edificioDoble" :t="22" /></span><h2 class="titulo-tarjeta">Esquema de trabajo</h2></div></div>
        <div class="columna" style="gap: 20px">
          <div class="campo">
            <span class="etiqueta-campo">Modalidad</span>
            <div class="opciones" role="radiogroup" aria-label="Modalidad">
              <button v-for="m in MODALIDADES" :key="m.v" type="button" class="opcion" :class="{ sel: f.esquema.modalidad === m.v }" role="radio"
                      :aria-checked="f.esquema.modalidad === m.v" @click="elegirModalidad(m.v)">
                <Icono :n="m.icono" :t="20" />{{ m.t }}
              </button>
            </div>
          </div>
          <div class="rejilla-form">
            <div class="campo">
              <label for="c-centro">Centro base</label>
              <select id="c-centro" v-model="f.esquema.centroBaseSucursalId" class="entrada">
                <option v-for="x in sucursales" :key="x.id" :value="x.id">{{ x.nombre }}</option>
              </select>
            </div>
            <div class="campo">
              <label for="c-reg">Registro permitido</label>
              <select id="c-reg" v-model="f.esquema.registroPermitido" class="entrada">
                <option v-for="r in REGISTROS" :key="r.v" :value="r.v">{{ r.t }}</option>
              </select>
            </div>
            <div class="campo" :class="{ 'con-error': err('diasOficina') }">
              <span class="etiqueta-campo">Días en oficina</span>
              <div class="dias" :class="{ deshabilitado: !oficinaHabilitada }">
                <button v-for="d in DIAS_SEMANA" :key="d.v" type="button" class="dia" :class="{ sel: f.esquema.diasOficina.includes(d.v) }" :title="d.t"
                        :aria-pressed="f.esquema.diasOficina.includes(d.v)" :aria-label="`${d.t} en oficina`" :disabled="!oficinaHabilitada" @click="alternarDia('diasOficina', d.v)">{{ d.l }}</button>
              </div>
              <span v-if="err('diasOficina')" class="error-campo">{{ err('diasOficina') }}</span>
            </div>
            <div class="campo" :class="{ 'con-error': err('diasRemotos') }">
              <span class="etiqueta-campo">Días remotos</span>
              <div class="dias remoto" :class="{ deshabilitado: !remotosHabilitados }">
                <button v-for="d in DIAS_SEMANA" :key="d.v" type="button" class="dia" :class="{ sel: f.esquema.diasRemotos.includes(d.v) }" :title="d.t"
                        :aria-pressed="f.esquema.diasRemotos.includes(d.v)" :aria-label="`${d.t} remoto`" :disabled="!remotosHabilitados" @click="alternarDia('diasRemotos', d.v)">{{ d.l }}</button>
              </div>
              <span v-if="err('diasRemotos')" class="error-campo">{{ err('diasRemotos') }}</span>
              <span v-else-if="!remotosHabilitados" class="ayuda">La modalidad presencial no tiene días remotos.</span>
            </div>
          </div>
          <label class="casilla"><input v-model="f.esquema.requiereFoto" type="checkbox" />Requiere foto de evidencia al registrar asistencia</label>
        </div>
      </section>

      <!-- Acceso -->
      <section v-if="!esEdicion" class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><span class="ico-sec"><Icono n="llave" :t="22" /></span><h2 class="titulo-tarjeta">Acceso al sistema</h2></div></div>
        <label class="casilla"><input v-model="f.crearUsuario" type="checkbox" @change="limpiarError('email')" />Crear usuario de acceso</label>
        <p class="ayuda" style="margin-top: 8px">Se crea con el rol Colaborador y una contraseña temporal que deberá cambiar al entrar. Necesita el correo electrónico.</p>
      </section>

      <div v-if="errorGeneral && !Object.keys(errores).length" class="caja-alerta"><Icono n="alertaCirculo" :t="22" class="rojo-i" /><span>{{ errorGeneral }}</span></div>

      <div class="pie">
        <button type="button" class="btn btn-contorno" @click="cancelar">Cancelar</button>
        <button type="submit" class="btn btn-primario" :disabled="guardando" data-prueba="guardar">
          <span v-if="guardando" class="spinner chico" /><Icono v-else n="check" :t="19" />{{ esEdicion ? 'Guardar cambios' : 'Agregar colaborador' }}
        </button>
      </div>
    </form>

    <Modal v-if="resultado" titulo="Colaborador agregado" :subtitulo="`${resultado.numero} · ${f.nombres} ${f.apellidos}`" :ancho="480" @cerrar="terminar">
      <p>Comparte estos datos de acceso. Deberá cambiar la contraseña la primera vez que entre.</p>
      <div class="cred">
        <span>Usuario</span><b>{{ resultado.email }}</b>
        <span>Contraseña temporal</span><b class="mono">{{ resultado.passwordTemporal }}</b>
      </div>
      <p class="caja-aviso" style="margin-top: 14px"><Icono n="candado" :t="18" />Por seguridad, la contraseña no se volverá a mostrar.</p>
      <template #pie>
        <button class="btn btn-contorno" @click="copiar(resultado.passwordTemporal)"><Icono n="copiar" :t="18" />Copiar contraseña</button>
        <button class="btn btn-primario" @click="terminar">Ir a la ficha</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.formulario { display: flex; flex-direction: column; gap: 20px; max-width: 1080px; }
.migas { display: flex; gap: 10px; font-size: 14.5px; color: var(--texto-2); flex-wrap: wrap; }
.secciones { display: flex; flex-direction: column; gap: 20px; }
.ico-sec { width: 40px; height: 40px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-texto); display: grid; place-items: center; }
.req { color: var(--rojo); }
.mayus { text-transform: uppercase; }
.opciones { display: flex; gap: 10px; flex-wrap: wrap; }
.opcion { display: inline-flex; align-items: center; gap: 8px; height: 44px; padding: 0 18px; border-radius: var(--radio-s); border: 1px solid var(--gris-300); background: #fff; color: var(--titulo); font-size: 15px; font-weight: 500; }
.opcion:hover { border-color: var(--gris-400); }
.opcion.sel { border-color: var(--verde); background: var(--verde-suave); color: var(--verde-texto); font-weight: 650; box-shadow: 0 0 0 1px var(--verde) inset; }
.dias { display: flex; gap: 6px; flex-wrap: wrap; }
.dia { width: 42px; height: 42px; border-radius: 50%; border: 1px solid var(--gris-300); background: #fff; color: var(--titulo); font-weight: 600; font-size: 15px; }
.dia:hover:not(:disabled) { border-color: var(--verde); }
.dia.sel { background: var(--verde-osc); border-color: var(--verde-osc); color: #fff; }
.remoto .dia.sel { background: var(--azul); border-color: var(--azul); }
.dias.deshabilitado .dia { opacity: .45; cursor: not-allowed; }
.con-error .dias .dia:not(.sel) { border-color: var(--rojo-borde); }
.pie { display: flex; justify-content: flex-end; gap: 12px; position: sticky; bottom: 0; padding: 14px 0; background: linear-gradient(180deg, rgba(248, 249, 252, 0) 0%, var(--fondo) 35%); z-index: 5; }
.rojo-i { color: var(--rojo); }
.spinner.chico { width: 16px; height: 16px; border-width: 2px; border-color: rgba(255, 255, 255, .4); border-top-color: #fff; }
.cred { display: grid; grid-template-columns: auto 1fr; gap: 8px 16px; margin-top: 14px; background: var(--gris-50); padding: 14px; border-radius: 10px; font-size: 15px; }
.cred span { color: var(--texto-3); }
.mono { font-family: ui-monospace, Consolas, monospace; font-size: 17px; letter-spacing: .03em; }
@media (max-width: 760px) {
  .pie .btn { flex: 1; }
  .opcion { flex: 1; justify-content: center; padding: 0 10px; }
  .dia { width: 38px; height: 38px; }
}
</style>
