using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Data;
using WowTime.Api.Domain;
using WowTime.Api.Infra;

namespace WowTime.Api.Services.Integracion;

public class OpcionesMigracion
{
    /// <summary>Sucursal única donde quedan los colaboradores de InovaOS.</summary>
    public string Sucursal { get; set; } = "Monterrey";
    /// <summary>Correos que reciben el rol Recursos Humanos.</summary>
    public string[] CorreosRh { get; set; } = [];
    /// <summary>Si nadie tiene el rol RH, Dirección General (la raíz del organigrama) lo recibe para que el paso de RH tenga quién lo decida.</summary>
    public bool RhALaRaizSiNoHay { get; set; } = true;
    /// <summary>Escala de vacaciones, tolerancia, salida no obligatoria, festivos y tipos de permiso según InovaOS.</summary>
    public bool AjustarReglas { get; set; } = true;
    public bool ImportarChecadas { get; set; } = true;
    /// <summary>Genera las faltas de los días ya pasados desde el inicio del cálculo de asistencia de InovaOS.</summary>
    public bool CerrarHistorico { get; set; } = true;
    public bool Justificaciones { get; set; } = true;

    public static OpcionesMigracion DesdeConfiguracion(IConfiguration cfg) => new()
    {
        Sucursal = cfg["Integracion:InovaOS:Sucursal"] is { Length: > 0 } s ? s : "Monterrey",
        CorreosRh = (cfg["Integracion:InovaOS:CorreosRh"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
    };
}

public class InformeMigracion
{
    public Dictionary<string, int> Conteos { get; } = new();
    public List<string> Avisos { get; } = [];
    public void Suma(string clave, int n = 1) => Conteos[clave] = Conteos.GetValueOrDefault(clave) + n;
    public override string ToString() => string.Join(", ", Conteos.Where(kv => kv.Value != 0).Select(kv => $"{kv.Key}: {kv.Value}"));
}

/// <summary>
/// Migración idempotente desde las vistas wowtime.* de InovaOS (docs/INTEGRACION_INOVAOS.md, sección 4) e importación de checadas.
/// Cada fila guarda su OrigenId y una huella de la fila de origen: si la fila no cambió, la migración no toca lo que ya existe en WOW! Time.
/// </summary>
public class MigradorInova(AppDb db, IOrigenInova origen, IReloj reloj, ServicioAsistencia asistencia, Bitacora bitacora, ILogger<MigradorInova> log)
{
    public const string Tz = "America/Monterrey";
    public const string Canal = "InovaOS";
    public const string NombreLector = "Lector facial (InovaOS)";

    /// <summary>CreatedAt de InovaOS se guarda en hora de Monterrey.</summary>
    private static DateTime Utc(DateTime local) => Zona.AUtc(local, Tz);
    /// <summary>ActedAt, BossApprovedDate, DirectorApprovedDate y las fechas de justificaciones ya vienen en UTC.</summary>
    private static DateTime EnUtc(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    /// <summary>Cambia cuando cambia la forma de migrar una solicitud, para que la siguiente corrida la reconstruya.</summary>
    private const string VersionSolicitud = "2";

    public static string Firma(params object?[] v) => Hash.Sha256(string.Join("|", v.Select(x => x switch
    {
        null => "",
        DateOnly d => d.ToString("yyyy-MM-dd"),
        DateTime t => t.ToString("yyyy-MM-ddTHH:mm:ss"),
        TimeOnly h => h.ToString("HH:mm"),
        decimal m => m.ToString("0.####", CultureInfo.InvariantCulture),
        _ => x.ToString()
    })))[..20];

    /// <summary>"1111100" (lunes a domingo) → días de descanso ISO.</summary>
    public static int[] Descanso(string diasLaborales)
    {
        var d = (diasLaborales ?? "").PadRight(7, '0');
        return Enumerable.Range(0, 7).Where(i => d[i] != '1').Select(i => i + 1).ToArray();
    }

    public static TipoSolicitud? Tipo(string requestType) => requestType.Trim().ToUpperInvariant() switch
    {
        "VACATION" => TipoSolicitud.Vacaciones,
        "CASH_OUT" => TipoSolicitud.CanjeDias,
        "PERMIT_3H" or "PERMIT_5H" or "PERMIT_DAY" => TipoSolicitud.Permiso,
        _ => null
    };

    public static EstadoSolicitud Estado(string? status) => (status ?? "").Trim().ToUpperInvariant() switch
    {
        "APPROVED" => EstadoSolicitud.Aprobada,
        "REJECTED" => EstadoSolicitud.Rechazada,
        "CANCELLED" or "CANCELED" => EstadoSolicitud.Cancelada,
        _ => EstadoSolicitud.Pendiente
    };

    /// <summary>La raíz del organigrama: activo sin jefe, preferentemente Dirección General, si no el que más reportes tiene.</summary>
    public static ColaboradorOrigen? Raiz(List<ColaboradorOrigen> fuente) => fuente.Where(f => f.Activo && f.ManagerID is null)
        .OrderByDescending(f => (f.JobTitle ?? "").Contains("direcc", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
        .ThenByDescending(f => fuente.Count(r => r.ManagerID == f.EmployeeID && r.Activo))
        .ThenBy(f => f.EmployeeID).FirstOrDefault();

    public async Task<InformeMigracion> Migrar(OpcionesMigracion o)
    {
        if (!origen.Configurado) throw new InvalidOperationException("Falta ConnectionStrings__Origen en backend/WowTime.Api/.env.");
        var inf = new InformeMigracion();
        var hoy = Zona.Hoy(reloj, Tz);

        // ── Sucursal y horarios ──
        var empresa = await db.Empresas.OrderBy(e => e.Id).FirstOrDefaultAsync();
        if (empresa is null) { db.Empresas.Add(empresa = new Empresa { Nombre = "WOW! Time", ZonaHoraria = Tz }); await db.SaveChangesAsync(); }
        var suc = await db.Sucursales.FirstOrDefaultAsync(s => s.Nombre.ToLower() == o.Sucursal.ToLower());
        if (suc is null)
        {
            db.Sucursales.Add(suc = new Sucursal { EmpresaId = empresa.Id, Nombre = o.Sucursal, ZonaHoraria = Tz });
            await db.SaveChangesAsync();
            inf.Suma("sucursales creadas");
            inf.Avisos.Add($"La sucursal {o.Sucursal} se creó sin coordenadas: captúralas en Configuración → Sucursales para usar la geocerca de la app.");
        }
        var cfgA = await origen.ConfigAsistencia();
        var general = cfgA is null ? null : await TurnoDe("General", cfgA.HoraEntrada, cfgA.HoraSalida, cfgA.DiasLaborales, cfgA.ToleranciaMin);
        var horarios = new Dictionary<int, Turno>();
        foreach (var h in (await origen.Horarios()).Where(h => h.VigenteDesde <= hoy && (h.VigenteHasta is null || h.VigenteHasta >= hoy)).OrderBy(h => h.VigenteDesde))
            horarios[h.EmployeeID] = await TurnoDe("Horario", h.HoraEntrada, h.HoraSalida, h.DiasLaborales, h.ToleranciaMin ?? cfgA?.ToleranciaMin ?? 10);

        // ── Colaboradores y organigrama ──
        var fuente = await origen.Colaboradores();
        var raiz = Raiz(fuente);
        int? JefeOrigen(ColaboradorOrigen f) => f.ManagerID ?? (f.Activo && raiz is not null && f.EmployeeID != raiz.EmployeeID ? raiz.EmployeeID : null);
        var porOrigen = await db.Colaboradores.Where(c => c.OrigenId != null).ToDictionaryAsync(c => c.OrigenId!.Value);
        var numeros = (await db.Colaboradores.Select(c => new { c.Id, c.Numero }).ToListAsync()).ToDictionary(x => x.Numero, x => x.Id);
        var areas = await db.Areas.Where(a => a.SucursalId == suc.Id).ToListAsync();
        var puestos = await db.Puestos.ToListAsync();
        var cambiados = new List<(Colaborador c, ColaboradorOrigen f)>();
        var nuevos = new List<Colaborador>();
        foreach (var f in fuente)
        {
            var turno = horarios.GetValueOrDefault(f.EmployeeID) ?? general;
            var firma = Firma(f.NumeroEmpleadoLector, f.FirstName, f.LastName, f.Email, f.JobTitle, f.Department, f.EntryDate, JefeOrigen(f), f.Activo, turno?.Nombre, suc.Id);
            if (!porOrigen.TryGetValue(f.EmployeeID, out var c))
            {
                // Adopta a quien ya se dio de alta en WOW! Time con el mismo correo
                var correo = f.Email.Trim().ToLower();
                c = await db.Colaboradores.FirstOrDefaultAsync(x => x.OrigenId == null && x.Email != null && x.Email.ToLower() == correo);
                if (c is null) { c = new Colaborador(); db.Colaboradores.Add(c); nuevos.Add(c); }
                c.OrigenId = f.EmployeeID;
                porOrigen[f.EmployeeID] = c;
            }
            else if (c.OrigenFirma == firma) { inf.Suma("colaboradores sin cambios"); continue; }

            var lector = string.IsNullOrWhiteSpace(f.NumeroEmpleadoLector) ? null : f.NumeroEmpleadoLector.Trim();
            var numero = lector ?? $"INV-{f.EmployeeID}";
            if (numeros.TryGetValue(numero, out var otro) && otro != c.Id) numero = $"INV-{f.EmployeeID}";
            if (c.Numero != numero) { if (c.Numero.Length > 0) numeros.Remove(c.Numero); c.Numero = numero; numeros[numero] = c.Id; }
            c.Nombres = f.FirstName ?? "";
            c.Apellidos = f.LastName ?? "";
            c.Email = f.Email.Trim();
            c.FechaIngreso = f.EntryDate;
            c.SucursalId = suc.Id;
            if (string.IsNullOrWhiteSpace(f.Department)) { c.Area = null; c.AreaId = null; } else c.Area = Area(areas, suc.Id, f.Department);
            if (string.IsNullOrWhiteSpace(f.JobTitle)) { c.Puesto = null; c.PuestoId = null; } else c.Puesto = Puesto(puestos, f.JobTitle);
            c.TurnoId = turno?.Id;
            // Quien no tiene número en el lector no checa en InovaOS: no se le generan faltas
            c.Esquema ??= await db.Esquemas.FirstOrDefaultAsync(x => x.ColaboradorId == c.Id && c.Id != 0)
                ?? new EsquemaTrabajo { Modalidad = Modalidad.Presencial, DiasOficina = Descanso(cfgA?.DiasLaborales ?? "1111100").Length == 2 ? [1, 2, 3, 4, 5] : [1, 2, 3, 4, 5, 6] };
            c.Esquema.CentroBaseSucursalId = suc.Id;
            c.Esquema.RegistroPermitido = lector is null ? MetodoRegistro.Ninguno : MetodoRegistro.TerminalYApp;
            if (f.Activo)
            {
                if (c.Estatus == EstatusColaborador.Baja) { c.FechaBaja = null; c.MotivoBaja = null; }
                c.Estatus = EstatusColaborador.Activo;
            }
            else if (c.Estatus != EstatusColaborador.Baja)
            {
                c.Estatus = EstatusColaborador.Baja;
                c.FechaBaja = hoy;
                c.MotivoBaja = "Baja detectada en InovaOS: el usuario quedó inactivo.";
            }
            c.OrigenFirma = firma;
            cambiados.Add((c, f));
        }
        await db.SaveChangesAsync(); // ids de áreas, puestos y colaboradores nuevos
        foreach (var (c, f) in cambiados)
        {
            var jefe = JefeOrigen(f) is int j && porOrigen.TryGetValue(j, out var cj) && cj.Id != c.Id ? cj.Id : (int?)null;
            if (f.ManagerID is null && jefe is not null && raiz is not null)
                inf.Avisos.Add($"{c.NombreCompleto} no tenía jefe en InovaOS: queda bajo {porOrigen[raiz.EmployeeID].NombreCompleto}, como aprobaba InovaOS.");
            c.SupervisorId = jefe;
            if (nuevos.Contains(c))
            {
                bitacora.Registrar(c.Id, "colaborador", "Alta por migración desde InovaOS", $"EmployeeID {f.EmployeeID}", "Colaborador", c.Id);
                inf.Suma("colaboradores creados");
            }
            else inf.Suma("colaboradores actualizados");
        }
        if (raiz is not null && porOrigen.TryGetValue(raiz.EmployeeID, out var cr) && cambiados.Any(x => x.c == cr))
            inf.Avisos.Add($"{cr.NombreCompleto} es la raíz del organigrama: sus propias solicitudes las captura Recursos Humanos.");
        await db.SaveChangesAsync();

        var cols = porOrigen.Values.ToList();
        await Usuarios(cols, raiz is null ? null : porOrigen.GetValueOrDefault(raiz.EmployeeID), suc, o, inf);
        if (o.AjustarReglas) await ReglasInova(cfgA, inf);

        // ── Saldos, solicitudes y aprobaciones ──
        var saldosTocados = new HashSet<int>();
        var saldos = await Saldos(porOrigen, inf, saldosTocados);
        await Solicitudes(porOrigen, saldos, inf, saldosTocados);
        await Recalcular(saldosTocados);
        await db.SaveChangesAsync();

        // ── Asistencia ──
        if (o.ImportarChecadas && origen.ChecadasConfiguradas)
        {
            var r = await ImportarChecadas(cfgA?.InicioCalculo, notificar: false);
            foreach (var kv in r.Conteos) inf.Suma(kv.Key, kv.Value);
            inf.Avisos.AddRange(r.Avisos);
        }
        if (o.CerrarHistorico && cfgA is not null)
        {
            int faltas = 0, salidas = 0;
            for (var d = cfgA.InicioCalculo; d < hoy; d = d.AddDays(1))
            {
                var (f, s) = await asistencia.CerrarDia(d, notificar: false);
                faltas += f; salidas += s;
            }
            inf.Suma("faltas generadas (días pasados)", faltas);
            inf.Suma("salidas omitidas generadas", salidas);
        }
        if (o.Justificaciones) await Justificaciones(porOrigen, inf);
        await db.SaveChangesAsync();
        log.LogInformation("Migración InovaOS: {Informe}", inf);
        return inf;
    }

    private async Task<Turno> TurnoDe(string prefijo, TimeOnly entrada, TimeOnly salida, string dias, int tolerancia)
    {
        var descanso = Descanso(dias);
        var extra = descanso.SequenceEqual([6, 7]) ? "" : $" ({string.Join("", (dias ?? "").PadRight(7, '0').Select((ch, i) => ch == '1' ? "LMXJVSD"[i] : '·'))})";
        var nombre = $"{prefijo} {entrada:HH\\:mm}–{salida:HH\\:mm}{extra}";
        var t = await db.Turnos.FirstOrDefaultAsync(x => x.Nombre == nombre);
        if (t is null) db.Turnos.Add(t = new Turno { Nombre = nombre });
        t.HoraEntrada = entrada; t.HoraSalida = salida; t.ToleranciaMinutos = tolerancia; t.DiasDescanso = descanso; t.Activo = true;
        await db.SaveChangesAsync();
        return t;
    }

    private Area Area(List<Area> areas, int sucursalId, string nombre)
    {
        nombre = nombre.Trim();
        var a = areas.FirstOrDefault(x => string.Equals(x.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        if (a is null) { areas.Add(a = new Area { SucursalId = sucursalId, Nombre = nombre }); db.Areas.Add(a); }
        return a;
    }

    private Puesto Puesto(List<Puesto> puestos, string nombre)
    {
        nombre = nombre.Trim();
        var p = puestos.FirstOrDefault(x => string.Equals(x.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        if (p is null) { puestos.Add(p = new Puesto { Nombre = nombre }); db.Puestos.Add(p); }
        return p;
    }

    /// <summary>Un usuario por colaborador, para que pueda actuar por InovaOS (X-Actuar-Como) y entrar a WOW! Time.</summary>
    private async Task Usuarios(List<Colaborador> cols, Colaborador? raiz, Sucursal suc, OpcionesMigracion o, InformeMigracion inf)
    {
        var usuarios = await db.Usuarios.ToListAsync();
        var conReportes = cols.Where(c => c.Estatus == EstatusColaborador.Activo && c.SupervisorId != null).Select(c => c.SupervisorId!.Value).ToHashSet();
        var correosRh = o.CorreosRh.Select(x => x.ToLowerInvariant()).ToHashSet();
        var ids = cols.Select(c => c.Id).ToHashSet();
        var hayRh = correosRh.Count > 0 || usuarios.Any(u => u.Activo && u.Roles.Contains(Roles.RecursosHumanos) && u.ColaboradorId is int ci && ids.Contains(ci));
        foreach (var c in cols.Where(c => !string.IsNullOrWhiteSpace(c.Email)))
        {
            var correo = c.Email!.Trim().ToLowerInvariant();
            var u = usuarios.FirstOrDefault(x => x.ColaboradorId == c.Id) ?? usuarios.FirstOrDefault(x => x.Email.ToLower() == correo);
            if (u is null)
            {
                // Sin contraseña: entra por InovaOS o con "¿Olvidaste tu contraseña?" en WOW! Time
                u = new Usuario { Email = correo, PasswordHash = "", DebeCambiarPassword = true, Roles = [Roles.Colaborador], Sucursales = [suc.Id] };
                db.Usuarios.Add(u);
                usuarios.Add(u);
                inf.Suma("usuarios creados");
            }
            u.ColaboradorId = c.Id;
            u.Nombre = c.NombreCompleto;
            u.Activo = c.Estatus != EstatusColaborador.Baja;
            var roles = u.Roles.ToHashSet();
            roles.Add(Roles.Colaborador);
            if (conReportes.Contains(c.Id)) roles.Add(Roles.Supervisor);
            if (c == raiz) roles.Add(Roles.Administrador);
            if (correosRh.Contains(correo) || (!hayRh && o.RhALaRaizSiNoHay && c == raiz)) roles.Add(Roles.RecursosHumanos);
            if (!roles.SetEquals(u.Roles)) u.Roles = roles.OrderBy(r => r).ToArray();
            if (u.Sucursales.Length == 0 && !roles.Contains(Roles.Administrador)) u.Sucursales = [suc.Id];
        }
        if (!hayRh && raiz is not null && o.RhALaRaizSiNoHay)
            inf.Avisos.Add($"Nadie tenía el rol de Recursos Humanos: lo recibió {raiz.NombreCompleto}. Indica los correos de RH en Integracion__InovaOS__CorreosRh y vuelve a migrar.");
        await db.SaveChangesAsync();
    }

    /// <summary>Reglas de InovaOS que se conservan: escala de vacaciones, tolerancia, checada única al llegar, festivos y permisos con clave.</summary>
    private async Task ReglasInova(ConfigAsistenciaOrigen? cfgA, InformeMigracion inf)
    {
        var escala = await origen.Escala();
        var pol = await db.Politicas.Include(p => p.Tabla).FirstOrDefaultAsync(p => p.PorDefecto);
        if (pol is not null && escala.Count > 0)
        {
            var actual = string.Join(";", pol.Tabla.OrderBy(t => t.AniosDesde).Select(t => $"{t.AniosDesde}-{t.AniosHasta}:{t.Dias}"));
            var nueva = string.Join(";", escala.OrderBy(e => e.MinYears).Select(e => $"{e.MinYears}-{e.MaxYears}:{e.DaysEntitled}"));
            if (actual != nueva)
            {
                db.RemoveRange(pol.Tabla.ToList());
                pol.Tabla = escala.Select(e => new PoliticaAntiguedad { AniosDesde = e.MinYears, AniosHasta = e.MaxYears, Dias = e.DaysEntitled }).ToList();
                inf.Suma("escala de vacaciones actualizada");
            }
            pol.PlazoVencimientoMeses = 12;
        }
        var pars = await db.Parametros.ToDictionaryAsync(p => p.Clave);
        if (cfgA is not null && pars.TryGetValue(Parametros.ToleranciaMinutos, out var tol)) tol.Valor = cfgA.ToleranciaMin.ToString();
        // En InovaOS casi todos checan solo al llegar: no se genera "Salida omitida"
        if (pars.TryGetValue(Parametros.ExigirSalida, out var sal)) sal.Valor = "false";
        foreach (var f in await origen.Festivos())
            if (!await db.DiasFestivos.AnyAsync(x => x.Fecha == f.Fecha)) { db.DiasFestivos.Add(new DiaFestivo { Fecha = f.Fecha, Nombre = f.Nombre }); inf.Suma("festivos agregados"); }
        // InovaOS solo maneja permisos de 3 h, 5 h y día completo
        foreach (var t in await db.TiposPermiso.Where(t => t.Clave == null && t.Activo).ToListAsync()) { t.Activo = false; inf.Suma("tipos de permiso desactivados"); }
        foreach (var t in await db.TiposPermiso.Where(t => t.Clave != null && !t.Activo).ToListAsync()) t.Activo = true;
        await db.SaveChangesAsync();
    }

    /// <summary>Un saldo por periodo de InovaOS, que vence en FechaFin. Se respeta DiasDerecho aunque difiera de la escala.</summary>
    private async Task<Dictionary<int, SaldoVacaciones>> Saldos(Dictionary<int, Colaborador> porOrigen, InformeMigracion inf, HashSet<int> tocados)
    {
        var res = await db.Saldos.Where(s => s.OrigenId != null).ToDictionaryAsync(s => s.OrigenId!.Value);
        foreach (var p in await origen.Periodos())
        {
            if (!porOrigen.TryGetValue(p.EmployeeID, out var c)) { inf.Avisos.Add($"Periodo {p.PeriodoID}: el colaborador {p.EmployeeID} no existe en el origen."); continue; }
            var firma = Firma(p.NumeroPeriodo, p.FechaInicio, p.FechaFin, p.DiasDerecho, p.Notas, c.Id);
            var periodo = Math.Max(0, p.NumeroPeriodo - 1); // en WOW! Time, el periodo es el número de años cumplidos al otorgarse
            if (!res.TryGetValue(p.PeriodoID, out var s))
            {
                s = await db.Saldos.FirstOrDefaultAsync(x => x.ColaboradorId == c.Id && x.Periodo == periodo);
                if (s is null) { s = new SaldoVacaciones { ColaboradorId = c.Id }; db.Saldos.Add(s); inf.Suma("saldos creados"); }
                else inf.Suma("saldos adoptados");
                res[p.PeriodoID] = s;
            }
            else if (s.OrigenFirma == firma) continue;
            else inf.Suma("saldos actualizados");
            s.ColaboradorId = c.Id;
            s.Periodo = periodo;
            s.FechaOtorgamiento = p.FechaInicio;
            s.FechaLimite = p.FechaFin;
            s.DiasOtorgados = p.DiasDerecho;
            s.Notas = p.Notas;
            s.OrigenId = p.PeriodoID;
            s.OrigenFirma = firma;
            await db.SaveChangesAsync();
            var otorg = await db.MovimientosSaldo.FirstOrDefaultAsync(m => m.SaldoVacacionesId == s.Id && m.Tipo == TipoMovimientoSaldo.Otorgamiento);
            if (otorg is null)
                db.MovimientosSaldo.Add(otorg = new MovimientoSaldo { SaldoVacacionesId = s.Id, ColaboradorId = c.Id, Tipo = TipoMovimientoSaldo.Otorgamiento, Fecha = reloj.Ahora });
            otorg.Dias = p.DiasDerecho;
            otorg.Motivo = $"Periodo {p.NumeroPeriodo} de InovaOS" + (string.IsNullOrWhiteSpace(p.Notas) ? "" : $" · {p.Notas}");
            tocados.Add(s.Id);
        }
        await db.SaveChangesAsync();
        return res;
    }

    /// <summary>Solicitudes con su cadena: aprobadas como consumo del periodo, pendientes en el mismo paso, rechazadas sin mover saldo.</summary>
    private async Task Solicitudes(Dictionary<int, Colaborador> porOrigen, Dictionary<int, SaldoVacaciones> saldos, InformeMigracion inf, HashSet<int> tocados)
    {
        var aprob = (await origen.Aprobaciones()).GroupBy(a => a.RequestID).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Nivel).ToList());
        var existentes = await db.Solicitudes.Include(s => s.Pasos).Where(s => s.OrigenId != null).ToDictionaryAsync(s => s.OrigenId!.Value);
        var tipos = await db.TiposPermiso.Where(t => t.Clave != null).ToDictionaryAsync(t => t.Clave!);
        var usuarioDe = (await db.Usuarios.Where(u => u.ColaboradorId != null).Select(u => new { u.Id, C = u.ColaboradorId!.Value }).ToListAsync())
            .GroupBy(x => x.C).ToDictionary(g => g.Key, g => g.Min(x => x.Id));
        foreach (var r in await origen.Solicitudes())
        {
            if (!porOrigen.TryGetValue(r.EmployeeID, out var c)) { inf.Avisos.Add($"Solicitud {r.RequestID}: el colaborador {r.EmployeeID} no existe en el origen."); continue; }
            var tipo = Tipo(r.RequestType);
            if (tipo is null) { inf.Avisos.Add($"Solicitud {r.RequestID}: tipo {r.RequestType} desconocido, no se migró."); continue; }
            var filas = aprob.GetValueOrDefault(r.RequestID) ?? [];
            var firma = Firma(VersionSolicitud, r.RequestType, r.StartDate, r.EndDate, r.DaysQuantity, r.DeductionValue, r.Reason, r.Status, r.PeriodoID, r.SinDescuento,
                r.NotaAjuste, r.BossApproved, r.DirectorApproved, c.Id,
                string.Join(";", filas.Select(a => $"{a.Nivel}:{a.Status}:{a.ApproverEmployeeID}:{a.ActedAt:yyyy-MM-ddTHH:mm:ss}:{a.Comment}")));
            if (existentes.TryGetValue(r.RequestID, out var s) && s.OrigenFirma == firma) { inf.Suma("solicitudes sin cambios"); continue; }
            var estado = Estado(r.Status);
            if (s is null)
            {
                s = new Solicitud { Folio = $"TMP-{Guid.NewGuid():N}"[..20] };
                db.Solicitudes.Add(s);
                existentes[r.RequestID] = s;
                inf.Suma("solicitudes creadas");
            }
            else
            {
                // La fila cambió en InovaOS (todavía se usa allá antes del corte): se reconstruye desde el origen
                var movs = await db.MovimientosSaldo.Where(m => m.SolicitudId == s.Id).ToListAsync();
                foreach (var m in movs) tocados.Add(m.SaldoVacacionesId);
                db.MovimientosSaldo.RemoveRange(movs);
                db.RemoveRange(s.Pasos.ToList());
                s.Pasos = [];
                inf.Suma("solicitudes actualizadas");
            }
            var ultima = filas.Where(a => a.ActedAt != null).OrderByDescending(a => a.Nivel).FirstOrDefault();
            var rechazo = filas.FirstOrDefault(a => a.Status.Equals("REJECTED", StringComparison.OrdinalIgnoreCase));
            s.Tipo = tipo.Value;
            s.ColaboradorId = c.Id;
            s.SucursalId = c.SucursalId;
            s.TipoPermisoId = tipo == TipoSolicitud.Permiso && tipos.TryGetValue(r.RequestType.Trim().ToUpperInvariant(), out var tp) ? tp.Id : null;
            s.FechaInicio = r.StartDate;
            s.FechaFin = r.EndDate < r.StartDate ? r.StartDate : r.EndDate;
            s.DiasHabiles = r.DaysQuantity;
            s.Descuento = r.SinDescuento ? 0 : r.DeductionValue;
            s.Comentario = r.SinDescuento && !string.IsNullOrWhiteSpace(r.NotaAjuste) ? $"{r.Reason} · Sin descuento: {r.NotaAjuste}".Trim(' ', '·') : r.Reason;
            s.Estado = estado;
            s.Canal = Canal;
            s.ReglaAprobacionId = null;
            s.ReglaNombre = "InovaOS";
            s.CapturadaPorUsuarioId = usuarioDe.GetValueOrDefault(c.Id);
            s.OrigenId = r.RequestID;
            s.OrigenFirma = firma;
            if (r.CreatedAt is DateTime ca) s.CreadoEn = Utc(ca);
            s.ResueltaEn = estado == EstadoSolicitud.Pendiente ? null
                : (ultima?.ActedAt ?? r.DirectorApprovedDate ?? r.BossApprovedDate) is DateTime fr ? EnUtc(fr) : r.CreatedAt is DateTime cr ? Utc(cr) : null;
            s.MotivoRechazo = estado == EstadoSolicitud.Rechazada ? rechazo?.Comment ?? "Rechazada en InovaOS." : null;

            if (filas.Count > 0)
            {
                var primera = true;
                foreach (var a in filas)
                {
                    int? titular = a.ApproverEmployeeID is int e && porOrigen.TryGetValue(e, out var tc) ? tc.Id : null;
                    var st = a.Status.Trim().ToUpperInvariant();
                    var ep = st switch
                    {
                        "APPROVED" => EstadoPaso.Aprobado,
                        "REJECTED" => EstadoPaso.Rechazado,
                        "PENDING" when estado == EstadoSolicitud.Pendiente => primera ? EstadoPaso.Pendiente : EstadoPaso.EnEspera,
                        _ => EstadoPaso.Omitido
                    };
                    if (ep == EstadoPaso.Pendiente) primera = false;
                    var decidido = ep is EstadoPaso.Aprobado or EstadoPaso.Rechazado;
                    s.Pasos.Add(new PasoAprobacion
                    {
                        Orden = a.Nivel, TipoAprobador = a.Nivel == 1 ? TipoAprobador.SupervisorDirecto : TipoAprobador.Gerente,
                        TitularColaboradorId = titular, Estado = ep,
                        DecididoPorNombre = decidido ? a.ApproverName : null,
                        DecididoPorUsuarioId = decidido && titular is int t2 && usuarioDe.TryGetValue(t2, out var uid) ? uid : null,
                        Fecha = decidido && a.ActedAt is DateTime f ? EnUtc(f) : null,
                        Motivo = decidido ? a.Comment : null,
                        NotaBitacora = decidido ? "Decidido en InovaOS" : null
                    });
                }
            }
            else if (estado is EstadoSolicitud.Aprobada or EstadoSolicitud.Rechazada)
            {
                var fecha = (r.DirectorApprovedDate ?? r.BossApprovedDate) is DateTime fd ? EnUtc(fd) : r.CreatedAt is DateTime fc ? Utc(fc) : (DateTime?)null;
                s.Pasos.Add(new PasoAprobacion
                {
                    Orden = 1, TipoAprobador = TipoAprobador.SupervisorDirecto,
                    Estado = estado == EstadoSolicitud.Aprobada ? EstadoPaso.Aprobado : EstadoPaso.Rechazado,
                    DecididoPorNombre = "InovaOS (histórico)", Fecha = fecha, NotaBitacora = "Decidido en InovaOS"
                });
            }
            if (estado == EstadoSolicitud.Pendiente && !s.Pasos.Any(p => p.Estado == EstadoPaso.Pendiente))
                inf.Avisos.Add($"Solicitud {r.RequestID} pendiente sin paso pendiente en InovaOS: revísala en WOW! Time.");
            s.PasoActual = s.Pasos.FirstOrDefault(p => p.Estado == EstadoPaso.Pendiente)?.Orden ?? (s.Pasos.Count > 0 ? s.Pasos.Max(p => p.Orden) : 1);
            await db.SaveChangesAsync();
            if (s.Folio.StartsWith("TMP-")) s.Folio = ServicioSolicitudes.PrefijoFolio(s.Tipo) + s.Id.ToString("D5");

            // Saldo: pendientes comprometen y aprobadas consumen el periodo indicado
            if (s.DiasADescontar > 0 && estado is EstadoSolicitud.Pendiente or EstadoSolicitud.Aprobada)
            {
                if (r.PeriodoID is int pid && saldos.TryGetValue(pid, out var saldo))
                {
                    var motivo = $"Solicitud {s.Folio} (InovaOS {r.RequestID})";
                    db.MovimientosSaldo.Add(new MovimientoSaldo { SaldoVacacionesId = saldo.Id, ColaboradorId = c.Id, Tipo = TipoMovimientoSaldo.Compromiso, Dias = s.DiasADescontar, SolicitudId = s.Id, Fecha = s.CreadoEn == default ? reloj.Ahora : s.CreadoEn, Motivo = motivo });
                    if (estado == EstadoSolicitud.Aprobada)
                        db.MovimientosSaldo.Add(new MovimientoSaldo { SaldoVacacionesId = saldo.Id, ColaboradorId = c.Id, Tipo = TipoMovimientoSaldo.Consumo, Dias = s.DiasADescontar, SolicitudId = s.Id, Fecha = s.ResueltaEn ?? reloj.Ahora, Motivo = motivo });
                    tocados.Add(saldo.Id);
                }
                else inf.Avisos.Add($"Solicitud {r.RequestID}: no tiene periodo en InovaOS, no se descontó del saldo.");
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Recalcula usados y comprometidos a partir de los movimientos.</summary>
    private async Task Recalcular(HashSet<int> saldoIds)
    {
        foreach (var id in saldoIds)
        {
            var s = await db.Saldos.FindAsync(id);
            if (s is null) continue;
            var movs = await db.MovimientosSaldo.Where(m => m.SaldoVacacionesId == id).ToListAsync();
            decimal Suma(TipoMovimientoSaldo t) => movs.Where(m => m.Tipo == t).Sum(m => m.Dias);
            s.DiasUsados = Suma(TipoMovimientoSaldo.Consumo);
            s.DiasComprometidos = Suma(TipoMovimientoSaldo.Compromiso) - Suma(TipoMovimientoSaldo.Liberacion) - Suma(TipoMovimientoSaldo.Consumo);
        }
    }

    private async Task Justificaciones(Dictionary<int, Colaborador> porOrigen, InformeMigracion inf)
    {
        foreach (var j in await origen.Justificaciones())
        {
            if (!porOrigen.TryGetValue(j.EmployeeID, out var c)) continue;
            TipoIncidencia? tipo = j.Tipo.Trim().ToLowerInvariant() switch
            {
                "retardo" => TipoIncidencia.Retardo,
                "falta" or "comision" or "home_office" => TipoIncidencia.Falta,
                "sin_salida" => TipoIncidencia.SalidaOmitida,
                _ => null
            };
            if (tipo is null) { inf.Avisos.Add($"Justificación {j.JustificacionID} de tipo {j.Tipo}: WOW! Time no tiene ese tipo de incidencia, no se migró."); continue; }
            var estado = j.Estado.Trim().ToLowerInvariant() switch
            {
                "aprobada" => EstadoIncidencia.Justificada, "rechazada" => EstadoIncidencia.Confirmada, _ => EstadoIncidencia.PorRevisar
            };
            var inc = await db.Incidencias.FirstOrDefaultAsync(i => i.ColaboradorId == c.Id && i.Fecha == j.Fecha && i.Tipo == tipo);
            if (inc is null)
            {
                if (estado != EstadoIncidencia.Justificada) continue;
                db.Incidencias.Add(inc = new IncidenciaAsistencia { ColaboradorId = c.Id, SucursalId = c.SucursalId, Fecha = j.Fecha, Tipo = tipo.Value, Detalle = $"Justificación de InovaOS ({j.Tipo})" });
            }
            if (inc.Justificacion == j.Motivo && inc.Estado == estado) continue;
            inc.Justificacion = j.Motivo;
            inc.Estado = estado;
            inc.ComentarioResolucion = j.Respuesta ?? (j.ResueltoNombre is null ? null : $"Resuelta por {j.ResueltoNombre} en InovaOS");
            inc.ResueltaEn = j.ResueltoEn is DateTime r ? EnUtc(r) : null;
            inf.Suma("justificaciones migradas");
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Importa las checadas nuevas de wotime_inova (EventoId mayor al último importado). El lector no distingue entrada y salida:
    /// la primera del día es la entrada, la última es la salida y una checada a menos de 2 minutos de la anterior se ignora.
    /// </summary>
    public async Task<InformeMigracion> ImportarChecadas(DateOnly? desde = null, bool notificar = true)
    {
        var inf = new InformeMigracion();
        if (!origen.ChecadasConfiguradas) { inf.Avisos.Add("Falta ConnectionStrings__OrigenChecadas."); return inf; }
        var hoy = Zona.Hoy(reloj, Tz);
        var inicio = desde ?? (origen.Configurado ? (await origen.ConfigAsistencia())?.InicioCalculo : null) ?? hoy.AddDays(-30);
        var ultimo = await db.Registros.MaxAsync(r => r.OrigenId) ?? 0;
        var eventos = await origen.Checadas(ultimo, inicio);
        if (eventos.Count == 0) return inf;

        var lector = (await origen.Colaboradores()).Where(f => !string.IsNullOrWhiteSpace(f.NumeroEmpleadoLector))
            .GroupBy(f => f.NumeroEmpleadoLector!.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().EmployeeID, StringComparer.OrdinalIgnoreCase);
        var cols = await db.Colaboradores.Include(c => c.Turno).Include(c => c.Esquema).Include(c => c.Sucursal)
            .Where(c => c.OrigenId != null).ToDictionaryAsync(c => c.OrigenId!.Value);
        var dispositivos = new Dictionary<int, Dispositivo>();
        var sinColaborador = new HashSet<string>();
        foreach (var e in eventos.OrderBy(x => x.EventoId))
        {
            var num = e.NumeroEmpleado?.Trim();
            if (num is null || !lector.TryGetValue(num, out var eid) || !cols.TryGetValue(eid, out var c)) { sinColaborador.Add(num ?? "?"); inf.Suma("checadas sin colaborador"); continue; }
            var fecha = DateOnly.FromDateTime(e.FechaLocal);
            var hora = TimeOnly.FromDateTime(e.FechaLocal);
            var utc = e.FechaEvento.UtcDateTime;
            var dia = await db.Registros.Where(r => r.ColaboradorId == c.Id && r.FechaLocal == fecha).OrderBy(r => r.FechaHora).ToListAsync();
            var previo = dia.LastOrDefault();
            if (previo is not null && (utc - previo.FechaHora).Duration() < TimeSpan.FromMinutes(2)) { inf.Suma("checadas repetidas"); continue; }
            if (!dispositivos.TryGetValue(c.SucursalId, out var disp))
            {
                disp = await db.Dispositivos.FirstOrDefaultAsync(d => d.Nombre == NombreLector && d.SucursalId == c.SucursalId);
                if (disp is null) { db.Dispositivos.Add(disp = new Dispositivo { Nombre = NombreLector, Tipo = TipoDispositivo.Terminal, SucursalId = c.SucursalId }); await db.SaveChangesAsync(); }
                dispositivos[c.SucursalId] = disp;
            }
            var j = await asistencia.JornadaDe(c, fecha);
            var parcial = await db.Solicitudes.AnyAsync(s => s.ColaboradorId == c.Id && s.Estado == EstadoSolicitud.Aprobada && s.Tipo == TipoSolicitud.Permiso
                && s.TipoPermiso != null && s.TipoPermiso.Parcial && s.FechaInicio <= fecha && s.FechaFin >= fecha);
            var entrada = dia.FirstOrDefault(r => r.Tipo == TipoRegistro.Entrada);
            var salida = dia.FirstOrDefault(r => r.Tipo == TipoRegistro.Salida);
            EstadoRegistro EstadoSalida() => j.HoraSalida is TimeOnly hs && hora < hs && !parcial ? EstadoRegistro.SalidaAnticipada : EstadoRegistro.SalidaRegistrada;
            if (entrada is null)
            {
                var estado = j.HoraEntrada is TimeOnly h0 ? WowTime.Api.Services.Reglas.EstadoEntrada(hora, h0, j.Tolerancia) : EstadoRegistro.EnHorario;
                if (estado == EstadoRegistro.Retardo && parcial) estado = EstadoRegistro.EnHorario; // los permisos de 3 y 5 horas perdonan el retardo
                var reg = Nuevo(c, TipoRegistro.Entrada, utc, fecha, hora, disp, j.Modalidad, estado, e.EventoId);
                db.Registros.Add(reg);
                await db.SaveChangesAsync();
                inf.Suma("entradas importadas");
                if (estado == EstadoRegistro.Retardo)
                    await asistencia.CrearIncidencia(c, fecha, TipoIncidencia.Retardo, reg.Id,
                        $"Entrada a las {hora:HH\\:mm} en el lector; horario {j.HoraEntrada:HH\\:mm} con {j.Tolerancia} min de tolerancia.", notificar && fecha == hoy);
            }
            else if (salida is null)
            {
                db.Registros.Add(Nuevo(c, TipoRegistro.Salida, utc, fecha, hora, disp, j.Modalidad, EstadoSalida(), e.EventoId));
                inf.Suma("salidas importadas");
            }
            else
            {
                salida.FechaHora = utc; salida.HoraLocal = hora; salida.OrigenId = e.EventoId; salida.Estado = EstadoSalida();
                inf.Suma("salidas actualizadas");
            }
            await db.SaveChangesAsync();
        }
        if (sinColaborador.Count > 0) inf.Avisos.Add($"Checadas de números del lector sin colaborador en InovaOS: {string.Join(", ", sinColaborador.Order())}.");
        log.LogInformation("Checadas InovaOS: {Informe}", inf);
        return inf;
    }

    private static RegistroAsistencia Nuevo(Colaborador c, TipoRegistro tipo, DateTime utc, DateOnly fecha, TimeOnly hora, Dispositivo disp, Modalidad modalidad, EstadoRegistro estado, long eventoId) => new()
    {
        ColaboradorId = c.Id, SucursalId = c.SucursalId, Tipo = tipo, FechaHora = utc, FechaLocal = fecha, HoraLocal = hora, Origen = OrigenRegistro.Terminal,
        DispositivoId = disp.Id, Modalidad = modalidad, Estado = estado, OrigenId = eventoId, LugarAutorizado = disp.Nombre
    };
}

/// <summary>Importa checadas cada 15 minutos (y, si se pide, vuelve a migrar cada hora) mientras Integracion__InovaOS__Activa=true.</summary>
public class TrabajadorInovaOS(IServiceProvider sp, IConfiguration cfg, ILogger<TrabajadorInovaOS> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (cfg["Integracion:InovaOS:Activa"] != "true" || cfg["Tareas:Deshabilitar"] == "true") return;
        var minutos = int.TryParse(cfg["Integracion:InovaOS:MinutosChecadas"], out var m) && m > 0 ? m : 15;
        var cadaHora = cfg["Integracion:InovaOS:MigrarCadaHora"] == "true";
        var ultimaMigracion = DateTime.MinValue;
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = sp.CreateScope();
                var mig = scope.ServiceProvider.GetRequiredService<MigradorInova>();
                if (cadaHora && DateTime.UtcNow - ultimaMigracion >= TimeSpan.FromHours(1))
                {
                    var o = OpcionesMigracion.DesdeConfiguracion(cfg);
                    o.ImportarChecadas = false; o.CerrarHistorico = false; o.AjustarReglas = false;
                    await mig.Migrar(o);
                    ultimaMigracion = DateTime.UtcNow;
                }
                await mig.ImportarChecadas();
            }
            catch (Exception ex) { log.LogError(ex, "Error al importar desde InovaOS"); }
            await Task.Delay(TimeSpan.FromMinutes(minutos), ct);
        }
    }
}
