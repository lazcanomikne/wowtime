# Origen SQL Server

Sistema actual: **InovaOS** (portal de Inovatech). Revisado el 17 de noviembre de 2026 con consultas de solo lectura sobre producción.

Los datos viven en **dos bases del mismo servidor**:

- **`InovatechWAP`** (base del portal): colaboradores, organigrama, vacaciones, permisos, reglas de asistencia y justificaciones.
- **`wotime_inova`** (base del servicio del lector): checadas. Un servicio recibe por *push* los eventos del lector Hikvision y los guarda aquí.

## Conexión (sin contraseña)

| Dato | Valor |
| --- | --- |
| Servidor/instancia | `192.168.15.128`, instancia predeterminada (nombre del equipo `WIN-8ORFOTT1I5B`) |
| Puerto | 1433 |
| Bases | `InovatechWAP` (RH) y `wotime_inova` (checadas) |
| Usuario | `wowtime_lectura`, creado el 2026-11-17 con el script del Anexo A. Su contraseña está solo en `.env` |
| Acceso | Usuario de SQL Server (el servidor acepta autenticación mixta) |
| Cifrado | El servidor no lo exige. Con `Microsoft.Data.SqlClient` usar `Encrypt=True;TrustServerCertificate=True`, porque el certificado es autofirmado |
| Versión | Microsoft SQL Server 2019 (RTM) 15.0.2000.5, Standard Edition 64 bits, sobre Windows Server 2022 |
| Intercalación | `InovatechWAP` usa `SQL_Latin1_General_CP850_CI_AS` y `wotime_inova` usa `SQL_Latin1_General_CP1_CI_AS`. Un cruce de texto entre las dos necesita `COLLATE DATABASE_DEFAULT` |

El servidor de WOW! Time (`192.168.155.21`) ya llega a `192.168.15.128:1433`. El portal actual se conecta por esa ruta.

Cadenas para `backend/WowTime.Api/.env`, con la contraseña solo ahí:

```
ConnectionStrings__Origen=Server=192.168.15.128,1433;Database=InovatechWAP;User Id=wowtime_lectura;Password=<contraseña>;Encrypt=True;TrustServerCertificate=True;Application Name=WOW! Time
ConnectionStrings__OrigenChecadas=Server=192.168.15.128,1433;Database=wotime_inova;User Id=wowtime_lectura;Password=<contraseña>;Encrypt=True;TrustServerCertificate=True;Application Name=WOW! Time
```

No conviene dar `db_datareader` sobre `InovatechWAP`. Esa base también guarda tokens de Microsoft 365 y contraseñas cifradas de SAP en `Users`, además de cobranza, CRM y KPI's. El Anexo A da acceso solo a un esquema `wowtime` con vistas que exponen únicamente las columnas de este documento.

## Decisiones

Las marcadas **(propuesta)** aplican mientras no se diga otra cosa. Las marcadas **(hecho)** describen cómo opera hoy el sistema actual.

1. **Sistema que manda (DECIDIDO por el usuario el 2026-11-17): WOW! Time.** WOW! Time gestiona todo: colaboradores, organigrama, asistencia, vacaciones y permisos. InovaOS pasa a ser un canal más para pedir y aprobar, como la app móvil. Ver la sección "Pedir desde ambos lados".
2. **Frecuencia (propuesta):** cada 15 minutos para las checadas mientras el lector siga enviando a `wotime_inova`. Para RH, una carga inicial y después lecturas cada hora, solo durante la transición, hasta el corte.
3. **Vacaciones se piden en (DECIDIDO por el usuario el 2026-11-17): en ambos lados.** Se pueden pedir en WOW! Time (web y app) y en InovaOS (portal y la app InovaWay, que usa el mismo backend). Los saldos, la cadena de aprobación y los descuentos los lleva **solo** WOW! Time.
4. **Checadas se registran en (hecho):** un lector facial Hikvision DS-K1T673DWX en la IP `192.168.15.53` las envía a `wotime_inova`. Llegan casi al momento. WOW! Time las importa y aplica sus reglas.
5. **Historial a importar (propuesta):** todo. Es poco: las checadas empiezan en septiembre de 2026 y las solicitudes en diciembre de 2023. El cálculo de asistencia del sistema actual arranca el **2026-10-29**; antes de esa fecha las checadas son de prueba e instalación.

## Pedir desde ambos lados (decisión del 2026-11-17)

> El contrato de la API, la migración y el lado de InovaOS (ya hecho y apagado) están en **`docs/INTEGRACION_INOVAOS.md`**.

**Principio: una sola fuente de verdad.** No se sincronizan dos bases en ambos sentidos. Si las dos guardaran solicitudes y saldos, una misma solicitud podría descontarse dos veces, o aprobarse en un lado y rechazarse en el otro. Por eso:

- **WOW! Time guarda** solicitudes, saldos, aprobaciones, incidencias y checadas.
- **InovaOS deja de escribir** en `TimeOffRequests`, `TimeOffApprovals` y `VacationPeriods` a partir del corte. Sus pantallas de vacaciones (portal e InovaWay) **llaman a la API de WOW! Time** desde el backend de InovaOS, nunca desde el navegador.
- **El usuario `wowtime_lectura` sigue siendo de solo lectura.** WOW! Time no necesita escribir en InovaOS: lo usa para la migración y para leer durante la transición.

### Pasos

1. **Referencia al origen.** Agregar en `Colaborador` un campo con el `EmployeeID` de InovaOS (por ejemplo `OrigenId`, único), y en `Solicitud` uno con el `RequestID`. Así la migración se puede repetir sin duplicar. Para identificar a una persona que viene de InovaOS, se usa el correo (`Employees.Email`, que todos tienen) o ese `OrigenId`.
2. **Migración inicial**, idempotente y repetible hasta el corte:
   - Colaboradores con su jefe.
   - Periodos con `DiasDerecho`, respetando los ajustes manuales y sus `Notas`.
   - Solicitudes con sus aprobaciones: las históricas como movimientos ya consumidos.
   - Justificaciones y horarios.
   - Lo que no existe en el origen (sucursal, puestos y áreas como catálogo, fecha de baja) se completa en WOW! Time.
3. **API para InovaOS.** Una llave de API para el backend de InovaOS que pueda actuar **en nombre de** un colaborador identificado por correo u `OrigenId`, con estas operaciones:
   - Ver saldo y movimientos.
   - Simular, crear y cancelar solicitudes.
   - Ver la bandeja de aprobaciones de un jefe, y aprobar o rechazar.

   Hoy `POST /solicitudes` exige un usuario con sesión (`Yo.UsuarioIdRequerido`), así que una llave sola no basta: hace falta ese permiso de "actuar en nombre de". La bitácora debe registrar que llegó por InovaOS.
4. **Corte.** Se fija un día:
   - InovaOS cambia sus pantallas a la API.
   - Se hace la última corrida de migración.
   - Las tablas de InovaOS quedan como histórico de solo lectura.
   - Desde ese día, los avisos por correo los manda solo WOW! Time; InovaOS deja de mandar los suyos para que no lleguen dos.
5. **Asistencia.** El módulo de Asistencia de InovaOS (que lee `TimeOffRequests` para perdonar retardos) también pasa a leer de WOW! Time, o se retira si WOW! Time lo reemplaza. Más adelante el lector puede enviar directo a WOW! Time (`/api/v1/terminal/registros`) en lugar de a `wotime_inova`.

Lado de InovaOS que cambia en el corte: `controllers/gestionVacacionesController.js`, `controllers/rhController.js`, `utils/aprobaciones.js`, `utils/vacacionesNotif.js`, `services/asistencia/datos.js` y la página `MisVacacionesPage.vue` de InovaWay. Lo hace la sesión de InovaOS cuando la API de WOW! Time esté lista.

## Mapeo

### Colaboradores — tabla `InovatechWAP.dbo.Employees` + `dbo.Users`

| Campo WOW! Time | Columna origen | Notas |
| --- | --- | --- |
| Identificador único* | `Employees.EmployeeID` (int, identidad) | No cambia nunca. Es la llave para todo lo demás |
| Número de empleado* | `Employees.NumeroEmpleadoLector` (nvarchar 32) | **No hay número de nómina.** Este es el número del colaborador en el lector. **16 de 31 no lo tienen** (personal que no checa). Propuesta: si falta, generar uno en WOW! Time y guardar `EmployeeID` como referencia |
| Nombre(s) | `Employees.FirstName` | |
| Apellidos | `Employees.LastName` | Paterno y materno juntos |
| Correo | `Employees.Email` (obligatorio) | Dominios `inovatech.com.mx` y uno externo |
| Teléfono | — | No existe |
| Fecha de nacimiento | — | No existe |
| Fecha de ingreso* | `Employees.EntryDate` (date, obligatorio) | Con ella se calculan antigüedad y vacaciones |
| Puesto* | `Employees.JobTitle` | Texto libre, sin catálogo. 7 de 31 vacíos |
| Área* | `Employees.Department` | Texto libre, sin catálogo. **20 de 31 vacíos** |
| Sucursal* | — | No existe: hay una sola ubicación y un solo lector. Propuesta: sucursal única "Monterrey" |
| Jefe directo* | `Employees.ManagerID` → `Employees.EmployeeID` | Con llave foránea. 8 de 31 sin jefe; uno es Dirección General (la raíz) |
| Estatus* | `Users.IsActive`, por `Employees.UserID` → `Users.UserID` | 27 activos y 4 inactivos. Todos los colaboradores tienen usuario |
| Fecha de baja* | — | **No se guarda.** La baja solo apaga `Users.IsActive`. Propuesta: WOW! Time anota como fecha de baja el día que detecte el cambio |
| Turno u horario | `AsistConfig` (general) + `AsistHorarioPersona` (excepciones) | Ver Catálogos |
| RFC, CURP, NSS | — | No existen |
| Empresa (extra) | — | No hay columna. `UserEmpresas` dice a qué empresas de SAP tiene acceso el usuario, no dónde trabaja: no usarla como sucursal |

### Catálogos

| Catálogo | Origen | Notas |
| --- | --- | --- |
| Sucursales | — | No hay. Una sola, con el lector `Dispositivos` de `wotime_inova` (nombre, modelo e IP). No hay latitud ni longitud |
| Áreas | `DISTINCT Employees.Department` | Hoy: Contabilidad, Desarrollo, Ingeniería, Operaciones y Ventas |
| Puestos | `DISTINCT Employees.JobTitle` | 14 valores, texto libre |
| Turno general | `AsistConfig` (una fila, `ConfigID = 1`) | `HoraEntrada`, `HoraSalida` y `DiasLaborales` (7 caracteres de lunes a domingo; `1111100` = lunes a viernes) |
| Horario por persona | `AsistHorarioPersona` | `EmployeeID`, `HoraEntrada`, `HoraSalida`, `DiasLaborales`, `ToleranciaMin` (si es nulo, usa la general), `VigenteDesde` y `VigenteHasta` (si es nulo, sigue vigente). Hoy hay 1 |
| Días festivos | `ProyFestivo` (`Company = 'SBOINOVA'`) | La tabla existe pero **está vacía** |
| Tipos de ausencia | Fijos en el código (ver Vacaciones) | No hay tabla de catálogo |

### Organigrama

Vive en `Employees.ManagerID`. No hay otra tabla.

Las aprobaciones de vacaciones suben por esa cadena: nivel 1 es el jefe directo, nivel 2 el jefe del jefe, y así hasta la raíz. Si alguien no tiene jefe, aprueba una sola persona de Dirección: un usuario activo con rol administrador, prefiriendo a uno que tenga ficha de empleado. Si no hay a quién pedirle, la solicitud se aprueba sola.

### Asistencia — `wotime_inova.dbo.Eventos` + `CatalogoEventos`

| Campo WOW! Time | Columna origen | Notas |
| --- | --- | --- |
| Llave de la checada | `Eventos.EventoId` (bigint) | También es única la pareja `DispositivoId` + `SerialNo` del lector |
| Colaborador* | `Eventos.NumeroEmpleado` | Se une con `Employees.NumeroEmpleadoLector`. Hay distinta intercalación (ver Conexión) |
| Fecha y hora* | `Eventos.FechaLocal` (datetime2) | **En hora de Monterrey.** `FechaEvento` (datetimeoffset) trae la misma hora en UTC y `Fecha` (date) el día local |
| Es checada | `CatalogoEventos.EsChecada = 1`, unido por `Major` + `Minor` | El lector también registra intentos fallidos y eventos de puerta. Cuentan como checada: tarjeta válida (5/1, 5/2), huella válida (5/38) y rostro reconocido (5/75) |
| Tipo (entrada/salida) | — | **El lector no lo distingue.** La vista `vw_ChecadasDia` lo deduce: la primera del día es Entrada, la última Salida y las de en medio Intermedia. Si una checada cae a menos de 2 minutos de la anterior, es Repetida y no cuenta |
| Origen | `Eventos.DispositivoId` → `Dispositivos`; `ModoVerificacion`; `Origen` (`push`) | Un solo lector |
| Foto | `Eventos.TieneFoto`; imagen en `EventoFotos` | **No incluida** en el acceso propuesto: es biométrico. Se agrega solo si se decide |
| Incidencias y justificaciones | `InovatechWAP.dbo.AsistJustificacion` | `EmployeeID`, `Fecha` y `Tipo` (`retardo`, `salida_anticipada`, `falta`, `sin_salida`, `comision` o `home_office`). También `HoraCorregida`, `Motivo`, `Estado` (`pendiente`, `aprobada` o `rechazada`), `Origen` (`empleado` o `rh`), y quién la pidió y quién la resolvió, con su fecha |

El cálculo diario (retardos, faltas y descuentos) **no se guarda**: InovaOS lo calcula al consultar. WOW! Time debe calcularlo con sus reglas a partir de las checadas, el horario, las justificaciones y las ausencias aprobadas.

### Vacaciones

**Saldos — `InovatechWAP.dbo.VacationPeriods`** (un renglón por colaborador y año de antigüedad)

| Campo | Columna | Notas |
| --- | --- | --- |
| Llave | `PeriodoID` | |
| Colaborador | `EmployeeID` | |
| Periodo | `NumeroPeriodo`, `FechaInicio`, `FechaFin` | Del aniversario al día anterior al siguiente. Hay periodos generados a futuro |
| Días otorgados | `DiasDerecho` (decimal) | RH puede ajustarlo a mano; el motivo queda en `Notas` |
| Días tomados | No hay columna | Es la suma de `TimeOffRequests.DeductionValue` con `Status = 'APPROVED'` y el mismo `PeriodoID`. InovaOS los separa en usados (ya iniciados), programados (a futuro) y canjeados (`CASH_OUT`) |
| Vencimiento | `FechaFin` | InovaOS muestra los "días para vencer" contra esta fecha |

**Solicitudes — `InovatechWAP.dbo.TimeOffRequests`**

| Campo | Columna | Notas |
| --- | --- | --- |
| Llave | `RequestID` | |
| Colaborador | `EmployeeID` | |
| Tipo | `RequestType` | Ver la tabla de tipos de abajo |
| Fechas | `StartDate`, `EndDate`, `ReturnDate` | `ReturnDate` casi siempre viene vacío |
| Días | `DaysQuantity` (días hábiles) y `DeductionValue` (lo que descuenta del saldo) | `SinDescuento = 1` indica que se registró sin descontar, con su `NotaAjuste` |
| Estatus | `Status` | `PENDING`, `APPROVED` o `REJECTED` |
| Periodo | `PeriodoID` → `VacationPeriods` | |
| Motivo | `Reason` | |
| Creada | `CreatedAt` | |
| Aprobaciones | `TimeOffApprovals`: `RequestID`, `Nivel`, `ApproverEmployeeID`, `ApproverUserID`, `ApproverName`, `Status`, `ActedAt`, `Comment` | Una fila por nivel. Las columnas viejas `BossApproved`/`DirectorApproved` (con sus fechas) siguen llenas en solicitudes anteriores |

**Tipos de ausencia** (fijos en el código de InovaOS; no hay tabla)

| Código | Significado | Descuenta |
| --- | --- | --- |
| `VACATION` | Vacaciones | Días hábiles del rango |
| `PERMIT_3H` | Permiso de 3 horas | 0.3 días |
| `PERMIT_5H` | Permiso de 5 horas | 0.5 días |
| `PERMIT_DAY` | Permiso de día completo | 1 día |
| `CASH_OUT` | Canje de días (se pagan en lugar de tomarse) | Los días indicados |

No hay incapacidades ni otros tipos de ausencia.

**Escala de días — `InovatechWAP.dbo.VacationTiers`:**

| Años cumplidos | Días |
| --- | --- |
| 1 | 12 |
| 2 | 14 |
| 3 | 16 |
| 4 | 18 |
| 5 | 20 |
| 6 a 10 | 22 |
| 11 a 15 | 24 |
| 16 a 20 | 26 |
| 21 a 25 | 28 |
| 26 a 30 | 30 |
| 31 a 35 | 32 |

## Ejemplos (5 filas por tabla, con datos personales ocultos)

**Employees + Users.IsActive**

| EmployeeID | Nombre | Correo | JobTitle | Department | EntryDate | ManagerID | NumeroEmpleadoLector | UserID | Activo |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | L. L. | x\*\*\*@(externo) | Desarrollador Sr. | Desarrollo | 2024-04-01 | | | 1 | 1 |
| 8 | C. G. | x\*\*\*@inovatech.com.mx | Dirección General | | 2012-11-01 | | 17 | 9 | 1 |
| 9 | A. R. | x\*\*\*@inovatech.com.mx | Contador | Contabilidad | 2019-12-02 | 8 | 19 | 19 | 1 |
| 10 | F. A. | x\*\*\*@inovatech.com.mx | Gerente Operaciones | Operaciones | 2019-10-01 | 8 | | 21 | 1 |
| 11 | G. O. | x\*\*\*@inovatech.com.mx | Contador | Contabilidad | 2018-03-26 | 8 | 3 | 18 | 1 |

**TimeOffRequests**

| RequestID | EmployeeID | RequestType | StartDate | EndDate | DaysQuantity | DeductionValue | Status | CreatedAt | PeriodoID |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 475 | 11 | PERMIT_5H | 2026-11-18 | 2026-11-18 | 0.5 | 0.5 | APPROVED | 2026-11-17 09:05 | 44 |
| 474 | 13 | PERMIT_3H | 2026-11-13 | 2026-11-13 | 0.3 | 0.3 | APPROVED | 2026-11-13 07:56 | 227 |
| 473 | 9 | PERMIT_3H | 2026-11-09 | 2026-11-09 | 0.3 | 0.3 | APPROVED | 2026-11-12 09:30 | 26 |
| 472 | 17 | VACATION | 2026-12-21 | 2026-12-24 | 4 | 4 | APPROVED | 2026-11-09 09:31 | 86 |
| 471 | 21 | VACATION | 2026-11-05 | 2026-11-05 | 1 | 1 | APPROVED | 2026-11-03 16:44 | 123 |

**VacationPeriods**

| PeriodoID | EmployeeID | NumeroPeriodo | FechaInicio | FechaFin | DiasDerecho |
| --- | --- | --- | --- | --- | --- |
| 1257 | 8 | 16 | 2027-11-01 | 2028-10-31 | 24 |
| 1256 | 14 | 2 | 2027-02-16 | 2028-02-15 | 12 |
| 1255 | 14 | 1 | 2026-02-16 | 2027-02-15 | 0 |
| 255 | 10 | 9 | 2027-10-01 | 2028-09-30 | 22 |
| 254 | 12 | 8 | 2027-02-19 | 2028-02-18 | 22 |

**wotime_inova.Eventos** (solo checadas)

| EventoId | DispositivoId | FechaEvento (UTC) | FechaLocal | Major/Minor | NumeroEmpleado | ModoVerificacion | TieneFoto | Origen |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 3856 | 1 | 2026-11-17 15:20:33Z | 2026-11-17 09:20:33 | 5/75 | 15 | faceOrFpOrCardOrPw | 1 | push |
| 3853 | 1 | 2026-11-17 15:06:18Z | 2026-11-17 09:06:18 | 5/75 | 13 | faceOrFpOrCardOrPw | 1 | push |
| 3850 | 1 | 2026-11-17 15:04:38Z | 2026-11-17 09:04:38 | 5/75 | 16 | faceOrFpOrCardOrPw | 1 | push |
| 3847 | 1 | 2026-11-17 14:56:27Z | 2026-11-17 08:56:27 | 5/75 | 14 | faceOrFpOrCardOrPw | 1 | push |
| 3844 | 1 | 2026-11-17 14:55:53Z | 2026-11-17 08:55:53 | 5/75 | 10 | faceOrFpOrCardOrPw | 1 | push |

**AsistConfig** (única fila)

| HoraEntrada | HoraSalida | DiasLaborales | ToleranciaMin | ToleranciaSalidaMin | ContarSalidaAnticipada | RetardosPorFalta | DiasPorRetardos | DescontarFaltas | DiasPorFalta | Periodo | InicioCalculo |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 08:30 | 18:30 | 1111100 | 10 | 0 | 1 | 3 | 1 | 1 | 1 | mes | 2026-10-29 |

`AsistHorarioPersona` (1 fila): EmployeeID 36, de 09:30 a 17:30, `1111100`, vigente desde 2026-11-02. `AsistJustificacion` tiene 2 filas (una `comision` y una `falta`, ambas aprobadas y pedidas por el empleado).

## Volumen

| Concepto | Cantidad |
| --- | --- |
| Colaboradores | 31 en total: 27 activos y 4 de baja |
| Personas en el lector | 22, todas habilitadas |
| Checadas | Septiembre 2026: 44 (2 personas, instalación). Octubre: 93 (arranque el 10-28). Noviembre al día 17: 328 (19 personas). Un mes completo: unas 600 |
| Eventos del lector | Unos 1,300 al mes, incluidos intentos fallidos y eventos de puerta |
| Solicitudes | 452 desde diciembre de 2023, unas 13 al mes. 269 de vacaciones, 131 permisos de horas, 18 de día y 34 canjes |
| Periodos de vacaciones | 179 (incluye periodos futuros) |

En la práctica casi todos checan **solo al llegar**. En noviembre hay 230 checadas tipo Entrada y ninguna tipo Salida, así que las reglas de "salida omitida" de WOW! Time marcarían a casi todos.

## Reglas del sistema actual

- **Jornada:** de lunes a viernes, de 08:30 a 18:30, con **10 minutos de tolerancia** a la entrada y 0 a la salida (todo configurable en `AsistConfig`; hay excepciones por persona en `AsistHorarioPersona`).
- **Retardo:** si la entrada pasa de la hora más la tolerancia, se cuentan los minutos desde la hora de entrada, no desde que termina la tolerancia. La salida anticipada también cuenta (`ContarSalidaAnticipada = 1`).
- **Falta:** día laborable ya pasado sin ninguna checada ni justificación. "Sin salida" es un día pasado con una sola checada.
- **Descuentos:** se descuentan en **días de vacaciones**, y no en nómina. Cada **3 retardos** en el **mes** equivalen a **1 día**, y cada falta injustificada a **1 día**. Se calculan al consultar y no se guardan.
- **Qué perdona una incidencia:** las vacaciones y los permisos de día completo aprobados cubren el día. Los permisos de 3 y 5 horas perdonan el retardo y la salida anticipada. Una justificación aprobada perdona la incidencia de su tipo; las de falta, comisión y home office cubren el día completo.
- **Días hábiles para vacaciones:** de lunes a viernes. **Los festivos no se restan**, porque `ProyFestivo` está vacío y el cálculo de vacaciones no lo consulta.
- **Antigüedad:** se cuentan los años **cumplidos** (Ley Federal del Trabajo de 2023). El primer año da 0 días; al cumplirlo se otorgan 12, y después se sigue `VacationTiers`. RH puede ajustar los días a mano, y esos ajustes se respetan.
- **Vencimiento:** los días no tomados **se pierden al empezar el periodo siguiente** (`FechaFin`, un día antes del siguiente aniversario). No pasan saldos de un periodo a otro: `BalancePreviousPeriod` siempre vale 0. Lo programado ya aprobado se descuenta aunque todavía no ocurra.
- **Aprobación:** la cadena sigue `ManagerID` por niveles, como se explica en Organigrama.
- **Hora oficial:** `America/Monterrey`.

## Anexo A — usuario de solo lectura (EJECUTADO el 2026-11-17)

Se ejecutó en producción con estas definiciones. La contraseña se generó al azar y quedó solo en `backend/WowTime.Api/.env` (`ConnectionStrings__Origen` y `ConnectionStrings__OrigenChecadas`).

Resultado de la prueba con ese usuario:

- **Puede leer:** las 9 vistas de `InovatechWAP` (31 colaboradores, 452 solicitudes, 179 periodos, 118 aprobaciones) y las 2 de `wotime_inova` (465 checadas).
- **Se le niega:** leer `dbo.Users`, `dbo.Employees`, `dbo.Eventos` y `dbo.EventoFotos`; escribir en las vistas; y entrar a las bases de SAP.

Para volver a crearlo en otro servidor, este es el script.

```sql
USE master;
CREATE LOGIN wowtime_lectura WITH PASSWORD = '<contraseña>', CHECK_POLICY = ON, DEFAULT_DATABASE = InovatechWAP;
GO
/* ---------- InovatechWAP: solo el esquema wowtime ---------- */
USE InovatechWAP;
CREATE USER wowtime_lectura FOR LOGIN wowtime_lectura;
GO
CREATE SCHEMA wowtime AUTHORIZATION dbo;
GO
CREATE VIEW wowtime.Colaboradores AS
SELECT e.EmployeeID, e.NumeroEmpleadoLector, e.FirstName, e.LastName, e.Email, e.JobTitle, e.Department,
       e.EntryDate, e.ManagerID, e.UserID, CAST(ISNULL(u.IsActive, 0) AS bit) AS Activo
FROM dbo.Employees e LEFT JOIN dbo.Users u ON u.UserID = e.UserID;
GO
CREATE VIEW wowtime.PeriodosVacaciones AS SELECT PeriodoID, EmployeeID, NumeroPeriodo, FechaInicio, FechaFin, DiasDerecho, Notas, CreatedAt FROM dbo.VacationPeriods;
GO
CREATE VIEW wowtime.EscalaVacaciones AS SELECT MinYears, MaxYears, DaysEntitled FROM dbo.VacationTiers;
GO
CREATE VIEW wowtime.Solicitudes AS
SELECT RequestID, EmployeeID, RequestType, StartDate, EndDate, ReturnDate, DaysQuantity, DeductionValue, Reason, Status,
       BossApproved, BossApprovedDate, DirectorApproved, DirectorApprovedDate, CreatedAt, PeriodoID, SinDescuento, NotaAjuste
FROM dbo.TimeOffRequests;
GO
CREATE VIEW wowtime.Aprobaciones AS SELECT ApprovalID, RequestID, Nivel, ApproverEmployeeID, ApproverUserID, ApproverName, Status, ActedAt, Comment, CreatedAt FROM dbo.TimeOffApprovals;
GO
CREATE VIEW wowtime.ConfigAsistencia AS
SELECT HoraEntrada, HoraSalida, DiasLaborales, ToleranciaMin, ToleranciaSalidaMin, ContarSalidaAnticipada, RetardosPorFalta,
       DiasPorRetardos, DescontarFaltas, DiasPorFalta, Periodo, InicioCalculo
FROM dbo.AsistConfig;
GO
CREATE VIEW wowtime.HorariosPersona AS SELECT HorarioID, EmployeeID, HoraEntrada, HoraSalida, DiasLaborales, ToleranciaMin, VigenteDesde, VigenteHasta FROM dbo.AsistHorarioPersona;
GO
CREATE VIEW wowtime.Justificaciones AS
SELECT JustificacionID, EmployeeID, Fecha, Tipo, HoraCorregida, Motivo, Estado, Origen, SolicitadoNombre, SolicitadoEn, ResueltoNombre, ResueltoEn, Respuesta
FROM dbo.AsistJustificacion;
GO
CREATE VIEW wowtime.Festivos AS SELECT FestivoID, Fecha, Nombre FROM dbo.ProyFestivo WHERE Company = N'SBOINOVA';
GO
GRANT SELECT ON SCHEMA::wowtime TO wowtime_lectura;
GO
/* ---------- wotime_inova: checadas sin datos crudos ni biometría ---------- */
USE wotime_inova;
CREATE USER wowtime_lectura FOR LOGIN wowtime_lectura;
GO
CREATE SCHEMA wowtime AUTHORIZATION dbo;
GO
CREATE VIEW wowtime.Checadas AS
SELECT e.EventoId, e.DispositivoId, e.SerialNo, e.FechaEvento, e.FechaLocal, e.Fecha, e.Major, e.Minor, c.Descripcion,
       e.NumeroEmpleado, e.ModoVerificacion, e.TieneFoto, e.Origen, e.FechaRecepcion
FROM dbo.Eventos e
JOIN dbo.CatalogoEventos c ON c.Major = e.Major AND c.Minor = e.Minor AND c.EsChecada = 1
WHERE e.NumeroEmpleado IS NOT NULL;
GO
CREATE VIEW wowtime.Dispositivos AS SELECT DispositivoId, Nombre, Modelo, Activo, UltimoContacto FROM dbo.Dispositivos;
GO
GRANT SELECT ON SCHEMA::wowtime TO wowtime_lectura;
GO
```

Las vistas pertenecen a `dbo`, así que el usuario lee a través de ellas sin tener permiso sobre las tablas (*ownership chaining*). Para leer de forma incremental, conviene filtrar por `EventoId > último importado` en `Checadas`. Para las solicitudes, `TimeOffRequests` no tiene fecha de modificación: se puede volver a leer completa, porque son pocas, o comparar contra un hash.
