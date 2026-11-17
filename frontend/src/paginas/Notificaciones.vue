<script setup lang="ts">
// Avisos del usuario: lista paginada, filtro de no leídas y marcar como leídas.
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'
import { useCarga, useEsMovil, useEventos } from '@/utils/usos'
import { useUi } from '@/stores/ui'
import { hace, momento } from '@/utils/formato'

interface Notificacion { id: number; tipo: string; titulo: string; mensaje: string | null; enlace: string | null; leida: boolean; creadaEn: string }
interface Respuesta { items: Notificacion[]; total: number; paginaActual: number; tamano: number; paginas: number; noLeidas: number }

const router = useRouter()
const ui = useUi()
const esMovil = useEsMovil()
const soloNoLeidas = ref(false)
const pagina = ref(1)
const { datos, cargando, cargar } = useCarga(() => api.get<Respuesta>('/notificaciones', { soloNoLeidas: soloNoLeidas.value || undefined, pagina: pagina.value, tamano: 20 }))
useEventos(tipo => { if (tipo === 'notificacion') cargar(true) })

const ICONOS: Record<string, string> = {
  solicitud_por_aprobar: 'bandeja', solicitud_aprobada: 'checkCirculo', solicitud_rechazada: 'x', documento_por_vencer: 'documentoAlerta', dias_por_vencer: 'reloArena',
  visitante_llego: 'visitantes', cumpleanos: 'pastel', incidencia: 'alerta', cobertura: 'colaboradores', delegacion: 'llave', sin_supervisor: 'organigrama',
}
const COLORES: Record<string, string> = { solicitud_aprobada: 'verde', solicitud_rechazada: 'rojo', documento_por_vencer: 'ambar', dias_por_vencer: 'ambar', incidencia: 'naranja', cobertura: 'naranja', sin_supervisor: 'rojo', cumpleanos: 'verde' }
const icono = (t: string) => ICONOS[t] ?? 'campana'
const color = (t: string) => COLORES[t] ?? 'azul'

function filtrar(v: boolean) { soloNoLeidas.value = v; pagina.value = 1; cargar() }
function ir(p: number) { pagina.value = p; cargar() }
/** Enlaces que el servidor genera con rutas antiguas. */
const ruta = (e: string) => e
  .replace(/^\/vacaciones\/solicitudes\//, '/solicitudes/')
  .replace(/^\/asistencia\/incidencias(\?|$)/, '/asistencia?pestana=incidencias&')
  .replace(/[?&]$/, '')

async function abrir(n: Notificacion) {
  if (!n.leida) {
    try { await api.post(`/notificaciones/${n.id}/leida`); n.leida = true; if (datos.value) datos.value.noLeidas = Math.max(0, datos.value.noLeidas - 1) } catch (e) { ui.error(e) }
  }
  if (n.enlace) router.push(ruta(n.enlace))
}
const marcando = ref(false)
async function leerTodas() {
  marcando.value = true
  try { await api.post('/notificaciones/leer-todas'); ui.avisar('Todas las notificaciones están leídas'); await cargar(true) }
  catch (e) { ui.error(e) } finally { marcando.value = false }
}
</script>

<template>
  <div class="notif" :class="{ movil: esMovil }">
    <EncabezadoMovil v-if="esMovil" titulo="Notificaciones" />
    <div class="contenido">
      <div class="encabezado">
        <div v-if="!esMovil">
          <h1 class="titulo-pagina">Notificaciones</h1>
          <p class="subtitulo-pagina">{{ datos?.noLeidas ? `Tienes ${datos.noLeidas} sin leer` : 'Estás al día' }}</p>
        </div>
        <button class="btn btn-contorno" :class="{ 'btn-ch': esMovil }" :disabled="marcando || !datos?.noLeidas" data-prueba="leer-todas" @click="leerTodas">
          <Icono n="check" :t="18" />Marcar todas como leídas
        </button>
      </div>

      <nav class="pestanas">
        <button class="pestana" :class="{ activa: !soloNoLeidas }" @click="filtrar(false)">Todas</button>
        <button class="pestana" :class="{ activa: soloNoLeidas }" @click="filtrar(true)">No leídas<span v-if="datos?.noLeidas" class="cuenta">{{ datos.noLeidas }}</span></button>
      </nav>

      <section class="tarjeta lista">
        <div v-if="cargando && !datos" class="esqueleto" style="height: 160px" />
        <button v-for="n in datos?.items" :key="n.id" type="button" class="item" :class="{ nueva: !n.leida }" :data-prueba="`notif-${n.id}`" @click="abrir(n)">
          <span class="ico" :class="color(n.tipo)"><Icono :n="icono(n.tipo)" :t="20" /></span>
          <span class="textos">
            <span class="titulo">{{ n.titulo }}</span>
            <span v-if="n.mensaje" class="mensaje">{{ n.mensaje }}</span>
            <span class="cuando" :title="momento(n.creadaEn)">{{ hace(n.creadaEn) }}</span>
          </span>
          <span v-if="!n.leida" class="punto" aria-label="Sin leer" />
          <Icono v-if="n.enlace" n="derecha" :t="18" class="flecha" />
        </button>
        <div v-if="datos && !datos.items.length" class="vacio">
          <Icono n="campana" :t="32" />
          <p>{{ soloNoLeidas ? 'No tienes notificaciones sin leer.' : 'Todavía no tienes notificaciones.' }}</p>
        </div>
        <Paginacion v-if="datos" :pagina="datos.paginaActual" :paginas="datos.paginas" :total="datos.total" :tamano="datos.tamano" @ir="ir" />
      </section>
    </div>
  </div>
</template>

<style scoped>
.contenido { display: flex; flex-direction: column; gap: 20px; max-width: 920px; }
.notif.movil .contenido { padding: 16px; gap: 14px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; flex-wrap: wrap; }
.notif.movil .encabezado { justify-content: flex-end; }
.cuenta { display: inline-grid; place-items: center; min-width: 22px; height: 22px; padding: 0 6px; border-radius: 99px; background: var(--verde); color: #fff; font-size: 12px; font-weight: 700; margin-left: 8px; }
.lista { padding: 8px 12px; }
.item { display: flex; align-items: flex-start; gap: 14px; width: 100%; padding: 14px 10px; border: 0; border-bottom: 1px solid var(--gris-200); background: none; text-align: left; border-radius: 0; color: inherit; }
.item:hover { background: var(--gris-50); }
.item.nueva { background: var(--verde-suave); }
.item.nueva:hover { background: #e7f8ee; }
.ico { width: 42px; height: 42px; border-radius: 50%; display: grid; place-items: center; flex-shrink: 0; }
.ico.azul { background: var(--azul-claro); color: var(--azul); }
.ico.verde { background: var(--verde-claro); color: var(--verde-texto); }
.ico.rojo { background: var(--rojo-claro); color: var(--rojo-texto); }
.ico.ambar { background: var(--ambar-claro); color: var(--ambar); }
.ico.naranja { background: var(--naranja-claro); color: #c4520a; }
.textos { display: flex; flex-direction: column; gap: 2px; flex: 1; min-width: 0; }
.titulo { font-weight: 650; color: var(--titulo); }
.item:not(.nueva) .titulo { font-weight: 500; }
.mensaje { color: var(--texto-2); font-size: 14px; }
.cuando { color: var(--texto-3); font-size: 13px; margin-top: 2px; }
.punto { width: 10px; height: 10px; border-radius: 50%; background: var(--verde); flex-shrink: 0; margin-top: 8px; }
.flecha { color: var(--gris-400); flex-shrink: 0; margin-top: 10px; }
.vacio { display: flex; flex-direction: column; align-items: center; gap: 8px; }
</style>
