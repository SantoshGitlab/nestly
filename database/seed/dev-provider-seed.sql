-- Bootstrap a verified provider account for local development and manual
-- testing, companion to dev-admin-seed.sql / dev-customer-seed.sql.
--
-- Why direct insert rather than the real registration endpoint: provider
-- registration also requires OTP verification (POST /auth/registration/otp),
-- and provider-api has NO password login path at all (AuthController.cs:
-- "OTP-only - there is no password login for providers", PROVIDER.md's API
-- surface). SandboxNotificationProvider never exposes the generated code, so
-- there is no way to complete registration OR login through the UI without
-- either brute-forcing the unsalted SHA-256 OTP hash (feasible for a 6-digit
-- space, but not something to rely on) or bootstrapping the row directly, the
-- same reasoning dev-admin-seed.sql / dev-customer-seed.sql already use.
--
-- Status is set to 'Active' (skipping PendingVerification) so this account
-- also exercises the post-KYC-approval paths without needing an admin to
-- manually approve KYC first; ProviderLoginService allows login for both
-- PendingVerification and Active anyway (only Suspended/Deactivated are
-- blocked), so this choice only affects which screens are reachable, not
-- whether login succeeds.
--
-- Mobile: +919888888888 (local/dev only). There is still no password for
-- this account - logging in still requires a real OTP requested through
-- POST /api/v1/auth/login/otp, and reading the resulting code_hash out of
-- provider_otp and brute-forcing the 6-digit space (unsalted SHA-256) since
-- SandboxNotificationProvider never exposes it.
--
-- Usage: psql "$DATABASE_URL" -f database/seed/dev-provider-seed.sql

INSERT INTO provider (id, legal_name, display_name, provider_type, phone, email, status, onboarding_status, created_at, updated_at)
SELECT
    '44444444-4444-4444-4444-444444444444',
    'E2E Test Provider',
    'E2E Test Provider',
    'Individual',
    '+919888888888',
    'e2e-provider@nestly.local',
    'Active',
    'Completed',
    now(),
    now()
WHERE NOT EXISTS (SELECT 1 FROM provider WHERE phone = '+919888888888');
