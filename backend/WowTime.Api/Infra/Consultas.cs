using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace WowTime.Api.Infra;

/// <summary>Parámetros comunes de paginación y orden: ?pagina=1&amp;tamano=25&amp;orden=campo&amp;dir=asc|desc</summary>
public class ConsultaPaginada
{
    public int Pagina { get; set; } = 1;
    public int Tamano { get; set; } = 25;
    public string? Orden { get; set; }
    public string? Dir { get; set; }
}

public record Pagina<T>(IReadOnlyList<T> Items, int Total, int PaginaActual, int Tamano)
{
    public int Paginas => Tamano == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamano);
}

public static class ConsultasExt
{
    public static async Task<Pagina<TOut>> Paginar<T, TOut>(this IQueryable<T> q, ConsultaPaginada c, Expression<Func<T, TOut>> sel,
        string ordenDefecto = "Id", IDictionary<string, string>? alias = null, CancellationToken ct = default)
    {
        var tam = Math.Clamp(c.Tamano, 1, 500);
        var pag = Math.Max(1, c.Pagina);
        var total = await q.CountAsync(ct);
        q = q.OrdenarPor(c.Orden, c.Dir, ordenDefecto, alias);
        var items = await q.Skip((pag - 1) * tam).Take(tam).Select(sel).ToListAsync(ct);
        return new Pagina<TOut>(items, total, pag, tam);
    }

    /// <summary>Ordena por nombre de propiedad (insensible a mayúsculas). Soporta rutas con punto: "Puesto.Nombre".</summary>
    public static IQueryable<T> OrdenarPor<T>(this IQueryable<T> q, string? campo, string? dir, string defecto = "Id", IDictionary<string, string>? alias = null)
    {
        campo = string.IsNullOrWhiteSpace(campo) ? defecto : campo;
        if (alias is not null && alias.TryGetValue(campo, out var a)) campo = a;
        var p = Expression.Parameter(typeof(T), "x");
        Expression cuerpo = p;
        var tipo = typeof(T);
        foreach (var parte in campo.Split('.'))
        {
            var prop = tipo.GetProperty(parte, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop is null || prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute>() is not null)
            {
                if (campo == defecto) return q;
                return q.OrdenarPor(defecto, dir, defecto);
            }
            cuerpo = Expression.Property(cuerpo, prop);
            tipo = prop.PropertyType;
        }
        var lambda = Expression.Lambda(cuerpo, p);
        var metodo = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "OrderByDescending" : "OrderBy";
        var call = Expression.Call(typeof(Queryable), metodo, [typeof(T), tipo], q.Expression, Expression.Quote(lambda));
        return q.Provider.CreateQuery<T>(call);
    }

    public static string Normalizar(this string s)
    {
        var d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var ch in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().ToLowerInvariant();
    }
}
