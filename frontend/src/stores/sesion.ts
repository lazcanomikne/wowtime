import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { api, tokens } from '@/api'

export interface Me {
  id: number; email: string; nombre: string; roles: string[]; rolesTexto: string[]; permisos: string[]
  sucursales: { id: number; nombre: string; zonaHoraria: string }[]; debeCambiarPassword: boolean
  colaborador: null | {
    id: number; numero: string; nombre: string; nombres: string; apellidos: string; puesto?: string; area?: string; sucursalId: number; sucursal?: string
    iniciales: string; fotoUrl?: string | null; email?: string; telefono?: string; supervisor?: any; sinSupervisor: boolean; fechaNacimiento?: string; fechaIngreso?: string
  }
  saldo: null | { disponibles: number; porVencer: number; fechaLimiteProxima?: string; diasProximoVencimiento: number; fechaProximoVencimiento?: string; comprometidos: number }
  tieneReportes: boolean
  horaServidor: string
}

const CLAVE_SUC = 'wt.sucursal'

export const useSesion = defineStore('sesion', () => {
  const me = ref<Me | null>(null)
  const cargando = ref(false)
  const sucursalId = ref<number | null>(leerSucursal())
  const marca = ref<Record<string, string>>({})

  function leerSucursal() { try { const v = localStorage.getItem(CLAVE_SUC); return v ? Number(v) : null } catch { return null } }

  const autenticado = computed(() => !!me.value)
  const permisos = computed(() => new Set(me.value?.permisos ?? []))
  const tiene = (...p: string[]) => p.some(x => permisos.value.has(x))
  /** Portal de gestión (RH/Administrador) o autoservicio (colaborador, supervisor, gerente, recepción). */
  const esGestion = computed(() => tiene('dashboard.ver'))
  const esRecepcion = computed(() => (me.value?.roles ?? []).includes('Recepcion') && !esGestion.value)
  const sucursal = computed(() => me.value?.sucursales.find(s => s.id === sucursalId.value) ?? me.value?.sucursales[0] ?? null)
  const rolTexto = computed(() => {
    const r = me.value?.roles ?? []
    if (r.includes('Administrador')) return 'Administrador'
    if (r.includes('RecursosHumanos')) return 'Recursos Humanos'
    if (r.includes('Gerente')) return 'Gerente'
    if (r.includes('Supervisor')) return 'Supervisor'
    if (r.includes('Recepcion')) return 'Recepción'
    return me.value?.colaborador?.puesto ?? 'Colaborador'
  })

  async function cargar() {
    if (!tokens.leer()) { me.value = null; return }
    cargando.value = true
    try {
      me.value = await api.get<Me>('/me')
      if (!me.value.sucursales.some(s => s.id === sucursalId.value)) elegirSucursal(me.value.colaborador?.sucursalId && me.value.sucursales.some(s => s.id === me.value!.colaborador!.sucursalId) ? me.value.colaborador.sucursalId : me.value.sucursales[0]?.id ?? null)
    } finally { cargando.value = false }
  }

  async function cargarMarca() {
    try { marca.value = await api.get('/marca') } catch { /* sin conexión: se usan los textos guardados */ }
  }
  const frase = (ubicacion: string) => marca.value[ubicacion] ?? ''

  function elegirSucursal(id: number | null) {
    sucursalId.value = id
    try { id ? localStorage.setItem(CLAVE_SUC, String(id)) : localStorage.removeItem(CLAVE_SUC) } catch { /* sin almacenamiento */ }
  }

  async function entrar(email: string, password: string) {
    const s = await api.post('/auth/login', { email, password })
    tokens.guardar({ acceso: s.tokenAcceso, renovacion: s.tokenRenovacion, expira: s.expira })
    await cargar()
    return s.usuario
  }

  async function salir() {
    const t = tokens.leer()
    try { if (t?.renovacion) await api.post('/auth/logout', { tokenRenovacion: t.renovacion }) } catch { /* sin conexión */ }
    tokens.guardar(null)
    me.value = null
  }

  return { me, cargando, sucursalId, sucursal, marca, autenticado, permisos, tiene, esGestion, esRecepcion, rolTexto, cargar, cargarMarca, frase, elegirSucursal, entrar, salir }
})
