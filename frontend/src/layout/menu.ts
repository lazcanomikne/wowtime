import { computed } from 'vue'
import { useSesion } from '@/stores/sesion'

export interface ItemMenu { clave: string; texto: string; icono: string; ruta: string }

/** Un solo menú: las opciones dependen de los permisos del usuario. */
export function useMenu() {
  const s = useSesion()
  return computed<ItemMenu[]>(() => {
    if (!s.me) return []
    if (s.esGestion) {
      const m: ItemMenu[] = [
        { clave: 'inicio', texto: 'Inicio', icono: 'inicio', ruta: '/' },
        { clave: 'colaboradores', texto: 'Colaboradores', icono: 'colaboradores', ruta: '/colaboradores' },
      ]
      if (s.tiene('asistencia.gestionar')) m.push({ clave: 'asistencia', texto: 'Asistencia', icono: 'asistencia', ruta: '/asistencia' })
      if (s.tiene('vacaciones.gestionar')) m.push({ clave: 'vacaciones', texto: 'Vacaciones', icono: 'vacaciones', ruta: '/vacaciones' })
      if (s.tiene('visitantes.ver')) m.push({ clave: 'visitantes', texto: 'Visitantes', icono: 'visitantes', ruta: '/visitantes' })
      if (s.tiene('expedientes.gestionar')) m.push({ clave: 'expedientes', texto: 'Expedientes', icono: 'expedientes', ruta: '/expedientes' })
      if (s.tiene('reportes.sucursal')) m.push({ clave: 'reportes', texto: 'Reportes', icono: 'reportes', ruta: '/reportes' })
      if (s.tiene('organizacion.gestionar', 'catalogos.gestionar', 'aprobaciones.configurar', 'usuarios.gestionar', 'llavesapi.gestionar', 'sistema.configurar'))
        m.push({ clave: 'configuracion', texto: 'Configuración', icono: 'configuracion', ruta: '/configuracion' })
      return m
    }
    const m: ItemMenu[] = [{ clave: 'inicio', texto: 'Inicio', icono: 'inicio', ruta: '/' }]
    if (s.esRecepcion) m.push({ clave: 'control-acceso', texto: 'Control de acceso', icono: 'escanear', ruta: '/control-acceso' })
    if (s.tiene('visitantes.ver') && s.esRecepcion) m.push({ clave: 'visitantes', texto: 'Visitantes', icono: 'visitantes', ruta: '/visitantes' })
    m.push(
      { clave: 'mi-asistencia', texto: 'Mi asistencia', icono: 'calendario', ruta: '/mi-asistencia' },
      { clave: 'mis-vacaciones', texto: 'Mis vacaciones', icono: 'vacaciones', ruta: '/mis-vacaciones' },
      { clave: 'mi-expediente', texto: 'Mi expediente', icono: 'expedientes', ruta: '/mi-expediente' },
    )
    if (s.tiene('aprobaciones.decidir')) m.push({ clave: 'aprobaciones', texto: 'Aprobaciones', icono: 'bandeja', ruta: '/aprobaciones' })
    if (s.tiene('equipo.ver')) m.push({ clave: 'mi-equipo', texto: 'Mi equipo', icono: 'colaboradores', ruta: '/mi-equipo' })
    if (s.tiene('reportes.equipo') && !s.tiene('reportes.sucursal')) m.push({ clave: 'reportes', texto: 'Reportes', icono: 'reportes', ruta: '/reportes' })
    return m
  })
}
