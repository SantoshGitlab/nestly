-- Men's Salon & Massage: full catalogue content mirroring UrbanCompany's real
-- Jaipur hierarchy and pricing (verified live against urbancompany.com on
-- 2026-09-08). Third category in the same replication pass as
-- dev-ac-appliance-repair-catalog-seed.sql / dev-womens-salon-spa-catalog-seed.sql -
-- see those files' headers for the full rationale.
--
-- STRUCTURAL CHANGE: same shape as the Women's Salon & Spa pass - "Salon for
-- Men" was a flat top-level category with 6 existing services (left
-- untouched here; they don't map cleanly 1:1 onto UC's named line items, so
-- they stay as additional Glavyx content rather than being force-fit or
-- deleted, same policy as AC & Appliance Repair's extra foam-jet tiers).
-- UC's real top-level tile is "Men's Salon & Massage", with two flat
-- subcategories: Salon for Men, Massage for Men. This creates that umbrella,
-- reparents "Salon for Men" under it, adds UC's real Haircut & beard
-- styling / Facial & cleanup line items as new grouped services alongside
-- the existing ungrouped ones, and builds "Massage for Men" from scratch.
--
-- IDEMPOTENT - see the AC & Appliance Repair seed for the pattern.
--
-- USAGE: psql "$DATABASE_URL" -f database/seed/dev-mens-salon-massage-catalog-seed.sql

\set ON_ERROR_STOP on

BEGIN;

-- 1. The umbrella category, taking over "Salon for Men"'s old top-level slot.
INSERT INTO category (id, name, slug, description, icon_url, banner_url, page_banner_url, is_active, is_featured, sort_order)
SELECT gen_random_uuid(), 'Men''s Salon & Massage', 'mens-salon-massage',
       'Grooming and massage services for men, at home.',
       (SELECT icon_url FROM category WHERE id = '93e0b3df-a3bf-42ec-af9a-bd5331c58524'),
       '/images/catalogue/mens-salon-massage/category-banner.jpg', '/images/catalogue/mens-salon-massage/category-banner.jpg',
       TRUE, FALSE, 1
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'mens-salon-massage');

-- 2. Reparent "Salon for Men" under it - own city mapping removed for the
-- same "leaks onto the flat grid" reason as Salon for Women.
UPDATE category SET parent_category_id = (SELECT id FROM category WHERE slug = 'mens-salon-massage'), sort_order = 0,
       banner_url = '/images/catalogue/mens-salon-massage/salon-for-men.jpg', page_banner_url = '/images/catalogue/mens-salon-massage/salon-for-men.jpg'
WHERE id = '93e0b3df-a3bf-42ec-af9a-bd5331c58524';

DELETE FROM category_city_mapping WHERE category_id = '93e0b3df-a3bf-42ec-af9a-bd5331c58524';

INSERT INTO category_city_mapping (id, category_id, city_id, is_active)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'mens-salon-massage'), c.id, TRUE
FROM city c
WHERE NOT EXISTS (
    SELECT 1 FROM category_city_mapping m
    WHERE m.category_id = (SELECT id FROM category WHERE slug = 'mens-salon-massage') AND m.city_id = c.id);

-- 3. "Massage for Men" - the new sibling subcategory.
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Massage for Men', 'massage-for-men', 'Massage therapies for men, at home.',
       '/images/catalogue/mens-salon-massage/massage-for-men.jpg', '/images/catalogue/mens-salon-massage/massage-for-men.jpg',
       TRUE, FALSE, 1, (SELECT id FROM category WHERE slug = 'mens-salon-massage')
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'massage-for-men');

-- 4. Salon for Men: two new service groups with UC's real line items,
-- alongside the six pre-existing ungrouped services (untouched).
INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), '93e0b3df-a3bf-42ec-af9a-bd5331c58524', g.name, g.sort_order, TRUE
FROM (VALUES ('Haircut & beard styling',0),('Facial & cleanup',1)) AS g(name, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = '93e0b3df-a3bf-42ec-af9a-bd5331c58524' AND name = g.name);

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), '93e0b3df-a3bf-42ec-af9a-bd5331c58524', sg.id,
       v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order
FROM (VALUES
    ('Haircut for men','haircut-for-men','Professional haircut that suits your face shape.','Expert haircut','Haircut','Beard grooming',259.00,30,'Haircut & beard styling',0),
    ('Haircut for boys','haircut-for-boys','Specially trained stylists for boys aged 2 years and above.','Haircut for boys','Haircut','Beard grooming',259.00,30,'Haircut & beard styling',1),
    ('Clean shave','clean-shave','Shave with a single-use blade for the closest shave.','Close shave','Single-use blade shave','Beard styling',199.00,20,'Haircut & beard styling',2),
    ('Beard trimming & styling','beard-trimming-styling','Customized beard shaping from trained stylists.','Beard shaping','Beard trim and styling','Coloring',199.00,25,'Haircut & beard styling',3),
    ('Beard color (with product)','beard-color-with-product','Even and mess-free beard colour application.','Beard coloring','Beard color application','Trimming',199.00,30,'Haircut & beard styling',4),
    ('Skin brightening facial','skin-brightening-facial','Orange peel, vitamin C and green tea enriched facial to reduce dullness.','Brightening facial','Full facial treatment','Cleanup only',1399.00,65,'Facial & cleanup',0),
    ('Skin hydrating facial','skin-hydrating-facial','Mulberry, saffron and arbutin enriched facial for deep cleansing and hydration.','Hydrating facial','Full facial treatment','Cleanup only',1399.00,65,'Facial & cleanup',1),
    ('Office-ready cleanup','office-ready-cleanup','Vitamin-E, charcoal and lemon enriched cleanup to cleanse and soften skin.','Quick cleanup','Cleanup treatment','Facial',699.00,35,'Facial & cleanup',2),
    ('Oil-free vacation cleanup','oil-free-vacation-cleanup','Vitamin-C, green tea and grapefruit enriched cleanup to control sebum.','Oil-control cleanup','Cleanup treatment','Facial',699.00,35,'Facial & cleanup',3),
    ('Charcoal de-toxifying cleanup','charcoal-detoxifying-cleanup','Charcoal-extract cleanup for deep cleansing and dead skin removal.','Charcoal cleanup','Cleanup treatment','Facial',599.00,35,'Facial & cleanup',4)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, group_name, sort_order)
JOIN service_group sg ON sg.category_id = '93e0b3df-a3bf-42ec-af9a-bd5331c58524' AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 5. Massage for Men: three service groups (Pain relief, Stress relief, Post workout).
INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'massage-for-men'), g.name, g.sort_order, TRUE
FROM (VALUES ('Pain relief',0),('Stress relief',1),('Post workout',2)) AS g(name, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service_group WHERE category_id = (SELECT id FROM category WHERE slug = 'massage-for-men') AND name = g.name);

INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'massage-for-men'), sg.id,
       v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order
FROM (VALUES
    ('Quick comfort therapy (men)','quick-comfort-therapy-men','Focuses on tensed shoulders, strained back and tired legs.','Targeted comfort massage','Targeted oil massage','Full-body coverage',999.00,45,'Pain relief',0),
    ('Deep tissue pain relief massage','deep-tissue-pain-relief-massage','Firm palm movements to ease muscle tightness and soreness.','Deep-pressure massage','Deep tissue full-body massage','Medical conditions requiring clearance',1429.00,60,'Pain relief',1),
    ('Deep tissue with head, neck & shoulder','deep-tissue-head-neck-shoulder','Deep tissue massage combined with a head, neck and shoulder massage.','Deep tissue + head/neck/shoulder','60 min deep tissue + 40 min head/neck/shoulder','Medical conditions requiring clearance',1999.00,100,'Pain relief',2),
    ('Back relief massage (men)','back-relief-massage-men','Focused massage to relieve knots and soreness in back and shoulders.','Back-focus massage','Back and shoulder massage','Full-body coverage',919.00,40,'Pain relief',3),
    ('Leg relief massage (men)','leg-relief-massage-men','Customised massage with natural oils to alleviate leg pain.','Leg-focus massage','Leg massage','Full-body coverage',919.00,40,'Pain relief',4),
    ('Swedish stress relief massage','swedish-stress-relief-massage-men','Full-body massage that improves circulation and sleep quality.','Full-body Swedish massage','Full-body massage','Medical conditions requiring clearance',1299.00,60,'Stress relief',0),
    ('Holistic de-stress massage','holistic-destress-massage','Medium-pressure massage with focus on head, neck and shoulder.','Head/neck/shoulder focus','Medium-pressure massage','Full-body deep pressure',1559.00,80,'Stress relief',1),
    ('Top-to-toe stress relief massage (men)','top-to-toe-stress-relief-massage-men','Full-body massage with scalp care and foot reflexology.','Full-body + scalp + feet','Full-body massage, scalp care, reflexology','Medical conditions requiring clearance',1979.00,100,'Stress relief',2),
    ('Sports recovery massage','sports-recovery-massage','High-pressure full-body massage to ease muscle tightness after workouts.','Post-workout recovery massage','High-pressure full-body massage','Medical conditions requiring clearance',1369.00,60,'Post workout',0)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, group_name, sort_order)
JOIN service_group sg ON sg.category_id = (SELECT id FROM category WHERE slug = 'massage-for-men') AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 6. Serviceability: map the new services (Massage for Men + the two new
-- Salon for Men groups) into every existing pincode. Salon for Men's six
-- pre-existing services already had their own mappings before this script.
INSERT INTO service_pincode_mapping (id, service_id, pincode_id, is_active)
SELECT gen_random_uuid(), sv.id, p.id, TRUE
FROM service sv
JOIN category c ON c.id = sv.category_id
CROSS JOIN pincode p
WHERE (c.parent_category_id = (SELECT id FROM category WHERE slug = 'mens-salon-massage') OR c.id = '93e0b3df-a3bf-42ec-af9a-bd5331c58524')
  AND sv.is_active
  AND NOT EXISTS (
      SELECT 1 FROM service_pincode_mapping m
      WHERE m.service_id = sv.id AND m.pincode_id = p.id);

-- 7. Service-level cover images for the 19 new line items (UC shows a
-- distinct image per service, not just per subcategory - see the
-- Service.CoverImageUrl doc comment). Pre-existing services in both
-- subcategories already carry their own cover_image_url and are untouched.
UPDATE service SET cover_image_url = '/images/catalogue/mens-salon-massage/services/' || v.slug || '.jpg'
FROM (VALUES
    ('haircut-for-men'),('haircut-for-boys'),('clean-shave'),('beard-trimming-styling'),
    ('beard-color-with-product'),('skin-brightening-facial'),('skin-hydrating-facial'),
    ('office-ready-cleanup'),('oil-free-vacation-cleanup'),('charcoal-detoxifying-cleanup'),
    ('quick-comfort-therapy-men'),('deep-tissue-pain-relief-massage'),('deep-tissue-head-neck-shoulder'),
    ('back-relief-massage-men'),('leg-relief-massage-men'),('swedish-stress-relief-massage-men'),
    ('holistic-destress-massage'),('top-to-toe-stress-relief-massage-men'),('sports-recovery-massage')
) AS v(slug)
WHERE service.slug = v.slug;

COMMIT;
