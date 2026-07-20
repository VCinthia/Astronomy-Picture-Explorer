# Wave P3-W10 - Migracion frontend APOD, fecha y search

Date: 2026-07-16
Status: DONE - 2026-07-20
Wave ID: `P3-W10`
Depends On: P3-W4 + P3-W6 merged
Suggested Branch: `wave/p3-w10-frontend-apod-search`

## Goal

Eliminar el mock/`availableDates` del runtime y migrar Home, Explorer, selector de fecha
y search a los endpoints P3 sin romper el layout P2.

## File scope

- `src/app/models/apod.model.ts`
- `src/app/services/astronomy.service.ts` + tests
- `src/app/pages/home/`
- `src/app/pages/explorer/`
- `src/app/components/date-picker/`
- `src/app/components/search-bar/`
- `src/app/app.component.*`, `src/app/components/bottom-nav/*`
- `src/app/app.routes.ts`
- `src/assets/mock/apod.json`

## Checklist

- [x] W10.1 Actualizar `ApodEntry`: remover `service_version`; opcionales aceptan null.
- [x] W10.2 `/home` lazy usa `GET /api/apod/today`; `/` redirige a `/home`; navs se
  actualizan.
- [x] W10.3 `selectDate(date)` dispara `/date/{date}` con loading/error/cancelacion de
  request anterior.
- [x] W10.4 Eliminar `availableDates`, `latestDate`, chips y todo import runtime del mock.
- [x] W10.5 DatePicker es `<input type=date min=1995-06-16 max=hoy>` y la fecha recibida
  del APOD real es la fuente de `selectedDate`.
- [x] W10.6 Stepper header resta/suma un dia calendario dentro del rango y hace la misma
  busqueda real; no depende de listas precargadas.
- [x] W10.7 Search conserva debounce, usa endpoint paginado y maneja
  loading/empty/`catalog_not_ready`/retry/cold-start.
- [x] W10.8 Tests cubren image/video/nulls, stale request, fecha invalida, rutas y search.

## Acceptance criteria

- `rg` no encuentra import de mock ni `availableDates` en runtime.
- Home, fecha exacta y search funcionan por HTTP.
- El toolbar P2 conserva estructura responsive.
- Cold start o catalog no ready se comunica como estado recuperable.
- Contrato TypeScript coincide con `ApodEntryDto` app-owned.

## Verification

```powershell
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
rg -n "assets/mock/apod.json|availableDates|service_version" src/app
```

El ultimo `rg` debe devolver cero referencias runtime (fixtures de test fuera de
`src/app` pueden documentarse explicitamente).

## Parent sync

- [x] Actualizar `R3.10`, master/readiness y estado con evidencia.

## Completion evidence

- `ApodEntry` replica exactamente `ApodEntryDto`: ocho campos snake_case, sin
  `service_version` y con `hdurl`, `thumbnail_url` y `copyright` normalizados a `null`.
- `/` redirige a `/home`. Home usa solo `GET /api/apod/today`; Explorer usa
  `GET /api/apod/date/{yyyy-MM-dd}` y `GET /api/apod/search?q=&page=1&pageSize=12`.
- `switchMap` cancela solicitudes date/search obsoletas. `requestedDate` representa una
  seleccion valida pendiente y `selectedDate` solo se confirma con la fecha devuelta por
  la API; un valor invalido tambien cancela la solicitud anterior.
- El `DatePicker` nativo valida el intervalo UTC `1995-06-16..hoy`; el stepper opera un
  dia UTC y no conoce listas precargadas. Search conserva el debounce de `SearchBar`.
- Home/Explorer muestran loading, cold-start/upstream, empty y `catalog_not_ready` con
  reintentos accesibles. W9 permanece intacta: auth sigue same-origin y el control de
  cuenta sigue visible en el shell.
- La fachada local temporal de favoritos queda aislada para W11; no vuelve a cargar mock
  ni intenta asociar fechas anonimas a una cuenta. W11 la elimina por completo antes de
  cualquier promocion de P3.
- Evidencia: `npm run build` PASS; ChromeHeadless `100/100` PASS; `git diff --check` PASS.
