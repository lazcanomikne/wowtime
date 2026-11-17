<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Estado from '@/componentes/Estado.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useCarga, useAccion } from '@/utils/usos'
import { hace } from '@/utils/formato'
import MenuAcciones from './MenuAcciones.vue'
import SecretoUnaVez from './SecretoUnaVez.vue'
import { useEnvio } from './comun'

type TipoDispositivo = 'Terminal' | 'LectorQr' | 'Kiosco'
interface Dispositivo { id: number; nombre: string; tipo: TipoDispositivo; sucursalId: number; activo: boolean; llavePrefijo: string | null; tieneLlave: boolean; ultimaConexion: string | null }

const TIPOS: Record<TipoDispositivo, { texto: string; icono: string; desc: string }> = {
  Terminal: { texto: 'Terminal', icono: 'terminal', desc: 'Reloj checador físico que envía registros a la API.' },
  LectorQr: { texto: 'Lector QR', icono: 'escanear', desc: 'Lector de códigos QR de visitantes en recepción.' },
  Kiosco: { texto: 'Quiosco', icono: 'monitor', desc: 'Pantalla con el quiosco web (/quiosco) donde se checa con número y PIN.' },
}

const s = useSesion()
const ui = useUi()
const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Dispositivo>>('/dispositivos', { sucursalId: s.sucursalId, tamano: 500 }), { porSucursal: true })

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ nombre: '', tipo: 'Terminal' as TipoDispositivo, sucursalId: 0, activo: true })
const intento = ref(false)
const errNombre = computed(() => (f.nombre.trim() ? '' : 'Escribe el nombre del dispositivo.'))
const llave = ref<{ nombre: string; valor: string } | null>(null)

function abrir(x?: Dispositivo) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x ? { nombre: x.nombre, tipo: x.tipo, sucursalId: x.sucursalId, activo: x.activo } : { nombre: '', tipo: 'Terminal', sucursalId: s.sucursalId ?? s.me?.sucursales[0]?.id ?? 0, activo: true })
  abierto.value = true
}
async function guardar() {
  intento.value = true
  if (errNombre.value || !f.sucursalId) return
  const c = { nombre: f.nombre.trim(), tipo: f.tipo, sucursalId: f.sucursalId, activo: f.activo }
  const r = await enviar(() => editId.value ? api.put(`/dispositivos/${editId.value}`, c) : api.post<{ id: number }>('/dispositivos', c), editId.value ? 'Dispositivo actualizado' : 'Dispositivo agregado')
  if (r.ok) { abierto.value = false; cargar() }
}
async function generarLlave(x: Dispositivo) {
  if (x.tieneLlave) {
    const c = await ui.confirmar({ titulo: `¿Generar una llave nueva para ${x.nombre}?`, mensaje: 'La llave actual dejará de funcionar de inmediato; tendrás que configurar la nueva en el dispositivo.', aceptar: 'Generar llave', peligro: true })
    if (!c.ok) return
  }
  const r = await ejecutar(() => api.post<{ id: number; llave: string }>(`/dispositivos/${x.id}/llave`))
  if (r) { llave.value = { nombre: x.nombre, valor: r.llave }; cargar() }
}
async function alternar(x: Dispositivo) {
  const ok = await ejecutar(async () => { await api.put(`/dispositivos/${x.id}`, { nombre: x.nombre, tipo: x.tipo, sucursalId: x.sucursalId, activo: !x.activo }); return true }, x.activo ? 'Dispositivo desactivado' : 'Dispositivo activado')
  if (ok) cargar()
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Dispositivos de {{ s.sucursal?.nombre }}</h2>
        <p class="subtitulo-tarjeta">Cada dispositivo se autentica con su propia llave.</p>
      </div>
      <button class="btn btn-primario" data-prueba="nuevo-dispositivo" @click="abrir()"><Icono n="mas" :t="20" />Agregar dispositivo</button>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Dispositivo</th><th>Tipo</th><th>Llave</th><th>Última conexión</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="6"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in datos?.items" :key="x.id">
            <td><div class="fila"><span class="ico"><Icono :n="TIPOS[x.tipo]?.icono ?? 'monitor'" :t="19" /></span><span class="negrita">{{ x.nombre }}</span></div></td>
            <td>{{ TIPOS[x.tipo]?.texto ?? x.tipo }}</td>
            <td>
              <code v-if="x.tieneLlave" class="prefijo">{{ x.llavePrefijo }}.••••</code>
              <span v-else class="etiqueta ambar"><span class="punto" />Sin llave</span>
            </td>
            <td>{{ x.ultimaConexion ? hace(x.ultimaConexion) : 'Nunca' }}</td>
            <td><Estado :v="x.activo ? 'Activo' : 'Inactivo'" /></td>
            <td class="acciones">
              <div class="fila">
                <button class="btn btn-contorno btn-ch" :data-prueba="`llave-${x.id}`" @click="generarLlave(x)"><Icono n="llave" :t="16" />{{ x.tieneLlave ? 'Regenerar llave' : 'Generar llave' }}</button>
                <MenuAcciones :opciones="[
                  { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                  { texto: x.activo ? 'Desactivar' : 'Activar', icono: x.activo ? 'x' : 'check', peligro: x.activo, accion: () => alternar(x) },
                ]" />
              </div>
            </td>
          </tr>
          <tr v-if="datos && !datos.items.length"><td colspan="6" class="vacio">No hay dispositivos registrados en esta sucursal.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar dispositivo' : 'Agregar dispositivo'" :ancho="540" @cerrar="abierto = false">
    <form class="columna" novalidate @submit.prevent="guardar">
      <div class="campo">
        <label for="dv-nombre">Nombre</label>
        <input id="dv-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errNombre }" placeholder="Acceso principal" />
        <span v-if="intento && errNombre" class="error-campo">{{ errNombre }}</span>
      </div>
      <div class="campo">
        <span class="etiqueta-campo">Tipo</span>
        <div class="tipos">
          <label v-for="(t, k) in TIPOS" :key="k" class="tipo" :class="{ sel: f.tipo === k }">
            <input v-model="f.tipo" type="radio" :value="k" class="oculto" />
            <Icono :n="t.icono" :t="22" />
            <span><b>{{ t.texto }}</b><small>{{ t.desc }}</small></span>
          </label>
        </div>
      </div>
      <div class="campo">
        <label for="dv-suc">Sucursal</label>
        <select id="dv-suc" v-model.number="f.sucursalId" class="entrada">
          <option v-for="x in s.me?.sucursales" :key="x.id" :value="x.id">{{ x.nombre }}</option>
        </select>
      </div>
      <label class="casilla"><input v-model="f.activo" type="checkbox" />Dispositivo activo</label>
      <p v-if="error" class="caja-alerta err" role="alert"><Icono n="alertaCirculo" :t="20" />{{ error }}</p>
      <button type="submit" class="oculto" />
    </form>
    <template #pie>
      <button class="btn btn-contorno" @click="abierto = false">Cancelar</button>
      <button class="btn btn-primario" :disabled="ocupado" data-prueba="guardar" @click="guardar">Guardar</button>
    </template>
  </Modal>

  <SecretoUnaVez v-if="llave" :titulo="`Llave de ${llave.nombre}`" :valor="llave.valor" etiqueta="Llave del dispositivo" @cerrar="llave = null">
    <div class="caja-info instrucciones">
      <Icono n="info" :t="20" />
      <span>Úsala en el quiosco <b>/quiosco</b> o en la terminal con el encabezado <code>X-Device-Key</code>.</span>
    </div>
  </SecretoUnaVez>
</template>

<style scoped>
.acciones { text-align: right; white-space: nowrap; }
.acciones .fila { justify-content: flex-end; gap: 8px; }
.ico { width: 36px; height: 36px; border-radius: 50%; display: grid; place-items: center; background: var(--verde-claro); color: var(--verde-texto); flex-shrink: 0; }
.prefijo { font-size: 13.5px; background: var(--gris-100); padding: 3px 8px; border-radius: 6px; color: var(--titulo); }
.tipos { display: flex; flex-direction: column; gap: 8px; }
.tipo { display: flex; gap: 12px; align-items: flex-start; padding: 12px 14px; border: 1px solid var(--gris-300); border-radius: var(--radio-s); cursor: pointer; color: var(--texto-2); }
.tipo span { display: flex; flex-direction: column; gap: 2px; }
.tipo b { color: var(--titulo); font-weight: 650; }
.tipo small { font-size: 13px; color: var(--texto-3); }
.tipo.sel { border-color: var(--verde); background: var(--verde-suave); color: var(--verde-texto); box-shadow: 0 0 0 1px var(--verde); }
.instrucciones { align-items: flex-start; }
.instrucciones code { background: #fff; padding: 1px 6px; border-radius: 4px; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
</style>
