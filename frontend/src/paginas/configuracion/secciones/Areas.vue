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
import { confirmarYEjecutar, useEnvio } from './comun'

interface Area { id: number; nombre: string; sucursalId: number; activa: boolean; colaboradores: number }

const s = useSesion()
const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const q = ref('')
const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Area>>('/areas', { sucursalId: s.sucursalId, tamano: 500 }), { porSucursal: true })
const filas = computed(() => (datos.value?.items ?? []).filter(x => !q.value || x.nombre.toLowerCase().includes(q.value.toLowerCase())))

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ nombre: '', sucursalId: 0, activa: true })
const intento = ref(false)
const errNombre = computed(() => (f.nombre.trim() ? '' : 'Escribe el nombre del área.'))

function abrir(x?: Area) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x ? { nombre: x.nombre, sucursalId: x.sucursalId, activa: x.activa } : { nombre: '', sucursalId: s.sucursalId ?? s.me?.sucursales[0]?.id ?? 0, activa: true })
  abierto.value = true
}
async function guardar() {
  intento.value = true
  if (errNombre.value || !f.sucursalId) return
  const c = { nombre: f.nombre.trim(), sucursalId: f.sucursalId, activa: f.activa }
  const r = await enviar(() => editId.value ? api.put(`/areas/${editId.value}`, c) : api.post('/areas', c), editId.value ? 'Área actualizada' : 'Área creada')
  if (r.ok) { abierto.value = false; cargar() }
}
async function alternar(x: Area) {
  const ok = await ejecutar(async () => { await api.put(`/areas/${x.id}`, { nombre: x.nombre, sucursalId: x.sucursalId, activa: !x.activa }); return true }, x.activa ? 'Área desactivada' : 'Área activada')
  if (ok) cargar()
}
async function eliminar(x: Area) {
  if (await confirmarYEjecutar({ titulo: `¿Eliminar el área ${x.nombre}?`, mensaje: 'Solo se pueden eliminar áreas sin colaboradores.' }, () => api.del(`/areas/${x.id}`), 'Área eliminada')) cargar()
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Áreas de {{ s.sucursal?.nombre }}</h2>
        <p class="subtitulo-tarjeta">{{ datos?.total ?? 0 }} áreas</p>
      </div>
      <button class="btn btn-primario" data-prueba="nueva-area" @click="abrir()"><Icono n="mas" :t="20" />Agregar área</button>
    </div>
    <div class="barra-filtros">
      <div class="con-icono buscar"><Icono n="buscar" :t="18" /><input v-model="q" class="entrada" placeholder="Buscar área…" aria-label="Buscar área" /></div>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Área</th><th class="num">Colaboradores</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="4"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in filas" :key="x.id" class="clic" @click="abrir(x)">
            <td class="negrita">{{ x.nombre }}</td>
            <td class="num">{{ num(x.colaboradores) }}</td>
            <td><Estado :v="x.activa ? 'Activa' : 'Inactiva'" /></td>
            <td class="acciones" @click.stop>
              <MenuAcciones :opciones="[
                { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                { texto: x.activa ? 'Desactivar' : 'Activar', icono: x.activa ? 'x' : 'check', accion: () => alternar(x) },
                { texto: 'Eliminar', icono: 'basura', peligro: true, accion: () => eliminar(x) },
              ]" />
            </td>
          </tr>
          <tr v-if="datos && !filas.length"><td colspan="4" class="vacio">No hay áreas registradas en esta sucursal.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar área' : 'Agregar área'" :ancho="520" @cerrar="abierto = false">
    <form class="columna" novalidate @submit.prevent="guardar">
      <div class="campo">
        <label for="area-nombre">Nombre</label>
        <input id="area-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errNombre }" placeholder="Operaciones" />
        <span v-if="intento && errNombre" class="error-campo">{{ errNombre }}</span>
      </div>
      <div class="campo">
        <label for="area-suc">Sucursal</label>
        <select id="area-suc" v-model.number="f.sucursalId" class="entrada">
          <option v-for="x in s.me?.sucursales" :key="x.id" :value="x.id">{{ x.nombre }}</option>
        </select>
      </div>
      <label class="casilla"><input v-model="f.activa" type="checkbox" />Área activa</label>
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
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
</style>
