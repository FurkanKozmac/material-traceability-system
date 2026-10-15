#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

command -v docker >/dev/null || { echo "docker is required" >&2; exit 1; }
command -v jq >/dev/null || { echo "jq is required to identify the Compose database volume" >&2; exit 1; }

domain="$(awk -F= '$1 == "DOMAIN" {gsub(/[[:space:]\r]/, "", $2); print $2; exit}' .env 2>/dev/null || true)"
domain="${domain:-localhost}"
if [[ "$domain" != "localhost" ]]; then
  echo "Refusing to reset a non-local deployment (DOMAIN=$domain)." >&2
  exit 1
fi

volume_name="$(docker compose config --format json | jq -r '.volumes.mts_postgres_data.name // empty')"
if [[ -z "$volume_name" ]]; then
  echo "Could not determine the named PostgreSQL volume from Docker Compose." >&2
  exit 1
fi

echo "This permanently deletes the local MTS database volume: $volume_name"
read -r -p 'Type RESET LOCAL DEMO DATA to continue: ' confirmation
if [[ "$confirmation" != "RESET LOCAL DEMO DATA" ]]; then
  echo "Reset cancelled."
  exit 1
fi

docker compose down
docker volume rm "$volume_name"
docker compose up --build -d
echo "Local demo database reset. The backend will reapply migrations and seed data on startup."
