using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>
/// Búsquedas de personas para solicitudes y delegaciones. El listado general de colaboradores solo muestra
/// al propio usuario (colaborador) o a su equipo (supervisor), así que no sirve para elegir cobertura ni delegado.
/// </summary>
[Tags("Solicitudes y aprobaciones")]
public class ExtraSolicitudesController : ApiBase
{
    public class FiltroPersonas : ConsultaPaginada
    {
        public string? Q { get; set; }
        public int? SucursalId { get; set; }
    }

    /// <summary>Compañeros activos de la sucursal del usuario para elegir la cobertura de una solicitud. Excluye al usuario en sesión.</summary>
    [HttpGet("solicitudes/companeros"), Permiso(Permisos.SolicitudesCrear, Permisos.VacacionesGestionar)]
    public Task<Pagina<PersonaMini>> Companeros([FromQuery] FiltroPersonas f) => Buscar(f, false);

    /// <summary>Personas activas con usuario de acceso en las que se pueden delegar aprobaciones. Excluye al usuario en sesión.</summary>
    [HttpGet("aprobaciones/delegados"), Permiso(Permisos.DelegacionesPropias, Permisos.AprobacionesConfigurar)]
    public Task<Pagina<PersonaMini>> Delegados([FromQuery] FiltroPersonas f) => Buscar(f, true);

    private async Task<Pagina<PersonaMini>> Buscar(FiltroPersonas f, bool conUsuario)
    {
        var yo = Yo.ColaboradorId ?? -1;
        var q = Db.Colaboradores.Include(c => c.Puesto).Include(c => c.Area)
            .Where(c => c.Estatus == EstatusColaborador.Activo && c.Id != yo);
        q = Yo.Alcance(q, c => c.SucursalId, f.SucursalId);
        if (conUsuario)
        {
            var conAcceso = Db.Usuarios.Where(u => u.Activo && u.ColaboradorId != null).Select(u => u.ColaboradorId!.Value);
            q = q.Where(c => conAcceso.Contains(c.Id));
        }
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var p = $"%{f.Q.Trim()}%";
            q = q.Where(c => EF.Functions.ILike(EF.Functions.Unaccent(c.Nombres + " " + c.Apellidos), EF.Functions.Unaccent(p)) || EF.Functions.ILike(c.Numero, p));
        }
        f.Tamano = Math.Clamp(f.Tamano, 1, 50);
        f.Orden ??= "Nombres";
        return await q.Pag(f, Mapas.Persona, "Nombres");
    }
}
