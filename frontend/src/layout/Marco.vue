<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Campana from './Campana.vue'
import Buscador from './Buscador.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useMenu } from './menu'
import { useEsMovil } from '@/utils/usos'
import { fechaLarga } from '@/utils/formato'
import logoOscuro from '@/assets/marca/logo-oscuro.svg'

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()
const menu = useMenu()
const esMovil = useEsMovil()
const menuUsuario = ref(false)
const selSucursal = ref(false)
const hoy = ref(new Date())
let reloj: ReturnType<typeof setInterval>

/** Autoservicio en teléfono: barra inferior en lugar de menú lateral. */
const conBarraInferior = computed(() => esMovil.value && !s.esGestion)
const activo = computed(() => route.meta.menu as string)
const tab = computed(() => (route.meta.tab as string) ?? (route.path === '/' ? 'inicio' : route.path.startsWith('/mi-asistencia') ? 'asistencia' : route.path.startsWith('/perfil') ? 'perfil' : ''))
const piePagina = computed(() => s.frase('menu_pie'))

watch(() => route.fullPath, () => { ui.menuAbierto = false; menuUsuario.value = false; selSucursal.value = false })
onMounted(() => { reloj = setInterval(() => (hoy.value = new Date()), 60000); document.addEventListener('mousedown', cerrarMenus) })
onBeforeUnmount(() => { clearInterval(reloj); document.removeEventListener('mousedown', cerrarMenus) })
function cerrarMenus(e: MouseEvent) {
  const t = e.target as HTMLElement
  if (!t.closest('.menu-usuario')) menuUsuario.value = false
  if (!t.closest('.sel-sucursal')) selSucursal.value = false
}
async function salir() { await s.salir(); router.push('/login') }
function elegir(id: number) { s.elegirSucursal(id); selSucursal.value = false }
</script>

<template>
  <div class="marco" :class="{ 'con-barra': conBarraInferior }">
    <!-- Menú lateral -->
    <aside v-if="!conBarraInferior" class="lateral" :class="{ abierto: ui.menuAbierto }">
      <RouterLink to="/" class="logo" aria-label="WOW! Time, ir a Inicio">
        <img :src="logoOscuro" alt="WOW! Time" />
      </RouterLink>
      <p v-if="s.frase('logo_lema')" class="lema">{{ s.frase('logo_lema') }}</p>
      <nav class="nav">
        <RouterLink v-for="i in menu" :key="i.clave" :to="i.ruta" class="item" :class="{ activo: activo === i.clave }" :data-menu="i.clave">
          <Icono :n="i.icono" :t="23" :g="1.9" />
          <span>{{ i.texto }}</span>
        </RouterLink>
      </nav>
      <div class="pie">
        <svg class="decoracion" viewBox="0 0 120 120" aria-hidden="true">
          <path d="M30 108c-6-28 4-52 34-66 4 26-4 52-34 66z" fill="#0aaa4a" opacity=".9" />
          <path d="M30 108c8-20 18-34 32-48" stroke="#0c1f38" stroke-width="2.4" fill="none" />
          <path d="M84 14l2.4 6.6 6.6 2.4-6.6 2.4L84 32l-2.4-6.6-6.6-2.4 6.6-2.4zM100 44l1.4 3.6 3.6 1.4-3.6 1.4-1.4 3.6-1.4-3.6-3.6-1.4 3.6-1.4zM18 30l1.2 3 3 1.2-3 1.2-1.2 3-1.2-3-3-1.2 3-1.2z" fill="#fff" opacity=".9" />
          <path d="M66 70l1 2.6 2.6 1-2.6 1-1 2.6-1-2.6-2.6-1 2.6-1zM104 82l1 2.6 2.6 1-2.6 1-1 2.6-1-2.6-2.6-1 2.6-1z" fill="#2fd27a" />
        </svg>
        <p class="frase">{{ piePagina }}</p>
        <span class="raya" />
      </div>
    </aside>
    <div v-if="ui.menuAbierto" class="velo-menu" @click="ui.menuAbierto = false" />

    <div class="principal">
      <!-- Barra superior -->
      <header v-if="!conBarraInferior" class="superior">
        <button class="btn-icono hamburguesa" aria-label="Abrir menú" @click="ui.menuAbierto = !ui.menuAbierto"><Icono n="menu" /></button>
        <template v-if="s.esGestion">
          <div class="sel-sucursal">
            <button class="sucursal" data-prueba="selector-sucursal" @click="selSucursal = !selSucursal">
              <Icono n="ubicacion" :t="22" class="pin" /><span>{{ s.sucursal?.nombre ?? 'Todas las sucursales' }}</span><Icono n="abajo" :t="18" />
            </button>
            <div v-if="selSucursal" class="desplegable">
              <button v-for="x in s.me?.sucursales" :key="x.id" class="op" :class="{ sel: x.id === s.sucursalId }" @click="elegir(x.id)">
                <Icono n="edificio" :t="18" />{{ x.nombre }}<Icono v-if="x.id === s.sucursalId" n="check" :t="18" class="ok" />
              </button>
            </div>
          </div>
          <span class="divisor" />
          <div class="fecha"><Icono n="calendario" :t="22" :g="1.7" /><span>{{ fechaLarga(hoy) }}</span></div>
          <Buscador class="buscador" />
        </template>
        <div class="crecer" />
        <Campana />
        <div class="menu-usuario">
          <button class="usuario" data-prueba="menu-usuario" @click="menuUsuario = !menuUsuario">
            <Avatar :nombre="s.me?.nombre" :iniciales="s.me?.colaborador?.iniciales" :foto="s.me?.colaborador?.fotoUrl" :t="44" />
            <span class="datos"><b>{{ s.me?.nombre }}</b><small v-if="s.esGestion">{{ s.rolTexto }}</small></span>
            <Icono n="abajo" :t="20" />
          </button>
          <div v-if="menuUsuario" class="desplegable derecha">
            <div class="cab-usuario"><b>{{ s.me?.nombre }}</b><small>{{ s.me?.email }}</small></div>
            <RouterLink to="/perfil" class="op"><Icono n="persona" :t="18" />Mi perfil</RouterLink>
            <template v-if="s.esGestion && s.me?.colaborador">
              <RouterLink to="/mi-asistencia" class="op"><Icono n="calendario" :t="18" />Mi asistencia</RouterLink>
              <RouterLink to="/mis-vacaciones" class="op"><Icono n="vacaciones" :t="18" />Mis vacaciones</RouterLink>
              <RouterLink to="/mi-expediente" class="op"><Icono n="expedientes" :t="18" />Mi expediente</RouterLink>
              <RouterLink to="/aprobaciones" class="op"><Icono n="bandeja" :t="18" />Bandeja de aprobaciones</RouterLink>
            </template>
            <RouterLink v-if="s.me?.colaborador" to="/mis-invitados" class="op"><Icono n="personaMas" :t="18" />Mis invitados</RouterLink>
            <RouterLink to="/perfil#contrasena" class="op"><Icono n="candado" :t="18" />Cambiar contraseña</RouterLink>
            <button class="op salir" data-prueba="salir" @click="salir"><Icono n="salir" :t="18" />Cerrar sesión</button>
          </div>
        </div>
      </header>

      <main class="contenido" :class="{ movil: conBarraInferior }">
        <RouterView v-slot="{ Component }">
          <component :is="Component" :key="route.path" />
        </RouterView>
      </main>
    </div>

    <!-- Barra inferior móvil (colaborador en teléfono) -->
    <nav v-if="conBarraInferior" class="inferior" aria-label="Navegación principal">
      <RouterLink to="/" :class="{ act: tab === 'inicio' }"><Icono n="inicio" :t="24" :g="1.8" /><span>Inicio</span></RouterLink>
      <RouterLink :to="s.esRecepcion ? '/control-acceso' : '/m/asistencia'" :class="{ act: tab === 'asistencia' }">
        <Icono :n="s.esRecepcion ? 'escanear' : 'reloj'" :t="24" :g="1.8" /><span>{{ s.esRecepcion ? 'Acceso' : 'Asistencia' }}</span>
      </RouterLink>
      <RouterLink to="/mis-solicitudes" :class="{ act: tab === 'solicitudes' }"><Icono n="documento" :t="24" :g="1.8" /><span>Solicitudes</span></RouterLink>
      <RouterLink to="/perfil" :class="{ act: tab === 'perfil' }"><Icono n="persona" :t="24" :g="1.8" /><span>Perfil</span></RouterLink>
    </nav>
  </div>
</template>

<style scoped>
.marco { display: flex; min-height: 100dvh; }
.lateral {
  width: var(--menu-ancho); flex-shrink: 0; background: linear-gradient(180deg, var(--navy) 0%, var(--navy-2) 100%); color: #fff;
  position: sticky; top: 0; height: 100dvh; display: flex; flex-direction: column; padding: 30px 12px 22px; z-index: 60; overflow-y: auto;
}
.logo { display: block; padding: 0 14px 0 14px; margin-bottom: 4px; }
.logo img { width: 188px; height: auto; display: block; }
.lema { color: #8fd8b3; font-size: 13px; padding: 4px 16px 0; line-height: 1.3; opacity: .85; }
.nav { display: flex; flex-direction: column; gap: 6px; margin-top: 30px; }
.item {
  display: flex; align-items: center; gap: 15px; height: 52px; padding: 0 16px; border-radius: 9px; color: rgba(255, 255, 255, .92);
  font-size: 17.5px; font-weight: 450; text-decoration: none !important; transition: background .15s;
}
.item:hover { background: rgba(255, 255, 255, .07); }
.item.activo { background: var(--verde); color: #fff; font-weight: 550; box-shadow: 0 4px 12px rgba(10, 170, 74, .25); }
.pie { margin-top: auto; padding: 24px 12px 0; }
.decoracion { width: 110px; height: 110px; display: block; margin-left: -6px; }
.frase { color: rgba(255, 255, 255, .78); font-size: 15px; line-height: 1.4; margin-top: 6px; max-width: 180px; }
.raya { display: block; width: 36px; height: 3px; border-radius: 3px; background: var(--verde); margin-top: 14px; }

.principal { flex: 1; min-width: 0; display: flex; flex-direction: column; }
.superior {
  height: var(--barra-alto); background: #fff; border-bottom: 1px solid var(--gris-200); display: flex; align-items: center; gap: 14px;
  padding: 0 26px; position: sticky; top: 0; z-index: 50;
}
.hamburguesa { display: none; }
.sel-sucursal { position: relative; }
.sucursal { display: flex; align-items: center; gap: 10px; background: none; border: 0; font-size: 16px; color: var(--titulo); padding: 8px 6px; border-radius: 8px; font-weight: 500; }
.sucursal:hover { background: var(--gris-50); }
.sucursal .pin { color: var(--verde); }
.divisor { width: 1px; height: 30px; background: var(--gris-200); }
.fecha { display: flex; align-items: center; gap: 10px; font-size: 16px; color: var(--titulo); white-space: nowrap; }
.fecha .icono { color: var(--texto-2); }
.buscador { margin-left: 18px; }
.menu-usuario { position: relative; }
.usuario { display: flex; align-items: center; gap: 12px; background: none; border: 0; padding: 4px 6px; border-radius: 10px; text-align: left; color: var(--titulo); }
.usuario:hover { background: var(--gris-50); }
.datos { display: flex; flex-direction: column; line-height: 1.25; }
.datos b { font-size: 16px; font-weight: 600; }
.datos small { font-size: 13px; color: var(--texto-2); }
.desplegable { position: absolute; top: calc(100% + 8px); left: 0; min-width: 240px; background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; box-shadow: var(--sombra-m); padding: 6px; z-index: 80; }
.desplegable.derecha { left: auto; right: 0; }
.op { display: flex; align-items: center; gap: 10px; width: 100%; padding: 10px 12px; border-radius: 8px; border: 0; background: none; color: var(--titulo); font-size: 14.5px; text-align: left; text-decoration: none !important; }
.op:hover { background: var(--gris-100); }
.op.sel { font-weight: 600; }
.op .ok { margin-left: auto; color: var(--verde); }
.op.salir { color: var(--rojo-texto); border-top: 1px solid var(--gris-200); border-radius: 0 0 8px 8px; margin-top: 4px; }
.cab-usuario { padding: 10px 12px 12px; border-bottom: 1px solid var(--gris-200); margin-bottom: 4px; display: flex; flex-direction: column; }
.cab-usuario small { color: var(--texto-3); }

.contenido { flex: 1; padding: 26px 26px 40px; max-width: 1640px; width: 100%; }
.contenido.movil { padding: 0 0 92px; }

.inferior {
  position: fixed; bottom: 0; left: 0; right: 0; height: calc(70px + env(safe-area-inset-bottom)); padding-bottom: env(safe-area-inset-bottom);
  background: #fff; border-top: 1px solid var(--gris-200); display: grid; grid-template-columns: repeat(4, 1fr); z-index: 70;
}
.inferior a { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px; font-size: 12px; color: var(--texto-3); text-decoration: none; position: relative; }
.inferior a.act { color: var(--verde-osc); font-weight: 650; }
.inferior a.act::after { content: ''; position: absolute; bottom: 6px; width: 28px; height: 3px; border-radius: 3px; background: var(--verde); }
.velo-menu { display: none; }

@media (max-width: 1100px) {
  .buscador { display: none; }
}
@media (max-width: 900px) {
  .lateral { position: fixed; left: 0; top: 0; transform: translateX(-100%); transition: transform .2s; box-shadow: var(--sombra-m); }
  .lateral.abierto { transform: none; }
  .velo-menu { display: block; position: fixed; inset: 0; background: rgba(12, 20, 48, .4); z-index: 55; }
  .hamburguesa { display: inline-flex; }
  .superior { padding: 0 14px; gap: 8px; }
  .fecha, .divisor { display: none; }
  .datos { display: none; }
  .contenido { padding: 18px 16px 40px; }
}
</style>
