# Guía del front (sistema visual y convenciones)

Esta guía sirve para que cualquier pantalla nueva se vea igual que las que tienen mockup. Las imágenes de referencia están en `docs/mockups/`.

## Estructura

```
frontend/src/
  api.ts                 cliente de la API (api.get/post/put/del/subir/descargar, urlArchivo, ErrorApi, tipos Pagina y Persona)
  router.ts              todas las rutas; cada página vive en src/paginas/<ruta>.vue
  stores/sesion.ts       useSesion(): me, sucursalId, sucursal, tiene(...permisos), esGestion, esRecepcion, frase(ubicacion)
  stores/ui.ts           useUi(): avisar(texto), error(e), confirmar({ titulo, mensaje, aceptar, peligro, pedirTexto })
  utils/formato.ts       fechas y números en es-MX: fechaLarga, fechaMedia, fechaCorta, fechaNum, rango, rangoLargo, hora, momento, hace, num, dias, iso, mesAnio, saludo
  utils/usos.ts          useCarga(fn, { porSucursal }), useAccion() → { ocupado, ejecutar(fn, 'Mensaje de éxito') }, useEsMovil(), useEventos(fn), debounce
  layout/Marco.vue       menú lateral, barra superior, barra inferior en teléfono (autoservicio)
  layout/EncabezadoMovil.vue  barra azul con flecha y título para pantallas internas en teléfono
  componentes/           Icono, Avatar, Persona, Estado, Modal, Paginacion, Calendario, Mapa, SelectorColaborador, Qr, LectorQr, LineaTiempo
  estilos/base.css       tokens y clases globales
```

## Reglas

1. **Nada de datos fijos**: todo sale de la API. Sin botones decorativos ni pantallas "próximamente".
2. **Textos en español de México**, con acentos, en el tono de los mockups ("Ver todos", "Ver detalle", "Aprobar", "Rechazar").
3. **Sucursal**: en pantallas de gestión, toda consulta manda `sucursalId: s.sucursalId` y se recarga al cambiarla (`useCarga(fn, { porSucursal: true })`).
4. **Permisos**: mostrar u ocultar acciones con `s.tiene('permiso')`; el servidor valida de todos modos.
5. **Errores**: `useAccion().ejecutar(...)` muestra el mensaje en español que manda el servidor.
6. **Responsivo**: a 375 px no debe haber desplazamiento horizontal de la página (las tablas van dentro de `.tabla-envoltura`). En autoservicio y teléfono (`useEsMovil()`), la página empieza con `<EncabezadoMovil titulo="…" />` y el contenido lleva `padding: 16px`.
7. **Íconos**: solo `<Icono n="…" />` (ver nombres en `componentes/Icono.vue`). No agregar otras librerías de íconos.

## Página de escritorio

```vue
<div class="encabezado">            <!-- display:flex; justify-content:space-between -->
  <div>
    <h1 class="titulo-pagina">Título</h1>
    <p class="subtitulo-pagina">Subtítulo</p>
  </div>
  <div class="fila"> botones </div>
</div>
<nav class="pestanas"> <button class="pestana activa">…</button> </nav>
<section class="tarjeta"> <div class="tarjeta-cabecera"><h2 class="titulo-tarjeta">…</h2><RouterLink class="ver-todo">Ver todos</RouterLink></div> … </section>
```

- Separación entre tarjetas: 20 px. Rejillas con `display:grid; gap:20px`.
- Botones: `btn btn-primario` (verde), `btn btn-contorno`, `btn btn-rechazar` (contorno rojo), `btn btn-peligro`, tamaños `btn-ch` y `btn-g`.
- Etiquetas de estado: `<Estado v="EnHorario" />` (colores fijos por valor) o `<span class="etiqueta verde|naranja|ambar|azul|rojo|gris"><span class="punto"/>Texto</span>`.
- Tablas: `<div class="tabla-envoltura"><table class="tabla">…</table></div>`; encabezado gris claro, filas de 48–56 px. Persona en tabla: `<Persona :p="fila.colaborador" />`.
- Formularios: `.campo > label + .entrada`, rejilla `.rejilla-form`.
- Indicadores: `.kpi`, `.kpi-icono` (círculo de 60 px con fondo claro del color), `.kpi-valor`, `.barra > span`.
- Calendario: siempre `<Calendario>` (lunes a domingo, L M X J V S D).
