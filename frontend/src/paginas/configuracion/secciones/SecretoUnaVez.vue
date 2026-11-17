<script setup lang="ts">
// Muestra una llave o contraseña que el servidor entrega una sola vez, con botón para copiarla.
import Modal from '@/componentes/Modal.vue'
import Icono from '@/componentes/Icono.vue'
import { copiar } from './comun'
defineProps<{ titulo: string; valor: string; etiqueta?: string; aviso?: string }>()
const emit = defineEmits<{ cerrar: [] }>()
</script>

<template>
  <Modal :titulo="titulo" :ancho="560" @cerrar="emit('cerrar')">
    <div class="columna">
      <p class="caja-aviso"><Icono n="alerta" :t="20" />{{ aviso ?? 'Cópiala ahora: por seguridad no se volverá a mostrar.' }}</p>
      <div class="campo">
        <label>{{ etiqueta ?? 'Llave' }}</label>
        <div class="secreto">
          <code data-prueba="secreto">{{ valor }}</code>
          <button type="button" class="btn btn-verde-contorno btn-ch" @click="copiar(valor)"><Icono n="copiar" :t="16" />Copiar</button>
        </div>
      </div>
      <slot />
    </div>
    <template #pie>
      <button class="btn btn-primario" @click="emit('cerrar')">Listo</button>
    </template>
  </Modal>
</template>

<style scoped>
.secreto { display: flex; align-items: center; gap: 10px; padding: 10px 12px; border: 1px solid var(--gris-300); border-radius: var(--radio-s); background: var(--gris-50); }
.secreto code { flex: 1; min-width: 0; font-size: 14px; word-break: break-all; color: var(--titulo); font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace; }
</style>
