<script setup lang="ts">
// Perfil propio: foto, datos, contacto, cambio de contraseña, instalación de la app y cierre de sesión.
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useEsMovil } from '@/utils/usos'
import { fechaMedia } from '@/utils/formato'

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()
const esMovil = useEsMovil()
const me = computed(() => s.me)
const col = computed(() => s.me?.colaborador ?? null)

// ───── Foto ─────
const archivo = ref<HTMLInputElement>()
const subiendo = ref(false)
async function cambiarFoto(e: Event) {
  const input = e.target as HTMLInputElement
  const a = input.files?.[0]
  input.value = ''
  if (!a) return
  if (!a.type.startsWith('image/')) { ui.avisar('Elige una imagen (JPG o PNG).', 'error'); return }
  if (a.size > 8 * 1024 * 1024) { ui.avisar('La foto no debe pesar más de 8 MB.', 'error'); return }
  const fd = new FormData()
  fd.append('archivo', a)
  subiendo.value = true
  try {
    await api.subir('/me/foto', fd)
    await s.cargar()
    ui.avisar('Foto actualizada')
  } catch (err) { ui.error(err) } finally { subiendo.value = false }
}

// ───── Contacto ─────
const contacto = reactive({ telefono: '', contactoEmergenciaNombre: '', contactoEmergenciaTelefono: '' })
const original = ref('')
const guardandoContacto = ref(false)
const intentoContacto = ref(false)
const telValido = (t: string) => !t.trim() || /^[\d\s()+-]{7,20}$/.test(t.trim())
const erroresContacto = computed(() => {
  const e: Record<string, string> = {}
  if (!telValido(contacto.telefono)) e.telefono = 'Escribe un teléfono válido (solo números, de 7 a 20 dígitos).'
  if (!telValido(contacto.contactoEmergenciaTelefono)) e.emergenciaTel = 'Escribe un teléfono válido.'
  if (contacto.contactoEmergenciaTelefono.trim() && !contacto.contactoEmergenciaNombre.trim()) e.emergenciaNombre = 'Escribe el nombre de tu contacto.'
  return e
})
const contactoCambiado = computed(() => JSON.stringify(contacto) !== original.value)
async function cargarContacto() {
  if (!col.value) return
  contacto.telefono = col.value.telefono ?? ''
  try {
    const c = await api.get(`/colaboradores/${col.value.id}`)
    contacto.telefono = c.personales?.telefono ?? contacto.telefono
    contacto.contactoEmergenciaNombre = c.personales?.contactoEmergenciaNombre ?? ''
    contacto.contactoEmergenciaTelefono = c.personales?.contactoEmergenciaTelefono ?? ''
  } catch { /* se muestra lo que trae /me */ }
  original.value = JSON.stringify(contacto)
}
async function guardarContacto() {
  intentoContacto.value = true
  if (Object.keys(erroresContacto.value).length) return
  guardandoContacto.value = true
  try {
    await api.put('/me', {
      telefono: contacto.telefono.trim() || null, email: col.value?.email ?? null,
      contactoEmergenciaNombre: contacto.contactoEmergenciaNombre.trim() || null, contactoEmergenciaTelefono: contacto.contactoEmergenciaTelefono.trim() || null,
    })
    original.value = JSON.stringify(contacto)
    intentoContacto.value = false
    ui.avisar('Datos de contacto guardados')
    s.cargar()
  } catch (e) { ui.error(e) } finally { guardandoContacto.value = false }
}

// ───── Contraseña ─────
const pwd = reactive({ actual: '', nueva: '', confirmar: '' })
const verPwd = ref(false)
const intentoPwd = ref(false)
const guardandoPwd = ref(false)
const errorPwd = ref<string | null>(null)
const okPwd = ref(false)
const erroresPwd = computed(() => {
  const e: Record<string, string> = {}
  if (!pwd.actual) e.actual = 'Escribe tu contraseña actual.'
  if (pwd.nueva.length < 8 || !/\d/.test(pwd.nueva) || !/[a-zA-ZáéíóúñÁÉÍÓÚÑ]/.test(pwd.nueva)) e.nueva = 'Al menos 8 caracteres, con letras y números.'
  else if (pwd.nueva === pwd.actual) e.nueva = 'La nueva contraseña debe ser distinta de la actual.'
  if (pwd.confirmar !== pwd.nueva) e.confirmar = 'Las contraseñas no coinciden.'
  return e
})
async function cambiarPassword() {
  intentoPwd.value = true; errorPwd.value = null; okPwd.value = false
  if (Object.keys(erroresPwd.value).length) return
  guardandoPwd.value = true
  try {
    await api.post('/auth/cambiar-password', { passwordActual: pwd.actual, passwordNueva: pwd.nueva })
    // El servidor cierra las demás sesiones; se vuelve a entrar con la contraseña nueva para conservar esta.
    try { await s.entrar(me.value!.email, pwd.nueva) } catch { /* si falla, la siguiente petición pedirá iniciar sesión */ }
    Object.assign(pwd, { actual: '', nueva: '', confirmar: '' })
    intentoPwd.value = false
    okPwd.value = true
    ui.avisar('Contraseña actualizada')
  } catch (e) {
    errorPwd.value = e instanceof Error ? e.message : 'No se pudo cambiar la contraseña.'
  } finally { guardandoPwd.value = false }
}

// ───── Instalar la app ─────
interface EventoInstalar extends Event { prompt: () => Promise<void>; userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }> }
const eventoInstalar = ref<EventoInstalar | null>(null)
const instalada = ref(typeof window !== 'undefined' && (window.matchMedia('(display-mode: standalone)').matches || (navigator as any).standalone === true))
const esIos = /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)
const alInstalar = (e: Event) => { e.preventDefault(); eventoInstalar.value = e as EventoInstalar }
const alInstalada = () => { instalada.value = true; eventoInstalar.value = null }
async function instalar() {
  const e = eventoInstalar.value
  if (!e) return
  await e.prompt()
  const r = await e.userChoice
  if (r.outcome === 'accepted') { instalada.value = true; ui.avisar('WOW! Time se instaló en tu dispositivo') }
  eventoInstalar.value = null
}

// ───── Sesión ─────
async function salir() { await s.salir(); router.push('/login') }

function irAContrasena() {
  if (route.hash !== '#contrasena') return
  nextTick(() => setTimeout(() => document.getElementById('contrasena')?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 60))
}
onMounted(() => {
  window.addEventListener('beforeinstallprompt', alInstalar)
  window.addEventListener('appinstalled', alInstalada)
  cargarContacto()
  irAContrasena()
})
onBeforeUnmount(() => {
  window.removeEventListener('beforeinstallprompt', alInstalar)
  window.removeEventListener('appinstalled', alInstalada)
})
watch(() => route.hash, irAContrasena)
</script>

<template>
  <div class="perfil" :class="{ movil: esMovil }">
    <EncabezadoMovil v-if="esMovil" titulo="Perfil" atras="/" />
    <div class="contenido">
      <div v-if="!esMovil" class="encabezado">
        <div>
          <h1 class="titulo-pagina">Mi perfil</h1>
          <p class="subtitulo-pagina">Tus datos, contraseña y preferencias de acceso.</p>
        </div>
      </div>

      <p v-if="me?.debeCambiarPassword" class="caja-aviso" role="alert" data-prueba="aviso-temporal">
        <Icono n="alerta" :t="20" /><span><b>Debes cambiar tu contraseña temporal</b> antes de continuar usando WOW! Time.</span>
      </p>

      <div class="rejilla">
        <!-- Datos -->
        <section class="tarjeta ficha">
          <div class="foto">
            <Avatar :nombre="me?.nombre" :iniciales="col?.iniciales" :foto="col?.fotoUrl" :t="112" />
            <template v-if="col">
              <button class="btn btn-contorno btn-ch" :disabled="subiendo" data-prueba="cambiar-foto" @click="archivo?.click()">
                <span v-if="subiendo" class="spinner" style="width: 14px; height: 14px" /><Icono v-else n="camara" :t="16" />Cambiar foto
              </button>
              <input ref="archivo" type="file" accept="image/*" class="oculto" @change="cambiarFoto" />
            </template>
          </div>
          <div class="datos">
            <h2 class="nombre">{{ me?.nombre }}</h2>
            <p class="puesto">{{ col?.puesto ?? s.rolTexto }}</p>
            <dl>
              <template v-if="col">
                <div><dt>Número</dt><dd>{{ col.numero }}</dd></div>
                <div><dt>Sucursal</dt><dd>{{ col.sucursal ?? '—' }}</dd></div>
                <div><dt>Área</dt><dd>{{ col.area ?? '—' }}</dd></div>
                <div><dt>Supervisor</dt><dd>{{ col.supervisor?.nombre ?? 'Sin supervisor asignado' }}</dd></div>
                <div v-if="col.fechaIngreso"><dt>Ingreso</dt><dd>{{ fechaMedia(col.fechaIngreso, true) }}</dd></div>
              </template>
              <div><dt>Correo</dt><dd>{{ me?.email }}</dd></div>
              <div><dt>Roles</dt><dd>{{ me?.rolesTexto.join(', ') }}</dd></div>
            </dl>
          </div>
        </section>

        <!-- Contacto -->
        <section v-if="col" class="tarjeta">
          <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Datos de contacto</h2></div>
          <form class="columna" novalidate @submit.prevent="guardarContacto">
            <div class="campo">
              <label for="pf-tel">Teléfono</label>
              <div class="con-icono"><Icono n="telefono" :t="18" /><input id="pf-tel" v-model="contacto.telefono" type="tel" inputmode="tel" autocomplete="tel" class="entrada" :class="{ error: intentoContacto && erroresContacto.telefono }" placeholder="81 1234 5678" /></div>
              <span v-if="intentoContacto && erroresContacto.telefono" class="error-campo">{{ erroresContacto.telefono }}</span>
            </div>
            <div class="campo">
              <label for="pf-en">Contacto de emergencia</label>
              <div class="con-icono"><Icono n="persona" :t="18" /><input id="pf-en" v-model="contacto.contactoEmergenciaNombre" class="entrada" :class="{ error: intentoContacto && erroresContacto.emergenciaNombre }" placeholder="Nombre completo" /></div>
              <span v-if="intentoContacto && erroresContacto.emergenciaNombre" class="error-campo">{{ erroresContacto.emergenciaNombre }}</span>
            </div>
            <div class="campo">
              <label for="pf-et">Teléfono de emergencia</label>
              <div class="con-icono"><Icono n="telefono" :t="18" /><input id="pf-et" v-model="contacto.contactoEmergenciaTelefono" type="tel" inputmode="tel" class="entrada" :class="{ error: intentoContacto && erroresContacto.emergenciaTel }" placeholder="81 1234 5678" /></div>
              <span v-if="intentoContacto && erroresContacto.emergenciaTel" class="error-campo">{{ erroresContacto.emergenciaTel }}</span>
            </div>
            <div><button class="btn btn-primario" :disabled="guardandoContacto || !contactoCambiado" data-prueba="guardar-contacto">Guardar cambios</button></div>
          </form>
        </section>

        <!-- Contraseña -->
        <section id="contrasena" class="tarjeta" :class="{ resaltada: me?.debeCambiarPassword }">
          <div class="tarjeta-cabecera">
            <div class="izq"><span class="ico"><Icono n="candado" :t="20" /></span><h2 class="titulo-tarjeta">Cambiar contraseña</h2></div>
          </div>
          <form class="columna" novalidate @submit.prevent="cambiarPassword">
            <div class="campo">
              <label for="pw-actual">Contraseña actual</label>
              <input id="pw-actual" v-model="pwd.actual" :type="verPwd ? 'text' : 'password'" autocomplete="current-password" class="entrada" :class="{ error: intentoPwd && erroresPwd.actual }" />
              <span v-if="intentoPwd && erroresPwd.actual" class="error-campo">{{ erroresPwd.actual }}</span>
            </div>
            <div class="campo">
              <label for="pw-nueva">Nueva contraseña</label>
              <input id="pw-nueva" v-model="pwd.nueva" :type="verPwd ? 'text' : 'password'" autocomplete="new-password" class="entrada" :class="{ error: intentoPwd && erroresPwd.nueva }" />
              <span v-if="intentoPwd && erroresPwd.nueva" class="error-campo">{{ erroresPwd.nueva }}</span>
              <span v-else class="ayuda">Al menos 8 caracteres, con letras y números.</span>
            </div>
            <div class="campo">
              <label for="pw-conf">Confirmar nueva contraseña</label>
              <input id="pw-conf" v-model="pwd.confirmar" :type="verPwd ? 'text' : 'password'" autocomplete="new-password" class="entrada" :class="{ error: intentoPwd && erroresPwd.confirmar }" />
              <span v-if="intentoPwd && erroresPwd.confirmar" class="error-campo">{{ erroresPwd.confirmar }}</span>
            </div>
            <label class="casilla"><input v-model="verPwd" type="checkbox" />Mostrar contraseñas</label>
            <p v-if="errorPwd" class="caja-alerta err" role="alert"><Icono n="alertaCirculo" :t="20" />{{ errorPwd }}</p>
            <p v-if="okPwd" class="caja-ok"><Icono n="checkCirculo" :t="20" />Tu contraseña se cambió. Se cerraron tus otras sesiones.</p>
            <div><button class="btn btn-primario" :disabled="guardandoPwd" data-prueba="cambiar-password">Cambiar contraseña</button></div>
          </form>
        </section>

        <!-- Instalar -->
        <section class="tarjeta">
          <div class="tarjeta-cabecera">
            <div class="izq"><span class="ico"><Icono n="instalar" :t="20" /></span><h2 class="titulo-tarjeta">Instalar la app</h2></div>
          </div>
          <p v-if="instalada" class="caja-ok"><Icono n="checkCirculo" :t="20" />WOW! Time ya está instalada en este dispositivo.</p>
          <template v-else-if="eventoInstalar">
            <p class="muted">Agrega WOW! Time a tu pantalla de inicio para registrar tu asistencia en un toque, aun con poca señal.</p>
            <button class="btn btn-primario" style="margin-top: 14px" data-prueba="instalar" @click="instalar"><Icono n="instalar" :t="18" />Instalar WOW! Time</button>
          </template>
          <template v-else-if="esIos">
            <p class="muted">En iPhone o iPad, desde Safari:</p>
            <ol class="pasos">
              <li>Toca <b>Compartir</b> <Icono n="compartir" :t="16" /> en la barra del navegador.</li>
              <li>Elige <b>Agregar a inicio</b>.</li>
              <li>Toca <b>Agregar</b>. El ícono de WOW! Time aparecerá en tu pantalla de inicio.</li>
            </ol>
          </template>
          <template v-else>
            <p class="muted">Abre el menú de tu navegador y elige <b>Instalar aplicación</b> o <b>Agregar a la pantalla de inicio</b>.</p>
            <p class="muted pequeno" style="margin-top: 8px">En iPhone o iPad: toca <b>Compartir</b> → <b>Agregar a inicio</b>.</p>
          </template>
        </section>
      </div>

      <button class="btn btn-rechazar salir" data-prueba="cerrar-sesion" @click="salir"><Icono n="salir" :t="18" />Cerrar sesión</button>
    </div>
  </div>
</template>

<style scoped>
.perfil .contenido { display: flex; flex-direction: column; gap: 20px; }
.perfil.movil .contenido { padding: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-start; gap: 20px; }
.rejilla { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; align-items: start; }
.ficha { display: flex; gap: 24px; align-items: flex-start; grid-column: 1 / -1; }
.foto { display: flex; flex-direction: column; align-items: center; gap: 12px; flex-shrink: 0; }
.datos { flex: 1; min-width: 0; }
.nombre { font-size: 26px; font-weight: 800; letter-spacing: -.02em; }
.puesto { color: var(--texto-2); font-size: 16px; margin-top: 2px; }
dl { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 12px 20px; margin: 18px 0 0; }
dl div { min-width: 0; }
dt { font-size: 13px; color: var(--texto-3); }
dd { margin: 2px 0 0; font-weight: 600; color: var(--titulo); overflow-wrap: anywhere; }
.ico { width: 40px; height: 40px; border-radius: 50%; display: grid; place-items: center; background: var(--verde-claro); color: var(--verde-texto); }
.resaltada { border-color: #f3c97a; box-shadow: 0 0 0 3px var(--ambar-claro); }
#contrasena { scroll-margin-top: calc(var(--barra-alto) + 16px); }
.pasos { margin: 10px 0 0; padding-left: 20px; display: flex; flex-direction: column; gap: 6px; color: var(--texto-2); }
.pasos .icono { vertical-align: -3px; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
.salir { align-self: flex-start; }
@media (max-width: 900px) { .rejilla { grid-template-columns: 1fr; } }
@media (max-width: 600px) {
  .ficha { flex-direction: column; align-items: center; text-align: center; }
  dl { grid-template-columns: 1fr 1fr; text-align: left; }
  .salir { align-self: stretch; }
  #contrasena { scroll-margin-top: 80px; }
}
</style>
