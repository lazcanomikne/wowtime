# Integración con InovaOS: pedir vacaciones y permisos desde ambos lados

Decisión del usuario del 17 de noviembre de 2026:

- **WOW! Time gestiona todo**: colaboradores, organigrama, asistencia, vacaciones y permisos.
- **También se puede pedir y autorizar desde InovaOS**: el portal web y la app InovaWay, que usan el mismo backend.

Hay **una sola fuente de verdad, WOW! Time**. No hay sincronización en ambos sentidos. El backend de InovaOS llama a la API de este documento y traduce las respuestas a la forma que ya usan sus pantallas.

El lado de InovaOS **ya está hecho y probado** con un servidor falso que sigue este contrato. Está apagado por un interruptor (ver "Lado de InovaOS"). **Falta construir en WOW! Time lo que pide este documento.**

## 1. Autenticación

| Encabezado | Valor |
| --- | --- |
| `X-Api-Key` | Llave de API de WOW! Time (`<prefijo>.<secreto>`) con el permiso nuevo **`integracion.inovaos`**, con alcance a todas las sucursales |
| `X-Actuar-Como` | Correo del usuario de InovaOS. WOW! Time lo resuelve contra `Usuario`/`Colaborador` por correo, sin distinguir mayúsculas. Va en las rutas marcadas "como usuario" |

Reglas:

- En las rutas "como usuario", la operación se hace **con los permisos y el alcance de esa persona**, igual que si hubiera entrado a WOW! Time. Por ejemplo, un colaborador solo ve lo suyo y solo aprueba si le toca. La llave por sí sola no da más.
- La bitácora debe registrar al usuario real y el canal "InovaOS".
- Si el correo no corresponde a nadie activo: `404 { "error": { "codigo": "colaborador_no_encontrado", "mensaje": "..." } }`.
- Las rutas sin `X-Actuar-Como` (calendario, colaboradores y reporte) usan el alcance de la llave.
- Hoy `POST /api/v1/solicitudes` exige `Yo.UsuarioIdRequerido`. Este canal necesita un `UsuarioActual` construido desde `X-Actuar-Como`, solo cuando la llave tiene `integracion.inovaos`.
- Errores en el formato de siempre: `{ "error": { "codigo", "mensaje", "detalles" } }`. InovaOS muestra `mensaje` tal cual, así que debe venir en español y entendible para el usuario final.

## 2. Tipos

Las fechas sin hora van como `YYYY-MM-DD`; las que tienen hora, en ISO 8601 con zona.

```jsonc
// Colaborador
{ "id": 7, "origenId": 11, "nombre": "Ana Lucía Martínez", "puesto": "Contador", "area": "Contabilidad",
  "correo": "ana@empresa.mx", "fechaIngreso": "2018-03-26" }
// origenId = EmployeeID de InovaOS (null si nació en WOW! Time)

// Solicitud
{ "id": 501, "origenId": 472, "tipo": "VACATION", "inicio": "2026-12-21", "fin": "2026-12-24", "regreso": null,
  "dias": 4, "descuento": 4, "motivo": "Fin de año", "estado": "PENDING", "creada": "2026-11-17T15:00:00Z",
  "colaborador": { /* Colaborador */ },
  "aprobaciones": [
    { "nivel": 1, "aprobador": { "nombre": "Carlos Méndez", "correo": "carlos@empresa.mx" },
      "estado": "APPROVED", "fecha": "2026-11-17T16:00:00Z", "comentario": null }
  ],
  "puedeActuar": true }
```

| Campo | Valores | Equivalencia en WOW! Time |
| --- | --- | --- |
| `tipo` | `VACATION` | `TipoSolicitud.Vacaciones` |
| | `PERMIT_3H` | `Permiso` con un tipo de permiso "3 horas", que descuenta **0.3** días |
| | `PERMIT_5H` | `Permiso` "5 horas", que descuenta **0.5** días |
| | `PERMIT_DAY` | `Permiso` "Día completo", que descuenta **1** día |
| | `CASH_OUT` | **Nuevo:** canje de días, que se pagan en lugar de tomarse. Consume saldo, no ocupa calendario y no lleva fechas reales (se manda la fecha del día) |
| `estado` | `PENDING`, `APPROVED`, `REJECTED`, `CANCELLED` | `EstadoSolicitud` Pendiente, Aprobada, Rechazada, Cancelada |
| `aprobaciones[].estado` | `PENDING` (le toca ahora), `WAITING` (aún no llega), `APPROVED`, `REJECTED`, `SKIPPED` | `EstadoPaso` Pendiente, EnEspera, Aprobado, Rechazado, Omitido |
| `dias` | Días que abarca: hábiles para vacaciones, fijos para permisos, los capturados para canje | |
| `descuento` | Lo que resta del saldo. Es 0 si se rechazó o se capturó sin descuento | |
| `puedeActuar` | `true` si el usuario de `X-Actuar-Como` puede aprobar o rechazar **ahora** | |

Los tipos de permiso 3 h, 5 h y día completo, y el canje, son reglas de InovaOS que hay que conservar. En WOW! Time se pueden modelar como `TipoPermiso` con una clave fija (`PERMIT_3H`…) y el descuento configurado. El canje necesita su propio tipo.

## 3. Endpoints

Base: `/api/v1/integracion/inovaos`

| # | Método y ruta | Como usuario | Qué hace | Respuesta |
| --- | --- | --- | --- | --- |
| 1 | `GET /perfil` | Sí | Ficha y saldo del periodo vigente | `{ colaborador, antiguedad: { anios, meses }, periodo: { inicio, fin, otorgados, usados, programados, canjeados, disponible, diasParaVencer } \| null }` |
| 2 | `GET /solicitudes/mias` | Sí | Todas sus solicitudes, de cualquier estado | `Solicitud[]` |
| 3 | `POST /solicitudes` | Sí | Crea una solicitud: compromete saldo y avisa al primer aprobador, igual que la web | `201 Solicitud`, o `422` con `codigo` y `mensaje` (saldo insuficiente, traslape, rango inválido…) |
| 4 | `GET /aprobaciones` | Sí | Las que le conciernen: las que le toca decidir ahora y las que ya decidió. RH y Dirección ven además todas las pendientes | `Solicitud[]` con `puedeActuar` |
| 5 | `POST /solicitudes/{id}/aprobar` | Sí | Aprueba su paso | `{ "estado": "PENDING" \| "APPROVED", "siguienteNivel": 2 \| null }`. `403` con `no_le_toca`; `409` con `ya_resuelta` |
| 6 | `POST /solicitudes/{id}/rechazar` | Sí | Rechaza; corta la cadena y libera el saldo | `{ "estado": "REJECTED", "siguienteNivel": null }` |
| 7 | `GET /ausencias?desde=YYYY-MM-DD` | No | Aprobadas cuyo `fin >= desde`, de todos (calendario de la empresa) | `Solicitud[]` |
| 8 | `GET /colaboradores` | No | Colaboradores activos, para el filtro del reporte | `Colaborador[]` |
| 9 | `GET /solicitudes?desde&hasta&tipo&colaboradorId&estado` | No | Reporte. `desde` filtra por `inicio >=` y `hasta` por `fin <=`. `tipo` acepta una lista separada por comas. `colaboradorId` es el `id` de WOW! Time | `Solicitud[]` |

Cuerpos:

- Endpoint 3: `{ "tipo": "VACATION", "inicio": "2026-12-21", "fin": "2026-12-24", "dias": 3, "motivo": "texto" }`. `dias` solo va con `CASH_OUT`. Para los demás tipos, WOW! Time calcula días y descuento con sus reglas: días hábiles de lunes a viernes, permisos de valor fijo.
- Endpoints 5 y 6: `{ "comentario": "texto o null" }`.

**Correos de aviso:** los manda WOW! Time en todos los casos, también cuando la solicitud llega por InovaOS. InovaOS no manda nada en este modo.

## 4. Migración desde InovaOS

Fuente: el usuario de solo lectura `wowtime_lectura` y las vistas `wowtime.*`, ya creados. El detalle está en `docs/ORIGEN_SQLSERVER.md`, y la cadena de conexión en `backend/WowTime.Api/.env` (`ConnectionStrings__Origen` y `ConnectionStrings__OrigenChecadas`).

1. **Campos de referencia** con índice único: `Colaborador.OrigenId` (= `EmployeeID`) y `Solicitud.OrigenId` (= `RequestID`). Así la migración se puede correr varias veces sin duplicar.
2. **Colaboradores, desde `wowtime.Colaboradores`:**
   - El supervisor sale de `ManagerID`.
   - La sucursal es una sola, "Monterrey".
   - Puesto y área se crean como catálogo a partir del texto.
   - `Activo = 0` se migra como Baja, con la fecha de la migración.
3. **Saldos, desde `wowtime.PeriodosVacaciones`:** un otorgamiento por periodo, que vence en `FechaFin`. Se respeta `DiasDerecho` aunque difiera de la escala, porque son ajustes manuales con su `Notas`.
4. **Solicitudes, desde `wowtime.Solicitudes` + `wowtime.Aprobaciones`:**
   - Las aprobadas se convierten en consumos del periodo por `PeriodoID`.
   - Las pendientes siguen pendientes, en el mismo paso.
   - Las rechazadas no mueven saldo.
5. **Checadas:** se importan cada 15 minutos desde `wowtime.Checadas` (`EventoId > último`) hasta que el lector envíe directo a WOW! Time.

## 5. Lado de InovaOS (ya hecho, apagado)

Repositorio `C:\Desarrollo\Inova\InovaOS\server`, con commit local.

| Archivo | Qué es |
| --- | --- |
| `services/wowtime/cliente.js` | Cliente HTTP: encabezados, tiempo de espera y errores. Lee las variables de entorno |
| `services/wowtime/vacaciones.js` | Traduce este contrato a las respuestas que ya usan el portal e InovaWay |
| `controllers/rhController.js` | Los 7 endpoints `/rh/*` de vacaciones usan WOW! Time cuando el interruptor está encendido |
| `routes/rhRoutes.js` | Con el interruptor encendido, la gestión administrativa (`/rh/gestion-vacaciones/*`) responde 409 "se hace en WOW! Time" con la liga |
| `tests/wowtime/adaptador.test.js` | Servidor falso con este contrato. 10 pruebas; se corren con `npm run test:wowtime` |

Variables del `.env` de InovaOS (IIS: `C:\inetpub\wwwroot\PortalInova_Back\.env`):

```
VACACIONES_ORIGEN=wowtime          # sin ella, o con 'inovaos', todo sigue como hoy
WOWTIME_API_URL=http://<host>:<puerto>
WOWTIME_API_KEY=<prefijo>.<secreto>
WOWTIME_PORTAL_URL=https://<portal de WOW! Time>
WOWTIME_TIMEOUT_MS=10000
```

Endpoints de InovaOS que cambian al encender el interruptor:

| Endpoint | Llama a |
| --- | --- |
| `/rh/my-vacations` | 1 y 2 |
| `/rh/request-time-off` | 3 |
| `/rh/authorization/dashboard` | 4 |
| `/rh/authorization/action` | 5 y 6 |
| `/rh/calendar-events` | 7 |
| `/rh/employees-list` | 8 |
| `/rh/reports/generate` | 9 |

Pendiente para el corte, del lado de InovaOS:

- **Organigrama:** sigue leyendo y escribiendo `Employees`. Cuando WOW! Time sea el dueño del organigrama, InovaOS debe dejar de editarlo y, si se necesita, leerlo de WOW! Time.
- **Asistencia:** `services/asistencia/datos.js` sigue leyendo `TimeOffRequests` para perdonar retardos. Hay que pasarlo a WOW! Time o retirar ese módulo.

## 6. Pasos del corte

1. WOW! Time implementa las secciones 1 a 4, con pruebas contra este contrato. Las de InovaOS sirven de referencia: `tests/wowtime/adaptador.test.js`.
2. Se crea la llave `integracion.inovaos`. Se prueba InovaOS en desarrollo (puerto 7110) con `VACACIONES_ORIGEN=wowtime` apuntando a WOW! Time.
3. Última migración. En el `.env` de IIS se ponen las variables, se recicla el pool `PortalInova_Back` y se verifica en el portal y en InovaWay. Publicar en producción requiere el visto bueno del usuario.
4. Las tablas de vacaciones de InovaOS quedan como histórico. Para regresar, basta con quitar `VACACIONES_ORIGEN` y reciclar el pool, pero lo pedido mientras tanto vivirá solo en WOW! Time.

## 7. Implementación en WOW! Time (17 de noviembre de 2026)

Las secciones 1 a 4 están construidas y probadas. **No hay nada publicado** en IIS ni en Vercel, y el `.env` de IIS de InovaOS no se tocó.

| Pieza | Dónde |
| --- | --- |
| 9 endpoints | `backend/WowTime.Api/Controllers/IntegracionInovaOSController.cs` |
| `X-Actuar-Como` | `Infra/Seguridad.cs` (`LlaveApiHandler`). Solo funciona con una llave con `integracion.inovaos` y solo en `/api/v1/integracion/inovaos/*`. Fuera de esas rutas la llave no toma los permisos del usuario |
| Migración y checadas | `Services/Integracion/OrigenInova.cs` (lectura) y `Services/Integracion/MigradorInova.cs` |
| Pruebas del contrato | `backend/WowTime.Tests/IntegracionInovaOSTests.cs` (6) y `MigracionInovaOSTests.cs` (1, con origen falso) |

Verificación:

- **Pruebas:** 50 pruebas del back y 10 en navegador pasan.
- **Migración real:** se corrió contra las vistas de producción (solo lectura) en una base local aparte, `wowtime_inova`. Llegaron 31 colaboradores, 179 periodos, 453 solicitudes, 183 entradas y 128 salidas. **El disponible del periodo vigente coincide con InovaOS en los 31 colaboradores.** La segunda corrida no cambió nada.
- **Adaptador de InovaOS:** se llamó a `services/wowtime/vacaciones.js`, sin modificarlo, contra esa base. Funcionaron mis vacaciones, autorizaciones con su cadena, calendario, empleados, reportes y el 404 por correo desconocido.

### Detalles del contrato (sin cambios incompatibles)

Nada de esto rompe el adaptador de InovaOS ni sus pruebas:

- **`tipo`:** puede valer `PERMIT_OTHER` para un permiso de WOW! Time sin clave. La migración desactiva esos tipos porque InovaOS no los maneja, así que en la práctica no aparece.
- **Errores:**
  - `400 falta_actuar_como`: una ruta "como usuario" llegó sin el encabezado.
  - `403 sin_permiso`: el usuario no puede crear solicitudes.
  - `404 solicitud_no_encontrada`.
  - `422` con códigos `tipo_invalido`, `dias_invalidos`, `rango_invalido`, `saldo_insuficiente`, `traslape`, `fechas_pasadas`, `sin_supervisor` y `estado_invalido`.
- **Rechazar:** acepta `comentario: null`; WOW! Time guarda "Rechazada sin comentario.".
- **`GET /ausencias`:** sin `desde`, usa los últimos 60 días.
- **Filtros del reporte:** `GET /solicitudes` acepta `estado` como lista separada por comas, igual que `tipo`.
- **`dias` y `descuento` en permisos:** valen 0.3, 0.5 o 1. Un permiso de valor fijo debe ser de un solo día (`inicio = fin`).
- **Canje (`CASH_OUT`):** WOW! Time ignora `inicio` y `fin` y guarda la fecha del día.

### Cómo se migra

```bash
dotnet WowTime.Api.dll migrar-inovaos
```

Opciones del comando:

- `--sin-checadas`: no importa checadas.
- `--sin-cierre`: no genera faltas de días pasados.
- `--sin-ajustes`: no ajusta escala, parámetros ni tipos de permiso.

```bash
dotnet WowTime.Api.dll importar-checadas
```

Variables de entorno (en `backend/WowTime.Api/.env`):

| Variable | Para qué |
| --- | --- |
| `ConnectionStrings__Origen`, `ConnectionStrings__OrigenChecadas` | Usuario de solo lectura `wowtime_lectura` |
| `Integracion__InovaOS__CorreosRh` | Correos, separados por coma, que reciben el rol Recursos Humanos. Si no hay ninguno, lo recibe Dirección General |
| `Integracion__InovaOS__Activa=true` | Enciende la importación de checadas cada 15 minutos y la tarea `checadas_inovaos`. **Apagada por omisión** |
| `Integracion__InovaOS__MigrarCadaHora=true` | Durante la transición, vuelve a migrar cada hora |
| `Integracion__InovaOS__Sucursal` | Sucursal única. Por omisión, `Monterrey` |

Con IIS, la importación cada 15 minutos también se puede registrar en el Programador de tareas: `backend\registrar-tareas.ps1 -InovaOS`.

### Reglas de la migración

- **Idempotente:** cada fila guarda su `OrigenId` y una huella de la fila de origen. Si la fila no cambió, la migración no toca lo que ya se editó o decidió en WOW! Time; si cambió en InovaOS antes del corte, se reconstruye desde el origen.
- **Jefe directo:** sale de `ManagerID`. Quien no tiene jefe queda bajo Dirección General, como aprobaba InovaOS, y se avisa en el informe. Dirección General es la raíz: sus propias solicitudes las captura RH.
- **Roles:**
  - Cada colaborador recibe un usuario sin contraseña. Puede actuar por InovaOS y, en WOW! Time, entrar con "¿Olvidaste tu contraseña?".
  - Quien tiene reportes recibe el rol Supervisor.
  - Dirección General recibe el rol Administrador.
- **Número de colaborador:** es el del lector. Si no hay, se usa `INV-<EmployeeID>`. Quien no tiene número en el lector queda con el registro "No registra asistencia", así no se le generan faltas.
- **Periodos:** el periodo N de InovaOS es el saldo de N−1 años cumplidos. Vence en `FechaFin` y respeta `DiasDerecho` y sus `Notas`.
- **Solicitudes según su estado:**
  - Aprobadas: consumen su periodo (`PeriodoID`).
  - Pendientes: comprometen saldo y siguen en el mismo paso, con los titulares de InovaOS.
  - Rechazadas: no mueven saldo.
  - Con `SinDescuento`: descuentan 0.
- **Horas:** `CreatedAt` de InovaOS está en hora de Monterrey. `ActedAt`, `BossApprovedDate`, `DirectorApprovedDate` y las fechas de las justificaciones están en UTC. Se verificó con los datos: ninguna decisión quedaba antes de su creación.
- **Checadas:**
  - Solo se importan desde `InicioCalculo` (2026-10-29).
  - La primera checada del día es la entrada y la última es la salida.
  - Una checada a menos de 2 minutos de la anterior se ignora.
  - Las checadas de números que no corresponden a nadie en InovaOS se cuentan y se avisan. Hoy son `0001`, `18`, `5` y `9`, con 57 eventos.
- **Ajustes con `AjustarReglas`:**
  - La escala de vacaciones se toma de `VacationTiers`.
  - La tolerancia de 10 minutos se copia.
  - Se apaga "Salida omitida" (parámetro `asistencia.exigir_salida`).
  - Se desactivan los tipos de permiso sin clave.
- **Retardos:** los permisos de 3 y 5 horas los perdonan y no cubren la jornada.

### Pendiente para el corte (requiere tu visto bueno)

1. **Correos de RH:** definir `Integracion__InovaOS__CorreosRh` y volver a migrar. Hoy el rol RH cae en Dirección General.
2. **Ruta de aprobación:**
   - Las solicitudes nuevas siguen las reglas de WOW! Time: supervisor directo → RH, o gerente → RH para quien tiene reportes.
   - InovaOS subía por la cadena de jefes. Si se quiere igual, se cambia en Configuración → Reglas de aprobación.
   - Las pendientes migradas conservan la cadena de InovaOS.
3. **Base de producción:** crear la base de WOW! Time de producción, correr `migrar-inovaos` y crear la llave `integracion.inovaos` (permisos `integracion.inovaos` y `sucursales.todas`).
4. **Encender InovaOS:** probar InovaOS en desarrollo (puerto 7110) con `VACACIONES_ORIGEN=wowtime`; después, el `.env` de IIS de InovaOS y el reciclado del pool.
5. **Coordenadas de la sucursal Monterrey:** capturarlas si se va a usar la geocerca de la app.
