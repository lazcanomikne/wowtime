<script setup lang="ts">
// Página pública del QR del visitante (sin sesión): saludo, código, anfitrión, lugar, llegada y vigencia.
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { api, ErrorApi } from '@/api'
import { fechaLarga, hora } from '@/utils/formato'
import Icono from '@/componentes/Icono.vue'
import Qr from '@/componentes/Qr.vue'
import logoClaro from '@/assets/marca/logo-claro.svg'

interface Publico {
  nombre: string; anfitrion: string | null; sucursal: string | null; direccion: string | null
  llegada: string; vigenciaDesde: string; vigenciaHasta: string; estado: string; qr: string
}

const route = useRoute()
const d = ref<Publico | null>(null)
const cargando = ref(true)
const error = ref<string | null>(null)

async function cargar() {
  cargando.value = true; error.value = null
  try {
    d.value = await api.get<Publico>(`/publico/qr/${encodeURIComponent(String(route.params.codigo))}`)
  } catch (e) {
    error.value = e instanceof ErrorApi && e.status === 404 ? 'No encontramos esta invitación. Revisa el enlace o pide a tu anfitrión que te lo envíe de nuevo.' : (e instanceof Error ? e.message : 'No se pudo cargar la invitación.')
  } finally { cargando.value = false }
}
onMounted(cargar)

const valido = computed(() => d.value?.estado === 'Esperado' || d.value?.estado === 'Dentro')
const aviso = computed(() => {
  switch (d.value?.estado) {
    case 'Vencida': return { clase: 'rojo', titulo: 'Invitación vencida', texto: 'Este código ya no es válido. Pide a tu anfitrión una nueva invitación.' }
    case 'Cancelada': return { clase: 'rojo', titulo: 'Invitación cancelada', texto: 'Tu anfitrión canceló esta invitación.' }
    case 'Salio': return { clase: 'gris', titulo: 'Visita concluida', texto: 'Ya registraste tu salida. Este código ya no es válido.' }
    case 'Dentro': return { clase: 'verde', titulo: 'Ya estás dentro', texto: 'Presenta este mismo código en recepción al salir.' }
    default: return null
  }
})
const mapa = computed(() => d.value?.direccion ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(d.value.direccion)}` : null)
</script>

<template>
  <main class="qr-publico">
    <div class="contenedor">
      <img :src="logoClaro" alt="WOW! Time" class="logo" />

      <div v-if="cargando" class="tarjeta centro">
        <div class="spinner" style="margin: 30px auto" />
      </div>

      <div v-else-if="error" class="tarjeta centro">
        <span class="circulo rojo"><Icono n="alertaCirculo" :t="34" /></span>
        <h1 class="titulo">Invitación no disponible</h1>
        <p class="muted">{{ error }}</p>
        <button class="btn btn-contorno" style="margin-top: 16px" @click="cargar"><Icono n="actualizar" :t="18" />Reintentar</button>
      </div>

      <template v-else-if="d">
        <section class="tarjeta principal">
          <h1 class="titulo">Hola, {{ d.nombre }}</h1>
          <p class="instruccion">Presenta este código en recepción</p>

          <div class="qr-caja" :class="{ inactivo: !valido }">
            <Qr :valor="d.qr" :t="260" />
          </div>

          <div v-if="aviso" class="estado" :class="aviso.clase" role="status">
            <Icono :n="aviso.clase === 'verde' ? 'checkCirculo' : 'alertaCirculo'" :t="22" />
            <div><b>{{ aviso.titulo }}</b><span>{{ aviso.texto }}</span></div>
          </div>

          <div class="vigencia">
            <Icono n="reloj" :t="18" />Válido de {{ hora(d.vigenciaDesde) }} a {{ hora(d.vigenciaHasta) }}
          </div>
        </section>

        <section class="tarjeta datos">
          <div class="dato">
            <span class="icono-dato"><Icono n="calendario" :t="20" /></span>
            <div>
              <small>Fecha y hora de llegada</small>
              <b>{{ fechaLarga(d.llegada, true) }}</b>
              <span>{{ hora(d.llegada) }} h</span>
            </div>
          </div>
          <div v-if="d.anfitrion" class="dato">
            <span class="icono-dato"><Icono n="persona" :t="20" /></span>
            <div>
              <small>Te recibe</small>
              <b>{{ d.anfitrion }}</b>
            </div>
          </div>
          <div v-if="d.sucursal || d.direccion" class="dato">
            <span class="icono-dato"><Icono n="ubicacion" :t="20" /></span>
            <div>
              <small>Lugar</small>
              <b>{{ d.sucursal }}</b>
              <span v-if="d.direccion">{{ d.direccion }}</span>
              <a v-if="mapa" :href="mapa" target="_blank" rel="noopener" class="como-llegar">Cómo llegar <Icono n="externo" :t="14" /></a>
            </div>
          </div>
        </section>

        <p class="nota"><Icono n="escudo" :t="18" />Válido por tiempo limitado y solo para el acceso a nuestras instalaciones.</p>
      </template>
    </div>
  </main>
</template>

<style scoped>
.qr-publico { min-height: 100dvh; background: var(--fondo); padding: calc(20px + env(safe-area-inset-top)) 16px calc(28px + env(safe-area-inset-bottom)); }
.contenedor { max-width: 440px; margin: 0 auto; display: flex; flex-direction: column; gap: 16px; }
.logo { width: 150px; height: auto; display: block; margin: 4px auto 8px; }
.centro { text-align: center; display: flex; flex-direction: column; align-items: center; gap: 8px; }
.principal { display: flex; flex-direction: column; align-items: center; text-align: center; padding: 24px 20px; }
.titulo { font-size: 26px; font-weight: 800; letter-spacing: -.02em; color: var(--titulo); overflow-wrap: anywhere; }
.instruccion { font-size: 16px; color: var(--texto-2); margin-top: 4px; }
.qr-caja { margin: 20px 0 16px; padding: 14px; border-radius: 18px; border: 1px solid var(--gris-200); background: #fff; box-shadow: var(--sombra); max-width: 100%; }
.qr-caja :deep(.qr) { max-width: 100%; height: auto !important; }
.qr-caja.inactivo :deep(.qr) { opacity: .15; filter: grayscale(1); }
.vigencia { display: inline-flex; align-items: center; gap: 8px; padding: 8px 14px; border-radius: 999px; background: var(--verde-claro); color: var(--verde-texto); font-weight: 650; font-size: 15px; }
.estado { display: flex; gap: 10px; align-items: flex-start; text-align: left; width: 100%; padding: 12px 14px; border-radius: var(--radio-s); margin-bottom: 14px; font-size: 14px; }
.estado b { display: block; font-size: 15px; }
.estado span { display: block; }
.estado.rojo { background: var(--rojo-claro); color: var(--rojo-texto); }
.estado.gris { background: var(--gris-100); color: var(--texto-2); }
.estado.verde { background: var(--verde-claro); color: var(--verde-texto); }
.datos { display: flex; flex-direction: column; gap: 16px; padding: 18px 18px; }
.dato { display: flex; gap: 12px; align-items: flex-start; }
.dato > div { display: flex; flex-direction: column; min-width: 0; }
.dato small { font-size: 13px; color: var(--texto-3); }
.dato b { font-size: 15.5px; color: var(--titulo); }
.dato span { font-size: 14px; color: var(--texto-2); }
.icono-dato { width: 40px; height: 40px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-osc); display: grid; place-items: center; flex-shrink: 0; }
.como-llegar { font-size: 14px; font-weight: 600; margin-top: 4px; display: inline-flex; align-items: center; gap: 4px; }
.nota { display: flex; gap: 10px; align-items: center; justify-content: center; text-align: left; color: var(--texto-2); font-size: 13.5px; padding: 0 8px; }
.circulo { width: 64px; height: 64px; border-radius: 50%; display: grid; place-items: center; }
.circulo.rojo { background: var(--rojo-claro); color: var(--rojo); }
</style>
