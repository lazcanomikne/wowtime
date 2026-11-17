<script setup lang="ts">
// Buscador global: colaboradores, reportes, solicitudes y visitantes, agrupados por tipo.
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import { api } from '@/api'
import { debounce } from '@/utils/usos'
import { useSesion } from '@/stores/sesion'

const s = useSesion()
const router = useRouter()
const q = ref('')
const abierto = ref(false)
const r = ref<any>(null)
const raiz = ref<HTMLElement>()
const buscar = debounce(async () => {
  if (q.value.trim().length < 2) { r.value = null; return }
  try { r.value = await api.get('/buscar', { q: q.value.trim(), sucursalId: s.sucursalId ?? undefined }) } catch { r.value = null }
}, 250)
function ir(ruta: string) { abierto.value = false; q.value = ''; r.value = null; router.push(ruta) }
function enter() { if (q.value.trim().length >= 2) ir(`/buscar?q=${encodeURIComponent(q.value.trim())}`) }
const fuera = (e: MouseEvent) => { if (raiz.value && !raiz.value.contains(e.target as Node)) abierto.value = false }
onMounted(() => document.addEventListener('mousedown', fuera))
onBeforeUnmount(() => document.removeEventListener('mousedown', fuera))
const hay = () => r.value && (r.value.colaboradores?.length || r.value.reportes?.length || r.value.solicitudes?.length || r.value.visitantes?.length)
</script>

<template>
  <div ref="raiz" class="buscador">
    <div class="caja">
      <Icono n="buscar" :t="20" />
      <input v-model="q" placeholder="Buscar colaboradores, reportes, etc." aria-label="Buscar" data-prueba="buscador" @input="abierto = true; buscar()" @focus="abierto = true" @keydown.enter="enter" />
    </div>
    <div v-if="abierto && q.trim().length >= 2" class="res">
      <template v-if="hay()">
        <template v-if="r.colaboradores?.length">
          <p class="grupo">Colaboradores</p>
          <button v-for="c in r.colaboradores" :key="'c' + c.id" class="op" @click="ir(`/colaboradores/${c.id}`)">
            <Avatar :nombre="c.nombre" :iniciales="c.iniciales" :foto="c.fotoUrl" :t="30" /><span><b>{{ c.nombre }}</b><small>{{ c.puesto }} · {{ c.numero }}</small></span>
          </button>
        </template>
        <template v-if="r.solicitudes?.length">
          <p class="grupo">Solicitudes</p>
          <button v-for="x in r.solicitudes" :key="'s' + x.id" class="op" @click="ir(`/solicitudes/${x.id}`)"><Icono n="documento" :t="20" /><span><b>{{ x.folio }}</b><small>{{ x.tipo }} · {{ x.colaborador }}</small></span></button>
        </template>
        <template v-if="r.visitantes?.length">
          <p class="grupo">Visitantes</p>
          <button v-for="x in r.visitantes" :key="'v' + x.id" class="op" @click="ir(`/visitantes/${x.id}`)"><Icono n="visitantes" :t="20" /><span><b>{{ x.nombre }}</b><small>{{ x.empresaMotivo }}</small></span></button>
        </template>
        <template v-if="r.reportes?.length">
          <p class="grupo">Reportes</p>
          <button v-for="x in r.reportes" :key="'r' + x.clave" class="op" @click="ir(x.enlace)"><Icono n="reportes" :t="20" /><span><b>{{ x.nombre }}</b></span></button>
        </template>
      </template>
      <p v-else class="vacio">Sin resultados para "{{ q }}"</p>
    </div>
  </div>
</template>

<style scoped>
.buscador { position: relative; width: 100%; max-width: 420px; }
.caja { display: flex; align-items: center; gap: 10px; height: 44px; padding: 0 14px; border: 1px solid var(--gris-200); border-radius: 10px; background: var(--gris-50); color: var(--texto-3); }
.caja:focus-within { border-color: var(--verde); background: #fff; }
.caja input { border: 0; background: none; outline: none; flex: 1; font-size: 15px; color: var(--titulo); min-width: 0; }
.res { position: absolute; top: calc(100% + 6px); left: 0; right: 0; min-width: 340px; background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; box-shadow: var(--sombra-m); padding: 6px; z-index: 90; max-height: 460px; overflow-y: auto; }
.grupo { font-size: 12px; font-weight: 600; color: var(--texto-3); text-transform: uppercase; letter-spacing: .04em; padding: 8px 10px 4px; }
.op { display: flex; align-items: center; gap: 10px; width: 100%; padding: 8px 10px; border: 0; background: none; border-radius: 8px; text-align: left; color: var(--titulo); }
.op:hover { background: var(--gris-100); }
.op span { display: flex; flex-direction: column; font-size: 14px; }
.op small { color: var(--texto-3); font-size: 12.5px; }
</style>
