<script setup lang="ts">
// Pestaña "Datos personales" de la ficha: solo lectura, con acceso a la edición.
import { computed } from 'vue'
import Icono from '@/componentes/Icono.vue'
import { fechaMedia } from '@/utils/formato'

const props = defineProps<{ ficha: any; id: number }>()
defineEmits<{ actualizar: [] }>()

const p = computed(() => props.ficha?.personales ?? {})
function edad(f?: string | null) {
  if (!f) return null
  const [a, m, d] = f.split('-').map(Number)
  const hoy = new Date()
  let n = hoy.getFullYear() - a
  if (hoy.getMonth() + 1 < m || (hoy.getMonth() + 1 === m && hoy.getDate() < d)) n--
  return n
}
const filas = computed(() => {
  const x = p.value
  const e = edad(x.fechaNacimiento)
  return [
    { icono: 'persona', etiqueta: 'Nombre(s)', valor: props.ficha?.nombres },
    { icono: 'persona', etiqueta: 'Apellidos', valor: props.ficha?.apellidos },
    { icono: 'correo', etiqueta: 'Correo electrónico', valor: x.email, enlace: x.email ? `mailto:${x.email}` : null },
    { icono: 'telefono', etiqueta: 'Teléfono', valor: x.telefono, enlace: x.telefono ? `tel:${x.telefono}` : null },
    { icono: 'pastel', etiqueta: 'Fecha de nacimiento', valor: x.fechaNacimiento ? `${fechaMedia(x.fechaNacimiento, true)}${e !== null ? ` (${e} años)` : ''}` : null },
    { icono: 'personas', etiqueta: 'Género', valor: x.genero },
    { icono: 'fichaPersona', etiqueta: 'CURP', valor: x.curp, mono: true },
    { icono: 'fichaPersona', etiqueta: 'RFC', valor: x.rfc, mono: true },
    { icono: 'escudo', etiqueta: 'NSS', valor: x.nss, mono: true },
    { icono: 'ubicacion', etiqueta: 'Dirección', valor: x.direccion },
  ] as { icono: string; etiqueta: string; valor?: string | null; enlace?: string | null; mono?: boolean }[]
})
</script>

<template>
  <div class="pestana-datos">
    <section class="tarjeta">
      <div class="tarjeta-cabecera">
        <div class="izq"><Icono n="fichaPersona" :t="26" /><h2 class="titulo-tarjeta">Datos personales</h2></div>
        <RouterLink v-if="ficha.puedeEditar" :to="`/colaboradores/${id}/editar`" class="btn btn-contorno btn-ch"><Icono n="lapiz" :t="17" />Editar</RouterLink>
      </div>
      <dl class="rejilla-datos">
        <div v-for="f in filas" :key="f.etiqueta" class="dato">
          <dt><Icono :n="f.icono" :t="18" />{{ f.etiqueta }}</dt>
          <dd :class="{ mono: f.mono }">
            <a v-if="f.valor && f.enlace" :href="f.enlace">{{ f.valor }}</a>
            <template v-else-if="f.valor">{{ f.valor }}</template>
            <span v-else class="muted">—</span>
          </dd>
        </div>
      </dl>
    </section>

    <section class="tarjeta">
      <div class="tarjeta-cabecera"><div class="izq"><Icono n="alertaCirculo" :t="26" /><h2 class="titulo-tarjeta">Contacto de emergencia</h2></div></div>
      <div v-if="p.contactoEmergenciaNombre || p.contactoEmergenciaTelefono" class="emergencia">
        <span class="em-ico"><Icono n="telefono" :t="24" /></span>
        <div>
          <b>{{ p.contactoEmergenciaNombre || 'Sin nombre registrado' }}</b>
          <a v-if="p.contactoEmergenciaTelefono" :href="`tel:${p.contactoEmergenciaTelefono}`">{{ p.contactoEmergenciaTelefono }}</a>
          <span v-else class="muted">Sin teléfono registrado</span>
        </div>
      </div>
      <p v-else class="vacio">Sin contacto de emergencia registrado.</p>
    </section>
  </div>
</template>

<style scoped>
.pestana-datos { display: grid; grid-template-columns: minmax(0, 2fr) minmax(0, 1fr); gap: 20px; align-items: start; }
.tarjeta-cabecera .izq .icono { color: var(--titulo); }
.rejilla-datos { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0 32px; margin: 0; }
.dato { padding: 13px 0; border-bottom: 1px solid var(--gris-100); min-width: 0; }
.dato dt { display: flex; align-items: center; gap: 8px; color: var(--texto-3); font-size: 13.5px; }
.dato dt .icono { color: var(--gris-500); }
.dato dd { margin: 4px 0 0 26px; color: var(--titulo); font-size: 15.5px; font-weight: 500; overflow-wrap: anywhere; }
.dato dd.mono { font-family: ui-monospace, Consolas, monospace; font-size: 15px; letter-spacing: .02em; }
.emergencia { display: flex; gap: 14px; align-items: center; background: var(--gris-50); border-radius: 10px; padding: 14px 16px; }
.emergencia div { display: flex; flex-direction: column; gap: 2px; font-size: 15px; }
.emergencia b { color: var(--titulo); font-weight: 650; }
.em-ico { width: 48px; height: 48px; border-radius: 50%; background: var(--rojo-claro); color: var(--rojo-texto); display: grid; place-items: center; flex-shrink: 0; }
@media (max-width: 1100px) { .pestana-datos { grid-template-columns: 1fr; } }
@media (max-width: 640px) { .rejilla-datos { grid-template-columns: 1fr; } }
</style>
