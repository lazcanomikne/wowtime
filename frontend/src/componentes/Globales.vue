<script setup lang="ts">
// Avisos flotantes y diálogo de confirmación (con texto opcional, p. ej. motivo de rechazo).
import { ref, watch } from 'vue'
import { useUi } from '@/stores/ui'
import Modal from './Modal.vue'
import Icono from './Icono.vue'
const ui = useUi()
const texto = ref('')
const intento = ref(false)
watch(() => ui.confirmacion, () => { texto.value = ''; intento.value = false })
function aceptar() {
  const c = ui.confirmacion!
  if (c.pedirTexto?.obligatorio && !texto.value.trim()) { intento.value = true; return }
  ui.cerrarConfirmacion({ ok: true, texto: texto.value.trim() })
}
</script>

<template>
  <div class="avisos" aria-live="polite">
    <TransitionGroup name="aviso">
      <div v-for="a in ui.avisos" :key="a.id" class="aviso" :class="a.tipo">
        <Icono :n="a.tipo === 'ok' ? 'checkCirculo' : a.tipo === 'error' ? 'alertaCirculo' : 'info'" :t="20" />
        <span>{{ a.texto }}</span>
      </div>
    </TransitionGroup>
  </div>
  <Modal v-if="ui.confirmacion" :titulo="ui.confirmacion.titulo" :ancho="460" @cerrar="ui.cerrarConfirmacion({ ok: false })">
    <p v-if="ui.confirmacion.mensaje" class="msg">{{ ui.confirmacion.mensaje }}</p>
    <div v-if="ui.confirmacion.pedirTexto" class="campo" style="margin-top: 14px">
      <label for="conf-texto">{{ ui.confirmacion.pedirTexto.etiqueta }}</label>
      <textarea id="conf-texto" v-model="texto" class="entrada" :class="{ error: intento && !texto.trim() }" :placeholder="ui.confirmacion.pedirTexto.placeholder" rows="3" />
      <span v-if="intento && !texto.trim()" class="error-campo">Este dato es obligatorio.</span>
    </div>
    <template #pie>
      <button class="btn btn-contorno" @click="ui.cerrarConfirmacion({ ok: false })">{{ ui.confirmacion.cancelar ?? 'Cancelar' }}</button>
      <button class="btn" :class="ui.confirmacion.peligro ? 'btn-peligro' : 'btn-primario'" data-prueba="confirmar" @click="aceptar">{{ ui.confirmacion.aceptar ?? 'Aceptar' }}</button>
    </template>
  </Modal>
</template>

<style scoped>
.avisos { position: fixed; right: 18px; bottom: 18px; z-index: 300; display: flex; flex-direction: column; gap: 10px; max-width: min(420px, calc(100vw - 36px)); }
.aviso { display: flex; gap: 10px; align-items: flex-start; padding: 13px 16px; border-radius: 10px; background: #fff; box-shadow: var(--sombra-m); border-left: 4px solid var(--verde); font-size: 14.5px; color: var(--titulo); }
.aviso.ok { color: var(--verde-texto); }
.aviso.ok span { color: var(--titulo); }
.aviso.error { border-left-color: var(--rojo); color: var(--rojo-texto); }
.aviso.error span { color: var(--titulo); }
.aviso.info { border-left-color: var(--azul); color: var(--azul); }
.aviso.info span { color: var(--titulo); }
.aviso-enter-from, .aviso-leave-to { opacity: 0; transform: translateY(10px); }
.aviso-enter-active, .aviso-leave-active { transition: all .2s; }
.msg { color: var(--texto-2); font-size: 15px; }
@media (max-width: 767px) { .avisos { bottom: 84px; right: 12px; left: 12px; max-width: none; } }
</style>
