using Microsoft.EntityFrameworkCore;
using WowTime.Api.Data;
using WowTime.Api.Domain;
using WowTime.Api.Infra;

namespace WowTime.Api.Services;

/// <summary>
/// Tareas programadas idempotentes. Se ejecutan al arrancar, cada hora mientras el sitio está activo,
/// y pueden dispararse desde el Programador de tareas de Windows con POST /api/v1/tareas/ejecutar.
/// </summary>
public class TareasProgramadas(AppDb db, IReloj reloj, Parametros parametros, ServicioSaldos saldos, ServicioAsistencia asistencia,
    Notificador notif, ILogger<TareasProgramadas> log)
{
    public static readonly string[] Nombres = ["asignaciones", "qr", "saldos", "vencimiento_dias", "documentos", "cumpleanos", "cierre_asistencia"];

    public async Task<Dictionary<string, string>> EjecutarTodas()
    {
        var res = new Dictionary<string, string>();
        foreach (var n in Nombres)
        {
            try { res[n] = await Ejecutar(n); }
            catch (Exception ex) { log.LogError(ex, "Tarea {Tarea} falló", n); res[n] = "Error: " + ex.Message; }
        }
        return res;
    }

    public async Task<string> Ejecutar(string nombre)
    {
        var hoy = Zona.Hoy(reloj, "America/Monterrey");
        var inicio = reloj.Ahora;
        string r = nombre switch
        {
            "asignaciones" => await Asignaciones(hoy),
            "qr" => await ExpirarQr(),
            "saldos" => await OtorgarSaldos(hoy),
            "vencimiento_dias" => await VencimientoDias(hoy),
            "documentos" => await Documentos(hoy),
            "cumpleanos" => await Cumpleanos(hoy),
            "cierre_asistencia" => await CierreAsistencia(hoy),
            _ => throw ApiException.NoEncontrado("La tarea")
        };
        var ej = await db.EjecucionesTarea.FirstOrDefaultAsync(e => e.Tarea == nombre && e.Fecha == hoy);
        if (ej is null) db.EjecucionesTarea.Add(ej = new EjecucionTarea { Tarea = nombre, Fecha = hoy });
        ej.Inicio = inicio; ej.Fin = reloj.Ahora; ej.Resultado = r;
        await db.SaveChangesAsync();
        return r;
    }

    private async Task<string> Asignaciones(DateOnly hoy)
    {
        var lista = await db.Asignaciones.ToListAsync();
        var n = 0;
        foreach (var a in lista)
        {
            var e = Reglas.EstadoAsignacion(a.FechaInicio, a.FechaFin, hoy, a.Cancelada);
            if (a.Estado != e) { a.Estado = e; n++; }
        }
        await db.SaveChangesAsync();
        return $"{n} asignaciones actualizadas";
    }

    private async Task<string> ExpirarQr()
    {
        var ahora = reloj.Ahora;
        var venc = await db.Invitaciones.Where(i => i.Estado == EstadoInvitacion.Esperado && i.VigenciaHasta < ahora).ToListAsync();
        foreach (var i in venc) i.Estado = EstadoInvitacion.Vencida;
        await db.SaveChangesAsync();
        return $"{venc.Count} invitaciones vencidas";
    }

    private async Task<string> OtorgarSaldos(DateOnly hoy)
    {
        var defecto = await db.Politicas.Include(p => p.Tabla).FirstOrDefaultAsync(p => p.PorDefecto);
        var politicas = await db.Politicas.Include(p => p.Tabla).ToDictionaryAsync(p => p.Id);
        var cols = await db.Colaboradores.Where(c => c.Estatus == EstatusColaborador.Activo).ToListAsync();
        var n = 0;
        foreach (var c in cols)
        {
            var pol = c.PoliticaVacacionesId is int p && politicas.TryGetValue(p, out var pp) ? pp : defecto;
            if (pol is null) continue;
            n += await saldos.Otorgar(c, pol, hoy);
        }
        await db.SaveChangesAsync();
        return $"{n} periodos otorgados";
    }

    private async Task<string> VencimientoDias(DateOnly hoy)
    {
        var vencidos = await saldos.Vencer(hoy);
        var alerta = await parametros.Entero(Parametros.AlertaVencimiento, 30);
        var limite = hoy.AddDays(alerta);
        var porVencer = await db.Saldos.Include(s => s.Colaborador)
            .Where(s => s.FechaLimite >= hoy && s.FechaLimite <= limite && s.DiasOtorgados - s.DiasUsados - s.DiasComprometidos - s.DiasVencidos > 0).ToListAsync();
        foreach (var s in porVencer)
            await notif.EnviarAColaborador(s.ColaboradorId, "dias_por_vencer", $"Tienes {s.Disponibles:0.#} días de vacaciones por vencer",
                $"Vencen el {s.FechaLimite:dd/MM/yyyy}. Planifica tu descanso.", "/mis-vacaciones", $"diasvencer:{s.Id}");
        await db.SaveChangesAsync();
        return $"{vencidos} saldos vencidos, {porVencer.Count} avisos de días por vencer";
    }

    private async Task<string> Documentos(DateOnly hoy)
    {
        var urg = await parametros.Entero(Parametros.DocUrgente, 7);
        var prox = await parametros.Entero(Parametros.DocProximo, 30);
        var limite = hoy.AddDays(prox);
        var docs = await db.Documentos.Include(d => d.TipoDocumento).Include(d => d.Colaborador)
            .Where(d => !d.Reemplazado && d.FechaVencimiento != null && d.FechaVencimiento <= limite && d.Colaborador!.Estatus == EstatusColaborador.Activo).ToListAsync();
        var n = 0;
        foreach (var d in docs)
        {
            var estado = Reglas.EstadoDocumento(d.FechaVencimiento, hoy, urg, prox);
            var dias = d.FechaVencimiento!.Value.DayNumber - hoy.DayNumber;
            var texto = dias < 0 ? $"venció hace {-dias} días" : $"vence en {dias} días";
            var clave = $"doc:{d.Id}:{estado}";
            await notif.EnviarAColaborador(d.ColaboradorId, "documento_por_vencer", $"{d.TipoDocumento!.Nombre} {texto}",
                "Renueva el documento para evitar inconvenientes.", "/mi-expediente", clave);
            await notif.EnviarARol(Roles.RecursosHumanos, d.Colaborador!.SucursalId, "documento_por_vencer",
                $"{d.TipoDocumento.Nombre} de {d.Colaborador.Nombres} {d.Colaborador.Apellidos} {texto}", null, $"/expedientes?colaborador={d.ColaboradorId}", clave);
            n++;
        }
        await db.SaveChangesAsync();
        return $"{n} documentos por vencer revisados";
    }

    private async Task<string> Cumpleanos(DateOnly hoy)
    {
        var cols = await db.Colaboradores.Where(c => c.Estatus == EstatusColaborador.Activo && c.FechaNacimiento != null
            && c.FechaNacimiento.Value.Month == hoy.Month && c.FechaNacimiento.Value.Day == hoy.Day).ToListAsync();
        foreach (var c in cols)
        {
            await notif.EnviarAColaborador(c.Id, "cumpleanos", $"¡Feliz cumpleaños, {c.Nombres.Split(' ')[0]}!", "Todo el equipo de WOW! Time te desea un gran día.", "/", $"cumple:{c.Id}:{hoy.Year}");
            await notif.EnviarARol(Roles.RecursosHumanos, c.SucursalId, "cumpleanos", $"Hoy es el cumpleaños de {c.Nombres} {c.Apellidos}", null, $"/colaboradores/{c.Id}", $"cumple:{c.Id}:{hoy.Year}");
        }
        await db.SaveChangesAsync();
        return $"{cols.Count} cumpleaños hoy";
    }

    /// <summary>Cierra los días anteriores pendientes (hasta 7 días atrás) y hoy si ya pasó la hora de cierre.</summary>
    private async Task<string> CierreAsistencia(DateOnly hoy)
    {
        var horaCierre = await parametros.Entero(Parametros.HoraCierreDiario, 23);
        var cerrados = await db.EjecucionesTarea.Where(e => e.Tarea.StartsWith("cierre_dia:")).Select(e => e.Tarea).ToListAsync();
        int f = 0, s = 0, dias = 0;
        var local = Zona.ALocal(reloj.Ahora, "America/Monterrey");
        for (var d = hoy.AddDays(-7); d <= hoy; d = d.AddDays(1))
        {
            if (d == hoy && local.Hour < horaCierre) break;
            var clave = $"cierre_dia:{d:yyyy-MM-dd}";
            if (cerrados.Contains(clave)) continue;
            var (ff, ss) = await asistencia.CerrarDia(d);
            f += ff; s += ss; dias++;
            db.EjecucionesTarea.Add(new EjecucionTarea { Tarea = clave, Fecha = d, Inicio = reloj.Ahora, Fin = reloj.Ahora, Resultado = $"{ff} faltas, {ss} salidas omitidas" });
            await db.SaveChangesAsync();
        }
        return $"{dias} días cerrados: {f} faltas, {s} salidas omitidas";
    }
}

/// <summary>Ejecuta las tareas al arrancar y luego cada hora.</summary>
public class TrabajadorTareas(IServiceProvider sp, ILogger<TrabajadorTareas> log, IConfiguration cfg) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (cfg["Tareas:Deshabilitar"] == "true") return;
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = sp.CreateScope();
                var r = await scope.ServiceProvider.GetRequiredService<TareasProgramadas>().EjecutarTodas();
                log.LogInformation("Tareas programadas: {Resultado}", string.Join("; ", r.Select(kv => $"{kv.Key}={kv.Value}")));
            }
            catch (Exception ex) { log.LogError(ex, "Error en tareas programadas"); }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
