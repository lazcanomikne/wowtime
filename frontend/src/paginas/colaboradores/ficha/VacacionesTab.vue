<script setup lang="ts">
// Pestaña "Vacaciones": saldo y periodos, historial de solicitudes, movimientos de saldo e incapacidades.
import { computed, onMounted, reactive, ref } from 'vue'
import Icono from '@/componentes/Icono.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import { api, type Pagina } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion } from '@/utils/usos'
import { dias, fechaCorta, fechaMedia, iso, momento, num, rango } from '@/utils/formato'

interface Saldo {
  disponibles: number; comprometidos: number; usados: number; otorgados: number; porVencer: number; fechaLimiteProxima?: string | null
  diasProximoVencimiento: number; fechaProximoVencimiento?: string | null; politica?: { id: number; nombre: string; plazoVencimientoMeses: number } | null
  periodos: { id: number; periodo: number; fechaOtorgamiento: string; fechaLimite: string; diasOtorgados: number; diasUsados: number; diasComprometidos: number; diasVencidos: number; disponibles: number; vigente: boolean }[]
}
interface Solicitud { id: number; folio: string; tipo: string; tipoTexto: string; fechaInicio: string; fechaFin: string; diasHabiles: number; estado: string; creadoEn: string; pasoActualTexto?: string | null }
interface Movimiento { id: number; tipo: string; dias: number; fecha: string; motivo?: string | null; solicitudId?: number | null }
interface Incapacidad { id: number; fechaInicio: string; fechaFin: string; folio?: string | null; tipo?: string | null; nota?: string | null; cancelada: boolean; dias: number }

const props = defineProps<{ ficha: any; id: number }>()
const emit = defineEmits<{ actualizar: [] }>()
const s = useSesion()
const ui = useUi()
const { ocupado, ejecutar } = useAccion()
const esRh = computed(() => s.tiene('vacaciones.gestionar'))
const hoy = iso(new Date())

const saldo = ref<Saldo | null>(null)
const solicitudes = ref<Pagina<Solicitud> | null>(null)
const movimientos = ref<Pagina<Movimiento> | null>(null)
const incapacidades = ref<Pagina<Incapacidad> | null>(null)
const pagSol = ref(1), pagMov = ref(1), pagInc = ref(1)

const cargarSaldo = async () => { try { saldo.value = await api.get<Saldo>('/vacaciones/saldo', { colaboradorId: props.id }) } catch (e) { ui.error(e) } }
const cargarSolicitudes = async () => { try { solicitudes.value = await api.get('/solicitudes', { colaboradorId: props.id, pagina: pagSol.value, tamano: 8 }) } catch (e) { ui.error(e) } }
const cargarMovimientos = async () => { try { movimientos.value = await api.get('/vacaciones/movimientos', { colaboradorId: props.id, pagina: pagMov.value, tamano: 8 }) } catch (e) { ui.error(e) } }
const cargarIncapacidades = async () => { try { incapacidades.value = await api.get('/incapacidades', { colaboradorId: props.id, pagina: pagInc.value, tamano: 8 }) } catch (e) { ui.error(e) } }
onMounted(() => { cargarSaldo(); cargarSolicitudes(); cargarMovimientos(); cargarIncapacidades() })
const irSol = (p: number) => { pagSol.value = p; cargarSolicitudes() }
const irMov = (p: number) => { pagMov.value = p; cargarMovimientos() }
const irInc = (p: number) => { pagInc.value = p; cargarIncapacidades() }

const MOV: Record<string, { t: string; signo: 1 | -1 | 0; color: string }> = {
  Otorgamiento: { t: 'Otorgamiento', signo: 1, color: 'verde' }, Liberacion: { t: 'Liberación', signo: 1, color: 'azul' },
  Compromiso: { t: 'Compromiso', signo: -1, color: 'ambar' }, Consumo: { t: 'Consumo', signo: -1, color: 'gris' },
  Vencimiento: { t: 'Vencimiento', signo: -1, color: 'rojo' }, Ajuste: { t: 'Ajuste', signo: 0, color: 'azul' },
}
function diasMov(m: Movimiento) {
  const info = MOV[m.tipo]
  const v = info?.signo === 0 || !info ? Number(m.dias) : Math.abs(Number(m.dias)) * info.signo
  return { texto: `${v > 0 ? '+' : v < 0 ? '−' : ''}${num(Math.abs(v), 1)}`, positivo: v > 0 }
}
function estadoInc(i: Incapacidad) {
  if (i.cancelada) return 'Cancelada'
  if (i.fechaFin < hoy) return 'Concluida'
  if (i.fechaInicio > hoy) return 'Programada'
  return 'Vigente'
}

// ───── Ajuste de saldo ─────
const modal = ref<'' | 'ajuste' | 'incapacidad'>('')
const ajuste = reactive({ dias: null as number | null, motivo: '' })
const errAj = ref<Record<string, string>>({})
const resultante = computed(() => saldo.value && typeof ajuste.dias === 'number' ? Number(saldo.value.disponibles) + ajuste.dias : null)
function abrirAjuste() { ajuste.dias = null; ajuste.motivo = ''; errAj.value = {}; modal.value = 'ajuste' }
async function guardarAjuste() {
  const er: Record<string, string> = {}
  if (typeof ajuste.dias !== 'number' || !ajuste.dias) er.dias = 'Escribe los días a sumar (positivo) o restar (negativo).'
  else if (resultante.value !== null && resultante.value < 0) er.dias = 'El ajuste dejaría el saldo en negativo.'
  if (!ajuste.motivo.trim()) er.motivo = 'El motivo del ajuste es obligatorio.'
  errAj.value = er
  if (Object.keys(er).length) return
  await ejecutar(async () => {
    await api.post(`/vacaciones/saldos/${props.id}/ajuste`, { dias: ajuste.dias, motivo: ajuste.motivo.trim() })
    modal.value = ''
    pagMov.value = 1
    await Promise.all([cargarSaldo(), cargarMovimientos()])
    emit('actualizar')
  }, 'Saldo ajustado')
}

// ───── Incapacidades ─────
const TIPOS_INC = ['Enfermedad general', 'Riesgo de trabajo', 'Maternidad']
const inc = reactive({ fechaInicio: hoy, fechaFin: hoy, folio: '', tipo: TIPOS_INC[0], nota: '' })
const errInc = ref<Record<string, string>>({})
const diasInc = computed(() => {
  if (!inc.fechaInicio || !inc.fechaFin || inc.fechaFin < inc.fechaInicio) return 0
  return Math.round((new Date(inc.fechaFin).getTime() - new Date(inc.fechaInicio).getTime()) / 86400000) + 1
})
function abrirIncapacidad() { Object.assign(inc, { fechaInicio: hoy, fechaFin: hoy, folio: '', tipo: TIPOS_INC[0], nota: '' }); errInc.value = {}; modal.value = 'incapacidad' }
async function guardarIncapacidad() {
  const er: Record<string, string> = {}
  if (!inc.fechaInicio) er.fechaInicio = 'Indica la fecha de inicio.'
  if (!inc.fechaFin) er.fechaFin = 'Indica la fecha de término.'
  else if (inc.fechaFin < inc.fechaInicio) er.fechaFin = 'La fecha de término no puede ser anterior a la de inicio.'
  errInc.value = er
  if (Object.keys(er).length) return
  await ejecutar(async () => {
    await api.post('/incapacidades', { colaboradorId: props.id, fechaInicio: inc.fechaInicio, fechaFin: inc.fechaFin, folio: inc.folio.trim() || null, tipo: inc.tipo || null, nota: inc.nota.trim() || null })
    modal.value = ''
    pagInc.value = 1
    await cargarIncapacidades()
    emit('actualizar')
  }, 'Incapacidad registrada')
}
async function cancelarIncapacidad(i: Incapacidad) {
  const r = await ui.confirmar({ titulo: 'Cancelar incapacidad', mensaje: `${rango(i.fechaInicio, i.fechaFin, true)}${i.folio ? ` · folio ${i.folio}` : ''}. Los días volverán a contar como laborables.`, aceptar: 'Cancelar incapacidad', cancelar: 'Volver', peligro: true })
  if (!r.ok) return
  await ejecutar(async () => { await api.post(`/incapacidades/${i.id}/cancelar`); await cargarIncapacidades(); emit('actualizar') }, 'Incapacidad cancelada')
}
</script>

<template>
  <div class="vacaciones">
    <div class="superior">
      <section class="tarjeta resumen">
        <div class="tarjeta-cabecera">
          <h2 class="titulo-tarjeta">Saldo de vacaciones</h2>
          <span v-if="saldo?.politica" class="chip pequeno"><Icono n="documento" :t="16" />{{ saldo.politica.nombre }}</span>
        </div>
        <div v-if="!saldo" class="esqueleto" style="height: 120px" />
        <template v-else>
          <div class="fila" style="gap: 18px">
            <span class="vac-ico"><Icono n="sombrilla" :t="38" :g="1.7" /></span>
            <div><b class="dias-g">{{ dias(saldo.disponibles) }}</b><span class="disp-t">disponibles</span></div>
          </div>
          <div class="cifras">
            <div><span>Otorgados</span><b>{{ num(saldo.otorgados, 1) }}</b></div>
            <div><span>Usados</span><b>{{ num(saldo.usados, 1) }}</b></div>
            <div><span>Comprometidos</span><b>{{ num(saldo.comprometidos, 1) }}</b></div>
            <div><span>Por vencer</span><b :class="{ alerta: saldo.porVencer > 0 }">{{ num(saldo.porVencer, 1) }}</b></div>
          </div>
          <p v-if="saldo.porVencer > 0 && saldo.fechaLimiteProxima" class="caja-aviso"><Icono n="reloArena" :t="18" />{{ dias(saldo.porVencer) }} vencen el {{ fechaMedia(saldo.fechaLimiteProxima, true) }}.</p>
          <p v-else-if="saldo.fechaProximoVencimiento" class="ayuda">Próximo vencimiento: {{ dias(saldo.diasProximoVencimiento) }} el {{ fechaMedia(saldo.fechaProximoVencimiento, true) }}.</p>
          <div v-if="esRh" class="fila envolver acciones-rh">
            <button class="btn btn-contorno" :disabled="!saldo.periodos.some(p => p.vigente)" :title="saldo.periodos.some(p => p.vigente) ? undefined : 'Sin periodo vigente: aún no cumple su primer aniversario'" @click="abrirAjuste"><Icono n="lapiz" :t="18" />Ajustar saldo</button>
            <button class="btn btn-contorno" @click="abrirIncapacidad"><Icono n="masCirculo" :t="18" />Registrar incapacidad</button>
          </div>
        </template>
      </section>

      <section class="tarjeta periodos">
        <div class="tarjeta-cabecera"><div><h2 class="titulo-tarjeta">Periodos</h2><p class="subtitulo-tarjeta">Un periodo por aniversario; los días vencen en la fecha límite.</p></div></div>
        <div class="tabla-envoltura">
          <table class="tabla">
            <thead><tr><th>Periodo</th><th>Otorgado</th><th>Fecha límite</th><th class="num">Otorgados</th><th class="num">Usados</th><th class="num">Comprom.</th><th class="num">Vencidos</th><th class="num">Disponibles</th><th /></tr></thead>
            <tbody>
              <tr v-for="p in saldo?.periodos" :key="p.id">
                <td><b>Año {{ p.periodo }}</b></td>
                <td class="nowrap">{{ fechaCorta(p.fechaOtorgamiento) }}</td>
                <td class="nowrap">{{ fechaCorta(p.fechaLimite) }}</td>
                <td class="num">{{ num(p.diasOtorgados, 1) }}</td>
                <td class="num">{{ num(p.diasUsados, 1) }}</td>
                <td class="num">{{ num(p.diasComprometidos, 1) }}</td>
                <td class="num">{{ num(p.diasVencidos, 1) }}</td>
                <td class="num"><b>{{ num(p.disponibles, 1) }}</b></td>
                <td><Estado :v="p.vigente ? 'Vigente' : p.fechaLimite < hoy ? 'Concluida' : 'Programada'" :texto="p.vigente ? 'Vigente' : p.fechaLimite < hoy ? 'Cerrado' : 'Próximo'" /></td>
              </tr>
              <tr v-if="saldo && !saldo.periodos.length"><td colspan="9" class="vacio">Aún no tiene periodos de vacaciones otorgados.</td></tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>

    <section class="tarjeta">
      <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Solicitudes</h2></div>
      <div class="tabla-envoltura">
        <table class="tabla">
          <thead><tr><th>Folio</th><th>Tipo</th><th>Periodo</th><th class="num">Días</th><th>Estado</th><th>Creada</th><th /></tr></thead>
          <tbody>
            <tr v-for="x in solicitudes?.items" :key="x.id">
              <td><RouterLink :to="`/solicitudes/${x.id}`" class="negrita">{{ x.folio }}</RouterLink></td>
              <td>{{ x.tipoTexto }}</td>
              <td class="nowrap">{{ rango(x.fechaInicio, x.fechaFin, true) }}</td>
              <td class="num">{{ num(x.diasHabiles, 1) }}</td>
              <td><Estado :v="x.estado" /><small v-if="x.pasoActualTexto" class="muted bloque">Con {{ x.pasoActualTexto.toLowerCase() }}</small></td>
              <td class="nowrap muted">{{ momento(x.creadoEn) }}</td>
              <td><RouterLink :to="`/solicitudes/${x.id}`" class="ver-todo">Ver detalle <Icono n="flecha" :t="15" /></RouterLink></td>
            </tr>
            <tr v-if="solicitudes && !solicitudes.items.length"><td colspan="7" class="vacio">Sin solicitudes registradas.</td></tr>
          </tbody>
        </table>
      </div>
      <Paginacion v-if="solicitudes" :pagina="solicitudes.paginaActual" :paginas="solicitudes.paginas" :total="solicitudes.total" :tamano="solicitudes.tamano" @ir="irSol" />
    </section>

    <div class="inferior">
      <section class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Movimientos de saldo</h2></div>
        <div class="tabla-envoltura">
          <table class="tabla">
            <thead><tr><th>Fecha</th><th>Movimiento</th><th class="num">Días</th><th>Motivo</th></tr></thead>
            <tbody>
              <tr v-for="m in movimientos?.items" :key="m.id">
                <td class="nowrap muted">{{ momento(m.fecha) }}</td>
                <td><span class="etiqueta cuadrada" :class="MOV[m.tipo]?.color ?? 'gris'">{{ MOV[m.tipo]?.t ?? m.tipo }}</span></td>
                <td class="num negrita" :class="diasMov(m).positivo ? 'pos' : 'neg'">{{ diasMov(m).texto }}</td>
                <td>
                  <RouterLink v-if="m.solicitudId" :to="`/solicitudes/${m.solicitudId}`">{{ m.motivo || 'Ver solicitud' }}</RouterLink>
                  <span v-else>{{ m.motivo || '—' }}</span>
                </td>
              </tr>
              <tr v-if="movimientos && !movimientos.items.length"><td colspan="4" class="vacio">Sin movimientos.</td></tr>
            </tbody>
          </table>
        </div>
        <Paginacion v-if="movimientos" :pagina="movimientos.paginaActual" :paginas="movimientos.paginas" :total="movimientos.total" :tamano="movimientos.tamano" @ir="irMov" />
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera">
          <h2 class="titulo-tarjeta">Incapacidades</h2>
          <button v-if="esRh" class="btn btn-contorno btn-ch" @click="abrirIncapacidad"><Icono n="mas" :t="16" />Registrar</button>
        </div>
        <div class="tabla-envoltura">
          <table class="tabla">
            <thead><tr><th>Periodo</th><th class="num">Días</th><th>Tipo</th><th>Folio</th><th>Estado</th><th v-if="esRh" /></tr></thead>
            <tbody>
              <tr v-for="i in incapacidades?.items" :key="i.id">
                <td class="nowrap">{{ rango(i.fechaInicio, i.fechaFin, true) }}<small v-if="i.nota" class="muted bloque">{{ i.nota }}</small></td>
                <td class="num">{{ i.dias }}</td>
                <td>{{ i.tipo ?? '—' }}</td>
                <td>{{ i.folio ?? '—' }}</td>
                <td><Estado :v="estadoInc(i)" /></td>
                <td v-if="esRh"><button v-if="!i.cancelada" class="btn btn-fantasma btn-ch" :disabled="ocupado" @click="cancelarIncapacidad(i)">Cancelar</button></td>
              </tr>
              <tr v-if="incapacidades && !incapacidades.items.length"><td :colspan="esRh ? 6 : 5" class="vacio">Sin incapacidades registradas.</td></tr>
            </tbody>
          </table>
        </div>
        <Paginacion v-if="incapacidades" :pagina="incapacidades.paginaActual" :paginas="incapacidades.paginas" :total="incapacidades.total" :tamano="incapacidades.tamano" @ir="irInc" />
      </section>
    </div>

    <Modal v-if="modal === 'ajuste'" titulo="Ajustar saldo" :subtitulo="ficha.nombre" :ancho="480" @cerrar="modal = ''">
      <div class="columna" style="gap: 14px">
        <div class="campo">
          <label for="aj-dias">Días a ajustar</label>
          <input id="aj-dias" v-model.number="ajuste.dias" type="number" step="0.5" class="entrada" :class="{ error: errAj.dias }" placeholder="Ej. 2 o -1.5" />
          <span v-if="errAj.dias" class="error-campo">{{ errAj.dias }}</span>
          <span v-else class="ayuda">Positivo para sumar, negativo para restar. Se aplica al periodo vigente.</span>
        </div>
        <div v-if="saldo" class="cuenta">
          <span>Disponibles hoy</span><b>{{ num(saldo.disponibles, 1) }}</b>
          <span>Saldo resultante</span><b :class="{ neg: resultante !== null && resultante < 0 }">{{ resultante === null ? '—' : num(resultante, 1) }}</b>
        </div>
        <div class="campo">
          <label for="aj-motivo">Motivo</label>
          <textarea id="aj-motivo" v-model="ajuste.motivo" class="entrada" :class="{ error: errAj.motivo }" rows="3" placeholder="Ej. Corrección por días tomados antes del sistema" />
          <span v-if="errAj.motivo" class="error-campo">{{ errAj.motivo }}</span>
        </div>
      </div>
      <template #pie>
        <button class="btn btn-contorno" @click="modal = ''">Cancelar</button>
        <button class="btn btn-primario" :disabled="ocupado" @click="guardarAjuste">Aplicar ajuste</button>
      </template>
    </Modal>

    <Modal v-if="modal === 'incapacidad'" titulo="Registrar incapacidad" :subtitulo="ficha.nombre" :ancho="520" @cerrar="modal = ''">
      <div class="columna" style="gap: 14px">
        <div class="rejilla-form">
          <div class="campo">
            <label for="in-ini">Fecha de inicio</label>
            <input id="in-ini" v-model="inc.fechaInicio" type="date" class="entrada" :class="{ error: errInc.fechaInicio }" />
            <span v-if="errInc.fechaInicio" class="error-campo">{{ errInc.fechaInicio }}</span>
          </div>
          <div class="campo">
            <label for="in-fin">Fecha de término</label>
            <input id="in-fin" v-model="inc.fechaFin" type="date" class="entrada" :class="{ error: errInc.fechaFin }" :min="inc.fechaInicio" />
            <span v-if="errInc.fechaFin" class="error-campo">{{ errInc.fechaFin }}</span>
            <span v-else-if="diasInc" class="ayuda">{{ diasInc }} {{ diasInc === 1 ? 'día' : 'días' }} naturales</span>
          </div>
          <div class="campo">
            <label for="in-tipo">Tipo</label>
            <select id="in-tipo" v-model="inc.tipo" class="entrada"><option v-for="t in TIPOS_INC" :key="t" :value="t">{{ t }}</option></select>
          </div>
          <div class="campo">
            <label for="in-folio">Folio del IMSS</label>
            <input id="in-folio" v-model="inc.folio" class="entrada" placeholder="Ej. AB123456" />
          </div>
          <div class="campo completo">
            <label for="in-nota">Nota</label>
            <textarea id="in-nota" v-model="inc.nota" class="entrada" rows="2" placeholder="Opcional" />
          </div>
        </div>
      </div>
      <template #pie>
        <button class="btn btn-contorno" @click="modal = ''">Cancelar</button>
        <button class="btn btn-primario" :disabled="ocupado" @click="guardarIncapacidad">Registrar incapacidad</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.vacaciones { display: flex; flex-direction: column; gap: 20px; }
.superior { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 2fr); gap: 20px; align-items: start; }
.inferior { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; align-items: start; }
.vac-ico { width: 72px; height: 72px; border-radius: 50%; background: #e0f6ee; color: #0c8e6c; display: grid; place-items: center; flex-shrink: 0; }
.dias-g { display: block; font-size: 30px; font-weight: 800; color: var(--titulo); letter-spacing: -.02em; }
.disp-t { font-size: 18px; color: var(--texto-2); }
.cifras { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; margin: 18px 0 14px; background: var(--gris-50); border-radius: 10px; padding: 12px; }
.cifras div { display: flex; flex-direction: column; align-items: center; text-align: center; }
.cifras span { font-size: 12.5px; color: var(--texto-3); }
.cifras b { font-size: 19px; color: var(--titulo); }
.cifras b.alerta { color: var(--ambar); }
.acciones-rh { margin-top: 16px; gap: 10px; }
.acciones-rh .btn { flex: 1; }
.nowrap { white-space: nowrap; }
.bloque { display: block; font-size: 12.5px; }
.pos { color: var(--verde-texto); }
.neg { color: var(--rojo-texto); }
.cuenta { display: grid; grid-template-columns: 1fr auto; gap: 6px 16px; background: var(--gris-50); border-radius: 10px; padding: 12px 14px; font-size: 14.5px; }
.cuenta span { color: var(--texto-2); }
.cuenta b { text-align: right; color: var(--titulo); }
@media (max-width: 1250px) { .superior, .inferior { grid-template-columns: 1fr; } }
@media (max-width: 480px) { .cifras { grid-template-columns: repeat(2, 1fr); } .acciones-rh .btn { flex: 1 1 100%; } }
</style>
