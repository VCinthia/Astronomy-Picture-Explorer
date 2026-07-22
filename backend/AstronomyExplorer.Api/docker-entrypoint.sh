#!/bin/sh
set -eu

read_secret() {
  if [ ! -r "$1" ]; then
    echo "Required Docker secret is unavailable: $1" >&2
    exit 1
  fi

  tr -d '\r\n' < "$1"
}

# Compose supplies these two secrets as read-only files. A production host has no Docker
# secret mount, so it must provide the complete connection string and session key through
# its own encrypted environment-variable store instead.
if [ -r /run/secrets/postgres_password ]; then
  postgres_password="$(read_secret /run/secrets/postgres_password)"
  export ConnectionStrings__Postgres="Host=postgres;Port=5432;Database=astronomy_explorer;Username=astronomy;Password=${postgres_password}"
fi

if [ -r /run/secrets/session_signing_key ]; then
  session_signing_key="$(read_secret /run/secrets/session_signing_key)"
  export Session__SigningKey="${session_signing_key}"
fi

if [ -z "${ConnectionStrings__Postgres:-}" ] || [ -z "${Session__SigningKey:-}" ]; then
  echo "ConnectionStrings__Postgres and Session__SigningKey must be configured." >&2
  exit 1
fi

# Render assigns PORT at runtime. Compose has no PORT, so it retains the image's 8080
# default. An explicitly supplied ASPNETCORE_URLS remains available for diagnostics.
if [ -z "${ASPNETCORE_URLS:-}" ]; then
  export ASPNETCORE_URLS="http://+:${PORT:-8080}"
fi

exec dotnet AstronomyExplorer.Api.dll "$@"
