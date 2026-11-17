<script setup lang="ts">
// Campo de fecha con formato dd/mm/aaaa (como en los mockups) que abre el selector nativo.
import { ref } from 'vue'
import Icono from './Icono.vue'
import { fechaNum } from '@/utils/formato'
const props = defineProps<{ modelValue?: string | null; min?: string | null; max?: string | null; id?: string; placeholder?: string }>()
const emit = defineEmits<{ 'update:modelValue': [v: string | null] }>()
const nativo = ref<HTMLInputElement>()
function abrir() { const n = nativo.value as any; if (n?.showPicker) { try { n.showPicker(); return } catch { /* sin soporte */ } } n?.focus(); n?.click() }
</script>

<template>
  <div class="campo-fecha" @click="abrir">
    <Icono n="calendario" :t="20" class="ico" />
    <input :id="id" class="entrada visible" readonly :value="modelValue ? fechaNum(modelValue) : ''" :placeholder="placeholder ?? 'dd/mm/aaaa'" />
    <Icono n="abajo" :t="20" class="chev" />
    <input ref="nativo" class="nativo" type="date" tabindex="-1" aria-hidden="true" :value="modelValue ?? ''" :min="min ?? undefined" :max="max ?? undefined"
           @change="emit('update:modelValue', ($event.target as HTMLInputElement).value || null)" />
  </div>
</template>

<style scoped>
.campo-fecha { position: relative; cursor: pointer; }
.visible { padding-left: 44px; padding-right: 40px; cursor: pointer; font-size: 16.5px; }
.ico { position: absolute; left: 14px; top: 50%; transform: translateY(-50%); color: var(--texto-2); pointer-events: none; }
.chev { position: absolute; right: 12px; top: 50%; transform: translateY(-50%); color: var(--titulo); pointer-events: none; }
.nativo { position: absolute; left: 0; bottom: 0; width: 100%; height: 1px; opacity: 0; pointer-events: none; border: 0; padding: 0; }
</style>
