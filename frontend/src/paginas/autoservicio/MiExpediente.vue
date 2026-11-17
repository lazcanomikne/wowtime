<script setup lang="ts">
// Mi expediente (colaborador): avance, alertas de vencimiento y carga de documentos requeridos.
import { computed, onMounted, ref } from 'vue'
import Icono from '@/componentes/Icono.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'
import { api, urlArchivo } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion, useEsMovil } from '@/utils/usos'
import { bytes, fechaCorta, iso } from '@/utils/formato'

const s = useSesion()
const ui = useUi()
const esMovil = useEsMovil()
const colaboradorId = computed(() => s.me?.colaborador?.id ?? null)
const datos = ref<any>(null)
const cargando = ref(true)

async function cargar() {
  if (!colaboradorId.value) { cargando.value = false; return }
  cargando.value = true
  try { datos.value = await api.get(`/colaboradores/${colaboradorId.value}/documentos`) } catch (e) { ui.error(e) } finally { cargando.value = false }
}
onMounted(cargar)

const plazo = (dias: number) => (dias < 0 ? `venció hace ${-dias} ${dias === -1 ? 'día' : 'días'}` : dias === 0 ? 'vence hoy' : `vence en ${dias} ${dias === 1 ? 'día' : 'días'}`)
const alertas = computed(() => (datos.value?.lista ?? [])
  .filter((t: any) => t.documento && ['Vencido', 'Urgente', 'Proximo'].includes(t.documento.estado))
  .map((t: any) => ({ ...t, critica: t.documento.estado !== 'Proximo' }))
  .sort((a: any, b: any) => a.documento.dias - b.documento.dias))
const pendientes = computed(() => (datos.value?.lista ?? []).filter((t: any) => !t.documento).length)

function vigencia(t: any) {
  const d = t.documento
  if (!d) return t.descripcion ?? ''
  if (!d.fechaVencimiento) return `Cargado el ${fechaCorta(d.cargado)} · sin vencimiento`
  if (d.dias < 0) return `Venció el ${fechaCorta(d.fechaVencimiento)}`
  return `Vigente hasta el ${fechaCorta(d.fechaVencimiento)}`
}

// ───── Subir / actualizar ─────
const modal = ref<{ tipo: any; archivo: File | null; fechaVencimiento: string; notas: string; intento: boolean } | null>(null)
const { ocupado, ejecutar } = useAccion()
const hoyIso = iso(new Date())
function abrir(t: any) { modal.value = { tipo: t, archivo: null, fechaVencimiento: '', notas: '', intento: false } }
function elegirArchivo(e: Event) {
  const f = (e.target as HTMLInputElement).files?.[0] ?? null
  if (modal.value) modal.value.archivo = f
}
async function guardar() {
  const m = modal.value
  if (!m || !colaboradorId.value) return
  m.intento = true
  if (!m.archivo || (m.tipo.conVigencia && !m.fechaVencimiento)) return
  const fd = new FormData()
  fd.append('tipoDocumentoId', String(m.tipo.tipoDocumentoId))
  if (m.fechaVencimiento) fd.append('fechaVencimiento', m.fechaVencimiento)
  if (m.notas.trim()) fd.append('notas', m.notas.trim())
  fd.append('archivo', m.archivo)
  const r = await ejecutar(() => api.subir(`/colaboradores/${colaboradorId.value}/documentos`, fd), m.tipo.documento ? 'Documento actualizado.' : 'Documento cargado.')
  if (r === undefined) return
  modal.value = null
  cargar()
}
</script>

<template>
  <div class="mi-expediente" :class="{ movil: esMovil }">
    <EncabezadoMovil v-if="esMovil" titulo="Mi expediente" />
    <div class="contenido-pag">
      <div v-if="!esMovil" class="encabezado">
        <div>
          <h1 class="titulo-pagina">Mi expediente</h1>
          <p class="subtitulo-pagina">Mantén tus documentos completos y vigentes</p>
        </div>
      </div>

      <p v-if="!colaboradorId" class="caja-info"><Icono n="info" :t="20" />Tu usuario no está vinculado a un colaborador, por lo que no tiene expediente.</p>
      <div v-else-if="cargando && !datos" class="tarjeta vacio"><span class="spinner" /></div>

      <template v-else-if="datos">
        <!-- Avance -->
        <section class="tarjeta avance">
          <span class="avance-ic"><Icono n="documento" :t="30" :g="1.9" /></span>
          <div class="crecer">
            <p class="avance-txt"><b>{{ datos.entregados }} de {{ datos.requeridos }}</b> documentos</p>
            <div class="avance-barra">
              <div class="barra verde"><span :style="{ width: datos.porcentaje + '%' }" /></div>
              <span class="pct">{{ datos.porcentaje }}%</span>
            </div>
            <p class="muted pequeno">{{ pendientes ? `Te ${pendientes === 1 ? 'falta' : 'faltan'} ${pendientes} ${pendientes === 1 ? 'documento' : 'documentos'} por entregar.` : '¡Tu expediente está completo!' }}</p>
          </div>
        </section>

        <!-- Alertas -->
        <div v-if="alertas.length" class="alertas">
          <button v-for="a in alertas" :key="a.tipoDocumentoId" class="alerta" :class="{ critica: a.critica }" @click="abrir(a)">
            <span class="alerta-ic"><Icono n="alertaCirculo" :t="22" :g="2.2" /></span>
            <span class="crecer">
              <b>{{ a.nombre }} {{ plazo(a.documento.dias) }}</b>
              <small>Renueva el documento para evitar inconvenientes.</small>
            </span>
            <Icono n="derecha" :t="20" />
          </button>
        </div>

        <!-- Documentos -->
        <section class="tarjeta">
          <div class="tarjeta-cabecera">
            <div>
              <h2 class="titulo-tarjeta">Documentos requeridos</h2>
              <p class="subtitulo-tarjeta">Según tu puesto{{ datos.colaborador?.puesto ? `: ${datos.colaborador.puesto}` : '' }}</p>
            </div>
          </div>
          <ul class="docs">
            <li v-for="t in datos.lista" :key="t.tipoDocumentoId" class="doc" :class="{ falta: !t.documento }">
              <span class="doc-ic" :class="t.documento ? (['Vencido', 'Urgente'].includes(t.documento.estado) ? 'rojo' : 'verde') : 'gris'">
                <Icono :n="t.documento ? (['Vencido', 'Urgente'].includes(t.documento.estado) ? 'documentoAlerta' : 'documento') : 'subir'" :t="22" />
              </span>
              <div class="doc-info">
                <p class="doc-nombre">{{ t.nombre }}<span v-if="t.obligatorio" class="oblig" title="Obligatorio">*</span></p>
                <p class="doc-sub">{{ vigencia(t) }}</p>
                <p v-if="t.documento" class="doc-arch"><Icono n="clip" :t="14" />{{ t.documento.archivo }} · {{ bytes(t.documento.tamano) }}</p>
              </div>
              <div class="doc-estado">
                <Estado v-if="t.documento" :v="t.documento.estado" punto />
                <span v-else class="etiqueta gris"><span class="punto" />Pendiente</span>
              </div>
              <div class="doc-acciones">
                <template v-if="t.documento">
                  <a :href="urlArchivo(t.documento.url)" target="_blank" rel="noopener" class="btn btn-contorno btn-ch" title="Ver"><Icono n="ojo" :t="17" /><span class="txt-btn">Ver</span></a>
                  <a :href="urlArchivo(t.documento.descarga)" class="btn btn-contorno btn-ch" title="Descargar"><Icono n="descargar" :t="17" /><span class="txt-btn">Descargar</span></a>
                  <button class="btn btn-verde-contorno btn-ch" @click="abrir(t)"><Icono n="actualizar" :t="17" />Actualizar</button>
                </template>
                <button v-else class="btn btn-primario btn-ch" @click="abrir(t)"><Icono n="subir" :t="17" />Subir</button>
              </div>
            </li>
            <li v-if="!datos.lista.length" class="vacio">Tu puesto no tiene documentos requeridos.</li>
          </ul>
        </section>

        <section v-if="datos.adicionales?.length" class="tarjeta">
          <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Otros documentos</h2></div>
          <ul class="docs">
            <li v-for="d in datos.adicionales" :key="d.id" class="doc">
              <span class="doc-ic gris"><Icono n="documento" :t="22" /></span>
              <div class="doc-info">
                <p class="doc-nombre">{{ d.tipo }}</p>
                <p class="doc-sub">{{ d.fechaVencimiento ? `Vence el ${fechaCorta(d.fechaVencimiento)}` : `Cargado el ${fechaCorta(d.cargado)}` }}</p>
              </div>
              <div class="doc-estado"><Estado :v="d.estado" punto /></div>
              <div class="doc-acciones">
                <a :href="urlArchivo(d.url)" target="_blank" rel="noopener" class="btn btn-contorno btn-ch"><Icono n="ojo" :t="17" /><span class="txt-btn">Ver</span></a>
                <a :href="urlArchivo(d.descarga)" class="btn btn-contorno btn-ch"><Icono n="descargar" :t="17" /><span class="txt-btn">Descargar</span></a>
              </div>
            </li>
          </ul>
        </section>
      </template>
    </div>

    <Modal v-if="modal" :titulo="`${modal.tipo.documento ? 'Actualizar' : 'Subir'} ${modal.tipo.nombre}`" :subtitulo="modal.tipo.descripcion ?? undefined" :ancho="520" @cerrar="modal = null">
      <form class="columna formulario" @submit.prevent="guardar">
        <p v-if="modal.tipo.documento" class="caja-info"><Icono n="info" :t="18" />El documento actual quedará en tu historial como reemplazado.</p>
        <div class="campo">
          <label for="archivo-doc">Archivo</label>
          <label class="soltar" :class="{ error: modal.intento && !modal.archivo, lleno: modal.archivo }">
            <input id="archivo-doc" type="file" accept=".pdf,image/*" @change="elegirArchivo" />
            <Icono :n="modal.archivo ? 'checkCirculo' : 'subir'" :t="26" />
            <span v-if="modal.archivo"><b>{{ modal.archivo.name }}</b><small>{{ bytes(modal.archivo.size) }} · toca para cambiarlo</small></span>
            <span v-else><b>Elige un archivo</b><small>PDF o imagen (puedes tomar una foto)</small></span>
          </label>
          <span v-if="modal.intento && !modal.archivo" class="error-campo">Elige el archivo del documento.</span>
        </div>
        <div v-if="modal.tipo.conVigencia" class="campo">
          <label for="venc-doc">Fecha de vencimiento</label>
          <input id="venc-doc" v-model="modal.fechaVencimiento" type="date" class="entrada" :class="{ error: modal.intento && !modal.fechaVencimiento }" :min="hoyIso" />
          <span v-if="modal.intento && !modal.fechaVencimiento" class="error-campo">Este documento requiere fecha de vencimiento.</span>
        </div>
        <div class="campo">
          <label for="notas-doc">Notas (opcional)</label>
          <textarea id="notas-doc" v-model="modal.notas" class="entrada" rows="2" placeholder="Ej. Renovado en noviembre" />
        </div>
      </form>
      <template #pie>
        <button class="btn btn-contorno" @click="modal = null">Cancelar</button>
        <button class="btn btn-primario" :disabled="ocupado" @click="guardar"><Icono n="subir" :t="18" />{{ ocupado ? 'Subiendo…' : modal.tipo.documento ? 'Actualizar' : 'Subir documento' }}</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.contenido-pag { display: flex; flex-direction: column; gap: 20px; max-width: 1100px; }
.movil .contenido-pag { padding: 16px; gap: 16px; }
.encabezado { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
.avance { display: flex; align-items: center; gap: 20px; }
.avance-ic { width: 64px; height: 64px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-texto); display: grid; place-items: center; flex-shrink: 0; }
.avance-txt { font-size: 18px; color: var(--texto-2); }
.avance-txt b { color: var(--titulo); font-weight: 750; }
.avance-barra { display: flex; align-items: center; gap: 14px; margin: 8px 0 6px; }
.avance-barra .barra { flex: 1; height: 12px; max-width: 420px; }
.pct { font-weight: 650; color: var(--titulo); font-variant-numeric: tabular-nums; }
.alertas { display: flex; flex-direction: column; gap: 10px; }
.alerta { display: flex; align-items: center; gap: 14px; text-align: left; padding: 14px 18px; border-radius: var(--radio); border: 1px solid #f3dcae; background: var(--ambar-claro); color: #8a5a0b; font: inherit; width: 100%; }
.alerta b { display: block; font-weight: 650; }
.alerta small { display: block; font-size: 13.5px; color: var(--texto-2); margin-top: 2px; }
.alerta.critica { background: var(--rojo-claro); border-color: var(--rojo-borde); color: var(--rojo-texto); }
.alerta-ic { width: 34px; height: 34px; border-radius: 50%; background: currentColor; display: grid; place-items: center; flex-shrink: 0; }
.alerta-ic .icono { color: #fff; }
.alerta.critica .alerta-ic { background: var(--rojo); }
.alerta:not(.critica) .alerta-ic { background: #e9a23b; }
.docs { list-style: none; margin: 0; padding: 0; }
.doc { display: grid; grid-template-columns: auto minmax(0, 1fr) auto auto; gap: 16px; align-items: center; padding: 14px 0; border-bottom: 1px solid var(--gris-200); }
.doc:last-child { border-bottom: 0; }
.doc-ic { width: 44px; height: 44px; border-radius: 10px; display: grid; place-items: center; }
.doc-ic.verde { background: var(--verde-claro); color: var(--verde-texto); }
.doc-ic.rojo { background: var(--rojo-claro); color: var(--rojo); }
.doc-ic.gris { background: var(--gris-100); color: var(--texto-3); }
.doc-nombre { font-weight: 650; color: var(--titulo); font-size: 15px; }
.oblig { color: var(--rojo); margin-left: 3px; }
.doc-sub { font-size: 13.5px; color: var(--texto-3); }
.doc-arch { display: flex; align-items: center; gap: 4px; font-size: 12.5px; color: var(--texto-3); margin-top: 2px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.doc-acciones { display: flex; gap: 8px; justify-content: flex-end; flex-wrap: wrap; }
.formulario { gap: 16px; }
.soltar { position: relative; display: flex; align-items: center; gap: 14px; padding: 16px; border: 1.5px dashed var(--gris-300); border-radius: 10px; cursor: pointer; color: var(--texto-3); background: var(--gris-50); }
.soltar:hover { border-color: var(--verde); }
.soltar.lleno { border-style: solid; border-color: var(--verde-borde); background: var(--verde-suave); color: var(--verde-texto); }
.soltar.error { border-color: var(--rojo); }
.soltar input { position: absolute; inset: 0; opacity: 0; cursor: pointer; }
.soltar b { display: block; color: var(--titulo); font-weight: 600; word-break: break-all; }
.soltar small { display: block; font-size: 13px; color: var(--texto-3); }

@media (max-width: 760px) {
  .doc { grid-template-columns: auto minmax(0, 1fr) auto; gap: 10px 12px; }
  .doc-acciones { grid-column: 1 / -1; justify-content: flex-start; }
  .doc-acciones .btn { flex: 1; }
  .avance { gap: 14px; }
  .avance-ic { width: 52px; height: 52px; }
}
@media (max-width: 380px) { .txt-btn { display: none; } }
</style>
