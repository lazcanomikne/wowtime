<script setup lang="ts">
// Recuperar contraseña: pide el correo y muestra el mensaje genérico del servidor.
import { ref } from 'vue'
import Icono from '@/componentes/Icono.vue'
import { useSesion } from '@/stores/sesion'
import { api, ErrorApi } from '@/api'
import logoClaro from '@/assets/marca/logo-claro.svg'
import logoOscuro from '@/assets/marca/logo-oscuro.svg'

const s = useSesion()
const email = ref('')
const error = ref<string | null>(null)
const mensaje = ref<string | null>(null)
const cargando = ref(false)

async function enviar() {
  error.value = null
  const e = email.value.trim()
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e)) { error.value = 'Escribe un correo válido.'; return }
  cargando.value = true
  try {
    const r = await api.post<{ mensaje: string }>('/auth/recuperar', { email: e })
    mensaje.value = r.mensaje
  } catch (err) {
    error.value = err instanceof ErrorApi ? err.message : 'No se pudo enviar el enlace. Intenta de nuevo.'
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
      <form class="caja" novalidate @submit.prevent="enviar">
        <img :src="logoClaro" alt="WOW! Time" class="logo-movil" />
        <h1 class="titulo-pagina">Recupera tu contraseña</h1>
        <p class="subtitulo-pagina">Te enviaremos un enlace para crear una nueva. Es válido por 1 hora.</p>
        <template v-if="!mensaje">
          <div class="campo">
            <label for="email">Correo</label>
            <div class="con-icono"><Icono n="correo" :t="19" /><input id="email" v-model="email" class="entrada" type="email" autocomplete="username" placeholder="nombre@empresa.mx" /></div>
          </div>
          <p v-if="error" class="caja-alerta err" role="alert"><Icono n="alertaCirculo" :t="20" />{{ error }}</p>
          <button class="btn btn-primario btn-g btn-bloque" :disabled="cargando" data-prueba="enviar-enlace">
            <span v-if="cargando" class="spinner" style="border-top-color:#fff;width:18px;height:18px" />Enviar enlace
          </button>
        </template>
        <template v-else>
          <p class="caja-ok listo" role="status" data-prueba="mensaje-recuperar"><Icono n="correo" :t="22" />{{ mensaje }}</p>
          <p class="muted">Revisa también tu carpeta de correo no deseado. Si no llega en unos minutos, pide ayuda a Recursos Humanos.</p>
          <button type="button" class="btn btn-contorno btn-bloque" @click="mensaje = null">Usar otro correo</button>
        </template>
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
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
.listo { align-items: flex-start; font-weight: 500; line-height: 1.4; }
.olvido { display: inline-flex; align-items: center; justify-content: center; gap: 6px; font-weight: 500; }
@media (max-width: 860px) { .login { grid-template-columns: 1fr; } .marca { display: none; } .logo-movil { display: block; } }
</style>
