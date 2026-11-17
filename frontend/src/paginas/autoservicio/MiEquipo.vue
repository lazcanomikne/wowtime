<script setup lang="ts">
// Mi equipo (supervisores y gerentes): integrantes, incidencias por resolver y asistencia de hoy.
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'
import { api, type Pagina, type Persona as TPersona } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion, useEsMovil } from '@/utils/usos'
import { fechaCorta, fechaLarga, hora, iso, num } from '@/utils/formato'

interface Integrante extends TPersona { estatus: string; equipo?: string | null; turno?: string | null; modalidad?: string | null; supervisorId?: number | null; supervisor?: string | null }
interface Incidencia { id: number; colaborador: TPersona; fecha: string; tipo: string; tipoTexto: string; detalle?: string | null; justificacion?: string | null; estado: string; puedeResolver: boolean }
interface Jornada { id: number; colaborador: TPersona; entrada?: string | null; salida?: string | null; estado: string; estadoEntrada: string; origen?: string | null; modalidad?: string | null; horas?: number | null }

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()
const esMovil = useEsMovil()
const { ocupado, ejecutar } = useAccion()
const conBarra = computed(() => esMovil.value && !s.esGestion)
const yo = computed(() => s.me?.colaborador?.id ?? null)

type Pestana = 'integrantes' | 'incidencias' | 'hoy'
const PESTANAS: { k: Pestana; t: string }[] = [{ k: 'integrantes', t: 'Integrantes' }, { k: 'incidencias', t: 'Incidencias' }, { k: 'hoy', t: 'Asistencia de hoy' }]
const tab = computed<Pestana>(() => (PESTANAS.some(p => p.k === route.query.tab) ? route.query.tab as Pestana : 'integrantes'))
const ir = (k: Pestana) => router.replace({ query: { ...route.query, tab: k === 'integrantes' ? undefined : k } })

const integrantes = ref<Integrante[]>([])
const incidencias = ref<Incidencia[]>([])
const jornadas = ref<Jornada[]>([])
const cargandoInt = ref(true), cargandoInc = ref(true), cargandoHoy = ref(true)
const q = ref('')
const hoy = iso(new Date())

/** RH y administradores también tienen equipo.ver; para ellos "Mi equipo" son sus reportes directos. */
const params = computed(() => (s.tiene('colaboradores.ver') && yo.value ? { supervisorId: yo.value } : {}))
const idsEquipo = computed(() => new Set(integrantes.value.map(i => i.id)))

async function cargarIntegrantes() {
  cargandoInt.value = true
  try {
    const r = await api.get<Pagina<Integrante>>('/colaboradores', { ...params.value, tamano: 500, orden: 'nombre' })
    integrantes.value = r.items.filter(i => i.id !== yo.value)
  } catch (e) { ui.error(e) } finally { cargandoInt.value = false }
}
async function cargarIncidencias(silencioso = false) {
  if (!silencioso) cargandoInc.value = true
  try {
    const r = await api.get<Pagina<Incidencia>>('/asistencia/incidencias', { abiertas: true, tamano: 200 })
    incidencias.value = r.items.filter(i => idsEquipo.value.has(i.colaborador.id))
  } catch (e) { ui.error(e) } finally { cargandoInc.value = false }
}
async function cargarHoy() {
  cargandoHoy.value = true
  try {
    const r = await api.get<Pagina<Jornada>>('/asistencia/registros', { agrupado: true, fecha: hoy, tamano: 500 })
    jornadas.value = r.items.filter(j => idsEquipo.value.has(j.colaborador.id))
  } catch (e) { ui.error(e) } finally { cargandoHoy.value = false }
}
async function cargarTodo() {
  await cargarIntegrantes()
  await Promise.all([cargarIncidencias(), cargarHoy()])
}
onMounted(cargarTodo)
watch(() => s.sucursalId, () => { if (s.esGestion) cargarTodo() })

// ───── Integrantes ─────
const norm = (t: string) => t.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()
const filtrados = computed(() => {
  const t = norm(q.value.trim())
  if (!t) return integrantes.value
  return integrantes.value.filter(i => norm(`${i.nombre} ${i.numero} ${i.puesto ?? ''} ${i.equipo ?? ''}`).includes(t))
})
function abrir(id: number) { router.push(`/colaboradores/${id}`) }

// ───── Incidencias ─────
const ESTADO_TXT: Record<string, string> = { PorRevisar: 'Por revisar', EnRevision: 'En revisión' }
async function resolver(i: Incidencia, justificada: boolean) {
  const r = await ui.confirmar({
    titulo: justificada ? 'Justificar incidencia' : 'Confirmar incidencia',
    mensaje: `${i.tipoTexto} de ${i.colaborador.nombre} del ${fechaLarga(i.fecha)}.${justificada ? ' No contará en su historial de asistencia.' : ' Quedará registrada en su historial.'}`,
    aceptar: justificada ? 'Justificar' : 'Confirmar incidencia', peligro: !justificada,
    pedirTexto: { etiqueta: justificada ? 'Comentario (opcional)' : 'Comentario', obligatorio: !justificada, placeholder: justificada ? 'Ej. Avisó con anticipación' : 'Explica por qué se confirma' }
  })
  if (!r.ok) return
  await ejecutar(async () => {
    await api.post(`/asistencia/incidencias/${i.id}/resolver`, { justificada, comentario: r.texto || null })
    incidencias.value = incidencias.value.filter(x => x.id !== i.id)
  }, justificada ? 'Incidencia justificada' : 'Incidencia confirmada')
}

// ───── Asistencia de hoy: todo el equipo activo, con o sin registro ─────
const filasHoy = computed(() => {
  const porId = new Map(jornadas.value.map(j => [j.colaborador.id, j]))
  return integrantes.value.filter(i => i.estatus === 'Activo').map(i => ({ persona: i as TPersona, j: porId.get(i.id) ?? null }))
    .sort((a, b) => Number(!a.j) - Number(!b.j) || (a.j?.entrada ?? '').localeCompare(b.j?.entrada ?? '') || a.persona.nombre.localeCompare(b.persona.nombre))
})
const resumenHoy = computed(() => {
  const f = filasHoy.value
  return {
    presentes: f.filter(x => x.j && !x.j.salida).length, salieron: f.filter(x => x.j?.salida).length,
    retardos: f.filter(x => x.j?.estadoEntrada === 'Retardo').length, sinRegistro: f.filter(x => !x.j).length
  }
})
const ORIGEN: Record<string, string> = { App: 'App móvil', Terminal: 'Terminal', Kiosco: 'Quiosco', Rh: 'Captura de RH' }
</script>

<template>
  <EncabezadoMovil v-if="conBarra" titulo="Mi equipo" atras="/" />
  <div class="mi-equipo" :class="{ movil: conBarra }">
    <div v-if="!conBarra" class="encabezado">
      <div>
        <h1 class="titulo-pagina">Mi equipo</h1>
        <p class="subtitulo-pagina">{{ cargandoInt ? 'Cargando…' : `${num(integrantes.length)} ${integrantes.length === 1 ? 'integrante' : 'integrantes'} · ${fechaLarga(hoy)}` }}</p>
      </div>
      <button class="btn btn-contorno" :disabled="cargandoInt" @click="cargarTodo"><Icono n="actualizar" :t="18" />Actualizar</button>
    </div>

    <nav class="pestanas">
      <button v-for="p in PESTANAS" :key="p.k" class="pestana" :class="{ activa: tab === p.k }" @click="ir(p.k)">
        {{ p.t }}<span v-if="p.k === 'incidencias' && incidencias.length" class="contador">{{ incidencias.length }}</span>
      </button>
    </nav>

    <!-- Integrantes -->
    <section v-if="tab === 'integrantes'" class="tarjeta">
      <div class="barra-filtros">
        <div class="con-icono buscar">
          <Icono n="buscar" :t="18" />
          <input v-model="q" class="entrada" type="search" placeholder="Buscar por nombre, número o puesto" aria-label="Buscar integrante" />
        </div>
      </div>
      <div v-if="cargandoInt" class="columna" style="gap: 10px"><div v-for="i in 5" :key="i" class="esqueleto" style="height: 46px" /></div>
      <template v-else-if="esMovil">
        <ul class="lista-m">
          <li v-for="i in filtrados" :key="i.id" tabindex="0" @click="abrir(i.id)" @keydown.enter="abrir(i.id)">
            <Persona :p="i" :sub="`${i.puesto ?? 'Sin puesto'} · ${i.numero}`" />
            <Estado :v="i.estatus" />
            <Icono n="derecha" :t="18" class="chev" />
          </li>
        </ul>
      </template>
      <div v-else class="tabla-envoltura">
        <table class="tabla">
          <thead><tr><th>Colaborador</th><th>Puesto</th><th>Equipo</th><th>Turno</th><th>Modalidad</th><th>Supervisor</th><th>Estatus</th></tr></thead>
          <tbody>
            <tr v-for="i in filtrados" :key="i.id" class="clic" tabindex="0" @click="abrir(i.id)" @keydown.enter="abrir(i.id)">
              <td><Persona :p="i" :sub="i.numero" /></td>
              <td>{{ i.puesto ?? '—' }}</td>
              <td>{{ i.equipo ?? '—' }}</td>
              <td>{{ i.turno ?? '—' }}</td>
              <td>{{ i.modalidad ?? '—' }}</td>
              <td>{{ i.supervisorId === yo ? 'Directo' : i.supervisor ?? '—' }}</td>
              <td><Estado :v="i.estatus" /></td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-if="!cargandoInt && !filtrados.length" class="vacio">{{ q ? 'Nadie coincide con la búsqueda.' : 'Aún no tienes colaboradores a tu cargo.' }}</p>
    </section>

    <!-- Incidencias -->
    <section v-else-if="tab === 'incidencias'" class="tarjeta">
      <div class="tarjeta-cabecera"><div><h2 class="titulo-tarjeta">Incidencias abiertas</h2><p class="subtitulo-tarjeta">Retardos, faltas, salidas omitidas y registros fuera de área por revisar.</p></div></div>
      <div v-if="cargandoInc" class="columna" style="gap: 10px"><div v-for="i in 3" :key="i" class="esqueleto" style="height: 76px" /></div>
      <ul v-else-if="incidencias.length" class="incidencias">
        <li v-for="i in incidencias" :key="i.id">
          <div class="inc-cab">
            <Persona :p="i.colaborador" enlace />
            <Estado :v="i.estado" :texto="ESTADO_TXT[i.estado]" />
          </div>
          <div class="inc-cuerpo">
            <p><Estado :v="i.tipo" :texto="i.tipoTexto" :punto="false" class="tipo" /><span class="muted">{{ fechaCorta(i.fecha) }}</span></p>
            <p v-if="i.detalle" class="detalle">{{ i.detalle }}</p>
            <p v-if="i.justificacion" class="justif"><Icono n="notas" :t="16" />“{{ i.justificacion }}”</p>
          </div>
          <div v-if="i.puedeResolver" class="inc-acciones">
            <button class="btn btn-rechazar btn-ch" :disabled="ocupado" @click="resolver(i, false)">Confirmar</button>
            <button class="btn btn-primario btn-ch" :disabled="ocupado" @click="resolver(i, true)"><Icono n="check" :t="16" />Justificar</button>
          </div>
        </li>
      </ul>
      <div v-else class="vacio"><Icono n="checkCirculo" :t="34" class="ok-ico" /><p>Sin incidencias por revisar. ¡Todo en orden!</p></div>
    </section>

    <!-- Asistencia de hoy -->
    <section v-else class="tarjeta">
      <div class="tarjeta-cabecera">
        <div><h2 class="titulo-tarjeta">Asistencia de hoy</h2><p class="subtitulo-tarjeta">{{ fechaLarga(hoy) }}</p></div>
        <button class="btn-icono borde" aria-label="Actualizar" :disabled="cargandoHoy" @click="cargarHoy"><Icono n="actualizar" :t="18" /></button>
      </div>
      <div class="chips-hoy">
        <span class="etiqueta verde"><span class="punto" />{{ resumenHoy.presentes }} en turno</span>
        <span class="etiqueta azul"><span class="punto" />{{ resumenHoy.salieron }} con salida</span>
        <span class="etiqueta naranja"><span class="punto" />{{ resumenHoy.retardos }} con retardo</span>
        <span class="etiqueta gris"><span class="punto" />{{ resumenHoy.sinRegistro }} sin registro</span>
      </div>
      <div v-if="cargandoHoy || cargandoInt" class="columna" style="gap: 10px"><div v-for="i in 5" :key="i" class="esqueleto" style="height: 46px" /></div>
      <ul v-else-if="esMovil" class="lista-m">
        <li v-for="f in filasHoy" :key="f.persona.id" tabindex="0" @click="abrir(f.persona.id)" @keydown.enter="abrir(f.persona.id)">
          <Persona :p="f.persona" :sub="f.j ? `Entrada ${hora(f.j.entrada)}${f.j.salida ? ` · Salida ${hora(f.j.salida)}` : ''}` : 'Sin registro'" />
          <Estado :v="f.j ? f.j.estado : 'SinRegistro'" />
        </li>
      </ul>
      <div v-else class="tabla-envoltura">
        <table class="tabla">
          <thead><tr><th>Colaborador</th><th>Entrada</th><th>Salida</th><th class="num">Horas</th><th>Modalidad</th><th>Origen</th><th>Estado</th></tr></thead>
          <tbody>
            <tr v-for="f in filasHoy" :key="f.persona.id" class="clic" @click="abrir(f.persona.id)">
              <td><Persona :p="f.persona" /></td>
              <td>{{ f.j?.entrada ? hora(f.j.entrada) : '—' }}</td>
              <td>{{ f.j?.salida ? hora(f.j.salida) : '—' }}</td>
              <td class="num">{{ f.j?.horas != null ? num(f.j.horas, 1) : '—' }}</td>
              <td>{{ f.j?.modalidad ?? '—' }}</td>
              <td>{{ f.j?.origen ? ORIGEN[f.j.origen] ?? f.j.origen : '—' }}</td>
              <td>
                <Estado :v="f.j ? f.j.estado : 'SinRegistro'" />
                <Estado v-if="f.j?.salida && f.j.estadoEntrada === 'Retardo'" v="Retardo" class="extra" />
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-if="!cargandoHoy && !cargandoInt && !filasHoy.length" class="vacio">Sin integrantes activos.</p>
    </section>
  </div>
</template>

<style scoped>
.mi-equipo { display: flex; flex-direction: column; gap: 20px; }
.mi-equipo.movil { padding: 16px; gap: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; }
.contador { display: inline-grid; place-items: center; min-width: 22px; height: 22px; padding: 0 6px; margin-left: 8px; border-radius: 999px; background: var(--naranja); color: #fff; font-size: 12px; font-weight: 700; }
.tabla td { white-space: nowrap; }
.lista-m { list-style: none; margin: 0; padding: 0; }
.lista-m li { display: flex; align-items: center; gap: 10px; padding: 12px 0; border-bottom: 1px solid var(--gris-200); cursor: pointer; }
.lista-m li:last-child { border-bottom: 0; }
.lista-m li .persona { flex: 1; min-width: 0; }
.chev { color: var(--gris-400); }
.incidencias { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 12px; }
.incidencias li { border: 1px solid var(--gris-200); border-radius: 12px; padding: 14px 16px; display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(0, 2fr) auto; gap: 14px; align-items: center; }
.inc-cab { display: flex; flex-direction: column; align-items: flex-start; gap: 8px; min-width: 0; }
.inc-cuerpo { display: flex; flex-direction: column; gap: 4px; font-size: 14.5px; min-width: 0; }
.inc-cuerpo p:first-child { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.tipo { height: 24px; }
.detalle { color: var(--texto-2); }
.justif { display: flex; gap: 6px; align-items: flex-start; color: var(--titulo); font-style: italic; }
.inc-acciones { display: flex; gap: 8px; justify-content: flex-end; }
.ok-ico { color: var(--verde); margin-bottom: 6px; }
.chips-hoy { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 16px; }
.extra { margin-left: 6px; }
@media (max-width: 900px) {
  .incidencias li { grid-template-columns: 1fr; }
  .inc-cab { flex-direction: row; justify-content: space-between; align-items: center; width: 100%; }
  .inc-acciones .btn { flex: 1; }
}
</style>
