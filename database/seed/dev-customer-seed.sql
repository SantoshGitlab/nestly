-- Bootstrap a verified customer account for local development and E2E
-- testing (tasks 140a-140d), companion to dev-admin-seed.sql.
--
-- Why direct insert rather than the real registration endpoint: OTP
-- verification (POST /auth/registration/otp) generates a real random code
-- and sends it through SandboxNotificationProvider, which deliberately never
-- logs or exposes the code anywhere retrievable -- there is no test-mode
-- bypass. Password-mode login (POST /auth/login/password) is a real,
-- unmodified endpoint that only needs a CustomerAuthIdentity row with
-- Provider='EmailPassword' and a matching password hash, so E2E setup
-- authenticates through that endpoint normally; only the *bootstrap* of the
-- account skips the OTP UI, the same reasoning as dev-admin-seed.sql
-- skipping the (nonexistent) admin self-registration UI.
--
-- Password: E2eCustomer!Passw0rd (local/dev only). Hash generated with the
-- same Microsoft.AspNetCore.Identity.PasswordHasher<T> CustomerRegistrationService
-- uses, so POST /auth/login/password verifies it correctly.
--
-- Usage: psql "$DATABASE_URL" -f database/seed/dev-customer-seed.sql

INSERT INTO customer (id, mobile, email, name, date_of_birth, address, city, state, pincode, country, created_at, updated_at, status)
SELECT
    '22222222-2222-2222-2222-222222222222',
    '+919999999999',
    'e2e-customer@nestly.local',
    'E2E Test Customer',
    '1990-01-01T00:00:00Z',
    '',
    '',
    '',
    '',
    'India',
    now(),
    now(),
    'Active'
WHERE NOT EXISTS (SELECT 1 FROM customer WHERE email = 'e2e-customer@nestly.local');

INSERT INTO customer_auth_identity (id, customer_id, provider, identifier, password_hash, is_primary, created_at)
SELECT
    '33333333-3333-3333-3333-333333333333',
    '22222222-2222-2222-2222-222222222222',
    'EmailPassword',
    'e2e-customer@nestly.local',
    'AQAAAAIAAYagAAAAEKwQUh5YlPiRBR1Sa8hcFTkvuPEXs+VwLzQ/bgXqYo91TbriZmcbw7WMVHiM++WnqA==',
    true,
    now()
WHERE NOT EXISTS (
    SELECT 1 FROM customer_auth_identity
    WHERE customer_id = '22222222-2222-2222-2222-222222222222' AND provider = 'EmailPassword'
);
