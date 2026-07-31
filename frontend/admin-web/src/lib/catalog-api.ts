/**
 * Typed client for the Admin API's catalog management surface (SRS 12.5-12.7,
 * tasks 103a-107): `CategoriesController`, `ServicesController`,
 * `ServiceAddOnsController`. Every call is authenticated - these are
 * admin-only endpoints gated behind the "catalog" permission module
 * server-side (SRS 12.5-12.7 share one module).
 */
import { API_V1, apiFetch } from "./api";
import type {
  CategoryCreateRequest,
  CategoryResponse,
  CategoryUpdateRequest,
  ServiceAddOnAdminResponse,
  ServiceAddOnCreateRequest,
  ServiceAddOnUpdateRequest,
  ServiceAdminResponse,
  ServiceCreateRequest,
  ServiceMediaCreateRequest,
  ServiceMediaResponse,
  ServiceUpdateRequest,
} from "./catalog-types";

const CATEGORIES_BASE = `${API_V1}/catalog/categories`;
const SERVICES_BASE = `${API_V1}/catalog/services`;
const ADDONS_BASE = `${API_V1}/catalog/addons`;

function query(params: Record<string, string | undefined>): string {
  const entries = Object.entries(params).filter(([, value]) => value !== undefined);
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries as [string, string][]).toString()}`;
}

// ---- Categories ----

export const listCategories = () =>
  apiFetch<CategoryResponse[]>(CATEGORIES_BASE, { authenticated: true });

export const getCategory = (id: string) =>
  apiFetch<CategoryResponse>(`${CATEGORIES_BASE}/${id}`, { authenticated: true });

export const createCategory = (request: CategoryCreateRequest) =>
  apiFetch<CategoryResponse>(CATEGORIES_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateCategory = (id: string, request: CategoryUpdateRequest) =>
  apiFetch<CategoryResponse>(`${CATEGORIES_BASE}/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setCategoryActive = (id: string, isActive: boolean) =>
  apiFetch<void>(`${CATEGORIES_BASE}/${id}/${isActive ? "activate" : "deactivate"}`, {
    method: "POST",
    authenticated: true,
  });

export const setCategoryFeatured = (id: string, isFeatured: boolean) =>
  apiFetch<void>(`${CATEGORIES_BASE}/${id}/${isFeatured ? "feature" : "unfeature"}`, {
    method: "POST",
    authenticated: true,
  });

// ---- Services / packages ----

export const listServices = (categoryId?: string) =>
  apiFetch<ServiceAdminResponse[]>(`${SERVICES_BASE}${query({ categoryId })}`, { authenticated: true });

export const getService = (id: string) =>
  apiFetch<ServiceAdminResponse>(`${SERVICES_BASE}/${id}`, { authenticated: true });

export const createService = (request: ServiceCreateRequest) =>
  apiFetch<ServiceAdminResponse>(SERVICES_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateService = (id: string, request: ServiceUpdateRequest) =>
  apiFetch<ServiceAdminResponse>(`${SERVICES_BASE}/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setServiceActive = (id: string, isActive: boolean) =>
  apiFetch<void>(`${SERVICES_BASE}/${id}/${isActive ? "activate" : "deactivate"}`, {
    method: "POST",
    authenticated: true,
  });

export const setServiceFeatured = (id: string, isFeatured: boolean) =>
  apiFetch<void>(`${SERVICES_BASE}/${id}/${isFeatured ? "feature" : "unfeature"}`, {
    method: "POST",
    authenticated: true,
  });

export const listServiceMedia = (serviceId: string) =>
  apiFetch<ServiceMediaResponse[]>(`${SERVICES_BASE}/${serviceId}/media`, { authenticated: true });

export const addServiceMedia = (serviceId: string, request: ServiceMediaCreateRequest) =>
  apiFetch<ServiceMediaResponse>(`${SERVICES_BASE}/${serviceId}/media`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const removeServiceMedia = (serviceId: string, mediaId: string) =>
  apiFetch<void>(`${SERVICES_BASE}/${serviceId}/media/${mediaId}`, {
    method: "DELETE",
    authenticated: true,
  });

// ---- Add-ons ----

export const listServiceAddOns = (serviceId?: string) =>
  apiFetch<ServiceAddOnAdminResponse[]>(`${ADDONS_BASE}${query({ serviceId })}`, { authenticated: true });

export const getServiceAddOn = (id: string) =>
  apiFetch<ServiceAddOnAdminResponse>(`${ADDONS_BASE}/${id}`, { authenticated: true });

export const createServiceAddOn = (request: ServiceAddOnCreateRequest) =>
  apiFetch<ServiceAddOnAdminResponse>(ADDONS_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateServiceAddOn = (id: string, request: ServiceAddOnUpdateRequest) =>
  apiFetch<ServiceAddOnAdminResponse>(`${ADDONS_BASE}/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setServiceAddOnActive = (id: string, isActive: boolean) =>
  apiFetch<void>(`${ADDONS_BASE}/${id}/${isActive ? "activate" : "deactivate"}`, {
    method: "POST",
    authenticated: true,
  });
