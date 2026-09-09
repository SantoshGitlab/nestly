-- Electrician, Plumber & Carpenter: full catalogue content mirroring
-- UrbanCompany's real hierarchy and pricing (verified live against
-- urbancompany.com on 2026-09-09). Sixth and final category in the same
-- replication pass as dev-ac-appliance-repair-catalog-seed.sql /
-- dev-womens-salon-spa-catalog-seed.sql / dev-mens-salon-massage-catalog-seed.sql
-- / dev-cleaning-catalog-seed.sql / dev-painting-waterproofing-catalog-seed.sql -
-- see those files' headers for the full rationale.
--
-- STRUCTURAL CHANGE: the old flat top-level (7 direct services) becomes 3
-- UC-named subcategories - Electrician, Plumbers, Carpenter - each holding
-- a representative subset of UC's real named line items and prices (UC's
-- real pages run 6-11 sub-groups and 20-30+ line items per trade; only the
-- headline items are replicated here, same scoping call as every other
-- category in this pass - e.g. AC & Appliance Repair skipping the spare-
-- parts price list, Cleaning skipping the mini-services add-ons).
--
-- IDEMPOTENT - see the AC & Appliance Repair seed for the pattern.
--
-- USAGE: psql "$DATABASE_URL" -f database/seed/dev-electrician-plumber-carpenter-catalog-seed.sql

\set ON_ERROR_STOP on

BEGIN;

-- 1. Fix the umbrella's banner.
UPDATE category SET banner_url = '/images/catalogue/electrician-plumber-carpenter/category-banner.jpg',
       page_banner_url = '/images/catalogue/electrician-plumber-carpenter/category-banner.jpg'
WHERE id = 'e6e4e568-482b-4455-8378-6181c84394e8';

-- 2. Electrician - new subcategory, reparenting 3 existing services.
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Electrician', 'electrician',
       'Electrical repairs and installations, at home.',
       '/images/catalogue/electrician-plumber-carpenter/electrician.jpg', '/images/catalogue/electrician-plumber-carpenter/electrician.jpg',
       TRUE, FALSE, 0, 'e6e4e568-482b-4455-8378-6181c84394e8'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'electrician');

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'electrician'),
       name = 'Electrician consultation', slug = 'electrician-consultation', price = 49.00, duration_minutes = 35, sort_order = 0,
       description = 'An electrician will assess your needs upon arrival at your home.',
       short_description = 'Visit and assessment',
       inclusions = E'On-site assessment of the electrical issue\nQuote provided before work begins\nNo obligation to proceed after the visit',
       exclusions = E'Repair or installation work itself\nSpare parts or materials',
       cover_image_url = '/images/catalogue/electrician-plumber-carpenter/services/electrician-consultation.jpg'
WHERE slug IN ('electrician-visit', 'electrician-consultation');

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'electrician'),
       name = 'Fan repair', price = 149.00, sort_order = 1,
       description = 'Repair for ceiling, exhaust or wall-mounted fans.',
       short_description = 'Fan repair',
       inclusions = E'Diagnosis of the fan fault\nRepair using standard spare parts\nFinal functionality check',
       exclusions = E'BLDC or smart fan repair\nFan replacement',
       cover_image_url = '/images/catalogue/electrician-plumber-carpenter/services/fan-repair.jpg'
WHERE slug = 'fan-repair-ceiling-exhaust-wall-e2e';

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'electrician'),
       name = 'Switchboard repair & replacement', slug = 'switchboard-repair-replacement', price = 99.00, sort_order = 2,
       description = 'Repair or replacement using existing in-wall wiring.',
       short_description = 'Switchboard repair',
       inclusions = E'Diagnosis of the switchboard fault\nRepair or replacement using existing wiring\nFinal safety check',
       exclusions = E'New wiring runs\nSwitchbox relocation',
       cover_image_url = '/images/catalogue/electrician-plumber-carpenter/services/switchboard-repair-replacement.jpg'
WHERE slug IN ('switchboard-switchbox-repair-e2e', 'switchboard-repair-replacement');

INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'electrician'), v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/electrician-plumber-carpenter/services/' || v.slug || '.jpg'
FROM (VALUES
    ('Switch/socket repair & replacement','switch-socket-repair-replacement','Repair or replacement of a faulty switch or socket.','Switch/socket repair',E'Diagnosis of the switch or socket fault\nRepair or replacement with a standard fitting\nFinal safety check',E'Wiring beyond the switch point\nDecorative or designer fittings',69.00,20,3),
    ('Regular ceiling fan installation','regular-ceiling-fan-installation','Installation of a standard ceiling fan.','Fan installation',E'Mounting and wiring of the fan\nBalancing and speed check\nRemote or regulator setup if applicable',E'Fan unit cost\nCeiling reinforcement work',99.00,45,4),
    ('MCB/fuse repair','mcb-fuse-repair','Repair of a faulty MCB or fuse in the distribution board.','MCB/fuse repair',E'Diagnosis of the trip or fuse fault\nRepair using standard components\nFinal load test',E'Full distribution board replacement\nMain line fault beyond the MCB',149.00,40,5)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 3. Plumbers - new subcategory, reparenting 2 existing services.
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Plumbers', 'plumbers',
       'Plumbing repairs and installations, at home.',
       '/images/catalogue/electrician-plumber-carpenter/plumbers.jpg', '/images/catalogue/electrician-plumber-carpenter/plumbers.jpg',
       TRUE, FALSE, 1, 'e6e4e568-482b-4455-8378-6181c84394e8'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'plumbers');

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'plumbers'),
       price = 129.00, sort_order = 0,
       description = 'Repair for a leaking or faulty tap.',
       short_description = 'Tap repair',
       inclusions = E'Diagnosis of the leak or fault\nRepair using standard washers and fittings\nFinal leak check',
       exclusions = E'Tap replacement\nPipe fitting beyond the tap',
       cover_image_url = '/images/catalogue/electrician-plumber-carpenter/services/tap-repair.jpg'
WHERE slug = 'tap-repair-e2e';

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'plumbers'),
       sort_order = 1,
       description = 'Repair for a leaking or faulty flush tank.',
       short_description = 'Flush tank repair',
       inclusions = E'Diagnosis of the flush tank fault\nRepair using standard internal fittings\nFinal flush and leak check',
       exclusions = E'Flush tank replacement\nTile or ceramic repair',
       cover_image_url = '/images/catalogue/electrician-plumber-carpenter/services/flush-tank-repair.jpg'
WHERE slug = 'flush-tank-repair-e2e';

INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'plumbers'), v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/electrician-plumber-carpenter/services/' || v.slug || '.jpg'
FROM (VALUES
    ('Toilet seat cover installation','toilet-seat-cover-installation','Installation of a new toilet seat cover.','Seat cover install',E'Removal of the old seat cover if present\nFitting and securing the new cover\nStability check',E'Seat cover cost\nToilet bowl repair',149.00,30,2),
    ('Shower repair','shower-repair','Repair for a leaking or low-pressure shower.','Shower repair',E'Diagnosis of the leak or pressure issue\nRepair using standard fittings\nFinal flow and leak check',E'Shower unit replacement\nWall tile or plumbing-line repair',99.00,30,3),
    ('Wash basin leakage repair','wash-basin-leakage-repair','Repair for a leaking wash basin connection.','Basin leak repair',E'Diagnosis of the leak source\nRepair using standard fittings and sealant\nFinal leak check',E'Basin replacement\nCountertop or tile repair',99.00,30,4),
    ('Drain blockage removal','drain-blockage-removal','Clears a blocked drain for free-flowing water.','Drain unclog',E'Diagnosis of the blockage location\nClearing using standard tools\nFinal flow check',E'Sewer line or deep pipeline blockage\nPipe replacement',199.00,40,5)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 4. Carpenter - new subcategory, reparenting 2 existing services.
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Carpenter', 'carpenter',
       'Carpentry repairs and installations, at home.',
       '/images/catalogue/electrician-plumber-carpenter/carpenter.jpg', '/images/catalogue/electrician-plumber-carpenter/carpenter.jpg',
       TRUE, FALSE, 2, 'e6e4e568-482b-4455-8378-6181c84394e8'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'carpenter');

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'carpenter'),
       name = 'Decor installation (Small)', slug = 'decor-installation-small', price = 79.00, sort_order = 0,
       description = 'For item sizes up to 8 x 8 inch, such as photo frames, clocks and key holders.',
       short_description = 'Small decor install',
       inclusions = E'Wall drilling and fitting for the item\nSecure mounting and levelling\nFinal stability check',
       exclusions = E'Decor item cost\nPorcelain tile special drilling',
       cover_image_url = '/images/catalogue/electrician-plumber-carpenter/services/decor-installation-small.jpg'
WHERE slug IN ('drill-hang-wall-decor-e2e', 'decor-installation-small');

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'carpenter'),
       name = 'Cupboard repair & installation', slug = 'cupboard-repair-installation', price = 89.00, sort_order = 1,
       description = 'Repair or installation of cupboard hinges and fittings.',
       short_description = 'Cupboard repair',
       inclusions = E'Diagnosis of the hinge or door fault\nRepair or installation using standard fittings\nSmooth-operation check',
       exclusions = E'New cupboard construction\nHardware cost beyond standard fittings',
       cover_image_url = '/images/catalogue/electrician-plumber-carpenter/services/cupboard-repair-installation.jpg'
WHERE slug = 'cupboard-hinge-installation-e2e';

INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'carpenter'), v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/electrician-plumber-carpenter/services/' || v.slug || '.jpg'
FROM (VALUES
    ('Door repair','door-repair','Repair for a sticking, loose or damaged wooden door.','Door repair',E'Diagnosis of the door fault\nRepair using standard tools and fittings\nSmooth-operation check',E'New door installation\nDoor replacement',99.00,45,2),
    ('Mirror installation','mirror-installation','Installation of a wall-mounted mirror.','Mirror install',E'Wall drilling and mounting hardware fitting\nSecure mounting and levelling\nFinal stability check',E'Mirror cost\nPorcelain tile special drilling',149.00,40,3),
    ('Shelf installation','shelf-installation','Installation of a wall-mounted shelf.','Shelf install',E'Wall drilling and bracket fitting\nSecure mounting and levelling\nWeight-bearing check',E'Shelf unit cost\nPorcelain tile special drilling',99.00,30,4),
    ('Door lock replace/install','door-lock-replace-install','Replacement or new installation of a door lock.','Door lock',E'Removal of the old lock if present\nFitting and aligning the new lock\nFinal operation check',E'Lock unit cost\nDoor repair or replacement',129.00,40,5)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 5. Serviceability: map every new/moved service into every pincode the
-- umbrella category already serves.
INSERT INTO service_pincode_mapping (id, service_id, pincode_id, is_active)
SELECT gen_random_uuid(), sv.id, p.id, TRUE
FROM service sv
JOIN category c ON c.id = sv.category_id
CROSS JOIN pincode p
WHERE c.parent_category_id = 'e6e4e568-482b-4455-8378-6181c84394e8'
  AND sv.is_active
  AND NOT EXISTS (
      SELECT 1 FROM service_pincode_mapping m
      WHERE m.service_id = sv.id AND m.pincode_id = p.id);

COMMIT;
