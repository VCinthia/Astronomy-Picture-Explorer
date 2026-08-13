# ADR-0005 - Calendario funcional de APOD

Date: 2026-08-13
Status: Accepted
Builds on: ADR-0003

## Context

La aplicación P3 interpretó “hoy” como la fecha UTC. A partir de las 21:00 de Argentina
(UTC-3), eso permite que Home y el picker intenten la fecha siguiente mientras la edición
diaria de APOD todavía no está publicada. El resultado observado fue un error upstream
recuperable aunque la edición del día local anterior existía.

La región de hosting no es la autoridad de esta regla: el comportamiento proviene de
conversiones explícitas a UTC en API y Angular. UTC sigue siendo correcto para instantes,
sesiones, expiraciones, caché y datos persistidos, pero no representa la fecha funcional
que este portfolio muestra a sus visitantes.

## Decision

1. La fecha funcional máxima de APOD se calcula con la zona IANA
   `America/Argentina/Buenos_Aires`. La implementación debe resolverla explícitamente y
   no depender de la zona del sistema, de la región de Render/Netlify ni de un offset
   `-03` escrito a mano. Debe funcionar en Windows local y Linux hospedado; si el runtime
   no puede resolver la zona, debe fallar de forma visible en vez de usar una fecha
   silenciosamente incorrecta.
2. La API posee la autoridad de ese calendario. Una política reutilizable derivada de
   `TimeProvider` determina el último `DateOnly` soportado para `/api/apod/today`,
   `/api/apod/date/{date}`, favoritos, validación de target de catálogo y el CLI local.
   El contrato HTTP, el schema y la identidad `DateOnly` de cada entrada no cambian.
3. Angular replica la misma zona sólo para impedir una selección imposible en el cliente:
   fecha inicial, máximo del input, validación previa y botón siguiente de Home. Debe usar
   `Intl.DateTimeFormat` con `formatToParts`, no `toISOString`, el timezone del navegador
   ni un offset fijo. La respuesta de la API sigue reemplazando la fecha solicitada por la
   fecha real mostrada.
4. P5 no cambia a zona Argentina los timestamps, locks, sesiones, expiraciones, rate
   limits, `CachedAt`, ni la región/configuración horaria de proveedores. Tampoco crea una
   tarea programada, un backfill, una migración o un costo nuevo.
5. P5 no hace fallback automático al día anterior ante un error real de NASA. La zona
   evita el salto anticipado conocido; un retraso o caída genuinos conserva el estado
   accesible de Retry.

## Boundary examples

Con Argentina Standard Time actual:

| Instante UTC | Fecha APOD permitida |
|---|---|
| `2026-08-13T02:59:59Z` | `2026-08-12` |
| `2026-08-13T03:00:00Z` | `2026-08-13` |

Estas pruebas son de calendario de producto. No implican una garantía de que el proveedor
publique exactamente a medianoche argentina.

## Consequences

### Positive

- Home no pide la edición siguiente durante la noche local argentina.
- Fecha explícita, favoritos, CLI y UX no discrepan sobre qué día es válido.
- Las pruebas pueden controlar el borde con `TimeProvider`, sin mutar el reloj del host.

### Negative

- Existe una política horaria adicional que debe permanecer documentada y probada en
  ambas plataformas.
- Un navegador puede tener otro timezone local; el producto seguirá deliberadamente la
  referencia Argentina para su calendario APOD.

## Verification impact

P5 debe probar ambos lados del cambio de día, rechazar la fecha siguiente antes del borde
y aceptarla después. Debe demostrar que API, CLI y Angular aplican la misma semántica,
sin reemplazar las verificaciones UTC existentes para datos de seguridad.
