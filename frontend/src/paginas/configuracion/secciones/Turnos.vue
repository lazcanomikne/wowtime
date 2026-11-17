<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Estado from '@/componentes/Estado.vue'
import { useCarga, useAccion } from '@/utils/usos'
import { hora } from '@/utils/formato'
import MenuAcciones from './MenuAcciones.vue'
import { aHoraApi, confirmarYEjecutar, DIAS_SEMANA, useEnvio } from './comun'

interface Turno { id: number; nombre: string; horaEntrada: string; horaSalida: string; toleranciaMinutos: number; diasDescanso: number[]; activo: boolean }

const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const q = ref('')
const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Turno>>('/turnos', { tamano: 500 }))
const filas = computed(() => (datos.value?.items ?? []).filter(x => !q.value || x.nombre.toLowerCase().includes(q.value.toLowerCase())))

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ nombre: '', horaEntrada: '08:00', horaSalida: '17:00', toleranciaMinutos: 10, diasDescanso: [6, 7] as number[], activo: true })
const intento = ref(false)
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!f.nombre.trim()) e.nombre = 'Escribe el nombre del turno.'
  if (!f.horaEntrada) e.horaEntrada = 'Indica la hora de entrada.'
  if (!f.horaSalida) e.horaSalida = 'Indica la hora de salida.'
  if (f.horaEntrada && f.horaEntrada === f.horaSalida) e.horaSalida = 'La salida debe ser distinta de la entrada.'
  if (!(Number(f.toleranciaMinutos) >= 0 && Number(f.toleranciaMinutos) <= 120)) e.tolerancia = 'Entre 0 y 120 minutos.'
  if (f.diasDescanso.length >= 7) e.dias = 'El turno debe tener al menos un día laborable.'
  return e
})

function abrir(x?: Turno) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x
    ? { nombre: x.nombre, horaEntrada: hora(x.horaEntrada), horaSalida: hora(x.horaSalida), toleranciaMinutos: x.toleranciaMinutos, diasDescanso: [...x.diasDescanso], activo: x.activo }
    : { nombre: '', horaEntrada: '08:00', horaSalida: '17:00', toleranciaMinutos: 10, diasDescanso: [6, 7], activo: true })
  abierto.value = true
}
function alternarDia(n: number) {
  f.diasDescanso = f.diasDescanso.includes(n) ? f.diasDescanso.filter(d => d !== n) : [...f.diasDescanso, n].sort()
}
const cuerpo = (x: { nombre: string; horaEntrada: string; horaSalida: string; toleranciaMinutos: number; diasDescanso: number[]; activo: boolean }) =>
  ({ nombre: x.nombre.trim(), horaEntrada: aHoraApi(x.horaEntrada), horaSalida: aHoraApi(x.horaSalida), toleranciaMinutos: Number(x.toleranciaMinutos), diasDescanso: x.diasDescanso, activo: x.activo })
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  const r = await enviar(() => editId.value ? api.put(`/turnos/${editId.value}`, cuerpo(f)) : api.post('/turnos', cuerpo(f)), editId.value ? 'Turno actualizado' : 'Turno creado')
  if (r.ok) { abierto.value = false; cargar() }
}
async function alternar(x: Turno) {
  const ok = await ejecutar(async () => { await api.put(`/turnos/${x.id}`, cuerpo({ ...x, activo: !x.activo })); return true }, x.activo ? 'Turno desactivado' : 'Turno activado')
  if (ok) cargar()
}
async function eliminar(x: Turno) {
  if (await confirmarYEjecutar({ titulo: `¿Eliminar ${x.nombre}?`, mensaje: 'Solo se pueden eliminar turnos que no estén asignados a colaboradores.' }, () => api.del(`/turnos/${x.id}`), 'Turno eliminado')) cargar()
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Turnos</h2>
        <p class="subtitulo-tarjeta">La tolerancia define hasta qué minuto una entrada cuenta como "En horario".</p>
      </div>
      <button class="btn btn-primario" data-prueba="nuevo-turno" @click="abrir()"><Icono n="mas" :t="20" />Agregar turno</button>
    </div>
    <div class="barra-filtros">
      <div class="con-icono buscar"><Icono n="buscar" :t="18" /><input v-model="q" class="entrada" placeholder="Buscar turno…" aria-label="Buscar turno" /></div>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Turno</th><th>Horario</th><th>Tolerancia</th><th>Días laborables</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="6"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in filas" :key="x.id" class="clic" @click="abrir(x)">
            <td class="negrita">{{ x.nombre }}</td>
            <td><span class="fila horario"><Icono n="reloj" :t="17" />{{ hora(x.horaEntrada) }} – {{ hora(x.horaSalida) }}</span></td>
            <td>{{ x.toleranciaMinutos }} min</td>
            <td>
              <span class="dias">
                <span v-for="d in DIAS_SEMANA" :key="d.n" class="dia" :class="{ descanso: x.diasDescanso.includes(d.n) }" :title="`${d.nombre}: ${x.diasDescanso.includes(d.n) ? 'descanso' : 'laborable'}`">{{ d.c }}</span>
              </span>
            </td>
            <td><Estado :v="x.activo ? 'Activo' : 'Inactivo'" /></td>
            <td class="acciones" @click.stop>
              <MenuAcciones :opciones="[
                { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                { texto: x.activo ? 'Desactivar' : 'Activar', icono: x.activo ? 'x' : 'check', accion: () => alternar(x) },
                { texto: 'Eliminar', icono: 'basura', peligro: true, accion: () => eliminar(x) },
              ]" />
            </td>
          </tr>
          <tr v-if="datos && !filas.length"><td colspan="6" class="vacio">No hay turnos que coincidan.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar turno' : 'Agregar turno'" :ancho="580" @cerrar="abierto = false">
    <form class="rejilla-form" novalidate @submit.prevent="guardar">
      <div class="campo completo">
        <label for="tu-nombre">Nombre</label>
        <input id="tu-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errores.nombre }" placeholder="Turno matutino" />
        <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
      </div>
      <div class="campo">
        <label for="tu-ent">Hora de entrada</label>
        <input id="tu-ent" v-model="f.horaEntrada" type="time" class="entrada" :class="{ error: intento && errores.horaEntrada }" />
        <span v-if="intento && errores.horaEntrada" class="error-campo">{{ errores.horaEntrada }}</span>
      </div>
      <div class="campo">
        <label for="tu-sal">Hora de salida</label>
        <input id="tu-sal" v-model="f.horaSalida" type="time" class="entrada" :class="{ error: intento && errores.horaSalida }" />
        <span v-if="intento && errores.horaSalida" class="error-campo">{{ errores.horaSalida }}</span>
      </div>
      <div class="campo">
        <label for="tu-tol">Tolerancia (minutos)</label>
        <input id="tu-tol" v-model.number="f.toleranciaMinutos" type="number" min="0" max="120" class="entrada" :class="{ error: intento && errores.tolerancia }" />
        <span v-if="intento && errores.tolerancia" class="error-campo">{{ errores.tolerancia }}</span>
      </div>
      <div class="campo completo">
        <span class="etiqueta-campo">Días de descanso</span>
        <div class="dias-sel" role="group" aria-label="Días de descanso">
          <button v-for="d in DIAS_SEMANA" :key="d.n" type="button" class="dia-btn" :class="{ sel: f.diasDescanso.includes(d.n) }" :aria-pressed="f.diasDescanso.includes(d.n)" :title="d.nombre" @click="alternarDia(d.n)">{{ d.c }}</button>
        </div>
        <span v-if="intento && errores.dias" class="error-campo">{{ errores.dias }}</span>
        <span v-else class="ayuda">Toca un día para marcarlo como descanso (gris). Los días en verde son laborables; los de descanso no generan faltas.</span>
      </div>
      <label class="casilla completo"><input v-model="f.activo" type="checkbox" />Turno activo</label>
      <p v-if="error" class="caja-alerta err completo" role="alert"><Icono n="alertaCirculo" :t="20" />{{ error }}</p>
      <button type="submit" class="oculto" />
    </form>
    <template #pie>
      <button class="btn btn-contorno" @click="abierto = false">Cancelar</button>
      <button class="btn btn-primario" :disabled="ocupado" data-prueba="guardar" @click="guardar">Guardar</button>
    </template>
  </Modal>
</template>

<style scoped>
.acciones { width: 50px; text-align: right; }
.horario { gap: 6px; white-space: nowrap; }
.horario .icono { color: var(--texto-3); }
.dias { display: inline-flex; gap: 4px; }
.dia { width: 26px; height: 26px; border-radius: 6px; display: grid; place-items: center; font-size: 12.5px; font-weight: 700; background: var(--verde-claro); color: var(--verde-texto); }
.dia.descanso { background: var(--gris-100); color: var(--gris-400); }
.dias-sel { display: flex; gap: 8px; flex-wrap: wrap; }
.dia-btn { width: 44px; height: 44px; border-radius: 10px; border: 1px solid var(--gris-300); background: #fff; font-weight: 700; color: var(--texto-2); }
.dia-btn.sel { background: var(--gris-100); border-color: var(--gris-400); color: var(--titulo); box-shadow: inset 0 0 0 1px var(--gris-400); }
.dia-btn:not(.sel) { border-color: var(--verde-borde); color: var(--verde-texto); background: var(--verde-suave); }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
</style>
