<script setup lang="ts">
// Detalle de una invitación: datos, vigencia, accesos, QR real y enlace público para compartir.
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { api, type Persona as TPersona } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion, useCarga, useEsMovil, useEventos } from '@/utils/usos'
import { fechaCorta, fechaLarga, hora } from '@/utils/formato'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Persona from '@/componentes/Persona.vue'
import Estado from '@/componentes/Estado.vue'
import Qr from '@/componentes/Qr.vue'
import EncabezadoMovil from '@/layout/EncabezadoMovil.vue'

interface Invitacion {
  id: number; nombre: string; empresaMotivo: string; telefono: string | null; correo: string | null; fechaLlegada: string; llegadaLocal: string; fechaLocal: string
  anfitrion: TPersona | null; sucursalId: number; sucursal: string | null; estado: string; vigenciaDesdeLocal: string; vigenciaHastaLocal: string
  qr: string; enlace: string; entrada: string | null; salida: string | null
}

const route = useRoute()
const s = useSesion()
const ui = useUi()
const esMovil = useEsMovil()
const { ocupado, ejecutar } = useAccion()
const id = computed(() => Number(route.params.id))
const conEncabezadoMovil = computed(() => esMovil.value && !s.esGestion)
const regreso = computed(() => (s.tiene('visitantes.ver') ? '/visitantes' : '/mis-invitados'))

const { datos: d, cargando, error, cargar } = useCarga(() => api.get<Invitacion>(`/visitantes/invitaciones/${id.value}`))
useEventos((t, x) => { if (t === 'visitante' && (!x || x.id === id.value)) cargar(true) })

const qrActivo = computed(() => d.value?.estado === 'Esperado' || d.value?.estado === 'Dentro')
const mismaFechaVigencia = computed(() => !!d.value && d.value.vigenciaDesdeLocal.slice(0, 10) === d.value.vigenciaHastaLocal.slice(0, 10))
const puedeCompartir = typeof navigator !== 'undefined' && typeof navigator.share === 'function'

async function copiar() {
  const texto = d.value!.enlace
  try {
    await navigator.clipboard.writeText(texto)
  } catch {
    const t = document.createElement('textarea')
    t.value = texto; t.setAttribute('readonly', ''); t.style.position = 'fixed'; t.style.opacity = '0'
    document.body.appendChild(t); t.select()
    try { document.execCommand('copy') } finally { t.remove() }
  }
  ui.avisar('Enlace copiado.')
}
async function compartir() {
  const x = d.value!
  if (!puedeCompartir) { await copiar(); return }
  try {
    await navigator.share({
      title: 'Invitación WOW! Time',
      text: `Hola ${x.nombre}, te esperamos el ${fechaLarga(x.llegadaLocal).toLowerCase()} a las ${hora(x.llegadaLocal)}. Presenta este código QR en recepción:`,
      url: x.enlace
    })
  } catch (e: any) {
    if (e?.name !== 'AbortError') await copiar()
  }
}
async function cancelar() {
  const x = d.value!
  const r = await ui.confirmar({
    titulo: 'Cancelar invitación',
    mensaje: `El código QR de ${x.nombre} dejará de ser válido.`,
    aceptar: 'Cancelar invitación', cancelar: 'Regresar', peligro: true
  })
  if (!r.ok) return
  const res = await ejecutar(() => api.post<Invitacion>(`/visitantes/invitaciones/${x.id}/cancelar`, {}), 'Invitación cancelada.')
  if (res) d.value = res
}
</script>

<template>
  <div class="detalle-visitante" :class="{ movil: conEncabezadoMovil }">
    <EncabezadoMovil v-if="conEncabezadoMovil" titulo="Invitación" :atras="regreso" />

    <div class="cuerpo">
      <div v-if="cargando && !d" class="columna">
        <div class="esqueleto" style="height: 64px" />
        <div class="esqueleto" style="height: 320px" />
      </div>
      <div v-else-if="error && !d" class="tarjeta vacio">
        <Icono n="alertaCirculo" :t="30" />
        <p style="margin-top: 8px">{{ error }}</p>
        <RouterLink :to="regreso" class="btn btn-contorno btn-ch" style="margin-top: 14px">Regresar</RouterLink>
      </div>

      <template v-else-if="d">
        <div class="encabezado">
          <RouterLink v-if="!conEncabezadoMovil" :to="regreso" class="ver-todo regresar"><Icono n="izquierda" :t="18" />Regresar</RouterLink>
          <div class="fila titulo-fila">
            <Avatar :nombre="d.nombre" :t="esMovil ? 52 : 64" />
            <div class="crecer">
              <div class="fila envolver" style="gap: 12px">
                <h1 class="titulo-pagina">{{ d.nombre }}</h1>
                <Estado :v="d.estado" />
              </div>
              <p class="subtitulo-pagina">{{ d.empresaMotivo }}</p>
            </div>
          </div>
        </div>

        <div class="rejilla">
          <section class="tarjeta">
            <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Datos de la visita</h2></div>
            <div class="datos">
              <div class="dato">
                <span class="dato-icono verde"><Icono n="calendario" :t="20" /></span>
                <div>
                  <div class="dato-etiqueta">Llegada</div>
                  <div class="dato-valor">{{ fechaLarga(d.llegadaLocal, true) }}</div>
                  <div class="muted">{{ hora(d.llegadaLocal) }} h</div>
                </div>
              </div>
              <div class="dato">
                <span class="dato-icono azul"><Icono n="qr" :t="20" /></span>
                <div>
                  <div class="dato-etiqueta">Vigencia del QR</div>
                  <div class="dato-valor">De {{ hora(d.vigenciaDesdeLocal) }} a {{ hora(d.vigenciaHastaLocal) }}</div>
                  <div class="muted">
                    <template v-if="mismaFechaVigencia">{{ fechaCorta(d.vigenciaDesdeLocal) }}</template>
                    <template v-else>{{ fechaCorta(d.vigenciaDesdeLocal) }} – {{ fechaCorta(d.vigenciaHastaLocal) }}</template>
                  </div>
                </div>
              </div>
              <div class="dato">
                <span class="dato-icono gris"><Icono n="edificio" :t="20" /></span>
                <div>
                  <div class="dato-etiqueta">Sucursal</div>
                  <div class="dato-valor">{{ d.sucursal ?? '—' }}</div>
                </div>
              </div>
              <div v-if="d.telefono || d.correo" class="dato">
                <span class="dato-icono gris"><Icono :n="d.telefono ? 'telefono' : 'correo'" :t="20" /></span>
                <div class="min0">
                  <div class="dato-etiqueta">Contacto</div>
                  <div v-if="d.telefono" class="dato-valor"><a :href="`tel:${d.telefono}`">{{ d.telefono }}</a></div>
                  <div v-if="d.correo" class="recorte"><a :href="`mailto:${d.correo}`">{{ d.correo }}</a></div>
                </div>
              </div>
            </div>

            <div class="separador" />
            <div class="dato-etiqueta" style="margin-bottom: 8px">Anfitrión</div>
            <Persona :p="d.anfitrion" :t="44" />

            <div class="separador" />
            <div class="accesos">
              <div class="acceso" :class="{ hecho: d.entrada }">
                <span class="acceso-icono"><Icono n="flecha" :t="20" /></span>
                <div>
                  <div class="dato-etiqueta">Entrada</div>
                  <div class="dato-valor">{{ d.entrada ? hora(d.entrada) : 'Sin registrar' }}</div>
                  <div v-if="d.entrada" class="pequeno muted">{{ fechaCorta(d.entrada) }}</div>
                </div>
              </div>
              <div class="acceso salida" :class="{ hecho: d.salida }">
                <span class="acceso-icono"><Icono n="salir" :t="20" /></span>
                <div>
                  <div class="dato-etiqueta">Salida</div>
                  <div class="dato-valor">{{ d.salida ? hora(d.salida) : 'Sin registrar' }}</div>
                  <div v-if="d.salida" class="pequeno muted">{{ fechaCorta(d.salida) }}</div>
                </div>
              </div>
            </div>

            <template v-if="d.estado === 'Esperado'">
              <div class="separador" />
              <div class="fila-entre envolver">
                <span class="muted pequeno">Solo se pueden cancelar invitaciones que aún no se usan.</span>
                <button class="btn btn-rechazar" :disabled="ocupado" data-prueba="cancelar-invitacion" @click="cancelar"><Icono n="x" :t="18" />Cancelar invitación</button>
              </div>
            </template>
          </section>

          <section class="tarjeta qr-tarjeta">
            <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">Invitación QR</h2></div>
            <div class="qr-caja" :class="{ inactivo: !qrActivo }">
              <Qr :valor="d.qr" :t="esMovil ? 220 : 240" />
              <span v-if="!qrActivo" class="qr-sello"><Estado :v="d.estado" /></span>
            </div>
            <p class="centro pequeno muted">Válido de {{ hora(d.vigenciaDesdeLocal) }} a {{ hora(d.vigenciaHastaLocal) }} · solo para el acceso a nuestras instalaciones</p>

            <div class="campo" style="margin-top: 16px">
              <label for="enlace">Enlace público</label>
              <input id="enlace" class="entrada" :value="d.enlace" readonly @focus="($event.target as HTMLInputElement).select()" />
            </div>
            <div class="botones">
              <button class="btn btn-contorno" data-prueba="copiar-enlace" @click="copiar"><Icono n="copiar" :t="18" />Copiar enlace</button>
              <button class="btn btn-primario" data-prueba="compartir" @click="compartir"><Icono n="compartir" :t="18" />Compartir</button>
            </div>
            <a :href="d.enlace" target="_blank" rel="noopener" class="ver-todo centro" style="margin-top: 12px">Abrir página del visitante <Icono n="externo" :t="15" /></a>
          </section>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.detalle-visitante.movil .cuerpo { padding: 16px; }
.encabezado { margin-bottom: 20px; }
.regresar { margin-bottom: 10px; }
.titulo-fila { gap: 16px; align-items: center; }
.rejilla { display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(300px, 1fr); gap: 20px; align-items: start; }
.datos { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 18px 20px; }
.dato { display: flex; gap: 12px; align-items: flex-start; min-width: 0; }
.min0 { min-width: 0; }
.dato-icono { width: 40px; height: 40px; border-radius: 50%; display: grid; place-items: center; flex-shrink: 0; }
.dato-icono.verde { background: var(--verde-claro); color: var(--verde-texto); }
.dato-icono.azul { background: var(--azul-claro); color: var(--azul); }
.dato-icono.gris { background: var(--gris-100); color: var(--texto-2); }
.dato-etiqueta { font-size: 13px; color: var(--texto-3); }
.dato-valor { font-size: 16px; font-weight: 700; color: var(--titulo); }
.recorte { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.accesos { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.acceso { display: flex; gap: 12px; align-items: center; padding: 12px 14px; border-radius: var(--radio-s); background: var(--gris-50); border: 1px solid var(--gris-200); }
.acceso-icono { width: 38px; height: 38px; border-radius: 50%; display: grid; place-items: center; background: #fff; color: var(--texto-3); flex-shrink: 0; }
.acceso.hecho { background: var(--verde-suave); border-color: var(--verde-borde); }
.acceso.hecho .acceso-icono { background: var(--verde); color: #fff; }
.acceso.salida.hecho { background: var(--azul-claro); border-color: #cfe0f6; }
.acceso.salida.hecho .acceso-icono { background: var(--azul); }
.qr-tarjeta { display: flex; flex-direction: column; }
.qr-caja { position: relative; align-self: center; padding: 14px; border: 1px solid var(--gris-200); border-radius: 16px; background: #fff; margin-bottom: 12px; }
.qr-caja.inactivo :deep(.qr) { opacity: .18; filter: grayscale(1); }
.qr-sello { position: absolute; inset: 0; display: grid; place-items: center; }
.centro { text-align: center; align-self: center; }
.botones { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-top: 12px; }
@media (max-width: 1000px) {
  .rejilla { grid-template-columns: minmax(0, 1fr); }
  .qr-tarjeta { order: -1; }
}
@media (max-width: 760px) {
  .detalle-visitante.movil .titulo-pagina { font-size: 24px; }
  .detalle-visitante.movil .subtitulo-pagina { font-size: 15px; }
  .accesos { grid-template-columns: minmax(0, 1fr); }
}
</style>
