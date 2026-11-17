using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Inicio de sesión, renovación, cambio y recuperación de contraseña.</summary>
[Tags("Sesión")]
public class AuthController : ApiBase
{
    public record LoginDto(string Email, string Password);
    public record RefreshDto(string TokenRenovacion);
    public record SesionDto(string TokenAcceso, string TokenRenovacion, DateTime Expira, UsuarioDto Usuario);
    public record UsuarioDto(int Id, string Email, string Nombre, int? ColaboradorId, string[] Roles, string[] Permisos, int[] Sucursales, bool DebeCambiarPassword);

    private static readonly Dictionary<string, (int n, DateTime hasta)> Intentos = new();

    /// <summary>Inicia sesión y devuelve tokens de acceso y renovación con rol, permisos y sucursales.</summary>
    [AllowAnonymous, HttpPost("auth/login")]
    public async Task<SesionDto> Login(LoginDto d)
    {
        var email = (d.Email ?? "").Trim().ToLowerInvariant();
        lock (Intentos)
            if (Intentos.TryGetValue(email, out var i) && i.n >= 5 && i.hasta > DateTime.UtcNow)
                throw new ApiException(429, "demasiados_intentos", "Demasiados intentos fallidos. Espera 5 minutos e intenta de nuevo.");
        var u = await Db.Usuarios.FirstOrDefaultAsync(x => x.Email == email);
        if (u is null || !u.Activo || !Hash.VerificarPassword(u.PasswordHash, d.Password ?? ""))
        {
            lock (Intentos)
            {
                Intentos.TryGetValue(email, out var i);
                Intentos[email] = (i.n + 1, DateTime.UtcNow.AddMinutes(5));
            }
            throw new ApiException(401, "credenciales", "Correo o contraseña incorrectos.");
        }
        lock (Intentos) Intentos.Remove(email);
        if (u.ColaboradorId is int cid && await Db.Colaboradores.AnyAsync(c => c.Id == cid && c.Estatus == EstatusColaborador.Baja))
            throw new ApiException(401, "baja", "Tu acceso fue desactivado. Contacta a Recursos Humanos.");
        u.UltimoAcceso = Reloj.Ahora;
        return await Sesion(u);
    }

    private async Task<SesionDto> Sesion(Usuario u)
    {
        var (acc, ren, exp, perms) = await Svc<ServicioTokens>().Emitir(u);
        return new SesionDto(acc, ren, exp, new UsuarioDto(u.Id, u.Email, u.Nombre, u.ColaboradorId, u.Roles, perms, u.Sucursales, u.DebeCambiarPassword));
    }

    /// <summary>Renueva la sesión con el token de renovación (rotación: el anterior queda revocado).</summary>
    [AllowAnonymous, HttpPost("auth/refresh")]
    public async Task<SesionDto> Refresh(RefreshDto d)
    {
        var h = Hash.Sha256(d.TokenRenovacion ?? "");
        var t = await Db.TokensRenovacion.FirstOrDefaultAsync(x => x.TokenHash == h);
        if (t is null || t.RevocadoEn is not null || t.Expira < Reloj.Ahora)
            throw new ApiException(401, "sesion_expirada", "Tu sesión expiró. Inicia sesión de nuevo.");
        var u = await Db.Usuarios.FindAsync(t.UsuarioId);
        if (u is null || !u.Activo) throw new ApiException(401, "sesion_expirada", "Tu sesión expiró. Inicia sesión de nuevo.");
        t.RevocadoEn = Reloj.Ahora;
        return await Sesion(u);
    }

    /// <summary>Cierra la sesión revocando el token de renovación.</summary>
    [AllowAnonymous, HttpPost("auth/logout")]
    public async Task<IActionResult> Logout(RefreshDto d)
    {
        var h = Hash.Sha256(d.TokenRenovacion ?? "");
        var t = await Db.TokensRenovacion.FirstOrDefaultAsync(x => x.TokenHash == h);
        if (t is not null) { t.RevocadoEn = Reloj.Ahora; await Db.SaveChangesAsync(); }
        return NoContent();
    }

    public record CambioDto(string PasswordActual, string PasswordNueva);

    /// <summary>Cambia la contraseña del usuario en sesión.</summary>
    [HttpPost("auth/cambiar-password")]
    public async Task<IActionResult> Cambiar(CambioDto d)
    {
        var u = await Db.Usuarios.FindAsync(Yo.UsuarioIdRequerido) ?? throw ApiException.NoEncontrado("El usuario");
        if (!Hash.VerificarPassword(u.PasswordHash, d.PasswordActual ?? "")) throw ApiException.Validacion("La contraseña actual no es correcta.");
        ValidarPassword(d.PasswordNueva);
        u.PasswordHash = Hash.Password(d.PasswordNueva);
        u.DebeCambiarPassword = false;
        foreach (var t in Db.TokensRenovacion.Where(x => x.UsuarioId == u.Id && x.RevocadoEn == null)) t.RevocadoEn = Reloj.Ahora;
        Bitacora.Registrar(u.ColaboradorId, "seguridad", "Cambio de contraseña", null);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    public static void ValidarPassword(string? p)
    {
        if (string.IsNullOrWhiteSpace(p) || p.Length < 8 || !p.Any(char.IsDigit) || !p.Any(char.IsLetter))
            throw ApiException.Validacion("La contraseña debe tener al menos 8 caracteres e incluir letras y números.");
    }

    public record RecuperarDto(string Email);

    /// <summary>Envía un enlace de recuperación al correo (si existe). Siempre responde igual para no revelar cuentas.</summary>
    [AllowAnonymous, HttpPost("auth/recuperar")]
    public async Task<object> Recuperar(RecuperarDto d)
    {
        var email = (d.Email ?? "").Trim().ToLowerInvariant();
        var u = await Db.Usuarios.FirstOrDefaultAsync(x => x.Email == email && x.Activo);
        if (u is not null)
        {
            var token = Hash.Aleatorio(32);
            Db.TokensRecuperacion.Add(new TokenRecuperacion { UsuarioId = u.Id, TokenHash = Hash.Sha256(token), Expira = Reloj.Ahora.AddHours(1) });
            await Db.SaveChangesAsync();
            var url = (await Svc<Parametros>().Texto(Parametros.UrlPublica))?.TrimEnd('/') ?? "";
            var enlace = $"{url}/restablecer?token={token}";
            await Svc<Correo>().Enviar(u.Email, "Recupera tu contraseña de WOW! Time",
                $"<p>Hola {u.Nombre},</p><p>Para crear una nueva contraseña entra a este enlace (vigente 1 hora):</p><p><a href=\"{enlace}\">{enlace}</a></p><p>Si no lo solicitaste, ignora este correo.</p>");
        }
        return new { mensaje = "Si el correo está registrado, recibirás un enlace para restablecer tu contraseña." };
    }

    public record RestablecerDto(string Token, string PasswordNueva);

    /// <summary>Restablece la contraseña con el token recibido por correo.</summary>
    [AllowAnonymous, HttpPost("auth/restablecer")]
    public async Task<IActionResult> Restablecer(RestablecerDto d)
    {
        var h = Hash.Sha256(d.Token ?? "");
        var t = await Db.TokensRecuperacion.FirstOrDefaultAsync(x => x.TokenHash == h);
        if (t is null || t.UsadoEn is not null || t.Expira < Reloj.Ahora) throw ApiException.Validacion("El enlace no es válido o ya expiró. Solicita uno nuevo.");
        ValidarPassword(d.PasswordNueva);
        var u = await Db.Usuarios.FindAsync(t.UsuarioId) ?? throw ApiException.NoEncontrado("El usuario");
        u.PasswordHash = Hash.Password(d.PasswordNueva);
        u.DebeCambiarPassword = false;
        t.UsadoEn = Reloj.Ahora;
        foreach (var r in Db.TokensRenovacion.Where(x => x.UsuarioId == u.Id && x.RevocadoEn == null)) r.RevocadoEn = Reloj.Ahora;
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
