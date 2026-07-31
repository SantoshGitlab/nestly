/**
 * Types for the Admin API's catalog management surface (SRS 12.5-12.7, tasks
 * 103a-107): categories, services/packages and add-ons. Mirrors the backend
 * contracts in backend/shared/Application/Catalog/*ManagementContracts.cs
 * field-for-field.
 */

// ---- Categories (SRS 12.5) ----

export interface CategoryResponse {
  id: string;
  name: string;
  slug: string;
  description: string;
  iconUrl: string | null;
  bannerUrl: string | null;
  isActive: boolean;
  isFeatured: boolean;
  sortOrder: number;
  seoTitle: string | null;
  seoMetaDescription: string | null;
}

export interface CategoryCreateRequest {
  name: string;
  slug: string;
  description: string;
  iconUrl: string | null;
  bannerUrl: string | null;
  sortOrder: number;
  seoTitle: string | null;
  seoMetaDescription: string | null;
}

export type CategoryUpdateRequest = CategoryCreateRequest;

// ---- Services / packages (SRS 12.6) ----

export type ServicePricingType = "Fixed" | "Variable";

export interface ServiceAdminResponse {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  slug: string;
  description: string;
  shortDescription: string | null;
  price: number;
  isActive: boolean;
  inclusions: string;
  exclusions: string;
  cancellationPolicy: string | null;
  reschedulePolicy: string | null;
  durationMinutes: number;
  isFeatured: boolean;
  sortOrder: number;
  seoTitle: string | null;
  seoMetaDescription: string | null;
  pricingType: ServicePricingType;
  isTaxApplicable: boolean;
  isAddOnAllowed: boolean;
  isQuantityAllowed: boolean;
  isInspectionBased: boolean;
  isSlotRequired: boolean;
  isAddressRequired: boolean;
  isCustomerNoteAllowed: boolean;
}

export interface ServiceCreateRequest {
  categoryId: string;
  name: string;
  slug: string;
  description: string;
  shortDescription: string | null;
  price: number;
  inclusions: string;
  exclusions: string;
  cancellationPolicy: string | null;
  reschedulePolicy: string | null;
  durationMinutes: number;
  sortOrder: number;
  seoTitle: string | null;
  seoMetaDescription: string | null;
  pricingType: ServicePricingType;
  isTaxApplicable: boolean;
  isAddOnAllowed: boolean;
  isQuantityAllowed: boolean;
  isInspectionBased: boolean;
  isSlotRequired: boolean;
  isAddressRequired: boolean;
  isCustomerNoteAllowed: boolean;
}

export type ServiceUpdateRequest = ServiceCreateRequest;

export interface ServiceMediaResponse {
  id: string;
  serviceId: string;
  url: string;
}

export interface ServiceMediaCreateRequest {
  url: string;
}

// ---- Add-ons (SRS 12.7) ----

export interface ServiceAddOnAdminResponse {
  id: string;
  serviceId: string;
  serviceName: string;
  name: string;
  description: string | null;
  price: number;
  isActive: boolean;
  sortOrder: number;
  isQuantityAllowed: boolean;
  isMandatory: boolean;
}

export interface ServiceAddOnCreateRequest {
  serviceId: string;
  name: string;
  description: string | null;
  price: number;
  sortOrder: number;
  isQuantityAllowed: boolean;
  isMandatory: boolean;
}

export type ServiceAddOnUpdateRequest = ServiceAddOnCreateRequest;
