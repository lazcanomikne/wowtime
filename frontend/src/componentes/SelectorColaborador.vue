<script setup lang="ts">
// Selector de colaborador con búsqueda (cobertura, supervisor, delegaciones, alta por RH).
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { api, type Persona } from '@/api'
import { debounce } from '@/utils/usos'
import { useSesion } from '@/stores/sesion'
import Avatar from './Avatar.vue'
import Icono from './Icono.vue'

const props = withDefaults(defineProps<{ modelValue?: number | null; inicial?: Persona | null; placeholder?: string; excluir?: number[]; sucursalId?: number | null; ruta?: string }>(),
  { placeholder: 'Buscar colaborador…' })
const emit = defineEmits<{ 'update:modelValue': [v: number | null]; elegir: [p: Persona | null] }>()
const s = useSesion()
// RH busca en el listado general; los demás, entre compañeros de su sucursal
const rutaBusqueda = () => props.ruta ?? (s.tiene('colaboradores.ver') ? '/colaboradores' : '/solicitudes/companeros')
const q = ref('')
const abierto = ref(false)
const opciones = ref<Persona[]>([])
const elegido = ref<Persona | null>(props.inicial ?? null)
const raiz = ref<HTMLElement>()
watch(() => props.inicial, v => { if (v) elegido.value = v })

const buscar = debounce(async () => {
  try {
    const r = await api.get(rutaBusqueda(), { q: q.value, tamano: 8, sucursalId: props.sucursalId ?? undefined, estatus: 'Activo' })
    opciones.value = (r.items as any[]).filter(p => !props.excluir?.includes(p.id)).map(p => ({ id: p.id, numero: p.numero, nombre: p.nombre, puesto: p.puesto, area: p.area, iniciales: p.iniciales, fotoUrl: p.fotoUrl }))
  } catch { opciones.value = [] }
}, 250)
function abrir() { abierto.value = true; buscar() }
function elegir(p: Persona | null) { elegido.value = p; emit('update:modelValue', p?.id ?? null); emit('elegir', p); abierto.value = false; q.value = '' }
const fuera = (e: MouseEvent) => { if (raiz.value && !raiz.value.contains(e.target as Node)) abierto.value = false }
onMounted(() => document.addEventListener('mousedown', fuera))
onBeforeUnmount(() => document.removeEventListener('mousedown', fuera))
</script>

<template>
  <div ref="raiz" class="sel">
    <button v-if="elegido && !abierto" type="button" class="entrada elegido" @click="abrir">
      <Avatar :nombre="elegido.nombre" :iniciales="elegido.iniciales" :foto="elegido.fotoUrl" :t="26" />
      <span class="crecer txt">{{ elegido.nombre }} <span class="muted">· {{ elegido.area ?? elegido.puesto }}</span></span>
      <span class="quitar" role="button" aria-label="Quitar" @click.stop="elegir(null)"><Icono n="x" :t="16" /></span>
    </button>
    <div v-else class="con-icono">
      <Icono n="buscar" :t="18" />
      <input v-model="q" class="entrada" :placeholder="placeholder" @focus="abrir" @input="abierto = true; buscar()" />
    </div>
    <div v-if="abierto" class="lista">
      <button v-for="p in opciones" :key="p.id" type="button" class="op" @click="elegir(p)">
        <Avatar :nombre="p.nombre" :iniciales="p.iniciales" :foto="p.fotoUrl" :t="30" />
        <span class="crecer"><b>{{ p.nombre }}</b><br><small class="muted">{{ p.puesto }} · {{ p.numero }}</small></span>
      </button>
      <div v-if="!opciones.length" class="vacio" style="padding: 14px">Sin resultados</div>
    </div>
  </div>
</template>

<style scoped>
.sel { position: relative; }
.elegido { display: flex; align-items: center; gap: 10px; text-align: left; cursor: pointer; padding: 0 10px; }
.txt { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.quitar { color: var(--texto-3); display: grid; place-items: center; width: 26px; height: 26px; border-radius: 6px; }
.quitar:hover { background: var(--gris-100); }
.lista { position: absolute; z-index: 50; top: calc(100% + 4px); left: 0; right: 0; background: #fff; border: 1px solid var(--gris-200); border-radius: 10px; box-shadow: var(--sombra-m); max-height: 300px; overflow-y: auto; padding: 4px; }
.op { display: flex; gap: 10px; align-items: center; width: 100%; padding: 8px 10px; border: 0; background: none; border-radius: 8px; text-align: left; font-size: 14px; }
.op:hover { background: var(--gris-100); }
</style>
