namespace WowTime.Api.Domain;

public enum EstatusColaborador { Activo, Inactivo, Baja }
public enum Modalidad { Presencial, Hibrido, HomeOffice }
/// <summary>Ninguno: no registra asistencia (no se le generan faltas).</summary>
public enum MetodoRegistro { Terminal, App, TerminalYApp, Ninguno }
public enum TipoAsignacion { HomeOffice, OtraSucursal, Comision }
public enum TipoUbicacion { Domicilio, Otro }
public enum TipoRegistro { Entrada, Salida }
public enum OrigenRegistro { App, Terminal, Kiosco, Rh }
public enum EstadoRegistro { EnHorario, Retardo, SalidaRegistrada, SalidaAnticipada }
public enum TipoIncidencia { Retardo, Falta, SalidaOmitida, FueraDeArea }
public enum EstadoIncidencia { PorRevisar, EnRevision, Justificada, Confirmada }
public enum TipoSolicitud { Vacaciones, Permiso, IncidenciaAsistencia, CanjeDias }
public enum EstadoSolicitud { Pendiente, Aprobada, Rechazada, Cancelada }
public enum TipoAprobador { SupervisorDirecto, Gerente, RecursosHumanos }
public enum EstadoPaso { EnEspera, Pendiente, Aprobado, Rechazado, Omitido }
public enum EstadoInvitacion { Esperado, Dentro, Salio, Vencida, Cancelada }
public enum TipoDispositivo { Terminal, LectorQr, Kiosco }
public enum EstadoDocumento { Vigente, Proximo, Urgente, Vencido, Reemplazado }
public enum TipoMovimientoSaldo { Otorgamiento, Compromiso, Liberacion, Consumo, Vencimiento, Ajuste }

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string RecursosHumanos = "RecursosHumanos";
    public const string Gerente = "Gerente";
    public const string Supervisor = "Supervisor";
    public const string Colaborador = "Colaborador";
    public const string Recepcion = "Recepcion";
    public static readonly string[] Todos = [Administrador, RecursosHumanos, Gerente, Supervisor, Colaborador, Recepcion];
    public static string Nombre(string clave) => clave switch
    {
        Administrador => "Administrador",
        RecursosHumanos => "Recursos Humanos",
        Gerente => "Gerente",
        Supervisor => "Supervisor",
        Colaborador => "Colaborador",
        Recepcion => "Recepción",
        _ => clave
    };
}
