using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WowTime.Api.Infra;

/// <summary>Da un resumen en español a las operaciones que no tienen comentario XML propio.</summary>
public class ResumenesOpenApi : IOperationFilter
{
    private static string Singular(string s) => s.EndsWith("es") && s.Length > 3 && "rlndz".Contains(s[^3]) ? s[..^2] : s.TrimEnd('s');

    public void Apply(OpenApiOperation op, OperationFilterContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(op.Summary)) return;
        var ruta = (ctx.ApiDescription.RelativePath ?? "").Replace("api/v1/", "").Split('?')[0];
        var seg = ruta.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (seg.Length == 0) return;
        string Texto(string s) => s.Replace('-', ' ');
        var ultimo = seg[^1];
        var esParam = ultimo.StartsWith('{');
        var recurso = Texto(seg.LastOrDefault(s => !s.StartsWith('{')) ?? "recurso");
        var metodo = ctx.ApiDescription.HttpMethod?.ToUpperInvariant();
        var padre = seg.Length >= 3 && seg[1].StartsWith('{') ? $" del {Singular(Texto(seg[0]))}" : "";
        op.Summary = metodo switch
        {
            "GET" when esParam => $"Detalle de {recurso}",
            "GET" => $"Consulta de {recurso}{padre} (filtros, paginación y orden)",
            "POST" when !esParam && seg.Length >= 2 && seg[^2].StartsWith('{') && !char.IsUpper(ultimo[0]) && ultimo is "cancelar" or "revocar" or "llave" or "estatus" or "duplicar" or "restablecer"
                => $"{char.ToUpper(ultimo[0]) + Texto(ultimo)[1..]} ({Texto(seg[0])})",
            "POST" => $"Alta de {recurso}{padre}",
            "PUT" => $"Edición de {recurso}",
            "DELETE" => $"Eliminación de {recurso}",
            _ => op.Summary
        };
    }
}
