using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Reglas de aprobación por tipo de solicitud y delegaciones temporales.</summary>
[Tags("Solicitudes y aprobaciones")]
public class AprobacionesController : ApiBase
{
    /// <summary>Reglas por tipo de solicitud con sus pasos.</summary>
    [HttpGet("aprobaciones/reglas")]
    public async Task<object> Reglas()
    {
        var r = await Db.Reglas.Include(x => x.Pasos).OrderBy(x => x.Id).ToListAsync();
        return r.Select(x => new
        {
            x.Id, x.Clave, x.Nombre, x.Tipos, x.SoloSolicitantesConReportes, x.Activa,
            Pasos = x.Pasos.OrderBy(p => p.Orden).Select(p => new { p.Orden, p.TipoAprobador, Etiqueta = Mapas.NombreAprobador(p.TipoAprobador) })
        });
    }

    public record ReglaDto(string Nombre, bool Activa, TipoAprobador[] Pasos);

    /// <summary>Edita pasos o estado de una regla. Desactivar deja sin ruta a ese tipo de solicitud.</summary>
    [HttpPut("aprobaciones/reglas/{id:int}"), Permiso(Permisos.AprobacionesConfigurar)]
    public async Task<object> Editar(int id, ReglaDto d)
    {
        var r = await Db.Reglas.Include(x => x.Pasos).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NoEncontrado("La regla");
        if (d.Pasos.Length is 0 or > 4) throw ApiException.Validacion("La regla debe tener de 1 a 4 pasos.");
        if (d.Pasos.Distinct().Count() != d.Pasos.Length) throw ApiException.Validacion("Un mismo tipo de aprobador no puede repetirse en la regla.");
        var avisos = new List<string>();
        if (!d.Activa && r.Activa)
        {
            var pend = await Db.Solicitudes.CountAsync(s => s.ReglaAprobacionId == id && s.Estado == EstadoSolicitud.Pendiente);
            avisos.Add($"Al desactivar esta regla, las nuevas solicitudes de {r.Nombre.ToLower()} no podrán enviarse." + (pend > 0 ? $" Hay {pend} solicitudes pendientes que conservan su ruta." : ""));
        }
        r.Nombre = d.Nombre.Trim(); r.Activa = d.Activa;
        Db.PasosRegla.RemoveRange(r.Pasos);
        r.Pasos = d.Pasos.Select((p, i) => new PasoRegla { ReglaAprobacionId = id, Orden = i + 1, TipoAprobador = p }).ToList();
        Bitacora.Registrar(null, "configuracion", "Regla de aprobación modificada", $"{r.Nombre}: {string.Join(" → ", d.Pasos.Select(Mapas.NombreAprobador))}{(d.Activa ? "" : " (inactiva)")}");
        await Db.SaveChangesAsync();
        return new { r.Id, Avisos = avisos };
    }

    /// <summary>Duplica una regla (queda inactiva para editarla).</summary>
    [HttpPost("aprobaciones/reglas/{id:int}/duplicar"), Permiso(Permisos.AprobacionesConfigurar)]
    public async Task<object> Duplicar(int id)
    {
        var r = await Db.Reglas.Include(x => x.Pasos).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NoEncontrado("La regla");
        var n = new ReglaAprobacion { Clave = $"{r.Clave}_copia_{Hash.Aleatorio(3)}", Nombre = $"{r.Nombre} (copia)", Tipos = [], SoloSolicitantesConReportes = r.SoloSolicitantesConReportes, Activa = false,
            Pasos = r.Pasos.Select(p => new PasoRegla { Orden = p.Orden, TipoAprobador = p.TipoAprobador }).ToList() };
        Db.Reglas.Add(n); await Db.SaveChangesAsync();
        return new { n.Id };
    }

    public class FiltroDelegaciones : ConsultaPaginada { public bool? Vigentes { get; set; } public int? SucursalId { get; set; } }

    /// <summary>Delegaciones vigentes y programadas (o todas con vigentes=false).</summary>
    [HttpGet("aprobaciones/delegaciones")]
    public async Task<object> Delegaciones([FromQuery] FiltroDelegaciones f)
    {
        var hoy = Hoy;
        var q = Db.Delegaciones.Include(d => d.De).ThenInclude(c => c!.Puesto).Include(d => d.Hacia).ThenInclude(c => c!.Puesto).AsQueryable();
        if (!Yo.Tiene(Permisos.AprobacionesConfigurar))
        {
            var yo = Yo.ColaboradorIdRequerido;
            q = q.Where(d => d.DeColaboradorId == yo || d.HaciaColaboradorId == yo);
        }
        else if (!Yo.TodasSucursales) { var s = Yo.Sucursales; q = q.Where(d => s.Contains(d.De!.SucursalId)); }
        if (f.SucursalId is int su) q = q.Where(d => d.De!.SucursalId == su);
        if (f.Vigentes != false) q = q.Where(d => d.TerminadaEn == null && d.FechaFin >= hoy);
        f.Orden ??= "FechaInicio";
        return await q.Pag(f, d => new
        {
            d.Id, De = Per(d.De), Hacia = Per(d.Hacia), d.FechaInicio, d.FechaFin, Periodo = ServicioSolicitudes.TextoPeriodo(d.FechaInicio, d.FechaFin), d.Tipos,
            TiposTexto = string.Join(" y ", d.Tipos.Select(t => t switch { TipoSolicitud.Vacaciones => "vacaciones", TipoSolicitud.Permiso => "permisos", TipoSolicitud.CanjeDias => "canjes de días", _ => "incidencias" })) is var tx && tx.Length > 0 ? char.ToUpper(tx[0]) + tx[1..] : "",
            Estado = d.TerminadaEn != null ? "Terminada" : d.FechaInicio > hoy ? "Programada" : d.FechaFin < hoy ? "Concluida" : "Vigente", d.TerminadaEn
        });
    }

    public record DelegacionDto(int DeColaboradorId, int HaciaColaboradorId, DateOnly FechaInicio, DateOnly FechaFin, TipoSolicitud[] Tipos);

    private async Task Validar(DelegacionDto d, int? excluir)
    {
        if (!Yo.Tiene(Permisos.AprobacionesConfigurar) && d.DeColaboradorId != Yo.ColaboradorId) throw ApiException.Prohibido("Solo puedes delegar tus propias aprobaciones.");
        if (d.DeColaboradorId == d.HaciaColaboradorId) throw ApiException.Validacion("No puedes delegar en la misma persona.");
        if (d.FechaFin < d.FechaInicio) throw ApiException.Validacion("La fecha de término no puede ser anterior a la de inicio.");
        if (d.Tipos.Length == 0) throw ApiException.Validacion("Selecciona al menos un tipo de solicitud.");
        var hacia = await Db.Colaboradores.FindAsync(d.HaciaColaboradorId) ?? throw ApiException.NoEncontrado("El delegado");
        if (hacia.Estatus != EstatusColaborador.Activo) throw ApiException.Validacion("El delegado debe estar activo.");
        var de = await Db.Colaboradores.FindAsync(d.DeColaboradorId) ?? throw ApiException.NoEncontrado("El delegante");
        if (Yo.Tiene(Permisos.AprobacionesConfigurar)) Yo.ExigirSucursal(de.SucursalId);
        if (!await Db.Usuarios.AnyAsync(u => u.ColaboradorId == d.HaciaColaboradorId && u.Activo)) throw ApiException.Validacion("El delegado no tiene usuario de acceso para aprobar.");
        var existentes = await Db.Delegaciones.Where(x => x.Id != excluir && x.DeColaboradorId == d.DeColaboradorId && x.TerminadaEn == null && x.FechaInicio <= d.FechaFin && x.FechaFin >= d.FechaInicio).ToListAsync();
        if (existentes.Any(x => x.Tipos.Intersect(d.Tipos).Any())) throw ApiException.Validacion("Ya existe una delegación para ese delegante y tipo en un periodo que se traslapa.");
        if (await Db.Delegaciones.AnyAsync(x => x.DeColaboradorId == d.HaciaColaboradorId && x.HaciaColaboradorId == d.DeColaboradorId && x.TerminadaEn == null && x.FechaInicio <= d.FechaFin && x.FechaFin >= d.FechaInicio))
            throw ApiException.Validacion("El delegado ya delegó sus aprobaciones en el delegante en ese periodo.");
    }

    /// <summary>Crea una delegación: durante el periodo los pendientes del delegante llegan al delegado.</summary>
    [HttpPost("aprobaciones/delegaciones"), Permiso(Permisos.AprobacionesConfigurar, Permisos.DelegacionesPropias)]
    public async Task<object> Crear(DelegacionDto d)
    {
        await Validar(d, null);
        var x = new Delegacion { DeColaboradorId = d.DeColaboradorId, HaciaColaboradorId = d.HaciaColaboradorId, FechaInicio = d.FechaInicio, FechaFin = d.FechaFin, Tipos = d.Tipos.Distinct().ToArray() };
        Db.Delegaciones.Add(x);
        var hacia = await Db.Colaboradores.FindAsync(d.HaciaColaboradorId);
        Bitacora.Registrar(d.DeColaboradorId, "delegacion", "Delegación de aprobaciones", $"Hacia {hacia!.NombreCompleto} · {ServicioSolicitudes.TextoPeriodo(d.FechaInicio, d.FechaFin)}");
        await Svc<Notificador>().EnviarAColaborador(d.HaciaColaboradorId, "delegacion", "Recibiste una delegación de aprobaciones",
            $"{ServicioSolicitudes.TextoPeriodo(d.FechaInicio, d.FechaFin)}", "/aprobaciones", $"del:{Guid.NewGuid():N}");
        await Db.SaveChangesAsync();
        return new { x.Id };
    }

    [HttpPut("aprobaciones/delegaciones/{id:int}"), Permiso(Permisos.AprobacionesConfigurar, Permisos.DelegacionesPropias)]
    public async Task<object> EditarDelegacion(int id, DelegacionDto d)
    {
        var x = await Db.Delegaciones.FindAsync(id) ?? throw ApiException.NoEncontrado("La delegación");
        if (x.TerminadaEn is not null) throw ApiException.Conflicto("La delegación ya terminó.");
        await Validar(d, id);
        x.DeColaboradorId = d.DeColaboradorId; x.HaciaColaboradorId = d.HaciaColaboradorId; x.FechaInicio = d.FechaInicio; x.FechaFin = d.FechaFin; x.Tipos = d.Tipos.Distinct().ToArray();
        await Db.SaveChangesAsync();
        return new { x.Id };
    }

    /// <summary>Termina la delegación antes de tiempo: los pendientes regresan al titular.</summary>
    [HttpDelete("aprobaciones/delegaciones/{id:int}"), Permiso(Permisos.AprobacionesConfigurar, Permisos.DelegacionesPropias)]
    public async Task<IActionResult> Terminar(int id)
    {
        var x = await Db.Delegaciones.FindAsync(id) ?? throw ApiException.NoEncontrado("La delegación");
        if (!Yo.Tiene(Permisos.AprobacionesConfigurar) && x.DeColaboradorId != Yo.ColaboradorId) throw ApiException.Prohibido();
        x.TerminadaEn = Reloj.Ahora;
        Bitacora.Registrar(x.DeColaboradorId, "delegacion", "Delegación terminada", "Los pendientes regresan al titular");
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
