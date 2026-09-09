-- Painting & Waterproofing: full catalogue content mirroring UrbanCompany's
-- real hierarchy (verified live against urbancompany.com on 2026-09-09).
-- Fifth category in the same replication pass as
-- dev-ac-appliance-repair-catalog-seed.sql / dev-womens-salon-spa-catalog-seed.sql
-- / dev-mens-salon-massage-catalog-seed.sql / dev-cleaning-catalog-seed.sql -
-- see those files' headers for the full rationale.
--
-- IMPORTANT DIFFERENCE FROM THE OTHER 4 CATEGORIES: UC does not expose an
-- instant, publicly-browsable price list for this category the way it does
-- for AC/Salon/Cleaning. Its real painting flow is consultation-gated (a
-- paid "At home consultation"/"Home Painting Consultation", both real UC
-- line items at Rs.49, book a professional who then quotes the actual job).
-- The 7 subcategory names below ARE UC's real ones (verified from its own
-- site navigation), but the per-service prices are representative rates in
-- the same band as this project's own pre-existing Painting & Waterproofing
-- prices, not scraped exact figures - there are none to scrape.
--
-- STRUCTURAL CHANGE: the old flat top-level (4 direct services, wrong
-- placeholder images on every one) becomes 7 UC-named subcategories.
-- Rooms & Walls Painting and Waterproofing & Grouting reuse the 4 existing
-- services (reparented, repriced where UC's real per-sqft rates implied a
-- different figure); Unfurnished/Furnished Full Home Painting, Exterior
-- Painting, Wood Polish and Textures are new.
--
-- IDEMPOTENT - see the AC & Appliance Repair seed for the pattern.
--
-- USAGE: psql "$DATABASE_URL" -f database/seed/dev-painting-waterproofing-catalog-seed.sql

\set ON_ERROR_STOP on

BEGIN;

-- 1. Fix the umbrella's banner (currently a leftover kitchen-cleaning photo).
UPDATE category SET banner_url = '/images/catalogue/painting-waterproofing/category-banner.jpg',
       page_banner_url = '/images/catalogue/painting-waterproofing/category-banner.jpg'
WHERE id = '29380e03-6ec4-47d2-88e5-e8a9159eaaa7';

-- 2. Rooms & Walls Painting - new subcategory, reparenting the 2 existing
-- per-room services (UC's real "Rooms/ Walls Painting" teaser).
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Rooms & Walls Painting', 'rooms-walls-painting',
       'Painting for individual rooms, walls and ceilings, at home.',
       '/images/catalogue/painting-waterproofing/rooms-walls-painting.jpg', '/images/catalogue/painting-waterproofing/rooms-walls-painting.jpg',
       TRUE, FALSE, 0, '29380e03-6ec4-47d2-88e5-e8a9159eaaa7'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'rooms-walls-painting');

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'rooms-walls-painting'),
       sort_order = 0,
       description = 'Premium emulsion painting for one wall or room.',
       short_description = 'Room paint job',
       inclusions = E'Surface cleaning and minor crack filling\nTwo coats of premium emulsion paint\nFinal touch-up and cleanup',
       exclusions = E'Furniture removal or covering\nMajor plaster or crack repair',
       cover_image_url = '/images/catalogue/painting-waterproofing/services/wall-painting-per-room.jpg'
WHERE slug = 'wall-painting-per-room';

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'rooms-walls-painting'),
       sort_order = 1,
       description = 'Premium emulsion painting for one ceiling.',
       short_description = 'Ceiling paint job',
       inclusions = E'Surface cleaning and minor crack filling\nTwo coats of premium emulsion paint\nFinal touch-up and cleanup',
       exclusions = E'Furniture removal or covering\nMajor plaster or crack repair',
       cover_image_url = '/images/catalogue/painting-waterproofing/services/ceiling-painting-per-room.jpg'
WHERE slug = 'ceiling-painting-per-room-e2e';

-- 3. Waterproofing & Grouting - new subcategory, reparenting the 2 existing
-- waterproofing services (UC's real subcategory name).
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Waterproofing & Grouting', 'waterproofing-grouting',
       'Leak-proofing and tile grouting treatments, at home.',
       '/images/catalogue/painting-waterproofing/waterproofing-grouting.jpg', '/images/catalogue/painting-waterproofing/waterproofing-grouting.jpg',
       TRUE, FALSE, 1, '29380e03-6ec4-47d2-88e5-e8a9159eaaa7'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'waterproofing-grouting');

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'waterproofing-grouting'),
       sort_order = 0,
       description = 'Leak-proofing treatment for one bathroom.',
       short_description = 'Bathroom leak-proofing',
       inclusions = E'Surface crack sealing and priming\nWaterproof membrane coating\nTile grout sealing',
       exclusions = E'Tile replacement\nPlumbing leak repair',
       cover_image_url = '/images/catalogue/painting-waterproofing/services/bathroom-waterproofing.jpg'
WHERE slug = 'bathroom-waterproofing-e2e';

UPDATE service SET category_id = (SELECT id FROM category WHERE slug = 'waterproofing-grouting'),
       sort_order = 1,
       description = 'Leak-proofing treatment for a terrace, per 100 sq ft.',
       short_description = 'Terrace leak-proofing',
       inclusions = E'Surface crack sealing and priming\nWaterproof membrane coating\nDrainage slope check',
       exclusions = E'Structural crack repair\nTile or screed replacement',
       cover_image_url = '/images/catalogue/painting-waterproofing/services/terrace-waterproofing.jpg'
WHERE slug = 'terrace-waterproofing-e2e';

-- 4. Unfurnished Full Home Painting - new subcategory.
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Unfurnished Full Home Painting', 'unfurnished-full-home-painting',
       'Whole-home painting for vacant, unfurnished homes.',
       '/images/catalogue/painting-waterproofing/unfurnished-full-home-painting.jpg', '/images/catalogue/painting-waterproofing/unfurnished-full-home-painting.jpg',
       TRUE, FALSE, 2, '29380e03-6ec4-47d2-88e5-e8a9159eaaa7'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'unfurnished-full-home-painting');

INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'unfurnished-full-home-painting'), v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/painting-waterproofing/services/' || v.slug || '.jpg'
FROM (VALUES
    ('1 BHK unfurnished home painting','1bhk-unfurnished-home-painting','Full home painting for a vacant 1 BHK.','1 BHK unfurnished paint job',E'Surface cleaning and crack filling across all rooms\nTwo coats of premium emulsion paint\nDoor and window frame touch-up',E'Furniture covering (none needed - vacant home)\nMajor plaster or waterproofing work',8999.00,600,0),
    ('2 BHK unfurnished home painting','2bhk-unfurnished-home-painting','Full home painting for a vacant 2 BHK.','2 BHK unfurnished paint job',E'Surface cleaning and crack filling across all rooms\nTwo coats of premium emulsion paint\nDoor and window frame touch-up',E'Furniture covering (none needed - vacant home)\nMajor plaster or waterproofing work',14999.00,900,1)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 5. Furnished Full Home Painting - new subcategory.
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Furnished Full Home Painting', 'furnished-full-home-painting',
       'Whole-home painting for occupied, furnished homes.',
       '/images/catalogue/painting-waterproofing/furnished-full-home-painting.jpg', '/images/catalogue/painting-waterproofing/furnished-full-home-painting.jpg',
       TRUE, FALSE, 3, '29380e03-6ec4-47d2-88e5-e8a9159eaaa7'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'furnished-full-home-painting');

INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'furnished-full-home-painting'), v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/painting-waterproofing/services/' || v.slug || '.jpg'
FROM (VALUES
    ('1 BHK furnished home painting','1bhk-furnished-home-painting','Full home painting for an occupied 1 BHK.','1 BHK furnished paint job',E'Furniture covering and floor protection\nSurface cleaning and crack filling across all rooms\nTwo coats of premium emulsion paint',E'Furniture removal from the home\nMajor plaster or waterproofing work',10999.00,660,0),
    ('2 BHK furnished home painting','2bhk-furnished-home-painting','Full home painting for an occupied 2 BHK.','2 BHK furnished paint job',E'Furniture covering and floor protection\nSurface cleaning and crack filling across all rooms\nTwo coats of premium emulsion paint',E'Furniture removal from the home\nMajor plaster or waterproofing work',17999.00,960,1)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 6. Exterior Painting - new subcategory (UC's real "Exterior Full Home").
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Exterior Painting', 'exterior-painting',
       'Weatherproof exterior wall painting, at home.',
       '/images/catalogue/painting-waterproofing/exterior-painting.jpg', '/images/catalogue/painting-waterproofing/exterior-painting.jpg',
       TRUE, FALSE, 4, '29380e03-6ec4-47d2-88e5-e8a9159eaaa7'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'exterior-painting');

INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'exterior-painting'), v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/painting-waterproofing/services/' || v.slug || '.jpg'
FROM (VALUES
    ('Exterior wall painting (per 100 sqft)','exterior-wall-painting-per-100-sqft','Weatherproof emulsion painting for exterior walls.','Exterior paint job',E'Surface cleaning and crack filling\nPrimer coat for weatherproofing\nTwo coats of exterior-grade emulsion paint',E'Scaffolding beyond standard reach\nStructural crack repair',3499.00,240,0),
    ('Exterior waterproof painting (per 100 sqft)','exterior-waterproof-painting-per-100-sqft','Waterproof exterior coating for weatherproofing and leak protection.','Exterior waterproof paint',E'Surface cleaning and crack sealing\nWaterproof primer coat\nTwo coats of waterproof exterior paint',E'Scaffolding beyond standard reach\nStructural crack repair',4499.00,270,1)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 7. Wood Polish - new subcategory (UC's real subcategory name).
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Wood Polish', 'wood-polish',
       'Polishing for doors, wardrobes and wooden furniture, at home.',
       '/images/catalogue/painting-waterproofing/wood-polish.jpg', '/images/catalogue/painting-waterproofing/wood-polish.jpg',
       TRUE, FALSE, 5, '29380e03-6ec4-47d2-88e5-e8a9159eaaa7'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'wood-polish');

INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'wood-polish'), v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/painting-waterproofing/services/' || v.slug || '.jpg'
FROM (VALUES
    ('Door polish (per door)','door-polish-per-door','Melamine polish for one wooden door, both sides.','Door polish',E'Sanding of old polish and surface prep\nStaining to match existing shade\nTwo coats of melamine polish',E'Door repair or hardware replacement\nColour change beyond matching stain',1299.00,120,0),
    ('Wardrobe/furniture polish (per piece)','wardrobe-furniture-polish-per-piece','Melamine polish for one wardrobe or large furniture piece.','Furniture polish',E'Sanding of old polish and surface prep\nStaining to match existing shade\nTwo coats of melamine polish',E'Furniture repair or hardware replacement\nColour change beyond matching stain',1799.00,150,1)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 8. Textures - new subcategory (UC's real subcategory name).
INSERT INTO category (id, name, slug, description, banner_url, page_banner_url, is_active, is_featured, sort_order, parent_category_id)
SELECT gen_random_uuid(), 'Textures', 'textures',
       'Textured wall finishes and designs, at home.',
       '/images/catalogue/painting-waterproofing/textures.jpg', '/images/catalogue/painting-waterproofing/textures.jpg',
       TRUE, FALSE, 6, '29380e03-6ec4-47d2-88e5-e8a9159eaaa7'
WHERE NOT EXISTS (SELECT 1 FROM category WHERE slug = 'textures');

INSERT INTO service (id, category_id, name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, is_active, sort_order, cover_image_url)
SELECT gen_random_uuid(), (SELECT id FROM category WHERE slug = 'textures'), v.name, v.slug, v.description, v.short_description, v.inclusions, v.exclusions, v.price, v.duration_minutes, TRUE, v.sort_order, '/images/catalogue/painting-waterproofing/services/' || v.slug || '.jpg'
FROM (VALUES
    ('Textured wall (per wall)','textured-wall-per-wall','Textured finish for one accent wall.','Textured accent wall',E'Surface prep and base coat\nTexture application with chosen pattern\nFinal sealant coat',E'Design consultation beyond standard patterns\nRepair of underlying wall damage',2499.00,180,0),
    ('3D textured wall (per wall)','3d-textured-wall-per-wall','Raised 3D textured finish for one accent wall.','3D textured accent wall',E'Surface prep and base coat\n3D texture application with chosen pattern\nFinal sealant and highlight coat',E'Design consultation beyond standard patterns\nRepair of underlying wall damage',3499.00,240,1)
) AS v(name, slug, description, short_description, inclusions, exclusions, price, duration_minutes, sort_order)
WHERE NOT EXISTS (SELECT 1 FROM service WHERE slug = v.slug);

-- 9. Serviceability: map every new/moved service into every pincode the
-- umbrella category already serves.
INSERT INTO service_pincode_mapping (id, service_id, pincode_id, is_active)
SELECT gen_random_uuid(), sv.id, p.id, TRUE
FROM service sv
JOIN category c ON c.id = sv.category_id
CROSS JOIN pincode p
WHERE c.parent_category_id = '29380e03-6ec4-47d2-88e5-e8a9159eaaa7'
  AND sv.is_active
  AND NOT EXISTS (
      SELECT 1 FROM service_pincode_mapping m
      WHERE m.service_id = sv.id AND m.pincode_id = p.id);

COMMIT;
