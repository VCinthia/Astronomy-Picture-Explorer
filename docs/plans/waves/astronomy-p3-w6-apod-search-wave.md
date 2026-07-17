# Wave P3-W6 - Busqueda PostgreSQL FTS

Date: 2026-07-16
Status: READY - Not Started
Wave ID: `P3-W6`
Depends On: P3-W5 merged
Suggested Branch: `wave/p3-w6-apod-search`

## Goal

Implementar busqueda paginada por titulo y explicacion con PostgreSQL Full Text Search.
`pg_trgm` solo se agrega si una prueba documentada justifica el fallback.

## File scope

- `backend/AstronomyExplorer.Api/Apod/ApodSearchService.cs`
- `backend/AstronomyExplorer.Api/Apod/ApodEndpoints.cs`
- `backend/AstronomyExplorer.Api/Data/AppDbContext.cs`
- `backend/AstronomyExplorer.Api/Migrations/`
- `backend/AstronomyExplorer.Api.Tests/Apod/Search/`
- `backend/README.md`

## Checklist

- [ ] W6.1 `search_vector` ingles: title peso A + explanation peso B, indice GIN.
- [ ] W6.2 `GET /api/apod/search?q=&page=1&pageSize=...`; trim/normalizacion,
  `pageSize` 1..30 y query vacia invalida.
- [ ] W6.3 Ranking estable por relevancia, luego fecha descendente.
- [ ] W6.4 Catalog no ready -> `503 catalog_not_ready`; sin resultados -> `200 []`.
- [ ] W6.5 Tests PostgreSQL reales: titulo gana a explanation, stemming, caracteres
  especiales, paginacion e injection-safe query.
- [ ] W6.6 Medir casos parciales/typos. Agregar `pg_trgm` e indices solo si se adopta;
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

- [ ] Actualizar `R3.6`, ADR si se adopta trigram, master/readiness y estado.
