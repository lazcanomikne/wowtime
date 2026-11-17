<script setup lang="ts">
// Matriz de permisos por rol: casillas editables y guardado por rol (PUT /roles/:rol).
import { computed, reactive } from 'vue'
import { api } from '@/api'
import Icono from '@/componentes/Icono.vue'
import { useSesion } from '@/stores/sesion'
import { useUi } from '@/stores/ui'
import { useCarga } from '@/utils/usos'
import { mensajeError } from './comun'

interface Rol { clave: string; nombre: string; permisos: string[] }
interface Permiso { clave: string; nombre: string; grupo: string }

const s = useSesion()
const ui = useUi()
const sel = reactive<Record<string, Set<string>>>({})
const guardando = reactive<Record<string, boolean>>({})
const { datos, cargando, cargar } = useCarga(async () => {
  const r = await api.get<{ roles: Rol[]; permisos: Permiso[] }>('/roles')
  for (const x of r.roles) sel[x.clave] = new Set(x.permisos)
  return r
})
const grupos = computed(() => {
  const m = new Map<string, Permiso[]>()
  for (const p of datos.value?.permisos ?? []) { if (!m.has(p.grupo)) m.set(p.grupo, []); m.get(p.grupo)!.push(p) }
  return [...m.entries()].map(([nombre, permisos]) => ({ nombre, permisos }))
})
function cambiado(r: Rol) {
  const a = sel[r.clave]
  return !!a && (a.size !== r.permisos.length || r.permisos.some(p => !a.has(p)))
}
const pendientes = computed(() => (datos.value?.roles ?? []).filter(cambiado))
function alternar(rol: string, permiso: string) {
  const a = new Set(sel[rol])
  if (a.has(permiso)) a.delete(permiso); else a.add(permiso)
  sel[rol] = a
}
function deshacer(r: Rol) { sel[r.clave] = new Set(r.permisos) }
async function guardar(r: Rol) {
  guardando[r.clave] = true
  try {
    const permisos = [...(sel[r.clave] ?? [])].sort()
    await api.put(`/roles/${encodeURIComponent(r.clave)}`, { permisos })
    r.permisos = permisos
    ui.avisar(`Permisos de ${r.nombre} guardados. Aplican cuando cada usuario renueve su sesión (máximo 15 minutos).`)
    if (s.me?.roles.includes(r.clave)) s.cargar()
  } catch (e) {
    ui.avisar(mensajeError(e), 'error', 6000)
  } finally { guardando[r.clave] = false }
}
async function guardarTodos() { for (const r of pendientes.value) await guardar(r) }
</script>

<template>
  <section class="tarjeta">
    <div class="tarjeta-cabecera">
      <div>
        <h2 class="titulo-tarjeta">Permisos por rol</h2>
        <p class="subtitulo-tarjeta">Un usuario con varios roles tiene la suma de sus permisos.</p>
      </div>
      <button v-if="pendientes.length > 1" class="btn btn-primario" @click="guardarTodos"><Icono n="check" :t="18" />Guardar {{ pendientes.length }} roles</button>
    </div>
    <div v-if="cargando && !datos" class="esqueleto" style="height: 200px" />
    <div v-if="datos" class="tabla-envoltura">
      <table class="tabla matriz">
        <thead>
          <tr>
            <th class="permiso">Permiso</th>
            <th v-for="r in datos.roles" :key="r.clave" class="rol">
              <div class="cab-rol">
                <span>{{ r.nombre }}</span>
                <small class="muted">{{ sel[r.clave]?.size ?? 0 }} permisos</small>
              </div>
            </th>
          </tr>
        </thead>
        <tbody>
          <template v-for="g in grupos" :key="g.nombre">
            <tr class="grupo"><td :colspan="datos.roles.length + 1">{{ g.nombre }}</td></tr>
            <tr v-for="p in g.permisos" :key="p.clave">
              <td class="permiso"><div class="negrita">{{ p.nombre }}</div><code class="clave">{{ p.clave }}</code></td>
              <td v-for="r in datos.roles" :key="r.clave" class="celda">
                <label class="casilla">
                  <input type="checkbox" :checked="sel[r.clave]?.has(p.clave)" :aria-label="`${r.nombre}: ${p.nombre}`"
                    :disabled="r.clave === 'Administrador' && p.clave === 'usuarios.gestionar'" @change="alternar(r.clave, p.clave)" />
                </label>
              </td>
            </tr>
          </template>
        </tbody>
        <tfoot>
          <tr>
            <td class="permiso muted pequeno">Los cambios se guardan por rol.</td>
            <td v-for="r in datos.roles" :key="r.clave" class="celda">
              <div class="pie-rol">
                <button class="btn btn-primario btn-ch" :disabled="!cambiado(r) || guardando[r.clave]" :data-prueba="`guardar-rol-${r.clave}`" @click="guardar(r)">Guardar</button>
                <button v-if="cambiado(r)" class="ver-todo" @click="deshacer(r)">Deshacer</button>
              </div>
            </td>
          </tr>
        </tfoot>
      </table>
    </div>
  </section>
</template>

<style scoped>
.matriz th.rol, .matriz td.celda { text-align: center; min-width: 118px; }
.cab-rol { display: flex; flex-direction: column; align-items: center; gap: 2px; color: var(--titulo); font-weight: 650; }
.cab-rol small { font-weight: 400; font-size: 12px; }
.permiso { min-width: 260px; position: sticky; left: 0; background: #fff; z-index: 1; }
th.permiso { background: var(--gris-100); }
.clave { font-size: 12px; color: var(--texto-3); }
.grupo td { background: var(--gris-50); font-weight: 700; color: var(--texto-2); font-size: 13px; text-transform: uppercase; letter-spacing: .04em; padding: 8px 12px; }
.celda .casilla { justify-content: center; }
tfoot td { border-top: 1px solid var(--gris-200); padding-top: 14px; vertical-align: top; }
.pie-rol { display: flex; flex-direction: column; align-items: center; gap: 6px; }
</style>
