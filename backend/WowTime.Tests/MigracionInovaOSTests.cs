using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WowTime.Api.Data;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;
using WowTime.Api.Services.Integracion;

namespace WowTime.Tests;

/// <summary>Origen falso con la forma de las vistas wowtime.* de InovaOS.</summary>
public class OrigenFalso : IOrigenInova
{
    public bool Configurado => true;
    public bool ChecadasConfiguradas => true;
    public List<ColaboradorOrigen> Cols = [];
    public List<PeriodoOrigen> Pers = [];
    public List<SolicitudOrigen> Sols = [];
    public List<AprobacionOrigen> Aprs = [];
    public List<ChecadaOrigen> Chs = [];
    public ConfigAsistenciaOrigen? Config;
    public Task<List<ColaboradorOrigen>> Colaboradores() => Task.FromResult(Cols);
    public Task<List<PeriodoOrigen>> Periodos() => Task.FromResult(Pers);
    public Task<List<EscalaOrigen>> Escala() => Task.FromResult(new List<EscalaOrigen>());
    public Task<List<SolicitudOrigen>> Solicitudes() => Task.FromResult(Sols);
    public Task<List<AprobacionOrigen>> Aprobaciones() => Task.FromResult(Aprs);
    public Task<ConfigAsistenciaOrigen?> ConfigAsistencia() => Task.FromResult(Config);
    public Task<List<HorarioOrigen>> Horarios() => Task.FromResult(new List<HorarioOrigen>());
    public Task<List<JustificacionOrigen>> Justificaciones() => Task.FromResult(new List<JustificacionOrigen>());
    public Task<List<FestivoOrigen>> Festivos() => Task.FromResult(new List<FestivoOrigen>());
    public Task<List<ChecadaOrigen>> Checadas(long despuesDe, DateOnly desde) =>
        Task.FromResult(Chs.Where(c => c.EventoId > despuesDe && DateOnly.FromDateTime(c.FechaLocal) >= desde).OrderBy(c => c.EventoId).ToList());
}

/// <summary>Migración desde InovaOS: mapeo, saldos, cadena de aprobación, checadas e idempotencia.</summary>
[Collection("api")]
public class MigracionInovaOSTests(ApiFixture fx)
{
    private const string Dom = "@inova.prueba";
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Today);

    private static OrigenFalso Origen()
    {
        var o = new OrigenFalso
        {
            Config = new(new TimeOnly(8, 30), new TimeOnly(18, 30), "1111100", 10, 0, Hoy.AddDays(-3))
        };
        o.Cols.AddRange([
            new(9100, "900", "Carla", "Directora Raíz", "direccion" + Dom, "Dirección General", null, new DateOnly(2012, 11, 1), null, true),
            new(9101, "901", "Gerardo", "Jefe Contable", "jefe" + Dom, "Contador", "Contabilidad", Hoy.AddYears(-6).AddDays(-100), 9100, true),
            new(9102, null, "Ana", "Sin Lector", "ana" + Dom, "Auxiliar", "Contabilidad", Hoy.AddYears(-1).AddDays(-30), 9101, true),
            new(9103, null, "Luis", "Sin Jefe", "luis" + Dom, null, null, new DateOnly(2020, 1, 6), null, true),
            new(9104, "904", "Pedro", "De Baja", "pedro" + Dom, "Chofer", null, new DateOnly(2019, 5, 1), null, false),
        ]);
        var ini101 = Hoy.AddYears(-6).AddDays(-100).AddYears(6);
        o.Pers.AddRange([
            new(9501, 9101, 7, ini101, ini101.AddYears(1).AddDays(-1), 22, "Ajuste manual"),
            new(9502, 9101, 8, ini101.AddYears(1), ini101.AddYears(2).AddDays(-1), 22, null),
            new(9503, 9102, 2, Hoy.AddDays(-30), Hoy.AddDays(-30).AddYears(1).AddDays(-1), 12, null),
        ]);
        DateTime Local(int dias, int h) => Hoy.AddDays(dias).ToDateTime(new TimeOnly(h, 0));
        o.Sols.AddRange([
            new(9001, 9101, "VACATION", Hoy.AddDays(-20), Hoy.AddDays(-18), 3, 3, "Viaje", "APPROVED", true, null, true, null, Local(-25, 10), 9501, false, null),
            new(9002, 9101, "PERMIT_3H", Hoy.AddDays(-10), Hoy.AddDays(-10), 0.3m, 0.3m, "Médico", "APPROVED", true, null, true, null, Local(-11, 10), 9501, false, null),
            new(9003, 9101, "CASH_OUT", Hoy.AddDays(-5), Hoy.AddDays(-5), 2, 2, null, "APPROVED", true, null, true, null, Local(-5, 10), 9501, false, null),
            new(9004, 9102, "VACATION", Hoy.AddDays(60), Hoy.AddDays(61), 2, 2, "Boda", "PENDING", null, null, null, null, Local(-1, 9), 9503, false, null),
            new(9005, 9102, "VACATION", Hoy.AddDays(70), Hoy.AddDays(70), 1, 1, null, "REJECTED", null, null, null, null, Local(-2, 9), 9503, false, null),
            new(9006, 9101, "VACATION", Hoy.AddDays(-40), Hoy.AddDays(-40), 1, 1, null, "APPROVED", true, null, true, null, Local(-41, 9), 9501, true, "Cortesía"),
        ]);
        DateTime Utc(int dias, int h) => Hoy.AddDays(dias).ToDateTime(new TimeOnly(h, 0)).AddHours(6);
        o.Aprs.AddRange([
            new(1, 9001, 1, 9100, "Carla Directora Raíz", "APPROVED", Utc(-24, 11), "Ok"),
            new(2, 9004, 1, 9101, "Gerardo Jefe Contable", "PENDING", null, null),
            new(3, 9004, 2, 9100, "Carla Directora Raíz", "PENDING", null, null),
            new(4, 9005, 1, 9101, "Gerardo Jefe Contable", "REJECTED", Utc(-1, 12), "Cierre de mes"),
        ]);
        DateTimeOffset Ev(int dias, int h, int m) => new(Hoy.AddDays(dias).ToDateTime(new TimeOnly(h, m)), TimeSpan.FromHours(-6));
        void Ch(long id, int dias, int h, int m, string num) => o.Chs.Add(new(id, Ev(dias, h, m), Ev(dias, h, m).DateTime, num));
        Ch(91001, -2, 9, 0, "901");   // retardo
        Ch(91002, -1, 8, 35, "901");  // en horario
        Ch(91003, -1, 8, 36, "901");  // repetida (menos de 2 minutos)
        Ch(91004, -1, 18, 40, "901"); // salida
        Ch(91005, -1, 19, 5, "901");  // la salida se mueve a la última
        Ch(91006, -1, 9, 0, "999");   // número sin colaborador
        return o;
    }

    private static MigradorInova Migrador(IServiceProvider sp, IOrigenInova o) => new(sp.GetRequiredService<AppDb>(), o, sp.GetRequiredService<IReloj>(),
        sp.GetRequiredService<ServicioAsistencia>(), sp.GetRequiredService<Bitacora>(), NullLogger<MigradorInova>.Instance);

    private static OpcionesMigracion Opciones() => new() { Sucursal = "Inova (pruebas)", AjustarReglas = false, CerrarHistorico = false, RhALaRaizSiNoHay = false };

    private async Task<InformeMigracion> Correr(OrigenFalso o)
    {
        using var scope = fx.Fabrica.Services.CreateScope();
        return await Migrador(scope.ServiceProvider, o).Migrar(Opciones());
    }

    private T Db<T>(Func<AppDb, T> f) => fx.Servicio(sp => f(sp.GetRequiredService<AppDb>()));

    [Fact(DisplayName = "Migración InovaOS: organigrama, saldos, cadena, checadas e idempotencia")]
    public async Task Migracion_Completa()
    {
        var o = Origen();
        var inf = await Correr(o);
        Assert.Equal(5, inf.Conteos["colaboradores creados"]);
        Assert.Equal(6, inf.Conteos["solicitudes creadas"]);
        Assert.Equal(2, inf.Conteos["entradas importadas"]);
        Assert.Equal(1, inf.Conteos["salidas importadas"]);
        Assert.Equal(1, inf.Conteos["salidas actualizadas"]);
        Assert.Equal(1, inf.Conteos["checadas repetidas"]);
        Assert.Equal(1, inf.Conteos["checadas sin colaborador"]);

        var cols = Db(db => db.Colaboradores.Include(c => c.Esquema).Where(c => c.OrigenId >= 9100 && c.OrigenId <= 9104).ToDictionary(c => c.OrigenId!.Value));
        Assert.Null(cols[9100].SupervisorId);
        Assert.Equal(cols[9100].Id, cols[9101].SupervisorId);
        Assert.Equal(cols[9101].Id, cols[9102].SupervisorId);
        Assert.Equal(cols[9100].Id, cols[9103].SupervisorId); // sin jefe: queda bajo Dirección, como aprobaba InovaOS
        Assert.Equal(EstatusColaborador.Baja, cols[9104].Estatus);
        Assert.Equal(Hoy, cols[9104].FechaBaja);
        Assert.Equal("901", cols[9101].Numero);
        Assert.Equal("INV-9102", cols[9102].Numero);
        Assert.Equal(MetodoRegistro.Ninguno, cols[9102].Esquema!.RegistroPermitido); // no checa: no se le generan faltas
        Assert.Equal(MetodoRegistro.TerminalYApp, cols[9101].Esquema!.RegistroPermitido);

        var usuarios = Db(db => db.Usuarios.Where(u => u.Email.EndsWith(Dom)).ToDictionary(u => u.Email));
        Assert.Contains(Roles.Administrador, usuarios["direccion" + Dom].Roles);
        Assert.Contains(Roles.Supervisor, usuarios["jefe" + Dom].Roles);
        Assert.DoesNotContain(Roles.Supervisor, usuarios["ana" + Dom].Roles);
        Assert.False(usuarios["pedro" + Dom].Activo);

        // Saldos: el periodo vigente con sus consumos; el futuro existe pero no cuenta
        decimal Disp(int cid) => fx.Servicio(sp => sp.GetRequiredService<ServicioSaldos>().Resumen(cid, Hoy).GetAwaiter().GetResult().Disponibles);
        Assert.Equal(22 - 3 - 0.3m - 2, Disp(cols[9101].Id)); // la de cortesía (SinDescuento) no descuenta
        Assert.Equal(12 - 2, Disp(cols[9102].Id));             // la pendiente compromete; la rechazada no
        var futuro = Db(db => db.Saldos.Single(s => s.OrigenId == 9502));
        Assert.Equal(6, Db(db => db.Saldos.Single(s => s.OrigenId == 9501)).Periodo); // periodo 7 de InovaOS = 6 años cumplidos
        Assert.True(futuro.FechaOtorgamiento > Hoy);

        // La pendiente sigue en el mismo paso, con la cadena de InovaOS
        var pend = Db(db => db.Solicitudes.Include(s => s.Pasos).Single(s => s.OrigenId == 9004));
        Assert.Equal(EstadoSolicitud.Pendiente, pend.Estado);
        Assert.Equal(1, pend.PasoActual);
        Assert.Equal([EstadoPaso.Pendiente, EstadoPaso.EnEspera], pend.Pasos.OrderBy(p => p.Orden).Select(p => p.Estado));
        Assert.Equal(cols[9101].Id, pend.Pasos.Single(p => p.Orden == 1).TitularColaboradorId);
        var rech = Db(db => db.Solicitudes.Include(s => s.Pasos).Single(s => s.OrigenId == 9005));
        Assert.Equal("Cierre de mes", rech.MotivoRechazo);
        Assert.Equal(DateTimeKind.Utc, DateTime.SpecifyKind(rech.Pasos[0].Fecha!.Value, DateTimeKind.Utc).Kind);
        Assert.True(rech.Pasos[0].Fecha > rech.CreadoEn, "ActedAt (UTC) debe quedar después de CreatedAt (hora local)");

        // Checadas: primera entrada del día, salida movida a la última, retardo con incidencia
        var regs = Db(db => db.Registros.Where(r => r.ColaboradorId == cols[9101].Id).OrderBy(r => r.FechaHora).ToList());
        Assert.Equal(3, regs.Count);
        Assert.Equal(EstadoRegistro.Retardo, regs[0].Estado);
        Assert.Equal(new TimeOnly(19, 5), regs.Single(r => r.Tipo == TipoRegistro.Salida).HoraLocal);
        Assert.True(Db(db => db.Incidencias.Any(i => i.ColaboradorId == cols[9101].Id && i.Tipo == TipoIncidencia.Retardo && i.Fecha == Hoy.AddDays(-2))));

        // La pendiente se aprueba por la API de InovaOS: el jefe y luego Dirección
        var admin = await fx.Como("admin@wowtime.mx");
        var k = await admin.Ok(admin.Post("/llaves-api", new { nombre = "InovaOS migración (prueba)", permisos = new[] { "integracion.inovaos", "sucursales.todas" }, sucursales = Array.Empty<int>(), expira = (DateTime?)null }));
        async Task<JsonNode> Aprobar(string como)
        {
            var c = fx.Fabrica.CreateClient();
            c.DefaultRequestHeaders.Add("X-Api-Key", k["llave"]!.GetValue<string>());
            c.DefaultRequestHeaders.Add("X-Actuar-Como", como);
            var r = await c.PostAsJsonAsync($"/api/v1/integracion/inovaos/solicitudes/{pend.Id}/aprobar", new { comentario = "Ok" });
            var t = await r.Content.ReadAsStringAsync();
            Assert.True(r.IsSuccessStatusCode, t);
            return JsonNode.Parse(t)!;
        }
        Assert.Equal(2, (await Aprobar("jefe" + Dom))["siguienteNivel"]!.GetValue<int>());
        Assert.Equal("APPROVED", (await Aprobar("DIRECCION" + Dom))["estado"]!.GetValue<string>());
        Assert.Equal(12 - 2, Disp(cols[9102].Id));

        // Segunda corrida sin cambios en el origen: no duplica ni pisa lo decidido en WOW! Time
        var inf2 = await Correr(o);
        Assert.Equal(5, inf2.Conteos["colaboradores sin cambios"]);
        Assert.Equal(6, inf2.Conteos["solicitudes sin cambios"]);
        Assert.False(inf2.Conteos.ContainsKey("entradas importadas"));
        Assert.Equal(EstadoSolicitud.Aprobada, Db(db => db.Solicitudes.Single(s => s.OrigenId == 9004)).Estado);
        Assert.Equal(6, Db(db => db.Solicitudes.Count(s => s.OrigenId >= 9001 && s.OrigenId <= 9006)));
        Assert.Equal(3, Db(db => db.Registros.Count(r => r.ColaboradorId == cols[9101].Id)));

        // Si el origen cambia antes del corte, se actualiza solo lo que cambió
        o.Cols[2] = o.Cols[2] with { LastName = "Con Apellido Nuevo" };
        o.Sols[0] = o.Sols[0] with { DeductionValue = 2, DaysQuantity = 2 };
        var inf3 = await Correr(o);
        Assert.Equal(1, inf3.Conteos["colaboradores actualizados"]);
        Assert.Equal(1, inf3.Conteos["solicitudes actualizadas"]);
        Assert.Equal(22 - 2 - 0.3m - 2, Disp(cols[9101].Id));
        Assert.Equal("Con Apellido Nuevo", Db(db => db.Colaboradores.Single(c => c.OrigenId == 9102)).Apellidos);
    }
}
