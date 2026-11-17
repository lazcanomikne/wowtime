<script setup lang="ts">
import Icono from './Icono.vue'
const props = defineProps<{ pagina: number; paginas: number; total: number; tamano: number }>()
const emit = defineEmits<{ ir: [p: number] }>()
</script>

<template>
  <div v-if="total > 0" class="paginacion">
    <span>{{ (pagina - 1) * tamano + 1 }}–{{ Math.min(pagina * tamano, total) }} de {{ total }}</span>
    <div class="botones">
      <button class="btn btn-contorno btn-ch" :disabled="pagina <= 1" aria-label="Página anterior" @click="emit('ir', pagina - 1)"><Icono n="izquierda" :t="18" /></button>
      <span class="pag">Página {{ pagina }} de {{ Math.max(1, paginas) }}</span>
      <button class="btn btn-contorno btn-ch" :disabled="pagina >= paginas" aria-label="Página siguiente" @click="emit('ir', pagina + 1)"><Icono n="derecha" :t="18" /></button>
    </div>
  </div>
</template>

<style scoped>
.pag { align-self: center; padding: 0 6px; color: var(--texto-2); }
</style>
