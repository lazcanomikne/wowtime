<script setup lang="ts">
// Nueva contraseña con el token del correo de recuperación (/restablecer?token=).
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import { useSesion } from '@/stores/sesion'
import { api, ErrorApi } from '@/api'
import logoClaro from '@/assets/marca/logo-claro.svg'
import logoOscuro from '@/assets/marca/logo-oscuro.svg'

const s = useSesion()
const route = useRoute()
const token = computed(() => String(route.query.token ?? ''))
const nueva = ref('')
const confirmar = ref('')
const ver = ref(false)
const intento = ref(false)
const error = ref<string | null>(null)
const listo = ref(false)
const cargando = ref(false)

const errores = computed(() => {
  const e: Record<string, string> = {}
  if (nueva.value.length < 8 || !/\d/.test(nueva.value) || !/[a-zA-ZáéíóúñÁÉÍÓÚÑ]/.test(nueva.value)) e.nueva = 'Al menos 8 caracteres, con letras y números.'
  if (confirmar.value !== nueva.value) e.confirmar = 'Las contraseñas no coinciden.'
  return e
})

async function guardar() {
  intento.value = true; error.value = null
  if (Object.keys(errores.value).length) return
  cargando.value = true
  try {
    await api.post('/auth/restablecer', { token: token.value, passwordNueva: nueva.value })
    listo.value = true
  } catch (e) {
    error.value = e instanceof ErrorApi ? e.message : 'No se pudo guardar la contraseña.'
  } finally { cargando.value = false }
}
</script>

<template>
  <div class="login">
    <section class="marca">
      <img :src="logoOscuro" alt="WOW! Time" class="logo" />
      <div class="mensaje">
        <h2>{{ s.frase('dashboard_banner') || 'Espacios más limpios, personas más felices.' }}</h2>
        <p>{{ s.frase('logo_lema') }}</p>
      </div>
      <p class="pie">{{ s.frase('menu_pie') }}</p>
    </section>
    <section class="formulario">
      <div v-if="!token" class="caja">
        <img :src="logoClaro" alt="WOW! Time" class="logo-movil" />
        <h1 class="titulo-pagina">Enlace incompleto</h1>
        <p class="caja-alerta err"><Icono n="alertaCirculo" :t="20" />El enlace no trae el código de recuperación. Ábrelo tal cual desde el correo o solicita uno nuevo.</p>
        <RouterLink to="/recuperar" class="btn btn-primario btn-g btn-bloque">Solicitar un enlace nuevo</RouterLink>
        <RouterLink to="/login" class="olvido"><Icono n="regresar" :t="16" />Volver a iniciar sesión</RouterLink>
      </div>
      <div v-else-if="listo" class="caja">
        <img :src="logoClaro" alt="WOW! Time" class="logo-movil" />
        <h1 class="titulo-pagina">Contraseña actualizada</h1>
        <p class="caja-ok" role="status"><Icono n="checkCirculo" :t="22" />Ya puedes iniciar sesión con tu nueva contraseña.</p>
        <RouterLink to="/login" class="btn btn-primario btn-g btn-bloque" data-prueba="ir-login">Iniciar sesión</RouterLink>
      </div>
      <form v-else class="caja" novalidate @submit.prevent="guardar">
        <img :src="logoClaro" alt="WOW! Time" class="logo-movil" />
        <h1 class="titulo-pagina">Crea tu nueva contraseña</h1>
        <p class="subtitulo-pagina">Usa al menos 8 caracteres, con letras y números.</p>
        <div class="campo">
          <label for="pwd">Nueva contraseña</label>
          <div class="con-icono">
            <Icono n="candado" :t="19" />
            <input id="pwd" v-model="nueva" class="entrada" :class="{ error: intento && errores.nueva }" :type="ver ? 'text' : 'password'" autocomplete="new-password" placeholder="Nueva contraseña" />
            <button type="button" class="ver" :aria-label="ver ? 'Ocultar contraseña' : 'Mostrar contraseña'" @click="ver = !ver"><Icono n="ojo" :t="19" /></button>
          </div>
          <span v-if="intento && errores.nueva" class="error-campo">{{ errores.nueva }}</span>
        </div>
        <div class="campo">
          <label for="pwd2">Confirmar contraseña</label>
          <div class="con-icono">
            <Icono n="candado" :t="19" />
            <input id="pwd2" v-model="confirmar" class="entrada" :class="{ error: intento && errores.confirmar }" :type="ver ? 'text' : 'password'" autocomplete="new-password" placeholder="Repite la contraseña" />
          </div>
          <span v-if="intento && errores.confirmar" class="error-campo">{{ errores.confirmar }}</span>
        </div>
        <p v-if="error" class="caja-alerta err" role="alert"><Icono n="alertaCirculo" :t="20" /><span>{{ error }} <RouterLink v-if="/expir|válido/i.test(error)" to="/recuperar">Solicitar otro enlace</RouterLink></span></p>
        <button class="btn btn-primario btn-g btn-bloque" :disabled="cargando" data-prueba="guardar-password">
          <span v-if="cargando" class="spinner" style="border-top-color:#fff;width:18px;height:18px" />Guardar contraseña
        </button>
        <RouterLink to="/login" class="olvido"><Icono n="regresar" :t="16" />Volver a iniciar sesión</RouterLink>
      </form>
    </section>
  </div>
</template>

<style scoped>
.login { display: grid; grid-template-columns: minmax(380px, 46%) 1fr; min-height: 100dvh; }
.marca { background: linear-gradient(160deg, var(--navy) 0%, var(--navy-2) 100%); color: #fff; padding: 48px 56px; display: flex; flex-direction: column; position: relative; overflow: hidden; }
.marca::after { content: ''; position: absolute; width: 420px; height: 420px; border-radius: 50%; right: -140px; bottom: -120px; background: radial-gradient(circle, rgba(10,170,74,.35), transparent 65%); }
.logo { width: 220px; }
.mensaje { margin-top: auto; margin-bottom: auto; }
.mensaje h2 { color: #fff; font-size: 40px; line-height: 1.15; font-weight: 800; max-width: 440px; letter-spacing: -.02em; }
.mensaje p { color: #9fe3bf; margin-top: 14px; font-size: 18px; }
.pie { color: rgba(255,255,255,.7); font-size: 15px; }
.formulario { display: grid; place-items: center; padding: 32px 20px; background: var(--fondo); }
.caja { width: 100%; max-width: 420px; display: flex; flex-direction: column; gap: 18px; }
.caja .subtitulo-pagina { margin-top: -10px; margin-bottom: 6px; font-size: 16px; }
.logo-movil { display: none; width: 180px; margin-bottom: 8px; }
.ver { position: absolute; right: 6px; top: 50%; transform: translateY(-50%); border: 0; background: none; color: var(--texto-3); width: 36px; height: 36px; border-radius: 8px; display: grid; place-items: center; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; align-items: flex-start; }
.olvido { display: inline-flex; align-items: center; justify-content: center; gap: 6px; font-weight: 500; }
@media (max-width: 860px) { .login { grid-template-columns: 1fr; } .marca { display: none; } .logo-movil { display: block; } }
</style>
