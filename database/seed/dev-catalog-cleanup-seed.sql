-- Deactivates pre-existing demo/placeholder services that predate the
-- UrbanCompany catalogue replication and don't match anything in UC's real
-- hierarchy: "AC Service & Repair" (a stray sitting directly on the AC &
-- Appliance Repair umbrella, with old-model variants/add-on-groups and a
-- broken icon path) and a loose "Geyser" service on the Geyser subcategory
-- (external picsum.photos placeholder image, duplicating the properly-built
-- "Geyser check-up"). Applied to production 2026-09-09 via
-- POST /admin/catalog/services/{id}/deactivate; mirrored here for local dev.
--
-- IDEMPOTENT.
--
-- Usage: psql "$DATABASE_URL" -f database/seed/dev-catalog-cleanup-seed.sql

\set ON_ERROR_STOP on

BEGIN;

UPDATE service SET is_active = FALSE
WHERE id IN ('351bbad1-00ee-48bf-8f26-cf0d5b21f0c4', '98c0460c-8eb7-49b6-bc33-5e74f2c6de15');

COMMIT;
