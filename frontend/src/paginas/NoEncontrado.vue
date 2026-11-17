<script setup lang="ts">
import { useRoute } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import logoClaro from '@/assets/marca/logo-claro.svg'
const route = useRoute()
</script>

<template>
  <main class="no-encontrado">
    <div class="caja">
      <img :src="logoClaro" alt="WOW! Time" class="logo" />
      <p class="codigo">404</p>
      <h1 class="titulo-pagina">Página no encontrada</h1>
      <p class="subtitulo-pagina">La dirección <code>{{ route.fullPath }}</code> no existe o ya no está disponible.</p>
      <RouterLink to="/" class="btn btn-primario btn-g" data-prueba="ir-inicio"><Icono n="inicio" :t="20" />Ir al inicio</RouterLink>
    </div>
  </main>
</template>

<style scoped>
.no-encontrado { min-height: 100dvh; display: grid; place-items: center; padding: 32px 16px; background: var(--fondo); position: relative; overflow: hidden; }
.no-encontrado::after { content: ''; position: absolute; width: 520px; height: 520px; border-radius: 50%; right: -180px; bottom: -200px; background: radial-gradient(circle, rgba(10,170,74,.14), transparent 65%); pointer-events: none; }
.caja { max-width: 520px; width: 100%; text-align: center; display: flex; flex-direction: column; align-items: center; gap: 12px; position: relative; z-index: 1; }
.logo { width: 200px; margin-bottom: 18px; }
.codigo { font-size: 88px; font-weight: 800; line-height: 1; letter-spacing: -.04em; color: var(--verde); }
.subtitulo-pagina { margin-bottom: 14px; }
code { font-size: 15px; background: var(--gris-100); padding: 1px 6px; border-radius: 6px; word-break: break-all; }
</style>
