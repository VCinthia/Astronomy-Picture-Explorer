# Wave P3-W1 - Backend foundation y schema

Date: 2026-07-16
Status: READY - Not Started
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

- [ ] W1.1 Crear Web API .NET 10 y proyectos de tests; fijar SDK `10.0.x`.
- [ ] W1.2 Configurar Npgsql, Identity `Guid`, ProblemDetails, OpenAPI Development y
  `GET /health`.
- [ ] W1.3 Modelar `ApplicationUser`, `RefreshSession`, `ApodEntry`, `Favorite` y
  `CatalogSyncState` segun ADR-0003; no incluir metadata de keywords ni blobs.
- [ ] W1.4 Configurar PK/FK/unicidad, UTC timestamps y nulabilidad APOD exacta.
- [ ] W1.5 Generar migracion inicial y tests Testcontainers de migracion/constraints.
- [ ] W1.6 Configuracion sensible solo por environment/user-secrets; documentar setup.

## Acceptance criteria

- Solucion compila y `/health` se mapea.
- Migracion crea Identity, sessions, APOD, favorites y sync state.
- `ApodEntry` contiene solo el contrato app-owned + metadata interna de cache/search.
- PostgreSQL Testcontainers valida migracion y constraints reales.
- No existen secrets ni hashing/token providers custom.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln
dotnet ef migrations list --project backend/AstronomyExplorer.Api
```

## Parent sync

- [ ] Actualizar `R3.1`, master/readiness y estado `DONE` o `BLOCKED` con evidencia.
