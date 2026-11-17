using WowTime.Api.Domain;
using WowTime.Api.Services;

namespace WowTime.Tests;

/// <summary>Reglas de negocio puras: días hábiles, traslapes, estado de asistencia, geocerca, QR, documentos y antigüedad.</summary>
public class ReglasTests
{
    private static readonly int[] LunesAViernes = [6, 7];

    [Fact(DisplayName = "Caso obligatorio: 15 al 19 de septiembre de 2026 con festivo el 16 da 3 días hábiles")]
    public void DiasHabiles_CasoObligatorio()
    {
        var festivos = new[] { new DateOnly(2026, 9, 16) };
        Assert.Equal(3, Reglas.DiasHabiles(new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 19), LunesAViernes, festivos));
    }

    [Theory(DisplayName = "Días hábiles excluyen descansos del turno y festivos")]
    [InlineData("2026-09-21", "2026-09-25", 5)]
    [InlineData("2026-09-19", "2026-09-20", 0)]
    [InlineData("2026-09-14", "2026-09-20", 4)]
    [InlineData("2026-09-25", "2026-09-21", 0)]
    public void DiasHabiles_Rangos(string a, string b, int esperado)
        => Assert.Equal(esperado, Reglas.DiasHabiles(DateOnly.Parse(a), DateOnly.Parse(b), LunesAViernes, [new DateOnly(2026, 9, 16)]));

    [Fact(DisplayName = "Turno de lunes a sábado cuenta el sábado")]
    public void DiasHabiles_TurnoSabado()
        => Assert.Equal(4, Reglas.DiasHabiles(new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 20), [7], [new DateOnly(2026, 9, 16)]));

    [Theory(DisplayName = "Traslape de periodos")]
    [InlineData("2026-09-15", "2026-09-19", "2026-09-19", "2026-09-22", true)]
    [InlineData("2026-09-15", "2026-09-19", "2026-09-20", "2026-09-22", false)]
    [InlineData("2026-09-15", "2026-09-19", "2026-09-10", "2026-09-30", true)]
    public void Traslape(string a1, string a2, string b1, string b2, bool esperado)
        => Assert.Equal(esperado, Reglas.Traslapa(DateOnly.Parse(a1), DateOnly.Parse(a2), DateOnly.Parse(b1), DateOnly.Parse(b2)));

    [Theory(DisplayName = "Estado de asistencia con 10 minutos de tolerancia")]
    [InlineData("07:15", EstadoRegistro.EnHorario)]
    [InlineData("08:10", EstadoRegistro.EnHorario)]
    [InlineData("08:11", EstadoRegistro.Retardo)]
    [InlineData("08:12", EstadoRegistro.Retardo)]
    public void EstadoEntrada(string hora, EstadoRegistro esperado)
        => Assert.Equal(esperado, Reglas.EstadoEntrada(TimeOnly.Parse(hora), new TimeOnly(8, 0), 10));

    [Fact(DisplayName = "Geocerca: dentro y fuera del radio")]
    public void Geocerca()
    {
        const double lat = 25.6694, lng = -100.3097;
        Assert.True(Reglas.DentroGeocerca(25.6700, -100.3100, lat, lng, 150, out var d1));
        Assert.InRange(d1, 50, 90);
        Assert.False(Reglas.DentroGeocerca(25.6800, -100.3097, lat, lng, 150, out var d2));
        Assert.InRange(d2, 1100, 1250);
    }

    [Fact(DisplayName = "Vigencia del QR: de 30 minutos antes a 2 horas después")]
    public void VigenciaQr()
    {
        var llegada = new DateTime(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);
        var (desde, hasta) = Reglas.VigenciaQr(llegada, 30, 120);
        Assert.Equal(llegada.AddMinutes(-30), desde);
        Assert.Equal(llegada.AddHours(2), hasta);
        Assert.False(Reglas.QrVigente(llegada.AddMinutes(-31), desde, hasta));
        Assert.True(Reglas.QrVigente(llegada.AddMinutes(-30), desde, hasta));
        Assert.True(Reglas.QrVigente(llegada.AddMinutes(119), desde, hasta));
        Assert.False(Reglas.QrVigente(llegada.AddMinutes(121), desde, hasta));
    }

    [Fact(DisplayName = "El QR está firmado: un código alterado se rechaza")]
    public void FirmaQr()
    {
        var contenido = Reglas.ContenidoQr("abc123XYZ", "secreto");
        Assert.Equal("abc123XYZ", Reglas.LeerQr(contenido, "secreto"));
        Assert.Equal("abc123XYZ", Reglas.LeerQr("https://wowtime.mx/qr/x?c=" + contenido, "secreto"));
        Assert.Null(Reglas.LeerQr(contenido, "otro-secreto"));
        Assert.Null(Reglas.LeerQr(contenido.Replace("abc", "abd"), "secreto"));
        Assert.Null(Reglas.LeerQr("cualquier cosa", "secreto"));
    }

    [Theory(DisplayName = "Vigencia de documentos: Urgente ≤ 7 días, Próximo ≤ 30")]
    [InlineData(-1, EstadoDocumento.Vencido)]
    [InlineData(0, EstadoDocumento.Urgente)]
    [InlineData(5, EstadoDocumento.Urgente)]
    [InlineData(7, EstadoDocumento.Urgente)]
    [InlineData(12, EstadoDocumento.Proximo)]
    [InlineData(30, EstadoDocumento.Proximo)]
    [InlineData(31, EstadoDocumento.Vigente)]
    public void EstadoDeDocumento(int dias, EstadoDocumento esperado)
    {
        var hoy = new DateOnly(2026, 9, 8);
        Assert.Equal(esperado, Reglas.EstadoDocumento(hoy.AddDays(dias), hoy, 7, 30));
    }

    [Fact(DisplayName = "Documento sin vencimiento siempre está vigente")]
    public void DocumentoSinVigencia() => Assert.Equal(EstadoDocumento.Vigente, Reglas.EstadoDocumento(null, new DateOnly(2026, 9, 8), 7, 30));

    [Fact(DisplayName = "Estado de asignación temporal por fechas")]
    public void EstadoAsignacion()
    {
        var hoy = new DateOnly(2026, 9, 16);
        Assert.Equal("Vigente", Reglas.EstadoAsignacion(new(2026, 9, 15), new(2026, 9, 19), hoy, false));
        Assert.Equal("Programada", Reglas.EstadoAsignacion(new(2026, 9, 17), new(2026, 9, 19), hoy, false));
        Assert.Equal("Concluida", Reglas.EstadoAsignacion(new(2026, 9, 1), new(2026, 9, 15), hoy, false));
        Assert.Equal("Cancelada", Reglas.EstadoAsignacion(new(2026, 9, 15), new(2026, 9, 19), hoy, true));
    }

    [Fact(DisplayName = "Días por antigüedad según la tabla de la política")]
    public void DiasPorAntiguedad()
    {
        var tabla = new List<PoliticaAntiguedad> { new() { AniosDesde = 1, AniosHasta = 5, Dias = 20 }, new() { AniosDesde = 6, AniosHasta = 10, Dias = 22 } };
        Assert.Equal(20, Reglas.DiasPorAntiguedad(1, tabla));
        Assert.Equal(20, Reglas.DiasPorAntiguedad(5, tabla));
        Assert.Equal(22, Reglas.DiasPorAntiguedad(7, tabla));
        Assert.Equal(22, Reglas.DiasPorAntiguedad(15, tabla));
    }

    [Fact(DisplayName = "Resumen de saldo: disponibles y por vencer")]
    public void ResumenSaldo()
    {
        var hoy = new DateOnly(2026, 11, 17);
        var saldos = new List<SaldoVacaciones>
        {
            new() { DiasOtorgados = 20, DiasUsados = 7, DiasComprometidos = 5, FechaOtorgamiento = new(2025, 12, 10), FechaLimite = new(2026, 12, 10) },
        };
        var r = ServicioSaldos.Calcular(saldos, hoy, 30);
        Assert.Equal(8, r.Disponibles);
        Assert.Equal(8, r.PorVencer);
        Assert.Equal(5, r.Comprometidos);
    }

    [Fact(DisplayName = "Cobertura de equipo")]
    public void Cobertura()
    {
        Assert.Equal(63, Reglas.PorcentajeCobertura(8, 3));
        Assert.Equal(100, Reglas.PorcentajeCobertura(0, 0));
    }

    [Fact(DisplayName = "Festivos oficiales de México 2026")]
    public void Festivos()
    {
        var f = WowTime.Api.Data.Semilla.FestivosMexico(2026).Select(x => x.fecha).ToList();
        Assert.Contains(new DateOnly(2026, 2, 2), f);
        Assert.Contains(new DateOnly(2026, 3, 16), f);
        Assert.Contains(new DateOnly(2026, 9, 16), f);
        Assert.Contains(new DateOnly(2026, 11, 16), f);
        Assert.Equal(7, f.Count);
    }
}
