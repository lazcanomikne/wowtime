<script setup lang="ts">
// Inicio del portal del colaborador (escritorio).
import { computed, onMounted, ref } from 'vue'
import Icono from '@/componentes/Icono.vue'
import Estado from '@/componentes/Estado.vue'
import Persona from '@/componentes/Persona.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useRelojServidor } from '@/utils/reloj'
import { MESES, aFecha, fechaLarga, hora, num, rangoLargo, saludo } from '@/utils/formato'

const s = useSesion()
const { ahora, sincronizar } = useRelojServidor()
const jornada = ref<any>(null)
const mias = ref<any[]>([])
const bandeja = ref<any>(null)
const invitados = ref<any[]>([])
const docs = ref<any>(null)

onMounted(async () => {
  const tareas: Promise<unknown>[] = [
    api.get('/asistencia/jornada-hoy').then(r => { jornada.value = r; sincronizar(r.horaServidor) }),
    api.get('/solicitudes', { mias: true, tamano: 4 }).then(r => (mias.value = r.items)),
    api.get('/visitantes/invitaciones', { mias: true, tamano: 3, orden: 'llegada', dir: 'desc' }).then(r => (invitados.value = r.items)),
  ]
  if (s.me?.colaborador) tareas.push(api.get(`/colaboradores/${s.me.colaborador.id}/documentos`).then(r => (docs.value = r)))
  if (s.tiene('aprobaciones.decidir')) tareas.push(api.get('/aprobaciones/bandeja').then(r => (bandeja.value = r)))
  await Promise.allSettled(tareas)
})
const j = computed(() => jornada.value?.jornada)
const venceMes = computed(() => s.me?.saldo?.fechaProximoVencimiento ? MESES[aFecha(s.me.saldo.fechaProximoVencimiento)!.getMonth()] : null)
const alertasDocs = computed(() => (docs.value?.lista ?? []).filter((l: any) => l.documento && ['Urgente', 'Vencido', 'Proximo'].includes(l.documento.estado)))
</script>

<template>
  <div class="ini">
    <header class="fila-entre">
      <div>
        <h1 class="titulo-pagina">{{ saludo(ahora) }}, {{ s.me?.colaborador?.nombres ?? s.me?.nombre }}</h1>
        <p class="subtitulo-pagina">{{ fechaLarga(ahora) }} · {{ s.frase('movil_inicio_lema') }}</p>
      </div>
    </header>

    <div v-if="s.me?.colaborador?.sinSupervisor" class="caja-alerta"><Icono n="alerta" :t="22" style="color: var(--rojo-texto)" /><span>No tienes un supervisor asignado. No podrás enviar solicitudes hasta que Recursos Humanos te asigne uno.</span></div>

    <div class="rejilla">
      <section class="tarjeta hoy">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Mi jornada de hoy</h2><RouterLink to="/mi-asistencia" class="ver-todo">Ver detalle <Icono n="flecha" :t="16" /></RouterLink></div>
        <div class="fila" style="gap: 22px">
          <div class="reloj"><b>{{ hora(ahora) }}</b><span>Hora del servidor</span></div>
          <div class="crecer info">
            <p v-if="j"><Icono :n="j.modalidad === 'HomeOffice' ? 'hogar' : 'edificio'" :t="20" />{{ j.modalidadTexto }} · {{ j.lugar }}<template v-if="j.horaEntrada"> · {{ hora(j.horaEntrada) }} – {{ hora(j.horaSalida) }}</template></p>
            <p v-if="jornada"><Estado :v="jornada.salida ? 'SalidaRegistrada' : jornada.estadoEntrada ?? 'SinRegistro'" :texto="jornada.estado" /></p>
            <p v-if="jornada?.entrada" class="muted">Entrada {{ hora(jornada.entrada) }}<template v-if="jornada.salida"> · Salida {{ hora(jornada.salida) }}</template></p>
          </div>
        </div>
        <RouterLink v-if="jornada?.siguiente" to="/m/asistencia" class="btn btn-primario btn-g btn-bloque" style="margin-top: 18px">
          <Icono n="reloj" :t="22" />Registrar {{ jornada.siguiente === 'Entrada' ? 'entrada' : 'salida' }}
        </RouterLink>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Mis vacaciones</h2><RouterLink to="/mis-vacaciones" class="ver-todo">Solicitar <Icono n="flecha" :t="16" /></RouterLink></div>
        <div class="saldo">
          <span class="circ"><Icono n="calendario" :t="28" /></span>
          <div><b>{{ num(s.me?.saldo?.disponibles, 1) }}</b><span>días disponibles</span></div>
          <div v-if="venceMes" class="vence"><b>{{ num(s.me?.saldo?.diasProximoVencimiento, 1) }}</b><span>días vencen en {{ venceMes }}</span></div>
        </div>
        <div class="fila" style="margin-top: 16px; gap: 10px">
          <RouterLink to="/mis-vacaciones" class="btn btn-primario crecer"><Icono n="palmera" :t="18" />Solicitar vacaciones</RouterLink>
          <RouterLink to="/solicitar-permiso" class="btn btn-contorno crecer"><Icono n="calendario" :t="18" />Solicitar permiso</RouterLink>
        </div>
      </section>

      <section v-if="bandeja" class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Por aprobar</h2><RouterLink to="/aprobaciones" class="ver-todo">Ver todas <Icono n="flecha" :t="16" /></RouterLink></div>
        <RouterLink v-for="x in bandeja.items.slice(0, 3)" :key="x.id" :to="`/aprobaciones?solicitud=${x.id}`" class="fila-sol">
          <Persona :p="x.colaborador" :sub="`${x.tipoTexto} · ${rangoLargo(x.fechaInicio, x.fechaFin)}`" :t="36" />
          <Icono n="derecha" :t="20" />
        </RouterLink>
        <p v-if="!bandeja.items.length" class="vacio">No tienes solicitudes por aprobar.</p>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Mis solicitudes</h2><RouterLink to="/mis-solicitudes" class="ver-todo">Ver todas <Icono n="flecha" :t="16" /></RouterLink></div>
        <RouterLink v-for="x in mias" :key="x.id" :to="`/solicitudes/${x.id}`" class="fila-sol">
          <span><b>{{ x.tipoTexto }}</b><small>{{ rangoLargo(x.fechaInicio, x.fechaFin) }} · {{ x.diasHabiles }} días</small></span>
          <Estado :v="x.estado" />
        </RouterLink>
        <p v-if="!mias.length" class="vacio">Aún no tienes solicitudes.</p>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Mi expediente</h2><RouterLink to="/mi-expediente" class="ver-todo">Ver detalle <Icono n="flecha" :t="16" /></RouterLink></div>
        <template v-if="docs">
          <p><b>{{ docs.entregados }} de {{ docs.requeridos }}</b> documentos</p>
          <div class="fila" style="margin-top: 10px"><div class="barra crecer"><span :style="{ width: docs.porcentaje + '%' }" /></div><span class="muted">{{ docs.porcentaje }}%</span></div>
          <RouterLink v-for="a in alertasDocs" :key="a.tipoDocumentoId" to="/mi-expediente" class="alerta-doc">
            <Icono n="documentoAlerta" :t="20" />{{ a.nombre }} {{ a.documento.dias < 0 ? `venció hace ${-a.documento.dias} días` : `vence en ${a.documento.dias} días` }}
          </RouterLink>
        </template>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Mis invitados</h2><RouterLink to="/m/invitado" class="ver-todo">Registrar invitado <Icono n="flecha" :t="16" /></RouterLink></div>
        <RouterLink v-for="v in invitados" :key="v.id" :to="`/visitantes/${v.id}`" class="fila-sol">
          <span><b>{{ v.nombre }}</b><small>{{ v.empresaMotivo }} · {{ rangoLargo(v.fechaLocal, v.fechaLocal) }} {{ hora(v.llegadaLocal) }}</small></span>
          <Estado :v="v.estado" />
        </RouterLink>
        <p v-if="!invitados.length" class="vacio">No has registrado invitados.</p>
      </section>
    </div>
  </div>
</template>

<style scoped>
.ini { display: flex; flex-direction: column; gap: 20px; }
.rejilla { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: 20px; align-items: start; }
.reloj b { display: block; font-size: 44px; font-weight: 800; color: var(--titulo); line-height: 1; }
.reloj span { font-size: 13px; color: var(--texto-3); }
.info { display: flex; flex-direction: column; gap: 8px; font-size: 15px; }
.info p { display: flex; align-items: center; gap: 8px; }
.saldo { display: flex; align-items: center; gap: 18px; background: var(--verde-suave); border-radius: 12px; padding: 16px; }
.saldo .circ { width: 56px; height: 56px; border-radius: 50%; background: #d8f3e4; color: var(--verde-texto); display: grid; place-items: center; }
.saldo b { display: block; font-size: 34px; font-weight: 800; color: var(--verde-texto); line-height: 1; }
.saldo span { color: var(--verde-texto); }
.vence { margin-left: auto; border-left: 1px solid #cde7d7; padding-left: 18px; }
.vence b { color: #c98a14; font-size: 26px; }
.vence span { color: var(--titulo); font-size: 14px; }
.fila-sol { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 0; border-bottom: 1px solid var(--gris-100); color: var(--titulo); text-decoration: none !important; }
.fila-sol:last-of-type { border-bottom: 0; }
.fila-sol b { display: block; font-size: 15px; }
.fila-sol small { color: var(--texto-3); font-size: 13px; }
.alerta-doc { display: flex; gap: 8px; align-items: center; margin-top: 10px; padding: 10px 12px; border-radius: 8px; background: var(--rojo-claro); color: var(--rojo-texto); font-size: 14px; font-weight: 600; text-decoration: none !important; }
</style>
