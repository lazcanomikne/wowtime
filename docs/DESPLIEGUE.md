# Despliegue

Durante el desarrollo todo corre en local. Estos son los pasos para publicar el back en IIS (este servidor) y el front en Vercel cuando se autorice.

## 1. Base de datos (PostgreSQL)

1. En el servidor existe la instancia propia `wowtime-postgres` (puerto 5433, datos en `C:\Desarrollo\wowtime-data\pg`). Para producción puede seguir usándose o crear la base en otra instancia:
   ```sql
   CREATE ROLE wowtime LOGIN PASSWORD '<contraseña larga>';
   CREATE DATABASE wowtime OWNER wowtime ENCODING 'UTF8';
   ```
2. Las tablas se crean con las migraciones al arrancar la API (`Db__MigrarAlIniciar=true`). También se pueden aplicar a mano: `dotnet WowTime.Api.dll migrar`.
3. **No cargues la semilla de demostración en producción.** Si la base está vacía, al arrancar se crean los catálogos base y el usuario `admin@wowtime.mx` con la contraseña de `ADMIN_PASSWORD_INICIAL` (obligado a cambiarla al entrar).

## 2. Back en IIS

Requisitos: rol IIS y el **ASP.NET Core Hosting Bundle de .NET 10** (ya instalados en este servidor).

1. Prepara el `.env` de producción en `backend\WowTime.Api\.env` (ver `.env.example`):

   | Variable | Valor de producción |
   | --- | --- |
   | `ConnectionStrings__Default` | Cadena a la base de producción |
   | `Jwt__Clave` | 64 caracteres aleatorios, distinta a la de desarrollo |
   | `Archivos__SecretoEnlaces` | Otra clave aleatoria (firma de enlaces de archivos y QR) |
   | `Cors__Origenes` | `https://<proyecto>.vercel.app,https://<dominio propio>` |
   | `Archivos__Ruta` | Carpeta fuera del sitio, p. ej. `D:\wowtime-archivos` |
   | `Https__Redirigir` | `true` si IIS tiene el enlace HTTPS |
   | `ADMIN_PASSWORD_INICIAL` | Solo si la base está vacía |
   | `Smtp__*` | Datos del correo saliente (opcional) |

   Después de publicar, el `.env` queda junto a `WowTime.Api.dll`. Alternativa: definir las mismas variables en el `web.config` (`<environmentVariables>`) o en el grupo de aplicaciones.

2. Publica (PowerShell como administrador):
   ```powershell
   cd backend
   .\publicar-iis.ps1 -CrearSitio -CopiarEnv -Puerto 7030 -HostName api.tudominio.mx
   ```
   El script ejecuta `dotnet publish`, crea el grupo de aplicaciones `WowTimeApi` (sin código administrado, *AlwaysRunning*, sin tiempo de inactividad) y el sitio `WowTimeApi`, da permisos al grupo sobre la carpeta de archivos y arranca el sitio. **No modifica otros sitios.** Para actualizar, vuelve a correrlo sin `-CrearSitio`.

3. Verifica: `https://api.tudominio.mx/api/v1/salud` y la documentación en `/api/docs`.

### HTTPS

- La geolocalización y la cámara del navegador **solo funcionan en HTTPS o en localhost**. El front en Vercel ya es HTTPS; la API también debe serlo, o el navegador bloqueará las llamadas (contenido mixto).
- En IIS agrega un enlace `https` al sitio `WowTimeApi` con un certificado válido (por ejemplo, win-acme para Let's Encrypt o el certificado corporativo) y pon `Https__Redirigir=true`.
- Abre el puerto en el firewall solo para el enlace HTTPS.

### CORS

`Cors__Origenes` es la lista exacta de orígenes del front separados por comas (sin `/` final). En pruebas: `http://localhost:5173`. En producción: el dominio de Vercel y el dominio propio. Las vistas previas de Vercel tienen otra URL; agrégala si se van a probar contra la API de producción.

## 3. Front en Vercel

1. En Vercel, **Add New → Project** e importa el repositorio `lazcanomikne/wowtime`.
2. **Root Directory: `frontend`**. Framework: Vite (lo detecta `frontend/vercel.json`). Build: `npm run build`. Salida: `dist`.
3. Variable de entorno (Production y Preview): `VITE_API_URL=https://api.tudominio.mx` (sin `/` final).
4. Despliega. `vercel.json` ya redirige todas las rutas a `index.html` (SPA) y evita que el service worker quede en caché.
5. En el back, agrega el dominio de Vercel a `Cors__Origenes` y en **Configuración → Parámetros del sistema** cambia `sistema.url_publica_front` al dominio público (se usa en los enlaces de QR y de recuperación de contraseña).

La PWA se instala desde el navegador: en Android/Chrome aparece "Instalar app"; en iPhone, Compartir → Agregar a inicio; en escritorio, el ícono de instalar en la barra de direcciones.

## 4. Tareas programadas

Las tareas (vencimiento de documentos y de días, cumpleaños, cierre diario de asistencia con faltas, expiración de QR, estado de asignaciones y otorgamiento de saldos) son idempotentes. Se ejecutan:

- al arrancar la API y cada hora mientras el sitio está activo, y
- desde el **Programador de tareas de Windows**, para no depender de que IIS tenga el sitio despierto.

Registra la tarea (PowerShell como administrador):

1. Crea en **Configuración → Llaves de API** una llave llamada "Programador de tareas" con el permiso **Parámetros del sistema** (`sistema.configurar`).
2. Ejecuta:
   ```powershell
   cd backend
   .\registrar-tareas.ps1 -UrlApi https://api.tudominio.mx -Llave <llave>
   ```
   Crea "WOW! Time - Tareas" (cada hora, `POST /api/v1/tareas/ejecutar`, bitácora en `C:\ProgramData\WowTime\tareas.log`) y "WOW! Time - Respaldo" (diario a las 02:30).

El historial de ejecuciones se ve en **Configuración → Tareas programadas**.

## 5. Respaldo de la base

- `backend\respaldar-bd.ps1` genera un respaldo comprimido con `pg_dump` en `C:\Desarrollo\wowtime-data\respaldos` (parámetro `-Destino`) y conserva 14 días (`-Dias`).
- Lo programa `registrar-tareas.ps1`. Copia esa carpeta a otro equipo o a la nube.
- Restaurar: `pg_restore -h localhost -p 5433 -U wowtime -d wowtime --clean --if-exists <archivo.dump>`.
- Respalda también la carpeta de archivos (`Archivos__Ruta`): ahí están los documentos del expediente, fotos y evidencias.

## 6. Lista de verificación de salida

- [ ] `.env` de producción con claves nuevas, sin datos de demostración.
- [ ] HTTPS en la API y CORS con los dominios del front.
- [ ] `VITE_API_URL` en Vercel y `sistema.url_publica_front` apuntando al dominio público.
- [ ] Tareas y respaldos registrados en el Programador de tareas.
- [ ] Contraseña del administrador cambiada y usuarios reales creados.
- [ ] SMTP configurado (o revisar **Configuración → Correos enviados**).
