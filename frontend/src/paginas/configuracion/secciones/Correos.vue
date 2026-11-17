<script setup lang="ts">
import { ref } from 'vue'
import { api, type Pagina } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import Paginacion from '@/componentes/Paginacion.vue'
import { useCarga } from '@/utils/usos'
import { momento } from '@/utils/formato'

interface Correo { id: number; para: string; asunto: string; cuerpo: string; creadoEn: string; enviadoEn: string | null; error: string | null }

const pagina = ref(1)
const { datos, cargando, cargar } = useCarga(() => api.get<Pagina<Correo>>('/correos', { pagina: pagina.value, tamano: 25 }))
function ir(p: number) { pagina.value = p; cargar() }
const abierto = ref<Correo | null>(null)
function estado(c: Correo): [string, string] {
  if (c.enviadoEn) return ['verde', 'Enviado']
  if (c.error) return ['rojo', 'Con error']
  return ['ambar', 'Pendiente']
}
/** Documento del iframe: estilos base para que el correo se lea bien sin heredar los de la app. */
const documento = (c: Correo) => `<!doctype html><html><head><meta charset="utf-8"><base target="_blank"><style>body{font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;font-size:15px;color:#1b2436;margin:16px;line-height:1.5}a{color:#2f5bc1}img{max-width:100%}</style></head><body>${c.cuerpo}</body></html>`
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Bandeja de salida</h2>
        <p class="subtitulo-tarjeta">{{ datos?.total ?? 0 }} correos generados por el sistema</p>
      </div>
      <button class="btn btn-contorno" :disabled="cargando" @click="cargar()"><Icono n="actualizar" :t="18" />Actualizar</button>
    </div>
    <div class="tabla-envoltura">
      <table class="tabla">
        <thead><tr><th>Para</th><th>Asunto</th><th>Fecha</th><th>Estado</th><th>Error</th></tr></thead>
        <tbody>
          <tr v-if="cargando && !datos"><td colspan="5"><div class="esqueleto" style="height: 40px" /></td></tr>
          <tr v-for="c in datos?.items" :key="c.id" class="clic" @click="abierto = c">
            <td class="nowrap">{{ c.para }}</td>
            <td class="negrita asunto">{{ c.asunto }}</td>
            <td class="nowrap">{{ momento(c.enviadoEn ?? c.creadoEn) }}</td>
            <td><span class="etiqueta" :class="estado(c)[0]"><span class="punto" />{{ estado(c)[1] }}</span></td>
            <td class="error-txt">{{ c.error ?? '—' }}</td>
          </tr>
          <tr v-if="datos && !datos.items.length"><td colspan="5" class="vacio">Todavía no se ha enviado ningún correo.</td></tr>
        </tbody>
      </table>
    </div>
    <Paginacion v-if="datos" :pagina="datos.paginaActual" :paginas="datos.paginas" :total="datos.total" :tamano="datos.tamano" @ir="ir" />
  </section>

  <Modal v-if="abierto" :titulo="abierto.asunto" :ancho="760" @cerrar="abierto = null">
    <div class="columna">
      <div class="meta">
        <div><span class="muted">Para</span><b>{{ abierto.para }}</b></div>
        <div><span class="muted">Creado</span><b>{{ momento(abierto.creadoEn) }}</b></div>
        <div><span class="muted">Enviado</span><b>{{ abierto.enviadoEn ? momento(abierto.enviadoEn) : 'Pendiente' }}</b></div>
      </div>
      <p v-if="abierto.error" class="caja-alerta err"><Icono n="alertaCirculo" :t="20" />{{ abierto.error }}</p>
      <iframe class="vista" sandbox="" :srcdoc="documento(abierto)" title="Contenido del correo" />
    </div>
    <template #pie>
      <button class="btn btn-primario" @click="abierto = null">Cerrar</button>
    </template>
  </Modal>
</template>

<style scoped>
.nowrap { white-space: nowrap; }
.asunto { min-width: 220px; }
.error-txt { color: var(--rojo-texto); font-size: 13px; max-width: 280px; }
.meta { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; }
.meta > div { display: flex; flex-direction: column; gap: 2px; padding: 10px 12px; background: var(--gris-50); border-radius: var(--radio-s); font-size: 13px; min-width: 0; }
.meta b { font-size: 14.5px; color: var(--titulo); overflow-wrap: anywhere; }
.vista { width: 100%; height: 420px; border: 1px solid var(--gris-200); border-radius: var(--radio-s); background: #fff; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
@media (max-width: 600px) { .meta { grid-template-columns: 1fr; } }
</style>
