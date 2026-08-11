/**
 * Typed client for the Admin API's CMS surface (SRS 12.16, tasks 124a-125c):
 * `CmsPagesController`, `BannersController`, `CmsFaqsController`,
 * `CmsMediaController`. Every call is authenticated - these are admin-only
 * endpoints gated behind the "cms" permission module server-side.
 */
import { API_V1, apiFetch, apiFetchUpload } from "./api";
import type {
  BannerAdminSearchResponse,
  BannerCreateRequest,
  BannerResponse,
  BannerSearchParams,
  BannerUpdateRequest,
  CategoryLookupResponse,
  CmsFaqAdminSearchResponse,
  CmsFaqCreateRequest,
  CmsFaqResponse,
  CmsFaqSearchParams,
  CmsFaqUpdateRequest,
  CmsMediaCreateRequest,
  CmsMediaResponse,
  CmsPageAdminSearchResponse,
  CmsPageCreateRequest,
  CmsPageResponse,
  CmsPageSearchParams,
  CmsPageUpdateRequest,
} from "./cms-types";

const CMS_BASE = `${API_V1}/cms`;
const PAGES_BASE = `${CMS_BASE}/pages`;
const BANNERS_BASE = `${CMS_BASE}/banners`;
const FAQS_BASE = `${CMS_BASE}/faqs`;
const MEDIA_BASE = `${CMS_BASE}/media`;

// Parameter typed as `object` (not `Record<string, ...>`) so that named
// interfaces like CmsPageSearchParams - which have no index signature of
// their own - can be passed in without a cast; the entries are read via a
// loosely-typed view of the same object instead. Mirrors coupon-api.ts's
// `query` helper exactly.
function query(params: object): string {
  const entries = Object.entries(params as Record<string, string | number | boolean | undefined>)
    .filter(([, value]) => value !== undefined);
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries.map(([key, value]) => [key, String(value)])).toString()}`;
}

// ---------------------------------------------------------------------
// Pages
// ---------------------------------------------------------------------

export const searchCmsPages = (params: CmsPageSearchParams) =>
  apiFetch<CmsPageAdminSearchResponse>(`${PAGES_BASE}${query(params)}`, { authenticated: true });

export const getCmsPage = (id: string) =>
  apiFetch<CmsPageResponse>(`${PAGES_BASE}/${id}`, { authenticated: true });

export const createCmsPage = (request: CmsPageCreateRequest) =>
  apiFetch<CmsPageResponse>(PAGES_BASE, { method: "POST", authenticated: true, body: JSON.stringify(request) });

export const updateCmsPage = (id: string, request: CmsPageUpdateRequest) =>
  apiFetch<CmsPageResponse>(`${PAGES_BASE}/${id}`, { method: "PUT", authenticated: true, body: JSON.stringify(request) });

export const setCmsPagePublished = (id: string, published: boolean) =>
  apiFetch<void>(`${PAGES_BASE}/${id}/${published ? "publish" : "unpublish"}`, { method: "POST", authenticated: true });

// ---------------------------------------------------------------------
// Banners
// ---------------------------------------------------------------------

export const listBannerCategories = () =>
  apiFetch<CategoryLookupResponse[]>(`${BANNERS_BASE}/categories`, { authenticated: true });

export const listBannerMedia = () =>
  apiFetch<CmsMediaResponse[]>(`${BANNERS_BASE}/media`, { authenticated: true });

export const searchBanners = (params: BannerSearchParams) =>
  apiFetch<BannerAdminSearchResponse>(`${BANNERS_BASE}${query(params)}`, { authenticated: true });

export const getBanner = (id: string) =>
  apiFetch<BannerResponse>(`${BANNERS_BASE}/${id}`, { authenticated: true });

export const createBanner = (request: BannerCreateRequest) =>
  apiFetch<BannerResponse>(BANNERS_BASE, { method: "POST", authenticated: true, body: JSON.stringify(request) });

export const updateBanner = (id: string, request: BannerUpdateRequest) =>
  apiFetch<BannerResponse>(`${BANNERS_BASE}/${id}`, { method: "PUT", authenticated: true, body: JSON.stringify(request) });

export const setBannerPublished = (id: string, published: boolean) =>
  apiFetch<void>(`${BANNERS_BASE}/${id}/${published ? "publish" : "unpublish"}`, { method: "POST", authenticated: true });

// ---------------------------------------------------------------------
// FAQs
// ---------------------------------------------------------------------

export const searchCmsFaqs = (params: CmsFaqSearchParams) =>
  apiFetch<CmsFaqAdminSearchResponse>(`${FAQS_BASE}${query(params)}`, { authenticated: true });

export const getCmsFaq = (id: string) =>
  apiFetch<CmsFaqResponse>(`${FAQS_BASE}/${id}`, { authenticated: true });

export const createCmsFaq = (request: CmsFaqCreateRequest) =>
  apiFetch<CmsFaqResponse>(FAQS_BASE, { method: "POST", authenticated: true, body: JSON.stringify(request) });

export const updateCmsFaq = (id: string, request: CmsFaqUpdateRequest) =>
  apiFetch<CmsFaqResponse>(`${FAQS_BASE}/${id}`, { method: "PUT", authenticated: true, body: JSON.stringify(request) });

export const setCmsFaqPublished = (id: string, published: boolean) =>
  apiFetch<void>(`${FAQS_BASE}/${id}/${published ? "publish" : "unpublish"}`, { method: "POST", authenticated: true });

// ---------------------------------------------------------------------
// Media
// ---------------------------------------------------------------------

export const listCmsMedia = () => apiFetch<CmsMediaResponse[]>(MEDIA_BASE, { authenticated: true });

export const createCmsMedia = (request: CmsMediaCreateRequest) =>
  apiFetch<CmsMediaResponse>(MEDIA_BASE, { method: "POST", authenticated: true, body: JSON.stringify(request) });

/** Task 314: uploads a file directly instead of registering an already-hosted URL. */
export const uploadCmsMedia = (file: File, altText: string | null) => {
  const formData = new FormData();
  formData.append("file", file);
  if (altText !== null) formData.append("altText", altText);
  return apiFetchUpload<CmsMediaResponse>(`${MEDIA_BASE}/upload`, formData, { authenticated: true });
};
