# Wave P3-W12 - Contenedores y stack local

Date: 2026-07-16
Status: DONE - 2026-07-20
Wave ID: `P3-W12`
Depends On: P3-W1 + P3-W11 merged
Suggested Branch: `wave/p3-w12-local-containers`

## Goal

Empaquetar frontend/API y levantar el sistema completo con PostgreSQL local, sin tocar
proveedores productivos.

## File scope

- `backend/AstronomyExplorer.Api/Dockerfile`
- `backend/AstronomyExplorer.Api/docker-entrypoint.sh`
- `backend/AstronomyExplorer.Api/Program.cs`
- `backend/AstronomyExplorer.Api/Data/LocalDevelopmentFixtureSeeder.cs`
- `backend/AstronomyExplorer.Api/Email/EmailOptions.cs`
- `backend/AstronomyExplorer.Api/Nasa/NasaApodOptions.cs`
- `backend/AstronomyExplorer.Api.Tests/Apod/NasaApodOptionsTests.cs`
- `backend/AstronomyExplorer.Api.Tests/Data/LocalDevelopmentFixtureSeederTests.cs`
- `backend/AstronomyExplorer.Api.Tests/Data/LocalFixtureModeTests.cs`
- `frontend.Dockerfile`
- `docker-compose.yml`
- `docker/nginx/*`, `docker/nasa-mock/default.conf.template`
- `.dockerignore`, `backend/.dockerignore`, `.gitignore`
- `.env.example`
- `docs/deploy/p3-local-runbook.md`
- `README.md`, `backend/README.md`
- `docs/prd/prd.md`, `docs/adr/0003-backend-auth-apod-stack.md`,
  `docs/architecture/p3-flow-overview.md`, `docs/engineering-readiness.md`,
  `docs/maintenance/framework-version-policy.md`, P3/master plans and W13 handoff

## Checklist

- [x] W12.1 Dockerfile API multi-stage .NET 10, usuario no-root y healthcheck.
- [x] W12.2 Dockerfile frontend build Angular + servidor estatico con proxy `/api|auth`.
- [x] W12.3 Compose levanta frontend/API/PostgreSQL con health/dependency correctos.
- [x] W12.4 Estrategia de migracion explicita, repetible y segura; no carrera al startup.
- [x] W12.5 `.env.example` lista todos los nombres sin secretos/reales.
- [x] W12.6 Runbook prueba registro con email fake/local, APOD mock o key dev, search y
  favorites E2E.

## Acceptance criteria

- `docker compose up -d --build` llega a healthy desde ambiente limpio con los dos
  archivos secret locales creados segun runbook.
- Frontend consume API por rutas same-origin.
- Reiniciar conserva DB en volumen local y no duplica migraciones.
- Ningun secret aparece en layers, compose config o repo.

## Verification

```powershell
docker compose config
docker compose up -d --build
Invoke-WebRequest http://localhost:5179/health
dotnet test backend/AstronomyExplorer.sln
npm test -- --watch=false --browsers=ChromeHeadless
docker compose down
```

## Parent sync

- [x] Actualizar `R3.12`, master/readiness y estado con evidencia.

## Completion evidence - 2026-07-20

- `docker compose config` PASS. Secret values are file-backed Docker secrets; output
  contains only the ignored local file paths, not password/signing-key values.
- `docker compose up -d --build` PASS. PostgreSQL and `nasa-mock` became healthy;
  one-shot `migrator` then one-shot `demo-seed` exited 0; API and non-root frontend
  became healthy.
- HTTP smoke PASS through the frontend origin: API health `200`, catalog
  `completed/ready`, FTS search one result, fixture date and dynamic mock today response,
  and SPA `/home` all returned successfully.
- Local no-provider E2E PASS: register `202` -> LocalLog confirmation `204` -> login
  `200` -> favorite POST `204` / GET one result -> logout `204`.
- Restart without `-v` PASS. EF migration history stayed at three rows; the demo APOD
  row and its catalog state remained one each and a favorite remained persisted. The
  seed updates its fixed entry URL/metadata on a local port change rather than duplicating
  it.
- `LocalFixtures:Enabled` and `Email:Provider=LocalLog` are Development-only.
  The local NASA base URL permits HTTP solely for `nasa-mock`/loopback in Development;
  arbitrary HTTP and every Production HTTP origin fail validation before a user key can
  be sent insecurely.
