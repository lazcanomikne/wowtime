using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>
/// API para el backend de InovaOS (contrato en docs/INTEGRACION_INOVAOS.md). Exige una llave con el permiso integracion.inovaos.
/// Las rutas "como usuario" usan el encabezado X-Actuar-Como y operan con los permisos y el alcance de esa persona.
/// </summary>
[Tags("Integración InovaOS")]
[Permiso(Permisos.IntegracionInovaOS)]
public class IntegracionInovaOSController : ApiBase
{
    public const string Canal = "InovaOS";

    public record ColaboradorIo(int Id, int? OrigenId, string Nombre, string? Puesto, string? Area, string? Correo, DateOnly FechaIngreso);
    public record AprobadorIo(string? Nombre, string? Correo);
    public record AprobacionIo(int Nivel, AprobadorIo Aprobador, string Estado, DateTime? Fecha, string? Comentario);
    public record SolicitudIo(int Id, int? OrigenId, string Tipo, DateOnly Inicio, DateOnly Fin, DateOnly? Regreso, decimal Dias, decimal Descuento,
        string? Motivo, string Estado, DateTime Creada, ColaboradorIo Colaborador, List<AprobacionIo> Aprobaciones, bool PuedeActuar);
    public record AntiguedadIo(int Anios, int Meses);
    public record PeriodoIo(DateOnly Inicio, DateOnly Fin, decimal Otorgados, decimal Usados, decimal Programados, decimal Canjeados, decimal Disponible, int DiasParaVencer);
    public record PerfilIo(ColaboradorIo Colaborador, AntiguedadIo Antiguedad, PeriodoIo? Periodo);
    public record NuevaSolicitudIo(string? Tipo, string? Inicio, string? Fin, decimal? Dias, string? Motivo);
    public record DecisionIo(string? Comentario);
    public record ResultadoDecisionIo(string Estado, int? SiguienteNivel);

    public static readonly string[] TiposContrato = ["VACATION", "PERMIT_3H", "PERMIT_5H", "PERMIT_DAY", "CASH_OUT"];

    // ───────────── Como usuario ─────────────

    /// <summary>1. Ficha y saldo del periodo vigente de quien indica X-Actuar-Como.</summary>
    [HttpGet("integracion/inovaos/perfil")]
    public async Task<PerfilIo> Perfil()
    {
        var c = await Persona();
        var hoy = Hoy;
        var meses = (hoy.Year - c.FechaIngreso.Year) * 12 + hoy.Month - c.FechaIngreso.Month - (hoy.Day < c.FechaIngreso.Day ? 1 : 0);
        meses = Math.Max(0, meses);
        var saldo = await ServicioSaldos.Vigentes(Db.Saldos.Where(s => s.ColaboradorId == c.Id), hoy)
            .OrderByDescending(s => s.FechaOtorgamiento).FirstOrDefaultAsync();
        PeriodoIo? periodo = null;
        if (saldo is not null)
        {
            // Lo consumido del periodo se separa en usado (ya inició), programado (a futuro) y canjeado
            var consumos = await Db.MovimientosSaldo.Where(m => m.SaldoVacacionesId == saldo.Id && m.Tipo == TipoMovimientoSaldo.Consumo && m.SolicitudId != null)
                .Join(Db.Solicitudes, m => m.SolicitudId, s => s.Id, (m, s) => new { m.Dias, s.Tipo, s.FechaInicio }).ToListAsync();
            var canjeados = consumos.Where(x => x.Tipo == TipoSolicitud.CanjeDias).Sum(x => x.Dias);
            var programados = consumos.Where(x => x.Tipo != TipoSolicitud.CanjeDias && x.FechaInicio > hoy).Sum(x => x.Dias);
            periodo = new(saldo.FechaOtorgamiento, saldo.FechaLimite, saldo.DiasOtorgados, saldo.DiasUsados - canjeados - programados,
                programados, canjeados, saldo.Disponibles, saldo.FechaLimite.DayNumber - hoy.DayNumber);
        }
        return new(Colab(c), new(meses / 12, meses), periodo);
    }

    /// <summary>2. Todas las solicitudes de quien indica X-Actuar-Como, de cualquier estado.</summary>
    [HttpGet("integracion/inovaos/solicitudes/mias")]
    public async Task<List<SolicitudIo>> Mias()
    {
        var c = await Persona();
        return await Lista(Consulta().Where(s => s.ColaboradorId == c.Id).OrderByDescending(s => s.CreadoEn), decisor: Decisor());
    }

    /// <summary>3. Crea una solicitud a nombre de quien indica X-Actuar-Como: compromete saldo y avisa al primer aprobador, igual que la web.</summary>
    [HttpPost("integracion/inovaos/solicitudes")]
    [ProducesResponseType(typeof(SolicitudIo), 201)]
    public async Task<IActionResult> Crear(NuevaSolicitudIo d)
    {
        var c = await Persona();
        if (!Yo.Tiene(Permisos.SolicitudesCrear)) throw new ApiException(403, "sin_permiso", "Tu usuario no puede enviar solicitudes. Pide a Recursos Humanos que revise tus permisos.");
        var tipo = (d.Tipo ?? "").Trim().ToUpperInvariant();
        if (!TiposContrato.Contains(tipo))
            throw new ApiException(422, "tipo_invalido", "El tipo de solicitud no es válido. Usa VACATION, PERMIT_3H, PERMIT_5H, PERMIT_DAY o CASH_OUT.");
        var hoy = Hoy;
        var datos = new DatosSolicitud { Comentario = d.Motivo };
        if (tipo == "CASH_OUT")
        {
            if (d.Dias is not decimal dias || dias <= 0) throw new ApiException(422, "dias_invalidos", "Indica cuántos días quieres canjear.");
            datos.Tipo = TipoSolicitud.CanjeDias;
            datos.Dias = dias;
            datos.FechaInicio = datos.FechaFin = hoy;
        }
        else
        {
            if (!Fecha(d.Inicio, out var ini)) throw new ApiException(422, "rango_invalido", "Indica la fecha de inicio (AAAA-MM-DD).");
            var fin = ini;
            if (!string.IsNullOrWhiteSpace(d.Fin) && !Fecha(d.Fin, out fin)) throw new ApiException(422, "rango_invalido", "La fecha final no es válida (AAAA-MM-DD).");
            if (fin < ini) throw new ApiException(422, "rango_invalido", "La fecha final es anterior a la inicial.");
            datos.FechaInicio = ini;
            datos.FechaFin = fin;
            if (tipo == "VACATION") datos.Tipo = TipoSolicitud.Vacaciones;
            else
            {
                var tp = await Db.TiposPermiso.FirstOrDefaultAsync(t => t.Clave == tipo && t.Activo)
                    ?? throw new ApiException(422, "tipo_invalido", "Ese tipo de permiso no está disponible en WOW! Time.");
                datos.Tipo = TipoSolicitud.Permiso;
                datos.TipoPermisoId = tp.Id;
            }
        }
        Solicitud s;
        try { s = await Svc<ServicioSolicitudes>().Crear(datos, c.Id, Yo.UsuarioIdRequerido, false, canal: Canal); }
        catch (ApiException ex) when (ex.Status == 422 && ex.Detalles is List<Validacion> v && v.Count > 0)
        {
            throw new ApiException(422, CodigoContrato(v[0].Codigo), ex.Message, v);
        }
        var r = (await Lista(Consulta().Where(x => x.Id == s.Id), decisor: Decisor())).Single();
        return StatusCode(201, r);
    }

    /// <summary>4. Las solicitudes que le conciernen: las que le toca decidir ahora y las que ya decidió. RH y Dirección ven además todas las pendientes.</summary>
    [HttpGet("integracion/inovaos/aprobaciones")]
    public async Task<List<SolicitudIo>> Aprobaciones()
    {
        await Persona(requiereColaborador: false);
        var decisor = Decisor();
        var hoy = Hoy;
        var porDecidir = await Svc<MotorAprobaciones>().Bandeja(decisor, hoy);
        var uid = decisor.UsuarioId;
        var todasPendientes = Yo.Roles.Contains(Roles.RecursosHumanos) || Yo.Roles.Contains(Roles.Administrador);
        var q = Consulta().Where(s => porDecidir.Contains(s.Id) || s.Pasos.Any(p => p.DecididoPorUsuarioId == uid)
            || (todasPendientes && s.Estado == EstadoSolicitud.Pendiente));
        if (todasPendientes) q = Yo.Alcance(q, s => s.SucursalId);
        return await Lista(q.OrderByDescending(s => s.CreadoEn), decisor, porDecidir.ToHashSet());
    }

    /// <summary>5. Aprueba el paso que le toca a quien indica X-Actuar-Como.</summary>
    [HttpPost("integracion/inovaos/solicitudes/{id:int}/aprobar")]
    public async Task<ResultadoDecisionIo> Aprobar(int id, DecisionIo? d)
    {
        await Persona(requiereColaborador: false);
        var s = await Decidible(id);
        s = await Svc<ServicioSolicitudes>().Aprobar(s.Id, Decisor(), d?.Comentario);
        return new(Estado(s.Estado), s.Estado == EstadoSolicitud.Pendiente ? s.PasoActual : null);
    }

    /// <summary>6. Rechaza: corta la cadena y libera el saldo comprometido.</summary>
    [HttpPost("integracion/inovaos/solicitudes/{id:int}/rechazar")]
    public async Task<ResultadoDecisionIo> Rechazar(int id, DecisionIo? d)
    {
        await Persona(requiereColaborador: false);
        var s = await Decidible(id);
        var motivo = string.IsNullOrWhiteSpace(d?.Comentario) ? "Rechazada sin comentario." : d!.Comentario!;
        s = await Svc<ServicioSolicitudes>().Rechazar(s.Id, Decisor(), motivo);
        return new(Estado(s.Estado), null);
    }

    // ───────────── Con el alcance de la llave ─────────────

    /// <summary>7. Ausencias aprobadas cuyo fin es igual o posterior a "desde" (calendario de la empresa). El canje de días no ocupa calendario.</summary>
    [HttpGet("integracion/inovaos/ausencias")]
    public async Task<List<SolicitudIo>> Ausencias([FromQuery] string? desde)
    {
        var d = Hoy.AddDays(-60);
        if (!string.IsNullOrWhiteSpace(desde) && !Fecha(desde, out d)) throw new ApiException(422, "rango_invalido", "El parámetro desde no es una fecha válida (AAAA-MM-DD).");
        var q = Yo.Alcance(Consulta(), s => s.SucursalId)
            .Where(s => s.Estado == EstadoSolicitud.Aprobada && (s.Tipo == TipoSolicitud.Vacaciones || s.Tipo == TipoSolicitud.Permiso) && s.FechaFin >= d);
        return await Lista(q.OrderBy(s => s.FechaInicio));
    }

    /// <summary>8. Colaboradores activos, para el filtro del reporte.</summary>
    [HttpGet("integracion/inovaos/colaboradores")]
    public async Task<List<ColaboradorIo>> Colaboradores()
    {
        var cs = await Yo.Alcance(Db.Colaboradores.Include(c => c.Puesto).Include(c => c.Area), c => c.SucursalId)
            .Where(c => c.Estatus == EstatusColaborador.Activo).OrderBy(c => c.Nombres).ThenBy(c => c.Apellidos).ToListAsync();
        return cs.Select(Colab).ToList();
    }

    /// <summary>9. Reporte: "desde" filtra por inicio y "hasta" por fin. "tipo" y "estado" aceptan listas separadas por comas; "colaboradorId" es el id de WOW! Time.</summary>
    [HttpGet("integracion/inovaos/solicitudes")]
    public async Task<List<SolicitudIo>> Reporte([FromQuery] string? desde, [FromQuery] string? hasta, [FromQuery] string? tipo, [FromQuery] int? colaboradorId, [FromQuery] string? estado)
    {
        var q = Yo.Alcance(Consulta(), s => s.SucursalId);
        if (!string.IsNullOrWhiteSpace(desde))
        {
            if (!Fecha(desde, out var d)) throw new ApiException(422, "rango_invalido", "El parámetro desde no es una fecha válida (AAAA-MM-DD).");
            q = q.Where(s => s.FechaInicio >= d);
        }
        if (!string.IsNullOrWhiteSpace(hasta))
        {
            if (!Fecha(hasta, out var h)) throw new ApiException(422, "rango_invalido", "El parámetro hasta no es una fecha válida (AAAA-MM-DD).");
            q = q.Where(s => s.FechaFin <= h);
        }
        if (colaboradorId is int cid) q = q.Where(s => s.ColaboradorId == cid);
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var estados = Lista(estado).Select(e => e switch
            {
                "PENDING" => EstadoSolicitud.Pendiente, "APPROVED" => EstadoSolicitud.Aprobada, "REJECTED" => EstadoSolicitud.Rechazada,
                "CANCELLED" => EstadoSolicitud.Cancelada,
                _ => throw new ApiException(422, "estado_invalido", "El estado no es válido. Usa PENDING, APPROVED, REJECTED o CANCELLED.")
            }).ToList();
            q = q.Where(s => estados.Contains(s.Estado));
        }
        var lista = await Lista(q.OrderByDescending(s => s.FechaInicio));
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            var tipos = Lista(tipo);
            if (tipos.Any(t => !TiposContrato.Contains(t) && t != "PERMIT_OTHER"))
                throw new ApiException(422, "tipo_invalido", "El tipo no es válido. Usa VACATION, PERMIT_3H, PERMIT_5H, PERMIT_DAY o CASH_OUT.");
            lista = lista.Where(s => tipos.Contains(s.Tipo)).ToList();
        }
        return lista;
    }

    // ───────────── Apoyo ─────────────

    /// <summary>Colaborador de X-Actuar-Como. 404 colaborador_no_encontrado si el correo no corresponde a nadie activo.</summary>
    private async Task<Colaborador> Persona(bool requiereColaborador = true)
    {
        if (Yo.ActuarComoNoEncontrado is string correo)
            throw new ApiException(404, "colaborador_no_encontrado", $"No hay un colaborador activo en WOW! Time con el correo {correo}.");
        if (Yo.EsLlaveApi || Yo.Canal != Canal)
            throw new ApiException(400, "falta_actuar_como", "Esta operación requiere el encabezado X-Actuar-Como con el correo del usuario.");
        if (Yo.ColaboradorId is not int cid)
        {
            if (!requiereColaborador) return null!;
            throw new ApiException(404, "colaborador_no_encontrado", "Tu usuario de WOW! Time no está ligado a un colaborador.");
        }
        var c = await Db.Colaboradores.Include(x => x.Puesto).Include(x => x.Area).FirstOrDefaultAsync(x => x.Id == cid);
        if (c is null || c.Estatus == EstatusColaborador.Baja)
            throw new ApiException(404, "colaborador_no_encontrado", "No hay un colaborador activo en WOW! Time con ese correo.");
        return c;
    }

    /// <summary>La solicitud debe existir, estar pendiente y tocarle a quien decide.</summary>
    private async Task<Solicitud> Decidible(int id)
    {
        var s = await Db.Solicitudes.Include(x => x.Pasos).FirstOrDefaultAsync(x => x.Id == id && x.Tipo != TipoSolicitud.IncidenciaAsistencia)
            ?? throw new ApiException(404, "solicitud_no_encontrada", "La solicitud no existe.");
        if (s.Estado != EstadoSolicitud.Pendiente)
            throw new ApiException(409, "ya_resuelta", $"Esta solicitud ya fue {Estado(s.Estado) switch { "APPROVED" => "aprobada", "REJECTED" => "rechazada", _ => "cancelada" }}.");
        var ev = await Svc<MotorAprobaciones>().PuedeDecidir(s, Decisor(), Hoy);
        if (!ev.Puede || !Yo.Tiene(Permisos.AprobacionesDecidir))
            throw new ApiException(403, "no_le_toca", "No te corresponde autorizar esta solicitud en este momento.");
        return s;
    }

    private IQueryable<Solicitud> Consulta() => Db.Solicitudes.AsNoTracking()
        .Where(s => s.Tipo != TipoSolicitud.IncidenciaAsistencia)
        .Include(s => s.Pasos).Include(s => s.TipoPermiso)
        .Include(s => s.Colaborador!).ThenInclude(c => c.Puesto)
        .Include(s => s.Colaborador!).ThenInclude(c => c.Area);

    private async Task<List<SolicitudIo>> Lista(IQueryable<Solicitud> q, Decisor? decisor = null, HashSet<int>? puede = null)
    {
        var sols = await q.ToListAsync();
        var hoy = Hoy;
        if (decisor is not null && puede is null)
        {
            puede = [];
            var motor = Svc<MotorAprobaciones>();
            foreach (var s in sols.Where(s => s.Estado == EstadoSolicitud.Pendiente))
                if ((await motor.PuedeDecidir(s, decisor, hoy)).Puede && Yo.Tiene(Permisos.AprobacionesDecidir)) puede.Add(s.Id);
        }
        // Titulares de los pasos y correos de quienes decidieron
        var titulares = sols.SelectMany(s => s.Pasos).Where(p => p.TitularColaboradorId != null).Select(p => p.TitularColaboradorId!.Value).Distinct().ToList();
        var personas = await Db.Colaboradores.Where(c => titulares.Contains(c.Id)).Select(c => new { c.Id, Nombre = c.Nombres + " " + c.Apellidos, c.Email }).ToDictionaryAsync(c => c.Id);
        var usuarios = sols.SelectMany(s => s.Pasos).Where(p => p.DecididoPorUsuarioId != null).Select(p => p.DecididoPorUsuarioId!.Value).Distinct().ToList();
        var correos = await Db.Usuarios.Where(u => usuarios.Contains(u.Id)).Select(u => new { u.Id, u.Email }).ToDictionaryAsync(u => u.Id, u => u.Email);

        return sols.Select(s => new SolicitudIo(
            s.Id, s.OrigenId, TipoDe(s), s.FechaInicio, s.FechaFin, null, s.DiasHabiles,
            s.Estado is EstadoSolicitud.Rechazada or EstadoSolicitud.Cancelada ? 0 : s.DiasADescontar,
            s.Comentario, Estado(s.Estado), DateTime.SpecifyKind(s.CreadoEn, DateTimeKind.Utc), Colab(s.Colaborador!),
            s.Pasos.OrderBy(p => p.Orden).Select(p =>
            {
                string? nombre, correo;
                if (p.DecididoPorUsuarioId is int du) { nombre = p.DecididoPorNombre; correo = correos.GetValueOrDefault(du); }
                else if (p.TitularColaboradorId is int t && personas.TryGetValue(t, out var per)) { nombre = per.Nombre; correo = per.Email; }
                else if (p.DecididoPorNombre is not null) { nombre = p.DecididoPorNombre; correo = null; }
                else { nombre = p.TipoAprobador == TipoAprobador.RecursosHumanos ? "Recursos Humanos" : Mapas.NombreAprobador(p.TipoAprobador); correo = null; }
                return new AprobacionIo(p.Orden, new(nombre, correo), EstadoPaso(p.Estado),
                    p.Fecha is DateTime f ? DateTime.SpecifyKind(f, DateTimeKind.Utc) : null, p.Motivo);
            }).ToList(),
            puede?.Contains(s.Id) == true)).ToList();
    }

    public static ColaboradorIo Colab(Colaborador c) => new(c.Id, c.OrigenId, c.NombreCompleto, c.Puesto?.Nombre, c.Area?.Nombre, c.Email, c.FechaIngreso);

    public static string TipoDe(Solicitud s) => s.Tipo switch
    {
        TipoSolicitud.Vacaciones => "VACATION",
        TipoSolicitud.CanjeDias => "CASH_OUT",
        TipoSolicitud.Permiso when s.TipoPermiso?.Clave is string k => k,
        // Los permisos creados por RH sin clave: los de un día completo se presentan como PERMIT_DAY en InovaOS
        _ => "PERMIT_OTHER"
    };

    public static string Estado(EstadoSolicitud e) => e switch
    {
        EstadoSolicitud.Pendiente => "PENDING", EstadoSolicitud.Aprobada => "APPROVED", EstadoSolicitud.Rechazada => "REJECTED", _ => "CANCELLED"
    };

    public static string EstadoPaso(EstadoPaso e) => e switch
    {
        Domain.EstadoPaso.Pendiente => "PENDING", Domain.EstadoPaso.EnEspera => "WAITING", Domain.EstadoPaso.Aprobado => "APPROVED",
        Domain.EstadoPaso.Rechazado => "REJECTED", _ => "SKIPPED"
    };

    /// <summary>Códigos de validación de WOW! Time con los nombres del contrato.</summary>
    public static string CodigoContrato(string codigo) => codigo switch
    {
        "fechas" or "rango" or "permiso_un_dia" or "sin_dias_habiles" => "rango_invalido",
        _ => codigo
    };

    private static bool Fecha(string? v, out DateOnly f) =>
        DateOnly.TryParseExact((v ?? "").Trim().Length >= 10 ? v!.Trim()[..10] : v ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out f);

    private static List<string> Lista(string v) => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToUpperInvariant()).ToList();
}
