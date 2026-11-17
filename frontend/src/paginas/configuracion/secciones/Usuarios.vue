<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { api, type Pagina, type Persona } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Avatar from '@/componentes/Avatar.vue'
import Estado from '@/componentes/Estado.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { debounce, useCarga, useAccion } from '@/utils/usos'
import { hace } from '@/utils/formato'
import MenuAcciones from './MenuAcciones.vue'
import SecretoUnaVez from './SecretoUnaVez.vue'
import { todos, useEnvio } from './comun'

interface Usuario { id: number; email: string; nombre: string; roles: string[]; sucursales: number[]; activo: boolean; ultimoAcceso: string | null; debeCambiarPassword: boolean; colaboradorId: number | null; colaborador: string | null }
interface Rol { clave: string; nombre: string }

const s = useSesion()
const ui = useUi()
const { ejecutar } = useAccion()
const { ocupado, error, enviar } = useEnvio()

const q = ref('')
const rol = ref('')
const activo = ref<'' | 'true' | 'false'>('true')
const pagina = ref(1)
const roles = ref<Rol[]>([])
const sucursales = ref<{ id: number; nombre: string }[]>([])
api.get<{ roles: Rol[] }>('/roles').then(r => (roles.value = r.roles)).catch(() => { /* sin catálogo de roles */ })
todos<{ id: number; nombre: string }>('/sucursales').then(r => (sucursales.value = r)).catch(() => (sucursales.value = s.me?.sucursales ?? []))
const nombreRol = (c: string) => roles.value.find(r => r.clave === c)?.nombre ?? c
const nombreSuc = (id: number) => sucursales.value.find(x => x.id === id)?.nombre ?? `Sucursal ${id}`

const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Usuario>>('/usuarios', { q: q.value.trim(), rol: rol.value, activo: activo.value, pagina: pagina.value, tamano: 20 }))
const recargar = () => { pagina.value = 1; cargar() }
const buscar = debounce(recargar, 300)
watch([rol, activo], recargar)
function ir(p: number) { pagina.value = p; cargar() }

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ email: '', nombre: '', colaboradorId: null as number | null, roles: [] as string[], sucursales: [] as number[], activo: true })
const colabInicial = ref<Persona | null>(null)
const intento = ref(false)
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email.trim())) e.email = 'Escribe un correo válido.'
  if (!f.nombre.trim()) e.nombre = 'Escribe el nombre.'
  if (!f.roles.length) e.roles = 'Elige al menos un rol.'
  if (!f.sucursales.length && !f.roles.includes('Administrador')) e.sucursales = 'Elige al menos una sucursal.'
  return e
})
const temporal = ref<{ nombre: string; valor: string } | null>(null)

const iniciales = (n: string) => n.split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase()
function abrir(x?: Usuario) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  colabInicial.value = x?.colaboradorId ? { id: x.colaboradorId, numero: '', nombre: x.colaborador ?? x.nombre, puesto: 'Colaborador ligado', iniciales: iniciales(x.colaborador ?? x.nombre) } : null
  if (x?.colaboradorId) {
    const id = x.colaboradorId
    api.get(`/colaboradores/${id}`).then(c => {
      if (editId.value === x.id) colabInicial.value = { id, numero: c.numero, nombre: c.nombre, puesto: c.puesto, area: c.laborales?.area, iniciales: c.iniciales, fotoUrl: c.fotoUrl }
    }).catch(() => { /* se queda con el nombre de la lista */ })
  }
  Object.assign(f, x
    ? { email: x.email, nombre: x.nombre, colaboradorId: x.colaboradorId, roles: [...x.roles], sucursales: [...x.sucursales], activo: x.activo }
    : { email: '', nombre: '', colaboradorId: null, roles: ['Colaborador'], sucursales: s.sucursalId ? [s.sucursalId] : [], activo: true })
  abierto.value = true
}
function elegirColaborador(p: Persona | null) { if (p && !f.nombre.trim()) f.nombre = p.nombre }
const cuerpo = (x: { email: string; nombre: string; colaboradorId: number | null; roles: string[]; sucursales: number[]; activo: boolean }) =>
  ({ email: x.email.trim(), nombre: x.nombre.trim(), colaboradorId: x.colaboradorId, roles: x.roles, sucursales: x.sucursales, activo: x.activo })
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  if (editId.value) {
    const r = await enviar(() => api.put(`/usuarios/${editId.value}`, cuerpo(f)), 'Usuario actualizado')
    if (r.ok) { abierto.value = false; cargar() }
  } else {
    const r = await enviar(() => api.post<{ id: number; passwordTemporal: string }>('/usuarios', cuerpo(f)), 'Usuario creado')
    if (r.ok) { abierto.value = false; temporal.value = { nombre: f.nombre.trim(), valor: r.valor.passwordTemporal }; cargar() }
  }
}
async function restablecer(x: Usuario) {
  const c = await ui.confirmar({ titulo: `¿Restablecer la contraseña de ${x.nombre}?`, mensaje: 'Se generará una contraseña temporal y se cerrarán sus sesiones abiertas. Deberá cambiarla al entrar.', aceptar: 'Restablecer' })
  if (!c.ok) return
  const r = await ejecutar(() => api.post<{ passwordTemporal: string }>(`/usuarios/${x.id}/restablecer`))
  if (r) { temporal.value = { nombre: x.nombre, valor: r.passwordTemporal }; cargar() }
}
async function alternar(x: Usuario) {
  if (x.activo) {
    const c = await ui.confirmar({ titulo: `¿Desactivar a ${x.nombre}?`, mensaje: 'No podrá iniciar sesión y se cerrarán sus sesiones abiertas.', aceptar: 'Desactivar', peligro: true })
    if (!c.ok) return
  }
  const ok = await ejecutar(async () => { await api.put(`/usuarios/${x.id}`, cuerpo({ ...x, activo: !x.activo })); return true }, x.activo ? 'Usuario desactivado' : 'Usuario activado')
  if (ok) cargar()
}
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Usuarios</h2>
        <p class="subtitulo-tarjeta">{{ datos?.total ?? 0 }} usuarios</p>
      </div>
      <button class="btn btn-primario" data-prueba="nuevo-usuario" @click="abrir()"><Icono n="personaMas" :t="20" />Agregar usuario</button>
    </div>
    <div class="barra-filtros">
      <div class="con-icono buscar"><Icono n="buscar" :t="18" /><input v-model="q" class="entrada" placeholder="Buscar por nombre o correo…" aria-label="Buscar usuario" @input="buscar" /></div>
      <select v-model="rol" class="entrada" aria-label="Filtrar por rol">
        <option value="">Todos los roles</option>
        <option v-for="r in roles" :key="r.clave" :value="r.clave">{{ r.nombre }}</option>
      </select>
      <select v-model="activo" class="entrada" aria-label="Filtrar por estado">
        <option value="true">Activos</option>
        <option value="false">Inactivos</option>
        <option value="">Todos</option>
      </select>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Usuario</th><th>Roles</th><th>Sucursales</th><th>Colaborador</th><th>Último acceso</th><th>Estado</th><th aria-label="Acciones" /></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="7"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="x in datos?.items" :key="x.id" class="clic" @click="abrir(x)">
            <td>
              <div class="persona">
                <Avatar :nombre="x.nombre" :t="38" />
                <div class="textos"><div class="nombre">{{ x.nombre }}</div><div class="sub">{{ x.email }}</div></div>
              </div>
            </td>
            <td><div class="chips"><span v-for="r in x.roles" :key="r" class="etiqueta cuadrada" :class="r === 'Administrador' ? 'azul' : 'gris'">{{ nombreRol(r) }}</span></div></td>
            <td class="pequeno">{{ x.roles.includes('Administrador') && !x.sucursales.length ? 'Todas' : x.sucursales.map(nombreSuc).join(', ') || '—' }}</td>
            <td>{{ x.colaborador ?? '—' }}</td>
            <td class="nowrap">{{ x.ultimoAcceso ? hace(x.ultimoAcceso) : 'Nunca' }}</td>
            <td>
              <div class="columna estado">
                <Estado :v="x.activo ? 'Activo' : 'Inactivo'" />
                <span v-if="x.debeCambiarPassword" class="etiqueta ambar" title="Debe cambiar su contraseña temporal">Contraseña temporal</span>
              </div>
            </td>
            <td class="acciones" @click.stop>
              <MenuAcciones :opciones="[
                { texto: 'Editar', icono: 'lapiz', accion: () => abrir(x) },
                { texto: 'Restablecer contraseña', icono: 'candado', accion: () => restablecer(x) },
                { texto: x.activo ? 'Desactivar' : 'Activar', icono: x.activo ? 'x' : 'check', peligro: x.activo, oculto: x.id === s.me?.id, accion: () => alternar(x) },
              ]" />
            </td>
          </tr>
          <tr v-if="datos && !datos.items.length"><td colspan="7" class="vacio">No hay usuarios que coincidan.</td></tr>
        </tbody>
      </table>
    </div>
    <Paginacion v-if="datos" :pagina="datos.paginaActual" :paginas="datos.paginas" :total="datos.total" :tamano="datos.tamano" @ir="ir" />
  </section>

  <Modal v-if="abierto" :titulo="editId ? 'Editar usuario' : 'Agregar usuario'" :subtitulo="editId ? undefined : 'Se generará una contraseña temporal que deberá cambiar al entrar.'" :ancho="660" @cerrar="abierto = false">
    <form class="rejilla-form" novalidate @submit.prevent="guardar">
      <div class="campo completo">
        <span class="etiqueta-campo">Colaborador ligado</span>
        <SelectorColaborador v-model="f.colaboradorId" :inicial="colabInicial" placeholder="Buscar colaborador (opcional)…" @elegir="elegirColaborador" />
        <span class="ayuda">Liga la cuenta a su ficha para que vea su asistencia, vacaciones y expediente.</span>
      </div>
      <div class="campo">
        <label for="us-nombre">Nombre</label>
        <input id="us-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errores.nombre }" autocomplete="off" />
        <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
      </div>
      <div class="campo">
        <label for="us-email">Correo</label>
        <input id="us-email" v-model="f.email" type="email" class="entrada" :class="{ error: intento && errores.email }" autocomplete="off" placeholder="nombre@empresa.mx" />
        <span v-if="intento && errores.email" class="error-campo">{{ errores.email }}</span>
      </div>
      <div class="campo completo">
        <span class="etiqueta-campo">Roles</span>
        <div class="sel">
          <label v-for="r in roles" :key="r.clave" class="casilla"><input v-model="f.roles" type="checkbox" :value="r.clave" />{{ r.nombre }}</label>
        </div>
        <span v-if="intento && errores.roles" class="error-campo">{{ errores.roles }}</span>
      </div>
      <div class="campo completo">
        <span class="etiqueta-campo">Sucursales</span>
        <div class="sel">
          <label v-for="x in sucursales" :key="x.id" class="casilla"><input v-model="f.sucursales" type="checkbox" :value="x.id" />{{ x.nombre }}</label>
        </div>
        <span v-if="intento && errores.sucursales" class="error-campo">{{ errores.sucursales }}</span>
        <span v-else class="ayuda">El Administrador ve todas las sucursales aunque no las marques.</span>
      </div>
      <label class="casilla completo"><input v-model="f.activo" type="checkbox" :disabled="editId === s.me?.id" />Usuario activo</label>
      <p v-if="error" class="caja-alerta err completo" role="alert"><Icono n="alertaCirculo" :t="20" />{{ error }}</p>
      <button type="submit" class="oculto" />
    </form>
    <template #pie>
      <button class="btn btn-contorno" @click="abierto = false">Cancelar</button>
      <button class="btn btn-primario" :disabled="ocupado" data-prueba="guardar" @click="guardar">Guardar</button>
    </template>
  </Modal>

  <SecretoUnaVez v-if="temporal" :titulo="`Contraseña temporal de ${temporal.nombre}`" :valor="temporal.valor" etiqueta="Contraseña temporal"
    aviso="Compártela por un medio seguro. No se volverá a mostrar y deberá cambiarla al iniciar sesión." @cerrar="temporal = null" />
</template>

<style scoped>
.acciones { width: 50px; text-align: right; }
.chips { display: flex; flex-wrap: wrap; gap: 4px; max-width: 260px; }
.chips .etiqueta { height: 24px; font-size: 12.5px; }
.nowrap { white-space: nowrap; }
.estado { gap: 4px; align-items: flex-start; }
.estado .etiqueta.ambar { height: 22px; font-size: 12px; }
.sel { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px 16px; padding: 12px 14px; border: 1px solid var(--gris-200); border-radius: var(--radio-s); background: var(--gris-50); }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
@media (max-width: 600px) { .sel { grid-template-columns: 1fr 1fr; } }
</style>
