<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api } from '@/api'
import Icono from '@/componentes/Icono.vue'
import Modal from '@/componentes/Modal.vue'
import { useCarga } from '@/utils/usos'
import { dias } from '@/utils/formato'
import { useEnvio } from './comun'

interface Fila { aniosDesde: number; aniosHasta: number; dias: number }
interface Politica { id: number; nombre: string; plazoVencimientoMeses: number; anticipacionMinimaDias: number; diasAlertaVencimiento: number; porDefecto: boolean; tabla: Fila[] }

const { ocupado, error, enviar } = useEnvio()
const { datos, cargando, cargar } = useCarga(() => api.get<Politica[]>('/politicas-vacaciones'))
const anios = (f: Fila) => (f.aniosDesde === f.aniosHasta ? `${f.aniosDesde} ${f.aniosDesde === 1 ? 'año' : 'años'}` : f.aniosHasta >= 99 ? `${f.aniosDesde} años o más` : `${f.aniosDesde} a ${f.aniosHasta} años`)

const abierto = ref(false)
const editId = ref<number | null>(null)
const f = reactive({ nombre: '', plazoVencimientoMeses: 12, anticipacionMinimaDias: 0, diasAlertaVencimiento: 30, porDefecto: false, tabla: [] as Fila[] })
const intento = ref(false)
const entero = (v: unknown, min: number, max: number) => Number.isInteger(Number(v)) && Number(v) >= min && Number(v) <= max && v !== ''
const errores = computed(() => {
  const e: Record<string, string> = {}
  if (!f.nombre.trim()) e.nombre = 'Escribe el nombre de la política.'
  if (!entero(f.plazoVencimientoMeses, 1, 60)) e.plazo = 'Entre 1 y 60 meses.'
  if (!entero(f.anticipacionMinimaDias, 0, 365)) e.anticipacion = 'Entre 0 y 365 días.'
  if (!entero(f.diasAlertaVencimiento, 0, 365)) e.alerta = 'Entre 0 y 365 días.'
  if (!f.tabla.length) e.tabla = 'Agrega al menos una fila.'
  else {
    const orden = [...f.tabla].sort((a, b) => Number(a.aniosDesde) - Number(b.aniosDesde))
    for (const [i, r] of orden.entries()) {
      if (!entero(r.aniosDesde, 1, 99) || !entero(r.aniosHasta, 1, 99) || !entero(r.dias, 1, 365)) { e.tabla = 'Revisa los valores: años de 1 a 99 y días de 1 a 365.'; break }
      if (Number(r.aniosHasta) < Number(r.aniosDesde)) { e.tabla = `En la fila de ${r.aniosDesde} años, "hasta" no puede ser menor que "desde".`; break }
      const prev = orden[i - 1]
      if (prev && Number(r.aniosDesde) <= Number(prev.aniosHasta)) { e.tabla = `Los rangos ${anios(prev)} y ${anios(r)} se traslapan.`; break }
    }
  }
  return e
})

function abrir(x?: Politica) {
  intento.value = false; error.value = null
  editId.value = x?.id ?? null
  Object.assign(f, x
    ? { nombre: x.nombre, plazoVencimientoMeses: x.plazoVencimientoMeses, anticipacionMinimaDias: x.anticipacionMinimaDias, diasAlertaVencimiento: x.diasAlertaVencimiento, porDefecto: x.porDefecto, tabla: x.tabla.map(t => ({ aniosDesde: t.aniosDesde, aniosHasta: t.aniosHasta, dias: t.dias })) }
    : { nombre: '', plazoVencimientoMeses: 12, anticipacionMinimaDias: 0, diasAlertaVencimiento: 30, porDefecto: !datos.value?.length, tabla: (datos.value?.find(p => p.porDefecto) ?? datos.value?.[0])?.tabla.map(t => ({ aniosDesde: t.aniosDesde, aniosHasta: t.aniosHasta, dias: t.dias })) ?? [{ aniosDesde: 1, aniosHasta: 1, dias: 12 }] })
  abierto.value = true
}
function agregarFila() {
  const ult = f.tabla[f.tabla.length - 1]
  const desde = ult ? Math.min(99, Number(ult.aniosHasta) + 1) : 1
  f.tabla.push({ aniosDesde: desde, aniosHasta: desde, dias: ult ? Number(ult.dias) + 2 : 12 })
}
function quitarFila(i: number) { f.tabla.splice(i, 1) }
async function guardar() {
  intento.value = true
  if (Object.keys(errores.value).length) return
  const c = {
    nombre: f.nombre.trim(), plazoVencimientoMeses: Number(f.plazoVencimientoMeses), anticipacionMinimaDias: Number(f.anticipacionMinimaDias), diasAlertaVencimiento: Number(f.diasAlertaVencimiento),
    porDefecto: f.porDefecto, tabla: f.tabla.map(t => ({ aniosDesde: Number(t.aniosDesde), aniosHasta: Number(t.aniosHasta), dias: Number(t.dias) }))
  }
  const r = await enviar(() => editId.value ? api.put(`/politicas-vacaciones/${editId.value}`, c) : api.post('/politicas-vacaciones', c), editId.value ? 'Política actualizada' : 'Política creada')
  if (r.ok) { abierto.value = false; cargar() }
}
</script>

<template>
  <div class="columna" style="gap: 20px">
    <div class="fila-entre envolver">
      <p class="caja-info crecer"><Icono n="info" :t="20" />La tabla no puede dar menos días que la Ley Federal del Trabajo (art. 76): 12 días el primer año, 14 el segundo, y así sucesivamente. El servidor lo valida al guardar.</p>
      <button class="btn btn-primario" data-prueba="nueva-politica" @click="abrir()"><Icono n="mas" :t="20" />Nueva política</button>
    </div>
    <div v-if="cargando && !datos" class="tarjeta"><div class="esqueleto" style="height: 160px" /></div>
    <div class="rejilla">
      <section v-for="p in datos" :key="p.id" class="tarjeta">
        <div class="tarjeta-cabecera">
          <div>
            <h2 class="titulo-tarjeta">{{ p.nombre }}</h2>
            <span v-if="p.porDefecto" class="etiqueta verde" style="margin-top: 6px"><span class="punto" />Por defecto</span>
          </div>
          <button class="btn btn-contorno btn-ch" :data-prueba="`editar-politica-${p.id}`" @click="abrir(p)"><Icono n="lapiz" :t="16" />Editar</button>
        </div>
        <div class="datos">
          <div><span class="muted">Vencimiento</span><b>{{ p.plazoVencimientoMeses }} meses</b></div>
          <div><span class="muted">Anticipación mínima</span><b>{{ dias(p.anticipacionMinimaDias) }}</b></div>
          <div><span class="muted">Alerta de vencimiento</span><b>{{ dias(p.diasAlertaVencimiento) }} antes</b></div>
        </div>
        <div class="tabla-envoltura">
          <table class="tabla">
            <thead><tr><th>Antigüedad</th><th class="num">Días de vacaciones</th></tr></thead>
            <tbody>
              <tr v-for="t in p.tabla" :key="t.aniosDesde"><td>{{ anios(t) }}</td><td class="num negrita">{{ t.dias }}</td></tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
    <p v-if="datos && !datos.length" class="tarjeta vacio">No hay políticas de vacaciones. Crea la primera.</p>
  </div>

  <Modal v-if="abierto" :titulo="editId ? 'Editar política' : 'Nueva política'" :ancho="680" @cerrar="abierto = false">
    <form class="rejilla-form" novalidate @submit.prevent="guardar">
      <div class="campo completo">
        <label for="po-nombre">Nombre</label>
        <input id="po-nombre" v-model="f.nombre" class="entrada" :class="{ error: intento && errores.nombre }" placeholder="Política general" />
        <span v-if="intento && errores.nombre" class="error-campo">{{ errores.nombre }}</span>
      </div>
      <div class="campo">
        <label for="po-plazo">Vencimiento (meses)</label>
        <input id="po-plazo" v-model.number="f.plazoVencimientoMeses" type="number" min="1" max="60" class="entrada" :class="{ error: intento && errores.plazo }" />
        <span v-if="intento && errores.plazo" class="error-campo">{{ errores.plazo }}</span>
        <span v-else class="ayuda">Desde que se otorgan los días.</span>
      </div>
      <div class="campo">
        <label for="po-ant">Anticipación mínima (días)</label>
        <input id="po-ant" v-model.number="f.anticipacionMinimaDias" type="number" min="0" max="365" class="entrada" :class="{ error: intento && errores.anticipacion }" />
        <span v-if="intento && errores.anticipacion" class="error-campo">{{ errores.anticipacion }}</span>
        <span v-else class="ayuda">Días antes del inicio para solicitar.</span>
      </div>
      <div class="campo">
        <label for="po-alerta">Alerta de vencimiento (días)</label>
        <input id="po-alerta" v-model.number="f.diasAlertaVencimiento" type="number" min="0" max="365" class="entrada" :class="{ error: intento && errores.alerta }" />
        <span v-if="intento && errores.alerta" class="error-campo">{{ errores.alerta }}</span>
        <span v-else class="ayuda">Avisa al colaborador antes de que venzan.</span>
      </div>
      <label class="casilla"><input v-model="f.porDefecto" type="checkbox" />Política por defecto</label>

      <div class="campo completo">
        <div class="fila-entre">
          <span class="etiqueta-campo">Días por antigüedad</span>
          <button type="button" class="ver-todo" data-prueba="agregar-fila" @click="agregarFila"><Icono n="mas" :t="16" />Agregar fila</button>
        </div>
        <div class="tabla-envoltura">
          <table class="tabla editable">
            <thead><tr><th>Desde (años)</th><th>Hasta (años)</th><th>Días</th><th aria-label="Quitar" /></tr></thead>
            <tbody>
              <tr v-for="(t, i) in f.tabla" :key="i">
                <td><input v-model.number="t.aniosDesde" type="number" min="1" max="99" class="entrada" :aria-label="`Desde, fila ${i + 1}`" /></td>
                <td><input v-model.number="t.aniosHasta" type="number" min="1" max="99" class="entrada" :aria-label="`Hasta, fila ${i + 1}`" /></td>
                <td><input v-model.number="t.dias" type="number" min="1" max="365" class="entrada" :aria-label="`Días, fila ${i + 1}`" /></td>
                <td><button type="button" class="btn-icono" :aria-label="`Quitar fila ${i + 1}`" :disabled="f.tabla.length === 1" @click="quitarFila(i)"><Icono n="basura" :t="18" /></button></td>
              </tr>
            </tbody>
          </table>
        </div>
        <span v-if="intento && errores.tabla" class="error-campo">{{ errores.tabla }}</span>
        <span v-else class="ayuda">Usa 99 en "Hasta" para la última fila (en adelante).</span>
      </div>
      <p v-if="error" class="caja-alerta err completo" role="alert"><Icono n="alertaCirculo" :t="20" />{{ error }}</p>
      <button type="submit" class="oculto" />
    </form>
    <template #pie>
      <button class="btn btn-contorno" @click="abierto = false">Cancelar</button>
      <button class="btn btn-primario" :disabled="ocupado" data-prueba="guardar" @click="guardar">Guardar</button>
    </template>
  </Modal>
</template>

<style scoped>
.rejilla { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: 20px; }
.datos { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; margin-bottom: 16px; }
.datos > div { display: flex; flex-direction: column; gap: 2px; padding: 10px 12px; background: var(--gris-50); border-radius: var(--radio-s); font-size: 13px; }
.datos b { font-size: 16px; color: var(--titulo); }
.editable td { padding: 6px 8px; }
.editable .entrada { height: 38px; min-width: 80px; }
.caja-info { margin: 0; }
.err { font-size: 14px; color: var(--rojo-texto); padding: 10px 14px; }
@media (max-width: 760px) { .rejilla { grid-template-columns: 1fr; } .datos { grid-template-columns: 1fr; } }
</style>
