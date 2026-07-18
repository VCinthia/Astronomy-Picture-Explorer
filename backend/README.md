# Astronomy Explorer backend

ASP.NET Core 10 API for Astronomy Picture Explorer. P3-W1 established the host,
PostgreSQL schema and Identity persistence. P3-W2 adds registration, confirmation,
resend, rate limiting and the Resend adapter. P3-W3 adds Identity login, short JWTs,
rotating PostgreSQL refresh sessions and Origin-protected logout/refresh.
P3-W4 adds the app-owned APOD contract, public today/date endpoints and layered cache.
P3-W5 adds the local resumable catalog CLI and public catalog status.

## Prerequisites

- .NET SDK `10.0.301` or a later `10.0.3xx` patch accepted by `global.json`.
- Docker Desktop or another Docker-compatible engine for integration tests.
- PostgreSQL 17 for local application execution.

Restore the repository-local EF Core tool from the repository root:

```powershell
dotnet tool restore
```

## Configuration

No connection string or credential is committed. For local development, store the
PostgreSQL connection string in user secrets:

```powershell
dotnet user-secrets set `
  "ConnectionStrings:Postgres" `
  "Host=localhost;Port=5432;Database=astronomy_explorer;Username=<user>;Password=<password>" `
  --project backend/AstronomyExplorer.Api
```

CI and deployed environments must use the equivalent environment variable:

```text
ConnectionStrings__Postgres
```

Account email links use `Frontend:PublicBaseUrl` (local default
`http://localhost:4200`). Real delivery is deferred to W13; when enabled, secrets and
sender configuration are supplied only through user-secrets/provider environment:

```text
Frontend__PublicBaseUrl
Resend__ApiKey
Resend__FromAddress
Session__Issuer
Session__Audience
Session__SigningKey
Session__AccessTokenLifetime
Session__RefreshTokenLifetime
Session__RefreshCookieName
NasaApod__ApiKey
Catalog__RequiredFrom
Catalog__RequiredTo
```

`Session__SigningKey` must contain at least 32 UTF-8 bytes and must never be committed,
printed or copied into documentation. Session and frontend options validate on startup;
production fails closed when issuer, audience, key or HTTPS public origin are invalid.

The application fails at startup when this setting is absent. Do not add connection
strings, API keys or passwords to either `appsettings.json` file.

## Schema decisions

- Identity uses `ApplicationUser : IdentityUser<Guid>` with the user-only
  `IdentityUserContext`; roles are intentionally absent.
- `NormalizedEmail` is unique, in addition to Identity's unique normalized username.
- Refresh tokens are represented only by a required, unique `token_hash` up to 128
  characters. W3 stores SHA-256 hexadecimal; rotate/logout serialize by family with a
  transaction-scoped PostgreSQL advisory lock. Replay and logout revoke that family,
  while other login families for the same user remain active.
- `apod_entries` stores metadata only. Optional NASA fields are nullable and
  `search_vector` is a PostgreSQL stored generated column weighted with title `A` and
  explanation `B`, indexed with GIN.
- `catalog_sync_state` preserves range history and resumable checkpoints. Its .NET enum
  is stored as a constrained string, not as a PostgreSQL enum.
- All persisted instants use PostgreSQL `timestamp with time zone`. Application values
  must use `DateTimeOffset.UtcNow`; Npgsql rejects non-zero offsets for this type.
- Identity Data Protection keys use application name `AstronomyExplorer` and persist in
  PostgreSQL so confirmation links survive host restart/cold start. W13 revalidates
  provider encryption at rest before production.

The initial migration creates the Identity tables plus `refresh_sessions`,
`apod_entries`, `favorites` and `catalog_sync_state`. The W2 migration
`PersistDataProtectionKeys` adds the shared key ring without rewriting W1 history.
The W5 migration `AddCatalogSyncProgress` adds `retry_not_before` and
`synced_entry_count`. The former preserves a NASA 429 window across restarts; the
latter counts provider entries independently from calendar days because historical
APOD ranges can be sparse.

## Build, migrate and run

From the repository root with the connection string configured:

```powershell
dotnet restore backend/AstronomyExplorer.sln
dotnet build backend/AstronomyExplorer.sln --no-restore
dotnet ef database update --project backend/AstronomyExplorer.Api
dotnet run --project backend/AstronomyExplorer.Api
```

OpenAPI is mapped only when `ASPNETCORE_ENVIRONMENT=Development`. Health is available
at `GET /health` and reports unhealthy when PostgreSQL cannot be reached.

Account endpoints are:

- `POST /auth/register`
- `POST /auth/resend-confirmation`
- `POST /auth/confirm-email`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`

Register/resend are generic to avoid direct account enumeration. Confirmation accepts
`userId + code` and mutates state only through POST. Register/resend have independent
limits by transport IP and normalized email; W13 must configure only verified forwarded
proxies before treating the partition as the public client IP.

Login returns a ten-minute HS256 access JWT plus a host-only refresh cookie. Unknown
emails verify a dummy hash using the configured Identity hasher to reduce timing
enumeration. Refresh/logout
require the exact configured Origin in Production; logout needs the cookie rather than a
valid Bearer token. Cookie attributes are HttpOnly, SameSite=Lax, Path `/auth`, explicit
Max-Age/Expires and Secure, with an HTTP exception only for loopback Development.

Login uses an IP-only fixed-window limit (default 10 attempts per 15 minutes, no queue).
There is deliberately no email partition or account lockout, avoiding targeted denial of
service. W13 must verify the trusted Netlify/Render forwarding chain before resolving the
public visitor IP.

Public APOD endpoints are:

- `GET /api/apod/today`
- `GET /api/apod/date/{date}`
- `GET /api/apod/catalog-status`

`catalog-status` reports total cached rows and global coverage. Readiness is anchored to
the exact optional `Catalog__RequiredFrom`/`Catalog__RequiredTo` range; W13 sets both to
the approved seed target. Without that configuration it reports `not_started`. With a
target, `ready` requires `Completed`, checkpoint equal to `target_to`, and at least the
persisted `synced_entry_count` rows inside the target. A newer ad-hoc small sync cannot
replace the canonical target.

## Local catalog synchronization

The catalog loader is a local operator command. It never runs from API startup, a
hosted service, Render, a scheduler or a paid job. Preview a range without reading a
connection string/API key or opening DB/network connections:

```powershell
dotnet run --project backend/AstronomyExplorer.Catalog -- `
  catalog sync --from 2026-01-01 --to 2026-01-31 --batch-size 30 --dry-run
```

Live execution requires `ConnectionStrings__Postgres` and a personal
`NasaApod__ApiKey`; `DEMO_KEY` is rejected. Ranges must remain within
`1995-06-16..UTC today` and batch size within `1..30`. Run migrations first, inspect the
dry-run estimate, then execute locally:

```powershell
$env:ConnectionStrings__Postgres = "<target PostgreSQL connection>"
$env:NasaApod__ApiKey = "<personal NASA key>"
dotnet run --project backend/AstronomyExplorer.Catalog -- `
  catalog sync --from 1995-06-16 --to 2026-07-17 --batch-size 30
```

An incomplete range must be continued with `--resume`. The command holds one global
PostgreSQL advisory lock, so overlapping loaders cannot run concurrently. Each fetch
happens outside a transaction; its upserts and checkpoint commit together. Ctrl+C,
timeout, network/408/5xx and 429 pause safely. A 429 persists `retry_not_before`, using
a safe one-hour fallback if `Retry-After` is absent; an early resume fails before
contacting NASA. Invalid payloads and permanent 4xx mark the run failed.

Historical NASA arrays may be empty, sparse or unordered. The client sorts valid
entries and rejects null elements, duplicate dates and dates outside the requested
batch; it does not require one item per calendar day. A completed run stores the number
of entries actually returned. If later row drift drops below that count, a normal rerun
fails safely and `--resume` deliberately replays the full range to repair it.

The global lock connection is monitored by a periodic heartbeat. Losing that session
cancels the active provider/persistence operation, leaves the current batch checkpoint
unchanged and records `Paused`; the operator can then resume safely.

Render execution is always blocked, including when an override flag is present. If a
local shell intentionally uses `DOTNET_ENVIRONMENT=Production`, it additionally
requires `--allow-local-production`. That flag never bypasses Render detection. W13 is
the only wave authorized to point this command at the production Neon database, after
revalidating free-plan quotas and zero-overage controls.

## Tests

The tests start a temporary PostgreSQL 17 container, apply the real EF Core migrations
and validate relational constraints, FTS/GIN, health, account anti-enumeration, token
expiry/reuse, rate limits, the Resend HTTP contract, cross-instance key persistence,
JWT claims/configuration, refresh concurrency/replay, Origin-protected logout, catalog
range validation, resumable checkpoints, advisory locking and status readiness.

```powershell
dotnet test backend/AstronomyExplorer.sln
dotnet ef migrations list --project backend/AstronomyExplorer.Api --no-connect
```

The `--no-connect` inspection command does not require a configured connection string.
EF Core design time builds the PostgreSQL model without opening a database. Runtime
startup and database-mutating EF commands still require explicit configuration.

Testcontainers never uses a production database and removes the temporary container
after the test collection completes.
