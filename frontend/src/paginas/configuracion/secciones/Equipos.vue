<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Estado from '@/componentes/Estado.vue'
import { useSesion } from '@/stores/sesion'
import { useCarga, useAccion } from '@/utils/usos'
import { num } from '@/utils/formato'
import MenuAcciones from './MenuAcciones.vue'
import { confirmarYEjecutar, todos, useEnvio } from './comun'

interface Equipo { id: number; nombre: string; areaId: number; area: string; sucursalId: number; coberturaMinima: number; activo: boolean; miembros: number }
interface Area { id: number; nombre: string; activa: boolean }

const s = useSesion()
const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const q = ref('')
const areaFiltro = ref<number | ''>('')
const coberturaDefecto = ref(70)
const { datos, cargando, cargar } = useCarga(async () => {
  const [eq, ar] = await Promise.all([
    api.get<Pagina<Equipo>>('/equipos', { sucursalId: s.sucursalId, tamano: 500 }),
    todos<Area>('/areas', { sucursalId: s.sucursalId }),
  ])
  return { equipos: eq.items, areas: ar }
}, { porSucursal: true })
api.get<{ clave: string; valor: string }[]>('/parametros').then(ps => {
  const v = Number(ps.find(p => p.clave === 'vacaciones.cobertura_minima_defecto')?.valor)
  if (v >= 0 && v <= 100) coberturaDefecto.value = v
}).catch(() => { /* valor por omisión */ })

const filas = computed(() => (datos.value?.equipos ?? []).filter(x =>
  (!q.value || x.nombre.toLowerCase().includes(q.value.toLowerCase())) && (areaFiltro.value === '' || x.areaId === areaFiltro.value)))

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ nombre: '', areaId: 0, coberturaMinima: 70, activo: true })
const intento = ref(false)
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!f.nombre.trim()) e.nombre = 'Escribe el nombre del equipo.'
  if (!f.areaId) e.areaId = 'Elige el área.'
  if (!(Number(f.coberturaMinima) >= 0 && Number(f.coberturaMinima) <= 100)) e.cobertura = 'Debe estar entre 0 y 100%.'
  return e
})

function abrir(x?: Equipo) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x
    ? { nombre: x.nombre, areaId: x.areaId, coberturaMinima: x.coberturaMinima, activo: x.activo }
    : { nombre: '', areaId: areaFiltro.value || datos.value?.areas.find(a => a.activa)?.id || 0, coberturaMinima: coberturaDefecto.value, activo: true })
  abierto.value = true
}
const cuerpo = () => ({ nombre: f.nombre.trim(), areaId: f.areaId, coberturaMinima: Number(f.coberturaMinima), activo: f.activo })
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  const r = await enviar(() => editId.value ? api.put(`/equipos/${editId.value}`, cuerpo()) : api.post('/equipos', cuerpo()), editId.value ? 'Equipo actualizado' : 'Equipo creado')
  if (r.ok) { abierto.value = false; cargar() }
}
async function alternar(x: Equipo) {
  const ok = await ejecutar(async () => { await api.put(`/equipos/${x.id}`, { nombre: x.nombre, areaId: x.areaId, coberturaMinima: x.coberturaMinima, activo: !x.activo }); return true }, x.activo ? 'Equipo desactivado' : 'Equipo activado')
  if (ok) cargar()
}
async function eliminar(x: Equipo) {
  if (await confirmarYEjecutar({ titulo: `¿Eliminar ${x.nombre}?`, mensaje: 'Solo se pueden eliminar equipos sin miembros.' }, () => api.del(`/equipos/${x.id}`), 'Equipo eliminado')) cargar()
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Equipos de {{ s.sucursal?.nombre }}</h2>
        <p class="subtitulo-tarjeta">La cobertura mínima dispara la alerta de "Cobertura baja" al aprobar vacaciones.</p>
      </div>
      <button class="btn btn-primario" data-prueba="nuevo-equipo" @click="abrir()"><Icono n="mas" :t="20" />Agregar equipo</button>
    </div>
    <div class="barra-filtros">
      <div class="con-icono buscar"><Icono n="buscar" :t="18" /><input v-model="q" class="entrada" placeholder="Buscar equipo…" aria-label="Buscar equipo" /></div>
      <select v-model="areaFiltro" class="entrada" aria-label="Filtrar por área">
        <option value="">Todas las áreas</option>
        <option v-for="a in datos?.areas" :key="a.id" :value="a.id">{{ a.nombre }}</option>
      </select>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Equipo</th><th>Área</th><th class="num">Miembros</th><th>Cobertura mínima</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="6"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in filas" :key="x.id" class="clic" @click="abrir(x)">
            <td class="negrita">{{ x.nombre }}</td>
            <td>{{ x.area }}</td>
            <td class="num">{{ num(x.miembros) }}</td>
            <td><div class="cob"><div class="barra verde crecer"><span :style="{ width: x.coberturaMinima + '%' }" /></div><span class="pct">{{ x.coberturaMinima }}%</span></div></td>
            <td><Estado :v="x.activo ? 'Activo' : 'Inactivo'" /></td>
            <td class="acciones" @click.stop>
              <MenuAcciones :opciones="[
                { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                { texto: x.activo ? 'Desactivar' : 'Activar', icono: x.activo ? 'x' : 'check', accion: () => alternar(x) },
                { texto: 'Eliminar', icono: 'basura', peligro: true, accion: () => eliminar(x) },
              ]" />
            </td>
          </tr>
          <tr v-if="datos && !filas.length"><td colspan="6" class="vacio">No hay equipos que coincidan.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar equipo' : 'Agregar equipo'" :ancho="540" @cerrar="abierto = false">
    <form class="columna" novalidate @submit.prevent="guardar">
      <div class="campo">
        <label for="eq-nombre">Nombre</label>
        <input id="eq-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errores.nombre }" placeholder="Equipo Norte" />
        <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
      </div>
      <div class="campo">
        <label for="eq-area">Área</label>
        <select id="eq-area" v-model.number="f.areaId" class="entrada" :class="{ error: intento && errores.areaId }">
          <option :value="0" disabled>Elige un área</option>
          <option v-for="a in datos?.areas" :key="a.id" :value="a.id">{{ a.nombre }}{{ a.activa ? '' : ' (inactiva)' }}</option>
        </select>
        <span v-if="intento && errores.areaId" class="error-campo">{{ errores.areaId }}</span>
      </div>
      <div class="campo">
        <label for="eq-cob">Cobertura mínima (%)</label>
        <div class="fila">
          <input v-model.number="f.coberturaMinima" type="range" min="0" max="100" step="5" class="crecer rango" aria-label="Cobertura mínima" />
          <input id="eq-cob" v-model.number="f.coberturaMinima" type="number" min="0" max="100" class="entrada corto" :class="{ error: intento && errores.cobertura }" />
        </div>
        <span v-if="intento && errores.cobertura" class="error-campo">{{ errores.cobertura }}</span>
        <span v-else class="ayuda">Porcentaje de miembros programados que deben estar presentes cada día.</span>
      </div>
      <label class="casilla"><input v-model="f.activo" type="checkbox" />Equipo activo</label>
      <p v-if="error" class="caja-alerta err" role="alert"><Icono n="alertaCirculo" :t="20" />{{ error }}</p>
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
.cob { display: flex; align-items: center; gap: 10px; min-width: 150px; }
.pct { font-variant-numeric: tabular-nums; font-weight: 600; width: 42px; }
.rango { accent-color: var(--verde); }
.corto { width: 96px; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
</style>
