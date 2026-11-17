using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Solicitudes de vacaciones, permisos e incidencias, con motor de aprobaciones.</summary>
[Tags("Solicitudes y aprobaciones")]
public class SolicitudesController : ApiBase
{
    private ServicioSolicitudes S => Svc<ServicioSolicitudes>();

    private async Task<(int colaboradorId, bool capturaRh)> Solicitante(int? colaboradorId)
    {
        if (colaboradorId is int c && c != Yo.ColaboradorId)
        {
            if (!Yo.Tiene(Permisos.VacacionesGestionar)) throw ApiException.Prohibido("Solo Recursos Humanos puede capturar solicitudes a nombre de otra persona.");
            await ColaboradorVisible(c, true);
            return (c, true);
        }
        return (Yo.ColaboradorIdRequerido, false);
    }

    /// <summary>Devuelve días hábiles, saldo posterior, validaciones y ruta de aprobación. Web y móvil lo usan; ningún cliente calcula esto por su cuenta.</summary>
    [HttpPost("solicitudes/simular"), Permiso(Permisos.SolicitudesCrear, Permisos.VacacionesGestionar)]
    public async Task<Simulacion> Simular(DatosSolicitud d)
    {
        var (c, rh) = await Solicitante(d.ColaboradorId);
        return await S.Simular(d, c, rh);
    }

    /// <summary>Crea la solicitud, compromete saldo y avisa al primer aprobador. Las capturadas por RH siguen la misma regla.</summary>
    [HttpPost("solicitudes"), Permiso(Permisos.SolicitudesCrear, Permisos.VacacionesGestionar)]
    public async Task<object> Crear(DatosSolicitud d)
    {
        if (d.Tipo == TipoSolicitud.IncidenciaAsistencia) throw ApiException.Validacion("Las incidencias se justifican desde Mi asistencia.");
        var (c, rh) = await Solicitante(d.ColaboradorId);
        var s = await S.Crear(d, c, Yo.UsuarioIdRequerido, rh);
        return await Detalle(s.Id);
    }

    public class FiltroSolicitudes : ConsultaPaginada
    {
        public int? SucursalId { get; set; }
        public int? ColaboradorId { get; set; }
        public int? AreaId { get; set; }
        public int? EquipoId { get; set; }
        public TipoSolicitud? Tipo { get; set; }
        public EstadoSolicitud? Estado { get; set; }
        public DateOnly? Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        /// <summary>Solo las propias del usuario en sesión.</summary>
        public bool Mias { get; set; }
        /// <summary>Solo las pendientes cuyo paso actual es de RH.</summary>
        public bool PasoRh { get; set; }
        public string? Q { get; set; }
    }

    /// <summary>Listado por estado, tipo, periodo, área o colaborador.</summary>
    [HttpGet("solicitudes")]
    public async Task<object> Lista([FromQuery] FiltroSolicitudes f)
    {
        IQueryable<Solicitud> q = Db.Solicitudes.Include(s => s.Colaborador).ThenInclude(c => c!.Puesto).Include(s => s.Colaborador).ThenInclude(c => c!.Area)
            .Include(s => s.CoberturaColaborador).ThenInclude(c => c!.Area).Include(s => s.TipoPermiso).Include(s => s.Pasos);
        if (f.Mias) { var yo = Yo.ColaboradorIdRequerido; q = q.Where(s => s.ColaboradorId == yo); }
        else
        {
            var visibles = (await ColaboradoresVisibles(f.SucursalId)).Select(c => c.Id);
            q = q.Where(s => visibles.Contains(s.ColaboradorId));
        }
        if (f.SucursalId is int su) q = q.Where(s => s.SucursalId == su);
        if (f.ColaboradorId is int c) q = q.Where(s => s.ColaboradorId == c);
        if (f.AreaId is int a) q = q.Where(s => s.Colaborador!.AreaId == a);
        if (f.EquipoId is int e) q = q.Where(s => s.Colaborador!.EquipoId == e);
        if (f.Tipo is TipoSolicitud t) q = q.Where(s => s.Tipo == t);
        else if (!f.Mias) q = q.Where(s => s.Tipo != TipoSolicitud.IncidenciaAsistencia);
        if (f.Estado is EstadoSolicitud es) q = q.Where(s => s.Estado == es);
        if (f.Desde is DateOnly de) q = q.Where(s => s.FechaFin >= de);
        if (f.Hasta is DateOnly ha) q = q.Where(s => s.FechaInicio <= ha);
        if (f.PasoRh) q = q.Where(s => s.Estado == EstadoSolicitud.Pendiente && s.Pasos.Any(p => p.Orden == s.PasoActual && p.TipoAprobador == TipoAprobador.RecursosHumanos));
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(s => EF.Functions.ILike(EF.Functions.Unaccent(s.Colaborador!.Nombres + " " + s.Colaborador.Apellidos), EF.Functions.Unaccent($"%{f.Q}%")) || EF.Functions.ILike(s.Folio, $"%{f.Q}%"));
        f.Orden ??= "CreadoEn"; f.Dir ??= "desc";
        var pag = await q.Pag(f, s => s, "CreadoEn");
        var hoy = Hoy;
        var saldos = new Dictionary<int, decimal>();
        foreach (var cid in pag.Items.Where(s => s.Tipo == TipoSolicitud.Vacaciones).Select(s => s.ColaboradorId).Distinct())
            saldos[cid] = (await Svc<ServicioSaldos>().Resumen(cid, hoy)).Disponibles;
        var decisor = Yo.UsuarioId is int ? Decisor() : null;
        var motor = Svc<MotorAprobaciones>();
        var items = new List<object>();
        foreach (var s in pag.Items)
        {
            var puede = decisor is not null && s.Estado == EstadoSolicitud.Pendiente && (await motor.PuedeDecidir(s, decisor, hoy)).Puede;
            items.Add(Resumen(s, saldos.TryGetValue(s.ColaboradorId, out var sd) ? sd : null, puede));
        }
        return new Pagina<object>(items, pag.Total, pag.PaginaActual, pag.Tamano);
    }

    private static object Resumen(Solicitud s, decimal? saldo, bool puedeDecidir)
    {
        var paso = s.Pasos.FirstOrDefault(p => p.Orden == s.PasoActual);
        return new
        {
            s.Id, s.Folio, s.Tipo, TipoTexto = s.Tipo == TipoSolicitud.Permiso && s.TipoPermiso is not null ? s.TipoPermiso.Nombre : Mapas.NombreTipoSolicitud(s.Tipo),
            Colaborador = Per(s.Colaborador), s.FechaInicio, s.FechaFin, Periodo = ServicioSolicitudes.TextoPeriodo(s.FechaInicio, s.FechaFin), s.DiasHabiles,
            s.Estado, s.PasoActual, PasoActualTexto = s.Estado == EstadoSolicitud.Pendiente && paso is not null ? Mapas.NombreAprobador(paso.TipoAprobador) : null,
            Cobertura = Per(s.CoberturaColaborador), s.CoberturaNota, s.Comentario, s.CreadoEn, s.ResueltaEn, SaldoDisponible = saldo, PuedeDecidir = puedeDecidir, s.MotivoRechazo
        };
    }

    /// <summary>Detalle con pasos, decisiones (en nombre de quién) y bitácora.</summary>
    [HttpGet("solicitudes/{id:int}")]
    public async Task<object> Detalle(int id)
    {
        var s = await Db.Solicitudes.Include(x => x.Colaborador).ThenInclude(c => c!.Puesto).Include(x => x.Colaborador).ThenInclude(c => c!.Area)
            .Include(x => x.CoberturaColaborador).ThenInclude(c => c!.Area).Include(x => x.TipoPermiso)
            .Include(x => x.Pasos).ThenInclude(p => p.TitularColaborador).ThenInclude(c => c!.Puesto)
            .FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NoEncontrado("La solicitud");
        var esMia = s.ColaboradorId == Yo.ColaboradorId;
        var hoy = Hoy;
        var puede = Yo.UsuarioId is int && s.Estado == EstadoSolicitud.Pendiente ? await Svc<MotorAprobaciones>().PuedeDecidir(s, Decisor(), hoy) : null;
        var esAprobador = s.Pasos.Any(p => p.TitularColaboradorId == Yo.ColaboradorId || p.DecididoPorUsuarioId == Yo.UsuarioId) || puede?.Puede == true;
        if (!esMia && !esAprobador) await ColaboradorVisible(s.ColaboradorId);
        var bit = await Db.Bitacora.Where(b => b.Entidad == "Solicitud" && b.EntidadId == id).OrderBy(b => b.Fecha).ToListAsync();
        var saldo = s.Tipo == TipoSolicitud.Vacaciones ? (await Svc<ServicioSaldos>().Resumen(s.ColaboradorId, hoy)).Disponibles : (decimal?)null;
        var motor = Svc<MotorAprobaciones>();
        var pasos = new List<object>();
        foreach (var p in s.Pasos.OrderBy(p => p.Orden))
        {
            var del = p.TitularColaboradorId is int t && p.Estado == EstadoPaso.Pendiente ? await motor.DelegacionActiva(t, s.Tipo, hoy) : null;
            pasos.Add(new
            {
                p.Id, p.Orden, p.TipoAprobador, Etiqueta = Mapas.NombreAprobador(p.TipoAprobador), Titular = Per(p.TitularColaborador), DelegadoA = del is null ? null : await Db.Colaboradores.Where(c => c.Id == del.HaciaColaboradorId).Select(c => c.Nombres + " " + c.Apellidos).FirstAsync(),
                p.Estado, p.DecididoPorNombre, p.EnNombreDeNombre, p.Motivo, p.Fecha, p.NotaBitacora
            });
        }
        return new
        {
            s.Id, s.Folio, s.Tipo, TipoTexto = s.Tipo == TipoSolicitud.Permiso && s.TipoPermiso is not null ? s.TipoPermiso.Nombre : Mapas.NombreTipoSolicitud(s.Tipo), s.TipoPermisoId,
            Colaborador = Per(s.Colaborador), s.FechaInicio, s.FechaFin, Periodo = ServicioSolicitudes.TextoPeriodo(s.FechaInicio, s.FechaFin), s.DiasHabiles, s.Estado, s.PasoActual,
            s.ReglaNombre, Cobertura = Per(s.CoberturaColaborador), s.CoberturaNota, s.Comentario, s.MotivoRechazo, s.CreadoEn, s.ResueltaEn, SaldoDisponible = saldo,
            Pasos = pasos, Bitacora = bit.Select(b => new { b.Fecha, b.Titulo, b.Detalle, b.AutorNombre }),
            PuedeDecidir = puede?.Puede == true, MotivoNoDecidir = puede?.Motivo, PuedeCancelar = s.Estado == EstadoSolicitud.Pendiente && (esMia || Yo.Tiene(Permisos.VacacionesGestionar)), EsMia = esMia
        };
    }

    public record DecisionDto(string? Comentario);
    public record RechazoDto(string Motivo);

    /// <summary>Aprueba el paso actual. Al aprobar el último paso descuenta el saldo y avisa al colaborador.</summary>
    [HttpPost("solicitudes/{id:int}/aprobar"), Permiso(Permisos.AprobacionesDecidir)]
    public async Task<object> Aprobar(int id, DecisionDto? d)
    {
        await S.Aprobar(id, Decisor(), d?.Comentario);
        return await Detalle(id);
    }

    /// <summary>Rechaza la solicitud con motivo obligatorio. Libera el saldo comprometido.</summary>
    [HttpPost("solicitudes/{id:int}/rechazar"), Permiso(Permisos.AprobacionesDecidir)]
    public async Task<object> Rechazar(int id, RechazoDto d)
    {
        await S.Rechazar(id, Decisor(), d.Motivo);
        return await Detalle(id);
    }

    /// <summary>Cancela una solicitud pendiente (solicitante o RH).</summary>
    [HttpPost("solicitudes/{id:int}/cancelar")]
    public async Task<object> Cancelar(int id, DecisionDto? d)
    {
        await S.Cancelar(id, Yo.UsuarioIdRequerido, Yo.ColaboradorId, Yo.Tiene(Permisos.VacacionesGestionar), d?.Comentario);
        return await Detalle(id);
    }

    /// <summary>Bandeja de aprobaciones: solicitudes cuyo paso actual puede decidir el usuario (propias, por delegación o de RH).</summary>
    [HttpGet("aprobaciones/bandeja"), Permiso(Permisos.AprobacionesDecidir)]
    public async Task<object> Bandeja([FromQuery] int? sucursalId, [FromQuery] TipoSolicitud? tipo)
    {
        var ids = await Svc<MotorAprobaciones>().Bandeja(Decisor(), Hoy, sucursalId);
        var q = Db.Solicitudes.Include(s => s.Colaborador).ThenInclude(c => c!.Puesto).Include(s => s.Colaborador).ThenInclude(c => c!.Area)
            .Include(s => s.CoberturaColaborador).ThenInclude(c => c!.Area).Include(s => s.TipoPermiso).Include(s => s.Pasos).Where(s => ids.Contains(s.Id));
        if (tipo is TipoSolicitud t) q = q.Where(s => s.Tipo == t);
        var lista = await q.OrderBy(s => s.FechaInicio).ToListAsync();
        var hoy = Hoy;
        var res = new List<object>();
        foreach (var s in lista)
            res.Add(Resumen(s, s.Tipo == TipoSolicitud.Vacaciones ? (await Svc<ServicioSaldos>().Resumen(s.ColaboradorId, hoy)).Disponibles : null, true));
        return new { Total = res.Count, Items = res };
    }

    /// <summary>Pasos de aprobación (consulta de la entidad con filtros).</summary>
    [HttpGet("pasos-aprobacion"), Permiso(Permisos.VacacionesGestionar)]
    public async Task<object> Pasos([FromQuery] ConsultaPaginada f, [FromQuery] int? solicitudId, [FromQuery] EstadoPaso? estado)
    {
        var visibles = (await ColaboradoresVisibles()).Select(c => c.Id);
        var q = Db.PasosAprobacion.Include(p => p.Solicitud).Where(p => visibles.Contains(p.Solicitud!.ColaboradorId));
        if (solicitudId is int s) q = q.Where(p => p.SolicitudId == s);
        if (estado is EstadoPaso e) q = q.Where(p => p.Estado == e);
        return await q.Pag(f, p => new { p.Id, p.SolicitudId, p.Solicitud!.Folio, p.Orden, p.TipoAprobador, p.TitularColaboradorId, p.Estado, p.DecididoPorUsuarioId, p.DecididoPorNombre, p.EnNombreDeColaboradorId, p.EnNombreDeNombre, p.Motivo, p.Fecha, p.NotaBitacora });
    }
}
