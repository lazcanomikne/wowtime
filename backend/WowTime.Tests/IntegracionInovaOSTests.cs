using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WowTime.Api.Data;
using WowTime.Api.Domain;

namespace WowTime.Tests;

/// <summary>
/// API de integración con InovaOS contra el contrato de docs/INTEGRACION_INOVAOS.md.
/// Las formas de respuesta siguen el servidor falso de InovaOS (tests/wowtime/adaptador.test.js).
/// </summary>
[Collection("api")]
public class IntegracionInovaOSTests(ApiFixture fx)
{
    private const string Base = "/api/v1/integracion/inovaos";
    /// <summary>
    /// Una colaboradora del equipo de Ana Lucía, con saldo amplio y usuario creado aquí, para no gastar el saldo
    /// de Sofía que usan las otras pruebas. También prueba que X-Actuar-Como acepta el correo de la ficha.
    /// </summary>
    private string Colaboradora => _colaboradora ??= fx.Servicio(sp =>
    {
        var db = sp.GetRequiredService<AppDb>();
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var ana = db.Usuarios.Where(u => u.Email == Supervisora).Select(u => u.ColaboradorId).First();
        var conUsuario = db.Usuarios.Where(u => u.ColaboradorId != null).Select(u => u.ColaboradorId!.Value).ToList();
        var c = db.Colaboradores.Where(x => x.SupervisorId == ana && x.Estatus == EstatusColaborador.Activo && x.Email != null && !conUsuario.Contains(x.Id))
            .ToList()
            .First(x => db.Saldos.Where(s => s.ColaboradorId == x.Id && s.FechaOtorgamiento <= hoy && s.FechaLimite >= hoy).AsEnumerable().Sum(s => s.Disponibles) >= 8);
        db.Usuarios.Add(new Usuario { ColaboradorId = c.Id, Email = c.Email!, Nombre = c.NombreCompleto, PasswordHash = "", Roles = [Roles.Colaborador], Sucursales = [c.SucursalId] });
        db.SaveChanges();
        return c.Email!;
    });
    private static string? _colaboradora;
    private const string Supervisora = "supervisor@wowtime.mx";
    private const string Rh = "rh@wowtime.mx";

    private static string? _llave;

    private async Task<string> Llave()
    {
        if (_llave is not null) return _llave;
        var admin = await fx.Como("admin@wowtime.mx");
        var k = await admin.Ok(admin.Post("/llaves-api", new { nombre = "InovaOS (prueba)", permisos = new[] { "integracion.inovaos", "sucursales.todas" }, sucursales = Array.Empty<int>(), expira = (DateTime?)null }));
        return _llave = k["llave"]!.GetValue<string>();
    }

    private async Task<HttpClient> Cli(string? como = null, string? llave = null)
    {
        var c = fx.Fabrica.CreateClient();
        c.DefaultRequestHeaders.Add("X-Api-Key", llave ?? await Llave());
        if (como is not null) c.DefaultRequestHeaders.Add("X-Actuar-Como", como);
        return c;
    }

    private static async Task<(int status, JsonNode? cuerpo)> Leer(HttpResponseMessage r)
    {
        var t = await r.Content.ReadAsStringAsync();
        return ((int)r.StatusCode, string.IsNullOrWhiteSpace(t) ? null : JsonNode.Parse(t));
    }
    private async Task<(int status, JsonNode? cuerpo)> Get(string? como, string ruta) => await Leer(await (await Cli(como)).GetAsync(Base + ruta));
    private async Task<(int status, JsonNode? cuerpo)> Post(string? como, string ruta, object cuerpo) => await Leer(await (await Cli(como)).PostAsJsonAsync(Base + ruta, cuerpo));

    private static JsonNode Ok((int status, JsonNode? cuerpo) r, int esperado = 200)
    {
        Assert.True(r.status == esperado, $"Se esperaba {esperado} y llegó {r.status}: {r.cuerpo?.ToJsonString()}");
        return r.cuerpo!;
    }
    private static void Error((int status, JsonNode? cuerpo) r, int status, string codigo)
    {
        Assert.True(r.status == status, $"Se esperaba {status} y llegó {r.status}: {r.cuerpo?.ToJsonString()}");
        Assert.Equal(codigo, r.cuerpo!["error"]!["codigo"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(r.cuerpo!["error"]!["mensaje"]!.GetValue<string>()));
    }

    private static string Habil(int dias)
    {
        var d = DateTime.Today.AddDays(dias);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        return d.ToString("yyyy-MM-dd");
    }
    private static string Lunes(int dias)
    {
        var d = DateTime.Today.AddDays(dias);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d.ToString("yyyy-MM-dd");
    }
    /// <summary>Correo de la ficha del colaborador ligado a un usuario (en la demo difiere del correo de acceso).</summary>
    private string CorreoColaborador(string usuario) => fx.Servicio(sp =>
        sp.GetRequiredService<AppDb>().Usuarios.Where(u => u.Email == usuario).Select(u => u.Colaborador!.Email).First()!);

    private static decimal Num(JsonNode n) => n.GetValue<decimal>();
    private static JsonNode? Buscar(JsonNode lista, int id) => lista.AsArray().FirstOrDefault(x => x!["id"]!.GetValue<int>() == id);

    /// <summary>Verifica que una solicitud tenga exactamente los campos del contrato.</summary>
    private static void FormaSolicitud(JsonNode s)
    {
        foreach (var k in new[] { "id", "origenId", "tipo", "inicio", "fin", "regreso", "dias", "descuento", "motivo", "estado", "creada", "colaborador", "aprobaciones", "puedeActuar" })
            Assert.True(s.AsObject().ContainsKey(k), $"Falta {k} en {s.ToJsonString()}");
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", s["inicio"]!.GetValue<string>());
        Assert.EndsWith("Z", s["creada"]!.GetValue<string>());
        foreach (var k in new[] { "id", "origenId", "nombre", "puesto", "area", "correo", "fechaIngreso" })
            Assert.True(s["colaborador"]!.AsObject().ContainsKey(k), $"Falta colaborador.{k}");
        foreach (var a in s["aprobaciones"]!.AsArray())
            foreach (var k in new[] { "nivel", "aprobador", "estado", "fecha", "comentario" })
                Assert.True(a!.AsObject().ContainsKey(k), $"Falta aprobaciones[].{k}");
    }

    [Fact(DisplayName = "InovaOS: la llave exige el permiso y X-Actuar-Como resuelve al usuario por correo")]
    public async Task Autenticacion_Y_ActuarComo()
    {
        var sinLlave = fx.Fabrica.CreateClient();
        Assert.Equal(401, (int)(await sinLlave.GetAsync(Base + "/colaboradores")).StatusCode);

        var admin = await fx.Como("admin@wowtime.mx");
        var otra = await admin.Ok(admin.Post("/llaves-api", new { nombre = "Sin integración", permisos = new[] { "colaboradores.ver" }, sucursales = Array.Empty<int>(), expira = (DateTime?)null }));
        var sinPermiso = await Cli(Colaboradora, otra["llave"]!.GetValue<string>());
        Assert.Equal(403, (int)(await sinPermiso.GetAsync(Base + "/perfil")).StatusCode);

        Error(await Get("nadie@empresa.mx", "/perfil"), 404, "colaborador_no_encontrado");
        Error(await Get(null, "/perfil"), 400, "falta_actuar_como");

        // Sin distinguir mayúsculas
        var p = Ok(await Get("COLABORADOR@WowTime.mx", "/perfil"));
        Assert.Equal(CorreoColaborador("colaborador@wowtime.mx"), p["colaborador"]!["correo"]!.GetValue<string>());
        Assert.Equal(Colaboradora, Ok(await Get(Colaboradora.ToUpperInvariant(), "/perfil"))["colaborador"]!["correo"]!.GetValue<string>());

        // Fuera de las rutas de la integración, X-Actuar-Como no da los permisos del usuario
        var fuera = await Cli("admin@wowtime.mx");
        Assert.Equal(403, (int)(await fuera.GetAsync("/api/v1/llaves-api")).StatusCode); // el administrador sí tendría permiso

        // Un usuario con sesión normal no entra a la integración
        var rh = await fx.Como(Rh);
        Assert.Equal(403, (await rh.Get("/integracion/inovaos/colaboradores")).status);
    }

    [Fact(DisplayName = "InovaOS: perfil con antigüedad y periodo vigente")]
    public async Task Perfil()
    {
        var p = Ok(await Get(Colaboradora, "/perfil"));
        var c = p["colaborador"]!;
        Assert.True(c["id"]!.GetValue<int>() > 0);
        Assert.Null(c["origenId"]);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", c["fechaIngreso"]!.GetValue<string>());
        var ant = p["antiguedad"]!;
        Assert.Equal(ant["meses"]!.GetValue<int>() / 12, ant["anios"]!.GetValue<int>());
        var per = p["periodo"]!;
        foreach (var k in new[] { "inicio", "fin", "otorgados", "usados", "programados", "canjeados", "disponible", "diasParaVencer" })
            Assert.True(per.AsObject().ContainsKey(k), $"Falta periodo.{k}");
        Assert.True(per["diasParaVencer"]!.GetValue<int>() >= 0);

        // El disponible del periodo es el mismo saldo que ve la colaboradora en WOW! Time cuando solo tiene un periodo vigente
        var cid = c["id"]!.GetValue<int>();
        var vigentes = fx.Servicio(sp =>
        {
            var db = sp.GetRequiredService<AppDb>();
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            return db.Saldos.Count(s => s.ColaboradorId == cid && s.FechaOtorgamiento <= hoy && s.FechaLimite >= hoy);
        });
        if (vigentes == 1)
        {
            var disponibles = fx.Servicio(sp => sp.GetRequiredService<WowTime.Api.Services.ServicioSaldos>()
                .Resumen(cid, DateOnly.FromDateTime(DateTime.Today)).GetAwaiter().GetResult().Disponibles);
            Assert.Equal(disponibles, Num(per["disponible"]!));
        }
    }

    [Fact(DisplayName = "InovaOS: vacaciones pedidas, aprobadas por la supervisora y por RH, con bitácora del canal")]
    public async Task Vacaciones_DePuntaAPunta()
    {
        var antes = Num(Ok(await Get(Colaboradora, "/perfil"))["periodo"]!["disponible"]!);
        var ini = Lunes(150);
        var fin = DateOnly.Parse(ini).AddDays(3).ToString("yyyy-MM-dd");
        var s = Ok(await Post(Colaboradora, "/solicitudes", new { tipo = "VACATION", inicio = ini, fin, motivo = "Viaje" }), 201);
        FormaSolicitud(s);
        var id = s["id"]!.GetValue<int>();
        var dias = Num(s["dias"]!);
        Assert.Equal("VACATION", s["tipo"]!.GetValue<string>());
        Assert.Equal("PENDING", s["estado"]!.GetValue<string>());
        Assert.Equal(dias, Num(s["descuento"]!));
        Assert.True(dias is > 0 and <= 4);
        Assert.Equal("Viaje", s["motivo"]!.GetValue<string>());
        var ap = s["aprobaciones"]!.AsArray();
        Assert.Equal(2, ap.Count);
        Assert.Equal(1, ap[0]!["nivel"]!.GetValue<int>());
        Assert.Equal("PENDING", ap[0]!["estado"]!.GetValue<string>());
        Assert.Equal(CorreoColaborador(Supervisora), ap[0]!["aprobador"]!["correo"]!.GetValue<string>());
        Assert.Equal("WAITING", ap[1]!["estado"]!.GetValue<string>());
        Assert.Equal(antes - dias, Num(Ok(await Get(Colaboradora, "/perfil"))["periodo"]!["disponible"]!));

        Assert.NotNull(Buscar(Ok(await Get(Colaboradora, "/solicitudes/mias")), id));

        // RH no puede saltarse el paso de la supervisora
        Error(await Post(Rh, $"/solicitudes/{id}/aprobar", new { comentario = (string?)null }), 403, "no_le_toca");
        var rhBandeja = Buscar(Ok(await Get(Rh, "/aprobaciones")), id);
        Assert.NotNull(rhBandeja); // RH ve todas las pendientes
        Assert.False(rhBandeja!["puedeActuar"]!.GetValue<bool>());

        var deAna = Buscar(Ok(await Get(Supervisora, "/aprobaciones")), id);
        Assert.True(deAna!["puedeActuar"]!.GetValue<bool>());
        var r1 = Ok(await Post(Supervisora, $"/solicitudes/{id}/aprobar", new { comentario = "Adelante" }));
        Assert.Equal("PENDING", r1["estado"]!.GetValue<string>());
        Assert.Equal(2, r1["siguienteNivel"]!.GetValue<int>());
        var yaDecidida = Buscar(Ok(await Get(Supervisora, "/aprobaciones")), id);
        Assert.False(yaDecidida!["puedeActuar"]!.GetValue<bool>()); // la sigue viendo porque ya la decidió
        Assert.Equal("APPROVED", yaDecidida["aprobaciones"]![0]!["estado"]!.GetValue<string>());
        Assert.Equal("Adelante", yaDecidida["aprobaciones"]![0]!["comentario"]!.GetValue<string>());
        Assert.EndsWith("Z", yaDecidida["aprobaciones"]![0]!["fecha"]!.GetValue<string>());

        var r2 = Ok(await Post(Rh, $"/solicitudes/{id}/aprobar", new { }));
        Assert.Equal("APPROVED", r2["estado"]!.GetValue<string>());
        Assert.Null(r2["siguienteNivel"]);
        Error(await Post(Rh, $"/solicitudes/{id}/aprobar", new { }), 409, "ya_resuelta");

        // Calendario de la empresa con el alcance de la llave
        Assert.NotNull(Buscar(Ok(await Get(null, $"/ausencias?desde={ini}")), id));
        Assert.Null(Buscar(Ok(await Get(null, $"/ausencias?desde={DateOnly.Parse(fin).AddDays(1):yyyy-MM-dd}")), id));

        // La bitácora guarda al usuario real y el canal
        var (canal, eventos) = fx.Servicio(sp =>
        {
            var db = sp.GetRequiredService<AppDb>();
            var sol = db.Solicitudes.First(x => x.Id == id);
            var ev = db.Bitacora.Where(b => b.Entidad == "Solicitud" && b.EntidadId == id).ToList();
            return (sol.Canal, ev);
        });
        Assert.Equal("InovaOS", canal);
        Assert.NotEmpty(eventos);
        Assert.All(eventos, e => Assert.Equal("InovaOS", e.Canal));
        Assert.Contains(eventos, e => e.AutorNombre == "Ana Lucía Martínez" || (e.AutorNombre ?? "").StartsWith("Ana Lucía"));
    }

    [Fact(DisplayName = "InovaOS: permiso de 3 horas descuenta 0.3 y al rechazarlo se libera")]
    public async Task Permiso3h_Y_Rechazo()
    {
        var dia = Habil(160);
        Error(await Post(Colaboradora, "/solicitudes", new { tipo = "PERMIT_3H", inicio = dia, fin = DateOnly.Parse(dia).AddDays(-2).ToString("yyyy-MM-dd") }), 422, "rango_invalido");
        var r = await Post(Colaboradora, "/solicitudes", new { tipo = "PERMIT_3H", inicio = "2026-12-10", fin = "2026-12-01" });
        Assert.Equal("La fecha final es anterior a la inicial.", r.cuerpo!["error"]!["mensaje"]!.GetValue<string>());
        Error(await Post(Colaboradora, "/solicitudes", new { tipo = "PERMIT_3H", inicio = dia, fin = DateOnly.Parse(dia).AddDays(1).ToString("yyyy-MM-dd") }), 422, "rango_invalido");
        Error(await Post(Colaboradora, "/solicitudes", new { tipo = "OTRO", inicio = dia, fin = dia }), 422, "tipo_invalido");

        var antes = Num(Ok(await Get(Colaboradora, "/perfil"))["periodo"]!["disponible"]!);
        var s = Ok(await Post(Colaboradora, "/solicitudes", new { tipo = "PERMIT_3H", inicio = dia, fin = dia, motivo = "Médico" }), 201);
        var id = s["id"]!.GetValue<int>();
        Assert.Equal("PERMIT_3H", s["tipo"]!.GetValue<string>());
        Assert.Equal(0.3m, Num(s["dias"]!));
        Assert.Equal(0.3m, Num(s["descuento"]!));
        Assert.Equal(antes - 0.3m, Num(Ok(await Get(Colaboradora, "/perfil"))["periodo"]!["disponible"]!));

        var r1 = Ok(await Post(Supervisora, $"/solicitudes/{id}/rechazar", new { comentario = (string?)null }));
        Assert.Equal("REJECTED", r1["estado"]!.GetValue<string>());
        Assert.Null(r1["siguienteNivel"]);
        var mia = Buscar(Ok(await Get(Colaboradora, "/solicitudes/mias")), id)!;
        Assert.Equal("REJECTED", mia["estado"]!.GetValue<string>());
        Assert.Equal(0m, Num(mia["descuento"]!));
        Assert.Equal("REJECTED", mia["aprobaciones"]![0]!["estado"]!.GetValue<string>());
        Assert.Equal("SKIPPED", mia["aprobaciones"]![1]!["estado"]!.GetValue<string>());
        Assert.Equal(antes, Num(Ok(await Get(Colaboradora, "/perfil"))["periodo"]!["disponible"]!));
        Error(await Post(Supervisora, $"/solicitudes/{id}/rechazar", new { }), 409, "ya_resuelta");
    }

    [Fact(DisplayName = "InovaOS: canje de días consume saldo, no ocupa calendario y se ve como canjeado")]
    public async Task CanjeDeDias()
    {
        Error(await Post(Colaboradora, "/solicitudes", new { tipo = "CASH_OUT", inicio = Habil(1), fin = Habil(1), dias = 999 }), 422, "saldo_insuficiente");
        var antes = Ok(await Get(Colaboradora, "/perfil"))["periodo"]!;
        var s = Ok(await Post(Colaboradora, "/solicitudes", new { tipo = "CASH_OUT", inicio = Habil(1), fin = Habil(1), dias = 1.5m }), 201);
        var id = s["id"]!.GetValue<int>();
        Assert.Equal("CASH_OUT", s["tipo"]!.GetValue<string>());
        Assert.Equal(1.5m, Num(s["dias"]!));
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), s["inicio"]!.GetValue<string>()); // lleva la fecha del día
        Ok(await Post(Supervisora, $"/solicitudes/{id}/aprobar", new { }));
        Assert.Equal("APPROVED", Ok(await Post(Rh, $"/solicitudes/{id}/aprobar", new { }))["estado"]!.GetValue<string>());

        var despues = Ok(await Get(Colaboradora, "/perfil"))["periodo"]!;
        Assert.Equal(Num(antes["canjeados"]!) + 1.5m, Num(despues["canjeados"]!));
        Assert.Equal(Num(antes["disponible"]!) - 1.5m, Num(despues["disponible"]!));
        Assert.Equal(Num(antes["usados"]!), Num(despues["usados"]!));
        Assert.Null(Buscar(Ok(await Get(null, $"/ausencias?desde={DateTime.Today.AddDays(-1):yyyy-MM-dd}")), id));
    }

    [Fact(DisplayName = "InovaOS: colaboradores activos y reporte con filtros")]
    public async Task Colaboradores_Y_Reporte()
    {
        var cols = Ok(await Get(null, "/colaboradores")).AsArray();
        var activos = fx.Servicio(sp => sp.GetRequiredService<AppDb>().Colaboradores.Count(c => c.Estatus == EstatusColaborador.Activo));
        Assert.Equal(activos, cols.Count);
        var sid = Ok(await Get(Colaboradora, "/perfil"))["colaborador"]!["id"]!.GetValue<int>();
        Assert.NotNull(Buscar(cols, sid));

        var dia = Habil(170);
        var s = Ok(await Post(Colaboradora, "/solicitudes", new { tipo = "PERMIT_DAY", inicio = dia, fin = dia }), 201);
        Assert.Equal(1m, Num(s["descuento"]!));

        var todos = Ok(await Get(null, $"/solicitudes?colaboradorId={sid}")).AsArray();
        Assert.All(todos, x => Assert.Equal(sid, x!["colaborador"]!["id"]!.GetValue<int>()));
        var permisos = Ok(await Get(null, $"/solicitudes?tipo=PERMIT_3H,PERMIT_5H,PERMIT_DAY&colaboradorId={sid}")).AsArray();
        Assert.NotEmpty(permisos);
        Assert.All(permisos, x => Assert.StartsWith("PERMIT_", x!["tipo"]!.GetValue<string>()));
        Assert.NotNull(Buscar(permisos, s["id"]!.GetValue<int>()));
        var rango = Ok(await Get(null, $"/solicitudes?desde={dia}&hasta={dia}&estado=PENDING")).AsArray();
        Assert.All(rango, x => Assert.Equal("PENDING", x!["estado"]!.GetValue<string>()));
        Assert.NotNull(Buscar(rango, s["id"]!.GetValue<int>()));
        Error(await Get(null, "/solicitudes?tipo=OTRO"), 422, "tipo_invalido");
    }
}
