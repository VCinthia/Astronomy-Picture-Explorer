# Wave P3-W5 - Ingestion de catalogo resumible

Date: 2026-07-16
Status: READY - Not Started
Wave ID: `P3-W5`
Depends On: P3-W4 merged
Suggested Branch: `wave/p3-w5-catalog-ingestion`

## Goal

Crear un comando local idempotente que cargue APOD por rangos, persista checkpoints y
pueda poblar Neon sin usar jobs/compute de Render ni generar costo.

## File scope

- `backend/AstronomyExplorer.Catalog/`
- `backend/AstronomyExplorer.Api/Apod/CatalogStatusEndpoint.cs`
- `backend/AstronomyExplorer.Api/Data/`
- `backend/AstronomyExplorer.Api/Migrations/`
- `backend/AstronomyExplorer.Api.Tests/Catalog/`
- `backend/README.md`

## Checklist

- [ ] W5.1 CLI `catalog sync --from --to --batch-size 30 --resume --dry-run`.
- [ ] W5.2 Cada batch usa NASA `start_date/end_date`, upsert transaccional y checkpoint
  solo despues de commit.
- [ ] W5.3 Retry/backoff acotado; 429 respeta `Retry-After` y detiene/reanuda seguro.
- [ ] W5.4 Lock logico evita dos sync del mismo rango; resume no duplica ni salta fechas.
- [ ] W5.5 `catalog-status` devuelve coverage/count/status y marca ready solo al completar
  el rango objetivo.
- [ ] W5.6 Preflight muestra request count estimado y prohibe ejecutar desde environment
  Production/Render salvo override explicito de desarrollo documentado.

## Acceptance criteria

- Interrumpir y reanudar produce el mismo catalogo que una corrida completa.
- Tests NASA son mock; ninguna suite consume cuota real.
- El comando no corre en API startup ni requiere scheduler.
- Dry-run y status permiten estimar/verificar antes de tocar Neon.
- Runbook deja claro que se usa API key propia y planes $0 sin overages.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~Catalog"
dotnet run --project backend/AstronomyExplorer.Catalog -- catalog sync --from 2026-01-01 --to 2026-01-31 --dry-run
```

## Parent sync

- [ ] Actualizar `R3.5`, master/readiness y estado con evidencia.
