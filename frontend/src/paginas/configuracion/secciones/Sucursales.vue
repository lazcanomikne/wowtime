<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Mapa from '@/componentes/Mapa.vue'
import Estado from '@/componentes/Estado.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useCarga, useAccion } from '@/utils/usos'
import { num } from '@/utils/formato'
import MenuAcciones from './MenuAcciones.vue'
import { useEnvio } from './comun'

interface Sucursal { id: number; nombre: string; direccion?: string | null; latitud: number; longitud: number; radioMetros: number; zonaHoraria: string; activa: boolean }

const s = useSesion()
const ui = useUi()
const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const q = ref('')
const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Sucursal>>('/sucursales', { tamano: 200 }))
const radioDefecto = ref(150)
api.get<{ clave: string; valor: string }[]>('/parametros').then(ps => {
  const v = Number(ps.find(p => p.clave === 'asistencia.radio_geocerca_metros')?.valor)
  if (v > 0) radioDefecto.value = v
}).catch(() => { /* se usa el valor por omisión */ })

const filas = computed(() => (datos.value?.items ?? []).filter(x => !q.value || x.nombre.toLowerCase().includes(q.value.toLowerCase())))
const ZONAS = ['America/Monterrey', 'America/Mexico_City', 'America/Matamoros', 'America/Chihuahua', 'America/Ciudad_Juarez', 'America/Ojinaga', 'America/Mazatlan', 'America/Hermosillo', 'America/Tijuana', 'America/Cancun', 'America/Merida', 'America/Bahia_Banderas']

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ nombre: '', direccion: '', latitud: 25.6694, longitud: -100.3097, radioMetros: 150, zonaHoraria: 'America/Monterrey', activa: true })
const intento = ref(false)
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!f.nombre.trim()) e.nombre = 'Escribe el nombre de la sucursal.'
  if (!Number.isFinite(Number(f.latitud)) || Math.abs(Number(f.latitud)) > 90) e.latitud = 'Latitud no válida.'
  if (!Number.isFinite(Number(f.longitud)) || Math.abs(Number(f.longitud)) > 180) e.longitud = 'Longitud no válida.'
  if (!(Number(f.radioMetros) >= 20 && Number(f.radioMetros) <= 5000)) e.radioMetros = 'Entre 20 y 5000 metros.'
  if (!f.zonaHoraria.trim()) e.zonaHoraria = 'Escribe la zona horaria.'
  return e
})
const centro = computed<[number, number] | null>(() => Number.isFinite(Number(f.latitud)) && Number.isFinite(Number(f.longitud)) ? [Number(f.latitud), Number(f.longitud)] : null)

function abrir(x?: Sucursal) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  const base = s.me?.sucursales.length ? datos.value?.items.find(i => i.id === s.sucursalId) : null
  Object.assign(f, x
    ? { nombre: x.nombre, direccion: x.direccion ?? '', latitud: x.latitud, longitud: x.longitud, radioMetros: x.radioMetros, zonaHoraria: x.zonaHoraria, activa: x.activa }
    : { nombre: '', direccion: '', latitud: base?.latitud ?? 25.6694, longitud: base?.longitud ?? -100.3097, radioMetros: radioDefecto.value, zonaHoraria: 'America/Monterrey', activa: true })
  abierto.value = true
}
function mover(lat: number, lng: number) { f.latitud = Math.round(lat * 1e6) / 1e6; f.longitud = Math.round(lng * 1e6) / 1e6 }
function miUbicacion() {
  if (!navigator.geolocation) { ui.avisar('Tu navegador no permite obtener la ubicación.', 'error'); return }
  navigator.geolocation.getCurrentPosition(p => mover(p.coords.latitude, p.coords.longitude), () => ui.avisar('No se pudo obtener tu ubicación.', 'error'), { enableHighAccuracy: true, timeout: 10000 })
}
const cuerpo = (x: typeof f) => ({ nombre: x.nombre.trim(), direccion: x.direccion.trim() || null, latitud: Number(x.latitud), longitud: Number(x.longitud), radioMetros: Number(x.radioMetros), zonaHoraria: x.zonaHoraria.trim(), activa: x.activa })
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  const r = await enviar(() => editId.value ? api.put(`/sucursales/${editId.value}`, cuerpo(f)) : api.post('/sucursales', cuerpo(f)), editId.value ? 'Sucursal actualizada' : 'Sucursal creada')
  if (r.ok) { abierto.value = false; cargar(); s.cargar() }
}
async function alternar(x: Sucursal) {
  if (x.activa) {
    const c = await ui.confirmar({ titulo: `¿Desactivar ${x.nombre}?`, mensaje: 'La sucursal dejará de aparecer en el selector. Sus colaboradores y registros se conservan.', aceptar: 'Desactivar', peligro: true })
    if (!c.ok) return
  }
  const ok = await ejecutar(async () => { await api.put(`/sucursales/${x.id}`, { ...cuerpo({ ...x, direccion: x.direccion ?? '' }), activa: !x.activa }); return true }, x.activa ? 'Sucursal desactivada' : 'Sucursal activada')
  if (ok) { cargar(); s.cargar() }
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Sucursales</h2>
        <p class="subtitulo-tarjeta">{{ datos?.total ?? 0 }} sucursales registradas</p>
      </div>
      <button class="btn btn-primario" data-prueba="nueva-sucursal" @click="abrir()"><Icono n="mas" :t="20" />Agregar sucursal</button>
    </div>
    <div class="barra-filtros">
      <div class="con-icono buscar"><Icono n="buscar" :t="18" /><input v-model="q" class="entrada" placeholder="Buscar sucursal…" aria-label="Buscar sucursal" /></div>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Sucursal</th><th>Geocerca</th><th>Zona horaria</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="5"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in filas" :key="x.id" class="clic" @click="abrir(x)">
            <td>
              <div class="fila"><span class="ico"><Icono n="edificio" :t="20" /></span>
                <div><div class="negrita">{{ x.nombre }}</div><div class="muted pequeno">{{ x.direccion || 'Sin dirección' }}</div></div>
              </div>
            </td>
            <td><div>{{ num(x.radioMetros) }} m</div><div class="muted pequeno">{{ x.latitud.toFixed(5) }}, {{ x.longitud.toFixed(5) }}</div></td>
            <td>{{ x.zonaHoraria }}</td>
            <td><Estado :v="x.activa ? 'Activa' : 'Inactiva'" /></td>
            <td class="acciones" @click.stop>
              <MenuAcciones :opciones="[
                { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                { texto: x.activa ? 'Desactivar' : 'Activar', icono: x.activa ? 'x' : 'check', peligro: x.activa, accion: () => alternar(x) },
              ]" />
            </td>
          </tr>
          <tr v-if="datos && !filas.length"><td colspan="5" class="vacio">No hay sucursales que coincidan.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar sucursal' : 'Agregar sucursal'" subtitulo="Arrastra el marcador o haz clic en el mapa para fijar la ubicación." :ancho="780" @cerrar="abierto = false">
    <form class="rejilla-form" novalidate @submit.prevent="guardar">
      <div class="campo completo">
        <label for="suc-nombre">Nombre</label>
        <input id="suc-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errores.nombre }" placeholder="Sucursal Monterrey" />
        <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
      </div>
      <div class="campo completo">
        <label for="suc-dir">Dirección</label>
        <input id="suc-dir" v-model="f.direccion" class="entrada" placeholder="Calle, número, colonia, ciudad" />
      </div>
      <div class="completo">
        <Mapa :centro="centro" :radio="Number(f.radioMetros) || null" editable icono="edificio" :alto="280" @mover="mover" />
        <button type="button" class="btn btn-contorno btn-ch ubic" @click="miUbicacion"><Icono n="navegar" :t="16" />Usar mi ubicación</button>
      </div>
      <div class="campo">
        <label for="suc-lat">Latitud</label>
        <input id="suc-lat" v-model.number="f.latitud" type="number" step="0.000001" class="entrada" :class="{ error: intento && errores.latitud }" />
        <span v-if="intento && errores.latitud" class="error-campo">{{ errores.latitud }}</span>
      </div>
      <div class="campo">
        <label for="suc-lng">Longitud</label>
        <input id="suc-lng" v-model.number="f.longitud" type="number" step="0.000001" class="entrada" :class="{ error: intento && errores.longitud }" />
        <span v-if="intento && errores.longitud" class="error-campo">{{ errores.longitud }}</span>
      </div>
      <div class="campo">
        <label for="suc-radio">Radio de geocerca (metros)</label>
        <input id="suc-radio" v-model.number="f.radioMetros" type="number" min="20" max="5000" step="10" class="entrada" :class="{ error: intento && errores.radioMetros }" />
        <span v-if="intento && errores.radioMetros" class="error-campo">{{ errores.radioMetros }}</span>
        <input v-model.number="f.radioMetros" type="range" min="20" max="1000" step="10" aria-label="Radio de geocerca" class="rango" />
      </div>
      <div class="campo">
        <label for="suc-zona">Zona horaria</label>
        <input id="suc-zona" v-model="f.zonaHoraria" class="entrada" list="zonas-mx" :class="{ error: intento && errores.zonaHoraria }" />
        <datalist id="zonas-mx"><option v-for="z in ZONAS" :key="z" :value="z" /></datalist>
        <span v-if="intento && errores.zonaHoraria" class="error-campo">{{ errores.zonaHoraria }}</span>
        <span v-else class="ayuda">Las checadas se fechan con esta zona.</span>
      </div>
      <label class="casilla completo"><input v-model="f.activa" type="checkbox" />Sucursal activa</label>
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
.ico { width: 38px; height: 38px; border-radius: 50%; display: grid; place-items: center; background: var(--verde-claro); color: var(--verde-texto); flex-shrink: 0; }
.acciones { width: 50px; text-align: right; }
.ubic { margin-top: 10px; }
.rango { accent-color: var(--verde); width: 100%; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
</style>
