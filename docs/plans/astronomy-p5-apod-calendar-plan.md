# Phase Plan P5 - Calendario funcional APOD

Date: 2026-08-13
Status: IN PROGRESS (P5-W1 through P5-W3 DONE)
Phase: `P5`
Source master plan: `docs/plans/astronomy-master-plan.md`
Architecture decision: `docs/adr/0005-apod-product-calendar.md`
Depends on: P3/P4 production release on `main`

## 1. Goal

Eliminar el salto anticipado de APOD observado en horario nocturno argentino. La fecha de
producto será `America/Argentina/Buenos_Aires`; la API define el límite y Angular lo
refleja para una UX consistente.

## 2. Scope

### Included

- Una política reutilizable y testeable de fecha APOD basada en `TimeProvider` y zona
  explícita Argentina, compatible con Windows y Linux.
- Aplicación de esa política a Home/today, fecha explícita, favoritos, catálogo local y
  validación de su target.
- Límites de fecha, validación y stepper de Angular basados en la misma zona mediante
  `Intl` explícito.
- Pruebas deterministas de los dos lados de medianoche Argentina y regresión de rutas.
- Documentación/README técnico alineado, smoke local y promoción única de P5 a `main`.

### Excluded

- Cambiar timezone, región, variables de entorno o configuración de Netlify, Render,
  Neon, NASA o correo.
- Cambiar sesiones, expiraciones, timestamps, cache TTL, migraciones, schema, FTS,
  contratos JSON, búsqueda o el seed existente.
- Añadir cron, keepalive, fallback silencioso al día anterior, reintentos automáticos o
  cualquier recurso que pueda generar costo.
- Publicar configuración operativa o modificar los límites documentales de ADR-0004.

## 3. Product and architecture contract

- `DateOnly` continúa siendo la identidad de una edición APOD; la zona sólo define cuál es
  el último día que el producto puede pedir ahora.
- La API rechaza por encima del día Argentina aun si el cliente está adelantado o fue
  alterado. Angular reduce solicitudes inválidas pero nunca es una autorización.
- Todos los instantes persistidos y controles de seguridad permanecen UTC.
- En el borde `02:59:59Z` la fecha máxima es el día argentino anterior; a `03:00:00Z`
  pasa al nuevo día. Un error posterior del proveedor conserva Retry y no se disfraza.

## 4. Execution model

- P5 acumula cambios en `codex/p5-integration`, creada desde `main` en `e1096b1` o su
  sucesor fast-forward. Cada wave usa una subrama `wave/p5-w<n>-<slug>`.
- La secuencia es W1 -> W2 -> W3 -> W4: la política backend se establece primero, el CLI
  reutiliza esa autoridad, Angular alinea su UX y W4 integra/verifica/documenta.
- Cada subrama se revisa, se integra fast-forward en P5 y se publica allí. `main` no recibe
  un estado parcial que cambie la fecha de producto.
- W4 sólo autoriza P5 -> `main` cuando los gates locales y el smoke público estén claros.

## 5. Requirements checklist

- [x] **R5.1** Definir y aplicar la política Argentina autoritativa en API/favoritos (W1).
- [x] **R5.2** Alinear CLI y validación del catálogo con esa autoridad (W2).
- [x] **R5.3** Alinear fecha inicial, picker, validación y stepper Angular (W3).
- [ ] **R5.4** Actualizar contratos/documentación y ejecutar regresión/smoke/promoción (W4).

## 6. Waves

| Wave | Depends on | Outcome |
|---|---|---|
| P5-W1 | P4 DONE | DONE — Política backend APOD + rutas/favoritos y tests de borde |
| P5-W2 | W1 | DONE — Límite coherente del catálogo local |
| P5-W3 | W2 | DONE — Picker, validación y stepper Angular con calendario Argentina |
| P5-W4 | W3 | Documentación, regresión, smoke y promoción P5 |

## 7. Phase exit criteria

- A `02:59:59Z`, Home y las rutas API no pueden pedir la fecha siguiente Argentina.
- A `03:00:00Z`, la nueva fecha se habilita para API, favoritos, CLI y UI.
- El navegador no depende del timezone local para el máximo del picker ni el stepper.
- API y Angular siguen usando el contrato APOD actual y la API conserva la validación final.
- Build, pruebas frontend/backend aplicables, Compose y smoke público posterior a la
  promoción pasan o una limitación externa se registra sin presentarla como éxito.
- No se cambia proveedor, región, secreto, costo ni timestamp de seguridad.
