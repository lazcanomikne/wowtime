using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Expediente digital: documentos requeridos, carga, vigencias, avance y documentos por vencer.</summary>
[Tags("Expedientes")]
public class ExpedientesController : ApiBase
{
    private async Task<(int urg, int prox)> Umbrales()
    {
        var p = Svc<Parametros>();
        return (await p.Entero(Parametros.DocUrgente, 7), await p.Entero(Parametros.DocProximo, 30));
    }

    private async Task ExigirExpediente(int colaboradorId, bool escribir)
    {
        if (Yo.ColaboradorId == colaboradorId && Yo.Tiene(Permisos.ExpedientePropio)) return;
        var c = await ColaboradorVisible(colaboradorId, true);
        if (!Yo.Tiene(Permisos.ExpedientesGestionar)) throw ApiException.Prohibido("No tienes acceso a expedientes de otros colaboradores.");
    }

    /// <summary>Documentos del colaborador, documentos requeridos por su puesto y avance del expediente.</summary>
    [HttpGet("colaboradores/{id:int}/documentos")]
    public async Task<object> Documentos(int id, [FromQuery] bool incluirReemplazados = false)
    {
        await ExigirExpediente(id, false);
        var c = await Db.Colaboradores.Include(x => x.Puesto).Include(x => x.Sucursal).FirstAsync(x => x.Id == id);
        var hoy = Zona.Hoy(Reloj, c.Sucursal?.ZonaHoraria);
        var (urg, prox) = await Umbrales();
        var requeridos = await Db.PuestoDocumentos.Include(p => p.TipoDocumento).Where(p => p.PuestoId == c.PuestoId && p.TipoDocumento!.Activo).Select(p => p.TipoDocumento!).OrderBy(t => t.Id).ToListAsync();
        var q = Db.Documentos.Include(d => d.TipoDocumento).Include(d => d.Archivo).Where(d => d.ColaboradorId == id);
        if (!incluirReemplazados) q = q.Where(d => !d.Reemplazado);
        var docs = await q.OrderByDescending(d => d.CreadoEn).ToListAsync();
        var autores = await Db.Usuarios.Where(u => docs.Select(d => d.CreadoPor).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre);
        object Doc(Documento d) => new
        {
            d.Id, d.TipoDocumentoId, Tipo = d.TipoDocumento!.Nombre, d.FechaVencimiento, d.Reemplazado, d.Notas, Cargado = d.CreadoEn,
            CargadoPor = d.CreadoPor is int u ? autores.GetValueOrDefault(u) : null, Archivo = d.Archivo!.NombreOriginal, d.Archivo.ContentType, d.Archivo.Tamano,
            Url = Enlaces.Archivo(d.ArchivoId, 30), Descarga = Enlaces.Archivo(d.ArchivoId, 30) + "&descargar=true",
            Estado = Services.Reglas.EstadoDocumento(d.FechaVencimiento, hoy, urg, prox), Dias = d.FechaVencimiento is DateOnly v ? v.DayNumber - hoy.DayNumber : (int?)null
        };
        var entregados = docs.Where(d => !d.Reemplazado).Select(d => d.TipoDocumentoId).Distinct().Count(t => requeridos.Any(r => r.Id == t));
        return new
        {
            Colaborador = Mapas.Persona(c), Requeridos = requeridos.Count, Entregados = entregados,
            Porcentaje = requeridos.Count == 0 ? 100 : (int)Math.Round(entregados * 100.0 / requeridos.Count),
            Lista = requeridos.Select(t => new { TipoDocumentoId = t.Id, t.Nombre, t.Descripcion, t.ConVigencia, t.Obligatorio, Documento = docs.Where(d => d.TipoDocumentoId == t.Id && !d.Reemplazado).Select(Doc).FirstOrDefault() }),
            Adicionales = docs.Where(d => requeridos.All(r => r.Id != d.TipoDocumentoId) || d.Reemplazado).Select(Doc)
        };
    }

    /// <summary>Carga un documento con su vigencia. Si ya existía uno del mismo tipo, el anterior queda como reemplazado.</summary>
    [HttpPost("colaboradores/{id:int}/documentos"), Consumes("multipart/form-data")]
    public async Task<object> Cargar(int id, [FromForm] int tipoDocumentoId, [FromForm] DateOnly? fechaVencimiento, [FromForm] string? notas, IFormFile archivo)
    {
        await ExigirExpediente(id, true);
        var tipo = await Db.TiposDocumento.FindAsync(tipoDocumentoId) ?? throw ApiException.NoEncontrado("El tipo de documento");
        if (tipo.ConVigencia && fechaVencimiento is null) throw ApiException.Validacion($"El documento {tipo.Nombre} requiere fecha de vencimiento.");
        var a = await Svc<Almacen>().Guardar(archivo, "expedientes", id);
        var anteriores = await Db.Documentos.Where(d => d.ColaboradorId == id && d.TipoDocumentoId == tipoDocumentoId && !d.Reemplazado).ToListAsync();
        foreach (var x in anteriores) x.Reemplazado = true;
        var doc = new Documento { ColaboradorId = id, TipoDocumentoId = tipoDocumentoId, Archivo = a, FechaVencimiento = tipo.ConVigencia ? fechaVencimiento : fechaVencimiento, Notas = notas };
        Db.Documentos.Add(doc);
        Bitacora.Registrar(id, "documento", anteriores.Count > 0 ? "Se actualizó el documento" : "Se agregó un documento", tipo.Nombre, "Documento", null);
        await Db.SaveChangesAsync();
        // Avisos inmediatos de vencimiento
        var hoy = Hoy;
        var (urg, prox) = await Umbrales();
        var estado = Services.Reglas.EstadoDocumento(doc.FechaVencimiento, hoy, urg, prox);
        if (estado != EstadoDocumento.Vigente)
        {
            var c = await Db.Colaboradores.FindAsync(id);
            var dias = doc.FechaVencimiento!.Value.DayNumber - hoy.DayNumber;
            var texto = dias < 0 ? $"venció hace {-dias} días" : $"vence en {dias} días";
            await Svc<Notificador>().EnviarAColaborador(id, "documento_por_vencer", $"{tipo.Nombre} {texto}", "Renueva el documento para evitar inconvenientes.", "/mi-expediente", $"doc:{doc.Id}:{estado}");
            await Svc<Notificador>().EnviarARol(Roles.RecursosHumanos, c!.SucursalId, "documento_por_vencer", $"{tipo.Nombre} de {c.Nombres} {c.Apellidos} {texto}", null, $"/expedientes?colaborador={id}", $"doc:{doc.Id}:{estado}", Yo.UsuarioId is int u ? [u] : null);
            await Db.SaveChangesAsync();
        }
        return new { doc.Id, Estado = estado };
    }

    public record DocumentoDto(DateOnly? FechaVencimiento, string? Notas);
    /// <summary>Corrige la vigencia o las notas de un documento.</summary>
    [HttpPut("documentos/{id:int}"), Permiso(Permisos.ExpedientesGestionar)]
    public async Task<object> Editar(int id, DocumentoDto d)
    {
        var doc = await Db.Documentos.Include(x => x.TipoDocumento).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NoEncontrado("El documento");
        await ExigirExpediente(doc.ColaboradorId, true);
        doc.FechaVencimiento = d.FechaVencimiento; doc.Notas = d.Notas;
        Bitacora.Registrar(doc.ColaboradorId, "documento", "Se actualizó la vigencia del documento", $"{doc.TipoDocumento!.Nombre}: {d.FechaVencimiento:dd/MM/yyyy}");
        await Db.SaveChangesAsync();
        return new { doc.Id };
    }

    [HttpDelete("documentos/{id:int}"), Permiso(Permisos.ExpedientesGestionar)]
    public async Task<IActionResult> Borrar(int id)
    {
        var doc = await Db.Documentos.Include(x => x.TipoDocumento).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NoEncontrado("El documento");
        await ExigirExpediente(doc.ColaboradorId, true);
        Db.Documentos.Remove(doc);
        Bitacora.Registrar(doc.ColaboradorId, "documento", "Se eliminó un documento", doc.TipoDocumento!.Nombre);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    public class FiltroPorVencer : ConsultaPaginada { public int? SucursalId { get; set; } public int? Dias { get; set; } public int? TipoDocumentoId { get; set; } public bool IncluirVencidos { get; set; } = true; }

    /// <summary>Documentos próximos a vencer (y vencidos), ordenados por fecha de vencimiento.</summary>
    [HttpGet("expedientes/por-vencer"), Permiso(Permisos.ExpedientesGestionar)]
    public async Task<object> PorVencer([FromQuery] FiltroPorVencer f)
    {
        var hoy = await HoySucursal(f.SucursalId);
        var (urg, prox) = await Umbrales();
        var limite = hoy.AddDays(f.Dias ?? prox);
        var ids = (await ColaboradoresVisibles(f.SucursalId)).Where(c => c.Estatus == EstatusColaborador.Activo).Select(c => c.Id);
        var q = Db.Documentos.Include(d => d.TipoDocumento).Include(d => d.Colaborador).ThenInclude(c => c!.Puesto)
            .Where(d => ids.Contains(d.ColaboradorId) && !d.Reemplazado && d.FechaVencimiento != null && d.FechaVencimiento <= limite);
        if (!f.IncluirVencidos) q = q.Where(d => d.FechaVencimiento >= hoy);
        if (f.TipoDocumentoId is int t) q = q.Where(d => d.TipoDocumentoId == t);
        f.Orden ??= "FechaVencimiento";
        return await q.Pag(f, d => new
        {
            d.Id, Documento = d.TipoDocumento!.Nombre, Colaborador = Per(d.Colaborador), d.FechaVencimiento, Dias = d.FechaVencimiento!.Value.DayNumber - hoy.DayNumber,
            Estado = Services.Reglas.EstadoDocumento(d.FechaVencimiento, hoy, urg, prox)
        });
    }

    public class FiltroAvance : ConsultaPaginada { public int? SucursalId { get; set; } public int? AreaId { get; set; } public string? Q { get; set; } public bool SoloIncompletos { get; set; } }

    /// <summary>Avance del expediente por colaborador (listado general).</summary>
    [HttpGet("expedientes"), Permiso(Permisos.ExpedientesGestionar)]
    public async Task<object> Avance([FromQuery] FiltroAvance f)
    {
        var hoy = await HoySucursal(f.SucursalId);
        var (urg, prox) = await Umbrales();
        var q = (await ColaboradoresVisibles(f.SucursalId)).Include(c => c.Puesto).Include(c => c.Area).Where(c => c.Estatus == EstatusColaborador.Activo);
        if (f.AreaId is int a) q = q.Where(c => c.AreaId == a);
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(c => EF.Functions.ILike(EF.Functions.Unaccent(c.Nombres + " " + c.Apellidos), EF.Functions.Unaccent($"%{f.Q}%")) || EF.Functions.ILike(c.Numero, $"%{f.Q}%"));
        var cols = await q.ToListAsync();
        var ids = cols.Select(c => c.Id).ToList();
        var req = (await Db.PuestoDocumentos.ToListAsync()).ToLookup(p => p.PuestoId, p => p.TipoDocumentoId);
        var docs = (await Db.Documentos.Where(d => ids.Contains(d.ColaboradorId) && !d.Reemplazado).Select(d => new { d.ColaboradorId, d.TipoDocumentoId, d.FechaVencimiento }).ToListAsync()).ToLookup(d => d.ColaboradorId);
        var filas = cols.Select(c =>
        {
            var r = req[c.PuestoId ?? 0].ToList();
            var mios = docs[c.Id].ToList();
            var ent = mios.Select(d => d.TipoDocumentoId).Distinct().Count(r.Contains);
            var estados = mios.Select(d => Services.Reglas.EstadoDocumento(d.FechaVencimiento, hoy, urg, prox)).ToList();
            return new
            {
                Colaborador = Mapas.Persona(c), c.Nombres, Requeridos = r.Count, Entregados = ent, Porcentaje = r.Count == 0 ? 100 : (int)Math.Round(ent * 100.0 / r.Count),
                Vencidos = estados.Count(e => e == EstadoDocumento.Vencido), Urgentes = estados.Count(e => e == EstadoDocumento.Urgente), Proximos = estados.Count(e => e == EstadoDocumento.Proximo)
            };
        });
        if (f.SoloIncompletos) filas = filas.Where(x => x.Entregados < x.Requeridos || x.Vencidos > 0);
        var desc = string.Equals(f.Dir, "desc", StringComparison.OrdinalIgnoreCase);
        filas = (f.Orden ?? "").ToLowerInvariant() switch
        {
            "porcentaje" => desc ? filas.OrderByDescending(x => x.Porcentaje) : filas.OrderBy(x => x.Porcentaje),
            "alertas" => filas.OrderByDescending(x => x.Vencidos * 100 + x.Urgentes * 10 + x.Proximos),
            _ => desc ? filas.OrderByDescending(x => x.Nombres) : filas.OrderBy(x => x.Nombres)
        };
        var lista = filas.ToList();
        var tam = Math.Clamp(f.Tamano, 1, 500); var pag = Math.Max(1, f.Pagina);
        var totalReq = lista.Sum(x => x.Requeridos); var totalEnt = lista.Sum(x => x.Entregados);
        return new
        {
            Items = lista.Skip((pag - 1) * tam).Take(tam), Total = lista.Count, PaginaActual = pag, Tamano = tam, Paginas = (int)Math.Ceiling(lista.Count / (double)tam),
            Resumen = new { Colaboradores = lista.Count, Completos = lista.Count(x => x.Entregados >= x.Requeridos), Porcentaje = totalReq == 0 ? 100 : (int)Math.Round(totalEnt * 100.0 / totalReq),
                Vencidos = lista.Sum(x => x.Vencidos), Urgentes = lista.Sum(x => x.Urgentes), Proximos = lista.Sum(x => x.Proximos) }
        };
    }

    /// <summary>Documentos (entidad) con filtros, paginación y orden.</summary>
    [HttpGet("documentos"), Permiso(Permisos.ExpedientesGestionar)]
    public async Task<object> Entidad([FromQuery] ConsultaPaginada f, [FromQuery] int? colaboradorId, [FromQuery] int? tipoDocumentoId, [FromQuery] int? sucursalId)
    {
        var ids = (await ColaboradoresVisibles(sucursalId)).Select(c => c.Id);
        var q = Db.Documentos.Include(d => d.TipoDocumento).Where(d => ids.Contains(d.ColaboradorId));
        if (colaboradorId is int c) q = q.Where(d => d.ColaboradorId == c);
        if (tipoDocumentoId is int t) q = q.Where(d => d.TipoDocumentoId == t);
        return await q.Pag(f, d => new { d.Id, d.ColaboradorId, d.TipoDocumentoId, Tipo = d.TipoDocumento!.Nombre, d.FechaVencimiento, d.Reemplazado, d.CreadoEn, Url = Enlaces.Archivo(d.ArchivoId, 30) });
    }
}
