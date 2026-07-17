# Wave P3-W12 - Contenedores y stack local

Date: 2026-07-16
Status: READY - Not Started
Wave ID: `P3-W12`
Depends On: P3-W1 + P3-W11 merged
Suggested Branch: `wave/p3-w12-local-containers`

## Goal

Empaquetar frontend/API y levantar el sistema completo con PostgreSQL local, sin tocar
proveedores productivos.

## File scope

- `backend/AstronomyExplorer.Api/Dockerfile`
- `frontend.Dockerfile`
- `docker-compose.yml`
- `.dockerignore`, `backend/.dockerignore`
- `.env.example`
- `docs/deploy/p3-local-runbook.md`

## Checklist

- [ ] W12.1 Dockerfile API multi-stage .NET 10, usuario no-root y healthcheck.
- [ ] W12.2 Dockerfile frontend build Angular + servidor estatico con proxy `/api|auth`.
- [ ] W12.3 Compose levanta frontend/API/PostgreSQL con health/dependency correctos.
- [ ] W12.4 Estrategia de migracion explicita, repetible y segura; no carrera al startup.
- [ ] W12.5 `.env.example` lista todos los nombres sin secretos/reales.
- [ ] W12.6 Runbook prueba registro con email fake/local, APOD mock o key dev, search y
  favorites E2E.

## Acceptance criteria

- `docker compose up -d --build` llega a healthy desde ambiente limpio.
- Frontend consume API por rutas same-origin.
- Reiniciar conserva DB en volumen local y no duplica migraciones.
- Ningun secret aparece en layers, compose config o repo.

## Verification

```powershell
docker compose config
docker compose up -d --build
Invoke-WebRequest http://localhost:<api-port>/health
dotnet test backend/AstronomyExplorer.sln
npm test -- --watch=false --browsers=ChromeHeadless
docker compose down
```

## Parent sync

- [ ] Actualizar `R3.12`, master/readiness y estado con evidencia.
