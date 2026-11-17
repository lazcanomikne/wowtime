using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WowTime.Api.Infra;

/// <summary>Error de negocio con mensaje en español. Se traduce al formato único de errores.</summary>
public class ApiException(int status, string codigo, string mensaje, object? detalles = null) : Exception(mensaje)
{
    public int Status { get; } = status;
    public string Codigo { get; } = codigo;
    public object? Detalles { get; } = detalles;

    public static ApiException NoEncontrado(string que = "El recurso") => new(404, "no_encontrado", $"{que} no existe o no tienes acceso.");
    public static ApiException Prohibido(string mensaje = "No tienes permiso para realizar esta acción.") => new(403, "prohibido", mensaje);
    public static ApiException Validacion(string mensaje, object? detalles = null) => new(422, "validacion", mensaje, detalles);
    public static ApiException Conflicto(string mensaje) => new(409, "conflicto", mensaje);
}

/// <summary>Formato único: { "error": { "codigo", "mensaje", "detalles" } }</summary>
public record ErrorRespuesta(ErrorCuerpo Error);
public record ErrorCuerpo(string Codigo, string Mensaje, object? Detalles = null);

public class ErroresMiddleware(RequestDelegate next, ILogger<ErroresMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
            if (!ctx.Response.HasStarted && ctx.Response.ContentLength is null && ctx.Response.ContentType is null)
            {
                var (cod, msg) = ctx.Response.StatusCode switch
                {
                    401 => ("no_autenticado", "Tu sesión no es válida o expiró. Inicia sesión de nuevo."),
                    403 => ("prohibido", "No tienes permiso para realizar esta acción."),
                    404 when ctx.Request.Path.StartsWithSegments("/api") => ("no_encontrado", "La ruta solicitada no existe."),
                    405 => ("metodo_no_permitido", "Método no permitido para esta ruta."),
                    _ => (null, null)
                };
                if (cod is not null) await Escribir(ctx, ctx.Response.StatusCode, cod, msg!);
            }
        }
        catch (ApiException ex)
        {
            await Escribir(ctx, ex.Status, ex.Codigo, ex.Message, ex.Detalles);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505")
        {
            await Escribir(ctx, 409, "duplicado", "Ya existe un registro con esos datos.");
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23503")
        {
            await Escribir(ctx, 409, "en_uso", "El registro está en uso por otros datos y no puede eliminarse.");
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            log.LogError(ex, "Error no controlado en {Path}", ctx.Request.Path);
            await Escribir(ctx, 500, "error_interno", "Ocurrió un error inesperado. Intenta de nuevo o contacta al administrador.");
        }
    }

    public static async Task Escribir(HttpContext ctx, int status, string codigo, string mensaje, object? detalles = null)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new ErrorRespuesta(new ErrorCuerpo(codigo, mensaje, detalles)), Json.Opciones));
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}

/// <summary>Convierte errores de validación del modelo al formato único.</summary>
public static class ValidacionModelo
{
    public static IActionResult Respuesta(ActionContext ctx)
    {
        var detalles = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Valor no válido." : e.ErrorMessage).ToArray());
        return new ObjectResult(new ErrorRespuesta(new ErrorCuerpo("validacion", "Revisa los datos enviados.", detalles))) { StatusCode = 422 };
    }
}
