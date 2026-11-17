using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Saldos, indicadores, vencimientos, calendario de ausencias, cobertura e incapacidades.</summary>
[Tags("Vacaciones y ausencias")]
public class VacacionesController : ApiBase
{
    /// <summary>Saldo, días por vencer, fecha límite y periodos. Sin colaboradorId devuelve el propio.</summary>
    [HttpGet("vacaciones/saldo")]
    public async Task<object> Saldo([FromQuery] int? colaboradorId)
    {
        var id = colaboradorId ?? Yo.ColaboradorIdRequerido;
        var c = await ColaboradorVisible(id);
        var hoy = await HoySucursal(c.SucursalId);
        var pol = await Svc<ServicioSolicitudes>().Politica(c);
        var r = await Svc<ServicioSaldos>().Resumen(id, hoy, pol?.DiasAlertaVencimiento ?? 30);
        var periodos = await Db.Saldos.Where(s => s.ColaboradorId == id).OrderByDescending(s => s.Periodo).ToListAsync();
        return new
        {
            r.Disponibles, r.Comprometidos, r.Usados, r.Otorgados, r.PorVencer, r.FechaLimiteProxima, r.DiasProximoVencimiento, r.FechaProximoVencimiento,
            Politica = pol is null ? null : new { pol.Id, pol.Nombre, pol.PlazoVencimientoMeses },
            Periodos = periodos.Select(s => new { s.Id, s.Periodo, s.FechaOtorgamiento, s.FechaLimite, s.DiasOtorgados, s.DiasUsados, s.DiasComprometidos, s.DiasVencidos, s.Disponibles, Vigente = s.FechaOtorgamiento <= hoy && s.FechaLimite >= hoy })
        };
    }

    /// <summary>Movimientos de saldo (otorgamiento, compromiso, consumo, liberación, vencimiento y ajustes).</summary>
    [HttpGet("vacaciones/movimientos")]
    public async Task<object> Movimientos([FromQuery] int? colaboradorId, [FromQuery] ConsultaPaginada f)
    {
        var id = colaboradorId ?? Yo.ColaboradorIdRequerido;
        await ColaboradorVisible(id);
        f.Orden ??= "Fecha"; f.Dir ??= "desc";
        return await Db.MovimientosSaldo.Where(m => m.ColaboradorId == id).Paginar(f, m => m);
    }

    public record AjusteDto(decimal Dias, string Motivo);
    /// <summary>Ajuste manual de saldo por RH. Siempre deja movimiento y evento en bitácora.</summary>
    [HttpPost("vacaciones/saldos/{colaboradorId:int}/ajuste"), Permiso(Permisos.VacacionesGestionar)]
    public async Task<object> Ajuste(int colaboradorId, AjusteDto d)
    {
        var c = await ColaboradorVisible(colaboradorId, true);
        if (d.Dias == 0) throw ApiException.Validacion("El ajuste no puede ser cero.");
        if (string.IsNullOrWhiteSpace(d.Motivo)) throw ApiException.Validacion("El motivo del ajuste es obligatorio.");
        var hoy = await HoySucursal(c.SucursalId);
        var saldo = await ServicioSaldos.Vigentes(Db.Saldos.Where(s => s.ColaboradorId == colaboradorId), hoy).OrderByDescending(s => s.FechaLimite).FirstOrDefaultAsync()
            ?? throw ApiException.Validacion("El colaborador no tiene un periodo de vacaciones vigente para ajustar.");
        if (saldo.Disponibles + d.Dias < 0) throw ApiException.Validacion("El ajuste dejaría el saldo en negativo.");
        saldo.DiasOtorgados += d.Dias;
        Db.MovimientosSaldo.Add(new MovimientoSaldo { SaldoVacacionesId = saldo.Id, ColaboradorId = colaboradorId, Tipo = TipoMovimientoSaldo.Ajuste, Dias = d.Dias, Fecha = Reloj.Ahora, UsuarioId = Yo.UsuarioId, Motivo = d.Motivo.Trim() });
        Bitacora.Registrar(colaboradorId, "vacaciones", "Ajuste de saldo de vacaciones", $"{(d.Dias > 0 ? "+" : "")}{d.Dias:0.#} días · {d.Motivo}");
        await Db.SaveChangesAsync();
        return new { saldo.Id, saldo.Disponibles };
    }

    private static (DateOnly ini, DateOnly fin) Mes(string? mes, DateOnly hoy)
    {
        var d = DateOnly.TryParseExact((mes ?? "") + "-01", "yyyy-MM-dd", out var x) ? x : new DateOnly(hoy.Year, hoy.Month, 1);
        return (d, d.AddMonths(1).AddDays(-1));
    }

    /// <summary>Los 4 indicadores de Vacaciones: pendientes de RH, personas de vacaciones hoy, días por vencer y cobertura de equipo.</summary>
    [HttpGet("vacaciones/resumen"), Permiso(Permisos.VacacionesGestionar, Permisos.EquipoVer)]
    public async Task<object> Resumen([FromQuery] int? sucursalId, [FromQuery] string? mes, [FromQuery] int? areaId)
    {
        var hoy = await HoySucursal(sucursalId);
        var (ini, fin) = Mes(mes, hoy);
        var cols = await ColaboradoresVisibles(sucursalId);
        cols = cols.Where(c => c.Estatus == EstatusColaborador.Activo);
        if (areaId is int a) cols = cols.Where(c => c.AreaId == a);
        var ids = await cols.Select(c => c.Id).ToListAsync();
        var pendientes = await Db.Solicitudes.CountAsync(s => ids.Contains(s.ColaboradorId) && s.Estado == EstadoSolicitud.Pendiente && s.Tipo != TipoSolicitud.IncidenciaAsistencia
            && s.Pasos.Any(p => p.Orden == s.PasoActual && p.TipoAprobador == TipoAprobador.RecursosHumanos) && s.FechaFin >= ini);
        var deVacaciones = await Db.Solicitudes.Where(s => ids.Contains(s.ColaboradorId) && s.Estado == EstadoSolicitud.Aprobada && s.Tipo == TipoSolicitud.Vacaciones && s.FechaInicio <= hoy && s.FechaFin >= hoy)
            .Select(s => s.ColaboradorId).Distinct().CountAsync();
        var alerta = await Svc<Parametros>().Entero(Parametros.AlertaVencimiento, 30);
        var limite = hoy.AddDays(alerta);
        var porVencer = await Db.Saldos.Where(s => ids.Contains(s.ColaboradorId) && s.FechaLimite >= hoy && s.FechaLimite <= limite && s.DiasOtorgados - s.DiasUsados - s.DiasComprometidos - s.DiasVencidos > 0)
            .SumAsync(s => s.DiasOtorgados - s.DiasUsados - s.DiasComprometidos - s.DiasVencidos);
        var cobertura = await CoberturaDia(ids, hoy);
        return new { SolicitudesPendientes = pendientes, VacacionesHoy = deVacaciones, DiasPorVencer = Math.Max(0, porVencer), Cobertura = cobertura, Mes = ini };
    }

    private async Task<int> CoberturaDia(List<int> ids, DateOnly dia)
    {
        var cols = await Db.Colaboradores.Include(c => c.Turno).Where(c => ids.Contains(c.Id) && c.FechaIngreso <= dia).ToListAsync();
        var programados = cols.Where(c => !(c.Turno?.DiasDescanso ?? [6, 7]).Contains(Reglas.DiaIso(dia))).Select(c => c.Id).ToList();
        if (programados.Count == 0) return 100;
        var ausentes = await Ausentes(programados, dia, dia);
        var n = programados.Count(id => ausentes.Any(a => a.ColaboradorId == id));
        return (int)Reglas.PorcentajeCobertura(programados.Count, n);
    }

    private record Ausencia(int ColaboradorId, DateOnly Inicio, DateOnly Fin, string Tipo, int? SolicitudId, int? IncapacidadId, string Texto);

    private async Task<List<Ausencia>> Ausentes(List<int> ids, DateOnly ini, DateOnly fin, bool incluirPendientes = false)
    {
        var sols = await Db.Solicitudes.Include(s => s.TipoPermiso).Where(s => ids.Contains(s.ColaboradorId) && (s.Tipo == TipoSolicitud.Vacaciones || s.Tipo == TipoSolicitud.Permiso)
            && (s.Estado == EstadoSolicitud.Aprobada || (incluirPendientes && s.Estado == EstadoSolicitud.Pendiente)) && s.FechaInicio <= fin && s.FechaFin >= ini).ToListAsync();
        var incs = await Db.Incapacidades.Where(i => ids.Contains(i.ColaboradorId) && !i.Cancelada && i.FechaInicio <= fin && i.FechaFin >= ini).ToListAsync();
        var res = sols.Select(s => new Ausencia(s.ColaboradorId, s.FechaInicio, s.FechaFin,
            s.Estado == EstadoSolicitud.Pendiente ? "Pendiente" : s.Tipo == TipoSolicitud.Permiso ? "Permiso" : "Aprobada", s.Id, null,
            s.Estado == EstadoSolicitud.Pendiente ? "Solicitud pendiente" : s.Tipo == TipoSolicitud.Permiso ? (s.TipoPermiso?.Nombre ?? "Permiso") : "Vacaciones")).ToList();
        res.AddRange(incs.Select(i => new Ausencia(i.ColaboradorId, i.FechaInicio, i.FechaFin, "Incapacidad", null, i.Id, "Incapacidad")));
        return res;
    }

    /// <summary>Calendario de equipo: una fila por colaborador y una columna por día del mes, con alertas de cobertura.</summary>
    [HttpGet("ausencias/calendario")]
    public async Task<object> Calendario([FromQuery] int? sucursalId, [FromQuery] string? mes, [FromQuery] int? areaId, [FromQuery] int? equipoId, [FromQuery] bool todos = false)
    {
        var hoy = await HoySucursal(sucursalId);
        var (ini, fin) = Mes(mes, hoy);
        var q = (await ColaboradoresVisibles(sucursalId)).Include(c => c.Puesto).Include(c => c.Area).Include(c => c.Equipo).Where(c => c.Estatus == EstatusColaborador.Activo);
        if (areaId is int a) q = q.Where(c => c.AreaId == a);
        if (equipoId is int e) q = q.Where(c => c.EquipoId == e);
        var cols = await q.ToListAsync();
        var ids = cols.Select(c => c.Id).ToList();
        var aus = await Ausentes(ids, ini, fin, true);
        var filas = cols.Where(c => todos || equipoId is not null || aus.Any(x => x.ColaboradorId == c.Id))
            .OrderBy(c => c.Equipo?.Nombre).ThenBy(c => c.Nombres)
            .Select(c => new
            {
                Colaborador = Mapas.Persona(c), Equipo = c.Equipo?.Nombre,
                Ausencias = aus.Where(x => x.ColaboradorId == c.Id).OrderBy(x => x.Inicio).Select(x => new { x.Tipo, x.Texto, x.Inicio, x.Fin, x.SolicitudId, x.IncapacidadId })
            }).ToList();
        var festivos = await Db.DiasFestivos.Where(f => f.Fecha >= ini && f.Fecha <= fin && (f.SucursalId == null || f.SucursalId == sucursalId)).Select(f => new { f.Fecha, f.Nombre }).ToListAsync();
        return new { Desde = ini, Hasta = fin, Filas = filas, Festivos = festivos, Alertas = await AlertasCobertura(cols, aus.Where(x => x.Tipo != "Pendiente").ToList(), ini, fin) };
    }

    private async Task<List<object>> AlertasCobertura(List<Colaborador> cols, List<Ausencia> aus, DateOnly ini, DateOnly fin)
    {
        var equipos = cols.Where(c => c.EquipoId != null).Select(c => c.EquipoId!.Value).Distinct().ToList();
        var eqs = await Db.Equipos.Where(e => equipos.Contains(e.Id)).ToListAsync();
        var miembrosTodos = await Db.Colaboradores.Include(c => c.Turno).Where(c => c.EquipoId != null && equipos.Contains(c.EquipoId.Value) && c.Estatus == EstatusColaborador.Activo).ToListAsync();
        var ausTodos = await Ausentes(miembrosTodos.Select(m => m.Id).ToList(), ini, fin);
        var res = new List<object>();
        foreach (var eq in eqs)
        {
            var miembros = miembrosTodos.Where(m => m.EquipoId == eq.Id).ToList();
            DateOnly? desde = null; int peor = 0; int peorAus = 0; var ausentesNombres = new HashSet<string>();
            void Cerrar(DateOnly hasta)
            {
                if (desde is null) return;
                res.Add(new { EquipoId = eq.Id, Equipo = eq.Nombre, Desde = desde, Hasta = hasta, Ausentes = peorAus, Miembros = miembros.Count, Cobertura = peor, Minimo = eq.CoberturaMinima, Personas = ausentesNombres.ToList() });
                desde = null; peor = 0; peorAus = 0; ausentesNombres = [];
            }
            for (var d = ini; d <= fin; d = d.AddDays(1))
            {
                var prog = miembros.Where(m => !(m.Turno?.DiasDescanso ?? [6, 7]).Contains(Reglas.DiaIso(d))).ToList();
                if (prog.Count == 0) continue;
                var ausentes = prog.Where(m => ausTodos.Any(x => x.ColaboradorId == m.Id && x.Inicio <= d && x.Fin >= d)).ToList();
                var pct = (int)Reglas.PorcentajeCobertura(prog.Count, ausentes.Count);
                if (pct < eq.CoberturaMinima)
                {
                    desde ??= d;
                    if (desde == d || pct < peor) { peor = pct; }
                    peorAus = Math.Max(peorAus, ausentes.Count);
                    foreach (var x in ausentes) ausentesNombres.Add(x.NombreCompleto);
                }
                else Cerrar(d.AddDays(-1));
            }
            Cerrar(fin);
        }
        return res;
    }

    /// <summary>Detalle de cobertura de un equipo por día: miembros, ausentes y porcentaje contra el mínimo.</summary>
    [HttpGet("ausencias/cobertura")]
    public async Task<object> Cobertura([FromQuery] int equipoId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var eq = await Db.Equipos.Include(e => e.Area).FirstOrDefaultAsync(e => e.Id == equipoId) ?? throw ApiException.NoEncontrado("El equipo");
        Yo.ExigirSucursal(eq.Area!.SucursalId);
        var hoy = await HoySucursal(eq.Area.SucursalId);
        var d0 = desde ?? hoy; var d1 = hasta ?? d0.AddDays(13);
        var miembros = await Db.Colaboradores.Include(c => c.Turno).Include(c => c.Puesto).Include(c => c.Area).Where(c => c.EquipoId == equipoId && c.Estatus == EstatusColaborador.Activo).ToListAsync();
        var aus = await Ausentes(miembros.Select(m => m.Id).ToList(), d0, d1, true);
        var dias = new List<object>();
        for (var d = d0; d <= d1; d = d.AddDays(1))
        {
            var prog = miembros.Where(m => !(m.Turno?.DiasDescanso ?? [6, 7]).Contains(Reglas.DiaIso(d))).ToList();
            if (prog.Count == 0) continue;
            var ausentes = prog.Select(m => (m, a: aus.FirstOrDefault(x => x.ColaboradorId == m.Id && x.Inicio <= d && x.Fin >= d && x.Tipo != "Pendiente"))).Where(x => x.a is not null).ToList();
            var pendientes = prog.Count(m => aus.Any(x => x.ColaboradorId == m.Id && x.Inicio <= d && x.Fin >= d && x.Tipo == "Pendiente"));
            var pct = (int)Reglas.PorcentajeCobertura(prog.Count, ausentes.Count);
            dias.Add(new { Fecha = d, Programados = prog.Count, Ausentes = ausentes.Select(x => new { Colaborador = Mapas.Persona(x.m), x.a!.Texto }), PendientesPorAprobar = pendientes, Cobertura = pct, BajoMinimo = pct < eq.CoberturaMinima });
        }
        return new { Equipo = new { eq.Id, eq.Nombre, eq.CoberturaMinima, Area = eq.Area.Nombre }, Miembros = miembros.Count, Dias = dias };
    }

    /// <summary>Días por vencer por colaborador (Próximos vencimientos).</summary>
    [HttpGet("vacaciones/vencimientos")]
    public async Task<object> Vencimientos([FromQuery] int? sucursalId, [FromQuery] int? areaId, [FromQuery] int? dias, [FromQuery] ConsultaPaginada f)
    {
        var hoy = await HoySucursal(sucursalId);
        var ventana = dias ?? 90;
        var cols = await ColaboradoresVisibles(sucursalId);
        if (areaId is int a) cols = cols.Where(c => c.AreaId == a);
        var ids = cols.Where(c => c.Estatus == EstatusColaborador.Activo).Select(c => c.Id);
        var alerta = await Svc<Parametros>().Entero(Parametros.AlertaVencimiento, 30);
        var q = Db.Saldos.Include(s => s.Colaborador).ThenInclude(c => c!.Puesto).Include(s => s.Colaborador).ThenInclude(c => c!.Area)
            .Where(s => ids.Contains(s.ColaboradorId) && s.FechaLimite >= hoy && s.FechaLimite <= hoy.AddDays(ventana) && s.DiasOtorgados - s.DiasUsados - s.DiasComprometidos - s.DiasVencidos > 0);
        f.Orden ??= "FechaLimite";
        return await q.Pag(f, s => new
        {
            Colaborador = Per(s.Colaborador), s.ColaboradorId, DiasPorVencer = s.Disponibles, s.FechaLimite, DiasRestantes = s.FechaLimite.DayNumber - hoy.DayNumber,
            Alerta = s.FechaLimite.DayNumber - hoy.DayNumber <= alerta, s.DiasOtorgados
        });
    }

    public class FiltroSaldos : ConsultaPaginada
    {
        public int? SucursalId { get; set; }
        public int? AreaId { get; set; }
        public string? Q { get; set; }
    }

    /// <summary>Saldos por colaborador (pestaña Políticas y saldos). Orden: disponibles, porVencer, fechaLimite, nombre.</summary>
    [HttpGet("vacaciones/saldos")]
    public async Task<object> Saldos([FromQuery] FiltroSaldos f)
    {
        var hoy = await HoySucursal(f.SucursalId);
        var q = (await ColaboradoresVisibles(f.SucursalId)).Include(c => c.Puesto).Include(c => c.Area).Where(c => c.Estatus == EstatusColaborador.Activo);
        if (f.AreaId is int a) q = q.Where(c => c.AreaId == a);
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(c => EF.Functions.ILike(EF.Functions.Unaccent(c.Nombres + " " + c.Apellidos), EF.Functions.Unaccent($"%{f.Q}%")));
        var cols = await q.ToListAsync();
        var ids = cols.Select(c => c.Id).ToList();
        var saldos = await ServicioSaldos.Vigentes(Db.Saldos.Where(s => ids.Contains(s.ColaboradorId)), hoy).ToListAsync();
        var alerta = await Svc<Parametros>().Entero(Parametros.AlertaVencimiento, 30);
        var filas = cols.Select(c =>
        {
            var r = ServicioSaldos.Calcular(saldos.Where(s => s.ColaboradorId == c.Id).OrderBy(s => s.FechaLimite).ToList(), hoy, alerta);
            return new { Colaborador = Mapas.Persona(c), c.Nombres, r.Otorgados, r.Usados, r.Comprometidos, r.Disponibles, r.PorVencer, r.FechaLimiteProxima, FechaLimite = r.FechaProximoVencimiento };
        });
        var orden = (f.Orden ?? "nombre").ToLowerInvariant();
        var desc = string.Equals(f.Dir, "desc", StringComparison.OrdinalIgnoreCase);
        filas = orden switch
        {
            "disponibles" => desc ? filas.OrderByDescending(x => x.Disponibles) : filas.OrderBy(x => x.Disponibles),
            "porvencer" => desc ? filas.OrderByDescending(x => x.PorVencer) : filas.OrderBy(x => x.PorVencer),
            "fechalimite" => desc ? filas.OrderByDescending(x => x.FechaLimite) : filas.OrderBy(x => x.FechaLimite ?? DateOnly.MaxValue),
            _ => desc ? filas.OrderByDescending(x => x.Nombres) : filas.OrderBy(x => x.Nombres)
        };
        var lista = filas.ToList();
        var tam = Math.Clamp(f.Tamano, 1, 500); var pag = Math.Max(1, f.Pagina);
        return new Pagina<object>(lista.Skip((pag - 1) * tam).Take(tam).Cast<object>().ToList(), lista.Count, pag, tam);
    }

    /// <summary>Saldos de vacaciones (entidad) con filtros.</summary>
    [HttpGet("saldos-vacaciones"), Permiso(Permisos.VacacionesGestionar)]
    public async Task<object> SaldosEntidad([FromQuery] ConsultaPaginada f, [FromQuery] int? colaboradorId, [FromQuery] int? sucursalId)
    {
        var ids = (await ColaboradoresVisibles(sucursalId)).Select(c => c.Id);
        var q = Db.Saldos.Where(s => ids.Contains(s.ColaboradorId));
        if (colaboradorId is int c) q = q.Where(s => s.ColaboradorId == c);
        return await q.Pag(f, s => new { s.Id, s.ColaboradorId, s.Periodo, s.FechaOtorgamiento, s.FechaLimite, s.DiasOtorgados, s.DiasUsados, s.DiasComprometidos, s.DiasVencidos, s.Disponibles });
    }

    // ───── Incapacidades ─────
    public class FiltroIncapacidades : ConsultaPaginada { public int? SucursalId { get; set; } public int? ColaboradorId { get; set; } public DateOnly? Desde { get; set; } public DateOnly? Hasta { get; set; } }

    [HttpGet("incapacidades")]
    public async Task<object> Incapacidades([FromQuery] FiltroIncapacidades f)
    {
        var ids = (await ColaboradoresVisibles(f.SucursalId)).Select(c => c.Id);
        var q = Db.Incapacidades.Include(i => i.Colaborador).ThenInclude(c => c!.Puesto).Where(i => ids.Contains(i.ColaboradorId));
        if (f.ColaboradorId is int c) q = q.Where(i => i.ColaboradorId == c);
        if (f.Desde is DateOnly d) q = q.Where(i => i.FechaFin >= d);
        if (f.Hasta is DateOnly h) q = q.Where(i => i.FechaInicio <= h);
        f.Orden ??= "FechaInicio"; f.Dir ??= "desc";
        return await q.Pag(f, i => new { i.Id, Colaborador = Per(i.Colaborador), i.FechaInicio, i.FechaFin, i.Folio, i.Tipo, i.Nota, i.DocumentoId, i.Cancelada, Dias = i.FechaFin.DayNumber - i.FechaInicio.DayNumber + 1 });
    }

    public record IncapacidadDto(int ColaboradorId, DateOnly FechaInicio, DateOnly FechaFin, string? Folio, string? Tipo, string? Nota, int? DocumentoId);
    /// <summary>Registra una incapacidad (la captura RH con el folio del IMSS). Bloquea traslapes con solicitudes y otras incapacidades.</summary>
    [HttpPost("incapacidades"), Permiso(Permisos.VacacionesGestionar)]
    public async Task<object> CrearIncapacidad(IncapacidadDto d)
    {
        var c = await ColaboradorVisible(d.ColaboradorId, true);
        if (d.FechaFin < d.FechaInicio) throw ApiException.Validacion("La fecha de término no puede ser anterior a la de inicio.");
        if (await Db.Incapacidades.AnyAsync(i => i.ColaboradorId == d.ColaboradorId && !i.Cancelada && i.FechaInicio <= d.FechaFin && i.FechaFin >= d.FechaInicio))
            throw ApiException.Validacion("Se traslapa con otra incapacidad del colaborador.");
        var pend = await Db.Solicitudes.Where(s => s.ColaboradorId == d.ColaboradorId && s.Estado == EstadoSolicitud.Pendiente && (s.Tipo == TipoSolicitud.Vacaciones || s.Tipo == TipoSolicitud.Permiso) && s.FechaInicio <= d.FechaFin && s.FechaFin >= d.FechaInicio).Select(s => s.Folio).ToListAsync();
        if (pend.Count > 0) throw ApiException.Validacion($"Se traslapa con la solicitud pendiente {pend[0]}. Cancélala o recházala primero.");
        var i = new Incapacidad { ColaboradorId = d.ColaboradorId, FechaInicio = d.FechaInicio, FechaFin = d.FechaFin, Folio = d.Folio, Tipo = d.Tipo, Nota = d.Nota, DocumentoId = d.DocumentoId };
        Db.Incapacidades.Add(i);
        Bitacora.Registrar(c.Id, "ausencia", "Incapacidad registrada", $"{ServicioSolicitudes.TextoPeriodo(d.FechaInicio, d.FechaFin)}{(d.Folio is null ? "" : " · " + d.Folio)}");
        await Db.SaveChangesAsync();
        Svc<CanalEventos>().PublicarSucursal(c.SucursalId, "ausencia", new { i.Id });
        return new { i.Id };
    }

    [HttpPost("incapacidades/{id:int}/cancelar"), Permiso(Permisos.VacacionesGestionar)]
    public async Task<IActionResult> CancelarIncapacidad(int id)
    {
        var i = await Db.Incapacidades.FindAsync(id) ?? throw ApiException.NoEncontrado("La incapacidad");
        await ColaboradorVisible(i.ColaboradorId, true);
        i.Cancelada = true;
        Bitacora.Registrar(i.ColaboradorId, "ausencia", "Incapacidad cancelada", ServicioSolicitudes.TextoPeriodo(i.FechaInicio, i.FechaFin));
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Ausencias consolidadas (vacaciones, permisos e incapacidades aprobadas) para integrar con nómina.</summary>
    [HttpGet("ausencias")]
    public async Task<object> AusenciasLista([FromQuery] int? sucursalId, [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta)
    {
        var ids = await (await ColaboradoresVisibles(sucursalId)).Select(c => c.Id).ToListAsync();
        var aus = await Ausentes(ids, desde, hasta);
        var cols = await Db.Colaboradores.Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Numero, c.Nombres, c.Apellidos }).ToDictionaryAsync(c => c.Id);
        return aus.OrderBy(a => a.Inicio).Select(a => new { a.ColaboradorId, cols[a.ColaboradorId].Numero, Nombre = cols[a.ColaboradorId].Nombres + " " + cols[a.ColaboradorId].Apellidos, a.Tipo, a.Texto, a.Inicio, a.Fin, a.SolicitudId, a.IncapacidadId });
    }
}
