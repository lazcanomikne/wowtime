<script setup lang="ts">
// Organización y aprobaciones (Organigrama_Autorizaciones.png): organigrama, reglas, delegaciones y alerta de estructura.
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icono from '@/componentes/Icono.vue'
import Avatar from '@/componentes/Avatar.vue'
import Persona from '@/componentes/Persona.vue'
import Modal from '@/componentes/Modal.vue'
import SelectorColaborador from '@/componentes/SelectorColaborador.vue'
import OrgNodo from './OrgNodo.vue'
import { api } from '@/api'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useAccion } from '@/utils/usos'
import { rango, iso } from '@/utils/formato'

const s = useSesion()
const ui = useUi()
const route = useRoute()
const router = useRouter()
const { ejecutar, ocupado } = useAccion()
const tab = computed(() => (route.query.tab as string) || 'organigrama')
const org = ref<any>(null)
const reglas = ref<any[]>([])
const delegaciones = ref<any[]>([])
const abiertos = ref(new Set<number>())
const zoom = ref(100)
const editar = ref(false)
const arrastrando = ref<number | null>(null)
const selSuc = ref(false)
const menuRegla = ref<number | null>(null)
const menuDel = ref<number | null>(null)
const modal = ref<'' | 'regla' | 'delegacion' | 'sinsup'>('')
const reglaEdit = ref<any>(null)
const delEdit = ref<any>(null)
const asignar = ref<Record<number, number | null>>({})
const prueba = ref<{ colaboradorId: number | null; tipo: string; resultado: any }>({ colaboradorId: null, tipo: 'Vacaciones', resultado: null })

const ICONOS: Record<string, [string, string]> = { vacaciones: ['vacaciones', '#0aaa4a'], permisos: ['documento', '#2f74d6'], incidencias: ['reloj', '#f26913'], solicitud_supervisor: ['personas', '#2f74d6'] }
const TIPOS = [{ v: 'Vacaciones', t: 'Vacaciones' }, { v: 'Permiso', t: 'Permisos' }, { v: 'IncidenciaAsistencia', t: 'Incidencias' }]
const APROBADORES = [{ v: 'SupervisorDirecto', t: 'Supervisor directo' }, { v: 'Gerente', t: 'Gerente' }, { v: 'RecursosHumanos', t: 'Recursos Humanos' }]

async function cargarOrg() {
  org.value = await api.get('/organigrama', { sucursalId: s.sucursalId ?? undefined })
  const ab = new Set<number>()
  const recorrer = (n: any, nivel: number) => {
    if (nivel === 0 || n.hijos.length <= 3) ab.add(n.id)
    n.hijos.forEach((h: any) => recorrer(h, nivel + 1))
  }
  org.value.raices.forEach((r: any) => recorrer(r, 0))
  abiertos.value = ab
  centrar()
  if (route.query.sinSupervisor) modal.value = 'sinsup'
}
const lienzo = ref<HTMLElement>()
function centrar() { nextTick(() => { const l = lienzo.value; if (l) l.scrollLeft = Math.max(0, (l.scrollWidth - l.clientWidth) / 2) }) }
async function cargarReglas() { reglas.value = await api.get('/aprobaciones/reglas') }
async function cargarDelegaciones() { delegaciones.value = (await api.get('/aprobaciones/delegaciones', { sucursalId: s.sucursalId ?? undefined, tamano: 50 })).items }
async function todo() { await Promise.allSettled([cargarOrg(), cargarReglas(), cargarDelegaciones()]) }
onMounted(() => { todo(); document.addEventListener('mousedown', cerrar) })
onBeforeUnmount(() => document.removeEventListener('mousedown', cerrar))
watch(() => s.sucursalId, todo)
function cerrar(e: MouseEvent) {
  const t = e.target as HTMLElement
  if (!t.closest('.menu-wrap')) { menuRegla.value = null; menuDel.value = null }
  if (!t.closest('.sel-suc')) selSuc.value = false
}

function alternar(id: number) { const n = new Set(abiertos.value); n.has(id) ? n.delete(id) : n.add(id); abiertos.value = n; centrar() }
const todosIds = computed(() => { const ids: number[] = []; const r = (n: any) => { if (n.hijos.length) ids.push(n.id); n.hijos.forEach(r) }; org.value?.raices.forEach(r); return ids })
const todoAbierto = computed(() => todosIds.value.every(id => abiertos.value.has(id)))
function expandirTodo() { abiertos.value = todoAbierto.value ? new Set(org.value.raices.map((r: any) => r.id)) : new Set(todosIds.value) }

async function soltar(id: number, jefe: number) {
  arrastrando.value = null
  const buscar = (n: any): any => n.id === id ? n : n.hijos.map(buscar).find(Boolean)
  const nodo = org.value.raices.map(buscar).find(Boolean)
  const jefeN = org.value.raices.map((n: any) => (function b(x: any): any { return x.id === jefe ? x : x.hijos.map(b).find(Boolean) })(n)).find(Boolean)
  if (!nodo || !jefeN || nodo.supervisorId === jefe) return
  const r = await ui.confirmar({ titulo: 'Cambiar línea de reporte', mensaje: `${nodo.nombre} reportará a ${jefeN.nombre}.`, aceptar: 'Cambiar' })
  if (!r.ok) return
  await ejecutar(async () => { await api.put(`/colaboradores/${id}/supervisor`, { supervisorId: jefe }); await cargarOrg() }, 'Línea de reporte actualizada')
}
async function asignarSupervisor(id: number) {
  await ejecutar(async () => { await api.put(`/colaboradores/${id}/supervisor`, { supervisorId: asignar.value[id] }); await cargarOrg() }, 'Supervisor asignado')
  if (!org.value?.sinSupervisor.length) modal.value = ''
}

// Reglas
function editarRegla(r: any) { menuRegla.value = null; reglaEdit.value = { id: r.id, nombre: r.nombre, activa: r.activa, pasos: r.pasos.map((p: any) => p.tipoAprobador) }; modal.value = 'regla' }
async function guardarRegla() {
  const r = await ejecutar(() => api.put(`/aprobaciones/reglas/${reglaEdit.value.id}`, reglaEdit.value), 'Regla guardada')
  if (r) { r.avisos?.forEach((a: string) => ui.avisar(a, 'info', 7000)); modal.value = ''; await cargarReglas() }
}
async function alternarActiva(r: any) {
  menuRegla.value = null
  if (r.activa) {
    const c = await ui.confirmar({ titulo: 'Desactivar regla', mensaje: `Una regla inactiva deja sin ruta a las solicitudes de "${r.nombre}": nadie podrá enviarlas hasta reactivarla.`, aceptar: 'Desactivar', peligro: true })
    if (!c.ok) return
  }
  const res = await ejecutar(() => api.put(`/aprobaciones/reglas/${r.id}`, { nombre: r.nombre, activa: !r.activa, pasos: r.pasos.map((p: any) => p.tipoAprobador) }), r.activa ? 'Regla desactivada' : 'Regla activada')
  if (res) { res.avisos?.forEach((a: string) => ui.avisar(a, 'info', 7000)); await cargarReglas() }
}
async function duplicar(r: any) { menuRegla.value = null; await ejecutar(async () => { await api.post(`/aprobaciones/reglas/${r.id}/duplicar`); await cargarReglas() }, 'Regla duplicada (inactiva)') }

// Delegaciones
function nuevaDelegacion() { delEdit.value = { deColaboradorId: null, haciaColaboradorId: null, fechaInicio: iso(new Date()), fechaFin: iso(new Date(Date.now() + 4 * 864e5)), tipos: ['Vacaciones', 'Permiso'], de: null, hacia: null }; modal.value = 'delegacion' }
function editarDelegacion(d: any) { menuDel.value = null; delEdit.value = { id: d.id, deColaboradorId: d.de.id, haciaColaboradorId: d.hacia.id, fechaInicio: d.fechaInicio, fechaFin: d.fechaFin, tipos: [...d.tipos], de: d.de, hacia: d.hacia }; modal.value = 'delegacion' }
async function guardarDelegacion() {
  const d = delEdit.value
  const cuerpo = { deColaboradorId: d.deColaboradorId, haciaColaboradorId: d.haciaColaboradorId, fechaInicio: d.fechaInicio, fechaFin: d.fechaFin, tipos: d.tipos }
  const r = await ejecutar(() => d.id ? api.put(`/aprobaciones/delegaciones/${d.id}`, cuerpo) : api.post('/aprobaciones/delegaciones', cuerpo), 'Delegación guardada')
  if (r) { modal.value = ''; await cargarDelegaciones() }
}
async function terminar(d: any) {
  menuDel.value = null
  if (!(await ui.confirmar({ titulo: 'Terminar delegación', mensaje: 'Los pendientes regresarán al titular.', aceptar: 'Terminar', peligro: true })).ok) return
  await ejecutar(async () => { await api.del(`/aprobaciones/delegaciones/${d.id}`); await cargarDelegaciones() }, 'Delegación terminada')
}
async function probarRuta() {
  if (!prueba.value.colaboradorId) return
  const m = new Date(); m.setDate(m.getDate() + 14)
  const r = await ejecutar(() => api.post('/solicitudes/simular', { tipo: prueba.value.tipo, colaboradorId: prueba.value.colaboradorId, fechaInicio: iso(m), fechaFin: iso(m), tipoPermisoId: undefined }))
  if (r) prueba.value.resultado = r
}
const tiposTexto = (t: string[]) => t.map(x => TIPOS.find(y => y.v === x)?.t.toLowerCase() ?? x).join(' y ').replace(/^./, c => c.toUpperCase())
</script>

<template>
  <div class="org">
    <div class="encabezado">
      <h1 class="titulo-pagina">Organización y aprobaciones</h1>
      <div class="fila acciones">
        <div class="sel-suc">
          <button class="btn btn-contorno alto" @click="selSuc = !selSuc"><Icono n="edificio" :t="22" />{{ s.sucursal?.nombre }}<Icono n="abajo" :t="18" /></button>
          <div v-if="selSuc" class="menu">
            <button v-for="x in s.me?.sucursales" :key="x.id" @click="s.elegirSucursal(x.id); selSuc = false">{{ x.nombre }}</button>
          </div>
        </div>
        <button class="btn alto" :class="editar ? 'btn-verde-contorno' : 'btn-contorno'" data-prueba="editar-estructura" @click="editar = !editar"><Icono :n="editar ? 'check' : 'lapiz'" :t="21" />{{ editar ? 'Terminar edición' : 'Editar estructura' }}</button>
        <RouterLink to="/colaboradores/nuevo" class="btn btn-primario alto"><Icono n="masCirculo" :t="22" />Agregar colaborador</RouterLink>
      </div>
    </div>

    <nav class="pestanas">
      <button class="pestana" :class="{ activa: tab === 'organigrama' }" @click="router.replace({ query: {} })">Organigrama</button>
      <button class="pestana" :class="{ activa: tab === 'flujos' }" @click="router.replace({ query: { tab: 'flujos' } })">Flujos de aprobación</button>
    </nav>

    <div v-if="tab === 'organigrama'" class="rejilla">
      <section class="tarjeta arbol-t">
        <div class="tarjeta-cabecera arriba">
          <div><h2 class="titulo-tarjeta">Organigrama de la empresa</h2><p class="subtitulo-tarjeta">Visualiza la estructura de tu organización y las líneas de reporte.</p></div>
          <div class="fila controles">
            <label class="zoom"><Icono n="buscar" :t="16" /><select v-model.number="zoom" aria-label="Zoom"><option v-for="z in [50, 75, 100, 125, 150]" :key="z" :value="z">{{ z }}%</option></select></label>
            <button class="btn btn-contorno btn-ch" @click="expandirTodo"><Icono :n="todoAbierto ? 'contraer' : 'expandir'" :t="16" />{{ todoAbierto ? 'Contraer' : 'Expandir todo' }}</button>
          </div>
        </div>
        <p v-if="editar" class="caja-info editando"><Icono n="info" :t="18" />Arrastra un colaborador sobre su nuevo jefe para cambiar la línea de reporte. Usa + para agregar a alguien a su cargo.</p>
        <div ref="lienzo" class="lienzo">
          <ul v-if="org" class="arbol" :style="{ transform: `scale(${zoom / 100})` }">
            <OrgNodo v-for="r in org.raices" :key="r.id" :n="r" :nivel="0" :abiertos="abiertos" :editar="editar" :arrastrando="arrastrando"
                     @alternar="alternar" @abrir="id => router.push(`/colaboradores/${id}`)" @soltar="soltar" @arrastrar="id => (arrastrando = id)"
                     @agregar="id => router.push(`/colaboradores/nuevo?supervisorId=${id}`)" />
          </ul>
        </div>
      </section>

      <div class="col-der">
        <section class="tarjeta">
          <h2 class="titulo-tarjeta">Reglas de aprobación</h2>
          <p class="subtitulo-tarjeta">Define quién debe aprobar cada tipo de solicitud.</p>
          <div class="reglas">
            <div v-for="r in reglas" :key="r.id" class="regla" :class="{ inactiva: !r.activa }">
              <Icono :n="(ICONOS[r.clave] ?? ['documento'])[0]" :t="28" :style="{ color: (ICONOS[r.clave] ?? ['', '#2f74d6'])[1] }" />
              <b class="nombre">{{ r.nombre }}</b>
              <div class="pasos">
                <template v-for="(p, k) in r.pasos" :key="p.orden"><Icono v-if="k > 0" n="flecha" :t="18" class="fl" /><span class="paso">{{ p.etiqueta }}</span></template>
              </div>
              <span class="etiqueta" :class="r.activa ? 'verde' : 'gris'"><span class="punto" />{{ r.activa ? 'Activa' : 'Inactiva' }}</span>
              <div class="menu-wrap">
                <button class="btn-icono borde" aria-label="Opciones de la regla" @click="menuRegla = menuRegla === r.id ? null : r.id"><Icono n="puntos" :t="18" /></button>
                <div v-if="menuRegla === r.id" class="menu derecha">
                  <button @click="editarRegla(r)"><Icono n="lapiz" :t="17" />Editar pasos</button>
                  <button @click="alternarActiva(r)"><Icono :n="r.activa ? 'x' : 'check'" :t="17" />{{ r.activa ? 'Desactivar' : 'Activar' }}</button>
                  <button @click="duplicar(r)"><Icono n="copiar" :t="17" />Duplicar</button>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section class="tarjeta">
          <div class="tarjeta-cabecera arriba">
            <div><h2 class="titulo-tarjeta">Delegaciones vigentes</h2><p class="subtitulo-tarjeta">Personas que tienen aprobaciones delegadas temporalmente.</p></div>
            <button class="btn btn-contorno btn-ch azul" @click="nuevaDelegacion"><Icono n="mas" :t="17" />Nueva delegación</button>
          </div>
          <div class="tabla-envoltura">
            <table class="tabla dele">
              <thead><tr><th>De</th><th>Hacia</th><th>Periodo</th><th>Tipo(s)</th><th /></tr></thead>
              <tbody>
                <tr v-for="d in delegaciones" :key="d.id">
                  <td><div class="fila p"><Persona :p="d.de" :t="36" /><Icono n="flecha" :t="16" class="muted" /></div></td>
                  <td><Persona :p="d.hacia" :t="36" /></td>
                  <td class="nw">{{ rango(d.fechaInicio, d.fechaFin, true) }}</td>
                  <td>{{ tiposTexto(d.tipos) }}</td>
                  <td>
                    <div class="menu-wrap">
                      <button class="btn-icono borde" aria-label="Opciones de la delegación" @click="menuDel = menuDel === d.id ? null : d.id"><Icono n="puntos" :t="18" /></button>
                      <div v-if="menuDel === d.id" class="menu derecha">
                        <button @click="editarDelegacion(d)"><Icono n="lapiz" :t="17" />Editar</button>
                        <button class="peligro" @click="terminar(d)"><Icono n="x" :t="17" />Terminar</button>
                      </div>
                    </div>
                  </td>
                </tr>
                <tr v-if="!delegaciones.length"><td colspan="5" class="vacio">No hay delegaciones vigentes.</td></tr>
              </tbody>
            </table>
          </div>
        </section>

        <section v-if="org?.sinSupervisor.length" class="alerta-est" data-prueba="alerta-sin-supervisor">
          <span class="ico"><Icono n="alerta" :t="30" /></span>
          <div class="crecer">
            <b>{{ org.sinSupervisor.length }} {{ org.sinSupervisor.length === 1 ? 'colaborador sin supervisor asignado' : 'colaboradores sin supervisor asignado' }}</b>
            <p>Estos colaboradores no tienen un jefe directo definido, por lo que no podrán ser parte de los flujos de aprobación.</p>
          </div>
          <button class="btn btn-peligro" @click="modal = 'sinsup'">Revisar</button>
        </section>
      </div>
    </div>

    <!-- Flujos de aprobación -->
    <div v-else class="flujos">
      <section v-for="r in reglas" :key="r.id" class="tarjeta flujo">
        <div class="fila-entre">
          <div class="fila"><Icono :n="(ICONOS[r.clave] ?? ['documento'])[0]" :t="28" :style="{ color: (ICONOS[r.clave] ?? ['', '#2f74d6'])[1] }" /><h2 class="titulo-tarjeta">{{ r.nombre }}</h2>
            <span class="etiqueta" :class="r.activa ? 'verde' : 'gris'"><span class="punto" />{{ r.activa ? 'Activa' : 'Inactiva' }}</span></div>
          <button class="btn btn-contorno btn-ch" @click="editarRegla(r)"><Icono n="lapiz" :t="16" />Editar</button>
        </div>
        <p class="muted pequeno" style="margin: 6px 0 14px">Aplica a: {{ tiposTexto(r.tipos) || 'ningún tipo (regla duplicada)' }}{{ r.soloSolicitantesConReportes ? ' · solo cuando quien solicita tiene reportes directos' : '' }}</p>
        <div class="diagrama">
          <span class="nodo-f inicio"><Icono n="enviar" :t="18" />Enviada</span>
          <template v-for="p in r.pasos" :key="p.orden"><Icono n="flecha" :t="20" class="muted" /><span class="nodo-f"><small>Paso {{ p.orden }}</small>{{ p.etiqueta }}</span></template>
          <Icono n="flecha" :t="20" class="muted" />
          <span class="nodo-f fin"><Icono n="checkCirculo" :t="18" />Aprobada</span>
        </div>
        <p class="ayuda" style="margin-top: 12px">Un rechazo en cualquier paso termina la solicitud. Las delegaciones vigentes redirigen el paso del titular al delegado.</p>
      </section>
      <section class="tarjeta">
        <h2 class="titulo-tarjeta">Probar una ruta</h2>
        <p class="subtitulo-tarjeta">Calcula con el organigrama y las reglas quién aprobaría una solicitud.</p>
        <div class="fila envolver" style="margin-top: 14px; align-items: flex-end">
          <div class="campo crecer" style="min-width: 260px"><label>Colaborador</label><SelectorColaborador v-model="prueba.colaboradorId" :sucursal-id="s.sucursalId" /></div>
          <div class="campo"><label>Tipo</label><select v-model="prueba.tipo" class="entrada"><option value="Vacaciones">Vacaciones</option><option value="Permiso">Permiso</option></select></div>
          <button class="btn btn-primario" :disabled="!prueba.colaboradorId || ocupado" @click="probarRuta">Calcular ruta</button>
        </div>
        <div v-if="prueba.resultado" class="resultado">
          <p class="muted">Regla: <b>{{ prueba.resultado.regla ?? '—' }}</b></p>
          <div class="fila envolver">
            <template v-for="(p, k) in prueba.resultado.ruta" :key="p.orden">
              <Icono v-if="k > 0" n="flecha" :t="20" class="muted" />
              <div class="fila"><Avatar v-if="p.titular" :nombre="p.titular.nombre" :iniciales="p.titular.iniciales" :t="34" /><span><b>{{ p.titular?.nombre ?? 'Recursos Humanos' }}</b><br><small class="muted">{{ p.etiqueta }}{{ p.delegadoA ? ` · delegado a ${p.delegadoA.nombre}` : '' }}</small></span></div>
            </template>
          </div>
          <p v-for="v in prueba.resultado.validaciones.filter((x: any) => ['sin_supervisor', 'regla_inactiva', 'sin_regla', 'autoaprobacion'].includes(x.codigo))" :key="v.codigo" class="caja-aviso" style="margin-top: 10px">{{ v.mensaje }}</p>
        </div>
      </section>
    </div>

    <!-- Diálogos -->
    <Modal v-if="modal === 'regla' && reglaEdit" titulo="Editar regla de aprobación" @cerrar="modal = ''">
      <div class="columna">
        <div class="campo"><label>Nombre</label><input v-model="reglaEdit.nombre" class="entrada" /></div>
        <div class="campo">
          <label>Pasos en orden</label>
          <div v-for="(p, k) in reglaEdit.pasos" :key="k" class="fila">
            <span class="num-paso">{{ k + 1 }}</span>
            <select v-model="reglaEdit.pasos[k]" class="entrada"><option v-for="a in APROBADORES" :key="a.v" :value="a.v">{{ a.t }}</option></select>
            <button class="btn-icono" :disabled="reglaEdit.pasos.length === 1" aria-label="Quitar paso" @click="reglaEdit.pasos.splice(k, 1)"><Icono n="basura" :t="18" /></button>
          </div>
          <button v-if="reglaEdit.pasos.length < 4" class="btn btn-contorno btn-ch" style="align-self: flex-start" @click="reglaEdit.pasos.push('RecursosHumanos')"><Icono n="mas" :t="16" />Agregar paso</button>
        </div>
        <label class="casilla"><input v-model="reglaEdit.activa" type="checkbox" />Regla activa</label>
        <p v-if="!reglaEdit.activa" class="caja-aviso"><Icono n="alerta" :t="18" />Una regla inactiva deja sin ruta a ese tipo de solicitud.</p>
      </div>
      <template #pie><button class="btn btn-contorno" @click="modal = ''">Cancelar</button><button class="btn btn-primario" :disabled="ocupado" @click="guardarRegla">Guardar</button></template>
    </Modal>

    <Modal v-if="modal === 'delegacion' && delEdit" :titulo="delEdit.id ? 'Editar delegación' : 'Nueva delegación'" subtitulo="Durante el periodo, los pendientes del delegante llegan al delegado." @cerrar="modal = ''">
      <div class="columna">
        <div class="campo"><label>De (quien delega)</label><SelectorColaborador v-model="delEdit.deColaboradorId" :inicial="delEdit.de" /></div>
        <div class="campo"><label>Hacia (delegado)</label><SelectorColaborador v-model="delEdit.haciaColaboradorId" :inicial="delEdit.hacia" :excluir="delEdit.deColaboradorId ? [delEdit.deColaboradorId] : []" /></div>
        <div class="rejilla-form">
          <div class="campo"><label>Desde</label><input v-model="delEdit.fechaInicio" type="date" class="entrada" /></div>
          <div class="campo"><label>Hasta</label><input v-model="delEdit.fechaFin" type="date" class="entrada" :min="delEdit.fechaInicio" /></div>
        </div>
        <div class="campo"><label>Tipo(s)</label>
          <div class="fila envolver"><label v-for="t in TIPOS" :key="t.v" class="casilla"><input v-model="delEdit.tipos" type="checkbox" :value="t.v" />{{ t.t }}</label></div>
        </div>
      </div>
      <template #pie><button class="btn btn-contorno" @click="modal = ''">Cancelar</button><button class="btn btn-primario" :disabled="ocupado || !delEdit.deColaboradorId || !delEdit.haciaColaboradorId || !delEdit.tipos.length" @click="guardarDelegacion">Guardar</button></template>
    </Modal>

    <Modal v-if="modal === 'sinsup'" titulo="Colaboradores sin supervisor" subtitulo="Asigna un jefe directo para que puedan enviar solicitudes." :ancho="640" @cerrar="modal = ''; router.replace({ query: {} })">
      <div v-for="c in org?.sinSupervisor" :key="c.id" class="sin">
        <Persona :p="c" :t="38" enlace />
        <div class="crecer"><SelectorColaborador v-model="asignar[c.id]" placeholder="Elegir supervisor…" :excluir="[c.id]" :sucursal-id="s.sucursalId" /></div>
        <button class="btn btn-primario btn-ch" :disabled="!asignar[c.id] || ocupado" @click="asignarSupervisor(c.id)">Asignar</button>
      </div>
      <p v-if="!org?.sinSupervisor.length" class="vacio">Todos los colaboradores tienen supervisor.</p>
    </Modal>
  </div>
</template>

<style scoped>
.org { display: flex; flex-direction: column; gap: 18px; }
.encabezado { display: flex; justify-content: space-between; align-items: center; gap: 16px; }
@media (max-width: 1400px) { .encabezado { flex-wrap: wrap; } }
.encabezado .titulo-pagina { font-size: 38px; white-space: nowrap; }
.acciones { gap: 12px; flex-shrink: 0; }
.alto { height: 46px; font-size: 16px; padding: 0 16px; }
.sel-suc, .menu-wrap { position: relative; }
.menu { position: absolute; top: calc(100% + 6px); left: 0; background: #fff; border: 1px solid var(--gris-200); border-radius: 12px; box-shadow: var(--sombra-m); padding: 6px; min-width: 200px; z-index: 40; }
.menu.derecha { left: auto; right: 0; }
.menu button { display: flex; gap: 10px; align-items: center; width: 100%; padding: 9px 12px; border: 0; background: none; border-radius: 8px; text-align: left; font-size: 14.5px; color: var(--titulo); }
.menu button:hover { background: var(--gris-100); }
.menu button.peligro { color: var(--rojo-texto); }
.rejilla { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.02fr); gap: 20px; align-items: start; }
.arriba { align-items: flex-start; }
.controles { gap: 10px; }
.zoom { display: flex; align-items: center; gap: 6px; height: 34px; border: 1px solid var(--gris-300); border-radius: 8px; padding: 0 8px; font-size: 13.5px; }
.zoom select { border: 0; background: none; font-size: 13.5px; }
.editando { margin-bottom: 12px; font-size: 13.5px; }
.lienzo { overflow: auto; padding: 8px 0 18px; min-height: 420px; }
.arbol { padding: 0; margin: 0; display: flex; justify-content: center; transform-origin: top center; transition: transform .15s; width: max-content; min-width: 100%; }
.arbol > :deep(li) { padding-top: 0; }
.arbol > :deep(li::before), .arbol > :deep(li::after) { display: none; }
.arbol > :deep(li > .nodo::before) { display: none; }
.col-der { display: flex; flex-direction: column; gap: 20px; }
.reglas { display: flex; flex-direction: column; gap: 10px; margin-top: 16px; }
.regla { display: grid; grid-template-columns: 30px 96px 1fr auto auto; align-items: center; gap: 10px; border: 1px solid var(--gris-200); border-radius: 10px; padding: 12px 12px 12px 14px; }
.regla.inactiva { background: var(--gris-50); }
.regla .nombre { font-size: 14.5px; color: var(--titulo); line-height: 1.2; }
.pasos { display: flex; align-items: center; gap: 6px; flex-wrap: nowrap; }
.paso { background: var(--gris-100); border-radius: 6px; padding: 8px 10px; font-size: 13.5px; color: var(--titulo); white-space: nowrap; }
.fl { color: var(--texto-2); }
.regla .etiqueta { height: 30px; padding: 0 12px; font-size: 13.5px; }
@media (max-width: 1500px) { .pasos { flex-wrap: wrap; } }
.azul { color: var(--azul); border-color: var(--gris-300); }
.dele td { padding: 10px 8px; }
.dele .p { gap: 6px; }
.nw { white-space: nowrap; }
.alerta-est { display: flex; align-items: center; gap: 18px; background: #fdeef0; border: 1px solid #f7c9cf; border-radius: 12px; padding: 20px 22px; }
.alerta-est .ico { width: 58px; height: 58px; border-radius: 50%; background: #fcd9dd; color: #e1262c; display: grid; place-items: center; flex-shrink: 0; }
.alerta-est b { font-size: 18px; color: var(--titulo); }
.alerta-est p { font-size: 14.5px; color: var(--texto-2); margin-top: 4px; }
.alerta-est .btn { height: 44px; padding: 0 26px; font-size: 16px; }
.flujos { display: grid; grid-template-columns: repeat(auto-fill, minmax(520px, 1fr)); gap: 20px; }
.flujo h2 { margin: 0 6px; }
.diagrama { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.nodo-f { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2px; padding: 10px 14px; border-radius: 10px; background: var(--gris-100); font-size: 14px; color: var(--titulo); font-weight: 600; min-height: 54px; }
.nodo-f small { font-weight: 500; color: var(--texto-3); font-size: 11.5px; }
.nodo-f.inicio { background: var(--azul-claro); color: var(--azul); flex-direction: row; gap: 6px; }
.nodo-f.fin { background: var(--verde-claro); color: var(--verde-texto); flex-direction: row; gap: 6px; }
.resultado { margin-top: 16px; display: flex; flex-direction: column; gap: 10px; }
.num-paso { width: 28px; height: 28px; border-radius: 50%; background: var(--verde-claro); color: var(--verde-texto); display: grid; place-items: center; font-weight: 700; flex-shrink: 0; }
.sin { display: flex; align-items: center; gap: 12px; padding: 12px 0; border-bottom: 1px solid var(--gris-100); flex-wrap: wrap; }
@media (max-width: 1300px) { .rejilla { grid-template-columns: 1fr; } }
@media (max-width: 760px) { .regla { grid-template-columns: 30px 1fr auto; } .regla .pasos { grid-column: 1 / -1; order: 5; } .flujos { grid-template-columns: 1fr; } .encabezado .titulo-pagina { font-size: 28px; } }
</style>
