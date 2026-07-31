/**
 * Response/request shapes for the Partner API's profile/onboarding surface
 * (`/api/v1/profile`, docs/PARTNER.md's Capability & Coverage / Identity
 * domains): profile details, KYC documents, service areas, and skills.
 *
 * partner-api registers a JsonStringEnumConverter (per the task brief this
 * client was built against), so enum-like fields below are plain string
 * unions rather than the ordinal-number encoding admin-web's AdminApi types
 * need - no declaration-order coupling to a C# source to maintain here.
 */
import type { PartnerOnboardingStatus, PartnerProfile } from "./types";

export interface UpdateProfileRequest {
  legalName: string;
  displayName: string;
  email?: string;
}

/** partner_kyc_document.doc_type (docs/PARTNER.md's Identity domain). */
export type KycDocType =
  | "IdentityProof"
  | "AddressProof"
  | "BankAccountProof"
  | "ProfessionalCertificate"
  | "Other";

/** partner_kyc_document.verification_status. */
export type KycVerificationStatus = "Pending" | "Approved" | "Rejected";

export interface KycDocument {
  id: string;
  partnerId: string;
  docType: KycDocType;
  docNumber: string | null;
  fileRef: string;
  verificationStatus: KycVerificationStatus;
  submittedAt: string;
  verifiedAt: string | null;
}

export interface KycStatusResponse {
  partnerId: string;
  onboardingStatus: PartnerOnboardingStatus;
  documents: KycDocument[];
}

/**
 * `fileRef` is a file *reference/URL* string, not a binary upload - there is
 * no file storage backend wired up yet (see the KYC submission form's own
 * comment), so this only ever carries a reference the partner pasted in or
 * that a future upload flow will populate once storage exists.
 */
export interface SubmitKycDocumentRequest {
  docType: KycDocType;
  fileRef: string;
  docNumber?: string;
}

export interface ServiceArea {
  id: string;
  partnerId: string;
  cityId: string;
  zoneId: string | null;
  pincodeId: string | null;
  isActive: boolean;
}

export interface ServiceAreaInput {
  cityId: string;
  zoneId?: string;
  pincodeId?: string;
}

/** Full replace, per the API contract - the whole coverage set is sent every time. */
export interface UpdateServiceAreasRequest {
  areas: ServiceAreaInput[];
}

export interface PartnerSkill {
  id: string;
  partnerId: string;
  categoryId: string;
  serviceId: string | null;
  isActive: boolean;
}

export interface PartnerSkillInput {
  categoryId: string;
  serviceId?: string;
}

/** Full replace, per the API contract - the whole skill set is sent every time. */
export interface UpdateSkillsRequest {
  skills: PartnerSkillInput[];
}

export type { PartnerProfile };
