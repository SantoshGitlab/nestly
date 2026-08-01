#!/usr/bin/env bash
# Restores a pg_dump custom-format backup (produced by backup-postgres.sh)
# into a target database.
#
# Usage:
#   ./database/scripts/restore-postgres.sh <backup-file> [target-database]
#
# [target-database] defaults to PGDATABASE (or "nestly" if unset) - i.e. by
# default this restores IN PLACE over the live database. For the documented
# restore drill (docs/RUNBOOK-BACKUP-RESTORE.md) and for any verification
# before trusting a backup, pass a *different* target-database name (e.g.
# nestly_restore_test) so the real database is never touched.
#
# The target database is created (via `createdb`) if it does not already
# exist, then `pg_restore --clean --if-exists` drops and recreates every
# object the dump contains before restoring - safe to run against an
# existing (already-migrated) database of the same name, e.g. as a
# disaster-recovery drill, since --if-exists means an empty database also
# restores cleanly.
#
# Connection is configured via standard libpq environment variables:
#   PGHOST, PGPORT, PGUSER, PGPASSWORD (same as backup-postgres.sh).
set -euo pipefail
cd "$(dirname "$0")/../.."

BACKUP_FILE="${1:?Usage: $0 <backup-file> [target-database]}"
[ -f "$BACKUP_FILE" ] || { echo "Backup file not found: $BACKUP_FILE" >&2; exit 1; }

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-nestly}"
export PGHOST PGPORT PGUSER
export PGPASSWORD="${PGPASSWORD:-nestly_dev}"

TARGET_DB="${2:-${PGDATABASE:-nestly}}"

echo "Restoring ${BACKUP_FILE} -> ${PGUSER}@${PGHOST}:${PGPORT}/${TARGET_DB}"

if ! psql -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '${TARGET_DB}'" | grep -q 1; then
  echo "Database '${TARGET_DB}' does not exist - creating it"
  createdb "$TARGET_DB"
fi

pg_restore \
  --dbname="$TARGET_DB" \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges \
  "$BACKUP_FILE"

echo "Restore complete into ${TARGET_DB}."
echo "Row-count summary (public schema):"
psql -d "$TARGET_DB" -c "
  SELECT relname AS table_name, n_live_tup AS row_count
  FROM pg_stat_user_tables
  WHERE schemaname = 'public'
  ORDER BY relname;
"
