<script setup lang="ts">
// Solicitar vacaciones en móvil (App_Movil.png vista 4 y Solicitar_vacaciones.png). Todo se calcula con POST /solicitudes/simular.
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Calendario from '@/componentes/Calendario.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { debounce } from '@/utils/usos'
import { iso, num } from '@/utils/formato'

const s = useSesion()
const ui = useUi()
const router = useRouter()
const saldo = ref<any>(null)
const mes = ref(new Date())
const inicio = ref<string | null>(null)
const fin = ref<string | null>(null)
const sim = ref<any>(null)
const rutaTexto = ref('')
const cobertura = ref(false)
const coberturaId = ref<number | null>(null)
const coberturaNota = ref('')
const comentario = ref('')
const enviando = ref(false)

onMounted(async () => {
  try { saldo.value = await api.get('/vacaciones/saldo') } catch (e) { ui.error(e) }
  const m = new Date(); m.setDate(m.getDate() + 1)
  try { rutaTexto.value = (await api.post('/solicitudes/simular', { tipo: 'Vacaciones', fechaInicio: iso(m), fechaFin: iso(m) })).rutaTexto } catch { /* sin ruta */ }
})
const simular = debounce(async () => {
  if (!inicio.value) { sim.value = null; return }
  try {
    sim.value = await api.post('/solicitudes/simular', { tipo: 'Vacaciones', fechaInicio: inicio.value, fechaFin: fin.value ?? inicio.value, coberturaColaboradorId: coberturaId.value })
    if (sim.value.rutaTexto) rutaTexto.value = sim.value.rutaTexto
  } catch (e) { ui.error(e) }
}, 200)
watch([inicio, fin, coberturaId], simular)
const errores = computed(() => (sim.value?.validaciones ?? []).filter((v: any) => v.nivel !== 'ok'))
const fmtDel = computed(() => {
  if (!sim.value) return ''
  const t = sim.value.periodoTexto as string
  return t.includes('–') ? `Del ${t.replace('–', ' al ')}` : `El ${t}`
})

async function enviar() {
  enviando.value = true
  try {
    const r = await api.post('/solicitudes', { tipo: 'Vacaciones', fechaInicio: inicio.value, fechaFin: fin.value ?? inicio.value, coberturaColaboradorId: coberturaId.value, coberturaNota: coberturaNota.value || null, comentario: comentario.value || null })
    ui.avisar(`Solicitud ${r.folio} enviada`)
    s.cargar().catch(() => {})
    router.push(`/solicitudes/${r.id}`)
  } catch (e) { ui.error(e) } finally { enviando.value = false }
}
</script>

<template>
  <div class="sv">
    <EncabezadoMovil titulo="Solicitar vacaciones" />
    <div class="cuerpo">
      <div class="saldo">
        <Icono n="calendario" :t="32" :g="1.8" />
        <div><b>{{ num(saldo?.disponibles ?? s.me?.saldo?.disponibles, 1) }} días disponibles</b><span>Saldo actual</span></div>
      </div>

      <div v-if="s.me?.colaborador?.sinSupervisor" class="caja-alerta aviso"><Icono n="alerta" :t="20" />No tienes un supervisor asignado; no puedes enviar solicitudes. Recursos Humanos ya fue notificado.</div>

      <div class="tarjeta cal">
        <Calendario v-model:mes="mes" variante="movil" :inicio="inicio" :fin="fin" :descanso="sim?.diasDescanso ?? [6, 7]" :festivos="(sim?.festivos ?? []).map((f: any) => f.fecha)" :minimo="iso(new Date())" @rango="(a, b) => { inicio = a; fin = b }" />
      </div>

      <div class="tarjeta resumen">
        <Icono n="vacaciones" :t="30" :g="1.9" class="avion" />
        <div v-if="sim">
          <b>{{ sim.diasHabiles }} {{ sim.diasHabiles === 1 ? 'día hábil' : 'días hábiles' }}</b>
          <span>{{ fmtDel }}</span>
          <b class="despues">Saldo después: {{ num(sim.saldoPosterior, 1) }} {{ Math.abs(sim.saldoPosterior) === 1 ? 'día' : 'días' }}</b>
        </div>
        <div v-else><b>Elige tus fechas</b><span>Toca el día de inicio y luego el de término.</span></div>
      </div>

      <ul v-if="errores.length" class="validaciones">
        <li v-for="v in errores" :key="v.mensaje" :class="v.nivel"><Icono :n="v.nivel === 'error' ? 'alertaCirculo' : 'alerta'" :t="17" />{{ v.mensaje }}</li>
      </ul>

      <button class="mas-op" @click="cobertura = !cobertura"><Icono n="personas" :t="20" />Cobertura y comentario (opcional)<Icono :n="cobertura ? 'arriba' : 'abajo'" :t="18" /></button>
      <div v-if="cobertura" class="columna">
        <SelectorColaborador v-model="coberturaId" placeholder="¿Quién cubre tus actividades?" :excluir="s.me?.colaborador ? [s.me.colaborador.id] : []" />
        <textarea v-model="coberturaNota" class="entrada" rows="2" placeholder="Ej. Entregar pendientes a mi responsable..." />
        <textarea v-model="comentario" class="entrada" rows="2" placeholder="Comentario. Ej. Viaje familiar..." />
      </div>

      <button class="btn btn-primario btn-g btn-bloque" :disabled="!sim?.valida || enviando" data-prueba="enviar-solicitud-movil" @click="enviar">
        Enviar solicitud <Icono n="flecha" :t="20" />
      </button>
      <p v-if="rutaTexto" class="ruta"><Icono n="personas" :t="18" />Revisará: {{ rutaTexto }}</p>
    </div>
  </div>
</template>

<style scoped>
.sv { min-height: 100%; background: var(--fondo); }
.cuerpo { padding: 14px; display: flex; flex-direction: column; gap: 12px; max-width: 560px; margin: 0 auto; }
.saldo { display: flex; align-items: center; gap: 14px; background: var(--verde-claro); border-radius: 12px; padding: 14px 16px; color: var(--verde-osc); }
.saldo b { display: block; font-size: 18px; color: var(--verde-texto); }
.saldo span { font-size: 13.5px; color: var(--verde-texto); }
.cal { padding: 10px 6px; }
.resumen { display: flex; gap: 14px; align-items: flex-start; padding: 14px 16px; }
.avion { color: var(--verde-osc); margin-top: 2px; }
.resumen b { display: block; font-size: 16px; color: var(--titulo); }
.resumen span { display: block; font-size: 13.5px; color: var(--texto-2); }
.resumen .despues { margin-top: 6px; font-size: 15px; }
.validaciones { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 6px; }
.validaciones li { display: flex; gap: 8px; align-items: center; font-size: 13.5px; padding: 8px 12px; border-radius: 8px; }
.validaciones .error { background: var(--rojo-claro); color: var(--rojo-texto); }
.validaciones .aviso { background: var(--ambar-claro); color: #8a5a0b; }
.mas-op { display: flex; align-items: center; gap: 10px; background: none; border: 0; color: var(--texto-2); font-size: 14px; padding: 4px 2px; }
.mas-op .icono:last-child { margin-left: auto; }
.ruta { display: flex; justify-content: center; align-items: center; gap: 8px; color: var(--texto-2); font-size: 14px; }
.aviso { font-size: 13.5px; color: var(--rojo-texto); padding: 10px 12px; }
</style>
