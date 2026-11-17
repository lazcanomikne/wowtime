using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Colaboradores: listado, ficha, alta, edición, baja, esquema de trabajo, asignaciones, notas, bitácora y organigrama.</summary>
[Tags("Colaboradores")]
public class ColaboradoresController : ApiBase
{
    public class FiltroColaboradores : ConsultaPaginada
    {
        public string? Q { get; set; }
        public int? SucursalId { get; set; }
        public int? AreaId { get; set; }
        public int? EquipoId { get; set; }
        public int? PuestoId { get; set; }
        public int? TurnoId { get; set; }
        public int? SupervisorId { get; set; }
        public EstatusColaborador? Estatus { get; set; }
        public Modalidad? Modalidad { get; set; }
        public bool? SinSupervisor { get; set; }
    }

    private static readonly Dictionary<string, string> AliasOrden = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nombre"] = "Nombres", ["puesto"] = "Puesto.Nombre", ["area"] = "Area.Nombre", ["ingreso"] = "FechaIngreso"
    };

    private IQueryable<Colaborador> ConIncludes(IQueryable<Colaborador> q) => q.Include(c => c.Puesto).Include(c => c.Area).Include(c => c.Equipo)
        .Include(c => c.Turno).Include(c => c.Sucursal).Include(c => c.Supervisor).Include(c => c.Esquema);

    /// <summary>Listado con filtros, paginación y orden. Supervisores ven a su equipo; colaboradores, solo a sí mismos.</summary>
    [HttpGet("colaboradores")]
    public async Task<object> Lista([FromQuery] FiltroColaboradores f)
    {
        var q = ConIncludes(await ColaboradoresVisibles(f.SucursalId));
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var p = $"%{f.Q.Trim()}%";
            q = q.Where(c => EF.Functions.ILike(EF.Functions.Unaccent(c.Nombres + " " + c.Apellidos), EF.Functions.Unaccent(p)) || EF.Functions.ILike(c.Numero, p) || EF.Functions.ILike(c.Email ?? "", p));
        }
        if (f.AreaId is int a) q = q.Where(c => c.AreaId == a);
        if (f.EquipoId is int e) q = q.Where(c => c.EquipoId == e);
        if (f.PuestoId is int pu) q = q.Where(c => c.PuestoId == pu);
        if (f.TurnoId is int t) q = q.Where(c => c.TurnoId == t);
        if (f.SupervisorId is int s) q = q.Where(c => c.SupervisorId == s);
        if (f.Estatus is EstatusColaborador es) q = q.Where(c => c.Estatus == es);
        else q = q.Where(c => c.Estatus != EstatusColaborador.Baja);
        if (f.Modalidad is Modalidad m) q = q.Where(c => c.Esquema != null && c.Esquema.Modalidad == m);
        if (f.SinSupervisor == true)
        {
            var raices = Db.Puestos.Where(p => p.Nivel == 1).Select(p => p.Id);
            q = q.Where(c => c.SupervisorId == null && (c.PuestoId == null || !raices.Contains(c.PuestoId.Value)));
        }
        f.Orden ??= "Nombres";
        return await q.Pag(f, Fila, "Nombres", AliasOrden);
    }

    private static object Fila(Colaborador c) => new
    {
        c.Id, c.Numero, Nombre = c.NombreCompleto, c.Nombres, c.Apellidos, c.Email, c.Telefono, c.Estatus, c.SucursalId, Sucursal = c.Sucursal?.Nombre,
        c.AreaId, Area = c.Area?.Nombre, c.EquipoId, Equipo = c.Equipo?.Nombre, c.PuestoId, Puesto = c.Puesto?.Nombre, c.TurnoId, Turno = c.Turno?.Nombre,
        c.SupervisorId, Supervisor = c.Supervisor?.NombreCompleto, c.FechaIngreso, c.FechaNacimiento,
        Modalidad = c.Esquema is null ? null : Mapas.NombreModalidad(c.Esquema.Modalidad),
        Iniciales = Mapas.Iniciales(c.Nombres, c.Apellidos), FotoUrl = Enlaces.Archivo(c.FotoArchivoId)
    };

    /// <summary>Ficha completa: encabezado, datos personales y laborales, esquema y tarjetas de la pestaña Resumen.</summary>
    [HttpGet("colaboradores/{id:int}")]
    public async Task<object> Ficha(int id)
    {
        await ColaboradorVisible(id);
        var c = await ConIncludes(Db.Colaboradores).Include(x => x.Dispositivo).FirstAsync(x => x.Id == id);
        var hoy = Zona.Hoy(Reloj, c.Sucursal?.ZonaHoraria);
        var esGestion = Yo.Tiene(Permisos.ColaboradoresVer);
        var esq = c.Esquema ?? new EsquemaTrabajo();
        var centro = esq.CentroBaseSucursalId is int cb ? await Db.Sucursales.Where(s => s.Id == cb).Select(s => s.Nombre).FirstOrDefaultAsync() : c.Sucursal?.Nombre;

        var asignacion = await Db.Asignaciones.Where(a => a.ColaboradorId == id && !a.Cancelada && a.FechaFin >= hoy).OrderBy(a => a.FechaInicio).FirstOrDefaultAsync();
        var regsHoy = await Db.Registros.Include(r => r.Dispositivo).Where(r => r.ColaboradorId == id && r.FechaLocal == hoy).OrderBy(r => r.FechaHora).ToListAsync();
        var entrada = regsHoy.FirstOrDefault(r => r.Tipo == TipoRegistro.Entrada);
        var salida = regsHoy.FirstOrDefault(r => r.Tipo == TipoRegistro.Salida);
        var saldo = await Svc<ServicioSaldos>().Resumen(id, hoy);
        var proxima = await Db.Solicitudes.Where(s => s.ColaboradorId == id && s.Tipo == TipoSolicitud.Vacaciones && s.FechaFin >= hoy && (s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.Aprobada))
            .OrderBy(s => s.FechaInicio).Select(s => new { s.Id, s.Folio, s.FechaInicio, s.FechaFin, s.Estado, s.DiasHabiles }).FirstOrDefaultAsync();
        var exp = await Expediente(c, hoy);
        var usuario = await Db.Usuarios.Where(u => u.ColaboradorId == id).Select(u => new { u.Id, u.Email, u.Roles, u.Activo, u.UltimoAcceso }).FirstOrDefaultAsync();
        var reportes = await Db.Colaboradores.CountAsync(x => x.SupervisorId == id && x.Estatus != EstatusColaborador.Baja);
        var ventana = await Svc<Parametros>().Entero(Parametros.VentanaCumple, 7);
        DateOnly? cumple = null;
        if (c.FechaNacimiento is DateOnly fn)
        {
            var proximo = new DateOnly(hoy.Year, fn.Month, Math.Min(fn.Day, DateTime.DaysInMonth(hoy.Year, fn.Month)));
            if (proximo < hoy) proximo = proximo.AddYears(1);
            cumple = proximo;
        }
        return new
        {
            c.Id, c.Numero, Nombre = c.NombreCompleto, c.Nombres, c.Apellidos, c.Estatus, c.FechaBaja, c.MotivoBaja,
            Iniciales = Mapas.Iniciales(c.Nombres, c.Apellidos), FotoUrl = Enlaces.Archivo(c.FotoArchivoId), Puesto = c.Puesto?.Nombre,
            Personales = new
            {
                c.Email, c.Telefono, c.FechaNacimiento, c.Genero, Curp = esGestion || Yo.ColaboradorId == id ? c.Curp : null, Rfc = esGestion || Yo.ColaboradorId == id ? c.Rfc : null,
                Nss = esGestion || Yo.ColaboradorId == id ? c.Nss : null, Direccion = esGestion || Yo.ColaboradorId == id ? c.Direccion : null, c.ContactoEmergenciaNombre, c.ContactoEmergenciaTelefono
            },
            Laborales = new
            {
                c.SucursalId, Sucursal = c.Sucursal?.Nombre, c.AreaId, Area = c.Area?.Nombre, c.EquipoId, Equipo = c.Equipo?.Nombre, c.PuestoId, c.TurnoId,
                Turno = c.Turno is null ? null : new { c.Turno.Nombre, c.Turno.HoraEntrada, c.Turno.HoraSalida, c.Turno.ToleranciaMinutos, c.Turno.DiasDescanso },
                c.FechaIngreso, Supervisor = Per(c.Supervisor), c.SupervisorId, c.DispositivoId, Dispositivo = c.Dispositivo?.Nombre, c.PoliticaVacacionesId, ReportesDirectos = reportes,
                TienePin = c.PinHash != null
            },
            Esquema = new
            {
                esq.Modalidad, ModalidadTexto = Mapas.NombreModalidad(esq.Modalidad), esq.CentroBaseSucursalId, CentroBase = centro,
                esq.DiasOficina, esq.DiasRemotos, DiasOficinaTexto = Mapas.ListaDias(esq.DiasOficina), DiasRemotosTexto = Mapas.ListaDias(esq.DiasRemotos),
                esq.RegistroPermitido, RegistroTexto = Mapas.NombreMetodo(esq.RegistroPermitido), esq.RequiereFoto
            },
            Asignacion = asignacion is null ? null : new { asignacion.Id, asignacion.Tipo, TipoTexto = Mapas.NombreAsignacion(asignacion.Tipo), asignacion.FechaInicio, asignacion.FechaFin, asignacion.Motivo, Estado = Reglas.EstadoAsignacion(asignacion.FechaInicio, asignacion.FechaFin, hoy, asignacion.Cancelada) },
            AsistenciaHoy = new
            {
                Entrada = entrada?.HoraLocal, Salida = salida?.HoraLocal,
                Estado = salida is not null ? "Salida registrada" : entrada is not null ? Mapas.NombreEstadoRegistro(entrada.Estado) : "Sin registro",
                EstadoClave = salida is not null ? "SalidaRegistrada" : entrada?.Estado.ToString() ?? "SinRegistro",
                Dispositivo = entrada?.Dispositivo?.Nombre ?? c.Dispositivo?.Nombre, Origen = entrada?.Origen
            },
            Vacaciones = new { saldo.Disponibles, saldo.PorVencer, saldo.FechaLimiteProxima, Proxima = proxima },
            Expediente = exp,
            Cumpleanos = cumple is DateOnly cu ? new { Fecha = cu, MostrarBanner = cu.DayNumber - hoy.DayNumber <= ventana } : null,
            Usuario = usuario,
            PuedeEditar = Yo.Tiene(Permisos.ColaboradoresEditar) && Yo.PuedeVerSucursal(c.SucursalId),
            PuedeVerNotas = Yo.Tiene(Permisos.NotasGestionar)
        };
    }

    private async Task<object> Expediente(Colaborador c, DateOnly hoy)
    {
        var requeridos = await Db.PuestoDocumentos.Where(p => p.PuestoId == c.PuestoId).Select(p => p.TipoDocumentoId).ToListAsync();
        var docs = await Db.Documentos.Include(d => d.TipoDocumento).Where(d => d.ColaboradorId == c.Id && !d.Reemplazado).ToListAsync();
        var entregados = docs.Select(d => d.TipoDocumentoId).Distinct().Count(requeridos.Contains);
        var urg = await Svc<Parametros>().Entero(Parametros.DocUrgente, 7);
        var prox = await Svc<Parametros>().Entero(Parametros.DocProximo, 30);
        var alertas = docs.Where(d => d.FechaVencimiento != null).Select(d => new { d.Id, Tipo = d.TipoDocumento!.Nombre, d.FechaVencimiento, Dias = d.FechaVencimiento!.Value.DayNumber - hoy.DayNumber, Estado = Reglas.EstadoDocumento(d.FechaVencimiento, hoy, urg, prox) })
            .Where(a => a.Estado != EstadoDocumento.Vigente).OrderBy(a => a.Dias).ToList();
        return new { Entregados = entregados, Requeridos = requeridos.Count, Porcentaje = requeridos.Count == 0 ? 100 : (int)Math.Round(entregados * 100.0 / requeridos.Count), Alertas = alertas };
    }

    public class ColaboradorDto
    {
        public string Nombres { get; set; } = "";
        public string Apellidos { get; set; } = "";
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public DateOnly? FechaNacimiento { get; set; }
        public DateOnly FechaIngreso { get; set; }
        public string? Genero { get; set; }
        public string? Curp { get; set; }
        public string? Rfc { get; set; }
        public string? Nss { get; set; }
        public string? Direccion { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
        public int SucursalId { get; set; }
        public int? AreaId { get; set; }
        public int? EquipoId { get; set; }
        public int? PuestoId { get; set; }
        public int? TurnoId { get; set; }
        public int? SupervisorId { get; set; }
        public int? DispositivoId { get; set; }
        public int? PoliticaVacacionesId { get; set; }
        public string? Pin { get; set; }
        /// <summary>Solo en alta: crea usuario de acceso con rol Colaborador.</summary>
        public bool CrearUsuario { get; set; }
        public EsquemaDto? Esquema { get; set; }
    }

    public record EsquemaDto(Modalidad Modalidad, int? CentroBaseSucursalId, int[] DiasOficina, int[] DiasRemotos, MetodoRegistro RegistroPermitido, bool RequiereFoto);

    private async Task Validar(ColaboradorDto d, int? id)
    {
        var err = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(d.Nombres)) err["nombres"] = "El nombre es obligatorio.";
        if (string.IsNullOrWhiteSpace(d.Apellidos)) err["apellidos"] = "Los apellidos son obligatorios.";
        if (!string.IsNullOrWhiteSpace(d.Email) && !d.Email.Contains('@')) err["email"] = "El correo no es válido.";
        if (d.FechaIngreso == default) err["fechaIngreso"] = "La fecha de ingreso es obligatoria.";
        if (d.Pin is { Length: > 0 } pin && (pin.Length is < 4 or > 8 || !pin.All(char.IsDigit))) err["pin"] = "El PIN debe tener de 4 a 8 dígitos.";
        if (err.Count > 0) throw ApiException.Validacion(err.First().Value, err);
        Yo.ExigirSucursal(d.SucursalId);
        var esRaiz = d.PuestoId is int p && await Db.Puestos.AnyAsync(x => x.Id == p && x.Nivel == 1);
        if (d.SupervisorId is null && !esRaiz && id is null)
            throw ApiException.Validacion("El supervisor es obligatorio, salvo para la raíz del organigrama.", new { supervisorId = "Selecciona un supervisor." });
        if (d.SupervisorId is int sup && id is int yo)
        {
            if (sup == yo) throw ApiException.Validacion("Un colaborador no puede ser su propio supervisor.");
            if ((await Subordinados(yo)).Contains(sup)) throw ApiException.Validacion("No se puede asignar como supervisor a un subordinado: se formaría un ciclo en el organigrama.");
        }
        if (d.Esquema is { } e)
        {
            if (e.DiasOficina.Intersect(e.DiasRemotos).Any()) throw ApiException.Validacion("Un día no puede ser de oficina y remoto a la vez.");
            if (e.Modalidad == Modalidad.Hibrido && e.DiasRemotos.Length == 0) throw ApiException.Validacion("El esquema híbrido requiere al menos un día remoto.");
        }
    }

    private static void Aplicar(Colaborador c, ColaboradorDto d)
    {
        c.Nombres = d.Nombres.Trim(); c.Apellidos = d.Apellidos.Trim(); c.Email = d.Email?.Trim().ToLowerInvariant(); c.Telefono = d.Telefono?.Trim();
        c.FechaNacimiento = d.FechaNacimiento; c.FechaIngreso = d.FechaIngreso; c.Genero = d.Genero; c.Curp = d.Curp?.Trim().ToUpperInvariant(); c.Rfc = d.Rfc?.Trim().ToUpperInvariant();
        c.Nss = d.Nss?.Trim(); c.Direccion = d.Direccion; c.ContactoEmergenciaNombre = d.ContactoEmergenciaNombre; c.ContactoEmergenciaTelefono = d.ContactoEmergenciaTelefono;
        c.SucursalId = d.SucursalId; c.AreaId = d.AreaId; c.EquipoId = d.EquipoId; c.PuestoId = d.PuestoId; c.TurnoId = d.TurnoId; c.SupervisorId = d.SupervisorId;
        c.DispositivoId = d.DispositivoId; c.PoliticaVacacionesId = d.PoliticaVacacionesId;
        if (!string.IsNullOrWhiteSpace(d.Pin)) c.PinHash = Hash.Password(d.Pin);
    }

    /// <summary>Alta de colaborador con número consecutivo, esquema de trabajo y, opcionalmente, usuario de acceso.</summary>
    [HttpPost("colaboradores"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<object> Crear(ColaboradorDto d)
    {
        await Validar(d, null);
        var prefijo = await Svc<Parametros>().Texto(Parametros.PrefijoNumero) ?? "WC-";
        var numeros = await Db.Colaboradores.Where(c => c.Numero.StartsWith(prefijo)).Select(c => c.Numero).ToListAsync();
        var siguiente = numeros.Select(n => int.TryParse(n[prefijo.Length..], out var x) ? x : 0).DefaultIfEmpty(0).Max() + 1;
        var c = new Colaborador { Numero = $"{prefijo}{siguiente:D4}" };
        Aplicar(c, d);
        var e = d.Esquema ?? new EsquemaDto(Modalidad.Presencial, d.SucursalId, [1, 2, 3, 4, 5], [], MetodoRegistro.TerminalYApp, false);
        c.Esquema = new EsquemaTrabajo { Modalidad = e.Modalidad, CentroBaseSucursalId = e.CentroBaseSucursalId ?? d.SucursalId, DiasOficina = e.DiasOficina, DiasRemotos = e.DiasRemotos, RegistroPermitido = e.RegistroPermitido, RequiereFoto = e.RequiereFoto };
        Db.Colaboradores.Add(c);
        await Db.SaveChangesAsync();
        Bitacora.Registrar(c.Id, "datos", "Alta de colaborador", $"{c.Numero} · ingreso {c.FechaIngreso:dd/MM/yyyy}");
        string? pwd = null;
        if (d.CrearUsuario)
        {
            if (string.IsNullOrWhiteSpace(c.Email)) throw ApiException.Validacion("Para crear el usuario de acceso se necesita el correo.");
            pwd = "Wt" + Hash.Aleatorio(8) + "1";
            Db.Usuarios.Add(new Usuario { ColaboradorId = c.Id, Email = c.Email, Nombre = c.NombreCompleto, PasswordHash = Hash.Password(pwd), Roles = [Roles.Colaborador], Sucursales = [c.SucursalId], DebeCambiarPassword = true });
        }
        var pol = await Svc<ServicioSolicitudes>().Politica(c);
        if (pol is not null) await Svc<ServicioSaldos>().Otorgar(c, pol, Hoy);
        await Db.SaveChangesAsync();
        return new { c.Id, c.Numero, passwordTemporal = pwd };
    }

    /// <summary>Edición de datos personales, laborales y esquema.</summary>
    [HttpPut("colaboradores/{id:int}"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<object> Editar(int id, ColaboradorDto d)
    {
        await ColaboradorVisible(id, true);
        var c = await Db.Colaboradores.Include(x => x.Esquema).Include(x => x.Turno).FirstAsync(x => x.Id == id);
        await Validar(d, id);
        var cambios = new List<string>();
        if (c.TurnoId != d.TurnoId) cambios.Add(await Db.Turnos.Where(t => t.Id == d.TurnoId).Select(t => t.Nombre).FirstOrDefaultAsync() ?? "Turno");
        if (c.PuestoId != d.PuestoId) cambios.Add("Puesto");
        if (c.AreaId != d.AreaId) cambios.Add("Área");
        if (c.SupervisorId != d.SupervisorId) cambios.Add("Supervisor");
        if (c.SucursalId != d.SucursalId) cambios.Add("Sucursal");
        Aplicar(c, d);
        if (d.Esquema is { } e)
        {
            c.Esquema ??= new EsquemaTrabajo { ColaboradorId = id };
            if (c.Esquema.Modalidad != e.Modalidad || c.Esquema.RegistroPermitido != e.RegistroPermitido || c.Esquema.RequiereFoto != e.RequiereFoto) cambios.Add("Esquema de trabajo");
            c.Esquema.Modalidad = e.Modalidad; c.Esquema.CentroBaseSucursalId = e.CentroBaseSucursalId; c.Esquema.DiasOficina = e.DiasOficina; c.Esquema.DiasRemotos = e.DiasRemotos;
            c.Esquema.RegistroPermitido = e.RegistroPermitido; c.Esquema.RequiereFoto = e.RequiereFoto;
        }
        Bitacora.Registrar(id, "datos", cambios.Count > 0 ? "Se modificaron datos laborales" : "Se actualizaron datos del colaborador", cambios.Count > 0 ? string.Join(", ", cambios) : null);
        var u = await Db.Usuarios.FirstOrDefaultAsync(x => x.ColaboradorId == id);
        if (u is not null) { u.Nombre = c.NombreCompleto; if (!u.Sucursales.Contains(c.SucursalId) && u.Sucursales.Length > 0) u.Sucursales = [.. u.Sucursales, c.SucursalId]; }
        await Db.SaveChangesAsync();
        return new { c.Id };
    }

    public record BajaDto(DateOnly Fecha, string Motivo);
    /// <summary>Da de baja al colaborador, desactiva su acceso y cancela sus solicitudes pendientes.</summary>
    [HttpPost("colaboradores/{id:int}/baja"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<IActionResult> Baja(int id, BajaDto d)
    {
        var c = await ColaboradorVisible(id, true);
        if (string.IsNullOrWhiteSpace(d.Motivo)) throw ApiException.Validacion("El motivo de la baja es obligatorio.");
        if (await Db.Colaboradores.AnyAsync(x => x.SupervisorId == id && x.Estatus != EstatusColaborador.Baja))
            throw ApiException.Conflicto("El colaborador tiene reportes directos. Reasígnalos a otro supervisor antes de darlo de baja.");
        c.Estatus = EstatusColaborador.Baja; c.FechaBaja = d.Fecha; c.MotivoBaja = d.Motivo.Trim();
        foreach (var u in Db.Usuarios.Where(u => u.ColaboradorId == id)) u.Activo = false;
        var svc = Svc<ServicioSolicitudes>();
        foreach (var s in await Db.Solicitudes.Where(s => s.ColaboradorId == id && s.Estado == EstadoSolicitud.Pendiente).Select(s => s.Id).ToListAsync())
            await svc.Cancelar(s, Yo.UsuarioIdRequerido, null, true, "Baja del colaborador");
        Bitacora.Registrar(id, "datos", "Baja de colaborador", $"{d.Fecha:dd/MM/yyyy} · {d.Motivo}");
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Reactiva a un colaborador dado de baja o inactivo.</summary>
    [HttpPost("colaboradores/{id:int}/reactivar"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<IActionResult> Reactivar(int id)
    {
        var c = await ColaboradorVisible(id, true);
        c.Estatus = EstatusColaborador.Activo; c.FechaBaja = null; c.MotivoBaja = null;
        foreach (var u in Db.Usuarios.Where(u => u.ColaboradorId == id)) u.Activo = true;
        Bitacora.Registrar(id, "datos", "Reactivación de colaborador", null);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    public record EstatusDto(EstatusColaborador Estatus);
    [HttpPost("colaboradores/{id:int}/estatus"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<IActionResult> Estatus(int id, EstatusDto d)
    {
        var c = await ColaboradorVisible(id, true);
        if (d.Estatus == EstatusColaborador.Baja) throw ApiException.Validacion("Usa la acción Dar de baja.");
        c.Estatus = d.Estatus;
        Bitacora.Registrar(id, "datos", "Cambio de estatus", d.Estatus == EstatusColaborador.Activo ? "Activo" : "Inactivo");
        await Db.SaveChangesAsync();
        return NoContent();
    }

    public record SupervisorDto(int? SupervisorId);
    /// <summary>Cambia la línea de reporte. Impide ciclos.</summary>
    [HttpPut("colaboradores/{id:int}/supervisor"), Permiso(Permisos.OrganizacionGestionar, Permisos.ColaboradoresEditar)]
    public async Task<object> CambiarSupervisor(int id, SupervisorDto d)
    {
        var c = await ColaboradorVisible(id, true);
        if (d.SupervisorId is int s)
        {
            if (s == id) throw ApiException.Validacion("Un colaborador no puede ser su propio supervisor.");
            if ((await Subordinados(id)).Contains(s)) throw ApiException.Validacion("No se puede asignar como supervisor a un subordinado: se formaría un ciclo en el organigrama.");
            var sup = await Db.Colaboradores.FindAsync(s) ?? throw ApiException.NoEncontrado("El supervisor");
            if (sup.Estatus != EstatusColaborador.Activo) throw ApiException.Validacion("El supervisor debe estar activo.");
        }
        else
        {
            var esRaiz = c.PuestoId is int p && await Db.Puestos.AnyAsync(x => x.Id == p && x.Nivel == 1);
            if (!esRaiz) throw ApiException.Validacion("Solo la raíz del organigrama puede quedar sin supervisor.");
        }
        var antes = c.SupervisorId is int a ? await Db.Colaboradores.Where(x => x.Id == a).Select(x => x.Nombres + " " + x.Apellidos).FirstOrDefaultAsync() : "Sin supervisor";
        var despues = d.SupervisorId is int b ? await Db.Colaboradores.Where(x => x.Id == b).Select(x => x.Nombres + " " + x.Apellidos).FirstOrDefaultAsync() : "Sin supervisor";
        c.SupervisorId = d.SupervisorId;
        Bitacora.Registrar(id, "datos", "Cambio de supervisor", $"{antes} → {despues}");
        await Db.SaveChangesAsync();
        return new { c.Id, c.SupervisorId };
    }

    /// <summary>Próximos cumpleaños de la sucursal (ventana configurable).</summary>
    [HttpGet("colaboradores/cumpleanos")]
    public async Task<object> Cumpleanos([FromQuery] int? sucursalId, [FromQuery] int? dias)
    {
        var hoy = await HoySucursal(sucursalId);
        var ventana = dias ?? await Svc<Parametros>().Entero(Parametros.VentanaCumple, 7);
        var cols = await (await ColaboradoresVisibles(sucursalId)).Include(c => c.Puesto).Include(c => c.Area)
            .Where(c => c.Estatus == EstatusColaborador.Activo && c.FechaNacimiento != null).ToListAsync();
        var mensajes = await Db.MensajesMarca.Where(m => m.Ubicacion.StartsWith("cumple_") && m.Activo).OrderBy(m => m.Ubicacion).Select(m => m.Texto).ToListAsync();
        if (mensajes.Count == 0) mensajes = ["¡Feliz cumpleaños!"];
        var lista = cols.Select(c =>
        {
            var fn = c.FechaNacimiento!.Value;
            var p = new DateOnly(hoy.Year, fn.Month, Math.Min(fn.Day, DateTime.DaysInMonth(hoy.Year, fn.Month)));
            if (p < hoy) p = p.AddYears(1);
            return (c, p);
        }).Where(x => x.p.DayNumber - hoy.DayNumber <= ventana).OrderBy(x => x.p).ThenBy(x => x.c.Nombres).ToList();
        return lista.Select((x, i) => new
        {
            Colaborador = Mapas.Persona(x.c), Fecha = x.p, EsHoy = x.p == hoy, Dias = x.p.DayNumber - hoy.DayNumber,
            Mensaje = mensajes[i % mensajes.Count].Replace("{nombre}", x.c.Nombres.Split(' ')[0])
        });
    }

    /// <summary>Bitácora paginada del colaborador (Actividad reciente e Historial).</summary>
    [HttpGet("colaboradores/{id:int}/actividad")]
    public async Task<Pagina<EventoBitacora>> Actividad(int id, [FromQuery] ConsultaPaginada f, [FromQuery] string? tipo)
    {
        await ColaboradorVisible(id);
        var q = Db.Bitacora.Where(b => b.ColaboradorId == id);
        if (!string.IsNullOrWhiteSpace(tipo)) q = q.Where(b => b.Tipo == tipo);
        f.Orden ??= "Fecha"; f.Dir ??= "desc";
        return await q.Paginar(f, b => b);
    }

    // ───── Notas (solo RH) ─────
    public record NotaDto(string Texto);
    [HttpGet("colaboradores/{id:int}/notas"), Permiso(Permisos.NotasGestionar)]
    public async Task<object> Notas(int id, [FromQuery] ConsultaPaginada f)
    {
        await ColaboradorVisible(id, true);
        f.Orden ??= "CreadoEn"; f.Dir ??= "desc";
        return await Db.Notas.Include(n => n.Autor).Where(n => n.ColaboradorId == id).Pag(f, n => new
        {
            n.Id, n.Texto, n.CreadoEn, n.ModificadoEn, Autor = n.Autor?.Nombre, AutorRol = n.Autor is null ? null : (n.Autor.Roles.Contains(Roles.RecursosHumanos) ? "RH" : Roles.Nombre(n.Autor.Roles.FirstOrDefault() ?? "")),
            EsMia = n.AutorUsuarioId == Yo.UsuarioId
        });
    }
    [HttpPost("colaboradores/{id:int}/notas"), Permiso(Permisos.NotasGestionar)]
    public async Task<object> CrearNota(int id, NotaDto d)
    {
        await ColaboradorVisible(id, true);
        if (string.IsNullOrWhiteSpace(d.Texto)) throw ApiException.Validacion("Escribe el texto de la nota.");
        var n = new Nota { ColaboradorId = id, AutorUsuarioId = Yo.UsuarioIdRequerido, Texto = d.Texto.Trim() };
        Db.Notas.Add(n); await Db.SaveChangesAsync();
        return new { n.Id };
    }
    [HttpPut("notas/{id:int}"), Permiso(Permisos.NotasGestionar)]
    public async Task<IActionResult> EditarNota(int id, NotaDto d)
    {
        var n = await Db.Notas.FindAsync(id) ?? throw ApiException.NoEncontrado("La nota");
        await ColaboradorVisible(n.ColaboradorId, true);
        if (string.IsNullOrWhiteSpace(d.Texto)) throw ApiException.Validacion("Escribe el texto de la nota.");
        n.Texto = d.Texto.Trim(); await Db.SaveChangesAsync(); return NoContent();
    }
    [HttpDelete("notas/{id:int}"), Permiso(Permisos.NotasGestionar)]
    public async Task<IActionResult> BorrarNota(int id)
    {
        var n = await Db.Notas.FindAsync(id) ?? throw ApiException.NoEncontrado("La nota");
        await ColaboradorVisible(n.ColaboradorId, true);
        Db.Notas.Remove(n); await Db.SaveChangesAsync(); return NoContent();
    }

    // ───── Asignaciones temporales ─────
    [HttpGet("colaboradores/{id:int}/asignaciones")]
    public async Task<object> Asignaciones(int id)
    {
        await ColaboradorVisible(id);
        var hoy = Hoy;
        var lista = await Db.Asignaciones.Where(a => a.ColaboradorId == id).OrderByDescending(a => a.FechaInicio).ToListAsync();
        var suc = await Db.Sucursales.ToDictionaryAsync(s => s.Id, s => s.Nombre);
        return lista.Select(a => new
        {
            a.Id, a.Tipo, TipoTexto = Mapas.NombreAsignacion(a.Tipo), a.FechaInicio, a.FechaFin, a.Motivo, a.SucursalId, Sucursal = a.SucursalId is int s ? suc.GetValueOrDefault(s) : null,
            a.UbicacionAutorizadaId, a.Cancelada, Estado = Reglas.EstadoAsignacion(a.FechaInicio, a.FechaFin, hoy, a.Cancelada)
        });
    }

    /// <summary>Asignaciones temporales de todos los colaboradores visibles.</summary>
    [HttpGet("asignaciones"), Permiso(Permisos.ColaboradoresVer, Permisos.EquipoVer)]
    public async Task<object> TodasAsignaciones([FromQuery] ConsultaPaginada f, [FromQuery] int? sucursalId, [FromQuery] string? estado)
    {
        var ids = (await ColaboradoresVisibles(sucursalId)).Select(c => c.Id);
        var q = Db.Asignaciones.Include(a => a.Colaborador).ThenInclude(c => c!.Puesto).Where(a => ids.Contains(a.ColaboradorId));
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(a => a.Estado == estado);
        f.Orden ??= "FechaInicio"; f.Dir ??= "desc";
        return await q.Pag(f, a => new { a.Id, Colaborador = Per(a.Colaborador), a.Tipo, TipoTexto = Mapas.NombreAsignacion(a.Tipo), a.FechaInicio, a.FechaFin, a.Motivo, a.Estado, a.Cancelada });
    }

    public record AsignacionDto(TipoAsignacion Tipo, DateOnly FechaInicio, DateOnly FechaFin, string Motivo, int? SucursalId, int? UbicacionAutorizadaId);
    [HttpPost("colaboradores/{id:int}/asignaciones"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<object> CrearAsignacion(int id, AsignacionDto d)
    {
        await ColaboradorVisible(id, true);
        await ValidarAsignacion(id, d, null);
        var a = new AsignacionTemporal { ColaboradorId = id, Tipo = d.Tipo, FechaInicio = d.FechaInicio, FechaFin = d.FechaFin, Motivo = d.Motivo.Trim(), SucursalId = d.SucursalId, UbicacionAutorizadaId = d.UbicacionAutorizadaId, Estado = Reglas.EstadoAsignacion(d.FechaInicio, d.FechaFin, Hoy, false) };
        Db.Asignaciones.Add(a);
        Bitacora.Registrar(id, "asignacion", "Asignación temporal registrada", $"{Mapas.NombreAsignacion(d.Tipo)} · {ServicioSolicitudes.TextoPeriodo(d.FechaInicio, d.FechaFin)} · {d.Motivo}");
        await Db.SaveChangesAsync();
        return new { a.Id };
    }
    [HttpPut("asignaciones/{id:int}"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<object> EditarAsignacion(int id, AsignacionDto d)
    {
        var a = await Db.Asignaciones.FindAsync(id) ?? throw ApiException.NoEncontrado("La asignación");
        await ColaboradorVisible(a.ColaboradorId, true);
        await ValidarAsignacion(a.ColaboradorId, d, id);
        a.Tipo = d.Tipo; a.FechaInicio = d.FechaInicio; a.FechaFin = d.FechaFin; a.Motivo = d.Motivo.Trim(); a.SucursalId = d.SucursalId; a.UbicacionAutorizadaId = d.UbicacionAutorizadaId;
        a.Estado = Reglas.EstadoAsignacion(a.FechaInicio, a.FechaFin, Hoy, a.Cancelada);
        Bitacora.Registrar(a.ColaboradorId, "asignacion", "Asignación temporal modificada", $"{Mapas.NombreAsignacion(d.Tipo)} · {ServicioSolicitudes.TextoPeriodo(d.FechaInicio, d.FechaFin)}");
        await Db.SaveChangesAsync();
        return new { a.Id };
    }
    [HttpPost("asignaciones/{id:int}/cancelar"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<IActionResult> CancelarAsignacion(int id)
    {
        var a = await Db.Asignaciones.FindAsync(id) ?? throw ApiException.NoEncontrado("La asignación");
        await ColaboradorVisible(a.ColaboradorId, true);
        a.Cancelada = true; a.Estado = "Cancelada";
        Bitacora.Registrar(a.ColaboradorId, "asignacion", "Asignación temporal cancelada", Mapas.NombreAsignacion(a.Tipo));
        await Db.SaveChangesAsync();
        return NoContent();
    }
    private async Task ValidarAsignacion(int colaboradorId, AsignacionDto d, int? excluir)
    {
        if (d.FechaFin < d.FechaInicio) throw ApiException.Validacion("La fecha de término no puede ser anterior a la de inicio.");
        if (string.IsNullOrWhiteSpace(d.Motivo)) throw ApiException.Validacion("El motivo es obligatorio.");
        if (d.Tipo == TipoAsignacion.OtraSucursal && d.SucursalId is null) throw ApiException.Validacion("Selecciona la sucursal de la asignación.");
        if (d.Tipo == TipoAsignacion.HomeOffice && !await Db.UbicacionesAutorizadas.AnyAsync(u => u.ColaboradorId == colaboradorId && u.Activa && (d.UbicacionAutorizadaId == null || u.Id == d.UbicacionAutorizadaId)))
            throw ApiException.Validacion("El colaborador no tiene un domicilio autorizado para home office. Regístralo primero en su esquema de trabajo.");
        if (await Db.Asignaciones.AnyAsync(a => a.ColaboradorId == colaboradorId && a.Id != excluir && !a.Cancelada && a.FechaInicio <= d.FechaFin && a.FechaFin >= d.FechaInicio))
            throw ApiException.Validacion("Se traslapa con otra asignación temporal del colaborador.");
        if (await Db.Solicitudes.AnyAsync(s => s.ColaboradorId == colaboradorId && s.Tipo != TipoSolicitud.IncidenciaAsistencia && (s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.Aprobada) && s.FechaInicio <= d.FechaFin && s.FechaFin >= d.FechaInicio))
            throw ApiException.Validacion("Se traslapa con una solicitud de vacaciones o permiso del colaborador.");
    }

    // ───── Ubicaciones autorizadas ─────
    [HttpGet("colaboradores/{id:int}/ubicaciones")]
    public async Task<List<UbicacionAutorizada>> Ubicaciones(int id) { await ColaboradorVisible(id); return await Db.UbicacionesAutorizadas.Where(u => u.ColaboradorId == id).OrderBy(u => u.Id).ToListAsync(); }
    public record UbicacionDto(TipoUbicacion Tipo, string Nombre, double Latitud, double Longitud, int RadioMetros, bool Activa = true);
    [HttpPost("colaboradores/{id:int}/ubicaciones"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<UbicacionAutorizada> CrearUbicacion(int id, UbicacionDto d)
    {
        await ColaboradorVisible(id, true);
        if (d.RadioMetros is < 20 or > 2000) throw ApiException.Validacion("El radio debe estar entre 20 y 2000 metros.");
        var u = new UbicacionAutorizada { ColaboradorId = id, Tipo = d.Tipo, Nombre = d.Nombre.Trim(), Latitud = d.Latitud, Longitud = d.Longitud, RadioMetros = d.RadioMetros, Activa = d.Activa };
        Db.UbicacionesAutorizadas.Add(u);
        Bitacora.Registrar(id, "datos", "Ubicación autorizada registrada", $"{d.Nombre} · radio {d.RadioMetros} m");
        await Db.SaveChangesAsync(); return u;
    }
    [HttpPut("ubicaciones/{id:int}"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<UbicacionAutorizada> EditarUbicacion(int id, UbicacionDto d)
    {
        var u = await Db.UbicacionesAutorizadas.FindAsync(id) ?? throw ApiException.NoEncontrado("La ubicación");
        await ColaboradorVisible(u.ColaboradorId, true);
        if (d.RadioMetros is < 20 or > 2000) throw ApiException.Validacion("El radio debe estar entre 20 y 2000 metros.");
        u.Tipo = d.Tipo; u.Nombre = d.Nombre.Trim(); u.Latitud = d.Latitud; u.Longitud = d.Longitud; u.RadioMetros = d.RadioMetros; u.Activa = d.Activa;
        await Db.SaveChangesAsync(); return u;
    }

    /// <summary>Sube la foto de un colaborador.</summary>
    [HttpPost("colaboradores/{id:int}/foto"), Consumes("multipart/form-data"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<object> Foto(int id, IFormFile archivo)
    {
        var c = await ColaboradorVisible(id, true);
        if (!archivo.ContentType.StartsWith("image/")) throw ApiException.Validacion("La foto debe ser una imagen.");
        var a = await Svc<Almacen>().Guardar(archivo, "fotos", id);
        await Db.SaveChangesAsync();
        c.FotoArchivoId = a.Id;
        Bitacora.Registrar(id, "datos", "Se actualizó la foto", null);
        await Db.SaveChangesAsync();
        return new { fotoUrl = Enlaces.Archivo(a.Id) };
    }

    /// <summary>Crea el usuario de acceso del colaborador o restablece su contraseña.</summary>
    [HttpPost("colaboradores/{id:int}/acceso"), Permiso(Permisos.ColaboradoresEditar)]
    public async Task<object> Acceso(int id)
    {
        var c = await ColaboradorVisible(id, true);
        var u = await Db.Usuarios.FirstOrDefaultAsync(x => x.ColaboradorId == id);
        var pwd = "Wt" + Hash.Aleatorio(8) + "1";
        if (u is null)
        {
            if (string.IsNullOrWhiteSpace(c.Email)) throw ApiException.Validacion("Registra el correo del colaborador antes de crear su acceso.");
            u = new Usuario { ColaboradorId = id, Email = c.Email, Nombre = c.NombreCompleto, Roles = [Roles.Colaborador], Sucursales = [c.SucursalId] };
            Db.Usuarios.Add(u);
        }
        else if (u.Roles.Contains(Roles.Administrador) && !Yo.Tiene(Permisos.UsuariosGestionar)) throw ApiException.Prohibido();
        u.PasswordHash = Hash.Password(pwd); u.DebeCambiarPassword = true; u.Activo = c.Estatus != EstatusColaborador.Baja;
        foreach (var t in Db.TokensRenovacion.Where(x => x.UsuarioId == u.Id && x.RevocadoEn == null)) t.RevocadoEn = Reloj.Ahora;
        Bitacora.Registrar(id, "seguridad", "Se restableció el acceso", $"Usuario {u.Email}");
        await Db.SaveChangesAsync();
        return new { u.Email, passwordTemporal = pwd };
    }

    /// <summary>Exporta la ficha del colaborador a Excel.</summary>
    [HttpGet("colaboradores/{id:int}/exportar")]
    [ProducesResponseType(typeof(FileResult), 200)]
    public async Task<IActionResult> Exportar(int id)
    {
        await ColaboradorVisible(id, true);
        var c = await ConIncludes(Db.Colaboradores).FirstAsync(x => x.Id == id);
        var hoy = Hoy;
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Ficha");
        var filas = new (string, object?)[]
        {
            ("Número", c.Numero), ("Nombre", c.NombreCompleto), ("Estatus", c.Estatus.ToString()), ("Puesto", c.Puesto?.Nombre), ("Área", c.Area?.Nombre), ("Equipo", c.Equipo?.Nombre),
            ("Sucursal", c.Sucursal?.Nombre), ("Turno", c.Turno?.Nombre), ("Supervisor", c.Supervisor?.NombreCompleto), ("Fecha de ingreso", c.FechaIngreso.ToString("dd/MM/yyyy")),
            ("Fecha de nacimiento", c.FechaNacimiento?.ToString("dd/MM/yyyy")), ("Correo", c.Email), ("Teléfono", c.Telefono), ("CURP", c.Curp), ("RFC", c.Rfc), ("NSS", c.Nss),
            ("Dirección", c.Direccion), ("Modalidad", c.Esquema is null ? null : Mapas.NombreModalidad(c.Esquema.Modalidad)),
            ("Registro permitido", c.Esquema is null ? null : Mapas.NombreMetodo(c.Esquema.RegistroPermitido)),
        };
        ws.Cell(1, 1).Value = "Ficha de colaborador"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        for (var i = 0; i < filas.Length; i++) { ws.Cell(i + 3, 1).Value = filas[i].Item1; ws.Cell(i + 3, 1).Style.Font.Bold = true; ws.Cell(i + 3, 2).Value = filas[i].Item2?.ToString() ?? ""; }
        var saldo = await Svc<ServicioSaldos>().Resumen(id, hoy);
        var r = filas.Length + 4;
        ws.Cell(r, 1).Value = "Vacaciones disponibles"; ws.Cell(r, 1).Style.Font.Bold = true; ws.Cell(r, 2).Value = saldo.Disponibles;
        var docs = wb.AddWorksheet("Expediente");
        docs.Cell(1, 1).InsertTable((await Db.Documentos.Include(d => d.TipoDocumento).Where(d => d.ColaboradorId == id && !d.Reemplazado).ToListAsync())
            .Select(d => new { Documento = d.TipoDocumento!.Nombre, Cargado = d.CreadoEn.ToString("dd/MM/yyyy"), Vence = d.FechaVencimiento?.ToString("dd/MM/yyyy") ?? "Sin vigencia" }));
        var bit = wb.AddWorksheet("Historial");
        bit.Cell(1, 1).InsertTable((await Db.Bitacora.Where(b => b.ColaboradorId == id).OrderByDescending(b => b.Fecha).Take(500).ToListAsync())
            .Select(b => new { Fecha = Zona.ALocal(b.Fecha, null).ToString("dd/MM/yyyy HH:mm"), Evento = b.Titulo, b.Detalle, Autor = b.AutorNombre }));
        ws.Columns().AdjustToContents(); docs.Columns().AdjustToContents(); bit.Columns().AdjustToContents();
        var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;
        Bitacora.Registrar(id, "datos", "Se exportó la ficha", null);
        await Db.SaveChangesAsync();
        return File(ms, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Ficha {c.Numero} {c.NombreCompleto}.xlsx");
    }

    // ───── Organigrama ─────
    /// <summary>Árbol de reporte de la sucursal. Los conteos de reportes directos son dentro de la sucursal.</summary>
    [HttpGet("organigrama")]
    public async Task<object> Organigrama([FromQuery] int? sucursalId)
    {
        var suc = Yo.Sucursal(sucursalId) ?? (await Db.Sucursales.OrderBy(s => s.Id).FirstAsync()).Id;
        Yo.ExigirSucursal(suc);
        var cols = await Db.Colaboradores.Include(c => c.Puesto).Include(c => c.Area).Include(c => c.Supervisor)
            .Where(c => c.SucursalId == suc && c.Estatus != EstatusColaborador.Baja).ToListAsync();
        var ids = cols.Select(c => c.Id).ToHashSet();
        var hijos = cols.Where(c => c.SupervisorId is int s && ids.Contains(s)).ToLookup(c => c.SupervisorId!.Value);
        var raicesNivel = await Db.Puestos.Where(p => p.Nivel == 1).Select(p => p.Id).ToListAsync();
        object Nodo(Colaborador c) => new
        {
            c.Id, c.Numero, Nombre = c.NombreCompleto, Puesto = c.Puesto?.Nombre, Area = c.Area?.Nombre, Iniciales = Mapas.Iniciales(c.Nombres, c.Apellidos),
            FotoUrl = Enlaces.Archivo(c.FotoArchivoId), ReportesDirectos = hijos[c.Id].Count(), c.SupervisorId,
            SupervisorExterno = c.SupervisorId is int s && !ids.Contains(s) ? c.Supervisor?.NombreCompleto : null,
            Hijos = hijos[c.Id].OrderBy(h => h.Puesto?.Nivel ?? 9).ThenBy(h => hijos[h.Id].Count()).ThenBy(h => h.Nombres).Select(Nodo).ToList()
        };
        var raices = cols.Where(c => c.SupervisorId is null ? raicesNivel.Contains(c.PuestoId ?? 0) : !ids.Contains(c.SupervisorId.Value)).OrderBy(c => c.Puesto?.Nivel ?? 9).ToList();
        var sinSupervisor = cols.Where(c => c.SupervisorId is null && !raicesNivel.Contains(c.PuestoId ?? 0)).Select(Mapas.Persona).ToList();
        return new { SucursalId = suc, Raices = raices.Select(Nodo), SinSupervisor = sinSupervisor, Total = cols.Count };
    }
}
