<script setup lang="ts">
import { onMounted } from 'vue'
import { useRegisterSW } from 'virtual:pwa-register/vue'
import Globales from '@/componentes/Globales.vue'
import { useSesion } from '@/stores/sesion'

const s = useSesion()
onMounted(() => s.cargarMarca())
useRegisterSW({ immediate: true })
</script>

<template>
  <RouterView />
  <Globales />
</template>
