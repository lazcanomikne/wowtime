# WOW! Time — Progreso

Plan por fases y una casilla por cada función de la lista de control. Se marca cuando la función está terminada **y verificada** contra la base real.

Si la sesión se reinicia: leer este archivo y `docs/DECISIONES.md`, y retomar en la primera casilla sin marcar.

## Fases

- [x] 1. Entorno, repositorio, estructura, base de datos, migraciones y datos de demostración
- [x] 2. Acceso, roles, marco de navegación, sistema visual y logo
- [x] 3. Organización y colaboradores
- [x] 4. Asistencia
- [x] 5. Vacaciones, permisos y motor de aprobaciones
- [x] 6. Visitantes
- [x] 7. Expedientes
- [x] 8. Dashboard, notificaciones y tareas programadas
- [x] 9. Reportes y configuración completa
- [x] 10. PWA, verificación visual, pruebas de punta a punta y guía de despliegue

## Acceso
- [x] Inicio de sesión
- [x] Cierre de sesión
- [x] Cambio de contraseña
- [x] Recuperación de contraseña
- [x] Roles y permisos aplicados en el servidor
- [x] Alcance por sucursal

## Marco
- [x] Menú según rol
- [x] Selector de sucursal
- [x] Buscador global
- [x] Centro de notificaciones con contador
- [x] Menú de usuario y perfil

## Inicio RH
- [x] Los 4 indicadores
- [x] Gráfica de asistencia en tiempo real
- [x] Solicitudes pendientes con Aprobar y Rechazar
- [x] Últimos registros
- [x] Cumpleaños
- [x] Documentos por vencer
- [x] Visitantes de hoy

## Colaboradores
- [x] Listado con filtros
- [x] Alta
- [x] Edición
- [x] Baja
- [x] Ficha con sus 7 pestañas
- [x] Esquema de trabajo
- [x] Asignaciones temporales
- [x] Notas
- [x] Bitácora
- [x] Menú Más acciones

## Asistencia
- [x] Registro de entrada y salida desde la app con geocerca
- [x] Mapa
- [x] Foto opcional
- [x] Registro por terminal (quiosco web y endpoint con llave por dispositivo)
- [x] Cálculo de estado
- [x] Incidencias con su flujo
- [x] Vista de RH
- [x] Vista de supervisor
- [x] Vista del colaborador

## Vacaciones y permisos
- [x] Saldos
- [x] Políticas
- [x] Solicitud en web
- [x] Solicitud en móvil
- [x] Simulación de días hábiles y ruta
- [x] Calendario de equipo
- [x] Cobertura y alertas
- [x] Vencimientos
- [x] Permisos
- [x] Incapacidades
- [x] Las 4 pestañas del portal RH

## Aprobaciones
- [x] Motor único con reglas por tipo de solicitud
- [x] Pasos
- [x] Delegaciones
- [x] Bandeja de aprobaciones (web y móvil)
- [x] Bitácora de cada decisión

## Visitantes
- [x] Registro de invitado
- [x] QR firmado con vigencia
- [x] Página pública del QR
- [x] Control de acceso con lectura por cámara
- [x] Entrada y salida
- [x] Listado y detalle para RH

## Expedientes
- [x] Tipos de documento
- [x] Documentos requeridos por puesto
- [x] Carga y descarga
- [x] Vigencias
- [x] Alertas
- [x] Avance por colaborador
- [x] Mi expediente

## Organización
- [x] Organigrama navegable y editable
- [x] Colaboradores sin supervisor
- [x] Sucursales
- [x] Áreas
- [x] Equipos
- [x] Puestos
- [x] Turnos

## Reportes
- [x] Asistencia por periodo
- [x] Retardos y faltas
- [x] Vacaciones y saldos
- [x] Visitantes
- [x] Documentos por vencer
- [x] Filtros
- [x] Exportación a CSV
- [x] Exportación a Excel

## Configuración
- [x] Catálogos y parámetros de la sección 13
- [x] Reglas de aprobación
- [x] Delegaciones
- [x] Días festivos
- [x] Mensajes de marca
- [x] Llaves de API

## Tareas programadas
- [x] Vencimiento de documentos
- [x] Vencimiento de días
- [x] Cumpleaños
- [x] Cierre diario de asistencia con faltas
- [x] Expiración de QR
- [x] Estado de asignaciones

## PWA
- [x] Manifiesto
- [x] Service worker
- [x] Instalación en teléfono y escritorio
- [x] Íconos generados a partir del logo

## Cierre
- [x] Pruebas automáticas de reglas de negocio
- [x] Pruebas de punta a punta (5 recorridos)
- [x] Capturas de las 11 pantallas en `docs/capturas/`
- [x] Documentación OpenAPI de todos los endpoints
- [x] README, DECISIONES y DESPLIEGUE completos

## Cómo se verificó

- **Back**: 43 pruebas xUnit (`backend/WowTime.Tests`): reglas puras (días hábiles con el caso obligatorio del 15 al 19 de septiembre de 2026 = 3, traslapes, estado de asistencia, geocerca, vigencia y firma del QR, vigencia de documentos, antigüedad, cobertura, festivos) y recorridos de la API contra PostgreSQL (ruta Gerente → RH, flujo completo con saldo y calendario, delegación y separación de funciones, autoaprobación, saldo insuficiente, traslapes y liberación de saldo, colaborador sin supervisor, entrada y salida con geocerca, invitado con QR, documento con alerta, permisos y alcance por sucursal, llaves de API, recuperación y cambio de contraseña, tareas programadas idempotentes).
- **Punta a punta**: 5 recorridos en navegador (`frontend/e2e/recorridos.spec.ts`) y una prueba de humo que recorre todas las pantallas con cada rol sin errores de JavaScript ni respuestas 4xx/5xx (`frontend/e2e/humo.spec.ts`).
- **Visual**: capturas de las 11 pantallas diseñadas en `docs/capturas/`, comparadas con `docs/mockups/` (ver `docs/capturas/COMPARACION.md`).
