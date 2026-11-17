<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import { useSesion } from '@/stores/sesion'
import { useCarga } from '@/utils/usos'
import { fechaLarga, aFecha } from '@/utils/formato'
import MenuAcciones from './MenuAcciones.vue'
import { confirmarYEjecutar, todos, useEnvio } from './comun'

interface Festivo { id: number; fecha: string; nombre: string; sucursalId: number | null }
interface Sucursal { id: number; nombre: string }

const s = useSesion()
const { ocupado, error, enviar } = useEnvio()
const anioActual = new Date().getFullYear()
const anio = ref(anioActual)
const anios = computed(() => Array.from({ length: 5 }, (_, i) => anioActual - 2 + i))
const sucFiltro = ref<number | ''>('')
const sucursales = ref<Sucursal[]>([])
todos<Sucursal>('/sucursales').then(r => (sucursales.value = r)).catch(() => (sucursales.value = s.me?.sucursales ?? []))
const nombreSuc = (id: number | null) => (id ? sucursales.value.find(x => x.id === id)?.nombre ?? `Sucursal ${id}` : 'Todas las sucursales')

const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Festivo>>('/dias-festivos', { anio: anio.value, sucursalId: sucFiltro.value || undefined, tamano: 500 }))
watch([anio, sucFiltro], () => cargar())
const pasado = (x: Festivo) => (aFecha(x.fecha)?.getTime() ?? 0) < new Date(new Date().toDateString()).getTime()

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ fecha: '', nombre: '', sucursalId: '' as number | '' })
const intento = ref(false)
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!f.nombre.trim()) e.nombre = 'Escribe el nombre del día festivo.'
  if (!f.fecha) e.fecha = 'Elige la fecha.'
  return e
})
function abrir(x?: Festivo) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x ? { fecha: x.fecha, nombre: x.nombre, sucursalId: x.sucursalId ?? '' } : { fecha: '', nombre: '', sucursalId: sucFiltro.value })
  abierto.value = true
}
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  const c = { fecha: f.fecha, nombre: f.nombre.trim(), sucursalId: f.sucursalId || null }
  const r = await enviar(() => editId.value ? api.put(`/dias-festivos/${editId.value}`, c) : api.post('/dias-festivos', c), editId.value ? 'Día festivo actualizado' : 'Día festivo agregado')
  if (r.ok) {
    abierto.value = false
    const a = Number(f.fecha.slice(0, 4))
    if (a !== anio.value && anios.value.includes(a)) anio.value = a; else cargar()
  }
}
async function eliminar(x: Festivo) {
  if (await confirmarYEjecutar({ titulo: `¿Eliminar "${x.nombre}"?`, mensaje: `${fechaLarga(x.fecha, true)} volverá a ser un día laborable.` }, () => api.del(`/dias-festivos/${x.id}`), 'Día festivo eliminado')) cargar()
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Días festivos {{ anio }}</h2>
        <p class="subtitulo-tarjeta">{{ datos?.total ?? 0 }} días en el calendario</p>
      </div>
      <button class="btn btn-primario" data-prueba="nuevo-festivo" @click="abrir()"><Icono n="mas" :t="20" />Agregar día festivo</button>
    </div>
    <div class="barra-filtros">
      <select v-model.number="anio" class="entrada" aria-label="Año">
        <option v-for="a in anios" :key="a" :value="a">{{ a }}</option>
      </select>
      <select v-model="sucFiltro" class="entrada" aria-label="Sucursal">
        <option value="">Todas las sucursales</option>
        <option v-for="x in sucursales" :key="x.id" :value="x.id">{{ x.nombre }}</option>
      </select>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Fecha</th><th>Nombre</th><th>Aplica a</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="4"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in datos?.items" :key="x.id" class="clic" :class="{ pasado: pasado(x) }" @click="abrir(x)">
            <td><span class="fila fecha"><span class="ico"><Icono n="calendario" :t="18" /></span>{{ fechaLarga(x.fecha, true) }}</span></td>
            <td class="negrita">{{ x.nombre }}</td>
            <td><span class="etiqueta" :class="x.sucursalId ? 'azul' : 'verde'"><span class="punto" />{{ nombreSuc(x.sucursalId) }}</span></td>
            <td class="acciones" @click.stop>
              <MenuAcciones :opciones="[
                { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                { texto: 'Eliminar', icono: 'basura', peligro: true, accion: () => eliminar(x) },
              ]" />
            </td>
          </tr>
          <tr v-if="datos && !datos.items.length"><td colspan="4" class="vacio">No hay días festivos registrados en {{ anio }}.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar día festivo' : 'Agregar día festivo'" :ancho="520" @cerrar="abierto = false">
    <form class="columna" novalidate @submit.prevent="guardar">
      <div class="campo">
        <label for="fe-nombre">Nombre</label>
        <input id="fe-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errores.nombre }" placeholder="Día de la Independencia" />
        <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
      </div>
      <div class="campo">
        <label for="fe-fecha">Fecha</label>
        <input id="fe-fecha" v-model="f.fecha" type="date" class="entrada" :class="{ error: intento && errores.fecha }" />
        <span v-if="intento && errores.fecha" class="error-campo">{{ errores.fecha }}</span>
      </div>
      <div class="campo">
        <label for="fe-suc">Aplica a</label>
        <select id="fe-suc" v-model="f.sucursalId" class="entrada">
          <option value="">Todas las sucursales</option>
          <option v-for="x in sucursales" :key="x.id" :value="x.id">Solo {{ x.nombre }}</option>
        </select>
      </div>
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
.fecha { gap: 10px; white-space: nowrap; }
.ico { width: 34px; height: 34px; border-radius: 50%; display: grid; place-items: center; background: var(--verde-claro); color: var(--verde-texto); flex-shrink: 0; }
.pasado td { color: var(--texto-3); }
.pasado .ico { background: var(--gris-100); color: var(--gris-500); }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
</style>
