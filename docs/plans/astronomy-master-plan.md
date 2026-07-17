# Master Plan - Astronomy Picture Explorer

Date: 2026-06-25
Last revised: 2026-07-16
Status: P1 DONE; P2 DONE in production; P3 READY - Not Started
Owner: `CinthiaRV`

## 1. Goal

Construir un portfolio Angular accesible que explore NASA APOD, genere paletas Canvas,
permita fecha/search/favoritos y evolucione desde mock local hasta backend .NET seguro,
persistente y desplegado sin costo monetario.

## 2. Source of truth

1. `docs/prd/prd.md`
2. `docs/adr/0001-angular-standalone-signals-tailwind.md`
3. `docs/adr/0002-canvas-color-palette-extraction.md`
4. `docs/adr/0003-backend-auth-apod-stack.md`
5. `docs/engineering-readiness.md`
6. Phase plans bajo `docs/plans/`
7. Wave plans bajo `docs/plans/waves/`
8. `docs/architecture/p3-flow-overview.md`

Ante contradiccion P3, ADR-0003 prevalece y los planes deben sincronizarse antes de
implementar.

## 3. Program state

### P1 - DONE

- Angular 19 standalone/signals, Tailwind v4 tokens, Home/Explorer mock, image/video,
  Canvas palette y WCAG AA base.
- Deploy publico: `https://astronomy-picture-explorer.netlify.app/`.
- Tag `v1.0.0`; polish posterior integrado.

### P2 - DONE in production

- P2-W1 `7d1f031`: state/search/favorites local + grid.
- P2-W2 `b50cb87`: favorites UI/ruta.
- P2-W3 `0bde545`: search/toolbar/nav desktop.
- P2-W4 `b72c7e2`: bottom nav/mobile/icon polish.
- `main == origin/main` en `b72c7e2`.
- Build PASS, 77/77 tests PASS.
- Smoke productivo 2026-07-16 PASS: rutas, search Hydra, favorito persistido entre
  Home/Favorites y navegacion responsive 390x844. Evidencia detallada en readiness y P2.

### P3 - READY, not started

La revision del 2026-07-16 establecio 13 waves ejecutables:

- Identity y sesiones rotadas.
- Confirmacion POST con `userId + code` Base64URL.
- DTO APOD app-owned sin metadata de keywords ni `service_version`.
- PostgreSQL FTS title+explanation; `pg_trgm` opcional con evidencia.
- Ingestion CLI local resumible; nunca backfill en Render.
- Proxy same-origin Netlify -> Render y cookie SameSite=Lax.
- Costo obligatorio $0 y experiencia explicita de cold start.

## 4. Execution contract

- Branch por wave: `wave/p<n>-w<m>-<slug>` o nombre sugerido en la wave.
- Implementar -> verificar -> review -> aprobacion -> commit/merge -> sincronizar docs.
- Una wave solo se cierra con evidencia de sus acceptance criteria.
- No avanzar si una decision cambia ADR/phase/dependency graph sin actualizar docs primero.
- P3-W13 es la unica wave que despliega/muta produccion; waves previas usan local/fakes.
- Ningun agente habilita recursos pagos, overages, keepalive o upgrades automaticos.

## 5. Non-negotiable engineering rules

1. Codigo, identifiers y commits en ingles.
2. Angular standalone + Signals; sin NgModule/BehaviorSubject/NgRx.
3. Tailwind con tokens `@theme`; sin valores arbitrarios evitables.
4. WCAG 2.1 AA minimo y estados async accesibles.
5. Canvas palette permanece client-side segun ADR-0002.
6. Secrets solo por environment/user-secrets/provider dashboard.
7. PostgreSQL features se prueban con PostgreSQL real/Testcontainers.
8. API externa se adapta a DTO app-owned; no se filtra shape de proveedor por accidente.
9. Search no llama NASA; usa exclusivamente PostgreSQL sobre title/explanation.
10. Browser productivo usa rutas same-origin; no CORS wildcard ni third-party refresh.
11. Todas las queries/listados tienen limites y orden estable.
12. Iconos de controles son SVG; se conserva solo la estrella de marca aprobada.
13. Costo $0 prevalece sobre always-on o automatizacion diaria.

## 6. Phases

- [x] **P1 - Frontend mock**: `docs/plans/astronomy-p1-frontend-mock-plan.md`.
- [x] **P2 - Favorites/search local**: `docs/plans/astronomy-p2-favorites-search-plan.md`.
- [ ] **P3 - Backend/auth/persistence/deploy**:
  `docs/plans/astronomy-p3-backend-plan.md`.

## 7. P3 wave map

| Wave | Resultado |
|---|---|
| W1 | .NET foundation + schema + Testcontainers |
| W2 | Account registration/email/confirmation |
| W3 | Login/JWT/refresh/logout |
| W4 | NASA today/date/cache/DTO |
| W5 | Resumable catalog CLI/status |
| W6 | PostgreSQL FTS search |
| W7 | Hydrated protected favorites API |
| W8 | Frontend account/auth forms |
| W9 | Frontend bootstrap/guard/single-flight session |
| W10 | Frontend APOD/date/search; remove availableDates/mock |
| W11 | Frontend favorites API migration |
| W12 | Local containers/full stack |
| W13 | Zero-cost seed/deploy/production smoke |

El grafo normativo esta en P3 y `p3-flow-overview.md`.

## 8. Program exit criteria

- P1/P2 permanecen funcionales hasta que P3 sustituye sus fuentes de datos.
- P3 cumple todos sus exit criteria y smoke productivo.
- Auth, APOD, search y favorites funcionan desde primera visita, incluyendo cold start.
- Catalogo productivo esta ready y observable.
- No quedan mock/localStorage como fuente runtime de P3.
- No existe configuracion capaz de generar cargos automaticos.
- PRD/ADR/readiness/master/phases/waves/runbooks coinciden con la implementacion final.

## 9. Canonical validation

```powershell
npm ci
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln
docker compose config
```

Los comandos .NET/Docker aplican desde las waves que crean esos artefactos.
