-- Bootstrap a verified provider account for local development and manual
-- testing, companion to dev-admin-seed.sql / dev-customer-seed.sql.
--
-- Why direct insert rather than the real registration endpoint: provider
-- registration requires OTP verification (POST /auth/registration/otp), and
-- SandboxNotificationProvider never exposes the generated code, so there is
-- no way to complete registration through the UI without either brute-
-- forcing the unsalted SHA-256 OTP hash (feasible for a 6-digit space, but
-- not something to rely on) or bootstrapping the row directly - the same
-- reasoning dev-admin-seed.sql / dev-customer-seed.sql already use.
--
-- Status is set to 'Active' (skipping PendingVerification) so this account
-- also exercises the post-KYC-approval paths without needing an admin to
-- manually approve KYC first; ProviderLoginService allows login for both
-- PendingVerification and Active anyway (only Suspended/Deactivated are
-- blocked), so this choice only affects which screens are reachable, not
-- whether login succeeds.
--
-- Mobile: +919888888888 (local/dev only, mobile OTP login still has no
-- password/bypass - see above). provider-web's login screen now defaults to
-- Email & password (SHOW_MOBILE_OTP_LOGIN = false in its login page), which
-- *does* have a real password path (ProviderLoginService.LoginWithPasswordAsync,
-- verified via provider_auth_identity same as customer_auth_identity) - the
-- insert below wires that up for this account so the UI is actually reachable.
--
-- Password: E2eProvider!Passw0rd (local/dev only -- never use in staging or
-- production). Hash generated with the same
-- Microsoft.AspNetCore.Identity.PasswordHasher<Provider> ProviderLoginService
-- uses, so POST /api/v1/auth/login/password verifies it correctly.
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

INSERT INTO provider_auth_identity (id, provider_id, provider, identifier, password_hash, is_primary, created_at)
SELECT
    '66666666-6666-6666-6666-666666666666',
    '44444444-4444-4444-4444-444444444444',
    'EmailPassword',
    'e2e-provider@nestly.local',
    'AQAAAAIAAYagAAAAEMZQG+5dFVe/rgmVp6ruyICdfDbHMOeI7otEA1JNHznjyZn3IxQxOvabASqU4D7yzA==',
    true,
    now()
WHERE NOT EXISTS (SELECT 1 FROM provider_auth_identity WHERE provider_id = '44444444-4444-4444-4444-444444444444');
