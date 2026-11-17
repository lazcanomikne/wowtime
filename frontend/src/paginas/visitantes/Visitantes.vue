<script setup lang="ts">
// Visitantes de la sucursal: indicadores del día, filtros, invitaciones (en vivo) y registro de invitados.
import { computed, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api, type Pagina, type Persona as TPersona } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useAccion, useCarga, useEsMovil, useEventos, debounce } from '@/utils/usos'
import { fechaLarga, hora, iso } from '@/utils/formato'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'

interface Fila {
  id: number; nombre: string; empresaMotivo: string; fechaLocal: string; llegadaLocal: string; anfitrion: TPersona | null
  estado: string; entrada: string | null; salida: string | null; telefono: string | null; correo: string | null
}
interface Resumen { registrados: number; dentro: number; esperados: number; salieron: number; vencidas: number }

const s = useSesion()
const router = useRouter()
const esMovil = useEsMovil()
const { ocupado, ejecutar } = useAccion()
const conEncabezadoMovil = computed(() => esMovil.value && !s.esGestion)

const ESTADOS = [
  { v: '', t: 'Todos los estados' }, { v: 'Esperado', t: 'Esperado' }, { v: 'Dentro', t: 'Dentro' }, { v: 'Salio', t: 'Salió' }, { v: 'Vencida', t: 'Vencida' }, { v: 'Cancelada', t: 'Cancelada' }
]
const f = reactive({ fecha: iso(new Date()), estado: '', q: '', orden: 'llegada', dir: 'asc' as 'asc' | 'desc', pagina: 1 })
const TAMANO = 20

const resumen = useCarga(() => api.get<Resumen>('/visitantes/resumen', { sucursalId: s.sucursalId, fecha: f.fecha }), { porSucursal: true })
const lista = useCarga(() => api.get<Pagina<Fila>>('/visitantes/invitaciones', {
  sucursalId: s.sucursalId, fecha: f.fecha, estado: f.estado, q: f.q.trim(), orden: f.orden, dir: f.dir, pagina: f.pagina, tamano: TAMANO
}), { porSucursal: true })

watch(() => [f.fecha, f.estado], () => { f.pagina = 1; lista.cargar(); resumen.cargar() })
const buscar = debounce(() => { f.pagina = 1; lista.cargar() }, 300)
watch(() => f.q, buscar)
function ordenar(campo: string) {
  if (f.orden === campo) f.dir = f.dir === 'asc' ? 'desc' : 'asc'
  else { f.orden = campo; f.dir = 'asc' }
  f.pagina = 1; lista.cargar()
}
function ir(p: number) { f.pagina = p; lista.cargar() }
useEventos(t => { if (t === 'visitante') { lista.cargar(true); resumen.cargar(true) } })

const KPIS = computed(() => {
  const r = resumen.datos.value
  return [
    { t: 'Registrados', v: r?.registrados, i: 'personas', c: 'azul' },
    { t: 'Dentro', v: r?.dentro, i: 'visitantes', c: 'verde' },
    { t: 'Esperados', v: r?.esperados, i: 'reloj', c: 'ambar' },
    { t: 'Salieron', v: r?.salieron, i: 'salir', c: 'gris' },
    { t: 'Vencidas', v: r?.vencidas, i: 'alertaCirculo', c: 'rojo' },
  ]
})
const esHoy = computed(() => f.fecha === iso(new Date()))

// ───── Registrar invitado ─────
const modal = ref(false)
const intento = ref(false)
const inv = reactive({ nombre: '', empresaMotivo: '', fecha: iso(new Date()), hora: '', telefono: '', correo: '', anfitrionColaboradorId: null as number | null })
const anfitrionInicial = computed<TPersona | null>(() => {
  const c = s.me?.colaborador
  return c ? { id: c.id, numero: c.numero, nombre: c.nombre, puesto: c.puesto, area: c.area, iniciales: c.iniciales, fotoUrl: c.fotoUrl } : null
})
const rutaPersonas = computed(() => (s.tiene('colaboradores.ver') ? '/colaboradores' : '/solicitudes/companeros'))
const errores = computed(() => ({
  nombre: inv.nombre.trim().length < 3 ? 'Escribe el nombre completo del visitante.' : '',
  empresaMotivo: !inv.empresaMotivo.trim() ? 'Escribe la empresa o el motivo de la visita.' : '',
  fecha: !inv.fecha ? 'Elige la fecha de llegada.' : '',
  hora: !inv.hora ? 'Indica la hora de llegada.' : '',
  correo: inv.correo.trim() && !inv.correo.includes('@') ? 'El correo no es válido.' : '',
}))
const hayErrores = computed(() => Object.values(errores.value).some(Boolean))

function abrirRegistro() {
  const ahora = new Date(); ahora.setMinutes(Math.ceil(ahora.getMinutes() / 15) * 15 + 15, 0, 0)
  Object.assign(inv, { nombre: '', empresaMotivo: '', fecha: iso(ahora), hora: hora(ahora), telefono: '', correo: '', anfitrionColaboradorId: anfitrionInicial.value?.id ?? null })
  intento.value = false
  modal.value = true
}
async function registrar() {
  intento.value = true
  if (hayErrores.value) return
  const r = await ejecutar(() => api.post<{ id: number }>('/visitantes/invitaciones', {
    nombre: inv.nombre.trim(), empresaMotivo: inv.empresaMotivo.trim(), fecha: inv.fecha, hora: inv.hora,
    telefono: inv.telefono.trim() || null, correo: inv.correo.trim() || null, anfitrionColaboradorId: inv.anfitrionColaboradorId
  }), 'Invitación generada.')
  if (r?.id) { modal.value = false; router.push(`/visitantes/${r.id}`) }
}
</script>

<template>
  <div class="visitantes" :class="{ movil: conEncabezadoMovil }">
    <EncabezadoMovil v-if="conEncabezadoMovil" titulo="Visitantes" atras="/" />

    <div class="cuerpo">
      <div class="encabezado">
        <div v-if="!conEncabezadoMovil">
          <h1 class="titulo-pagina">Visitantes</h1>
          <p class="subtitulo-pagina">{{ esHoy ? 'Invitaciones y accesos de hoy' : `Invitaciones del ${fechaLarga(f.fecha, true).toLowerCase()}` }}{{ s.sucursal ? ` · ${s.sucursal.nombre}` : '' }}</p>
        </div>
        <div class="fila envolver botones">
          <RouterLink v-if="s.tiene('visitantes.acceso')" to="/control-acceso" class="btn btn-contorno" data-prueba="control-acceso"><Icono n="escanear" :t="18" />Control de acceso</RouterLink>
          <button v-if="s.tiene('visitantes.registrar')" class="btn btn-primario" data-prueba="registrar-invitado" @click="abrirRegistro"><Icono n="personaMas" :t="18" />Registrar invitado</button>
        </div>
      </div>

      <!-- Indicadores -->
      <div class="kpis">
        <section v-for="k in KPIS" :key="k.t" class="tarjeta compacta kpi">
          <span class="kpi-icono" :class="k.c"><Icono :n="k.i" :t="26" /></span>
          <div>
            <div class="kpi-titulo">{{ k.t }}</div>
            <div class="kpi-valor">
              <span v-if="k.v === undefined" class="esqueleto" style="display: inline-block; width: 40px; height: 30px" />
              <template v-else>{{ k.v }}</template>
            </div>
          </div>
        </section>
      </div>

      <section class="tarjeta" :class="{ compacta: esMovil }">
        <div class="barra-filtros">
          <input v-model="f.fecha" type="date" class="entrada" aria-label="Fecha" />
          <select v-model="f.estado" class="entrada" aria-label="Estado">
            <option v-for="e in ESTADOS" :key="e.v" :value="e.v">{{ e.t }}</option>
          </select>
          <div class="con-icono buscar">
            <Icono n="buscar" :t="18" />
            <input v-model="f.q" class="entrada" placeholder="Buscar por nombre o empresa…" aria-label="Buscar" />
          </div>
          <span class="crecer" />
          <span v-if="lista.datos.value" class="muted pequeno">{{ lista.datos.value.total }} {{ lista.datos.value.total === 1 ? 'invitación' : 'invitaciones' }}</span>
        </div>

        <div v-if="lista.cargando.value && !lista.datos.value" class="columna">
          <div v-for="i in 4" :key="i" class="esqueleto" style="height: 52px" />
        </div>
        <div v-else-if="!lista.datos.value?.items.length" class="vacio">
          <Icono n="visitantes" :t="34" />
          <p style="margin-top: 8px">No hay invitaciones con estos filtros.</p>
        </div>

        <!-- Teléfono: tarjetas -->
        <div v-else-if="esMovil" class="tarjetas">
          <RouterLink v-for="v in lista.datos.value.items" :key="v.id" :to="`/visitantes/${v.id}`" class="visita">
            <Avatar :nombre="v.nombre" :t="42" />
            <div class="crecer textos">
              <b>{{ v.nombre }}</b>
              <small>{{ v.empresaMotivo }}</small>
              <small class="muted">Llega {{ hora(v.llegadaLocal) }}<template v-if="v.anfitrion"> · {{ v.anfitrion.nombre }}</template></small>
            </div>
            <div class="lado">
              <Estado :v="v.estado" />
              <small v-if="v.entrada" class="muted">{{ hora(v.entrada) }}<template v-if="v.salida"> – {{ hora(v.salida) }}</template></small>
            </div>
          </RouterLink>
        </div>

        <!-- Escritorio: tabla -->
        <div v-else class="tabla-envoltura">
          <table class="tabla">
            <thead>
              <tr>
                <th class="ordenable" @click="ordenar('nombre')">Nombre <Icono v-if="f.orden === 'nombre'" :n="f.dir === 'asc' ? 'arriba' : 'abajo'" :t="14" /></th>
                <th class="ordenable" @click="ordenar('empresa')">Empresa / Motivo <Icono v-if="f.orden === 'empresa'" :n="f.dir === 'asc' ? 'arriba' : 'abajo'" :t="14" /></th>
                <th>Anfitrión</th>
                <th class="ordenable" @click="ordenar('llegada')">Llegada <Icono v-if="f.orden === 'llegada'" :n="f.dir === 'asc' ? 'arriba' : 'abajo'" :t="14" /></th>
                <th>Entrada</th>
                <th>Salida</th>
                <th>Estado</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="v in lista.datos.value.items" :key="v.id" class="clic" :data-prueba="`visitante-${v.id}`" @click="router.push(`/visitantes/${v.id}`)">
                <td>
                  <div class="persona">
                    <Avatar :nombre="v.nombre" :t="36" />
                    <div class="textos"><div class="nombre">{{ v.nombre }}</div><div v-if="v.telefono || v.correo" class="sub">{{ v.telefono || v.correo }}</div></div>
                  </div>
                </td>
                <td>{{ v.empresaMotivo }}</td>
                <td><Persona :p="v.anfitrion" :t="32" /></td>
                <td class="nowrap">{{ hora(v.llegadaLocal) }}</td>
                <td class="nowrap">{{ v.entrada ? hora(v.entrada) : '—' }}</td>
                <td class="nowrap">{{ v.salida ? hora(v.salida) : '—' }}</td>
                <td><Estado :v="v.estado" /></td>
              </tr>
            </tbody>
          </table>
        </div>
        <Paginacion v-if="lista.datos.value && lista.datos.value.paginas > 1" :pagina="lista.datos.value.paginaActual" :paginas="lista.datos.value.paginas"
                    :total="lista.datos.value.total" :tamano="lista.datos.value.tamano" @ir="ir" />
      </section>
    </div>

    <Modal v-if="modal" titulo="Registrar invitado" subtitulo="Comparte los datos del visitante para agilizar su ingreso." :ancho="600" @cerrar="modal = false">
      <div class="rejilla-form">
        <div class="campo completo">
          <label for="inv-nombre">Nombre completo</label>
          <input id="inv-nombre" v-model="inv.nombre" class="entrada" :class="{ error: intento && errores.nombre }" placeholder="Ej. Carlos López Ramírez" maxlength="150" />
          <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
        </div>
        <div class="campo completo">
          <label for="inv-empresa">Empresa o motivo</label>
          <input id="inv-empresa" v-model="inv.empresaMotivo" class="entrada" :class="{ error: intento && errores.empresaMotivo }" placeholder="Ej. Cliente / Entrevista / Mantenimiento" maxlength="150" />
          <span v-if="intento && errores.empresaMotivo" class="error-campo">{{ errores.empresaMotivo }}</span>
        </div>
        <div class="campo">
          <label for="inv-fecha">Fecha de llegada</label>
          <input id="inv-fecha" v-model="inv.fecha" type="date" class="entrada" :min="iso(new Date())" :class="{ error: intento && errores.fecha }" />
          <span v-if="intento && errores.fecha" class="error-campo">{{ errores.fecha }}</span>
        </div>
        <div class="campo">
          <label for="inv-hora">Hora de llegada</label>
          <input id="inv-hora" v-model="inv.hora" type="time" class="entrada" :class="{ error: intento && errores.hora }" />
          <span v-if="intento && errores.hora" class="error-campo">{{ errores.hora }}</span>
        </div>
        <div class="campo">
          <label for="inv-tel">Teléfono (opcional)</label>
          <input id="inv-tel" v-model="inv.telefono" type="tel" class="entrada" placeholder="10 dígitos" maxlength="20" />
        </div>
        <div class="campo">
          <label for="inv-correo">Correo (opcional)</label>
          <input id="inv-correo" v-model="inv.correo" type="email" class="entrada" :class="{ error: intento && errores.correo }" placeholder="Recibirá el enlace de su QR" maxlength="150" />
          <span v-if="intento && errores.correo" class="error-campo">{{ errores.correo }}</span>
        </div>
        <div class="campo completo">
          <label>Anfitrión</label>
          <SelectorColaborador v-model="inv.anfitrionColaboradorId" :inicial="anfitrionInicial" :ruta="rutaPersonas" placeholder="Buscar anfitrión…" />
          <span class="ayuda">Si lo dejas vacío, tú serás el anfitrión.</span>
        </div>
      </div>
      <p class="caja-info" style="margin-top: 16px"><Icono n="qr" :t="20" />Se generará un código QR único con vigencia limitada para este visitante.</p>
      <template #pie>
        <button class="btn btn-contorno" @click="modal = false">Cancelar</button>
        <button class="btn btn-primario" :disabled="ocupado" data-prueba="generar-invitacion" @click="registrar"><Icono n="enviar" :t="18" />Generar invitación</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.cuerpo { display: flex; flex-direction: column; gap: 20px; }
.visitantes.movil .cuerpo { padding: 16px; gap: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; }
.kpis { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 20px; }
.kpi { align-items: center; gap: 14px; }
.kpi .kpi-icono { width: 52px; height: 52px; }
.kpi-icono.azul { background: var(--azul-claro); color: var(--azul); }
.kpi-icono.verde { background: var(--verde-claro); color: var(--verde-texto); }
.kpi-icono.ambar { background: var(--ambar-circulo); color: var(--ambar); }
.kpi-icono.gris { background: var(--gris-100); color: var(--texto-2); }
.kpi-icono.rojo { background: var(--rojo-claro); color: var(--rojo); }
.kpi .kpi-valor { font-size: 30px; }
.nowrap { white-space: nowrap; font-variant-numeric: tabular-nums; }
.tarjetas { display: flex; flex-direction: column; gap: 10px; }
.visita { display: flex; align-items: center; gap: 12px; padding: 12px 14px; border: 1px solid var(--gris-200); border-radius: var(--radio); color: var(--titulo); text-decoration: none !important; min-width: 0; }
.visita:hover { background: #fafbfd; }
.visita b { display: block; font-size: 15px; }
.visita small { display: block; font-size: 13.5px; color: var(--texto-2); }
.textos { min-width: 0; }
.textos > * { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.lado { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; flex-shrink: 0; }

@media (max-width: 1280px) {
  .kpis { grid-template-columns: repeat(3, minmax(0, 1fr)); }
}
@media (max-width: 760px) {
  .kpis { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
  .kpi { padding: 14px; }
  .kpi .kpi-icono { width: 42px; height: 42px; }
  .kpi .kpi-valor { font-size: 24px; }
  .botones { width: 100%; }
  .botones .btn { flex: 1; }
}
</style>
