using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;

namespace WowTime.Api.Data;

public class AppDb(DbContextOptions<AppDb> options, IUsuarioActualAccessor? actual = null) : DbContext(options)
{
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<PuestoDocumento> PuestoDocumentos => Set<PuestoDocumento>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<DiaFestivo> DiasFestivos => Set<DiaFestivo>();
    public DbSet<Colaborador> Colaboradores => Set<Colaborador>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<TokenRenovacion> TokensRenovacion => Set<TokenRenovacion>();
    public DbSet<TokenRecuperacion> TokensRecuperacion => Set<TokenRecuperacion>();
    public DbSet<LlaveApi> LlavesApi => Set<LlaveApi>();
    public DbSet<EsquemaTrabajo> Esquemas => Set<EsquemaTrabajo>();
    public DbSet<UbicacionAutorizada> UbicacionesAutorizadas => Set<UbicacionAutorizada>();
    public DbSet<AsignacionTemporal> Asignaciones => Set<AsignacionTemporal>();
    public DbSet<Nota> Notas => Set<Nota>();
    public DbSet<EventoBitacora> Bitacora => Set<EventoBitacora>();
    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();
    public DbSet<RegistroAsistencia> Registros => Set<RegistroAsistencia>();
    public DbSet<IncidenciaAsistencia> Incidencias => Set<IncidenciaAsistencia>();
    public DbSet<PoliticaVacaciones> Politicas => Set<PoliticaVacaciones>();
    public DbSet<PoliticaAntiguedad> PoliticaAntiguedad => Set<PoliticaAntiguedad>();
    public DbSet<SaldoVacaciones> Saldos => Set<SaldoVacaciones>();
    public DbSet<MovimientoSaldo> MovimientosSaldo => Set<MovimientoSaldo>();
    public DbSet<TipoPermiso> TiposPermiso => Set<TipoPermiso>();
    public DbSet<ReglaAprobacion> Reglas => Set<ReglaAprobacion>();
    public DbSet<PasoRegla> PasosRegla => Set<PasoRegla>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<PasoAprobacion> PasosAprobacion => Set<PasoAprobacion>();
    public DbSet<Delegacion> Delegaciones => Set<Delegacion>();
    public DbSet<Incapacidad> Incapacidades => Set<Incapacidad>();
    public DbSet<InvitacionVisitante> Invitaciones => Set<InvitacionVisitante>();
    public DbSet<AccesoVisitante> AccesosVisitante => Set<AccesoVisitante>();
    public DbSet<TipoDocumento> TiposDocumento => Set<TipoDocumento>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<Archivo> Archivos => Set<Archivo>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<MensajeMarca> MensajesMarca => Set<MensajeMarca>();
    public DbSet<Parametro> Parametros => Set<Parametro>();
    public DbSet<CorreoSaliente> Correos => Set<CorreoSaliente>();
    public DbSet<EjecucionTarea> EjecucionesTarea => Set<EjecucionTarea>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        b.Properties<decimal>().HavePrecision(8, 2);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.HasPostgresExtension("unaccent");
        m.Entity<Colaborador>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasIndex(x => x.SucursalId);
            e.HasIndex(x => x.SupervisorId);
            e.Ignore(x => x.NombreCompleto);
            e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Esquema).WithOne().HasForeignKey<EsquemaTrabajo>(x => x.ColaboradorId);
        });
        m.Entity<EsquemaTrabajo>().HasKey(x => x.ColaboradorId);
        m.Entity<PuestoDocumento>().HasKey(x => new { x.PuestoId, x.TipoDocumentoId });
        m.Entity<Puesto>().HasMany(x => x.DocumentosRequeridos).WithOne().HasForeignKey(x => x.PuestoId);
        m.Entity<RolPermiso>().HasKey(x => new { x.Rol, x.Permiso });
        m.Entity<Usuario>().HasIndex(x => x.Email).IsUnique();
        m.Entity<Usuario>().HasIndex(x => x.ColaboradorId);
        m.Entity<TokenRenovacion>().HasIndex(x => x.TokenHash).IsUnique();
        m.Entity<TokenRecuperacion>().HasIndex(x => x.TokenHash).IsUnique();
        m.Entity<LlaveApi>().HasIndex(x => x.Prefijo).IsUnique();
        m.Entity<Dispositivo>().HasIndex(x => x.LlavePrefijo).IsUnique();
        m.Entity<DiaFestivo>().HasIndex(x => x.Fecha);
        m.Entity<EventoBitacora>().HasIndex(x => new { x.ColaboradorId, x.Fecha });
        m.Entity<RegistroAsistencia>().HasIndex(x => new { x.SucursalId, x.FechaLocal });
        m.Entity<RegistroAsistencia>().HasIndex(x => new { x.ColaboradorId, x.FechaLocal });
        m.Entity<IncidenciaAsistencia>().HasIndex(x => new { x.ColaboradorId, x.Fecha, x.Tipo });
        m.Entity<IncidenciaAsistencia>().HasOne(x => x.Colaborador).WithMany().HasForeignKey(x => x.ColaboradorId);
        m.Entity<PoliticaVacaciones>().HasMany(x => x.Tabla).WithOne().HasForeignKey(x => x.PoliticaVacacionesId);
        m.Entity<SaldoVacaciones>(e => { e.Ignore(x => x.Disponibles); e.HasIndex(x => new { x.ColaboradorId, x.Periodo }).IsUnique(); });
        m.Entity<ReglaAprobacion>().HasMany(x => x.Pasos).WithOne().HasForeignKey(x => x.ReglaAprobacionId);
        m.Entity<ReglaAprobacion>().HasIndex(x => x.Clave).IsUnique();
        m.Entity<Solicitud>(e =>
        {
            e.HasIndex(x => x.Folio).IsUnique();
            e.HasIndex(x => new { x.ColaboradorId, x.Estado });
            e.HasOne(x => x.Colaborador).WithMany().HasForeignKey(x => x.ColaboradorId);
            e.HasOne(x => x.CoberturaColaborador).WithMany().HasForeignKey(x => x.CoberturaColaboradorId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Pasos).WithOne(x => x.Solicitud).HasForeignKey(x => x.SolicitudId);
        });
        m.Entity<PasoAprobacion>().HasOne(x => x.TitularColaborador).WithMany().HasForeignKey(x => x.TitularColaboradorId).OnDelete(DeleteBehavior.SetNull);
        m.Entity<Delegacion>(e =>
        {
            e.HasOne(x => x.De).WithMany().HasForeignKey(x => x.DeColaboradorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Hacia).WithMany().HasForeignKey(x => x.HaciaColaboradorId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<InvitacionVisitante>(e =>
        {
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasIndex(x => new { x.SucursalId, x.FechaLocal });
            e.HasOne(x => x.Anfitrion).WithMany().HasForeignKey(x => x.AnfitrionColaboradorId);
            e.HasOne(x => x.Acceso).WithOne().HasForeignKey<AccesoVisitante>(x => x.InvitacionId);
        });
        m.Entity<Documento>().HasIndex(x => new { x.ColaboradorId, x.TipoDocumentoId });
        m.Entity<Notificacion>().HasIndex(x => new { x.UsuarioId, x.Leida });
        m.Entity<Notificacion>().HasIndex(x => x.ClaveUnica).IsUnique();
        m.Entity<MensajeMarca>().HasIndex(x => x.Ubicacion);
        m.Entity<Parametro>().HasKey(x => x.Clave);
        m.Entity<EjecucionTarea>().HasIndex(x => new { x.Tarea, x.Fecha }).IsUnique();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var uid = actual?.UsuarioId;
        foreach (var e in ChangeTracker.Entries<Auditable>())
        {
            if (e.State == EntityState.Added)
            {
                if (e.Entity.CreadoEn == default) e.Entity.CreadoEn = ahora;
                e.Entity.CreadoPor ??= uid;
                e.Entity.ModificadoEn = ahora;
                e.Entity.ModificadoPor = uid;
            }
            else if (e.State == EntityState.Modified)
            {
                e.Entity.ModificadoEn = ahora;
                e.Entity.ModificadoPor = uid;
            }
        }
        return base.SaveChangesAsync(ct);
    }
}

public interface IUsuarioActualAccessor { int? UsuarioId { get; } }
