# WOW! Time — Arquitectura del sistema

Versión del 17 de noviembre de 2026

## 1. Resumen y alcance

WOW! Time es un sistema de control de asistencia y Recursos Humanos con tres canales: portal web de RH, portal web de autoservicio del colaborador y app móvil. Las 8 imágenes contienen 11 pantallas, porque App_Movil.png agrupa 4.

Lo que el diseño deja establecido:

- **Ocho módulos en el portal RH:** Inicio, Colaboradores, Asistencia, Vacaciones, Visitantes, Expedientes, Reportes y Configuración.
- **Operación multisucursal:** las pantallas de RH filtran por sucursal (ejemplo: Sucursal Monterrey).
- **Tres modalidades de trabajo:** presencial, híbrido y home office, con registro por terminal, por app móvil o por ambos.
- **Asistencia móvil con geocerca:** la app valida la ubicación contra un área autorizada y puede pedir foto de evidencia.
- **Aprobaciones en cadena:** cada tipo de solicitud sigue una regla configurable (ejemplo: Supervisor directo → Recursos Humanos) y admite delegaciones temporales.
- **Visitantes con QR temporal:** el colaborador anfitrión registra al invitado y el sistema genera un código de acceso.
- **Expediente digital con vigencias:** documentos por colaborador con alertas de vencimiento.

Los datos de ejemplo corresponden a una empresa de servicios de limpieza con 160 colaboradores en la sucursal mostrada.

Convención del documento: **visible** es lo que aparece en la imagen; **implícito** es lo que el diseño obliga a construir aunque no esté dibujado. Las secciones 5 a 10 recorren cada pantalla elemento por elemento. La sección 15 lista las contradicciones entre imágenes que conviene resolver antes de programar.

## 2. Inventario de pantallas

Las 8 imágenes cubren 5 pantallas web y 6 vistas móviles; dos imágenes móviles son el detalle ampliado de vistas que ya aparecen en App_Movil.png.

| # | Archivo | Canal | Usuario en pantalla | Módulo | Pantalla |
| --- | --- | --- | --- | --- | --- |
| 1 | Dashboard.png | Portal RH (web) | Carolina G., Recursos Humanos | Inicio | Resumen de hoy |
| 2 | Ficha_colaborador.png | Portal RH (web) | Daniel Ramírez, Recursos Humanos | Colaboradores | Ficha de Ana Lucía Martínez, pestaña Resumen |
| 3 | Portal_Autorizar_Vacaciones.png | Portal RH (web) | Mariana Torres, RH | Vacaciones | Pestaña Resumen, con autorización de solicitudes |
| 4 | Organigrama_Autorizaciones.png | Portal RH (web) | Mariana Torres, RH | Configuración | Organización y aprobaciones, pestaña Organigrama |
| 5 | Solicitud_de_Vacaciones.png | Portal colaborador (web) | Ana Lucía Martínez | Mis vacaciones | Solicitar vacaciones |
| 6 | App_Movil.png | App móvil | Ana Lucía | Inicio, Asistencia, Solicitudes | Cuatro vistas: Inicio, Registrar asistencia, Registrar invitado, Solicitar vacaciones |
| 7 | Solicitar_vacaciones.png | App móvil | Colaborador | Solicitudes | Solicitar vacaciones, detalle ampliado |
| 8 | Registro_de_invitados.png | App móvil | Ana Lucía Martínez como anfitriona | Visitantes | Registrar invitado, detalle ampliado |

Cobertura por módulo del menú de RH:

| Módulo | Pantallas diseñadas | Pendiente de diseño |
| --- | --- | --- |
| Inicio | Dashboard completo | Nada |
| Colaboradores | Ficha, pestaña Resumen | Listado, alta, edición y 6 pestañas |
| Asistencia | Registro móvil y paneles del dashboard | Pantalla propia de RH |
| Vacaciones | Resumen RH y solicitud en web y móvil | Pestañas Solicitudes, Calendario de equipo, Políticas y saldos |
| Visitantes | Registro móvil y panel del dashboard | Pantalla propia de RH y control de acceso |
| Expedientes | Tarjeta en ficha y panel del dashboard | Pantalla propia y carga de documentos |
| Reportes | Ninguna | Módulo completo |
| Configuración | Organigrama, reglas y delegaciones | Pestaña Flujos de aprobación y catálogos |

## 3. Arquitectura general

Los tres canales y los puntos de acceso físicos consumen una sola API. La API aplica identidad, rol y alcance por sucursal antes de llegar a los servicios de dominio.

Diagrama de arquitectura, en texto:

- Canales (arriba): Portal RH (web, 8 opciones de menú), Portal colaborador (web, 4 opciones de menú), App móvil (colaborador y anfitrión; geocerca, foto, QR), Puntos de acceso (terminal de asistencia y lector QR de visitantes).
- Todos los canales llaman a una sola API: identidad, roles y alcance por sucursal.
- La API llama a 8 servicios de dominio: Organización, Colaboradores, Asistencia, Vacaciones, Aprobaciones, Visitantes, Expedientes, Reportes.
- Los servicios usan: Base de datos, Archivos, Tareas programadas y Notificaciones.

Ningún canal escribe directo en la base de datos. Saldos, días hábiles, estado de asistencia y rutas de aprobación se calculan en el servidor, para que web y móvil muestren siempre el mismo número.

### Componentes

| Componente | Responsabilidad | Evidencia en las imágenes |
| --- | --- | --- |
| Portal RH | Operación diaria de RH: indicadores, fichas, autorizaciones, configuración | Dashboard, Ficha, Vacaciones, Organización y aprobaciones |
| Portal colaborador | Autoservicio web: asistencia, vacaciones y expediente propios | Menú Inicio, Mi asistencia, Mis vacaciones, Mi expediente |
| App móvil | Registro de asistencia con ubicación, solicitudes e invitados | Las 4 vistas de App_Movil.png |
| Terminal de asistencia | Registro de entrada y salida en sitio | "Terminal + app móvil" y "Dispositivo asignado: Acceso principal" |
| Lector QR de visitantes | Valida la invitación y marca entrada y salida | "Invitación QR temporal" y estado "Dentro" |
| Servicio de aprobaciones | Motor único que resuelve quién aprueba cada solicitud | Reglas de aprobación y Delegaciones vigentes |
| Notificaciones | Avisos en campana, push móvil y envío del QR al visitante | Campana con contador y "El visitante recibirá un QR" |
| Tareas programadas | Vencimiento de documentos y de días, cumpleaños, cierre de asistencia, expiración de QR, acumulación de saldos | "Vence en 5 días", "Días por vencer", "Próximos cumpleaños" |
| Archivos | Documentos del expediente, fotos de perfil y fotos de evidencia | "8 de 9 documentos" y "Agregar evidencia (foto)" |

### Principios de diseño

1. **Alcance por sucursal.** Toda consulta de RH lleva la sucursal seleccionada; el rol define qué sucursales puede ver cada usuario.
2. **Un solo motor de aprobaciones.** Vacaciones, permisos, incidencias y solicitudes de supervisores usan las mismas tablas de reglas, pasos y delegaciones.
3. **Hora del servidor.** La hora de una checada la pone el servidor, no el teléfono ni el navegador.
4. **Bitácora de todo cambio.** "Actividad reciente" y la pestaña Historial se alimentan de un registro de eventos por colaborador.
5. **Ubicación mínima.** La app toma la ubicación solo al registrar, como promete el texto "Se registrará ubicación solo en este momento".

### Propuesta técnica

Las imágenes no definen tecnología; esta es una propuesta de partida.

| Capa | Propuesta | Motivo |
| --- | --- | --- |
| Portales web | Una aplicación Vue 3 con dos diseños de menú según el rol | RH y colaborador comparten componentes: calendario, tarjetas, tablas |
| App móvil | PWA instalable sobre la misma base | La ubicación se toma solo al registrar, sin rastreo en segundo plano; validar notificaciones push en iOS antes de decidir |
| API | REST en .NET, versión LTS vigente, con tokens de acceso y de renovación | Un contrato único para web, móvil, terminales y lector QR |
| Base de datos | SQL Server o PostgreSQL | Modelo relacional con integridad entre organigrama, solicitudes y saldos |
| Archivos | Almacenamiento de objetos con enlaces firmados y caducidad | Los documentos de identidad no deben quedar en enlaces públicos |
| Tiempo real | Canal de eventos del servidor al dashboard | Gráfica "Asistencia en tiempo real" e indicadores del día |
| Tareas programadas | Proceso en segundo plano con calendario | Vencimientos, cumpleaños, cierre diario y expiración de QR |

## 4. Roles, permisos y navegación

Las imágenes muestran 7 roles y obligan a crear 2 más. Un rol no excluye a otro: una supervisora también es colaboradora y anfitriona.

| Rol | Evidencia en las imágenes | Qué hace |
| --- | --- | --- |
| Recursos Humanos | Mariana Torres, Daniel Ramírez y Carolina G. en el portal RH | Opera los 8 módulos y da la aprobación final |
| Colaborador | Ana Lucía Martínez en el portal de autoservicio y en la app | Registra asistencia, solicita vacaciones, consulta su expediente |
| Supervisor directo | Primer aprobador en 3 reglas; nodos con reportes directos | Aprueba vacaciones, permisos e incidencias de su equipo |
| Gerente | Primer aprobador de "Solicitud del supervisor" | Aprueba solicitudes de supervisores y puede delegar |
| Dirección general | Raíz del organigrama, 2 reportes directos | Cima de la línea de reporte |
| Anfitrión | Campo "Anfitrión" en Registrar invitado | Registra y recibe visitantes |
| Visitante | Recibe el QR temporal | Persona externa sin cuenta; solo presenta el código |
| Administrador (implícito) | Menú Configuración y botón "Editar estructura" | Mantiene organigrama, reglas, catálogos y permisos |
| Recepción o vigilancia (implícito) | Estado "Dentro" y hora de entrada de visitantes | Valida el QR y registra entrada y salida |

### Menús por canal

| Canal | Tipo de menú | Opciones, en orden |
| --- | --- | --- |
| Portal RH | Lateral fijo | Inicio, Colaboradores, Asistencia, Vacaciones, Visitantes, Expedientes, Reportes, Configuración |
| Portal colaborador | Lateral fijo | Inicio, Mi asistencia, Mis vacaciones, Mi expediente |
| App móvil | Barra inferior | Inicio, Asistencia, Solicitudes, Perfil |

La opción activa se resalta con fondo verde en web y con ícono y subrayado verde en móvil. En Portal_Autorizar_Vacaciones.png el menú no muestra Configuración; el menú debe armarse según los permisos del usuario.

### Barra superior web

| Elemento | Dónde aparece | Comportamiento |
| --- | --- | --- |
| Selector de sucursal | Dashboard (barra), Vacaciones y Organización (junto al título) | Cambia el alcance de toda la pantalla y se conserva al navegar |
| Fecha del día | Dashboard: "Martes, 8 de septiembre" | Fecha del servidor en la zona horaria de la sucursal |
| Buscador global | Ficha: "Buscar colaboradores, reportes, etc." | Busca en colaboradores y reportes; resultados agrupados por tipo |
| Campana | Todas; con contador "3" en Dashboard y punto verde en las demás | Abre el centro de notificaciones; el contador son las no leídas |
| Menú de usuario | Todas: foto o iniciales, nombre, área y flecha | Perfil, cambio de contraseña y cierre de sesión (contenido no visible) |

### Matriz de permisos propuesta

| Módulo | Colaborador | Supervisor o gerente | Recursos Humanos | Administrador |
| --- | --- | --- | --- | --- |
| Inicio | Su resumen | Resumen de su equipo | Resumen de la sucursal | Todas las sucursales |
| Colaboradores | Su ficha, solo lectura | Fichas de su equipo, solo lectura | Alta, edición y notas | Todo |
| Asistencia | Registrar y consultar la propia | Revisar incidencias de su equipo | Todo en su sucursal | Todo |
| Vacaciones y permisos | Solicitar y consultar | Aprobar el primer paso | Aprobar el paso final, saldos y políticas | Todo |
| Visitantes | Registrar sus invitados | Registrar sus invitados | Consultar todos | Todo |
| Expedientes | Consultar y subir los propios | Sin acceso | Gestionar todos | Todo |
| Reportes | Sin acceso | De su equipo | De su sucursal | Todos |
| Configuración | Sin acceso | Delegar sus aprobaciones | Organigrama, reglas y delegaciones | Todo |

Ninguna imagen muestra dónde aprueba el supervisor directo. Hace falta una bandeja de aprobaciones para supervisores y gerentes, en web, en móvil o en ambos.

## 5. Pantalla: Dashboard (Inicio del portal RH)

Imagen: Dashboard.png. Es la pantalla de inicio de RH: resume el día de una sucursal en 4 indicadores y 6 paneles.

### Marco y encabezado

| Elemento | Visible en la imagen | Comportamiento |
| --- | --- | --- |
| Logo | "WOW! Time" con ícono de reloj y lema "Personas que hacen espacios mejores" | Regresa a Inicio |
| Menú lateral | 8 opciones; Inicio activa | Navegación principal |
| Mensaje al pie del menú | "Un equipo limpio también hace grandes lugares" con ilustración | Texto de marca configurable |
| Selector de sucursal | "Sucursal Monterrey" con desplegable | Recalcula indicadores y paneles |
| Fecha | "Martes, 8 de septiembre" | Solo lectura |
| Campana | Contador "3" | Centro de notificaciones |
| Menú de usuario | Foto, "Carolina G.", "Recursos Humanos" | Perfil y cierre de sesión |
| Título | "Resumen de hoy" | Fijo |
| Subtítulo | "Todo en orden para un gran día de trabajo" | Debe cambiar cuando hay incidencias o alertas (implícito) |
| Banner | "Espacios más limpios, personas más felices." | Mensaje de marca configurable |

### Indicadores

| Tarjeta | Valor visible | Cálculo |
| --- | --- | --- |
| Colaboradores activos | 124 de 160, barra al 78% | Colaboradores con entrada registrada hoy entre la plantilla de la sucursal |
| Asistencia puntual | 92%, 114 de 124, barra | Entradas en horario entre entradas registradas hoy |
| Incidencias por revisar | 6, flecha roja "2 vs. día anterior" | Incidencias de asistencia abiertas y su variación contra ayer |
| Visitantes dentro | 8 de 12 registrados hoy | Visitantes con entrada y sin salida entre las invitaciones del día |

Cada tarjeta debe llevar al módulo correspondiente con el filtro aplicado (implícito).

### Paneles

| Panel | Contenido visible | Enlace | Datos |
| --- | --- | --- | --- |
| Asistencia en tiempo real | Gráfica de línea; eje X de 6:00 a 20:00 cada 2 horas; eje Y de 0 a 100; etiqueta "Hoy, 8 de septiembre"; globo "12:00 · 96 colaboradores" | Ninguno | Colaboradores presentes por hora: entradas menos salidas acumuladas |
| Solicitudes pendientes | Tabla de 3 filas: Colaborador (foto, nombre, puesto), Tipo, Fechas (rango y días), Acciones | Ver todas | Solicitudes cuyo paso actual corresponde a RH |
| Últimos registros de asistencia | Tabla de 6 filas: Colaborador, Entrada, Salida, Estado | Ver todos | Registros del día, del más reciente al más antiguo |
| Próximos cumpleaños | 3 tarjetas: foto, nombre, puesto, fecha y mensaje | Ver todos | Cumpleaños de los próximos días en la sucursal |
| Documentos por vencer | 3 filas: documento y colaborador, "Vence en N días", etiqueta | Ver todos | Documentos del expediente ordenados por fecha de vencimiento |
| Visitantes de hoy | Tabla de 5 filas: Nombre, Empresa / Motivo, Entrada, Estado | Ver todos | Invitaciones del día con su estado de acceso |

### Valores y estados que aparecen

| Dato | Valores en la imagen |
| --- | --- |
| Tipo de solicitud | Vacaciones (2 filas), Permiso personal (1 fila) |
| Estado de asistencia | En horario (verde), Retardo (naranja), Salida registrada (azul) |
| Urgencia de documento | Urgente a 5 días; Próximo a 12 y a 18 días |
| Documentos listados | INE, Examen médico, Constancia de capacitación |
| Estado de visitante | Dentro |
| Motivos de visita | Proveedor de insumos, Auditoría, Mantenimiento, Entrevista de trabajo, Cliente |
| Mensajes de cumpleaños | "¡Feliz cumple, Paola!", "¡Que tengas un gran día!", "¡Muchas felicidades!" |

### Botones y acciones

| Control | Cantidad | Qué hace | Regla |
| --- | --- | --- | --- |
| Aprobar | 3, uno por solicitud | Aprueba el paso de RH | Pide confirmación; al aprobar el último paso descuenta saldo y avisa al colaborador |
| Rechazar | 3, uno por solicitud | Rechaza la solicitud | Exige motivo; avisa al colaborador |
| Ver todas / Ver todos | 5 enlaces | Abre el módulo de origen con el filtro del día | Solicitudes, Asistencia, Cumpleaños, Expedientes, Visitantes |
| Encabezados "Nombre" y "Empresa" | Subrayados en Visitantes de hoy | Ordenan la tabla | Orden ascendente y descendente |
| Punto de la gráfica | Al pasar el cursor | Muestra hora y número de colaboradores | Solo lectura |

La gráfica y los 4 indicadores deben actualizarse sin recargar la página, porque el panel se llama "en tiempo real".

## 6. Pantalla: Ficha de colaborador

Imagen: Ficha_colaborador.png. Es la vista de 360 grados de un colaborador: 7 pestañas, de las cuales solo Resumen está diseñada, con 7 tarjetas.

### Marco y encabezado

| Elemento | Visible en la imagen | Comportamiento |
| --- | --- | --- |
| Menú lateral | 8 opciones; Colaboradores activa; lema al pie "Personas que hacen grande cada día" | Navegación principal |
| Buscador global | "Buscar colaboradores, reportes, etc." | Búsqueda en todo el portal |
| Campana | Punto verde | Centro de notificaciones |
| Menú de usuario | Iniciales "DR", "Daniel Ramírez", "Recursos Humanos" | Perfil y cierre de sesión; iniciales cuando no hay foto |
| Ruta de navegación | "Colaboradores / Ana Lucía Martínez" | "Colaboradores" regresa al listado |
| Foto | Circular, grande | Se cambia desde Editar colaborador |
| Nombre y puesto | "Ana Lucía Martínez", "Supervisora de Limpieza" | Solo lectura |
| Número de colaborador | "WC-0248" | Identificador único con prefijo |
| Estatus | "Activa" con punto verde | Catálogo de estatus; inactivo o baja son implícitos |
| Etiquetas de esquema | "Sucursal Monterrey", "Turno matutino", "Híbrido", "Terminal + app móvil" | Resumen de sucursal, turno, modalidad y método de registro |
| Botón Editar colaborador | Verde, con lápiz | Abre el formulario de edición |
| Botón Más acciones | Desplegable | Contenido no visible; propuesto: dar de baja, cambiar supervisor, restablecer acceso, exportar ficha |
| Banner de cumpleaños | "Cumpleaños: 9 de septiembre" y "¡Felicidades, Ana Lucía!" | Aparece en los días cercanos a la fecha |

### Pestañas

Resumen (activa), Datos personales, Datos laborales, Asistencia, Vacaciones, Expediente e Historial. Los enlaces "Ver detalle" de cada tarjeta llevan a la pestaña correspondiente.

### Tarjetas de la pestaña Resumen

| Tarjeta | Contenido visible | Controles | Destino |
| --- | --- | --- | --- |
| Esquema de trabajo | Modalidad: Híbrido. Centro base: Sucursal Monterrey. Días en oficina: lunes, miércoles y viernes. Días remotos: martes y jueves. Registro permitido: Terminal + app móvil | Ver detalle | Datos laborales |
| Asignación temporal | "Home office", 15 – 19 sep, estado "Vigente". Motivo: "Apoyo por inventario en sucursal" | Ver detalle; botón Ver asignaciones | Lista de asignaciones del colaborador |
| Asistencia de hoy | Entrada 07:02. Estado actual: "En horario". Dispositivo asignado: "Acceso principal" | Ver detalle | Pestaña Asistencia |
| Vacaciones | "8 días disponibles". Próxima solicitud: 15 – 19 sep | Ver detalle; fila con flecha | Pestaña Vacaciones; la fila abre la solicitud |
| Expediente digital | "8 de 9 documentos", barra al 89%. Alerta roja: "INE vence en 5 días. Renueva el documento para evitar inconvenientes." | Ver detalle; la alerta tiene flecha | Pestaña Expediente; la alerta abre el documento |
| Actividad reciente | Línea de tiempo con 5 eventos | Ver todo | Pestaña Historial |
| Notas | Subtítulo "Información relevante sobre el colaborador." Una nota: "15 ago 2024, 10:12 · RH" con su texto | Botón Agregar nota; menú de tres puntos por nota | Editar o eliminar la nota (contenido del menú no visible) |

### Eventos de Actividad reciente

| Momento | Evento | Detalle | Tipo de evento |
| --- | --- | --- | --- |
| Hoy, 10:24 | Se actualizó el documento | Comprobante de domicilio | Documento actualizado |
| Hoy, 07:02 | Registro de entrada | Acceso principal | Asistencia |
| Ayer, 16:31 | Se modificaron datos laborales | Turno matutino | Cambio de datos |
| 3 sep 2024, 11:15 | Se agregó un documento | Constancia de situación fiscal | Documento agregado |
| 2 sep 2024, 09:20 | Solicitud de vacaciones aprobada | 15 – 19 de septiembre | Solicitud |

### Reglas que salen de esta pantalla

- **Esquema de trabajo.** Cada colaborador tiene modalidad, centro base, días en oficina, días remotos y método de registro permitido.
- **Asignación temporal.** Sustituye al esquema durante su periodo. Su estado se calcula por fechas: programada, vigente o concluida.
- **Validación de registro.** Una checada solo es válida si el método y el lugar coinciden con el esquema o la asignación del día.
- **Avance del expediente.** Documentos entregados entre documentos requeridos para el puesto: 8 de 9 da 89%.
- **Notas.** Guardan autor, fecha y hora. Deben ser visibles solo para RH.
- **Bitácora.** Cada cambio en documentos, asistencia, datos laborales y solicitudes genera un evento con fecha y hora.

## 7. Pantalla: Vacaciones del portal RH

Imagen: Portal_Autorizar_Vacaciones.png. Es el centro de control de ausencias: RH autoriza solicitudes, vigila la cobertura de los equipos y da seguimiento a saldos por vencer.

### Encabezado y filtros

| Elemento | Visible en la imagen | Comportamiento |
| --- | --- | --- |
| Menú lateral | 7 opciones, sin Configuración; Vacaciones activa | Menú según permisos |
| Campana y usuario | Punto verde; "Mariana Torres", "RH" | Igual que en el resto del portal |
| Filtro de sucursal | "Sucursal Monterrey" | Filtra toda la pantalla |
| Filtro de mes | "Septiembre 2026" | Define el periodo de indicadores, tabla y calendario |
| Filtro de área | "Área: Todas" | Limita a un área: Diseño, Ventas, Finanzas, Operaciones, Recursos Humanos, TI, Marketing |
| Botón Nueva solicitud | Verde, con signo de más | RH captura una solicitud a nombre de un colaborador |
| Pestañas | Resumen (activa), Solicitudes, Calendario de equipo, Políticas y saldos | Solo Resumen está diseñada |

### Indicadores

| Tarjeta | Valor | Cálculo |
| --- | --- | --- |
| Solicitudes pendientes | 6 | Solicitudes en espera de la decisión de RH, según los filtros |
| Personas de vacaciones hoy | 4 | Colaboradores con vacaciones aprobadas que incluyen la fecha de hoy |
| Días por vencer | 18 | Suma de días de vacaciones que caducan dentro del plazo de alerta |
| Cobertura de equipo | 92% | Colaboradores disponibles entre colaboradores programados |

### Solicitudes por resolver

Tabla con enlace "Ver todas" y 6 columnas: Colaborador (foto, nombre, área), Periodo (inicio y fin), Días, Saldo disp., Cobertura (foto, nombre y área de quien cubre) y Acciones.

| Colaborador | Área | Periodo | Días | Saldo disp. | Cobertura |
| --- | --- | --- | --- | --- | --- |
| Sofía Herrera | Diseño | 15 – 19 sep 2026 | 5 | 12 | Diego López (Diseño) |
| Carlos Ramírez | Ventas | 21 – 25 sep 2026 | 5 | 8 | Mariana Cruz (Ventas) |
| Valeria Torres | Finanzas | 28 sep – 2 oct 2026 | 5 | 4 | Luis Mendoza (Finanzas) |
| Jorge Sánchez | Operaciones | 10 – 14 sep 2026 | 5 | 15 | Paola Ruiz (Operaciones) |
| Daniela Núñez | Recursos Humanos | 22 – 26 sep 2026 | 5 | 6 | Ana Lucía M. (RH) |
| Emilio Garza | TI | 29 sep – 3 oct 2026 | 5 | 10 | Ricardo Solís (TI) |

Cada fila tiene dos botones: **Aprobar** (verde) y **Rechazar** (contorno rojo). La fila de Valeria Torres pide 5 días con saldo de 4: el sistema necesita una regla para solicitudes que exceden el saldo.

### Calendario de equipo

| Elemento | Visible en la imagen | Comportamiento |
| --- | --- | --- |
| Leyenda | Aprobadas (verde), Pendientes (amarillo), Permiso (azul), Incapacidad (rojo) | Cuatro tipos de ausencia con color fijo |
| Navegador de mes | Flecha anterior, "Septiembre 2026", flecha siguiente | Cambia el mes del calendario |
| Alerta | "Cobertura baja · Equipo Norte · 3 personas ausentes" con "Ver detalle" | Se dispara cuando un equipo cae bajo su cobertura mínima |
| Columnas | L, M, X, J, V, S, D | Semana de lunes a domingo |
| Filas | 7 colaboradores con foto, nombre y área | Una fila por persona |
| Barras | "Vacaciones" (3), "Solicitud pendiente" (2), "Permiso" (1), "Incapacidad" (1) | Cada barra abarca los días de la ausencia; al hacer clic abre el detalle (implícito) |

La cuadrícula mezcla dos diseños: las filas son personas, pero cada fila muestra una semana distinta del mes. Hay que elegir uno: línea de tiempo por colaborador o calendario mensual.

### Saldos y alertas

Tres tarjetas con enlace "Ver todos".

| Colaborador | Área | Texto | Barra | Ícono |
| --- | --- | --- | --- | --- |
| Ana Lucía Martínez | Marketing | 8 días disponibles | Verde; referencia "20 días" | Palmera |
| Carlos Ramírez | Ventas | 2 días por vencer | Amarilla; "2 / 20 días" | Advertencia |
| Sofía Herrera | Diseño | 12 días disponibles | Verde; "12 / 20 días" | Palmera |

### Próximos vencimientos

Tabla con enlace "Ver todos" y columnas Colaborador, Días por vencer y Fecha límite.

| Colaborador | Días por vencer | Fecha límite | Etiqueta |
| --- | --- | --- | --- |
| Carlos Ramírez | 2 días | 30 sep 2026 | "Vence en 12 días" |
| Valeria Torres | 4 días | 5 oct 2026 | Sin etiqueta |
| Daniela Núñez | 6 días | 7 oct 2026 | Sin etiqueta |

### Botones y acciones

| Control | Qué hace | Regla |
| --- | --- | --- |
| Aprobar (6) | Aprueba el paso de RH | Revalida saldo y traslapes al momento de aprobar |
| Rechazar (6) | Rechaza la solicitud | Motivo obligatorio |
| Nueva solicitud | Alta por parte de RH | Sigue la misma regla de aprobación o se registra como aprobada directa; por definir |
| Ver todas | Abre la pestaña Solicitudes | Conserva los filtros |
| Ver detalle | Abre el detalle de cobertura baja | Lista de ausentes del equipo y fechas |
| Ver todos (2) | Abre la pestaña Políticas y saldos | Uno ordenado por saldo, otro por vencimiento |
| Flechas del calendario | Mes anterior y siguiente | Independientes del filtro de mes, o sincronizadas con él; por definir |

## 8. Pantalla: Solicitar vacaciones en el portal del colaborador

Imagen: Solicitud_de_Vacaciones.png. El colaborador elige fechas, ve su saldo antes y después, conoce quién aprobará y envía la solicitud.

### Marco y encabezado

| Elemento | Visible en la imagen | Comportamiento |
| --- | --- | --- |
| Menú lateral | Inicio, Mi asistencia, Mis vacaciones (activa), Mi expediente | Menú del rol colaborador |
| Campana | Punto verde | Avisos de aprobación o rechazo |
| Menú de usuario | Foto y "Ana Lucía Martínez" con desplegable | Perfil y cierre de sesión |
| Título | "Solicitar vacaciones" y "Planifica tu descanso y envía tu solicitud" | Fijo |

### Tu saldo disponible

| Dato | Valor | Origen |
| --- | --- | --- |
| Días disponibles | 8 | Saldo vigente del colaborador |
| Días que vencen | "2 días vencen en diciembre" | Días con fecha límite más cercana y el mes en que caducan |

### Selecciona tus fechas

| Elemento | Visible en la imagen | Comportamiento |
| --- | --- | --- |
| Indicador de saldo | "Tienes saldo suficiente" con palomita verde | Cambia a aviso de saldo insuficiente cuando los días pedidos superan el saldo |
| Calendario | "Septiembre 2026" con flechas de mes anterior y siguiente; columnas Do, Lu, Ma, Mi, Ju, Vi, Sá | Selección de rango con dos clics |
| Días seleccionados | 15 y 19 en verde oscuro; 16, 17 y 18 en verde claro | Extremos y días intermedios del rango |
| Días no hábiles | Columnas de domingo y sábado en gris | No se descuentan del saldo |
| Días de otro mes | 30 y 31 de agosto, 1 a 3 de octubre en gris tenue | Visibles pero fuera del mes |
| Leyenda | "Días seleccionados", "Fin de semana", "Otro mes" | Explica los tres colores |
| Nota | "Los días de descanso y festivos no se descuentan." | Regla de cálculo de días hábiles |
| Fecha de inicio | "15/09/2026" con ícono de calendario y desplegable | Sincronizada con el calendario en ambos sentidos |
| Fecha de término | "19/09/2026" con ícono de calendario y desplegable | No puede ser anterior a la fecha de inicio |
| Días a utilizar | "5 días hábiles", campo en gris | Calculado; no editable |
| Cobertura o entrega de actividades (opcional) | Área de texto: "Ej. Entregar pendientes a mi responsable..." | Texto libre |
| Comentario (opcional) | Área de texto: "Ej. Viaje familiar..." | Texto libre visible para los aprobadores |

### Resumen de solicitud

| Dato | Valor |
| --- | --- |
| Periodo | 15–19 de septiembre de 2026 |
| Días | 5 días hábiles |
| Saldo después de esta solicitud | 3 días |

### Ruta de aprobación

Texto "Tu solicitud será revisada por:" seguido de dos pasos: "Ana Lucía Martínez, Supervisora" → "Recursos Humanos". La ruta se calcula con la regla del tipo de solicitud y el organigrama, antes de enviar. Aquí la solicitante aparece como su propia revisora; ver sección 15.

### Mis solicitudes

Lista con enlace "Ver todas" y dos filas, cada una con flecha para abrir el detalle:

- "Vacaciones aprobadas", 10–12 junio, ícono de palomita verde.
- "Solicitud enviada", 15–19 septiembre, ícono de reloj.

### Botones y validaciones

| Control | Qué hace | Validación en el servidor |
| --- | --- | --- |
| Enviar solicitud | Crea la solicitud y avisa al primer aprobador | Saldo suficiente, sin traslape con otra solicitud o ausencia, fechas futuras, días hábiles mayores que cero |
| Flechas del calendario | Cambian de mes | Conservan el rango seleccionado |
| Fecha de inicio y de término | Abren un selector de fecha | Recalculan días y saldo posterior |
| Ver todas | Abre el historial de solicitudes del colaborador | Solo las propias |
| Fila de solicitud | Abre el detalle con estado y pasos | Permite cancelar mientras esté pendiente (implícito) |

El campo de cobertura es texto libre aquí, pero el portal RH muestra una persona en la columna Cobertura. Para que coincidan, este campo debe permitir elegir a un colaborador.

## 9. Pantalla: Organización y aprobaciones

Imagen: Organigrama_Autorizaciones.png. Vive en Configuración y define tres cosas: quién reporta a quién, quién aprueba cada tipo de solicitud y quién sustituye temporalmente a un aprobador.

### Encabezado

| Elemento | Visible en la imagen | Comportamiento |
| --- | --- | --- |
| Menú lateral | 8 opciones; Configuración activa | Solo para roles con permiso de configuración |
| Campana y usuario | Punto verde; "Mariana Torres", "RH" | Igual que en el resto del portal |
| Título | "Organización y aprobaciones" | Fijo |
| Filtro de sucursal | "Sucursal Monterrey" | Muestra el organigrama de esa sucursal |
| Botón Editar estructura | Contorno, con lápiz | Activa el modo edición: mover nodos y cambiar supervisor |
| Botón Agregar colaborador | Verde, con signo de más | Abre el alta de colaborador con su supervisor |
| Pestañas | Organigrama (activa), Flujos de aprobación | La segunda no está diseñada |

### Organigrama de la empresa

Subtítulo: "Visualiza la estructura de tu organización y las líneas de reporte." Controles: zoom "100%" con desplegable y botón "Expandir todo".

| Nivel | Persona | Puesto o área | Reportes directos | Reporta a |
| --- | --- | --- | --- | --- |
| 1 | Carolina Guajardo | Dirección general | 2 | Nadie |
| 2 | Mariana Torres | Recursos Humanos | 0 | Carolina Guajardo |
| 2 | Carlos Méndez | Gerente de Operaciones | 1 | Carolina Guajardo |
| 3 | Ana Lucía Martínez | Supervisora de Limpieza | 3 | Carlos Méndez |
| 4 | Sofía Herrera | Personal de Limpieza | 0 | Ana Lucía Martínez |
| 4 | Jorge López | Personal de Limpieza | 0 | Ana Lucía Martínez |
| 4 | Diego Ramírez | Personal de Limpieza | 0 | Ana Lucía Martínez |

Cada nodo muestra foto, nombre, puesto y una etiqueta con el número de reportes directos. El nodo raíz y los nodos con reportes llevan la etiqueta en verde; los nodos sin reportes, en gris. Al hacer clic en un nodo se abre la ficha del colaborador (implícito).

### Reglas de aprobación

Subtítulo: "Define quién debe aprobar cada tipo de solicitud." Cada regla tiene ícono, nombre, pasos en orden, estado y menú de tres puntos.

| Tipo de solicitud | Paso 1 | Paso 2 | Estado |
| --- | --- | --- | --- |
| Vacaciones | Supervisor directo | Recursos Humanos | Activa |
| Permisos | Supervisor directo | Recursos Humanos | Activa |
| Incidencias de asistencia | Supervisor directo | Sin segundo paso | Activa |
| Solicitud del supervisor | Gerente | Recursos Humanos | Activa |

El menú de tres puntos no muestra su contenido; propuesto: editar pasos, activar o desactivar, duplicar. Los aprobadores se definen por relación (supervisor directo, gerente) o por área (Recursos Humanos), no por persona fija.

### Delegaciones vigentes

Subtítulo: "Personas que tienen aprobaciones delegadas temporalmente." Botón "Nueva delegación" y tabla con columnas De, Hacia, Periodo y Tipo(s), más menú de tres puntos por fila.

| De | Hacia | Periodo | Tipo(s) |
| --- | --- | --- | --- |
| Carlos Méndez, Gerente de Operaciones | Mariana Torres, Recursos Humanos | 15 – 19 sep 2026 | Vacaciones y permisos |

### Alerta de estructura

Banner rojo: "2 colaboradores sin supervisor asignado. Estos colaboradores no tienen un jefe directo definido, por lo que no podrán ser parte de los flujos de aprobación." El botón rojo "Revisar" abre la lista de esos colaboradores para asignarles supervisor.

### Botones y acciones

| Control | Qué hace | Regla |
| --- | --- | --- |
| Editar estructura | Cambia líneas de reporte | Impide ciclos: nadie puede reportar a un subordinado suyo |
| Agregar colaborador | Alta con supervisor asignado | Supervisor obligatorio, salvo el nodo raíz |
| Zoom 100% | Cambia la escala del organigrama | Solo visual |
| Expandir todo | Despliega todos los niveles | Alterna con contraer |
| Menú de regla (4) | Edita o desactiva la regla | Una regla inactiva deja sin ruta a ese tipo de solicitud; debe advertirlo |
| Nueva delegación | Crea una delegación con De, Hacia, Periodo y Tipo(s) | Sin traslape de periodo para el mismo delegante y tipo |
| Menú de delegación | Edita o termina la delegación | Terminar antes de tiempo regresa los pendientes al titular |
| Revisar | Lista colaboradores sin supervisor | El banner desaparece cuando el conteo llega a cero |

El sistema debe bloquear o desviar a RH toda solicitud de un colaborador sin supervisor, porque su regla no puede resolverse.

## 10. App móvil del colaborador

Imágenes: App_Movil.png, Solicitar_vacaciones.png y Registro_de_invitados.png. La app tiene 4 vistas diseñadas y una barra inferior fija con Inicio, Asistencia, Solicitudes y Perfil.

### Vista 1: Inicio

| Elemento | Visible en la imagen | Comportamiento |
| --- | --- | --- |
| Encabezado | Logo "WOW! Time" y campana | La campana abre las notificaciones |
| Saludo | "Buenos días, Ana Lucía" | Cambia según la hora: días, tardes, noches |
| Fecha | "Jueves, 11 de septiembre" | Fecha del día |
| Avatar | Círculo verde con iniciales "AM" | Abre Perfil (implícito) |
| Lema | "Un equipo más limpio hace grandes días" | Texto de marca configurable |
| Botón Registrar entrada | Verde, grande, con reloj, hora "07:58" y flecha | Abre Registrar asistencia; después de la entrada debe ofrecer Registrar salida |
| Tarjeta de jornada | "Hoy trabajas: Home office · 08:00 – 17:00" | Modalidad y horario del día según esquema o asignación temporal |
| Acceso rápido Solicitar vacaciones | Tarjeta con palmera y flecha | Abre Solicitar vacaciones |
| Acceso rápido Registrar invitado | Tarjeta con ícono de persona y flecha | Abre Registrar invitado |
| Sección Mi asistencia | Enlace "Ver detalles" | Abre el historial de asistencia |
| Tarjeta de estado | "En horario" y "Todo en orden" | Estado de la asistencia del día |
| Tarjeta de saldo | "8 días disponibles de vacaciones" | Saldo vigente |
| Barra inferior | Inicio activa | Navegación |

### Vista 2: Registrar asistencia

| Elemento | Visible en la imagen | Comportamiento |
| --- | --- | --- |
| Flecha de regreso y título | "Registrar asistencia" | Regresa a Inicio |
| Reloj | "07:58" y "Jueves, 11 de septiembre" | Hora de referencia; la hora oficial la pone el servidor |
| Tarjeta de ubicación | "Ubicación validada" y "Home office autorizado" con palomita | Resultado de comparar la posición con el área autorizada del día |
| Mapa | Círculo verde de geocerca con pin de casa; globo "Tu ubicación está dentro del área autorizada" | Muestra posición y radio autorizado |
| Botón de centrar | Flecha de ubicación sobre el mapa | Vuelve a leer la posición |
| Fila de evidencia | "Agregar evidencia (foto)" y "Solo si tu política lo requiere", con flecha | Abre la cámara; obligatoria solo si la política lo marca |
| Botón Confirmar entrada | Verde, ancho completo | Envía la checada con posición, precisión y foto |
| Nota de privacidad | "Se registrará ubicación solo en este momento" | La app no rastrea en segundo plano |
| Barra inferior | Asistencia activa | Navegación |

### Vista 3: Registrar invitado

Aparece en dos imágenes con diferencias de detalle.

| Elemento | App_Movil.png | Registro_de_invitados.png |
| --- | --- | --- |
| Encabezado | Flecha de regreso y título | Logo, lema "Personas que hacen grandes días", campana, avatar y frase "Un lugar seguro para grandes encuentros" |
| Subtítulo | No tiene | "Comparte los datos del visitante para agilizar su ingreso." |
| Nombre completo | Ejemplo: "Ej. Carlos López Ramírez" | Ejemplo: "Ej. Carlos Andrés Ramírez", con ícono de persona |
| Empresa o motivo | Ejemplo: "Ej. Cliente / Entrevista / Mantenimiento" | Ejemplo: "Ej. Innovación S.A. / Reunión de trabajo", con ícono de edificio |
| Fecha y hora de llegada | Un solo campo: "11 de septiembre de 2026, 10:00" | Dos campos: fecha "15 abr 2025" y hora "10:00" |
| Anfitrión | "Ana Lucía Martínez" | Foto, "Ana Lucía Martínez", "Anfitrión del visitante" y frase "Recibimos personas, impulsamos oportunidades" |
| Invitación QR temporal | QR de muestra y "Se generará un código único para este visitante." | QR de muestra, "El visitante recibirá un QR para su acceso." y aviso "Válido por tiempo limitado y solo para el acceso a nuestras instalaciones." |
| Botón | "Generar invitación" | "Generar invitación" con ícono de envío |
| Nota al pie | "El visitante recibirá un QR para su acceso" | "El visitante recibirá un QR para su acceso." |
| Barra inferior | Solicitudes activa | Inicio activa |

Reglas: el anfitrión es por defecto el usuario en sesión. El QR real se genera en el servidor al confirmar; el que se ve antes es decorativo. El formulario no pide teléfono ni correo del visitante, así que falta definir por dónde recibe el QR.

### Vista 4: Solicitar vacaciones

| Elemento | Visible en ambas imágenes | Comportamiento |
| --- | --- | --- |
| Encabezado | Flecha y título en App_Movil.png; logo, lema "Tu tiempo, también cuenta" y avatar en Solicitar_vacaciones.png | Dos variantes del mismo encabezado |
| Tarjeta de saldo | "8 días disponibles"; "Saldo actual" o "Tu saldo actual de vacaciones" | Saldo vigente |
| Calendario | "Septiembre 2026" con flechas; semana de lunes a domingo; 15 a 19 seleccionados en verde | Selección de rango tocando inicio y fin |
| Fines de semana | Sábado y domingo en gris en Solicitar_vacaciones.png | No se descuentan |
| Resumen | "5 días hábiles", "15–19 de septiembre de 2026", "Saldo después: 3 días" | Calculado en el servidor |
| Botón Enviar solicitud | Verde, ancho completo; con flecha en el detalle | Crea la solicitud |
| Ruta | "Revisará: Supervisora → RH" | Ruta resuelta con la regla de aprobación |
| Barra inferior | Solicitudes activa | Navegación |

La versión móvil no tiene los campos de cobertura ni de comentario que sí tiene la versión web.

### Estados que la app necesita y no están dibujados

- Ubicación fuera del área autorizada, con opción de reintentar o de registrar con justificación.
- Permiso de ubicación o de cámara denegado.
- Sin conexión al confirmar la entrada.
- Entrada ya registrada: botón de salida y resumen de la jornada.
- Saldo insuficiente o fechas traslapadas al solicitar vacaciones.
- Invitación generada: QR real, vigencia y opción de compartir.
- Listado de Solicitudes, pantalla de Perfil y centro de notificaciones.

## 11. Módulos funcionales y reglas de negocio

Siete módulos comparten un motor de aprobaciones y una bitácora. Estas son las reglas que el servidor debe aplicar, sin importar el canal.

### Asistencia

- **Registro.** Entrada y salida por terminal o por app. La checada solo es válida si el método está permitido para el colaborador.
- **Lugar.** La app compara la posición con el área autorizada del día: la sucursal en días de oficina, el domicilio autorizado en días remotos o de home office.
- **Evidencia.** La foto es obligatoria solo cuando la política del colaborador lo marca.
- **Estado.** En horario si la entrada cae dentro del horario más la tolerancia; Retardo si la supera; Salida registrada al cerrar la jornada. La falta por ausencia de entrada es implícita.
- **Incidencias.** Retardos, faltas, salidas omitidas y registros fuera de área generan una incidencia por revisar. Las resuelve el supervisor directo.
- **Jornada.** El horario del día (ejemplo: 08:00 – 17:00) sale del turno; la modalidad, del esquema o de la asignación temporal.

### Vacaciones y permisos

- **Saldo.** Por colaborador: días otorgados (20 en los ejemplos), usados, disponibles y por vencer con fecha límite.
- **Días hábiles.** El conteo excluye los días de descanso del colaborador y los festivos del calendario laboral.
- **Compromiso de saldo.** Al enviar, los días quedan comprometidos; al aprobar se descuentan; al rechazar o cancelar se liberan.
- **Validaciones.** Saldo suficiente, sin traslape con otra solicitud, ausencia o asignación temporal, y fechas futuras.
- **Tipos de ausencia.** Vacaciones, Permiso e Incapacidad; cada uno con color propio en el calendario de equipo.
- **Cobertura.** La solicitud puede nombrar a quien cubre. El sistema calcula la cobertura por equipo y alerta cuando baja del mínimo.
- **Vencimiento.** Una tarea diaria detecta días próximos a caducar y alimenta "Días por vencer" y "Próximos vencimientos".

### Aprobaciones

Flujo de aprobación de vacaciones, en texto:

1. Enviada: el servidor valida saldo, fechas y traslapes.
2. Paso 1: supervisor directo o delegado vigente. Si aprueba, sigue; si rechaza, termina en Rechazada.
3. Paso 2: Recursos Humanos, decisión final. Si aprueba, termina en Aprobada; si rechaza, termina en Rechazada.
4. Aprobada: descuenta saldo y pasa al calendario. Rechazada: motivo obligatorio, no afecta el saldo.

La ruta de vacaciones tiene dos pasos; un rechazo en cualquiera de ellos termina la solicitud. Las demás reglas cambian los pasos, no el mecanismo.

- **Resolución de ruta.** Tipo de solicitud → regla activa → pasos. "Supervisor directo" se lee del organigrama; "Gerente" es el jefe del supervisor; "Recursos Humanos" es cualquier usuario con ese rol en la sucursal.
- **Solicitante supervisor.** Si quien solicita tiene reportes directos, aplica la regla "Solicitud del supervisor": Gerente → Recursos Humanos.
- **Delegación.** Durante el periodo, los pendientes del delegante llegan al delegado para los tipos indicados. La bitácora guarda a nombre de quién se aprobó.
- **Separación de funciones.** Nadie aprueba su propia solicitud, y una misma persona no debería aprobar dos pasos de la misma solicitud.
- **Sin supervisor.** Un colaborador sin jefe directo no puede iniciar flujos hasta que se le asigne uno.
- **Estados.** Enviada, en revisión por paso, aprobada, rechazada y cancelada por el solicitante.

### Visitantes

- **Invitación.** Nombre completo, empresa o motivo, fecha y hora de llegada, y anfitrión.
- **Código QR.** Identificador único firmado por el servidor, con vigencia limitada alrededor de la hora de llegada y válido solo para el acceso.
- **Acceso.** La lectura del QR registra la hora de entrada, cambia el estado a Dentro y avisa al anfitrión. La salida cierra la visita.
- **Estados.** Dentro es el único visible; esperado, salió, vencida y cancelada son implícitos.
- **Indicador.** "8 de 12" son visitantes dentro entre invitaciones registradas para hoy.

### Expedientes

- **Tipos de documento.** INE, comprobante de domicilio, constancia de situación fiscal, examen médico y constancia de capacitación aparecen en las imágenes. El ejemplo exige 9 por colaborador.
- **Vigencia.** Cada documento puede tener fecha de vencimiento. La etiqueta pasa de Próximo a Urgente según los días restantes.
- **Avance.** Documentos entregados entre documentos requeridos.
- **Carga.** El colaborador sube documentos desde Mi expediente y RH desde la ficha; cada carga queda en la bitácora.

### Colaboradores y organización

- **Ficha.** Datos personales, datos laborales, esquema de trabajo, asignaciones temporales, notas y bitácora.
- **Organigrama.** Un supervisor por colaborador, sin ciclos; la raíz es la única sin jefe.
- **Cumpleaños.** La fecha de nacimiento alimenta el panel del dashboard y el banner de la ficha.

### Notificaciones y mensajes de marca

- **Eventos que avisan.** Solicitud por aprobar, solicitud aprobada o rechazada, documento por vencer, días por vencer, visitante que llegó, cobertura baja y cumpleaños.
- **Canales.** Campana en web, notificación push en móvil y un canal externo para el QR del visitante.
- **Mensajes de marca.** Las imágenes usan 9 frases distintas en menús, encabezados y banners. Conviene un catálogo de frases por ubicación, editable desde Configuración.

## 12. Modelo de datos

Las pantallas piden 31 entidades en 6 grupos. Cada entidad lleva sucursal o colaborador como llave de alcance, y campos de auditoría: quién y cuándo creó y modificó.

### Organización

| Entidad | Campos clave | Evidencia |
| --- | --- | --- |
| Empresa | Nombre, logo, zona horaria | Marca WOW! Time |
| Sucursal | Nombre, dirección, coordenadas, radio de geocerca | "Sucursal Monterrey", "Centro base" |
| Área | Nombre, sucursal | Diseño, Ventas, Finanzas, Operaciones, Recursos Humanos, TI, Marketing |
| Equipo | Nombre, área, cobertura mínima | "Equipo Norte" |
| Puesto | Nombre, nivel, documentos requeridos | Supervisora de Limpieza, Auxiliar, Operador, Gerente de Operaciones |
| Turno | Nombre, hora de entrada y salida, tolerancia, días de descanso | "Turno matutino", "08:00 – 17:00" |
| Día festivo | Fecha, nombre, sucursal o alcance general | "festivos no se descuentan" |

### Personas y acceso

| Entidad | Campos clave | Evidencia |
| --- | --- | --- |
| Colaborador | Número (WC-0248), nombre, foto, fecha de nacimiento, estatus, sucursal, área, equipo, puesto, turno, supervisor | Ficha y organigrama |
| Usuario | Colaborador, credenciales, último acceso | Menú de usuario |
| Rol y permiso | Rol, módulo, acción, sucursales permitidas | Menús distintos por rol |
| Esquema de trabajo | Modalidad, centro base, días en oficina, días remotos, registro permitido, política de evidencia | Tarjeta "Esquema de trabajo" |
| Ubicación autorizada | Colaborador, tipo (domicilio u otro), coordenadas, radio | "Home office autorizado" |
| Asignación temporal | Tipo, fecha de inicio y fin, motivo, estado | Tarjeta "Asignación temporal" |
| Nota | Colaborador, autor, fecha y hora, texto | Tarjeta "Notas" |
| Evento de bitácora | Colaborador, tipo, detalle, fecha y hora, autor | "Actividad reciente", pestaña Historial |

### Asistencia

| Entidad | Campos clave | Evidencia |
| --- | --- | --- |
| Dispositivo | Nombre, tipo (terminal o lector QR), sucursal, estado | "Acceso principal" |
| Registro de asistencia | Colaborador, tipo (entrada o salida), hora del servidor, origen, dispositivo, coordenadas, precisión, foto, estado | Tablas de asistencia y vista móvil |
| Incidencia de asistencia | Colaborador, fecha, tipo, justificación, estado | "Incidencias por revisar" |

### Ausencias y aprobaciones

| Entidad | Campos clave | Evidencia |
| --- | --- | --- |
| Política de vacaciones | Días por antigüedad, plazo de vencimiento, anticipación mínima, trato del saldo insuficiente | Pestaña "Políticas y saldos" |
| Saldo de vacaciones | Colaborador, periodo, días otorgados, usados, comprometidos, fecha límite | "8 días disponibles", "2 / 20 días", "Fecha límite" |
| Solicitud | Tipo, colaborador, fecha de inicio y fin, días hábiles, persona que cubre, nota de cobertura, comentario, estado, paso actual | Formularios y tablas de solicitudes |
| Paso de aprobación | Solicitud, orden, aprobador asignado, quien decidió, en nombre de quién, decisión, motivo, fecha | "Ruta de aprobación", botones Aprobar y Rechazar |
| Regla de aprobación | Tipo de solicitud, pasos ordenados, tipo de aprobador por paso, activa | "Reglas de aprobación" |
| Delegación | Delegante, delegado, fecha de inicio y fin, tipos | "Delegaciones vigentes" |
| Incapacidad | Colaborador, fecha de inicio y fin, documento | Barra "Incapacidad" del calendario |

### Visitantes

| Entidad | Campos clave | Evidencia |
| --- | --- | --- |
| Invitación de visitante | Nombre completo, empresa o motivo, fecha y hora de llegada, anfitrión, sucursal, código QR, vigencia, estado | Formulario "Registrar invitado" |
| Acceso de visitante | Invitación, hora de entrada, hora de salida, dispositivo | Columnas Entrada y Estado |

### Expediente y sistema

| Entidad | Campos clave | Evidencia |
| --- | --- | --- |
| Tipo de documento | Nombre, obligatorio, con vigencia, puestos a los que aplica | INE, Examen médico, Constancia de capacitación |
| Documento | Colaborador, tipo, archivo, fecha de carga, fecha de vencimiento, estado | "8 de 9 documentos", "INE vence en 5 días" |
| Notificación | Destinatario, evento, enlace, leída | Campana con contador |
| Mensaje de marca | Ubicación, texto, vigencia | Lemas y banners |

### Relaciones principales

- Un colaborador pertenece a una sucursal, un área, un equipo y un puesto, y reporta a un solo colaborador.
- Un colaborador tiene un esquema de trabajo vigente y cero o más asignaciones temporales.
- Una solicitud pertenece a un colaborador y tiene de uno a varios pasos de aprobación, generados a partir de su regla.
- Una solicitud de vacaciones aprobada descuenta de un saldo; el saldo nunca se edita a mano sin dejar evento en bitácora.
- Una invitación pertenece a un anfitrión y tiene cero o un acceso.
- Un documento pertenece a un colaborador y a un tipo de documento.

## 13. Configuraciones y catálogos

Solo 3 configuraciones tienen pantalla diseñada: organigrama, reglas de aprobación y delegaciones. Las demás se deducen de lo que muestran las imágenes y necesitan su propia pantalla en Configuración.

### Con pantalla diseñada

| Configuración | Opciones visibles | Efecto |
| --- | --- | --- |
| Organigrama | Editar estructura, Agregar colaborador, zoom, Expandir todo | Define supervisor directo y gerente de cada persona |
| Reglas de aprobación | 4 tipos de solicitud, 1 o 2 pasos, estado Activa | Define la ruta de cada solicitud |
| Delegaciones | De, Hacia, Periodo, Tipo(s) | Redirige aprobaciones por un periodo |

### Catálogos

| Catálogo | Valores en las imágenes |
| --- | --- |
| Sucursales | Sucursal Monterrey |
| Áreas | Diseño, Ventas, Finanzas, Operaciones, Recursos Humanos, TI, Marketing |
| Equipos | Equipo Norte |
| Puestos | Dirección general, Gerente de Operaciones, Supervisora de Limpieza, Supervisor de Área, Personal de Limpieza, Auxiliar de Limpieza, Operador de Limpieza |
| Turnos | Turno matutino, 08:00 – 17:00 |
| Modalidades de trabajo | Híbrido y Home office; presencial es implícita |
| Métodos de registro | Terminal + app móvil; solo terminal y solo app son implícitos |
| Dispositivos | Acceso principal |
| Estatus de colaborador | Activa |
| Tipos de solicitud | Vacaciones, Permisos, Incidencias de asistencia, Solicitud del supervisor |
| Tipos de permiso | Permiso personal |
| Tipos de ausencia | Vacaciones aprobadas, Pendientes, Permiso, Incapacidad |
| Estados de asistencia | En horario, Retardo, Salida registrada |
| Tipos de documento | INE, Comprobante de domicilio, Constancia de situación fiscal, Examen médico, Constancia de capacitación |
| Urgencia de documento | Urgente, Próximo |
| Estados de visitante | Dentro |
| Tipos de aprobador | Supervisor directo, Gerente, Recursos Humanos |
| Tipos de asignación temporal | Home office |

### Parámetros

| Parámetro | Pista en las imágenes | Propuesta de valor inicial |
| --- | --- | --- |
| Tolerancia de entrada | Entrada de 07:15 en horario y de 08:12 con retardo; jornada de 08:00 a 17:00 | 10 minutos |
| Radio de geocerca | Círculo en el mapa | 100 a 200 metros, por ubicación |
| Foto de evidencia | "Solo si tu política lo requiere" | Apagada; se activa por colaborador o por modalidad |
| Días de oficina y remotos | Lunes, miércoles y viernes; martes y jueves | Por colaborador |
| Días de descanso | Fines de semana en gris | Por turno |
| Calendario de festivos | "festivos no se descuentan" | Por año y por sucursal |
| Días de vacaciones por antigüedad | "20 días" como total en los ejemplos | Tabla configurable, con el mínimo de ley como piso |
| Vencimiento de días | "2 días vencen en diciembre", "Fecha límite" | Plazo configurable desde que se otorgan |
| Alerta de días por vencer | "Vence en 12 días" | 30 días antes |
| Saldo insuficiente | Solicitud de 5 días con saldo de 4 | Bloquear, o permitir con marca de excepción; por definir |
| Cobertura mínima por equipo | "Cobertura baja · 3 personas ausentes" | Porcentaje o número máximo de ausentes por equipo |
| Umbral Urgente de documentos | Urgente a 5 días | 7 días o menos |
| Umbral Próximo de documentos | Próximo a 12 y 18 días | 30 días o menos |
| Documentos requeridos | "8 de 9 documentos" | Lista por puesto |
| Vigencia del QR | "Válido por tiempo limitado" | Desde 30 minutos antes hasta 2 horas después de la hora de llegada |
| Canal de envío del QR | "El visitante recibirá un QR" | El anfitrión comparte el enlace desde su teléfono |
| Ventana de cumpleaños | Cumpleaños del 9 al 12 de septiembre vistos el 8 | 7 días hacia adelante |
| Formato del número de colaborador | "WC-0248" | Prefijo y consecutivo de 4 dígitos |
| Mensajes de marca | 9 frases en menús, encabezados y banners | Catálogo por ubicación |
| Inicio de semana | Lunes en móvil y portal RH; domingo en portal colaborador | Uno solo para todo el sistema |

## 14. Servicios y API

Cada control de las pantallas se resuelve con uno de estos 47 servicios. Todas las rutas van bajo `/api/v1`, exigen sesión y filtran por las sucursales permitidas al usuario, salvo la validación del QR.

### Sesión y marco

| Método y ruta | Uso | Pantalla |
| --- | --- | --- |
| POST /auth/login | Inicia sesión y devuelve rol, permisos y sucursales | Todas |
| POST /auth/refresh | Renueva la sesión | Todas |
| GET /me | Datos del usuario, menú permitido y saldo resumido | Menú de usuario, Inicio móvil |
| GET /sucursales | Lista para el selector de sucursal | Dashboard, Vacaciones, Organización |
| GET /notificaciones | Avisos y contador de no leídos | Campana |
| POST /notificaciones/{id}/leida | Marca un aviso como leído | Campana |
| GET /buscar?q= | Búsqueda global | Buscador de la ficha |

### Dashboard

| Método y ruta | Uso | Pantalla |
| --- | --- | --- |
| GET /dashboard/resumen | Los 4 indicadores del día | Dashboard |
| GET /dashboard/asistencia-por-hora | Serie de la gráfica en tiempo real | Dashboard |
| GET /colaboradores/cumpleanos | Próximos cumpleaños | Dashboard, Ficha |
| Canal de eventos /eventos/sucursal/{id} | Empuja nuevas checadas, solicitudes y visitas | Dashboard |

### Colaboradores y organización

| Método y ruta | Uso | Pantalla |
| --- | --- | --- |
| GET /colaboradores | Listado con filtros | Colaboradores |
| POST /colaboradores | Alta | Botón Agregar colaborador |
| GET /colaboradores/{id} | Ficha completa para la pestaña Resumen | Ficha |
| PUT /colaboradores/{id} | Edición | Botón Editar colaborador |
| GET /colaboradores/{id}/actividad | Bitácora paginada | Actividad reciente, Historial |
| GET y POST /colaboradores/{id}/notas | Lista y alta de notas | Tarjeta Notas |
| PUT y DELETE /notas/{id} | Edición y baja de una nota | Menú de tres puntos |
| GET y POST /colaboradores/{id}/asignaciones | Asignaciones temporales | Botón Ver asignaciones |
| GET /organigrama | Árbol de reporte de la sucursal | Organigrama |
| PUT /colaboradores/{id}/supervisor | Cambia la línea de reporte | Editar estructura, Revisar |
| GET /colaboradores?sinSupervisor=true | Colaboradores sin jefe directo | Banner de alerta |

### Asistencia

| Método y ruta | Uso | Pantalla |
| --- | --- | --- |
| GET /asistencia/jornada-hoy | Modalidad, horario y área autorizada del día | Inicio móvil, Registrar asistencia |
| POST /asistencia/validar-ubicacion | Compara coordenadas con el área autorizada | Tarjeta "Ubicación validada" |
| POST /asistencia/registros | Registra entrada o salida con coordenadas y foto | Botón Confirmar entrada, terminal |
| GET /asistencia/registros | Registros por fecha, sucursal o colaborador | Dashboard, Ficha, Mi asistencia |
| GET /asistencia/incidencias | Incidencias por revisar | Indicador del dashboard |
| POST /asistencia/incidencias/{id}/resolver | Justifica o confirma una incidencia | Bandeja del supervisor |

### Vacaciones, permisos y aprobaciones

| Método y ruta | Uso | Pantalla |
| --- | --- | --- |
| GET /vacaciones/saldo | Saldo, días por vencer y fecha límite | Tarjetas de saldo en web y móvil |
| POST /solicitudes/simular | Devuelve días hábiles, saldo posterior, validaciones y ruta de aprobación | Resumen de solicitud, "Revisará" |
| POST /solicitudes | Crea la solicitud | Botón Enviar solicitud, Nueva solicitud |
| GET /solicitudes | Listado por estado, tipo, periodo, área o colaborador | Solicitudes por resolver, Mis solicitudes |
| GET /solicitudes/{id} | Detalle con pasos y bitácora | Fila de solicitud |
| POST /solicitudes/{id}/aprobar | Aprueba el paso actual | Botón Aprobar |
| POST /solicitudes/{id}/rechazar | Rechaza con motivo | Botón Rechazar |
| POST /solicitudes/{id}/cancelar | Cancela una solicitud pendiente | Detalle de solicitud |
| GET /vacaciones/resumen | Los 4 indicadores de Vacaciones | Portal RH |
| GET /ausencias/calendario | Ausencias por colaborador y día, con alertas de cobertura | Calendario de equipo |
| GET /vacaciones/vencimientos | Días por vencer por colaborador | Saldos y alertas, Próximos vencimientos |
| GET y PUT /aprobaciones/reglas | Reglas por tipo de solicitud | Reglas de aprobación |
| GET, POST y DELETE /aprobaciones/delegaciones | Delegaciones vigentes | Nueva delegación |

### Visitantes y expedientes

| Método y ruta | Uso | Pantalla |
| --- | --- | --- |
| POST /visitantes/invitaciones | Crea la invitación y devuelve el QR | Botón Generar invitación |
| GET /visitantes/invitaciones | Invitaciones por fecha, sucursal o anfitrión | Visitantes de hoy |
| POST /visitantes/acceso | Valida el QR y registra entrada o salida | Lector QR |
| GET /expedientes/por-vencer | Documentos próximos a vencer | Documentos por vencer |
| GET /colaboradores/{id}/documentos | Documentos y avance del expediente | Expediente digital, Mi expediente |
| POST /colaboradores/{id}/documentos | Carga un documento con su vigencia | Expediente |

`POST /solicitudes/simular` es la pieza que mantiene iguales a web y móvil: ningún cliente cuenta días ni arma la ruta por su cuenta.

## 15. Inconsistencias detectadas en los mockups

Las imágenes se contradicen en 16 puntos. Los primeros 7 afectan reglas de negocio y deben resolverse antes de programar; los demás son de diseño o de datos de ejemplo.

### Afectan reglas de negocio

| # | Dónde | Qué se contradice | Resolución propuesta |
| --- | --- | --- | --- |
| 1 | Solicitar vacaciones, web y móvil | El 15 al 19 de septiembre de 2026 va de martes a sábado, y el miércoles 16 es festivo oficial en México. Con semana de lunes a viernes son 3 días hábiles, no 5, y el saldo posterior sería 5, no 3. Choca con la nota "Los días de descanso y festivos no se descuentan" | Usar como ejemplo del 21 al 25 de septiembre, o mostrar 3 días. El conteo siempre lo hace el servidor |
| 2 | Solicitud web, "Ruta de aprobación" | Ana Lucía Martínez solicita y aparece como su propia revisora | Para quien tiene reportes directos aplica "Solicitud del supervisor": Carlos Méndez → Recursos Humanos |
| 3 | Móvil, "Revisará: Supervisora → RH" | La usuaria es la supervisora; su ruta debería decir Gerente → RH | El texto sale de la ruta calculada, no de un texto fijo |
| 4 | Ficha de colaborador | Asignación temporal "Home office" del 15 al 19 de septiembre y vacaciones en las mismas fechas. Además, el motivo "Apoyo por inventario en sucursal" no corresponde a home office | Validar traslapes entre vacaciones y asignaciones; corregir el ejemplo |
| 5 | Ficha, portal colaborador y formulario | La solicitud del 15 al 19 aparece como aprobada en la bitácora, como "Próxima solicitud" en la tarjeta, como "Solicitud enviada" en Mis solicitudes y aún en captura en el formulario | Un solo estado por solicitud; bloquear una segunda solicitud con fechas traslapadas |
| 6 | Delegaciones vigentes | Carlos Méndez delega en Mariana Torres, que es de RH: la misma persona aprobaría el paso 1 y el paso 2 | Regla de separación: el paso final lo toma otro usuario de RH, o se registra una sola aprobación con aviso |
| 7 | Solicitudes por resolver | Valeria Torres pide 5 días con saldo de 4 | Definir si se bloquea al enviar o se permite como excepción visible para RH |

### De diseño y de datos de ejemplo

| # | Dónde | Qué se contradice | Resolución propuesta |
| --- | --- | --- | --- |
| 8 | Saldos y alertas contra Solicitudes por resolver | Carlos Ramírez tiene saldo de 8 días en la tabla y "2 / 20 días" en la tarjeta | Definir qué muestra la fracción: disponibles, usados o por vencer |
| 9 | Calendarios | La semana empieza en lunes en móvil y portal RH, y en domingo en el portal del colaborador. El miércoles es "X" en dos pantallas y "M" en otra | Un solo componente de calendario con inicio en lunes |
| 10 | Fechas de ejemplo | La ficha usa 2024, el dashboard y el invitado usan 2025, y el resto 2026. "Jueves, 11 de septiembre" es viernes en 2026 | Unificar todos los ejemplos en 2026 |
| 11 | Personas de ejemplo | Ana Lucía Martínez aparece en Marketing, en RH y como Supervisora de Limpieza. Sofía Herrera, en Diseño y en Limpieza. Carolina, como Dirección general y como Recursos Humanos | Un solo juego de datos de prueba para todas las pantallas |
| 12 | Calendario de equipo | Las filas son personas, pero cada fila muestra una semana distinta. Las barras no coinciden con la tabla: Sofía aprobada del 15 al 17 contra pendiente del 15 al 19 | Elegir línea de tiempo por colaborador o calendario mensual |
| 13 | Registrar invitado | Fecha y hora en un campo o en dos; pestaña activa Inicio o Solicitudes; el QR se ve antes de generarlo | Dos campos, pestaña Inicio y QR solo después de generar |
| 14 | Encabezado móvil | Una variante con logo y lema; otra con flecha de regreso y título | Flecha y título en pantallas internas; logo solo en Inicio |
| 15 | Menú lateral e identidad | Configuración falta en una pantalla; el logo lleva ícono de reloj solo en el dashboard; los íconos de Asistencia y Vacaciones cambian entre pantallas | Un solo menú, un solo logo y un solo juego de íconos |
| 16 | Cobertura de la solicitud | Texto libre en la web, persona con foto en el portal RH, y ausente en móvil | Selector de colaborador más nota opcional, en web y en móvil |

El indicador "Colaboradores activos: 124 de 160" también necesita definición: por los demás números parece contar presentes del día, no personal con estatus activo.

## 16. Pantallas por diseñar y decisiones pendientes

Los botones, pestañas y enlaces de las 8 imágenes apuntan a más de 30 pantallas y diálogos que todavía no existen. El sistema no queda completo sin ellas.

### Pantallas por diseñar

| Módulo | Pantalla o diálogo | Qué la pide |
| --- | --- | --- |
| Acceso | Inicio de sesión y recuperación de contraseña | Todos los canales |
| Acceso | Permisos de ubicación, cámara y notificaciones en la app | Registrar asistencia |
| Marco | Centro de notificaciones | Campana |
| Marco | Menú de usuario y perfil | Flecha junto al nombre; pestaña Perfil en móvil |
| Marco | Resultados del buscador global | "Buscar colaboradores, reportes, etc." |
| Colaboradores | Listado con filtros | Opción del menú y ruta de navegación |
| Colaboradores | Alta y edición | Botones Agregar colaborador y Editar colaborador |
| Colaboradores | Pestañas Datos personales, Datos laborales, Asistencia, Vacaciones, Expediente e Historial | Pestañas de la ficha y enlaces "Ver detalle" |
| Colaboradores | Menú Más acciones | Botón de la ficha |
| Colaboradores | Asignaciones temporales: lista y alta | Botón Ver asignaciones |
| Colaboradores | Alta y edición de nota | Botón Agregar nota y menú de tres puntos |
| Asistencia | Registros e incidencias para RH | Opción del menú; "Ver todos"; "Incidencias por revisar" |
| Asistencia | Mi asistencia en web e historial en móvil | Menú del colaborador; "Ver detalles" |
| Asistencia | Registrar salida y estados de error de ubicación | Botón Registrar entrada |
| Asistencia | Captura de foto de evidencia | "Agregar evidencia (foto)" |
| Vacaciones | Pestañas Solicitudes, Calendario de equipo y Políticas y saldos | Pestañas del portal RH |
| Vacaciones | Detalle de solicitud con pasos y bitácora | Filas de solicitud |
| Vacaciones | Diálogos de confirmar aprobación y de rechazar con motivo | Botones Aprobar y Rechazar |
| Vacaciones | Nueva solicitud capturada por RH | Botón Nueva solicitud |
| Vacaciones | Detalle de cobertura baja | Enlace "Ver detalle" de la alerta |
| Vacaciones | Solicitud de permiso | Regla "Permisos" y tipo "Permiso personal" |
| Aprobaciones | Bandeja de aprobaciones del supervisor y del gerente | Paso "Supervisor directo" de las reglas |
| Visitantes | Listado y detalle para RH | Opción del menú; "Ver todos" |
| Visitantes | Invitación generada con QR real y opción de compartir | Botón Generar invitación |
| Visitantes | Validación del QR en el acceso, entrada y salida | Estado "Dentro" |
| Expedientes | Listado general y documentos por vencer | Opción del menú; "Ver todos" |
| Expedientes | Carga de documento y Mi expediente | Alerta "INE vence en 5 días"; menú del colaborador |
| Reportes | Módulo completo | Opción del menú |
| Configuración | Pestaña Flujos de aprobación, edición de regla, Nueva delegación, modo Editar estructura y lista de colaboradores sin supervisor | Controles de Organización y aprobaciones |
| Configuración | Catálogos y parámetros de la sección 13 | Datos que las pantallas muestran |

### Decisiones pendientes

1. ¿Qué pasa cuando los días pedidos superan el saldo: se bloquea o se permite como excepción?
2. ¿La semana laboral es de lunes a viernes, o incluye sábado para algunos turnos?
3. ¿Qué tabla de días por antigüedad y qué plazo de vencimiento aplican?
4. ¿Dónde aprueba el supervisor: en web, en la app o en ambas?
5. ¿Puede una persona de RH aprobar dos pasos cuando además recibe una delegación?
6. ¿Las solicitudes que captura RH siguen la regla de aprobación o entran aprobadas?
7. ¿Cómo recibe el visitante su QR, si el formulario no pide teléfono ni correo?
8. ¿Quién valida el QR en el acceso y con qué equipo: lector fijo, tableta o teléfono?
9. ¿Qué terminal de asistencia se integrará y por qué medio entrega sus registros?
10. ¿Un registro fuera del área autorizada se bloquea o se acepta como incidencia?
11. ¿La foto de evidencia se exige por colaborador, por modalidad o por sucursal?
12. ¿La app debe permitir registrar asistencia sin conexión?
13. ¿Cómo se calcula la cobertura de equipo y cuál es el mínimo por equipo?
14. ¿El calendario de equipo será línea de tiempo por colaborador o calendario mensual?
15. ¿Qué reportes se necesitan y en qué formato se exportan?
16. ¿Las incidencias y ausencias deben enviarse a un sistema de nómina?
17. ¿El sistema atenderá a una sola empresa con varias sucursales, o a varias empresas?
