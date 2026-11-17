<script setup lang="ts">
// /configuracion/:seccion — encabezado con migas y despacho al componente de cada catálogo.
import { computed, defineAsyncComponent, type Component } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import { useSesion } from '@/stores/sesion'
import { SECCIONES } from './catalogo'

const route = useRoute()
const router = useRouter()
const s = useSesion()

const c = (f: () => Promise<any>) => defineAsyncComponent(f)
const COMPONENTES: Record<string, Component> = {
  sucursales: c(() => import('./secciones/Sucursales.vue')),
  areas: c(() => import('./secciones/Areas.vue')),
  equipos: c(() => import('./secciones/Equipos.vue')),
  puestos: c(() => import('./secciones/Puestos.vue')),
  turnos: c(() => import('./secciones/Turnos.vue')),
  festivos: c(() => import('./secciones/Festivos.vue')),
  'tipos-permiso': c(() => import('./secciones/TiposPermiso.vue')),
  'tipos-documento': c(() => import('./secciones/TiposDocumento.vue')),
  dispositivos: c(() => import('./secciones/Dispositivos.vue')),
  mensajes: c(() => import('./secciones/Mensajes.vue')),
  parametros: c(() => import('./secciones/Parametros.vue')),
  politicas: c(() => import('./secciones/Politicas.vue')),
  usuarios: c(() => import('./secciones/Usuarios.vue')),
  roles: c(() => import('./secciones/Roles.vue')),
  'llaves-api': c(() => import('./secciones/LlavesApi.vue')),
  correos: c(() => import('./secciones/Correos.vue')),
  tareas: c(() => import('./secciones/Tareas.vue')),
  bitacora: c(() => import('./secciones/Bitacora.vue')),
}

const clave = computed(() => String(route.params.seccion ?? ''))
const info = computed(() => SECCIONES[clave.value])
const componente = computed(() => COMPONENTES[clave.value])
const permitido = computed(() => !!info.value && s.tiene(...info.value.permisos))
const titulo = computed(() => {
  if (clave.value === 'parametros' && route.query.grupo) return `Parámetros de ${String(route.query.grupo).toLowerCase()}`
  return info.value?.titulo ?? 'Sección no encontrada'
})
</script>

<template>
  <div class="pagina">
    <nav class="migas" aria-label="Ruta">
      <RouterLink to="/configuracion">Configuración</RouterLink>
      <Icono n="derecha" :t="16" />
      <span>{{ titulo }}</span>
    </nav>
    <div class="encabezado">
      <button class="btn-icono borde regreso" aria-label="Regresar a Configuración" @click="router.push('/configuracion')"><Icono n="regresar" :t="20" /></button>
      <div class="crecer">
        <h1 class="titulo-pagina">{{ titulo }}</h1>
        <p v-if="info" class="subtitulo-pagina">{{ info.descripcion }}</p>
      </div>
    </div>

    <component :is="componente" v-if="componente && permitido" />
    <section v-else class="tarjeta vacio">
      <Icono :n="info ? 'candado' : 'alertaCirculo'" :t="32" />
      <p>{{ info ? 'No tienes permiso para administrar esta sección.' : 'La sección que buscas no existe.' }}</p>
      <RouterLink to="/configuracion" class="btn btn-contorno" style="margin-top: 14px">Ir a Configuración</RouterLink>
    </section>
  </div>
</template>

<style scoped>
.pagina { display: flex; flex-direction: column; gap: 20px; }
.migas { display: flex; align-items: center; gap: 6px; font-size: 14.5px; color: var(--texto-3); margin-bottom: -8px; flex-wrap: wrap; }
.migas a { color: var(--texto-2); font-weight: 500; }
.migas span { color: var(--titulo); font-weight: 600; }
.encabezado { display: flex; align-items: flex-start; gap: 14px; }
.regreso { margin-top: 4px; flex-shrink: 0; }
.vacio { display: flex; flex-direction: column; align-items: center; gap: 8px; }
</style>
