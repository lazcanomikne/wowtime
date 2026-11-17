# WOW! Time

Sistema de control de asistencia y Recursos Humanos. Una sola API REST con tres caras:

- **Portal RH** (escritorio): Inicio, Colaboradores, Asistencia, Vacaciones, Visitantes, Expedientes, Reportes y Configuración.
- **Portal del colaborador** (escritorio): Inicio, Mi asistencia, Mis vacaciones y Mi expediente (más Aprobaciones y Mi equipo para supervisores y gerentes).
- **App móvil** (la misma PWA en el teléfono): Inicio, Asistencia (con geocerca, mapa y foto), Solicitudes y Perfil; registro de invitados con QR.

Además: modo quiosco de asistencia (`/quiosco`), control de acceso de visitantes con lectura de QR por cámara (`/control-acceso`) y página pública del QR del visitante (`/qr/:codigo`).

| Carpeta | Contenido |
| --- | --- |
| `backend/` | API en ASP.NET Core 10 + EF Core + PostgreSQL, pruebas xUnit y scripts de IIS, tareas y respaldo |
| `frontend/` | PWA en Vue 3 + Vite + TypeScript, pruebas de punta a punta con Playwright |
| `docs/` | Arquitectura, mockups, capturas, decisiones, progreso, bloqueos y despliegue |

## Requisitos

- Windows, Linux o macOS con **.NET SDK 10**, **Node.js 22** y **PostgreSQL 15 o superior**.
- Para las pruebas de punta a punta, Playwright descarga Chromium la primera vez.

## Levantar en local desde un clon limpio

1. Crea la base de datos y un usuario propio (en `psql` como superusuario; cambia la contraseña):

   ```sql
   CREATE ROLE wowtime LOGIN PASSWORD 'cambia-esta-contraseña';
   CREATE DATABASE wowtime OWNER wowtime ENCODING 'UTF8';
   CREATE DATABASE wowtime_test OWNER wowtime ENCODING 'UTF8';
   ```

2. Configura el back: copia `backend/WowTime.Api/.env.example` como `backend/WowTime.Api/.env` y llena al menos `ConnectionStrings__Default`, `ConnectionStrings__Pruebas` y `Jwt__Clave` (32 caracteres o más). El archivo `.env` no se sube a git.

3. Levanta todo con un solo comando (PowerShell). La primera vez usa `-Semilla` para cargar los datos de demostración:

   ```powershell
   .\iniciar.ps1 -Semilla
   ```

   - Front: http://localhost:5173
   - API: http://localhost:5080 · documentación interactiva: http://localhost:5080/api/docs

   Las siguientes veces basta `.\iniciar.ps1`. Las migraciones se aplican solas al arrancar la API.

   Sin PowerShell: `cd backend/WowTime.Api && dotnet run -- semilla --reiniciar && dotnet run` y, en otra terminal, `cd frontend && npm ci && npm run dev`.

## Usuarios de demostración

> Solo para demostración. Todos usan la contraseña **`WowTime2026!`**. No los uses en producción: borra la base o cambia las contraseñas.

| Rol | Correo | Persona |
| --- | --- | --- |
| Administrador | `admin@wowtime.mx` | Carolina Guajardo (Dirección general), todas las sucursales |
| Recursos Humanos | `rh@wowtime.mx` | Mariana Torres, Sucursal Monterrey |
| Recursos Humanos (2.º) | `daniel.ramirez@wowtime.mx` | Daniel Ramírez, Monterrey y Saltillo |
| Gerente | `gerente@wowtime.mx` | Carlos Méndez (Gerente de Operaciones) |
| Supervisor | `supervisor@wowtime.mx` | Ana Lucía Martínez (Supervisora de Limpieza) |
| Colaborador | `colaborador@wowtime.mx` | Sofía Herrera (Personal de Limpieza) |
| Recepción | `recepcion@wowtime.mx` | Laura Jiménez Salinas (Recepcionista) |

Otros usuarios de colaborador: `daniela.torres@`, `miguel.sanchez@`, `carlos.ramirez@` y `maria.fernandez@wowtime.mx`. El PIN del quiosco de todos los colaboradores de demostración es `1234`. Para usar el quiosco, genera la llave del dispositivo "Quiosco de asistencia" en **Configuración → Dispositivos** y pégala en `/quiosco`.

## Pruebas

```powershell
.\probar.ps1
```

Corre las pruebas del back (reglas de negocio y recorridos de la API contra `wowtime_test`) y los 5 recorridos de punta a punta en navegador. Usan la base de pruebas, que se borra y se vuelve a cargar; no tocan los datos de desarrollo. Con `-SoloBack` corre solo las del back.

Capturas de las 11 pantallas diseñadas (quedan en `docs/capturas/`):

```powershell
cd frontend; npx playwright test --config playwright.capturas.config.ts
```

## API

- REST bajo `/api/v1`, documentada con OpenAPI 3: interfaz en `/api/docs`, especificación en `/api/docs/v1/openapi.json`.
- Autenticación por sesión (`POST /api/v1/auth/login` → token de acceso de 15 min y de renovación de 14 días) o por llave de API en el encabezado `X-Api-Key` (se crean y revocan en **Configuración → Llaves de API**).
- Permisos y alcance por sucursal aplicados en el servidor en cada endpoint. Errores siempre con el formato `{ "error": { "codigo", "mensaje", "detalles" } }` y mensajes en español.

## Documentación

- [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md): pantallas, reglas, modelo de datos y API.
- [docs/DECISIONES.md](docs/DECISIONES.md): stack y decisiones propias.
- [docs/DESPLIEGUE.md](docs/DESPLIEGUE.md): IIS, Vercel, HTTPS, CORS, respaldos y tareas programadas.
- [docs/PROGRESO.md](docs/PROGRESO.md): lista de control por módulo.
- [docs/BLOQUEOS.md](docs/BLOQUEOS.md): dependencias externas y sus sustitutos.
- [docs/GUIA_FRONT.md](docs/GUIA_FRONT.md): sistema visual y convenciones del front.
