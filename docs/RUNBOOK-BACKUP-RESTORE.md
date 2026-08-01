# RUNBOOK: PostgreSQL Backup & Restore (task 139)

Companion to `docs/DEVOPS.md` DATABASE OPERATIONS ("Backups: automated daily
backups with tested restore procedure"). This document is the tested
procedure referenced there.

## Scripts

| Script | Purpose |
|---|---|
| `database/scripts/backup-postgres.sh` | `pg_dump -Fc` (custom format, compressed) backup of the Nestly database |
| `database/scripts/restore-postgres.sh` | `pg_restore --clean --if-exists` into a target database (creates it if missing) |

Both are configured entirely through standard libpq environment variables
(`PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD`) - no connection
string is ever hardcoded, matching `docs/DEVOPS.md` CONFIGURATION AND
SECRETS. Defaults match the local `docker-compose.yml` database so a bare
invocation backs up/restores local dev data.

## Automated schedule

`.github/workflows/backup-postgres.yml` runs `backup-postgres.sh` daily at
02:00 UTC via GitHub Actions `schedule:` (cron), against
`PRODUCTION_DATABASE_*` secrets that do not exist yet (no real production
host exists - see `docs/DEVOPS.md` OPEN DECISIONS). A GitHub Actions cron
workflow was chosen over a systemd timer / host crontab because this repo
already has no persistent host of its own and GitHub Actions is the already-
decided CI/CD platform; **once a real production host is provisioned**, the
lower-latency alternative is a systemd timer unit calling
`backup-postgres.sh` directly on that host (same script, no network hop to
reach the database) - documented here as the fallback, not implemented,
since there is no host to install it on yet.

**Storage target**: `docs/DEVOPS.md` OPEN DECISIONS lists the cloud
provider as unresolved, so there is no chosen durable object-storage bucket
yet. `backup-postgres.sh` writes locally and, only when `BACKUP_S3_BUCKET`
is set, also uploads via `aws s3 cp` to S3-compatible storage. Until that
secret exists, `backup-postgres.yml` falls back to attaching the dump as a
GitHub Actions workflow artifact (14-day retention) purely so a scheduled
run still produces something inspectable - **this is explicitly not a real
backup destination** (tied to one workflow run, capped retention, no stated
durability/encryption guarantee) and must not be relied on once real
production data exists.

## Restore drill - actually executed 2026-08-01

The drill below was run for real against the local docker-compose Postgres
(`nestly-postgres-1`, started from this repo's root `docker-compose.yml`),
using the actual committed scripts (copied into the container and executed
via `bash`, not hand-typed equivalents), to verify the backup/restore path
end-to-end before trusting it. Commands and real output:

### 1. Stage the scripts inside the running Postgres container

```
$ docker exec nestly-postgres-1 mkdir -p /tmp/drill/database/scripts /tmp/drill/database/backups
$ docker cp database/scripts/backup-postgres.sh  nestly-postgres-1:/tmp/drill/database/scripts/backup-postgres.sh
$ docker cp database/scripts/restore-postgres.sh nestly-postgres-1:/tmp/drill/database/scripts/restore-postgres.sh
$ docker exec nestly-postgres-1 chmod +x /tmp/drill/database/scripts/backup-postgres.sh /tmp/drill/database/scripts/restore-postgres.sh
```

(`postgres:16-alpine` ships `pg_dump`/`pg_restore`/`psql`/`createdb` and
`bash`, so the scripts run unmodified inside the container. In a real
deployment these run on a host/runner with `postgresql-client` installed
against a remote database instead - see `backup-postgres.yml`.)

### 2. Record exact baseline row counts (source `nestly` database)

Used an exact per-table `SELECT COUNT(*)` (not `pg_stat_user_tables.n_live_tup`,
which is a planner estimate and was observed to be stale/wrong on a couple
of tables before an `ANALYZE`) across all 74 public tables. Total: 74 tables,
non-zero rows in `__EFMigrationsHistory` (38), `admin_permission` (34),
`admin_role` (9), `customer_session` (1), `login_attempt` (1),
`notification_template` (24), `role_permission_mapping` (125),
`system_setting` (7) - everything else 0 (a lightly-seeded dev database:
RBAC/notification-template/system-setting seed data plus one real customer
session and one login attempt from local testing).

### 3. Run the actual backup script

```
$ docker exec -e PGPASSWORD=nestly_dev -e PGUSER=nestly -e PGDATABASE=nestly -e PGHOST=localhost -e PGPORT=5432 \
    nestly-postgres-1 bash /tmp/drill/database/scripts/backup-postgres.sh

Backing up nestly@localhost:5432/nestly -> database/backups/nestly_20260801T055655Z.dump
Backup complete: database/backups/nestly_20260801T055655Z.dump (204390 bytes)
BACKUP_S3_BUCKET not set - backup left on local disk only (fine for local dev; a real staging/production run needs off-host storage configured, see this script's header comment).
```

### 4. Run the actual restore script into a throwaway database

Restored into `nestly_restore_test` (never into the live `nestly` database -
this is the documented safe pattern for verifying a backup):

```
$ docker exec -e PGPASSWORD=nestly_dev -e PGUSER=nestly -e PGHOST=localhost -e PGPORT=5432 \
    nestly-postgres-1 bash /tmp/drill/database/scripts/restore-postgres.sh \
    /tmp/drill/database/backups/nestly_20260801T055655Z.dump nestly_restore_test

Restoring /tmp/drill/database/backups/nestly_20260801T055655Z.dump -> nestly@localhost:5432/nestly_restore_test
Database 'nestly_restore_test' does not exist - creating it
Restore complete into nestly_restore_test.
Row-count summary (public schema):
 ... (74 rows, one per table)
```

### 5. Verify - exact row counts, source vs. restored

Ran the same exact-`COUNT(*)` query (generated dynamically from
`pg_tables`, not hand-listed) against both databases and diffed the output:

```
$ diff source_counts.txt restored_counts.txt && echo "IDENTICAL - restore verified row-for-row across all 74 tables"
IDENTICAL - restore verified row-for-row across all 74 tables
```

All 74 tables matched exactly, including the non-zero ones
(`role_permission_mapping`: 125/125, `notification_template`: 24/24,
`admin_permission`: 34/34, `admin_role`: 9/9, `system_setting`: 7/7,
`__EFMigrationsHistory`: 38/38, `customer_session`: 1/1, `login_attempt`: 1/1).

### 6. Verify - content, not just counts

Row counts alone can't catch column-level corruption, so also checksummed
full row content on the two populated business tables (`md5` of the
row-ordered, concatenated row text):

```
notification_template: 29e5b16bb86782214028c8dbb9265944  (source)
                        29e5b16bb86782214028c8dbb9265944  (restored)
role_permission_mapping: c23e97bd71d91a0557874dea44c54c72 (source)
                          c23e97bd71d91a0557874dea44c54c72 (restored)
```

Identical. Also compared constraint counts (`pg_constraint` rows in the
`public` schema, i.e. every PK/FK/unique/check constraint): **148 in both**
the source and restored database, confirming `pg_restore` recreated the
full schema (not just data) faithfully, including foreign keys and unique
indexes.

### 7. Clean up

```
$ docker exec nestly-postgres-1 psql -U nestly -d postgres -c "DROP DATABASE nestly_restore_test;"
DROP DATABASE
$ docker exec nestly-postgres-1 rm -rf /tmp/drill
```

Confirmed only the real `nestly` database remained afterward.

## Restoring over a live/broken database (real incident, not the drill above)

The drill above always restores into a *new*, differently-named database to
avoid ever touching real data during verification. For an actual
disaster-recovery restore where the target database name is meant to be
the real one:

```
./database/scripts/restore-postgres.sh <backup-file> nestly
```

`pg_restore --clean --if-exists` (baked into the script) drops every object
the dump contains before recreating it, so this is destructive to whatever
is currently in the target database - **only run this against the real
database name during an actual incident**, after confirming the backup file
is the intended one and, ideally, after taking one more backup of the
current (broken) state first in case the restore itself needs to be undone.

## Restore time / data-loss expectations (RTO/RPO)

Not yet formally defined - `docs/DEVOPS.md` does not set explicit RTO/RPO
targets, and there is no production traffic yet to size them against. As a
starting point based on this drill: a ~200KB dump (current dev-seed-sized
database) restores in well under a second; actual production RTO will scale
with real data volume and the daily backup cadence caps RPO at ~24 hours
until/unless a more frequent schedule or WAL-based continuous archiving is
adopted - both open items for whoever finalizes the OPEN DECISIONS in
`docs/DEVOPS.md`.
