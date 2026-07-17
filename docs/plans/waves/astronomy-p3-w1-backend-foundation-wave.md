# Wave P3-W1 - Backend foundation y schema

Date: 2026-07-16
Completed: 2026-07-17
Status: DONE
Wave ID: `P3-W1`
Depends On: P2 DONE in production
Suggested Branch: `wave/p3-w1-backend-foundation`

## Goal

Crear la solucion .NET 10, host API, schema PostgreSQL inicial e infraestructura de
tests que utilizaran todas las waves backend. No implementa casos de uso HTTP.

## File scope

- `global.json`
- `backend/AstronomyExplorer.sln`
- `backend/AstronomyExplorer.Api/`
- `backend/AstronomyExplorer.Api/Data/AppDbContext.cs`
- `backend/AstronomyExplorer.Api/Domain/`
- `backend/AstronomyExplorer.Api/Migrations/`
- `backend/AstronomyExplorer.Api.Tests/`
- `backend/README.md`, `backend/.dockerignore`

## Checklist

- [x] W1.1 Crear Web API .NET 10 y proyectos de tests; fijar SDK `10.0.x`.
- [x] W1.2 Configurar Npgsql, Identity `Guid`, ProblemDetails, OpenAPI Development y
  `GET /health`.
- [x] W1.3 Modelar `ApplicationUser`, `RefreshSession`, `ApodEntry`, `Favorite` y
  `CatalogSyncState` segun ADR-0003; no incluir metadata de keywords ni blobs.
- [x] W1.4 Configurar PK/FK/unicidad, UTC timestamps y nulabilidad APOD exacta.
- [x] W1.5 Generar migracion inicial y tests Testcontainers de migracion/constraints.
- [x] W1.6 Configuracion sensible solo por environment/user-secrets; documentar setup.

## Acceptance criteria

- Solucion compila y `/health` se mapea.
- Migracion crea Identity, sessions, APOD, favorites y sync state.
- `ApodEntry` contiene solo el contrato app-owned + metadata interna de cache/search.
- PostgreSQL Testcontainers valida migracion y constraints reales.
- No existen secrets ni hashing/token providers custom.

## Verification

```powershell
dotnet tool restore
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln
dotnet ef migrations list --project backend/AstronomyExplorer.Api --no-connect
```

## Completion evidence

- `dotnet build`: PASS, 0 warnings y 0 errors.
- Testcontainers PostgreSQL 17: 11/11 tests PASS sobre migracion, PK/FK/checks,
  delete behaviors, nulabilidad, tipos fisicos, UTC, `tsvector` stored/GIN y health.
- `/health`: `200 Healthy` con DB y `503 Unhealthy` sin DB.
- OpenAPI: disponible en Development y ausente en Production.
- `dotnet ef migrations list --no-connect`: lista
  `20260717192106_InitialCreate` sin requerir secrets; runtime y operaciones mutables
  siguen fallando cerradas sin connection string.
- `dotnet format --verify-no-changes` y auditoria NuGet: PASS.

## Implemented design clarifications

- Identity usa `IdentityUserContext<ApplicationUser, Guid>` sin roles, endpoints
  automaticos ni cookie auth; `NormalizedEmail` tiene indice unico.
- `search_vector` es generated stored con title peso A, explanation peso B e indice GIN.
- La relacion self-FK de refresh usa `NO ACTION`; la FK de usuario conserva cascade y
  esta combinacion fue validada eliminando una familia rotada completa.
- `CatalogSyncState` es unico por rango, persiste status como string restringido y
  valida rango, checkpoint y coherencia `updated_at >= created_at`.

## Parent sync

- [x] Actualizar `R3.1`, master/readiness y estado `DONE` con evidencia.
