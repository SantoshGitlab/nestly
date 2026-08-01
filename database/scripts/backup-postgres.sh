#!/usr/bin/env bash
# Takes a pg_dump backup of the Nestly database in custom (-Fc) format,
# suitable for restore-postgres.sh / pg_restore.
#
# Usage:
#   ./database/scripts/backup-postgres.sh [output-dir]
#
# Connection is configured via standard libpq environment variables (never
# via a hardcoded connection string in this script - docs/DEVOPS.md
# CONFIGURATION AND SECRETS: secrets never in source code):
#   PGHOST, PGPORT, PGDATABASE, PGUSER, PGPASSWORD
# Falls back to the local docker-compose database (see docker-compose.yml)
# when unset, so a bare invocation backs up local dev data.
#
# Output: <output-dir>/nestly_<UTC timestamp>.dump (gzip is not applied
# separately - custom format (-Fc) is already compressed).
#
# Storage target: writes locally to database/backups/ (gitignored) by
# default. That is NOT a real backup destination on its own - a local file
# on the machine that produced it is lost if that machine is lost. Once
# docs/DEVOPS.md's "cloud provider" OPEN DECISION is resolved, this script
# should also ship the resulting file to durable off-host storage (e.g. S3
# or an equivalent object store). The optional upload block below is wired
# for S3-compatible storage (`aws s3 cp`) and only runs when BACKUP_S3_BUCKET
# is set, so it is inert everywhere else, including local dev and until a
# bucket exists.
#
# Scheduling: see .github/workflows/backup-postgres.yml (daily cron) for how
# this script is expected to run against the real staging/production
# databases once PRODUCTION_DATABASE_CONNECTION_STRING-equivalent secrets
# exist - a GitHub Actions cron matches this repo's existing devops pattern
# (CI/CD already lives in .github/workflows/) better than a systemd timer,
# since there is no persistent host of our own to run cron on yet either.
set -euo pipefail
cd "$(dirname "$0")/../.."

OUTPUT_DIR="${1:-database/backups}"
mkdir -p "$OUTPUT_DIR"

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-nestly}"
PGUSER="${PGUSER:-nestly}"
export PGHOST PGPORT PGDATABASE PGUSER
export PGPASSWORD="${PGPASSWORD:-nestly_dev}"

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
BACKUP_FILE="${OUTPUT_DIR}/nestly_${TIMESTAMP}.dump"

echo "Backing up ${PGUSER}@${PGHOST}:${PGPORT}/${PGDATABASE} -> ${BACKUP_FILE}"

pg_dump \
  --format=custom \
  --no-owner \
  --no-privileges \
  --file="$BACKUP_FILE" \
  "$PGDATABASE"

SIZE_BYTES="$(wc -c < "$BACKUP_FILE" | tr -d ' ')"
echo "Backup complete: ${BACKUP_FILE} (${SIZE_BYTES} bytes)"

if [ -n "${BACKUP_S3_BUCKET:-}" ]; then
  echo "BACKUP_S3_BUCKET set - uploading to s3://${BACKUP_S3_BUCKET}/$(basename "$BACKUP_FILE")"
  aws s3 cp "$BACKUP_FILE" "s3://${BACKUP_S3_BUCKET}/$(basename "$BACKUP_FILE")"
else
  echo "BACKUP_S3_BUCKET not set - backup left on local disk only (fine for" \
       "local dev; a real staging/production run needs off-host storage" \
       "configured, see this script's header comment)."
fi

# Retention: keep the most recent 14 local dumps, delete older ones. Applies
# regardless of whether an off-host upload also happened, so a scheduled run
# on a long-lived host does not fill the disk. Durable retention policy (how
# long backups are kept in off-host storage) belongs to that storage
# provider once chosen, not to this script.
KEEP=14
# shellcheck disable=SC2012
ls -1t "${OUTPUT_DIR}"/nestly_*.dump 2>/dev/null | tail -n +$((KEEP + 1)) | while read -r old; do
  echo "Pruning old local backup: $old"
  rm -f "$old"
done
