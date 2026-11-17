<script setup lang="ts">
import Avatar from './Avatar.vue'
import type { Persona } from '@/api'
withDefaults(defineProps<{ p?: Persona | null; sub?: string | null; t?: number; enlace?: boolean }>(), { t: 40 })
</script>

<template>
  <div class="persona" v-if="p">
    <Avatar :nombre="p.nombre" :iniciales="p.iniciales" :foto="p.fotoUrl" :t="t" />
    <div class="textos">
      <RouterLink v-if="enlace" :to="`/colaboradores/${p.id}`" class="nombre enlace">{{ p.nombre }}</RouterLink>
      <div v-else class="nombre">{{ p.nombre }}</div>
      <div class="sub">{{ sub === undefined ? p.puesto : sub }}</div>
    </div>
  </div>
  <span v-else class="muted">—</span>
</template>

<style scoped>
.enlace { color: var(--titulo); display: block; }
.enlace:hover { color: var(--verde-texto); text-decoration: none; }
</style>
