<script setup lang="ts">
// Componente único de calendario: semana de lunes a domingo, encabezados L M X J V S D, selección de rango con dos clics.
import { computed } from 'vue'
import Icono from './Icono.vue'
import { DIAS_CAL, iso, mesAnio } from '@/utils/formato'

const props = withDefaults(defineProps<{
  mes: Date
  inicio?: string | null
  fin?: string | null
  descanso?: number[]
  festivos?: string[]
  variante?: 'web' | 'movil'
  seleccionable?: boolean
  minimo?: string | null
}>(), { descanso: () => [6, 7], festivos: () => [], variante: 'web', seleccionable: true })
const emit = defineEmits<{ 'update:mes': [d: Date]; rango: [inicio: string, fin: string | null] }>()

const celdas = computed(() => {
  const a = props.mes.getFullYear(), m = props.mes.getMonth()
  const primero = new Date(a, m, 1)
  const desplaz = (primero.getDay() + 6) % 7
  const inicio = new Date(a, m, 1 - desplaz)
  const total = Math.ceil((desplaz + new Date(a, m + 1, 0).getDate()) / 7) * 7
  return Array.from({ length: total }, (_, i) => {
    const d = new Date(inicio); d.setDate(inicio.getDate() + i)
    const f = iso(d)
    const diaIso = ((d.getDay() + 6) % 7) + 1
    const ini = props.inicio, fin = props.fin ?? props.inicio
    return {
      f, dia: d.getDate(), otroMes: d.getMonth() !== m, descanso: props.descanso.includes(diaIso), festivo: props.festivos.includes(f),
      extremo: f === ini || f === fin, enRango: !!ini && !!fin && f > ini && f < fin, hoy: f === iso(new Date()),
      bloqueado: !!props.minimo && f < props.minimo
    }
  })
})

function clic(c: { f: string; bloqueado: boolean }) {
  if (!props.seleccionable || c.bloqueado) return
  if (!props.inicio || (props.inicio && props.fin && props.fin !== props.inicio) || c.f < props.inicio) emit('rango', c.f, null)
  else emit('rango', props.inicio, c.f)
}
function mover(n: number) { emit('update:mes', new Date(props.mes.getFullYear(), props.mes.getMonth() + n, 1)) }
</script>

<template>
  <div class="cal" :class="variante">
    <div class="cal-cab">
      <button type="button" class="nav" aria-label="Mes anterior" @click="mover(-1)"><Icono n="izquierda" :t="20" /></button>
      <span class="mes">{{ mesAnio(mes) }}</span>
      <button type="button" class="nav" aria-label="Mes siguiente" @click="mover(1)"><Icono n="derecha" :t="20" /></button>
    </div>
    <div class="rejilla">
      <span v-for="d in DIAS_CAL" :key="d" class="dsem">{{ d }}</span>
      <button v-for="c in celdas" :key="c.f" type="button" class="dia"
              :class="{ otro: c.otroMes, desc: c.descanso, fest: c.festivo, ext: c.extremo && !c.otroMes, rango: c.enRango && !c.otroMes, hoy: c.hoy, bloq: c.bloqueado }"
              :title="c.festivo ? 'Día festivo' : undefined" :data-fecha="c.f" @click="clic(c)">
        {{ c.dia }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.cal.web { border: 1px solid var(--gris-200); border-radius: 12px; padding: 14px 10px 12px; background: #fff; }
.cal-cab { display: flex; align-items: center; justify-content: space-between; padding: 0 6px 12px; }
.mes { font-size: 17.5px; font-weight: 650; color: var(--titulo); }
.nav { background: none; border: 0; color: var(--titulo); width: 34px; height: 34px; border-radius: 8px; display: grid; place-items: center; }
.nav:hover { background: var(--gris-100); }
.rejilla { display: grid; grid-template-columns: repeat(7, 1fr); gap: 5px; }
.dsem { text-align: center; font-size: 14px; color: var(--texto-2); padding: 6px 0 8px; font-weight: 500; }
.dia { height: 42px; border: 0; border-radius: 8px; background: transparent; font-size: 15.5px; color: var(--titulo); position: relative; font-variant-numeric: tabular-nums; }
.dia:hover:not(.bloq) { background: var(--gris-100); }
.dia.desc { background: var(--gris-50); color: var(--texto-3); }
.dia.otro { color: var(--gris-300); background: var(--gris-50); }
.dia.fest::after { content: ''; position: absolute; bottom: 5px; left: 50%; width: 5px; height: 5px; margin-left: -2.5px; border-radius: 50%; background: var(--naranja); }
.dia.rango { background: var(--verde-claro); color: var(--titulo); font-weight: 600; }
.dia.ext, .dia.ext:hover { background: var(--verde-osc) !important; color: #fff; font-weight: 700; }
.dia.rango:hover { background: #c8f0da !important; }
.dia.hoy:not(.ext)::before { content: ''; position: absolute; inset: 2px; border: 1.5px solid var(--verde); border-radius: 7px; }
.dia.bloq { color: var(--gris-300); cursor: not-allowed; }
/* Variante móvil: días circulares como en App_Movil.png */
.cal.movil .mes { font-size: 16px; }
.cal.movil .dsem { font-size: 12.5px; color: var(--texto-3); }
.cal.movil .rejilla { gap: 2px 0; }
.cal.movil .dia { height: 38px; border-radius: 50%; width: 38px; justify-self: center; font-size: 14.5px; background: transparent; }
.cal.movil .dia.otro { visibility: hidden; }
.cal.movil .dia.desc { color: var(--texto-3); background: transparent; }
.cal.movil .dia.rango, .cal.movil .dia.ext { background: var(--verde-osc); color: #fff; }
.cal.movil .dia.rango { box-shadow: -10px 0 0 var(--verde-claro), 10px 0 0 var(--verde-claro); }
.cal.movil .dia.hoy:not(.ext)::before { border-radius: 50%; }
</style>
