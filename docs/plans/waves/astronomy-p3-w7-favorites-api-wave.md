# Wave P3-W7 - Favorites API protegida

Date: 2026-07-20
Status: DONE - 2026-07-20
Wave ID: `P3-W7`
Depends On: P3-W3 + P3-W4 merged
Suggested Branch: `wave/p3-w7-favorites-api`

## Goal

Persistir favoritos por usuario y devolver entries hidratadas sin N+1.

## File scope

- `backend/AstronomyExplorer.Api/Favorites/`
- `backend/AstronomyExplorer.Api/Apod/ApodCacheService.cs`
- `backend/AstronomyExplorer.Api/Program.cs`
- `backend/AstronomyExplorer.Api.Tests/Favorites/`

## Checklist

- [x] W7.1 GET protegido devuelve `ApodEntryDto[]` estable mediante join.
- [x] W7.2 POST recibe solo `apod_date`, obtiene usuario del claim y asegura APOD cacheado.
- [x] W7.3 POST duplicado es idempotente y consistente bajo concurrencia.
- [x] W7.4 DELETE filtra por usuario del claim + fecha.
- [x] W7.5 Tests con dos usuarios, 401, fecha no cacheada, duplicado y no N+1.

## HTTP contract

- `GET /api/favorites` requiere JWT y devuelve `200` con un array top-level de
  `ApodEntryDto`, ordenado por fecha APOD descendente. Es una unica proyeccion/join
  filtrada por el `sub` del JWT; no pagina ni aplica un limite silencioso porque W11
  carga la coleccion completa de la sesion autenticada de este portfolio.
- `POST /api/favorites` recibe exclusivamente `{ "apod_date": "YYYY-MM-DD" }`.
  Valida `1995-06-16..UTC today` antes de cache/NASA, asegura la entry mediante
  `ApodCacheService` y devuelve `204` tanto al crear como al repetir. El insert usa la
  PK compuesta con `ON CONFLICT DO NOTHING` para mantener la idempotencia concurrente.
- `DELETE /api/favorites/{date}` valida el mismo rango, filtra por `sub + date` y
  devuelve `204` tanto si existia como si no existia.
- Los tres endpoints requieren Bearer. `sub` se lee literalmente porque JWT usa
  `MapInboundClaims=false`; un principal autenticado pero sin GUID valido recibe el
  ProblemDetails app-owned `401 invalid_authenticated_user`. Las fechas invalidas usan
  `400 invalid_favorite_apod_date`; los fallos de cache/NASA reutilizan los
  ProblemDetails APOD sanitizados.

## Acceptance criteria

- Ningun endpoint acepta `userId` del cliente.
- Un usuario no puede leer/borrar favoritos de otro.
- GET devuelve cards completas en una consulta acotada.
- Solo se guarda metadata + relacion, nunca blobs.

## Verification

```powershell
dotnet build backend/AstronomyExplorer.sln
dotnet test backend/AstronomyExplorer.sln --filter "FullyQualifiedName~Favorites"
dotnet test backend/AstronomyExplorer.sln
```

## Completion evidence

- Focused execution: 9/9 Favorites tests PASS against PostgreSQL 17 Testcontainers.
- Build Release: PASS, 0 warnings and 0 errors.
- Backend suite: 159/159 PASS against PostgreSQL 17 Testcontainers.
- Independent review: APPROVED; `dotnet format --verify-no-changes` and
  `git diff --check` PASS.

## Parent sync

- [x] Actualizar `R3.7`, master/readiness, ADR, PRD, flow y backend README con la
  evidencia de cierre.
