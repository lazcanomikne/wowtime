<script setup lang="ts">
// Registrar invitado (App_Movil.png vista 3 y Registro_de_invitados.png). Fecha y hora en dos campos; el QR real aparece solo al generar.
import { computed, ref } from 'vue'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Qr from '@/componentes/Qr.vue'
import CampoFecha from '@/componentes/CampoFecha.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'
import { api, ErrorApi } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { fechaMedia, hora, iso } from '@/utils/formato'

const s = useSesion()
const ui = useUi()
const proxHora = () => { const d = new Date(); d.setMinutes(Math.ceil((d.getMinutes() + 1) / 15) * 15, 0, 0); return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}` }
const vacio = () => ({ nombre: '', empresaMotivo: '', telefono: '', correo: '', fecha: iso(new Date()), hora: proxHora() })
const f = ref(vacio())
const horas = Array.from({ length: 96 }, (_, k) => `${String(Math.floor(k / 4)).padStart(2, '0')}:${String((k % 4) * 15).padStart(2, '0')}`)
const errores = ref<Record<string, string>>({})
const generando = ref(false)
const inv = ref<any>(null)

async function generar() {
  errores.value = {}
  if (f.value.nombre.trim().length < 3) errores.value.nombre = 'Escribe el nombre completo del visitante.'
  if (!f.value.empresaMotivo.trim()) errores.value.empresaMotivo = 'Escribe la empresa o el motivo de la visita.'
  if (Object.keys(errores.value).length) return
  generando.value = true
  try {
    inv.value = await api.post('/visitantes/invitaciones', { nombre: f.value.nombre, empresaMotivo: f.value.empresaMotivo, telefono: f.value.telefono || null, correo: f.value.correo || null, fecha: f.value.fecha, hora: f.value.hora + ':00' })
  } catch (e) {
    if (e instanceof ErrorApi && e.detalles && typeof e.detalles === 'object' && !Array.isArray(e.detalles)) errores.value = e.detalles as any
    ui.error(e)
  } finally { generando.value = false }
}
const textoCompartir = computed(() => inv.value ? `Hola ${inv.value.nombre}, te espero el ${fechaMedia(inv.value.llegadaLocal)} a las ${hora(inv.value.llegadaLocal)}. Presenta este código QR en recepción: ${inv.value.enlace}` : '')
async function compartir() {
  if (navigator.share) { try { await navigator.share({ title: 'Invitación WOW! Time', text: textoCompartir.value, url: inv.value.enlace }) } catch { /* cancelado */ } }
  else copiar()
}
function copiar() { navigator.clipboard?.writeText(inv.value.enlace); ui.avisar('Enlace copiado') }
const whatsapp = computed(() => inv.value?.telefono ? `https://wa.me/52${inv.value.telefono.replace(/\D/g, '').slice(-10)}?text=${encodeURIComponent(textoCompartir.value)}` : null)
function otro() { inv.value = null; f.value = vacio() }
</script>

<template>
  <div class="ri">
    <EncabezadoMovil titulo="Registrar invitado" />
    <div class="cuerpo">
      <template v-if="!inv">
        <p class="intro">Comparte los datos del visitante para agilizar su ingreso.</p>
        <div class="campo">
          <label for="n">Nombre completo</label>
          <div class="con-icono"><Icono n="persona" :t="18" /><input id="n" v-model="f.nombre" class="entrada" :class="{ error: errores.nombre }" placeholder="Ej. Carlos López Ramírez" autocomplete="off" /></div>
          <span v-if="errores.nombre" class="error-campo">{{ errores.nombre }}</span>
        </div>
        <div class="campo">
          <label for="e">Empresa o motivo</label>
          <div class="con-icono"><Icono n="edificio" :t="18" /><input id="e" v-model="f.empresaMotivo" class="entrada" :class="{ error: errores.empresaMotivo }" placeholder="Ej. Cliente / Entrevista / Mantenimiento" /></div>
          <span v-if="errores.empresaMotivo" class="error-campo">{{ errores.empresaMotivo }}</span>
        </div>
        <div class="dos">
          <div class="campo"><label for="t">Teléfono (opcional)</label><input id="t" v-model="f.telefono" class="entrada" type="tel" inputmode="tel" placeholder="81 1234 5678" /></div>
          <div class="campo"><label for="c">Correo (opcional)</label><input id="c" v-model="f.correo" class="entrada" type="email" placeholder="nombre@correo.com" /><span v-if="errores.correo" class="error-campo">{{ errores.correo }}</span></div>
        </div>
        <div class="dos">
          <div class="campo"><label for="fe">Fecha de llegada</label><CampoFecha id="fe" :model-value="f.fecha" :min="iso(new Date())" @update:model-value="v => (f.fecha = v ?? iso(new Date()))" /></div>
          <div class="campo"><label for="ho">Hora</label>
            <div class="con-icono"><Icono n="reloj" :t="18" /><select id="ho" v-model="f.hora" class="entrada"><option v-for="h in horas" :key="h" :value="h">{{ h }}</option></select></div>
          </div>
        </div>
        <div class="anfitrion">
          <Avatar :nombre="s.me?.nombre" :iniciales="s.me?.colaborador?.iniciales" :foto="s.me?.colaborador?.fotoUrl" :t="40" />
          <div class="crecer"><small>Anfitrión</small><b>{{ s.me?.colaborador?.nombre ?? s.me?.nombre }}</b><span v-if="s.frase('invitado_anfitrion')" class="frase">{{ s.frase('invitado_anfitrion') }}</span></div>
        </div>
        <div class="qr-prev">
          <span class="qr-ico"><Icono n="qr" :t="54" :g="1.4" /></span>
          <div><b>Invitación QR temporal</b><p>Se generará un código único para este visitante.</p></div>
        </div>
        <button class="btn btn-primario btn-g btn-bloque" :disabled="generando" data-prueba="generar-invitacion" @click="generar">
          <Icono n="enviar" :t="20" />Generar invitación
        </button>
        <p class="pie"><Icono n="info" :t="16" />El visitante recibirá un QR para su acceso.</p>
      </template>

      <template v-else>
        <div class="listo">
          <span class="ok"><Icono n="check" :t="30" :g="3" /></span>
          <h2>Invitación generada</h2>
          <p>{{ inv.nombre }} · {{ inv.empresaMotivo }}</p>
        </div>
        <div class="tarjeta qr-t">
          <Qr :valor="inv.qr" :t="230" />
          <b>{{ fechaMedia(inv.llegadaLocal, true) }}, {{ hora(inv.llegadaLocal) }}</b>
          <span class="muted">Válido de {{ hora(inv.vigenciaDesdeLocal) }} a {{ hora(inv.vigenciaHastaLocal) }}</span>
          <p class="aviso">Válido por tiempo limitado y solo para el acceso a nuestras instalaciones.</p>
        </div>
        <button class="btn btn-primario btn-g btn-bloque" @click="compartir"><Icono n="compartir" :t="20" />Compartir invitación</button>
        <div class="dos">
          <button class="btn btn-contorno" @click="copiar"><Icono n="copiar" :t="18" />Copiar enlace</button>
          <a v-if="whatsapp" :href="whatsapp" target="_blank" rel="noopener" class="btn btn-contorno"><Icono n="telefono" :t="18" />WhatsApp</a>
          <RouterLink v-else :to="`/visitantes/${inv.id}`" class="btn btn-contorno"><Icono n="ojo" :t="18" />Ver detalle</RouterLink>
        </div>
        <button class="btn btn-fantasma btn-bloque" @click="otro"><Icono n="personaMas" :t="18" />Registrar otro invitado</button>
      </template>
    </div>
  </div>
</template>

<style scoped>
.ri { min-height: 100%; background: var(--fondo); }
.cuerpo { padding: 16px 14px 24px; display: flex; flex-direction: column; gap: 14px; max-width: 560px; margin: 0 auto; }
.intro { color: var(--texto-2); font-size: 14.5px; }
.campo label { font-size: 14px; font-weight: 600; }
.dos { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.anfitrion { display: flex; align-items: center; gap: 12px; background: var(--gris-100); border: 1px solid var(--gris-200); border-radius: 10px; padding: 12px 14px; }
.anfitrion small { display: block; font-size: 12px; color: var(--texto-3); }
.anfitrion b { display: block; font-size: 15px; color: var(--titulo); }
.frase { display: block; font-size: 12px; color: var(--verde-texto); margin-top: 2px; }
.qr-prev { display: flex; align-items: center; gap: 16px; background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; padding: 14px; }
.qr-ico { width: 84px; height: 84px; border: 2px dashed var(--gris-300); border-radius: 10px; display: grid; place-items: center; color: var(--gris-400); flex-shrink: 0; }
.qr-prev b { font-size: 16px; color: var(--titulo); }
.qr-prev p { font-size: 13.5px; color: var(--texto-2); margin-top: 4px; }
.pie { display: flex; gap: 6px; align-items: center; justify-content: center; color: var(--texto-2); font-size: 13px; }
.listo { text-align: center; display: flex; flex-direction: column; align-items: center; gap: 6px; padding-top: 6px; }
.listo .ok { width: 58px; height: 58px; border-radius: 50%; background: var(--verde); color: #fff; display: grid; place-items: center; }
.listo h2 { font-size: 21px; }
.listo p { color: var(--texto-2); }
.qr-t { display: flex; flex-direction: column; align-items: center; gap: 6px; text-align: center; }
.qr-t b { margin-top: 8px; color: var(--titulo); }
.aviso { font-size: 12.5px; color: var(--texto-3); max-width: 280px; margin-top: 4px; }
@media (max-width: 380px) { .dos { grid-template-columns: 1fr; } }
</style>
