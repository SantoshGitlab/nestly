-- Bootstrap admin account for local development and E2E testing (tasks 140a-140d).
--
-- Not part of any EF Core migration deliberately: AdminUser.cs documents
-- "admin accounts are provisioned by a Super Admin rather than
-- self-registered" -- there is intentionally no self-registration endpoint,
-- and the Phase 6 seed migration (20260731140113_AddAdminPermissionMatrix)
-- seeds admin_role/admin_permission/role_permission_mapping only, not a
-- first admin_user row, so a brand-new environment has roles but no account
-- that can log in and use them. This script is that one-time bootstrap,
-- separate from schema migrations per DEVOPS.md ("Seed data scripts live in
-- database/seed and must be idempotent").
--
-- Password: E2eTest!Passw0rd (local/dev only -- never use in staging or
-- production; PRODUCTION_DATABASE_* environments should provision their
-- first Super Admin through a controlled, audited out-of-band process
-- instead of running this script).
--
-- The hash below is Microsoft.AspNetCore.Identity.PasswordHasher<T>'s
-- PBKDF2 output for that password (generated with the same hasher
-- AdminUserManagementService/AdminLoginService use, so login verification
-- succeeds) -- not fabricated, and not runnable through psql alone since
-- correct output requires .NET's exact PBKDF2 parameters.
--
-- Usage: psql "$DATABASE_URL" -f database/seed/dev-admin-seed.sql

INSERT INTO admin_user (id, email, password_hash, full_name, status, created_at, updated_at)
SELECT
    '11111111-1111-1111-1111-111111111111',
    'dev-admin@nestly.local',
    'AQAAAAIAAYagAAAAEHUtlYAQ9dvmfB3fa30z2RBFLwvftdksOC40mRupESgmVd0bWbnRyavcIZkKKye1ZQ==',
    'Dev Admin',
    'Active',
    now(),
    now()
WHERE NOT EXISTS (SELECT 1 FROM admin_user WHERE email = 'dev-admin@nestly.local');

UPDATE admin_user
SET role_id = (SELECT id FROM admin_role WHERE name = 'Super Admin')
WHERE email = 'dev-admin@nestly.local' AND role_id IS NULL;
