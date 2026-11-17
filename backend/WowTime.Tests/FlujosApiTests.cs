using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WowTime.Api.Data;
using WowTime.Api.Domain;

namespace WowTime.Tests;

/// <summary>Recorridos de punta a punta contra la API real y PostgreSQL (base de pruebas).</summary>
[Collection("api")]
public class FlujosApiTests(ApiFixture fx)
{
    private static string Lunes(int dias)
    {
        var d = DateTime.Today.AddDays(dias);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d.ToString("yyyy-MM-dd");
    }
    private static string Mas(string f, int dias) => DateOnly.Parse(f).AddDays(dias).ToString("yyyy-MM-dd");
    private static decimal Disp(JsonNode saldo) => saldo["disponibles"]!.GetValue<decimal>();

    [Fact(DisplayName = "Una supervisora solicita vacaciones y la ruta resulta Gerente y luego RH")]
    public async Task Supervisora_RutaGerenteYRh()
    {
        var ana = await fx.Como("supervisor@wowtime.mx");
        var ini = Lunes(70);
        var sim = await ana.Ok(ana.Post("/solicitudes/simular", new { tipo = "Vacaciones", fechaInicio = ini, fechaFin = Mas(ini, 2) }));
        var ruta = sim["ruta"]!.AsArray();
        Assert.Equal(2, ruta.Count);
        Assert.Equal("Gerente", ruta[0]!["tipoAprobador"]!.GetValue<string>());
        Assert.Equal("Carlos Méndez", ruta[0]!["titular"]!["nombre"]!.GetValue<string>());
        Assert.Equal("RecursosHumanos", ruta[1]!["tipoAprobador"]!.GetValue<string>());
        Assert.Equal("Gerente → RH", sim["rutaTexto"]!.GetValue<string>());
        Assert.Equal("Solicitud del supervisor", sim["regla"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Colaborador solicita, supervisora aprueba, RH aprueba; cambian saldo y calendario")]
    public async Task Flujo_Completo_Vacaciones()
    {
        var sofia = await fx.Como("colaborador@wowtime.mx");
        var ana = await fx.Como("supervisor@wowtime.mx");
        var rh = await fx.Como("rh@wowtime.mx");
        var antes = Disp(await sofia.Ok(sofia.Get("/vacaciones/saldo")));
        var ini = Lunes(77);
        var sim = await sofia.Ok(sofia.Post("/solicitudes/simular", new { tipo = "Vacaciones", fechaInicio = ini, fechaFin = Mas(ini, 2) }));
        Assert.Equal(3, sim["diasHabiles"]!.GetValue<int>());
        Assert.Equal("Supervisor directo → RH", sim["rutaTexto"]!.GetValue<string>());
        var s = await sofia.Ok(sofia.Post("/solicitudes", new { tipo = "Vacaciones", fechaInicio = ini, fechaFin = Mas(ini, 2), comentario = "Prueba" }));
        var id = s["id"]!.GetValue<int>();
        Assert.Equal(antes - 3, Disp(await sofia.Ok(sofia.Get("/vacaciones/saldo"))));

        var bandeja = await ana.Ok(ana.Get("/aprobaciones/bandeja"));
        Assert.Contains(bandeja["items"]!.AsArray(), x => x!["id"]!.GetValue<int>() == id);
        var d1 = await ana.Ok(ana.Post($"/solicitudes/{id}/aprobar", new { comentario = "Sin problema" }));
        Assert.Equal("Pendiente", d1["estado"]!.GetValue<string>());
        Assert.Equal(2, d1["pasoActual"]!.GetValue<int>());

        var d2 = await rh.Ok(rh.Post($"/solicitudes/{id}/aprobar", new { }));
        Assert.Equal("Aprobada", d2["estado"]!.GetValue<string>());
        var saldo = await sofia.Ok(sofia.Get("/vacaciones/saldo"));
        Assert.Equal(antes - 3, Disp(saldo));

        var me = await rh.Ok(rh.Get("/me"));
        var suc = me["sucursales"]![0]!["id"]!.GetValue<int>();
        var cal = await rh.Ok(rh.Get($"/ausencias/calendario?sucursalId={suc}&mes={ini[..7]}"));
        Assert.Contains(cal["filas"]!.AsArray(), f => f!["colaborador"]!["nombre"]!.GetValue<string>() == "Sofía Herrera"
            && f["ausencias"]!.AsArray().Any(a => a!["solicitudId"]?.GetValue<int>() == id && a["tipo"]!.GetValue<string>() == "Aprobada"));
    }

    [Fact(DisplayName = "Delegación y separación de funciones: quien decidió el paso 1 no decide el paso de RH")]
    public async Task Delegacion_SeparacionDeFunciones()
    {
        var ana = await fx.Como("supervisor@wowtime.mx");
        var mariana = await fx.Como("rh@wowtime.mx");
        var daniel = await fx.Como("daniel.ramirez@wowtime.mx");
        var ini = Lunes(84);
        var s = await ana.Ok(ana.Post("/solicitudes", new { tipo = "Vacaciones", fechaInicio = ini, fechaFin = Mas(ini, 1) }));
        var id = s["id"]!.GetValue<int>();
        // Carlos Méndez delegó en Mariana Torres: ella decide el paso del gerente en nombre de él
        var d1 = await mariana.Ok(mariana.Post($"/solicitudes/{id}/aprobar", new { }));
        var paso1 = d1["pasos"]![0]!;
        Assert.Equal("Aprobado", paso1["estado"]!.GetValue<string>());
        Assert.Equal("Carlos Méndez", paso1["enNombreDeNombre"]!.GetValue<string>());
        Assert.Equal("Mariana Torres", paso1["decididoPorNombre"]!.GetValue<string>());
        // Mariana ya no puede decidir el paso de RH porque hay otro usuario de RH
        var (st, err) = await mariana.Post($"/solicitudes/{id}/aprobar", new { });
        Assert.Equal(403, st);
        Assert.Contains("otra persona", err!["error"]!["mensaje"]!.GetValue<string>());
        var d2 = await daniel.Ok(daniel.Post($"/solicitudes/{id}/aprobar", new { }));
        Assert.Equal("Aprobada", d2["estado"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Nadie aprueba su propia solicitud")]
    public async Task NadieApruebaSuPropia()
    {
        var mariana = await fx.Como("rh@wowtime.mx");
        var ini = Lunes(91);
        var s = await mariana.Ok(mariana.Post("/solicitudes", new { tipo = "Vacaciones", fechaInicio = ini, fechaFin = ini }));
        var id = s["id"]!.GetValue<int>();
        var (st, _) = await mariana.Post($"/solicitudes/{id}/aprobar", new { });
        Assert.Equal(403, st);
        var bandeja = await mariana.Ok(mariana.Get("/aprobaciones/bandeja"));
        Assert.DoesNotContain(bandeja["items"]!.AsArray(), x => x!["id"]!.GetValue<int>() == id);
        await mariana.Ok(mariana.Post($"/solicitudes/{id}/cancelar", new { }));
    }

    [Fact(DisplayName = "El servidor bloquea una solicitud que supera el saldo")]
    public async Task SaldoInsuficiente()
    {
        var sofia = await fx.Como("colaborador@wowtime.mx");
        var ini = Lunes(120);
        var sim = await sofia.Ok(sofia.Post("/solicitudes/simular", new { tipo = "Vacaciones", fechaInicio = ini, fechaFin = Mas(ini, 60) }));
        Assert.False(sim["valida"]!.GetValue<bool>());
        Assert.Contains(sim["validaciones"]!.AsArray(), v => v!["codigo"]!.GetValue<string>() == "saldo_insuficiente");
        var (st, err) = await sofia.Post("/solicitudes", new { tipo = "Vacaciones", fechaInicio = ini, fechaFin = Mas(ini, 60) });
        Assert.Equal(422, st);
        Assert.Equal("validacion", err!["error"]!["codigo"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Se bloquean traslapes y al cancelar se libera el saldo comprometido")]
    public async Task Traslape_Y_Liberacion()
    {
        var c = await fx.Como("colaborador@wowtime.mx");
        var antes = Disp(await c.Ok(c.Get("/vacaciones/saldo")));
        var ini = Lunes(98);
        var s = await c.Ok(c.Post("/solicitudes", new { tipo = "Vacaciones", fechaInicio = ini, fechaFin = ini }));
        Assert.Equal(antes - 1, Disp(await c.Ok(c.Get("/vacaciones/saldo"))));
        var (st, err) = await c.Post("/solicitudes", new { tipo = "Vacaciones", fechaInicio = ini, fechaFin = Mas(ini, 1) });
        Assert.Equal(422, st);
        Assert.Contains("traslapa", err!["error"]!["mensaje"]!.GetValue<string>());
        await c.Ok(c.Post($"/solicitudes/{s["id"]!.GetValue<int>()}/cancelar", new { }));
        Assert.Equal(antes, Disp(await c.Ok(c.Get("/vacaciones/saldo"))));
    }

    [Fact(DisplayName = "Colaborador sin supervisor no puede enviar solicitudes")]
    public async Task SinSupervisor()
    {
        var rh = await fx.Como("rh@wowtime.mx");
        var lista = await rh.Ok(rh.Get("/colaboradores?sinSupervisor=true"));
        var id = lista["items"]![0]!["id"]!.GetValue<int>();
        var ini = Lunes(105);
        var sim = await rh.Ok(rh.Post("/solicitudes/simular", new { tipo = "Vacaciones", colaboradorId = id, fechaInicio = ini, fechaFin = ini }));
        Assert.Contains(sim["validaciones"]!.AsArray(), v => v!["codigo"]!.GetValue<string>() == "sin_supervisor");
        var (st, _) = await rh.Post("/solicitudes", new { tipo = "Vacaciones", colaboradorId = id, fechaInicio = ini, fechaFin = ini });
        Assert.Equal(422, st);
    }

    [Fact(DisplayName = "Un colaborador registra entrada y salida desde el teléfono con geocerca")]
    public async Task Asistencia_EntradaYSalida()
    {
        const string email = "daniela.torres@wowtime.mx";
        fx.Servicio(sp =>
        {
            var db = sp.GetRequiredService<AppDb>();
            var u = db.Usuarios.First(x => x.Email == email);
            var cid = u.ColaboradorId!.Value;
            db.Registros.Where(r => r.ColaboradorId == cid).ExecuteDelete();
            var e = db.Esquemas.First(x => x.ColaboradorId == cid);
            e.Modalidad = Modalidad.Presencial; e.RegistroPermitido = MetodoRegistro.TerminalYApp; e.RequiereFoto = false;
            db.Asignaciones.Where(a => a.ColaboradorId == cid).ExecuteDelete();
            db.SaveChanges();
            return 0;
        });
        var c = await fx.Como(email);
        var j = await c.Ok(c.Get("/asistencia/jornada-hoy"));
        Assert.Equal("Entrada", j["siguiente"]!.GetValue<string>());
        var area = j["jornada"]!["area"]!;
        double lat = area["latitud"]!.GetValue<double>(), lng = area["longitud"]!.GetValue<double>();

        // Fuera de la geocerca y sin justificación: se rechaza
        var (st, err) = await c.Post("/asistencia/registros", new { latitud = lat + 0.02, longitud = lng, precision = 10 });
        Assert.Equal(422, st);
        Assert.Equal("fuera_de_area", err!["error"]!["codigo"]!.GetValue<string>());

        var v = await c.Ok(c.Post("/asistencia/validar-ubicacion", new { latitud = lat + 0.0003, longitud = lng, precision = 8 }));
        Assert.True(v["dentro"]!.GetValue<bool>());
        var e1 = await c.Ok(c.Post("/asistencia/registros", new { latitud = lat + 0.0003, longitud = lng, precision = 8 }));
        Assert.Equal("Entrada", e1["tipo"]!.GetValue<string>());
        Assert.True(e1["dentroGeocerca"]!.GetValue<bool>());
        Assert.Contains(e1["estado"]!.GetValue<string>(), new[] { "EnHorario", "Retardo" });

        var (dup, _) = await c.Post("/asistencia/registros", new { tipo = "Entrada", latitud = lat, longitud = lng, precision = 8 });
        Assert.Equal(409, dup);
        var e2 = await c.Ok(c.Post("/asistencia/registros", new { latitud = lat, longitud = lng, precision = 8 }));
        Assert.Equal("Salida", e2["tipo"]!.GetValue<string>());
        Assert.Equal("SalidaRegistrada", e2["estado"]!.GetValue<string>());
        var j2 = await c.Ok(c.Get("/asistencia/jornada-hoy"));
        Assert.Null(j2["siguiente"]);
    }

    [Fact(DisplayName = "Un anfitrión registra un invitado, Recepción lee el QR y RH lo ve Dentro")]
    public async Task Visitante_QrYAcceso()
    {
        var ana = await fx.Como("supervisor@wowtime.mx");
        var recepcion = await fx.Como("recepcion@wowtime.mx");
        var rh = await fx.Como("rh@wowtime.mx");
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Monterrey")).AddMinutes(10);
        var inv = await ana.Ok(ana.Post("/visitantes/invitaciones", new
        {
            nombre = "Visitante de Prueba", empresaMotivo = "Auditoría", telefono = "8112345678", fecha = local.ToString("yyyy-MM-dd"), hora = local.ToString("HH:mm") + ":00"
        }));
        var id = inv["id"]!.GetValue<int>();
        var qr = inv["qr"]!.GetValue<string>();
        Assert.StartsWith("WT1.", qr);
        Assert.Equal("Esperado", inv["estado"]!.GetValue<string>());

        var (falso, _) = await recepcion.Post("/visitantes/acceso", new { codigo = qr[..^2] + "zz" });
        Assert.Equal(422, falso);
        var a = await recepcion.Ok(recepcion.Post("/visitantes/acceso", new { codigo = qr }));
        Assert.Equal("entrada", a["accion"]!.GetValue<string>());
        var d = await rh.Ok(rh.Get($"/visitantes/invitaciones/{id}"));
        Assert.Equal("Dentro", d["estado"]!.GetValue<string>());

        var pub = fx.Fabrica.CreateClient();
        var r = await pub.GetAsync($"/api/v1/publico/qr/{qr.Split('.')[1]}");
        Assert.True(r.IsSuccessStatusCode);

        var s = await recepcion.Ok(recepcion.Post("/visitantes/acceso", new { codigo = qr, accion = "salida" }));
        Assert.Equal("salida", s["accion"]!.GetValue<string>());
    }

    [Fact(DisplayName = "RH sube un documento con vigencia y aparece la alerta de vencimiento")]
    public async Task Documento_AlertaDeVencimiento()
    {
        var rh = await fx.Como("rh@wowtime.mx");
        var col = (await rh.Ok(rh.Get("/colaboradores?q=Jorge%20L%C3%B3pez")))["items"]![0]!["id"]!.GetValue<int>();
        var tipos = await rh.Ok(rh.Get("/tipos-documento"));
        var ine = tipos["items"]!.AsArray().First(t => t!["nombre"]!.GetValue<string>() == "INE")!["id"]!.GetValue<int>();
        var vence = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd");
        using var form = new MultipartFormDataContent
        {
            { new StringContent(ine.ToString()), "tipoDocumentoId" },
            { new StringContent(vence), "fechaVencimiento" },
        };
        var pdf = new ByteArrayContent("%PDF-1.4\n%prueba\n"u8.ToArray());
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "archivo", "ine.pdf");
        var r = await rh.Http.PostAsync($"/api/v1/colaboradores/{col}/documentos", form);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var doc = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
        Assert.Equal("Urgente", doc["estado"]!.GetValue<string>());
        var docId = doc["id"]!.GetValue<int>();

        var me = await rh.Ok(rh.Get("/me"));
        var suc = me["sucursales"]![0]!["id"]!.GetValue<int>();
        var pv = await rh.Ok(rh.Get($"/expedientes/por-vencer?sucursalId={suc}&tamano=200"));
        Assert.Contains(pv["items"]!.AsArray(), x => x!["id"]!.GetValue<int>() == docId && x["estado"]!.GetValue<string>() == "Urgente");
        var exp = await rh.Ok(rh.Get($"/colaboradores/{col}/documentos"));
        Assert.Contains(exp["lista"]!.AsArray(), l => l!["documento"]?["id"]?.GetValue<int>() == docId);
    }

    [Fact(DisplayName = "Permisos y alcance por sucursal se aplican en el servidor")]
    public async Task PermisosYAlcance()
    {
        var sofia = await fx.Como("colaborador@wowtime.mx");
        Assert.Equal(403, (await sofia.Get("/dashboard/resumen")).status);
        var otro = (await (await fx.Como("rh@wowtime.mx")).Ok((await fx.Como("rh@wowtime.mx")).Get("/colaboradores?q=Carlos%20M%C3%A9ndez")))["items"]![0]!["id"]!.GetValue<int>();
        Assert.Equal(404, (await sofia.Get($"/colaboradores/{otro}")).status);

        var mariana = await fx.Como("rh@wowtime.mx");
        var admin = await fx.Como("admin@wowtime.mx");
        var sucursales = (await admin.Ok(admin.Get("/sucursales")))["items"]!.AsArray();
        var saltillo = sucursales.First(x => x!["nombre"]!.GetValue<string>().Contains("Saltillo"))!["id"]!.GetValue<int>();
        var (st, err) = await mariana.Get($"/dashboard/resumen?sucursalId={saltillo}");
        Assert.Equal(403, st);
        Assert.Equal("prohibido", err!["error"]!["codigo"]!.GetValue<string>());
        Assert.Equal(200, (await admin.Get($"/dashboard/resumen?sucursalId={saltillo}")).status);

        var anonimo = fx.Fabrica.CreateClient();
        var r = await anonimo.GetAsync("/api/v1/me");
        Assert.Equal(401, (int)r.StatusCode);
        Assert.Contains("no_autenticado", await r.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Llave de API: autentica integraciones y se puede revocar")]
    public async Task LlaveDeApi()
    {
        var admin = await fx.Como("admin@wowtime.mx");
        var k = await admin.Ok(admin.Post("/llaves-api", new { nombre = "Nómina (prueba)", permisos = new[] { "colaboradores.ver", "asistencia.gestionar" }, sucursales = Array.Empty<int>(), expira = (DateTime?)null }));
        var llave = k["llave"]!.GetValue<string>();
        var cli = fx.Fabrica.CreateClient();
        cli.DefaultRequestHeaders.Add("X-Api-Key", llave);
        Assert.Equal(200, (int)(await cli.GetAsync("/api/v1/asistencia/incidencias?tamano=1")).StatusCode);
        Assert.Equal(403, (int)(await cli.GetAsync("/api/v1/usuarios")).StatusCode);
        await admin.Ok(admin.Post($"/llaves-api/{k["id"]!.GetValue<int>()}/revocar", new { }));
        Assert.Equal(401, (int)(await cli.GetAsync("/api/v1/asistencia/incidencias?tamano=1")).StatusCode);
    }

    [Fact(DisplayName = "Recuperación y cambio de contraseña")]
    public async Task RecuperacionYCambioDeContrasena()
    {
        var anon = fx.Fabrica.CreateClient();
        var r = await anon.PostAsJsonAsync("/api/v1/auth/recuperar", new { email = "miguel.sanchez@wowtime.mx" });
        Assert.True(r.IsSuccessStatusCode);
        // El correo queda en la bandeja de salida con el enlace (sin SMTP configurado)
        var cuerpo = fx.Servicio(sp => sp.GetRequiredService<AppDb>().Correos.OrderByDescending(c => c.Id).First(c => c.Para == "miguel.sanchez@wowtime.mx").Cuerpo);
        var token = System.Text.RegularExpressions.Regex.Match(cuerpo, "token=([A-Za-z0-9_-]+)").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));
        var (corta, _) = await new Cliente(anon).Post("/auth/restablecer", new { token, passwordNueva = "corta" });
        Assert.Equal(422, corta);
        await new Cliente(anon).Ok(new Cliente(anon).Post("/auth/restablecer", new { token, passwordNueva = "NuevaClave2026" }));
        var (reuso, _) = await new Cliente(anon).Post("/auth/restablecer", new { token, passwordNueva = "OtraClave2026" });
        Assert.Equal(422, reuso);
        var login = await anon.PostAsJsonAsync("/api/v1/auth/login", new { email = "miguel.sanchez@wowtime.mx", password = "NuevaClave2026" });
        Assert.True(login.IsSuccessStatusCode);
        var tok = JsonNode.Parse(await login.Content.ReadAsStringAsync())!["tokenAcceso"]!.GetValue<string>();
        var c = fx.Fabrica.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok);
        await new Cliente(c).Ok(new Cliente(c).Post("/auth/cambiar-password", new { passwordActual = "NuevaClave2026", passwordNueva = Semilla.PasswordDemo }));
        Assert.True((await anon.PostAsJsonAsync("/api/v1/auth/login", new { email = "miguel.sanchez@wowtime.mx", password = Semilla.PasswordDemo })).IsSuccessStatusCode);
    }

    [Fact(DisplayName = "Tareas programadas: se ejecutan y son idempotentes")]
    public async Task TareasProgramadas()
    {
        var admin = await fx.Como("admin@wowtime.mx");
        var r1 = await admin.Ok(admin.Post("/tareas/ejecutar"));
        foreach (var t in new[] { "asignaciones", "qr", "saldos", "vencimiento_dias", "documentos", "cumpleanos", "cierre_asistencia" })
            Assert.DoesNotContain("Error", r1[t]!.GetValue<string>());
        var notifs = fx.Servicio(sp => sp.GetRequiredService<AppDb>().Notificaciones.Count());
        await admin.Ok(admin.Post("/tareas/ejecutar"));
        Assert.Equal(notifs, fx.Servicio(sp => sp.GetRequiredService<AppDb>().Notificaciones.Count()));
        Assert.Equal(403, (await (await fx.Como("rh@wowtime.mx")).Post("/tareas/ejecutar")).status);
    }
}
