<script setup lang="ts">
// Centro de notificaciones: contador de no leídas y lista desplegable.
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import { api } from '@/api'
import { hace } from '@/utils/formato'
import { useEventos } from '@/utils/usos'

const props = withDefaults(defineProps<{ claro?: boolean }>(), { claro: false })
const router = useRouter()
const abierto = ref(false)
const items = ref<any[]>([])
const noLeidas = ref(0)
const raiz = ref<HTMLElement>()
let t: ReturnType<typeof setInterval>

async function cargar() {
  try { const r = await api.get('/notificaciones', { tamano: 8 }); items.value = r.items; noLeidas.value = r.noLeidas } catch { /* sin conexión */ }
}
async function abrir(n: any) {
  abierto.value = false
  if (!n.leida) { try { await api.post(`/notificaciones/${n.id}/leida`) } catch { /* nada */ } }
  await cargar()
  if (n.enlace) router.push(n.enlace)
}
async function leerTodas() { await api.post('/notificaciones/leer-todas'); await cargar() }
const icono = (tipo: string) => ({ solicitud_por_aprobar: 'bandeja', solicitud_aprobada: 'checkCirculo', solicitud_rechazada: 'x', documento_por_vencer: 'documentoAlerta', dias_por_vencer: 'reloArena', visitante_llego: 'visitantes', cumpleanos: 'pastel', incidencia: 'alerta', cobertura: 'colaboradores', delegacion: 'llave', sin_supervisor: 'organigrama' } as Record<string, string>)[tipo] ?? 'campana'
const fuera = (e: MouseEvent) => { if (raiz.value && !raiz.value.contains(e.target as Node)) abierto.value = false }
onMounted(() => { cargar(); t = setInterval(cargar, 60000); document.addEventListener('mousedown', fuera) })
onBeforeUnmount(() => { clearInterval(t); document.removeEventListener('mousedown', fuera) })
useEventos(tipo => { if (tipo === 'notificacion') cargar() })
defineExpose({ cargar })
</script>

<template>
  <div ref="raiz" class="campana">
    <button class="btn-campana" :class="{ claro: props.claro }" :aria-label="`Notificaciones, ${noLeidas} sin leer`" data-prueba="campana" @click="abierto = !abierto; if (abierto) cargar()">
      <Icono n="campana" :t="26" :g="1.8" />
      <span v-if="noLeidas > 0 && noLeidas < 10" class="contador">{{ noLeidas }}</span>
      <span v-else-if="noLeidas >= 10" class="contador">9+</span>
    </button>
    <div v-if="abierto" class="panel">
      <div class="cab">
        <b>Notificaciones</b>
        <button v-if="noLeidas" class="ver-todo" @click="leerTodas">Marcar todas como leídas</button>
      </div>
      <div class="lista">
        <button v-for="n in items" :key="n.id" class="noti" :class="{ nueva: !n.leida }" @click="abrir(n)">
          <span class="ico"><Icono :n="icono(n.tipo)" :t="18" /></span>
          <span class="crecer">
            <span class="t">{{ n.titulo }}</span>
            <span v-if="n.mensaje" class="m">{{ n.mensaje }}</span>
            <span class="h">{{ hace(n.creadaEn) }}</span>
          </span>
        </button>
        <p v-if="!items.length" class="vacio">No tienes notificaciones.</p>
      </div>
      <RouterLink to="/notificaciones" class="todas" @click="abierto = false">Ver todas</RouterLink>
    </div>
  </div>
</template>

<style scoped>
.campana { position: relative; }
.btn-campana { position: relative; width: 46px; height: 46px; border-radius: 50%; border: 0; background: none; color: var(--titulo); display: grid; place-items: center; }
.btn-campana:hover { background: var(--gris-50); }
.btn-campana.claro { color: #fff; }
.btn-campana.claro:hover { background: rgba(255,255,255,.1); }
.contador { position: absolute; top: 3px; right: 3px; min-width: 19px; height: 19px; padding: 0 5px; border-radius: 10px; background: #ef3b3b; color: #fff; font-size: 11.5px; font-weight: 700; display: grid; place-items: center; border: 2px solid #fff; }
.panel { position: absolute; right: 0; top: calc(100% + 8px); width: 380px; max-width: calc(100vw - 24px); background: #fff; border: 1px solid var(--gris-200); border-radius: 14px; box-shadow: var(--sombra-m); z-index: 90; overflow: hidden; }
.cab { display: flex; justify-content: space-between; align-items: center; padding: 14px 16px; border-bottom: 1px solid var(--gris-200); color: var(--titulo); }
.lista { max-height: 420px; overflow-y: auto; }
.noti { display: flex; gap: 12px; width: 100%; padding: 12px 16px; border: 0; background: none; text-align: left; border-bottom: 1px solid var(--gris-100); }
.noti:hover { background: var(--gris-50); }
.noti.nueva { background: #f6fdf9; }
.noti.nueva .t::before { content: ''; display: inline-block; width: 7px; height: 7px; border-radius: 50%; background: var(--verde); margin-right: 7px; vertical-align: middle; }
.ico { width: 34px; height: 34px; border-radius: 50%; background: var(--gris-100); color: var(--texto-2); display: grid; place-items: center; flex-shrink: 0; }
.t { display: block; font-weight: 600; color: var(--titulo); font-size: 14px; }
.m { display: block; color: var(--texto-2); font-size: 13px; margin-top: 2px; }
.h { display: block; color: var(--texto-3); font-size: 12px; margin-top: 3px; }
.todas { display: block; text-align: center; padding: 12px; font-weight: 600; font-size: 14px; border-top: 1px solid var(--gris-200); }
@media (max-width: 767px) { .panel { position: fixed; top: 64px; left: 12px; right: 12px; width: auto; } }
</style>
