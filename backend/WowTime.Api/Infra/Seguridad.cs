using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WowTime.Api.Data;
using WowTime.Api.Domain;

namespace WowTime.Api.Infra;

public class OpcionesJwt
{
    public string Clave { get; set; } = "";
    public string Emisor { get; set; } = "wowtime";
    public int MinutosAcceso { get; set; } = 15;
    public int DiasRenovacion { get; set; } = 14;
}

public static class Hash
{
    public static string Sha256(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();
    public static string Aleatorio(int bytes = 32) => Base64Url(RandomNumberGenerator.GetBytes(bytes));
    public static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static readonly Microsoft.AspNetCore.Identity.PasswordHasher<object> Hasher = new();
    public static string Password(string pwd) => Hasher.HashPassword(new object(), pwd);
    public static bool VerificarPassword(string hash, string pwd)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        try { return Hasher.VerifyHashedPassword(new object(), hash, pwd) != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed; }
        catch (FormatException) { return false; }
    }
}

/// <summary>Identidad de quien llama: usuario con sesión o llave de API.</summary>
public class UsuarioActual(IHttpContextAccessor http) : IUsuarioActualAccessor
{
    private ClaimsPrincipal? P => http.HttpContext?.User;
    public bool Autenticado => P?.Identity?.IsAuthenticated == true;
    public int? UsuarioId => int.TryParse(P?.FindFirstValue("uid"), out var v) ? v : null;
    public int? ColaboradorId => int.TryParse(P?.FindFirstValue("cid"), out var v) ? v : null;
    public string Nombre => P?.FindFirstValue("name") ?? "";
    public bool EsLlaveApi => P?.FindFirstValue("tipo") == "llave";
    public IReadOnlyList<string> Roles => P?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
    public HashSet<string> PermisosSet => P?.FindAll("perm").Select(c => c.Value).ToHashSet() ?? [];
    public bool Tiene(string permiso) => PermisosSet.Contains(permiso);
    public bool TodasSucursales => Tiene(Permisos.TodasSucursales);
    public int[] Sucursales => P?.FindAll("suc").Select(c => int.Parse(c.Value)).ToArray() ?? [];

    public int ColaboradorIdRequerido => ColaboradorId ?? throw new ApiException(403, "sin_colaborador", "Tu usuario no está ligado a un colaborador.");
    public int UsuarioIdRequerido => UsuarioId ?? throw new ApiException(403, "sin_usuario", "Esta operación requiere una sesión de usuario.");

    public bool PuedeVerSucursal(int sucursalId) => TodasSucursales || Sucursales.Contains(sucursalId);

    public void ExigirSucursal(int sucursalId)
    {
        if (!PuedeVerSucursal(sucursalId)) throw ApiException.Prohibido("No tienes acceso a esa sucursal.");
    }

    /// <summary>Devuelve la sucursal pedida si está permitida, o la primera permitida.</summary>
    public int? Sucursal(int? pedida)
    {
        if (pedida is int s) { ExigirSucursal(s); return s; }
        return TodasSucursales ? null : Sucursales.FirstOrDefault() is var f && f > 0 ? f : -1;
    }

    /// <summary>Filtra una consulta por sucursales permitidas.</summary>
    public IQueryable<T> Alcance<T>(IQueryable<T> q, System.Linq.Expressions.Expression<Func<T, int>> sucursal, int? pedida = null)
    {
        if (pedida is int s)
        {
            ExigirSucursal(s);
            var p = System.Linq.Expressions.Expression.Parameter(typeof(T));
            var body = System.Linq.Expressions.Expression.Equal(System.Linq.Expressions.Expression.Invoke(sucursal, p), System.Linq.Expressions.Expression.Constant(s));
            return q.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, p));
        }
        if (TodasSucursales) return q;
        var permitidas = Sucursales;
        var par = System.Linq.Expressions.Expression.Parameter(typeof(T));
        var contains = System.Linq.Expressions.Expression.Call(typeof(Enumerable), "Contains", [typeof(int)],
            System.Linq.Expressions.Expression.Constant(permitidas), System.Linq.Expressions.Expression.Invoke(sucursal, par));
        return q.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(contains, par));
    }
}

/// <summary>Exige al menos uno de los permisos indicados. Se valida en el servidor en cada endpoint.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class PermisoAttribute(params string[] permisos) : Attribute, IAuthorizationFilter
{
    public string[] Permisos { get; } = permisos;
    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        if (ctx.ActionDescriptor.EndpointMetadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any()) return;
        var user = ctx.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) return; // lo resuelve [Authorize]
        var perms = user.FindAll("perm").Select(c => c.Value).ToHashSet();
        if (!Permisos.Any(perms.Contains))
            throw ApiException.Prohibido();
    }
}

public class ServicioTokens(IOptions<OpcionesJwt> opt, AppDb db, IReloj reloj)
{
    public async Task<string[]> PermisosDe(IEnumerable<string> roles)
    {
        var r = roles.ToArray();
        return await db.RolPermisos.Where(x => r.Contains(x.Rol)).Select(x => x.Permiso).Distinct().ToArrayAsync();
    }

    public async Task<(string acceso, string renovacion, DateTime expira, string[] permisos)> Emitir(Usuario u)
    {
        var permisos = await PermisosDe(u.Roles);
        var claims = new List<Claim>
        {
            new("uid", u.Id.ToString()),
            new("name", u.Nombre),
            new("tipo", "usuario"),
            new(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
        };
        if (u.ColaboradorId is int c) claims.Add(new("cid", c.ToString()));
        claims.AddRange(u.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permisos.Select(p => new Claim("perm", p)));
        claims.AddRange(u.Sucursales.Select(s => new Claim("suc", s.ToString())));
        // El JWT se valida contra el reloj real del sistema
        var expira = DateTime.UtcNow.AddMinutes(opt.Value.MinutosAcceso);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Value.Clave));
        var token = new JwtSecurityToken(opt.Value.Emisor, opt.Value.Emisor, claims, DateTime.UtcNow.AddSeconds(-5), expira,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var acceso = new JwtSecurityTokenHandler().WriteToken(token);

        var renovacion = Hash.Aleatorio(48);
        db.TokensRenovacion.Add(new TokenRenovacion
        {
            UsuarioId = u.Id, TokenHash = Hash.Sha256(renovacion), CreadoEn = reloj.Ahora,
            Expira = reloj.Ahora.AddDays(opt.Value.DiasRenovacion)
        });
        await db.SaveChangesAsync();
        return (acceso, renovacion, expira, permisos);
    }
}

/// <summary>Autenticación por llave de API (encabezado X-Api-Key).</summary>
public class LlaveApiHandler(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e, AppDb db, IReloj reloj)
    : AuthenticationHandler<AuthenticationSchemeOptions>(o, l, e)
{
    public const string Esquema = "LlaveApi";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var valor) || string.IsNullOrWhiteSpace(valor)) return AuthenticateResult.NoResult();
        var llave = valor.ToString();
        var partes = llave.Split('.', 2);
        if (partes.Length != 2) return AuthenticateResult.Fail("Llave inválida");
        var k = await db.LlavesApi.FirstOrDefaultAsync(x => x.Prefijo == partes[0]);
        if (k is null || k.RevocadaEn is not null || (k.Expira is DateTime ex && ex < reloj.Ahora) || k.Hash != Hash.Sha256(partes[1]))
            return AuthenticateResult.Fail("Llave inválida o revocada");
        k.UltimoUso = reloj.Ahora;
        await db.SaveChangesAsync();
        var claims = new List<Claim> { new("tipo", "llave"), new("name", $"Llave: {k.Nombre}"), new("llave", k.Id.ToString()) };
        claims.AddRange(k.Permisos.Select(p => new Claim("perm", p)));
        claims.AddRange(k.Sucursales.Select(s => new Claim("suc", s.ToString())));
        var id = new ClaimsIdentity(claims, Esquema, "name", ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), Esquema));
    }
}

public interface IReloj { DateTime Ahora { get; } }
public class RelojSistema : IReloj { public DateTime Ahora => DateTime.UtcNow; }

/// <summary>Solo para demostraciones y capturas: el reloj avanza normalmente pero desde una hora local simulada (Reloj__Simular).</summary>
public class RelojDesplazado(TimeSpan desfase) : IReloj
{
    public DateTime Ahora => DateTime.UtcNow + desfase;
    public static RelojDesplazado Desde(string horaLocal, string zona)
    {
        var local = DateTime.Parse(horaLocal, System.Globalization.CultureInfo.InvariantCulture);
        return new RelojDesplazado(Zona.AUtc(local, zona) - DateTime.UtcNow);
    }
}

public static class Zona
{
    private static readonly Dictionary<string, TimeZoneInfo> Cache = new();
    public static TimeZoneInfo Get(string? id)
    {
        id ??= "America/Monterrey";
        lock (Cache)
        {
            if (Cache.TryGetValue(id, out var tz)) return tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time (Mexico)"); }
            Cache[id] = tz;
            return tz;
        }
    }
    public static DateTime ALocal(DateTime utc, string? zona) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Get(zona));
    public static DateTime AUtc(DateTime local, string? zona) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Get(zona));
    public static DateOnly Hoy(IReloj r, string? zona) => DateOnly.FromDateTime(ALocal(r.Ahora, zona));
}
