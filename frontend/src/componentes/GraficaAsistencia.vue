<script setup lang="ts">
// Gráfica de línea "Asistencia en tiempo real": presentes por hora (entradas menos salidas acumuladas).
import { computed, ref } from 'vue'

const props = defineProps<{ puntos: { hora: string; presentes: number | null }[] }>()
const W = 520, H = 196, ML = 34, MR = 12, MT = 14, MB = 30
const activo = ref<number | null>(null)

const validos = computed(() => props.puntos.map((p, i) => ({ ...p, i })).filter(p => p.presentes !== null && p.hora.endsWith(':00')) as { hora: string; presentes: number; i: number }[])
const maxY = computed(() => {
  const m = Math.max(10, ...validos.value.map(p => p.presentes))
  const paso = m <= 50 ? 10 : m <= 100 ? 20 : m <= 200 ? 40 : 100
  return Math.ceil(m / paso) * paso
})
const ticksY = computed(() => Array.from({ length: 6 }, (_, k) => Math.round((maxY.value / 5) * k)))
const minutos = (h: string) => { const [a, b] = h.split(':').map(Number); return a * 60 + b }
const x = (h: string) => ML + ((minutos(h) - 360) / (840)) * (W - ML - MR)
const y = (v: number) => MT + (1 - v / maxY.value) * (H - MT - MB)
const ruta = computed(() => {
  const p = validos.value
  if (p.length < 2) return ''
  let d = `M ${x(p[0].hora)} ${y(p[0].presentes)}`
  for (let k = 1; k < p.length; k++) {
    const a = p[k - 1], b = p[k]
    const cx = (x(a.hora) + x(b.hora)) / 2
    d += ` C ${cx} ${y(a.presentes)}, ${cx} ${y(b.presentes)}, ${x(b.hora)} ${y(b.presentes)}`
  }
  return d
})
const etiquetasX = ['6:00', '8:00', '10:00', '12:00', '14:00', '16:00', '18:00', '20:00']
const pico = computed(() => validos.value.reduce<null | { hora: string; presentes: number }>((m, p) => (!m || p.presentes > m.presentes ? p : m), null))
const mostrado = computed(() => (activo.value !== null ? validos.value.find(p => p.i === activo.value) : pico.value) ?? null)
const hora12 = (h: string) => h.replace(/^0/, '')
</script>

<template>
  <div class="grafica">
    <svg :viewBox="`0 0 ${W} ${H}`" preserveAspectRatio="none" role="img" aria-label="Colaboradores presentes por hora" @mouseleave="activo = null">
      <g v-for="t in ticksY" :key="t">
        <line :x1="ML" :x2="W - MR" :y1="y(t)" :y2="y(t)" class="rej" />
        <text :x="ML - 8" :y="y(t) + 4" class="ejeY">{{ t }}</text>
      </g>
      <line v-for="e in etiquetasX" :key="'v' + e" :x1="x(e.padStart(5, '0'))" :x2="x(e.padStart(5, '0'))" :y1="MT" :y2="H - MB" class="rej v" />
      <text v-for="e in etiquetasX" :key="e" :x="x(e.padStart(5, '0'))" :y="H - 8" class="ejeX">{{ e }}</text>
      <line class="base" :x1="ML" :x2="W - MR" :y1="H - MB" :y2="H - MB" />
      <path v-if="ruta" :d="ruta" class="linea" />
      <line v-if="mostrado" :x1="x(mostrado.hora)" :x2="x(mostrado.hora)" :y1="y(mostrado.presentes)" :y2="H - MB" class="guia" />
      <g v-for="p in validos" :key="p.hora">
        <circle :cx="x(p.hora)" :cy="y(p.presentes)" :r="mostrado?.hora === p.hora ? 5.5 : 3.6" class="punto" :class="{ act: mostrado?.hora === p.hora }" />
        <rect :x="x(p.hora) - 14" :y="MT" width="28" :height="H - MT - MB" fill="transparent" @mouseenter="activo = p.i" />
      </g>
    </svg>
    <div v-if="mostrado" class="globo" :class="{ abajo: y(mostrado.presentes) < H * 0.35 }" :style="{ left: `${(x(mostrado.hora) / W) * 100}%`, top: `${(y(mostrado.presentes) / H) * 100}%` }">
      <span>{{ hora12(mostrado.hora) }}</span><b>{{ mostrado.presentes }} colaboradores</b>
    </div>
    <p v-if="!validos.length" class="sin-datos">Aún no hay registros de asistencia hoy.</p>
  </div>
</template>

<style scoped>
.grafica { position: relative; width: 100%; }
svg { width: 100%; height: 196px; display: block; overflow: visible; }
.rej { stroke: #e9edf2; stroke-width: 1; }
.rej.v { stroke: #f1f4f7; }
.base { stroke: #cfd6df; }
.ejeY { font-size: 11px; fill: #6b7385; text-anchor: end; }
.ejeX { font-size: 11px; fill: #6b7385; text-anchor: middle; }
.linea { fill: none; stroke: #0ca47c; stroke-width: 2.6; stroke-linecap: round; }
.punto { fill: #0ca47c; stroke: #fff; stroke-width: 1.5; transition: r .15s; }
.punto.act { stroke-width: 2.5; }
.guia { stroke: #0ca47c; stroke-width: 1.2; stroke-dasharray: 4 4; }
.globo { position: absolute; transform: translate(12px, -110%); background: #fff; border: 1px solid var(--gris-200); box-shadow: var(--sombra); border-radius: 8px; padding: 7px 11px; font-size: 12.5px; color: var(--texto-2); white-space: nowrap; pointer-events: none; display: flex; flex-direction: column; line-height: 1.35; }
.globo b { color: var(--titulo); font-weight: 650; }
.globo.abajo { transform: translate(12px, 10px); }
.sin-datos { position: absolute; inset: 0; display: grid; place-items: center; color: var(--texto-3); font-size: 14px; }
</style>
