<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Estado from '@/componentes/Estado.vue'
import { useCarga, useAccion } from '@/utils/usos'
import MenuAcciones from './MenuAcciones.vue'
import { confirmarYEjecutar, todos, useEnvio } from './comun'

interface Puesto { id: number; nombre: string; nivel: number; activo: boolean; documentosRequeridos: number[] }
interface TipoDoc { id: number; nombre: string; descripcion?: string | null; obligatorio: boolean; activo: boolean }

const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const q = ref('')
const { datos, cargando, cargar } = useCarga(async () => {
  const [p, t] = await Promise.all([api.get<Pagina<Puesto>>('/puestos', { tamano: 500 }), todos<TipoDoc>('/tipos-documento')])
  return { puestos: p.items, tipos: t }
})
const filas = computed(() => (datos.value?.puestos ?? []).filter(x => !q.value || x.nombre.toLowerCase().includes(q.value.toLowerCase())))
const tiposActivos = computed(() => (datos.value?.tipos ?? []).filter(t => t.activo))
const nombreDoc = (id: number) => datos.value?.tipos.find(t => t.id === id)?.nombre ?? `#${id}`

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ nombre: '', nivel: 3, activo: true, documentosRequeridos: [] as number[] })
const intento = ref(false)
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!f.nombre.trim()) e.nombre = 'Escribe el nombre del puesto.'
  if (!(Number.isInteger(Number(f.nivel)) && Number(f.nivel) >= 1 && Number(f.nivel) <= 20)) e.nivel = 'El nivel va de 1 a 20.'
  return e
})

function abrir(x?: Puesto) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x
    ? { nombre: x.nombre, nivel: x.nivel, activo: x.activo, documentosRequeridos: [...x.documentosRequeridos] }
    : { nombre: '', nivel: 3, activo: true, documentosRequeridos: tiposActivos.value.filter(t => t.obligatorio).map(t => t.id) })
  abierto.value = true
}
function todosDocs(sel: boolean) { f.documentosRequeridos = sel ? tiposActivos.value.map(t => t.id) : [] }
const cuerpo = (x: { nombre: string; nivel: number; activo: boolean; documentosRequeridos: number[] }) => ({ nombre: x.nombre.trim(), nivel: Number(x.nivel), activo: x.activo, documentosRequeridos: x.documentosRequeridos })
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  const r = await enviar(() => editId.value ? api.put(`/puestos/${editId.value}`, cuerpo(f)) : api.post('/puestos', cuerpo(f)), editId.value ? 'Puesto actualizado' : 'Puesto creado')
  if (r.ok) { abierto.value = false; cargar() }
}
async function alternar(x: Puesto) {
  const ok = await ejecutar(async () => { await api.put(`/puestos/${x.id}`, cuerpo({ ...x, activo: !x.activo })); return true }, x.activo ? 'Puesto desactivado' : 'Puesto activado')
  if (ok) cargar()
}
async function eliminar(x: Puesto) {
  if (await confirmarYEjecutar({ titulo: `¿Eliminar el puesto ${x.nombre}?`, mensaje: 'Solo se pueden eliminar puestos que no estén asignados a colaboradores.' }, () => api.del(`/puestos/${x.id}`), 'Puesto eliminado')) cargar()
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Puestos</h2>
        <p class="subtitulo-tarjeta">El nivel 1 es la raíz del organigrama (Dirección general).</p>
      </div>
      <button class="btn btn-primario" data-prueba="nuevo-puesto" @click="abrir()"><Icono n="mas" :t="20" />Agregar puesto</button>
    </div>
    <div class="barra-filtros">
      <div class="con-icono buscar"><Icono n="buscar" :t="18" /><input v-model="q" class="entrada" placeholder="Buscar puesto…" aria-label="Buscar puesto" /></div>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Puesto</th><th class="num">Nivel</th><th>Documentos requeridos</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="5"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in filas" :key="x.id" class="clic" @click="abrir(x)">
            <td class="negrita">{{ x.nombre }}</td>
            <td class="num"><span class="etiqueta gris cuadrada">Nivel {{ x.nivel }}</span></td>
            <td>
              <span class="negrita">{{ x.documentosRequeridos.length }}</span> <span class="muted">de {{ tiposActivos.length }}</span>
              <div class="muted pequeno docs">{{ x.documentosRequeridos.map(nombreDoc).join(', ') || 'Ninguno' }}</div>
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
          <tr v-if="datos && !filas.length"><td colspan="5" class="vacio">No hay puestos que coincidan.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar puesto' : 'Agregar puesto'" :ancho="620" @cerrar="abierto = false">
    <form class="rejilla-form" novalidate @submit.prevent="guardar">
      <div class="campo">
        <label for="pu-nombre">Nombre</label>
        <input id="pu-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errores.nombre }" placeholder="Supervisor de Área" />
        <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
      </div>
      <div class="campo">
        <label for="pu-nivel">Nivel</label>
        <input id="pu-nivel" v-model.number="f.nivel" type="number" min="1" max="20" class="entrada" :class="{ error: intento && errores.nivel }" />
        <span v-if="intento && errores.nivel" class="error-campo">{{ errores.nivel }}</span>
        <span v-else class="ayuda">1 = Dirección general; números mayores, niveles más bajos.</span>
      </div>
      <div class="campo completo">
        <div class="fila-entre">
          <span class="etiqueta-campo">Documentos requeridos</span>
          <span class="fila">
            <button type="button" class="ver-todo" @click="todosDocs(true)">Todos</button>
            <button type="button" class="ver-todo" @click="todosDocs(false)">Ninguno</button>
          </span>
        </div>
        <div class="docs-sel">
          <label v-for="t in tiposActivos" :key="t.id" class="casilla doc">
            <input v-model="f.documentosRequeridos" type="checkbox" :value="t.id" />
            <span><span class="negrita">{{ t.nombre }}</span><small v-if="t.descripcion" class="muted"> · {{ t.descripcion }}</small></span>
          </label>
          <p v-if="!tiposActivos.length" class="muted">No hay tipos de documento activos.</p>
        </div>
      </div>
      <label class="casilla completo"><input v-model="f.activo" type="checkbox" />Puesto activo</label>
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
.docs { max-width: 420px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.docs-sel { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px 16px; padding: 12px 14px; border: 1px solid var(--gris-200); border-radius: var(--radio-s); background: var(--gris-50); }
.doc { align-items: flex-start; }
.doc input { margin-top: 2px; flex-shrink: 0; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
@media (max-width: 600px) { .docs-sel { grid-template-columns: 1fr; } }
</style>
