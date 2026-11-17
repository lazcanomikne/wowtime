using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using WowTime.Api.Data;
using WowTime.Api.Domain;
using WowTime.Api.Infra;

namespace WowTime.Api.Services;

public class Bitacora(AppDb db, UsuarioActual actual, IReloj reloj)
{
    public void Registrar(int? colaboradorId, string tipo, string titulo, string? detalle = null, string? entidad = null, int? entidadId = null)
    {
        db.Bitacora.Add(new EventoBitacora
        {
            ColaboradorId = colaboradorId, Tipo = tipo, Titulo = titulo, Detalle = detalle,
            Fecha = reloj.Ahora, AutorUsuarioId = actual.UsuarioId, AutorNombre = actual.Autenticado ? actual.Nombre : "Sistema",
            Entidad = entidad, EntidadId = entidadId
        });
    }
}

public class Parametros(AppDb db)
{
    private Dictionary<string, string>? _cache;
    public async Task<string?> Texto(string clave)
    {
        _cache ??= await db.Parametros.AsNoTracking().ToDictionaryAsync(p => p.Clave, p => p.Valor);
        return _cache.TryGetValue(clave, out var v) ? v : null;
    }
    public async Task<int> Entero(string clave, int defecto) => int.TryParse(await Texto(clave), out var v) ? v : defecto;
    public async Task<bool> Booleano(string clave, bool defecto) => bool.TryParse(await Texto(clave), out var v) ? v : defecto;

    public const string ToleranciaMinutos = "asistencia.tolerancia_minutos";
    public const string RadioGeocerca = "asistencia.radio_geocerca_metros";
    public const string HoraCierreDiario = "asistencia.hora_cierre";
    public const string DocUrgente = "expedientes.umbral_urgente_dias";
    public const string DocProximo = "expedientes.umbral_proximo_dias";
    public const string QrMinAntes = "visitantes.qr_minutos_antes";
    public const string QrMinDespues = "visitantes.qr_minutos_despues";
    public const string VentanaCumple = "colaboradores.ventana_cumpleanos_dias";
    public const string PrefijoNumero = "colaboradores.prefijo_numero";
    public const string CoberturaMinima = "vacaciones.cobertura_minima_defecto";
    public const string AlertaVencimiento = "vacaciones.alerta_vencimiento_dias";
    public const string UrlPublica = "sistema.url_publica_front";

    public static readonly Parametro[] Defectos =
    [
        new() { Clave = ToleranciaMinutos, Valor = "10", Descripcion = "Minutos de tolerancia para registrar la entrada en horario", Tipo = "entero", Grupo = "Asistencia" },
        new() { Clave = RadioGeocerca, Valor = "150", Descripcion = "Radio de geocerca por omisión, en metros", Tipo = "entero", Grupo = "Asistencia" },
        new() { Clave = HoraCierreDiario, Valor = "23", Descripcion = "Hora local a partir de la cual se cierra la jornada y se generan faltas", Tipo = "entero", Grupo = "Asistencia" },
        new() { Clave = DocUrgente, Valor = "7", Descripcion = "Días o menos para marcar un documento como Urgente", Tipo = "entero", Grupo = "Expedientes" },
        new() { Clave = DocProximo, Valor = "30", Descripcion = "Días o menos para marcar un documento como Próximo", Tipo = "entero", Grupo = "Expedientes" },
        new() { Clave = QrMinAntes, Valor = "30", Descripcion = "Minutos antes de la llegada en que el QR empieza a ser válido", Tipo = "entero", Grupo = "Visitantes" },
        new() { Clave = QrMinDespues, Valor = "120", Descripcion = "Minutos después de la llegada en que el QR deja de ser válido", Tipo = "entero", Grupo = "Visitantes" },
        new() { Clave = VentanaCumple, Valor = "7", Descripcion = "Días hacia adelante para mostrar próximos cumpleaños", Tipo = "entero", Grupo = "Colaboradores" },
        new() { Clave = PrefijoNumero, Valor = "WC-", Descripcion = "Prefijo del número de colaborador", Tipo = "texto", Grupo = "Colaboradores" },
        new() { Clave = CoberturaMinima, Valor = "70", Descripcion = "Cobertura mínima por omisión para equipos nuevos (%)", Tipo = "entero", Grupo = "Vacaciones" },
        new() { Clave = AlertaVencimiento, Valor = "30", Descripcion = "Días antes de la fecha límite para alertar días por vencer", Tipo = "entero", Grupo = "Vacaciones" },
        new() { Clave = UrlPublica, Valor = "http://localhost:5173", Descripcion = "URL pública del front, usada en enlaces de QR y correos", Tipo = "texto", Grupo = "Sistema" },
    ];
}

public class Notificador(AppDb db, IReloj reloj, CanalEventos eventos)
{
    public async Task Enviar(int usuarioId, string tipo, string titulo, string? mensaje, string? enlace, string? claveUnica = null)
    {
        if (claveUnica is not null && await db.Notificaciones.AnyAsync(n => n.ClaveUnica == claveUnica)) return;
        if (claveUnica is not null && db.ChangeTracker.Entries<Notificacion>().Any(e => e.Entity.ClaveUnica == claveUnica)) return;
        db.Notificaciones.Add(new Notificacion
        {
            UsuarioId = usuarioId, Tipo = tipo, Titulo = titulo, Mensaje = mensaje, Enlace = enlace,
            CreadaEn = reloj.Ahora, ClaveUnica = claveUnica
        });
        eventos.PublicarUsuario(usuarioId, "notificacion", new { titulo });
    }

    public async Task EnviarAColaborador(int colaboradorId, string tipo, string titulo, string? mensaje, string? enlace, string? claveUnica = null)
    {
        var uid = await db.Usuarios.Where(u => u.ColaboradorId == colaboradorId && u.Activo).Select(u => (int?)u.Id).FirstOrDefaultAsync();
        if (uid is int id) await Enviar(id, tipo, titulo, mensaje, enlace, claveUnica is null ? null : $"{claveUnica}:{id}");
    }

    public async Task EnviarARol(string rol, int? sucursalId, string tipo, string titulo, string? mensaje, string? enlace, string? claveUnica = null, int[]? excluir = null)
    {
        var usuarios = await db.Usuarios.Where(u => u.Activo && u.Roles.Contains(rol)).ToListAsync();
        foreach (var u in usuarios)
        {
            if (excluir?.Contains(u.Id) == true) continue;
            if (sucursalId is int s && u.Sucursales.Length > 0 && !u.Sucursales.Contains(s)) continue;
            await Enviar(u.Id, tipo, titulo, mensaje, enlace, claveUnica is null ? null : $"{claveUnica}:{u.Id}");
        }
    }
}

/// <summary>Correo saliente. Si no hay SMTP configurado, queda en la bandeja de salida visible en Configuración.</summary>
public class Correo(AppDb db, IReloj reloj, IConfiguration cfg, ILogger<Correo> log)
{
    public async Task Enviar(string para, string asunto, string cuerpo)
    {
        var c = new CorreoSaliente { Para = para, Asunto = asunto, Cuerpo = cuerpo, CreadoEn = reloj.Ahora };
        db.Correos.Add(c);
        var host = cfg["Smtp:Host"];
        if (!string.IsNullOrWhiteSpace(host))
        {
            try
            {
                using var smtp = new System.Net.Mail.SmtpClient(host, int.TryParse(cfg["Smtp:Puerto"], out var p) ? p : 587)
                {
                    EnableSsl = cfg["Smtp:Ssl"] != "false",
                    Credentials = new System.Net.NetworkCredential(cfg["Smtp:Usuario"], cfg["Smtp:Password"])
                };
                using var msg = new System.Net.Mail.MailMessage(cfg["Smtp:Remitente"] ?? cfg["Smtp:Usuario"]!, para, asunto, cuerpo) { IsBodyHtml = true };
                await smtp.SendMailAsync(msg);
                c.EnviadoEn = reloj.Ahora;
            }
            catch (Exception ex) { c.Error = ex.Message; log.LogWarning(ex, "No se pudo enviar correo a {Para}", para); }
        }
        await db.SaveChangesAsync();
    }
}

/// <summary>Almacenamiento de archivos fuera de la carpeta pública. Se sirven solo por endpoint con sesión.</summary>
public class Almacen(IConfiguration cfg, IWebHostEnvironment env, AppDb db, IReloj reloj, UsuarioActual actual)
{
    public string Raiz
    {
        get
        {
            var r = cfg["Archivos:Ruta"];
            if (string.IsNullOrWhiteSpace(r)) r = Path.Combine(env.ContentRootPath, "..", "..", "..", "wowtime-data", "archivos");
            r = Path.GetFullPath(r);
            Directory.CreateDirectory(r);
            return r;
        }
    }

    public static readonly string[] TiposPermitidos = ["application/pdf", "image/jpeg", "image/png", "image/webp", "image/heic"];
    public const long MaxBytes = 10 * 1024 * 1024;

    public async Task<Archivo> Guardar(IFormFile f, string categoria, int? colaboradorId)
    {
        if (f.Length == 0) throw ApiException.Validacion("El archivo está vacío.");
        if (f.Length > MaxBytes) throw ApiException.Validacion("El archivo supera el máximo de 10 MB.");
        var tipo = f.ContentType?.ToLowerInvariant() ?? "";
        if (!TiposPermitidos.Contains(tipo)) throw ApiException.Validacion("Tipo de archivo no permitido. Usa PDF, JPG, PNG o WEBP.");
        var ext = Path.GetExtension(f.FileName);
        if (ext.Length > 8) ext = "";
        var rel = Path.Combine(categoria, reloj.Ahora.ToString("yyyy"), reloj.Ahora.ToString("MM"), $"{Guid.NewGuid():N}{ext}");
        var full = Path.Combine(Raiz, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using (var s = File.Create(full)) await f.CopyToAsync(s);
        var a = new Archivo
        {
            NombreOriginal = Path.GetFileName(f.FileName), ContentType = tipo, Tamano = f.Length, Ruta = rel,
            Categoria = categoria, ColaboradorId = colaboradorId, CreadoEn = reloj.Ahora, SubidoPorUsuarioId = actual.UsuarioId
        };
        db.Archivos.Add(a);
        return a;
    }

    public string RutaCompleta(Archivo a)
    {
        var full = Path.GetFullPath(Path.Combine(Raiz, a.Ruta));
        if (!full.StartsWith(Raiz, StringComparison.OrdinalIgnoreCase)) throw ApiException.Prohibido();
        return full;
    }
}

/// <summary>Canal de eventos del servidor (SSE) por sucursal y por usuario.</summary>
public class CanalEventos
{
    private readonly ConcurrentDictionary<Guid, (int? sucursal, int? usuario, Channel<string> canal)> _subs = new();

    public (Guid id, ChannelReader<string> lector) Suscribir(int? sucursal, int? usuario)
    {
        var c = Channel.CreateBounded<string>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        var id = Guid.NewGuid();
        _subs[id] = (sucursal, usuario, c);
        return (id, c.Reader);
    }

    public void Desuscribir(Guid id)
    {
        if (_subs.TryRemove(id, out var s)) s.canal.Writer.TryComplete();
    }

    public void PublicarSucursal(int sucursalId, string tipo, object datos)
    {
        var msg = Formato(tipo, datos);
        foreach (var s in _subs.Values.Where(s => s.sucursal is null || s.sucursal == sucursalId)) s.canal.Writer.TryWrite(msg);
    }

    public void PublicarUsuario(int usuarioId, string tipo, object datos)
    {
        var msg = Formato(tipo, datos);
        foreach (var s in _subs.Values.Where(s => s.usuario == usuarioId)) s.canal.Writer.TryWrite(msg);
    }

    private static string Formato(string tipo, object datos) => $"event: {tipo}\ndata: {JsonSerializer.Serialize(datos, Json.Opciones)}\n\n";
}
