using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

public class FiltroCatalogo : ConsultaPaginada
{
    public string? Q { get; set; }
    public int? SucursalId { get; set; }
    public bool? Activos { get; set; }
}

/// <summary>Catálogos de organización: empresa, sucursales, áreas, equipos, puestos, turnos y días festivos.</summary>
[Tags("Organización")]
public class OrganizacionCatalogosController : ApiBase
{
    // ───── Empresa ─────
    /// <summary>Datos de la empresa.</summary>
    [HttpGet("empresa")]
    public async Task<Empresa> Empresa() => await Db.Empresas.FirstAsync();

    public record EmpresaDto(string Nombre, string ZonaHoraria, string PrefijoNumero);
    [HttpPut("empresa"), Permiso(Permisos.SistemaConfigurar)]
    public async Task<Empresa> EditarEmpresa(EmpresaDto d)
    {
        var e = await Db.Empresas.FirstAsync();
        e.Nombre = d.Nombre.Trim(); e.ZonaHoraria = d.ZonaHoraria; e.PrefijoNumero = d.PrefijoNumero;
        await Db.SaveChangesAsync();
        return e;
    }

    // ───── Sucursales ─────
    /// <summary>Sucursales visibles para el usuario (selector de sucursal).</summary>
    [HttpGet("sucursales")]
    public async Task<Pagina<Sucursal>> Sucursales([FromQuery] FiltroCatalogo f)
    {
        var q = Db.Sucursales.AsQueryable();
        if (!Yo.TodasSucursales) { var s = Yo.Sucursales; q = q.Where(x => s.Contains(x.Id)); }
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(x => EF.Functions.ILike(x.Nombre, $"%{f.Q}%"));
        if (f.Activos == true) q = q.Where(x => x.Activa);
        return await q.Paginar(f, x => x, "Id");
    }

    public record SucursalDto(string Nombre, string? Direccion, double Latitud, double Longitud, int RadioMetros, string ZonaHoraria, bool Activa = true);

    [HttpPost("sucursales"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<Sucursal> CrearSucursal(SucursalDto d)
    {
        var s = new Sucursal { EmpresaId = (await Db.Empresas.FirstAsync()).Id };
        Aplicar(s, d);
        Db.Sucursales.Add(s);
        await Db.SaveChangesAsync();
        return s;
    }

    [HttpPut("sucursales/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<Sucursal> EditarSucursal(int id, SucursalDto d)
    {
        var s = await Db.Sucursales.FindAsync(id) ?? throw ApiException.NoEncontrado("La sucursal");
        Yo.ExigirSucursal(id);
        Aplicar(s, d);
        await Db.SaveChangesAsync();
        return s;
    }

    private static void Aplicar(Sucursal s, SucursalDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Nombre)) throw ApiException.Validacion("El nombre es obligatorio.");
        if (d.RadioMetros is < 20 or > 5000) throw ApiException.Validacion("El radio de geocerca debe estar entre 20 y 5000 metros.");
        Zona.Get(d.ZonaHoraria);
        s.Nombre = d.Nombre.Trim(); s.Direccion = d.Direccion; s.Latitud = d.Latitud; s.Longitud = d.Longitud; s.RadioMetros = d.RadioMetros; s.ZonaHoraria = d.ZonaHoraria; s.Activa = d.Activa;
    }

    // ───── Áreas ─────
    [HttpGet("areas")]
    public async Task<object> Areas([FromQuery] FiltroCatalogo f)
    {
        var q = Yo.Alcance(Db.Areas.AsQueryable(), a => a.SucursalId, f.SucursalId);
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(x => EF.Functions.ILike(x.Nombre, $"%{f.Q}%"));
        if (f.Activos == true) q = q.Where(x => x.Activa);
        f.Orden ??= "Nombre";
        return await q.Paginar(f, a => new { a.Id, a.Nombre, a.SucursalId, a.Activa, Colaboradores = Db.Colaboradores.Count(c => c.AreaId == a.Id && c.Estatus != EstatusColaborador.Baja) });
    }

    public record AreaDto(string Nombre, int SucursalId, bool Activa = true);
    [HttpPost("areas"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<Area> CrearArea(AreaDto d)
    {
        Yo.ExigirSucursal(d.SucursalId);
        var a = new Area { Nombre = d.Nombre.Trim(), SucursalId = d.SucursalId, Activa = d.Activa };
        Db.Areas.Add(a); await Db.SaveChangesAsync(); return a;
    }
    [HttpPut("areas/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<Area> EditarArea(int id, AreaDto d)
    {
        var a = await Db.Areas.FindAsync(id) ?? throw ApiException.NoEncontrado("El área");
        Yo.ExigirSucursal(a.SucursalId); Yo.ExigirSucursal(d.SucursalId);
        a.Nombre = d.Nombre.Trim(); a.SucursalId = d.SucursalId; a.Activa = d.Activa;
        await Db.SaveChangesAsync(); return a;
    }
    [HttpDelete("areas/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<IActionResult> BorrarArea(int id)
    {
        var a = await Db.Areas.FindAsync(id) ?? throw ApiException.NoEncontrado("El área");
        Yo.ExigirSucursal(a.SucursalId);
        if (await Db.Colaboradores.AnyAsync(c => c.AreaId == id)) throw ApiException.Conflicto("El área tiene colaboradores; desactívala en lugar de eliminarla.");
        Db.Areas.Remove(a); await Db.SaveChangesAsync(); return NoContent();
    }

    // ───── Equipos ─────
    [HttpGet("equipos")]
    public async Task<object> Equipos([FromQuery] FiltroCatalogo f, [FromQuery] int? areaId)
    {
        var q = Yo.Alcance(Db.Equipos.Include(e => e.Area).AsQueryable(), e => e.Area!.SucursalId, f.SucursalId);
        if (areaId is int ar) q = q.Where(e => e.AreaId == ar);
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(x => EF.Functions.ILike(x.Nombre, $"%{f.Q}%"));
        if (f.Activos == true) q = q.Where(x => x.Activo);
        f.Orden ??= "Nombre";
        return await q.Paginar(f, e => new { e.Id, e.Nombre, e.AreaId, Area = e.Area!.Nombre, e.Area.SucursalId, e.CoberturaMinima, e.Activo, Miembros = Db.Colaboradores.Count(c => c.EquipoId == e.Id && c.Estatus == EstatusColaborador.Activo) });
    }

    public record EquipoDto(string Nombre, int AreaId, int CoberturaMinima = 70, bool Activo = true);
    [HttpPost("equipos"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<Equipo> CrearEquipo(EquipoDto d)
    {
        var area = await Db.Areas.FindAsync(d.AreaId) ?? throw ApiException.NoEncontrado("El área");
        Yo.ExigirSucursal(area.SucursalId);
        if (d.CoberturaMinima is < 0 or > 100) throw ApiException.Validacion("La cobertura mínima debe estar entre 0 y 100%.");
        var e = new Equipo { Nombre = d.Nombre.Trim(), AreaId = d.AreaId, CoberturaMinima = d.CoberturaMinima, Activo = d.Activo };
        Db.Equipos.Add(e); await Db.SaveChangesAsync(); return e;
    }
    [HttpPut("equipos/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<Equipo> EditarEquipo(int id, EquipoDto d)
    {
        var e = await Db.Equipos.Include(x => x.Area).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NoEncontrado("El equipo");
        Yo.ExigirSucursal(e.Area!.SucursalId);
        if (d.CoberturaMinima is < 0 or > 100) throw ApiException.Validacion("La cobertura mínima debe estar entre 0 y 100%.");
        e.Nombre = d.Nombre.Trim(); e.AreaId = d.AreaId; e.CoberturaMinima = d.CoberturaMinima; e.Activo = d.Activo;
        await Db.SaveChangesAsync(); return e;
    }
    [HttpDelete("equipos/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<IActionResult> BorrarEquipo(int id)
    {
        var e = await Db.Equipos.FindAsync(id) ?? throw ApiException.NoEncontrado("El equipo");
        if (await Db.Colaboradores.AnyAsync(c => c.EquipoId == id)) throw ApiException.Conflicto("El equipo tiene miembros; desactívalo en lugar de eliminarlo.");
        Db.Equipos.Remove(e); await Db.SaveChangesAsync(); return NoContent();
    }

    // ───── Puestos ─────
    [HttpGet("puestos")]
    public async Task<object> Puestos([FromQuery] FiltroCatalogo f)
    {
        var q = Db.Puestos.Include(p => p.DocumentosRequeridos).AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(x => EF.Functions.ILike(x.Nombre, $"%{f.Q}%"));
        if (f.Activos == true) q = q.Where(x => x.Activo);
        f.Orden ??= "Nivel";
        return await q.Pag(f, p => new { p.Id, p.Nombre, p.Nivel, p.Activo, DocumentosRequeridos = p.DocumentosRequeridos.Select(d => d.TipoDocumentoId).ToArray() });
    }

    public record PuestoDto(string Nombre, int Nivel, bool Activo, int[] DocumentosRequeridos);
    [HttpPost("puestos"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<object> CrearPuesto(PuestoDto d)
    {
        var p = new Puesto { Nombre = d.Nombre.Trim(), Nivel = d.Nivel, Activo = d.Activo, DocumentosRequeridos = d.DocumentosRequeridos.Distinct().Select(t => new PuestoDocumento { TipoDocumentoId = t }).ToList() };
        Db.Puestos.Add(p); await Db.SaveChangesAsync(); return new { p.Id };
    }
    [HttpPut("puestos/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<object> EditarPuesto(int id, PuestoDto d)
    {
        var p = await Db.Puestos.Include(x => x.DocumentosRequeridos).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NoEncontrado("El puesto");
        p.Nombre = d.Nombre.Trim(); p.Nivel = d.Nivel; p.Activo = d.Activo;
        p.DocumentosRequeridos.Clear();
        foreach (var t in d.DocumentosRequeridos.Distinct()) p.DocumentosRequeridos.Add(new PuestoDocumento { PuestoId = id, TipoDocumentoId = t });
        await Db.SaveChangesAsync(); return new { p.Id };
    }
    [HttpDelete("puestos/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<IActionResult> BorrarPuesto(int id)
    {
        var p = await Db.Puestos.FindAsync(id) ?? throw ApiException.NoEncontrado("El puesto");
        if (await Db.Colaboradores.AnyAsync(c => c.PuestoId == id)) throw ApiException.Conflicto("El puesto está asignado a colaboradores; desactívalo en lugar de eliminarlo.");
        Db.Puestos.Remove(p); await Db.SaveChangesAsync(); return NoContent();
    }

    // ───── Turnos ─────
    [HttpGet("turnos")]
    public async Task<Pagina<Turno>> Turnos([FromQuery] FiltroCatalogo f)
    {
        var q = Db.Turnos.AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(x => EF.Functions.ILike(x.Nombre, $"%{f.Q}%"));
        if (f.Activos == true) q = q.Where(x => x.Activo);
        f.Orden ??= "HoraEntrada";
        return await q.Paginar(f, t => t);
    }

    public record TurnoDto(string Nombre, TimeOnly HoraEntrada, TimeOnly HoraSalida, int ToleranciaMinutos, int[] DiasDescanso, bool Activo = true);
    [HttpPost("turnos"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<Turno> CrearTurno(TurnoDto d) { var t = new Turno(); AplicarTurno(t, d); Db.Turnos.Add(t); await Db.SaveChangesAsync(); return t; }
    [HttpPut("turnos/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<Turno> EditarTurno(int id, TurnoDto d) { var t = await Db.Turnos.FindAsync(id) ?? throw ApiException.NoEncontrado("El turno"); AplicarTurno(t, d); await Db.SaveChangesAsync(); return t; }
    [HttpDelete("turnos/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<IActionResult> BorrarTurno(int id)
    {
        var t = await Db.Turnos.FindAsync(id) ?? throw ApiException.NoEncontrado("El turno");
        if (await Db.Colaboradores.AnyAsync(c => c.TurnoId == id)) throw ApiException.Conflicto("El turno está asignado a colaboradores; desactívalo en lugar de eliminarlo.");
        Db.Turnos.Remove(t); await Db.SaveChangesAsync(); return NoContent();
    }
    private static void AplicarTurno(Turno t, TurnoDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Nombre)) throw ApiException.Validacion("El nombre es obligatorio.");
        if (d.DiasDescanso.Any(x => x is < 1 or > 7)) throw ApiException.Validacion("Los días de descanso van de 1 (lunes) a 7 (domingo).");
        if (d.DiasDescanso.Distinct().Count() >= 7) throw ApiException.Validacion("El turno debe tener al menos un día laborable.");
        if (d.ToleranciaMinutos is < 0 or > 120) throw ApiException.Validacion("La tolerancia debe estar entre 0 y 120 minutos.");
        t.Nombre = d.Nombre.Trim(); t.HoraEntrada = d.HoraEntrada; t.HoraSalida = d.HoraSalida; t.ToleranciaMinutos = d.ToleranciaMinutos; t.DiasDescanso = d.DiasDescanso.Distinct().Order().ToArray(); t.Activo = d.Activo;
    }

    // ───── Días festivos ─────
    [HttpGet("dias-festivos")]
    public async Task<Pagina<DiaFestivo>> Festivos([FromQuery] FiltroCatalogo f, [FromQuery] int? anio)
    {
        var q = Db.DiasFestivos.AsQueryable();
        if (anio is int a) q = q.Where(x => x.Fecha.Year == a);
        if (f.SucursalId is int s) q = q.Where(x => x.SucursalId == null || x.SucursalId == s);
        f.Orden ??= "Fecha";
        return await q.Paginar(f, x => x);
    }

    public record FestivoDto(DateOnly Fecha, string Nombre, int? SucursalId);
    [HttpPost("dias-festivos"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<DiaFestivo> CrearFestivo(FestivoDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Nombre)) throw ApiException.Validacion("El nombre es obligatorio.");
        if (d.SucursalId is int s) Yo.ExigirSucursal(s);
        var x = new DiaFestivo { Fecha = d.Fecha, Nombre = d.Nombre.Trim(), SucursalId = d.SucursalId };
        Db.DiasFestivos.Add(x); await Db.SaveChangesAsync(); return x;
    }
    [HttpPut("dias-festivos/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<DiaFestivo> EditarFestivo(int id, FestivoDto d)
    {
        var x = await Db.DiasFestivos.FindAsync(id) ?? throw ApiException.NoEncontrado("El día festivo");
        x.Fecha = d.Fecha; x.Nombre = d.Nombre.Trim(); x.SucursalId = d.SucursalId;
        await Db.SaveChangesAsync(); return x;
    }
    [HttpDelete("dias-festivos/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<IActionResult> BorrarFestivo(int id)
    {
        var x = await Db.DiasFestivos.FindAsync(id) ?? throw ApiException.NoEncontrado("El día festivo");
        Db.DiasFestivos.Remove(x); await Db.SaveChangesAsync(); return NoContent();
    }
}
