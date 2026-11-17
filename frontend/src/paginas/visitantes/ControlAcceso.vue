<script setup lang="ts">
// Control de acceso (Recepción y RH): lectura del QR con la cámara o código escrito, resultado grande y visitantes dentro.
import { computed, ref } from 'vue'
import { api, ErrorApi, type Pagina, type Persona as TPersona } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useCarga, useEsMovil, useEventos } from '@/utils/usos'
import { hora, iso } from '@/utils/formato'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Persona from '@/componentes/Persona.vue'
import LectorQr from '@/componentes/LectorQr.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'

interface Invitacion {
  id: number; nombre: string; empresaMotivo: string; anfitrion: TPersona | null; estado: string; qr: string
  llegadaLocal: string; entrada: string | null; salida: string | null; sucursal?: string | null
}
interface Fila { id: number; nombre: string; empresaMotivo: string; llegadaLocal: string; anfitrion: TPersona | null; estado: string; entrada: string | null }
type Resultado =
  | { tipo: 'ok'; accion: 'entrada' | 'salida'; inv: Invitacion; momento: Date }
  | { tipo: 'error'; titulo: string; mensaje: string; momento: Date }

const s = useSesion()
const esMovil = useEsMovil()
const conEncabezadoMovil = computed(() => esMovil.value && !s.esGestion)

const codigo = ref('')
const validando = ref(false)
const saliendo = ref<number | null>(null)
const resultado = ref<Resultado | null>(null)

const dentro = useCarga(() => api.get<Pagina<Fila>>('/visitantes/invitaciones', {
  sucursalId: s.sucursalId, fecha: iso(new Date()), estado: 'Dentro', orden: 'entrada', dir: 'desc', tamano: 100
}), { porSucursal: true })
useEventos(t => { if (t === 'visitante') dentro.cargar(true) })

const TITULOS: Record<string, string> = {
  qr_invalido: 'Código no válido', qr_fuera_vigencia: 'QR fuera de vigencia', cancelada: 'Invitación cancelada',
  usada: 'Invitación ya usada', sin_entrada: 'Sin entrada registrada', sin_conexion: 'Sin conexión', prohibido: 'Sin acceso a esta sucursal'
}
function tituloError(e: unknown) {
  if (!(e instanceof ErrorApi)) return 'No se pudo validar'
  if (e.codigo === 'qr_fuera_vigencia') return e.message.includes('aún no') ? 'El QR aún no es válido' : 'QR vencido'
  return TITULOS[e.codigo] ?? 'No se pudo validar'
}

async function registrar(texto: string, accion?: 'salida') {
  const c = texto.trim()
  if (!c || validando.value) return
  validando.value = true
  try {
    const r = await api.post<{ accion: 'entrada' | 'salida'; invitacion: Invitacion }>('/visitantes/acceso', accion ? { codigo: c, accion } : { codigo: c })
    resultado.value = { tipo: 'ok', accion: r.accion, inv: r.invitacion, momento: new Date() }
    codigo.value = ''
    try { navigator.vibrate?.(120) } catch { /* sin vibración */ }
    dentro.cargar(true)
  } catch (e) {
    resultado.value = { tipo: 'error', titulo: tituloError(e), mensaje: e instanceof Error ? e.message : 'Ocurrió un error inesperado.', momento: new Date() }
    try { navigator.vibrate?.([80, 60, 80]) } catch { /* sin vibración */ }
  } finally { validando.value = false }
}

function alLeer(texto: string) { registrar(texto) }
function validar() { registrar(codigo.value) }

async function registrarSalida(f: Fila) {
  saliendo.value = f.id
  try {
    const inv = await api.get<Invitacion>(`/visitantes/invitaciones/${f.id}`)
    await registrar(inv.qr, 'salida')
  } catch (e) {
    resultado.value = { tipo: 'error', titulo: tituloError(e), mensaje: e instanceof Error ? e.message : 'Ocurrió un error inesperado.', momento: new Date() }
  } finally { saliendo.value = null }
}

const horaAcceso = computed(() => {
  const r = resultado.value
  if (r?.tipo !== 'ok') return ''
  return hora(r.accion === 'entrada' ? r.inv.entrada : r.inv.salida) || hora(r.momento)
})
</script>

<template>
  <div class="control-acceso" :class="{ movil: conEncabezadoMovil }">
    <EncabezadoMovil v-if="conEncabezadoMovil" titulo="Control de acceso" atras="/" />

    <div class="cuerpo">
      <div v-if="!conEncabezadoMovil" class="encabezado">
        <div>
          <h1 class="titulo-pagina">Control de acceso</h1>
          <p class="subtitulo-pagina">Escanea el QR del visitante para registrar su entrada o salida{{ s.sucursal ? ` · ${s.sucursal.nombre}` : '' }}</p>
        </div>
        <RouterLink v-if="s.tiene('visitantes.ver')" to="/visitantes" class="btn btn-contorno"><Icono n="visitantes" :t="18" />Visitantes</RouterLink>
      </div>

      <div class="rejilla">
        <!-- Lector -->
        <section class="tarjeta" :class="{ compacta: esMovil }">
          <div class="tarjeta-cabecera">
            <h2 class="titulo-tarjeta">Escanear código</h2>
            <span v-if="validando" class="fila pequeno muted"><span class="spinner chico" />Validando…</span>
          </div>
          <LectorQr @leido="alLeer" />
          <form class="manual" @submit.prevent="validar">
            <label for="codigo" class="etiqueta-campo">Escribir código</label>
            <div class="fila">
              <div class="con-icono crecer">
                <Icono n="qr" :t="18" />
                <input id="codigo" v-model="codigo" class="entrada" placeholder="Código de la invitación" autocomplete="off" autocapitalize="off" spellcheck="false" />
              </div>
              <button class="btn btn-primario" :disabled="!codigo.trim() || validando" data-prueba="validar-codigo">Validar</button>
            </div>
            <span class="ayuda">Si la cámara no lee el QR, escribe el código que aparece en el enlace de la invitación.</span>
          </form>
        </section>

        <!-- Resultado -->
        <section class="tarjeta resultado" :class="[{ compacta: esMovil }, resultado ? (resultado.tipo === 'ok' ? resultado.accion : 'error') : 'espera']" aria-live="assertive">
          <template v-if="!resultado">
            <span class="sello gris"><Icono n="escanear" :t="44" /></span>
            <h2 class="res-titulo">Esperando lectura</h2>
            <p class="muted">Acerca el código QR a la cámara o escribe el código.</p>
          </template>
          <template v-else-if="resultado.tipo === 'ok'">
            <span class="sello"><Icono :n="resultado.accion === 'entrada' ? 'checkCirculo' : 'salir'" :t="48" /></span>
            <h2 class="res-titulo" data-prueba="resultado-acceso">{{ resultado.accion === 'entrada' ? 'Entrada registrada' : 'Salida registrada' }}</h2>
            <div class="res-hora">{{ horaAcceso }}</div>
            <div class="res-datos">
              <div class="res-nombre">{{ resultado.inv.nombre }}</div>
              <div class="res-empresa">{{ resultado.inv.empresaMotivo }}</div>
              <div v-if="resultado.inv.anfitrion" class="res-anfitrion">
                <span class="muted pequeno">Anfitrión</span>
                <Persona :p="resultado.inv.anfitrion" :t="34" />
              </div>
              <p v-if="resultado.accion === 'entrada'" class="pequeno muted">Avisamos al anfitrión que su visitante llegó.</p>
            </div>
          </template>
          <template v-else>
            <span class="sello"><Icono n="alertaCirculo" :t="48" /></span>
            <h2 class="res-titulo" data-prueba="resultado-error">{{ resultado.titulo }}</h2>
            <p class="res-mensaje">{{ resultado.mensaje }}</p>
            <p class="pequeno muted">{{ hora(resultado.momento) }}</p>
          </template>
        </section>
      </div>

      <!-- Dentro ahora -->
      <section class="tarjeta" :class="{ compacta: esMovil }">
        <div class="tarjeta-cabecera">
          <div class="izq">
            <h2 class="titulo-tarjeta">Dentro ahora</h2>
            <span v-if="dentro.datos.value" class="etiqueta verde">{{ dentro.datos.value.total }}</span>
          </div>
          <button class="btn-icono borde" aria-label="Actualizar" @click="dentro.cargar()"><Icono n="actualizar" :t="18" /></button>
        </div>
        <div v-if="dentro.cargando.value && !dentro.datos.value" class="columna"><div v-for="i in 2" :key="i" class="esqueleto" style="height: 56px" /></div>
        <div v-else-if="!dentro.datos.value?.items.length" class="vacio">No hay visitantes dentro de las instalaciones.</div>
        <div v-else class="dentro">
          <div v-for="v in dentro.datos.value.items" :key="v.id" class="visitante">
            <Avatar :nombre="v.nombre" :t="42" />
            <RouterLink :to="`/visitantes/${v.id}`" class="crecer textos">
              <b>{{ v.nombre }}</b>
              <small>{{ v.empresaMotivo }}<template v-if="v.anfitrion"> · Visita a {{ v.anfitrion.nombre }}</template></small>
            </RouterLink>
            <div class="entrada-hora"><small class="muted">Entrada</small><b>{{ hora(v.entrada) || '—' }}</b></div>
            <button class="btn btn-contorno btn-ch" :disabled="saliendo !== null || validando" :data-prueba="`salida-${v.id}`" @click="registrarSalida(v)">
              <span v-if="saliendo === v.id" class="spinner chico" /><Icono v-else n="salir" :t="16" />Registrar salida
            </button>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.cuerpo { display: flex; flex-direction: column; gap: 20px; }
.control-acceso.movil .cuerpo { padding: 16px; gap: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; }
.rejilla { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; align-items: stretch; }
.manual { display: flex; flex-direction: column; gap: 8px; margin-top: 18px; }
.spinner.chico { width: 16px; height: 16px; border-width: 2.5px; }

.resultado { display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; gap: 8px; min-height: 360px; transition: background .2s, border-color .2s; }
.sello { width: 96px; height: 96px; border-radius: 50%; display: grid; place-items: center; margin-bottom: 6px; }
.res-titulo { font-size: 30px; font-weight: 800; letter-spacing: -.02em; }
.res-hora { font-size: 44px; font-weight: 800; color: var(--titulo); letter-spacing: -.03em; line-height: 1; font-variant-numeric: tabular-nums; }
.res-datos { display: flex; flex-direction: column; align-items: center; gap: 6px; margin-top: 10px; }
.res-nombre { font-size: 22px; font-weight: 750; color: var(--titulo); }
.res-empresa { font-size: 16px; color: var(--texto-2); }
.res-anfitrion { display: flex; flex-direction: column; align-items: center; gap: 6px; margin-top: 8px; }
.res-mensaje { font-size: 17px; color: var(--rojo-texto); max-width: 420px; }
.resultado.espera .sello { background: var(--gris-100); color: var(--texto-3); }
.resultado.entrada { background: var(--verde-suave); border-color: var(--verde-borde); }
.resultado.entrada .sello { background: var(--verde); color: #fff; }
.resultado.entrada .res-titulo { color: var(--verde-texto); }
.resultado.salida { background: var(--azul-claro); border-color: #cfe0f6; }
.resultado.salida .sello { background: var(--azul); color: #fff; }
.resultado.salida .res-titulo { color: var(--azul); }
.resultado.error { background: var(--rojo-claro); border-color: var(--rojo-borde); }
.resultado.error .sello { background: var(--rojo); color: #fff; }
.resultado.error .res-titulo { color: var(--rojo-texto); }

.dentro { display: flex; flex-direction: column; gap: 10px; }
.visitante { display: flex; align-items: center; gap: 14px; padding: 12px 14px; border: 1px solid var(--gris-200); border-radius: var(--radio); }
.textos { min-width: 0; color: var(--titulo); text-decoration: none !important; }
.textos b { display: block; font-size: 15px; }
.textos small { display: block; font-size: 13.5px; color: var(--texto-2); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.textos:hover b { color: var(--verde-texto); }
.entrada-hora { display: flex; flex-direction: column; align-items: flex-end; font-variant-numeric: tabular-nums; }
.entrada-hora small { font-size: 12px; }

@media (max-width: 900px) {
  .rejilla { grid-template-columns: minmax(0, 1fr); }
  .resultado { min-height: 0; padding-top: 24px; padding-bottom: 24px; }
}
@media (max-width: 760px) {
  .sello { width: 72px; height: 72px; }
  .res-titulo { font-size: 24px; }
  .res-hora { font-size: 34px; }
  .visitante { flex-wrap: wrap; gap: 10px; }
  .visitante .textos { flex-basis: calc(100% - 56px); }
  .entrada-hora { flex-direction: row; gap: 6px; align-items: center; }
  .visitante .btn { margin-left: auto; }
}
</style>
