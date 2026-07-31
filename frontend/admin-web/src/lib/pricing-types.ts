/**
 * Response/request shapes for the Admin API's pricing management surface
 * (SRS 12.8, tasks 109a-109e). Mirrors the backend records in
 * `backend/shared/Application/Pricing/PricingManagementContracts.cs`
 * field-for-field - ASP.NET Core's default controller JSON options
 * camelCase every property, which is what these interfaces assume.
 */

// ---- Base price ----

export interface ServicePriceResponse {
  serviceId: string;
  serviceName: string;
  price: number;
}

export interface ServicePriceUpdateRequest {
  price: number;
}

// ---- Add-on price ----

export interface AddOnPriceResponse {
  addOnId: string;
  serviceId: string;
  serviceName: string;
  addOnName: string;
  price: number;
}

export interface AddOnPriceUpdateRequest {
  price: number;
}

// ---- City-wise price (effective dating, task 109d) ----

export interface CityPriceResponse {
  id: string;
  serviceId: string;
  serviceName: string;
  cityId: string;
  cityName: string;
  price: number;
  /** ISO date string (yyyy-MM-dd). */
  effectiveStartDate: string;
  /** ISO date string (yyyy-MM-dd); null = no expiry. */
  effectiveEndDate: string | null;
}

export interface CityPriceCreateRequest {
  serviceId: string;
  cityId: string;
  price: number;
  effectiveStartDate?: string | null;
  effectiveEndDate?: string | null;
}

export interface CityPriceUpdateRequest {
  price: number;
  effectiveStartDate: string;
  effectiveEndDate: string | null;
}

// ---- Promotional price ----

export interface PromotionalPriceResponse {
  id: string;
  serviceId: string;
  serviceName: string;
  cityId: string | null;
  cityName: string | null;
  discountedPrice: number;
  startDate: string;
  endDate: string;
  isActive: boolean;
}

export interface PromotionalPriceCreateRequest {
  serviceId: string;
  cityId: string | null;
  discountedPrice: number;
  startDate: string;
  endDate: string;
}

export interface PromotionalPriceUpdateRequest {
  discountedPrice: number;
  startDate: string;
  endDate: string;
}

// ---- City pricing policy: tax + fees (tasks 109b/109c) ----

export interface CityPricingPolicyResponse {
  id: string;
  cityId: string;
  cityName: string;
  visitCharge: number;
  taxPercentage: number;
  platformFee: number;
}

export interface CityPricingPolicyUpsertRequest {
  visitCharge: number;
  taxPercentage: number;
  platformFee: number;
}
