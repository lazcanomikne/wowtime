# Bloqueos externos

Cosas que dependen de algo fuera de mi alcance (credenciales, permisos, archivos). Cada una tiene un sustituto local que funciona.

## 1. Faltan 3 de las 8 imágenes del diseño

- **Qué pasa:** el repositorio `lazcanomikne/wowtime` estaba vacío y en la conversación llegaron solo 5 imágenes: `Dashboard.png`, `Ficha_colaborador.png`, `Solicitud_de_Vacaciones.png`, `Organigrama_Autorizaciones.png` y `App_Movil.png`. No están `Portal_Autorizar_Vacaciones.png`, `Solicitar_vacaciones.png` ni `Registro_de_invitados.png`.
- **Sustituto:** esas tres pantallas se construyeron con la descripción elemento por elemento de `docs/ARQUITECTURA.md` (secciones 7 y 10) y con el sistema visual de las otras cinco. La versión móvil de Solicitar vacaciones y Registrar invitado sí aparece en `App_Movil.png`.
- **Qué hacer:** copiar las tres imágenes a `docs/mockups/` y repetir la comparación visual de esas pantallas.

## 2. Contraseña del superusuario de PostgreSQL existente

- **Qué pasa:** el servidor ya tenía PostgreSQL 18 (servicio `postgresql-x64-18`, puerto 5432) usado por otros proyectos. No hay contraseña del superusuario disponible y el acceso exige `scram-sha-256`.
- **Sustituto:** para no tocar esa instancia ni su configuración, se creó un clúster propio con los mismos binarios: servicio de Windows `wowtime-postgres`, puerto **5433**, datos en `C:\Desarrollo\wowtime-data\pg`. Tiene la base `wowtime` (y `wowtime_test`) con el usuario propio `wowtime`. Las credenciales están en `C:\Desarrollo\wowtime-data\credenciales.txt` (fuera del repositorio).
- **Qué hacer (opcional):** si se prefiere la instancia principal, crear ahí la base y el usuario y cambiar `ConnectionStrings__Default` en el `.env`. Las migraciones crean todo.

## 3. Correo saliente (SMTP)

- **Qué pasa:** no hay servidor SMTP ni credenciales para enviar la recuperación de contraseña y la invitación al visitante.
- **Sustituto:** todo correo se guarda en la tabla `Correos` y se ve en **Configuración → Correos enviados** (rol Administrador), con el enlace de recuperación listo para abrir. Si se configuran las variables `Smtp__*` del `.env`, el envío es real.
- **Qué hacer:** llenar `Smtp__Host`, `Smtp__Usuario`, `Smtp__Password` y `Smtp__Remitente`.
