<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Estado from '@/componentes/Estado.vue'
import { useUi } from '@/stores/ui'
import { useCarga, useAccion } from '@/utils/usos'
import MenuAcciones from './MenuAcciones.vue'
import { todos, useEnvio } from './comun'

interface TipoDoc { id: number; nombre: string; descripcion: string | null; obligatorio: boolean; conVigencia: boolean; activo: boolean; puestos: number[] }
interface Puesto { id: number; nombre: string; nivel: number; activo: boolean }

const ui = useUi()
const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const { datos, cargando, cargar } = useCarga(async () => {
  const [t, p] = await Promise.all([api.get<Pagina<TipoDoc>>('/tipos-documento', { tamano: 500 }), todos<Puesto>('/puestos')])
  return { tipos: t.items, puestos: p }
})
const puestosActivos = computed(() => (datos.value?.puestos ?? []).filter(p => p.activo))

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ nombre: '', descripcion: '', obligatorio: true, conVigencia: false, activo: true, puestos: [] as number[] })
const intento = ref(false)
const errNombre = computed(() => (f.nombre.trim() ? '' : 'Escribe el nombre del documento.'))
function abrir(x?: TipoDoc) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x
    ? { nombre: x.nombre, descripcion: x.descripcion ?? '', obligatorio: x.obligatorio, conVigencia: x.conVigencia, activo: x.activo, puestos: [...x.puestos] }
    : { nombre: '', descripcion: '', obligatorio: true, conVigencia: false, activo: true, puestos: [] })
  abierto.value = true
}
const cuerpo = (x: { nombre: string; descripcion: string | null; obligatorio: boolean; conVigencia: boolean; activo: boolean; puestos: number[] | null }) =>
  ({ nombre: x.nombre.trim(), descripcion: x.descripcion?.trim() || null, obligatorio: x.obligatorio, conVigencia: x.conVigencia, activo: x.activo, puestos: x.puestos })
async function guardar() {
  intento.value = true
  if (errNombre.value) return
  const r = await enviar(() => editId.value ? api.put(`/tipos-documento/${editId.value}`, cuerpo(f)) : api.post('/tipos-documento', cuerpo(f)), editId.value ? 'Tipo de documento actualizado' : 'Tipo de documento creado')
  if (r.ok) { abierto.value = false; cargar() }
}
async function alternar(x: TipoDoc) {
  if (x.activo) {
    const c = await ui.confirmar({ titulo: `¿Desactivar "${x.nombre}"?`, mensaje: 'Dejará de pedirse en los expedientes. Los archivos ya entregados se conservan.', aceptar: 'Desactivar', peligro: true })
    if (!c.ok) return
  }
  const ok = await ejecutar(async () => { await api.put(`/tipos-documento/${x.id}`, cuerpo({ ...x, puestos: null, activo: !x.activo })); return true }, x.activo ? 'Tipo de documento desactivado' : 'Tipo de documento activado')
  if (ok) cargar()
}
function todosPuestos(sel: boolean) { f.puestos = sel ? puestosActivos.value.map(p => p.id) : [] }
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Tipos de documento</h2>
        <p class="subtitulo-tarjeta">Los documentos con vigencia generan alertas de "Urgente" y "Próximo" antes de vencer.</p>
      </div>
      <button class="btn btn-primario" data-prueba="nuevo-tipo-documento" @click="abrir()"><Icono n="mas" :t="20" />Agregar tipo</button>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Documento</th><th>Obligatorio</th><th>Vigencia</th><th class="num">Puestos que lo piden</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="6"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in datos?.tipos" :key="x.id" class="clic" @click="abrir(x)">
            <td>
              <div class="fila"><span class="ico"><Icono n="documento" :t="19" /></span>
                <div><div class="negrita">{{ x.nombre }}</div><div class="muted pequeno">{{ x.descripcion || 'Sin descripción' }}</div></div>
              </div>
            </td>
            <td>{{ x.obligatorio ? 'Sí' : 'No' }}</td>
            <td>{{ x.conVigencia ? 'Con vencimiento' : 'Sin vencimiento' }}</td>
            <td class="num">{{ x.puestos.length }}</td>
            <td><Estado :v="x.activo ? 'Activo' : 'Inactivo'" /></td>
            <td class="acciones" @click.stop>
              <MenuAcciones :opciones="[
                { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                { texto: x.activo ? 'Desactivar' : 'Activar', icono: x.activo ? 'x' : 'check', peligro: x.activo, accion: () => alternar(x) },
              ]" />
            </td>
          </tr>
          <tr v-if="datos && !datos.tipos.length"><td colspan="6" class="vacio">No hay tipos de documento.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar tipo de documento' : 'Agregar tipo de documento'" :ancho="620" @cerrar="abierto = false">
    <form class="rejilla-form" novalidate @submit.prevent="guardar">
      <div class="campo completo">
        <label for="td-nombre">Nombre</label>
        <input id="td-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errNombre }" placeholder="Constancia de situación fiscal" />
        <span v-if="intento && errNombre" class="error-campo">{{ errNombre }}</span>
      </div>
      <div class="campo completo">
        <label for="td-desc">Descripción</label>
        <input id="td-desc" v-model="f.descripcion" class="entrada" placeholder="No mayor a 3 meses" />
      </div>
      <label class="casilla"><input v-model="f.obligatorio" type="checkbox" />Obligatorio</label>
      <label class="casilla"><input v-model="f.conVigencia" type="checkbox" />Tiene fecha de vencimiento</label>
      <div class="campo completo">
        <div class="fila-entre">
          <span class="etiqueta-campo">Puestos que lo requieren</span>
          <span class="fila">
            <button type="button" class="ver-todo" @click="todosPuestos(true)">Todos</button>
            <button type="button" class="ver-todo" @click="todosPuestos(false)">Ninguno</button>
          </span>
        </div>
        <div class="sel">
          <label v-for="p in puestosActivos" :key="p.id" class="casilla"><input v-model="f.puestos" type="checkbox" :value="p.id" />{{ p.nombre }} <small class="muted">· nivel {{ p.nivel }}</small></label>
        </div>
      </div>
      <label class="casilla completo"><input v-model="f.activo" type="checkbox" />Activo</label>
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
.ico { width: 36px; height: 36px; border-radius: 50%; display: grid; place-items: center; background: var(--azul-claro); color: var(--azul); flex-shrink: 0; }
.sel { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px 16px; padding: 12px 14px; border: 1px solid var(--gris-200); border-radius: var(--radio-s); background: var(--gris-50); max-height: 240px; overflow-y: auto; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
@media (max-width: 600px) { .sel { grid-template-columns: 1fr; } }
</style>
