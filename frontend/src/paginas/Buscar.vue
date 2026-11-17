<script setup lang="ts">
// Resultados de la búsqueda global (/buscar?q=), agrupados por tipo.
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, type Persona } from '@/api'
import Icono from '@/componentes/Icono.vue'
import PersonaC from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import { useSesion } from '@/stores/sesion'
import { fechaCorta } from '@/utils/formato'

interface Resultados {
  colaboradores: Persona[]
  solicitudes: { id: number; folio: string; colaborador: string; tipo: string; estado: string; enlace: string }[]
  visitantes: { id: number; nombre: string; empresaMotivo: string | null; fechaLocal: string; estado: string; enlace: string }[]
  reportes: { clave: string; nombre: string; enlace: string }[]
}

const s = useSesion()
const route = useRoute()
const router = useRouter()
const texto = ref(String(route.query.q ?? ''))
const r = ref<Resultados | null>(null)
const cargando = ref(false)
const error = ref<string | null>(null)
const consulta = computed(() => String(route.query.q ?? '').trim())

async function buscar() {
  texto.value = consulta.value
  if (consulta.value.length < 2) { r.value = null; return }
  cargando.value = true; error.value = null
  try { r.value = await api.get<Resultados>('/buscar', { q: consulta.value, sucursalId: s.sucursalId ?? undefined }) }
  catch (e) { r.value = null; error.value = e instanceof Error ? e.message : 'No se pudo buscar.' }
  finally { cargando.value = false }
}
watch([consulta, () => s.sucursalId], buscar, { immediate: true })
function enviar() { router.replace({ query: { q: texto.value.trim() || undefined } }) }

const total = computed(() => r.value ? r.value.colaboradores.length + r.value.solicitudes.length + r.value.visitantes.length + r.value.reportes.length : 0)
/** El servidor arma algunos enlaces con rutas antiguas; se ajustan a las del front. */
const rutaSolicitud = (x: { id: number }) => `/solicitudes/${x.id}`
</script>

<template>
  <div class="pagina">
    <div>
      <h1 class="titulo-pagina">Búsqueda</h1>
      <p v-if="consulta.length >= 2 && r" class="subtitulo-pagina">{{ total }} {{ total === 1 ? 'resultado' : 'resultados' }} para "{{ consulta }}"</p>
    </div>
    <form class="busqueda" role="search" @submit.prevent="enviar">
      <div class="con-icono crecer"><Icono n="buscar" :t="20" /><input v-model="texto" class="entrada" type="search" placeholder="Buscar colaboradores, solicitudes, visitantes o reportes…" aria-label="Buscar" autofocus /></div>
      <button class="btn btn-primario" data-prueba="buscar">Buscar</button>
    </form>

    <p v-if="consulta.length > 0 && consulta.length < 2" class="caja-info"><Icono n="info" :t="20" />Escribe al menos 2 caracteres.</p>
    <p v-if="error" class="caja-alerta err"><Icono n="alertaCirculo" :t="20" />{{ error }}</p>
    <div v-if="cargando && !r" class="tarjeta"><div class="esqueleto" style="height: 120px" /></div>

    <template v-if="r">
      <section v-if="r.colaboradores.length" class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><Icono n="colaboradores" :t="22" class="ic" /><h2 class="titulo-tarjeta">Colaboradores</h2></div><span class="muted">{{ r.colaboradores.length }}</span></div>
        <div class="lista">
          <RouterLink v-for="c in r.colaboradores" :key="c.id" :to="`/colaboradores/${c.id}`" class="fila-res">
            <PersonaC :p="c" :sub="[c.puesto, c.area].filter(Boolean).join(' · ')" />
            <span class="muted pequeno nowrap">{{ c.numero }}</span>
            <Icono n="derecha" :t="18" class="flecha" />
          </RouterLink>
        </div>
      </section>

      <section v-if="r.solicitudes.length" class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><Icono n="documento" :t="22" class="ic" /><h2 class="titulo-tarjeta">Solicitudes</h2></div><span class="muted">{{ r.solicitudes.length }}</span></div>
        <div class="lista">
          <RouterLink v-for="x in r.solicitudes" :key="x.id" :to="rutaSolicitud(x)" class="fila-res">
            <span class="crecer"><b>{{ x.folio }}</b><span class="muted"> · {{ x.tipo }}</span><br><span class="pequeno muted">{{ x.colaborador }}</span></span>
            <Estado :v="x.estado" />
            <Icono n="derecha" :t="18" class="flecha" />
          </RouterLink>
        </div>
      </section>

      <section v-if="r.visitantes.length" class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><Icono n="visitantes" :t="22" class="ic" /><h2 class="titulo-tarjeta">Visitantes</h2></div><span class="muted">{{ r.visitantes.length }}</span></div>
        <div class="lista">
          <RouterLink v-for="x in r.visitantes" :key="x.id" :to="`/visitantes/${x.id}`" class="fila-res">
            <span class="crecer"><b>{{ x.nombre }}</b><br><span class="pequeno muted">{{ x.empresaMotivo || 'Sin motivo' }} · {{ fechaCorta(x.fechaLocal) }}</span></span>
            <Estado :v="x.estado" />
            <Icono n="derecha" :t="18" class="flecha" />
          </RouterLink>
        </div>
      </section>

      <section v-if="r.reportes.length" class="tarjeta">
        <div class="tarjeta-cabecera"><div class="izq"><Icono n="reportes" :t="22" class="ic" /><h2 class="titulo-tarjeta">Reportes</h2></div><span class="muted">{{ r.reportes.length }}</span></div>
        <div class="lista">
          <RouterLink v-for="x in r.reportes" :key="x.clave" :to="x.enlace" class="fila-res">
            <span class="crecer negrita">{{ x.nombre }}</span>
            <Icono n="derecha" :t="18" class="flecha" />
          </RouterLink>
        </div>
      </section>

      <section v-if="!total" class="tarjeta vacio">
        <Icono n="buscar" :t="32" />
        <p>Sin resultados para "{{ consulta }}". Prueba con otro nombre, número de colaborador o folio.</p>
      </section>
    </template>
  </div>
</template>

<style scoped>
.pagina { display: flex; flex-direction: column; gap: 20px; max-width: 980px; }
.busqueda { display: flex; gap: 10px; }
.ic { color: var(--verde); }
.lista { display: flex; flex-direction: column; }
.fila-res { display: flex; align-items: center; gap: 14px; padding: 12px 8px; border-top: 1px solid var(--gris-200); color: inherit; text-decoration: none !important; border-radius: 6px; }
.fila-res:first-child { border-top: 0; }
.fila-res:hover { background: var(--gris-50); }
.fila-res > .persona { flex: 1; }
.fila-res b { color: var(--titulo); }
.flecha { color: var(--gris-400); flex-shrink: 0; }
.nowrap { white-space: nowrap; }
.vacio { display: flex; flex-direction: column; align-items: center; gap: 8px; }
.err { font-size: 14px; color: var(--rojo-texto); }
@media (max-width: 600px) { .busqueda { flex-direction: column; } }
</style>
