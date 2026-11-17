# Integración con el sistema actual (SQL Server)

WOW! Time va a leer del sistema actual: colaboradores, organigrama, asistencia y vacaciones. La base es SQL Server y está en la misma red que este servidor.

Este documento dice qué hace falta saber para conectarlo. Las respuestas van en `docs/ORIGEN_SQLSERVER.md` (plantilla al final).

> **Contraseñas y llaves no van en ningún archivo del repositorio.** La cadena de conexión con su contraseña va solo en `backend/WowTime.Api/.env`, como `ConnectionStrings__Origen`. Ese archivo no se sube a git.

## 1. Conexión

| Dato | Ejemplo |
| --- | --- |
| Servidor e instancia | `192.168.1.20\SQLEXPRESS` o `sql01` |
| Puerto | `1433` |
| Base de datos | `RHAsistencia` |
| Tipo de acceso | Usuario de SQL Server o cuenta de Windows |
| Usuario | `wowtime_lectura` |
| Cifrado | Si pide `Encrypt=True` o `TrustServerCertificate=True` |
| Versión de SQL Server | `SELECT @@VERSION` |

Lo ideal es un usuario **solo de lectura** sobre las tablas o vistas de abajo:

```sql
CREATE LOGIN wowtime_lectura WITH PASSWORD = '...';
CREATE USER wowtime_lectura FOR LOGIN wowtime_lectura;
ALTER ROLE db_datareader ADD MEMBER wowtime_lectura;
```

## 2. Cómo se va a usar

Responder con una opción por pregunta. Lo marcado como **propuesta** es lo que hago si no se indica otra cosa.

1. **¿Qué sistema manda?**
   - **Propuesta:** el sistema actual sigue siendo el dueño de colaboradores y organigrama. WOW! Time los copia y no los modifica allá.
   - Otra opción: migrar una sola vez y que WOW! Time pase a ser el sistema principal.
2. **Cada cuándo se lee.**
   - **Propuesta:** cada 15 minutos para las checadas, y cada hora para colaboradores, organigrama y saldos.
3. **¿Dónde se piden las vacaciones de aquí en adelante?** Puede ser en el sistema actual o en WOW! Time. Si siguen allá, WOW! Time solo las muestra.
4. **¿Las checadas se siguen registrando en el sistema actual?** Por ejemplo, desde los relojes checadores. Si es así, WOW! Time las importa y calcula retardos y faltas con sus reglas.
5. **¿Cuánto historial se importa?**
   - **Propuesta:** 12 meses de asistencia y todo el historial de vacaciones.

## 3. Qué datos necesito

Para cada tema hace falta lo siguiente:

- La tabla o vista y el nombre de cada columna.
- Su llave: el identificador que nunca cambia.
- Cómo se distingue una baja de un registro activo.

Los campos marcados con \* son indispensables.

### Colaboradores

| Campo | Notas |
| --- | --- |
| Identificador único\* | El que no cambia nunca |
| Número de empleado\* | |
| Nombre(s) y apellidos\* | Juntos o separados |
| Correo | Con él se crea su usuario para entrar a WOW! Time |
| Teléfono | |
| Fecha de nacimiento | |
| Fecha de ingreso\* | Con ella se calculan los días de vacaciones por antigüedad |
| Puesto, área, sucursal\* | El id o el nombre, y su tabla de catálogo |
| Jefe directo\* | El id del jefe; con este campo se arma el organigrama |
| Estatus y fecha de baja\* | |
| Turno u horario | Hora de entrada y salida, y días que trabaja |
| RFC, CURP, NSS | Opcionales |

### Catálogos

- **Sucursales:** nombre y dirección. Si tienen latitud y longitud, mejor, porque sirven para la geocerca.
- **Áreas o departamentos.**
- **Puestos.**
- **Turnos y horarios.**
- **Días festivos**, si el sistema actual los tiene.

### Organigrama

Si el jefe directo está en la tabla de colaboradores, no hace falta nada más. Si el organigrama vive en otra tabla, necesito esa tabla.

### Asistencia

| Campo | Notas |
| --- | --- |
| Colaborador\* | Su id |
| Fecha y hora de la checada\* | Indicar si se guarda en hora de México o en UTC |
| Tipo | Entrada o salida, si el sistema lo distingue |
| Origen | Reloj, dispositivo o sucursal |
| Incidencias y justificaciones | Si el sistema las maneja |

### Vacaciones y ausencias

- **Saldos por colaborador y periodo:** días otorgados, días tomados y fecha de vencimiento.
- **Solicitudes:** fecha de inicio y fin, días, estatus, quién aprobó y cuándo.
- **Permisos, incapacidades y otros tipos de ausencia**, con su catálogo.

## 4. Lo que puede sacar la otra sesión de Code

Esta consulta lista las columnas de todas las tablas y vistas. Hay que correrla en la base del sistema actual y pegar el resultado de las que tienen que ver con los temas de arriba:

```sql
SELECT t.TABLE_SCHEMA, t.TABLE_NAME, t.TABLE_TYPE, c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE
FROM INFORMATION_SCHEMA.TABLES t
JOIN INFORMATION_SCHEMA.COLUMNS c ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION;
```

Además:

- **Ejemplos:** 5 filas de cada tabla elegida. Se pueden ocultar nombres y datos personales.
- **Volumen:** cuántos colaboradores activos hay y cuántas checadas por mes, más o menos.
- **Reglas propias:** cualquier regla que aplique el sistema actual. Por ejemplo, tolerancia de retardo, cómo cuenta días hábiles o cómo vencen las vacaciones.

## Plantilla de respuesta (`docs/ORIGEN_SQLSERVER.md`)

```markdown
# Origen SQL Server

## Conexión (sin contraseña)
Servidor/instancia: · Puerto: · Base: · Usuario: · Acceso (SQL/Windows): · Cifrado: · Versión:

## Decisiones
1. Sistema que manda: 
2. Frecuencia: 
3. Vacaciones se piden en: 
4. Checadas se registran en: 
5. Historial a importar: 

## Mapeo
### Colaboradores — tabla/vista: 
| Campo WOW! Time | Columna origen | Notas |
### Catálogos — sucursales / áreas / puestos / turnos / festivos
### Organigrama
### Asistencia — tabla/vista: 
### Vacaciones — saldos / solicitudes / tipos de ausencia

## Ejemplos (5 filas por tabla)
## Volumen
## Reglas del sistema actual
```
