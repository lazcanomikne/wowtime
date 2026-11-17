using Microsoft.Data.SqlClient;

namespace WowTime.Api.Services.Integracion;

// Filas de las vistas wowtime.* de InovaOS (docs/ORIGEN_SQLSERVER.md). Las fechas sin zona están en hora de Monterrey.

public record ColaboradorOrigen(int EmployeeID, string? NumeroEmpleadoLector, string? FirstName, string? LastName, string Email,
    string? JobTitle, string? Department, DateOnly EntryDate, int? ManagerID, bool Activo);
public record PeriodoOrigen(int PeriodoID, int EmployeeID, int NumeroPeriodo, DateOnly FechaInicio, DateOnly FechaFin, decimal DiasDerecho, string? Notas);
public record EscalaOrigen(int MinYears, int MaxYears, int DaysEntitled);
public record SolicitudOrigen(int RequestID, int EmployeeID, string RequestType, DateOnly StartDate, DateOnly EndDate, decimal DaysQuantity,
    decimal DeductionValue, string? Reason, string? Status, bool? BossApproved, DateTime? BossApprovedDate, bool? DirectorApproved,
    DateTime? DirectorApprovedDate, DateTime? CreatedAt, int? PeriodoID, bool SinDescuento, string? NotaAjuste);
public record AprobacionOrigen(int ApprovalID, int RequestID, int Nivel, int? ApproverEmployeeID, string? ApproverName, string Status, DateTime? ActedAt, string? Comment);
public record ConfigAsistenciaOrigen(TimeOnly HoraEntrada, TimeOnly HoraSalida, string DiasLaborales, int ToleranciaMin, int ToleranciaSalidaMin, DateOnly InicioCalculo);
public record HorarioOrigen(int HorarioID, int EmployeeID, TimeOnly HoraEntrada, TimeOnly HoraSalida, string DiasLaborales, int? ToleranciaMin, DateOnly VigenteDesde, DateOnly? VigenteHasta);
public record JustificacionOrigen(int JustificacionID, int EmployeeID, DateOnly Fecha, string Tipo, string Motivo, string Estado, string? ResueltoNombre, DateTime? ResueltoEn, string? Respuesta);
public record FestivoOrigen(DateOnly Fecha, string Nombre);
public record ChecadaOrigen(long EventoId, DateTimeOffset FechaEvento, DateTime FechaLocal, string? NumeroEmpleado);

/// <summary>Lectura del sistema de origen. Solo lectura; se puede sustituir en pruebas.</summary>
public interface IOrigenInova
{
    bool Configurado { get; }
    bool ChecadasConfiguradas { get; }
    Task<List<ColaboradorOrigen>> Colaboradores();
    Task<List<PeriodoOrigen>> Periodos();
    Task<List<EscalaOrigen>> Escala();
    Task<List<SolicitudOrigen>> Solicitudes();
    Task<List<AprobacionOrigen>> Aprobaciones();
    Task<ConfigAsistenciaOrigen?> ConfigAsistencia();
    Task<List<HorarioOrigen>> Horarios();
    Task<List<JustificacionOrigen>> Justificaciones();
    Task<List<FestivoOrigen>> Festivos();
    /// <summary>Checadas válidas con EventoId mayor al indicado y fecha local desde el día indicado, en orden.</summary>
    Task<List<ChecadaOrigen>> Checadas(long despuesDe, DateOnly desde);
}

/// <summary>Lee las vistas wowtime.* con el usuario de solo lectura (ConnectionStrings:Origen y ConnectionStrings:OrigenChecadas).</summary>
public class SqlOrigenInova(IConfiguration cfg) : IOrigenInova
{
    private string? Rh => Limpia(cfg.GetConnectionString("Origen"));
    private string? Lector => Limpia(cfg.GetConnectionString("OrigenChecadas"));
    public bool Configurado => Rh is not null;
    public bool ChecadasConfiguradas => Lector is not null;

    private static string? Limpia(string? c) => string.IsNullOrWhiteSpace(c) || c.Contains("Password=;") ? null : c;

    private static async Task<List<T>> Leer<T>(string? cadena, string sql, Func<SqlDataReader, T> fila, params SqlParameter[] ps)
    {
        if (cadena is null) throw new InvalidOperationException("Falta la cadena de conexión al origen (ConnectionStrings__Origen / ConnectionStrings__OrigenChecadas en .env).");
        await using var cn = new SqlConnection(cadena);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 120 };
        cmd.Parameters.AddRange(ps);
        await using var r = await cmd.ExecuteReaderAsync();
        var res = new List<T>();
        while (await r.ReadAsync()) res.Add(fila(r));
        return res;
    }

    private static string? S(SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToString(r[c])?.Trim();
    private static int? I(SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToInt32(r[c]);
    private static decimal D(SqlDataReader r, string c) => r[c] is DBNull ? 0 : Convert.ToDecimal(r[c]);
    private static bool? B(SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToBoolean(r[c]);
    private static DateTime? Dt(SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToDateTime(r[c]);
    private static DateOnly F(SqlDataReader r, string c) => DateOnly.FromDateTime(Convert.ToDateTime(r[c]));
    private static DateOnly? Fn(SqlDataReader r, string c) => r[c] is DBNull ? null : DateOnly.FromDateTime(Convert.ToDateTime(r[c]));
    private static TimeOnly H(SqlDataReader r, string c) => TimeOnly.FromTimeSpan((TimeSpan)r[c]);

    public Task<List<ColaboradorOrigen>> Colaboradores() => Leer(Rh,
        "SELECT EmployeeID, NumeroEmpleadoLector, FirstName, LastName, Email, JobTitle, Department, EntryDate, ManagerID, Activo FROM wowtime.Colaboradores ORDER BY EmployeeID",
        r => new ColaboradorOrigen((int)r["EmployeeID"], S(r, "NumeroEmpleadoLector"), S(r, "FirstName"), S(r, "LastName"), S(r, "Email") ?? "",
            S(r, "JobTitle"), S(r, "Department"), F(r, "EntryDate"), I(r, "ManagerID"), B(r, "Activo") ?? false));

    public Task<List<PeriodoOrigen>> Periodos() => Leer(Rh,
        "SELECT PeriodoID, EmployeeID, NumeroPeriodo, FechaInicio, FechaFin, DiasDerecho, Notas FROM wowtime.PeriodosVacaciones ORDER BY PeriodoID",
        r => new PeriodoOrigen((int)r["PeriodoID"], (int)r["EmployeeID"], (int)r["NumeroPeriodo"], F(r, "FechaInicio"), F(r, "FechaFin"), D(r, "DiasDerecho"), S(r, "Notas")));

    public Task<List<EscalaOrigen>> Escala() => Leer(Rh, "SELECT MinYears, MaxYears, DaysEntitled FROM wowtime.EscalaVacaciones ORDER BY MinYears",
        r => new EscalaOrigen((int)r["MinYears"], (int)r["MaxYears"], (int)r["DaysEntitled"]));

    public Task<List<SolicitudOrigen>> Solicitudes() => Leer(Rh,
        """
        SELECT RequestID, EmployeeID, RequestType, StartDate, EndDate, DaysQuantity, DeductionValue, Reason, Status, BossApproved, BossApprovedDate,
               DirectorApproved, DirectorApprovedDate, CreatedAt, PeriodoID, SinDescuento, NotaAjuste
        FROM wowtime.Solicitudes ORDER BY RequestID
        """,
        r => new SolicitudOrigen((int)r["RequestID"], (int)r["EmployeeID"], S(r, "RequestType") ?? "", F(r, "StartDate"), F(r, "EndDate"), D(r, "DaysQuantity"),
            D(r, "DeductionValue"), S(r, "Reason"), S(r, "Status"), B(r, "BossApproved"), Dt(r, "BossApprovedDate"), B(r, "DirectorApproved"),
            Dt(r, "DirectorApprovedDate"), Dt(r, "CreatedAt"), I(r, "PeriodoID"), B(r, "SinDescuento") ?? false, S(r, "NotaAjuste")));

    public Task<List<AprobacionOrigen>> Aprobaciones() => Leer(Rh,
        "SELECT ApprovalID, RequestID, Nivel, ApproverEmployeeID, ApproverName, Status, ActedAt, Comment FROM wowtime.Aprobaciones ORDER BY RequestID, Nivel",
        r => new AprobacionOrigen((int)r["ApprovalID"], (int)r["RequestID"], (int)r["Nivel"], I(r, "ApproverEmployeeID"), S(r, "ApproverName"),
            S(r, "Status") ?? "", Dt(r, "ActedAt"), S(r, "Comment")));

    public async Task<ConfigAsistenciaOrigen?> ConfigAsistencia() => (await Leer(Rh,
        "SELECT TOP 1 HoraEntrada, HoraSalida, DiasLaborales, ToleranciaMin, ToleranciaSalidaMin, InicioCalculo FROM wowtime.ConfigAsistencia",
        r => new ConfigAsistenciaOrigen(H(r, "HoraEntrada"), H(r, "HoraSalida"), S(r, "DiasLaborales") ?? "1111100", (int)r["ToleranciaMin"],
            (int)r["ToleranciaSalidaMin"], F(r, "InicioCalculo")))).FirstOrDefault();

    public Task<List<HorarioOrigen>> Horarios() => Leer(Rh,
        "SELECT HorarioID, EmployeeID, HoraEntrada, HoraSalida, DiasLaborales, ToleranciaMin, VigenteDesde, VigenteHasta FROM wowtime.HorariosPersona ORDER BY VigenteDesde",
        r => new HorarioOrigen((int)r["HorarioID"], (int)r["EmployeeID"], H(r, "HoraEntrada"), H(r, "HoraSalida"), S(r, "DiasLaborales") ?? "1111100",
            I(r, "ToleranciaMin"), F(r, "VigenteDesde"), Fn(r, "VigenteHasta")));

    public Task<List<JustificacionOrigen>> Justificaciones() => Leer(Rh,
        "SELECT JustificacionID, EmployeeID, Fecha, Tipo, Motivo, Estado, ResueltoNombre, ResueltoEn, Respuesta FROM wowtime.Justificaciones ORDER BY JustificacionID",
        r => new JustificacionOrigen((int)r["JustificacionID"], (int)r["EmployeeID"], F(r, "Fecha"), S(r, "Tipo") ?? "", S(r, "Motivo") ?? "",
            S(r, "Estado") ?? "", S(r, "ResueltoNombre"), Dt(r, "ResueltoEn"), S(r, "Respuesta")));

    public Task<List<FestivoOrigen>> Festivos() => Leer(Rh, "SELECT Fecha, Nombre FROM wowtime.Festivos ORDER BY Fecha",
        r => new FestivoOrigen(F(r, "Fecha"), S(r, "Nombre") ?? "Día festivo"));

    public Task<List<ChecadaOrigen>> Checadas(long despuesDe, DateOnly desde) => Leer(Lector,
        "SELECT EventoId, FechaEvento, FechaLocal, NumeroEmpleado FROM wowtime.Checadas WHERE EventoId > @ultimo AND FechaLocal >= @desde ORDER BY EventoId",
        r => new ChecadaOrigen((long)r["EventoId"], (DateTimeOffset)r["FechaEvento"], Convert.ToDateTime(r["FechaLocal"]), S(r, "NumeroEmpleado")),
        new SqlParameter("@ultimo", despuesDe), new SqlParameter("@desde", desde.ToDateTime(TimeOnly.MinValue)));
}
