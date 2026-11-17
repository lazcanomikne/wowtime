# Decisiones

Decisiones tomadas por cuenta propia durante la construcción. Las "Decisiones ya tomadas" del encargo (1 a 28) se aplican tal cual y no se repiten aquí salvo para explicar cómo se implementaron.

## Stack

| Capa | Elección | Motivo |
| --- | --- | --- |
| Back | ASP.NET Core 10 (LTS vigente, soporte hasta nov. 2028) con controladores | Corre nativo en IIS con el ASP.NET Core Module V2 que ya está instalado en el servidor, y en local con `dotnet run`. |
| Acceso a datos | EF Core 10 + Npgsql 10, migraciones en `backend/WowTime.Api/Data/Migraciones` | Migraciones versionadas en el repositorio y tipos nativos de PostgreSQL (arreglos para días de descanso, tipos de solicitud, sucursales). |
| Base de datos | PostgreSQL 18 | Ya instalado en el servidor. Instancia propia en el puerto 5433 (ver BLOQUEOS). |
| Documentación API | Swashbuckle (OpenAPI 3) con interfaz en `/api/docs` | Interfaz navegable y comentarios XML de cada endpoint. |
| Excel | ClosedXML | Genera `.xlsx` sin Office instalado. |
| Front | Vue 3 + Vite + TypeScript + vue-router + Pinia + vite-plugin-pwa | Una sola PWA instalable que sirve a RH, colaborador y móvil; se publica estática en Vercel. |
| Mapa | Leaflet con teselas de OpenStreetMap | No requiere llave de pago. |
| Lectura de QR | jsQR sobre el video de la cámara | Funciona en cualquier navegador con `getUserMedia`, sin dependencias nativas. |
| Pruebas | xUnit (reglas y API contra `wowtime_test`) y Playwright (punta a punta) | |

## Arquitectura

- **Un solo proyecto de API** (`WowTime.Api`) con carpetas `Domain`, `Data`, `Services`, `Controllers` e `Infra`. Las reglas puras (días hábiles, geocerca, QR, vigencias) viven en `Services/Reglas.cs` sin base de datos, para probarlas aisladas.
- **Errores con formato único**: `{ "error": { "codigo", "mensaje", "detalles" } }`, siempre en español, incluidos 401, 403, 404, 422 de validación y 500.
- **Permisos en el servidor**: cada endpoint declara `[Permiso(...)]`. Los roles agrupan permisos en la tabla `RolPermisos`, editable en Configuración → Roles. El token lleva roles, permisos y sucursales; los cambios aplican al renovar la sesión (15 minutos como máximo).
- **Alcance por sucursal**: cada consulta pasa por `UsuarioActual.Alcance(...)`. RH ve solo sus sucursales; el permiso `sucursales.todas` (Administrador) ve todas. Supervisores y gerentes ven a sus subordinados directos e indirectos; el colaborador, solo a sí mismo.
- **Llaves de API**: encabezado `X-Api-Key: <prefijo>.<secreto>`. Se guarda solo el hash SHA-256. Cada llave tiene su propia lista de permisos y sucursales.
- **Tokens**: acceso JWT de 15 minutos y renovación de 14 días con rotación (cada renovación revoca el anterior). Se guardan hasheados.
- **Archivos**: fuera de la carpeta pública (`Archivos__Ruta`). Se sirven solo por `GET /api/v1/archivos/{id}/contenido`, que exige sesión o un **enlace firmado** (HMAC, caducidad de 30 a 120 minutos) que la API emite únicamente a usuarios con sesión. Se usa así porque las etiquetas `<img>` no envían el encabezado `Authorization`.
- **Tiempo real**: canal SSE `GET /api/v1/eventos/sucursal/{id}` (el token va en la query porque `EventSource` no admite encabezados). El dashboard recarga indicadores y gráfica al recibir eventos.
- **Tareas programadas**: `TrabajadorTareas` las ejecuta al arrancar y cada hora; todas son idempotentes (claves únicas en notificaciones, tabla `EjecucionesTarea`). Además existe `POST /api/v1/tareas/ejecutar` para llamarlas desde el Programador de tareas de Windows con una llave de API, así no dependen de que IIS tenga el sitio despierto.
- **Hora oficial**: toda checada la fecha el servidor en la zona horaria de la sucursal (`America/Monterrey` por omisión).

## Reglas de negocio (interpretaciones propias)

1. **Gerente en la ruta de aprobación**: si el solicitante tiene reportes directos (es supervisor), "Gerente" es su jefe directo; si no, es el jefe de su supervisor. Así la ruta de Ana Lucía Martínez resulta Carlos Méndez → Recursos Humanos, como pide la decisión 5.
2. **Paso de Recursos Humanos**: lo puede decidir cualquier usuario con rol RH de la sucursal, excepto el solicitante y quien decidió un paso previo (aunque haya sido por delegación). Si no hay otro usuario de RH, se permite y se guarda la nota en la bitácora de la solicitud (decisión 6).
3. **Delegaciones**: se evalúan al momento de decidir, no al crear la solicitud. Si una delegación termina antes, los pendientes regresan solos al titular.
4. **Incapacidades**: no son solicitudes con aprobación; las captura RH con su folio y bloquean traslapes. Aparecen en rojo en el calendario.
5. **Incidencias**: el colaborador las justifica y eso crea una solicitud del tipo "Incidencia de asistencia" que sigue la regla de incidencias (supervisor directo). El supervisor o RH también pueden resolverlas directamente desde la bandeja de incidencias.
6. **Cierre diario**: a partir de las 23:00 locales (parámetro) se generan faltas para quien no registró entrada en un día laborable, y "salida omitida" para quien entró y no salió. Se cierran también hasta 7 días atrás si el sitio estuvo apagado.
7. **Cobertura**: miembros programados ese día (según su turno) que no están ausentes (vacaciones o permisos aprobados, incapacidades) entre miembros programados. Las solicitudes pendientes se muestran en el detalle, pero no bajan el porcentaje.
8. **Saldos**: se otorgan por aniversario con la tabla de la política (demostración: 20 días de 1 a 5 años, con piso de ley validado al editar) y vencen a los 12 meses. Al enviar se comprometen, al aprobar se consumen y al rechazar o cancelar se liberan, siempre consumiendo primero el periodo que vence antes. Cada movimiento queda en `MovimientosSaldo`.
9. **"Días que vencen"** en Tu saldo disponible: días disponibles del periodo con fecha límite más cercana, y el mes de esa fecha.
10. **Organigrama por sucursal**: los conteos de reportes directos se calculan dentro de la sucursal mostrada. La raíz de la empresa es el puesto con nivel 1 (Dirección general); cualquier otro colaborador sin supervisor aparece en la alerta "sin supervisor asignado".
11. **Número de colaborador**: prefijo configurable (`WC-`) más consecutivo de 4 dígitos.
12. **Terminal física**: el endpoint `POST /api/v1/terminal/registros` acepta número de colaborador y, opcionalmente, PIN, autenticado con la llave del dispositivo (`X-Device-Key`). El modo quiosco web exige PIN.

## Datos de demostración

- Personas de los mockups unificadas en un solo juego de datos (inconsistencia 11): Ana Lucía Martínez es Supervisora de Limpieza del Equipo Norte; Sofía Herrera, Jorge López y Diego Ramírez son Personal de Limpieza bajo ella; Carolina Guajardo es Dirección general (rol Administrador); Mariana Torres y Daniel Ramírez son de Recursos Humanos.
- Para llegar a 160 en Monterrey se agregaron tres supervisores más bajo Carlos Méndez (Andrea Ruiz, Verónica Castro y Ricardo Solís), con su personal. Por eso Carlos tiene más de 1 reporte directo en el organigrama.
- La asignación temporal de Ana Lucía es "Home office" por "Remodelación del área de supervisión" (corrige la inconsistencia 4) y no se traslapa con su solicitud de vacaciones.
- El comando de semilla genera asistencia de los últimos 45 días y del día de hoy **hasta la hora en que se ejecuta**. Para ver el dashboard "vivo" conviene cargarla en horario laboral.

## Interfaz

1. **Un solo menú** armado con los permisos. RH y Administrador ven los 8 módulos del mockup; su propio autoservicio (Mi asistencia, Mis vacaciones, Mi expediente, Bandeja de aprobaciones, Mis invitados) está en el menú de usuario para no alterar el menú del diseño. Supervisores y gerentes ven, además de las 4 opciones del colaborador, **Aprobaciones**, **Mi equipo** y **Reportes** (decisión 9 y matriz de permisos); por eso el menú de Ana Lucía tiene más opciones que en `Solicitud_de_Vacaciones.png`.
2. **Recepción** usa el portal de autoservicio con **Control de acceso** y **Visitantes**; en teléfono la segunda pestaña de la barra inferior es "Acceso" en lugar de "Asistencia".
3. **Barra inferior** solo para quien no es de RH y está en un teléfono (< 768 px). RH en teléfono conserva el menú lateral como cajón.
4. **Búsqueda de personas para no RH**: el listado de colaboradores respeta el alcance (un colaborador solo se ve a sí mismo), así que la cobertura, el delegado y el anfitrión se eligen con `GET /solicitudes/companeros` y `GET /aprobaciones/delegados` (compañeros activos de sus sucursales, sin datos sensibles).
5. **Organigrama**: por omisión se despliegan la raíz y los nodos con hasta 3 reportes directos; el árbol se centra y el zoom se ajusta para que quepa. Los demás se abren con su etiqueta de reportes o con "Expandir todo". Hijos ordenados de menos a más reportes (Mariana Torres a la izquierda, como en el mockup).
6. **Filtro de mes de Vacaciones**: el mes del encabezado y las flechas del calendario de equipo están sincronizados (una sola fuente de verdad del periodo).
7. **Campos de fecha** con formato `dd/mm/aaaa` propio (no el del navegador) y hora en formato de 24 h en intervalos de 15 minutos.
8. **Ruta en móvil**: "Revisará: Gerente → RH" se arma con las etiquetas de los pasos que calcula el servidor; en web se muestran nombre y puesto de cada aprobador.
9. **Control de acceso**: lee el QR con la cámara (jsQR) y acepta el contenido o el código escrito. La salida se registra con el mismo QR o con el botón "Registrar salida" de la lista "Dentro ahora".
10. **Quiosco web**: la llave del dispositivo se guarda en el navegador de la tableta (`localStorage`) y se valida contra la API; si se revoca, regresa a la pantalla de configuración.
11. **Sin conexión**: el service worker guarda las lecturas de la API (red primero, caché como respaldo) y las teselas del mapa, así la app abre con los últimos datos. Registrar asistencia exige conexión y lo indica en pantalla.

## Pruebas y verificación visual

1. **Pruebas del back**: xUnit con `WebApplicationFactory` contra `wowtime_test`, que se borra y se recarga con la semilla en cada corrida.
2. **Punta a punta**: Playwright levanta su propia API (puerto 5081, base `wowtime_test`) y su propio front (5174), así no dependen ni alteran el entorno de desarrollo.
3. **Reloj simulado (solo demostración)**: la variable `Reloj__Simular=AAAA-MM-DDTHH:mm:ss` desplaza la hora del servidor. Se usa únicamente para las capturas (12:15 de hoy), para que el dashboard se vea "vivo" aunque se generen de madrugada. El token JWT siempre usa el reloj real. Nunca se activa en producción.
4. **Datos de demostración**: María Fernández nunca tiene registro del día (para la prueba de entrada y salida); nadie cumple aniversario laboral el día de la carga y ningún saldo de demostración queda negativo.
