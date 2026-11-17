using Microsoft.EntityFrameworkCore;
using WowTime.Api.Data;
using WowTime.Api.Domain;
using WowTime.Api.Infra;

namespace WowTime.Api.Services;

public record PasoRuta(int Orden, TipoAprobador TipoAprobador, string Etiqueta, PersonaMini? Titular, PersonaMini? DelegadoA, string Descripcion);
public record RutaResuelta(ReglaAprobacion? Regla, List<PasoRuta> Pasos, string? Error, string? CodigoError);

/// <summary>Usuario que intenta decidir un paso.</summary>
public record Decisor(int UsuarioId, int? ColaboradorId, IReadOnlyCollection<string> Roles, int[] Sucursales, bool TodasSucursales, string Nombre);

/// <summary>Motor único de aprobaciones: resuelve rutas con la regla y el organigrama, y decide quién puede aprobar cada paso.</summary>
public class MotorAprobaciones(AppDb db)
{
    public async Task<bool> TieneReportes(int colaboradorId)
        => await db.Colaboradores.AnyAsync(c => c.SupervisorId == colaboradorId && c.Estatus != EstatusColaborador.Baja);

    public async Task<ReglaAprobacion?> ReglaPara(int colaboradorId, TipoSolicitud tipo)
    {
        var reglas = await db.Reglas.Include(r => r.Pasos).ToListAsync();
        if (tipo != TipoSolicitud.IncidenciaAsistencia && await TieneReportes(colaboradorId))
        {
            var sup = reglas.FirstOrDefault(r => r.SoloSolicitantesConReportes && r.Tipos.Contains(tipo));
            if (sup is not null) return sup;
        }
        return reglas.FirstOrDefault(r => !r.SoloSolicitantesConReportes && r.Tipos.Contains(tipo));
    }

    private Task<Colaborador?> Cargar(int? id) => id is int i
        ? db.Colaboradores.Include(c => c.Puesto).Include(c => c.Area).FirstOrDefaultAsync(c => c.Id == i)
        : Task.FromResult<Colaborador?>(null);

    /// <summary>
    /// Supervisor directo: jefe del solicitante.
    /// Gerente: si el solicitante tiene reportes directos (es supervisor), su jefe directo; si no, el jefe de su supervisor.
    /// </summary>
    public async Task<Colaborador?> Titular(Colaborador solicitante, TipoAprobador tipo)
    {
        var jefe = await Cargar(solicitante.SupervisorId);
        if (tipo == TipoAprobador.SupervisorDirecto) return jefe;
        if (tipo == TipoAprobador.Gerente)
        {
            if (await TieneReportes(solicitante.Id)) return jefe;
            return jefe is null ? null : await Cargar(jefe.SupervisorId);
        }
        return null;
    }

    public async Task<Delegacion?> DelegacionActiva(int titularId, TipoSolicitud tipo, DateOnly hoy)
    {
        var ds = await db.Delegaciones.Include(d => d.Hacia).ThenInclude(h => h!.Puesto)
            .Where(d => d.DeColaboradorId == titularId && d.TerminadaEn == null && d.FechaInicio <= hoy && d.FechaFin >= hoy).ToListAsync();
        return ds.FirstOrDefault(d => d.Tipos.Contains(tipo));
    }

    public async Task<RutaResuelta> Resolver(Colaborador solicitante, TipoSolicitud tipo, DateOnly hoy)
    {
        var regla = await ReglaPara(solicitante.Id, tipo);
        if (regla is null)
            return new(null, [], "No hay una regla de aprobación configurada para este tipo de solicitud.", "sin_regla");
        if (!regla.Activa)
            return new(regla, [], $"La regla de aprobación \"{regla.Nombre}\" está inactiva. Pide a Recursos Humanos que la active.", "regla_inactiva");
        var pasos = new List<PasoRuta>();
        foreach (var p in regla.Pasos.OrderBy(p => p.Orden))
        {
            var etiqueta = Mapas.NombreAprobador(p.TipoAprobador);
            if (p.TipoAprobador == TipoAprobador.RecursosHumanos)
            {
                pasos.Add(new(p.Orden, p.TipoAprobador, etiqueta, null, null, "Recursos Humanos"));
                continue;
            }
            var titular = await Titular(solicitante, p.TipoAprobador);
            if (titular is null)
            {
                var msg = p.TipoAprobador == TipoAprobador.SupervisorDirecto
                    ? "No tienes un supervisor asignado, por lo que no puedes enviar solicitudes. Recursos Humanos ya fue notificado."
                    : "No se encontró al gerente en el organigrama para resolver tu ruta de aprobación. Recursos Humanos ya fue notificado.";
                return new(regla, pasos, msg, "sin_supervisor");
            }
            if (titular.Id == solicitante.Id)
                return new(regla, pasos, "La ruta de aprobación te asigna como tu propio aprobador. Revisa el organigrama.", "autoaprobacion");
            var del = await DelegacionActiva(titular.Id, tipo, hoy);
            var persona = Mapas.Persona(titular);
            pasos.Add(new(p.Orden, p.TipoAprobador, etiqueta, persona, Mapas.PersonaN(del?.Hacia),
                del is null ? $"{titular.NombreCompleto}" : $"{titular.NombreCompleto} (delegado a {del.Hacia!.NombreCompleto})"));
        }
        return new(regla, pasos, null, null);
    }

    public async Task<List<Usuario>> UsuariosRh(int sucursalId)
        => (await db.Usuarios.Where(u => u.Activo && u.Roles.Contains(Roles.RecursosHumanos)).ToListAsync())
            .Where(u => u.Sucursales.Length == 0 || u.Sucursales.Contains(sucursalId)).ToList();

    /// <summary>Usuarios que no deben decidir el paso de RH: el solicitante y quienes decidieron pasos previos.</summary>
    public async Task<HashSet<int>> Excluidos(Solicitud s)
    {
        var ex = s.Pasos.Where(p => p.Orden < s.PasoActual && p.DecididoPorUsuarioId != null).Select(p => p.DecididoPorUsuarioId!.Value).ToHashSet();
        var propio = await db.Usuarios.Where(u => u.ColaboradorId == s.ColaboradorId).Select(u => u.Id).ToListAsync();
        foreach (var p in propio) ex.Add(p);
        return ex;
    }

    public record Evaluacion(bool Puede, int? EnNombreDe, string? EnNombreDeNombre, string? Nota, string? Motivo);

    public async Task<Evaluacion> PuedeDecidir(Solicitud s, Decisor u, DateOnly hoy)
    {
        if (s.Estado != EstadoSolicitud.Pendiente) return new(false, null, null, null, "La solicitud ya no está pendiente.");
        var paso = s.Pasos.FirstOrDefault(p => p.Orden == s.PasoActual);
        if (paso is null) return new(false, null, null, null, "La solicitud no tiene un paso pendiente.");
        if (u.ColaboradorId == s.ColaboradorId) return new(false, null, null, null, "Nadie puede aprobar su propia solicitud.");

        if (paso.TipoAprobador == TipoAprobador.RecursosHumanos)
        {
            if (!u.Roles.Contains(Roles.RecursosHumanos)) return new(false, null, null, null, "Este paso corresponde a Recursos Humanos.");
            if (!u.TodasSucursales && u.Sucursales.Length > 0 && !u.Sucursales.Contains(s.SucursalId))
                return new(false, null, null, null, "No tienes acceso a la sucursal de esta solicitud.");
            var excluidos = await Excluidos(s);
            if (!excluidos.Contains(u.UsuarioId)) return new(true, null, null, null, null);
            var propio = await db.Usuarios.AnyAsync(x => x.Id == u.UsuarioId && x.ColaboradorId == s.ColaboradorId);
            if (propio) return new(false, null, null, null, "Nadie puede aprobar su propia solicitud.");
            var otros = (await UsuariosRh(s.SucursalId)).Where(x => !excluidos.Contains(x.Id)).ToList();
            if (otros.Count > 0)
                return new(false, null, null, null, $"Ya decidiste un paso previo de esta solicitud; el paso de Recursos Humanos lo debe tomar otra persona ({string.Join(", ", otros.Select(o => o.Nombre))}).");
            return new(true, null, null, "Separación de funciones: no hay otro usuario de Recursos Humanos disponible; la misma persona decidió dos pasos.", null);
        }

        if (paso.TitularColaboradorId is not int titular) return new(false, null, null, null, "El paso no tiene titular asignado.");
        if (u.ColaboradorId == titular) return new(true, null, null, null, null);
        if (u.ColaboradorId is int cid)
        {
            var del = await DelegacionActiva(titular, s.Tipo, hoy);
            if (del is not null && del.HaciaColaboradorId == cid)
            {
                var nombre = await db.Colaboradores.Where(c => c.Id == titular).Select(c => c.Nombres + " " + c.Apellidos).FirstAsync();
                return new(true, titular, nombre, null, null);
            }
        }
        return new(false, null, null, null, "No eres el aprobador de este paso ni tienes una delegación vigente del titular.");
    }

    /// <summary>Usuarios a notificar cuando un paso queda pendiente.</summary>
    public async Task<List<int>> UsuariosANotificar(Solicitud s, DateOnly hoy)
    {
        var paso = s.Pasos.First(p => p.Orden == s.PasoActual);
        if (paso.TipoAprobador == TipoAprobador.RecursosHumanos)
        {
            var ex = await Excluidos(s);
            var rh = await UsuariosRh(s.SucursalId);
            var elegibles = rh.Where(x => !ex.Contains(x.Id)).Select(x => x.Id).ToList();
            if (elegibles.Count == 0)
            {
                var propios = await db.Usuarios.Where(x => x.ColaboradorId == s.ColaboradorId).Select(x => x.Id).ToListAsync();
                elegibles = rh.Where(x => !propios.Contains(x.Id)).Select(x => x.Id).ToList();
            }
            return elegibles;
        }
        var ids = new List<int>();
        if (paso.TitularColaboradorId is int t)
        {
            ids.AddRange(await db.Usuarios.Where(x => x.ColaboradorId == t && x.Activo).Select(x => x.Id).ToListAsync());
            var del = await DelegacionActiva(t, s.Tipo, hoy);
            if (del is not null) ids.AddRange(await db.Usuarios.Where(x => x.ColaboradorId == del.HaciaColaboradorId && x.Activo).Select(x => x.Id).ToListAsync());
        }
        return ids.Distinct().ToList();
    }

    /// <summary>Solicitudes pendientes cuyo paso actual puede decidir este usuario.</summary>
    public async Task<List<int>> Bandeja(Decisor u, DateOnly hoy, int? sucursalId = null)
    {
        var q = db.Solicitudes.Include(s => s.Pasos).Where(s => s.Estado == EstadoSolicitud.Pendiente);
        if (sucursalId is int suc) q = q.Where(s => s.SucursalId == suc);
        var candidatas = new List<Solicitud>();
        if (u.Roles.Contains(Roles.RecursosHumanos))
            candidatas.AddRange(await q.Where(s => s.Pasos.Any(p => p.Orden == s.PasoActual && p.TipoAprobador == TipoAprobador.RecursosHumanos)).ToListAsync());
        if (u.ColaboradorId is int cid)
        {
            var titulares = new List<int> { cid };
            var dels = await db.Delegaciones.Where(d => d.HaciaColaboradorId == cid && d.TerminadaEn == null && d.FechaInicio <= hoy && d.FechaFin >= hoy).ToListAsync();
            titulares.AddRange(dels.Select(d => d.DeColaboradorId));
            candidatas.AddRange(await q.Where(s => s.Pasos.Any(p => p.Orden == s.PasoActual && p.TitularColaboradorId != null && titulares.Contains(p.TitularColaboradorId.Value))).ToListAsync());
        }
        var res = new List<int>();
        foreach (var s in candidatas.DistinctBy(s => s.Id))
            if ((await PuedeDecidir(s, u, hoy)).Puede) res.Add(s.Id);
        return res;
    }
}
