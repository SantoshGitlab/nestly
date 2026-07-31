#!/usr/bin/env bash
# Creates a new EF Core migration. Migration files live in database/migrations
# at the repo root (task 9c) - Infrastructure.csproj compiles them in via an
# explicit <Compile Include> since they sit outside its own directory tree.
# Usage: ./database/scripts/add-migration.sh MigrationName
#
# NOTE: because of that <Compile Include LinkBase="Migrations">, the EF tool
# writes the updated NestlyDbContextModelSnapshot.cs to a *stray* new file at
# backend/shared/Infrastructure/Migrations/NestlyDbContextModelSnapshot.cs
# instead of updating database/migrations/NestlyDbContextModelSnapshot.cs in
# place (it resolves the snapshot's location from the link path, not
# --output-dir). After running this script:
#   1. cp backend/shared/Infrastructure/Migrations/NestlyDbContextModelSnapshot.cs \
#        database/migrations/NestlyDbContextModelSnapshot.cs
#   2. rm -rf backend/shared/Infrastructure/Migrations
#   3. dotnet build to confirm the duplicate-attribute error is gone.
# Task 95a-95g hit this; task 94a-94d's author almost certainly did too.
set -euo pipefail
[ -n "${1:-}" ] || { echo "Usage: $0 MigrationName"; exit 1; }
cd "$(dirname "$0")/../.."

dotnet ef migrations add "$1" \
  --project backend/shared/Infrastructure \
  --startup-project backend/consumer-api/ConsumerApi \
  --output-dir ../../../database/migrations
