/**
 * Typed client for the Admin API's landing-page curation surface:
 * `LandingController` - which catalog entries the customer home page's
 * "New & Trending", "Most Booked Services" and category strips show.
 *
 * Gated behind the "cms" permission module server-side (it merchandises
 * catalog data rather than editing it), so every call is authenticated.
 */
import { API_V1, apiFetch } from "./api";
import type {
  LandingConfig,
  UpdateCategorySectionRequest,
  UpdateMostBookedRequest,
  UpdateNewAndTrendingRequest,
} from "./landing-types";

const LANDING_BASE = `${API_V1}/landing`;

export const getLandingConfig = () =>
  apiFetch<LandingConfig>(LANDING_BASE, { authenticated: true });

export const updateNewAndTrending = (request: UpdateNewAndTrendingRequest) =>
  apiFetch<void>(`${LANDING_BASE}/new-and-trending`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateMostBooked = (request: UpdateMostBookedRequest) =>
  apiFetch<void>(`${LANDING_BASE}/most-booked`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateCategorySection = (categoryId: string, request: UpdateCategorySectionRequest) =>
  apiFetch<void>(`${LANDING_BASE}/category-sections/${categoryId}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });
