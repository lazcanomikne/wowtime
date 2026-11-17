<script setup lang="ts">
// Nodo recursivo del organigrama.
import { computed } from 'vue'
import Avatar from '@/componentes/Avatar.vue'
import Icono from '@/componentes/Icono.vue'

const props = defineProps<{ n: any; nivel: number; abiertos: Set<number>; editar: boolean; arrastrando: number | null }>()
const emit = defineEmits<{ alternar: [id: number]; abrir: [id: number]; soltar: [id: number, nuevoJefe: number]; arrastrar: [id: number | null]; agregar: [id: number] }>()
const abierto = computed(() => props.abiertos.has(props.n.id))
const compacto = computed(() => props.nivel >= 3)
function soltar(e: DragEvent) {
  e.preventDefault()
  const id = Number(e.dataTransfer?.getData('text/plain'))
  if (id && id !== props.n.id) emit('soltar', id, props.n.id)
}
</script>

<template>
  <li>
    <div class="nodo" :class="{ raiz: nivel === 0, compacto, editable: editar, destino: editar && arrastrando && arrastrando !== n.id }"
         :draggable="editar && nivel > 0" :data-nodo="n.id"
         @dragstart="e => { e.dataTransfer?.setData('text/plain', String(n.id)); emit('arrastrar', n.id) }" @dragend="emit('arrastrar', null)"
         @dragover.prevent @drop="soltar" @click="!editar && emit('abrir', n.id)">
      <Avatar :nombre="n.nombre" :iniciales="n.iniciales" :foto="n.fotoUrl" :t="compacto ? 54 : 70" />
      <div class="txt">
        <b>{{ n.nombre }}</b>
        <span class="puesto">{{ n.puesto }}</span>
        <span v-if="n.supervisorExterno" class="ext">Reporta a {{ n.supervisorExterno }}</span>
        <button class="rep" :class="{ con: n.reportesDirectos > 0 }" :disabled="!n.hijos.length" @click.stop="emit('alternar', n.id)">
          <Icono n="personas" :t="compacto ? 15 : 17" />
          <template v-if="compacto">{{ n.reportesDirectos }} reportes</template>
          <template v-else>{{ n.reportesDirectos }} {{ n.reportesDirectos === 1 ? 'reporte directo' : 'reportes directos' }}</template>
          <Icono v-if="n.hijos.length" :n="abierto ? 'arriba' : 'abajo'" :t="14" />
        </button>
      </div>
      <button v-if="editar" class="mas" title="Agregar colaborador a su cargo" @click.stop="emit('agregar', n.id)"><Icono n="mas" :t="16" /></button>
    </div>
    <ul v-if="abierto && n.hijos.length">
      <OrgNodo v-for="h in n.hijos" :key="h.id" :n="h" :nivel="nivel + 1" :abiertos="abiertos" :editar="editar" :arrastrando="arrastrando"
               @alternar="id => emit('alternar', id)" @abrir="id => emit('abrir', id)" @soltar="(a, b) => emit('soltar', a, b)" @arrastrar="id => emit('arrastrar', id)" @agregar="id => emit('agregar', id)" />
    </ul>
  </li>
</template>

<style scoped>
li { list-style: none; position: relative; padding: 26px 6px 0; display: flex; flex-direction: column; align-items: center; }
/* Conectores del organigrama */
li::before, li::after { content: ''; position: absolute; top: 0; right: 50%; width: 50%; height: 26px; border-top: 2px solid #b9c2cf; }
li::after { right: auto; left: 50%; border-left: 2px solid #b9c2cf; }
li:only-child::before, li:only-child::after { display: none; }
li:only-child { padding-top: 26px; }
li:only-child > .nodo::before { content: ''; position: absolute; top: -26px; left: 50%; height: 26px; border-left: 2px solid #b9c2cf; }
li:first-child::before, li:last-child::after { border: 0 none; }
li:last-child::before { border-right: 2px solid #b9c2cf; border-radius: 0 8px 0 0; }
li:first-child::after { border-radius: 8px 0 0 0; }
ul { padding: 26px 0 0; margin: 0; display: flex; justify-content: center; position: relative; }
ul::before { content: ''; position: absolute; top: 0; left: 50%; height: 26px; border-left: 2px solid #b9c2cf; }
.nodo { position: relative; display: flex; align-items: center; gap: 14px; background: #fff; border: 1px solid var(--gris-200); border-radius: 10px; padding: 12px 18px 12px 12px; min-width: 250px; cursor: pointer; box-shadow: var(--sombra); transition: border-color .15s, box-shadow .15s; }
.nodo:hover { border-color: var(--gris-300); box-shadow: var(--sombra-m); }
.nodo.raiz { background: #eefcf5; border-color: #9fe0bd; padding: 14px 22px 14px 14px; }
.nodo.compacto { flex-direction: row; min-width: 0; width: 190px; padding: 10px; gap: 10px; }
.nodo.editable { cursor: grab; border-style: dashed; }
.nodo.destino { border-color: var(--verde); background: var(--verde-suave); }
.txt { display: flex; flex-direction: column; align-items: flex-start; min-width: 0; }
.txt b { font-size: 17px; color: var(--titulo); font-weight: 650; white-space: nowrap; }
.compacto .txt b { font-size: 14px; white-space: normal; line-height: 1.2; }
.puesto { color: var(--texto-2); font-size: 15px; white-space: nowrap; }
.compacto .puesto { font-size: 12px; white-space: normal; }
.ext { font-size: 11.5px; color: var(--azul); }
.rep { display: inline-flex; align-items: center; gap: 6px; margin-top: 8px; border: 0; border-radius: 999px; padding: 4px 10px; font-size: 13px; background: var(--gris-100); color: var(--texto-2); }
.compacto .rep { font-size: 11.5px; padding: 3px 8px; margin-top: 6px; }
.rep.con { background: var(--verde-claro); color: var(--verde-texto); }
.rep:disabled { cursor: default; }
.mas { position: absolute; right: -10px; bottom: -10px; width: 26px; height: 26px; border-radius: 50%; border: 0; background: var(--verde); color: #fff; display: grid; place-items: center; box-shadow: var(--sombra); }
</style>
