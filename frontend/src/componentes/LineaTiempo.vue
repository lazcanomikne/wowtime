<script setup lang="ts">
// Calendario de equipo (decisión 23): una fila por colaborador y una columna por día del mes.
import { computed } from 'vue'
import Persona from './Persona.vue'
import { DIAS_CAL, aFecha, iso } from '@/utils/formato'

const props = withDefaults(defineProps<{ filas: any[]; desde: string; hasta: string; festivos?: { fecha: string; nombre: string }[]; compacto?: boolean }>(), { festivos: () => [] })
const emit = defineEmits<{ abrir: [a: any] }>()

const dias = computed(() => {
  const r: { f: string; n: number; l: string; fds: boolean; hoy: boolean; fest: boolean }[] = []
  const a = aFecha(props.desde)!, b = aFecha(props.hasta)!
  const fest = new Set(props.festivos.map(f => f.fecha))
  for (let d = new Date(a); d <= b; d.setDate(d.getDate() + 1)) {
    const k = (d.getDay() + 6) % 7
    r.push({ f: iso(d), n: d.getDate(), l: DIAS_CAL[k], fds: k >= 5, hoy: iso(d) === iso(new Date()), fest: fest.has(iso(d)) })
  }
  return r
})
const COL: Record<string, string> = { Aprobada: 'apr', Pendiente: 'pen', Permiso: 'per', Incapacidad: 'inc' }
function barra(a: any) {
  const ini = a.inicio < props.desde ? props.desde : a.inicio
  const fin = a.fin > props.hasta ? props.hasta : a.fin
  const i = dias.value.findIndex(d => d.f === ini)
  const j = dias.value.findIndex(d => d.f === fin)
  return { gridColumn: `${i + 2} / ${j + 3}`, corta: j - i < 2 }
}
const plantilla = computed(() => `${props.compacto ? 190 : 230}px repeat(${dias.value.length}, minmax(24px, 1fr))`)
</script>

<template>
  <div class="lt-envoltura">
    <div class="lt" :style="{ minWidth: (compacto ? 190 : 230) + dias.length * 26 + 'px' }">
      <div class="fila-lt cab" :style="{ gridTemplateColumns: plantilla }">
        <div class="celda-nombre" />
        <div v-for="d in dias" :key="d.f" class="dia" :class="{ fds: d.fds, hoy: d.hoy, fest: d.fest }" :title="d.fest ? festivos.find(f => f.fecha === d.f)?.nombre : undefined">
          <span>{{ d.l }}</span><b>{{ d.n }}</b>
        </div>
      </div>
      <div v-for="f in filas" :key="f.colaborador.id" class="fila-lt" :style="{ gridTemplateColumns: plantilla }">
        <div class="celda-nombre"><Persona :p="f.colaborador" :sub="f.colaborador.area ?? f.equipo" :t="34" enlace /></div>
        <div v-for="(d, k) in dias" :key="d.f" class="fondo" :class="{ fds: d.fds, hoy: d.hoy, fest: d.fest }" :style="{ gridColumn: k + 2 }" />
        <button v-for="(a, k) in f.ausencias" :key="k" class="barra-lt" :class="[COL[a.tipo], { corta: barra(a).corta }]" :style="{ gridColumn: barra(a).gridColumn }"
                :title="`${a.texto}: ${a.inicio} a ${a.fin}`" @click="emit('abrir', a)">
          <span>{{ a.texto }}</span>
        </button>
      </div>
      <p v-if="!filas.length" class="vacio">No hay ausencias en este periodo.</p>
    </div>
  </div>
</template>

<style scoped>
.lt-envoltura { overflow-x: auto; -webkit-overflow-scrolling: touch; }
.lt { display: flex; flex-direction: column; }
.fila-lt { display: grid; align-items: center; min-height: 54px; border-bottom: 1px solid var(--gris-100); position: relative; }
.fila-lt.cab { min-height: 44px; border-bottom: 1px solid var(--gris-200); }
.celda-nombre { grid-column: 1; grid-row: 1; padding: 6px 10px 6px 0; position: sticky; left: 0; background: #fff; z-index: 2; height: 100%; display: flex; align-items: center; }
.dia { grid-row: 1; display: flex; flex-direction: column; align-items: center; font-size: 11.5px; color: var(--texto-3); line-height: 1.2; padding: 4px 0; }
.dia b { font-size: 13px; color: var(--titulo); font-weight: 600; }
.dia.fds { color: var(--gris-400); }
.dia.fds b { color: var(--texto-3); }
.dia.hoy b { color: #fff; background: var(--verde); border-radius: 50%; width: 22px; height: 22px; display: grid; place-items: center; }
.dia.fest b { color: var(--naranja); }
.fondo { grid-row: 1; align-self: stretch; }
.fondo.fds { background: var(--gris-50); }
.fondo.fest { background: #fff6ec; }
.fondo.hoy { background: rgba(10, 170, 74, .06); }
.barra-lt { grid-row: 1; height: 30px; border-radius: 7px; border: 0; margin: 0 2px; padding: 0 10px; font-size: 12.5px; font-weight: 600; text-align: left; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; z-index: 1; cursor: pointer; }
.barra-lt span { overflow: hidden; text-overflow: ellipsis; display: block; }
.barra-lt.corta { padding: 0 4px; font-size: 0; }
.apr { background: #d4f4e2; color: #087f38; border-left: 4px solid var(--verde); }
.pen { background: #fdf0cf; color: #946108; border-left: 4px solid #f3a51c; }
.per { background: #dde9fb; color: #2550a8; border-left: 4px solid var(--azul-punto); }
.inc { background: #fbdde0; color: #b11e24; border-left: 4px solid var(--rojo); }
</style>
