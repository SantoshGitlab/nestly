-- Maps every catalog category to E2E City for local development and manual
-- testing, companion to dev-admin-seed.sql / dev-customer-seed.sql /
-- dev-provider-seed.sql.
--
-- Why this exists: category_city_mapping controls which categories show up
-- on the home/categories pages for a given selected city (Catalog's
-- serviceability filter) - it is NOT auto-populated by any EF Core
-- migration (schema only, no data), so a fresh database only ever shows
-- whichever categories happen to already have a mapping row. Historically
-- that was just "E2E Home Cleaning" -> E2E City, leaving the other 9 seeded
-- categories (AC Repair & Service, Appliance Repair, Carpentry, Electrical,
-- Home Cleaning, Painting, Pest Control, Plumbing, Salon for Men, Salon for
-- Women) invisible to anyone testing against E2E City even though their
-- services are fully seeded and active.
--
-- Looked up by name rather than a hardcoded id: city/category ids are
-- generated per environment, so a literal uuid here would only work against
-- the exact database it was copied from.
--
-- Usage: psql "$DATABASE_URL" -f database/seed/dev-category-city-mapping-seed.sql

INSERT INTO category_city_mapping (id, category_id, city_id, is_active)
SELECT gen_random_uuid(), cat.id, city.id, true
FROM category cat
CROSS JOIN (SELECT id FROM city WHERE name = 'E2E City') city
WHERE NOT EXISTS (
    SELECT 1 FROM category_city_mapping ccm
    WHERE ccm.category_id = cat.id AND ccm.city_id = city.id
);
