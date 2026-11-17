<script setup lang="ts">
// Inicio según rol: resumen de la sucursal (RH), inicio del colaborador (web) o inicio móvil.
import { defineAsyncComponent } from 'vue'
import { useSesion } from '@/stores/sesion'
import { useEsMovil } from '@/utils/usos'
const s = useSesion()
const esMovil = useEsMovil()
const Dashboard = defineAsyncComponent(() => import('./rh/Dashboard.vue'))
const InicioColaborador = defineAsyncComponent(() => import('./autoservicio/InicioColaborador.vue'))
const InicioMovil = defineAsyncComponent(() => import('./movil/InicioMovil.vue'))
</script>

<template>
  <Dashboard v-if="s.esGestion" />
  <InicioMovil v-else-if="esMovil" />
  <InicioColaborador v-else />
</template>
