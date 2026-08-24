-- Second dev/test provider, companion to dev-provider-seed.sql ("E2E Test
-- Provider", Bengaluru-only). Originally created ad hoc via psql during a
-- session testing the Jaipur launch and multi-provider assignment/queue
-- scenarios (task: provider-queue early-release model) - this file makes
-- that state reproducible instead of living only in one developer's database.
--
-- Deliberately overlaps E2E Test Provider's coverage: both cover Bengaluru
-- pincode 560034, so a two-provider-in-one-area scenario (reassignment,
-- auto-assignment ranking, "who gets picked") is testable without inventing
-- a third provider. Jaipur pincode 302033 is this provider's own home area,
-- unique to them.
--
-- Password: E2eProviderJaipur!Passw0rd (local/dev only -- never use in
-- staging or production). Same PasswordHasher<Provider> convention as
-- dev-provider-seed.sql's E2E Test Provider credential.
--
-- Usage: psql "$DATABASE_URL" -f database/seed/dev-jaipur-provider-seed.sql
-- (run after dev-provider-seed.sql and the geography/category seeds - it
-- looks up Jaipur/Bengaluru pincodes and every active category by name/code,
-- not by assuming fixed ids.)

INSERT INTO provider (id, legal_name, display_name, provider_type, phone, email, status, onboarding_status, created_at, updated_at, latitude, longitude, location_updated_at_utc)
SELECT
    '55555555-5555-5555-5555-555555555555',
    'Jaipur Test Provider',
    'Jaipur Test Provider',
    'Individual',
    '+919777777777',
    'jaipur-provider@nestly.local',
    'Active',
    'Completed',
    now(),
    now(),
    26.9124,
    75.7873,
    now()
WHERE NOT EXISTS (SELECT 1 FROM provider WHERE phone = '+919777777777');

INSERT INTO provider_auth_identity (id, provider_id, provider, identifier, password_hash, is_primary, created_at)
SELECT
    '77777777-7777-7777-7777-777777777777',
    '55555555-5555-5555-5555-555555555555',
    'EmailPassword',
    'jaipur-provider@nestly.local',
    'AQAAAAIAAYagAAAAEMyK3fmDK9zpAO3Yg+NiimR7ertJ814/ZBxM4BlIrvJ7NYS698AIXgTNRG1n8IWomw==',
    true,
    now()
WHERE NOT EXISTS (SELECT 1 FROM provider_auth_identity WHERE provider_id = '55555555-5555-5555-5555-555555555555');

-- Service areas: Jaipur (home) and Bengaluru pincode 560034 (deliberate
-- overlap with E2E Test Provider - see file doc comment).
INSERT INTO provider_service_area (id, provider_id, city_id, zone_id, pincode_id, is_active)
SELECT gen_random_uuid(), '55555555-5555-5555-5555-555555555555', p.city_id, NULL, p.id, true
FROM pincode p
WHERE p.code IN ('302033', '560034')
  AND NOT EXISTS (
    SELECT 1 FROM provider_service_area a
    WHERE a.provider_id = '55555555-5555-5555-5555-555555555555' AND a.pincode_id = p.id
  );

-- Every day, 08:00-20:00 - wide enough that availability is never the reason
-- a scenario fails to reproduce.
INSERT INTO provider_availability_window (id, provider_id, day_of_week, start_time, end_time, is_active)
SELECT gen_random_uuid(), '55555555-5555-5555-5555-555555555555', d, TIME '08:00', TIME '20:00', true
FROM (VALUES ('Monday'),('Tuesday'),('Wednesday'),('Thursday'),('Friday'),('Saturday'),('Sunday')) AS days(d)
WHERE NOT EXISTS (
  SELECT 1 FROM provider_availability_window w
  WHERE w.provider_id = '55555555-5555-5555-5555-555555555555' AND w.day_of_week = days.d
);

-- Category-level skills across every active category, so this provider is a
-- candidate for any service in the multi-provider scenario, not just one.
INSERT INTO provider_skill_mapping (id, provider_id, category_id, service_id, is_active)
SELECT gen_random_uuid(), '55555555-5555-5555-5555-555555555555', c.id, NULL, true
FROM category c
WHERE c.is_active = true
  AND NOT EXISTS (
    SELECT 1 FROM provider_skill_mapping m
    WHERE m.provider_id = '55555555-5555-5555-5555-555555555555' AND m.category_id = c.id
  );
