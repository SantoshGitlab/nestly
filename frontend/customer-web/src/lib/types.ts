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
