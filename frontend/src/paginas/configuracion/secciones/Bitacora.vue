<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import { useCarga } from '@/utils/usos'
import { momento } from '@/utils/formato'

interface Evento { id: number; colaboradorId: number | null; tipo: string; titulo: string; detalle: string | null; fecha: string; autorUsuarioId: number | null; autorNombre: string | null; entidad: string | null; entidadId: number | null }

/** Tipos de evento que registra el servidor (Bitacora.Registrar), con su etiqueta y color. */
const TIPOS: Record<string, { texto: string; color: string; icono: string }> = {
  datos: { texto: 'Datos', color: 'azul', icono: 'lapiz' },
  seguridad: { texto: 'Seguridad', color: 'rojo', icono: 'candado' },
  documento: { texto: 'Documento', color: 'gris', icono: 'documento' },
  solicitud: { texto: 'Solicitud', color: 'ambar', icono: 'bandeja' },
  aprobacion: { texto: 'Aprobación', color: 'verde', icono: 'checkCirculo' },
  vacaciones: { texto: 'Vacaciones', color: 'verde', icono: 'vacaciones' },
  asistencia: { texto: 'Asistencia', color: 'azul', icono: 'reloj' },
  ausencia: { texto: 'Ausencia', color: 'naranja', icono: 'calendario' },
  asignacion: { texto: 'Asignación', color: 'azul', icono: 'asignacion' },
  delegacion: { texto: 'Delegación', color: 'gris', icono: 'llave' },
  visitante: { texto: 'Visitante', color: 'gris', icono: 'visitantes' },
  configuracion: { texto: 'Configuración', color: 'gris', icono: 'configuracion' },
}
const info = (t: string) => TIPOS[t] ?? { texto: t, color: 'gris', icono: 'info' }

const tipo = ref('')
const desde = ref('')
const hasta = ref('')
const pagina = ref(1)
const invalido = computed(() => !!desde.value && !!hasta.value && hasta.value < desde.value)
const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Evento>>('/bitacora', { tipo: tipo.value, desde: desde.value, hasta: hasta.value, pagina: pagina.value, tamano: 25 }))
watch([tipo, desde, hasta], () => { if (invalido.value) return; pagina.value = 1; cargar() })
function ir(p: number) { pagina.value = p; cargar() }
function limpiar() { tipo.value = ''; desde.value = ''; hasta.value = '' }
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Eventos</h2>
        <p class="subtitulo-tarjeta">{{ datos?.total ?? 0 }} eventos</p>
      </div>
    </div>
    <div class="barra-filtros">
      <select v-model="tipo" class="entrada" aria-label="Tipo de evento">
        <option value="">Todos los tipos</option>
        <option v-for="(t, k) in TIPOS" :key="k" :value="k">{{ t.texto }}</option>
      </select>
      <label class="fila rango"><span class="muted pequeno">Desde</span><input v-model="desde" type="date" class="entrada" aria-label="Desde" /></label>
      <label class="fila rango"><span class="muted pequeno">Hasta</span><input v-model="hasta" type="date" class="entrada" aria-label="Hasta" :class="{ error: invalido }" /></label>
      <button v-if="tipo || desde || hasta" class="ver-todo" @click="limpiar">Limpiar filtros</button>
    </div>
    <p v-if="invalido" class="error-campo" style="margin: -8px 0 12px">La fecha final no puede ser anterior a la inicial.</p>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Fecha</th><th>Tipo</th><th>Evento</th><th>Autor</th><th>Colaborador</th></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="5"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="e in datos?.items" :key="e.id">
            <td class="nowrap">{{ momento(e.fecha) }}</td>
            <td><span class="etiqueta cuadrada" :class="info(e.tipo).color"><Icono :n="info(e.tipo).icono" :t="14" />{{ info(e.tipo).texto }}</span></td>
            <td><div class="negrita">{{ e.titulo }}</div><div v-if="e.detalle" class="muted pequeno">{{ e.detalle }}</div></td>
            <td>{{ e.autorNombre ?? 'Sistema' }}</td>
            <td><RouterLink v-if="e.colaboradorId" :to="`/colaboradores/${e.colaboradorId}`" class="ver-todo">Ver ficha<Icono n="derecha" :t="14" /></RouterLink><span v-else class="muted">—</span></td>
          </tr>
          <tr v-if="datos && !datos.items.length"><td colspan="5" class="vacio">No hay eventos con estos filtros.</td></tr>
        </tbody>
      </table>
    </div>
    <Paginacion v-if="datos" :pagina="datos.paginaActual" :paginas="datos.paginas" :total="datos.total" :tamano="datos.tamano" @ir="ir" />
  </section>
</template>

<style scoped>
.nowrap { white-space: nowrap; }
.rango { gap: 8px; }
.rango .entrada { min-width: 150px; }
@media (max-width: 760px) { .rango { width: 100%; } .rango .entrada { flex: 1; } }
</style>
