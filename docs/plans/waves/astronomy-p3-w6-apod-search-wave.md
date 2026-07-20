# Wave P3-W6 - Busqueda PostgreSQL FTS

Date: 2026-07-16
Completed: 2026-07-20
Status: DONE
Wave ID: `P3-W6`
Depends On: P3-W5 merged
Suggested Branch: `wave/p3-w6-apod-search`

## Goal

Implementar busqueda paginada por titulo y explicacion con PostgreSQL Full Text Search.
`pg_trgm` solo se agrega si una prueba documentada justifica el fallback.

## File scope

- `backend/AstronomyExplorer.Api/Apod/ApodSearchService.cs`
- `backend/AstronomyExplorer.Api/Apod/CatalogReadinessService.cs`
- `backend/AstronomyExplorer.Api/Apod/ApodEndpoints.cs`
- `backend/AstronomyExplorer.Api/Apod/CatalogStatusEndpoint.cs`
- `backend/AstronomyExplorer.Api/Program.cs`
- `backend/AstronomyExplorer.Api.Tests/Apod/Search/`
- `backend/README.md`
- Parent documentation synchronized by this wave.

## Checklist

- [x] W6.1 `search_vector` ingles: title peso A + explanation peso B, indice GIN.
- [x] W6.2 `GET /api/apod/search?q=&page=1&pageSize=...`; trim/normalizacion,
  `page` 1..1000, `pageSize` 1..30 y query vacia invalida.
- [x] W6.3 Ranking estable por relevancia, luego fecha descendente.
- [x] W6.4 Catalog no ready -> `503 catalog_not_ready`; sin resultados -> `200 []`.
- [x] W6.5 Tests PostgreSQL reales: titulo gana a explanation, stemming, caracteres
  especiales, paginacion e injection-safe query.
- [x] W6.6 Medir casos parciales/typos. Agregar `pg_trgm` e indices solo si se adopta;
  documentar resultado y cubrir fallback. Si no, registrar decision de no habilitarlo.

## Acceptance criteria

- No existe llamada NASA ni dependencia de metadata externa durante search.
- Query usa indices PostgreSQL verificables y devuelve DTO app-owned.
- Limites evitan respuestas/queries sin cota.
- Comportamiento de trigram queda decidido con evidencia, no ambiguo.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~ApodSearch"
```

## Parent sync

- [x] Actualizar `R3.6`, ADR, master/readiness/flow y estado.

## Completion evidence

- W6 reutiliza el `search_vector` generated stored e indice GIN creados por W1; no fue
  necesaria una migracion nueva. `EXPLAIN` forzado sin sequential scan confirma
  `ix_apod_entries_search_vector` para el predicado FTS.
- `websearch_to_tsquery('english', q)` recibe `q` como parametro, no concatena SQL. El
  resultado top-level es `ApodEntryDto[]`; pagina default 1, `pageSize` default 12 y
  maximo 30. `q` se recorta y limita a 200 caracteres; `page` se limita a 1..1000
  antes de readiness/DB, evitando offsets profundos y overflow.
- `CatalogReadinessService` es la unica politica interna de readiness compartida por
  `catalog-status` y search. Search no se llama a si mismo por HTTP ni consulta NASA.
- Evidencia `pg_trgm`: stemming ingles (`galaxy` -> `galaxies`) funciona con FTS;
  prefijo `nebul` y typo `neubla` no producen match. Para el catalogo acotado de este
  portfolio, agregar extension, indice y una segunda politica de ranking no justifica
  ese beneficio parcial. W6 no habilita `pg_trgm`; una wave futura requeriria nueva
  evidencia y decision explicita.
- `dotnet build backend/AstronomyExplorer.sln -c Release`: PASS, 0 warnings/errors.
- Focus W6: 18/18 PASS sobre PostgreSQL 17 Testcontainers.
- `dotnet test backend/AstronomyExplorer.sln -c Release --no-build`: 150/150 PASS.
