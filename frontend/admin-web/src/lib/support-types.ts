/**
 * Response/request shapes for the Admin API's ticket workflow surface (SRS
 * 12.14, 16.2, tasks 120a-f): `SupportTicketsController`. Mirrors the backend
 * contracts in `Application/Support/AdminSupportTicketContracts.cs` field for
 * field. Reuses `SupportTicketCategory`/`SupportTicketPriority`/
 * `SupportTicketStatus`/`BookingStatus` from lib/types.ts rather than
 * redeclaring them - same enums, same ordinal-serialisation caveat (no
 * JsonStringEnumConverter on AdminApi).
 */
import type { BookingStatus, SupportTicketCategory, SupportTicketPriority, SupportTicketStatus } from "./types";

/** Mirrors Nestly.Domain.SupportTicketCommentAuthorType's declaration order exactly. */
export enum SupportTicketCommentAuthorType {
  Customer = 0,
  Support = 1,
  System = 2,
}

export interface AdminSupportTicketCommentResponse {
  id: string;
  authorType: SupportTicketCommentAuthorType;
  comment: string;
  createdAt: string;
}

/** Mirrors Nestly.Domain.DisputeResolutionOutcome's declaration order exactly. */
export enum DisputeResolutionOutcome {
  RefundValid = 0,
  ClosedInvalid = 1,
}

export interface AdminSupportTicketSummaryResponse {
  id: string;
  customerId: string;
  customerName: string;
  bookingId: string | null;
  category: SupportTicketCategory;
  priority: SupportTicketPriority;
  subject: string;
  status: SupportTicketStatus;
  isDisputed: boolean;
  assignedAdminUserId: string | null;
  assignedAdminName: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AdminSupportTicketSearchResponse {
  items: AdminSupportTicketSummaryResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** The linked booking's read-only summary (task 120e) - see SupportTicketsController's doc comment for why there's no cancel/refund action here yet. */
export interface LinkedBookingSummaryResponse {
  id: string;
  status: BookingStatus;
  customerNameSnapshot: string;
  slotDate: string;
  slotStartTimeSnapshot: string;
  slotEndTimeSnapshot: string;
  totalPayableSnapshot: number;
}

export interface AdminSupportTicketDetailResponse {
  id: string;
  customerId: string;
  customerName: string;
  bookingId: string | null;
  booking: LinkedBookingSummaryResponse | null;
  category: SupportTicketCategory;
  priority: SupportTicketPriority;
  subject: string;
  description: string;
  status: SupportTicketStatus;
  resolutionSummary: string | null;
  isDisputed: boolean;
  disputeOutcome: DisputeResolutionOutcome | null;
  assignedAdminUserId: string | null;
  assignedAdminName: string | null;
  assignedAtUtc: string | null;
  escalatedAtUtc: string | null;
  comments: AdminSupportTicketCommentResponse[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AssignableAdminResponse {
  id: string;
  fullName: string;
  email: string;
}

export interface AssignSupportTicketRequestBody {
  adminUserId: string;
}

export interface AddSupportTicketCommentRequestBody {
  comment: string;
}

export interface ResolveSupportTicketRequestBody {
  resolutionSummary: string;
}

export interface LinkSupportTicketBookingRequestBody {
  bookingId: string;
}
