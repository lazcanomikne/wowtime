<script setup lang="ts">
// Etiqueta de estado con color fijo por valor (asistencia, solicitudes, visitantes, documentos, incidencias).
import { computed } from 'vue'
const props = withDefaults(defineProps<{ v?: string | null; texto?: string | null; punto?: boolean }>(), { punto: true })
const MAPA: Record<string, [string, string]> = {
  EnHorario: ['verde', 'En horario'], Retardo: ['naranja', 'Retardo'], SalidaRegistrada: ['azul', 'Salida registrada'], SalidaAnticipada: ['ambar', 'Salida anticipada'], SinRegistro: ['gris', 'Sin registro'],
  Pendiente: ['ambar', 'Pendiente'], Aprobada: ['verde', 'Aprobada'], Rechazada: ['rojo', 'Rechazada'], Cancelada: ['gris', 'Cancelada'],
  Aprobado: ['verde', 'Aprobado'], Rechazado: ['rojo', 'Rechazado'], EnEspera: ['gris', 'En espera'], Omitido: ['gris', 'Omitido'],
  Dentro: ['verde', 'Dentro'], Esperado: ['azul', 'Esperado'], Salio: ['gris', 'Salió'], Vencida: ['rojo', 'Vencida'],
  Vigente: ['verde', 'Vigente'], Proximo: ['ambar', 'Próximo'], Urgente: ['rojo', 'Urgente'], Vencido: ['rojo', 'Vencido'], Reemplazado: ['gris', 'Reemplazado'],
  Programada: ['azul', 'Programada'], Concluida: ['gris', 'Concluida'], Terminada: ['gris', 'Terminada'],
  PorRevisar: ['naranja', 'Por revisar'], EnRevision: ['azul', 'En revisión'], Justificada: ['verde', 'Justificada'], Confirmada: ['rojo', 'Confirmada'],
  Activo: ['verde', 'Activo'], Activa: ['verde', 'Activa'], Inactivo: ['gris', 'Inactivo'], Inactiva: ['gris', 'Inactiva'], Baja: ['rojo', 'Baja'],
  Falta: ['rojo', 'Falta'], SalidaOmitida: ['ambar', 'Salida omitida'], FueraDeArea: ['naranja', 'Fuera de área'],
}
const info = computed(() => MAPA[props.v ?? ''] ?? ['gris', props.v ?? '—'])
</script>

<template>
  <span class="etiqueta" :class="info[0]"><span v-if="punto !== false" class="punto" />{{ texto ?? info[1] }}</span>
</template>
