-- Everyday-recurring services: Daily Help (maid), Car Wash, Laundry & Ironing,
-- plus one extra Pest Control service. Companion to the other
-- dev-*-catalog-seed.sql files; same conventions (top-level categories,
-- service groups, Fixed-price services, no variants - none of the existing
-- seeds use service_variant, so sizes are separate services).
--
-- WHY THESE: services a household needs weekly or monthly are what turns a
-- one-time booking into repeat orders (see docs/MARKET.md). Each new service
-- carries an FAQ pointing customers at the existing recurring-plan flow
-- (/recurring-bookings/new). The recurring plan currently supports weekly /
-- fortnightly / monthly only; a daily / chosen-days frequency is a separate,
-- later change - until then "daily" help is sold as individually bookable
-- visits or a weekly plan.
--
-- LOCAL / DEV ONLY. Prices are placeholder-realistic, not commercial data.
-- Categories have no banner images yet (columns are nullable).
--
-- Also makes the new catalog reachable, which fails closed otherwise
-- (see bootstrap-bookability.sql): category->city mapping, service->pincode
-- mapping, and provider skills for every existing provider.
--
-- IDEMPOTENT: every insert is guarded, safe to re-run.
--
-- USAGE: psql "$DATABASE_URL" -f database/seed/dev-daily-services-catalog-seed.sql

\set ON_ERROR_STOP on

BEGIN;

-- 1. Categories (top-level) --------------------------------------------------
INSERT INTO category (id, name, slug, description, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), v.name, v.slug, v.description, TRUE, v.is_featured, v.sort_order, NULL
FROM (VALUES
    ('Daily Help',        'daily-help',        'Sweeping, mopping, utensil washing and kitchen tidy-up - book once or repeat on a schedule.', TRUE,  20),
    ('Car Wash',          'car-wash',          'Doorstep car and bike washing, exterior or interior + exterior.',                            FALSE, 21),
    ('Laundry & Ironing', 'laundry-ironing',   'Pickup and drop wash, fold and iron for the whole household.',                                FALSE, 22)
) AS v(name, slug, description, is_featured, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = v.slug);

-- 2. Service groups ----------------------------------------------------------
INSERT INTO service_group (id, category_id, name, sort_order, is_active)
SELECT gen_random_uuid(), c.id, g.name, g.sort_order, TRUE
FROM (VALUES
    ('daily-help',      'Daily essentials',      0),
    ('daily-help',      'Combined help',         1),
    ('car-wash',        'Exterior wash',         0),
    ('car-wash',        'Interior + exterior',   1),
    ('car-wash',        'Bikes',                 2),
    ('laundry-ironing', 'Wash',                  0),
    ('laundry-ironing', 'Ironing',               1)
) AS g(category_slug, name, sort_order)
JOIN category c ON c.slug = g.category_slug
WHERE NOT EXISTS (SELECT 1 FROM service_group sg WHERE sg.category_id = c.id AND sg.name = g.name);

-- 3. Services ----------------------------------------------------------------
INSERT INTO service (id, category_id, service_group_id, name, slug, description, short_description,
                     inclusions, exclusions, price, duration_minutes, is_active, is_quantity_allowed, sort_order)
SELECT gen_random_uuid(), c.id, sg.id, v.name, v.slug, v.description, v.short_description,
       v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.is_quantity_allowed, v.sort_order
FROM (VALUES
    -- Daily Help
    ('daily-help','Daily essentials','Sweeping & mopping','sweeping-mopping','Sweeping and wet mopping of the living areas.','Floor sweeping and mopping',
        E'Sweeping of all rooms\nWet mopping with floor cleaner\nCorners and under-furniture reach',E'Deep stain removal\nBalcony and terrace washing',149.00,30,FALSE,0),
    ('daily-help','Daily essentials','Utensil washing','utensil-washing','Washing of daily utensils with dish soap.','Daily dishwashing',
        E'Washing of used utensils\nSink wipe-down\nDrying rack arrangement',E'Cleaning of appliances\nStove and chimney cleaning',129.00,30,FALSE,1),
    ('daily-help','Daily essentials','Kitchen & counter tidy-up','kitchen-counter-tidy-up','Wipe-down of the counter, stove top and sink.','Kitchen tidy-up',
        E'Counter and stove-top wipe-down\nSink cleaning\nGarbage bag replacement',E'Chimney and cabinet deep cleaning\nFridge cleaning',179.00,45,FALSE,2),
    ('daily-help','Combined help','Daily home help - 1 hour','daily-home-help-1-hour','Sweeping, mopping, dishes and dusting in one visit.','Complete daily help',
        E'Sweeping and mopping\nUtensil washing\nDusting of surfaces',E'Deep cleaning of bathrooms\nWindow and balcony cleaning',249.00,60,FALSE,0),
    -- Car Wash
    ('car-wash','Exterior wash','Hatchback exterior wash','hatchback-exterior-wash','Waterless-friendly exterior wash and wipe.','Hatchback exterior wash',
        E'Exterior body wash\nWindow and mirror cleaning\nTyre and rim wipe',E'Interior vacuuming\nPolish and waxing',199.00,30,FALSE,0),
    ('car-wash','Exterior wash','Sedan exterior wash','sedan-exterior-wash','Waterless-friendly exterior wash and wipe.','Sedan exterior wash',
        E'Exterior body wash\nWindow and mirror cleaning\nTyre and rim wipe',E'Interior vacuuming\nPolish and waxing',249.00,40,FALSE,1),
    ('car-wash','Exterior wash','SUV exterior wash','suv-exterior-wash','Waterless-friendly exterior wash and wipe.','SUV exterior wash',
        E'Exterior body wash\nWindow and mirror cleaning\nTyre and rim wipe',E'Interior vacuuming\nPolish and waxing',299.00,50,FALSE,2),
    ('car-wash','Interior + exterior','Hatchback interior + exterior','hatchback-interior-exterior-wash','Exterior wash with interior vacuuming and dashboard wipe.','Hatchback full wash',
        E'Exterior wash\nSeat and carpet vacuuming\nDashboard and door-panel wipe',E'Upholstery shampooing\nPolish and waxing',449.00,60,FALSE,0),
    ('car-wash','Interior + exterior','Sedan interior + exterior','sedan-interior-exterior-wash','Exterior wash with interior vacuuming and dashboard wipe.','Sedan full wash',
        E'Exterior wash\nSeat and carpet vacuuming\nDashboard and door-panel wipe',E'Upholstery shampooing\nPolish and waxing',549.00,75,FALSE,1),
    ('car-wash','Interior + exterior','SUV interior + exterior','suv-interior-exterior-wash','Exterior wash with interior vacuuming and dashboard wipe.','SUV full wash',
        E'Exterior wash\nSeat and carpet vacuuming\nDashboard and door-panel wipe',E'Upholstery shampooing\nPolish and waxing',649.00,90,FALSE,2),
    ('car-wash','Bikes','Bike wash','bike-wash','Doorstep wash and wipe for two-wheelers.','Two-wheeler wash',
        E'Body wash and wipe\nChain and wheel cleaning\nSeat wipe',E'Polish and waxing\nEngine degreasing',99.00,20,FALSE,0),
    -- Laundry & Ironing (pickup/drop; duration is the pickup visit)
    ('laundry-ironing','Wash','Wash & fold (up to 5 kg)','wash-fold-5kg','Pickup, machine wash, dry and fold, returned to your door.','Wash and fold',
        E'Pickup and drop\nMachine wash and dry\nFolded and packed',E'Dry cleaning\nStain treatment beyond regular wash',249.00,30,FALSE,0),
    ('laundry-ironing','Wash','Wash & iron (up to 5 kg)','wash-iron-5kg','Pickup, machine wash, dry and press, returned to your door.','Wash and iron',
        E'Pickup and drop\nMachine wash and dry\nSteam ironing and packing',E'Dry cleaning\nStain treatment beyond regular wash',349.00,30,FALSE,1),
    ('laundry-ironing','Ironing','Ironing (10 pieces)','ironing-10-pieces','Steam ironing of your own clean clothes, per 10 pieces.','Iron only',
        E'Pickup and drop\nSteam ironing\nFolded or on hangers',E'Washing\nDry cleaning',99.00,30,TRUE,0),
    -- Pest Control: one more service in the existing category
    ('pest-control',NULL,'Bed bug control','bed-bug-control','Targeted two-stage treatment for bed bugs in bedrooms and furniture.','Bed bug treatment',
        E'Inspection of beds, sofas and crevices\nTwo-stage spray treatment\nSafety guidance for re-entry',E'Furniture replacement\nWhole-home general pest treatment',1199.00,90,FALSE,3)
) AS v(category_slug, group_name, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_quantity_allowed, sort_order)
JOIN category c ON c.slug = v.category_slug
LEFT JOIN service_group sg ON sg.category_id = c.id AND sg.name = v.group_name
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 4. FAQ pointing at the recurring-plan flow -------------------------------
INSERT INTO service_faq (id, service_id, question, answer)
SELECT gen_random_uuid(), s.id, 'Can I make this repeat automatically?',
       'Yes. Use "Set up a repeat plan" on this page to book it weekly, fortnightly or monthly at the same time and address.'
FROM service s
WHERE s.slug IN ('sweeping-mopping','utensil-washing','kitchen-counter-tidy-up','daily-home-help-1-hour',
                 'hatchback-exterior-wash','sedan-exterior-wash','suv-exterior-wash',
                 'hatchback-interior-exterior-wash','sedan-interior-exterior-wash','suv-interior-exterior-wash','bike-wash',
                 'wash-fold-5kg','wash-iron-5kg','ironing-10-pieces','bed-bug-control')
  AND NOT EXISTS (SELECT 1 FROM service_faq f WHERE f.service_id = s.id AND f.question = 'Can I make this repeat automatically?');

-- 5. Reachability: category -> city, service -> pincode, provider skills ------
INSERT INTO category_city_mapping (id, category_id, city_id, is_active)
SELECT gen_random_uuid(), c.id, ci.id, TRUE
FROM category c
CROSS JOIN city ci
WHERE c.slug IN ('daily-help','car-wash','laundry-ironing') AND ci.is_active
  AND NOT EXISTS (SELECT 1 FROM category_city_mapping m WHERE m.category_id = c.id AND m.city_id = ci.id);

INSERT INTO service_pincode_mapping (id, service_id, pincode_id, is_active)
SELECT gen_random_uuid(), sv.id, p.id, TRUE
FROM service sv
JOIN category c ON c.id = sv.category_id
CROSS JOIN pincode p
WHERE (c.slug IN ('daily-help','car-wash','laundry-ironing') OR sv.slug = 'bed-bug-control')
  AND sv.is_active
  AND NOT EXISTS (SELECT 1 FROM service_pincode_mapping m WHERE m.service_id = sv.id AND m.pincode_id = p.id);

INSERT INTO provider_skill_mapping (id, provider_id, category_id, service_id, is_active)
SELECT gen_random_uuid(), pr.id, c.id, NULL, TRUE
FROM provider pr
CROSS JOIN category c
WHERE c.slug IN ('daily-help','car-wash','laundry-ironing')
  AND NOT EXISTS (SELECT 1 FROM provider_skill_mapping m WHERE m.provider_id = pr.id AND m.category_id = c.id);

COMMIT;
