using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Asistencia: jornada del día, geocerca, registros por app, terminal y quiosco, e incidencias.</summary>
[Tags("Asistencia")]
public class AsistenciaController : ApiBase
{
    private ServicioAsistencia Svc => Svc<ServicioAsistencia>();

    /// <summary>Modalidad, horario, área autorizada y registros del día del usuario en sesión.</summary>
    [HttpGet("asistencia/jornada-hoy"), Permiso(Permisos.AsistenciaRegistrar)]
    public async Task<object> JornadaHoy()
    {
        var c = await Svc.Cargar(Yo.ColaboradorIdRequerido);
        var local = Zona.ALocal(Reloj.Ahora, c.Sucursal?.ZonaHoraria);
        var hoy = DateOnly.FromDateTime(local);
        var j = await Svc.JornadaDe(c, hoy);
        var regs = await Db.Registros.Where(r => r.ColaboradorId == c.Id && r.FechaLocal == hoy).OrderBy(r => r.FechaHora).ToListAsync();
        var e = regs.FirstOrDefault(r => r.Tipo == TipoRegistro.Entrada);
        var s = regs.FirstOrDefault(r => r.Tipo == TipoRegistro.Salida);
        return new
        {
            Jornada = j, HoraServidor = local, Entrada = e?.HoraLocal, EstadoEntrada = e?.Estado, Salida = s?.HoraLocal,
            Siguiente = e is null ? "Entrada" : s is null ? "Salida" : null,
            Estado = s is not null ? "Salida registrada" : e is not null ? Mapas.NombreEstadoRegistro(e.Estado) : j.Ausencia ?? (j.DiaDescanso ? "Día de descanso" : j.Festivo is not null ? "Día festivo" : "Sin registro"),
            Mensaje = s is not null ? "Jornada concluida" : e is not null ? (e.Estado == EstadoRegistro.Retardo ? "Entrada con retardo" : "Todo en orden") : "Registra tu entrada",
            Horas = e is not null ? Math.Round(((s?.FechaHora ?? Reloj.Ahora) - e.FechaHora).TotalHours, 1) : 0
        };
    }

    public record UbicacionDto(double Latitud, double Longitud, double? Precision);

    /// <summary>Compara coordenadas con el área autorizada del día.</summary>
    [HttpPost("asistencia/validar-ubicacion"), Permiso(Permisos.AsistenciaRegistrar)]
    public async Task<ServicioAsistencia.ResultadoUbicacion> Validar(UbicacionDto d)
    {
        var c = await Svc.Cargar(Yo.ColaboradorIdRequerido);
        var j = await Svc.JornadaDe(c, Zona.Hoy(Reloj, c.Sucursal?.ZonaHoraria));
        return ServicioAsistencia.Validar(j, d.Latitud, d.Longitud);
    }

    /// <summary>Registra entrada o salida desde la app con coordenadas, precisión y foto. La hora la pone el servidor.</summary>
    [HttpPost("asistencia/registros"), Permiso(Permisos.AsistenciaRegistrar)]
    public async Task<object> Registrar(DatosRegistro d)
    {
        var c = await Svc.Cargar(Yo.ColaboradorIdRequerido);
        var r = await Svc.Registrar(c, d, OrigenRegistro.App, null, Yo.UsuarioId);
        return Dto(r, c);
    }

    public record ManualDto(int ColaboradorId, TipoRegistro Tipo, string Justificacion);
    /// <summary>RH registra una checada a nombre de un colaborador (queda con origen RH y justificación).</summary>
    [HttpPost("asistencia/registros/manual"), Permiso(Permisos.AsistenciaGestionar)]
    public async Task<object> Manual(ManualDto d)
    {
        await ColaboradorVisible(d.ColaboradorId, true);
        if (string.IsNullOrWhiteSpace(d.Justificacion)) throw ApiException.Validacion("La justificación es obligatoria.");
        var c = await Svc.Cargar(d.ColaboradorId);
        var r = await Svc.Registrar(c, new DatosRegistro { Tipo = d.Tipo, Justificacion = d.Justificacion }, OrigenRegistro.Rh, null, Yo.UsuarioId);
        return Dto(r, c);
    }

    private static object Dto(RegistroAsistencia r, Colaborador c) => new
    {
        r.Id, r.Tipo, r.FechaHora, r.FechaLocal, Hora = r.HoraLocal, r.Estado, EstadoTexto = Mapas.NombreEstadoRegistro(r.Estado), r.Origen, r.DentroGeocerca, r.DistanciaMetros,
        r.LugarAutorizado, Colaborador = c.NombreCompleto
    };

    public class FiltroRegistros : ConsultaPaginada
    {
        public int? SucursalId { get; set; }
        public int? ColaboradorId { get; set; }
        public int? AreaId { get; set; }
        public DateOnly? Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        public DateOnly? Fecha { get; set; }
        public TipoRegistro? Tipo { get; set; }
        public EstadoRegistro? Estado { get; set; }
        public OrigenRegistro? Origen { get; set; }
        public bool? FueraDeArea { get; set; }
        /// <summary>Agrupa entrada y salida por colaborador y día (vista de tabla).</summary>
        public bool Agrupado { get; set; }
    }

    /// <summary>Registros por fecha, sucursal o colaborador. Agrupado=true devuelve una fila por jornada (entrada, salida, estado).</summary>
    [HttpGet("asistencia/registros")]
    public async Task<object> Registros([FromQuery] FiltroRegistros f)
    {
        var visibles = (await ColaboradoresVisibles(f.SucursalId)).Select(c => c.Id);
        var q = Db.Registros.Include(r => r.Colaborador).ThenInclude(c => c!.Puesto).Include(r => r.Colaborador).ThenInclude(c => c!.Area).Include(r => r.Dispositivo)
            .Where(r => visibles.Contains(r.ColaboradorId));
        if (f.SucursalId is int s) q = q.Where(r => r.SucursalId == s);
        if (f.ColaboradorId is int c) q = q.Where(r => r.ColaboradorId == c);
        if (f.AreaId is int a) q = q.Where(r => r.Colaborador!.AreaId == a);
        if (f.Fecha is DateOnly fe) q = q.Where(r => r.FechaLocal == fe);
        if (f.Desde is DateOnly de) q = q.Where(r => r.FechaLocal >= de);
        if (f.Hasta is DateOnly ha) q = q.Where(r => r.FechaLocal <= ha);
        if (f.Tipo is TipoRegistro t) q = q.Where(r => r.Tipo == t);
        if (f.Estado is EstadoRegistro es) q = q.Where(r => r.Estado == es);
        if (f.Origen is OrigenRegistro o) q = q.Where(r => r.Origen == o);
        if (f.FueraDeArea == true) q = q.Where(r => r.DentroGeocerca == false);
        if (!f.Agrupado)
        {
            f.Orden ??= "FechaHora"; f.Dir ??= "desc";
            return await q.Pag(f, r => new
            {
                r.Id, Colaborador = Per(r.Colaborador), r.Tipo, r.FechaHora, r.FechaLocal, Hora = r.HoraLocal, r.Estado, EstadoTexto = Mapas.NombreEstadoRegistro(r.Estado),
                r.Origen, Dispositivo = r.Dispositivo?.Nombre, r.Latitud, r.Longitud, r.Precision, r.DentroGeocerca, r.DistanciaMetros, r.LugarAutorizado, r.Justificacion,
                FotoUrl = Enlaces.Archivo(r.FotoArchivoId), r.Modalidad
            });
        }
        // Agrupado por jornada: entradas como base; orden por la checada más reciente
        var entradas = q.Where(r => r.Tipo == TipoRegistro.Entrada);
        var total = await entradas.CountAsync();
        var tam = Math.Clamp(f.Tamano, 1, 500);
        var pag = Math.Max(1, f.Pagina);
        var lista = await entradas.OrderByDescending(r => Db.Registros.Where(x => x.ColaboradorId == r.ColaboradorId && x.FechaLocal == r.FechaLocal).Max(x => x.FechaHora))
            .Skip((pag - 1) * tam).Take(tam).ToListAsync();
        var claves = lista.Select(r => new { r.ColaboradorId, r.FechaLocal }).ToList();
        var colIds = claves.Select(k => k.ColaboradorId).Distinct().ToList();
        var fechas = claves.Select(k => k.FechaLocal).Distinct().ToList();
        var salidas = await Db.Registros.Where(r => r.Tipo == TipoRegistro.Salida && colIds.Contains(r.ColaboradorId) && fechas.Contains(r.FechaLocal)).ToListAsync();
        var items = lista.Select(e =>
        {
            var sa = salidas.FirstOrDefault(x => x.ColaboradorId == e.ColaboradorId && x.FechaLocal == e.FechaLocal);
            return new
            {
                e.Id, Colaborador = Per(e.Colaborador), Fecha = e.FechaLocal, Entrada = e.HoraLocal, Salida = sa?.HoraLocal,
                Estado = sa is not null ? EstadoRegistro.SalidaRegistrada : e.Estado, EstadoEntrada = e.Estado,
                EstadoTexto = sa is not null ? "Salida registrada" : Mapas.NombreEstadoRegistro(e.Estado),
                e.Origen, Dispositivo = e.Dispositivo?.Nombre, e.DentroGeocerca, e.Modalidad, e.LugarAutorizado,
                Horas = sa is null ? (double?)null : Math.Round((sa.FechaHora - e.FechaHora).TotalHours, 2)
            };
        }).ToList();
        return new Pagina<object>(items, total, pag, tam);
    }

    /// <summary>Resumen de asistencia de un colaborador en un periodo (Mi asistencia, pestaña Asistencia de la ficha).</summary>
    [HttpGet("asistencia/resumen-colaborador")]
    public async Task<object> ResumenColaborador([FromQuery] int? colaboradorId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        var id = colaboradorId ?? Yo.ColaboradorIdRequerido;
        await ColaboradorVisible(id);
        var c = await Svc.Cargar(id);
        var hoy = Zona.Hoy(Reloj, c.Sucursal?.ZonaHoraria);
        var d0 = desde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var d1 = hasta ?? hoy;
        var regs = await Db.Registros.Where(r => r.ColaboradorId == id && r.FechaLocal >= d0 && r.FechaLocal <= d1).ToListAsync();
        var incs = await Db.Incidencias.Where(i => i.ColaboradorId == id && i.Fecha >= d0 && i.Fecha <= d1).ToListAsync();
        var dias = new List<object>();
        for (var d = d1; d >= d0; d = d.AddDays(-1))
        {
            var e = regs.FirstOrDefault(r => r.FechaLocal == d && r.Tipo == TipoRegistro.Entrada);
            var s = regs.FirstOrDefault(r => r.FechaLocal == d && r.Tipo == TipoRegistro.Salida);
            var inc = incs.Where(i => i.Fecha == d).ToList();
            var j = await Svc.JornadaDe(c, d);
            if (e is null && inc.Count == 0 && (j.DiaDescanso || j.Festivo is not null || j.Ausencia is not null || d == hoy)) { if (j.Ausencia is null && j.Festivo is null) continue; }
            dias.Add(new
            {
                Fecha = d, Entrada = e?.HoraLocal, Salida = s?.HoraLocal, Estado = e?.Estado, EstadoTexto = s is not null ? "Salida registrada" : e is not null ? Mapas.NombreEstadoRegistro(e.Estado) : (j.Ausencia ?? j.Festivo ?? (inc.Any(i => i.Tipo == TipoIncidencia.Falta) ? "Falta" : "Sin registro")),
                Modalidad = e is null ? null : Mapas.NombreModalidad(e.Modalidad), Origen = e?.Origen, Horas = e is not null && s is not null ? Math.Round((s.FechaHora - e.FechaHora).TotalHours, 1) : (double?)null,
                Incidencias = inc.Select(i => new { i.Id, i.Tipo, TipoTexto = Mapas.NombreIncidencia(i.Tipo), i.Estado })
            });
        }
        var entradas = regs.Where(r => r.Tipo == TipoRegistro.Entrada).ToList();
        return new
        {
            Desde = d0, Hasta = d1, DiasTrabajados = entradas.Count, EnHorario = entradas.Count(r => r.Estado == EstadoRegistro.EnHorario), Retardos = entradas.Count(r => r.Estado == EstadoRegistro.Retardo),
            Faltas = incs.Count(i => i.Tipo == TipoIncidencia.Falta), IncidenciasAbiertas = incs.Count(i => i.Estado is EstadoIncidencia.PorRevisar or EstadoIncidencia.EnRevision),
            Puntualidad = entradas.Count == 0 ? 100 : (int)Math.Round(entradas.Count(r => r.Estado == EstadoRegistro.EnHorario) * 100.0 / entradas.Count),
            Dias = dias
        };
    }

    public class FiltroIncidencias : ConsultaPaginada
    {
        public int? SucursalId { get; set; }
        public int? ColaboradorId { get; set; }
        public EstadoIncidencia? Estado { get; set; }
        public TipoIncidencia? Tipo { get; set; }
        public DateOnly? Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        public bool? Abiertas { get; set; }
    }

    /// <summary>Incidencias de asistencia (retardos, faltas, salidas omitidas y registros fuera de área).</summary>
    [HttpGet("asistencia/incidencias")]
    public async Task<object> Incidencias([FromQuery] FiltroIncidencias f)
    {
        var visibles = (await ColaboradoresVisibles(f.SucursalId)).Select(c => c.Id);
        var q = Db.Incidencias.Include(i => i.Colaborador).ThenInclude(c => c!.Puesto).Include(i => i.Colaborador).ThenInclude(c => c!.Area).Where(i => visibles.Contains(i.ColaboradorId));
        if (f.SucursalId is int s) q = q.Where(i => i.SucursalId == s);
        if (f.ColaboradorId is int c) q = q.Where(i => i.ColaboradorId == c);
        if (f.Estado is EstadoIncidencia e) q = q.Where(i => i.Estado == e);
        if (f.Abiertas == true) q = q.Where(i => i.Estado == EstadoIncidencia.PorRevisar || i.Estado == EstadoIncidencia.EnRevision);
        if (f.Tipo is TipoIncidencia t) q = q.Where(i => i.Tipo == t);
        if (f.Desde is DateOnly de) q = q.Where(i => i.Fecha >= de);
        if (f.Hasta is DateOnly ha) q = q.Where(i => i.Fecha <= ha);
        f.Orden ??= "Fecha"; f.Dir ??= "desc";
        var yoCol = Yo.ColaboradorId;
        var subs = yoCol is int y ? await Subordinados(y) : [];
        return await q.Pag(f, i => new
        {
            i.Id, Colaborador = Per(i.Colaborador), i.Fecha, i.Tipo, TipoTexto = Mapas.NombreIncidencia(i.Tipo), i.Detalle, i.Justificacion, i.Estado, i.SolicitudId,
            i.ResueltaEn, i.ComentarioResolucion, i.RegistroId,
            PuedeResolver = (Yo.Tiene(Permisos.AsistenciaGestionar) || (Yo.Tiene(Permisos.IncidenciasResolver) && subs.Contains(i.ColaboradorId))) && i.ColaboradorId != yoCol,
            EsMia = i.ColaboradorId == yoCol
        });
    }

    public record ResolverDto(bool Justificada, string? Comentario);
    /// <summary>El supervisor o RH justifica o confirma una incidencia.</summary>
    [HttpPost("asistencia/incidencias/{id:int}/resolver"), Permiso(Permisos.IncidenciasResolver, Permisos.AsistenciaGestionar)]
    public async Task<object> Resolver(int id, ResolverDto d)
    {
        var i = await Db.Incidencias.FindAsync(id) ?? throw ApiException.NoEncontrado("La incidencia");
        if (i.ColaboradorId == Yo.ColaboradorId) throw ApiException.Prohibido("No puedes resolver tus propias incidencias.");
        var esRh = Yo.Tiene(Permisos.AsistenciaGestionar) && Yo.PuedeVerSucursal(i.SucursalId);
        if (!esRh && !(Yo.ColaboradorId is int yo && (await Subordinados(yo)).Contains(i.ColaboradorId))) throw ApiException.Prohibido("Solo el supervisor del colaborador o RH pueden resolver la incidencia.");
        if (i.Estado is EstadoIncidencia.Justificada or EstadoIncidencia.Confirmada) throw ApiException.Conflicto("La incidencia ya fue resuelta.");
        if (!d.Justificada && string.IsNullOrWhiteSpace(d.Comentario)) throw ApiException.Validacion("Escribe un comentario para confirmar la incidencia.");
        i.Estado = d.Justificada ? EstadoIncidencia.Justificada : EstadoIncidencia.Confirmada;
        i.ResueltaEn = Reloj.Ahora; i.ResueltaPorUsuarioId = Yo.UsuarioId; i.ComentarioResolucion = d.Comentario;
        if (i.SolicitudId is int sid)
        {
            var s = await Db.Solicitudes.Include(x => x.Pasos).FirstAsync(x => x.Id == sid);
            if (s.Estado == EstadoSolicitud.Pendiente)
            {
                s.Estado = d.Justificada ? EstadoSolicitud.Aprobada : EstadoSolicitud.Rechazada; s.ResueltaEn = Reloj.Ahora; s.MotivoRechazo = d.Justificada ? null : d.Comentario;
                foreach (var p in s.Pasos.Where(p => p.Estado == EstadoPaso.Pendiente)) { p.Estado = d.Justificada ? EstadoPaso.Aprobado : EstadoPaso.Rechazado; p.DecididoPorUsuarioId = Yo.UsuarioId; p.DecididoPorNombre = Yo.Nombre; p.Fecha = Reloj.Ahora; p.Motivo = d.Comentario; }
            }
        }
        Bitacora.Registrar(i.ColaboradorId, "asistencia", d.Justificada ? "Incidencia justificada" : "Incidencia confirmada", $"{Mapas.NombreIncidencia(i.Tipo)} del {i.Fecha:dd/MM/yyyy}{(d.Comentario is null ? "" : " · " + d.Comentario)}");
        await Svc<Notificador>().EnviarAColaborador(i.ColaboradorId, "incidencia_resuelta", d.Justificada ? "Tu incidencia fue justificada" : "Tu incidencia fue confirmada",
            $"{Mapas.NombreIncidencia(i.Tipo)} del {i.Fecha:dd/MM/yyyy}", "/mi-asistencia", $"incres:{i.Id}");
        await Db.SaveChangesAsync();
        return new { i.Id, i.Estado };
    }

    public record JustificarDto(string Justificacion);
    /// <summary>El colaborador justifica su incidencia: crea una solicitud que sigue la regla "Incidencias de asistencia".</summary>
    [HttpPost("asistencia/incidencias/{id:int}/justificar"), Permiso(Permisos.SolicitudesCrear)]
    public async Task<object> Justificar(int id, JustificarDto d)
    {
        var i = await Db.Incidencias.FindAsync(id) ?? throw ApiException.NoEncontrado("La incidencia");
        if (i.ColaboradorId != Yo.ColaboradorId) throw ApiException.Prohibido("Solo puedes justificar tus propias incidencias.");
        if (i.Estado != EstadoIncidencia.PorRevisar) throw ApiException.Conflicto("La incidencia ya está en revisión o resuelta.");
        if (string.IsNullOrWhiteSpace(d.Justificacion)) throw ApiException.Validacion("Escribe la justificación.");
        i.Justificacion = d.Justificacion.Trim();
        var s = await Svc<ServicioSolicitudes>().Crear(new DatosSolicitud
        {
            Tipo = TipoSolicitud.IncidenciaAsistencia, FechaInicio = i.Fecha, FechaFin = i.Fecha, Comentario = $"{Mapas.NombreIncidencia(i.Tipo)}: {i.Justificacion}"
        }, i.ColaboradorId, Yo.UsuarioIdRequerido, false, i.Id);
        i.Estado = EstadoIncidencia.EnRevision; i.SolicitudId = s.Id;
        await Db.SaveChangesAsync();
        return new { i.Id, i.Estado, SolicitudId = s.Id };
    }

    // ───── Terminal y quiosco ─────
    public record TerminalDto(string Numero, string? Pin, TipoRegistro? Tipo);

    private async Task<Dispositivo> DispositivoPorLlave()
    {
        if (!Request.Headers.TryGetValue("X-Device-Key", out var v)) throw new ApiException(401, "sin_llave", "Falta la llave del dispositivo (encabezado X-Device-Key).");
        var partes = v.ToString().Split('.', 2);
        if (partes.Length != 2) throw new ApiException(401, "llave_invalida", "La llave del dispositivo no es válida.");
        var d = await Db.Dispositivos.FirstOrDefaultAsync(x => x.LlavePrefijo == partes[0]);
        if (d is null || !d.Activo || d.LlaveHash != Hash.Sha256(partes[1])) throw new ApiException(401, "llave_invalida", "La llave del dispositivo no es válida o el dispositivo está inactivo.");
        d.UltimaConexion = Reloj.Ahora;
        return d;
    }

    /// <summary>Identifica el dispositivo por su llave (lo usa el quiosco al configurarse).</summary>
    [AllowAnonymous, HttpGet("terminal/dispositivo")]
    public async Task<object> Dispositivo()
    {
        var d = await DispositivoPorLlave();
        await Db.SaveChangesAsync();
        var s = await Db.Sucursales.FindAsync(d.SucursalId);
        return new { d.Id, d.Nombre, d.Tipo, Sucursal = s?.Nombre, HoraServidor = Zona.ALocal(Reloj.Ahora, s?.ZonaHoraria) };
    }

    /// <summary>Registro por terminal o quiosco: número de colaborador (y PIN en el quiosco). Autenticado con la llave del dispositivo.</summary>
    [AllowAnonymous, HttpPost("terminal/registros")]
    public async Task<object> Terminal(TerminalDto d)
    {
        var disp = await DispositivoPorLlave();
        if (disp.Tipo == TipoDispositivo.LectorQr) throw ApiException.Prohibido("Este dispositivo es un lector de visitantes.");
        var numero = (d.Numero ?? "").Trim().ToUpperInvariant();
        var prefijo = await Svc<Parametros>().Texto(Parametros.PrefijoNumero) ?? "WC-";
        if (numero.All(char.IsDigit) && numero.Length > 0) numero = $"{prefijo}{int.Parse(numero):D4}";
        var col = await Db.Colaboradores.FirstOrDefaultAsync(c => c.Numero == numero && c.SucursalId == disp.SucursalId);
        if (col is null) throw ApiException.Validacion("No se encontró el número de colaborador en esta sucursal.");
        if (disp.Tipo == TipoDispositivo.Kiosco || !string.IsNullOrEmpty(d.Pin))
            if (col.PinHash is null || !Hash.VerificarPassword(col.PinHash, d.Pin ?? "")) throw ApiException.Validacion("El número o el PIN no son correctos.");
        var c = await Svc.Cargar(col.Id);
        var r = await Svc.Registrar(c, new DatosRegistro { Tipo = d.Tipo }, disp.Tipo == TipoDispositivo.Kiosco ? OrigenRegistro.Kiosco : OrigenRegistro.Terminal, disp.Id, null);
        return new
        {
            r.Tipo, Hora = r.HoraLocal, r.Estado, EstadoTexto = Mapas.NombreEstadoRegistro(r.Estado), Colaborador = c.NombreCompleto, Iniciales = Mapas.Iniciales(c.Nombres, c.Apellidos),
            Mensaje = r.Tipo == TipoRegistro.Entrada ? $"¡Buen día, {c.Nombres.Split(' ')[0]}! Entrada registrada a las {r.HoraLocal:HH\\:mm}." : $"¡Hasta pronto, {c.Nombres.Split(' ')[0]}! Salida registrada a las {r.HoraLocal:HH\\:mm}."
        };
    }
}
