// Formatos de fecha y número en español de México. Las fechas "solo día" vienen como "2026-09-15".
export const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre']
export const MESES_CORTOS = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic']
export const DIAS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado']
/** Encabezados del calendario único (semana inicia en lunes). */
export const DIAS_CAL = ['L', 'M', 'X', 'J', 'V', 'S', 'D']

export function aFecha(v?: string | Date | null): Date | null {
  if (!v) return null
  if (v instanceof Date) return v
  if (/^\d{4}-\d{2}-\d{2}$/.test(v)) { const [a, m, d] = v.split('-').map(Number); return new Date(a, m - 1, d) }
  return new Date(v)
}
export function iso(d: Date) { return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` }
export const cap = (s: string) => (s ? s[0].toUpperCase() + s.slice(1) : s)

/** "Martes, 8 de septiembre" */
export function fechaLarga(v?: string | Date | null, conAnio = false) {
  const d = aFecha(v); if (!d) return ''
  return `${cap(DIAS[d.getDay()])}, ${d.getDate()} de ${MESES[d.getMonth()]}${conAnio ? ` de ${d.getFullYear()}` : ''}`
}
/** "8 de septiembre" o "8 de septiembre de 2026" */
export function fechaMedia(v?: string | Date | null, conAnio = false) {
  const d = aFecha(v); if (!d) return ''
  return `${d.getDate()} de ${MESES[d.getMonth()]}${conAnio ? ` de ${d.getFullYear()}` : ''}`
}
/** "15 sep 2026" */
export function fechaCorta(v?: string | Date | null, conAnio = true) {
  const d = aFecha(v); if (!d) return ''
  return `${d.getDate()} ${MESES_CORTOS[d.getMonth()]}${conAnio ? ` ${d.getFullYear()}` : ''}`
}
/** "15/09/2026" */
export function fechaNum(v?: string | Date | null) {
  const d = aFecha(v); if (!d) return ''
  return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`
}
/** "15 – 19 sep" o "28 sep – 2 oct 2026" */
export function rango(a?: string | null, b?: string | null, conAnio = false) {
  const x = aFecha(a), y = aFecha(b); if (!x || !y) return ''
  const anio = conAnio ? ` ${y.getFullYear()}` : ''
  if (x.getTime() === y.getTime()) return `${x.getDate()} ${MESES_CORTOS[x.getMonth()]}${anio}`
  if (x.getMonth() === y.getMonth() && x.getFullYear() === y.getFullYear()) return `${x.getDate()} – ${y.getDate()} ${MESES_CORTOS[x.getMonth()]}${anio}`
  return `${x.getDate()} ${MESES_CORTOS[x.getMonth()]} – ${y.getDate()} ${MESES_CORTOS[y.getMonth()]}${anio}`
}
/** "10–12 junio" */
export function rangoLargo(a?: string | null, b?: string | null) {
  const x = aFecha(a), y = aFecha(b); if (!x || !y) return ''
  if (x.getMonth() === y.getMonth()) return x.getDate() === y.getDate() ? `${x.getDate()} ${MESES[x.getMonth()]}` : `${x.getDate()}–${y.getDate()} ${MESES[x.getMonth()]}`
  return `${x.getDate()} ${MESES[x.getMonth()]} – ${y.getDate()} ${MESES[y.getMonth()]}`
}
/** "07:02" a partir de "07:02:00" o de una fecha */
export function hora(v?: string | Date | null) {
  if (!v) return ''
  if (typeof v === 'string' && /^\d{2}:\d{2}/.test(v)) return v.slice(0, 5)
  const d = aFecha(v); if (!d) return ''
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}
/** "Hoy, 10:24", "Ayer, 16:31" o "3 sep 2026, 11:15" */
export function momento(v?: string | null) {
  const d = aFecha(v); if (!d) return ''
  const hoy = new Date(); const ayer = new Date(); ayer.setDate(hoy.getDate() - 1)
  const h = hora(d)
  if (d.toDateString() === hoy.toDateString()) return `Hoy, ${h}`
  if (d.toDateString() === ayer.toDateString()) return `Ayer, ${h}`
  return `${fechaCorta(d)}, ${h}`
}
export function hace(v?: string | null) {
  const d = aFecha(v); if (!d) return ''
  const s = Math.round((Date.now() - d.getTime()) / 1000)
  if (s < 60) return 'hace un momento'
  if (s < 3600) return `hace ${Math.round(s / 60)} min`
  if (s < 86400) return `hace ${Math.round(s / 3600)} h`
  if (s < 86400 * 7) return `hace ${Math.round(s / 86400)} d`
  return fechaCorta(d)
}
export function num(v?: number | null, dec = 0) {
  if (v === null || v === undefined) return '—'
  return Number(v).toLocaleString('es-MX', { maximumFractionDigits: dec, minimumFractionDigits: 0 })
}
export function dias(v?: number | null) { const n = Number(v ?? 0); return `${num(n, 1)} ${Math.abs(n) === 1 ? 'día' : 'días'}` }
export function saludo(d = new Date()) { const h = d.getHours(); return h < 12 ? 'Buenos días' : h < 19 ? 'Buenas tardes' : 'Buenas noches' }
export function primerNombre(n?: string | null) { return (n ?? '').split(' ')[0] }
export function nombreCorto(n?: string | null) {
  const p = (n ?? '').split(' '); return p.length > 2 ? `${p[0]} ${p[1]}` : n ?? ''
}
export function mesAnio(d: Date) { return `${cap(MESES[d.getMonth()])} ${d.getFullYear()}` }
export function bytes(n: number) { return n < 1024 * 1024 ? `${Math.round(n / 1024)} KB` : `${(n / 1024 / 1024).toFixed(1)} MB` }
