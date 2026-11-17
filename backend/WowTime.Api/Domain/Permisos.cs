namespace WowTime.Api.Domain;

/// <summary>Catálogo de permisos. Cada endpoint exige uno o más. Los roles los agrupan.</summary>
public static class Permisos
{
    // Autoservicio
    public const string AsistenciaRegistrar = "asistencia.registrar";
    public const string SolicitudesCrear = "solicitudes.crear";
    public const string VisitantesRegistrar = "visitantes.registrar";
    public const string ExpedientePropio = "expediente.propio";

    // Equipo (supervisor / gerente)
    public const string EquipoVer = "equipo.ver";
    public const string AprobacionesDecidir = "aprobaciones.decidir";
    public const string IncidenciasResolver = "incidencias.resolver";
    public const string DelegacionesPropias = "delegaciones.propias";
    public const string ReportesEquipo = "reportes.equipo";

    // Recursos Humanos
    public const string DashboardVer = "dashboard.ver";
    public const string ColaboradoresVer = "colaboradores.ver";
    public const string ColaboradoresEditar = "colaboradores.editar";
    public const string NotasGestionar = "notas.gestionar";
    public const string AsistenciaGestionar = "asistencia.gestionar";
    public const string VacacionesGestionar = "vacaciones.gestionar";
    public const string VisitantesVer = "visitantes.ver";
    public const string VisitantesAcceso = "visitantes.acceso";
    public const string ExpedientesGestionar = "expedientes.gestionar";
    public const string ReportesSucursal = "reportes.sucursal";
    public const string OrganizacionGestionar = "organizacion.gestionar";
    public const string AprobacionesConfigurar = "aprobaciones.configurar";
    public const string CatalogosGestionar = "catalogos.gestionar";

    // Administración
    public const string UsuariosGestionar = "usuarios.gestionar";
    public const string LlavesApiGestionar = "llavesapi.gestionar";
    public const string SistemaConfigurar = "sistema.configurar";
    public const string TodasSucursales = "sucursales.todas";

    public static readonly (string Clave, string Nombre, string Grupo)[] Catalogo =
    [
        (AsistenciaRegistrar, "Registrar su asistencia", "Autoservicio"),
        (SolicitudesCrear, "Crear solicitudes propias", "Autoservicio"),
        (VisitantesRegistrar, "Registrar invitados", "Autoservicio"),
        (ExpedientePropio, "Consultar y subir su expediente", "Autoservicio"),
        (EquipoVer, "Ver a su equipo", "Equipo"),
        (AprobacionesDecidir, "Aprobar o rechazar solicitudes", "Equipo"),
        (IncidenciasResolver, "Resolver incidencias de su equipo", "Equipo"),
        (DelegacionesPropias, "Delegar sus aprobaciones", "Equipo"),
        (ReportesEquipo, "Reportes de su equipo", "Equipo"),
        (DashboardVer, "Ver el resumen de la sucursal", "Recursos Humanos"),
        (ColaboradoresVer, "Ver colaboradores", "Recursos Humanos"),
        (ColaboradoresEditar, "Alta, edición y baja de colaboradores", "Recursos Humanos"),
        (NotasGestionar, "Notas de colaboradores", "Recursos Humanos"),
        (AsistenciaGestionar, "Gestionar asistencia e incidencias", "Recursos Humanos"),
        (VacacionesGestionar, "Gestionar vacaciones, saldos y políticas", "Recursos Humanos"),
        (VisitantesVer, "Consultar todos los visitantes", "Recursos Humanos"),
        (VisitantesAcceso, "Control de acceso de visitantes", "Recepción"),
        (ExpedientesGestionar, "Gestionar expedientes", "Recursos Humanos"),
        (ReportesSucursal, "Reportes de la sucursal", "Recursos Humanos"),
        (OrganizacionGestionar, "Organigrama y estructura", "Configuración"),
        (AprobacionesConfigurar, "Reglas de aprobación y delegaciones", "Configuración"),
        (CatalogosGestionar, "Catálogos y parámetros", "Configuración"),
        (UsuariosGestionar, "Usuarios y roles", "Administración"),
        (LlavesApiGestionar, "Llaves de API", "Administración"),
        (SistemaConfigurar, "Parámetros del sistema", "Administración"),
        (TodasSucursales, "Acceso a todas las sucursales", "Administración"),
    ];

    public static readonly Dictionary<string, string[]> PorRol = new()
    {
        [Roles.Colaborador] = [AsistenciaRegistrar, SolicitudesCrear, VisitantesRegistrar, ExpedientePropio],
        [Roles.Supervisor] = [AsistenciaRegistrar, SolicitudesCrear, VisitantesRegistrar, ExpedientePropio,
            EquipoVer, AprobacionesDecidir, IncidenciasResolver, DelegacionesPropias, ReportesEquipo],
        [Roles.Gerente] = [AsistenciaRegistrar, SolicitudesCrear, VisitantesRegistrar, ExpedientePropio,
            EquipoVer, AprobacionesDecidir, IncidenciasResolver, DelegacionesPropias, ReportesEquipo],
        [Roles.Recepcion] = [AsistenciaRegistrar, SolicitudesCrear, VisitantesRegistrar, ExpedientePropio, VisitantesVer, VisitantesAcceso],
        [Roles.RecursosHumanos] = [AsistenciaRegistrar, SolicitudesCrear, VisitantesRegistrar, ExpedientePropio,
            EquipoVer, AprobacionesDecidir, IncidenciasResolver, DelegacionesPropias, ReportesEquipo,
            DashboardVer, ColaboradoresVer, ColaboradoresEditar, NotasGestionar, AsistenciaGestionar, VacacionesGestionar,
            VisitantesVer, VisitantesAcceso, ExpedientesGestionar, ReportesSucursal, OrganizacionGestionar, AprobacionesConfigurar, CatalogosGestionar],
        [Roles.Administrador] = Catalogo.Select(c => c.Clave).ToArray(),
    };
}
