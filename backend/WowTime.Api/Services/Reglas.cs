using System.Security.Cryptography;
using System.Text;
using WowTime.Api.Domain;

namespace WowTime.Api.Services;

/// <summary>Reglas de negocio puras (sin base de datos). Cubiertas por pruebas automáticas.</summary>
public static class Reglas
{
    /// <summary>ISO: 1 = lunes … 7 = domingo.</summary>
    public static int DiaIso(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    /// <summary>Cuenta días hábiles del rango (inclusivo) excluyendo días de descanso del turno y festivos.</summary>
    public static int DiasHabiles(DateOnly inicio, DateOnly fin, IEnumerable<int> diasDescanso, IEnumerable<DateOnly> festivos)
    {
        if (fin < inicio) return 0;
        var descanso = diasDescanso.ToHashSet();
        var fest = festivos.ToHashSet();
        var n = 0;
        for (var d = inicio; d <= fin; d = d.AddDays(1))
            if (!descanso.Contains(DiaIso(d)) && !fest.Contains(d)) n++;
        return n;
    }

    public static bool Traslapa(DateOnly aIni, DateOnly aFin, DateOnly bIni, DateOnly bFin) => aIni <= bFin && bIni <= aFin;

    /// <summary>Estado de una entrada según turno y tolerancia.</summary>
    public static EstadoRegistro EstadoEntrada(TimeOnly hora, TimeOnly entradaTurno, int toleranciaMin)
        => hora <= entradaTurno.AddMinutes(toleranciaMin) ? EstadoRegistro.EnHorario : EstadoRegistro.Retardo;

    /// <summary>Distancia en metros entre dos coordenadas (Haversine).</summary>
    public static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        double Rad(double g) => g * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static bool DentroGeocerca(double lat, double lon, double cLat, double cLon, int radio, out double distancia)
    {
        distancia = DistanciaMetros(lat, lon, cLat, cLon);
        return distancia <= radio;
    }

    /// <summary>Vigencia de un QR de visitante alrededor de la hora de llegada.</summary>
    public static (DateTime desde, DateTime hasta) VigenciaQr(DateTime llegadaUtc, int minAntes, int minDespues)
        => (llegadaUtc.AddMinutes(-minAntes), llegadaUtc.AddMinutes(minDespues));

    public static bool QrVigente(DateTime ahoraUtc, DateTime desde, DateTime hasta) => ahoraUtc >= desde && ahoraUtc <= hasta;

    /// <summary>Estado de vigencia de un documento.</summary>
    public static EstadoDocumento EstadoDocumento(DateOnly? vencimiento, DateOnly hoy, int umbralUrgente, int umbralProximo)
    {
        if (vencimiento is not DateOnly v) return Domain.EstadoDocumento.Vigente;
        var dias = v.DayNumber - hoy.DayNumber;
        if (dias < 0) return Domain.EstadoDocumento.Vencido;
        if (dias <= umbralUrgente) return Domain.EstadoDocumento.Urgente;
        if (dias <= umbralProximo) return Domain.EstadoDocumento.Proximo;
        return Domain.EstadoDocumento.Vigente;
    }

    public static string EstadoAsignacion(DateOnly ini, DateOnly fin, DateOnly hoy, bool cancelada)
        => cancelada ? "Cancelada" : hoy < ini ? "Programada" : hoy > fin ? "Concluida" : "Vigente";

    /// <summary>Días de vacaciones según años cumplidos y la tabla de la política.</summary>
    public static int DiasPorAntiguedad(int anios, IEnumerable<PoliticaAntiguedad> tabla)
    {
        var fila = tabla.OrderBy(t => t.AniosDesde).LastOrDefault(t => anios >= t.AniosDesde && anios <= t.AniosHasta)
                   ?? tabla.OrderBy(t => t.AniosDesde).LastOrDefault(t => anios >= t.AniosDesde);
        return fila?.Dias ?? 0;
    }

    /// <summary>Firma de un código de QR para que solo el servidor pueda emitirlos.</summary>
    public static string FirmarQr(string codigo, string secreto)
    {
        var h = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes(codigo));
        return Convert.ToHexString(h, 0, 8).ToLowerInvariant();
    }

    public static string ContenidoQr(string codigo, string secreto) => $"WT1.{codigo}.{FirmarQr(codigo, secreto)}";

    /// <summary>Valida el contenido leído de un QR y devuelve el código, o null si la firma no corresponde.</summary>
    public static string? LeerQr(string contenido, string secreto)
    {
        contenido = contenido.Trim();
        // Admite la URL pública del QR o el contenido directo
        var idx = contenido.IndexOf("WT1.", StringComparison.Ordinal);
        if (idx < 0) return null;
        var partes = contenido[idx..].Split('.');
        if (partes.Length < 3) return null;
        var firma = new string(partes[2].TakeWhile(char.IsLetterOrDigit).ToArray());
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(FirmarQr(partes[1], secreto)), Encoding.UTF8.GetBytes(firma)) ? partes[1] : null;
    }

    public static decimal PorcentajeCobertura(int miembros, int ausentes) => miembros == 0 ? 100 : Math.Round((miembros - ausentes) * 100m / miembros, 0);
}
