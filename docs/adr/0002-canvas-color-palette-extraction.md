# ADR 0002: Extraccion de paleta de color en cliente via Canvas API

Date: 2026-06-10
Status: Accepted
Builds on: ADR 0001

## Context

`PictureCardComponent` debe mostrar una paleta de colores dominantes derivada de cada imagen APOD, y el texto que se renderiza sobre esa paleta debe mantener contraste suficiente (WCAG AA). El brief tecnico incluye Canvas API como tecnologia clave, ejecutada en el cliente sin dependencias externas.

Las imagenes APOD provienen de dominios externos (NASA/ESA/Hubble), lo que puede activar restricciones CORS al leer pixeles via `getImageData`.

## Decision

`ColorPaletteComponent` implementa la extraccion de la siguiente forma:

- Recibe la URL de la imagen activa como input.
- Dibuja la imagen (reducida, ej. 50x50) en un `<canvas>` (o `OffscreenCanvas` si esta disponible) con `crossOrigin = "anonymous"`.
- Lee pixeles via `getImageData` y cuantiza/agrupa colores para devolver N (ej. 4-5) colores dominantes.
- Si `getImageData` lanza `SecurityError` (canvas "tainted" por CORS) o la imagen falla al cargar, se usa una **paleta de fallback fija** (tokens `space.surface-hi` / `accent`) y se loggea el caso sin romper la UI.
- El componente expone los colores via `output()`/signal para que `PictureCardComponent` los use en el fondo/acentos de la card.
- El contraste del texto sobre cada color de la paleta se valida (formula de contraste WCAG) antes de usarlo como fondo de texto; si no alcanza AA, se usa el color de texto secundario/terciario del token set en vez de blanco/negro fijo.

Toda la logica vive en `src/app/components/color-palette/` (o `src/app/utils/palette/` para la funcion pura de cuantizacion), separada de la UI para poder testearla con imagenes fixture sin DOM completo.

## Alternatives Considered

1. Libreria externa (`color-thief`, `vibrant.js`, etc.) - rechazada: el requerimiento explicito del proyecto es Canvas API "sin dependencias externas".
2. Procesar la paleta en backend - rechazada: no hay backend en Etapa 1/2; rompe el modelo "swap limpio" de Etapa 3 si se introduce antes.
3. Paleta hardcodeada por entrada en `apod.json` - rechazada: no demuestra la skill tecnica pedida y se desincroniza si se cambian las imagenes del mock.

## Consequences

### Positive

- Demuestra Canvas API real, ejecutandose 100% en cliente.
- No agrega dependencias ni peso al bundle.
- Reutilizable sin cambios en Etapa 3 (las imagenes seguiran siendo URLs externas de NASA).

### Negative

- Sujeto a fallas CORS dependiendo del host de la imagen; requiere manejo de fallback robusto.
- El calculo de cuantizacion debe mantenerse simple (evitar K-means complejo) para no introducir jank perceptible al cambiar de fecha.

## Verification Impact

- Unit tests de la funcion de cuantizacion con `ImageData` fixture (no requieren navegador real).
- Test/checklist manual: verificar que el fallback se activa correctamente si una imagen del mock no permite `crossOrigin`.
- Checklist de accesibilidad (P1-W3): contraste AA del texto sobre la paleta generada, en al menos 2 entradas del mock con paletas distintas.
