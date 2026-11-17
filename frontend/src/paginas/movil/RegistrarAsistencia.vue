<script setup lang="ts">
// Registrar asistencia (App_Movil.png, vista 2): geocerca, mapa, foto opcional y confirmación. Exige conexión; la hora la pone el servidor.
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Mapa from '@/componentes/Mapa.vue'
import Estado from '@/componentes/Estado.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'
import { api, ErrorApi } from '@/api'
import { useUi } from '@/stores/ui'
import { useSesion } from '@/stores/sesion'
import { useRelojServidor } from '@/utils/reloj'
import { fechaLarga, hora } from '@/utils/formato'

const router = useRouter()
const ui = useUi()
const s = useSesion()
const { ahora, sincronizar } = useRelojServidor()
const datos = ref<any>(null)
const pos = ref<{ lat: number; lng: number; precision: number } | null>(null)
const val = ref<any>(null)
const estadoGeo = ref<'buscando' | 'ok' | 'denegado' | 'error' | 'sin-soporte'>('buscando')
const foto = ref<{ id: number; url: string } | null>(null)
const subiendo = ref(false)
const justificacion = ref('')
const enviando = ref(false)
const hecho = ref<any>(null)
const enLinea = ref(navigator.onLine)
const entradaArchivo = ref<HTMLInputElement>()
const actualizarRed = () => (enLinea.value = navigator.onLine)

async function cargar() {
  datos.value = await api.get('/asistencia/jornada-hoy')
  sincronizar(datos.value.horaServidor)
  ubicar()
}
function ubicar() {
  if (!('geolocation' in navigator)) { estadoGeo.value = 'sin-soporte'; return }
  estadoGeo.value = 'buscando'
  navigator.geolocation.getCurrentPosition(async p => {
    pos.value = { lat: p.coords.latitude, lng: p.coords.longitude, precision: Math.round(p.coords.accuracy) }
    estadoGeo.value = 'ok'
    try { val.value = await api.post('/asistencia/validar-ubicacion', { latitud: pos.value.lat, longitud: pos.value.lng, precision: pos.value.precision }) } catch (e) { ui.error(e) }
  }, e => { estadoGeo.value = e.code === e.PERMISSION_DENIED ? 'denegado' : 'error' }, { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 })
}
onMounted(() => { cargar().catch(e => ui.error(e)); window.addEventListener('online', actualizarRed); window.addEventListener('offline', actualizarRed) })
onBeforeUnmount(() => { window.removeEventListener('online', actualizarRed); window.removeEventListener('offline', actualizarRed) })

const j = computed(() => datos.value?.jornada)
const siguiente = computed(() => datos.value?.siguiente as 'Entrada' | 'Salida' | null)
const area = computed(() => val.value?.area ?? j.value?.area)
const centro = computed<[number, number] | null>(() => area.value?.latitud ? [area.value.latitud, area.value.longitud] : null)
const fuera = computed(() => val.value && !val.value.dentro)
const requiereFoto = computed(() => !!j.value?.requiereFoto)
const puede = computed(() => enLinea.value && !!siguiente.value && !!pos.value && !enviando.value && (!fuera.value || justificacion.value.trim().length >= 5) && (!requiereFoto.value || !!foto.value) && !subiendo.value)

async function elegirFoto(e: Event) {
  const f = (e.target as HTMLInputElement).files?.[0]
  if (!f) return
  subiendo.value = true
  try {
    const fd = new FormData(); fd.append('archivo', f)
    const r = await api.subir('/archivos/evidencia', fd)
    foto.value = { id: r.id, url: URL.createObjectURL(f) }
  } catch (err) { ui.error(err) } finally { subiendo.value = false; (e.target as HTMLInputElement).value = '' }
}

async function confirmar() {
  if (!pos.value || !siguiente.value) return
  enviando.value = true
  try {
    hecho.value = await api.post('/asistencia/registros', {
      tipo: siguiente.value, latitud: pos.value.lat, longitud: pos.value.lng, precision: pos.value.precision,
      justificacion: justificacion.value.trim() || null, fotoArchivoId: foto.value?.id ?? null
    })
    s.cargar().catch(() => {})
  } catch (e) {
    if (e instanceof ErrorApi && e.codigo === 'fuera_de_area') { val.value = { ...(val.value ?? {}), dentro: false, mensaje: e.message } }
    ui.error(e)
  } finally { enviando.value = false }
}
</script>

<template>
  <div class="reg">
    <EncabezadoMovil titulo="Registrar asistencia" atras="/" />
    <div class="cuerpo">
      <!-- Registro completado -->
      <div v-if="hecho" class="hecho">
        <span class="ok"><Icono n="check" :t="44" :g="3" /></span>
        <h2>{{ hecho.tipo === 'Entrada' ? 'Entrada registrada' : 'Salida registrada' }}</h2>
        <p class="hora-g">{{ hora(hecho.hora) }}</p>
        <Estado :v="hecho.estado" :texto="hecho.estadoTexto" />
        <p v-if="hecho.dentroGeocerca === false" class="caja-aviso">Se registró fuera del área autorizada; tu supervisor revisará la justificación.</p>
        <RouterLink to="/" class="btn btn-primario btn-g btn-bloque">Ir a inicio</RouterLink>
        <RouterLink to="/mi-asistencia" class="btn btn-contorno btn-bloque">Ver mi asistencia</RouterLink>
      </div>

      <template v-else>
        <div class="reloj">
          <b>{{ hora(ahora) }}</b>
          <span>{{ fechaLarga(ahora) }}</span>
        </div>

        <div v-if="datos && !siguiente" class="caja-ok grande"><Icono n="checkCirculo" :t="24" />Ya registraste tu entrada ({{ hora(datos.entrada) }}) y tu salida ({{ hora(datos.salida) }}) hoy.</div>
        <div v-if="j?.ausencia" class="caja-aviso"><Icono n="info" :t="20" />Hoy tienes registrado: {{ j.ausencia }}.</div>
        <div v-else-if="j?.diaDescanso || j?.festivo" class="caja-aviso"><Icono n="info" :t="20" />{{ j.festivo ? `Hoy es día festivo (${j.festivo}).` : 'Hoy es tu día de descanso.' }}</div>

        <!-- Ubicación -->
        <div class="ubic" :class="{ mal: fuera || estadoGeo === 'denegado' || estadoGeo === 'error', esperando: estadoGeo === 'buscando' }">
          <Icono :n="estadoGeo === 'denegado' ? 'candado' : 'ubicacion'" :t="30" :g="2" class="pin" />
          <div class="crecer">
            <template v-if="estadoGeo === 'buscando'"><b>Obteniendo tu ubicación…</b><span>Mantén el GPS activado</span></template>
            <template v-else-if="estadoGeo === 'denegado'"><b>Permiso de ubicación denegado</b><span>Actívalo en la configuración del navegador para registrar.</span></template>
            <template v-else-if="estadoGeo === 'error' || estadoGeo === 'sin-soporte'"><b>No pudimos leer tu ubicación</b><span>Revisa el GPS e intenta de nuevo.</span></template>
            <template v-else-if="fuera"><b>Fuera del área autorizada</b><span>{{ val?.mensaje }}</span></template>
            <template v-else-if="val"><b>Ubicación validada</b><span>{{ area?.nombre }}</span></template>
            <template v-else><b>Validando ubicación…</b></template>
          </div>
          <span v-if="val && !fuera" class="palomita"><Icono n="check" :t="18" :g="3" /></span>
          <button v-else-if="estadoGeo !== 'buscando' && estadoGeo !== 'ok'" class="btn btn-contorno btn-ch" @click="ubicar">Reintentar</button>
        </div>

        <Mapa :centro="centro" :radio="area?.radio" :posicion="pos ? [pos.lat, pos.lng] : null" :dentro="val?.dentro ?? null" :icono="area?.tipo === 'sucursal' ? 'edificio' : 'casa'" :alto="168">
          <div v-if="val" class="globo-mapa">{{ fuera ? 'Tu ubicación está fuera del área autorizada' : 'Tu ubicación está dentro del área autorizada' }}</div>
          <button class="centrar" aria-label="Volver a leer mi ubicación" @click="ubicar"><Icono n="navegar" :t="18" /></button>
        </Mapa>

        <div v-if="fuera" class="campo">
          <label for="just">Justificación (obligatoria)</label>
          <textarea id="just" v-model="justificacion" class="entrada" rows="2" placeholder="Explica por qué registras desde otro lugar" />
          <span class="ayuda">Se generará una incidencia para que tu supervisor la revise.</span>
        </div>

        <!-- Evidencia -->
        <button class="evidencia" :disabled="subiendo" @click="entradaArchivo?.click()">
          <span class="cam"><img v-if="foto" :src="foto.url" alt="Foto de evidencia" /><Icono v-else n="camara" :t="22" /></span>
          <span class="crecer">
            <b>{{ foto ? 'Foto agregada' : 'Agregar evidencia (foto)' }}</b>
            <small>{{ subiendo ? 'Subiendo…' : requiereFoto ? 'Tu política requiere una foto' : 'Solo si tu política lo requiere' }}</small>
          </span>
          <span v-if="foto" class="quitar" role="button" aria-label="Quitar foto" @click.stop="foto = null"><Icono n="x" :t="18" /></span>
          <Icono v-else n="derecha" :t="20" />
        </button>
        <input ref="entradaArchivo" type="file" accept="image/*" capture="user" hidden @change="elegirFoto" />

        <p v-if="!enLinea" class="caja-alerta sinred"><Icono n="sinRed" :t="20" />Sin conexión. Para registrar asistencia necesitas internet.</p>

        <button class="btn btn-primario btn-g btn-bloque" :disabled="!puede" data-prueba="confirmar-registro" @click="confirmar">
          <span v-if="enviando" class="spinner" style="width:18px;height:18px;border-top-color:#fff" />
          Confirmar {{ siguiente === 'Salida' ? 'salida' : 'entrada' }}
        </button>
        <p class="privacidad"><Icono n="info" :t="16" />Se registrará ubicación solo en este momento</p>
      </template>
    </div>
  </div>
</template>

<style scoped>
.reg { min-height: 100%; background: var(--fondo); }
.cuerpo { padding: 14px 14px 24px; display: flex; flex-direction: column; gap: 12px; max-width: 560px; margin: 0 auto; }
.reloj { text-align: center; padding: 4px 0 2px; }
.reloj b { display: block; font-size: 44px; font-weight: 700; color: var(--titulo); letter-spacing: .01em; }
.reloj span { font-size: 14.5px; color: var(--texto-2); }
.ubic { display: flex; align-items: center; gap: 14px; background: var(--verde-suave); border: 1px solid var(--verde-borde); border-radius: 12px; padding: 12px 14px; }
.ubic b { display: block; font-size: 15.5px; color: var(--titulo); }
.ubic span { display: block; font-size: 13.5px; color: var(--texto-2); }
.ubic .pin { color: var(--verde-osc); }
.ubic.mal { background: #fff6ec; border-color: #f8d7b4; }
.ubic.mal .pin { color: var(--naranja); }
.ubic.esperando { background: var(--gris-50); border-color: var(--gris-200); }
.ubic.esperando .pin { color: var(--texto-3); }
.palomita { width: 28px; height: 28px; border-radius: 50%; background: var(--verde); color: #fff; display: grid; place-items: center; flex-shrink: 0; }
.globo-mapa { position: absolute; left: 50%; bottom: 14px; transform: translateX(-50%); z-index: 500; background: #fff; border-radius: 8px; padding: 6px 12px; font-size: 12px; color: var(--titulo); box-shadow: var(--sombra-m); text-align: center; max-width: 70%; }
.centrar { position: absolute; right: 10px; bottom: 10px; z-index: 500; width: 38px; height: 38px; border-radius: 50%; border: 0; background: #fff; color: var(--verde-osc); box-shadow: var(--sombra-m); display: grid; place-items: center; }
.evidencia { display: flex; align-items: center; gap: 14px; width: 100%; background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; padding: 12px 14px; text-align: left; color: var(--titulo); }
.evidencia b { display: block; font-size: 14.5px; }
.evidencia small { font-size: 12.5px; color: var(--texto-3); }
.cam { width: 42px; height: 42px; border-radius: 8px; background: var(--gris-100); display: grid; place-items: center; overflow: hidden; flex-shrink: 0; }
.cam img { width: 100%; height: 100%; object-fit: cover; }
.quitar { color: var(--texto-3); }
.privacidad { display: flex; gap: 6px; align-items: center; justify-content: center; color: var(--texto-2); font-size: 13px; }
.sinred { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
.grande { font-weight: 500; }
.hecho { display: flex; flex-direction: column; align-items: center; gap: 14px; text-align: center; padding: 30px 6px; }
.hecho .ok { width: 86px; height: 86px; border-radius: 50%; background: var(--verde); color: #fff; display: grid; place-items: center; box-shadow: 0 0 0 10px var(--verde-claro); }
.hecho h2 { font-size: 22px; }
.hora-g { font-size: 40px; font-weight: 800; color: var(--titulo); }
</style>
