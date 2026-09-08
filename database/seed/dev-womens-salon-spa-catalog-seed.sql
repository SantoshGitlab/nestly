-- Women's Salon & Spa: full catalogue content mirroring UrbanCompany's real
-- Jaipur hierarchy and pricing (verified live against urbancompany.com on
-- 2026-09-08), with Glavyx's own images and copy. Second category in the
-- same replication pass as dev-ac-appliance-repair-catalog-seed.sql - see
-- that file's header for the full rationale; this one is shorter since it
-- only restates what differs.
--
-- STRUCTURAL CHANGE: this database had "Salon for Women" as a flat top-level
-- category (27 real, already-correctly-priced services - confirmed against
-- UC's live prices, nothing to fix there). UC's real top-level tile is
-- "Women's Salon & Spa", which opens to four flat subcategories: Salon for
-- Women, Spa for Women, Hair Studio for Women, Makeup/Saree & Styling. This
-- script creates that umbrella, reparents the existing "Salon for Women"
-- under it (content untouched), and builds the three siblings that did not
-- exist at all.
--
-- IDEMPOTENT - see the AC & Appliance Repair seed for the pattern this follows.
--
-- USAGE: psql "$DATABASE_URL" -f database/seed/dev-womens-salon-spa-catalog-seed.sql

\set ON_ERROR_STOP on

BEGIN;

-- 1. The umbrella category, taking over "Salon for Women"'s old top-level
-- slot (sort_order 0).
INSERT INTO category (id, name, slug, description, icon_url, banner_url, page_banner_url, is_active, is_featured, sort_order)
SELECT gen_random_uuid(), 'Women''s Salon & Spa', 'womens-salon-spa',
       'Salon, spa, hair and styling services for women, at home.',
       (SELECT icon_url FROM category WHERE id = '49f4e986-fd12-4ced-98c7-f8d1c0780139'),
       '/images/catalogue/womens-salon-spa/category-banner.jpg', '/images/catalogue/womens-salon-spa/category-banner.jpg',
       TRUE, FALSE, 0
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'womens-salon-spa');

-- 2. Reparent "Salon for Women" under it. Its own category_city_mapping rows
-- must go - a subcategory is reached only through its parent's children
-- list (CategoryRepository.ListChildrenAsync, no city check), so a mapping
-- row left on it would leak it back onto the flat /categories grid as a
-- second, orphaned-looking top-level tile (exactly the bug three unrelated
-- AC & Appliance Repair subcategories had before the previous pass).
UPDATE category SET parent_category_id = (SELECT id FROM category WHERE slug = 'womens-salon-spa'), sort_order = 0
WHERE id = '49f4e986-fd12-4ced-98c7-f8d1c0780139';

DELETE FROM category_city_mapping WHERE category_id = '49f4e986-fd12-4ced-98c7-f8d1c0780139';

INSERT INTO category_city_mapping (id, category_id, city_id, is_active)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'womens-salon-spa'), c.id, TRUE
FROM city c
WHERE NOT EXISTS (
    SELECT 1 FROM category_city_mapping m
    WHERE m.category_id = (SELECT id FROM category WHERE slug = 'womens-salon-spa') AND m.city_id = c.id);

-- 3. The three new sibling subcategories - flat under the umbrella, no
-- category_group (UC shows these four as a plain row, not grouped headers).
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), v.name, v.slug, v.description,
       '/images/catalogue/womens-salon-spa/' || v.image_file, '/images/catalogue/womens-salon-spa/' || v.image_file,
       TRUE, FALSE, v.sort_order, (SELECT id FROM category WHERE slug = 'womens-salon-spa')
FROM (VALUES
    ('Spa for Women', 'spa-for-women', 'Massage and spa therapies for women, at home.', 'spa-for-women.jpg', 1),
    ('Hair Studio for Women', 'hair-studio-for-women', 'Haircuts, styling and hair treatments for women.', 'hair-studio-for-women.jpg', 2),
    ('Makeup, Saree & Styling', 'makeup-saree-styling', 'Makeup, saree draping and styling for occasions.', 'makeup-saree-styling.jpg', 3)
) AS v(name, slug, description, image_file, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = v.slug);

-- 4. Spa for Women: five service groups, each with UC's real flagship prices.
INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'spa-for-women'), g.name, g.sort_order, TRUE
FROM (VALUES ('Stress relief',0),('Pain relief',1),('Skin care scrubs',2),('Post natal',3),('Elderly care',4)) AS g(name, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'spa-for-women') AND name = g.name);

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'spa-for-women'), sg.id,
       v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order
FROM (VALUES
    ('Stress relief Swedish massage','stress-relief-swedish-massage','Full-body massage that improves circulation and sleep quality.','Full-body Swedish massage','Full-body massage','Medical conditions requiring clearance',1319.00,60,'Stress relief',0),
    ('Quick comfort therapy','quick-comfort-therapy','Revitalising oil massage focused on key stress areas.','Focused stress-area massage','Targeted oil massage','Full-body coverage',999.00,45,'Stress relief',1),
    ('Swedish with foot massage','swedish-with-foot-massage','Swedish massage combined with a foot massage.','Swedish + foot massage','60 min Swedish + 20 min foot massage','Medical conditions requiring clearance',1769.00,80,'Stress relief',2),
    ('Swedish with head & shoulder massage','swedish-head-shoulder-massage','Swedish massage combined with a head and shoulder massage.','Swedish + head & shoulder','60 min Swedish + 20 min head & shoulder','Medical conditions requiring clearance',1769.00,80,'Stress relief',3),
    ('Top-to-toe stress relief massage','top-to-toe-stress-relief-massage','Customisable pressure massage with scalp care and foot reflexology.','Full-body + scalp + feet','Full-body massage, scalp care, reflexology','Medical conditions requiring clearance',1929.00,100,'Stress relief',4),
    ('Deep tissue massage','deep-tissue-massage','Supports post-workout relaxation with firm, focused pressure.','Deep-pressure massage','Deep tissue full-body massage','Medical conditions requiring clearance',1469.00,60,'Pain relief',0),
    ('Deep tissue with foot massage','deep-tissue-with-foot-massage','Deep tissue massage combined with a foot massage.','Deep tissue + foot massage','60 min deep tissue + 20 min foot massage','Medical conditions requiring clearance',1898.00,80,'Pain relief',1),
    ('Back relief massage','back-relief-massage','Focuses on lower back, spine and shoulder blades to ease tension.','Lower back focus massage','Lower back, spine, shoulder blade massage','Full-body coverage',929.00,40,'Pain relief',2),
    ('Leg relief massage','leg-relief-massage','Customised massage with natural oils to alleviate soreness in the legs.','Leg-focus massage','Glutes, calf and feet massage','Full-body coverage',929.00,40,'Pain relief',3),
    ('Full body massage & scrub','full-body-massage-scrub','Removes dead skin, leaving it soft, smooth and hydrated.','Massage + body scrub','Full-body massage and scrub','Facial treatment',1699.00,90,'Skin care scrubs',0),
    ('Post natal massage','post-natal-massage','Reduces water retention and eases muscle tension after childbirth.','Post natal recovery massage','Full-body massage for new mothers','Deep tissue pressure',1369.00,60,'Post natal',0),
    ('Elderly care massage','elderly-care-massage','Light-pressure full-body massage suited for elderly clients.','Gentle full-body massage','Light-pressure full-body massage','Deep tissue pressure',1399.00,60,'Elderly care',0)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, group_name, sort_order)
JOIN service_group sg ON sg.category_id = (SELECT id FROM category WHERE slug = 'spa-for-women') AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 5. Hair Studio for Women: two service groups (Blow-dry & style, Cut & trim).
INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'hair-studio-for-women'), g.name, g.sort_order, TRUE
FROM (VALUES ('Blow-dry & style',0),('Cut & trim',1)) AS g(name, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'hair-studio-for-women') AND name = g.name);

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'hair-studio-for-women'), sg.id,
       v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order
FROM (VALUES
    ('Straight & smooth blow-dry','straight-smooth-blow-dry','Sleek, smooth and straight hair with a professional blow-dry.','Straight blow-dry','Wash and blow-dry','Haircut',399.00,45,'Blow-dry & style',0),
    ('In curl/out curl blow-dry','in-curl-out-curl-blow-dry','Beautiful curls, styled in or out, with a perfect blow-dry finish.','Curled blow-dry','Wash and blow-dry curls','Haircut',449.00,45,'Blow-dry & style',1),
    ('Hair straightening','hair-straightening','Transforms hair into a sleek, straight look with long-lasting results.','Long-lasting straightening','Straightening treatment','Haircut',549.00,45,'Blow-dry & style',2),
    ('Curls & waves','curls-waves','Soft curls or waves for a natural, voluminous hairstyle.','Curls or waves styling','Curling or waving treatment','Haircut',549.00,60,'Blow-dry & style',3),
    ('Haircut for women','haircut-for-women-hs','Expert haircut tailored to your style. Blow-dry not included.','Expert haircut','Haircut','Blow-dry',549.00,45,'Cut & trim',0),
    ('Haircut for girls','haircut-for-girls','A gentle, stylish haircut with care and precision. For girls aged 6-15.','Haircut for girls 6-15','Haircut','Blow-dry',649.00,45,'Cut & trim',1),
    ('Hair trim','hair-trim-women','Split-end trim to keep hair healthy without changing the style.','Split-end trim','Trim','Restyling',449.00,20,'Cut & trim',2)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, group_name, sort_order)
JOIN service_group sg ON sg.category_id = (SELECT id FROM category WHERE slug = 'hair-studio-for-women') AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 6. Makeup, Saree & Styling: two service groups (Saree draping, Party makeup).
INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'makeup-saree-styling'), g.name, g.sort_order, TRUE
FROM (VALUES ('Saree draping',0),('Party makeup',1)) AS g(name, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'makeup-saree-styling') AND name = g.name);

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'makeup-saree-styling'), sg.id,
       v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order
FROM (VALUES
    ('Basic saree draping','basic-saree-draping','Choose any saree/sari or lehenga draping style.','Basic draping','Saree or lehenga draping','Pins and accessories',499.00,20,'Saree draping',0),
    ('Advanced saree draping','advanced-saree-draping','Choose from double-pallu, heavy dupatta, or a lehenga style draping.','Advanced draping styles','Advanced draping style','Pins and accessories',699.00,30,'Saree draping',1),
    ('Basic makeup','basic-makeup-women','Natural, everyday glow with a lightweight formula for daytime events.','Everyday makeup','Base, eyes and lips makeup','Hairstyling',1599.00,45,'Party makeup',0)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, group_name, sort_order)
JOIN service_group sg ON sg.category_id = (SELECT id FROM category WHERE slug = 'makeup-saree-styling') AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 7. Serviceability: map every service in the three new subcategories into
-- every existing pincode, same "blanket coverage" policy as the AC pass.
-- "Salon for Women" is excluded (its services were already bookable before
-- this script ran, so it already has its own mapping rows).
INSERT INTO service_pincode_mapping (id, service_id, pincode_id, is_active)
SELECT gen_random_uuid(), sv.id, p.id, TRUE
FROM service sv
JOIN category c ON c.id = sv.category_id
CROSS JOIN pincode p
WHERE c.parent_category_id = (SELECT id FROM category WHERE slug = 'womens-salon-spa')
  AND c.slug != 'salon-women'
  AND sv.is_active
  AND NOT EXISTS (
      SELECT 1 FROM service_pincode_mapping m
      WHERE m.service_id = sv.id AND m.pincode_id = p.id);

COMMIT;
