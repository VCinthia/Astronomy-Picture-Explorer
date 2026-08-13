# Astronomy Explorer backend

ASP.NET Core 10 API for Astronomy Picture Explorer. P3 delivered PostgreSQL persistence,
Identity accounts with confirmation and password recovery, short-lived sessions, an
app-owned APOD contract, layered caching, a manually operated catalog loader, PostgreSQL
full-text search and protected per-user favorites. P4 keeps this technical guide aligned
with the released application without publishing operational production configuration;
P5 defines the APOD product calendar without changing infrastructure timestamps.

## Prerequisites

- .NET SDK `10.0.301` or a later `10.0.3xx` patch accepted by `global.json`.
- Docker Desktop or another Docker-compatible engine for integration tests.
- PostgreSQL 17 for local application execution.

Restore the repository-local EF Core tool from the repository root:

```powershell
dotnet tool restore
```

## Configuration boundaries

No connection string, API key, signing material, sender identity or production origin is
committed. Local Compose setup, including ignored local secret files and deterministic
fixtures, is documented in
[`docs/deploy/p3-local-runbook.md`](../docs/deploy/p3-local-runbook.md).

For a non-Compose local API run, configure the database and other required services with
your own user-secrets or local environment. Deployed configuration is supplied only by
the provider dashboard. Do not copy values from a deployed environment into repository
files, screenshots, terminal transcripts or documentation.

The application validates required session, frontend and provider configuration at
startup. Development-only fixtures and the local email log sink are rejected outside the
Development environment. The released application uses a verified transactional email
provider; local verification remains deterministic and does not contact that provider.

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
- Identity Data Protection keys persist in PostgreSQL so confirmation links survive host
  restart/cold start. The P3 production smoke verified that behavior with the managed
  database's standard at-rest protections.

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

## Docker Compose local stack

From P3-W12, the repository-root Compose stack is the preferred reproducible local
environment. It obtains PostgreSQL and Session values from ignored Docker-secret files,
runs migrations exactly once through a one-shot service, then runs an explicit local
fixture seed before the API starts. The API never migrates or backfills at startup.

See [`docs/deploy/p3-local-runbook.md`](../docs/deploy/p3-local-runbook.md) for setup,
the local-only APOD/email behavior, E2E smoke and safe cleanup. This configuration is
not a production deployment recipe and neither contacts a real provider nor accepts a
production secret by default.

Account endpoints are:

- `POST /auth/register`
- `POST /auth/resend-confirmation`
- `POST /auth/confirm-email`
- `POST /auth/forgot-password`
- `POST /auth/reset-password`
- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`

Register, resend and password-recovery requests use generic responses where exposing
account existence would be unsafe. Confirmation and reset mutate state only through POST.
A successful password reset revokes renewable sessions before the new password can be
used.

The browser uses relative, same-origin application routes. In production, the hosting
boundary accepts application traffic only through the configured frontend proxy, while
the platform health check remains separate. Session credentials are not stored in Web
Storage; refresh/logout use the configured production origin protections and a host-only,
secure cookie. Local and production controls also bound account and application traffic
without publishing operational thresholds.

Public APOD endpoints are:

- `GET /api/apod/today`
- `GET /api/apod/date/{date}`
- `GET /api/apod/catalog-status`
- `GET /api/apod/search?q=<text>&page=1&pageSize=12`

## APOD product calendar

The maximum APOD date is calculated explicitly from the Argentina product calendar.
The API is authoritative for today, an explicit date, favorites and local catalog range
validation; it rejects a future product date before a cache or provider request. The
browser mirrors that boundary only to prevent an impossible selection, and the API
response remains the date actually displayed.

This policy is deliberately separate from infrastructure time. Persisted timestamps,
cache freshness, sessions, expirations and rate limiting remain UTC. It does not change
hosting region or provider configuration, and it does not hide a real provider delay by
falling back to an earlier entry.

An already-open browser tab does not schedule a midnight refresh. Its date controls
re-evaluate after a reload or a relevant interaction/re-render; API validation remains the
final protection in the meantime.

`catalog-status` reports total cached rows and global coverage. Readiness is anchored to
the exact optional `Catalog__RequiredFrom`/`Catalog__RequiredTo` range; W13 sets both to
the approved seed target. Without that configuration it reports `not_started`. With a
target, `ready` requires `Completed`, checkpoint equal to `target_to`, and at least the
persisted `synced_entry_count` rows inside the target. A newer ad-hoc small sync cannot
replace the canonical target.

Search and catalog status consume the same internal readiness policy. Until the
configured canonical target is ready, search returns `503` with code
`catalog_not_ready`; it does not call catalog status over HTTP. Search trims `q`, accepts
1..200 characters, limits page to 1..1000 and pageSize to 1..30 (default 12). Results
are a top-level `ApodEntryDto[]`, ranked by weighted English FTS relevance and then APOD
date descending. No result is `200 []`.

Search uses parameterized `websearch_to_tsquery`, the stored title-A/explanation-B
vector and its GIN index. It never contacts NASA. English stemming is supported;
partial prefixes and typos are deliberately not expanded. W6 did not enable `pg_trgm`
because its limited portfolio benefit did not justify another index and mixed ranking.

Favorites endpoints require an access Bearer token:

- `GET /api/favorites` returns a top-level `ApodEntryDto[]` in APOD-date descending
  order from one `favorites -> apod_entries` projection/join. The collection is not
  silently paginated or truncated because the frontend loads the complete collection
  once per authenticated portfolio session.
- `POST /api/favorites` accepts only `{ "apod_date": "YYYY-MM-DD" }`; it validates
  the supported APOD product range before cache/NASA, obtains the user only from the literal JWT
  `sub` claim, ensures a cache entry and returns `204` for either creation or duplicate.
- `DELETE /api/favorites/{date}` validates the same range, filters `sub + date` and
  returns `204` whether or not that favorite existed.

`MapInboundClaims=false` is intentional, so favorites reads `sub`, never
`ClaimTypes.NameIdentifier`. An authenticated principal whose `sub` is not a GUID gets
the app-owned `401 invalid_authenticated_user`; invalid dates get
`400 invalid_favorite_apod_date`. Cache/provider failures reuse the sanitized APOD
ProblemDetails contract.

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
`1995-06-16..current APOD product date` and batch size within `1..30`. Run migrations first, inspect the
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
local shell intentionally uses a Production environment, it requires an explicit local
override; that override never bypasses host detection. The approved initial catalog seed
completed during P3. Any future change to a deployed catalog target requires a separately
authorized operation that preserves the same cost and safety boundaries.

## Tests

The tests start a temporary PostgreSQL 17 container, apply the real EF Core migrations
and validate relational constraints, FTS/GIN, health, account anti-enumeration, token
expiry/reuse, rate limits, the Resend HTTP contract, cross-instance key persistence,
JWT claims/configuration, refresh concurrency/replay, Origin-protected logout, catalog
range validation, resumable checkpoints, advisory locking, shared status readiness,
weighted search ranking, stemming, web-search syntax, injection-safe parameters,
bounded stable pagination and GIN query plans.

```powershell
dotnet test backend/AstronomyExplorer.sln
dotnet ef migrations list --project backend/AstronomyExplorer.Api --no-connect
```

The `--no-connect` inspection command does not require a configured connection string.
EF Core design time builds the PostgreSQL model without opening a database. Runtime
startup and database-mutating EF commands still require explicit configuration.

Testcontainers never uses a production database and removes the temporary container
after the test collection completes.
