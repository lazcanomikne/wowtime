<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Estado from '@/componentes/Estado.vue'
import { useUi } from '@/stores/ui'
import { useCarga, useAccion } from '@/utils/usos'
import { dias } from '@/utils/formato'
import MenuAcciones from './MenuAcciones.vue'
import { useEnvio } from './comun'

interface TipoPermiso { id: number; nombre: string; conGoce: boolean; maximoDias: number | null; activo: boolean }

const ui = useUi()
const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<TipoPermiso>>('/tipos-permiso', { tamano: 500 }))

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ nombre: '', conGoce: true, maximoDias: '' as number | '', activo: true })
const intento = ref(false)
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!f.nombre.trim()) e.nombre = 'Escribe el nombre del tipo de permiso.'
  if (f.maximoDias !== '' && !(Number.isInteger(Number(f.maximoDias)) && Number(f.maximoDias) >= 1 && Number(f.maximoDias) <= 365)) e.maximo = 'Escribe un número de 1 a 365, o déjalo vacío.'
  return e
})
function abrir(x?: TipoPermiso) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x ? { nombre: x.nombre, conGoce: x.conGoce, maximoDias: x.maximoDias ?? '', activo: x.activo } : { nombre: '', conGoce: true, maximoDias: '', activo: true })
  abierto.value = true
}
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  const c = { nombre: f.nombre.trim(), conGoce: f.conGoce, maximoDias: f.maximoDias === '' ? null : Number(f.maximoDias), activo: f.activo }
  const r = await enviar(() => editId.value ? api.put(`/tipos-permiso/${editId.value}`, c) : api.post('/tipos-permiso', c), editId.value ? 'Tipo de permiso actualizado' : 'Tipo de permiso creado')
  if (r.ok) { abierto.value = false; cargar() }
}
async function alternar(x: TipoPermiso) {
  if (x.activo) {
    const c = await ui.confirmar({ titulo: `¿Desactivar "${x.nombre}"?`, mensaje: 'Ya no se podrá elegir en nuevas solicitudes. Las solicitudes existentes no cambian.', aceptar: 'Desactivar', peligro: true })
    if (!c.ok) return
  }
  const ok = await ejecutar(async () => { await api.put(`/tipos-permiso/${x.id}`, { nombre: x.nombre, conGoce: x.conGoce, maximoDias: x.maximoDias, activo: !x.activo }); return true }, x.activo ? 'Tipo de permiso desactivado' : 'Tipo de permiso activado')
  if (ok) cargar()
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Tipos de permiso</h2>
        <p class="subtitulo-tarjeta">Los tipos inactivos no aparecen al solicitar un permiso.</p>
      </div>
      <button class="btn btn-primario" data-prueba="nuevo-tipo-permiso" @click="abrir()"><Icono n="mas" :t="20" />Agregar tipo</button>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Tipo de permiso</th><th>Goce de sueldo</th><th>Máximo por solicitud</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="5"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in datos?.items" :key="x.id" class="clic" @click="abrir(x)">
            <td class="negrita">{{ x.nombre }}</td>
            <td><span class="etiqueta" :class="x.conGoce ? 'verde' : 'ambar'"><span class="punto" />{{ x.conGoce ? 'Con goce' : 'Sin goce' }}</span></td>
            <td>{{ x.maximoDias ? dias(x.maximoDias) : 'Sin límite' }}</td>
            <td><Estado :v="x.activo ? 'Activo' : 'Inactivo'" /></td>
            <td class="acciones" @click.stop>
              <MenuAcciones :opciones="[
                { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                { texto: x.activo ? 'Desactivar' : 'Activar', icono: x.activo ? 'x' : 'check', peligro: x.activo, accion: () => alternar(x) },
              ]" />
            </td>
          </tr>
          <tr v-if="datos && !datos.items.length"><td colspan="5" class="vacio">No hay tipos de permiso.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar tipo de permiso' : 'Agregar tipo de permiso'" :ancho="520" @cerrar="abierto = false">
    <form class="columna" novalidate @submit.prevent="guardar">
      <div class="campo">
        <label for="tp-nombre">Nombre</label>
        <input id="tp-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errores.nombre }" placeholder="Permiso personal" />
        <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
      </div>
      <div class="campo">
        <label for="tp-max">Máximo de días por solicitud</label>
        <input id="tp-max" v-model.number="f.maximoDias" type="number" min="1" max="365" class="entrada" :class="{ error: intento && errores.maximo }" placeholder="Sin límite" />
        <span v-if="intento && errores.maximo" class="error-campo">{{ errores.maximo }}</span>
      </div>
      <label class="casilla"><input v-model="f.conGoce" type="checkbox" />Con goce de sueldo</label>
      <label class="casilla"><input v-model="f.activo" type="checkbox" />Activo</label>
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
