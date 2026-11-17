<script setup lang="ts">
// Parámetros agrupados por grupo con edición en línea (PUT /parametros/:clave).
import { computed, reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '@/api'
import Icono from '@/componentes/Icono.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useCarga } from '@/utils/usos'
import { mensajeError } from './comun'

interface Parametro { clave: string; valor: string; descripcion: string; tipo: string; grupo: string }

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()
const valores = reactive<Record<string, string>>({})
const guardando = reactive<Record<string, boolean>>({})
const errores = reactive<Record<string, string>>({})
const { datos, cargando, cargar } = useCarga(async () => {
  const r = await api.get<Parametro[]>('/parametros')
  for (const p of r) { valores[p.clave] = p.valor; delete errores[p.clave] }
  return r
})

const grupoFiltro = computed(() => (route.query.grupo ? String(route.query.grupo) : ''))
const grupos = computed(() => {
  const m = new Map<string, Parametro[]>()
  for (const p of datos.value ?? []) {
    if (grupoFiltro.value && p.grupo !== grupoFiltro.value) continue
    if (!m.has(p.grupo)) m.set(p.grupo, [])
    m.get(p.grupo)!.push(p)
  }
  return [...m.entries()].map(([nombre, items]) => ({ nombre, items }))
})
const ICONOS: Record<string, string> = { Asistencia: 'asistencia', Colaboradores: 'colaboradores', Expedientes: 'expedientes', Sistema: 'configuracion', Vacaciones: 'vacaciones', Visitantes: 'visitantes' }

const editable = (p: Parametro) => (p.grupo === 'Sistema' ? s.tiene('sistema.configurar') : s.tiene('catalogos.gestionar'))
const esBool = (p: Parametro) => ['bool', 'booleano', 'logico'].includes(p.tipo)
const esNumero = (p: Parametro) => ['entero', 'decimal', 'numero'].includes(p.tipo)
/** Las entradas numéricas de Vue devuelven número; se normaliza a texto. */
const val = (p: Parametro) => String(valores[p.clave] ?? '').trim()
const cambiado = (p: Parametro) => val(p) !== p.valor

function validar(p: Parametro) {
  const v = val(p)
  if (p.tipo === 'entero' && !/^\d+$/.test(v)) return 'Escribe un número entero positivo.'
  if (!esBool(p) && !v && p.tipo !== 'texto') return 'Este dato es obligatorio.'
  return ''
}
async function guardar(p: Parametro) {
  const e = validar(p)
  if (e) { errores[p.clave] = e; return }
  delete errores[p.clave]
  guardando[p.clave] = true
  try {
    const r = await api.put<Parametro>(`/parametros/${encodeURIComponent(p.clave)}`, { valor: val(p) })
    p.valor = r.valor
    valores[p.clave] = r.valor
    ui.avisar('Parámetro guardado')
  } catch (err) {
    errores[p.clave] = mensajeError(err)
  } finally { guardando[p.clave] = false }
}
function deshacer(p: Parametro) { valores[p.clave] = p.valor; delete errores[p.clave] }
async function alternarBool(p: Parametro, v: boolean) { valores[p.clave] = v ? 'true' : 'false'; await guardar(p) }
</script>

<template>
  <div class="columna" style="gap: 20px">
    <div v-if="grupoFiltro" class="caja-info">
      <Icono n="filtro" :t="18" />
      <span class="crecer">Mostrando solo los parámetros de {{ grupoFiltro.toLowerCase() }}.</span>
      <button class="ver-todo" @click="router.replace({ query: {} })">Ver todos</button>
    </div>
    <div v-if="cargando && !datos" class="tarjeta"><div class="esqueleto" style="height: 120px" /></div>
    <section v-for="g in grupos" :key="g.nombre" class="tarjeta">
      <div class="tarjeta-cabecera">
        <div class="izq">
          <span class="ico"><Icono :n="ICONOS[g.nombre] ?? 'configuracion'" :t="22" /></span>
          <h2 class="titulo-tarjeta">{{ g.nombre }}</h2>
        </div>
        <span v-if="g.nombre === 'Sistema' && !s.tiene('sistema.configurar')" class="etiqueta gris"><Icono n="candado" :t="14" />Solo administradores</span>
      </div>
      <div class="lista">
        <div v-for="p in g.items" :key="p.clave" class="param">
          <div class="info">
            <label :for="`par-${p.clave}`" class="negrita">{{ p.descripcion }}</label>
            <code class="clave">{{ p.clave }}</code>
          </div>
          <div class="control">
            <template v-if="esBool(p)">
              <label class="casilla"><input :id="`par-${p.clave}`" type="checkbox" :checked="valores[p.clave] === 'true'" :disabled="!editable(p) || guardando[p.clave]" @change="alternarBool(p, ($event.target as HTMLInputElement).checked)" />{{ valores[p.clave] === 'true' ? 'Activado' : 'Desactivado' }}</label>
            </template>
            <template v-else>
              <input :id="`par-${p.clave}`" v-model="valores[p.clave]" class="entrada" :class="{ error: errores[p.clave], corto: esNumero(p) }"
                :type="esNumero(p) ? 'number' : 'text'" :min="esNumero(p) ? 0 : undefined" :disabled="!editable(p)" @keydown.enter="guardar(p)" @keydown.esc="deshacer(p)" />
              <div v-if="cambiado(p)" class="fila botones">
                <button class="btn btn-primario btn-ch" :disabled="guardando[p.clave]" :data-prueba="`guardar-${p.clave}`" @click="guardar(p)">Guardar</button>
                <button class="btn btn-fantasma btn-ch" @click="deshacer(p)">Deshacer</button>
              </div>
            </template>
            <span v-if="errores[p.clave]" class="error-campo">{{ errores[p.clave] }}</span>
          </div>
        </div>
      </div>
    </section>
    <p v-if="datos && !grupos.length" class="tarjeta vacio">No hay parámetros en este grupo.</p>
  </div>
</template>

<style scoped>
.ico { width: 42px; height: 42px; border-radius: 50%; display: grid; place-items: center; background: var(--verde-claro); color: var(--verde-texto); }
.lista { display: flex; flex-direction: column; }
.param { display: grid; grid-template-columns: minmax(0, 1fr) minmax(260px, 380px); gap: 16px; align-items: center; padding: 14px 0; border-top: 1px solid var(--gris-200); }
.param:first-child { border-top: 0; padding-top: 0; }
.info { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
.clave { font-size: 12.5px; color: var(--texto-3); }
.control { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.control .entrada { flex: 1; min-width: 140px; }
.control .entrada.corto { flex: 0 0 120px; min-width: 0; }
.control .error-campo { flex-basis: 100%; }
.botones { gap: 6px; }
@media (max-width: 760px) { .param { grid-template-columns: 1fr; gap: 8px; } }
</style>
