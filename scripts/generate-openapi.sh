#!/usr/bin/env bash
# Regenerates the "PART 3 — FULL ENDPOINT REFERENCE" section of docs/API.md
# from the real OpenAPI documents the three backend APIs already expose via
# Swashbuckle (AddSwaggerGen / UseSwagger, Development-only).
#
# Why this exists (tasks.csv #316): docs/API.md used to document zero real
# routes. This script + generate_endpoint_reference.py regenerate the
# section mechanically so it can't drift silently — re-run it whenever
# controllers change materially and diff docs/API.md.
#
# HOW IT WORKS
#   1. `dotnet build` each API in Release.
#   2. Run each API's built DLL for real (ASPNETCORE_ENVIRONMENT=Development,
#      a throwaway port, admin-api's Hangfire server disabled) and curl its
#      own /swagger/v1/swagger.json. This needs a *reachable* Postgres/Redis
#      (docker-compose's nestly-postgres-1 / nestly-redis-1, or your own) —
#      DbContext/Redis/Hangfire registration is lazy, but ASP.NET Core still
#      builds the full service graph and the SignalR Redis backplane connects
#      eagerly at startup, so the dependencies must be reachable even though
#      no query is ever issued against them.
#      (The Swashbuckle.AspNetCore.Cli tool — `swagger tofile` against the
#      built DLL without running it — was tried first and does NOT work here:
#      this solution uses the top-level-statement "minimal hosting" model
#      with no Startup/CreateHostBuilder type, which the CLI's 6.6.2
#      reflection-based host construction cannot locate. Hence: actually run
#      it and hit the endpoint, per this task's documented fallback.)
#   3. Feed the three swagger.json files + the controller source trees to
#      generate_endpoint_reference.py, which cross-references each OpenAPI
#      operation against its controller action's `/// <summary>` doc comment
#      (Swashbuckle has no XML-doc integration wired in this solution — no
#      `GenerateDocumentationFile`/`IncludeXmlComments` — so summaries in the
#      raw OpenAPI JSON are empty; the real ones live only in source) and
#      its [Authorize]/[AllowAnonymous] attributes, and prints replacement
#      markdown for docs/API.md's generated section.
#
# USAGE
#   scripts/generate-openapi.sh            # regenerates docs/API.md in place
#   scripts/generate-openapi.sh --keep-json /path/to/dir   # also dumps the
#                                            three raw swagger.json files there
#
# Requires: dotnet 8 SDK, curl, python3, and Postgres+Redis reachable at the
# connection strings in backend/*/*/appsettings.Development.json (defaults:
# localhost:5432 / localhost:6379 — `docker compose up -d postgres redis`
# from the repo root gives you both).

set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
REPO_ROOT="$(pwd)"

KEEP_JSON_DIR=""
if [[ "${1:-}" == "--keep-json" ]]; then
  KEEP_JSON_DIR="$2"
  mkdir -p "$KEEP_JSON_DIR"
fi

WORK_DIR="$(mktemp -d)"
trap 'kill $(jobs -p) 2>/dev/null || true; rm -rf "$WORK_DIR"' EXIT

declare -A APIS=(
  [consumer-api]="backend/consumer-api/ConsumerApi:ConsumerApi.dll:5301"
  [admin-api]="backend/admin-api/AdminApi:AdminApi.dll:5302"
  [provider-api]="backend/provider-api/ProviderApi:ProviderApi.dll:5303"
)

for name in "${!APIS[@]}"; do
  IFS=':' read -r proj_dir dll port <<< "${APIS[$name]}"
  echo "== building $name ==" >&2
  dotnet build "$REPO_ROOT/$proj_dir" -c Release >/dev/null
done

for name in "${!APIS[@]}"; do
  IFS=':' read -r proj_dir dll port <<< "${APIS[$name]}"
  echo "== running $name to fetch swagger.json ==" >&2
  extra_env=()
  if [[ "$name" == "admin-api" ]]; then
    extra_env+=(BackgroundJobs__ServerEnabled=false)
  fi
  (
    cd "$REPO_ROOT/$proj_dir"
    env ASPNETCORE_ENVIRONMENT=Development \
        ASPNETCORE_URLS="http://127.0.0.1:$port" \
        "${extra_env[@]}" \
        dotnet "bin/Release/net8.0/$dll" > "$WORK_DIR/$name.log" 2>&1 &
    echo $! > "$WORK_DIR/$name.pid"
  )
  for _ in $(seq 1 30); do
    sleep 1
    if curl -sf -o "$WORK_DIR/$name.json" "http://127.0.0.1:$port/swagger/v1/swagger.json"; then
      break
    fi
  done
  kill "$(cat "$WORK_DIR/$name.pid")" 2>/dev/null || true
  if [[ ! -s "$WORK_DIR/$name.json" ]]; then
    echo "FAILED to fetch swagger.json for $name — see $WORK_DIR/$name.log" >&2
    cat "$WORK_DIR/$name.log" >&2
    exit 1
  fi
  if [[ -n "$KEEP_JSON_DIR" ]]; then
    cp "$WORK_DIR/$name.json" "$KEEP_JSON_DIR/$name.json"
  fi
done

echo "== generating docs/API.md endpoint reference ==" >&2
python3 "$REPO_ROOT/scripts/generate_endpoint_reference.py" \
  --consumer-json "$WORK_DIR/consumer-api.json" --consumer-src "$REPO_ROOT/backend/consumer-api/ConsumerApi/Controllers" \
  --admin-json "$WORK_DIR/admin-api.json" --admin-src "$REPO_ROOT/backend/admin-api/AdminApi/Controllers" \
  --provider-json "$WORK_DIR/provider-api.json" --provider-src "$REPO_ROOT/backend/provider-api/ProviderApi/Controllers" \
  --api-md "$REPO_ROOT/docs/API.md"

echo "Done. docs/API.md updated." >&2
