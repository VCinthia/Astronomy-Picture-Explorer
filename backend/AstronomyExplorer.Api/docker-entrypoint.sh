#!/bin/sh
set -eu

read_secret() {
  if [ ! -r "$1" ]; then
    echo "Required Docker secret is unavailable: $1" >&2
    exit 1
  fi

  tr -d '\r\n' < "$1"
}

postgres_password="$(read_secret /run/secrets/postgres_password)"
session_signing_key="$(read_secret /run/secrets/session_signing_key)"

# Secrets are read only at container start. They are neither Docker build args nor
# Compose environment values, so `docker compose config` and image layers stay clean.
export ConnectionStrings__Postgres="Host=postgres;Port=5432;Database=astronomy_explorer;Username=astronomy;Password=${postgres_password}"
export Session__SigningKey="${session_signing_key}"

exec dotnet AstronomyExplorer.Api.dll "$@"
