/**
 * Response/request shapes for the Partner API's jobs surface
 * (`/api/v1/jobs`, docs/PARTNER.md's `booking_partner_assignment` bridge
 * table).
 *
 * NOTE ON PROVENANCE: as of this writing, the backend behind this surface is
 * a stub returning HTTP 501 - the underlying `BookingPartnerAssignment`
 * entity is sibling task #147, not yet merged. These types describe the
 * contract this client is built against (per the task brief); the status
 * union and detail fields are held loosely (`[key: string]: unknown`) so
 * this client does not need a follow-up rewrite the moment #147 fills in
 * fields this brief did not fully pin down - only lib/jobs-api.ts's response
 * typing and this file should need reconciling if the real shape differs.
 */

export type JobStatus =
  | "Assigned"
  | "Accepted"
  | "Rejected"
  | "Reassigned"
  | "InProgress"
  | "Completed";

export interface JobListItem {
  id: string;
  bookingId: string;
  status: JobStatus;
  assignedAt: string;
  responseDeadline: string | null;
  [key: string]: unknown;
}

/** Query parameters for GET /jobs. Both optional. */
export interface JobListParams {
  status?: string;
  date?: string;
}

/**
 * Detail view of a single assignment. Fields beyond the ones every list item
 * already carries (customer/service/address context an assignment would
 * realistically expose) are optional and read defensively - the stub backend
 * may omit them entirely until #147 lands.
 */
export interface JobDetail extends JobListItem {
  customerName?: string;
  serviceName?: string;
  addressLine?: string;
  slotDate?: string;
  completionProofRef?: string | null;
}

export interface SubmitCompletionProofRequest {
  fileRef: string;
}
