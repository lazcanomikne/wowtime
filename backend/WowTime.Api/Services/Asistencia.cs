using Microsoft.EntityFrameworkCore;
using WowTime.Api.Data;
using WowTime.Api.Domain;
using WowTime.Api.Infra;

namespace WowTime.Api.Services;

public record AreaAutorizada(string Tipo, string Nombre, double? Latitud, double? Longitud, int? Radio);

public record Jornada(DateOnly Fecha, Modalidad Modalidad, string ModalidadTexto, string Lugar, TimeOnly? HoraEntrada, TimeOnly? HoraSalida,
    bool DiaDescanso, string? Festivo, string? Ausencia, AreaAutorizada? Area, bool PermiteApp, bool PermiteTerminal, bool RequiereFoto,
    string? Asignacion, string? Turno, int Tolerancia);

public class DatosRegistro
{
    public TipoRegistro? Tipo { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public double? Precision { get; set; }
    public string? Justificacion { get; set; }
    public int? FotoArchivoId { get; set; }
}

public class ServicioAsistencia(AppDb db, IReloj reloj, Parametros parametros, Bitacora bitacora, CanalEventos eventos, Notificador notif)
{
    public async Task<Colaborador> Cargar(int id) =>
        await db.Colaboradores.Include(c => c.Turno).Include(c => c.Esquema).Include(c => c.Sucursal).Include(c => c.Dispositivo)
            .FirstOrDefaultAsync(c => c.Id == id) ?? throw ApiException.NoEncontrado("El colaborador");

    public async Task<Jornada> JornadaDe(Colaborador c, DateOnly fecha)
    {
        var esq = c.Esquema ?? new EsquemaTrabajo { ColaboradorId = c.Id, CentroBaseSucursalId = c.SucursalId };
        var turno = c.Turno;
        var dia = Reglas.DiaIso(fecha);
        var descanso = (turno?.DiasDescanso ?? [6, 7]).Contains(dia);
        var festivo = await db.DiasFestivos.Where(f => f.Fecha == fecha && (f.SucursalId == null || f.SucursalId == c.SucursalId)).Select(f => f.Nombre).FirstOrDefaultAsync();
        var tolerancia = turno?.ToleranciaMinutos ?? await parametros.Entero(Parametros.ToleranciaMinutos, 10);

        string? ausencia = null;
        var sol = await db.Solicitudes.Where(s => s.ColaboradorId == c.Id && s.Estado == EstadoSolicitud.Aprobada && s.Tipo != TipoSolicitud.IncidenciaAsistencia
            && s.FechaInicio <= fecha && s.FechaFin >= fecha).Select(s => (TipoSolicitud?)s.Tipo).FirstOrDefaultAsync();
        if (sol is TipoSolicitud ts) ausencia = Mapas.NombreTipoSolicitud(ts);
        if (await db.Incapacidades.AnyAsync(i => i.ColaboradorId == c.Id && !i.Cancelada && i.FechaInicio <= fecha && i.FechaFin >= fecha)) ausencia = "Incapacidad";

        var asig = await db.Asignaciones.Where(a => a.ColaboradorId == c.Id && !a.Cancelada && a.FechaInicio <= fecha && a.FechaFin >= fecha).FirstOrDefaultAsync();
        Modalidad modalidad;
        AreaAutorizada? area;
        var sucBase = esq.CentroBaseSucursalId is int sb && sb != c.SucursalId ? await db.Sucursales.FindAsync(sb) : c.Sucursal;
        AreaAutorizada AreaSucursal(Sucursal? s) => s is null ? new("sucursal", "Sucursal", null, null, null)
            : new("sucursal", s.Nombre, s.Latitud, s.Longitud, s.RadioMetros);
        async Task<AreaAutorizada> Domicilio(int? ubicacionId = null)
        {
            var u = ubicacionId is int uid ? await db.UbicacionesAutorizadas.FindAsync(uid)
                : await db.UbicacionesAutorizadas.Where(x => x.ColaboradorId == c.Id && x.Activa && x.Tipo == TipoUbicacion.Domicilio).FirstOrDefaultAsync();
            return u is null ? new("domicilio", "Home office (sin ubicación autorizada)", null, null, null) : new("domicilio", "Home office autorizado", u.Latitud, u.Longitud, u.RadioMetros);
        }

        string? asignacionTxt = null;
        if (asig is not null)
        {
            asignacionTxt = $"{Mapas.NombreAsignacion(asig.Tipo)}: {asig.Motivo}";
            switch (asig.Tipo)
            {
                case TipoAsignacion.HomeOffice: modalidad = Modalidad.HomeOffice; area = await Domicilio(asig.UbicacionAutorizadaId); break;
                case TipoAsignacion.OtraSucursal: modalidad = Modalidad.Presencial; area = AreaSucursal(asig.SucursalId is int s ? await db.Sucursales.FindAsync(s) : sucBase); break;
                default:
                    modalidad = Modalidad.Presencial;
                    area = asig.UbicacionAutorizadaId is int u ? await Domicilio(u) with { Tipo = "comision", Nombre = "Comisión" } : new("comision", "Comisión (sin geocerca)", null, null, null);
                    break;
            }
        }
        else if (esq.Modalidad == Modalidad.HomeOffice) { modalidad = Modalidad.HomeOffice; area = await Domicilio(); }
        else if (esq.Modalidad == Modalidad.Hibrido && esq.DiasRemotos.Contains(dia)) { modalidad = Modalidad.HomeOffice; area = await Domicilio(); }
        else { modalidad = Modalidad.Presencial; area = AreaSucursal(sucBase); }

        var lugar = modalidad == Modalidad.HomeOffice ? "Home office" : area?.Nombre ?? "Sucursal";
        return new(fecha, modalidad, Mapas.NombreModalidad(modalidad), lugar, turno?.HoraEntrada, turno?.HoraSalida, descanso, festivo, ausencia, area,
            esq.RegistroPermitido is MetodoRegistro.App or MetodoRegistro.TerminalYApp,
            esq.RegistroPermitido is MetodoRegistro.Terminal or MetodoRegistro.TerminalYApp,
            esq.RequiereFoto, asignacionTxt, turno is null ? null : $"{turno.Nombre}", tolerancia);
    }

    public record ResultadoUbicacion(bool Dentro, double? Distancia, AreaAutorizada? Area, string Mensaje, bool SinGeocerca);

    public static ResultadoUbicacion Validar(Jornada j, double lat, double lng)
    {
        if (j.Area?.Latitud is not double cLat || j.Area.Longitud is not double cLng || j.Area.Radio is not int radio)
            return new(true, null, j.Area, j.Area?.Tipo == "comision" ? "Comisión sin geocerca: se registrará tu ubicación." : "No hay un área autorizada configurada; se registrará tu ubicación.", true);
        var dentro = Reglas.DentroGeocerca(lat, lng, cLat, cLng, radio, out var dist);
        return new(dentro, Math.Round(dist), j.Area, dentro ? "Tu ubicación está dentro del área autorizada" : $"Estás a {Math.Round(dist)} m del área autorizada ({radio} m de radio).", false);
    }

    public async Task<RegistroAsistencia> Registrar(Colaborador c, DatosRegistro d, OrigenRegistro origen, int? dispositivoId, int? usuarioId)
    {
        var zona = c.Sucursal?.ZonaHoraria;
        var ahora = reloj.Ahora;
        var local = Zona.ALocal(ahora, zona);
        var fecha = DateOnly.FromDateTime(local);
        var hora = TimeOnly.FromDateTime(local);
        var j = await JornadaDe(c, fecha);
        if (c.Estatus != EstatusColaborador.Activo) throw ApiException.Validacion("El colaborador no está activo.");
        if (origen == OrigenRegistro.App && !j.PermiteApp) throw ApiException.Validacion("Tu esquema de trabajo no permite registrar asistencia desde la app.");
        if (origen is OrigenRegistro.Terminal or OrigenRegistro.Kiosco && !j.PermiteTerminal) throw ApiException.Validacion("El esquema de este colaborador no permite registrar en terminal.");

        var hoyRegs = await db.Registros.Where(r => r.ColaboradorId == c.Id && r.FechaLocal == fecha).OrderBy(r => r.FechaHora).ToListAsync();
        var entrada = hoyRegs.FirstOrDefault(r => r.Tipo == TipoRegistro.Entrada);
        var salida = hoyRegs.FirstOrDefault(r => r.Tipo == TipoRegistro.Salida);
        var tipo = d.Tipo ?? (entrada is null ? TipoRegistro.Entrada : TipoRegistro.Salida);
        if (tipo == TipoRegistro.Entrada && entrada is not null) throw ApiException.Conflicto($"Ya registraste tu entrada hoy a las {entrada.HoraLocal:HH\\:mm}.");
        if (tipo == TipoRegistro.Salida && entrada is null) throw ApiException.Validacion("Primero registra tu entrada.");
        if (tipo == TipoRegistro.Salida && salida is not null) throw ApiException.Conflicto($"Ya registraste tu salida hoy a las {salida.HoraLocal:HH\\:mm}.");

        var reg = new RegistroAsistencia
        {
            ColaboradorId = c.Id, SucursalId = c.SucursalId, Tipo = tipo, FechaHora = ahora, FechaLocal = fecha, HoraLocal = hora, Origen = origen,
            DispositivoId = dispositivoId ?? (origen == OrigenRegistro.App ? null : c.DispositivoId), Modalidad = j.Modalidad, RegistradoPorUsuarioId = usuarioId,
            Justificacion = string.IsNullOrWhiteSpace(d.Justificacion) ? null : d.Justificacion.Trim()
        };

        if (origen == OrigenRegistro.App)
        {
            if (d.Latitud is not double lat || d.Longitud is not double lng)
                throw ApiException.Validacion("No se recibió tu ubicación. Activa el permiso de ubicación e intenta de nuevo.");
            var v = Validar(j, lat, lng);
            reg.Latitud = lat; reg.Longitud = lng; reg.Precision = d.Precision;
            reg.DentroGeocerca = v.SinGeocerca ? null : v.Dentro;
            reg.DistanciaMetros = v.Distancia;
            reg.LugarAutorizado = j.Area?.Nombre;
            if (!v.Dentro && reg.Justificacion is null)
                throw new ApiException(422, "fuera_de_area", "Estás fuera del área autorizada. Escribe una justificación para registrar.", new { v.Distancia, j.Area });
            if (j.RequiereFoto && d.FotoArchivoId is null)
                throw new ApiException(422, "foto_requerida", "Tu política requiere una foto de evidencia para registrar.");
        }
        if (d.FotoArchivoId is int foto)
        {
            var a = await db.Archivos.FindAsync(foto);
            if (a is null || a.ColaboradorId != c.Id) throw ApiException.Validacion("La foto de evidencia no es válida.");
            reg.FotoArchivoId = foto;
        }

        reg.Estado = tipo == TipoRegistro.Entrada
            ? (j.HoraEntrada is TimeOnly he ? Reglas.EstadoEntrada(hora, he, j.Tolerancia) : EstadoRegistro.EnHorario)
            : EstadoRegistro.SalidaRegistrada;
        db.Registros.Add(reg);
        await db.SaveChangesAsync();

        if (tipo == TipoRegistro.Entrada && reg.Estado == EstadoRegistro.Retardo)
            await CrearIncidencia(c, fecha, TipoIncidencia.Retardo, reg.Id, $"Entrada a las {hora:HH\\:mm}; horario {j.HoraEntrada:HH\\:mm} con {j.Tolerancia} min de tolerancia.");
        if (reg.DentroGeocerca == false)
            await CrearIncidencia(c, fecha, TipoIncidencia.FueraDeArea, reg.Id, $"{(tipo == TipoRegistro.Entrada ? "Entrada" : "Salida")} a {reg.DistanciaMetros} m del área autorizada. Justificación: {reg.Justificacion}");

        var disp = reg.DispositivoId is int di ? (await db.Dispositivos.FindAsync(di))?.Nombre : null;
        bitacora.Registrar(c.Id, "asistencia", tipo == TipoRegistro.Entrada ? "Registro de entrada" : "Registro de salida",
            disp ?? (origen == OrigenRegistro.App ? $"App móvil · {j.Lugar}" : origen.ToString()), "RegistroAsistencia", (int)reg.Id);
        await db.SaveChangesAsync();
        eventos.PublicarSucursal(c.SucursalId, "asistencia", new { reg.Id, reg.ColaboradorId, Tipo = reg.Tipo.ToString(), Estado = reg.Estado.ToString() });
        return reg;
    }

    public async Task<IncidenciaAsistencia?> CrearIncidencia(Colaborador c, DateOnly fecha, TipoIncidencia tipo, long? registroId, string detalle)
    {
        if (await db.Incidencias.AnyAsync(i => i.ColaboradorId == c.Id && i.Fecha == fecha && i.Tipo == tipo)) return null;
        var inc = new IncidenciaAsistencia { ColaboradorId = c.Id, SucursalId = c.SucursalId, Fecha = fecha, Tipo = tipo, RegistroId = registroId, Detalle = detalle };
        db.Incidencias.Add(inc);
        await db.SaveChangesAsync();
        if (c.SupervisorId is int sup)
            await notif.EnviarAColaborador(sup, "incidencia", $"Incidencia por revisar: {c.Nombres} {c.Apellidos}",
                $"{Mapas.NombreIncidencia(tipo)} · {fecha:dd/MM/yyyy}", $"/asistencia?tab=incidencias&id={inc.Id}", $"inc:{inc.Id}");
        eventos.PublicarSucursal(c.SucursalId, "incidencia", new { inc.Id });
        return inc;
    }

    /// <summary>Cierre diario: faltas por ausencia de entrada y salidas omitidas. Idempotente.</summary>
    public async Task<(int faltas, int salidas)> CerrarDia(DateOnly fecha)
    {
        int faltas = 0, salidas = 0;
        var colaboradores = await db.Colaboradores.Include(c => c.Turno).Include(c => c.Esquema).Include(c => c.Sucursal)
            .Where(c => c.Estatus == EstatusColaborador.Activo && c.FechaIngreso <= fecha).ToListAsync();
        var regs = await db.Registros.Where(r => r.FechaLocal == fecha).Select(r => new { r.ColaboradorId, r.Tipo, r.Id }).ToListAsync();
        foreach (var c in colaboradores)
        {
            var j = await JornadaDe(c, fecha);
            if (j.DiaDescanso || j.Festivo is not null || j.Ausencia is not null) continue;
            var mios = regs.Where(r => r.ColaboradorId == c.Id).ToList();
            if (!mios.Any(r => r.Tipo == TipoRegistro.Entrada))
            {
                if (await CrearIncidencia(c, fecha, TipoIncidencia.Falta, null, "Sin registro de entrada en la jornada.") is not null) faltas++;
            }
            else if (!mios.Any(r => r.Tipo == TipoRegistro.Salida))
            {
                var e = mios.First(r => r.Tipo == TipoRegistro.Entrada);
                if (await CrearIncidencia(c, fecha, TipoIncidencia.SalidaOmitida, e.Id, "Hay entrada pero no se registró la salida.") is not null) salidas++;
            }
        }
        return (faltas, salidas);
    }
}
