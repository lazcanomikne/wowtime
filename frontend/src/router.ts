import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useSesion } from '@/stores/sesion'
import { tokens } from '@/api'

const paginas = import.meta.glob('./paginas/**/*.vue')
const p = (f: string) => {
  return () => {
    const c = paginas[`./paginas/${f}.vue`]
    return c ? (c() as Promise<any>) : Promise.reject(new Error(`Falta la página ${f}`))
  }
}

/** meta.permisos: se necesita al menos uno. meta.publica: sin sesión. meta.movil: pantalla interna móvil (flecha y título). */
const rutas: RouteRecordRaw[] = [
  { path: '/login', component: p('publico/Login'), meta: { publica: true, titulo: 'Iniciar sesión' } },
  { path: '/recuperar', component: p('publico/Recuperar'), meta: { publica: true, titulo: 'Recuperar contraseña' } },
  { path: '/restablecer', component: p('publico/Restablecer'), meta: { publica: true, titulo: 'Nueva contraseña' } },
  { path: '/qr/:codigo', component: p('publico/QrPublico'), meta: { publica: true, titulo: 'Invitación' } },
  { path: '/quiosco', component: p('publico/Quiosco'), meta: { publica: true, titulo: 'Quiosco de asistencia' } },
  {
    path: '/', component: () => import('./layout/Marco.vue'),
    children: [
      { path: '', component: p('Inicio'), meta: { titulo: 'Inicio', menu: 'inicio' } },
      // Colaboradores
      { path: 'colaboradores', component: p('colaboradores/Lista'), meta: { titulo: 'Colaboradores', menu: 'colaboradores', permisos: ['colaboradores.ver', 'equipo.ver'] } },
      { path: 'colaboradores/nuevo', component: p('colaboradores/Formulario'), meta: { titulo: 'Agregar colaborador', menu: 'colaboradores', permisos: ['colaboradores.editar'] } },
      { path: 'colaboradores/:id/editar', component: p('colaboradores/Formulario'), meta: { titulo: 'Editar colaborador', menu: 'colaboradores', permisos: ['colaboradores.editar'] } },
      { path: 'colaboradores/:id', component: p('colaboradores/Ficha'), meta: { titulo: 'Ficha de colaborador', menu: 'colaboradores', permisos: ['colaboradores.ver', 'equipo.ver'] } },
      // Asistencia
      { path: 'asistencia', component: p('asistencia/Asistencia'), meta: { titulo: 'Asistencia', menu: 'asistencia', permisos: ['asistencia.gestionar'] } },
      { path: 'mi-asistencia', component: p('autoservicio/MiAsistencia'), meta: { titulo: 'Mi asistencia', menu: 'mi-asistencia', permisos: ['asistencia.registrar'], movil: true } },
      { path: 'm/asistencia', component: p('movil/RegistrarAsistencia'), meta: { titulo: 'Registrar asistencia', menu: 'mi-asistencia', permisos: ['asistencia.registrar'], movil: true, tab: 'asistencia' } },
      // Vacaciones y solicitudes
      { path: 'vacaciones', component: p('vacaciones/Vacaciones'), meta: { titulo: 'Vacaciones', menu: 'vacaciones', permisos: ['vacaciones.gestionar'] } },
      { path: 'solicitudes/:id', component: p('vacaciones/DetalleSolicitud'), meta: { titulo: 'Detalle de solicitud', menu: 'vacaciones', movil: true, tab: 'solicitudes' } },
      { path: 'mis-vacaciones', component: p('autoservicio/MisVacaciones'), meta: { titulo: 'Solicitar vacaciones', menu: 'mis-vacaciones', permisos: ['solicitudes.crear'], movil: true, tab: 'solicitudes' } },
      { path: 'm/vacaciones', component: p('movil/SolicitarVacacionesMovil'), meta: { titulo: 'Solicitar vacaciones', menu: 'mis-vacaciones', permisos: ['solicitudes.crear'], movil: true, tab: 'solicitudes' } },
      { path: 'mis-solicitudes', component: p('autoservicio/MisSolicitudes'), meta: { titulo: 'Solicitudes', menu: 'mis-vacaciones', permisos: ['solicitudes.crear'], movil: true, tab: 'solicitudes' } },
      { path: 'mis-solicitudes/:id', redirect: to => `/solicitudes/${to.params.id}` },
      { path: 'solicitar-permiso', component: p('autoservicio/SolicitarPermiso'), meta: { titulo: 'Solicitar permiso', menu: 'mis-vacaciones', permisos: ['solicitudes.crear'], movil: true, tab: 'solicitudes' } },
      { path: 'aprobaciones', component: p('aprobaciones/Bandeja'), meta: { titulo: 'Aprobaciones', menu: 'aprobaciones', permisos: ['aprobaciones.decidir'], movil: true, tab: 'solicitudes' } },
      { path: 'mi-equipo', component: p('autoservicio/MiEquipo'), meta: { titulo: 'Mi equipo', menu: 'mi-equipo', permisos: ['equipo.ver'], movil: true } },
      // Visitantes
      { path: 'visitantes', component: p('visitantes/Visitantes'), meta: { titulo: 'Visitantes', menu: 'visitantes', permisos: ['visitantes.ver'] } },
      { path: 'visitantes/:id', component: p('visitantes/DetalleVisitante'), meta: { titulo: 'Detalle de visitante', menu: 'visitantes', movil: true } },
      { path: 'control-acceso', component: p('visitantes/ControlAcceso'), meta: { titulo: 'Control de acceso', menu: 'control-acceso', permisos: ['visitantes.acceso'] } },
      { path: 'm/invitado', component: p('movil/RegistrarInvitado'), meta: { titulo: 'Registrar invitado', menu: 'mis-invitados', permisos: ['visitantes.registrar'], movil: true, tab: 'inicio' } },
      { path: 'mis-invitados', component: p('autoservicio/MisInvitados'), meta: { titulo: 'Mis invitados', menu: 'mis-invitados', permisos: ['visitantes.registrar'], movil: true } },
      // Expedientes
      { path: 'expedientes', component: p('expedientes/Expedientes'), meta: { titulo: 'Expedientes', menu: 'expedientes', permisos: ['expedientes.gestionar'] } },
      { path: 'mi-expediente', component: p('autoservicio/MiExpediente'), meta: { titulo: 'Mi expediente', menu: 'mi-expediente', permisos: ['expediente.propio'], movil: true } },
      // Reportes
      { path: 'reportes', component: p('reportes/Reportes'), meta: { titulo: 'Reportes', menu: 'reportes', permisos: ['reportes.sucursal', 'reportes.equipo'] } },
      // Configuración
      { path: 'configuracion', component: p('configuracion/Configuracion'), meta: { titulo: 'Configuración', menu: 'configuracion', permisos: ['organizacion.gestionar', 'catalogos.gestionar', 'aprobaciones.configurar', 'usuarios.gestionar', 'llavesapi.gestionar', 'sistema.configurar'] } },
      { path: 'configuracion/organizacion', component: p('configuracion/Organizacion'), meta: { titulo: 'Organización y aprobaciones', menu: 'configuracion', permisos: ['organizacion.gestionar', 'aprobaciones.configurar'] } },
      { path: 'configuracion/:seccion', component: p('configuracion/Seccion'), meta: { titulo: 'Configuración', menu: 'configuracion', permisos: ['catalogos.gestionar', 'usuarios.gestionar', 'llavesapi.gestionar', 'sistema.configurar', 'vacaciones.gestionar', 'aprobaciones.configurar'] } },
      // Marco
      { path: 'perfil', component: p('Perfil'), meta: { titulo: 'Perfil', menu: 'perfil', movil: true, tab: 'perfil' } },
      { path: 'notificaciones', component: p('Notificaciones'), meta: { titulo: 'Notificaciones', menu: 'notificaciones', movil: true } },
      { path: 'buscar', component: p('Buscar'), meta: { titulo: 'Búsqueda', menu: 'buscar' } },
    ]
  },
  { path: '/:pathMatch(.*)*', component: p('NoEncontrado'), meta: { publica: true, titulo: 'No encontrado' } }
]

const router = createRouter({ history: createWebHistory(), routes: rutas, scrollBehavior: (_a, _b, guardada) => guardada ?? { top: 0 } })

router.beforeEach(async to => {
  const s = useSesion()
  if (to.meta.publica) return true
  if (!tokens.leer()) return { path: '/login', query: to.fullPath !== '/' ? { r: to.fullPath } : undefined }
  if (!s.me) { try { await s.cargar() } catch { return { path: '/login' } } }
  if (!s.me) return { path: '/login' }
  const req = to.meta.permisos as string[] | undefined
  if (req && !s.tiene(...req)) return { path: '/' }
  return true
})
router.afterEach(to => { document.title = to.meta.titulo ? `${to.meta.titulo} · WOW! Time` : 'WOW! Time' })

export default router
