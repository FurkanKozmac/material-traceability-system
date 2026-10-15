#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

demo_tables=(relocation_tasks units batches chemicals addresses)
echo "Demo reset will clear these business tables (child-to-parent FK order):"
printf '  - %s\n' "${demo_tables[@]}"
echo "Preserved tables: users, roles, refresh_tokens, system_settings"

confirmed=false
env_file=""
while (($#)); do
  case "$1" in
    --confirm-demo-reset)
      confirmed=true
      shift
      ;;
    --env-file)
      if (($# < 2)); then
        echo "--env-file requires a path." >&2
        exit 2
      fi
      env_file="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      echo "Usage: DEMO_DATA_ENABLED=true $0 [--env-file PATH] --confirm-demo-reset" >&2
      exit 2
      ;;
  esac
done

if [[ "${DEMO_DATA_ENABLED:-}" != "true" ]]; then
  echo "Refusing reset: export DEMO_DATA_ENABLED=true first." >&2
  exit 2
fi
if [[ "$confirmed" != "true" ]]; then
  echo "Refusing reset: --confirm-demo-reset is required." >&2
  exit 2
fi
if [[ -n "$env_file" && ! -f "$env_file" ]]; then
  echo "Compose env file not found: $env_file" >&2
  exit 2
fi

command -v docker >/dev/null || { echo "docker is required" >&2; exit 1; }
command -v curl >/dev/null || { echo "curl is required for the post-reset health check" >&2; exit 1; }
command -v jq >/dev/null || { echo "jq is required to read the Compose domain" >&2; exit 1; }

compose=(docker compose)
if [[ -n "$env_file" ]]; then
  compose+=(--env-file "$env_file")
fi

domain="$("${compose[@]}" config --format json | jq -r '.services.caddy.environment.DOMAIN // "localhost"')"
if ! "${compose[@]}" exec -T backend sh -ec 'test "${DEMO_DATA_ENABLED:-}" = "true"'; then
  echo "Refusing reset: the running backend is not configured with DEMO_DATA_ENABLED=true." >&2
  exit 2
fi
health_url="${DEMO_HEALTHCHECK_URL:-}"
if [[ -z "$health_url" ]]; then
  if [[ "$domain" == "localhost" ]]; then
    health_url="https://localhost/api/health"
  else
    health_url="https://${domain}/api/health"
  fi
fi
health_curl_args=(--silent --show-error --fail --location --max-time 5)
if [[ "$domain" == "localhost" && "$health_url" == https://localhost/* ]]; then
  health_curl_args+=(--insecure)
fi

backup_dir="${DEMO_BACKUP_DIR:-$repo_root/backups/demo-reset}"
mkdir -p "$backup_dir"
chmod 700 "$backup_dir"
umask 077
backup_file="$backup_dir/mts-demo-reset-$(date -u +%Y%m%dT%H%M%SZ)-$$.dump"

echo "Creating PostgreSQL backup: $backup_file"
if ! "${compose[@]}" exec -T db sh -ec 'pg_dump --format=custom --username "$POSTGRES_USER" --dbname "$POSTGRES_DB"' > "$backup_file"; then
  rm -f "$backup_file"
  echo "Backup failed; no business data was changed." >&2
  exit 1
fi
if [[ ! -s "$backup_file" ]]; then
  rm -f "$backup_file"
  echo "Backup is empty; no business data was changed." >&2
  exit 1
fi
chmod 600 "$backup_file"

old_backups="$(ls -1t "$backup_dir"/mts-demo-reset-*.dump 2>/dev/null | tail -n +3 || true)"
if [[ -n "$old_backups" ]]; then
  while IFS= read -r old_backup; do
    [[ -n "$old_backup" ]] && rm -f -- "$old_backup"
  done <<< "$old_backups"
fi

echo "Clearing demo business tables in a single PostgreSQL transaction."
"${compose[@]}" exec -T db sh -ec \
  'psql --set=ON_ERROR_STOP=1 --single-transaction --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -c "TRUNCATE TABLE relocation_tasks, units, batches, chemicals, addresses RESTART IDENTITY;"'

echo "Restarting backend to repopulate demo data."
DEMO_DATA_ENABLED=true "${compose[@]}" restart backend

echo "Checking health endpoint: $health_url"
for attempt in $(seq 1 30); do
  if health_response="$(curl "${health_curl_args[@]}" "$health_url")" &&
    printf '%s' "$health_response" | jq -e '.status == "HEALTHY"' >/dev/null; then
    echo "Demo data reset and backend health check succeeded."
    exit 0
  fi
  sleep 2
done

echo "Backend did not become healthy after reset." >&2
exit 1
