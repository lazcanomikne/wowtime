<script setup lang="ts">
// Pestaña "Asistencia": resumen del mes, detalle por día con incidencias y checada manual (RH).
import { computed, onMounted, reactive, ref } from 'vue'
import Icono from '@/componentes/Icono.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion } from '@/utils/usos'
import { DIAS, MESES_CORTOS, aFecha, cap, hora, iso, mesAnio, num } from '@/utils/formato'

interface Dia {
  fecha: string; entrada?: string | null; salida?: string | null; estado?: string | null; estadoTexto: string; modalidad?: string | null; origen?: string | null
  horas?: number | null; incidencias: { id: number; tipo: string; tipoTexto: string; estado: string }[]
}
interface Resumen { desde: string; hasta: string; diasTrabajados: number; enHorario: number; retardos: number; faltas: number; incidenciasAbiertas: number; puntualidad: number; dias: Dia[] }

const props = defineProps<{ ficha: any; id: number }>()
const emit = defineEmits<{ actualizar: [] }>()
const s = useSesion()
const ui = useUi()
const { ocupado, ejecutar } = useAccion()

const hoy = new Date()
const mes = ref(new Date(hoy.getFullYear(), hoy.getMonth(), 1))
const esMesActual = computed(() => mes.value.getFullYear() === hoy.getFullYear() && mes.value.getMonth() === hoy.getMonth())
const datos = ref<Resumen | null>(null)
const cargando = ref(false)

async function cargar() {
  cargando.value = true
  const ini = mes.value
  const fin = new Date(ini.getFullYear(), ini.getMonth() + 1, 0)
  try {
    datos.value = await api.get<Resumen>('/asistencia/resumen-colaborador', { colaboradorId: props.id, desde: iso(ini), hasta: iso(fin > hoy ? hoy : fin) })
  } catch (e) { ui.error(e) } finally { cargando.value = false }
}
onMounted(cargar)
function moverMes(n: number) {
  const m = new Date(mes.value.getFullYear(), mes.value.getMonth() + n, 1)
  if (m > hoy) return
  mes.value = m
  cargar()
}

const ORIGEN: Record<string, string> = { App: 'App móvil', Terminal: 'Terminal', Kiosco: 'Quiosco', Rh: 'Captura de RH' }
const ESTADO_INC: Record<string, string> = { PorRevisar: 'por revisar', EnRevision: 'en revisión', Justificada: 'justificada', Confirmada: 'confirmada' }
function diaTexto(f: string) {
  const d = aFecha(f)!
  return { dia: cap(DIAS[d.getDay()].slice(0, 3)), num: `${d.getDate()} ${MESES_CORTOS[d.getMonth()]}` }
}
function estadoDia(d: Dia): { v: string; texto?: string } {
  if (d.estado) return { v: d.estado }
  if (d.incidencias.some(i => i.tipo === 'Falta')) return { v: 'Falta' }
  if (d.estadoTexto === 'Sin registro') return { v: 'SinRegistro' }
  return { v: 'Ausencia', texto: d.estadoTexto }
}

const kpis = computed(() => {
  const r = datos.value
  return [
    { t: 'Días trabajados', v: num(r?.diasTrabajados ?? 0), de: null, icono: 'calendario', fondo: 'var(--verde-claro)', color: 'var(--verde-texto)' },
    { t: 'Puntualidad', v: `${r?.puntualidad ?? 0}%`, de: r ? `${r.enHorario} de ${r.diasTrabajados} en horario` : null, icono: 'reloj', fondo: '#d9f4f6', color: '#0b7a83', barra: r?.puntualidad ?? 0 },
    { t: 'Retardos', v: num(r?.retardos ?? 0), de: null, icono: 'alerta', fondo: 'var(--naranja-claro)', color: 'var(--naranja)' },
    { t: 'Faltas', v: num(r?.faltas ?? 0), de: r?.incidenciasAbiertas ? `${r.incidenciasAbiertas} incidencias por revisar` : null, icono: 'x', fondo: 'var(--rojo-claro)', color: 'var(--rojo)' },
  ]
})

// ───── Checada manual (RH) ─────
const puedeRegistrar = computed(() => s.tiene('asistencia.gestionar') && props.ficha?.estatus !== 'Baja')
const modal = ref(false)
const manual = reactive({ tipo: 'Entrada' as 'Entrada' | 'Salida', justificacion: '' })
const intento = ref(false)
function abrirManual() {
  const a = props.ficha?.asistenciaHoy
  manual.tipo = a?.entrada && !a?.salida ? 'Salida' : 'Entrada'
  manual.justificacion = ''
  intento.value = false
  modal.value = true
}
async function registrarManual() {
  intento.value = true
  if (!manual.justificacion.trim()) return
  await ejecutar(async () => {
    const r = await api.post('/asistencia/registros/manual', { colaboradorId: props.id, tipo: manual.tipo, justificacion: manual.justificacion.trim() })
    modal.value = false
    ui.avisar(`${manual.tipo} registrada a las ${hora(r.hora)}`)
    if (!esMesActual.value) mes.value = new Date(hoy.getFullYear(), hoy.getMonth(), 1)
    await cargar()
    emit('actualizar')
  })
}
</script>

<template>
  <div class="asistencia">
    <div class="barra-sup">
      <div class="sel-mes">
        <button class="btn-icono borde" aria-label="Mes anterior" @click="moverMes(-1)"><Icono n="izquierda" :t="20" /></button>
        <span class="mes">{{ mesAnio(mes) }}</span>
        <button class="btn-icono borde" aria-label="Mes siguiente" :disabled="esMesActual" @click="moverMes(1)"><Icono n="derecha" :t="20" /></button>
      </div>
      <button v-if="puedeRegistrar" class="btn btn-primario" @click="abrirManual"><Icono n="reloj" :t="19" />Registrar checada manual</button>
    </div>

    <div class="kpis">
      <section v-for="k in kpis" :key="k.t" class="tarjeta compacta kpi">
        <span class="kpi-icono" :style="{ background: k.fondo, color: k.color }"><Icono :n="k.icono" :t="28" /></span>
        <div class="crecer">
          <p class="kpi-titulo">{{ k.t }}</p>
          <p class="kpi-valor">{{ cargando && !datos ? '…' : k.v }}</p>
          <div v-if="k.barra !== undefined" class="barra verde" style="margin-top: 8px"><span :style="{ width: k.barra + '%' }" /></div>
          <p v-if="k.de" class="muted pequeno" style="margin-top: 4px">{{ k.de }}</p>
        </div>
      </section>
    </div>

    <section class="tarjeta">
      <div class="tarjeta-cabecera">
        <div><h2 class="titulo-tarjeta">Registro por día</h2><p class="subtitulo-tarjeta">Días laborables del periodo; los descansos sin registro no se muestran.</p></div>
      </div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead>
            <tr><th>Fecha</th><th>Entrada</th><th>Salida</th><th class="num">Horas</th><th>Modalidad</th><th>Estado</th><th>Incidencias</th></tr>
          </thead>
          <tbody v-if="cargando && !datos">
            <tr v-for="i in 6" :key="i"><td colspan="7"><div class="esqueleto" style="height: 28px" /></td></tr>
          </tbody>
          <tbody v-else :class="{ atenuado: cargando }">
            <tr v-for="d in datos?.dias" :key="d.fecha">
              <td class="nowrap"><b class="dia">{{ diaTexto(d.fecha).dia }}</b> {{ diaTexto(d.fecha).num }}</td>
              <td class="nowrap">
                <template v-if="d.entrada">{{ hora(d.entrada) }}<small v-if="d.origen" class="muted origen">{{ ORIGEN[d.origen] ?? d.origen }}</small></template>
                <span v-else class="muted">—</span>
              </td>
              <td class="nowrap">{{ d.salida ? hora(d.salida) : '—' }}</td>
              <td class="num">{{ d.horas != null ? num(d.horas, 1) : '—' }}</td>
              <td>{{ d.modalidad ?? '—' }}</td>
              <td><Estado :v="estadoDia(d).v" :texto="estadoDia(d).texto" /></td>
              <td>
                <div v-if="d.incidencias.length" class="fila envolver" style="gap: 6px">
                  <Estado v-for="i in d.incidencias" :key="i.id" :v="i.estado" :texto="`${i.tipoTexto} · ${ESTADO_INC[i.estado] ?? i.estado}`" :punto="false" />
                </div>
                <span v-else class="muted">—</span>
              </td>
            </tr>
            <tr v-if="datos && !datos.dias.length"><td colspan="7" class="vacio">Sin registros en este periodo.</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <Modal v-if="modal" titulo="Registrar checada manual" :subtitulo="ficha.nombre" :ancho="480" @cerrar="modal = false">
      <div class="columna" style="gap: 14px">
        <div class="campo">
          <span class="etiqueta-campo">Tipo de registro</span>
          <div class="tipos">
            <button type="button" class="tipo" :class="{ sel: manual.tipo === 'Entrada' }" @click="manual.tipo = 'Entrada'"><Icono n="salir" :t="18" style="transform: scaleX(-1)" />Entrada</button>
            <button type="button" class="tipo" :class="{ sel: manual.tipo === 'Salida' }" @click="manual.tipo = 'Salida'"><Icono n="salir" :t="18" />Salida</button>
          </div>
        </div>
        <div class="campo">
          <label for="m-just">Justificación</label>
          <textarea id="m-just" v-model="manual.justificacion" class="entrada" :class="{ error: intento && !manual.justificacion.trim() }" rows="3"
                    placeholder="Ej. La terminal no estaba disponible; se verificó su llegada con el supervisor." />
          <span v-if="intento && !manual.justificacion.trim()" class="error-campo">La justificación es obligatoria.</span>
        </div>
        <p class="caja-info"><Icono n="info" :t="20" />Se registra con la hora actual del servidor y queda con origen "Captura de RH" en el historial.</p>
      </div>
      <template #pie>
        <button class="btn btn-contorno" @click="modal = false">Cancelar</button>
        <button class="btn btn-primario" :disabled="ocupado" @click="registrarManual">Registrar {{ manual.tipo.toLowerCase() }}</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.asistencia { display: flex; flex-direction: column; gap: 20px; }
.barra-sup { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; }
.sel-mes { display: flex; align-items: center; gap: 10px; }
.sel-mes .btn-icono:disabled { opacity: .4; cursor: not-allowed; }
.mes { font-size: 19px; font-weight: 700; color: var(--titulo); min-width: 170px; text-align: center; }
.kpis { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 20px; }
.kpi .kpi-valor { font-size: 32px; }
.dia { font-weight: 650; color: var(--titulo); }
.origen { display: block; font-size: 12px; }
.nowrap { white-space: nowrap; }
.atenuado { opacity: .55; }
.tipos { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.tipo { display: flex; align-items: center; justify-content: center; gap: 8px; height: 46px; border-radius: var(--radio-s); border: 1px solid var(--gris-300); background: #fff; color: var(--titulo); font-weight: 500; }
.tipo.sel { border-color: var(--verde); background: var(--verde-suave); color: var(--verde-texto); font-weight: 650; box-shadow: 0 0 0 1px var(--verde) inset; }
@media (max-width: 1100px) { .kpis { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 560px) {
  .kpis { grid-template-columns: 1fr 1fr; gap: 12px; }
  .kpi { flex-direction: column; gap: 10px; }
  .kpi .kpi-icono { width: 44px; height: 44px; }
  .kpi .kpi-valor { font-size: 26px; }
  .barra-sup .btn { width: 100%; }
}
</style>
