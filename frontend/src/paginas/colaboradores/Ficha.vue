<script setup lang="ts">
// Ficha de colaborador (Ficha_colaborador.png): encabezado, 7 pestañas y tarjetas de la pestaña Resumen.
import { computed, defineAsyncComponent, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion } from '@/utils/usos'
import { fechaMedia, hora, momento, rango, iso, primerNombre } from '@/utils/formato'

const DatosPersonales = defineAsyncComponent(() => import('./ficha/DatosPersonales.vue'))
const DatosLaborales = defineAsyncComponent(() => import('./ficha/DatosLaborales.vue'))
const AsistenciaTab = defineAsyncComponent(() => import('./ficha/AsistenciaTab.vue'))
const VacacionesTab = defineAsyncComponent(() => import('./ficha/VacacionesTab.vue'))
const ExpedienteTab = defineAsyncComponent(() => import('./ficha/ExpedienteTab.vue'))
const Historial = defineAsyncComponent(() => import('./ficha/Historial.vue'))

const route = useRoute()
const router = useRouter()
const s = useSesion()
const ui = useUi()
const { ejecutar, ocupado } = useAccion()
const id = computed(() => Number(route.params.id))
const f = ref<any>(null)
const actividad = ref<any[]>([])
const notas = ref<any[]>([])
const masAcciones = ref(false)
const modal = ref<'' | 'baja' | 'supervisor' | 'nota' | 'acceso'>('')
const notaEdit = ref<{ id?: number; texto: string }>({ texto: '' })
const menuNota = ref<number | null>(null)
const baja = ref({ fecha: iso(new Date()), motivo: '' })
const nuevoSup = ref<number | null>(null)
const acceso = ref<{ email: string; passwordTemporal: string } | null>(null)

const PESTANAS = [
  { k: 'resumen', t: 'Resumen' }, { k: 'personales', t: 'Datos personales' }, { k: 'laborales', t: 'Datos laborales' },
  { k: 'asistencia', t: 'Asistencia' }, { k: 'vacaciones', t: 'Vacaciones' }, { k: 'expediente', t: 'Expediente' }, { k: 'historial', t: 'Historial' }
]
const tab = computed(() => (route.query.tab as string) || 'resumen')
const ir = (k: string, extra: Record<string, string> = {}) => router.replace({ query: { tab: k === 'resumen' ? undefined : k, ...extra } })

async function cargar() {
  f.value = await api.get(`/colaboradores/${id.value}`)
  actividad.value = (await api.get(`/colaboradores/${id.value}/actividad`, { tamano: 5 })).items
  if (f.value.puedeVerNotas) notas.value = (await api.get(`/colaboradores/${id.value}/notas`, { tamano: 10 })).items
}
onMounted(() => { cargar().catch(e => { ui.error(e); router.replace('/colaboradores') }); document.addEventListener('mousedown', cerrar) })
onBeforeUnmount(() => document.removeEventListener('mousedown', cerrar))
watch(id, () => cargar())
function cerrar(e: MouseEvent) {
  const t = e.target as HTMLElement
  if (!t.closest('.mas')) masAcciones.value = false
  if (!t.closest('.menu-nota')) menuNota.value = null
}

const cumple = computed(() => f.value?.cumpleanos?.mostrarBanner ? f.value.cumpleanos : null)
const iconoEvento = (t: string) => ({ documento: ['documento', '#0aaa4a'], asistencia: ['reloj', '#0aaa4a'], datos: ['lapiz', '#2f74d6'], solicitud: ['vacaciones', '#0aaa4a'], asignacion: ['asignacion', '#0c9b78'], seguridad: ['candado', '#7b8598'], vacaciones: ['palmera', '#0aaa4a'], visitante: ['visitantes', '#2f74d6'], delegacion: ['llave', '#c27a12'], ausencia: ['alerta', '#e5484d'] } as Record<string, [string, string]>)[t] ?? ['info', '#7b8598']

async function guardarNota() {
  if (!notaEdit.value.texto.trim()) return
  await ejecutar(async () => {
    if (notaEdit.value.id) await api.put(`/notas/${notaEdit.value.id}`, { texto: notaEdit.value.texto })
    else await api.post(`/colaboradores/${id.value}/notas`, { texto: notaEdit.value.texto })
    modal.value = ''
    notas.value = (await api.get(`/colaboradores/${id.value}/notas`, { tamano: 10 })).items
  }, 'Nota guardada')
}
async function borrarNota(n: any) {
  menuNota.value = null
  if (!(await ui.confirmar({ titulo: 'Eliminar nota', mensaje: '¿Eliminar esta nota? No se puede deshacer.', aceptar: 'Eliminar', peligro: true })).ok) return
  await ejecutar(async () => { await api.del(`/notas/${n.id}`); notas.value = notas.value.filter(x => x.id !== n.id) }, 'Nota eliminada')
}
async function darBaja() {
  await ejecutar(async () => { await api.post(`/colaboradores/${id.value}/baja`, baja.value); modal.value = ''; await cargar() }, 'Colaborador dado de baja')
}
async function reactivar() {
  masAcciones.value = false
  if (!(await ui.confirmar({ titulo: 'Reactivar colaborador', mensaje: 'Se reactivará su acceso al sistema.', aceptar: 'Reactivar' })).ok) return
  await ejecutar(async () => { await api.post(`/colaboradores/${id.value}/reactivar`); await cargar() }, 'Colaborador reactivado')
}
async function cambiarEstatus(estatus: string) {
  masAcciones.value = false
  await ejecutar(async () => { await api.post(`/colaboradores/${id.value}/estatus`, { estatus }); await cargar() }, estatus === 'Activo' ? 'Colaborador activo' : 'Colaborador marcado como inactivo')
}
async function guardarSupervisor() {
  await ejecutar(async () => { await api.put(`/colaboradores/${id.value}/supervisor`, { supervisorId: nuevoSup.value }); modal.value = ''; await cargar() }, 'Supervisor actualizado')
}
async function restablecerAcceso() {
  masAcciones.value = false
  const msg = f.value.usuario ? `Se generará una contraseña temporal para ${f.value.usuario.email}.` : 'Se creará su usuario de acceso con una contraseña temporal.'
  if (!(await ui.confirmar({ titulo: 'Restablecer acceso', mensaje: msg, aceptar: 'Continuar' })).ok) return
  const r = await ejecutar(() => api.post(`/colaboradores/${id.value}/acceso`))
  if (r) { acceso.value = r; modal.value = 'acceso'; await cargar() }
}
async function exportar() { masAcciones.value = false; await ejecutar(() => api.descargar(`/colaboradores/${id.value}/exportar`), 'Ficha exportada') }
function copiar(t: string) { navigator.clipboard?.writeText(t); ui.avisar('Copiado al portapapeles') }
</script>

<template>
  <div v-if="f" class="ficha">
    <nav class="migas"><RouterLink :to="s.esGestion ? '/colaboradores' : '/mi-equipo'">{{ s.esGestion ? 'Colaboradores' : 'Mi equipo' }}</RouterLink><span>/</span><span>{{ f.nombre }}</span></nav>

    <header class="cabecera">
      <Avatar :nombre="f.nombre" :iniciales="f.iniciales" :foto="f.fotoUrl" :t="160" class="foto" />
      <div class="datos">
        <h1 class="nombre">{{ f.nombre }}</h1>
        <p class="puesto">{{ f.puesto }}</p>
        <div class="fila envolver" style="gap: 12px; margin-top: 14px">
          <span class="numero">{{ f.numero }}</span>
          <Estado :v="f.estatus" :texto="f.estatus === 'Activo' ? 'Activa' : undefined" class="estatus" />
        </div>
        <div class="chips">
          <span class="chip"><Icono n="ubicacion" :t="18" />{{ f.laborales.sucursal }}</span>
          <span v-if="f.laborales.turno" class="chip"><Icono n="sol" :t="18" class="sol" />{{ f.laborales.turno.nombre }}</span>
          <span class="chip"><Icono n="edificioDoble" :t="18" />{{ f.esquema.modalidadTexto }}</span>
          <span class="chip"><Icono n="celular" :t="18" />{{ f.esquema.registroTexto }}</span>
        </div>
      </div>
      <div class="lado">
        <div class="fila" style="justify-content: flex-end">
          <RouterLink v-if="f.puedeEditar" :to="`/colaboradores/${f.id}/editar`" class="btn btn-primario editar"><Icono n="lapiz" :t="19" />Editar colaborador</RouterLink>
          <div v-if="f.puedeEditar" class="mas">
            <button class="btn btn-contorno editar" @click="masAcciones = !masAcciones">Más acciones <Icono n="abajo" :t="18" /></button>
            <div v-if="masAcciones" class="menu">
              <button v-if="f.estatus !== 'Baja'" @click="masAcciones = false; modal = 'supervisor'; nuevoSup = f.laborales.supervisorId"><Icono n="organigrama" :t="18" />Cambiar supervisor</button>
              <button @click="restablecerAcceso"><Icono n="llave" :t="18" />Restablecer acceso</button>
              <button @click="exportar"><Icono n="descargar" :t="18" />Exportar ficha</button>
              <button v-if="f.estatus === 'Activo'" @click="cambiarEstatus('Inactivo')"><Icono n="reloArena" :t="18" />Marcar como inactivo</button>
              <button v-if="f.estatus === 'Inactivo'" @click="cambiarEstatus('Activo')"><Icono n="checkCirculo" :t="18" />Marcar como activo</button>
              <button v-if="f.estatus === 'Baja'" @click="reactivar"><Icono n="actualizar" :t="18" />Reactivar</button>
              <button v-else class="peligro" @click="masAcciones = false; modal = 'baja'"><Icono n="salir" :t="18" />Dar de baja</button>
            </div>
          </div>
        </div>
        <div v-if="cumple" class="cumple">
          <Icono n="pastel" :t="34" class="pastel" />
          <div><b>Cumpleaños: {{ fechaMedia(cumple.fecha) }}</b><span>¡Felicidades, {{ primerNombre(f.nombres) }}!</span></div>
          <span class="confeti" aria-hidden="true"><i /><i /><i /><i /><i /></span>
        </div>
      </div>
    </header>

    <nav class="pestanas">
      <button v-for="p in PESTANAS" :key="p.k" class="pestana" :class="{ activa: tab === p.k }" @click="ir(p.k)">{{ p.t }}</button>
    </nav>

    <!-- Resumen -->
    <div v-if="tab === 'resumen'" class="resumen">
      <section class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><Icono n="edificioDoble" :t="26" /><h2 class="titulo-tarjeta">Esquema de trabajo</h2></div><button class="ver-todo" @click="ir('laborales')">Ver detalle <Icono n="flecha" :t="16" /></button></div>
        <dl class="esquema">
          <div><dt><Icono n="monitor" :t="20" />Modalidad</dt><dd><span class="etiqueta verde cuadrada">{{ f.esquema.modalidadTexto }}</span></dd></div>
          <div><dt><Icono n="ubicacion" :t="20" />Centro base</dt><dd>{{ f.esquema.centroBase }}</dd></div>
          <div><dt><Icono n="edificioDoble" :t="20" />Días en oficina</dt><dd>{{ f.esquema.diasOficinaTexto }}</dd></div>
          <div><dt><Icono n="casa" :t="20" />Días remotos</dt><dd>{{ f.esquema.diasRemotosTexto }}</dd></div>
          <div><dt><Icono n="celular" :t="20" />Registro permitido</dt><dd>{{ f.esquema.registroTexto }}</dd></div>
        </dl>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><Icono n="asignacion" :t="26" /><h2 class="titulo-tarjeta">Asignación temporal</h2></div><button class="ver-todo" @click="ir('laborales', { asignaciones: '1' })">Ver detalle <Icono n="flecha" :t="16" /></button></div>
        <template v-if="f.asignacion">
          <div class="asig">
            <div class="fila">
              <span class="asig-ico"><Icono :n="f.asignacion.tipo === 'HomeOffice' ? 'casa' : 'edificio'" :t="30" /></span>
              <div class="crecer"><b>{{ f.asignacion.tipoTexto }}</b><span>{{ rango(f.asignacion.fechaInicio, f.asignacion.fechaFin) }}</span></div>
              <Estado :v="f.asignacion.estado" />
            </div>
            <div class="motivo"><Icono n="notas" :t="20" /><span class="muted">Motivo</span><span>{{ f.asignacion.motivo }}</span></div>
          </div>
        </template>
        <p v-else class="vacio" style="padding: 22px 0">Sin asignaciones temporales vigentes o programadas.</p>
        <button class="btn btn-verde-contorno btn-bloque" style="margin-top: 12px" @click="ir('laborales', { asignaciones: '1' })">Ver asignaciones</button>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Asistencia de hoy</h2><button class="ver-todo" @click="ir('asistencia')">Ver detalle <Icono n="flecha" :t="16" /></button></div>
        <div class="hoy">
          <span class="hoy-ico"><Icono n="salir" :t="30" style="transform: scaleX(-1)" /></span>
          <div><span class="muted">Entrada</span><b class="hora-g">{{ hora(f.asistenciaHoy.entrada) || '—' }}</b></div>
          <div class="sep" />
          <div><span class="muted">Estado actual</span><Estado :v="f.asistenciaHoy.estadoClave" :texto="f.asistenciaHoy.estado" class="est-g" /></div>
        </div>
        <div class="separador" />
        <div class="fila disp"><Icono n="terminal" :t="26" /><div><span class="muted">Dispositivo asignado</span><b>{{ f.asistenciaHoy.dispositivo ?? 'Sin dispositivo' }}</b></div></div>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Vacaciones</h2><button class="ver-todo" @click="ir('vacaciones')">Ver detalle <Icono n="flecha" :t="16" /></button></div>
        <div class="fila" style="gap: 16px">
          <span class="vac-ico"><Icono n="sombrilla" :t="38" :g="1.7" /></span>
          <div><b class="dias-g">{{ f.vacaciones.disponibles }} {{ f.vacaciones.disponibles == 1 ? 'día' : 'días' }}</b><span class="disp-t">disponibles</span></div>
        </div>
        <RouterLink v-if="f.vacaciones.proxima" :to="`/solicitudes/${f.vacaciones.proxima.id}`" class="proxima">
          <Icono n="calendario" :t="24" />
          <div class="crecer"><span>Próxima solicitud</span><b>{{ rango(f.vacaciones.proxima.fechaInicio, f.vacaciones.proxima.fechaFin) }}</b></div>
          <Icono n="derecha" :t="20" />
        </RouterLink>
        <p v-else class="proxima vacia">Sin solicitudes próximas</p>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Expediente digital</h2><button class="ver-todo" @click="ir('expediente')">Ver detalle <Icono n="flecha" :t="16" /></button></div>
        <div class="fila" style="gap: 18px">
          <span class="exp-ico"><Icono n="documento" :t="34" :g="1.7" /></span>
          <div class="crecer">
            <p class="docs"><b>{{ f.expediente.entregados }} de {{ f.expediente.requeridos }}</b> documentos</p>
            <div class="fila" style="margin-top: 12px"><div class="barra crecer" style="height: 12px"><span :style="{ width: f.expediente.porcentaje + '%' }" /></div><span class="muted">{{ f.expediente.porcentaje }}%</span></div>
          </div>
        </div>
        <button v-if="f.expediente.alertas.length" class="alerta-doc" @click="ir('expediente', { documento: String(f.expediente.alertas[0].id) })">
          <span class="alerta-ico">!</span>
          <span class="crecer"><b>{{ f.expediente.alertas[0].tipo }} {{ f.expediente.alertas[0].dias < 0 ? `venció hace ${-f.expediente.alertas[0].dias} días` : `vence en ${f.expediente.alertas[0].dias} días` }}</b><span>Renueva el documento para evitar inconvenientes.</span></span>
          <Icono n="derecha" :t="20" />
        </button>
      </section>

      <section class="tarjeta actividad">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Actividad reciente</h2><button class="ver-todo" @click="ir('historial')">Ver todo <Icono n="flecha" :t="16" /></button></div>
        <ol class="linea">
          <li v-for="e in actividad" :key="e.id">
            <span class="punto" :style="{ background: iconoEvento(e.tipo)[1] }"><Icono :n="iconoEvento(e.tipo)[0]" :t="17" /></span>
            <div><span class="momento">{{ momento(e.fecha) }}</span><b>{{ e.titulo }}</b><span class="muted">{{ e.detalle }}</span></div>
          </li>
          <li v-if="!actividad.length" class="vacio">Sin actividad registrada.</li>
        </ol>
      </section>

      <section v-if="f.puedeVerNotas" class="tarjeta notas">
        <div class="tarjeta-cabecera">
          <div class="izq"><Icono n="notas" :t="28" /><div><h2 class="titulo-tarjeta">Notas</h2><p class="subtitulo-tarjeta">Información relevante sobre el colaborador.</p></div></div>
          <button class="btn btn-contorno" @click="notaEdit = { texto: '' }; modal = 'nota'"><Icono n="masCirculo" :t="20" class="verde-i" />Agregar nota</button>
        </div>
        <div v-for="n in notas" :key="n.id" class="nota">
          <div class="fila-entre">
            <span class="muted pequeno">{{ momento(n.creadoEn).replace('Hoy, ', '') }} · {{ n.autorRol ?? n.autor }}</span>
            <div class="menu-nota">
              <button class="btn-icono" aria-label="Opciones de la nota" @click="menuNota = menuNota === n.id ? null : n.id"><Icono n="puntosH" :t="20" /></button>
              <div v-if="menuNota === n.id" class="menu derecha">
                <button @click="menuNota = null; notaEdit = { id: n.id, texto: n.texto }; modal = 'nota'"><Icono n="lapiz" :t="17" />Editar</button>
                <button class="peligro" @click="borrarNota(n)"><Icono n="basura" :t="17" />Eliminar</button>
              </div>
            </div>
          </div>
          <p>{{ n.texto }}</p>
        </div>
        <p v-if="!notas.length" class="vacio">Sin notas.</p>
      </section>
    </div>

    <DatosPersonales v-else-if="tab === 'personales'" :ficha="f" :id="f.id" @actualizar="cargar" />
    <DatosLaborales v-else-if="tab === 'laborales'" :ficha="f" :id="f.id" @actualizar="cargar" />
    <AsistenciaTab v-else-if="tab === 'asistencia'" :ficha="f" :id="f.id" @actualizar="cargar" />
    <VacacionesTab v-else-if="tab === 'vacaciones'" :ficha="f" :id="f.id" @actualizar="cargar" />
    <ExpedienteTab v-else-if="tab === 'expediente'" :ficha="f" :id="f.id" @actualizar="cargar" />
    <Historial v-else-if="tab === 'historial'" :ficha="f" :id="f.id" @actualizar="cargar" />

    <!-- Diálogos -->
    <Modal v-if="modal === 'nota'" :titulo="notaEdit.id ? 'Editar nota' : 'Agregar nota'" subtitulo="Solo Recursos Humanos puede ver las notas." @cerrar="modal = ''">
      <textarea v-model="notaEdit.texto" class="entrada" rows="5" placeholder="Escribe la nota…" />
      <template #pie><button class="btn btn-contorno" @click="modal = ''">Cancelar</button><button class="btn btn-primario" :disabled="ocupado || !notaEdit.texto.trim()" @click="guardarNota">Guardar nota</button></template>
    </Modal>
    <Modal v-if="modal === 'baja'" titulo="Dar de baja" :subtitulo="f.nombre" @cerrar="modal = ''">
      <div class="columna">
        <p class="caja-aviso"><Icono n="alerta" :t="20" />Se desactivará su acceso y se cancelarán sus solicitudes pendientes.</p>
        <div class="campo"><label>Fecha de baja</label><input v-model="baja.fecha" type="date" class="entrada" /></div>
        <div class="campo"><label>Motivo</label><textarea v-model="baja.motivo" class="entrada" rows="3" placeholder="Renuncia voluntaria, término de contrato…" /></div>
      </div>
      <template #pie><button class="btn btn-contorno" @click="modal = ''">Cancelar</button><button class="btn btn-peligro" :disabled="ocupado || !baja.motivo.trim()" @click="darBaja">Dar de baja</button></template>
    </Modal>
    <Modal v-if="modal === 'supervisor'" titulo="Cambiar supervisor" :subtitulo="f.nombre" @cerrar="modal = ''">
      <div class="campo"><label>Nuevo supervisor</label><SelectorColaborador v-model="nuevoSup" :inicial="f.laborales.supervisor" :excluir="[f.id]" /></div>
      <p class="ayuda" style="margin-top: 10px">No se puede asignar a un subordinado: el sistema impide ciclos en el organigrama.</p>
      <template #pie><button class="btn btn-contorno" @click="modal = ''">Cancelar</button><button class="btn btn-primario" :disabled="ocupado" @click="guardarSupervisor">Guardar</button></template>
    </Modal>
    <Modal v-if="modal === 'acceso' && acceso" titulo="Acceso restablecido" @cerrar="modal = ''">
      <p>Comparte estos datos con {{ f.nombre }}. Deberá cambiar la contraseña al entrar.</p>
      <div class="cred"><span>Usuario</span><b>{{ acceso.email }}</b><span>Contraseña temporal</span><b class="mono">{{ acceso.passwordTemporal }}</b></div>
      <template #pie><button class="btn btn-contorno" @click="copiar(acceso.passwordTemporal)"><Icono n="copiar" :t="18" />Copiar contraseña</button><button class="btn btn-primario" @click="modal = ''">Listo</button></template>
    </Modal>
  </div>
  <div v-else class="vacio"><span class="spinner" style="display:inline-block" /></div>
</template>

<style scoped>
.ficha { display: flex; flex-direction: column; gap: 22px; }
.migas { display: flex; gap: 10px; font-size: 14.5px; color: var(--texto-2); }
.cabecera { display: grid; grid-template-columns: auto 1fr auto; gap: 26px; align-items: start; }
.nombre { font-size: 38px; font-weight: 800; letter-spacing: -0.025em; line-height: 1.1; margin-top: 4px; }
.puesto { font-size: 21px; color: var(--titulo); margin-top: 2px; }
.numero { display: inline-flex; align-items: center; height: 34px; padding: 0 16px; border: 1px solid var(--gris-300); border-radius: 8px; background: var(--gris-50); font-size: 15px; color: var(--texto-2); text-decoration: underline; text-underline-offset: 3px; }
.estatus { height: 34px; padding: 0 16px; font-size: 15px; }
.chips { display: flex; gap: 12px; flex-wrap: wrap; margin-top: 12px; }
.chip .sol { color: #f5b301; }
.lado { display: flex; flex-direction: column; gap: 22px; align-items: flex-end; }
.editar { height: 44px; }
.mas { position: relative; }
.menu { position: absolute; right: 0; top: calc(100% + 6px); background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; box-shadow: var(--sombra-m); padding: 6px; min-width: 230px; z-index: 30; }
.menu.derecha { right: 0; }
.menu button { display: flex; gap: 10px; align-items: center; width: 100%; padding: 10px 12px; border: 0; background: none; border-radius: 8px; text-align: left; font-size: 14.5px; color: var(--titulo); }
.menu button:hover { background: var(--gris-100); }
.menu button.peligro { color: var(--rojo-texto); }
.cumple { display: flex; align-items: center; gap: 16px; background: #e4f8f4; border-radius: 12px; padding: 16px 70px 16px 22px; position: relative; min-width: 350px; }
.cumple b { display: block; color: #0a7a5a; font-size: 16px; }
.cumple span { color: #0b5d48; font-size: 15px; }
.pastel { color: #0c9b78; }
.confeti i { position: absolute; width: 4px; height: 10px; border-radius: 2px; }
.confeti i:nth-child(1) { right: 26px; top: 14px; background: #f5b301; transform: rotate(30deg); }
.confeti i:nth-child(2) { right: 16px; top: 22px; background: #e5484d; transform: rotate(-20deg); }
.confeti i:nth-child(3) { right: 36px; top: 40px; background: #2f74d6; transform: rotate(60deg); }
.confeti i:nth-child(4) { right: 20px; top: 50px; background: #0aaa4a; transform: rotate(-50deg); }
.confeti i:nth-child(5) { right: 42px; top: 20px; background: #e5484d; transform: rotate(80deg); width: 3px; height: 7px; }
.resumen { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 20px; align-items: start; }
.resumen .actividad { grid-row: span 2; }
.resumen .notas { grid-column: span 2; }
.tarjeta-cabecera .icono { color: var(--titulo); }
.ver-todo .icono { color: var(--azul); }
.esquema { margin: 0; display: flex; flex-direction: column; gap: 14px; }
.esquema div { display: grid; grid-template-columns: 150px 1fr; align-items: center; }
.esquema dt { display: flex; align-items: center; gap: 12px; color: var(--texto-2); font-size: 14.5px; }
.esquema dd { margin: 0; color: var(--titulo); font-size: 15px; }
.asig { background: #f0faf7; border-radius: 10px; padding: 14px; }
.asig-ico { width: 54px; height: 54px; border-radius: 50%; background: #d8f3ea; color: #0c8e6c; display: grid; place-items: center; }
.asig b { display: block; font-size: 17px; color: var(--titulo); }
.asig .crecer span { font-size: 15px; }
.motivo { display: flex; gap: 12px; align-items: center; border-top: 1px solid #d8ece5; margin-top: 14px; padding-top: 12px; font-size: 14.5px; }
.hoy { display: flex; align-items: center; gap: 18px; }
.hoy > div { display: flex; flex-direction: column; gap: 4px; }
.hoy-ico { width: 54px; height: 54px; border-radius: 50%; background: #0c9b78; color: #fff; display: grid; place-items: center; }
.hora-g { font-size: 34px; font-weight: 800; color: var(--titulo); letter-spacing: -0.02em; }
.sep { width: 1px; align-self: stretch; background: var(--gris-200); }
.est-g { height: 36px; padding: 0 16px; font-size: 15px; }
.disp { gap: 16px; }
.disp div { display: flex; flex-direction: column; font-size: 14.5px; }
.vac-ico, .exp-ico { width: 72px; height: 72px; border-radius: 50%; background: #e0f6ee; color: #0c8e6c; display: grid; place-items: center; flex-shrink: 0; }
.dias-g { display: block; font-size: 28px; font-weight: 800; color: var(--titulo); }
.disp-t { font-size: 18px; color: var(--texto-2); }
.proxima { display: flex; align-items: center; gap: 14px; margin-top: 16px; background: var(--gris-50); border-radius: 10px; padding: 12px 16px; color: var(--titulo); text-decoration: none !important; }
.proxima span { display: block; font-size: 14.5px; }
.proxima b { font-size: 16px; font-weight: 500; }
.proxima.vacia { color: var(--texto-3); font-size: 14.5px; }
.docs { font-size: 16px; }
.docs b { font-weight: 650; }
.alerta-doc { display: flex; gap: 14px; align-items: center; width: 100%; margin-top: 18px; padding: 14px 16px; border: 0; border-radius: 10px; background: var(--rojo-claro); text-align: left; color: var(--rojo-texto); }
.alerta-doc b { display: block; color: #c4161c; font-size: 15.5px; }
.alerta-doc span span { color: #7a3a3c; font-size: 13.5px; }
.alerta-ico { width: 34px; height: 34px; border-radius: 50%; background: #e1262c; color: #fff; display: grid; place-items: center; font-weight: 800; flex-shrink: 0; }
.linea { list-style: none; padding: 0; margin: 0; position: relative; }
.linea li { display: flex; gap: 16px; padding-bottom: 18px; position: relative; }
.linea li:not(:last-child)::before { content: ''; position: absolute; left: 19px; top: 40px; bottom: 2px; width: 2px; background: var(--gris-200); }
.linea .punto { width: 40px; height: 40px; border-radius: 50%; color: #fff; display: grid; place-items: center; flex-shrink: 0; }
.linea div { display: flex; flex-direction: column; font-size: 14.5px; }
.momento { color: var(--texto-2); font-size: 14px; }
.linea b { font-weight: 550; color: var(--titulo); font-size: 15.5px; }
.nota { border: 1px solid var(--gris-200); border-radius: 10px; padding: 14px 16px; margin-top: 10px; }
.nota p { margin-top: 6px; font-size: 15px; }
.menu-nota { position: relative; }
.verde-i { color: var(--verde); }
.cred { display: grid; grid-template-columns: auto 1fr; gap: 8px 16px; margin-top: 14px; background: var(--gris-50); padding: 14px; border-radius: 10px; }
.mono { font-family: ui-monospace, Consolas, monospace; font-size: 16px; }
@media (max-width: 1250px) { .resumen { grid-template-columns: repeat(2, minmax(0, 1fr)); } .resumen .actividad { grid-row: auto; } .cabecera { grid-template-columns: auto 1fr; } .lado { grid-column: 1 / -1; align-items: stretch; } }
@media (max-width: 760px) {
  .cabecera { grid-template-columns: 1fr; justify-items: start; }
  .foto { width: 96px !important; height: 96px !important; }
  .nombre { font-size: 28px; }
  .resumen { grid-template-columns: 1fr; }
  .resumen .notas { grid-column: auto; }
  .cumple { min-width: 0; }
}
</style>
