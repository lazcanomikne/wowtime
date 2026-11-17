<script setup lang="ts">
// Lector de QR con la cámara (getUserMedia + jsQR). Requiere HTTPS o localhost.
import { onBeforeUnmount, onMounted, ref } from 'vue'
import jsQR from 'jsqr'
import Icono from './Icono.vue'

const emit = defineEmits<{ leido: [texto: string] }>()
const video = ref<HTMLVideoElement>()
const error = ref<string | null>(null)
const activo = ref(false)
let stream: MediaStream | null = null
let raf = 0
let ultimo = ''
let ultimoT = 0
const lienzo = document.createElement('canvas')

async function iniciar() {
  error.value = null
  if (!navigator.mediaDevices?.getUserMedia) { error.value = 'Este navegador no permite usar la cámara. Usa HTTPS o escribe el código.'; return }
  try {
    stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false })
    video.value!.srcObject = stream
    await video.value!.play()
    activo.value = true
    leer()
  } catch (e: any) {
    error.value = e?.name === 'NotAllowedError' ? 'Permiso de cámara denegado. Actívalo en la configuración del navegador o escribe el código.' : 'No se pudo abrir la cámara. Escribe el código manualmente.'
  }
}
function leer() {
  const v = video.value
  if (v && v.readyState === v.HAVE_ENOUGH_DATA) {
    lienzo.width = v.videoWidth; lienzo.height = v.videoHeight
    const ctx = lienzo.getContext('2d', { willReadFrequently: true })!
    ctx.drawImage(v, 0, 0)
    const img = ctx.getImageData(0, 0, lienzo.width, lienzo.height)
    const r = jsQR(img.data, img.width, img.height, { inversionAttempts: 'dontInvert' })
    const ahora = Date.now()
    if (r?.data && (r.data !== ultimo || ahora - ultimoT > 4000)) { ultimo = r.data; ultimoT = ahora; emit('leido', r.data) }
  }
  raf = requestAnimationFrame(leer)
}
function detener() { cancelAnimationFrame(raf); stream?.getTracks().forEach(t => t.stop()); stream = null; activo.value = false }
onMounted(iniciar)
onBeforeUnmount(detener)
defineExpose({ iniciar, detener })
</script>

<template>
  <div class="lector">
    <video ref="video" playsinline muted />
    <div v-if="activo" class="marco"><span /><span /><span /><span /></div>
    <div v-if="error" class="err"><Icono n="camara" :t="34" /><p>{{ error }}</p><button class="btn btn-contorno btn-ch" @click="iniciar">Reintentar</button></div>
  </div>
</template>

<style scoped>
.lector { position: relative; background: #0c1430; border-radius: 14px; overflow: hidden; aspect-ratio: 4 / 3; }
video { width: 100%; height: 100%; object-fit: cover; display: block; }
.marco { position: absolute; inset: 18%; }
.marco span { position: absolute; width: 34px; height: 34px; border: 4px solid var(--verde); }
.marco span:nth-child(1) { top: 0; left: 0; border-right: 0; border-bottom: 0; border-radius: 10px 0 0 0; }
.marco span:nth-child(2) { top: 0; right: 0; border-left: 0; border-bottom: 0; border-radius: 0 10px 0 0; }
.marco span:nth-child(3) { bottom: 0; left: 0; border-right: 0; border-top: 0; border-radius: 0 0 0 10px; }
.marco span:nth-child(4) { bottom: 0; right: 0; border-left: 0; border-top: 0; border-radius: 0 0 10px 0; }
.err { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; color: #fff; text-align: center; padding: 24px; font-size: 14.5px; }
</style>
