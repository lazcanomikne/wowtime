using System.Security.Cryptography;
using System.Text;
using WowTime.Api.Domain;

namespace WowTime.Api.Services;

/// <summary>Firma enlaces temporales a archivos para usarlos en etiquetas &lt;img&gt; y descargas.</summary>
public static class Enlaces
{
    public static string Secreto { get; set; } = "cambia-esto";
    public static Func<DateTime> Ahora { get; set; } = () => DateTime.UtcNow;

    public static string? Archivo(int? id, int minutos = 120)
    {
        if (id is not int a) return null;
        // Redondea la expiración a bloques para que el navegador pueda cachear la imagen
        var bloque = (long)Math.Ceiling((Ahora().AddMinutes(minutos) - DateTime.UnixEpoch).TotalSeconds / 3600) * 3600;
        return $"/api/v1/archivos/{a}/contenido?exp={bloque}&sig={Firma(a, bloque)}";
    }

    public static string Firma(int id, long exp)
    {
        var h = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secreto), Encoding.UTF8.GetBytes($"{id}:{exp}"));
        return Convert.ToHexString(h, 0, 12).ToLowerInvariant();
    }

    public static bool Valida(int id, long exp, string? sig)
    {
        if (sig is null) return false;
        if (DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime < Ahora()) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Firma(id, exp)), Encoding.UTF8.GetBytes(sig));
    }
}

public record PersonaMini(int Id, string Numero, string Nombre, string? Puesto, string? Area, string Iniciales, string? FotoUrl);

public static class Mapas
{
    public static string Iniciales(string nombres, string apellidos)
    {
        var a = nombres.Trim().FirstOrDefault();
        var b = apellidos.Trim().FirstOrDefault();
        return $"{a}{b}".ToUpperInvariant();
    }

    public static PersonaMini Persona(Colaborador c) => new(c.Id, c.Numero, c.NombreCompleto, c.Puesto?.Nombre, c.Area?.Nombre,
        Iniciales(c.Nombres, c.Apellidos), Enlaces.Archivo(c.FotoArchivoId));

    public static PersonaMini? PersonaN(Colaborador? c) => c is null ? null : Persona(c);

    public static string NombreTipoSolicitud(TipoSolicitud t) => t switch
    {
        TipoSolicitud.Vacaciones => "Vacaciones",
        TipoSolicitud.Permiso => "Permiso",
        TipoSolicitud.IncidenciaAsistencia => "Incidencia de asistencia",
        TipoSolicitud.CanjeDias => "Canje de días",
        _ => t.ToString()
    };

    public static string NombreAprobador(TipoAprobador t) => t switch
    {
        TipoAprobador.SupervisorDirecto => "Supervisor directo",
        TipoAprobador.Gerente => "Gerente",
        TipoAprobador.RecursosHumanos => "Recursos Humanos",
        _ => t.ToString()
    };

    public static string NombreModalidad(Modalidad m) => m switch
    {
        Modalidad.Presencial => "Presencial",
        Modalidad.Hibrido => "Híbrido",
        Modalidad.HomeOffice => "Home office",
        _ => m.ToString()
    };

    public static string NombreMetodo(MetodoRegistro m) => m switch
    {
        MetodoRegistro.Terminal => "Solo terminal",
        MetodoRegistro.App => "Solo app móvil",
        MetodoRegistro.TerminalYApp => "Terminal + app móvil",
        MetodoRegistro.Ninguno => "No registra asistencia",
        _ => m.ToString()
    };

    public static string NombreEstadoRegistro(EstadoRegistro e) => e switch
    {
        EstadoRegistro.EnHorario => "En horario",
        EstadoRegistro.Retardo => "Retardo",
        EstadoRegistro.SalidaRegistrada => "Salida registrada",
        EstadoRegistro.SalidaAnticipada => "Salida anticipada",
        _ => e.ToString()
    };

    public static string NombreIncidencia(TipoIncidencia t) => t switch
    {
        TipoIncidencia.Retardo => "Retardo",
        TipoIncidencia.Falta => "Falta",
        TipoIncidencia.SalidaOmitida => "Salida omitida",
        TipoIncidencia.FueraDeArea => "Registro fuera de área",
        _ => t.ToString()
    };

    public static string NombreAsignacion(TipoAsignacion t) => t switch
    {
        TipoAsignacion.HomeOffice => "Home office",
        TipoAsignacion.OtraSucursal => "Otra sucursal",
        TipoAsignacion.Comision => "Comisión",
        _ => t.ToString()
    };

    public static readonly string[] DiasSemana = ["", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo"];

    public static string ListaDias(int[] dias)
    {
        var n = dias.OrderBy(d => d).Select(d => DiasSemana[d]).ToList();
        if (n.Count == 0) return "Ninguno";
        if (n.Count == 1) return Capital(n[0]);
        return Capital(string.Join(", ", n.Take(n.Count - 1)) + " y " + n[^1]);
    }

    public static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
}
