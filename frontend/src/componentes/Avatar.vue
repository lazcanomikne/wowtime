<script setup lang="ts">
// Avatar con foto o iniciales (no se usan fotos de personas descargadas).
import { computed } from 'vue'
import { urlArchivo } from '@/api'

const props = withDefaults(defineProps<{ nombre?: string | null; iniciales?: string | null; foto?: string | null; t?: number; tono?: 'auto' | 'verde' | 'navy' }>(), { t: 40, tono: 'auto' })
const PALETA = [['#dcf8ec', '#087f38'], ['#e4f0fc', '#2f5bc1'], ['#fcf0d8', '#a8670c'], ['#efe7fd', '#6b3fc4'], ['#fde6ea', '#b4233a'], ['#d9f4f6', '#0b7a83'], ['#f0f4f8', '#3d4a63']]
const ini = computed(() => props.iniciales || (props.nombre ?? '').split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase())
const colores = computed(() => {
  if (props.tono === 'verde') return ['#0aaa4a', '#ffffff']
  if (props.tono === 'navy') return ['#102848', '#ffffff']
  let h = 0; for (const c of props.nombre ?? ini.value) h = (h * 31 + c.charCodeAt(0)) >>> 0
  return PALETA[h % PALETA.length]
})
</script>

<template>
  <span class="avatar" :style="{ width: t + 'px', height: t + 'px', fontSize: Math.round(t * 0.38) + 'px', background: colores[0], color: colores[1] }">
    <img v-if="foto" :src="urlArchivo(foto)" :alt="nombre ?? ''" loading="lazy" />
    <template v-else>{{ ini }}</template>
  </span>
</template>

<style scoped>
.avatar { border-radius: 50%; display: inline-grid; place-items: center; font-weight: 700; flex-shrink: 0; overflow: hidden; letter-spacing: .02em; user-select: none; }
.avatar img { width: 100%; height: 100%; object-fit: cover; }
</style>
