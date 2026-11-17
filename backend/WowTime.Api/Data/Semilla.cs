using Microsoft.EntityFrameworkCore;
using WowTime.Api.Domain;
using WowTime.Api.Infra;
using WowTime.Api.Services;

namespace WowTime.Api.Data;

/// <summary>Catálogos base (siempre) y datos de demostración (comando "semilla").</summary>
public class Semilla(AppDb db, IReloj reloj, Almacen almacen, ServicioSaldos saldos, IServiceProvider sp, ILogger<Semilla> log)
{
    public const string PasswordDemo = "WowTime2026!";

    public static readonly (string Ubicacion, string Descripcion, string Texto)[] Mensajes =
    [
        ("logo_lema", "Lema bajo el logo del menú", "Personas que hacen espacios mejores"),
        ("menu_pie", "Frase al pie del menú lateral", "Un equipo limpio también hace grandes lugares"),
        ("dashboard_subtitulo", "Subtítulo del resumen cuando todo está en orden", "Todo en orden para un gran día de trabajo"),
        ("dashboard_subtitulo_alertas", "Subtítulo del resumen cuando hay pendientes", "Hay pendientes que requieren tu atención hoy"),
        ("dashboard_banner", "Banner del resumen de hoy", "Espacios más limpios, personas más felices."),
        ("ficha_pie", "Frase alternativa del pie del menú", "Personas que hacen grande cada día"),
        ("movil_inicio_lema", "Lema junto al avatar en Inicio móvil", "Un equipo más limpio hace grandes días"),
        ("invitado_lema", "Frase de Registrar invitado", "Un lugar seguro para grandes encuentros"),
        ("invitado_anfitrion", "Frase de la tarjeta del anfitrión", "Recibimos personas, impulsamos oportunidades"),
        ("invitado_encabezado", "Lema del encabezado de invitados", "Personas que hacen grandes días"),
        ("vacaciones_lema", "Lema de Solicitar vacaciones", "Tu tiempo, también cuenta"),
        ("cumple_1", "Mensaje de cumpleaños 1", "¡Feliz cumple, {nombre}!"),
        ("cumple_2", "Mensaje de cumpleaños 2", "¡Que tengas un gran día!"),
        ("cumple_3", "Mensaje de cumpleaños 3", "¡Muchas felicidades!"),
    ];

    /// <summary>Catálogos mínimos para que el sistema funcione. Idempotente; se ejecuta al arrancar.</summary>
    public async Task Catalogos()
    {
        if (!await db.RolPermisos.AnyAsync())
            foreach (var (rol, perms) in Permisos.PorRol)
                foreach (var p in perms) db.RolPermisos.Add(new RolPermiso { Rol = rol, Permiso = p });

        var existentes = await db.Parametros.Select(p => p.Clave).ToListAsync();
        foreach (var p in Parametros.Defectos.Where(p => !existentes.Contains(p.Clave)))
            db.Parametros.Add(new Parametro { Clave = p.Clave, Valor = p.Valor, Descripcion = p.Descripcion, Tipo = p.Tipo, Grupo = p.Grupo });

        var ubic = await db.MensajesMarca.Select(m => m.Ubicacion).ToListAsync();
        foreach (var m in Mensajes.Where(m => !ubic.Contains(m.Ubicacion)))
            db.MensajesMarca.Add(new MensajeMarca { Ubicacion = m.Ubicacion, Descripcion = m.Descripcion, Texto = m.Texto });

        if (!await db.Reglas.AnyAsync())
        {
            db.Reglas.AddRange(
                Regla("vacaciones", "Vacaciones", [TipoSolicitud.Vacaciones], false, TipoAprobador.SupervisorDirecto, TipoAprobador.RecursosHumanos),
                Regla("permisos", "Permisos", [TipoSolicitud.Permiso], false, TipoAprobador.SupervisorDirecto, TipoAprobador.RecursosHumanos),
                Regla("incidencias", "Incidencias de asistencia", [TipoSolicitud.IncidenciaAsistencia], false, TipoAprobador.SupervisorDirecto),
                Regla("solicitud_supervisor", "Solicitud del supervisor", [TipoSolicitud.Vacaciones, TipoSolicitud.Permiso], true, TipoAprobador.Gerente, TipoAprobador.RecursosHumanos));
        }
        if (!await db.TiposPermiso.AnyAsync())
            db.TiposPermiso.AddRange(
                new TipoPermiso { Nombre = "Permiso personal", ConGoce = true, MaximoDias = 3 },
                new TipoPermiso { Nombre = "Permiso sin goce de sueldo", ConGoce = false, MaximoDias = 10 },
                new TipoPermiso { Nombre = "Permiso por defunción", ConGoce = true, MaximoDias = 5 },
                new TipoPermiso { Nombre = "Permiso por paternidad", ConGoce = true, MaximoDias = 5 },
                new TipoPermiso { Nombre = "Cita médica", ConGoce = true, MaximoDias = 1 });
        if (!await db.TiposDocumento.AnyAsync())
            db.TiposDocumento.AddRange(TiposDocumentoBase().Select(t => new TipoDocumento { Nombre = t.n, ConVigencia = t.v, Obligatorio = true, Descripcion = t.d }));
        if (!await db.Politicas.AnyAsync())
        {
            var p = new PoliticaVacaciones { Nombre = "Política general", PlazoVencimientoMeses = 12, AnticipacionMinimaDias = 0, DiasAlertaVencimiento = 30, PorDefecto = true };
            p.Tabla.AddRange([
                new PoliticaAntiguedad { AniosDesde = 1, AniosHasta = 5, Dias = 20 },
                new PoliticaAntiguedad { AniosDesde = 6, AniosHasta = 10, Dias = 22 },
                new PoliticaAntiguedad { AniosDesde = 11, AniosHasta = 15, Dias = 24 },
                new PoliticaAntiguedad { AniosDesde = 16, AniosHasta = 20, Dias = 26 },
                new PoliticaAntiguedad { AniosDesde = 21, AniosHasta = 99, Dias = 28 }]);
            db.Politicas.Add(p);
        }
        if (!await db.Empresas.AnyAsync()) db.Empresas.Add(new Empresa { Nombre = "WOW! Time", ZonaHoraria = "America/Monterrey", PrefijoNumero = "WC-" });
        await db.SaveChangesAsync();

        // Festivos oficiales del año en curso y el siguiente si no existen
        var anio = Zona.Hoy(reloj, null).Year;
        foreach (var a in new[] { anio, anio + 1 })
        {
            if (await db.DiasFestivos.AnyAsync(f => f.Fecha.Year == a)) continue;
            db.DiasFestivos.AddRange(FestivosMexico(a).Select(f => new DiaFestivo { Fecha = f.fecha, Nombre = f.nombre }));
        }
        await db.SaveChangesAsync();

        // Administrador inicial si no hay usuarios
        if (!await db.Usuarios.AnyAsync())
        {
            var pwd = Environment.GetEnvironmentVariable("ADMIN_PASSWORD_INICIAL");
            if (string.IsNullOrWhiteSpace(pwd)) pwd = Hash.Aleatorio(12);
            db.Usuarios.Add(new Usuario { Email = "admin@wowtime.mx", Nombre = "Administrador", PasswordHash = Hash.Password(pwd), Roles = [Roles.Administrador], DebeCambiarPassword = true });
            await db.SaveChangesAsync();
            log.LogWarning("Se creó el usuario admin@wowtime.mx con la contraseña inicial definida en ADMIN_PASSWORD_INICIAL (o una aleatoria: {Pwd}).", pwd);
        }
    }

    public static (string n, bool v, string d)[] TiposDocumentoBase() =>
    [
        ("INE", true, "Identificación oficial vigente"),
        ("Comprobante de domicilio", true, "No mayor a 3 meses"),
        ("Constancia de situación fiscal", false, "Emitida por el SAT"),
        ("Examen médico", true, "Examen médico de ingreso o periódico"),
        ("Constancia de capacitación", true, "DC-3 o equivalente"),
        ("Acta de nacimiento", false, "Copia certificada"),
        ("CURP", false, "Clave Única de Registro de Población"),
        ("Comprobante de estudios", false, "Último grado de estudios"),
        ("Contrato laboral", false, "Contrato firmado"),
    ];

    private static ReglaAprobacion Regla(string clave, string nombre, TipoSolicitud[] tipos, bool soloConReportes, params TipoAprobador[] pasos)
    {
        var r = new ReglaAprobacion { Clave = clave, Nombre = nombre, Tipos = tipos, SoloSolicitantesConReportes = soloConReportes, Activa = true };
        for (var i = 0; i < pasos.Length; i++) r.Pasos.Add(new PasoRegla { Orden = i + 1, TipoAprobador = pasos[i] });
        return r;
    }

    /// <summary>Días de descanso obligatorio (LFT art. 74).</summary>
    public static List<(DateOnly fecha, string nombre)> FestivosMexico(int a)
    {
        DateOnly NLunes(int mes, int n)
        {
            var d = new DateOnly(a, mes, 1);
            while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
            return d.AddDays(7 * (n - 1));
        }
        var l = new List<(DateOnly, string)>
        {
            (new DateOnly(a, 1, 1), "Año Nuevo"),
            (NLunes(2, 1), "Día de la Constitución"),
            (NLunes(3, 3), "Natalicio de Benito Juárez"),
            (new DateOnly(a, 5, 1), "Día del Trabajo"),
            (new DateOnly(a, 9, 16), "Día de la Independencia"),
            (NLunes(11, 3), "Día de la Revolución"),
            (new DateOnly(a, 12, 25), "Navidad"),
        };
        if ((a - 2024) % 6 == 0) l.Add((new DateOnly(a, 10, 1), "Transmisión del Poder Ejecutivo Federal"));
        return l.OrderBy(x => x.Item1).ToList();
    }

    // ─────────────────────────── Datos de demostración ───────────────────────────

    private Random rnd = new(2026);
    private DateOnly hoy;
    private DateTime ahoraLocal;
    private const string Tz = "America/Monterrey";

    private static readonly string[] NombresM = ["José", "Luis", "Juan", "Miguel", "Carlos", "Jorge", "Alejandro", "Ricardo", "Fernando", "Eduardo", "Roberto", "Javier", "Daniel", "Raúl", "Héctor", "Arturo", "Óscar", "Sergio", "Mario", "Francisco", "Gerardo", "Rubén", "Iván", "Andrés", "Emilio", "Hugo", "Omar", "Rafael", "Víctor", "Pedro"];
    private static readonly string[] NombresF = ["María", "Guadalupe", "Rosa", "Laura", "Patricia", "Adriana", "Claudia", "Verónica", "Gabriela", "Mónica", "Alejandra", "Leticia", "Silvia", "Norma", "Karla", "Daniela", "Paola", "Andrea", "Fernanda", "Valeria", "Mariana", "Diana", "Brenda", "Elena", "Lucía", "Martha", "Yolanda", "Teresa", "Liliana", "Rocío"];
    private static readonly string[] Apellidos = ["García", "Hernández", "Martínez", "López", "González", "Rodríguez", "Pérez", "Sánchez", "Ramírez", "Cruz", "Flores", "Gómez", "Morales", "Vázquez", "Reyes", "Jiménez", "Torres", "Díaz", "Gutiérrez", "Ruiz", "Mendoza", "Aguilar", "Ortiz", "Castillo", "Moreno", "Romero", "Herrera", "Medina", "Garza", "Treviño", "Villarreal", "Elizondo", "Salinas", "Cantú", "Leal", "Saldaña", "Rivera", "Domínguez", "Vega", "Núñez"];

    public async Task Cargar(bool reiniciar)
    {
        if (await db.Colaboradores.AnyAsync())
        {
            if (!reiniciar) throw new InvalidOperationException("La base ya tiene colaboradores. Usa 'semilla --reiniciar' para borrar todo y volver a cargar.");
            log.LogWarning("Borrando datos existentes…");
            var tablas = db.Model.GetEntityTypes().Select(t => t.GetTableName()).Where(t => t is not null && t != "__migraciones").Distinct();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE " + string.Join(", ", tablas.Select(t => $"\"{t}\"")) + " RESTART IDENTITY CASCADE;");
            db.ChangeTracker.Clear();
        }
        await Catalogos();
        // El admin inicial se reemplaza por los usuarios de demostración
        db.Usuarios.RemoveRange(db.Usuarios);
        await db.SaveChangesAsync();

        ahoraLocal = Zona.ALocal(reloj.Ahora, Tz);
        hoy = DateOnly.FromDateTime(ahoraLocal);
        var empresa = await db.Empresas.FirstAsync();

        // Sucursales
        var mty = new Sucursal { EmpresaId = empresa.Id, Nombre = "Sucursal Monterrey", Direccion = "Av. Constitución 400 Ote., Centro, Monterrey, N.L.", Latitud = 25.6694, Longitud = -100.3097, RadioMetros = 200, ZonaHoraria = Tz };
        var slt = new Sucursal { EmpresaId = empresa.Id, Nombre = "Sucursal Saltillo", Direccion = "Blvd. Venustiano Carranza 2300, Saltillo, Coah.", Latitud = 25.4383, Longitud = -100.9737, RadioMetros = 150, ZonaHoraria = Tz };
        db.Sucursales.AddRange(mty, slt);
        await db.SaveChangesAsync();

        // Áreas y equipos
        string[] areasMty = ["Dirección", "Operaciones", "Recursos Humanos", "Ventas", "Finanzas", "TI", "Marketing", "Diseño"];
        var areas = areasMty.Select(n => new Area { SucursalId = mty.Id, Nombre = n }).ToList();
        var areasSlt = new[] { "Operaciones", "Recursos Humanos" }.Select(n => new Area { SucursalId = slt.Id, Nombre = n }).ToList();
        db.Areas.AddRange(areas); db.Areas.AddRange(areasSlt);
        await db.SaveChangesAsync();
        Area A(string n) => areas.First(a => a.Nombre == n);
        var eqNorte = new Equipo { AreaId = A("Operaciones").Id, Nombre = "Equipo Norte", CoberturaMinima = 70 };
        var eqSur = new Equipo { AreaId = A("Operaciones").Id, Nombre = "Equipo Sur", CoberturaMinima = 70 };
        var eqCentro = new Equipo { AreaId = A("Operaciones").Id, Nombre = "Equipo Centro", CoberturaMinima = 70 };
        var eqOficina = new Equipo { AreaId = A("Recursos Humanos").Id, Nombre = "Equipo Administrativo", CoberturaMinima = 60 };
        var eqSaltillo = new Equipo { AreaId = areasSlt[0].Id, Nombre = "Equipo Saltillo", CoberturaMinima = 70 };
        db.Equipos.AddRange(eqNorte, eqSur, eqCentro, eqOficina, eqSaltillo);

        // Puestos
        string[] puestosN = ["Dirección general", "Gerente de Operaciones", "Recursos Humanos", "Supervisora de Limpieza", "Supervisor de Área", "Personal de Limpieza", "Auxiliar de Limpieza", "Operador de Limpieza", "Recepcionista", "Analista administrativo"];
        int[] niveles = [1, 2, 2, 3, 3, 4, 4, 4, 4, 3];
        var puestos = puestosN.Select((n, i) => new Puesto { Nombre = n, Nivel = niveles[i] }).ToList();
        db.Puestos.AddRange(puestos);
        await db.SaveChangesAsync();
        Puesto P(string n) => puestos.First(p => p.Nombre == n);
        var tiposDoc = await db.TiposDocumento.OrderBy(t => t.Id).ToListAsync();
        foreach (var p in puestos)
            foreach (var t in tiposDoc) db.PuestoDocumentos.Add(new PuestoDocumento { PuestoId = p.Id, TipoDocumentoId = t.Id });

        // Turnos
        var tMat = new Turno { Nombre = "Turno matutino", HoraEntrada = new(8, 0), HoraSalida = new(17, 0), ToleranciaMinutos = 10, DiasDescanso = [6, 7] };
        var tTem = new Turno { Nombre = "Turno temprano", HoraEntrada = new(6, 0), HoraSalida = new(14, 0), ToleranciaMinutos = 10, DiasDescanso = [6, 7] };
        var tVes = new Turno { Nombre = "Turno vespertino", HoraEntrada = new(12, 0), HoraSalida = new(20, 0), ToleranciaMinutos = 10, DiasDescanso = [6, 7] };
        var tSab = new Turno { Nombre = "Turno lunes a sábado", HoraEntrada = new(7, 0), HoraSalida = new(15, 0), ToleranciaMinutos = 10, DiasDescanso = [7] };
        db.Turnos.AddRange(tMat, tTem, tVes, tSab);

        // Dispositivos
        var acceso = new Dispositivo { Nombre = "Acceso principal", Tipo = TipoDispositivo.Terminal, SucursalId = mty.Id };
        var recepcionQr = new Dispositivo { Nombre = "Recepción", Tipo = TipoDispositivo.LectorQr, SucursalId = mty.Id };
        var kiosco = new Dispositivo { Nombre = "Quiosco de asistencia", Tipo = TipoDispositivo.Kiosco, SucursalId = mty.Id };
        var accesoSlt = new Dispositivo { Nombre = "Acceso Saltillo", Tipo = TipoDispositivo.Terminal, SucursalId = slt.Id };
        db.Dispositivos.AddRange(acceso, recepcionQr, kiosco, accesoSlt);
        await db.SaveChangesAsync();

        var num = 300;
        var todos = new List<Colaborador>();
        Colaborador Nuevo(string nombres, string apellidos, string puesto, string area, Sucursal suc, Turno turno, Colaborador? sup, Equipo? eq = null,
            DateOnly? ingreso = null, DateOnly? nacimiento = null, string? numero = null, char genero = 'F')
        {
            var c = new Colaborador
            {
                Numero = numero ?? $"WC-{num++:D4}", Nombres = nombres, Apellidos = apellidos, PuestoId = P(puesto).Id,
                AreaId = (suc == mty ? areas : areasSlt).FirstOrDefault(a => a.Nombre == area)?.Id ?? (suc == mty ? A("Operaciones").Id : areasSlt[0].Id),
                SucursalId = suc.Id, TurnoId = turno.Id, Supervisor = sup, EquipoId = eq?.Id,
                FechaIngreso = ingreso ?? hoy.AddDays(-rnd.Next(400, 3600)),
                FechaNacimiento = nacimiento ?? new DateOnly(rnd.Next(1970, 2003), rnd.Next(1, 13), rnd.Next(1, 28)),
                Genero = genero == 'F' ? "Femenino" : "Masculino",
                Email = $"{Simple(nombres)}.{Simple(apellidos.Split(' ')[0])}@wowtime.mx",
                Telefono = $"81{rnd.Next(10000000, 99999999)}",
                DispositivoId = suc == mty ? acceso.Id : accesoSlt.Id,
                PinHash = Hash.Password("1234"),
                Curp = $"{Simple(apellidos)[..2].ToUpper()}{Simple(nombres)[..2].ToUpper()}{rnd.Next(700101, 991231)}{(genero == 'F' ? "M" : "H")}NLRRR0{rnd.Next(1, 9)}",
                Rfc = $"{Simple(apellidos)[..2].ToUpper()}{Simple(nombres)[..2].ToUpper()}{rnd.Next(700101, 991231)}XX{rnd.Next(1, 9)}",
                Nss = $"{rnd.Next(10000, 99999)}{rnd.Next(100000, 999999)}",
                Direccion = $"Calle {Apellidos[rnd.Next(Apellidos.Length)]} {rnd.Next(100, 2500)}, Col. Centro, {(suc == mty ? "Monterrey, N.L." : "Saltillo, Coah.")}",
                ContactoEmergenciaNombre = $"{NombresF[rnd.Next(NombresF.Length)]} {Apellidos[rnd.Next(Apellidos.Length)]}",
                ContactoEmergenciaTelefono = $"81{rnd.Next(10000000, 99999999)}",
            };
            c.Esquema = new EsquemaTrabajo { ColaboradorId = 0, Modalidad = Modalidad.Presencial, CentroBaseSucursalId = suc.Id, DiasOficina = turno.DiasDescanso.Contains(6) ? [1, 2, 3, 4, 5] : [1, 2, 3, 4, 5, 6], RegistroPermitido = MetodoRegistro.TerminalYApp };
            todos.Add(c);
            return c;
        }

        // Organigrama de Organigrama_Autorizaciones.png
        var carolina = Nuevo("Carolina", "Guajardo", "Dirección general", "Dirección", mty, tMat, null, null, hoy.AddYears(-9), numero: "WC-0001");
        var mariana = Nuevo("Mariana", "Torres", "Recursos Humanos", "Recursos Humanos", mty, tMat, carolina, eqOficina, hoy.AddYears(-6).AddDays(-40), numero: "WC-0012");
        var carlosM = Nuevo("Carlos", "Méndez", "Gerente de Operaciones", "Operaciones", mty, tMat, carolina, null, hoy.AddYears(-7), numero: "WC-0015", genero: 'M');
        var ana = Nuevo("Ana Lucía", "Martínez", "Supervisora de Limpieza", "Operaciones", mty, tMat, carlosM, eqNorte, new DateOnly(hoy.Year - 5, 12, 10), hoy.AddDays(5).AddYears(-34), "WC-0248");
        var sofia = Nuevo("Sofía", "Herrera", "Personal de Limpieza", "Operaciones", mty, tMat, ana, eqNorte, hoy.AddYears(-3).AddDays(-75), numero: "WC-0251");
        var jorge = Nuevo("Jorge", "López", "Personal de Limpieza", "Operaciones", mty, tMat, ana, eqNorte, hoy.AddYears(-2).AddDays(-20), numero: "WC-0252", genero: 'M');
        var diego = Nuevo("Diego", "Ramírez", "Personal de Limpieza", "Operaciones", mty, tMat, ana, eqNorte, hoy.AddYears(-4).AddDays(-120), numero: "WC-0253", genero: 'M');
        // Otros supervisores de Monterrey
        var andrea = Nuevo("Andrea", "Ruiz", "Supervisora de Limpieza", "Operaciones", mty, tTem, carlosM, eqSur, hoy.AddYears(-5).AddDays(-200), numero: "WC-0101");
        var veronica = Nuevo("Verónica", "Castro", "Supervisor de Área", "Operaciones", mty, tVes, carlosM, eqCentro, hoy.AddYears(-4).AddDays(-30), hoy.AddDays(4).AddYears(-38), "WC-0102");
        var ricardo = Nuevo("Ricardo", "Solís", "Supervisor de Área", "Operaciones", mty, tMat, carlosM, eqNorte, hoy.AddYears(-6).AddDays(-10), numero: "WC-0103", genero: 'M');
        var recep = Nuevo("Laura", "Jiménez Salinas", "Recepcionista", "Recursos Humanos", mty, tMat, mariana, eqOficina, hoy.AddYears(-2), numero: "WC-0130");
        // Personas mencionadas en los mockups
        var named = new List<Colaborador>
        {
            Nuevo("Daniela", "Torres", "Auxiliar de Limpieza", "Operaciones", mty, tTem, andrea, eqSur, hoy.AddYears(-3)),
            Nuevo("Miguel", "Sánchez", "Operador de Limpieza", "Operaciones", mty, tTem, andrea, eqSur, hoy.AddYears(-2).AddDays(-60), genero: 'M'),
            Nuevo("Carlos", "Ramírez", "Operador de Limpieza", "Operaciones", mty, tMat, ricardo, eqNorte, hoy.AddYears(-1).AddDays(-200), genero: 'M'),
            Nuevo("María", "Fernández", "Auxiliar de Limpieza", "Operaciones", mty, tMat, ricardo, eqNorte, hoy.AddYears(-3)),
            Nuevo("Luis", "García", "Operador de Limpieza", "Operaciones", mty, tTem, andrea, eqSur, hoy.AddYears(-2), genero: 'M'),
            Nuevo("Paola", "Méndez", "Auxiliar de Limpieza", "Operaciones", mty, tVes, veronica, eqCentro, hoy.AddYears(-2), hoy.AddDays(1).AddYears(-27)),
            Nuevo("Luis", "Torres", "Operador de Limpieza", "Operaciones", mty, tVes, veronica, eqCentro, hoy.AddYears(-3), hoy.AddDays(2).AddYears(-31), genero: 'M'),
            Nuevo("Valeria", "Torres", "Auxiliar de Limpieza", "Operaciones", mty, tMat, ricardo, eqNorte, hoy.AddYears(-1).AddDays(-30)),
            Nuevo("Jorge", "Sánchez", "Operador de Limpieza", "Operaciones", mty, tTem, andrea, eqSur, hoy.AddYears(-4), genero: 'M'),
            Nuevo("Daniela", "Núñez", "Analista administrativo", "Recursos Humanos", mty, tMat, mariana, eqOficina, hoy.AddYears(-2)),
            Nuevo("Emilio", "Garza", "Analista administrativo", "TI", mty, tMat, carlosM, null, hoy.AddYears(-3), genero: 'M'),
            Nuevo("Diego", "López", "Personal de Limpieza", "Operaciones", mty, tMat, ricardo, eqNorte, hoy.AddYears(-2), genero: 'M'),
            Nuevo("Mariana", "Cruz", "Auxiliar de Limpieza", "Operaciones", mty, tVes, veronica, eqCentro, hoy.AddYears(-1).AddDays(-100)),
            Nuevo("Luis", "Mendoza", "Operador de Limpieza", "Operaciones", mty, tMat, ricardo, eqNorte, hoy.AddYears(-5), genero: 'M'),
            Nuevo("Paola", "Ruiz", "Auxiliar de Limpieza", "Operaciones", mty, tTem, andrea, eqSur, hoy.AddYears(-2)),
        };
        // Generados hasta ~160 en Monterrey
        var sups = new[] { (andrea, tTem, eqSur), (veronica, tVes, eqCentro), (ricardo, tMat, eqNorte) };
        string[] puestosOp = ["Auxiliar de Limpieza", "Operador de Limpieza", "Personal de Limpieza"];
        var objetivoMty = 160;
        var i2 = 0;
        while (todos.Count(c => c.SucursalId == mty.Id) < objetivoMty - 2)
        {
            var (s, t, e) = sups[i2++ % sups.Length];
            var f = rnd.Next(2) == 0;
            Nuevo(f ? NombresF[rnd.Next(NombresF.Length)] : NombresM[rnd.Next(NombresM.Length)],
                $"{Apellidos[rnd.Next(Apellidos.Length)]} {Apellidos[rnd.Next(Apellidos.Length)]}", puestosOp[rnd.Next(puestosOp.Length)], "Operaciones",
                mty, t, s, e, genero: f ? 'F' : 'M');
        }
        // Dos colaboradores sin supervisor (alerta de estructura)
        var sinSup1 = Nuevo("Ernesto", "Villarreal Cantú", "Auxiliar de Limpieza", "Operaciones", mty, tMat, null, null, hoy.AddDays(-6), genero: 'M');
        var sinSup2 = Nuevo("Brenda", "Leal Saldaña", "Operador de Limpieza", "Operaciones", mty, tMat, null, null, hoy.AddDays(-3));
        // Saltillo
        var daniel = Nuevo("Daniel", "Ramírez", "Recursos Humanos", "Recursos Humanos", slt, tMat, carolina, null, hoy.AddYears(-4), numero: "WC-0020", genero: 'M');
        var rosa = Nuevo("Rosa", "Elizondo", "Supervisora de Limpieza", "Operaciones", slt, tSab, carolina, eqSaltillo, hoy.AddYears(-5), numero: "WC-0021");
        for (var k = 0; k < 24; k++)
        {
            var f = rnd.Next(2) == 0;
            Nuevo(f ? NombresF[rnd.Next(NombresF.Length)] : NombresM[rnd.Next(NombresM.Length)], $"{Apellidos[rnd.Next(Apellidos.Length)]} {Apellidos[rnd.Next(Apellidos.Length)]}",
                puestosOp[rnd.Next(puestosOp.Length)], "Operaciones", slt, tSab, rosa, eqSaltillo, genero: f ? 'F' : 'M');
        }
        // Correos únicos
        var vistos = new HashSet<string>();
        foreach (var c in todos)
        {
            var baseMail = c.Email!; var n = 2;
            while (!vistos.Add(c.Email!)) c.Email = baseMail.Replace("@", $"{n++}@");
        }
        db.Colaboradores.AddRange(todos);
        await db.SaveChangesAsync();
        foreach (var c in todos) c.SupervisorId = c.Supervisor?.Id;

        // Esquema de Ana Lucía: híbrido, oficina L-M-V, remoto M-J
        ana.Esquema!.Modalidad = Modalidad.Hibrido;
        ana.Esquema.DiasOficina = [1, 3, 5];
        ana.Esquema.DiasRemotos = [2, 4];
        foreach (var c in new[] { mariana, carolina, daniel }) { c.Esquema!.Modalidad = Modalidad.Hibrido; c.Esquema.DiasOficina = [1, 2, 3, 4]; c.Esquema.DiasRemotos = [5]; }
        foreach (var c in todos.Where(x => x.Esquema!.Modalidad == Modalidad.Presencial && rnd.Next(10) == 0)) c.Esquema!.RegistroPermitido = MetodoRegistro.Terminal;
        sofia.Esquema!.RequiereFoto = true;
        db.UbicacionesAutorizadas.AddRange(
            new UbicacionAutorizada { ColaboradorId = ana.Id, Tipo = TipoUbicacion.Domicilio, Nombre = "Domicilio", Latitud = 25.6866, Longitud = -100.3161, RadioMetros = 150 },
            new UbicacionAutorizada { ColaboradorId = mariana.Id, Tipo = TipoUbicacion.Domicilio, Nombre = "Domicilio", Latitud = 25.6512, Longitud = -100.2898, RadioMetros = 150 },
            new UbicacionAutorizada { ColaboradorId = carolina.Id, Tipo = TipoUbicacion.Domicilio, Nombre = "Domicilio", Latitud = 25.6489, Longitud = -100.3562, RadioMetros = 150 },
            new UbicacionAutorizada { ColaboradorId = daniel.Id, Tipo = TipoUbicacion.Domicilio, Nombre = "Domicilio", Latitud = 25.4291, Longitud = -100.9952, RadioMetros = 150 });
        await db.SaveChangesAsync();

        // Usuarios por rol
        Usuario U(Colaborador? c, string email, string nombre, string[] roles, int[] sucursales)
            => new() { ColaboradorId = c?.Id, Email = email, Nombre = nombre, PasswordHash = Hash.Password(PasswordDemo), Roles = roles, Sucursales = sucursales };
        db.Usuarios.AddRange(
            U(carolina, "admin@wowtime.mx", "Carolina Guajardo", [Roles.Administrador, Roles.Colaborador], []),
            U(mariana, "rh@wowtime.mx", "Mariana Torres", [Roles.RecursosHumanos, Roles.Colaborador], [mty.Id]),
            U(daniel, "daniel.ramirez@wowtime.mx", "Daniel Ramírez", [Roles.RecursosHumanos, Roles.Colaborador], [mty.Id, slt.Id]),
            U(carlosM, "gerente@wowtime.mx", "Carlos Méndez", [Roles.Gerente, Roles.Colaborador], [mty.Id]),
            U(ana, "supervisor@wowtime.mx", "Ana Lucía Martínez", [Roles.Supervisor, Roles.Colaborador], [mty.Id]),
            U(sofia, "colaborador@wowtime.mx", "Sofía Herrera", [Roles.Colaborador], [mty.Id]),
            U(recep, "recepcion@wowtime.mx", "Laura Jiménez", [Roles.Recepcion, Roles.Colaborador], [mty.Id]),
            U(andrea, "andrea.ruiz@wowtime.mx", "Andrea Ruiz", [Roles.Supervisor, Roles.Colaborador], [mty.Id]),
            U(veronica, "veronica.castro@wowtime.mx", "Verónica Castro", [Roles.Supervisor, Roles.Colaborador], [mty.Id]),
            U(ricardo, "ricardo.solis@wowtime.mx", "Ricardo Solís", [Roles.Supervisor, Roles.Colaborador], [mty.Id]),
            U(rosa, "rosa.elizondo@wowtime.mx", "Rosa Elizondo", [Roles.Supervisor, Roles.Colaborador], [slt.Id]));
        foreach (var c in named.Take(4)) db.Usuarios.Add(U(c, c.Email!, c.NombreCompleto, [Roles.Colaborador], [c.SucursalId]));
        await db.SaveChangesAsync();
        var uMariana = await db.Usuarios.FirstAsync(u => u.ColaboradorId == mariana.Id);
        var uCarlos = await db.Usuarios.FirstAsync(u => u.ColaboradorId == carlosM.Id);
        var uAna = await db.Usuarios.FirstAsync(u => u.ColaboradorId == ana.Id);
        var uDaniel = await db.Usuarios.FirstAsync(u => u.ColaboradorId == daniel.Id);

        // Saldos (política general: 20 días, vencimiento a 12 meses)
        var pol = await db.Politicas.Include(p => p.Tabla).FirstAsync(p => p.PorDefecto);
        foreach (var c in todos) await saldos.Otorgar(c, pol, hoy);
        await db.SaveChangesAsync();
        var saldosTodos = await db.Saldos.ToListAsync();
        foreach (var s in saldosTodos)
        {
            var usados = s.FechaLimite <= hoy.AddDays(60) ? (int)s.DiasOtorgados - rnd.Next(0, 4) : rnd.Next(0, 14);
            s.DiasUsados = Math.Min(usados, s.DiasOtorgados);
            if (s.DiasUsados > 0)
                db.MovimientosSaldo.Add(new MovimientoSaldo { SaldoVacacionesId = s.Id, ColaboradorId = s.ColaboradorId, Tipo = TipoMovimientoSaldo.Consumo, Dias = s.DiasUsados, Fecha = reloj.Ahora, Motivo = "Vacaciones tomadas antes del arranque del sistema" });
        }
        void Usados(Colaborador c, decimal usados)
        {
            var s = saldosTodos.Where(x => x.ColaboradorId == c.Id).OrderByDescending(x => x.Periodo).First();
            s.DiasUsados = usados;
        }
        Usados(ana, 7); Usados(sofia, 8); Usados(named[2], 18); Usados(named[7], 11); Usados(named[9], 9); Usados(named[10], 5);
        await db.SaveChangesAsync();

        // Asignación temporal vigente de Ana Lucía
        db.Asignaciones.Add(new AsignacionTemporal { ColaboradorId = ana.Id, Tipo = TipoAsignacion.HomeOffice, FechaInicio = hoy.AddDays(-1), FechaFin = hoy.AddDays(3), Motivo = "Remodelación del área de supervisión", Estado = "Vigente" });
        db.Asignaciones.Add(new AsignacionTemporal { ColaboradorId = ana.Id, Tipo = TipoAsignacion.OtraSucursal, SucursalId = slt.Id, FechaInicio = hoy.AddDays(-40), FechaFin = hoy.AddDays(-36), Motivo = "Apoyo por inventario en Sucursal Saltillo", Estado = "Concluida" });

        // Delegación vigente: Carlos Méndez → Mariana Torres
        db.Delegaciones.Add(new Delegacion { DeColaboradorId = carlosM.Id, HaciaColaboradorId = mariana.Id, FechaInicio = hoy.AddDays(-2), FechaFin = hoy.AddDays(2), Tipos = [TipoSolicitud.Vacaciones, TipoSolicitud.Permiso], CreadoPor = uCarlos.Id });
        await db.SaveChangesAsync();

        await Asistencia(todos, mty, acceso, ana);
        await SolicitudesDemo(todos, named, ana, sofia, jorge, diego, andrea, mariana, carlosM, uMariana, uCarlos, uAna, uDaniel, eqNorte, ricardo);
        await VisitantesDemo(mty, ana, mariana, recep, recepcionQr);
        await DocumentosDemo(todos, ana, named, jorge);
        await FichaDemo(ana, uMariana, uDaniel);
        await NotificacionesDemo(uMariana, uAna);

        // Llave de dispositivo para el quiosco y la terminal (se muestran en Configuración > Dispositivos al regenerarlas)
        await db.SaveChangesAsync();
        var tareas = sp.GetRequiredService<TareasProgramadas>();
        await tareas.Ejecutar("asignaciones");
        await tareas.Ejecutar("qr");
        log.LogInformation("Semilla cargada: {N} colaboradores.", todos.Count);
    }

    private static string Simple(string s) => s.Normalizar().Replace(" ", "").Replace("ñ", "n");

    private async Task Asistencia(List<Colaborador> todos, Sucursal mty, Dispositivo acceso, Colaborador ana)
    {
        var turnos = await db.Turnos.ToDictionaryAsync(t => t.Id);
        var festivos = (await db.DiasFestivos.Select(f => f.Fecha).ToListAsync()).ToHashSet();
        var regs = new List<RegistroAsistencia>();
        var incs = new List<IncidenciaAsistencia>();
        var activos = todos.Where(c => c.Estatus == EstatusColaborador.Activo).ToList();
        for (var d = hoy.AddDays(-45); d <= hoy; d = d.AddDays(1))
        {
            if (festivos.Contains(d)) continue;
            var esHoy = d == hoy;
            foreach (var c in activos)
            {
                if (c.FechaIngreso > d) continue;
                var t = turnos[c.TurnoId!.Value];
                if (t.DiasDescanso.Contains(Reglas.DiaIso(d))) continue;
                var r = rnd.NextDouble();
                if (c.Id == ana.Id) r = 0.5;
                if (!esHoy && r < 0.03)
                {
                    incs.Add(new IncidenciaAsistencia { ColaboradorId = c.Id, SucursalId = c.SucursalId, Fecha = d, Tipo = TipoIncidencia.Falta, Detalle = "Sin registro de entrada en la jornada.", Estado = d >= hoy.AddDays(-4) ? EstadoIncidencia.PorRevisar : EstadoIncidencia.Confirmada });
                    continue;
                }
                if (esHoy && r < 0.12) continue; // aún no llega
                var retardo = r > 0.93;
                var minutos = retardo ? rnd.Next(12, 40) : -rnd.Next(1, 25);
                if (c.Id == ana.Id) minutos = -58;
                var horaE = t.HoraEntrada.AddMinutes(minutos);
                var dtE = d.ToDateTime(horaE);
                if (esHoy && dtE > ahoraLocal) continue;
                var remoto = c.Esquema!.Modalidad == Modalidad.Hibrido && c.Esquema.DiasRemotos.Contains(Reglas.DiaIso(d));
                if (c.Id == ana.Id) remoto = true;
                var porApp = remoto || rnd.Next(4) == 0;
                double? lat = porApp ? (c.Id == ana.Id ? 25.68662 : mty.Latitud + (rnd.NextDouble() - 0.5) * 0.002) : null;
                double? lng = porApp ? (c.Id == ana.Id ? -100.31608 : mty.Longitud + (rnd.NextDouble() - 0.5) * 0.002) : null;
                var e = new RegistroAsistencia
                {
                    ColaboradorId = c.Id, SucursalId = c.SucursalId, Tipo = TipoRegistro.Entrada, FechaLocal = d, HoraLocal = horaE,
                    FechaHora = Zona.AUtc(dtE, Tz), Origen = porApp ? OrigenRegistro.App : OrigenRegistro.Terminal,
                    DispositivoId = porApp ? null : c.DispositivoId, Latitud = lat, Longitud = lng, Precision = porApp ? rnd.Next(5, 30) : null,
                    DentroGeocerca = porApp ? true : null, DistanciaMetros = porApp ? rnd.Next(5, 90) : null, LugarAutorizado = porApp ? (remoto ? "Home office autorizado" : mty.Nombre) : null,
                    Estado = Reglas.EstadoEntrada(horaE, t.HoraEntrada, t.ToleranciaMinutos), Modalidad = remoto ? Modalidad.HomeOffice : Modalidad.Presencial
                };
                regs.Add(e);
                if (e.Estado == EstadoRegistro.Retardo)
                    incs.Add(new IncidenciaAsistencia { ColaboradorId = c.Id, SucursalId = c.SucursalId, Fecha = d, Tipo = TipoIncidencia.Retardo, Detalle = $"Entrada a las {horaE:HH\\:mm}; horario {t.HoraEntrada:HH\\:mm} con {t.ToleranciaMinutos} min de tolerancia.", Estado = d >= hoy.AddDays(-4) ? EstadoIncidencia.PorRevisar : EstadoIncidencia.Justificada });
                var horaS = t.HoraSalida.AddMinutes(rnd.Next(0, 25));
                var dtS = d.ToDateTime(horaS);
                if (!esHoy && rnd.NextDouble() < 0.01)
                {
                    incs.Add(new IncidenciaAsistencia { ColaboradorId = c.Id, SucursalId = c.SucursalId, Fecha = d, Tipo = TipoIncidencia.SalidaOmitida, Detalle = "Hay entrada pero no se registró la salida.", Estado = EstadoIncidencia.Confirmada });
                    continue;
                }
                if (dtS > ahoraLocal) continue;
                regs.Add(new RegistroAsistencia
                {
                    ColaboradorId = c.Id, SucursalId = c.SucursalId, Tipo = TipoRegistro.Salida, FechaLocal = d, HoraLocal = horaS, FechaHora = Zona.AUtc(dtS, Tz),
                    Origen = e.Origen, DispositivoId = e.DispositivoId, Latitud = e.Latitud, Longitud = e.Longitud, Precision = e.Precision, DentroGeocerca = e.DentroGeocerca,
                    DistanciaMetros = e.DistanciaMetros, LugarAutorizado = e.LugarAutorizado, Estado = EstadoRegistro.SalidaRegistrada, Modalidad = e.Modalidad
                });
            }
        }
        // Un registro fuera de área por revisar
        var fuera = regs.LastOrDefault(r => r.Origen == OrigenRegistro.App && r.FechaLocal == hoy && r.ColaboradorId != ana.Id);
        if (fuera is not null)
        {
            fuera.DentroGeocerca = false; fuera.DistanciaMetros = 640; fuera.Justificacion = "Atendí a un cliente en su domicilio antes de llegar a la sucursal.";
            incs.Add(new IncidenciaAsistencia { ColaboradorId = fuera.ColaboradorId, SucursalId = fuera.SucursalId, Fecha = hoy, Tipo = TipoIncidencia.FueraDeArea, Detalle = $"Entrada a 640 m del área autorizada. Justificación: {fuera.Justificacion}" });
        }
        db.Registros.AddRange(regs);
        db.Incidencias.AddRange(incs);
        await db.SaveChangesAsync();
        // Marca días previos como cerrados para que la tarea no duplique faltas
        for (var d = hoy.AddDays(-45); d < hoy; d = d.AddDays(1))
            db.EjecucionesTarea.Add(new EjecucionTarea { Tarea = $"cierre_dia:{d:yyyy-MM-dd}", Fecha = d, Inicio = reloj.Ahora, Fin = reloj.Ahora, Resultado = "Datos de demostración" });
        await db.SaveChangesAsync();
    }

    private DateOnly SiguienteLunes(int semanas)
    {
        var d = hoy.AddDays(1);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d.AddDays(7 * semanas);
    }

    private async Task SolicitudesDemo(List<Colaborador> todos, List<Colaborador> named, Colaborador ana, Colaborador sofia, Colaborador jorge, Colaborador diego,
        Colaborador andrea, Colaborador mariana, Colaborador carlosM, Usuario uMariana, Usuario uCarlos, Usuario uAna, Usuario uDaniel, Equipo eqNorte, Colaborador ricardo)
    {
        var reglas = await db.Reglas.ToListAsync();
        ReglaAprobacion R(string c) => reglas.First(r => r.Clave == c);
        var permisoPersonal = await db.TiposPermiso.FirstAsync(t => t.Nombre == "Permiso personal");
        var turnos = await db.Turnos.ToDictionaryAsync(t => t.Id);
        var festivos = await db.DiasFestivos.Select(f => f.Fecha).ToListAsync();
        int folio = 1;

        async Task<Solicitud> S(Colaborador c, TipoSolicitud tipo, DateOnly ini, DateOnly fin, EstadoSolicitud estado, int pasoActual, Colaborador? cobertura = null,
            string? comentario = null, string? nota = null, Colaborador? decidio1 = null, Usuario? decidio1U = null, Usuario? decidio2U = null, int? tipoPermiso = null, string? motivoRechazo = null)
        {
            var conReportes = todos.Any(x => x.SupervisorId == c.Id);
            var regla = tipo == TipoSolicitud.Permiso ? (conReportes ? R("solicitud_supervisor") : R("permisos")) : (conReportes ? R("solicitud_supervisor") : R("vacaciones"));
            var titular1 = conReportes ? todos.First(x => x.Id == c.SupervisorId) : todos.First(x => x.Id == c.SupervisorId);
            var dias = Reglas.DiasHabiles(ini, fin, turnos[c.TurnoId!.Value].DiasDescanso, festivos);
            var s = new Solicitud
            {
                Folio = $"{(tipo == TipoSolicitud.Vacaciones ? "VAC" : "PER")}-{folio++:D5}", Tipo = tipo, ColaboradorId = c.Id, SucursalId = c.SucursalId, FechaInicio = ini, FechaFin = fin,
                DiasHabiles = dias, CoberturaColaboradorId = cobertura?.Id, CoberturaNota = nota, Comentario = comentario, Estado = estado, PasoActual = pasoActual,
                ReglaAprobacionId = regla.Id, ReglaNombre = regla.Nombre, CapturadaPorUsuarioId = uMariana.Id, TipoPermisoId = tipoPermiso, MotivoRechazo = motivoRechazo,
                CreadoEn = reloj.Ahora.AddDays(-rnd.Next(1, 6))
            };
            var p1 = new PasoAprobacion { Orden = 1, TipoAprobador = conReportes ? TipoAprobador.Gerente : TipoAprobador.SupervisorDirecto, TitularColaboradorId = titular1.Id };
            var p2 = new PasoAprobacion { Orden = 2, TipoAprobador = TipoAprobador.RecursosHumanos };
            if (pasoActual >= 2 || estado != EstadoSolicitud.Pendiente)
            {
                p1.Estado = EstadoPaso.Aprobado; p1.DecididoPorUsuarioId = decidio1U?.Id; p1.DecididoPorNombre = decidio1U?.Nombre ?? titular1.NombreCompleto; p1.Fecha = s.CreadoEn.AddHours(5);
                if (decidio1 is not null && decidio1.Id != titular1.Id) { p1.EnNombreDeColaboradorId = titular1.Id; p1.EnNombreDeNombre = titular1.NombreCompleto; }
            }
            else p1.Estado = EstadoPaso.Pendiente;
            if (estado == EstadoSolicitud.Aprobada) { p2.Estado = EstadoPaso.Aprobado; p2.DecididoPorUsuarioId = decidio2U?.Id ?? uMariana.Id; p2.DecididoPorNombre = decidio2U?.Nombre ?? uMariana.Nombre; p2.Fecha = s.CreadoEn.AddDays(1); s.ResueltaEn = p2.Fecha; }
            else if (estado == EstadoSolicitud.Rechazada) { p2.Estado = EstadoPaso.Rechazado; p2.DecididoPorUsuarioId = uMariana.Id; p2.DecididoPorNombre = uMariana.Nombre; p2.Motivo = motivoRechazo; p2.Fecha = s.CreadoEn.AddDays(1); s.ResueltaEn = p2.Fecha; }
            else p2.Estado = pasoActual == 2 ? EstadoPaso.Pendiente : EstadoPaso.EnEspera;
            s.Pasos = [p1, p2];
            db.Solicitudes.Add(s);
            await db.SaveChangesAsync();
            if (tipo == TipoSolicitud.Vacaciones && estado is EstadoSolicitud.Pendiente or EstadoSolicitud.Aprobada)
            {
                var saldo = await db.Saldos.Where(x => x.ColaboradorId == c.Id).OrderByDescending(x => x.Periodo).FirstOrDefaultAsync();
                if (saldo is not null)
                {
                    if (estado == EstadoSolicitud.Pendiente) saldo.DiasComprometidos += dias; else saldo.DiasUsados += dias;
                    db.MovimientosSaldo.Add(new MovimientoSaldo { SaldoVacacionesId = saldo.Id, ColaboradorId = c.Id, Tipo = TipoMovimientoSaldo.Compromiso, Dias = dias, SolicitudId = s.Id, Fecha = s.CreadoEn, Motivo = $"Solicitud {s.Folio}" });
                    if (estado == EstadoSolicitud.Aprobada)
                        db.MovimientosSaldo.Add(new MovimientoSaldo { SaldoVacacionesId = saldo.Id, ColaboradorId = c.Id, Tipo = TipoMovimientoSaldo.Consumo, Dias = dias, SolicitudId = s.Id, Fecha = s.ResueltaEn ?? s.CreadoEn, Motivo = $"Solicitud {s.Folio}" });
                    await db.SaveChangesAsync();
                }
            }
            return s;
        }
        var lun1 = SiguienteLunes(0); var lun2 = SiguienteLunes(1); var lun3 = SiguienteLunes(2); var lun4 = SiguienteLunes(3);
        var dTorres = named[0]; var mSanchez = named[1]; var cRamirez = named[2]; var vTorres = named[7]; var jSanchez = named[8]; var dNunez = named[9]; var eGarza = named[10];
        var dLopez = named[11]; var mCruz = named[12]; var lMendoza = named[13]; var pRuiz = named[14];

        // Pendientes en el paso de RH (dashboard y portal de vacaciones)
        await S(dTorres, TipoSolicitud.Vacaciones, lun2, lun2.AddDays(4), EstadoSolicitud.Pendiente, 2, pRuiz, "Viaje familiar", null, andrea);
        await S(mSanchez, TipoSolicitud.Vacaciones, lun3, lun3.AddDays(4), EstadoSolicitud.Pendiente, 2, named[4], null, null, andrea);
        await S(andrea, TipoSolicitud.Permiso, lun1.AddDays(2), lun1.AddDays(2), EstadoSolicitud.Pendiente, 2, null, "Trámite personal", null, mariana, uMariana, tipoPermiso: permisoPersonal.Id);
        await S(cRamirez, TipoSolicitud.Vacaciones, lun2, lun2.AddDays(1), EstadoSolicitud.Pendiente, 2, mCruz, null, null, ricardo);
        await S(vTorres, TipoSolicitud.Vacaciones, lun4, lun4.AddDays(4), EstadoSolicitud.Pendiente, 2, lMendoza, null, null, ricardo);
        await S(jSanchez, TipoSolicitud.Vacaciones, lun1, lun1.AddDays(4), EstadoSolicitud.Pendiente, 2, pRuiz, null, null, andrea);
        // Pendientes con el supervisor (bandeja de Ana Lucía)
        await S(sofia, TipoSolicitud.Vacaciones, lun3, lun3.AddDays(4), EstadoSolicitud.Pendiente, 1, diego, "Viaje familiar", "Entregar pendientes a Diego");
        await S(jorge, TipoSolicitud.Permiso, lun1.AddDays(3), lun1.AddDays(3), EstadoSolicitud.Pendiente, 1, null, "Cita en el banco", tipoPermiso: permisoPersonal.Id);
        // Solicitud de Ana Lucía (supervisora): Gerente (delegado a Mariana) → RH
        await S(ana, TipoSolicitud.Vacaciones, lun2, lun2.AddDays(4), EstadoSolicitud.Pendiente, 1, ricardo, "Viaje familiar", "Ricardo cubre la supervisión del Equipo Norte");
        // Aprobadas pasadas y en curso (calendario y "Personas de vacaciones hoy")
        await S(ana, TipoSolicitud.Vacaciones, hoy.AddDays(-160), hoy.AddDays(-158), EstadoSolicitud.Aprobada, 2, ricardo, null, null, carlosM, uCarlos, uDaniel);
        var enCurso = todos.Where(c => c.EquipoId == eqNorte.Id && c.SupervisorId == ricardo.Id && c.Id != cRamirez.Id && c.Id != vTorres.Id).Take(3).ToList();
        foreach (var c in enCurso) await S(c, TipoSolicitud.Vacaciones, hoy.AddDays(-1), hoy.AddDays(3), EstadoSolicitud.Aprobada, 2, null, null, null, ricardo);
        foreach (var c in todos.Where(c => c.SupervisorId == andrea.Id).Skip(6).Take(2)) await S(c, TipoSolicitud.Vacaciones, hoy, hoy.AddDays(4), EstadoSolicitud.Aprobada, 2, null, null, null, andrea);
        foreach (var c in todos.Where(c => c.SupervisorId != null && c.SucursalId == ana.SucursalId).Skip(30).Take(8))
            await S(c, TipoSolicitud.Vacaciones, hoy.AddDays(rnd.Next(-60, -10)), hoy.AddDays(rnd.Next(-9, -5)), EstadoSolicitud.Aprobada, 2);
        foreach (var c in todos.Where(c => c.SupervisorId != null && c.SucursalId == ana.SucursalId).Skip(50).Take(6))
        { var i = hoy.AddDays(rnd.Next(8, 25)); await S(c, TipoSolicitud.Vacaciones, i, i.AddDays(rnd.Next(1, 5)), EstadoSolicitud.Aprobada, 2); }
        await S(dLopez, TipoSolicitud.Permiso, hoy.AddDays(2), hoy.AddDays(2), EstadoSolicitud.Aprobada, 2, null, "Trámite", tipoPermiso: permisoPersonal.Id);
        await S(eGarza, TipoSolicitud.Vacaciones, hoy.AddDays(-30), hoy.AddDays(-28), EstadoSolicitud.Rechazada, 2, motivoRechazo: "Cierre de mes en TI");
        await S(dNunez, TipoSolicitud.Vacaciones, lun4, lun4.AddDays(4), EstadoSolicitud.Aprobada, 2, ana);
        // Incapacidades
        db.Incapacidades.Add(new Incapacidad { ColaboradorId = todos.First(c => c.EquipoId == eqNorte.Id && c.SupervisorId == ricardo.Id && !enCurso.Contains(c) && c.Id != cRamirez.Id && c.Id != vTorres.Id).Id, FechaInicio = hoy.AddDays(-2), FechaFin = hoy.AddDays(5), Folio = "IMSS-AB12345", Tipo = "Enfermedad general", Nota = "Incapacidad por enfermedad general" });
        db.Incapacidades.Add(new Incapacidad { ColaboradorId = todos.Where(c => c.SupervisorId == andrea.Id).Skip(10).First().Id, FechaInicio = hoy.AddDays(3), FechaFin = hoy.AddDays(9), Folio = "IMSS-CD67890", Tipo = "Riesgo de trabajo" });
        await db.SaveChangesAsync();
    }

    private async Task VisitantesDemo(Sucursal mty, Colaborador ana, Colaborador mariana, Colaborador recep, Dispositivo lector)
    {
        var secreto = Enlaces.Secreto;
        var antes = 30; var despues = 120;
        (string n, string m, int minutos, bool salio)[] lista =
        [
            ("Ricardo Alanís", "Proveedor de insumos", -300, false), ("Patricia Vega", "Auditoría", -245, false), ("Diego Morales", "Mantenimiento", -195, false),
            ("Laura Jiménez", "Entrevista de trabajo", -150, false), ("Fernando Cruz", "Cliente", -110, false), ("Gabriela Ortiz", "Proveedor de uniformes", -80, false),
            ("Héctor Leal", "Auditoría", -60, false), ("Mónica Rivera", "Cliente", -35, false), ("Raúl Domínguez", "Mantenimiento", -400, true),
            ("Silvia Medina", "Entrevista de trabajo", 45, false), ("Óscar Aguilar", "Cliente", 90, false), ("Karla Romero", "Reunión de trabajo", 150, false),
        ];
        var anfitriones = new[] { ana, mariana, recep };
        var k = 0;
        foreach (var (n, m, minutos, salio) in lista)
        {
            var llegadaLocal = ahoraLocal.AddMinutes(minutos);
            llegadaLocal = new DateTime(llegadaLocal.Year, llegadaLocal.Month, llegadaLocal.Day, llegadaLocal.Hour, llegadaLocal.Minute / 5 * 5, 0);
            if (DateOnly.FromDateTime(llegadaLocal) != hoy) llegadaLocal = hoy.ToDateTime(new TimeOnly(minutos < 0 ? 0 : 23, minutos < 0 ? 5 : 30));
            var utc = Zona.AUtc(llegadaLocal, Tz);
            var (desde, hasta) = Reglas.VigenciaQr(utc, antes, despues);
            var inv = new InvitacionVisitante
            {
                Nombre = n, EmpresaMotivo = m, FechaLlegada = utc, FechaLocal = hoy, AnfitrionColaboradorId = anfitriones[k++ % 3].Id, SucursalId = mty.Id,
                Codigo = Hash.Aleatorio(12), VigenciaDesde = desde, VigenciaHasta = hasta, Telefono = $"81{rnd.Next(10000000, 99999999)}",
                Estado = minutos < 0 ? (salio ? EstadoInvitacion.Salio : EstadoInvitacion.Dentro) : EstadoInvitacion.Esperado
            };
            if (minutos < 0) inv.Acceso = new AccesoVisitante { Entrada = utc.AddMinutes(rnd.Next(0, 6)), Salida = salio ? utc.AddMinutes(90) : null, DispositivoId = lector.Id };
            db.Invitaciones.Add(inv);
        }
        // Historial de días previos
        for (var d = 1; d <= 20; d++)
        {
            var fecha = hoy.AddDays(-d);
            if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            for (var j = 0; j < rnd.Next(3, 9); j++)
            {
                var local = fecha.ToDateTime(new TimeOnly(rnd.Next(8, 17), rnd.Next(0, 12) * 5));
                var utc = Zona.AUtc(local, Tz);
                var f = rnd.Next(2) == 0;
                var vino = rnd.Next(10) > 0;
                db.Invitaciones.Add(new InvitacionVisitante
                {
                    Nombre = $"{(f ? NombresF[rnd.Next(NombresF.Length)] : NombresM[rnd.Next(NombresM.Length)])} {Apellidos[rnd.Next(Apellidos.Length)]}",
                    EmpresaMotivo = new[] { "Proveedor de insumos", "Auditoría", "Mantenimiento", "Entrevista de trabajo", "Cliente" }[rnd.Next(5)],
                    FechaLlegada = utc, FechaLocal = fecha, AnfitrionColaboradorId = anfitriones[rnd.Next(3)].Id, SucursalId = mty.Id, Codigo = Hash.Aleatorio(12),
                    VigenciaDesde = utc.AddMinutes(-antes), VigenciaHasta = utc.AddMinutes(despues), Estado = vino ? EstadoInvitacion.Salio : EstadoInvitacion.Vencida,
                    Acceso = vino ? new AccesoVisitante { Entrada = utc.AddMinutes(rnd.Next(0, 10)), Salida = utc.AddMinutes(rnd.Next(30, 180)), DispositivoId = lector.Id } : null
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private async Task DocumentosDemo(List<Colaborador> todos, Colaborador ana, List<Colaborador> named, Colaborador jorge)
    {
        var tipos = await db.TiposDocumento.OrderBy(t => t.Id).ToListAsync();
        var pdf = PdfDemo();
        foreach (var c in todos)
        {
            var faltan = c.Id == ana.Id ? 1 : rnd.Next(0, 4);
            var lista = tipos.Take(tipos.Count - faltan).ToList();
            if (c.Id == ana.Id) lista = tipos.Where(t => t.Nombre != "Comprobante de estudios").ToList();
            foreach (var t in lista)
            {
                DateOnly? venc = t.ConVigencia ? hoy.AddDays(rnd.Next(60, 900)) : null;
                if (c.Id == ana.Id && t.Nombre == "INE") venc = hoy.AddDays(5);
                if (c.Id == ana.Id && t.Nombre == "Examen médico") venc = hoy.AddDays(12);
                if (c.Id == named[2].Id && t.Nombre == "INE") venc = hoy.AddDays(5);
                if (c.Id == jorge.Id && t.Nombre == "Constancia de capacitación") venc = hoy.AddDays(18);
                if (t.ConVigencia && rnd.Next(60) == 0) venc = hoy.AddDays(rnd.Next(-20, 30));
                var rel = Path.Combine("expedientes", "demo", $"{c.Id}-{t.Id}.pdf");
                var full = Path.Combine(almacen.Raiz, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                await File.WriteAllBytesAsync(full, pdf);
                var a = new Archivo { NombreOriginal = $"{t.Nombre} - {c.Numero}.pdf", ContentType = "application/pdf", Tamano = pdf.Length, Ruta = rel, Categoria = "expedientes", ColaboradorId = c.Id, CreadoEn = reloj.Ahora.AddDays(-rnd.Next(1, 300)) };
                db.Documentos.Add(new Documento { ColaboradorId = c.Id, TipoDocumentoId = t.Id, Archivo = a, FechaVencimiento = venc, CreadoEn = a.CreadoEn });
            }
        }
        await db.SaveChangesAsync();
    }

    private static byte[] PdfDemo()
    {
        const string contenido = "BT /F1 18 Tf 72 720 Td (Documento de demostracion - WOW! Time) Tj ET";
        var objs = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {contenido.Length} >>\nstream\n{contenido}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var sb = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objs.Length; i++) { offsets.Add(sb.Length); sb.Append($"{i + 1} 0 obj\n{objs[i]}\nendobj\n"); }
        var xref = sb.Length;
        sb.Append($"xref\n0 {objs.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objs.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
    }

    private async Task FichaDemo(Colaborador ana, Usuario uMariana, Usuario uDaniel)
    {
        DateTime L(DateOnly d, int h, int m) => Zona.AUtc(d.ToDateTime(new TimeOnly(h, m)), Tz);
        var ahoraUtc = reloj.Ahora;
        var docUtc = L(hoy, 10, 24) < ahoraUtc ? L(hoy, 10, 24) : ahoraUtc.AddMinutes(-20);
        db.Notas.Add(new Nota { ColaboradorId = ana.Id, AutorUsuarioId = uMariana.Id, Texto = "Excelente desempeño en la supervisión del equipo. Destaca por su puntualidad y liderazgo.", CreadoEn = L(hoy.AddDays(-94), 10, 12) });
        db.Bitacora.AddRange(
            new EventoBitacora { ColaboradorId = ana.Id, Tipo = "documento", Titulo = "Se actualizó el documento", Detalle = "Comprobante de domicilio", Fecha = docUtc, AutorUsuarioId = uMariana.Id, AutorNombre = uMariana.Nombre },
            new EventoBitacora { ColaboradorId = ana.Id, Tipo = "datos", Titulo = "Se modificaron datos laborales", Detalle = "Turno matutino", Fecha = L(hoy.AddDays(-1), 16, 31), AutorUsuarioId = uDaniel.Id, AutorNombre = uDaniel.Nombre },
            new EventoBitacora { ColaboradorId = ana.Id, Tipo = "documento", Titulo = "Se agregó un documento", Detalle = "Constancia de situación fiscal", Fecha = L(hoy.AddDays(-75), 11, 15), AutorUsuarioId = uMariana.Id, AutorNombre = uMariana.Nombre },
            new EventoBitacora { ColaboradorId = ana.Id, Tipo = "solicitud", Titulo = "Solicitud de vacaciones aprobada", Detalle = ServicioSolicitudes.TextoPeriodo(hoy.AddDays(-160), hoy.AddDays(-158)), Fecha = L(hoy.AddDays(-165), 9, 20), AutorUsuarioId = uDaniel.Id, AutorNombre = uDaniel.Nombre },
            new EventoBitacora { ColaboradorId = ana.Id, Tipo = "asignacion", Titulo = "Asignación temporal registrada", Detalle = "Home office · Remodelación del área de supervisión", Fecha = L(hoy.AddDays(-3), 12, 40), AutorUsuarioId = uMariana.Id, AutorNombre = uMariana.Nombre });
        var regAna = await db.Registros.Where(r => r.ColaboradorId == ana.Id && r.FechaLocal == hoy && r.Tipo == TipoRegistro.Entrada).FirstOrDefaultAsync();
        if (regAna is not null)
            db.Bitacora.Add(new EventoBitacora { ColaboradorId = ana.Id, Tipo = "asistencia", Titulo = "Registro de entrada", Detalle = "Acceso principal", Fecha = regAna.FechaHora, Entidad = "RegistroAsistencia", EntidadId = (int)regAna.Id });
        await db.SaveChangesAsync();
    }

    private async Task NotificacionesDemo(Usuario uMariana, Usuario uAna)
    {
        var n = sp.GetRequiredService<Notificador>();
        await n.Enviar(uMariana.Id, "solicitud_por_aprobar", "Tienes solicitudes por aprobar", "6 solicitudes esperan la decisión de Recursos Humanos.", "/vacaciones", "demo:1");
        await n.Enviar(uMariana.Id, "documento_por_vencer", "INE de Ana Lucía Martínez vence en 5 días", null, "/expedientes", "demo:2");
        await n.Enviar(uMariana.Id, "cobertura", "Cobertura baja en Equipo Norte", "3 personas ausentes esta semana.", "/vacaciones?tab=calendario", "demo:3");
        await n.Enviar(uAna.Id, "solicitud_por_aprobar", "Solicitud por aprobar: Sofía Herrera", "Vacaciones", "/aprobaciones", "demo:4");
        await db.SaveChangesAsync();
    }
}
