// Textos y utilidades compartidas del módulo Colaboradores (lista, formulario y pestañas de la ficha).
import { ErrorApi } from '@/api'

export const MODALIDADES = [
  { v: 'Presencial', t: 'Presencial', icono: 'edificio' },
  { v: 'Hibrido', t: 'Híbrido', icono: 'edificioDoble' },
  { v: 'HomeOffice', t: 'Home office', icono: 'casa' },
] as const

export const REGISTROS = [
  { v: 'Terminal', t: 'Solo terminal', icono: 'terminal' },
  { v: 'App', t: 'Solo app móvil', icono: 'celular' },
  { v: 'TerminalYApp', t: 'Terminal + app móvil', icono: 'monitor' },
  { v: 'Ninguno', t: 'No registra asistencia', icono: 'x' },
] as const

export const ESTATUS = [
  { v: 'Activo', t: 'Activo' },
  { v: 'Inactivo', t: 'Inactivo' },
  { v: 'Baja', t: 'Baja' },
] as const

export const TIPOS_ASIGNACION = [
  { v: 'HomeOffice', t: 'Home office', icono: 'casa' },
  { v: 'OtraSucursal', t: 'Otra sucursal', icono: 'edificio' },
  { v: 'Comision', t: 'Comisión', icono: 'maletin' },
] as const

/** Días ISO 1 (lunes) a 7 (domingo) con las letras del calendario único. */
export const DIAS_SEMANA = [
  { v: 1, l: 'L', t: 'Lunes' }, { v: 2, l: 'M', t: 'Martes' }, { v: 3, l: 'X', t: 'Miércoles' }, { v: 4, l: 'J', t: 'Jueves' },
  { v: 5, l: 'V', t: 'Viernes' }, { v: 6, l: 'S', t: 'Sábado' }, { v: 7, l: 'D', t: 'Domingo' },
]

export const GENEROS = ['Femenino', 'Masculino', 'No binario', 'Prefiero no decir']

export const textoModalidad = (v?: string | null) => MODALIDADES.find(m => m.v === v)?.t ?? v ?? '—'
export const textoRegistro = (v?: string | null) => REGISTROS.find(m => m.v === v)?.t ?? v ?? '—'
export const textoAsignacion = (v?: string | null) => TIPOS_ASIGNACION.find(m => m.v === v)?.t ?? v ?? '—'

/** "Lunes, miércoles y viernes" a partir de [1, 3, 5]. */
export function listaDias(d?: number[] | null) {
  const n = [...(d ?? [])].sort((a, b) => a - b).map(x => DIAS_SEMANA.find(y => y.v === x)?.t.toLowerCase() ?? '')
  if (!n.length) return 'Ninguno'
  const t = n.length === 1 ? n[0] : `${n.slice(0, -1).join(', ')} y ${n[n.length - 1]}`
  return t[0].toUpperCase() + t.slice(1)
}

/** Años y meses de antigüedad: "5 años, 2 meses". */
export function antiguedad(desde?: string | null) {
  if (!desde) return ''
  const [a, m, d] = desde.split('-').map(Number)
  const hoy = new Date()
  let meses = (hoy.getFullYear() - a) * 12 + (hoy.getMonth() + 1 - m)
  if (hoy.getDate() < d) meses--
  if (meses < 0) return 'Aún no ingresa'
  const anios = Math.floor(meses / 12), resto = meses % 12
  const pa = anios ? `${anios} ${anios === 1 ? 'año' : 'años'}` : ''
  const pm = resto ? `${resto} ${resto === 1 ? 'mes' : 'meses'}` : ''
  return [pa, pm].filter(Boolean).join(', ') || 'Menos de un mes'
}

/**
 * Convierte `ErrorApi.detalles` en { campo: mensaje }. Acepta el formato de negocio ({ campo: "mensaje" })
 * y el de validación del modelo ({ "$.esquema.diasOficina": ["mensaje"] }); usa el último segmento como campo.
 */
export function erroresPorCampo(e: unknown): Record<string, string> {
  const res: Record<string, string> = {}
  if (!(e instanceof ErrorApi) || !e.detalles || typeof e.detalles !== 'object' || Array.isArray(e.detalles)) return res
  for (const [k, v] of Object.entries(e.detalles as Record<string, unknown>)) {
    const seg = k.replace(/^\$\.?/, '').split('.').filter(Boolean).pop() ?? ''
    if (!seg) continue
    const campo = seg[0].toLowerCase() + seg.slice(1)
    const msg = Array.isArray(v) ? v.filter(Boolean).join(' ') : typeof v === 'string' ? v : ''
    if (msg) res[campo] = msg
  }
  return res
}
