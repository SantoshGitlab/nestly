#!/usr/bin/env bash
# Applies EF Core migrations to the configured database.
# Usage: ./database/scripts/apply-migrations.sh [connection-string]
# Falls back to the local docker-compose database when no argument is given.
set -euo pipefail
cd "$(dirname "$0")/../.."

CONNECTION="${1:-Host=localhost;Port=5432;Database=nestly;Username=nestly;Password=nestly_dev}"

dotnet ef database update \
  --project backend/shared/Infrastructure \
  --startup-project backend/consumer-api/ConsumerApi \
  --connection "$CONNECTION"
