/**
 * Response/request shapes for the Admin API's CMS surface (SRS 12.16, tasks
 * 124a-125c): `CmsPagesController`, `BannersController`, `CmsFaqsController`,
 * `CmsMediaController`. Mirrors the backend contracts in
 * `Application/Cms/CmsContracts.cs` field for field.
 *
 * AdminApi has no JsonStringEnumConverter registered (see
 * lib/coupon-types.ts's doc comment), so every enum below serialises over
 * the wire as its ordinal and must stay in declaration-order sync with its
 * C# source (`Nestly.Domain.CmsContentStatus` / `Nestly.Domain.CmsPlacement`).
 */

export enum CmsContentStatus {
  Draft = 0,
  Published = 1,
}

export enum CmsPlacement {
  Home = 0,
  CategoryPage = 1,
  Promotional = 2,
  Footer = 3,
  General = 4,
}

/** Lightweight category lookup for the banner form's "category" picker (CategoryPage placement only). */
export interface CategoryLookupResponse {
  id: string;
  name: string;
}

// ---------------------------------------------------------------------
// Media (task 124e)
// ---------------------------------------------------------------------

export interface CmsMediaResponse {
  id: string;
  url: string;
  altText: string | null;
  createdAtUtc: string;
}

export interface CmsMediaCreateRequest {
  url: string;
  altText: string | null;
}

export interface CmsMediaUpdateRequest {
  url: string;
  altText: string | null;
}

// ---------------------------------------------------------------------
// Pages (task 124a)
// ---------------------------------------------------------------------

export interface CmsPageResponse {
  id: string;
  title: string;
  slug: string;
  body: string;
  seoTitle: string | null;
  seoDescription: string | null;
  seoKeywords: string | null;
  placement: CmsPlacement;
  status: CmsContentStatus;
  publishStartUtc: string | null;
  publishEndUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CmsPageAdminSearchResponse {
  items: CmsPageResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Query parameters for the page search endpoint. All optional. */
export interface CmsPageSearchParams {
  title?: string;
  slug?: string;
  status?: CmsContentStatus;
  placement?: CmsPlacement;
  page?: number;
  pageSize?: number;
}

export interface CmsPageCreateRequest {
  title: string;
  slug: string;
  body: string;
  seoTitle: string | null;
  seoDescription: string | null;
  seoKeywords: string | null;
  placement: CmsPlacement;
  publishStartUtc: string | null;
  publishEndUtc: string | null;
}

export interface CmsPageUpdateRequest {
  title: string;
  slug: string;
  body: string;
  seoTitle: string | null;
  seoDescription: string | null;
  seoKeywords: string | null;
  placement: CmsPlacement;
  publishStartUtc: string | null;
  publishEndUtc: string | null;
}

// ---------------------------------------------------------------------
// Banners (task 124b)
// ---------------------------------------------------------------------

export interface BannerResponse {
  id: string;
  title: string;
  subtitle: string | null;
  mediaId: string;
  mediaUrl: string;
  linkUrl: string | null;
  placement: CmsPlacement;
  categoryId: string | null;
  categoryName: string | null;
  sortOrder: number;
  status: CmsContentStatus;
  publishStartUtc: string | null;
  publishEndUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface BannerAdminSearchResponse {
  items: BannerResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Query parameters for the banner search endpoint. All optional. */
export interface BannerSearchParams {
  placement?: CmsPlacement;
  status?: CmsContentStatus;
  categoryId?: string;
  page?: number;
  pageSize?: number;
}

export interface BannerCreateRequest {
  title: string;
  subtitle: string | null;
  mediaId: string;
  linkUrl: string | null;
  placement: CmsPlacement;
  categoryId: string | null;
  sortOrder: number;
  publishStartUtc: string | null;
  publishEndUtc: string | null;
}

export interface BannerUpdateRequest {
  title: string;
  subtitle: string | null;
  mediaId: string;
  linkUrl: string | null;
  placement: CmsPlacement;
  categoryId: string | null;
  sortOrder: number;
  publishStartUtc: string | null;
  publishEndUtc: string | null;
}

// ---------------------------------------------------------------------
// FAQs (task 124c)
// ---------------------------------------------------------------------

export interface CmsFaqResponse {
  id: string;
  question: string;
  answer: string;
  placement: CmsPlacement;
  sortOrder: number;
  status: CmsContentStatus;
  publishStartUtc: string | null;
  publishEndUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CmsFaqAdminSearchResponse {
  items: CmsFaqResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Query parameters for the FAQ search endpoint. All optional. */
export interface CmsFaqSearchParams {
  placement?: CmsPlacement;
  status?: CmsContentStatus;
  page?: number;
  pageSize?: number;
}

export interface CmsFaqCreateRequest {
  question: string;
  answer: string;
  placement: CmsPlacement;
  sortOrder: number;
  publishStartUtc: string | null;
  publishEndUtc: string | null;
}

export interface CmsFaqUpdateRequest {
  question: string;
  answer: string;
  placement: CmsPlacement;
  sortOrder: number;
  publishStartUtc: string | null;
  publishEndUtc: string | null;
}
