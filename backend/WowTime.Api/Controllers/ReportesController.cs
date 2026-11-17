using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Reportes con filtros y exportación a CSV y Excel (formato=json|csv|xlsx).</summary>
[Tags("Reportes")]
[Permiso(Permisos.ReportesSucursal, Permisos.ReportesEquipo)]
public class ReportesController : ApiBase
{
    public class FiltroReporte
    {
        public int? SucursalId { get; set; }
        public int? AreaId { get; set; }
        public int? EquipoId { get; set; }
        public int? ColaboradorId { get; set; }
        public DateOnly? Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        /// <summary>json, csv o xlsx</summary>
        public string Formato { get; set; } = "json";
    }

    public record Columna(string Clave, string Titulo);
    public record Reporte(string Titulo, List<Columna> Columnas, List<Dictionary<string, object?>> Filas, Dictionary<string, object?> Totales);

    /// <summary>Catálogo de reportes disponibles.</summary>
    [HttpGet("reportes")]
    public object Catalogo() => new[]
    {
        new { Clave = "asistencia", Nombre = "Asistencia por periodo", Descripcion = "Días trabajados, puntualidad, retardos y faltas por colaborador." },
        new { Clave = "retardos-faltas", Nombre = "Retardos y faltas", Descripcion = "Detalle de incidencias de asistencia con su estado." },
        new { Clave = "vacaciones-saldos", Nombre = "Vacaciones y saldos", Descripcion = "Saldos, días usados, comprometidos y por vencer." },
        new { Clave = "visitantes", Nombre = "Visitantes", Descripcion = "Invitaciones, entradas y salidas de visitantes." },
        new { Clave = "documentos", Nombre = "Documentos por vencer", Descripcion = "Documentos del expediente vencidos o próximos a vencer." },
    };

    private async Task<List<Colaborador>> Colaboradores(FiltroReporte f)
    {
        var q = (await ColaboradoresVisibles(f.SucursalId)).Include(c => c.Puesto).Include(c => c.Area).Include(c => c.Equipo).Include(c => c.Turno).Include(c => c.Sucursal)
            .Where(c => c.Estatus != EstatusColaborador.Baja);
        if (f.AreaId is int a) q = q.Where(c => c.AreaId == a);
        if (f.EquipoId is int e) q = q.Where(c => c.EquipoId == e);
        if (f.ColaboradorId is int c) q = q.Where(x => x.Id == c);
        return await q.OrderBy(x => x.Nombres).ThenBy(x => x.Apellidos).ToListAsync();
    }

    private (DateOnly, DateOnly) Periodo(FiltroReporte f)
    {
        var hoy = Hoy;
        var d1 = f.Hasta ?? hoy;
        var d0 = f.Desde ?? new DateOnly(d1.Year, d1.Month, 1);
        if (d1 < d0) throw ApiException.Validacion("La fecha final no puede ser anterior a la inicial.");
        if (d1.DayNumber - d0.DayNumber > 370) throw ApiException.Validacion("El periodo máximo es de un año.");
        return (d0, d1);
    }

    /// <summary>Asistencia por periodo.</summary>
    [HttpGet("reportes/asistencia")]
    public async Task<IActionResult> Asistencia([FromQuery] FiltroReporte f)
    {
        var (d0, d1) = Periodo(f);
        var cols = await Colaboradores(f);
        var ids = cols.Select(c => c.Id).ToList();
        var regs = await Db.Registros.Where(r => ids.Contains(r.ColaboradorId) && r.FechaLocal >= d0 && r.FechaLocal <= d1).ToListAsync();
        var incs = await Db.Incidencias.Where(i => ids.Contains(i.ColaboradorId) && i.Fecha >= d0 && i.Fecha <= d1).ToListAsync();
        var filas = cols.Select(c =>
        {
            var e = regs.Where(r => r.ColaboradorId == c.Id && r.Tipo == TipoRegistro.Entrada).ToList();
            var horas = e.Sum(x =>
            {
                var s = regs.FirstOrDefault(r => r.ColaboradorId == c.Id && r.FechaLocal == x.FechaLocal && r.Tipo == TipoRegistro.Salida);
                return s is null ? 0 : (s.FechaHora - x.FechaHora).TotalHours;
            });
            var ci = incs.Where(i => i.ColaboradorId == c.Id).ToList();
            return new Dictionary<string, object?>
            {
                ["numero"] = c.Numero, ["colaborador"] = c.NombreCompleto, ["area"] = c.Area?.Nombre, ["puesto"] = c.Puesto?.Nombre, ["turno"] = c.Turno?.Nombre,
                ["dias"] = e.Count, ["enHorario"] = e.Count(x => x.Estado == EstadoRegistro.EnHorario), ["retardos"] = e.Count(x => x.Estado == EstadoRegistro.Retardo),
                ["faltas"] = ci.Count(i => i.Tipo == TipoIncidencia.Falta), ["salidasOmitidas"] = ci.Count(i => i.Tipo == TipoIncidencia.SalidaOmitida),
                ["fueraDeArea"] = ci.Count(i => i.Tipo == TipoIncidencia.FueraDeArea), ["horas"] = Math.Round(horas, 1),
                ["puntualidad"] = e.Count == 0 ? null : Math.Round(e.Count(x => x.Estado == EstadoRegistro.EnHorario) * 100.0 / e.Count, 1)
            };
        }).ToList();
        var cols2 = new List<Columna> { new("numero", "Número"), new("colaborador", "Colaborador"), new("area", "Área"), new("puesto", "Puesto"), new("turno", "Turno"), new("dias", "Días trabajados"),
            new("enHorario", "En horario"), new("retardos", "Retardos"), new("faltas", "Faltas"), new("salidasOmitidas", "Salidas omitidas"), new("fueraDeArea", "Fuera de área"), new("horas", "Horas"), new("puntualidad", "Puntualidad %") };
        var tot = new Dictionary<string, object?> { ["dias"] = filas.Sum(x => (int)x["dias"]!), ["retardos"] = filas.Sum(x => (int)x["retardos"]!), ["faltas"] = filas.Sum(x => (int)x["faltas"]!), ["horas"] = Math.Round(filas.Sum(x => (double)x["horas"]!), 1) };
        return Salida(new Reporte($"Asistencia del {d0:dd/MM/yyyy} al {d1:dd/MM/yyyy}", cols2, filas, tot), f.Formato, "asistencia");
    }

    /// <summary>Retardos y faltas (detalle de incidencias).</summary>
    [HttpGet("reportes/retardos-faltas")]
    public async Task<IActionResult> RetardosFaltas([FromQuery] FiltroReporte f, [FromQuery] TipoIncidencia? tipo)
    {
        var (d0, d1) = Periodo(f);
        var cols = await Colaboradores(f);
        var map = cols.ToDictionary(c => c.Id);
        var ids = map.Keys.ToList();
        var q = Db.Incidencias.Where(i => ids.Contains(i.ColaboradorId) && i.Fecha >= d0 && i.Fecha <= d1);
        if (tipo is TipoIncidencia t) q = q.Where(i => i.Tipo == t);
        var incs = await q.OrderByDescending(i => i.Fecha).ToListAsync();
        var filas = incs.Select(i => new Dictionary<string, object?>
        {
            ["fecha"] = i.Fecha.ToString("dd/MM/yyyy"), ["numero"] = map[i.ColaboradorId].Numero, ["colaborador"] = map[i.ColaboradorId].NombreCompleto, ["area"] = map[i.ColaboradorId].Area?.Nombre,
            ["tipo"] = Mapas.NombreIncidencia(i.Tipo), ["detalle"] = i.Detalle, ["estado"] = i.Estado switch { EstadoIncidencia.PorRevisar => "Por revisar", EstadoIncidencia.EnRevision => "En revisión", EstadoIncidencia.Justificada => "Justificada", _ => "Confirmada" },
            ["justificacion"] = i.Justificacion ?? i.ComentarioResolucion
        }).ToList();
        var cs = new List<Columna> { new("fecha", "Fecha"), new("numero", "Número"), new("colaborador", "Colaborador"), new("area", "Área"), new("tipo", "Tipo"), new("detalle", "Detalle"), new("estado", "Estado"), new("justificacion", "Justificación o comentario") };
        var tot = new Dictionary<string, object?> { ["retardos"] = incs.Count(i => i.Tipo == TipoIncidencia.Retardo), ["faltas"] = incs.Count(i => i.Tipo == TipoIncidencia.Falta), ["total"] = incs.Count };
        return Salida(new Reporte($"Retardos y faltas del {d0:dd/MM/yyyy} al {d1:dd/MM/yyyy}", cs, filas, tot), f.Formato, "retardos-faltas");
    }

    /// <summary>Vacaciones y saldos por colaborador, con días tomados en el periodo.</summary>
    [HttpGet("reportes/vacaciones-saldos")]
    public async Task<IActionResult> VacacionesSaldos([FromQuery] FiltroReporte f)
    {
        var (d0, d1) = Periodo(f);
        var hoy = Hoy;
        var cols = await Colaboradores(f);
        var ids = cols.Select(c => c.Id).ToList();
        var saldos = await ServicioSaldos.Vigentes(Db.Saldos.Where(s => ids.Contains(s.ColaboradorId)), hoy).ToListAsync();
        var sols = await Db.Solicitudes.Where(s => ids.Contains(s.ColaboradorId) && s.Tipo == TipoSolicitud.Vacaciones && s.FechaInicio <= d1 && s.FechaFin >= d0).ToListAsync();
        var alerta = await Svc<Parametros>().Entero(Parametros.AlertaVencimiento, 30);
        var filas = cols.Select(c =>
        {
            var r = ServicioSaldos.Calcular(saldos.Where(s => s.ColaboradorId == c.Id).OrderBy(s => s.FechaLimite).ToList(), hoy, alerta);
            var ss = sols.Where(s => s.ColaboradorId == c.Id).ToList();
            return new Dictionary<string, object?>
            {
                ["numero"] = c.Numero, ["colaborador"] = c.NombreCompleto, ["area"] = c.Area?.Nombre, ["ingreso"] = c.FechaIngreso.ToString("dd/MM/yyyy"),
                ["otorgados"] = r.Otorgados, ["usados"] = r.Usados, ["comprometidos"] = r.Comprometidos, ["disponibles"] = r.Disponibles, ["porVencer"] = r.PorVencer,
                ["fechaLimite"] = r.FechaProximoVencimiento?.ToString("dd/MM/yyyy"), ["aprobadasPeriodo"] = ss.Where(s => s.Estado == EstadoSolicitud.Aprobada).Sum(s => s.DiasHabiles),
                ["pendientesPeriodo"] = ss.Where(s => s.Estado == EstadoSolicitud.Pendiente).Sum(s => s.DiasHabiles)
            };
        }).ToList();
        var cs = new List<Columna> { new("numero", "Número"), new("colaborador", "Colaborador"), new("area", "Área"), new("ingreso", "Ingreso"), new("otorgados", "Otorgados"), new("usados", "Usados"),
            new("comprometidos", "Comprometidos"), new("disponibles", "Disponibles"), new("porVencer", "Por vencer"), new("fechaLimite", "Fecha límite"), new("aprobadasPeriodo", "Días aprobados en el periodo"), new("pendientesPeriodo", "Días pendientes en el periodo") };
        var tot = new Dictionary<string, object?> { ["disponibles"] = filas.Sum(x => (decimal)x["disponibles"]!), ["porVencer"] = filas.Sum(x => (decimal)x["porVencer"]!), ["aprobadasPeriodo"] = filas.Sum(x => (decimal)x["aprobadasPeriodo"]!) };
        return Salida(new Reporte($"Vacaciones y saldos al {hoy:dd/MM/yyyy}", cs, filas, tot), f.Formato, "vacaciones-saldos");
    }

    /// <summary>Visitantes del periodo.</summary>
    [HttpGet("reportes/visitantes")]
    public async Task<IActionResult> Visitantes([FromQuery] FiltroReporte f)
    {
        if (!Yo.Tiene(Permisos.VisitantesVer)) throw ApiException.Prohibido();
        var (d0, d1) = Periodo(f);
        var q = Yo.Alcance(Db.Invitaciones.Include(i => i.Anfitrion).Include(i => i.Acceso), i => i.SucursalId, f.SucursalId).Where(i => i.FechaLocal >= d0 && i.FechaLocal <= d1);
        var lista = await q.OrderByDescending(i => i.FechaLlegada).ToListAsync();
        var tzs = await Db.Sucursales.ToDictionaryAsync(s => s.Id, s => s.ZonaHoraria);
        string? H(DateTime? d, int s) => d is DateTime x ? Zona.ALocal(x, tzs.GetValueOrDefault(s)).ToString("HH:mm") : null;
        var filas = lista.Select(i => new Dictionary<string, object?>
        {
            ["fecha"] = i.FechaLocal.ToString("dd/MM/yyyy"), ["nombre"] = i.Nombre, ["empresa"] = i.EmpresaMotivo, ["anfitrion"] = i.Anfitrion?.NombreCompleto,
            ["llegada"] = H(i.FechaLlegada, i.SucursalId), ["entrada"] = H(i.Acceso?.Entrada, i.SucursalId), ["salida"] = H(i.Acceso?.Salida, i.SucursalId),
            ["estado"] = i.Estado switch { EstadoInvitacion.Dentro => "Dentro", EstadoInvitacion.Salio => "Salió", EstadoInvitacion.Vencida => "Vencida", EstadoInvitacion.Cancelada => "Cancelada", _ => "Esperado" },
            ["minutos"] = i.Acceso?.Salida is DateTime s ? (int)(s - i.Acceso.Entrada).TotalMinutes : null
        }).ToList();
        var cs = new List<Columna> { new("fecha", "Fecha"), new("nombre", "Visitante"), new("empresa", "Empresa / Motivo"), new("anfitrion", "Anfitrión"), new("llegada", "Llegada programada"), new("entrada", "Entrada"), new("salida", "Salida"), new("estado", "Estado"), new("minutos", "Minutos dentro") };
        var tot = new Dictionary<string, object?> { ["total"] = filas.Count, ["asistieron"] = lista.Count(i => i.Acceso is not null) };
        return Salida(new Reporte($"Visitantes del {d0:dd/MM/yyyy} al {d1:dd/MM/yyyy}", cs, filas, tot), f.Formato, "visitantes");
    }

    /// <summary>Documentos vencidos o por vencer.</summary>
    [HttpGet("reportes/documentos")]
    public async Task<IActionResult> Documentos([FromQuery] FiltroReporte f, [FromQuery] int? dias)
    {
        if (!Yo.Tiene(Permisos.ExpedientesGestionar)) throw ApiException.Prohibido();
        var hoy = Hoy;
        var cols = await Colaboradores(f);
        var map = cols.ToDictionary(c => c.Id);
        var ids = map.Keys.ToList();
        var p = Svc<Parametros>();
        var urg = await p.Entero(Parametros.DocUrgente, 7); var prox = await p.Entero(Parametros.DocProximo, 30);
        var limite = hoy.AddDays(dias ?? prox);
        var docs = await Db.Documentos.Include(d => d.TipoDocumento).Where(d => ids.Contains(d.ColaboradorId) && !d.Reemplazado && d.FechaVencimiento != null && d.FechaVencimiento <= limite).OrderBy(d => d.FechaVencimiento).ToListAsync();
        var filas = docs.Select(d => new Dictionary<string, object?>
        {
            ["documento"] = d.TipoDocumento!.Nombre, ["numero"] = map[d.ColaboradorId].Numero, ["colaborador"] = map[d.ColaboradorId].NombreCompleto, ["area"] = map[d.ColaboradorId].Area?.Nombre,
            ["vence"] = d.FechaVencimiento!.Value.ToString("dd/MM/yyyy"), ["dias"] = d.FechaVencimiento.Value.DayNumber - hoy.DayNumber,
            ["estado"] = Services.Reglas.EstadoDocumento(d.FechaVencimiento, hoy, urg, prox) switch { EstadoDocumento.Vencido => "Vencido", EstadoDocumento.Urgente => "Urgente", EstadoDocumento.Proximo => "Próximo", _ => "Vigente" }
        }).ToList();
        var cs = new List<Columna> { new("documento", "Documento"), new("numero", "Número"), new("colaborador", "Colaborador"), new("area", "Área"), new("vence", "Vence"), new("dias", "Días"), new("estado", "Estado") };
        var tot = new Dictionary<string, object?> { ["total"] = filas.Count, ["vencidos"] = filas.Count(x => (string)x["estado"]! == "Vencido"), ["urgentes"] = filas.Count(x => (string)x["estado"]! == "Urgente") };
        return Salida(new Reporte($"Documentos por vencer al {hoy:dd/MM/yyyy}", cs, filas, tot), f.Formato, "documentos");
    }

    private IActionResult Salida(Reporte r, string formato, string nombre)
    {
        var archivo = $"wowtime-{nombre}-{Hoy:yyyyMMdd}";
        switch ((formato ?? "json").ToLowerInvariant())
        {
            case "csv":
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",", r.Columnas.Select(c => Csv(c.Titulo))));
                foreach (var f in r.Filas) sb.AppendLine(string.Join(",", r.Columnas.Select(c => Csv(f.GetValueOrDefault(c.Clave)))));
                return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv; charset=utf-8", archivo + ".csv");
            case "xlsx":
                using (var wb = new XLWorkbook())
                {
                    var ws = wb.AddWorksheet("Reporte");
                    ws.Cell(1, 1).Value = r.Titulo; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
                    for (var i = 0; i < r.Columnas.Count; i++)
                    {
                        var h = ws.Cell(3, i + 1); h.Value = r.Columnas[i].Titulo; h.Style.Font.Bold = true;
                        h.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F2847"); h.Style.Font.FontColor = XLColor.White;
                    }
                    for (var j = 0; j < r.Filas.Count; j++)
                        for (var i = 0; i < r.Columnas.Count; i++)
                        {
                            var v = r.Filas[j].GetValueOrDefault(r.Columnas[i].Clave);
                            var cell = ws.Cell(j + 4, i + 1);
                            switch (v)
                            {
                                case null: break;
                                case int n: cell.Value = n; break;
                                case decimal d: cell.Value = d; break;
                                case double d: cell.Value = d; break;
                                default: cell.Value = v.ToString(); break;
                            }
                        }
                    if (r.Filas.Count > 0) ws.Range(3, 1, r.Filas.Count + 3, r.Columnas.Count).SetAutoFilter();
                    ws.SheetView.FreezeRows(3);
                    ws.Columns().AdjustToContents(8, 60);
                    var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;
                    return File(ms, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", archivo + ".xlsx");
                }
            default:
                return Ok(r);
        }
    }

    private static string Csv(object? v)
    {
        var s = v switch { null => "", decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture), double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => v.ToString() ?? "" };
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
