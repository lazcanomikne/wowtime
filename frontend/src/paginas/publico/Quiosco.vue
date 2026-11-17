<script setup lang="ts">
// Quiosco web de asistencia para una tableta en la entrada: número de colaborador + PIN, autenticado con la llave del dispositivo.
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import Icono from '@/componentes/Icono.vue'
import Estado from '@/componentes/Estado.vue'
import { API_URL, ErrorApi } from '@/api'
import { useUi } from '@/stores/ui'
import { fechaLarga } from '@/utils/formato'
import logoOscuro from '@/assets/marca/logo-oscuro.svg'

const CLAVE = 'wt.quiosco.llave'
const MAX_NUMERO = 6
const MAX_PIN = 8
const MIN_PIN = 4
const ui = useUi()

interface Dispositivo { id: number; nombre: string; tipo: string; sucursal?: string | null; horaServidor: string }
interface Resultado { ok: boolean; mensaje: string; iniciales?: string; colaborador?: string; estado?: string; hora?: string; tipo?: string }

function leerLlave() { try { return localStorage.getItem(CLAVE) } catch { return null } }
function guardarLlave(v: string | null) { try { v ? localStorage.setItem(CLAVE, v) : localStorage.removeItem(CLAVE) } catch { /* sin almacenamiento */ } }

const llave = ref<string | null>(leerLlave())
const dispositivo = ref<Dispositivo | null>(null)
const sinConexion = ref(false)
const iniciando = ref(!!llave.value)

// ───── Llamadas con la llave del dispositivo (sin sesión de usuario) ─────
async function llamar<T>(metodo: 'GET' | 'POST', ruta: string, clave: string, cuerpo?: unknown): Promise<T> {
  const headers: Record<string, string> = { 'X-Device-Key': clave }
  if (cuerpo !== undefined) headers['Content-Type'] = 'application/json'
  let r: Response
  try { r = await fetch(`${API_URL}/api/v1${ruta}`, { method: metodo, headers, body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo) }) }
  catch { throw new ErrorApi(0, 'sin_conexion', 'Sin conexión con el servidor. Revisa la red e intenta de nuevo.') }
  if (!r.ok) {
    let e: any = null
    try { e = (await r.json())?.error } catch { /* sin cuerpo */ }
    throw new ErrorApi(r.status, e?.codigo ?? 'error', e?.mensaje ?? 'No se pudo completar el registro. Intenta de nuevo.')
  }
  return r.json() as Promise<T>
}

// ───── Reloj con la hora de la sucursal ─────
const ahora = ref(new Date())
let desfase = 0
function sincronizarHora(horaServidor?: string) {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})/.exec(horaServidor ?? '')
  if (!m) return
  const local = new Date(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], +m[6])
  desfase = local.getTime() - Date.now()
  ahora.value = new Date(Date.now() + desfase)
}
const horaTexto = computed(() => `${String(ahora.value.getHours()).padStart(2, '0')}:${String(ahora.value.getMinutes()).padStart(2, '0')}`)
const segundos = computed(() => String(ahora.value.getSeconds()).padStart(2, '0'))
const fechaTexto = computed(() => fechaLarga(ahora.value, true))

// ───── Configuración ─────
const llaveEntrada = ref('')
const errorConfig = ref<string | null>(null)
const validando = ref(false)

async function identificar(clave: string) {
  const d = await llamar<Dispositivo>('GET', '/terminal/dispositivo', clave)
  dispositivo.value = d
  sinConexion.value = false
  sincronizarHora(d.horaServidor)
  return d
}
async function configurar() {
  const clave = llaveEntrada.value.trim()
  errorConfig.value = null
  if (!clave) { errorConfig.value = 'Escribe la llave del dispositivo.'; return }
  validando.value = true
  try {
    const d = await identificar(clave)
    if (d.tipo === 'LectorQr') { dispositivo.value = null; errorConfig.value = 'Esta llave es de un lector de visitantes. Usa la llave de un quiosco o terminal de asistencia.'; return }
    guardarLlave(clave); llave.value = clave; llaveEntrada.value = ''
    reiniciar()
  } catch (e) {
    errorConfig.value = e instanceof ErrorApi ? e.message : 'No se pudo validar la llave.'
  } finally { validando.value = false }
}
async function cambiarDispositivo() {
  const r = await ui.confirmar({ titulo: 'Cambiar dispositivo', mensaje: 'Se borrará la llave guardada en esta tableta y tendrás que configurarla de nuevo.', aceptar: 'Borrar llave', peligro: true })
  if (!r.ok) return
  guardarLlave(null); llave.value = null; dispositivo.value = null; errorConfig.value = null
}
function llaveInvalida(msg: string) {
  guardarLlave(null); llave.value = null; dispositivo.value = null; errorConfig.value = msg
}

// ───── Captura: número, PIN y registro ─────
const paso = ref<'numero' | 'pin'>('numero')
const numero = ref('')
const pin = ref('')
const enviando = ref(false)
const resultado = ref<Resultado | null>(null)
let tResultado: ReturnType<typeof setTimeout> | undefined
let tInactivo: ReturnType<typeof setTimeout> | undefined

const pinListo = computed(() => paso.value === 'pin' && numero.value.length > 0 && pin.value.length >= MIN_PIN)
function reiniciar() {
  paso.value = 'numero'; numero.value = ''; pin.value = ''; resultado.value = null
  if (tResultado) clearTimeout(tResultado)
  if (tInactivo) clearTimeout(tInactivo)
}
function actividad() {
  if (tInactivo) clearTimeout(tInactivo)
  tInactivo = setTimeout(() => { if (!resultado.value && !enviando.value) reiniciar() }, 30000)
}
function tecla(d: string) {
  if (resultado.value || enviando.value) return
  actividad()
  if (paso.value === 'numero') { if (numero.value.length < MAX_NUMERO) numero.value += d }
  else if (pin.value.length < MAX_PIN) pin.value += d
}
function borrar() {
  if (resultado.value || enviando.value) return
  actividad()
  if (paso.value === 'pin') { if (pin.value) pin.value = pin.value.slice(0, -1); else paso.value = 'numero' }
  else numero.value = numero.value.slice(0, -1)
}
function continuar() {
  if (paso.value === 'numero' && numero.value) { paso.value = 'pin'; pin.value = ''; actividad() }
}
function cambiarNumero() { paso.value = 'numero'; pin.value = ''; actividad() }

async function registrar(tipo: 'Entrada' | 'Salida') {
  if (!pinListo.value || enviando.value || !llave.value) return
  enviando.value = true
  try {
    const r = await llamar<any>('POST', '/terminal/registros', llave.value, { numero: numero.value, pin: pin.value, tipo })
    mostrar({ ok: true, mensaje: r.mensaje, iniciales: r.iniciales, colaborador: r.colaborador, estado: r.estado, hora: r.hora, tipo: r.tipo })
  } catch (e) {
    const err = e instanceof ErrorApi ? e : null
    if (err?.status === 401) {
      mostrar({ ok: false, mensaje: 'La llave de este quiosco ya no es válida. Pide a Recursos Humanos que lo configure de nuevo.' })
      setTimeout(() => llaveInvalida('La llave guardada dejó de ser válida. Escribe la nueva llave del dispositivo.'), 4000)
    } else mostrar({ ok: false, mensaje: err?.message ?? 'No se pudo completar el registro. Intenta de nuevo.' })
  } finally { enviando.value = false }
}
function mostrar(r: Resultado) {
  resultado.value = r
  numero.value = ''; pin.value = ''; paso.value = 'numero'
  if (tInactivo) clearTimeout(tInactivo)
  if (tResultado) clearTimeout(tResultado)
  tResultado = setTimeout(reiniciar, 4000)
}

// Teclado físico (tabletas con teclado o pruebas en escritorio)
function teclaFisica(e: KeyboardEvent) {
  if (!llave.value || ui.confirmacion) return
  if (/^\d$/.test(e.key)) { tecla(e.key); e.preventDefault() }
  else if (e.key === 'Backspace') { borrar(); e.preventDefault() }
  else if (e.key === 'Enter') { if (paso.value === 'numero') continuar(); e.preventDefault() }
  else if (e.key === 'Escape') reiniciar()
}

// ───── Pantalla completa ─────
const enPantallaCompleta = ref(false)
function alternarPantallaCompleta() {
  try {
    if (document.fullscreenElement) document.exitFullscreen()
    else document.documentElement.requestFullscreen?.()
  } catch { /* no disponible */ }
}
const alCambiarPantalla = () => (enPantallaCompleta.value = !!document.fullscreenElement)

let reloj: ReturnType<typeof setInterval> | undefined
let refresco: ReturnType<typeof setInterval> | undefined
onMounted(async () => {
  reloj = setInterval(() => (ahora.value = new Date(Date.now() + desfase)), 1000)
  window.addEventListener('keydown', teclaFisica)
  document.addEventListener('fullscreenchange', alCambiarPantalla)
  // Revalida la llave y la hora cada 10 minutos (también mantiene la última conexión del dispositivo).
  refresco = setInterval(async () => {
    if (!llave.value) return
    try { await identificar(llave.value) } catch (e) { if (e instanceof ErrorApi && e.status === 401) llaveInvalida(e.message); else sinConexion.value = true }
  }, 600000)
  if (llave.value) {
    try { await identificar(llave.value) } catch (e) {
      if (e instanceof ErrorApi && e.status === 401) llaveInvalida('La llave guardada ya no es válida. Escribe la nueva llave del dispositivo.')
      else sinConexion.value = true
    } finally { iniciando.value = false }
  }
})
onBeforeUnmount(() => {
  clearInterval(reloj); clearInterval(refresco)
  if (tResultado) clearTimeout(tResultado)
  if (tInactivo) clearTimeout(tInactivo)
  window.removeEventListener('keydown', teclaFisica)
  document.removeEventListener('fullscreenchange', alCambiarPantalla)
})

const TECLAS = ['1', '2', '3', '4', '5', '6', '7', '8', '9']
</script>

<template>
  <div class="quiosco">
    <!-- Cargando -->
    <div v-if="iniciando" class="centro"><span class="spinner grande" /></div>

    <!-- Configuración -->
    <div v-else-if="!llave" class="config">
      <img :src="logoOscuro" alt="WOW! Time" class="logo" />
      <form class="caja-config" @submit.prevent="configurar">
        <span class="config-ic"><Icono n="llave" :t="30" /></span>
        <h1>Configurar quiosco</h1>
        <p class="muted">Escribe la llave del dispositivo que generó Recursos Humanos en Configuración › Dispositivos. Solo se pide una vez en esta tableta.</p>
        <div class="campo">
          <label for="llave">Llave del dispositivo</label>
          <input id="llave" v-model="llaveEntrada" class="entrada llave" autocomplete="off" autocapitalize="off" spellcheck="false" placeholder="dv_xxxxxxxx.xxxxxxxxxxxxxxxx" />
        </div>
        <p v-if="errorConfig" class="caja-alerta err" role="alert"><Icono n="alertaCirculo" :t="20" />{{ errorConfig }}</p>
        <button class="btn btn-primario btn-g btn-bloque" :disabled="validando">{{ validando ? 'Validando…' : 'Validar y guardar' }}</button>
      </form>
    </div>

    <!-- Quiosco -->
    <div v-else class="kiosco">
      <section class="panel-info">
        <img :src="logoOscuro" alt="WOW! Time" class="logo" />
        <div class="reloj" aria-live="off">
          <p class="hora">{{ horaTexto }}<span class="seg">{{ segundos }}</span></p>
          <p class="fecha">{{ fechaTexto }}</p>
        </div>
        <div class="dispositivo">
          <Icono n="ubicacion" :t="20" />
          <span>{{ dispositivo?.sucursal ?? 'Sucursal' }}<template v-if="dispositivo"> · {{ dispositivo.nombre }}</template></span>
        </div>
        <p v-if="sinConexion" class="sin-red"><Icono n="sinRed" :t="18" />Sin conexión con el servidor</p>
        <p class="ayuda-q">Escribe tu número de colaborador, luego tu PIN y elige si registras entrada o salida.</p>
        <div class="pie-q">
          <button class="discreto" @click="cambiarDispositivo"><Icono n="configuracion" :t="16" />Cambiar dispositivo</button>
          <button class="discreto" :aria-label="enPantallaCompleta ? 'Salir de pantalla completa' : 'Pantalla completa'" @click="alternarPantallaCompleta">
            <Icono :n="enPantallaCompleta ? 'contraer' : 'expandir'" :t="16" />{{ enPantallaCompleta ? 'Salir de pantalla completa' : 'Pantalla completa' }}
          </button>
        </div>
      </section>

      <section class="panel-captura">
        <div class="tarjeta-q">
          <div class="pasos">
            <span :class="{ act: paso === 'numero', hecho: paso === 'pin' }"><b>1</b>Número</span>
            <span class="linea" />
            <span :class="{ act: paso === 'pin' }"><b>2</b>PIN</span>
          </div>

          <div class="pantalla">
            <template v-if="paso === 'numero'">
              <label>Número de colaborador</label>
              <p class="valor" :class="{ vacio: !numero }">{{ numero || 'Ej. 248' }}</p>
            </template>
            <template v-else>
              <label>PIN <button class="cambiar" @click="cambiarNumero">Número {{ numero }} · cambiar</button></label>
              <p class="valor puntos" :class="{ vacio: !pin }">
                <template v-if="pin"><span v-for="i in pin.length" :key="i" class="punto-pin" /></template>
                <template v-else>••••</template>
              </p>
            </template>
          </div>

          <div class="teclado">
            <button v-for="t in TECLAS" :key="t" class="tecla" @click="tecla(t)">{{ t }}</button>
            <button class="tecla secundaria" aria-label="Borrar" @click="borrar"><Icono n="regresar" :t="28" /></button>
            <button class="tecla" @click="tecla('0')">0</button>
            <button v-if="paso === 'numero'" class="tecla ok" :disabled="!numero" aria-label="Continuar" @click="continuar"><Icono n="check" :t="30" :g="2.6" /></button>
            <button v-else class="tecla secundaria" aria-label="Limpiar PIN" @click="pin = ''">C</button>
          </div>

          <div class="acciones-q">
            <button class="btn-q entrada" :disabled="!pinListo || enviando" data-prueba="quiosco-entrada" @click="registrar('Entrada')">
              <Icono n="flecha" :t="26" :g="2.4" />Registrar entrada
            </button>
            <button class="btn-q salida" :disabled="!pinListo || enviando" data-prueba="quiosco-salida" @click="registrar('Salida')">
              <Icono n="salir" :t="24" :g="2.2" />Registrar salida
            </button>
          </div>
          <p class="nota-q">{{ enviando ? 'Registrando…' : paso === 'numero' ? 'Escribe tu número y toca ✓ para continuar.' : pin.length < MIN_PIN ? `Escribe tu PIN (${MIN_PIN} dígitos o más).` : 'Elige entrada o salida.' }}</p>
        </div>
      </section>
      <div class="pie-q pie-movil">
        <button class="discreto" @click="cambiarDispositivo"><Icono n="configuracion" :t="16" />Cambiar dispositivo</button>
      </div>

      <!-- Resultado -->
      <Transition name="res">
        <div v-if="resultado" class="resultado" :class="resultado.ok ? 'ok' : 'error'" role="status" @click="reiniciar">
          <div class="res-caja">
            <span v-if="resultado.ok" class="res-avatar">{{ resultado.iniciales }}</span>
            <span v-else class="res-icono"><Icono n="x" :t="54" :g="2.6" /></span>
            <p class="res-msg">{{ resultado.mensaje }}</p>
            <div v-if="resultado.ok" class="res-det">
              <span class="res-nombre">{{ resultado.colaborador }}</span>
              <Estado :v="resultado.estado" punto />
            </div>
            <p v-else class="res-sub">Revisa tus datos e intenta de nuevo.</p>
            <span class="res-barra"><span /></span>
          </div>
        </div>
      </Transition>
    </div>
  </div>
</template>

<style scoped>
.quiosco { position: fixed; inset: 0; background: radial-gradient(1200px 700px at 15% 0%, var(--navy-3) 0%, var(--navy) 45%, var(--navy-2) 100%); color: #fff; overflow-y: auto; user-select: none; -webkit-tap-highlight-color: transparent; }
.centro { min-height: 100%; display: grid; place-items: center; }
.spinner.grande { width: 44px; height: 44px; border-width: 4px; border-color: rgba(255,255,255,.2); border-top-color: var(--verde); }
.logo { width: 220px; height: auto; display: block; }

/* Configuración */
.config { min-height: 100%; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 28px; padding: 24px 16px; }
.caja-config { background: #fff; color: var(--texto); border-radius: 16px; padding: 30px 28px; width: 100%; max-width: 460px; display: flex; flex-direction: column; gap: 16px; box-shadow: 0 20px 60px rgba(0,0,0,.25); user-select: text; }
.caja-config h1 { font-size: 26px; font-weight: 800; }
.config-ic { width: 60px; height: 60px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-texto); display: grid; place-items: center; }
.entrada.llave { font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: 14px; }
.err { color: var(--rojo-texto); font-size: 14px; }

/* Quiosco */
.kiosco { min-height: 100%; display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); }
.panel-info { display: flex; flex-direction: column; padding: 40px 48px; gap: 18px; }
.reloj { margin-top: auto; }
.hora { font-size: clamp(72px, 11vw, 150px); font-weight: 800; letter-spacing: -.04em; line-height: 1; font-variant-numeric: tabular-nums; }
.seg { font-size: .32em; font-weight: 600; opacity: .55; margin-left: 10px; letter-spacing: 0; }
.fecha { font-size: clamp(20px, 2.2vw, 28px); color: rgba(255,255,255,.85); margin-top: 10px; }
.dispositivo { display: inline-flex; align-items: center; gap: 10px; font-size: 18px; color: rgba(255,255,255,.9); background: rgba(255,255,255,.08); border: 1px solid rgba(255,255,255,.12); padding: 10px 16px; border-radius: 999px; align-self: flex-start; }
.dispositivo .icono { color: #38d17c; }
.sin-red { display: inline-flex; align-items: center; gap: 8px; color: #ffb4b8; font-size: 15px; }
.ayuda-q { color: rgba(255,255,255,.7); font-size: 16px; max-width: 420px; margin-bottom: auto; }
.pie-q { display: flex; gap: 18px; flex-wrap: wrap; }
.discreto { background: none; border: 0; color: rgba(255,255,255,.45); font-size: 13px; display: inline-flex; align-items: center; gap: 6px; padding: 6px 0; }
.discreto:hover { color: rgba(255,255,255,.8); }
.pie-movil { display: none; }

.panel-captura { display: flex; align-items: center; justify-content: center; padding: 28px; }
.tarjeta-q { background: #fff; color: var(--texto); border-radius: 22px; padding: 26px; width: 100%; max-width: 480px; box-shadow: 0 24px 70px rgba(0,0,0,.3); display: flex; flex-direction: column; gap: 16px; }
.pasos { display: flex; align-items: center; gap: 10px; justify-content: center; font-size: 14px; color: var(--texto-3); font-weight: 550; }
.pasos span:not(.linea) { display: inline-flex; align-items: center; gap: 8px; }
.pasos b { width: 26px; height: 26px; border-radius: 50%; display: grid; place-items: center; background: var(--gris-100); color: var(--texto-3); font-size: 13px; }
.pasos .act { color: var(--titulo); }
.pasos .act b { background: var(--verde); color: #fff; }
.pasos .hecho b { background: var(--verde-claro); color: var(--verde-texto); }
.linea { width: 40px; height: 2px; background: var(--gris-200); border-radius: 2px; }
.pantalla { background: var(--gris-50); border: 1px solid var(--gris-200); border-radius: 14px; padding: 12px 18px 14px; text-align: center; }
.pantalla label { display: flex; justify-content: center; align-items: center; gap: 10px; flex-wrap: wrap; font-size: 15px; color: var(--texto-2); font-weight: 550; }
.cambiar { background: none; border: 0; color: var(--azul); font-size: 14px; padding: 0; font-weight: 500; }
.valor { font-size: 44px; font-weight: 800; color: var(--titulo); letter-spacing: .08em; min-height: 58px; font-variant-numeric: tabular-nums; display: flex; align-items: center; justify-content: center; gap: 14px; }
.valor.vacio { color: var(--gris-300); font-weight: 600; letter-spacing: .02em; }
.punto-pin { width: 18px; height: 18px; border-radius: 50%; background: var(--navy); }
.teclado { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }
.tecla { height: 72px; border-radius: 14px; border: 1px solid var(--gris-200); background: #fff; font-size: 30px; font-weight: 650; color: var(--titulo); display: grid; place-items: center; box-shadow: 0 2px 0 var(--gris-200); transition: background .1s, transform .05s; touch-action: manipulation; }
.tecla:active:not(:disabled) { transform: translateY(2px); box-shadow: none; background: var(--gris-100); }
.tecla.secundaria { background: var(--gris-50); color: var(--texto-2); font-size: 24px; }
.tecla.ok { background: var(--verde); border-color: var(--verde); color: #fff; box-shadow: 0 2px 0 var(--verde-osc); }
.tecla.ok:disabled { background: var(--verde-claro); border-color: var(--verde-claro); box-shadow: none; cursor: not-allowed; color: #fff; }
.acciones-q { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.btn-q { height: 66px; border-radius: 14px; border: 0; font-size: 18px; font-weight: 700; display: inline-flex; align-items: center; justify-content: center; gap: 10px; color: #fff; transition: opacity .15s, background .15s; touch-action: manipulation; }
.btn-q.entrada { background: var(--verde); }
.btn-q.entrada:hover:not(:disabled) { background: var(--verde-hover); }
.btn-q.salida { background: var(--navy); }
.btn-q.salida:hover:not(:disabled) { background: var(--navy-3); }
.btn-q:disabled { opacity: .35; cursor: not-allowed; }
.nota-q { text-align: center; font-size: 14px; color: var(--texto-3); min-height: 20px; }

/* Resultado */
.resultado { position: fixed; inset: 0; display: grid; place-items: center; padding: 20px; z-index: 20; background: rgba(8, 22, 42, .72); backdrop-filter: blur(3px); }
.res-caja { background: #fff; color: var(--texto); border-radius: 24px; padding: 40px 36px 0; width: 100%; max-width: 560px; text-align: center; display: flex; flex-direction: column; align-items: center; gap: 16px; overflow: hidden; box-shadow: 0 30px 80px rgba(0,0,0,.35); }
.ok .res-caja { border-top: 8px solid var(--verde); }
.error .res-caja { border-top: 8px solid var(--rojo); }
.res-avatar { width: 104px; height: 104px; border-radius: 50%; background: var(--verde); color: #fff; font-size: 40px; font-weight: 800; display: grid; place-items: center; box-shadow: 0 0 0 10px var(--verde-claro); letter-spacing: .02em; }
.res-icono { width: 104px; height: 104px; border-radius: 50%; background: var(--rojo); color: #fff; display: grid; place-items: center; box-shadow: 0 0 0 10px var(--rojo-claro); }
.res-msg { font-size: 26px; font-weight: 750; color: var(--titulo); line-height: 1.25; margin-top: 8px; }
.error .res-msg { color: var(--rojo-texto); font-size: 23px; }
.res-det { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; justify-content: center; }
.res-nombre { font-size: 17px; color: var(--texto-2); font-weight: 550; }
.res-sub { color: var(--texto-3); font-size: 16px; }
.res-barra { display: block; width: calc(100% + 72px); height: 6px; background: var(--gris-100); margin-top: 24px; }
.res-barra span { display: block; height: 100%; width: 100%; transform-origin: left; animation: consumir 4s linear forwards; }
.ok .res-barra span { background: var(--verde); }
.error .res-barra span { background: var(--rojo); }
@keyframes consumir { to { transform: scaleX(0); } }
.res-enter-active, .res-leave-active { transition: opacity .18s; }
.res-enter-active .res-caja { animation: entrar .22s ease-out; }
.res-enter-from, .res-leave-to { opacity: 0; }
@keyframes entrar { from { transform: scale(.94); } }

/* Tableta vertical y teléfono */
@media (max-width: 900px) {
  .kiosco { grid-template-columns: minmax(0, 1fr); }
  .panel-info { padding: 20px 16px 0; gap: 10px; align-items: center; text-align: center; }
  .panel-info .logo { width: 160px; }
  .reloj { margin-top: 6px; }
  .hora { font-size: 64px; }
  .fecha { font-size: 17px; margin-top: 4px; }
  .dispositivo { align-self: center; font-size: 14px; padding: 7px 12px; }
  .ayuda-q { display: none; }
  .panel-info .pie-q { display: none; }
  .panel-captura { padding: 14px 16px 4px; align-items: flex-start; }
  .tarjeta-q { padding: 18px 16px; gap: 12px; border-radius: 18px; }
  .tecla { height: 60px; font-size: 26px; }
  .valor { font-size: 36px; min-height: 48px; }
  .btn-q { height: 58px; font-size: 16px; }
  .pie-movil { display: flex; justify-content: center; padding: 0 16px 14px; }
}
@media (max-width: 380px) {
  .tecla { height: 54px; }
  .btn-q { font-size: 15px; gap: 6px; }
  .res-msg { font-size: 21px; }
}
@media (max-height: 640px) and (min-width: 901px) {
  .tecla { height: 58px; }
  .btn-q { height: 56px; }
}
</style>
