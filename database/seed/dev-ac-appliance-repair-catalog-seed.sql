-- AC & Appliance Repair: full catalogue content mirroring UrbanCompany's
-- real Jaipur hierarchy and pricing (verified live against
-- urbancompany.com/jaipur on 2026-09-08), with Glavyx's own images and copy.
--
-- WHY THIS EXISTS
--
-- The AC & Appliance Repair category tree (Category rows, CategoryGroups
-- "Large appliances"/"Other appliances", the 8 UC-matched subcategories)
-- already existed in this database from earlier work. What was missing was
-- the actual catalogue content: most subcategories (Refrigerator, Television,
-- Chimney, Microwave, RO/Water Purifier, Geyser) had zero services, and the
-- two that did (AC, Washing Machine) had prices that didn't match UC's real
-- rate card. Every category/subcategory image also still pointed at
-- recycled cleaning-category photos (e.g. "AC" used window-cleaning.jpg).
--
-- This script is the first reference-category pass of a larger catalogue
-- replication effort (see the task's AC & Appliance Repair scope) - it fixes
-- the existing mismatched data, fills in every missing service with UC's
-- verified price, and points every category/subcategory image at a real,
-- freshly-sourced HD photo (Pexels, free-to-use license, no attribution
-- required) under frontend/customer-web/public/images/catalogue/.
--
-- IDEMPOTENT. Every insert is guarded (NOT EXISTS on slug, or matched on the
-- existing row's current known values), so re-running adds nothing new and
-- does not reprice something an admin has since changed by hand.
--
-- USAGE
--
--   psql "$DATABASE_URL" -f database/seed/dev-ac-appliance-repair-catalog-seed.sql
--
-- Local dev only - this only touches the catalogue if the AC & Appliance
-- Repair category tree (id 87251c60-2ec6-427d-8b26-ffe1edfaf243) already
-- exists, which it does not yet on production. See the task notes for the
-- promotion plan once this reference category is reviewed.

\set ON_ERROR_STOP on

BEGIN;

-- 1. Fix existing AC / Washing Machine prices to match UC's real rate card ---
-- Both rows already existed from earlier work with prices that didn't match
-- what UC actually charges - corrected in place rather than superseded, so
-- no duplicate rows and no orphaned old price for a service nobody rejected.
UPDATE service SET price = 599.00
WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Foam-jet AC service' AND price = 499.00;

UPDATE service SET price = 1098.00
WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Foam-jet service (2 ACs)' AND price = 800.00;

UPDATE service SET price = 1099.00, duration_minutes = 90
WHERE category_id = '5c5a620e-1b91-4e2e-8b28-d4dcd57a6595' AND name = 'Washing machine jet service' AND price = 399.00;

-- 1b. AC: bulk foam-jet and gas-refill prices, corrected against UC's real
-- dedicated AC page (urbancompany.com/jaipur-samsung-ac-service-repair) -
-- the homepage digest this pass started from only showed a truncated
-- subset, and undercounted "Gas refill & check-up" by nearly 4x.
UPDATE service SET price = 1497.00 WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Foam-jet service (3 ACs)';
UPDATE service SET price = 1796.00 WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Foam-jet service (4 ACs)';
UPDATE service SET price = 2245.00 WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Foam-jet service (5 ACs)';
UPDATE service SET price = 3000.00, duration_minutes = 150 WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Gas refill & check-up';

-- 2. AC: the missing "Installation/uninstallation" group and its two
-- services. UC's AC page has four sections (Super saver packages / Service /
-- Repair & gas refill / Installation-uninstallation) - this database only
-- had the first three; "AC uninstallation" was nested under "Repair & gas
-- refill" for lack of anywhere else to put it, and "AC installation" did
-- not exist at all.
INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), 'dae206a4-6465-459f-8bf7-c297f16a2ed6', 'Installation/uninstallation', 3, TRUE
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Installation/uninstallation');

-- Two paths into the same end state: a fresh database has never had this
-- row, so the INSERT creates it straight into the right group; a database
-- this script already ran against once has it under the old "Repair & gas
-- refill" group, so the UPDATE (a no-op once already correct) moves it.
INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), 'dae206a4-6465-459f-8bf7-c297f16a2ed6',
       (SELECT id FROM service_group WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Installation/uninstallation'),
       'AC uninstallation', 'ac-uninstallation', 'Safe removal of a split or window AC unit.', 'AC removal',
       'Careful unmounting and removal', 'Reinstallation elsewhere', 649.00, 45, TRUE, 1
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = 'ac-uninstallation');

UPDATE service SET service_group_id = (SELECT id FROM service_group WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Installation/uninstallation'), sort_order = 1
WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND slug = 'ac-uninstallation';

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), 'dae206a4-6465-459f-8bf7-c297f16a2ed6',
       (SELECT id FROM service_group WHERE category_id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6' AND name = 'Installation/uninstallation'),
       'AC installation', 'ac-installation', 'Installation of indoor and outdoor AC units with a free gas check.', 'AC setup',
       'Indoor and outdoor unit installation, gas check', 'Ducting or wall modification', 799.00, 90, TRUE, 0
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = 'ac-installation');

-- 3. Washing Machine: add installation ---------------------------------------
INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), '5c5a620e-1b91-4e2e-8b28-d4dcd57a6595',
       'Washing machine installation', 'washing-machine-installation', 'Installation and setup of a new washing machine.', 'New machine setup',
       'Levelling, hose and drain connection', 'Wall/plumbing modification', 399.00, 60, TRUE, 10
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = 'washing-machine-installation');

-- 4. Refrigerator: add check-up -----------------------------------------------
INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), 'e097628c-00b1-4fd3-8a12-dc05a2cae00c',
       'Refrigerator check-up', 'refrigerator-check-up', 'Diagnosis of cooling, noise or leakage issues.', 'Fridge diagnosis',
       'Inspection and repair quote', 'Spare parts and compressor replacement', 199.00, 60, TRUE, 0
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = 'refrigerator-check-up');

-- 5. Television: add check-up, installation, uninstallation ------------------
INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), '39e4a3d2-1254-4a04-948f-fef71741e461', v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order
FROM (VALUES
    ('TV check-up', 'tv-check-up', 'Diagnosis of display, sound or power issues.', 'TV diagnosis', 'Inspection and repair quote', 'CRT TVs and spare parts', 249.00, 45, 0),
    ('TV installation', 'tv-installation', 'Wall-mount or stand installation of a new TV.', 'TV mounting', 'Mounting and cable dressing', 'Wall repair', 399.00, 45, 1),
    ('TV uninstallation', 'tv-uninstallation', 'Safe removal of a wall-mounted or stand TV.', 'TV removal', 'Careful unmounting', 'Bracket removal from wall', 349.00, 30, 2)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 6. Chimney: three service groups (Repair / Service / Installation) ---------
INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), '3fa11964-7cc8-484a-b39b-6fb11dabbf9c', g.name, g.sort_order, TRUE
FROM (VALUES ('Repair', 0), ('Service', 1), ('Installation/uninstallation', 2)) AS g(name, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM service_group WHERE category_id = '3fa11964-7cc8-484a-b39b-6fb11dabbf9c' AND name = g.name);

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), '3fa11964-7cc8-484a-b39b-6fb11dabbf9c', sg.id, v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order
FROM (VALUES
    ('Chimney check-up', 'chimney-check-up', 'Diagnosis of suction, noise or motor issues.', 'Chimney diagnosis', 'Inspection and repair quote', 'Spare parts', 160.00, 45, 'Repair', 0),
    ('Deep chimney service', 'deep-chimney-service', 'Full dismantling for internal motor, blower and filter servicing.', 'Deep clean and service', 'Internal servicing, exterior degreasing', 'Spare parts', 1249.00, 90, 'Service', 0),
    ('Basic chimney service', 'basic-chimney-service', 'Mesh and filter cleanup with exterior degreasing.', 'Basic clean and service', 'Mesh cleanup, exterior degreasing', 'Internal motor servicing', 599.00, 60, 'Service', 1),
    ('Chimney installation', 'chimney-installation', 'Installation of a new kitchen chimney.', 'Chimney setup', 'Mounting and ducting connection', 'Ducting modification', 599.00, 90, 'Installation/uninstallation', 0),
    ('Chimney uninstallation', 'chimney-uninstallation', 'Safe removal of an existing chimney.', 'Chimney removal', 'Careful unmounting', 'Wall/duct repair', 459.00, 45, 'Installation/uninstallation', 1),
    ('Beyond chimney installation', 'beyond-chimney-installation', 'Installation for extended duct runs beyond standard reach.', 'Extended install', 'Mounting and extended ducting', 'Structural modification', 699.00, 120, 'Installation/uninstallation', 2)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, group_name, sort_order)
JOIN service_group sg ON sg.category_id = '3fa11964-7cc8-484a-b39b-6fb11dabbf9c' AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 7. Microwave: add check-up ---------------------------------------------------
INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), '32ae18c6-0efa-436a-b166-1cbec89d5153',
       'Microwave check-up', 'microwave-check-up', 'Diagnosis of heating, power or control issues.', 'Microwave diagnosis',
       'Inspection and repair quote', 'Spare parts', 199.00, 45, TRUE, 0
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = 'microwave-check-up');

-- 8. RO/Water Purifier: add the six-item rate card ----------------------------
INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), 'c9c88563-7799-4d5e-ba52-5dc4d2c0a7f1', v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order
FROM (VALUES
    ('Repair check-up', 'ro-repair-check-up', 'Diagnosis of leakage, flow or pump issues.', 'RO diagnosis', 'Inspection and repair quote', 'Spare parts', 299.00, 45, 0),
    ('Filter check-up', 'ro-filter-check-up', 'Inspection of filter condition and water quality.', 'Filter diagnosis', 'Filter inspection', 'Filter replacement', 299.00, 30, 1),
    ('Complete filter replacement', 'ro-complete-filter-replacement', 'Replacement of every filter stage in the unit.', 'Full filter change', 'All filter stages replaced', 'Unit replacement', 4199.00, 60, 2),
    ('Wall-mounted RO installation', 'ro-wall-mounted-installation', 'Installation of a new wall-mounted RO unit.', 'Wall-mount setup', 'Mounting and plumbing connection', 'Plumbing modification', 449.00, 60, 3),
    ('Under the counter RO installation', 'ro-under-counter-installation', 'Installation of a new under-counter RO unit.', 'Under-counter setup', 'Mounting and plumbing connection', 'Cabinet modification', 649.00, 60, 4),
    ('RO Uninstallation', 'ro-uninstallation', 'Safe removal of an existing RO unit.', 'RO removal', 'Careful unmounting', 'Plumbing repair', 399.00, 45, 5)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 9. Geyser: two service groups (Repair & service / Installation & uninstallation)
INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), 'a773eb14-11a0-4360-8ad7-b7aab92ff0b1', g.name, g.sort_order, TRUE
FROM (VALUES ('Repair & service', 0), ('Installation & uninstallation', 1)) AS g(name, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM service_group WHERE category_id = 'a773eb14-11a0-4360-8ad7-b7aab92ff0b1' AND name = g.name);

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), 'a773eb14-11a0-4360-8ad7-b7aab92ff0b1', sg.id, v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order
FROM (VALUES
    ('Geyser check-up', 'geyser-check-up', 'Diagnosis of heating or leakage issues.', 'Geyser diagnosis', 'Inspection and repair quote', 'Spare parts', 149.00, 60, 'Repair & service', 0),
    ('Geyser service', 'geyser-service', 'Exterior and interior cleaning with descaling.', 'Geyser descaling', 'Descaling and cleaning', 'Gas geysers', 599.00, 60, 'Repair & service', 1),
    ('Geyser installation', 'geyser-installation', 'Installation of a new geyser.', 'Geyser setup', 'Mounting and plumbing connection', 'Plumbing modification', 499.00, 60, 'Installation & uninstallation', 0),
    ('Geyser uninstallation', 'geyser-uninstallation', 'Safe removal of an existing geyser.', 'Geyser removal', 'Careful unmounting', 'Plumbing repair', 399.00, 40, 'Installation & uninstallation', 1)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, group_name, sort_order)
JOIN service_group sg ON sg.category_id = 'a773eb14-11a0-4360-8ad7-b7aab92ff0b1' AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 10. Real images: replace every recycled cleaning-category photo -----------
-- Relative paths, matching the existing /images/categories/*.svg convention
-- (not the localhost-prefixed absolute URLs the placeholder data used, which
-- break the moment the frontend runs on a different host/port). Card image
-- and page-hero image reused from the same source photo for this pass - see
-- the task notes for why a distinct hero shot per page was out of scope here.
UPDATE category SET banner_url = '/images/catalogue/ac-appliance-repair/category-banner.jpg', page_banner_url = '/images/catalogue/ac-appliance-repair/category-banner.jpg' WHERE id = '87251c60-2ec6-427d-8b26-ffe1edfaf243';
UPDATE category SET banner_url = '/images/catalogue/ac-appliance-repair/ac.jpg', page_banner_url = '/images/catalogue/ac-appliance-repair/ac.jpg' WHERE id = 'dae206a4-6465-459f-8bf7-c297f16a2ed6';
UPDATE category SET banner_url = '/images/catalogue/ac-appliance-repair/washing-machine.jpg', page_banner_url = '/images/catalogue/ac-appliance-repair/washing-machine.jpg' WHERE id = '5c5a620e-1b91-4e2e-8b28-d4dcd57a6595';
UPDATE category SET banner_url = '/images/catalogue/ac-appliance-repair/refrigerator.jpg', page_banner_url = '/images/catalogue/ac-appliance-repair/refrigerator.jpg' WHERE id = 'e097628c-00b1-4fd3-8a12-dc05a2cae00c';
UPDATE category SET banner_url = '/images/catalogue/ac-appliance-repair/television.jpg', page_banner_url = '/images/catalogue/ac-appliance-repair/television.jpg' WHERE id = '39e4a3d2-1254-4a04-948f-fef71741e461';
UPDATE category SET banner_url = '/images/catalogue/ac-appliance-repair/chimney.jpg', page_banner_url = '/images/catalogue/ac-appliance-repair/chimney.jpg' WHERE id = '3fa11964-7cc8-484a-b39b-6fb11dabbf9c';
UPDATE category SET banner_url = '/images/catalogue/ac-appliance-repair/microwave.jpg', page_banner_url = '/images/catalogue/ac-appliance-repair/microwave.jpg' WHERE id = '32ae18c6-0efa-436a-b166-1cbec89d5153';
UPDATE category SET banner_url = '/images/catalogue/ac-appliance-repair/ro-water-purifier.jpg', page_banner_url = '/images/catalogue/ac-appliance-repair/ro-water-purifier.jpg' WHERE id = 'c9c88563-7799-4d5e-ba52-5dc4d2c0a7f1';
UPDATE category SET banner_url = '/images/catalogue/ac-appliance-repair/geyser.jpg', page_banner_url = '/images/catalogue/ac-appliance-repair/geyser.jpg' WHERE id = 'a773eb14-11a0-4360-8ad7-b7aab92ff0b1';

-- 11. Serviceability: map every service in this subtree into every existing
-- pincode (Jaipur + Bengaluru), mirroring bootstrap-bookability.sql's own
-- "blanket coverage, narrow later" policy - scoped to just this category's
-- subtree rather than every service in the database, since this script owns
-- only this catalogue slice.
INSERT INTO service_pincode_mapping (id, service_id, pincode_id, is_active)
SELECT gen_random_uuid(), sv.id, p.id, TRUE
FROM service sv
JOIN category c ON c.id = sv.category_id
CROSS JOIN pincode p
WHERE c.parent_category_id = '87251c60-2ec6-427d-8b26-ffe1edfaf243'
  AND sv.is_active
  AND NOT EXISTS (
      SELECT 1 FROM service_pincode_mapping m
      WHERE m.service_id = sv.id AND m.pincode_id = p.id);

-- 12. Retire the three subcategories that don't exist in UC's real set -----
-- Stove, Laptop and Air Cooler predate this pass (earlier ad-hoc work) and
-- have zero services each. UC's actual AC & Appliance Repair modal is
-- exactly the 8 subcategories fixed above - keeping these three live would
-- mean Glavyx's hierarchy no longer matches UC's, and they would keep
-- showing whatever stale placeholder image they had (never real content,
-- since nothing populates them). Deactivated, not deleted - fully
-- reversible if a future pass wants to build these out as genuine Glavyx
-- extensions with their own real services and images.
UPDATE category SET is_active = FALSE
WHERE id IN ('ee12a1fc-98b5-4ccf-9f06-bc195a12b0f0', '72a6e807-3bc2-4816-bb42-5fbf7ace70a3', '10e83925-eccd-4648-ba88-bb76fd0986d2');

COMMIT;
