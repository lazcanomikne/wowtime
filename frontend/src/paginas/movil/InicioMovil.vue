<script setup lang="ts">
// Inicio móvil (App_Movil.png, vista 1): saludo, botón de registro, jornada del día, accesos rápidos y estado.
import { computed, onMounted, ref } from 'vue'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Campana from '@/layout/Campana.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useRelojServidor } from '@/utils/reloj'
import { fechaLarga, hora, num, primerNombre, saludo } from '@/utils/formato'
import logoOscuro from '@/assets/marca/logo-oscuro.svg'

const s = useSesion()
const jornada = ref<any>(null)
const porAprobar = ref(0)
const { ahora, sincronizar } = useRelojServidor()

async function cargar() {
  try { jornada.value = await api.get('/asistencia/jornada-hoy'); sincronizar(jornada.value.horaServidor) } catch { /* sin conexión: se muestran los últimos datos de la caché */ }
  if (s.tiene('aprobaciones.decidir')) { try { porAprobar.value = (await api.get('/aprobaciones/bandeja')).total } catch { /* nada */ } }
  s.cargar().catch(() => {})
}
onMounted(cargar)

const nombre = computed(() => primerNombre(s.me?.colaborador?.nombres?.split(' ').length && s.me?.colaborador?.nombres.includes(' ') ? s.me.colaborador.nombres : s.me?.colaborador?.nombres) || primerNombre(s.me?.nombre))
const nombres = computed(() => s.me?.colaborador?.nombres ?? primerNombre(s.me?.nombre))
const j = computed(() => jornada.value?.jornada)
const siguiente = computed(() => jornada.value?.siguiente as 'Entrada' | 'Salida' | null | undefined)
const textoJornada = computed(() => {
  if (!j.value) return ''
  if (j.value.ausencia) return j.value.ausencia
  if (j.value.festivo) return `Día festivo · ${j.value.festivo}`
  const h = j.value.horaEntrada ? ` · ${hora(j.value.horaEntrada)} – ${hora(j.value.horaSalida)}` : ''
  return `${j.value.modalidad === 'HomeOffice' ? 'Home office' : j.value.lugar}${h}`
})
const estadoOk = computed(() => !jornada.value?.estadoEntrada || jornada.value.estadoEntrada === 'EnHorario')
</script>

<template>
  <div class="inicio-m">
    <header class="cab">
      <div class="fila-entre top">
        <img :src="logoOscuro" alt="WOW! Time" class="logo" />
        <Campana claro />
      </div>
      <div class="fila-entre saludo">
        <div>
          <h1>{{ saludo(ahora) }},<br>{{ nombres }}</h1>
          <p>{{ fechaLarga(ahora) }}</p>
        </div>
        <RouterLink to="/perfil" class="perfil" aria-label="Perfil">
          <Avatar :nombre="s.me?.nombre" :iniciales="s.me?.colaborador?.iniciales" :foto="s.me?.colaborador?.fotoUrl" :t="56" tono="verde" />
          <span class="lema">{{ s.frase('movil_inicio_lema') }} <Icono n="hoja" :t="12" class="hoja" /></span>
        </RouterLink>
      </div>
    </header>

    <div class="cuerpo">
      <RouterLink v-if="siguiente" to="/m/asistencia" class="registrar" data-prueba="boton-registrar">
        <Icono n="reloj" :t="44" :g="1.7" />
        <span class="crecer"><span class="t">Registrar {{ siguiente === 'Entrada' ? 'entrada' : 'salida' }}</span><b>{{ hora(ahora) }}</b></span>
        <Icono n="derecha" :t="24" />
      </RouterLink>
      <RouterLink v-else-if="jornada" to="/mi-asistencia" class="registrar hecho">
        <Icono n="checkCirculo" :t="44" :g="1.7" />
        <span class="crecer"><span class="t">Jornada concluida</span><b>{{ hora(jornada.entrada) }} – {{ hora(jornada.salida) }}</b></span>
        <Icono n="derecha" :t="24" />
      </RouterLink>

      <div v-if="j" class="jornada">
        <Icono :n="j.modalidad === 'HomeOffice' ? 'hogar' : 'edificio'" :t="30" class="verde" />
        <div><span>Hoy trabajas:</span><b>{{ textoJornada }}</b></div>
      </div>

      <div class="rapidos">
        <RouterLink to="/m/vacaciones" class="rapido">
          <Icono n="palmera" :t="34" :g="1.8" class="verde" />
          <span class="fila-entre"><b>Solicitar<br>vacaciones</b><Icono n="derecha" :t="20" /></span>
        </RouterLink>
        <RouterLink to="/m/invitado" class="rapido">
          <Icono n="personaMas" :t="34" :g="1.8" class="verde" />
          <span class="fila-entre"><b>Registrar<br>invitado</b><Icono n="derecha" :t="20" /></span>
        </RouterLink>
        <RouterLink v-if="s.tiene('aprobaciones.decidir')" to="/aprobaciones" class="rapido">
          <Icono n="bandeja" :t="34" :g="1.8" class="verde" />
          <span class="fila-entre"><b>Por aprobar<br><span class="cuenta">{{ porAprobar }} pendientes</span></b><Icono n="derecha" :t="20" /></span>
        </RouterLink>
        <RouterLink v-if="s.esRecepcion" to="/control-acceso" class="rapido">
          <Icono n="escanear" :t="34" :g="1.8" class="verde" />
          <span class="fila-entre"><b>Control<br>de acceso</b><Icono n="derecha" :t="20" /></span>
        </RouterLink>
      </div>

      <div class="fila-entre seccion">
        <h2>Mi asistencia</h2>
        <RouterLink to="/mi-asistencia" class="det">Ver detalles <Icono n="derecha" :t="16" /></RouterLink>
      </div>
      <div class="rapidos">
        <RouterLink to="/mi-asistencia" class="estado">
          <span class="circ" :class="estadoOk ? 'ok' : 'warn'"><Icono :n="estadoOk ? 'check' : 'alerta'" :t="22" :g="2.6" /></span>
          <span><b>{{ jornada?.estado ?? '—' }}</b><small>{{ jornada?.mensaje ?? '' }}</small></span>
        </RouterLink>
        <RouterLink to="/m/vacaciones" class="estado">
          <Icono n="calendario" :t="30" :g="1.8" class="verde" />
          <span><b>{{ num(s.me?.saldo?.disponibles, 1) }} días disponibles</b><small>de vacaciones</small></span>
        </RouterLink>
      </div>
    </div>
  </div>
</template>

<style scoped>
.inicio-m { min-height: 100%; background: var(--fondo); }
.cab { background: var(--navy); color: #fff; padding: calc(16px + env(safe-area-inset-top)) 18px 64px; }
.top { margin-bottom: 18px; }
.logo { width: 118px; }
.saludo { align-items: flex-start; }
h1 { color: #fff; font-size: 24px; font-weight: 600; line-height: 1.25; letter-spacing: -0.01em; }
.saludo p { color: rgba(255,255,255,.88); font-size: 15px; margin-top: 6px; }
.perfil { display: flex; flex-direction: column; align-items: center; gap: 6px; text-decoration: none !important; max-width: 96px; }
.lema { color: rgba(255,255,255,.85); font-size: 9.5px; line-height: 1.2; text-align: center; }
.hoja { color: var(--verde); }
.cuerpo { padding: 0 14px 20px; margin-top: -48px; display: flex; flex-direction: column; gap: 12px; }
.registrar { display: flex; align-items: center; gap: 16px; background: linear-gradient(180deg, #13a85a, #0b9a4e); color: #fff; border-radius: 14px; padding: 18px 16px 18px 20px; box-shadow: 0 8px 22px rgba(10, 150, 80, .3); text-decoration: none !important; }
.registrar .t { display: block; font-size: 17px; font-weight: 500; }
.registrar b { display: block; font-size: 32px; font-weight: 700; letter-spacing: .01em; line-height: 1.1; }
.registrar.hecho { background: linear-gradient(180deg, #1d3d68, #102848); box-shadow: none; }
.jornada { display: flex; align-items: center; gap: 14px; background: var(--gris-100); border: 1px solid var(--gris-200); border-radius: 12px; padding: 12px 16px; }
.jornada span { display: block; font-size: 14px; color: var(--titulo); }
.jornada b { font-size: 15px; font-weight: 600; color: var(--titulo); }
.verde { color: var(--verde-osc); }
.rapidos { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.rapido { background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; padding: 14px; display: flex; flex-direction: column; gap: 8px; color: var(--titulo); text-decoration: none !important; box-shadow: var(--sombra); }
.rapido b { font-size: 14.5px; font-weight: 600; line-height: 1.25; }
.cuenta { font-weight: 500; color: var(--verde-texto); font-size: 13px; }
.seccion { margin-top: 6px; }
.seccion h2 { font-size: 18px; font-weight: 700; }
.det { color: var(--texto-2); font-size: 14px; display: flex; align-items: center; gap: 2px; }
.estado { background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; padding: 14px 12px; display: flex; align-items: center; gap: 12px; color: var(--titulo); text-decoration: none !important; box-shadow: var(--sombra); }
.estado b { display: block; font-size: 14.5px; font-weight: 600; line-height: 1.25; }
.estado small { display: block; font-size: 12.5px; color: var(--texto-3); }
.circ { width: 36px; height: 36px; border-radius: 50%; display: grid; place-items: center; color: #fff; flex-shrink: 0; }
.circ.ok { background: var(--verde); }
.circ.warn { background: var(--naranja); }
</style>
