<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, API_URL, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useCarga, useAccion } from '@/utils/usos'
import { fechaCorta, hace, iso } from '@/utils/formato'
import SecretoUnaVez from './SecretoUnaVez.vue'
import { copiar, todos, useEnvio } from './comun'

interface Llave { id: number; nombre: string; prefijo: string; permisos: string[]; sucursales: number[]; creadoEn: string; ultimoUso: string | null; revocadaEn: string | null; expira: string | null }
interface Permiso { clave: string; nombre: string; grupo: string }

const s = useSesion()
const ui = useUi()
const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()
const docs = `${API_URL}/api/docs`
const pagina = ref(1)
const permisos = ref<Permiso[]>([])
const sucursales = ref<{ id: number; nombre: string }[]>([])
api.get<{ permisos: Permiso[] }>('/roles').then(r => (permisos.value = r.permisos)).catch(() => { /* sin catálogo */ })
todos<{ id: number; nombre: string }>('/sucursales').then(r => (sucursales.value = r)).catch(() => (sucursales.value = s.me?.sucursales ?? []))
const grupos = computed(() => {
  const m = new Map<string, Permiso[]>()
  for (const p of permisos.value) { if (!m.has(p.grupo)) m.set(p.grupo, []); m.get(p.grupo)!.push(p) }
  return [...m.entries()].map(([nombre, items]) => ({ nombre, items }))
})
const nombrePermiso = (c: string) => permisos.value.find(p => p.clave === c)?.nombre ?? c
const nombreSuc = (id: number) => sucursales.value.find(x => x.id === id)?.nombre ?? `Sucursal ${id}`

const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Llave>>('/llaves-api', { pagina: pagina.value, tamano: 20 }))
function ir(p: number) { pagina.value = p; cargar() }
function estado(x: Llave): [string, string] {
  if (x.revocadaEn) return ['rojo', 'Revocada']
  if (x.expira && new Date(x.expira).getTime() < Date.now()) return ['gris', 'Vencida']
  return ['verde', 'Activa']
}

const abierto = ref(false)
const f = reactive({ nombre: '', permisos: [] as string[], sucursales: [] as number[], expira: '' })
const intento = ref(false)
const manana = iso(new Date(Date.now() + 86400000))
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!f.nombre.trim()) e.nombre = 'Escribe un nombre que identifique la integración.'
  if (!f.permisos.length) e.permisos = 'Elige al menos un permiso.'
  if (!f.sucursales.length) e.sucursales = 'Elige al menos una sucursal.'
  if (f.expira && f.expira < manana) e.expira = 'La fecha de expiración debe ser futura.'
  return e
})
const nueva = ref<{ nombre: string; valor: string } | null>(null)
function abrir() {
  intento.value = false; error.value = null
  Object.assign(f, { nombre: '', permisos: [], sucursales: s.sucursalId ? [s.sucursalId] : [], expira: '' })
  abierto.value = true
}
function grupoCompleto(items: Permiso[]) { return items.every(p => f.permisos.includes(p.clave)) }
function alternarGrupo(items: Permiso[]) {
  const claves = items.map(p => p.clave)
  f.permisos = grupoCompleto(items) ? f.permisos.filter(p => !claves.includes(p)) : [...new Set([...f.permisos, ...claves])]
}
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  const c = { nombre: f.nombre.trim(), permisos: f.permisos, sucursales: f.sucursales, expira: f.expira ? new Date(`${f.expira}T23:59:59`).toISOString() : null }
  const r = await enviar(() => api.post<{ id: number; llave: string }>('/llaves-api', c), 'Llave creada')
  if (r.ok) { abierto.value = false; nueva.value = { nombre: c.nombre, valor: r.valor.llave }; pagina.value = 1; cargar() }
}
async function revocar(x: Llave) {
  const c = await ui.confirmar({ titulo: `¿Revocar la llave "${x.nombre}"?`, mensaje: 'Las integraciones que la usan dejarán de funcionar de inmediato. Esta acción no se puede deshacer.', aceptar: 'Revocar', peligro: true })
  if (!c.ok) return
  const ok = await ejecutar(async () => { await api.post(`/llaves-api/${x.id}/revocar`); return true }, 'Llave revocada')
  if (ok) cargar()
}
</script>

<template>
  <div class="columna" style="gap: 20px">
    <div class="caja-info ayuda-api">
      <Icono n="info" :t="20" />
      <span class="crecer">Envía la llave en el encabezado <code>X-Api-Key</code>. Documentación: <a :href="docs" target="_blank" rel="noopener">{{ docs }}</a></span>
      <button class="ver-todo" @click="copiar(docs)"><Icono n="copiar" :t="16" />Copiar</button>
    </div>
    <section class="tarjeta">
      <div class="tarjeta-cabecera">
        <div>
          <h2 class="titulo-tarjeta">Llaves de API</h2>
          <p class="subtitulo-tarjeta">Cada llave tiene sus propios permisos y sucursales. Solo se guarda su huella, no la llave.</p>
        </div>
        <button class="btn btn-primario" data-prueba="nueva-llave" @click="abrir"><Icono n="mas" :t="20" />Nueva llave</button>
      </div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead><tr><th>Nombre</th><th>Prefijo</th><th>Permisos</th><th>Sucursales</th><th>Creada</th><th>Último uso</th><th>Expira</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
          <tbody>
            <tr v-if="cargando && !datos"><td colspan="9"><div class="esqueleto" style="height: 40px" /></td></tr>
            <tr v-for="x in datos?.items" :key="x.id" :class="{ revocada: x.revocadaEn }">
              <td class="negrita">{{ x.nombre }}</td>
              <td><code class="prefijo">{{ x.prefijo }}.••••</code></td>
              <td><span :title="x.permisos.map(nombrePermiso).join('\n')">{{ x.permisos.length }} {{ x.permisos.length === 1 ? 'permiso' : 'permisos' }}</span></td>
              <td class="pequeno">{{ x.sucursales.map(nombreSuc).join(', ') || '—' }}</td>
              <td class="nowrap">{{ fechaCorta(x.creadoEn) }}</td>
              <td class="nowrap">{{ x.ultimoUso ? hace(x.ultimoUso) : 'Nunca' }}</td>
              <td class="nowrap">{{ x.expira ? fechaCorta(x.expira) : 'Sin expiración' }}</td>
              <td><span class="etiqueta" :class="estado(x)[0]"><span class="punto" />{{ estado(x)[1] }}</span></td>
              <td class="acciones"><button v-if="!x.revocadaEn" class="btn btn-rechazar btn-ch" :data-prueba="`revocar-${x.id}`" @click="revocar(x)">Revocar</button></td>
            </tr>
            <tr v-if="datos && !datos.items.length"><td colspan="9" class="vacio">Aún no hay llaves de API.</td></tr>
          </tbody>
        </table>
      </div>
      <Paginacion v-if="datos" :pagina="datos.paginaActual" :paginas="datos.paginas" :total="datos.total" :tamano="datos.tamano" @ir="ir" />
    </section>
  </div>

  <Modal v-if="abierto" titulo="Nueva llave de API" subtitulo="La llave completa se mostrará una sola vez." :ancho="720" @cerrar="abierto = false">
    <form class="rejilla-form" novalidate @submit.prevent="guardar">
      <div class="campo">
        <label for="ll-nombre">Nombre</label>
        <input id="ll-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errores.nombre }" placeholder="Nómina externa" />
        <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
      </div>
      <div class="campo">
        <label for="ll-exp">Expira (opcional)</label>
        <input id="ll-exp" v-model="f.expira" type="date" :min="manana" class="entrada" :class="{ error: intento && errores.expira }" />
        <span v-if="intento && errores.expira" class="error-campo">{{ errores.expira }}</span>
      </div>
      <div class="campo completo">
        <span class="etiqueta-campo">Permisos</span>
        <div class="grupos">
          <div v-for="g in grupos" :key="g.nombre" class="grupo">
            <label class="casilla negrita"><input type="checkbox" :checked="grupoCompleto(g.items)" @change="alternarGrupo(g.items)" />{{ g.nombre }}</label>
            <label v-for="p in g.items" :key="p.clave" class="casilla sub"><input v-model="f.permisos" type="checkbox" :value="p.clave" />{{ p.nombre }}</label>
          </div>
        </div>
        <span v-if="intento && errores.permisos" class="error-campo">{{ errores.permisos }}</span>
      </div>
      <div class="campo completo">
        <span class="etiqueta-campo">Sucursales</span>
        <div class="sucs">
          <label v-for="x in sucursales" :key="x.id" class="casilla"><input v-model="f.sucursales" type="checkbox" :value="x.id" />{{ x.nombre }}</label>
        </div>
        <span v-if="intento && errores.sucursales" class="error-campo">{{ errores.sucursales }}</span>
      </div>
      <p v-if="error" class="caja-alerta err completo" role="alert"><Icono n="alertaCirculo" :t="20" />{{ error }}</p>
      <button type="submit" class="oculto" />
    </form>
    <template #pie>
      <button class="btn btn-contorno" @click="abierto = false">Cancelar</button>
      <button class="btn btn-primario" :disabled="ocupado" data-prueba="guardar" @click="guardar">Crear llave</button>
    </template>
  </Modal>

  <SecretoUnaVez v-if="nueva" :titulo="`Llave “${nueva.nombre}”`" :valor="nueva.valor" etiqueta="Llave de API" @cerrar="nueva = null">
    <div class="caja-info instrucciones">
      <Icono n="info" :t="20" />
      <span>Envía la llave en el encabezado <code>X-Api-Key</code>. Documentación: <a :href="docs" target="_blank" rel="noopener">{{ docs }}</a></span>
    </div>
  </SecretoUnaVez>
</template>

<style scoped>
.ayuda-api code, .instrucciones code { background: #fff; padding: 1px 6px; border-radius: 4px; color: var(--titulo); }
.instrucciones { align-items: flex-start; }
.instrucciones a, .ayuda-api a { word-break: break-all; }
.prefijo { font-size: 13px; background: var(--gris-100); padding: 3px 8px; border-radius: 6px; color: var(--titulo); white-space: nowrap; }
.nowrap { white-space: nowrap; }
.acciones { text-align: right; }
.revocada td { color: var(--texto-3); }
.grupos { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px 20px; padding: 14px; border: 1px solid var(--gris-200); border-radius: var(--radio-s); background: var(--gris-50); max-height: 340px; overflow-y: auto; }
.grupo { display: flex; flex-direction: column; gap: 6px; }
.grupo .sub { padding-left: 26px; font-size: 14px; }
.sucs { display: flex; flex-wrap: wrap; gap: 8px 20px; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
@media (max-width: 600px) { .grupos { grid-template-columns: 1fr; } }
</style>
