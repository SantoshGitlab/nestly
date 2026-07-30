/**
 * Response shapes returned by the Consumer API.
 *
 * These mirror the C# records in Nestly.Application (Identity/*, Profile/*,
 * Addresses/*) — ASP.NET serialises records as camelCase JSON by default, so
 * property names here are the camelCase form of the C# ones.
 */

export interface LoginResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
}

export interface CustomerSummary {
  id: string;
  mobile: string;
  email: string | null;
  name: string;
  status: string;
}

export interface CustomerProfile {
  id: string;
  mobile: string;
  email: string | null;
  name: string;
  dateOfBirth: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
  country: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface CommunicationPreferences {
  transactionalSms: boolean;
  transactionalEmail: boolean;
  transactionalWhatsApp: boolean;
  promotionalSms: boolean;
  promotionalEmail: boolean;
  promotionalWhatsApp: boolean;
  push: boolean;
  updatedAt: string;
}

export interface CustomerAddress {
  id: string;
  label: string;
  line1: string;
  line2: string | null;
  landmark: string | null;
  pincode: string;
  city: string;
  state: string;
  latitude: number;
  longitude: number;
  contactName: string;
  contactMobile: string;
  isDefault: boolean;
}

/**
 * Catalog/geography/serviceability/slot/pricing shapes mirror the C# records
 * in Nestly.Application (Catalog/*, Geography/*, Serviceability/*, Slots/*,
 * Pricing/*) - see CategoriesController, ServicesController, CatalogSearchController,
 * GeographyController, ServiceabilityController, SlotsController, PricingController.
 */

export interface CategorySummary {
  id: string;
  name: string;
  slug: string;
  iconUrl: string | null;
  bannerUrl: string | null;
  isFeatured: boolean;
}

export interface ServiceAddOnSummary {
  id: string;
  name: string;
  description: string | null;
  price: number;
}

export interface ServiceSummary {
  id: string;
  name: string;
  slug: string;
  description: string;
  price: number;
  addOns: ServiceAddOnSummary[];
}

export interface CategoryDetail {
  id: string;
  name: string;
  slug: string;
  description: string;
  iconUrl: string | null;
  bannerUrl: string | null;
  services: ServiceSummary[];
}

export interface ServiceListItem {
  id: string;
  name: string;
  slug: string;
  description: string;
  price: number;
}

export interface ServiceDetail {
  id: string;
  name: string;
  slug: string;
  description: string;
  price: number;
  inclusions: string;
  exclusions: string;
  cancellationPolicy: string | null;
  reschedulePolicy: string | null;
  categoryId: string;
  categoryName: string;
  categorySlug: string;
  addOns: ServiceAddOnSummary[];
}

export interface CatalogSearchResult {
  categories: CategorySummary[];
  services: ServiceListItem[];
}

export interface City {
  id: string;
  name: string;
  stateName: string;
}

export interface LocalitySearchResult {
  id: string;
  name: string;
  zoneName: string;
  pincodeCode: string;
  pincodeId: string;
}

export interface ServiceabilityResult {
  isServiceable: boolean;
}

export interface SlotOption {
  slotWindowId: string;
  name: string;
  /** .NET TimeSpan serialises as "hh:mm:ss". */
  startTime: string;
  endTime: string;
  maxBookingsPerSlot: number | null;
}

export interface SlotAvailability {
  isServiceable: boolean;
  slots: SlotOption[];
}

export interface SlotRevalidation {
  isValid: boolean;
  reason: string | null;
}

export interface AddOnSelection {
  addOnId: string;
  quantity: number;
}

export interface PriceCalculationRequest {
  serviceId: string;
  cityId: string;
  quantity: number;
  addOns: AddOnSelection[];
}

export interface AddOnLineItem {
  addOnId: string;
  name: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface PriceBreakdown {
  basePrice: number;
  quantity: number;
  baseTotal: number;
  addOnLineItems: AddOnLineItem[];
  addOnTotal: number;
  visitCharge: number;
  subtotal: number;
  taxPercentage: number;
  taxAmount: number;
  platformFee: number;
  totalPayable: number;
}

/**
 * Booking shapes mirror the C# records in Nestly.Application.Bookings
 * (BookingContracts.cs) - see BookingsController.
 */

export interface BookingSummaryRequestBody {
  serviceId: string;
  cityId: string;
  addressId: string;
  localityId: string;
  slotWindowId: string;
  /** .NET DateOnly serialises as "yyyy-MM-dd". */
  slotDate: string;
  quantity: number;
  addOns: AddOnSelection[];
}

export interface BookingServiceSummary {
  id: string;
  name: string;
  slug: string;
}

export interface BookingAddressSummary {
  id: string;
  label: string;
  line1: string;
  line2: string | null;
  landmark: string | null;
  pincode: string;
  city: string;
  state: string;
  latitude: number;
  longitude: number;
  contactName: string;
  contactMobile: string;
}

export interface BookingSlotSummary {
  slotWindowId: string;
  name: string;
  date: string;
  startTime: string;
  endTime: string;
}

/** Booking summary/preview (SRS 11.7). Coupon/wallet are omitted - neither module exists yet (Phase 4). */
export interface BookingSummary {
  service: BookingServiceSummary;
  addOns: ServiceAddOnSummary[];
  address: BookingAddressSummary;
  slot: BookingSlotSummary;
  price: PriceBreakdown;
  cancellationPolicy: string | null;
  reschedulePolicy: string | null;
}

/**
 * Mirrors Nestly.Domain.BookingStatus's declaration order exactly. The
 * ConsumerApi has no JsonStringEnumConverter registered, so this enum
 * serialises over the wire as its ordinal (a plain number), not its name -
 * this mapping must stay in sync with BookingStatus.cs if that enum's order
 * ever changes.
 */
export enum BookingStatus {
  Initiated = 0,
  PaymentPending = 1,
  PaymentFailed = 2,
  Confirmed = 3,
  AwaitingFulfilment = 4,
  Assigned = 5,
  InProgress = 6,
  Completed = 7,
  CancelledByCustomer = 8,
  CancelledByAdmin = 9,
  Rescheduled = 10,
  RefundPending = 11,
  Refunded = 12,
}

/** Matches Nestly.Domain.BookingStatusBucket's member names - passed as the `bucket` query string value, which ASP.NET binds by name. */
export type BookingStatusBucket = "Upcoming" | "Completed" | "Cancelled";

export interface BookingStatusTimelineEntry {
  fromStatus: BookingStatus | null;
  toStatus: BookingStatus;
  toStatusLabel: string;
  reason: string | null;
  changedAtUtc: string;
}

export interface BookingDetail {
  id: string;
  service: BookingServiceSummary;
  addOns: ServiceAddOnSummary[];
  address: BookingAddressSummary;
  slot: BookingSlotSummary;
  price: PriceBreakdown;
  status: BookingStatus;
  statusLabel: string;
  timeline: BookingStatusTimelineEntry[];
  createdAtUtc: string;
}

export interface BookingListItem {
  id: string;
  serviceName: string;
  slotDate: string;
  totalPayable: number;
  status: BookingStatus;
  statusLabel: string;
  createdAtUtc: string;
}
