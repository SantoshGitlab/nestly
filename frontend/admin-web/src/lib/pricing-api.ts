/**
 * Typed client for the Admin API's pricing management surface (SRS 12.8,
 * tasks 109a-109e): `PricingController`. Every call is authenticated - these
 * are admin-only endpoints gated behind the "pricing" permission module
 * server-side (`pricing.read`/`pricing.write`).
 */
import { API_V1, apiFetch } from "./api";
import type {
  AddOnPriceResponse,
  AddOnPriceUpdateRequest,
  CityPriceCreateRequest,
  CityPriceResponse,
  CityPriceUpdateRequest,
  CityPricingPolicyResponse,
  CityPricingPolicyUpsertRequest,
  PromotionalPriceCreateRequest,
  PromotionalPriceResponse,
  PromotionalPriceUpdateRequest,
  ServicePriceResponse,
  ServicePriceUpdateRequest,
} from "./pricing-types";

const PRICING_BASE = `${API_V1}/pricing`;

function query(params: Record<string, string | undefined>): string {
  const entries = Object.entries(params).filter(([, value]) => value !== undefined);
  if (entries.length === 0) return "";
  return `?${new URLSearchParams(entries as [string, string][]).toString()}`;
}

// ---- Base price ----

export const listServicePrices = () =>
  apiFetch<ServicePriceResponse[]>(`${PRICING_BASE}/services`, { authenticated: true });

export const updateServicePrice = (serviceId: string, request: ServicePriceUpdateRequest) =>
  apiFetch<ServicePriceResponse>(`${PRICING_BASE}/services/${serviceId}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

// ---- Add-on price ----

export const listAddOnPrices = (serviceId?: string) =>
  apiFetch<AddOnPriceResponse[]>(`${PRICING_BASE}/addons${query({ serviceId })}`, { authenticated: true });

export const updateAddOnPrice = (addOnId: string, request: AddOnPriceUpdateRequest) =>
  apiFetch<AddOnPriceResponse>(`${PRICING_BASE}/addons/${addOnId}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

// ---- City-wise price ----

export const listCityPrices = (serviceId?: string, cityId?: string) =>
  apiFetch<CityPriceResponse[]>(`${PRICING_BASE}/city-prices${query({ serviceId, cityId })}`, { authenticated: true });

export const createCityPrice = (request: CityPriceCreateRequest) =>
  apiFetch<CityPriceResponse>(`${PRICING_BASE}/city-prices`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateCityPrice = (id: string, request: CityPriceUpdateRequest) =>
  apiFetch<CityPriceResponse>(`${PRICING_BASE}/city-prices/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

// ---- Promotional price ----

export const listPromotionalPrices = (serviceId?: string) =>
  apiFetch<PromotionalPriceResponse[]>(`${PRICING_BASE}/promotions${query({ serviceId })}`, { authenticated: true });

export const createPromotionalPrice = (request: PromotionalPriceCreateRequest) =>
  apiFetch<PromotionalPriceResponse>(`${PRICING_BASE}/promotions`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updatePromotionalPrice = (id: string, request: PromotionalPriceUpdateRequest) =>
  apiFetch<PromotionalPriceResponse>(`${PRICING_BASE}/promotions/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setPromotionalPriceActive = (id: string, isActive: boolean) =>
  apiFetch<void>(`${PRICING_BASE}/promotions/${id}/${isActive ? "activate" : "deactivate"}`, {
    method: "POST",
    authenticated: true,
  });

// ---- City pricing policy: tax + fees ----

export const listCityPricingPolicies = () =>
  apiFetch<CityPricingPolicyResponse[]>(`${PRICING_BASE}/policies`, { authenticated: true });

export const upsertCityPricingPolicy = (cityId: string, request: CityPricingPolicyUpsertRequest) =>
  apiFetch<CityPricingPolicyResponse>(`${PRICING_BASE}/policies/${cityId}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });
