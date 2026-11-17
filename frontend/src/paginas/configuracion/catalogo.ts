// Catálogo de secciones de Configuración: tarjetas de /configuracion y encabezado de /configuracion/:seccion.
export interface TarjetaConfig {
  id: string
  /** Segmento de la ruta /configuracion/<seccion>. */
  seccion: string
  titulo: string
  descripcion: string
  icono: string
  /** Se necesita al menos uno. */
  permisos: string[]
  query?: Record<string, string>
}

export interface GrupoConfig { titulo: string; tarjetas: TarjetaConfig[] }

export const GRUPOS: GrupoConfig[] = [
  {
    titulo: 'Organización',
    tarjetas: [
      { id: 'organizacion', seccion: 'organizacion', titulo: 'Organización y aprobaciones', descripcion: 'Organigrama, reglas de aprobación y delegaciones.', icono: 'organigrama', permisos: ['organizacion.gestionar', 'aprobaciones.configurar'] },
      { id: 'sucursales', seccion: 'sucursales', titulo: 'Sucursales', descripcion: 'Dirección, geocerca y zona horaria de cada sucursal.', icono: 'edificio', permisos: ['catalogos.gestionar'] },
      { id: 'areas', seccion: 'areas', titulo: 'Áreas', descripcion: 'Áreas de trabajo de cada sucursal.', icono: 'edificioDoble', permisos: ['catalogos.gestionar'] },
      { id: 'equipos', seccion: 'equipos', titulo: 'Equipos', descripcion: 'Equipos por área y su cobertura mínima.', icono: 'personas', permisos: ['catalogos.gestionar'] },
      { id: 'puestos', seccion: 'puestos', titulo: 'Puestos y documentos requeridos', descripcion: 'Nivel en el organigrama y documentos que pide cada puesto.', icono: 'maletin', permisos: ['catalogos.gestionar'] },
      { id: 'turnos', seccion: 'turnos', titulo: 'Turnos', descripcion: 'Horario, tolerancia de entrada y días de descanso.', icono: 'reloj', permisos: ['catalogos.gestionar'] },
    ]
  },
  {
    titulo: 'Asistencia',
    tarjetas: [
      { id: 'dispositivos', seccion: 'dispositivos', titulo: 'Dispositivos y terminales', descripcion: 'Terminales, lectores de QR y quioscos con su llave de acceso.', icono: 'terminal', permisos: ['catalogos.gestionar'] },
      { id: 'parametros-asistencia', seccion: 'parametros', query: { grupo: 'Asistencia' }, titulo: 'Parámetros de asistencia', descripcion: 'Tolerancia, radio de geocerca y hora de cierre de la jornada.', icono: 'asistencia', permisos: ['catalogos.gestionar'] },
    ]
  },
  {
    titulo: 'Vacaciones',
    tarjetas: [
      { id: 'politicas', seccion: 'politicas', titulo: 'Políticas de vacaciones', descripcion: 'Días por antigüedad, vencimiento y anticipación mínima.', icono: 'vacaciones', permisos: ['vacaciones.gestionar'] },
      { id: 'festivos', seccion: 'festivos', titulo: 'Días festivos', descripcion: 'Calendario de festivos por año y por sucursal.', icono: 'calendario', permisos: ['catalogos.gestionar'] },
      { id: 'tipos-permiso', seccion: 'tipos-permiso', titulo: 'Tipos de permiso', descripcion: 'Permisos con o sin goce de sueldo y su máximo de días.', icono: 'documento', permisos: ['catalogos.gestionar'] },
    ]
  },
  {
    titulo: 'Expedientes',
    tarjetas: [
      { id: 'tipos-documento', seccion: 'tipos-documento', titulo: 'Tipos de documento', descripcion: 'Documentos del expediente, vigencia y obligatoriedad.', icono: 'expedientes', permisos: ['catalogos.gestionar'] },
    ]
  },
  {
    titulo: 'Marca',
    tarjetas: [
      { id: 'mensajes', seccion: 'mensajes', titulo: 'Mensajes de marca', descripcion: 'Frases de menús, encabezados, banners y cumpleaños.', icono: 'destellos', permisos: ['catalogos.gestionar'] },
    ]
  },
  {
    titulo: 'Administración',
    tarjetas: [
      { id: 'usuarios', seccion: 'usuarios', titulo: 'Usuarios', descripcion: 'Cuentas de acceso, roles y sucursales permitidas.', icono: 'persona', permisos: ['usuarios.gestionar'] },
      { id: 'roles', seccion: 'roles', titulo: 'Roles y permisos', descripcion: 'Qué puede hacer cada rol dentro del sistema.', icono: 'escudo', permisos: ['usuarios.gestionar'] },
      { id: 'llaves-api', seccion: 'llaves-api', titulo: 'Llaves de API', descripcion: 'Acceso para integraciones y sistemas externos.', icono: 'llave', permisos: ['llavesapi.gestionar'] },
      { id: 'parametros', seccion: 'parametros', titulo: 'Parámetros del sistema', descripcion: 'Umbrales, prefijos, ventanas de tiempo y URL pública.', icono: 'configuracion', permisos: ['sistema.configurar'] },
      { id: 'correos', seccion: 'correos', titulo: 'Correos enviados', descripcion: 'Bandeja de salida de los correos del sistema.', icono: 'correo', permisos: ['sistema.configurar'] },
      { id: 'tareas', seccion: 'tareas', titulo: 'Tareas programadas', descripcion: 'Procesos automáticos, su historial y ejecución manual.', icono: 'actualizar', permisos: ['sistema.configurar'] },
      { id: 'bitacora', seccion: 'bitacora', titulo: 'Bitácora', descripcion: 'Registro de cambios y eventos de auditoría.', icono: 'lista', permisos: ['sistema.configurar'] },
    ]
  },
]

/** Datos de cada sección para el encabezado y para validar permisos. */
export interface InfoSeccion { titulo: string; descripcion: string; icono: string; permisos: string[] }

export const SECCIONES: Record<string, InfoSeccion> = {
  sucursales: { titulo: 'Sucursales', descripcion: 'Administra las sucursales, su geocerca y su zona horaria.', icono: 'edificio', permisos: ['catalogos.gestionar'] },
  areas: { titulo: 'Áreas', descripcion: 'Áreas de trabajo de la sucursal seleccionada.', icono: 'edificioDoble', permisos: ['catalogos.gestionar'] },
  equipos: { titulo: 'Equipos', descripcion: 'Equipos de la sucursal seleccionada y su cobertura mínima.', icono: 'personas', permisos: ['catalogos.gestionar'] },
  puestos: { titulo: 'Puestos y documentos requeridos', descripcion: 'Nivel de cada puesto y los documentos que debe entregar.', icono: 'maletin', permisos: ['catalogos.gestionar'] },
  turnos: { titulo: 'Turnos', descripcion: 'Horarios de entrada y salida, tolerancia y días de descanso.', icono: 'reloj', permisos: ['catalogos.gestionar'] },
  festivos: { titulo: 'Días festivos', descripcion: 'Los días festivos no se descuentan de las vacaciones ni generan faltas.', icono: 'calendario', permisos: ['catalogos.gestionar'] },
  'tipos-permiso': { titulo: 'Tipos de permiso', descripcion: 'Tipos de permiso que pueden solicitar los colaboradores.', icono: 'documento', permisos: ['catalogos.gestionar'] },
  'tipos-documento': { titulo: 'Tipos de documento', descripcion: 'Documentos que forman el expediente de cada colaborador.', icono: 'expedientes', permisos: ['catalogos.gestionar'] },
  dispositivos: { titulo: 'Dispositivos y terminales', descripcion: 'Equipos que registran asistencia o leen QR de visitantes.', icono: 'terminal', permisos: ['catalogos.gestionar'] },
  mensajes: { titulo: 'Mensajes de marca', descripcion: 'Frases que aparecen en menús, encabezados, banners y felicitaciones.', icono: 'destellos', permisos: ['catalogos.gestionar'] },
  parametros: { titulo: 'Parámetros', descripcion: 'Valores que ajustan las reglas del sistema.', icono: 'configuracion', permisos: ['catalogos.gestionar', 'sistema.configurar'] },
  politicas: { titulo: 'Políticas de vacaciones', descripcion: 'Días por antigüedad, vencimiento y alertas. La ley marca el mínimo.', icono: 'vacaciones', permisos: ['vacaciones.gestionar'] },
  usuarios: { titulo: 'Usuarios', descripcion: 'Cuentas de acceso, roles y sucursales de cada usuario.', icono: 'persona', permisos: ['usuarios.gestionar'] },
  roles: { titulo: 'Roles y permisos', descripcion: 'Marca los permisos de cada rol y guarda los cambios por rol.', icono: 'escudo', permisos: ['usuarios.gestionar'] },
  'llaves-api': { titulo: 'Llaves de API', descripcion: 'Llaves para que otros sistemas consulten o registren información.', icono: 'llave', permisos: ['llavesapi.gestionar'] },
  correos: { titulo: 'Correos enviados', descripcion: 'Bandeja de salida: correos enviados, pendientes y con error.', icono: 'correo', permisos: ['sistema.configurar'] },
  tareas: { titulo: 'Tareas programadas', descripcion: 'Procesos que corren cada hora. También puedes ejecutarlos ahora.', icono: 'actualizar', permisos: ['sistema.configurar', 'catalogos.gestionar'] },
  bitacora: { titulo: 'Bitácora', descripcion: 'Eventos de auditoría: cambios de datos, seguridad, solicitudes y más.', icono: 'lista', permisos: ['sistema.configurar', 'colaboradores.ver'] },
}
