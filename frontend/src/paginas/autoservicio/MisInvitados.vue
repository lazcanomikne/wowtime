<script setup lang="ts">
// Invitados registrados por el usuario como anfitrión.
import { computed, ref, watch } from 'vue'
import { api, type Pagina } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useCarga, useEsMovil, useEventos } from '@/utils/usos'
import { fechaCorta, hora } from '@/utils/formato'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Estado from '@/componentes/Estado.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'

interface Fila { id: number; nombre: string; empresaMotivo: string; fechaLocal: string; llegadaLocal: string; estado: string; entrada: string | null; salida: string | null }

const s = useSesion()
const esMovil = useEsMovil()
const conEncabezadoMovil = computed(() => esMovil.value && !s.esGestion)
const FILTROS = [
  { v: '', t: 'Todos' }, { v: 'Esperado', t: 'Esperados' }, { v: 'Dentro', t: 'Dentro' }, { v: 'Salio', t: 'Salieron' }, { v: 'Vencida', t: 'Vencidas' }, { v: 'Cancelada', t: 'Cancelados' }
]
const estado = ref('')
const pagina = ref(1)

const lista = useCarga(() => api.get<Pagina<Fila>>('/visitantes/invitaciones', { mias: true, estado: estado.value, orden: 'llegada', dir: 'desc', pagina: pagina.value, tamano: 20 }))
watch(estado, () => { pagina.value = 1; lista.cargar() })
function ir(p: number) { pagina.value = p; lista.cargar() }
useEventos(t => { if (t === 'visitante') lista.cargar(true) })
</script>

<template>
  <div class="mis-invitados" :class="{ movil: conEncabezadoMovil }">
    <EncabezadoMovil v-if="conEncabezadoMovil" titulo="Mis invitados" atras="/" />

    <div class="cuerpo">
      <div class="encabezado">
        <div v-if="!conEncabezadoMovil">
          <h1 class="titulo-pagina">Mis invitados</h1>
          <p class="subtitulo-pagina">Visitantes que registraste como anfitrión</p>
        </div>
        <RouterLink to="/m/invitado" class="btn btn-primario" :class="{ 'btn-bloque': conEncabezadoMovil }" data-prueba="registrar-invitado">
          <Icono n="personaMas" :t="18" />Registrar invitado
        </RouterLink>
      </div>

      <section class="tarjeta" :class="{ compacta: esMovil }">
        <div class="filtros" role="tablist" aria-label="Filtrar por estado">
          <button v-for="f in FILTROS" :key="f.v" type="button" class="filtro" :class="{ activo: estado === f.v }" role="tab" :aria-selected="estado === f.v" @click="estado = f.v">{{ f.t }}</button>
        </div>

        <div v-if="lista.cargando.value && !lista.datos.value" class="columna">
          <div v-for="i in 3" :key="i" class="esqueleto" style="height: 64px" />
        </div>
        <div v-else-if="!lista.datos.value?.items.length" class="vacio">
          <Icono n="visitantes" :t="34" />
          <p style="margin-top: 8px">{{ estado ? 'No tienes invitados con este estado.' : 'Aún no registras invitados.' }}</p>
          <RouterLink v-if="!estado" to="/m/invitado" class="btn btn-verde-contorno btn-ch" style="margin-top: 12px"><Icono n="personaMas" :t="16" />Registrar invitado</RouterLink>
        </div>
        <div v-else class="lista">
          <RouterLink v-for="v in lista.datos.value.items" :key="v.id" :to="`/visitantes/${v.id}`" class="invitado" :data-prueba="`invitado-${v.id}`">
            <Avatar :nombre="v.nombre" :t="44" />
            <span class="crecer textos">
              <b>{{ v.nombre }}</b>
              <small>{{ v.empresaMotivo }}</small>
              <small class="muted"><Icono n="calendario" :t="14" /> {{ fechaCorta(v.llegadaLocal) }}, {{ hora(v.llegadaLocal) }}<template v-if="v.entrada"> · Entró {{ hora(v.entrada) }}</template><template v-if="v.salida"> · Salió {{ hora(v.salida) }}</template></small>
            </span>
            <Estado :v="v.estado" />
            <Icono n="derecha" :t="20" class="flecha" />
          </RouterLink>
        </div>
        <Paginacion v-if="lista.datos.value && lista.datos.value.paginas > 1" :pagina="lista.datos.value.paginaActual" :paginas="lista.datos.value.paginas"
                    :total="lista.datos.value.total" :tamano="lista.datos.value.tamano" @ir="ir" />
      </section>
    </div>
  </div>
</template>

<style scoped>
.cuerpo { display: flex; flex-direction: column; gap: 20px; }
.mis-invitados.movil .cuerpo { padding: 16px; gap: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; }
.filtros { display: flex; gap: 8px; overflow-x: auto; scrollbar-width: none; margin-bottom: 14px; padding-bottom: 2px; }
.filtros::-webkit-scrollbar { display: none; }
.filtro { height: 34px; padding: 0 14px; border-radius: 999px; border: 1px solid var(--gris-200); background: var(--gris-50); color: var(--texto-2); font-size: 14px; font-weight: 500; white-space: nowrap; }
.filtro:hover { border-color: var(--gris-300); }
.filtro.activo { background: var(--verde-claro); border-color: var(--verde-borde); color: var(--verde-texto); font-weight: 650; }
.lista { display: flex; flex-direction: column; gap: 10px; }
.invitado { display: flex; align-items: center; gap: 14px; padding: 12px 14px; border: 1px solid var(--gris-200); border-radius: var(--radio); color: var(--titulo); text-decoration: none !important; min-width: 0; }
.invitado:hover { background: #fafbfd; border-color: var(--gris-300); }
.textos { min-width: 0; }
.textos b { display: block; font-size: 15.5px; }
.textos small { display: block; font-size: 13.5px; color: var(--texto-2); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.textos small.muted { color: var(--texto-3); }
.flecha { color: var(--texto-3); }
@media (max-width: 760px) {
  .invitado { gap: 10px; flex-wrap: wrap; position: relative; padding-right: 36px; }
  .invitado .textos { flex-basis: calc(100% - 56px); }
  .invitado :deep(.etiqueta) { margin-left: 54px; }
  .flecha { position: absolute; right: 10px; top: 50%; transform: translateY(-50%); }
}
</style>
