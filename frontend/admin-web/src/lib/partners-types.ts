/**
 * Admin partner management shapes (PARTNER.md; tasks 147, 148, 150a-c, 159,
 * 160) mirror the C# records in Nestly.Application.PartnerManagement
 * (PartnerManagementContracts.cs, BookingPartnerAssignmentContracts.cs,
 * PartnerFinancialContracts.cs) - see PartnersController/PayoutsController.
 * AdminApi has no JsonStringEnumConverter registered (see bookings-types.ts's
 * same caveat), so every enum below serialises over the wire as its ordinal
 * and must stay in declaration-order sync with its C# source.
 */

/** Mirrors Nestly.Domain.PartnerType's declaration order exactly. */
export enum PartnerType {
  Individual = 0,
  Company = 1,
}

/** Mirrors Nestly.Domain.PartnerStatus's declaration order exactly. */
export enum PartnerStatus {
  PendingVerification = 0,
  Active = 1,
  Suspended = 2,
  Deactivated = 3,
}

/** Mirrors Nestly.Domain.PartnerOnboardingStatus's declaration order exactly. */
export enum PartnerOnboardingStatus {
  Registered = 0,
  ProfileCompleted = 1,
  KycSubmitted = 2,
  KycVerified = 3,
  Completed = 4,
}

/** Mirrors Nestly.Domain.PartnerKycDocumentType's declaration order exactly. */
export enum PartnerKycDocumentType {
  IdentityProof = 0,
  AddressProof = 1,
  BankAccountProof = 2,
  ProfessionalCertificate = 3,
  Other = 4,
}

/** Mirrors Nestly.Domain.PartnerKycVerificationStatus's declaration order exactly. */
export enum PartnerKycVerificationStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
}

/** Mirrors Nestly.Domain.PartnerBackgroundCheckStatus's declaration order exactly. */
export enum PartnerBackgroundCheckStatus {
  Pending = 0,
  Passed = 1,
  Failed = 2,
}

/** Mirrors Nestly.Domain.BookingAssignedByType's declaration order exactly. */
export enum BookingAssignedByType {
  Admin = 0,
  System = 1,
}

/** Mirrors Nestly.Domain.BookingPartnerAssignmentStatus's declaration order exactly. */
export enum BookingPartnerAssignmentStatus {
  Assigned = 0,
  Accepted = 1,
  Rejected = 2,
  Reassigned = 3,
}

/** Mirrors Nestly.Domain.PartnerEarningEntryType's declaration order exactly. */
export enum PartnerEarningEntryType {
  Credit = 0,
  Debit = 1,
}

/** Mirrors Nestly.Domain.PartnerEarningSourceType's declaration order exactly. */
export enum PartnerEarningSourceType {
  JobCompletion = 0,
  Penalty = 1,
  ManualAdjustment = 2,
}

/** Mirrors Nestly.Domain.PartnerPayoutStatus's declaration order exactly. */
export enum PartnerPayoutStatus {
  Pending = 0,
  Processing = 1,
  Paid = 2,
  Failed = 3,
}

// ---- CRUD (task 150a) ----

export interface PartnerSummary {
  id: string;
  legalName: string;
  displayName: string;
  phone: string;
  email: string | null;
  status: PartnerStatus;
  onboardingStatus: PartnerOnboardingStatus;
  createdAt: string;
}

export interface PartnerSearchResponse {
  items: PartnerSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PartnerSearchParams {
  name?: string;
  phone?: string;
  status?: PartnerStatus;
  onboardingStatus?: PartnerOnboardingStatus;
  page?: number;
  pageSize?: number;
}

export interface CreatePartnerRequest {
  legalName: string;
  displayName: string;
  phone: string;
  email?: string;
}

export interface UpdatePartnerRequest {
  legalName: string;
  displayName: string;
  email?: string;
}

export interface SuspendPartnerRequest {
  reason: string;
}

export interface PartnerKycDocument {
  id: string;
  docType: PartnerKycDocumentType;
  docNumber: string | null;
  fileRef: string;
  verificationStatus: PartnerKycVerificationStatus;
  verifiedBy: string | null;
  verifiedAt: string | null;
  submittedAt: string;
}

export interface PartnerBackgroundCheck {
  id: string;
  status: PartnerBackgroundCheckStatus;
  checkedBy: string;
  checkedAt: string;
  notes: string | null;
}

export interface PartnerDetail {
  id: string;
  legalName: string;
  displayName: string;
  partnerType: PartnerType;
  phone: string;
  email: string | null;
  status: PartnerStatus;
  onboardingStatus: PartnerOnboardingStatus;
  createdAt: string;
  updatedAt: string;
  kycDocuments: PartnerKycDocument[];
  backgroundChecks: PartnerBackgroundCheck[];
}

// ---- KYC approval and background check / activation (task 150b, 160) ----

export interface RejectPartnerKycDocumentRequest {
  reason: string;
}

export interface RecordBackgroundCheckRequest {
  status: PartnerBackgroundCheckStatus;
  notes?: string;
}

// ---- Performance (task 150c) ----

export interface PartnerPerformance {
  partnerId: string;
  totalAssignments: number;
  acceptedAssignments: number;
  rejectedAssignments: number;
  completedJobs: number;
  inProgressJobs: number;
  lifetimeEarnings: number;
}

// ---- Earnings ledger and payouts (task 148) ----

export interface PartnerEarningLedgerEntry {
  id: string;
  partnerId: string;
  entryType: PartnerEarningEntryType;
  amount: number;
  balanceAfter: number;
  sourceType: PartnerEarningSourceType;
  sourceReferenceId: string | null;
  description: string;
  createdAtUtc: string;
}

export interface PartnerEarningsSummary {
  partnerId: string;
  currentBalance: number;
  entries: PartnerEarningLedgerEntry[];
}

export interface RecordPartnerEarningAdjustmentRequest {
  entryType: PartnerEarningEntryType;
  amount: number;
  sourceType: PartnerEarningSourceType;
  sourceReferenceId?: string;
  description: string;
}

export interface CreatePartnerPayoutRequest {
  periodStart: string;
  periodEnd: string;
}

export interface UpdatePartnerPayoutStatusRequest {
  status: PartnerPayoutStatus;
  payoutReference?: string;
  notes?: string;
}

export interface PartnerPayout {
  id: string;
  partnerId: string;
  partnerDisplayName: string;
  periodStart: string;
  periodEnd: string;
  totalAmount: number;
  status: PartnerPayoutStatus;
  payoutReference: string | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PartnerPayoutSearchResponse {
  items: PartnerPayout[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ---- Booking assignment (task 147, 159) ----

export interface AssignPartnerRequest {
  partnerId: string;
  responseDeadline?: string;
}

export interface RejectAssignmentRequest {
  reason?: string;
}

export interface BookingPartnerAssignment {
  id: string;
  bookingId: string;
  partnerId: string;
  partnerDisplayName: string;
  assignedByType: BookingAssignedByType;
  assignedByUserId: string | null;
  assignedAt: string;
  status: BookingPartnerAssignmentStatus;
  responseDeadline: string | null;
  respondedAt: string | null;
  notes: string | null;
}
