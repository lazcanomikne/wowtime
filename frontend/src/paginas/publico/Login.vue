<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import { useSesion } from '@/stores/sesion'
import { ErrorApi } from '@/api'
import logoClaro from '@/assets/marca/logo-claro.svg'
import logoOscuro from '@/assets/marca/logo-oscuro.svg'

const s = useSesion()
const route = useRoute()
const router = useRouter()
const email = ref('')
const password = ref('')
const ver = ref(false)
const error = ref<string | null>(route.query.expirada ? 'Tu sesión expiró. Inicia sesión de nuevo.' : null)
const cargando = ref(false)

async function entrar() {
  error.value = null
  if (!email.value || !password.value) { error.value = 'Escribe tu correo y tu contraseña.'; return }
  cargando.value = true
  try {
    const u = await s.entrar(email.value.trim(), password.value)
    router.replace(u.debeCambiarPassword ? '/perfil#contrasena' : (route.query.r as string) || '/')
  } catch (e) {
    error.value = e instanceof ErrorApi ? e.message : 'No se pudo iniciar sesión.'
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
      <form class="caja" novalidate @submit.prevent="entrar">
        <img :src="logoClaro" alt="WOW! Time" class="logo-movil" />
        <h1 class="titulo-pagina">Inicia sesión</h1>
        <p class="subtitulo-pagina">Registra tu asistencia, solicita vacaciones y más.</p>
        <div class="campo">
          <label for="email">Correo</label>
          <div class="con-icono"><Icono n="correo" :t="19" /><input id="email" v-model="email" class="entrada" type="email" autocomplete="username" placeholder="nombre@empresa.mx" /></div>
        </div>
        <div class="campo">
          <label for="pwd">Contraseña</label>
          <div class="con-icono">
            <Icono n="candado" :t="19" />
            <input id="pwd" v-model="password" class="entrada" :type="ver ? 'text' : 'password'" autocomplete="current-password" placeholder="Tu contraseña" />
            <button type="button" class="ver" :aria-label="ver ? 'Ocultar contraseña' : 'Mostrar contraseña'" @click="ver = !ver"><Icono n="ojo" :t="19" /></button>
          </div>
        </div>
        <p v-if="error" class="caja-alerta err" role="alert"><Icono n="alertaCirculo" :t="20" />{{ error }}</p>
        <button class="btn btn-primario btn-g btn-bloque" :disabled="cargando" data-prueba="entrar">
          <span v-if="cargando" class="spinner" style="border-top-color:#fff;width:18px;height:18px" />Entrar
        </button>
        <RouterLink to="/recuperar" class="olvido">¿Olvidaste tu contraseña?</RouterLink>
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
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
.olvido { text-align: center; font-weight: 500; }
@media (max-width: 860px) { .login { grid-template-columns: 1fr; } .marca { display: none; } .logo-movil { display: block; } }
</style>
