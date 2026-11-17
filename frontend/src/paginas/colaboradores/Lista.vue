<script setup lang="ts">
// Listado de colaboradores con filtros, orden, paginación y exportación a CSV (todas las páginas).
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'
import { api, type Pagina } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { debounce, useAccion, useEsMovil } from '@/utils/usos'
import { fechaCorta, fechaNum, iso, num } from '@/utils/formato'
import { ESTATUS, MODALIDADES } from './comun'

interface Fila {
  id: number; numero: string; nombre: string; nombres: string; apellidos: string; email?: string | null; telefono?: string | null; estatus: string
  sucursal?: string | null; area?: string | null; equipo?: string | null; puesto?: string | null; turno?: string | null; supervisor?: string | null; supervisorId?: number | null
  fechaIngreso: string; modalidad?: string | null; iniciales: string; fotoUrl?: string | null
}
interface Opcion { id: number; nombre: string; areaId?: number }

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()
const esMovil = useEsMovil()
const { ocupado: exportando, ejecutar } = useAccion()
const conBarra = computed(() => esMovil.value && !s.esGestion)

const verdadero = (v: unknown) => v === '1' || v === 'true'
const filtros = reactive({
  q: '', areaId: null as number | null, equipoId: null as number | null, puestoId: null as number | null, turnoId: null as number | null,
  estatus: '', modalidad: '', sinSupervisor: verdadero(route.query.sinSupervisor)
})
const pagina = ref(1)
const orden = ref<'nombre' | 'puesto' | 'area' | 'ingreso'>('nombre')
const dir = ref<'asc' | 'desc'>('asc')
const TAMANO = 25

const datos = ref<Pagina<Fila> | null>(null)
const cargando = ref(false)
const areas = ref<Opcion[]>([])
const equipos = ref<Opcion[]>([])
const puestos = ref<Opcion[]>([])
const turnos = ref<(Opcion & { horaEntrada: string; horaSalida: string })[]>([])

const equiposFiltrados = computed(() => filtros.areaId ? equipos.value.filter(e => e.areaId === filtros.areaId) : equipos.value)
const hayFiltros = computed(() => !!(filtros.q || filtros.areaId || filtros.equipoId || filtros.puestoId || filtros.turnoId || filtros.estatus || filtros.modalidad || filtros.sinSupervisor))

function parametros(extra: Record<string, string | number | boolean | null | undefined> = {}) {
  return {
    sucursalId: s.sucursalId, q: filtros.q.trim(), areaId: filtros.areaId, equipoId: filtros.equipoId, puestoId: filtros.puestoId, turnoId: filtros.turnoId,
    estatus: filtros.estatus, modalidad: filtros.modalidad, sinSupervisor: filtros.sinSupervisor ? true : undefined, orden: orden.value, dir: dir.value, ...extra
  }
}

let turnoCarga = 0
async function cargar() {
  const n = ++turnoCarga
  cargando.value = true
  try {
    const r = await api.get<Pagina<Fila>>('/colaboradores', parametros({ pagina: pagina.value, tamano: TAMANO }))
    if (n === turnoCarga) datos.value = r
  } catch (e) { if (n === turnoCarga) ui.error(e) } finally { if (n === turnoCarga) cargando.value = false }
}

async function cargarCatalogos() {
  const suc = s.sucursalId ?? undefined
  const [a, e, p, t] = await Promise.allSettled([
    api.get('/areas', { sucursalId: suc, tamano: 500, activos: true }),
    api.get('/equipos', { sucursalId: suc, tamano: 500, activos: true }),
    api.get('/puestos', { tamano: 500, activos: true, orden: 'nombre' }),
    api.get('/turnos', { tamano: 500, activos: true }),
  ])
  areas.value = a.status === 'fulfilled' ? a.value.items : []
  equipos.value = e.status === 'fulfilled' ? e.value.items : []
  puestos.value = p.status === 'fulfilled' ? p.value.items : []
  turnos.value = t.status === 'fulfilled' ? t.value.items : []
}

onMounted(() => { cargar(); cargarCatalogos() })
watch(() => s.sucursalId, () => { filtros.areaId = null; filtros.equipoId = null; pagina.value = 1; cargar(); cargarCatalogos() })
const buscarDiferido = debounce(() => { pagina.value = 1; cargar() }, 300)
watch(() => filtros.q, buscarDiferido)
watch(() => [filtros.areaId, filtros.equipoId, filtros.puestoId, filtros.turnoId, filtros.estatus, filtros.modalidad, filtros.sinSupervisor], () => { pagina.value = 1; cargar() })
watch(() => filtros.areaId, () => { if (filtros.equipoId && !equiposFiltrados.value.some(e => e.id === filtros.equipoId)) filtros.equipoId = null })
watch(() => route.query.sinSupervisor, v => { filtros.sinSupervisor = verdadero(v) })
watch(() => filtros.sinSupervisor, v => {
  if (v === verdadero(route.query.sinSupervisor)) return
  router.replace({ query: { ...route.query, sinSupervisor: v ? '1' : undefined } })
})

function ordenar(campo: typeof orden.value) {
  if (orden.value === campo) dir.value = dir.value === 'asc' ? 'desc' : 'asc'
  else { orden.value = campo; dir.value = campo === 'ingreso' ? 'desc' : 'asc' }
  pagina.value = 1
  cargar()
}
function ir(p: number) { pagina.value = p; cargar(); window.scrollTo({ top: 0, behavior: 'smooth' }) }
function limpiar() {
  Object.assign(filtros, { q: '', areaId: null, equipoId: null, puestoId: null, turnoId: null, estatus: '', modalidad: '', sinSupervisor: false })
}
function abrir(f: Fila) { router.push(`/colaboradores/${f.id}`) }

// ───── Exportar a CSV (se arma en el navegador con todas las páginas) ─────
function celda(v: unknown) {
  const t = v === null || v === undefined ? '' : String(v)
  return /[",\n\r;]/.test(t) ? `"${t.replace(/"/g, '""')}"` : t
}
async function exportar() {
  await ejecutar(async () => {
    const filas: Fila[] = []
    let p = 1, paginas = 1
    do {
      const r = await api.get<Pagina<Fila>>('/colaboradores', parametros({ pagina: p, tamano: 500 }))
      filas.push(...r.items)
      paginas = r.paginas
      p++
    } while (p <= paginas)
    const enc = ['Número', 'Nombre', 'Puesto', 'Área', 'Equipo', 'Supervisor', 'Sucursal', 'Turno', 'Modalidad', 'Fecha de ingreso', 'Estatus', 'Correo', 'Teléfono']
    const lineas = [enc.map(celda).join(',')]
    for (const f of filas)
      lineas.push([f.numero, f.nombre, f.puesto, f.area, f.equipo, f.supervisor, f.sucursal, f.turno, f.modalidad, fechaNum(f.fechaIngreso), f.estatus, f.email, f.telefono].map(celda).join(','))
    const blob = new Blob(['﻿' + lineas.join('\r\n')], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = `colaboradores-${iso(new Date())}.csv`
    document.body.appendChild(a); a.click(); a.remove()
    setTimeout(() => URL.revokeObjectURL(url), 2000)
    ui.avisar(`Se exportaron ${num(filas.length)} colaboradores`)
  })
}

const subtitulo = computed(() => {
  const t = datos.value?.total
  if (t === undefined) return 'Cargando…'
  const base = `${num(t)} ${t === 1 ? 'colaborador' : 'colaboradores'}`
  return hayFiltros.value ? `${base} con los filtros aplicados` : `${base}${s.esGestion && s.sucursal ? ` en ${s.sucursal.nombre}` : ''}`
})
</script>

<template>
  <EncabezadoMovil v-if="conBarra" titulo="Colaboradores" />
  <div class="lista" :class="{ movil: conBarra }">
    <div class="encabezado">
      <div>
        <h1 class="titulo-pagina">Colaboradores</h1>
        <p class="subtitulo-pagina">{{ subtitulo }}</p>
      </div>
      <div class="fila envolver acciones">
        <button class="btn btn-contorno" :disabled="exportando || !datos?.total" data-prueba="exportar" @click="exportar">
          <span v-if="exportando" class="spinner chico" /><Icono v-else n="descargar" :t="19" />Exportar
        </button>
        <RouterLink v-if="s.tiene('colaboradores.editar')" to="/colaboradores/nuevo" class="btn btn-primario" data-prueba="agregar">
          <Icono n="personaMas" :t="19" />Agregar colaborador
        </RouterLink>
      </div>
    </div>

    <section class="tarjeta">
      <div class="barra-filtros">
        <div class="con-icono buscar">
          <Icono n="buscar" :t="18" />
          <input v-model="filtros.q" class="entrada" type="search" placeholder="Buscar por nombre, número o correo" aria-label="Buscar colaborador" />
        </div>
        <select v-model="filtros.areaId" class="entrada" aria-label="Área">
          <option :value="null">Todas las áreas</option>
          <option v-for="a in areas" :key="a.id" :value="a.id">{{ a.nombre }}</option>
        </select>
        <select v-model="filtros.equipoId" class="entrada" aria-label="Equipo">
          <option :value="null">Todos los equipos</option>
          <option v-for="e in equiposFiltrados" :key="e.id" :value="e.id">{{ e.nombre }}</option>
        </select>
        <select v-model="filtros.puestoId" class="entrada" aria-label="Puesto">
          <option :value="null">Todos los puestos</option>
          <option v-for="p in puestos" :key="p.id" :value="p.id">{{ p.nombre }}</option>
        </select>
        <select v-model="filtros.turnoId" class="entrada" aria-label="Turno">
          <option :value="null">Todos los turnos</option>
          <option v-for="t in turnos" :key="t.id" :value="t.id">{{ t.nombre }} ({{ t.horaEntrada.slice(0, 5) }}–{{ t.horaSalida.slice(0, 5) }})</option>
        </select>
        <select v-model="filtros.estatus" class="entrada" aria-label="Estatus">
          <option value="">Activos e inactivos</option>
          <option v-for="e in ESTATUS" :key="e.v" :value="e.v">{{ e.t }}</option>
        </select>
        <select v-model="filtros.modalidad" class="entrada" aria-label="Modalidad">
          <option value="">Todas las modalidades</option>
          <option v-for="m in MODALIDADES" :key="m.v" :value="m.v">{{ m.t }}</option>
        </select>
        <label class="casilla sin-sup"><input v-model="filtros.sinSupervisor" type="checkbox" />Sin supervisor</label>
        <button v-if="hayFiltros" class="ver-todo limpiar" @click="limpiar"><Icono n="x" :t="16" />Limpiar filtros</button>
      </div>

      <div v-if="filtros.sinSupervisor" class="caja-aviso aviso-sup">
        <Icono n="organigrama" :t="20" />
        <span>Colaboradores sin línea de reporte (excepto la dirección). Asígnales un supervisor desde su ficha o desde el organigrama.</span>
      </div>

      <div class="tabla-envoltura">
        <table class="tabla">
          <thead>
            <tr>
              <th class="ordenable" :aria-sort="orden === 'nombre' ? (dir === 'asc' ? 'ascending' : 'descending') : 'none'" @click="ordenar('nombre')">
                Colaborador <Icono v-if="orden === 'nombre'" :n="dir === 'asc' ? 'arriba' : 'abajo'" :t="14" />
              </th>
              <th class="ordenable" :aria-sort="orden === 'puesto' ? (dir === 'asc' ? 'ascending' : 'descending') : 'none'" @click="ordenar('puesto')">
                Puesto <Icono v-if="orden === 'puesto'" :n="dir === 'asc' ? 'arriba' : 'abajo'" :t="14" />
              </th>
              <th class="ordenable" :aria-sort="orden === 'area' ? (dir === 'asc' ? 'ascending' : 'descending') : 'none'" @click="ordenar('area')">
                Área <Icono v-if="orden === 'area'" :n="dir === 'asc' ? 'arriba' : 'abajo'" :t="14" />
              </th>
              <th>Equipo</th>
              <th>Supervisor</th>
              <th>Modalidad</th>
              <th class="ordenable" :aria-sort="orden === 'ingreso' ? (dir === 'asc' ? 'ascending' : 'descending') : 'none'" @click="ordenar('ingreso')">
                Ingreso <Icono v-if="orden === 'ingreso'" :n="dir === 'asc' ? 'arriba' : 'abajo'" :t="14" />
              </th>
              <th>Estatus</th>
            </tr>
          </thead>
          <tbody v-if="cargando && !datos">
            <tr v-for="i in 8" :key="i"><td colspan="8"><div class="esqueleto" style="height: 34px" /></td></tr>
          </tbody>
          <tbody v-else :class="{ atenuado: cargando }">
            <tr v-for="f in datos?.items" :key="f.id" class="clic" tabindex="0" @click="abrir(f)" @keydown.enter="abrir(f)">
              <td><Persona :p="f" :sub="f.numero" /></td>
              <td>{{ f.puesto ?? '—' }}</td>
              <td>{{ f.area ?? '—' }}</td>
              <td>{{ f.equipo ?? '—' }}</td>
              <td>
                <span v-if="f.supervisor">{{ f.supervisor }}</span>
                <span v-else class="etiqueta ambar">Sin supervisor</span>
              </td>
              <td>{{ f.modalidad ?? '—' }}</td>
              <td class="nowrap">{{ fechaCorta(f.fechaIngreso) }}</td>
              <td><Estado :v="f.estatus" /></td>
            </tr>
            <tr v-if="datos && !datos.items.length">
              <td colspan="8" class="vacio">
                <Icono n="colaboradores" :t="34" class="vacio-ico" />
                <p>{{ hayFiltros ? 'Ningún colaborador coincide con los filtros.' : 'Aún no hay colaboradores registrados.' }}</p>
                <button v-if="hayFiltros" class="btn btn-contorno btn-ch" style="margin-top: 12px" @click="limpiar">Limpiar filtros</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <Paginacion v-if="datos" :pagina="datos.paginaActual" :paginas="datos.paginas" :total="datos.total" :tamano="datos.tamano" @ir="ir" />
    </section>
  </div>
</template>

<style scoped>
.lista { display: flex; flex-direction: column; gap: 20px; }
.lista.movil { padding: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; }
.acciones { justify-content: flex-end; }
.sin-sup { height: 40px; padding: 0 12px; border: 1px solid var(--gris-300); border-radius: var(--radio-s); background: #fff; }
.limpiar { margin-left: 4px; }
.aviso-sup { margin-bottom: 14px; }
.tabla td { white-space: nowrap; }
.tabla td:first-child { min-width: 240px; }
.nowrap { white-space: nowrap; }
.tabla th .icono { color: var(--verde-texto); margin-left: 2px; }
.atenuado { opacity: .55; transition: opacity .15s; }
.vacio-ico { color: var(--gris-400); margin-bottom: 8px; }
.spinner.chico { width: 16px; height: 16px; border-width: 2px; }
@media (max-width: 760px) {
  .acciones { width: 100%; }
  .acciones .btn { flex: 1; }
  .sin-sup { width: 100%; }
}
</style>
