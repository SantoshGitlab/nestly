-- Cleaning: full catalogue content mirroring UrbanCompany's real Jaipur
-- hierarchy and pricing (verified live against urbancompany.com on
-- 2026-09-09). Fourth category in the same replication pass as
-- dev-ac-appliance-repair-catalog-seed.sql / dev-womens-salon-spa-catalog-seed.sql
-- / dev-mens-salon-massage-catalog-seed.sql - see those files' headers for
-- the full rationale.
--
-- STRUCTURAL CHANGE: "Home Cleaning" (flat top-level with 5 direct services
-- plus 2 pre-existing subcategories) becomes "Cleaning", matching UC's real
-- 4-subcategory shape: Bathroom Cleaning and Kitchen Cleaning (both
-- pre-existing - fixed prices, added UC's real grouped line items), plus two
-- new subcategories - Sofa & Carpet Cleaning (UC's real "Living & Bedroom
-- Cleaning" page, reparenting Mattress/Carpet/Dining-table-chairs cleaning
-- out of the old flat top-level) and Full Home Cleaning (UC's real
-- "Full Home/By Room Cleaning" page, replacing the old flat ad-hoc
-- "Deep Home Cleaning" single item with UC's real apartment/bungalow/partial
-- tiers). The old top-level's flat "Chimney cleaning" is deactivated since
-- it's now properly represented under Kitchen Cleaning's own Chimney group.
--
-- UC's real subcategory pages carry many more granular "mini services"
-- (single add-ons like "Mirror cleaning ₹59") than replicated here - same
-- scoping call as AC & Appliance Repair's spare-parts price list: the
-- headline named services are replicated with real prices, not every add-on.
--
-- IDEMPOTENT - see the AC & Appliance Repair seed for the pattern.
--
-- USAGE: psql "$DATABASE_URL" -f database/seed/dev-cleaning-catalog-seed.sql

\set ON_ERROR_STOP on

BEGIN;

-- 1. Rename the umbrella to match UC's real naming, fix its image.
UPDATE category SET name = 'Cleaning', slug = 'cleaning',
       banner_url = '/images/catalogue/cleaning/category-banner.jpg',
       page_banner_url = '/images/catalogue/cleaning/category-banner.jpg'
WHERE id = 'bbafa0c3-e612-45c9-815d-29b78b502c45';

-- 2. Bathroom Cleaning: fix prices to UC's real rate card, rename the base
-- item to match UC's real bestseller, add the missing "Move-in" tier, group
-- everything to mirror UC's real Value-deals / One-time-deep-clean split.
UPDATE category SET banner_url = '/images/catalogue/cleaning/bathroom-cleaning.jpg',
       page_banner_url = '/images/catalogue/cleaning/bathroom-cleaning.jpg'
WHERE id = '3036ba07-a0db-4109-be6d-2a0eb622f103';

INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), '3036ba07-a0db-4109-be6d-2a0eb622f103', g.name, g.sort_order, TRUE
FROM (VALUES ('Value deals',0),('One time deep clean',1)) AS g(name, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = '3036ba07-a0db-4109-be6d-2a0eb622f103' AND name = g.name);

UPDATE service SET name = 'Intense bathroom cleaning', slug = 'intense-bathroom-cleaning', price = 549.00,
       duration_minutes = 60, sort_order = 0,
       description = 'Recommended for deep-cleaning and tough stains.',
       short_description = 'Deep bathroom clean',
       inclusions = E'Floor and tile cleaning with scrubbing machine\nFixture and fitting deep clean\nRecommended for deep-cleaning and tough stains',
       exclusions = E'Repair of broken fixtures\nPest treatment',
       service_group_id = (SELECT id FROM service_group WHERE category_id = '3036ba07-a0db-4109-be6d-2a0eb622f103' AND name = 'One time deep clean'),
       cover_image_url = '/images/catalogue/cleaning/services/intense-bathroom-cleaning.jpg'
WHERE slug IN ('bathroom-cleaning-svc-e2e', 'intense-bathroom-cleaning');

UPDATE service SET price = 958.00, sort_order = 0,
       inclusions = E'Floor and tile cleaning with a scrub machine for 2 bathrooms\nFixture and fitting deep clean\nSuitable for regular 2-bathroom homes',
       exclusions = E'Repair of broken fixtures\nPest treatment',
       service_group_id = (SELECT id FROM service_group WHERE category_id = '3036ba07-a0db-4109-be6d-2a0eb622f103' AND name = 'Value deals'),
       cover_image_url = '/images/catalogue/cleaning/services/intense-cleaning-2-bathroom.jpg'
WHERE slug = 'intense-cleaning-2-bathroom-e2e';

UPDATE service SET price = 1377.00, sort_order = 1,
       inclusions = E'Floor and tile cleaning with a scrub machine for 3 bathrooms\nFixture and fitting deep clean\nSuitable for regular 3-bathroom homes',
       exclusions = E'Repair of broken fixtures\nPest treatment',
       service_group_id = (SELECT id FROM service_group WHERE category_id = '3036ba07-a0db-4109-be6d-2a0eb622f103' AND name = 'Value deals'),
       cover_image_url = '/images/catalogue/cleaning/services/intense-cleaning-3-bathroom.jpg'
WHERE slug = 'intense-cleaning-3-bathroom-e2e';

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), '3036ba07-a0db-4109-be6d-2a0eb622f103',
       (SELECT id FROM service_group WHERE category_id = '3036ba07-a0db-4109-be6d-2a0eb622f103' AND name = 'One time deep clean'),
       'Move-in bathroom cleaning', 'move-in-bathroom-cleaning',
       'Recommended before moving into a new or unused bathroom.', 'Move-in ready clean',
       E'Floor and tile deep cleaning\nExtra 30 minutes of machine scrubbing\nFixture and fitting wipe-down',
       E'Repair of broken fixtures\nPest treatment',
       629.00, 90, TRUE, 1, '/images/catalogue/cleaning/services/move-in-bathroom-cleaning.jpg'
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = 'move-in-bathroom-cleaning');

-- 3. Kitchen Cleaning: fix price on the existing item, add UC's real
-- Chimney-cleaning and Appliance-cleaning groups.
UPDATE category SET banner_url = '/images/catalogue/cleaning/kitchen-cleaning.jpg',
       page_banner_url = '/images/catalogue/cleaning/kitchen-cleaning.jpg'
WHERE id = 'c1a249c2-1d41-4ab5-b458-287269e86f1d';

INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), 'c1a249c2-1d41-4ab5-b458-287269e86f1d', g.name, g.sort_order, TRUE
FROM (VALUES ('Chimney cleaning',0),('Complete kitchen cleaning',1),('Appliance cleaning',2)) AS g(name, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = 'c1a249c2-1d41-4ab5-b458-287269e86f1d' AND name = g.name);

UPDATE service SET name = 'Complete kitchen cleaning', slug = 'complete-kitchen-cleaning', price = 999.00,
       duration_minutes = 150, sort_order = 0,
       inclusions = E'Cleaning of objects and surfaces with steam machine\nCabinet, tile and slab deep cleaning\nAppliance exterior wipe-down',
       exclusions = E'Chimney motor cleaning or repair\nAppliance repair',
       service_group_id = (SELECT id FROM service_group WHERE category_id = 'c1a249c2-1d41-4ab5-b458-287269e86f1d' AND name = 'Complete kitchen cleaning'),
       cover_image_url = '/images/catalogue/cleaning/services/complete-kitchen-cleaning.jpg'
WHERE slug IN ('kitchen-deep-cleaning-e2e', 'complete-kitchen-cleaning');

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), 'c1a249c2-1d41-4ab5-b458-287269e86f1d', sg.id,
       v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/cleaning/services/' || v.slug || '.jpg'
FROM (VALUES
    ('Regular chimney cleaning','regular-chimney-cleaning','Chimney exterior, mesh and filter cleaning with a steam machine.','Exterior chimney clean',E'Chimney exterior cleaning\nMesh and filter cleaning with steam\nGrease and oil removal',E'Motor cleaning or repair\nAutomatic chimney cleaning',399.00,45,'Chimney cleaning',0),
    ('Regular chimney & stove cleaning','regular-chimney-stove-cleaning','Stovetops, burners, mesh and filter cleaning with steam.','Chimney + stove clean',E'Stovetop and burner cleaning\nChimney mesh and filter cleaning with steam\nGrease and oil removal',E'Motor cleaning or repair\nAutomatic chimney cleaning',548.00,85,'Chimney cleaning',1),
    ('Fridge cleaning','fridge-cleaning','Deep cleaning of fridge exterior with steam machine.','Fridge deep clean',E'Exterior deep cleaning with steam machine\nWet wiping of interior\nRemoval of food spills, stains and odour',E'Repair of cooling or electrical faults\nDefrosting service',399.00,40,'Appliance cleaning',0),
    ('Gas stove cleaning','gas-stove-cleaning','Stovetops, burners and knobs cleaning with steam machine.','Stove deep clean',E'Stovetop, burner and knob cleaning with steam\nWet wiping to remove burnt stains and odour\nExterior wipe-down',E'Repair of gas leaks or ignition faults\nRegulator or pipe replacement',99.00,30,'Appliance cleaning',1)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, group_name, sort_order)
JOIN service_group sg ON sg.category_id = 'c1a249c2-1d41-4ab5-b458-287269e86f1d' AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 4. Sofa & Carpet Cleaning - new subcategory (UC's real "Living & Bedroom
-- Cleaning" page). Reparents the 3 services that used to sit directly on
-- the old flat top-level.
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Sofa & Carpet Cleaning', 'sofa-carpet-cleaning',
       'Sofa, carpet, mattress and furniture cleaning, at home.',
       '/images/catalogue/cleaning/sofa-carpet-cleaning.jpg', '/images/catalogue/cleaning/sofa-carpet-cleaning.jpg',
       TRUE, FALSE, 2, (SELECT id FROM category WHERE slug = 'cleaning')
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'sofa-carpet-cleaning');

INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning'), g.name, g.sort_order, TRUE
FROM (VALUES ('Sofa & carpet',0),('Mattress & bed',1),('Dining table & chairs',2)) AS g(name, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning') AND name = g.name);

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning'),
       service_group_id = (SELECT id FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning') AND name = 'Mattress & bed'),
       sort_order = 0,
       inclusions = E'Vacuuming and foam-based shampooing on both sides\nMinimal water usage\nReady to use in 1 to 2 hours',
       exclusions = E'Mattress cover or protector replacement\nStain removal for ink or permanent marks',
       cover_image_url = '/images/catalogue/cleaning/services/mattress-cleaning.jpg'
WHERE slug = 'mattress-cleaning-e2e';

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning'),
       service_group_id = (SELECT id FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning') AND name = 'Sofa & carpet'),
       sort_order = 1,
       inclusions = E'Vacuuming and foam-based shampooing for stain removal\n2-3 hours of dry time under the fan\nSuitable for wall-to-wall and area carpets',
       exclusions = E'Carpet repair or re-binding\nPermanent stain guarantee',
       cover_image_url = '/images/catalogue/cleaning/services/carpet-cleaning.jpg'
WHERE slug = 'carpet-cleaning-e2e';

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning'),
       service_group_id = (SELECT id FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning') AND name = 'Dining table & chairs'),
       price = 499.00, sort_order = 0,
       inclusions = E'Fabric shampooing of chair upholstery\nSurface cleaning and shining of the table\nStain removal from chair fabric',
       exclusions = E'Wood polish or refinishing\nRepair of loose joints or hardware',
       cover_image_url = '/images/catalogue/cleaning/services/dining-table-chairs-cleaning.jpg'
WHERE slug = 'dining-table-chairs-cleaning-e2e';

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning'),
       (SELECT id FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'sofa-carpet-cleaning') AND name = 'Sofa & carpet'),
       'Fabric sofa cleaning', 'fabric-sofa-cleaning',
       'Vacuuming and foam based shampooing for stain removal.', 'Sofa deep clean',
       E'Vacuuming to remove dust and debris\nFoam-based shampooing for stain removal\nDrying under fan',
       E'Recliner cleaning (booked separately)\nLeather or fabric repair',
       399.00, 45, TRUE, 0, '/images/catalogue/cleaning/services/fabric-sofa-cleaning.jpg'
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = 'fabric-sofa-cleaning');

-- 5. Full Home Cleaning - new subcategory (UC's real "Full Home/By Room
-- Cleaning" page). Deactivates the old ad-hoc flat "Deep Home Cleaning" and
-- "Chimney cleaning" items it replaces / that are now properly homed
-- elsewhere.
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Full Home Cleaning', 'full-home-cleaning',
       'Whole-home and by-room deep cleaning packages, at home.',
       '/images/catalogue/cleaning/full-home-cleaning.jpg', '/images/catalogue/cleaning/full-home-cleaning.jpg',
       TRUE, FALSE, 3, (SELECT id FROM category WHERE slug = 'cleaning')
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'full-home-cleaning');

INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'full-home-cleaning'), g.name, g.sort_order, TRUE
FROM (VALUES ('Full apartment',0),('Full bungalow/duplex',1),('Partial home cleaning',2)) AS g(name, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'full-home-cleaning') AND name = g.name);

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'full-home-cleaning'), sg.id,
       v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/cleaning/services/' || v.slug || '.jpg'
FROM (VALUES
    ('Unfurnished apartment - Home deep cleaning','unfurnished-apartment-deep-cleaning','Cleaning and stain removal from rooms, kitchen, bathroom and balcony.','Unfurnished apartment deep clean',E'Machine floor scrubbing across all rooms\nDusting of walls and ceilings\nCleaning and stain removal in kitchen, bathroom and balcony',E'Furniture and upholstery cleaning\nPest control treatment',3199.00,180,'Full apartment',0),
    ('Furnished apartment - Home deep cleaning','furnished-apartment-deep-cleaning','Cleaning and stain removal from rooms, kitchen, bathroom and balcony.','Furnished apartment deep clean',E'Machine floor scrubbing across all rooms\nDusting of walls, ceilings and furniture\nCleaning and stain removal in kitchen, bathroom and balcony',E'Upholstery shampooing (booked separately)\nPest control treatment',3499.00,225,'Full apartment',1),
    ('Unfurnished bungalow - Home deep cleaning','unfurnished-bungalow-deep-cleaning','Ideal for vacant, unoccupied homes.','Unfurnished bungalow deep clean',E'Machine floor scrubbing and stain removal across rooms\nCleaning of kitchen, bathrooms and balcony\nDusting of walls and ceilings',E'Furniture and upholstery cleaning\nPest control treatment',4199.00,300,'Full bungalow/duplex',0),
    ('Furnished bungalow - Home deep cleaning','furnished-bungalow-deep-cleaning','Ideal for furnished, occupied homes.','Furnished bungalow deep clean',E'Machine floor scrubbing and stain removal across rooms\nCleaning of kitchen, bathrooms and balcony\nDusting of walls, ceilings and furniture',E'Upholstery shampooing (booked separately)\nPest control treatment',4699.00,350,'Full bungalow/duplex',1),
    ('Partial home cleaning','partial-home-cleaning','Choose from bathroom, bedroom, kitchen, living room and balcony.','Custom room cleaning',E'Choice of bathroom, bedroom, kitchen, living room or balcony\nUpholstery and sofa cleaning add-on available\nAppliance cleaning add-on available',E'Rooms not selected in the package\nPest control treatment',2548.00,165,'Partial home cleaning',0)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, group_name, sort_order)
JOIN service_group sg ON sg.category_id = (SELECT id FROM category WHERE slug = 'full-home-cleaning') AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

UPDATE service SET is_active = FALSE WHERE slug IN ('deep-home-cleaning', 'chimney-cleaning-e2e');

-- 6. Serviceability: map every new/moved service into every pincode the
-- umbrella category already serves.
INSERT INTO service_pincode_mapping (id, service_id, pincode_id, is_active)
SELECT gen_random_uuid(), sv.id, p.id, TRUE
FROM service sv
JOIN category c ON c.id = sv.category_id
CROSS JOIN pincode p
WHERE (c.parent_category_id = (SELECT id FROM category WHERE slug = 'cleaning') OR c.id = (SELECT id FROM category WHERE slug = 'cleaning'))
  AND sv.is_active
  AND NOT EXISTS (
      SELECT 1 FROM service_pincode_mapping m
      WHERE m.service_id = sv.id AND m.pincode_id = p.id);

COMMIT;
