# Astronomy Explorer backend

ASP.NET Core 10 API for Astronomy Picture Explorer. P3-W1 established the host,
PostgreSQL schema and Identity persistence. P3-W2 adds registration, confirmation,
resend, rate limiting and the Resend adapter while keeping all external email traffic
fake/in-memory during tests.

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
```

The application fails at startup when this setting is absent. Do not add connection
strings, API keys or passwords to either `appsettings.json` file.

## Schema decisions

- Identity uses `ApplicationUser : IdentityUser<Guid>` with the user-only
  `IdentityUserContext`; roles are intentionally absent.
- `NormalizedEmail` is unique, in addition to Identity's unique normalized username.
- Refresh tokens are represented only by a required, unique `token_hash` up to 128
  characters. Hashing and rotation behavior belong to P3-W3.
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

Register/resend are generic to avoid direct account enumeration. Confirmation accepts
`userId + code` and mutates state only through POST. Register/resend have independent
limits by transport IP and normalized email; W13 must configure only verified forwarded
proxies before treating the partition as the public client IP.

## Tests

The tests start a temporary PostgreSQL 17 container, apply the real EF Core migrations
and validate relational constraints, FTS/GIN, health, account anti-enumeration, token
expiry/reuse, rate limits, the Resend HTTP contract and cross-instance key persistence.

```powershell
dotnet test backend/AstronomyExplorer.sln
dotnet ef migrations list --project backend/AstronomyExplorer.Api --no-connect
```

The `--no-connect` inspection command does not require a configured connection string.
EF Core design time builds the PostgreSQL model without opening a database. Runtime
startup and database-mutating EF commands still require explicit configuration.

Testcontainers never uses a production database and removes the temporary container
after the test collection completes.
