<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import Icono from './Icono.vue'
const props = withDefaults(defineProps<{ titulo?: string; subtitulo?: string; ancho?: number }>(), { ancho: 560 })
const emit = defineEmits<{ cerrar: [] }>()
const tecla = (e: KeyboardEvent) => { if (e.key === 'Escape') emit('cerrar') }
onMounted(() => { document.addEventListener('keydown', tecla); document.body.style.overflow = 'hidden' })
onBeforeUnmount(() => { document.removeEventListener('keydown', tecla); document.body.style.overflow = '' })
</script>

<template>
  <Teleport to="body">
    <div class="velo" @mousedown.self="emit('cerrar')">
      <div class="modal" :style="{ maxWidth: props.ancho + 'px' }" role="dialog" aria-modal="true" :aria-label="titulo">
        <header v-if="titulo || $slots.cabecera" class="cab">
          <div>
            <h3 class="titulo-tarjeta">{{ titulo }}</h3>
            <p v-if="subtitulo" class="subtitulo-tarjeta">{{ subtitulo }}</p>
            <slot name="cabecera" />
          </div>
          <button class="btn-icono" aria-label="Cerrar" @click="emit('cerrar')"><Icono n="x" /></button>
        </header>
        <div class="cuerpo"><slot /></div>
        <footer v-if="$slots.pie" class="pie"><slot name="pie" /></footer>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.velo { position: fixed; inset: 0; background: rgba(12, 20, 48, .45); display: grid; place-items: center; z-index: 200; padding: 16px; animation: aparecer .15s; }
.modal { background: #fff; border-radius: 14px; width: 100%; max-height: calc(100dvh - 32px); display: flex; flex-direction: column; box-shadow: var(--sombra-m); animation: subir .18s; }
.cab { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; padding: 20px 22px 6px; }
.cuerpo { padding: 14px 22px 20px; overflow-y: auto; }
.pie { display: flex; justify-content: flex-end; gap: 10px; padding: 14px 22px; border-top: 1px solid var(--gris-200); flex-wrap: wrap; }
@keyframes aparecer { from { opacity: 0 } }
@keyframes subir { from { transform: translateY(12px); opacity: 0 } }
@media (max-width: 600px) { .velo { place-items: end center; padding: 0; } .modal { border-radius: 16px 16px 0 0; max-height: 92dvh; } }
</style>
