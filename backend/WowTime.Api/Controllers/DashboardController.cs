using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Indicadores y gráfica del resumen de hoy.</summary>
[Tags("Dashboard")]
public class DashboardController : ApiBase
{
    /// <summary>Los 4 indicadores del día: Presentes hoy, Asistencia puntual, Incidencias por revisar y Visitantes dentro.</summary>
    [HttpGet("dashboard/resumen"), Permiso(Permisos.DashboardVer, Permisos.EquipoVer)]
    public async Task<object> Resumen([FromQuery] int? sucursalId)
    {
        var suc = Yo.Sucursal(sucursalId);
        var tz = suc is int s0 ? await Db.Sucursales.Where(x => x.Id == s0).Select(x => x.ZonaHoraria).FirstOrDefaultAsync() : null;
        var ahoraLocal = Zona.ALocal(Reloj.Ahora, tz);
        var hoy = DateOnly.FromDateTime(ahoraLocal);
        var ayer = hoy.AddDays(-1);
        var colsQ = (await ColaboradoresVisibles(suc)).Where(c => c.Estatus == EstatusColaborador.Activo);
        var plantilla = await colsQ.CountAsync();
        var ids = colsQ.Select(c => c.Id);
        var entradas = await Db.Registros.Where(r => ids.Contains(r.ColaboradorId) && r.FechaLocal == hoy && r.Tipo == TipoRegistro.Entrada).Select(r => r.Estado).ToListAsync();
        var presentes = entradas.Count;
        var puntuales = entradas.Count(e => e == EstadoRegistro.EnHorario);
        var incHoy = await Db.Incidencias.CountAsync(i => ids.Contains(i.ColaboradorId) && i.Fecha == hoy && (i.Estado == EstadoIncidencia.PorRevisar || i.Estado == EstadoIncidencia.EnRevision));
        var incAyer = await Db.Incidencias.CountAsync(i => ids.Contains(i.ColaboradorId) && i.Fecha == ayer);
        var abiertas = await Db.Incidencias.CountAsync(i => ids.Contains(i.ColaboradorId) && (i.Estado == EstadoIncidencia.PorRevisar || i.Estado == EstadoIncidencia.EnRevision));
        var inv = Yo.Alcance(Db.Invitaciones.Where(i => i.FechaLocal == hoy && i.Estado != EstadoInvitacion.Cancelada), i => i.SucursalId, suc);
        var visitantesHoy = await inv.CountAsync();
        var dentro = await inv.CountAsync(i => i.Estado == EstadoInvitacion.Dentro);
        var pendientesRh = Yo.Tiene(Permisos.VacacionesGestionar) ? await Db.Solicitudes.CountAsync(x => ids.Contains(x.ColaboradorId) && x.Estado == EstadoSolicitud.Pendiente && x.Pasos.Any(p => p.Orden == x.PasoActual && p.TipoAprobador == TipoAprobador.RecursosHumanos)) : 0;
        var (urg, prox) = (await Svc<Parametros>().Entero(Parametros.DocUrgente, 7), 0);
        var docsUrgentes = await Db.Documentos.CountAsync(d => ids.Contains(d.ColaboradorId) && !d.Reemplazado && d.FechaVencimiento != null && d.FechaVencimiento <= hoy.AddDays(urg));
        var marca = await Db.MensajesMarca.Where(m => m.Activo && (m.Ubicacion == "dashboard_subtitulo" || m.Ubicacion == "dashboard_subtitulo_alertas")).ToDictionaryAsync(m => m.Ubicacion, m => m.Texto);
        var hayAlertas = incHoy > 10 || docsUrgentes > 5 || (plantilla > 0 && presentes * 100 / Math.Max(1, plantilla) < 50 && ahoraLocal.Hour >= 10);
        return new
        {
            Fecha = hoy, HoraLocal = ahoraLocal,
            Presentes = new { Valor = presentes, Plantilla = plantilla, Porcentaje = plantilla == 0 ? 0 : (int)Math.Round(presentes * 100.0 / plantilla) },
            Puntualidad = new { Porcentaje = presentes == 0 ? 0 : (int)Math.Round(puntuales * 100.0 / presentes), Puntuales = puntuales, Entradas = presentes },
            Incidencias = new { PorRevisar = incHoy, Ayer = incAyer, Variacion = incHoy - incAyer, Abiertas = abiertas },
            Visitantes = new { Dentro = dentro, RegistradosHoy = visitantesHoy },
            SolicitudesPendientesRh = pendientesRh, DocumentosUrgentes = docsUrgentes,
            Subtitulo = hayAlertas ? marca.GetValueOrDefault("dashboard_subtitulo_alertas") : marca.GetValueOrDefault("dashboard_subtitulo"), HayAlertas = hayAlertas
        };
    }

    /// <summary>Colaboradores presentes por hora (entradas menos salidas acumuladas) de 6:00 a 20:00.</summary>
    [HttpGet("dashboard/asistencia-por-hora"), Permiso(Permisos.DashboardVer, Permisos.EquipoVer)]
    public async Task<object> PorHora([FromQuery] int? sucursalId, [FromQuery] DateOnly? fecha)
    {
        var suc = Yo.Sucursal(sucursalId);
        var tz = suc is int s0 ? await Db.Sucursales.Where(x => x.Id == s0).Select(x => x.ZonaHoraria).FirstOrDefaultAsync() : null;
        var ahoraLocal = Zona.ALocal(Reloj.Ahora, tz);
        var dia = fecha ?? DateOnly.FromDateTime(ahoraLocal);
        var ids = (await ColaboradoresVisibles(suc)).Select(c => c.Id);
        var regs = await Db.Registros.Where(r => ids.Contains(r.ColaboradorId) && r.FechaLocal == dia).Select(r => new { r.Tipo, r.HoraLocal }).ToListAsync();
        var puntos = new List<object>();
        for (var m = 6 * 60; m <= 20 * 60; m += 30)
        {
            var t = new TimeOnly(m / 60, m % 60);
            var futuro = dia == DateOnly.FromDateTime(ahoraLocal) && t > TimeOnly.FromDateTime(ahoraLocal);
            var e = regs.Count(r => r.Tipo == TipoRegistro.Entrada && r.HoraLocal <= t);
            var s = regs.Count(r => r.Tipo == TipoRegistro.Salida && r.HoraLocal <= t);
            puntos.Add(new { Hora = t.ToString("HH:mm"), Presentes = futuro ? (int?)null : e - s });
        }
        return new { Fecha = dia, Puntos = puntos };
    }
}
