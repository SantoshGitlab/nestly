/**
 * Landing-page curation shapes (admin), mirroring the C# records in
 * Nestly.Application.Landing (LandingManagementContracts.cs) - see
 * admin-api's LandingController.
 *
 * These pick which existing catalog entries the customer home page
 * merchandises; they never create or edit catalog data itself.
 */

/** One admin-picked sub-category in "New & Trending". */
export interface LandingNewAndTrendingItem {
  categoryId: string;
  categoryName: string;
  /** Empty when the picked category is top-level rather than a sub-category. */
  parentCategoryName: string;
  sortOrder: number;
}

/** One admin-picked service, in "Most Booked" or under a category strip. */
export interface LandingServiceItem {
  serviceId: string;
  serviceName: string;
  categoryName: string;
  price: number;
  sortOrder: number;
}

/** One configured category strip: the heading category and its ordered picks. */
export interface LandingCategorySectionItem {
  categoryId: string;
  categoryName: string;
  services: LandingServiceItem[];
}

/** The full curation config - all three sections in one call. */
export interface LandingConfig {
  newAndTrending: LandingNewAndTrendingItem[];
  mostBooked: LandingServiceItem[];
  categorySections: LandingCategorySectionItem[];
}

/**
 * Every write replaces a whole section, so list order IS display order and
 * the screen never has to manage sort values.
 */
export interface UpdateNewAndTrendingRequest {
  categoryIds: string[];
}

export interface UpdateMostBookedRequest {
  serviceIds: string[];
}

export interface UpdateCategorySectionRequest {
  serviceIds: string[];
}

/** Mirrors LandingSelection.MaxServicesPerCategorySection - the server rejects more. */
export const MAX_SERVICES_PER_CATEGORY_SECTION = 5;
