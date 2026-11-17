using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Usuario en sesión, notificaciones, búsqueda global, archivos y eventos en tiempo real.</summary>
[Tags("Marco")]
public class MarcoController : ApiBase
{
    /// <summary>Datos del usuario, permisos, sucursales permitidas, colaborador ligado y saldo resumido.</summary>
    [HttpGet("me")]
    public async Task<object> Me()
    {
        var u = await Db.Usuarios.FindAsync(Yo.UsuarioIdRequerido) ?? throw ApiException.NoEncontrado("El usuario");
        var sucursales = Yo.TodasSucursales
            ? await Db.Sucursales.Where(s => s.Activa).OrderBy(s => s.Id).Select(s => new { s.Id, s.Nombre, s.ZonaHoraria }).ToListAsync()
            : await Db.Sucursales.Where(s => u.Sucursales.Contains(s.Id)).OrderBy(s => s.Id).Select(s => new { s.Id, s.Nombre, s.ZonaHoraria }).ToListAsync();
        object? colaborador = null;
        object? saldo = null;
        var tieneReportes = false;
        if (u.ColaboradorId is int cid)
        {
            var c = await Db.Colaboradores.Include(x => x.Puesto).Include(x => x.Area).Include(x => x.Sucursal).Include(x => x.Supervisor).FirstAsync(x => x.Id == cid);
            tieneReportes = await Db.Colaboradores.AnyAsync(x => x.SupervisorId == cid && x.Estatus != EstatusColaborador.Baja);
            colaborador = new
            {
                c.Id, c.Numero, Nombre = c.NombreCompleto, c.Nombres, c.Apellidos, Puesto = c.Puesto?.Nombre, Area = c.Area?.Nombre, c.SucursalId, Sucursal = c.Sucursal?.Nombre,
                Iniciales = Mapas.Iniciales(c.Nombres, c.Apellidos), FotoUrl = Enlaces.Archivo(c.FotoArchivoId), c.Email, c.Telefono,
                Supervisor = Per(c.Supervisor), SinSupervisor = c.SupervisorId == null, c.FechaNacimiento, c.FechaIngreso
            };
            var hoy = Zona.Hoy(Reloj, c.Sucursal?.ZonaHoraria);
            var r = await Svc<ServicioSaldos>().Resumen(cid, hoy);
            saldo = r;
        }
        return new
        {
            u.Id, u.Email, u.Nombre, u.Roles, RolesTexto = u.Roles.Select(Roles.Nombre), Permisos = Yo.PermisosSet.OrderBy(p => p),
            Sucursales = sucursales, u.DebeCambiarPassword, u.UltimoAcceso, Colaborador = colaborador, Saldo = saldo, TieneReportes = tieneReportes,
            HoraServidor = Reloj.Ahora
        };
    }

    public record PerfilDto(string? Telefono, string? Email, string? ContactoEmergenciaNombre, string? ContactoEmergenciaTelefono);

    /// <summary>Actualiza datos de contacto propios.</summary>
    [HttpPut("me")]
    public async Task<IActionResult> ActualizarPerfil(PerfilDto d)
    {
        var c = await Db.Colaboradores.FindAsync(Yo.ColaboradorIdRequerido) ?? throw ApiException.NoEncontrado("El colaborador");
        c.Telefono = d.Telefono?.Trim();
        c.ContactoEmergenciaNombre = d.ContactoEmergenciaNombre?.Trim();
        c.ContactoEmergenciaTelefono = d.ContactoEmergenciaTelefono?.Trim();
        Bitacora.Registrar(c.Id, "datos", "Se actualizaron datos de contacto", "Desde el perfil");
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Sube o cambia la foto de perfil propia.</summary>
    [HttpPost("me/foto"), Consumes("multipart/form-data")]
    public async Task<object> Foto(IFormFile archivo)
    {
        var c = await Db.Colaboradores.FindAsync(Yo.ColaboradorIdRequerido) ?? throw ApiException.NoEncontrado("El colaborador");
        if (!archivo.ContentType.StartsWith("image/")) throw ApiException.Validacion("La foto debe ser una imagen.");
        var a = await Svc<Almacen>().Guardar(archivo, "fotos", c.Id);
        await Db.SaveChangesAsync();
        c.FotoArchivoId = a.Id;
        Bitacora.Registrar(c.Id, "datos", "Se actualizó la foto de perfil", null);
        await Db.SaveChangesAsync();
        return new { fotoUrl = Enlaces.Archivo(a.Id) };
    }

    /// <summary>Mensajes de marca vigentes, por ubicación. Público para la pantalla de inicio de sesión.</summary>
    [AllowAnonymous, HttpGet("marca")]
    public async Task<Dictionary<string, string>> Marca()
    {
        var hoy = Hoy;
        var ms = await Db.MensajesMarca.Where(m => m.Activo && (m.VigenciaInicio == null || m.VigenciaInicio <= hoy) && (m.VigenciaFin == null || m.VigenciaFin >= hoy))
            .OrderByDescending(m => m.VigenciaInicio).ToListAsync();
        return ms.GroupBy(m => m.Ubicacion).ToDictionary(g => g.Key, g => g.First().Texto);
    }

    public class FiltroNotif : ConsultaPaginada { public bool? SoloNoLeidas { get; set; } }

    /// <summary>Avisos del usuario y contador de no leídos.</summary>
    [HttpGet("notificaciones")]
    public async Task<object> Notificaciones([FromQuery] FiltroNotif f)
    {
        var uid = Yo.UsuarioIdRequerido;
        var q = Db.Notificaciones.Where(n => n.UsuarioId == uid);
        if (f.SoloNoLeidas == true) q = q.Where(n => !n.Leida);
        f.Orden ??= "CreadaEn"; f.Dir ??= "desc";
        var pag = await q.Pag(f, n => new { n.Id, n.Tipo, n.Titulo, n.Mensaje, n.Enlace, n.Leida, n.CreadaEn });
        var noLeidas = await Db.Notificaciones.CountAsync(n => n.UsuarioId == uid && !n.Leida);
        return new { pag.Items, pag.Total, pag.PaginaActual, pag.Tamano, pag.Paginas, NoLeidas = noLeidas };
    }

    /// <summary>Marca un aviso como leído.</summary>
    [HttpPost("notificaciones/{id:long}/leida")]
    public async Task<IActionResult> Leida(long id)
    {
        var n = await Db.Notificaciones.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == Yo.UsuarioIdRequerido) ?? throw ApiException.NoEncontrado("La notificación");
        n.Leida = true;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Marca todos los avisos como leídos.</summary>
    [HttpPost("notificaciones/leer-todas")]
    public async Task<IActionResult> LeerTodas()
    {
        var uid = Yo.UsuarioIdRequerido;
        await Db.Notificaciones.Where(n => n.UsuarioId == uid && !n.Leida).ExecuteUpdateAsync(s => s.SetProperty(n => n.Leida, true));
        return NoContent();
    }

    /// <summary>Búsqueda global en colaboradores, reportes, solicitudes y visitantes, agrupada por tipo.</summary>
    [HttpGet("buscar")]
    public async Task<object> Buscar([FromQuery] string q, [FromQuery] int? sucursalId)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return new { colaboradores = Array.Empty<object>(), reportes = Array.Empty<object>(), solicitudes = Array.Empty<object>(), visitantes = Array.Empty<object>(), pantallas = Array.Empty<object>() };
        var patron = $"%{q}%";
        var cols = await (await ColaboradoresVisibles(sucursalId)).Include(c => c.Puesto).Include(c => c.Area)
            .Where(c => EF.Functions.ILike(EF.Functions.Unaccent(c.Nombres + " " + c.Apellidos), EF.Functions.Unaccent(patron)) || EF.Functions.ILike(c.Numero, patron) || EF.Functions.ILike(c.Email ?? "", patron))
            .OrderBy(c => c.Nombres).Take(8).ToListAsync();
        var reportes = new[]
        {
            ("asistencia", "Asistencia por periodo"), ("retardos-faltas", "Retardos y faltas"), ("vacaciones-saldos", "Vacaciones y saldos"),
            ("visitantes", "Visitantes"), ("documentos", "Documentos por vencer")
        }.Where(r => r.Item2.Normalizar().Contains(q.Normalizar()) || "reportes".Contains(q.Normalizar()))
         .Where(_ => Yo.Tiene(Permisos.ReportesSucursal) || Yo.Tiene(Permisos.ReportesEquipo))
         .Select(r => new { clave = r.Item1, nombre = r.Item2, enlace = $"/reportes?tipo={r.Item1}" }).ToList();
        object solicitudes = Array.Empty<object>();
        if (Yo.Tiene(Permisos.VacacionesGestionar))
            solicitudes = (await Yo.Alcance(Db.Solicitudes.Include(s => s.Colaborador), s => s.SucursalId, sucursalId).Where(s => EF.Functions.ILike(s.Folio, patron)).Take(5).ToListAsync())
                .Select(s => new { s.Id, s.Folio, Colaborador = s.Colaborador!.NombreCompleto, Tipo = Mapas.NombreTipoSolicitud(s.Tipo), s.Estado, enlace = $"/solicitudes/{s.Id}" }).ToList();
        object visitantes = Array.Empty<object>();
        if (Yo.Tiene(Permisos.VisitantesVer))
            visitantes = (await Yo.Alcance(Db.Invitaciones, i => i.SucursalId, sucursalId).Where(i => EF.Functions.ILike(EF.Functions.Unaccent(i.Nombre), EF.Functions.Unaccent(patron)))
                .OrderByDescending(i => i.FechaLlegada).Take(5).ToListAsync())
                .Select(i => new { i.Id, i.Nombre, i.EmpresaMotivo, i.FechaLocal, i.Estado, enlace = $"/visitantes/{i.Id}" }).ToList();
        return new { colaboradores = cols.Select(Mapas.Persona), reportes, solicitudes, visitantes };
    }

    /// <summary>Descarga o muestra un archivo. Requiere sesión, o un enlace firmado vigente emitido a un usuario con sesión.</summary>
    [AllowAnonymous, HttpGet("archivos/{id:int}/contenido")]
    [ProducesResponseType(typeof(FileResult), 200)]
    public async Task<IActionResult> Contenido(int id, [FromQuery] long? exp, [FromQuery] string? sig, [FromQuery] bool descargar = false)
    {
        var a = await Db.Archivos.FindAsync(id) ?? throw ApiException.NoEncontrado("El archivo");
        var firmado = exp is long e && Enlaces.Valida(id, e, sig);
        if (!firmado)
        {
            if (!Yo.Autenticado) throw new ApiException(401, "no_autenticado", "Tu sesión no es válida o expiró. Inicia sesión de nuevo.");
            if (a.ColaboradorId is int cid) await ColaboradorVisible(cid);
        }
        var ruta = Svc<Almacen>().RutaCompleta(a);
        if (!System.IO.File.Exists(ruta)) throw ApiException.NoEncontrado("El archivo");
        Response.Headers.CacheControl = "private, max-age=3600";
        return descargar ? PhysicalFile(ruta, a.ContentType, a.NombreOriginal) : PhysicalFile(ruta, a.ContentType);
    }

    /// <summary>Sube una foto de evidencia de asistencia. Devuelve el id para enviarlo en el registro.</summary>
    [HttpPost("archivos/evidencia"), Consumes("multipart/form-data"), Permiso(Permisos.AsistenciaRegistrar)]
    public async Task<object> Evidencia(IFormFile archivo)
    {
        if (!archivo.ContentType.StartsWith("image/")) throw ApiException.Validacion("La evidencia debe ser una foto.");
        var a = await Svc<Almacen>().Guardar(archivo, "evidencias", Yo.ColaboradorIdRequerido);
        await Db.SaveChangesAsync();
        return new { a.Id, url = Enlaces.Archivo(a.Id) };
    }

    /// <summary>Canal de eventos del servidor (Server-Sent Events) de una sucursal: checadas, solicitudes, visitas y avisos.</summary>
    [HttpGet("eventos/sucursal/{id:int}")]
    [Produces("text/event-stream")]
    public async Task Eventos(int id, CancellationToken ct)
    {
        Yo.ExigirSucursal(id);
        var canal = Svc<CanalEventos>();
        var (sid, lector) = canal.Suscribir(id, Yo.UsuarioId);
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        try
        {
            await Response.WriteAsync("event: conectado\ndata: {}\n\n", ct);
            await Response.Body.FlushAsync(ct);
            while (!ct.IsCancellationRequested)
            {
                var espera = lector.WaitToReadAsync(ct).AsTask();
                var latido = Task.Delay(TimeSpan.FromSeconds(25), ct);
                if (await Task.WhenAny(espera, latido) == latido)
                {
                    await Response.WriteAsync(": latido\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    continue;
                }
                if (!await espera) break;
                while (lector.TryRead(out var msg)) await Response.WriteAsync(msg, ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { canal.Desuscribir(sid); }
    }
}
