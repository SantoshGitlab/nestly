#!/usr/bin/env bash
# Creates a new EF Core migration in the Infrastructure project.
# Usage: ./database/scripts/add-migration.sh MigrationName
set -euo pipefail
[ -n "${1:-}" ] || { echo "Usage: $0 MigrationName"; exit 1; }
cd "$(dirname "$0")/../.."

dotnet ef migrations add "$1" \
  --project backend/shared/Infrastructure \
  --startup-project backend/consumer-api/ConsumerApi \
  --output-dir Persistence/Migrations
