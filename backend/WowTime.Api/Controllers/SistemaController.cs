using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Controllers;

/// <summary>Catálogos y parámetros del sistema, usuarios, roles, llaves de API, dispositivos y tareas programadas.</summary>
[Tags("Configuración")]
public class SistemaController : ApiBase
{
    // ───── Tipos de permiso ─────
    [HttpGet("tipos-permiso")]
    public async Task<Pagina<TipoPermiso>> TiposPermiso([FromQuery] FiltroCatalogo f)
    {
        var q = Db.TiposPermiso.AsQueryable();
        if (f.Activos == true) q = q.Where(x => x.Activo);
        f.Orden ??= "Nombre";
        return await q.Paginar(f, x => x);
    }
    public record TipoPermisoDto(string Nombre, bool ConGoce, int? MaximoDias, bool Activo = true);
    [HttpPost("tipos-permiso"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<TipoPermiso> CrearTipoPermiso(TipoPermisoDto d) { var t = new TipoPermiso { Nombre = d.Nombre.Trim(), ConGoce = d.ConGoce, MaximoDias = d.MaximoDias, Activo = d.Activo }; Db.TiposPermiso.Add(t); await Db.SaveChangesAsync(); return t; }
    [HttpPut("tipos-permiso/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<TipoPermiso> EditarTipoPermiso(int id, TipoPermisoDto d)
    {
        var t = await Db.TiposPermiso.FindAsync(id) ?? throw ApiException.NoEncontrado("El tipo de permiso");
        t.Nombre = d.Nombre.Trim(); t.ConGoce = d.ConGoce; t.MaximoDias = d.MaximoDias; t.Activo = d.Activo;
        await Db.SaveChangesAsync(); return t;
    }

    // ───── Tipos de documento ─────
    [HttpGet("tipos-documento")]
    public async Task<object> TiposDocumento([FromQuery] FiltroCatalogo f)
    {
        var q = Db.TiposDocumento.AsQueryable();
        if (f.Activos == true) q = q.Where(x => x.Activo);
        f.Orden ??= "Id";
        return await q.Paginar(f, t => new { t.Id, t.Nombre, t.Descripcion, t.Obligatorio, t.ConVigencia, t.Activo, Puestos = Db.PuestoDocumentos.Where(p => p.TipoDocumentoId == t.Id).Select(p => p.PuestoId).ToArray() });
    }
    public record TipoDocumentoDto(string Nombre, string? Descripcion, bool Obligatorio, bool ConVigencia, bool Activo = true, int[]? Puestos = null);
    [HttpPost("tipos-documento"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<TipoDocumento> CrearTipoDocumento(TipoDocumentoDto d)
    {
        var t = new TipoDocumento { Nombre = d.Nombre.Trim(), Descripcion = d.Descripcion, Obligatorio = d.Obligatorio, ConVigencia = d.ConVigencia, Activo = d.Activo };
        Db.TiposDocumento.Add(t); await Db.SaveChangesAsync();
        foreach (var p in d.Puestos ?? []) Db.PuestoDocumentos.Add(new PuestoDocumento { PuestoId = p, TipoDocumentoId = t.Id });
        await Db.SaveChangesAsync(); return t;
    }
    [HttpPut("tipos-documento/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<TipoDocumento> EditarTipoDocumento(int id, TipoDocumentoDto d)
    {
        var t = await Db.TiposDocumento.FindAsync(id) ?? throw ApiException.NoEncontrado("El tipo de documento");
        t.Nombre = d.Nombre.Trim(); t.Descripcion = d.Descripcion; t.Obligatorio = d.Obligatorio; t.ConVigencia = d.ConVigencia; t.Activo = d.Activo;
        if (d.Puestos is not null)
        {
            Db.PuestoDocumentos.RemoveRange(Db.PuestoDocumentos.Where(p => p.TipoDocumentoId == id));
            foreach (var p in d.Puestos.Distinct()) Db.PuestoDocumentos.Add(new PuestoDocumento { PuestoId = p, TipoDocumentoId = id });
        }
        await Db.SaveChangesAsync(); return t;
    }

    // ───── Dispositivos ─────
    [HttpGet("dispositivos")]
    public async Task<object> Dispositivos([FromQuery] FiltroCatalogo f)
    {
        var q = Yo.Alcance(Db.Dispositivos.AsQueryable(), d => d.SucursalId, f.SucursalId);
        if (f.Activos == true) q = q.Where(x => x.Activo);
        return await q.Paginar(f, d => new { d.Id, d.Nombre, d.Tipo, d.SucursalId, d.Activo, d.LlavePrefijo, TieneLlave = d.LlaveHash != null, d.UltimaConexion });
    }
    public record DispositivoDto(string Nombre, TipoDispositivo Tipo, int SucursalId, bool Activo = true);
    [HttpPost("dispositivos"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<object> CrearDispositivo(DispositivoDto d)
    {
        Yo.ExigirSucursal(d.SucursalId);
        var x = new Dispositivo { Nombre = d.Nombre.Trim(), Tipo = d.Tipo, SucursalId = d.SucursalId, Activo = d.Activo };
        Db.Dispositivos.Add(x); await Db.SaveChangesAsync();
        return new { x.Id };
    }
    [HttpPut("dispositivos/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<object> EditarDispositivo(int id, DispositivoDto d)
    {
        var x = await Db.Dispositivos.FindAsync(id) ?? throw ApiException.NoEncontrado("El dispositivo");
        Yo.ExigirSucursal(x.SucursalId); Yo.ExigirSucursal(d.SucursalId);
        x.Nombre = d.Nombre.Trim(); x.Tipo = d.Tipo; x.SucursalId = d.SucursalId; x.Activo = d.Activo;
        await Db.SaveChangesAsync(); return new { x.Id };
    }
    /// <summary>Genera (o regenera) la llave del dispositivo. La llave completa solo se muestra en esta respuesta.</summary>
    [HttpPost("dispositivos/{id:int}/llave"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<object> LlaveDispositivo(int id)
    {
        var x = await Db.Dispositivos.FindAsync(id) ?? throw ApiException.NoEncontrado("El dispositivo");
        Yo.ExigirSucursal(x.SucursalId);
        var pref = "dv_" + Hash.Aleatorio(6).ToLowerInvariant().Replace("-", "x").Replace("_", "y");
        var secreto = Hash.Aleatorio(24);
        x.LlavePrefijo = pref; x.LlaveHash = Hash.Sha256(secreto);
        await Db.SaveChangesAsync();
        return new { x.Id, llave = $"{pref}.{secreto}" };
    }

    // ───── Mensajes de marca ─────
    [HttpGet("mensajes-marca")]
    public async Task<Pagina<MensajeMarca>> Mensajes([FromQuery] FiltroCatalogo f) { f.Orden ??= "Ubicacion"; return await Db.MensajesMarca.Paginar(f, x => x); }
    public record MensajeDto(string Ubicacion, string Descripcion, string Texto, DateOnly? VigenciaInicio, DateOnly? VigenciaFin, bool Activo = true);
    [HttpPost("mensajes-marca"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<MensajeMarca> CrearMensaje(MensajeDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Texto)) throw ApiException.Validacion("El texto es obligatorio.");
        var m = new MensajeMarca { Ubicacion = d.Ubicacion, Descripcion = d.Descripcion, Texto = d.Texto.Trim(), VigenciaInicio = d.VigenciaInicio, VigenciaFin = d.VigenciaFin, Activo = d.Activo };
        Db.MensajesMarca.Add(m); await Db.SaveChangesAsync(); return m;
    }
    [HttpPut("mensajes-marca/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<MensajeMarca> EditarMensaje(int id, MensajeDto d)
    {
        var m = await Db.MensajesMarca.FindAsync(id) ?? throw ApiException.NoEncontrado("El mensaje");
        if (string.IsNullOrWhiteSpace(d.Texto)) throw ApiException.Validacion("El texto es obligatorio.");
        m.Ubicacion = d.Ubicacion; m.Descripcion = d.Descripcion; m.Texto = d.Texto.Trim(); m.VigenciaInicio = d.VigenciaInicio; m.VigenciaFin = d.VigenciaFin; m.Activo = d.Activo;
        await Db.SaveChangesAsync(); return m;
    }
    [HttpDelete("mensajes-marca/{id:int}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<IActionResult> BorrarMensaje(int id)
    {
        var m = await Db.MensajesMarca.FindAsync(id) ?? throw ApiException.NoEncontrado("El mensaje");
        Db.MensajesMarca.Remove(m); await Db.SaveChangesAsync(); return NoContent();
    }

    // ───── Parámetros ─────
    [HttpGet("parametros")]
    public async Task<List<Parametro>> ParametrosLista() => await Db.Parametros.OrderBy(p => p.Grupo).ThenBy(p => p.Clave).ToListAsync();
    public record ParametroDto(string Valor);
    [HttpPut("parametros/{clave}"), Permiso(Permisos.CatalogosGestionar)]
    public async Task<Parametro> EditarParametro(string clave, ParametroDto d)
    {
        var p = await Db.Parametros.FindAsync(clave) ?? throw ApiException.NoEncontrado("El parámetro");
        if (p.Tipo == "entero" && (!int.TryParse(d.Valor, out var v) || v < 0)) throw ApiException.Validacion("El valor debe ser un número entero positivo.");
        if (p.Grupo == "Sistema" && !Yo.Tiene(Permisos.SistemaConfigurar)) throw ApiException.Prohibido();
        p.Valor = d.Valor.Trim();
        await Db.SaveChangesAsync(); return p;
    }

    // ───── Políticas de vacaciones ─────
    [HttpGet("politicas-vacaciones")]
    public async Task<List<PoliticaVacaciones>> Politicas() => await Db.Politicas.Include(p => p.Tabla.OrderBy(t => t.AniosDesde)).OrderBy(p => p.Id).ToListAsync();
    public record FilaAntiguedad(int AniosDesde, int AniosHasta, int Dias);
    public record PoliticaDto(string Nombre, int PlazoVencimientoMeses, int AnticipacionMinimaDias, int DiasAlertaVencimiento, bool PorDefecto, List<FilaAntiguedad> Tabla);
    [HttpPost("politicas-vacaciones"), Permiso(Permisos.VacacionesGestionar)]
    public async Task<PoliticaVacaciones> CrearPolitica(PoliticaDto d) { var p = new PoliticaVacaciones(); await AplicarPolitica(p, d); Db.Politicas.Add(p); await Db.SaveChangesAsync(); return p; }
    [HttpPut("politicas-vacaciones/{id:int}"), Permiso(Permisos.VacacionesGestionar)]
    public async Task<PoliticaVacaciones> EditarPolitica(int id, PoliticaDto d)
    {
        var p = await Db.Politicas.Include(x => x.Tabla).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NoEncontrado("La política");
        await AplicarPolitica(p, d); await Db.SaveChangesAsync(); return p;
    }
    private async Task AplicarPolitica(PoliticaVacaciones p, PoliticaDto d)
    {
        if (d.PlazoVencimientoMeses is < 1 or > 60) throw ApiException.Validacion("El plazo de vencimiento debe estar entre 1 y 60 meses.");
        if (d.Tabla.Count == 0) throw ApiException.Validacion("La tabla de días por antigüedad no puede estar vacía.");
        // Mínimo de ley (LFT art. 76, reforma 2023)
        int Ley(int a) => a switch { 1 => 12, 2 => 14, 3 => 16, 4 => 18, 5 => 20, <= 10 => 22, <= 15 => 24, <= 20 => 26, <= 25 => 28, <= 30 => 30, _ => 32 };
        foreach (var f in d.Tabla)
            for (var a = f.AniosDesde; a <= Math.Min(f.AniosHasta, 40); a++)
                if (f.Dias < Ley(a)) throw ApiException.Validacion($"Para {a} años la ley exige al menos {Ley(a)} días; la tabla da {f.Dias}.");
        p.Nombre = d.Nombre.Trim(); p.PlazoVencimientoMeses = d.PlazoVencimientoMeses; p.AnticipacionMinimaDias = d.AnticipacionMinimaDias; p.DiasAlertaVencimiento = d.DiasAlertaVencimiento;
        if (d.PorDefecto) await Db.Politicas.Where(x => x.Id != p.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.PorDefecto, false));
        p.PorDefecto = d.PorDefecto;
        p.Tabla.Clear();
        p.Tabla.AddRange(d.Tabla.OrderBy(t => t.AniosDesde).Select(t => new PoliticaAntiguedad { AniosDesde = t.AniosDesde, AniosHasta = t.AniosHasta, Dias = t.Dias }));
    }

    // ───── Roles y permisos ─────
    [HttpGet("roles")]
    public async Task<object> RolesLista()
    {
        var rp = await Db.RolPermisos.ToListAsync();
        return new
        {
            Roles = Roles.Todos.Select(r => new { Clave = r, Nombre = Roles.Nombre(r), Permisos = rp.Where(x => x.Rol == r).Select(x => x.Permiso).Order().ToArray() }),
            Permisos = Permisos.Catalogo.Select(p => new { p.Clave, p.Nombre, p.Grupo })
        };
    }
    public record RolPermisosDto(string[] Permisos);
    [HttpPut("roles/{rol}"), Permiso(Permisos.UsuariosGestionar)]
    public async Task<IActionResult> EditarRol(string rol, RolPermisosDto d)
    {
        if (!Roles.Todos.Contains(rol)) throw ApiException.NoEncontrado("El rol");
        if (rol == Roles.Administrador && !d.Permisos.Contains(Permisos.UsuariosGestionar)) throw ApiException.Validacion("El Administrador no puede perder el permiso de gestionar usuarios.");
        var validos = Permisos.Catalogo.Select(p => p.Clave).ToHashSet();
        Db.RolPermisos.RemoveRange(Db.RolPermisos.Where(x => x.Rol == rol));
        foreach (var p in d.Permisos.Distinct().Where(validos.Contains)) Db.RolPermisos.Add(new RolPermiso { Rol = rol, Permiso = p });
        await Db.SaveChangesAsync();
        return NoContent();
    }

    // ───── Usuarios ─────
    public class FiltroUsuarios : ConsultaPaginada { public string? Q { get; set; } public string? Rol { get; set; } public bool? Activo { get; set; } }
    [HttpGet("usuarios"), Permiso(Permisos.UsuariosGestionar)]
    public async Task<object> Usuarios([FromQuery] FiltroUsuarios f)
    {
        var q = Db.Usuarios.Include(u => u.Colaborador).AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(u => EF.Functions.ILike(EF.Functions.Unaccent(u.Nombre), EF.Functions.Unaccent($"%{f.Q}%")) || EF.Functions.ILike(u.Email, $"%{f.Q}%"));
        if (!string.IsNullOrWhiteSpace(f.Rol)) q = q.Where(u => u.Roles.Contains(f.Rol));
        if (f.Activo is bool a) q = q.Where(u => u.Activo == a);
        f.Orden ??= "Nombre";
        return await q.Pag(f, u => new { u.Id, u.Email, u.Nombre, u.Roles, u.Sucursales, u.Activo, u.UltimoAcceso, u.DebeCambiarPassword, u.ColaboradorId, Colaborador = u.Colaborador?.NombreCompleto });
    }
    public record UsuarioDto(string Email, string Nombre, int? ColaboradorId, string[] Roles, int[] Sucursales, bool Activo = true, string? Password = null);
    [HttpPost("usuarios"), Permiso(Permisos.UsuariosGestionar)]
    public async Task<object> CrearUsuario(UsuarioDto d)
    {
        var u = new Usuario();
        var pwd = string.IsNullOrWhiteSpace(d.Password) ? "Wt" + Hash.Aleatorio(8) + "1" : d.Password;
        AuthController.ValidarPassword(pwd);
        AplicarUsuario(u, d);
        u.PasswordHash = Hash.Password(pwd); u.DebeCambiarPassword = true;
        Db.Usuarios.Add(u); await Db.SaveChangesAsync();
        return new { u.Id, passwordTemporal = pwd };
    }
    [HttpPut("usuarios/{id:int}"), Permiso(Permisos.UsuariosGestionar)]
    public async Task<object> EditarUsuario(int id, UsuarioDto d)
    {
        var u = await Db.Usuarios.FindAsync(id) ?? throw ApiException.NoEncontrado("El usuario");
        if (u.Id == Yo.UsuarioId && (!d.Activo || !d.Roles.Contains(Roles.Administrador)) && u.Roles.Contains(Roles.Administrador))
            throw ApiException.Validacion("No puedes quitarte el rol de Administrador ni desactivarte a ti mismo.");
        AplicarUsuario(u, d);
        if (!d.Activo) foreach (var t in Db.TokensRenovacion.Where(x => x.UsuarioId == id && x.RevocadoEn == null)) t.RevocadoEn = Reloj.Ahora;
        await Db.SaveChangesAsync();
        return new { u.Id };
    }
    /// <summary>Genera una contraseña temporal para el usuario.</summary>
    [HttpPost("usuarios/{id:int}/restablecer"), Permiso(Permisos.UsuariosGestionar, Permisos.ColaboradoresEditar)]
    public async Task<object> RestablecerUsuario(int id)
    {
        var u = await Db.Usuarios.FindAsync(id) ?? throw ApiException.NoEncontrado("El usuario");
        if (!Yo.Tiene(Permisos.UsuariosGestionar) && u.Roles.Contains(Roles.Administrador)) throw ApiException.Prohibido();
        var pwd = "Wt" + Hash.Aleatorio(8) + "1";
        u.PasswordHash = Hash.Password(pwd); u.DebeCambiarPassword = true;
        foreach (var t in Db.TokensRenovacion.Where(x => x.UsuarioId == id && x.RevocadoEn == null)) t.RevocadoEn = Reloj.Ahora;
        Bitacora.Registrar(u.ColaboradorId, "seguridad", "Se restableció el acceso", "Contraseña temporal generada por " + Yo.Nombre);
        await Db.SaveChangesAsync();
        return new { passwordTemporal = pwd };
    }
    private static void AplicarUsuario(Usuario u, UsuarioDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Email) || !d.Email.Contains('@')) throw ApiException.Validacion("El correo no es válido.");
        if (d.Roles.Length == 0 || d.Roles.Any(r => !Roles.Todos.Contains(r))) throw ApiException.Validacion("Selecciona al menos un rol válido.");
        u.Email = d.Email.Trim().ToLowerInvariant(); u.Nombre = d.Nombre.Trim(); u.ColaboradorId = d.ColaboradorId; u.Roles = d.Roles.Distinct().ToArray(); u.Sucursales = d.Sucursales.Distinct().ToArray(); u.Activo = d.Activo;
    }

    // ───── Llaves de API ─────
    [HttpGet("llaves-api"), Permiso(Permisos.LlavesApiGestionar)]
    public async Task<object> Llaves([FromQuery] ConsultaPaginada f)
    {
        f.Orden ??= "CreadoEn"; f.Dir ??= "desc";
        return await Db.LlavesApi.Paginar(f, k => new { k.Id, k.Nombre, k.Prefijo, k.Permisos, k.Sucursales, k.CreadoEn, k.UltimoUso, k.RevocadaEn, k.Expira });
    }
    public record LlaveDto(string Nombre, string[] Permisos, int[] Sucursales, DateTime? Expira);
    /// <summary>Crea una llave de API. La llave completa solo se muestra en esta respuesta.</summary>
    [HttpPost("llaves-api"), Permiso(Permisos.LlavesApiGestionar)]
    public async Task<object> CrearLlave(LlaveDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Nombre)) throw ApiException.Validacion("El nombre es obligatorio.");
        if (d.Permisos.Length == 0) throw ApiException.Validacion("Selecciona al menos un permiso.");
        var validos = Permisos.Catalogo.Select(p => p.Clave).ToHashSet();
        var pref = "wt_" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
        var secreto = Hash.Aleatorio(32);
        var k = new LlaveApi { Nombre = d.Nombre.Trim(), Prefijo = pref, Hash = Hash.Sha256(secreto), Permisos = d.Permisos.Where(validos.Contains).Distinct().ToArray(), Sucursales = d.Sucursales, Expira = d.Expira };
        Db.LlavesApi.Add(k); await Db.SaveChangesAsync();
        return new { k.Id, llave = $"{pref}.{secreto}" };
    }
    [HttpPost("llaves-api/{id:int}/revocar"), Permiso(Permisos.LlavesApiGestionar)]
    public async Task<IActionResult> Revocar(int id)
    {
        var k = await Db.LlavesApi.FindAsync(id) ?? throw ApiException.NoEncontrado("La llave");
        k.RevocadaEn ??= Reloj.Ahora; await Db.SaveChangesAsync(); return NoContent();
    }

    // ───── Bitácora, correos y tareas ─────
    public class FiltroBitacora : ConsultaPaginada { public int? ColaboradorId { get; set; } public string? Tipo { get; set; } public DateOnly? Desde { get; set; } public DateOnly? Hasta { get; set; } }
    /// <summary>Eventos de bitácora del sistema (auditoría).</summary>
    [HttpGet("bitacora"), Permiso(Permisos.ColaboradoresVer)]
    public async Task<object> BitacoraLista([FromQuery] FiltroBitacora f)
    {
        var visibles = (await ColaboradoresVisibles()).Select(c => c.Id);
        var q = Db.Bitacora.Where(b => b.ColaboradorId == null ? Yo.TodasSucursales : visibles.Contains(b.ColaboradorId.Value));
        if (f.ColaboradorId is int c) q = q.Where(b => b.ColaboradorId == c);
        if (!string.IsNullOrWhiteSpace(f.Tipo)) q = q.Where(b => b.Tipo == f.Tipo);
        if (f.Desde is DateOnly de) { var u = Zona.AUtc(de.ToDateTime(TimeOnly.MinValue), null); q = q.Where(b => b.Fecha >= u); }
        if (f.Hasta is DateOnly ha) { var u = Zona.AUtc(ha.AddDays(1).ToDateTime(TimeOnly.MinValue), null); q = q.Where(b => b.Fecha < u); }
        f.Orden ??= "Fecha"; f.Dir ??= "desc";
        return await q.Paginar(f, b => b);
    }

    [HttpGet("correos"), Permiso(Permisos.SistemaConfigurar)]
    public async Task<object> Correos([FromQuery] ConsultaPaginada f) { f.Orden ??= "CreadoEn"; f.Dir ??= "desc"; return await Db.Correos.Paginar(f, c => c); }

    [HttpGet("tareas"), Permiso(Permisos.SistemaConfigurar, Permisos.CatalogosGestionar)]
    public async Task<object> Tareas([FromQuery] ConsultaPaginada f)
    {
        f.Orden ??= "Inicio"; f.Dir ??= "desc";
        return new { Nombres = TareasProgramadas.Nombres, Ejecuciones = await Db.EjecucionesTarea.Where(e => !e.Tarea.StartsWith("cierre_dia:")).Paginar(f, e => e) };
    }
    /// <summary>Ejecuta las tareas programadas (todas o una). Pensado para el Programador de tareas de Windows con una llave de API.</summary>
    [HttpPost("tareas/ejecutar"), Permiso(Permisos.SistemaConfigurar)]
    public async Task<Dictionary<string, string>> Ejecutar([FromQuery] string? tarea)
    {
        var t = Svc<TareasProgramadas>();
        return tarea is null ? await t.EjecutarTodas() : new Dictionary<string, string> { [tarea] = await t.Ejecutar(tarea) };
    }
}
