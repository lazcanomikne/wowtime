using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Data;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[ProducesResponseType(typeof(ErrorRespuesta), 401)]
[ProducesResponseType(typeof(ErrorRespuesta), 403)]
[ProducesResponseType(typeof(ErrorRespuesta), 422)]
public abstract class ApiBase : ControllerBase
{
    protected AppDb Db => HttpContext.RequestServices.GetRequiredService<AppDb>();
    protected UsuarioActual Yo => HttpContext.RequestServices.GetRequiredService<UsuarioActual>();
    protected IReloj Reloj => HttpContext.RequestServices.GetRequiredService<IReloj>();
    protected Bitacora Bitacora => HttpContext.RequestServices.GetRequiredService<Bitacora>();
    protected T Svc<T>() where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();
    protected DateOnly Hoy => Zona.Hoy(Reloj, "America/Monterrey");

    protected async Task<DateOnly> HoySucursal(int? sucursalId)
    {
        if (sucursalId is not int s) return Hoy;
        var tz = await Db.Sucursales.Where(x => x.Id == s).Select(x => x.ZonaHoraria).FirstOrDefaultAsync();
        return Zona.Hoy(Reloj, tz);
    }

    protected Decisor Decisor() => new(Yo.UsuarioIdRequerido, Yo.ColaboradorId, Yo.Roles, Yo.Sucursales, Yo.TodasSucursales, Yo.Nombre);

    /// <summary>IDs de colaboradores que reportan (directa o indirectamente) al colaborador dado.</summary>
    protected async Task<HashSet<int>> Subordinados(int colaboradorId)
    {
        var pares = await Db.Colaboradores.Where(c => c.SupervisorId != null).Select(c => new { c.Id, Sup = c.SupervisorId!.Value }).ToListAsync();
        var hijos = pares.ToLookup(p => p.Sup, p => p.Id);
        var res = new HashSet<int>();
        var cola = new Queue<int>([colaboradorId]);
        while (cola.Count > 0)
            foreach (var h in hijos[cola.Dequeue()])
                if (res.Add(h)) cola.Enqueue(h);
        return res;
    }

    /// <summary>Verifica que el usuario pueda ver al colaborador: él mismo, su equipo (supervisor) o su sucursal (RH).</summary>
    protected async Task<Colaborador> ColaboradorVisible(int id, bool soloGestion = false)
    {
        var c = await Db.Colaboradores.FindAsync(id) ?? throw ApiException.NoEncontrado("El colaborador");
        if (Yo.Tiene(Permisos.ColaboradoresVer) && Yo.PuedeVerSucursal(c.SucursalId)) return c;
        if (!soloGestion && Yo.ColaboradorId == id) return c;
        if (Yo.Tiene(Permisos.EquipoVer) && Yo.ColaboradorId is int yo && (await Subordinados(yo)).Contains(id)) return c;
        throw ApiException.NoEncontrado("El colaborador");
    }

    /// <summary>Alcance de colaboradores visibles para listados: sucursal (RH), equipo (supervisor) o propio.</summary>
    protected async Task<IQueryable<Colaborador>> ColaboradoresVisibles(int? sucursalId = null)
    {
        var q = Db.Colaboradores.AsQueryable();
        if (Yo.Tiene(Permisos.ColaboradoresVer)) return Yo.Alcance(q, c => c.SucursalId, sucursalId);
        if (Yo.Tiene(Permisos.EquipoVer) && Yo.ColaboradorId is int yo)
        {
            var subs = await Subordinados(yo);
            subs.Add(yo);
            return q.Where(c => subs.Contains(c.Id));
        }
        var mio = Yo.ColaboradorId ?? -1;
        return q.Where(c => c.Id == mio);
    }

    protected static PersonaMini? Per(Colaborador? c) => Mapas.PersonaN(c);
}

public static class PaginarExt
{
    /// <summary>Pagina y ordena en la base, y mapea en memoria (permite DTOs con enlaces firmados).</summary>
    public static async Task<Pagina<TOut>> Pag<T, TOut>(this IQueryable<T> q, ConsultaPaginada c, Func<T, TOut> map, string ordenDefecto = "Id",
        IDictionary<string, string>? alias = null)
    {
        var tam = Math.Clamp(c.Tamano, 1, 500);
        var pag = Math.Max(1, c.Pagina);
        var total = await q.CountAsync();
        var items = await q.OrdenarPor(c.Orden, c.Dir, ordenDefecto, alias).Skip((pag - 1) * tam).Take(tam).ToListAsync();
        return new Pagina<TOut>(items.Select(map).ToList(), total, pag, tam);
    }
}
