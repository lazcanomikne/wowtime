<script setup lang="ts">
// Menú de tres puntos por fila. Se dibuja fijo sobre la página para que la tabla con desplazamiento no lo recorte.
import { computed, onBeforeUnmount, ref } from 'vue'
import Icono from '@/componentes/Icono.vue'

export interface OpcionMenu { texto: string; icono?: string; peligro?: boolean; oculto?: boolean; accion: () => void }
const props = defineProps<{ opciones: OpcionMenu[]; etiqueta?: string }>()
const abierto = ref(false)
const boton = ref<HTMLButtonElement>()
const menu = ref<HTMLElement>()
const pos = ref({ top: 0, left: 0 })
const visibles = computed(() => props.opciones.filter(o => !o.oculto))

function abrir() {
  if (abierto.value) { cerrar(); return }
  const r = boton.value!.getBoundingClientRect()
  const ancho = 220
  const alto = visibles.value.length * 42 + 12
  const abajo = r.bottom + 4 + alto < window.innerHeight
  pos.value = { top: abajo ? r.bottom + 4 : Math.max(8, r.top - alto - 4), left: Math.max(8, Math.min(r.right - ancho, window.innerWidth - ancho - 8)) }
  abierto.value = true
  setTimeout(() => {
    document.addEventListener('mousedown', fuera)
    window.addEventListener('scroll', cerrar, true)
    window.addEventListener('resize', cerrar)
  })
}
function cerrar() {
  abierto.value = false
  document.removeEventListener('mousedown', fuera)
  window.removeEventListener('scroll', cerrar, true)
  window.removeEventListener('resize', cerrar)
}
function fuera(e: MouseEvent) {
  const t = e.target as Node
  if (menu.value?.contains(t) || boton.value?.contains(t)) return
  cerrar()
}
function elegir(o: OpcionMenu) { cerrar(); o.accion() }
onBeforeUnmount(cerrar)
</script>

<template>
  <button ref="boton" type="button" class="btn-icono borde puntos" :aria-label="etiqueta ?? 'Más acciones'" :aria-expanded="abierto" @click.stop="abrir">
    <Icono n="puntos" :t="18" />
  </button>
  <Teleport to="body">
    <div v-if="abierto" ref="menu" class="menu-acciones" role="menu" :style="{ top: pos.top + 'px', left: pos.left + 'px' }">
      <button v-for="o in visibles" :key="o.texto" type="button" role="menuitem" class="op" :class="{ peligro: o.peligro }" @click="elegir(o)">
        <Icono v-if="o.icono" :n="o.icono" :t="18" />{{ o.texto }}
      </button>
    </div>
  </Teleport>
</template>

<style scoped>
.puntos { width: 34px; height: 34px; }
.menu-acciones { position: fixed; z-index: 250; width: 220px; background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; box-shadow: var(--sombra-m); padding: 6px; }
.op { display: flex; align-items: center; gap: 10px; width: 100%; padding: 10px 12px; border-radius: 8px; border: 0; background: none; color: var(--titulo); font-size: 14.5px; text-align: left; }
.op:hover { background: var(--gris-100); }
.op.peligro { color: var(--rojo-texto); }
.op.peligro:hover { background: var(--rojo-claro); }
</style>
