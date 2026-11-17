<script setup lang="ts">
// Pestaña "Historial": bitácora completa del colaborador en línea de tiempo, con filtro por tipo y paginación.
import { onMounted, ref } from 'vue'
import Icono from '@/componentes/Icono.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import { api, type Pagina } from '@/api'
import { useUi } from '@/stores/ui'
import { fechaLarga, hace, momento } from '@/utils/formato'

interface Evento { id: number; tipo: string; titulo: string; detalle?: string | null; fecha: string; autorNombre?: string | null }

const props = defineProps<{ ficha: any; id: number }>()
defineEmits<{ actualizar: [] }>()
const ui = useUi()

/** Ícono y color por tipo de evento (mismos que la tarjeta "Actividad reciente"). */
const TIPOS: Record<string, { t: string; icono: string; color: string }> = {
  asistencia: { t: 'Asistencia', icono: 'reloj', color: '#0aaa4a' },
  solicitud: { t: 'Solicitudes', icono: 'vacaciones', color: '#0aaa4a' },
  documento: { t: 'Documentos', icono: 'documento', color: '#0aaa4a' },
  datos: { t: 'Datos', icono: 'lapiz', color: '#2f74d6' },
  asignacion: { t: 'Asignaciones', icono: 'asignacion', color: '#0c9b78' },
  vacaciones: { t: 'Vacaciones', icono: 'palmera', color: '#0aaa4a' },
  seguridad: { t: 'Seguridad', icono: 'candado', color: '#7b8598' },
  visitante: { t: 'Visitantes', icono: 'visitantes', color: '#2f74d6' },
  delegacion: { t: 'Delegaciones', icono: 'llave', color: '#c27a12' },
  ausencia: { t: 'Ausencias', icono: 'alerta', color: '#e5484d' },
}
const info = (t: string) => TIPOS[t] ?? { t, icono: 'info', color: '#7b8598' }

const tipo = ref('')
const pagina = ref(1)
const datos = ref<Pagina<Evento> | null>(null)
const cargando = ref(false)

async function cargar() {
  cargando.value = true
  try {
    datos.value = await api.get<Pagina<Evento>>(`/colaboradores/${props.id}/actividad`, { tipo: tipo.value, pagina: pagina.value, tamano: 20 })
  } catch (e) { ui.error(e) } finally { cargando.value = false }
}
onMounted(cargar)
function filtrar(t: string) { if (tipo.value === t) return; tipo.value = t; pagina.value = 1; cargar() }
function ir(p: number) { pagina.value = p; cargar() }

/** Agrupa por día para mostrar un separador con la fecha. */
function diaDe(f: string) { const d = new Date(f); return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}` }
const nuevoDia = (i: number) => !datos.value || i === 0 || diaDe(datos.value.items[i].fecha) !== diaDe(datos.value.items[i - 1].fecha)
</script>

<template>
  <section class="tarjeta historial">
    <div class="tarjeta-cabecera">
      <div><h2 class="titulo-tarjeta">Historial</h2><p class="subtitulo-tarjeta">Todo lo que ha pasado con {{ ficha.nombres ?? ficha.nombre }}: asistencia, solicitudes, documentos y cambios de datos.</p></div>
    </div>

    <div class="filtros" role="tablist" aria-label="Filtrar por tipo">
      <button class="filtro" :class="{ sel: !tipo }" role="tab" :aria-selected="!tipo" @click="filtrar('')">Todos</button>
      <button v-for="(v, k) in TIPOS" :key="k" class="filtro" :class="{ sel: tipo === k }" role="tab" :aria-selected="tipo === k" @click="filtrar(k)">
        <span class="mini" :style="{ background: v.color }" />{{ v.t }}
      </button>
    </div>

    <div v-if="cargando && !datos" class="columna" style="gap: 16px">
      <div v-for="i in 5" :key="i" class="fila"><div class="esqueleto" style="width: 40px; height: 40px; border-radius: 50%" /><div class="esqueleto crecer" style="height: 44px" /></div>
    </div>
    <ol v-else-if="datos?.items.length" class="linea" :class="{ atenuado: cargando }">
      <template v-for="(e, i) in datos.items" :key="e.id">
        <li v-if="nuevoDia(i)" class="dia">{{ fechaLarga(e.fecha, true) }}</li>
        <li class="evento">
          <span class="punto" :style="{ background: info(e.tipo).color }" :title="info(e.tipo).t"><Icono :n="info(e.tipo).icono" :t="17" /></span>
          <div class="texto">
            <span class="momento" :title="hace(e.fecha)">{{ momento(e.fecha) }}</span>
            <b>{{ e.titulo }}</b>
            <span v-if="e.detalle" class="muted">{{ e.detalle }}</span>
            <span class="autor"><Icono n="persona" :t="13" />{{ e.autorNombre ?? 'Sistema' }}</span>
          </div>
        </li>
      </template>
    </ol>
    <div v-else class="vacio">
      <Icono n="lista" :t="32" class="vacio-ico" />
      <p>{{ tipo ? `Sin eventos de ${info(tipo).t.toLowerCase()}.` : 'Sin actividad registrada.' }}</p>
    </div>
    <Paginacion v-if="datos" :pagina="datos.paginaActual" :paginas="datos.paginas" :total="datos.total" :tamano="datos.tamano" @ir="ir" />
  </section>
</template>

<style scoped>
.historial { max-width: 900px; }
.filtros { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 20px; }
.filtro { display: inline-flex; align-items: center; gap: 7px; height: 34px; padding: 0 14px; border-radius: 999px; border: 1px solid var(--gris-200); background: var(--gris-50); font-size: 14px; color: var(--texto-2); }
.filtro:hover { border-color: var(--gris-300); color: var(--titulo); }
.filtro.sel { background: var(--verde-claro); border-color: var(--verde-borde); color: var(--verde-texto); font-weight: 650; }
.mini { width: 8px; height: 8px; border-radius: 50%; }
.linea { list-style: none; padding: 0; margin: 0; position: relative; }
.linea .dia { font-size: 13px; font-weight: 650; color: var(--texto-3); text-transform: uppercase; letter-spacing: .04em; padding: 6px 0 12px 56px; }
.linea .dia:not(:first-child) { padding-top: 10px; }
.evento { display: flex; gap: 16px; padding-bottom: 20px; position: relative; }
.evento::before { content: ''; position: absolute; left: 19px; top: 40px; bottom: 0; width: 2px; background: var(--gris-200); }
.linea > .evento:last-child::before { display: none; }
.punto { width: 40px; height: 40px; border-radius: 50%; color: #fff; display: grid; place-items: center; flex-shrink: 0; position: relative; z-index: 1; }
.texto { display: flex; flex-direction: column; font-size: 14.5px; min-width: 0; }
.momento { color: var(--texto-2); font-size: 14px; }
.texto b { font-weight: 600; color: var(--titulo); font-size: 15.5px; }
.texto .muted { overflow-wrap: anywhere; }
.autor { display: inline-flex; align-items: center; gap: 5px; font-size: 12.5px; color: var(--texto-3); margin-top: 3px; }
.atenuado { opacity: .55; }
.vacio-ico { color: var(--gris-400); margin-bottom: 6px; }
</style>
