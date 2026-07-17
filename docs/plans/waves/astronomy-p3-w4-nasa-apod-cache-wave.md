# Wave P3-W4 - NASA APOD y cache

Date: 2026-07-16
Status: READY - Not Started
Wave ID: `P3-W4`
Depends On: P3-W1 merged
Suggested Branch: `wave/p3-w4-nasa-apod-cache`

## Goal

Integrar NASA para APOD del dia/fecha, adaptar la respuesta al DTO app-owned y mantener
cache en memoria + PostgreSQL. No implementa ingestion historica ni search.

## File scope

- `backend/AstronomyExplorer.Api/Nasa/`
- `backend/AstronomyExplorer.Api/Apod/ApodEntryDto.cs`
- `backend/AstronomyExplorer.Api/Apod/ApodCacheService.cs`
- `backend/AstronomyExplorer.Api/Apod/ApodEndpoints.cs`
- `backend/AstronomyExplorer.Api/Program.cs`
- `backend/AstronomyExplorer.Api.Tests/Nasa/`
- `backend/AstronomyExplorer.Api.Tests/Apod/Cache/`

## Checklist

- [ ] W4.1 Typed HttpClient con API key por environment y timeout/retry acotado.
- [ ] W4.2 Requests usan solamente `date` y `thumbs=true`.
- [ ] W4.3 Deserializar campos NASA reales y mapear DTO sin `service_version`; strings
  opcionales vacios se normalizan a null.
- [ ] W4.4 `GET /api/apod/today` y `/date/{date}` validan rango
  `1995-06-16..hoy` y devuelven ProblemDetails consistente.
- [ ] W4.5 Lectura memory -> DB -> NASA; miss hace upsert idempotente y llena memoria.
- [ ] W4.6 Tests de imagen, video sin thumbnail, copyright/hdurl ausentes, 429, timeout y
  5xx sin filtrar API key.

## Acceptance criteria

- Shape JSON coincide exactamente con ADR-0003.
- Metadata auxiliar, `resource` y `service_version` no se exponen/persisten.
- Repetir una fecha evita otra llamada NASA mientras cache sea valida.
- Fecha invalida/futura y fallas upstream tienen respuestas observables.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~Nasa|FullyQualifiedName~ApodCache"
```

## Parent sync

- [ ] Actualizar `R3.4`, master/readiness y estado con evidencia.
