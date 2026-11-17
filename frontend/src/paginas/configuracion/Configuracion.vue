<script setup lang="ts">
// Índice de Configuración: tarjetas-enlace agrupadas, solo las que el usuario puede usar.
import { computed } from 'vue'
import Icono from '@/componentes/Icono.vue'
import { useSesion } from '@/stores/sesion'
import { GRUPOS, type TarjetaConfig } from './catalogo'

const s = useSesion()
const grupos = computed(() => GRUPOS
  .map(g => ({ ...g, tarjetas: g.tarjetas.filter(t => s.tiene(...t.permisos)) }))
  .filter(g => g.tarjetas.length))
const destino = (t: TarjetaConfig) => ({ path: `/configuracion/${t.seccion}`, query: t.query })
</script>

<template>
  <div class="pagina">
    <div class="encabezado">
      <div>
        <h1 class="titulo-pagina">Configuración</h1>
        <p class="subtitulo-pagina">Catálogos, reglas y accesos de {{ s.sucursal?.nombre ?? 'tu empresa' }}.</p>
      </div>
    </div>

    <section v-for="g in grupos" :key="g.titulo" class="grupo">
      <h2 class="titulo-grupo">{{ g.titulo }}</h2>
      <div class="rejilla">
        <RouterLink v-for="t in g.tarjetas" :key="t.id" :to="destino(t)" class="tarjeta enlace" :data-prueba="`config-${t.id}`">
          <span class="ico"><Icono :n="t.icono" :t="24" /></span>
          <span class="textos">
            <span class="titulo">{{ t.titulo }}</span>
            <span class="desc">{{ t.descripcion }}</span>
          </span>
          <Icono n="derecha" :t="20" class="flecha" />
        </RouterLink>
      </div>
    </section>
    <p v-if="!grupos.length" class="tarjeta vacio">No tienes opciones de configuración disponibles.</p>
  </div>
</template>

<style scoped>
.pagina { display: flex; flex-direction: column; gap: 26px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-start; gap: 20px; }
.titulo-grupo { font-size: 17px; font-weight: 700; color: var(--texto-2); margin-bottom: 12px; }
.rejilla { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 20px; }
.enlace { display: flex; align-items: center; gap: 16px; padding: 20px 20px; color: inherit; text-decoration: none !important; transition: border-color .15s, box-shadow .15s, transform .15s; }
.enlace:hover { border-color: var(--verde-borde); box-shadow: var(--sombra-m); }
.ico { width: 52px; height: 52px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-texto); display: grid; place-items: center; flex-shrink: 0; }
.textos { display: flex; flex-direction: column; gap: 3px; min-width: 0; flex: 1; }
.titulo { font-size: 16.5px; font-weight: 700; color: var(--titulo); line-height: 1.25; }
.desc { font-size: 14px; color: var(--texto-3); line-height: 1.35; }
.flecha { color: var(--gris-400); flex-shrink: 0; }
.enlace:hover .flecha { color: var(--verde); }
@media (max-width: 760px) { .rejilla { grid-template-columns: 1fr; } }
</style>
