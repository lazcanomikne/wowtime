<script setup lang="ts">
// Mapa con Leaflet y teselas de OpenStreetMap (sin llave de pago). Muestra la geocerca y la posición.
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'

const props = withDefaults(defineProps<{
  centro?: [number, number] | null; radio?: number | null; posicion?: [number, number] | null; dentro?: boolean | null; icono?: 'casa' | 'edificio'; alto?: number; editable?: boolean
}>(), { alto: 200, icono: 'casa' })
const emit = defineEmits<{ mover: [lat: number, lng: number] }>()
const el = ref<HTMLDivElement>()
let mapa: L.Map | null = null
let capa: L.LayerGroup | null = null

const pin = (tipo: string) => L.divIcon({
  className: 'pin-wt', iconSize: [34, 34], iconAnchor: [17, 17],
  html: `<div class="pin-c"><svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">${tipo === 'casa' ? '<path d="M3 10.5 12 3l9 7.5"/><path d="M5 9.5V21h14V9.5"/>' : '<rect x="5" y="3" width="14" height="18" rx="1"/><path d="M9 7h2M13 7h2M9 11h2M13 11h2"/>'}</svg></div>`
})
const yo = L.divIcon({ className: 'pin-wt', iconSize: [18, 18], iconAnchor: [9, 9], html: '<div class="pin-yo"></div>' })

function dibujar() {
  if (!mapa || !capa) return
  capa.clearLayers()
  const puntos: L.LatLngExpression[] = []
  if (props.centro) {
    if (props.radio) L.circle(props.centro, { radius: props.radio, color: '#0aaa4a', weight: 2, fillColor: '#0aaa4a', fillOpacity: 0.16 }).addTo(capa)
    const m = L.marker(props.centro, { icon: pin(props.icono), draggable: props.editable }).addTo(capa)
    if (props.editable) m.on('dragend', () => { const p = m.getLatLng(); emit('mover', p.lat, p.lng) })
    puntos.push(props.centro)
  }
  if (props.posicion) { L.marker(props.posicion, { icon: yo }).addTo(capa); puntos.push(props.posicion) }
  if (props.centro && props.radio) mapa.fitBounds(L.latLng(props.centro).toBounds(props.radio * 2.6), { maxZoom: 17 })
  else if (puntos.length) mapa.setView(puntos[0], 16)
  if (props.posicion && props.centro && props.dentro === false) mapa.fitBounds(L.latLngBounds(puntos).pad(0.4))
}

onMounted(() => {
  mapa = L.map(el.value!, { zoomControl: false, attributionControl: true }).setView(props.centro ?? props.posicion ?? [25.6694, -100.3097], 15)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '© OpenStreetMap' }).addTo(mapa)
  capa = L.layerGroup().addTo(mapa)
  if (props.editable) mapa.on('click', (e: L.LeafletMouseEvent) => emit('mover', e.latlng.lat, e.latlng.lng))
  dibujar()
})
watch(() => [props.centro, props.radio, props.posicion, props.dentro], dibujar, { deep: true })
onBeforeUnmount(() => { mapa?.remove(); mapa = null })
</script>

<template>
  <div class="mapa-wt"><div ref="el" :style="{ height: alto + 'px' }" /><slot /></div>
</template>

<style>
.mapa-wt { position: relative; border-radius: 12px; overflow: hidden; border: 1px solid var(--gris-200); }
.mapa-wt .leaflet-container { font-family: var(--fuente); background: #eef1f4; }
.pin-wt { background: none; border: 0; }
.pin-c { width: 34px; height: 34px; border-radius: 50%; background: var(--verde); display: grid; place-items: center; box-shadow: 0 0 0 4px #fff, 0 2px 8px rgba(0,0,0,.25); }
.pin-yo { width: 18px; height: 18px; border-radius: 50%; background: #2f74f3; border: 3px solid #fff; box-shadow: 0 0 0 6px rgba(47,116,243,.22); }
</style>
