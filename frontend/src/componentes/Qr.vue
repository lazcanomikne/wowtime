<script setup lang="ts">
// QR real generado con el contenido firmado que devuelve el servidor.
import { onMounted, ref, watch } from 'vue'
import QRCode from 'qrcode'
const props = withDefaults(defineProps<{ valor: string; t?: number }>(), { t: 220 })
const canvas = ref<HTMLCanvasElement>()
async function dibujar() { if (canvas.value && props.valor) await QRCode.toCanvas(canvas.value, props.valor, { width: props.t, margin: 1, color: { dark: '#0c1430', light: '#ffffff' }, errorCorrectionLevel: 'M' }) }
onMounted(dibujar)
watch(() => props.valor, dibujar)
</script>

<template><canvas ref="canvas" class="qr" :aria-label="'Código QR'" /></template>

<style scoped>.qr { display: block; border-radius: 6px; }</style>
