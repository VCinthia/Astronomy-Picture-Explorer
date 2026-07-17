# Wave P3-W10 - Migracion frontend APOD, fecha y search

Date: 2026-07-16
Status: READY - Not Started
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

- [ ] W10.1 Actualizar `ApodEntry`: remover `service_version`; opcionales aceptan null.
- [ ] W10.2 `/home` lazy usa `GET /api/apod/today`; `/` redirige a `/home`; navs se
  actualizan.
- [ ] W10.3 `selectDate(date)` dispara `/date/{date}` con loading/error/cancelacion de
  request anterior.
- [ ] W10.4 Eliminar `availableDates`, `latestDate`, chips y todo import runtime del mock.
- [ ] W10.5 DatePicker es `<input type=date min=1995-06-16 max=hoy>` y la fecha recibida
  del APOD real es la fuente de `selectedDate`.
- [ ] W10.6 Stepper header resta/suma un dia calendario dentro del rango y hace la misma
  busqueda real; no depende de listas precargadas.
- [ ] W10.7 Search conserva debounce, usa endpoint paginado y maneja
  loading/empty/`catalog_not_ready`/retry/cold-start.
- [ ] W10.8 Tests cubren image/video/nulls, stale request, fecha invalida, rutas y search.

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

- [ ] Actualizar `R3.10`, master/readiness y estado con evidencia.
