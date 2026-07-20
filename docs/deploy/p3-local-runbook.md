# P3 local container runbook

Date: 2026-07-20
Scope: P3-W12 local verification only; it does not deploy or mutate a provider.

## What the stack provides

`docker compose` runs these local services in a strict dependency order:

```text
PostgreSQL healthy -> migrator (one shot) -> demo-seed (one shot) -> API healthy -> frontend
```

- **PostgreSQL** stores all local data in the named `postgres_data` volume.
- **migrator** is the only service that applies EF migrations (`--migrate`). The API
  never applies migrations during normal startup.
- **demo-seed** is an explicit Development-only one-shot (`--seed-local-fixtures`). It
  idempotently creates one `2020-01-01` APOD row and its completed one-day catalog state,
  so date, search and favorites can be verified without a historical backfill.
- **API** is a non-root .NET 10 container. Its health probe verifies PostgreSQL
  connectivity.
- **frontend** is a non-root Nginx static container. It proxies `/api/*` and `/auth/*`
  same-origin to the API, including the host-only refresh cookie.
- **nasa-mock** is an internal deterministic development fixture. It is the only APOD
  upstream used by this stack and serves a same-origin SVG through `/local-apod/`.

The local seed and `Email:Provider=LocalLog` are rejected outside `Development`.
`NasaApod:BaseUrl` allows HTTP only for the internal `nasa-mock` or loopback in
Development; all other environments require HTTPS. The production defaults remain
Resend and `https://api.nasa.gov/`.

## Prerequisites

- Docker Desktop/Engine with Compose v2.
- No NASA, Resend, Neon, Render, Netlify account or paid service is needed.
- Ports `8080` (frontend) and `5179` (loopback-only API health) available locally.

## Create local secrets

The Compose file never interpolates secret values. PostgreSQL reads its password from a
Docker secret and the API entrypoint reads both values from read-only secret mounts.
Consequently `docker compose config`, image history and the repository do not contain
the values. `.env` and `.secrets/` are ignored.

From the repository root in PowerShell:

```powershell
Copy-Item .env.example .env
New-Item -ItemType Directory -Force .secrets | Out-Null

$postgresBytes = New-Object byte[] 24
[Security.Cryptography.RandomNumberGenerator]::Fill($postgresBytes)
[Convert]::ToHexString($postgresBytes).ToLowerInvariant() |
  Set-Content -NoNewline -Encoding ascii .secrets/postgres_password

$sessionBytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Fill($sessionBytes)
[Convert]::ToBase64String($sessionBytes) |
  Set-Content -NoNewline -Encoding ascii .secrets/session_signing_key
```

Keep the generated files local. Do not copy a production password, API key or signing
key into them. To use other free local ports, edit only `FRONTEND_PORT` and
`API_DEBUG_PORT` in `.env`; the demo seed and APOD mock derive the same public frontend
origin on every run.

## Start and verify

```powershell
docker compose config
docker compose up -d --build
docker compose ps
Invoke-WebRequest http://localhost:5179/health
Invoke-RestMethod http://localhost:8080/api/apod/catalog-status
Invoke-RestMethod 'http://localhost:8080/api/apod/search?q=astronomy&page=1&pageSize=12'
```

Expected results:

- `postgres`, `nasa-mock`, `api` and `frontend` are `healthy`.
- `migrator` and `demo-seed` exit `0` after their one-shot work.
- health returns `200`, catalog status is `completed` / `ready: true`, and search returns
  the deterministic local entry.
- `http://localhost:8080/home` renders the SPA; `/api` and `/auth` are same-origin.

The API is deliberately published only as `127.0.0.1:5179` for diagnostics. Browser
traffic must use the frontend on port `8080`.

## Local E2E smoke

1. Open `http://localhost:8080/register` and create a local test account.
2. Retrieve the confirmation URL from the local container log:

   ```powershell
   docker compose logs api | Select-String 'Local development email'
   ```

   Open that URL locally to complete the normal confirmation POST flow. The URL is a
   local testing credential; do not paste it into an issue, commit or external service.
3. Sign in, open Explorer and request `2020-01-01`; then search for `astronomy`.
4. Toggle the resulting entry as a favorite and confirm it appears at `/favorites`.
5. Sign out. The refresh cookie is cleared and the frontend forgets the favorite state
   for that session; the database row remains isolated to the test account.

This demonstrates account confirmation, login/cookie, APOD today/date, ready catalog
search and protected favorites with no NASA or Resend network request. It is not a
substitute for W13's real-provider and production smoke.

## Restart and cleanup

To prove migrations/seed are repeatable while preserving local data:

```powershell
docker compose down
docker compose up -d
```

Do **not** add `-v` above: the named local PostgreSQL volume must remain. The migrator
observes the EF history table and the seed updates its one fixed row/state instead of
duplicating either. To deliberately discard all local data after testing:

```powershell
docker compose down -v
Remove-Item -Recurse -Force .secrets
Remove-Item .env
```

`down -v` is limited to the named local Compose volume; it never targets Neon or any
remote database.

## Boundaries before W13

- Do not point this Compose stack at Neon, Render, Resend or a production origin.
- Do not run `AstronomyExplorer.Catalog` as an API container, cron, worker or startup
  action. The local one-row demo seed is not the historical catalog backfill.
- Do not replace the fixture with `DEMO_KEY`. W13 separately authorizes and verifies a
  personal NASA key, real email provider/domain, free-plan constraints, deployed
  same-origin rewrites and production smoke.
