<script setup lang="ts">
// Encabezado móvil: flecha de regreso y título en pantallas internas (decisión 27).
// En escritorio se muestra como título de página normal.
import { useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import { useEsMovil } from '@/utils/usos'
const props = withDefaults(defineProps<{ titulo: string; atras?: string | null; subtitulo?: string | null }>(), { atras: null, subtitulo: null })
const router = useRouter()
const esMovil = useEsMovil()
function regresar() { if (props.atras) router.push(props.atras); else if (window.history.length > 1) router.back(); else router.push('/') }
</script>

<template>
  <header v-if="esMovil" class="enc-movil">
    <button class="atras" aria-label="Regresar" @click="regresar"><Icono n="izquierda" :t="26" :g="2.2" /></button>
    <h1>{{ titulo }}</h1>
    <div class="der"><slot /></div>
  </header>
  <div v-else class="enc-escritorio">
    <div>
      <h1 class="titulo-pagina">{{ titulo }}</h1>
      <p v-if="subtitulo" class="subtitulo-pagina">{{ subtitulo }}</p>
    </div>
    <div class="der"><slot /></div>
  </div>
</template>

<style scoped>
.enc-movil { background: var(--navy); color: #fff; height: calc(64px + env(safe-area-inset-top)); padding: env(safe-area-inset-top) 12px 0 6px; display: flex; align-items: center; gap: 6px; position: sticky; top: 0; z-index: 40; }
.atras { width: 44px; height: 44px; border: 0; background: none; color: #fff; display: grid; place-items: center; border-radius: 10px; }
.atras:hover { background: rgba(255,255,255,.08); }
.enc-movil h1 { color: #fff; font-size: 19px; font-weight: 600; letter-spacing: -.01em; flex: 1; }
.der { display: flex; align-items: center; gap: 10px; }
.enc-escritorio { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 20px; }
</style>
