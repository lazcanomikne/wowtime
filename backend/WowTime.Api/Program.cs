using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using WowTime.Api.Data;
using WowTime.Api.Infra;
using WowTime.Api.Services;

EnvArchivo.Cargar();

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var cadena = cfg.GetConnectionString("Default") ?? throw new InvalidOperationException("Falta ConnectionStrings__Default en el archivo .env");
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(cadena, n => n.MigrationsHistoryTable("__migraciones")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IReloj, RelojSistema>();
builder.Services.AddSingleton<CanalEventos>();
builder.Services.AddScoped<UsuarioActual>();
builder.Services.AddScoped<IUsuarioActualAccessor>(sp => sp.GetRequiredService<UsuarioActual>());
builder.Services.AddScoped<ServicioTokens>();
builder.Services.AddScoped<Bitacora>();
builder.Services.AddScoped<Parametros>();
builder.Services.AddScoped<Notificador>();
builder.Services.AddScoped<Correo>();
builder.Services.AddScoped<Almacen>();
builder.Services.AddScoped<ServicioSaldos>();
builder.Services.AddScoped<MotorAprobaciones>();
builder.Services.AddScoped<ServicioSolicitudes>();
builder.Services.AddScoped<ServicioAsistencia>();
builder.Services.AddScoped<TareasProgramadas>();
builder.Services.AddScoped<Semilla>();
builder.Services.AddHostedService<TrabajadorTareas>();

builder.Services.Configure<OpcionesJwt>(cfg.GetSection("Jwt"));
var jwtClave = cfg["Jwt:Clave"];
if (string.IsNullOrWhiteSpace(jwtClave) || jwtClave.Length < 32) throw new InvalidOperationException("Jwt__Clave debe tener al menos 32 caracteres (archivo .env).");
Enlaces.Secreto = cfg["Archivos:SecretoEnlaces"] ?? jwtClave;

builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = "Mixto";
        o.DefaultChallengeScheme = "Mixto";
    })
    .AddPolicyScheme("Mixto", "Sesión o llave de API", o =>
    {
        o.ForwardDefaultSelector = ctx => ctx.Request.Headers.ContainsKey("X-Api-Key") ? LlaveApiHandler.Esquema : JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = cfg["Jwt:Emisor"] ?? "wowtime", ValidAudience = cfg["Jwt:Emisor"] ?? "wowtime",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtClave)),
            ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "name", RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        o.Events = new JwtBearerEvents
        {
            // El canal de eventos (EventSource) no admite encabezados: acepta el token por query
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api/v1/eventos") && ctx.Request.Query.TryGetValue("token", out var t)) ctx.Token = t;
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, LlaveApiHandler>(LlaveApiHandler.Esquema, null);
builder.Services.AddAuthorization();

var origenes = (cfg["Cors:Origenes"] ?? "http://localhost:5173").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition").SetPreflightMaxAge(TimeSpan.FromHours(1))));

builder.Services.AddControllers(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter()))
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ValidacionModelo.Respuesta);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WOW! Time API", Version = "v1",
        Description = "API REST de WOW! Time: asistencia, vacaciones, aprobaciones, visitantes, expedientes y organización. " +
                      "Autenticación por sesión (Bearer) o por llave de API (encabezado X-Api-Key). Todos los errores usan el formato { error: { codigo, mensaje, detalles } }."
    });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Token de acceso de /auth/login" });
    o.AddSecurityDefinition("LlaveApi", new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Api-Key", Description = "Llave de API creada en Configuración" });
    o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = [],
        [new OpenApiSecuritySchemeReference("LlaveApi", doc)] = []
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "WowTime.Api.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
    o.CustomSchemaIds(t => t.FullName!.Replace("WowTime.Api.", "").Replace("+", "."));
    o.SupportNonNullableReferenceTypes();
    o.MapType<DateOnly>(() => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" });
    o.MapType<TimeOnly>(() => new OpenApiSchema { Type = JsonSchemaType.String, Format = "time" });
});

var app = builder.Build();

// Comandos de consola: migrar, semilla
if (args.Contains("migrar") || args.Contains("semilla"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.MigrateAsync();
    Console.WriteLine("Migraciones aplicadas.");
    if (args.Contains("semilla"))
    {
        await scope.ServiceProvider.GetRequiredService<Semilla>().Cargar(args.Contains("--reiniciar"));
        Console.WriteLine("Datos de demostración cargados.");
    }
    return;
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    if (cfg["Db:MigrarAlIniciar"] != "false") await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<Semilla>().Catalogos();
}

app.UseMiddleware<ErroresMiddleware>();
if (cfg["Https:Redirigir"] == "true") app.UseHttpsRedirection();
app.UseCors();
app.UseSwagger(o => o.RouteTemplate = "api/docs/{documentName}/openapi.json");
app.UseSwaggerUI(o =>
{
    o.RoutePrefix = "api/docs";
    o.SwaggerEndpoint("/api/docs/v1/openapi.json", "WOW! Time API v1");
    o.DocumentTitle = "WOW! Time API";
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/api/docs")).AllowAnonymous().ExcludeFromDescription();
app.MapGet("/api/v1/salud", () => Results.Ok(new { estado = "ok", hora = DateTime.UtcNow })).AllowAnonymous().WithTags("Sistema");

app.Run();

public partial class Program;

/// <summary>Carga variables desde un archivo .env (no versionado) si existe.</summary>
public static class EnvArchivo
{
    public static void Cargar()
    {
        foreach (var dir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory, Path.Combine(Directory.GetCurrentDirectory(), "..") })
        {
            var f = Path.Combine(dir, ".env");
            if (!File.Exists(f)) continue;
            foreach (var linea in File.ReadAllLines(f))
            {
                var l = linea.Trim();
                if (l.Length == 0 || l.StartsWith('#')) continue;
                var i = l.IndexOf('=');
                if (i <= 0) continue;
                var k = l[..i].Trim();
                var v = l[(i + 1)..].Trim().Trim('"');
                if (Environment.GetEnvironmentVariable(k) is null) Environment.SetEnvironmentVariable(k, v);
            }
            return;
        }
    }
}
