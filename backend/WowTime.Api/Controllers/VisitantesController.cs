using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Invitaciones con QR firmado, página pública del QR y control de acceso de visitantes.</summary>
[Tags("Visitantes")]
public class VisitantesController : ApiBase
{
    private string Secreto => Enlaces.Secreto;

    public record InvitacionDto(string Nombre, string EmpresaMotivo, DateOnly Fecha, TimeOnly Hora, string? Telefono, string? Correo, int? AnfitrionColaboradorId);

    /// <summary>Crea la invitación y devuelve el QR firmado y el enlace público para compartir.</summary>
    [HttpPost("visitantes/invitaciones"), Permiso(Permisos.VisitantesRegistrar)]
    public async Task<object> Crear(InvitacionDto d)
    {
        var err = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(d.Nombre) || d.Nombre.Trim().Length < 3) err["nombre"] = "Escribe el nombre completo del visitante.";
        if (string.IsNullOrWhiteSpace(d.EmpresaMotivo)) err["empresaMotivo"] = "Escribe la empresa o el motivo de la visita.";
        if (!string.IsNullOrWhiteSpace(d.Correo) && !d.Correo.Contains('@')) err["correo"] = "El correo no es válido.";
        if (err.Count > 0) throw ApiException.Validacion(err.First().Value, err);
        var anfitrionId = d.AnfitrionColaboradorId ?? Yo.ColaboradorIdRequerido;
        if (anfitrionId != Yo.ColaboradorId && !Yo.Tiene(Permisos.VisitantesVer)) throw ApiException.Prohibido("Solo puedes registrar invitados propios.");
        var anf = await Db.Colaboradores.Include(c => c.Sucursal).FirstOrDefaultAsync(c => c.Id == anfitrionId) ?? throw ApiException.NoEncontrado("El anfitrión");
        var tz = anf.Sucursal?.ZonaHoraria;
        var local = d.Fecha.ToDateTime(d.Hora);
        var utc = Zona.AUtc(local, tz);
        if (utc < Reloj.Ahora.AddMinutes(-15)) throw ApiException.Validacion("La fecha y hora de llegada no pueden estar en el pasado.");
        var p = Svc<Parametros>();
        var (desde, hasta) = Services.Reglas.VigenciaQr(utc, await p.Entero(Parametros.QrMinAntes, 30), await p.Entero(Parametros.QrMinDespues, 120));
        var inv = new InvitacionVisitante
        {
            Nombre = d.Nombre.Trim(), EmpresaMotivo = d.EmpresaMotivo.Trim(), Telefono = d.Telefono?.Trim(), Correo = d.Correo?.Trim(), FechaLlegada = utc, FechaLocal = d.Fecha,
            AnfitrionColaboradorId = anf.Id, SucursalId = anf.SucursalId, Codigo = Hash.Aleatorio(12), VigenciaDesde = desde, VigenciaHasta = hasta
        };
        Db.Invitaciones.Add(inv);
        Bitacora.Registrar(anf.Id, "visitante", "Invitado registrado", $"{inv.Nombre} · {local:dd/MM/yyyy HH:mm}", "InvitacionVisitante", null);
        await Db.SaveChangesAsync();
        var url = (await p.Texto(Parametros.UrlPublica))?.TrimEnd('/') ?? "";
        var enlace = $"{url}/qr/{inv.Codigo}";
        if (!string.IsNullOrWhiteSpace(inv.Correo))
            await Svc<Correo>().Enviar(inv.Correo, "Tu invitación a WOW! Time",
                $"<p>Hola {inv.Nombre},</p><p>{anf.NombreCompleto} te espera el {local:dd/MM/yyyy} a las {local:HH:mm} en {anf.Sucursal?.Nombre}.</p><p>Presenta tu código QR en recepción: <a href=\"{enlace}\">{enlace}</a></p>");
        Svc<CanalEventos>().PublicarSucursal(inv.SucursalId, "visitante", new { inv.Id });
        return await Dto(inv.Id, enlace);
    }

    private async Task<object> Dto(int id, string? enlace = null)
    {
        var i = await Db.Invitaciones.Include(x => x.Anfitrion).ThenInclude(a => a!.Puesto).Include(x => x.Acceso).FirstAsync(x => x.Id == id);
        var suc = await Db.Sucursales.FindAsync(i.SucursalId);
        enlace ??= $"{(await Svc<Parametros>().Texto(Parametros.UrlPublica))?.TrimEnd('/')}/qr/{i.Codigo}";
        return new
        {
            i.Id, i.Nombre, i.EmpresaMotivo, i.Telefono, i.Correo, i.FechaLlegada, LlegadaLocal = Zona.ALocal(i.FechaLlegada, suc?.ZonaHoraria), i.FechaLocal,
            Anfitrion = Per(i.Anfitrion), i.SucursalId, Sucursal = suc?.Nombre, Estado = EstadoEfectivo(i), i.VigenciaDesde, i.VigenciaHasta,
            VigenciaDesdeLocal = Zona.ALocal(i.VigenciaDesde, suc?.ZonaHoraria), VigenciaHastaLocal = Zona.ALocal(i.VigenciaHasta, suc?.ZonaHoraria),
            Qr = Services.Reglas.ContenidoQr(i.Codigo, Secreto), Enlace = enlace,
            Entrada = i.Acceso is null ? (DateTime?)null : Zona.ALocal(i.Acceso.Entrada, suc?.ZonaHoraria), Salida = i.Acceso?.Salida is DateTime s ? Zona.ALocal(s, suc?.ZonaHoraria) : (DateTime?)null
        };
    }

    private EstadoInvitacion EstadoEfectivo(InvitacionVisitante i) => i.Estado == EstadoInvitacion.Esperado && i.VigenciaHasta < Reloj.Ahora ? EstadoInvitacion.Vencida : i.Estado;

    public class FiltroInvitaciones : ConsultaPaginada
    {
        public int? SucursalId { get; set; }
        public DateOnly? Fecha { get; set; }
        public DateOnly? Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        public int? AnfitrionId { get; set; }
        public EstadoInvitacion? Estado { get; set; }
        public bool Mias { get; set; }
        public string? Q { get; set; }
    }

    /// <summary>Invitaciones por fecha, sucursal, estado o anfitrión. Sin permiso de RH devuelve solo las propias.</summary>
    [HttpGet("visitantes/invitaciones")]
    public async Task<object> Lista([FromQuery] FiltroInvitaciones f)
    {
        var q = Db.Invitaciones.Include(i => i.Anfitrion).ThenInclude(a => a!.Puesto).Include(i => i.Acceso).AsQueryable();
        if (f.Mias || !Yo.Tiene(Permisos.VisitantesVer)) { var yo = Yo.ColaboradorIdRequerido; q = q.Where(i => i.AnfitrionColaboradorId == yo); }
        else q = Yo.Alcance(q, i => i.SucursalId, f.SucursalId);
        if (f.Fecha is DateOnly fe) q = q.Where(i => i.FechaLocal == fe);
        if (f.Desde is DateOnly de) q = q.Where(i => i.FechaLocal >= de);
        if (f.Hasta is DateOnly ha) q = q.Where(i => i.FechaLocal <= ha);
        if (f.AnfitrionId is int an) q = q.Where(i => i.AnfitrionColaboradorId == an);
        if (f.Estado is EstadoInvitacion es)
        {
            var ahora = Reloj.Ahora;
            q = es switch
            {
                EstadoInvitacion.Vencida => q.Where(i => i.Estado == EstadoInvitacion.Vencida || (i.Estado == EstadoInvitacion.Esperado && i.VigenciaHasta < ahora)),
                EstadoInvitacion.Esperado => q.Where(i => i.Estado == EstadoInvitacion.Esperado && i.VigenciaHasta >= ahora),
                _ => q.Where(i => i.Estado == es)
            };
        }
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(i => EF.Functions.ILike(EF.Functions.Unaccent(i.Nombre + " " + i.EmpresaMotivo), EF.Functions.Unaccent($"%{f.Q}%")));
        f.Orden ??= "FechaLlegada";
        var tzs = await Db.Sucursales.ToDictionaryAsync(s => s.Id, s => s.ZonaHoraria);
        return await q.Pag(f, i => new
        {
            i.Id, i.Nombre, i.EmpresaMotivo, i.FechaLocal, LlegadaLocal = Zona.ALocal(i.FechaLlegada, tzs.GetValueOrDefault(i.SucursalId)), Anfitrion = Per(i.Anfitrion), Estado = EstadoEfectivo(i),
            Entrada = i.Acceso is null ? (DateTime?)null : Zona.ALocal(i.Acceso.Entrada, tzs.GetValueOrDefault(i.SucursalId)),
            Salida = i.Acceso?.Salida is DateTime s ? Zona.ALocal(s, tzs.GetValueOrDefault(i.SucursalId)) : (DateTime?)null, i.Telefono, i.Correo
        }, "FechaLlegada", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["empresa"] = "EmpresaMotivo", ["llegada"] = "FechaLlegada", ["entrada"] = "Acceso.Entrada" });
    }

    /// <summary>Detalle de una invitación con QR, vigencia y accesos.</summary>
    [HttpGet("visitantes/invitaciones/{id:int}")]
    public async Task<object> Detalle(int id)
    {
        var i = await Db.Invitaciones.FindAsync(id) ?? throw ApiException.NoEncontrado("La invitación");
        if (i.AnfitrionColaboradorId != Yo.ColaboradorId && !(Yo.Tiene(Permisos.VisitantesVer) && Yo.PuedeVerSucursal(i.SucursalId))) throw ApiException.NoEncontrado("La invitación");
        return await Dto(id);
    }

    /// <summary>Cancela una invitación que aún no se usa.</summary>
    [HttpPost("visitantes/invitaciones/{id:int}/cancelar")]
    public async Task<object> Cancelar(int id)
    {
        var i = await Db.Invitaciones.FindAsync(id) ?? throw ApiException.NoEncontrado("La invitación");
        if (i.AnfitrionColaboradorId != Yo.ColaboradorId && !(Yo.Tiene(Permisos.VisitantesVer) && Yo.PuedeVerSucursal(i.SucursalId))) throw ApiException.Prohibido();
        if (i.Estado != EstadoInvitacion.Esperado) throw ApiException.Conflicto("Solo se pueden cancelar invitaciones en espera.");
        i.Estado = EstadoInvitacion.Cancelada;
        await Db.SaveChangesAsync();
        return await Dto(id);
    }

    /// <summary>Página pública del QR: datos mínimos para que el visitante muestre su código. No requiere sesión.</summary>
    [AllowAnonymous, HttpGet("publico/qr/{codigo}")]
    public async Task<object> Publico(string codigo)
    {
        var i = await Db.Invitaciones.Include(x => x.Anfitrion).FirstOrDefaultAsync(x => x.Codigo == codigo) ?? throw ApiException.NoEncontrado("La invitación");
        var suc = await Db.Sucursales.FindAsync(i.SucursalId);
        return new
        {
            i.Nombre, Anfitrion = i.Anfitrion?.NombreCompleto, Sucursal = suc?.Nombre, Direccion = suc?.Direccion, Llegada = Zona.ALocal(i.FechaLlegada, suc?.ZonaHoraria),
            VigenciaDesde = Zona.ALocal(i.VigenciaDesde, suc?.ZonaHoraria), VigenciaHasta = Zona.ALocal(i.VigenciaHasta, suc?.ZonaHoraria), Estado = EstadoEfectivo(i),
            Qr = Services.Reglas.ContenidoQr(i.Codigo, Secreto)
        };
    }

    public record AccesoDto(string Codigo, string? Accion);

    /// <summary>Valida el QR (o el código escrito) y registra la entrada o la salida. Avisa al anfitrión.</summary>
    [HttpPost("visitantes/acceso"), Permiso(Permisos.VisitantesAcceso)]
    public async Task<object> Acceso(AccesoDto d)
    {
        var texto = (d.Codigo ?? "").Trim();
        var codigo = Services.Reglas.LeerQr(texto, Secreto);
        if (codigo is null)
        {
            // Código escrito a mano: el identificador sin firma
            if (texto.Length >= 8 && !texto.Contains('.')) codigo = texto;
            else throw new ApiException(422, "qr_invalido", "El código QR no es válido o no fue emitido por WOW! Time.");
        }
        var i = await Db.Invitaciones.Include(x => x.Acceso).Include(x => x.Anfitrion).FirstOrDefaultAsync(x => x.Codigo == codigo)
            ?? throw new ApiException(422, "qr_invalido", "No existe una invitación con ese código.");
        Yo.ExigirSucursal(i.SucursalId);
        var ahora = Reloj.Ahora;
        string accion;
        if (i.Estado == EstadoInvitacion.Cancelada) throw new ApiException(422, "cancelada", "La invitación fue cancelada por el anfitrión.");
        if (i.Estado == EstadoInvitacion.Salio) throw new ApiException(422, "usada", "Esta invitación ya se usó: el visitante registró su salida.");
        if (i.Estado == EstadoInvitacion.Dentro || d.Accion == "salida")
        {
            if (i.Acceso is null) throw new ApiException(422, "sin_entrada", "El visitante no tiene entrada registrada.");
            i.Acceso.Salida = ahora; i.Acceso.SalidaPorUsuarioId = Yo.UsuarioId; i.Estado = EstadoInvitacion.Salio; accion = "salida";
        }
        else
        {
            if (!Services.Reglas.QrVigente(ahora, i.VigenciaDesde, i.VigenciaHasta))
            {
                var tz = (await Db.Sucursales.FindAsync(i.SucursalId))?.ZonaHoraria;
                throw new ApiException(422, "qr_fuera_vigencia", ahora < i.VigenciaDesde
                    ? $"El QR aún no es válido. Podrá usarse desde las {Zona.ALocal(i.VigenciaDesde, tz):HH:mm}."
                    : $"El QR venció a las {Zona.ALocal(i.VigenciaHasta, tz):HH:mm}. Pide al anfitrión una nueva invitación.");
            }
            i.Acceso = new AccesoVisitante { InvitacionId = i.Id, Entrada = ahora, RegistradoPorUsuarioId = Yo.UsuarioId };
            i.Estado = EstadoInvitacion.Dentro; accion = "entrada";
            await Svc<Notificador>().EnviarAColaborador(i.AnfitrionColaboradorId, "visitante_llego", $"{i.Nombre} llegó", $"{i.EmpresaMotivo} · ya está en recepción", $"/visitantes/{i.Id}", $"llego:{i.Id}");
        }
        Bitacora.Registrar(i.AnfitrionColaboradorId, "visitante", accion == "entrada" ? "Llegó un visitante" : "Salió un visitante", i.Nombre, "InvitacionVisitante", i.Id);
        await Db.SaveChangesAsync();
        Svc<CanalEventos>().PublicarSucursal(i.SucursalId, "visitante", new { i.Id, accion });
        return new { Accion = accion, Invitacion = await Dto(i.Id) };
    }

    /// <summary>Accesos de visitantes (entidad) con filtros.</summary>
    [HttpGet("visitantes/accesos"), Permiso(Permisos.VisitantesVer)]
    public async Task<object> Accesos([FromQuery] ConsultaPaginada f, [FromQuery] int? sucursalId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var q = Db.AccesosVisitante.Join(Yo.Alcance(Db.Invitaciones, i => i.SucursalId, sucursalId), a => a.InvitacionId, i => i.Id, (a, i) => new { a, i });
        if (desde is DateOnly d) q = q.Where(x => x.i.FechaLocal >= d);
        if (hasta is DateOnly h) q = q.Where(x => x.i.FechaLocal <= h);
        var total = await q.CountAsync();
        var tam = Math.Clamp(f.Tamano, 1, 500); var pag = Math.Max(1, f.Pagina);
        var items = await q.OrderByDescending(x => x.a.Entrada).Skip((pag - 1) * tam).Take(tam)
            .Select(x => new { x.a.Id, x.a.InvitacionId, x.i.Nombre, x.i.EmpresaMotivo, x.a.Entrada, x.a.Salida, x.a.DispositivoId }).ToListAsync();
        return new Pagina<object>(items.Cast<object>().ToList(), total, pag, tam);
    }

    /// <summary>Indicadores de visitantes del día.</summary>
    [HttpGet("visitantes/resumen"), Permiso(Permisos.VisitantesVer)]
    public async Task<object> Resumen([FromQuery] int? sucursalId, [FromQuery] DateOnly? fecha)
    {
        var hoy = fecha ?? await HoySucursal(sucursalId);
        var q = Yo.Alcance(Db.Invitaciones.Where(i => i.FechaLocal == hoy), i => i.SucursalId, sucursalId);
        var ahora = Reloj.Ahora;
        return new
        {
            Registrados = await q.CountAsync(i => i.Estado != EstadoInvitacion.Cancelada), Dentro = await q.CountAsync(i => i.Estado == EstadoInvitacion.Dentro),
            Esperados = await q.CountAsync(i => i.Estado == EstadoInvitacion.Esperado && i.VigenciaHasta >= ahora), Salieron = await q.CountAsync(i => i.Estado == EstadoInvitacion.Salio),
            Vencidas = await q.CountAsync(i => i.Estado == EstadoInvitacion.Vencida || (i.Estado == EstadoInvitacion.Esperado && i.VigenciaHasta < ahora))
        };
    }
}
