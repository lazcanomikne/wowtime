<script setup lang="ts">
// Pestaña "Expediente": avance, documentos requeridos por el puesto con vigencia, carga de archivos y documentos anteriores.
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Estado from '@/componentes/Estado.vue'
import Modal from '@/componentes/Modal.vue'
import { api, urlArchivo, ErrorApi } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion } from '@/utils/usos'
import { bytes, fechaCorta, iso, momento } from '@/utils/formato'

interface Doc {
  id: number; tipoDocumentoId: number; tipo: string; fechaVencimiento?: string | null; reemplazado: boolean; notas?: string | null; cargado: string; cargadoPor?: string | null
  archivo: string; contentType: string; tamano: number; url: string; descarga: string; estado: string; dias?: number | null
}
interface Requerido { tipoDocumentoId: number; nombre: string; descripcion?: string | null; conVigencia: boolean; obligatorio: boolean; documento?: Doc | null }
interface Expediente { requeridos: number; entregados: number; porcentaje: number; lista: Requerido[]; adicionales: Doc[] }
interface TipoDoc { id: number; nombre: string; descripcion?: string | null; conVigencia: boolean; activo: boolean }

const props = defineProps<{ ficha: any; id: number }>()
const emit = defineEmits<{ actualizar: [] }>()
const route = useRoute()
const s = useSesion()
const ui = useUi()
const { ocupado, ejecutar } = useAccion()

const datos = ref<Expediente | null>(null)
const cargando = ref(true)
const sinAcceso = ref('')
const resaltado = ref<number | null>(null)
const puedeSubir = computed(() => s.tiene('expedientes.gestionar') || (s.me?.colaborador?.id === props.id && s.tiene('expediente.propio')))

async function cargar() {
  try {
    datos.value = await api.get<Expediente>(`/colaboradores/${props.id}/documentos`, { incluirReemplazados: true })
    sinAcceso.value = ''
  } catch (e) {
    if (e instanceof ErrorApi && (e.status === 403 || e.status === 404)) sinAcceso.value = e.message
    else ui.error(e)
  } finally { cargando.value = false }
}
async function resaltar() {
  const d = Number(route.query.documento)
  if (!d || !datos.value) return
  resaltado.value = d
  await nextTick()
  document.querySelector(`[data-documento="${d}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
}
onMounted(async () => { await cargar(); resaltar() })
watch(() => route.query.documento, resaltar)

const alertas = computed(() => {
  const docs = (datos.value?.lista ?? []).map(r => r.documento).filter((d): d is Doc => !!d)
  return { vencidos: docs.filter(d => d.estado === 'Vencido').length, porVencer: docs.filter(d => d.estado === 'Urgente' || d.estado === 'Proximo').length }
})
const faltantes = computed(() => (datos.value?.lista ?? []).filter(r => !r.documento).length)
const anteriores = computed(() => (datos.value?.adicionales ?? []).filter(d => d.reemplazado))
const adicionales = computed(() => (datos.value?.adicionales ?? []).filter(d => !d.reemplazado))
const colorBarra = computed(() => { const p = datos.value?.porcentaje ?? 0; return p >= 100 ? 'verde' : p >= 60 ? '' : 'ambar' })

function vence(d: Doc) {
  if (!d.fechaVencimiento) return { texto: 'Sin vigencia', clase: 'muted' }
  const n = d.dias ?? 0
  const rel = n < 0 ? `venció hace ${-n} ${-n === 1 ? 'día' : 'días'}` : n === 0 ? 'vence hoy' : `vence en ${n} ${n === 1 ? 'día' : 'días'}`
  return { texto: `${fechaCorta(d.fechaVencimiento)} · ${rel}`, clase: d.estado === 'Vencido' || d.estado === 'Urgente' ? 'rojo-t' : d.estado === 'Proximo' ? 'ambar-t' : '' }
}
const icono = (d?: Doc | null) => (!d ? 'documentoAlerta' : d.estado === 'Vencido' || d.estado === 'Urgente' ? 'documentoAlerta' : 'documento')

// ───── Subir documento ─────
const modal = ref(false)
const tipos = ref<TipoDoc[]>([])
const sub = reactive({ tipoDocumentoId: null as number | null, nombre: '', conVigencia: false, fijo: false, fechaVencimiento: '', notas: '', archivo: null as File | null })
const errSub = ref<Record<string, string>>({})
const entradaArchivo = ref<HTMLInputElement>()
const tipoElegido = computed(() => tipos.value.find(t => t.id === sub.tipoDocumentoId))
const conVigencia = computed(() => (sub.fijo ? sub.conVigencia : !!tipoElegido.value?.conVigencia))
const MAX = 15 * 1024 * 1024

function abrirSubir(r?: Requerido) {
  errSub.value = {}
  Object.assign(sub, {
    tipoDocumentoId: r?.tipoDocumentoId ?? null, nombre: r?.nombre ?? '', conVigencia: r?.conVigencia ?? false, fijo: !!r,
    fechaVencimiento: '', notas: '', archivo: null
  })
  if (!r && !tipos.value.length) api.get('/tipos-documento', { tamano: 200, activos: true }).then(x => (tipos.value = x.items.filter((t: TipoDoc) => t.activo))).catch(e => ui.error(e))
  modal.value = true
}
function elegirArchivo(ev: Event) {
  const f = (ev.target as HTMLInputElement).files?.[0] ?? null
  sub.archivo = f
  delete errSub.value.archivo
  if (f && f.size > MAX) errSub.value.archivo = `El archivo pesa ${bytes(f.size)}; el máximo es 15 MB.`
}
function soltar(ev: DragEvent) {
  const f = ev.dataTransfer?.files?.[0]
  if (f) { sub.archivo = f; delete errSub.value.archivo; if (f.size > MAX) errSub.value.archivo = `El archivo pesa ${bytes(f.size)}; el máximo es 15 MB.` }
}
async function subir() {
  const er: Record<string, string> = {}
  if (!sub.tipoDocumentoId) er.tipoDocumentoId = 'Selecciona el tipo de documento.'
  if (!sub.archivo) er.archivo = 'Selecciona el archivo.'
  else if (sub.archivo.size > MAX) er.archivo = `El archivo pesa ${bytes(sub.archivo.size)}; el máximo es 15 MB.`
  if (conVigencia.value && !sub.fechaVencimiento) er.fechaVencimiento = 'Este documento requiere fecha de vencimiento.'
  errSub.value = er
  if (Object.keys(er).length) return
  const fd = new FormData()
  fd.append('tipoDocumentoId', String(sub.tipoDocumentoId))
  if (sub.fechaVencimiento) fd.append('fechaVencimiento', sub.fechaVencimiento)
  if (sub.notas.trim()) fd.append('notas', sub.notas.trim())
  fd.append('archivo', sub.archivo!)
  await ejecutar(async () => {
    const r = await api.subir<{ id: number; estado: string }>(`/colaboradores/${props.id}/documentos`, fd)
    modal.value = false
    await cargar()
    resaltado.value = r.id
    emit('actualizar')
    if (r.estado !== 'Vigente') ui.avisar('El documento se cargó, pero está próximo a vencer o vencido.', 'info')
  }, 'Documento cargado')
}
</script>

<template>
  <div class="expediente">
    <div v-if="cargando" class="tarjeta"><div class="esqueleto" style="height: 220px" /></div>
    <div v-else-if="sinAcceso" class="tarjeta"><p class="caja-info"><Icono n="candado" :t="20" />{{ sinAcceso }}</p></div>
    <template v-else-if="datos">
      <section class="tarjeta avance">
        <div class="fila" style="gap: 20px">
          <span class="exp-ico"><Icono n="documento" :t="34" :g="1.7" /></span>
          <div class="crecer">
            <div class="fila-entre envolver">
              <h2 class="titulo-tarjeta">Expediente digital</h2>
              <button v-if="puedeSubir" class="btn btn-contorno btn-ch" @click="abrirSubir()"><Icono n="subir" :t="16" />Subir otro documento</button>
            </div>
            <p class="docs"><b>{{ datos.entregados }} de {{ datos.requeridos }}</b> documentos</p>
            <div class="fila" style="margin-top: 10px"><div class="barra crecer" :class="colorBarra" style="height: 12px"><span :style="{ width: datos.porcentaje + '%' }" /></div><b class="pct">{{ datos.porcentaje }}%</b></div>
            <div class="fila envolver resumen-alertas">
              <span v-if="faltantes" class="etiqueta gris"><span class="punto" />{{ faltantes }} sin cargar</span>
              <span v-if="alertas.vencidos" class="etiqueta rojo"><span class="punto" />{{ alertas.vencidos }} {{ alertas.vencidos === 1 ? 'vencido' : 'vencidos' }}</span>
              <span v-if="alertas.porVencer" class="etiqueta ambar"><span class="punto" />{{ alertas.porVencer }} por vencer</span>
              <span v-if="!faltantes && !alertas.vencidos && !alertas.porVencer" class="etiqueta verde"><span class="punto" />Expediente completo y vigente</span>
            </div>
          </div>
        </div>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><div><h2 class="titulo-tarjeta">Documentos requeridos</h2><p class="subtitulo-tarjeta">Según el puesto {{ ficha.puesto ?? '' }}.</p></div></div>
        <ul v-if="datos.lista.length" class="docs-lista">
          <li v-for="r in datos.lista" :key="r.tipoDocumentoId" :data-documento="r.documento?.id" :class="{ resaltado: r.documento && r.documento.id === resaltado, falta: !r.documento }">
            <span class="doc-ico" :class="r.documento ? r.documento.estado.toLowerCase() : 'falta'"><Icono :n="icono(r.documento)" :t="22" /></span>
            <div class="doc-info">
              <b>{{ r.nombre }}<span v-if="!r.obligatorio" class="muted opcional"> · opcional</span></b>
              <span v-if="r.documento" class="muted pequeno">{{ r.documento.archivo }} · {{ bytes(r.documento.tamano) }} · cargado {{ momento(r.documento.cargado) }}{{ r.documento.cargadoPor ? ` por ${r.documento.cargadoPor}` : '' }}</span>
              <span v-else class="muted pequeno">{{ r.descripcion || 'Pendiente de cargar' }}</span>
              <span v-if="r.documento?.notas" class="pequeno nota"><Icono n="notas" :t="14" />{{ r.documento.notas }}</span>
            </div>
            <div class="doc-vence">
              <template v-if="r.documento">
                <Estado :v="r.documento.estado" />
                <span class="pequeno" :class="vence(r.documento).clase">{{ vence(r.documento).texto }}</span>
              </template>
              <span v-else-if="r.obligatorio" class="etiqueta rojo"><span class="punto" />Faltante</span>
              <span v-else class="etiqueta gris"><span class="punto" />Sin cargar</span>
            </div>
            <div class="doc-acciones">
              <template v-if="r.documento">
                <a class="btn btn-contorno btn-ch" :href="urlArchivo(r.documento.url)" target="_blank" rel="noopener"><Icono n="ojo" :t="16" />Ver</a>
                <a class="btn btn-contorno btn-ch" :href="urlArchivo(r.documento.descarga)" :download="r.documento.archivo"><Icono n="descargar" :t="16" />Descargar</a>
              </template>
              <button v-if="puedeSubir" class="btn btn-ch" :class="r.documento ? 'btn-verde-contorno' : 'btn-primario'" @click="abrirSubir(r)">
                <Icono n="subir" :t="16" />{{ r.documento ? 'Reemplazar' : 'Subir' }}
              </button>
            </div>
          </li>
        </ul>
        <p v-else class="vacio">El puesto no tiene documentos requeridos configurados.</p>
      </section>

      <section class="tarjeta">
        <div class="tarjeta-cabecera"><div><h2 class="titulo-tarjeta">Documentos anteriores o adicionales</h2><p class="subtitulo-tarjeta">Versiones reemplazadas y documentos que no pide el puesto.</p></div></div>
        <div v-if="adicionales.length || anteriores.length" class="tabla-envoltura">
          <table class="tabla">
            <thead><tr><th>Documento</th><th>Archivo</th><th>Cargado</th><th>Vencimiento</th><th>Estado</th><th /></tr></thead>
            <tbody>
              <tr v-for="d in [...adicionales, ...anteriores]" :key="d.id" :data-documento="d.id" :class="{ resaltado: d.id === resaltado }">
                <td><b>{{ d.tipo }}</b></td>
                <td class="archivo">{{ d.archivo }}</td>
                <td class="nowrap muted">{{ momento(d.cargado) }}</td>
                <td class="nowrap">{{ d.fechaVencimiento ? fechaCorta(d.fechaVencimiento) : 'Sin vigencia' }}</td>
                <td><Estado :v="d.reemplazado ? 'Reemplazado' : d.estado" /></td>
                <td class="nowrap">
                  <a class="btn-icono" :href="urlArchivo(d.url)" target="_blank" rel="noopener" aria-label="Ver documento" title="Ver"><Icono n="ojo" :t="18" /></a>
                  <a class="btn-icono" :href="urlArchivo(d.descarga)" :download="d.archivo" aria-label="Descargar documento" title="Descargar"><Icono n="descargar" :t="18" /></a>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-else class="vacio">Sin documentos anteriores ni adicionales.</p>
      </section>
    </template>

    <Modal v-if="modal" :titulo="sub.fijo ? `Subir ${sub.nombre}` : 'Subir documento'" :subtitulo="ficha.nombre" :ancho="520" @cerrar="modal = false">
      <div class="columna" style="gap: 14px">
        <div v-if="!sub.fijo" class="campo">
          <label for="sd-tipo">Tipo de documento</label>
          <select id="sd-tipo" v-model="sub.tipoDocumentoId" class="entrada" :class="{ error: errSub.tipoDocumentoId }">
            <option :value="null" disabled>Selecciona…</option>
            <option v-for="t in tipos" :key="t.id" :value="t.id">{{ t.nombre }}</option>
          </select>
          <span v-if="errSub.tipoDocumentoId" class="error-campo">{{ errSub.tipoDocumentoId }}</span>
          <span v-else-if="tipoElegido?.descripcion" class="ayuda">{{ tipoElegido.descripcion }}</span>
        </div>
        <div class="campo">
          <span class="etiqueta-campo">Archivo</span>
          <label class="zona" :class="{ error: errSub.archivo, lleno: sub.archivo }" @dragover.prevent @drop.prevent="soltar">
            <input ref="entradaArchivo" type="file" accept=".pdf,image/*,.doc,.docx" class="oculto-accesible" @change="elegirArchivo" />
            <Icono :n="sub.archivo ? 'documento' : 'subir'" :t="28" />
            <span v-if="sub.archivo"><b>{{ sub.archivo.name }}</b><small class="muted"> · {{ bytes(sub.archivo.size) }}</small></span>
            <span v-else><b>Elige un archivo</b> o arrástralo aquí<small class="muted bloque">PDF o imagen, máximo 15 MB</small></span>
          </label>
          <span v-if="errSub.archivo" class="error-campo">{{ errSub.archivo }}</span>
        </div>
        <div class="campo">
          <label for="sd-vence">Fecha de vencimiento <span v-if="conVigencia" class="req">*</span><span v-else class="muted">(opcional)</span></label>
          <input id="sd-vence" v-model="sub.fechaVencimiento" type="date" class="entrada" :class="{ error: errSub.fechaVencimiento }" :min="conVigencia ? iso(new Date()) : undefined" />
          <span v-if="errSub.fechaVencimiento" class="error-campo">{{ errSub.fechaVencimiento }}</span>
        </div>
        <div class="campo">
          <label for="sd-notas">Notas</label>
          <textarea id="sd-notas" v-model="sub.notas" class="entrada" rows="2" placeholder="Opcional" />
        </div>
        <p v-if="sub.fijo && datos?.lista.find(r => r.tipoDocumentoId === sub.tipoDocumentoId)?.documento" class="caja-info"><Icono n="info" :t="18" />El documento actual quedará en "Documentos anteriores".</p>
      </div>
      <template #pie>
        <button class="btn btn-contorno" @click="modal = false">Cancelar</button>
        <button class="btn btn-primario" :disabled="ocupado" @click="subir"><span v-if="ocupado" class="spinner chico" /><Icono v-else n="subir" :t="18" />Subir documento</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.expediente { display: flex; flex-direction: column; gap: 20px; }
.exp-ico { width: 72px; height: 72px; border-radius: 50%; background: #e0f6ee; color: #0c8e6c; display: grid; place-items: center; flex-shrink: 0; }
.docs { font-size: 16px; margin-top: 6px; }
.docs b { font-weight: 650; }
.pct { color: var(--texto-2); min-width: 44px; text-align: right; }
.resumen-alertas { gap: 8px; margin-top: 12px; }
.docs-lista { list-style: none; margin: 0; padding: 0; }
.docs-lista li { display: grid; grid-template-columns: auto minmax(0, 1fr) 220px auto; gap: 16px; align-items: center; padding: 14px 12px; border-bottom: 1px solid var(--gris-200); border-radius: 10px; transition: background .3s; }
.docs-lista li:last-child { border-bottom: 0; }
.docs-lista li.resaltado, .tabla tr.resaltado td { background: var(--verde-suave); }
.docs-lista li.resaltado { box-shadow: 0 0 0 2px var(--verde-borde) inset; }
.doc-ico { width: 44px; height: 44px; border-radius: 50%; display: grid; place-items: center; background: var(--verde-claro); color: var(--verde-texto); }
.doc-ico.proximo { background: var(--ambar-claro); color: var(--ambar); }
.doc-ico.urgente, .doc-ico.vencido { background: var(--rojo-claro); color: var(--rojo); }
.doc-ico.falta { background: var(--gris-100); color: var(--texto-3); }
.doc-info { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.doc-info b { color: var(--titulo); font-weight: 650; font-size: 15.5px; }
.doc-info > span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.opcional { font-weight: 400; font-size: 13px; }
.nota { display: flex; gap: 6px; align-items: center; color: var(--texto-2); }
.doc-vence { display: flex; flex-direction: column; align-items: flex-start; gap: 4px; }
.doc-acciones { display: flex; gap: 8px; justify-content: flex-end; flex-wrap: wrap; }
.rojo-t { color: var(--rojo-texto); font-weight: 600; }
.ambar-t { color: var(--ambar); font-weight: 600; }
.archivo { max-width: 260px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.nowrap { white-space: nowrap; }
.zona { display: flex; align-items: center; gap: 14px; padding: 18px; border: 1.5px dashed var(--gris-300); border-radius: 10px; cursor: pointer; color: var(--texto-2); background: var(--gris-50); position: relative; }
.zona:hover { border-color: var(--verde); }
.zona.lleno { border-style: solid; border-color: var(--verde-borde); background: var(--verde-suave); color: var(--verde-texto); }
.zona.error { border-color: var(--rojo); }
.zona b { color: var(--titulo); }
.oculto-accesible { position: absolute; width: 1px; height: 1px; opacity: 0; overflow: hidden; }
.zona:focus-within { outline: 2px solid var(--verde); outline-offset: 2px; }
.bloque { display: block; }
.req { color: var(--rojo); }
.spinner.chico { width: 16px; height: 16px; border-width: 2px; border-color: rgba(255, 255, 255, .4); border-top-color: #fff; }
@media (max-width: 1100px) { .docs-lista li { grid-template-columns: auto minmax(0, 1fr) auto; } .doc-acciones { grid-column: 2 / -1; justify-content: flex-start; } }
@media (max-width: 640px) {
  .docs-lista li { grid-template-columns: auto minmax(0, 1fr); padding: 14px 4px; }
  .doc-vence, .doc-acciones { grid-column: 2 / -1; }
  .doc-info > span { white-space: normal; }
}
</style>
