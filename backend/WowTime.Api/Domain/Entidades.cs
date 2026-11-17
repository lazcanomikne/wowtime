namespace WowTime.Api.Domain;

/// <summary>Campos de auditoría comunes.</summary>
public abstract class Auditable
{
    public DateTime CreadoEn { get; set; }
    public int? CreadoPor { get; set; }
    public DateTime ModificadoEn { get; set; }
    public int? ModificadoPor { get; set; }
}

// ───────────── Organización ─────────────

public class Empresa : Auditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string ZonaHoraria { get; set; } = "America/Monterrey";
    public string PrefijoNumero { get; set; } = "WC-";
}

public class Sucursal : Auditable
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Direccion { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public int RadioMetros { get; set; } = 150;
    public string ZonaHoraria { get; set; } = "America/Monterrey";
    public bool Activa { get; set; } = true;
}

public class Area : Auditable
{
    public int Id { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activa { get; set; } = true;
}

public class Equipo : Auditable
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public Area? Area { get; set; }
    public string Nombre { get; set; } = "";
    /// <summary>Porcentaje mínimo de miembros no ausentes por día.</summary>
    public int CoberturaMinima { get; set; } = 70;
    public bool Activo { get; set; } = true;
}

public class Puesto : Auditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int Nivel { get; set; } = 5;
    public bool Activo { get; set; } = true;
    public List<PuestoDocumento> DocumentosRequeridos { get; set; } = [];
}

public class PuestoDocumento
{
    public int PuestoId { get; set; }
    public int TipoDocumentoId { get; set; }
    public TipoDocumento? TipoDocumento { get; set; }
}

public class Turno : Auditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public TimeOnly HoraEntrada { get; set; } = new(8, 0);
    public TimeOnly HoraSalida { get; set; } = new(17, 0);
    public int ToleranciaMinutos { get; set; } = 10;
    /// <summary>Días de descanso en ISO: 1 = lunes … 7 = domingo.</summary>
    public int[] DiasDescanso { get; set; } = [6, 7];
    public bool Activo { get; set; } = true;
}

public class DiaFestivo : Auditable
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public string Nombre { get; set; } = "";
    /// <summary>Nulo = aplica a todas las sucursales.</summary>
    public int? SucursalId { get; set; }
}

// ───────────── Personas y acceso ─────────────

public class Colaborador : Auditable
{
    public int Id { get; set; }
    public string Numero { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public string? Genero { get; set; }
    public string? Curp { get; set; }
    public string? Rfc { get; set; }
    public string? Nss { get; set; }
    public string? Direccion { get; set; }
    public string? ContactoEmergenciaNombre { get; set; }
    public string? ContactoEmergenciaTelefono { get; set; }
    public int? FotoArchivoId { get; set; }
    public EstatusColaborador Estatus { get; set; } = EstatusColaborador.Activo;
    public DateOnly? FechaBaja { get; set; }
    public string? MotivoBaja { get; set; }
    public int SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }
    public int? AreaId { get; set; }
    public Area? Area { get; set; }
    public int? EquipoId { get; set; }
    public Equipo? Equipo { get; set; }
    public int? PuestoId { get; set; }
    public Puesto? Puesto { get; set; }
    public int? TurnoId { get; set; }
    public Turno? Turno { get; set; }
    public int? SupervisorId { get; set; }
    public Colaborador? Supervisor { get; set; }
    public int? DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }
    public string? PinHash { get; set; }
    public int? PoliticaVacacionesId { get; set; }
    public EsquemaTrabajo? Esquema { get; set; }
    /// <summary>Identificador en el sistema de origen (EmployeeID de InovaOS). Nulo si nació en WOW! Time.</summary>
    public int? OrigenId { get; set; }
    /// <summary>Huella de la fila de origen en la última migración; si no cambia, la migración no pisa lo editado aquí.</summary>
    public string? OrigenFirma { get; set; }
}

public class Usuario : Auditable
{
    public int Id { get; set; }
    public int? ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }
    public string Email { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool Activo { get; set; } = true;
    public DateTime? UltimoAcceso { get; set; }
    public bool DebeCambiarPassword { get; set; }
    public string[] Roles { get; set; } = [];
    /// <summary>Sucursales permitidas. Vacío + permiso sucursales.todas = todas.</summary>
    public int[] Sucursales { get; set; } = [];
}

public class RolPermiso
{
    public string Rol { get; set; } = "";
    public string Permiso { get; set; } = "";
}

public class TokenRenovacion
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime Expira { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? RevocadoEn { get; set; }
}

public class TokenRecuperacion
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime Expira { get; set; }
    public DateTime? UsadoEn { get; set; }
}

public class LlaveApi : Auditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Prefijo { get; set; } = "";
    public string Hash { get; set; } = "";
    public string[] Permisos { get; set; } = [];
    public int[] Sucursales { get; set; } = [];
    public DateTime? UltimoUso { get; set; }
    public DateTime? RevocadaEn { get; set; }
    public DateTime? Expira { get; set; }
}

public class EsquemaTrabajo : Auditable
{
    public int ColaboradorId { get; set; }
    public Modalidad Modalidad { get; set; } = Modalidad.Presencial;
    public int? CentroBaseSucursalId { get; set; }
    public int[] DiasOficina { get; set; } = [1, 2, 3, 4, 5];
    public int[] DiasRemotos { get; set; } = [];
    public MetodoRegistro RegistroPermitido { get; set; } = MetodoRegistro.TerminalYApp;
    public bool RequiereFoto { get; set; }
}

public class UbicacionAutorizada : Auditable
{
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public TipoUbicacion Tipo { get; set; } = TipoUbicacion.Domicilio;
    public string Nombre { get; set; } = "";
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public int RadioMetros { get; set; } = 150;
    public bool Activa { get; set; } = true;
}

public class AsignacionTemporal : Auditable
{
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }
    public TipoAsignacion Tipo { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string Motivo { get; set; } = "";
    public int? SucursalId { get; set; }
    public int? UbicacionAutorizadaId { get; set; }
    public bool Cancelada { get; set; }
    /// <summary>Estado calculado por la tarea programada: Programada, Vigente, Concluida o Cancelada.</summary>
    public string Estado { get; set; } = "Programada";
}

public class Nota : Auditable
{
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public int AutorUsuarioId { get; set; }
    public Usuario? Autor { get; set; }
    public string Texto { get; set; } = "";
}

public class EventoBitacora
{
    public long Id { get; set; }
    public int? ColaboradorId { get; set; }
    public string Tipo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string? Detalle { get; set; }
    public DateTime Fecha { get; set; }
    public int? AutorUsuarioId { get; set; }
    public string? AutorNombre { get; set; }
    /// <summary>Canal por el que llegó la operación (por ejemplo "InovaOS"). Nulo = WOW! Time.</summary>
    public string? Canal { get; set; }
    public string? Entidad { get; set; }
    public int? EntidadId { get; set; }
}

// ───────────── Asistencia ─────────────

public class Dispositivo : Auditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public TipoDispositivo Tipo { get; set; }
    public int SucursalId { get; set; }
    public bool Activo { get; set; } = true;
    public string? LlavePrefijo { get; set; }
    public string? LlaveHash { get; set; }
    public DateTime? UltimaConexion { get; set; }
}

public class RegistroAsistencia
{
    public long Id { get; set; }
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }
    public int SucursalId { get; set; }
    public TipoRegistro Tipo { get; set; }
    public DateTime FechaHora { get; set; }
    public DateOnly FechaLocal { get; set; }
    public TimeOnly HoraLocal { get; set; }
    public OrigenRegistro Origen { get; set; }
    public int? DispositivoId { get; set; }
    public Dispositivo? Dispositivo { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public double? Precision { get; set; }
    public bool? DentroGeocerca { get; set; }
    public double? DistanciaMetros { get; set; }
    public string? LugarAutorizado { get; set; }
    public int? FotoArchivoId { get; set; }
    public string? Justificacion { get; set; }
    public EstadoRegistro Estado { get; set; }
    public Modalidad Modalidad { get; set; }
    public int? RegistradoPorUsuarioId { get; set; }
    /// <summary>Identificador del evento en el lector de origen (EventoId de wotime_inova). Evita importar dos veces.</summary>
    public long? OrigenId { get; set; }
}

public class IncidenciaAsistencia : Auditable
{
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }
    public int SucursalId { get; set; }
    public DateOnly Fecha { get; set; }
    public TipoIncidencia Tipo { get; set; }
    public long? RegistroId { get; set; }
    public string? Detalle { get; set; }
    public string? Justificacion { get; set; }
    public EstadoIncidencia Estado { get; set; } = EstadoIncidencia.PorRevisar;
    public int? ResueltaPorUsuarioId { get; set; }
    public DateTime? ResueltaEn { get; set; }
    public string? ComentarioResolucion { get; set; }
    public int? SolicitudId { get; set; }
}

// ───────────── Ausencias y aprobaciones ─────────────

public class PoliticaVacaciones : Auditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int PlazoVencimientoMeses { get; set; } = 12;
    public int AnticipacionMinimaDias { get; set; } = 0;
    public int DiasAlertaVencimiento { get; set; } = 30;
    public bool PorDefecto { get; set; }
    public List<PoliticaAntiguedad> Tabla { get; set; } = [];
}

public class PoliticaAntiguedad
{
    public int Id { get; set; }
    public int PoliticaVacacionesId { get; set; }
    /// <summary>Años cumplidos desde (inclusive).</summary>
    public int AniosDesde { get; set; }
    public int AniosHasta { get; set; }
    public int Dias { get; set; }
}

public class SaldoVacaciones : Auditable
{
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }
    public int Periodo { get; set; }
    public DateOnly FechaOtorgamiento { get; set; }
    public DateOnly FechaLimite { get; set; }
    public decimal DiasOtorgados { get; set; }
    public decimal DiasUsados { get; set; }
    public decimal DiasComprometidos { get; set; }
    public decimal DiasVencidos { get; set; }
    public decimal Disponibles => DiasOtorgados - DiasUsados - DiasComprometidos - DiasVencidos;
    /// <summary>PeriodoID de InovaOS cuando el saldo viene de la migración.</summary>
    public int? OrigenId { get; set; }
    public string? OrigenFirma { get; set; }
    public string? Notas { get; set; }
}

public class MovimientoSaldo
{
    public long Id { get; set; }
    public int SaldoVacacionesId { get; set; }
    public int ColaboradorId { get; set; }
    public TipoMovimientoSaldo Tipo { get; set; }
    public decimal Dias { get; set; }
    public int? SolicitudId { get; set; }
    public DateTime Fecha { get; set; }
    public int? UsuarioId { get; set; }
    public string? Motivo { get; set; }
}

public class TipoPermiso : Auditable
{
    public int Id { get; set; }
    /// <summary>Clave fija para integraciones (PERMIT_3H, PERMIT_5H, PERMIT_DAY). Nula en los tipos creados por RH.</summary>
    public string? Clave { get; set; }
    public string Nombre { get; set; } = "";
    public bool ConGoce { get; set; } = true;
    public int? MaximoDias { get; set; }
    /// <summary>Si tiene valor, el permiso vale siempre esos días y se descuenta del saldo de vacaciones.</summary>
    public decimal? DiasDescuento { get; set; }
    /// <summary>Permiso por horas: no cubre la jornada completa, solo perdona el retardo o la salida anticipada.</summary>
    public bool Parcial { get; set; }
    public bool Activo { get; set; } = true;
}

public class ReglaAprobacion : Auditable
{
    public int Id { get; set; }
    /// <summary>vacaciones, permisos, incidencias, solicitud_supervisor</summary>
    public string Clave { get; set; } = "";
    public string Nombre { get; set; } = "";
    public TipoSolicitud[] Tipos { get; set; } = [];
    public bool SoloSolicitantesConReportes { get; set; }
    public bool Activa { get; set; } = true;
    public List<PasoRegla> Pasos { get; set; } = [];
}

public class PasoRegla
{
    public int Id { get; set; }
    public int ReglaAprobacionId { get; set; }
    public int Orden { get; set; }
    public TipoAprobador TipoAprobador { get; set; }
}

public class Solicitud : Auditable
{
    public int Id { get; set; }
    public string Folio { get; set; } = "";
    public TipoSolicitud Tipo { get; set; }
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }
    public int SucursalId { get; set; }
    public int? TipoPermisoId { get; set; }
    public TipoPermiso? TipoPermiso { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public decimal DiasHabiles { get; set; }
    public int? CoberturaColaboradorId { get; set; }
    public Colaborador? CoberturaColaborador { get; set; }
    public string? CoberturaNota { get; set; }
    public string? Comentario { get; set; }
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
    public int PasoActual { get; set; } = 1;
    public int? ReglaAprobacionId { get; set; }
    public string? ReglaNombre { get; set; }
    public int CapturadaPorUsuarioId { get; set; }
    public int? IncidenciaId { get; set; }
    public string? MotivoRechazo { get; set; }
    public DateTime? ResueltaEn { get; set; }
    public List<PasoAprobacion> Pasos { get; set; } = [];
    /// <summary>Días que resta del saldo de vacaciones. Nulo = regla anterior (vacaciones: días hábiles; lo demás: 0).</summary>
    public decimal? Descuento { get; set; }
    /// <summary>Canal por el que se capturó (por ejemplo "InovaOS"). Nulo = WOW! Time.</summary>
    public string? Canal { get; set; }
    /// <summary>RequestID de InovaOS cuando la solicitud viene de la migración.</summary>
    public int? OrigenId { get; set; }
    public string? OrigenFirma { get; set; }

    public decimal DiasADescontar => Descuento ?? (Tipo == TipoSolicitud.Vacaciones ? DiasHabiles : 0);
    /// <summary>Ocupa días del calendario (el canje de días no).</summary>
    public bool OcupaCalendario => Tipo is TipoSolicitud.Vacaciones or TipoSolicitud.Permiso;
}

public class PasoAprobacion
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }
    public Solicitud? Solicitud { get; set; }
    public int Orden { get; set; }
    public TipoAprobador TipoAprobador { get; set; }
    /// <summary>Colaborador titular del paso (supervisor o gerente). Nulo cuando el paso es del grupo de RH.</summary>
    public int? TitularColaboradorId { get; set; }
    public Colaborador? TitularColaborador { get; set; }
    /// <summary>Usuarios excluidos de decidir este paso (separación de funciones).</summary>
    public int[] UsuariosExcluidos { get; set; } = [];
    /// <summary>Si el paso de RH quedó asignado a un usuario concreto.</summary>
    public int? UsuarioAsignadoId { get; set; }
    public EstadoPaso Estado { get; set; } = EstadoPaso.EnEspera;
    public int? DecididoPorUsuarioId { get; set; }
    public string? DecididoPorNombre { get; set; }
    public int? EnNombreDeColaboradorId { get; set; }
    public string? EnNombreDeNombre { get; set; }
    public string? Motivo { get; set; }
    public DateTime? Fecha { get; set; }
    public string? NotaBitacora { get; set; }
}

public class Delegacion : Auditable
{
    public int Id { get; set; }
    public int DeColaboradorId { get; set; }
    public Colaborador? De { get; set; }
    public int HaciaColaboradorId { get; set; }
    public Colaborador? Hacia { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public TipoSolicitud[] Tipos { get; set; } = [];
    public DateTime? TerminadaEn { get; set; }
}

public class Incapacidad : Auditable
{
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string? Folio { get; set; }
    public string? Tipo { get; set; }
    public int? DocumentoId { get; set; }
    public string? Nota { get; set; }
    public bool Cancelada { get; set; }
}

// ───────────── Visitantes ─────────────

public class InvitacionVisitante : Auditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string EmpresaMotivo { get; set; } = "";
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public DateTime FechaLlegada { get; set; }
    public DateOnly FechaLocal { get; set; }
    public int AnfitrionColaboradorId { get; set; }
    public Colaborador? Anfitrion { get; set; }
    public int SucursalId { get; set; }
    public string Codigo { get; set; } = "";
    public DateTime VigenciaDesde { get; set; }
    public DateTime VigenciaHasta { get; set; }
    public EstadoInvitacion Estado { get; set; } = EstadoInvitacion.Esperado;
    public AccesoVisitante? Acceso { get; set; }
}

public class AccesoVisitante
{
    public int Id { get; set; }
    public int InvitacionId { get; set; }
    public DateTime Entrada { get; set; }
    public DateTime? Salida { get; set; }
    public int? DispositivoId { get; set; }
    public int? RegistradoPorUsuarioId { get; set; }
    public int? SalidaPorUsuarioId { get; set; }
}

// ───────────── Expediente y sistema ─────────────

public class TipoDocumento : Auditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Obligatorio { get; set; } = true;
    public bool ConVigencia { get; set; }
    public bool Activo { get; set; } = true;
}

public class Documento : Auditable
{
    public int Id { get; set; }
    public int ColaboradorId { get; set; }
    public Colaborador? Colaborador { get; set; }
    public int TipoDocumentoId { get; set; }
    public TipoDocumento? TipoDocumento { get; set; }
    public int ArchivoId { get; set; }
    public Archivo? Archivo { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public bool Reemplazado { get; set; }
    public string? Notas { get; set; }
}

public class Archivo
{
    public int Id { get; set; }
    public string NombreOriginal { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Tamano { get; set; }
    public string Ruta { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int? ColaboradorId { get; set; }
    public DateTime CreadoEn { get; set; }
    public int? SubidoPorUsuarioId { get; set; }
}

public class Notificacion
{
    public long Id { get; set; }
    public int UsuarioId { get; set; }
    public string Tipo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string? Mensaje { get; set; }
    public string? Enlace { get; set; }
    public bool Leida { get; set; }
    public DateTime CreadaEn { get; set; }
    /// <summary>Para que las tareas programadas no dupliquen avisos.</summary>
    public string? ClaveUnica { get; set; }
}

public class MensajeMarca : Auditable
{
    public int Id { get; set; }
    public string Ubicacion { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Texto { get; set; } = "";
    public DateOnly? VigenciaInicio { get; set; }
    public DateOnly? VigenciaFin { get; set; }
    public bool Activo { get; set; } = true;
}

public class Parametro
{
    public string Clave { get; set; } = "";
    public string Valor { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Tipo { get; set; } = "entero";
    public string Grupo { get; set; } = "";
}

public class CorreoSaliente
{
    public long Id { get; set; }
    public string Para { get; set; } = "";
    public string Asunto { get; set; } = "";
    public string Cuerpo { get; set; } = "";
    public DateTime CreadoEn { get; set; }
    public DateTime? EnviadoEn { get; set; }
    public string? Error { get; set; }
}

public class EjecucionTarea
{
    public int Id { get; set; }
    public string Tarea { get; set; } = "";
    public DateOnly Fecha { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime? Fin { get; set; }
    public string? Resultado { get; set; }
}
