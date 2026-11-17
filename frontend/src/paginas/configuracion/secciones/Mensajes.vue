<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Estado from '@/componentes/Estado.vue'
import { useSesion } from '@/stores/sesion'
import { useCarga, useAccion } from '@/utils/usos'
import { fechaCorta } from '@/utils/formato'
import MenuAcciones from './MenuAcciones.vue'
import { confirmarYEjecutar, useEnvio } from './comun'

interface Mensaje { id: number; ubicacion: string; descripcion: string; texto: string; vigenciaInicio: string | null; vigenciaFin: string | null; activo: boolean }

const s = useSesion()
const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const q = ref('')
const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Mensaje>>('/mensajes-marca', { tamano: 500 }))
const filas = computed(() => {
  const t = q.value.toLowerCase()
  return (datos.value?.items ?? []).filter(x => !t || [x.ubicacion, x.descripcion, x.texto].some(v => v?.toLowerCase().includes(t)))
})
const ubicaciones = computed(() => [...new Set((datos.value?.items ?? []).map(x => x.ubicacion))].sort())
const vigencia = (x: Mensaje) => {
  if (!x.vigenciaInicio && !x.vigenciaFin) return 'Siempre'
  if (x.vigenciaInicio && x.vigenciaFin) return `${fechaCorta(x.vigenciaInicio)} – ${fechaCorta(x.vigenciaFin)}`
  return x.vigenciaInicio ? `Desde ${fechaCorta(x.vigenciaInicio)}` : `Hasta ${fechaCorta(x.vigenciaFin)}`
}
/** El mensaje que se muestra hoy en cada ubicación (el mismo criterio que /marca). */
const enUso = (x: Mensaje) => x.activo && s.frase(x.ubicacion) === x.texto

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ ubicacion: '', descripcion: '', texto: '', vigenciaInicio: '', vigenciaFin: '', activo: true })
const intento = ref(false)
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!f.ubicacion.trim()) e.ubicacion = 'Indica la ubicación.'
  else if (!/^[a-z0-9_]+$/.test(f.ubicacion.trim())) e.ubicacion = 'Usa minúsculas, números y guion bajo (por ejemplo, menu_pie).'
  if (!f.texto.trim()) e.texto = 'Escribe el texto del mensaje.'
  if (f.vigenciaInicio && f.vigenciaFin && f.vigenciaFin < f.vigenciaInicio) e.vigencia = 'La fecha final no puede ser anterior a la inicial.'
  return e
})
function abrir(x?: Mensaje) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x
    ? { ubicacion: x.ubicacion, descripcion: x.descripcion ?? '', texto: x.texto, vigenciaInicio: x.vigenciaInicio ?? '', vigenciaFin: x.vigenciaFin ?? '', activo: x.activo }
    : { ubicacion: '', descripcion: '', texto: '', vigenciaInicio: '', vigenciaFin: '', activo: true })
  abierto.value = true
}
const cuerpo = (x: { ubicacion: string; descripcion: string; texto: string; vigenciaInicio: string | null; vigenciaFin: string | null; activo: boolean }) =>
  ({ ubicacion: x.ubicacion.trim(), descripcion: x.descripcion.trim(), texto: x.texto.trim(), vigenciaInicio: x.vigenciaInicio || null, vigenciaFin: x.vigenciaFin || null, activo: x.activo })
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  const r = await enviar(() => editId.value ? api.put(`/mensajes-marca/${editId.value}`, cuerpo(f)) : api.post('/mensajes-marca', cuerpo(f)), editId.value ? 'Mensaje actualizado' : 'Mensaje creado')
  if (r.ok) { abierto.value = false; await cargar(); s.cargarMarca() }
}
async function alternar(x: Mensaje) {
  const ok = await ejecutar(async () => { await api.put(`/mensajes-marca/${x.id}`, cuerpo({ ...x, activo: !x.activo })); return true }, x.activo ? 'Mensaje desactivado' : 'Mensaje activado')
  if (ok) { await cargar(); s.cargarMarca() }
}
async function eliminar(x: Mensaje) {
  if (await confirmarYEjecutar({ titulo: '¿Eliminar este mensaje?', mensaje: `"${x.texto}" dejará de mostrarse en ${x.ubicacion}.` }, () => api.del(`/mensajes-marca/${x.id}`), 'Mensaje eliminado')) { await cargar(); s.cargarMarca() }
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Mensajes de marca</h2>
        <p class="subtitulo-tarjeta">Si hay varios mensajes vigentes en una ubicación, se muestra el de vigencia más reciente. En cumpleaños puedes usar {nombre}.</p>
      </div>
      <button class="btn btn-primario" data-prueba="nuevo-mensaje" @click="abrir()"><Icono n="mas" :t="20" />Agregar mensaje</button>
    </div>
    <div class="barra-filtros">
      <div class="con-icono buscar"><Icono n="buscar" :t="18" /><input v-model="q" class="entrada" placeholder="Buscar mensaje o ubicación…" aria-label="Buscar mensaje" /></div>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Ubicación</th><th>Texto</th><th>Vigencia</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="5"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in filas" :key="x.id" class="clic" @click="abrir(x)">
            <td><code class="ubic">{{ x.ubicacion }}</code><div class="muted pequeno">{{ x.descripcion }}</div></td>
            <td class="texto">
              <span class="negrita">{{ x.texto }}</span>
              <div v-if="enUso(x)" class="en-uso"><Icono n="check" :t="14" />Se muestra hoy</div>
            </td>
            <td class="nowrap">{{ vigencia(x) }}</td>
            <td><Estado :v="x.activo ? 'Activo' : 'Inactivo'" /></td>
            <td class="acciones" @click.stop>
              <MenuAcciones :opciones="[
                { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                { texto: x.activo ? 'Desactivar' : 'Activar', icono: x.activo ? 'x' : 'check', accion: () => alternar(x) },
                { texto: 'Eliminar', icono: 'basura', peligro: true, accion: () => eliminar(x) },
              ]" />
            </td>
          </tr>
          <tr v-if="datos && !filas.length"><td colspan="5" class="vacio">No hay mensajes que coincidan.</td></tr>
        </tbody>
      </table>
    </div>
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar mensaje' : 'Agregar mensaje'" :ancho="600" @cerrar="abierto = false">
    <form class="rejilla-form" novalidate @submit.prevent="guardar">
      <div class="campo">
        <label for="mm-ubic">Ubicación</label>
        <input id="mm-ubic" v-model="f.ubicacion" class="entrada" list="mm-ubicaciones" :class="{ error: intento && errores.ubicacion }" placeholder="menu_pie" />
        <datalist id="mm-ubicaciones"><option v-for="u in ubicaciones" :key="u" :value="u" /></datalist>
        <span v-if="intento && errores.ubicacion" class="error-campo">{{ errores.ubicacion }}</span>
      </div>
      <div class="campo">
        <label for="mm-desc">Descripción</label>
        <input id="mm-desc" v-model="f.descripcion" class="entrada" placeholder="Frase al pie del menú lateral" />
      </div>
      <div class="campo completo">
        <label for="mm-texto">Texto</label>
        <textarea id="mm-texto" v-model="f.texto" class="entrada" rows="3" maxlength="300" :class="{ error: intento && errores.texto }" placeholder="Un equipo limpio también hace grandes lugares" />
        <span v-if="intento && errores.texto" class="error-campo">{{ errores.texto }}</span>
        <span v-else class="ayuda">{{ f.texto.length }}/300</span>
      </div>
      <div class="campo">
        <label for="mm-ini">Vigente desde</label>
        <input id="mm-ini" v-model="f.vigenciaInicio" type="date" class="entrada" />
      </div>
      <div class="campo">
        <label for="mm-fin">Vigente hasta</label>
        <input id="mm-fin" v-model="f.vigenciaFin" type="date" class="entrada" :class="{ error: intento && errores.vigencia }" />
        <span v-if="intento && errores.vigencia" class="error-campo">{{ errores.vigencia }}</span>
      </div>
      <p class="ayuda completo">Deja las fechas vacías para que el mensaje esté vigente siempre.</p>
      <label class="casilla completo"><input v-model="f.activo" type="checkbox" />Mensaje activo</label>
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
.ubic { font-size: 13px; background: var(--gris-100); padding: 2px 8px; border-radius: 6px; color: var(--titulo); }
.texto { min-width: 240px; max-width: 460px; }
.nowrap { white-space: nowrap; }
.en-uso { display: inline-flex; align-items: center; gap: 4px; color: var(--verde-texto); font-size: 12.5px; font-weight: 600; margin-top: 3px; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
</style>
