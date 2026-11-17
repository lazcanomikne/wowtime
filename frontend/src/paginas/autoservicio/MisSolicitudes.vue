<script setup lang="ts">
// Solicitudes del colaborador (pestaña "Solicitudes" de la barra inferior): accesos rápidos, pendientes por aprobar y mis solicitudes.
import { computed, ref, watch } from 'vue'
import { api, type Pagina } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useCarga, useEsMovil, useEventos } from '@/utils/usos'
import { dias, momento, rangoLargo } from '@/utils/formato'
import Icono from '@/componentes/Icono.vue'
import Estado from '@/componentes/Estado.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'

interface Fila {
  id: number; folio: string; tipo: string; tipoTexto: string; fechaInicio: string; fechaFin: string; diasHabiles: number
  estado: string; pasoActualTexto: string | null; creadoEn: string; motivoRechazo: string | null
}

const s = useSesion()
const esMovil = useEsMovil()
const conEncabezadoMovil = computed(() => esMovil.value && !s.esGestion)
const rutaVacaciones = computed(() => (esMovil.value ? '/m/vacaciones' : '/mis-vacaciones'))
const puedeAprobar = computed(() => s.tiene('aprobaciones.decidir'))

const FILTROS = [
  { v: '', t: 'Todas' }, { v: 'Pendiente', t: 'Pendientes' }, { v: 'Aprobada', t: 'Aprobadas' }, { v: 'Rechazada', t: 'Rechazadas' }, { v: 'Cancelada', t: 'Canceladas' }
]
const estado = ref('')
const pagina = ref(1)
const TAMANO = 15

const lista = useCarga(() => api.get<Pagina<Fila>>('/solicitudes', { mias: true, estado: estado.value, pagina: pagina.value, tamano: TAMANO, orden: 'CreadoEn', dir: 'desc' }))
const bandeja = useCarga(async () => (puedeAprobar.value ? api.get<{ total: number }>('/aprobaciones/bandeja') : null))
watch(estado, () => { pagina.value = 1; lista.cargar() })
function ir(p: number) { pagina.value = p; lista.cargar() }
useEventos(t => { if (t === 'solicitud') { lista.cargar(true); bandeja.cargar(true) } })

function icono(e: string) {
  return e === 'Aprobada' ? 'checkCirculo' : e === 'Rechazada' ? 'x' : e === 'Cancelada' ? 'x' : 'reloj'
}
function descripcion(f: Fila) {
  if (f.estado === 'Pendiente') return f.pasoActualTexto ? `En revisión: ${f.pasoActualTexto}` : 'Solicitud enviada'
  if (f.estado === 'Rechazada') return f.motivoRechazo ? `Rechazada: ${f.motivoRechazo}` : 'Rechazada'
  return f.estado === 'Aprobada' ? 'Aprobada' : 'Cancelada'
}
</script>

<template>
  <div class="mis-solicitudes" :class="{ movil: conEncabezadoMovil }">
    <EncabezadoMovil v-if="conEncabezadoMovil" titulo="Solicitudes" atras="/" />

    <div class="cuerpo">
      <div v-if="!conEncabezadoMovil" class="encabezado">
        <div>
          <h1 class="titulo-pagina">Solicitudes</h1>
          <p class="subtitulo-pagina">Solicita vacaciones o permisos y da seguimiento a su aprobación</p>
        </div>
      </div>

      <!-- Accesos rápidos -->
      <div class="accesos">
        <RouterLink :to="rutaVacaciones" class="acceso" data-prueba="solicitar-vacaciones">
          <span class="acceso-icono verde"><Icono n="palmera" :t="26" /></span>
          <span class="crecer"><b>Solicitar vacaciones</b><small v-if="s.me?.saldo">{{ dias(s.me.saldo.disponibles) }} disponibles</small></span>
          <Icono n="derecha" :t="20" class="flecha" />
        </RouterLink>
        <RouterLink to="/solicitar-permiso" class="acceso" data-prueba="solicitar-permiso">
          <span class="acceso-icono azul"><Icono n="calendario" :t="24" /></span>
          <span class="crecer"><b>Solicitar permiso</b><small>Con o sin goce de sueldo</small></span>
          <Icono n="derecha" :t="20" class="flecha" />
        </RouterLink>
      </div>

      <!-- Por aprobar -->
      <section v-if="puedeAprobar" class="tarjeta compacta por-aprobar">
        <div class="tarjeta-cabecera" style="margin-bottom: 12px">
          <h2 class="titulo-tarjeta">Por aprobar</h2>
          <RouterLink to="/aprobaciones" class="ver-todo">Ver todas <Icono n="derecha" :t="16" /></RouterLink>
        </div>
        <RouterLink to="/aprobaciones" class="fila-solicitud" data-prueba="ir-aprobaciones">
          <span class="estado-icono" :class="(bandeja.datos.value?.total ?? 0) > 0 ? 'ambar' : 'verde'">
            <Icono :n="(bandeja.datos.value?.total ?? 0) > 0 ? 'bandeja' : 'checkCirculo'" :t="22" />
          </span>
          <span class="crecer">
            <template v-if="bandeja.cargando.value && !bandeja.datos.value"><span class="esqueleto" style="display: block; height: 18px; width: 60%" /></template>
            <template v-else-if="(bandeja.datos.value?.total ?? 0) > 0">
              <b>{{ bandeja.datos.value!.total }} {{ bandeja.datos.value!.total === 1 ? 'solicitud espera' : 'solicitudes esperan' }} tu decisión</b>
              <small>Revisa y aprueba o rechaza desde la bandeja</small>
            </template>
            <template v-else>
              <b>No tienes solicitudes por aprobar</b>
              <small>Te avisaremos cuando llegue una nueva</small>
            </template>
          </span>
          <Icono n="derecha" :t="20" class="flecha" />
        </RouterLink>
      </section>

      <!-- Mis solicitudes -->
      <section class="tarjeta" :class="{ compacta: conEncabezadoMovil }">
        <div class="tarjeta-cabecera">
          <h2 class="titulo-tarjeta">Mis solicitudes</h2>
          <span v-if="lista.datos.value" class="muted pequeno">{{ lista.datos.value.total }} en total</span>
        </div>
        <div class="filtros" role="tablist" aria-label="Filtrar por estado">
          <button v-for="f in FILTROS" :key="f.v" type="button" class="filtro" :class="{ activo: estado === f.v }" role="tab" :aria-selected="estado === f.v" @click="estado = f.v">{{ f.t }}</button>
        </div>

        <div v-if="lista.cargando.value && !lista.datos.value" class="columna">
          <div v-for="i in 3" :key="i" class="esqueleto" style="height: 68px" />
        </div>
        <div v-else-if="!lista.datos.value?.items.length" class="vacio">
          <Icono n="documento" :t="34" />
          <p style="margin-top: 8px">{{ estado ? 'No tienes solicitudes con este estado.' : 'Aún no has enviado solicitudes.' }}</p>
        </div>
        <div v-else class="lista">
          <RouterLink v-for="f in lista.datos.value.items" :key="f.id" :to="`/solicitudes/${f.id}`" class="fila-solicitud" :data-prueba="`solicitud-${f.id}`">
            <span class="estado-icono" :class="f.estado === 'Aprobada' ? 'verde' : f.estado === 'Rechazada' ? 'rojo' : f.estado === 'Cancelada' ? 'gris' : 'ambar'">
              <Icono :n="icono(f.estado)" :t="22" />
            </span>
            <span class="crecer textos">
              <b>{{ f.tipoTexto }}</b>
              <small>{{ rangoLargo(f.fechaInicio, f.fechaFin) }} · {{ dias(f.diasHabiles) }}</small>
              <small class="desc">{{ descripcion(f) }}</small>
            </span>
            <span class="lado">
              <Estado :v="f.estado" />
              <small class="muted">{{ momento(f.creadoEn) }}</small>
            </span>
            <Icono n="derecha" :t="20" class="flecha" />
          </RouterLink>
        </div>
        <Paginacion v-if="lista.datos.value && lista.datos.value.paginas > 1" :pagina="lista.datos.value.paginaActual" :paginas="lista.datos.value.paginas"
                    :total="lista.datos.value.total" :tamano="lista.datos.value.tamano" @ir="ir" />
      </section>
    </div>
  </div>
</template>

<style scoped>
.cuerpo { display: flex; flex-direction: column; gap: 20px; }
.mis-solicitudes.movil .cuerpo { padding: 16px; gap: 16px; }
.encabezado { margin-bottom: 0; }
.accesos { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }
.acceso {
  display: flex; align-items: center; gap: 14px; padding: 18px 20px; background: #fff; border: 1px solid var(--gris-200); border-radius: var(--radio);
  box-shadow: var(--sombra); color: var(--titulo); text-decoration: none !important; transition: border-color .15s, box-shadow .15s;
}
.acceso:hover { border-color: var(--verde-borde); box-shadow: var(--sombra-m); }
.acceso b { display: block; font-size: 16px; font-weight: 700; }
.acceso small { display: block; color: var(--texto-3); font-size: 13.5px; }
.acceso-icono { width: 52px; height: 52px; border-radius: 50%; display: grid; place-items: center; flex-shrink: 0; }
.acceso-icono.verde { background: var(--verde-claro); color: var(--verde-osc); }
.acceso-icono.azul { background: var(--azul-claro); color: var(--azul); }
.flecha { color: var(--texto-3); }

.filtros { display: flex; gap: 8px; overflow-x: auto; scrollbar-width: none; margin-bottom: 14px; padding-bottom: 2px; }
.filtros::-webkit-scrollbar { display: none; }
.filtro { height: 34px; padding: 0 14px; border-radius: 999px; border: 1px solid var(--gris-200); background: var(--gris-50); color: var(--texto-2); font-size: 14px; font-weight: 500; white-space: nowrap; }
.filtro:hover { border-color: var(--gris-300); }
.filtro.activo { background: var(--verde-claro); border-color: var(--verde-borde); color: var(--verde-texto); font-weight: 650; }

.lista { display: flex; flex-direction: column; gap: 10px; }
.fila-solicitud {
  display: flex; align-items: center; gap: 14px; padding: 14px 16px; border: 1px solid var(--gris-200); border-radius: var(--radio);
  background: #fff; color: var(--titulo); text-decoration: none !important; transition: background .15s, border-color .15s; min-width: 0;
}
.fila-solicitud:hover { background: #fafbfd; border-color: var(--gris-300); }
.fila-solicitud b { display: block; font-size: 15.5px; font-weight: 700; }
.fila-solicitud small { display: block; color: var(--texto-2); font-size: 14px; }
.textos { min-width: 0; }
.textos small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.textos .desc { color: var(--texto-3); font-size: 13px; }
.estado-icono { width: 42px; height: 42px; border-radius: 50%; display: grid; place-items: center; flex-shrink: 0; }
.estado-icono.verde { background: var(--verde-claro); color: var(--verde); }
.estado-icono.ambar { background: var(--ambar-claro); color: var(--ambar); }
.estado-icono.rojo { background: var(--rojo-claro); color: var(--rojo); }
.estado-icono.gris { background: var(--gris-100); color: var(--texto-3); }
.lado { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; flex-shrink: 0; }
.lado small { font-size: 12.5px; }

@media (max-width: 760px) {
  .accesos { gap: 12px; }
  .acceso { flex-direction: column; align-items: flex-start; gap: 10px; padding: 14px; position: relative; }
  .acceso .flecha { position: absolute; right: 12px; top: 50%; transform: translateY(-50%); }
  .acceso b { font-size: 15px; padding-right: 18px; }
  .acceso small { font-size: 12.5px; }
  .acceso-icono { width: 44px; height: 44px; }
  .lado { display: none; }
  .fila-solicitud { padding: 12px 14px; }
}
</style>
