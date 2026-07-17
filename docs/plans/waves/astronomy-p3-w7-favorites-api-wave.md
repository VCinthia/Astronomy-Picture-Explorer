# Wave P3-W7 - Favorites API protegida

Date: 2026-07-16
Status: READY - Not Started
Wave ID: `P3-W7`
Depends On: P3-W3 + P3-W4 merged
Suggested Branch: `wave/p3-w7-favorites-api`

## Goal

Persistir favoritos por usuario y devolver entries hidratadas sin N+1.

## File scope

- `backend/AstronomyExplorer.Api/Favorites/`
- `backend/AstronomyExplorer.Api/Apod/ApodCacheService.cs`
- `backend/AstronomyExplorer.Api.Tests/Favorites/`

## Checklist

- [ ] W7.1 GET protegido devuelve `ApodEntryDto[]` estable mediante join.
- [ ] W7.2 POST recibe solo `apodDate`, obtiene usuario del claim y asegura APOD cacheado.
- [ ] W7.3 POST duplicado es idempotente y consistente bajo concurrencia.
- [ ] W7.4 DELETE filtra por usuario del claim + fecha.
- [ ] W7.5 Tests con dos usuarios, 401, fecha no cacheada, duplicado y no N+1.

## Acceptance criteria

- Ningun endpoint acepta `userId` del cliente.
- Un usuario no puede leer/borrar favoritos de otro.
- GET devuelve cards completas en una consulta acotada.
- Solo se guarda metadata + relacion, nunca blobs.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~Favorites"
```

## Parent sync

- [ ] Actualizar `R3.7`, master/readiness y estado con evidencia.
