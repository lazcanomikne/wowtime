using Microsoft.EntityFrameworkCore;
using WowTime.Api.Data;
using WowTime.Api.Domain;
using WowTime.Api.Infra;

namespace WowTime.Api.Services;

public class DatosSolicitud
{
    public TipoSolicitud Tipo { get; set; } = TipoSolicitud.Vacaciones;
    /// <summary>Colaborador a nombre de quien se solicita. Si se omite, el usuario en sesión.</summary>
    public int? ColaboradorId { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public int? TipoPermisoId { get; set; }
    public int? CoberturaColaboradorId { get; set; }
    public string? CoberturaNota { get; set; }
    public string? Comentario { get; set; }
}

public record Validacion(string Codigo, string Mensaje, string Nivel);
public record FestivoRango(DateOnly Fecha, string Nombre);

public record Simulacion(
    PersonaMini Colaborador, TipoSolicitud Tipo, DateOnly FechaInicio, DateOnly FechaFin, string PeriodoTexto,
    int DiasNaturales, int DiasHabiles, decimal? SaldoDisponible, decimal? SaldoPosterior, bool SaldoSuficiente,
    List<FestivoRango> Festivos, int[] DiasDescanso, List<Validacion> Validaciones, bool Valida,
    string? Regla, List<PasoRuta> Ruta, string RutaTexto);

/// <summary>Ciclo de vida de solicitudes: simulación, envío, aprobación, rechazo y cancelación.</summary>
public class ServicioSolicitudes(AppDb db, MotorAprobaciones motor, ServicioSaldos saldos, IReloj reloj, Notificador notif,
    Bitacora bitacora, CanalEventos eventos, Parametros parametros)
{
    private static readonly string[] Meses = ["", "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    public static string TextoPeriodo(DateOnly a, DateOnly b)
    {
        if (a == b) return $"{a.Day} de {Meses[a.Month]} de {a.Year}";
        if (a.Year == b.Year && a.Month == b.Month) return $"{a.Day}–{b.Day} de {Meses[a.Month]} de {a.Year}";
        if (a.Year == b.Year) return $"{a.Day} de {Meses[a.Month]} – {b.Day} de {Meses[b.Month]} de {a.Year}";
        return $"{a.Day} de {Meses[a.Month]} de {a.Year} – {b.Day} de {Meses[b.Month]} de {b.Year}";
    }

    public async Task<(int[] descanso, List<DiaFestivo> festivos)> Calendario(Colaborador c, DateOnly ini, DateOnly fin)
    {
        var descanso = c.Turno?.DiasDescanso ?? [6, 7];
        var fest = await db.DiasFestivos.Where(f => f.Fecha >= ini && f.Fecha <= fin && (f.SucursalId == null || f.SucursalId == c.SucursalId)).OrderBy(f => f.Fecha).ToListAsync();
        return (descanso, fest);
    }

    public async Task<int> DiasHabiles(Colaborador c, DateOnly ini, DateOnly fin)
    {
        var (descanso, fest) = await Calendario(c, ini, fin);
        return Reglas.DiasHabiles(ini, fin, descanso, fest.Select(f => f.Fecha));
    }

    public async Task<Colaborador> CargarColaborador(int id)
        => await db.Colaboradores.Include(c => c.Turno).Include(c => c.Puesto).Include(c => c.Area).Include(c => c.Sucursal)
            .FirstOrDefaultAsync(c => c.Id == id) ?? throw ApiException.NoEncontrado("El colaborador");

    /// <summary>Traslapes con solicitudes activas, incapacidades y asignaciones temporales.</summary>
    public async Task<List<string>> Traslapes(int colaboradorId, DateOnly ini, DateOnly fin, int? excluirSolicitud = null)
    {
        var res = new List<string>();
        var sols = await db.Solicitudes.Where(s => s.ColaboradorId == colaboradorId && s.Id != excluirSolicitud
                && (s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.Aprobada)
                && s.Tipo != TipoSolicitud.IncidenciaAsistencia && s.FechaInicio <= fin && s.FechaFin >= ini)
            .Select(s => new { s.Folio, s.Tipo, s.Estado, s.FechaInicio, s.FechaFin }).ToListAsync();
        res.AddRange(sols.Select(s => $"Se traslapa con la solicitud {s.Folio} de {Mapas.NombreTipoSolicitud(s.Tipo).ToLower()} ({(s.Estado == EstadoSolicitud.Aprobada ? "aprobada" : "pendiente")}, {TextoPeriodo(s.FechaInicio, s.FechaFin)})."));
        var inc = await db.Incapacidades.Where(i => i.ColaboradorId == colaboradorId && !i.Cancelada && i.FechaInicio <= fin && i.FechaFin >= ini).ToListAsync();
        res.AddRange(inc.Select(i => $"Se traslapa con una incapacidad ({TextoPeriodo(i.FechaInicio, i.FechaFin)})."));
        var asig = await db.Asignaciones.Where(a => a.ColaboradorId == colaboradorId && !a.Cancelada && a.FechaInicio <= fin && a.FechaFin >= ini).ToListAsync();
        res.AddRange(asig.Select(a => $"Se traslapa con la asignación temporal \"{Mapas.NombreAsignacion(a.Tipo)}\" ({TextoPeriodo(a.FechaInicio, a.FechaFin)})."));
        return res;
    }

    public async Task<Simulacion> Simular(DatosSolicitud d, int colaboradorId, bool capturaRh)
    {
        var c = await CargarColaborador(colaboradorId);
        var hoy = Zona.Hoy(reloj, c.Sucursal?.ZonaHoraria);
        var val = new List<Validacion>();
        void Error(string cod, string msg) => val.Add(new(cod, msg, "error"));

        if (c.Estatus != EstatusColaborador.Activo) Error("colaborador_inactivo", "El colaborador no está activo.");
        if (d.FechaFin < d.FechaInicio) Error("fechas", "La fecha de término no puede ser anterior a la fecha de inicio.");
        if (d.FechaInicio < hoy && d.Tipo != TipoSolicitud.IncidenciaAsistencia) Error("fechas_pasadas", "Las fechas deben ser a partir de hoy.");
        if (d.FechaFin.DayNumber - d.FechaInicio.DayNumber > 120) Error("rango", "El periodo no puede ser mayor a 120 días.");

        var fin = d.FechaFin < d.FechaInicio ? d.FechaInicio : d.FechaFin;
        var (descanso, festivos) = await Calendario(c, d.FechaInicio, fin);
        var habiles = Reglas.DiasHabiles(d.FechaInicio, fin, descanso, festivos.Select(f => f.Fecha));
        if (habiles <= 0 && d.FechaFin >= d.FechaInicio) Error("sin_dias_habiles", "El periodo seleccionado no tiene días hábiles.");

        decimal? disponible = null, posterior = null;
        var suficiente = true;
        if (d.Tipo == TipoSolicitud.Vacaciones)
        {
            var r = await saldos.Resumen(c.Id, hoy);
            disponible = r.Disponibles;
            posterior = r.Disponibles - habiles;
            suficiente = posterior >= 0;
            if (!suficiente) Error("saldo_insuficiente", $"No tienes saldo suficiente: pides {habiles} días hábiles y tienes {r.Disponibles:0.#} disponibles.");
            var pol = await Politica(c);
            if (pol is not null && pol.AnticipacionMinimaDias > 0 && d.FechaInicio < hoy.AddDays(pol.AnticipacionMinimaDias) && !capturaRh)
                Error("anticipacion", $"Las vacaciones se solicitan con al menos {pol.AnticipacionMinimaDias} días de anticipación.");
        }
        if (d.Tipo == TipoSolicitud.Permiso)
        {
            if (d.TipoPermisoId is null) Error("tipo_permiso", "Selecciona el tipo de permiso.");
            else
            {
                var tp = await db.TiposPermiso.FindAsync(d.TipoPermisoId);
                if (tp is null || !tp.Activo) Error("tipo_permiso", "El tipo de permiso no existe o está inactivo.");
                else if (tp.MaximoDias is int max && habiles > max) Error("permiso_maximo", $"El permiso \"{tp.Nombre}\" admite como máximo {max} días hábiles.");
            }
        }
        if (d.Tipo != TipoSolicitud.IncidenciaAsistencia)
            foreach (var t in await Traslapes(c.Id, d.FechaInicio, fin)) Error("traslape", t);

        if (d.CoberturaColaboradorId is int cob)
        {
            var cc = await db.Colaboradores.FindAsync(cob);
            if (cc is null || cc.Estatus != EstatusColaborador.Activo) Error("cobertura", "La persona de cobertura no existe o no está activa.");
            else if (cc.Id == c.Id) Error("cobertura", "No puedes cubrirte a ti mismo.");
            else if (await db.Solicitudes.AnyAsync(s => s.ColaboradorId == cob && s.Estado != EstadoSolicitud.Rechazada && s.Estado != EstadoSolicitud.Cancelada
                         && s.Tipo != TipoSolicitud.IncidenciaAsistencia && s.FechaInicio <= fin && s.FechaFin >= d.FechaInicio))
                val.Add(new("cobertura_ausente", $"{cc.NombreCompleto} también estará ausente en esas fechas.", "aviso"));
        }

        var ruta = await motor.Resolver(c, d.Tipo, hoy);
        if (ruta.Error is not null) Error(ruta.CodigoError!, ruta.Error);

        if (val.All(v => v.Nivel != "error") && d.Tipo == TipoSolicitud.Vacaciones)
            val.Insert(0, new("saldo_ok", "Tienes saldo suficiente", "ok"));

        var rutaTexto = string.Join(" → ", ruta.Pasos.Select(p => p.TipoAprobador == TipoAprobador.RecursosHumanos ? "RH" : p.Etiqueta));
        return new(Mapas.Persona(c), d.Tipo, d.FechaInicio, fin, TextoPeriodo(d.FechaInicio, fin), fin.DayNumber - d.FechaInicio.DayNumber + 1,
            habiles, disponible, posterior, suficiente, festivos.Select(f => new FestivoRango(f.Fecha, f.Nombre)).ToList(), descanso,
            val, val.All(v => v.Nivel != "error"), ruta.Regla?.Nombre, ruta.Pasos, rutaTexto);
    }

    public async Task<PoliticaVacaciones?> Politica(Colaborador c)
        => c.PoliticaVacacionesId is int p
            ? await db.Politicas.Include(x => x.Tabla).FirstOrDefaultAsync(x => x.Id == p)
            : await db.Politicas.Include(x => x.Tabla).FirstOrDefaultAsync(x => x.PorDefecto);

    public async Task<Solicitud> Crear(DatosSolicitud d, int colaboradorId, int usuarioId, bool capturaRh, int? incidenciaId = null)
    {
        var sim = await Simular(d, colaboradorId, capturaRh);
        if (!sim.Valida)
        {
            var errores = sim.Validaciones.Where(v => v.Nivel == "error").ToList();
            if (errores.Any(e => e.Codigo == "sin_supervisor"))
            {
                var c0 = await db.Colaboradores.FindAsync(colaboradorId);
                await notif.EnviarARol(Roles.RecursosHumanos, c0!.SucursalId, "sin_supervisor", "Colaborador sin supervisor",
                    $"{c0.Nombres} {c0.Apellidos} intentó enviar una solicitud y no tiene supervisor asignado.", "/configuracion/organizacion?sinSupervisor=1",
                    $"sinsup:{c0.Id}:{Zona.Hoy(reloj, null)}");
                await db.SaveChangesAsync();
            }
            throw ApiException.Validacion(errores.First().Mensaje, errores);
        }

        var c = await CargarColaborador(colaboradorId);
        var hoy = Zona.Hoy(reloj, c.Sucursal?.ZonaHoraria);
        var ruta = await motor.Resolver(c, d.Tipo, hoy);
        var s = new Solicitud
        {
            Folio = $"TMP-{Guid.NewGuid():N}"[..20], Tipo = d.Tipo, ColaboradorId = c.Id, SucursalId = c.SucursalId, TipoPermisoId = d.Tipo == TipoSolicitud.Permiso ? d.TipoPermisoId : null,
            FechaInicio = d.FechaInicio, FechaFin = sim.FechaFin, DiasHabiles = sim.DiasHabiles,
            CoberturaColaboradorId = d.CoberturaColaboradorId, CoberturaNota = d.CoberturaNota?.Trim(), Comentario = d.Comentario?.Trim(),
            ReglaAprobacionId = ruta.Regla!.Id, ReglaNombre = ruta.Regla.Nombre, CapturadaPorUsuarioId = usuarioId, PasoActual = 1, IncidenciaId = incidenciaId,
            Pasos = ruta.Pasos.Select(p => new PasoAprobacion
            {
                Orden = p.Orden, TipoAprobador = p.TipoAprobador, TitularColaboradorId = p.Titular?.Id,
                Estado = p.Orden == ruta.Pasos.Min(x => x.Orden) ? EstadoPaso.Pendiente : EstadoPaso.EnEspera
            }).ToList()
        };
        s.PasoActual = s.Pasos.Min(p => p.Orden);
        db.Solicitudes.Add(s);
        await db.SaveChangesAsync();
        s.Folio = (d.Tipo switch { TipoSolicitud.Vacaciones => "VAC-", TipoSolicitud.Permiso => "PER-", _ => "INC-" }) + s.Id.ToString("D5");
        if (d.Tipo == TipoSolicitud.Vacaciones) await saldos.Comprometer(s, hoy, usuarioId);
        bitacora.Registrar(c.Id, "solicitud", $"Solicitud de {Mapas.NombreTipoSolicitud(d.Tipo).ToLower()} enviada", TextoPeriodo(s.FechaInicio, s.FechaFin), "Solicitud", s.Id);
        await AvisarPaso(s, hoy);
        await db.SaveChangesAsync();
        eventos.PublicarSucursal(s.SucursalId, "solicitud", new { s.Id, s.Folio });
        return s;
    }

    private async Task AvisarPaso(Solicitud s, DateOnly hoy)
    {
        var c = await db.Colaboradores.FindAsync(s.ColaboradorId);
        foreach (var uid in await motor.UsuariosANotificar(s, hoy))
            await notif.Enviar(uid, "solicitud_por_aprobar", $"Solicitud por aprobar: {c!.Nombres} {c.Apellidos}",
                $"{Mapas.NombreTipoSolicitud(s.Tipo)} · {TextoPeriodo(s.FechaInicio, s.FechaFin)}", $"/aprobaciones?solicitud={s.Id}", $"paso:{s.Id}:{s.PasoActual}:{uid}");
    }

    public async Task<Solicitud> Cargar(int id) =>
        await db.Solicitudes.Include(s => s.Pasos).FirstOrDefaultAsync(s => s.Id == id) ?? throw ApiException.NoEncontrado("La solicitud");

    public async Task<Solicitud> Aprobar(int id, Decisor u, string? comentario)
    {
        var s = await Cargar(id);
        var hoy = Zona.Hoy(reloj, null);
        var ev = await motor.PuedeDecidir(s, u, hoy);
        if (!ev.Puede) throw ApiException.Prohibido(ev.Motivo ?? "No puedes aprobar esta solicitud.");

        // Revalida traslapes al momento de aprobar
        if (s.Tipo != TipoSolicitud.IncidenciaAsistencia)
        {
            var tr = await Traslapes(s.ColaboradorId, s.FechaInicio, s.FechaFin, s.Id);
            if (tr.Count > 0) throw ApiException.Validacion(tr[0]);
        }
        var paso = s.Pasos.First(p => p.Orden == s.PasoActual);
        paso.Estado = EstadoPaso.Aprobado;
        paso.DecididoPorUsuarioId = u.UsuarioId;
        paso.DecididoPorNombre = u.Nombre;
        paso.EnNombreDeColaboradorId = ev.EnNombreDe;
        paso.EnNombreDeNombre = ev.EnNombreDeNombre;
        paso.Fecha = reloj.Ahora;
        paso.Motivo = comentario?.Trim();
        paso.NotaBitacora = ev.Nota;

        var siguiente = s.Pasos.Where(p => p.Orden > paso.Orden).OrderBy(p => p.Orden).FirstOrDefault();
        var detalle = ev.EnNombreDe is null ? $"Aprobó {u.Nombre}" : $"Aprobó {u.Nombre} en nombre de {ev.EnNombreDeNombre}";
        if (siguiente is not null)
        {
            siguiente.Estado = EstadoPaso.Pendiente;
            s.PasoActual = siguiente.Orden;
            bitacora.Registrar(s.ColaboradorId, "solicitud", $"Paso {paso.Orden} aprobado ({Mapas.NombreAprobador(paso.TipoAprobador)})", detalle, "Solicitud", s.Id);
            await AvisarPaso(s, hoy);
        }
        else
        {
            s.Estado = EstadoSolicitud.Aprobada;
            s.ResueltaEn = reloj.Ahora;
            if (s.Tipo == TipoSolicitud.Vacaciones) await saldos.Consumir(s, u.UsuarioId);
            if (s.IncidenciaId is int inc)
            {
                var i = await db.Incidencias.FindAsync(inc);
                if (i is not null) { i.Estado = EstadoIncidencia.Justificada; i.ResueltaEn = reloj.Ahora; i.ResueltaPorUsuarioId = u.UsuarioId; }
            }
            bitacora.Registrar(s.ColaboradorId, "solicitud", $"Solicitud de {Mapas.NombreTipoSolicitud(s.Tipo).ToLower()} aprobada", TextoPeriodo(s.FechaInicio, s.FechaFin), "Solicitud", s.Id);
            await notif.EnviarAColaborador(s.ColaboradorId, "solicitud_aprobada", $"Tu solicitud {s.Folio} fue aprobada",
                $"{Mapas.NombreTipoSolicitud(s.Tipo)} · {TextoPeriodo(s.FechaInicio, s.FechaFin)}", $"/mis-solicitudes/{s.Id}", $"aprobada:{s.Id}");
        }
        if (ev.Nota is not null) bitacora.Registrar(s.ColaboradorId, "aprobacion", "Nota de separación de funciones", ev.Nota, "Solicitud", s.Id);
        await db.SaveChangesAsync();
        eventos.PublicarSucursal(s.SucursalId, "solicitud", new { s.Id, s.Folio, s.Estado });
        return s;
    }

    public async Task<Solicitud> Rechazar(int id, Decisor u, string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw ApiException.Validacion("El motivo del rechazo es obligatorio.");
        var s = await Cargar(id);
        var hoy = Zona.Hoy(reloj, null);
        var ev = await motor.PuedeDecidir(s, u, hoy);
        if (!ev.Puede) throw ApiException.Prohibido(ev.Motivo ?? "No puedes rechazar esta solicitud.");
        var paso = s.Pasos.First(p => p.Orden == s.PasoActual);
        paso.Estado = EstadoPaso.Rechazado;
        paso.DecididoPorUsuarioId = u.UsuarioId;
        paso.DecididoPorNombre = u.Nombre;
        paso.EnNombreDeColaboradorId = ev.EnNombreDe;
        paso.EnNombreDeNombre = ev.EnNombreDeNombre;
        paso.Fecha = reloj.Ahora;
        paso.Motivo = motivo.Trim();
        paso.NotaBitacora = ev.Nota;
        foreach (var p in s.Pasos.Where(p => p.Orden > paso.Orden)) p.Estado = EstadoPaso.Omitido;
        s.Estado = EstadoSolicitud.Rechazada;
        s.MotivoRechazo = motivo.Trim();
        s.ResueltaEn = reloj.Ahora;
        if (s.Tipo == TipoSolicitud.Vacaciones) await saldos.Liberar(s, u.UsuarioId);
        if (s.IncidenciaId is int inc)
        {
            var i = await db.Incidencias.FindAsync(inc);
            if (i is not null) { i.Estado = EstadoIncidencia.Confirmada; i.ResueltaEn = reloj.Ahora; i.ResueltaPorUsuarioId = u.UsuarioId; i.ComentarioResolucion = motivo; }
        }
        bitacora.Registrar(s.ColaboradorId, "solicitud", $"Solicitud de {Mapas.NombreTipoSolicitud(s.Tipo).ToLower()} rechazada", $"{TextoPeriodo(s.FechaInicio, s.FechaFin)} · Motivo: {motivo}", "Solicitud", s.Id);
        await notif.EnviarAColaborador(s.ColaboradorId, "solicitud_rechazada", $"Tu solicitud {s.Folio} fue rechazada", $"Motivo: {motivo}", $"/mis-solicitudes/{s.Id}", $"rechazada:{s.Id}");
        await db.SaveChangesAsync();
        eventos.PublicarSucursal(s.SucursalId, "solicitud", new { s.Id, s.Folio, s.Estado });
        return s;
    }

    public async Task<Solicitud> Cancelar(int id, int usuarioId, int? colaboradorId, bool esRh, string? motivo)
    {
        var s = await Cargar(id);
        if (s.ColaboradorId != colaboradorId && !esRh) throw ApiException.Prohibido("Solo quien solicita o Recursos Humanos pueden cancelar la solicitud.");
        if (s.Estado != EstadoSolicitud.Pendiente) throw ApiException.Conflicto("Solo se pueden cancelar solicitudes pendientes.");
        s.Estado = EstadoSolicitud.Cancelada;
        s.ResueltaEn = reloj.Ahora;
        s.MotivoRechazo = motivo;
        foreach (var p in s.Pasos.Where(p => p.Estado is EstadoPaso.Pendiente or EstadoPaso.EnEspera)) p.Estado = EstadoPaso.Omitido;
        if (s.Tipo == TipoSolicitud.Vacaciones) await saldos.Liberar(s, usuarioId);
        if (s.IncidenciaId is int inc)
        {
            var i = await db.Incidencias.FindAsync(inc);
            if (i is not null && i.Estado == EstadoIncidencia.EnRevision) i.Estado = EstadoIncidencia.PorRevisar;
        }
        bitacora.Registrar(s.ColaboradorId, "solicitud", $"Solicitud de {Mapas.NombreTipoSolicitud(s.Tipo).ToLower()} cancelada", TextoPeriodo(s.FechaInicio, s.FechaFin), "Solicitud", s.Id);
        await db.SaveChangesAsync();
        eventos.PublicarSucursal(s.SucursalId, "solicitud", new { s.Id, s.Folio, s.Estado });
        return s;
    }
}
