/**
 * Curated home-page section shapes, mirroring the C# records in
 * Nestly.Application.Landing (LandingContracts.cs) - see consumer-api's
 * LandingController (`GET /landing/home`).
 */

/** A "New & Trending" sub-category card - image and name only, no price. */
export interface LandingSubCategory {
  id: string;
  name: string;
  slug: string;
  imageUrl: string | null;
  /** The top-level category this sits under, for the "Category → Sub-category" label. */
  parentCategoryName: string;
}

/** A bookable-service card ("Most Booked", category strips) - the same image/title/price triple `ServiceCard` renders. */
export interface LandingService {
  id: string;
  name: string;
  slug: string;
  imageUrl: string | null;
  price: number;
}

/** One category-wise strip: the heading category plus its admin-picked services (at most 5). */
export interface LandingCategorySection {
  categoryId: string;
  categoryName: string;
  categorySlug: string;
  services: LandingService[];
}

/**
 * The whole curated home page in one response. Sections the admin has not
 * configured come back as empty arrays, never undefined.
 */
export interface HomeLanding {
  newAndTrending: LandingSubCategory[];
  mostBooked: LandingService[];
  categorySections: LandingCategorySection[];
}
