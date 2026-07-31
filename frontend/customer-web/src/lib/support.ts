/**
 * Display labels for the support ticket enums (types.ts), shared by
 * support/page.tsx, support/new/page.tsx and support/[id]/page.tsx so the
 * category/priority/status switch statements aren't triplicated.
 */
import {
  SupportTicketCategory,
  SupportTicketCommentAuthorType,
  SupportTicketPriority,
  SupportTicketStatus,
} from "./types";

export const SUPPORT_TICKET_CATEGORY_OPTIONS: { value: SupportTicketCategory; label: string }[] = [
  { value: SupportTicketCategory.BookingIssue, label: "Booking issue" },
  { value: SupportTicketCategory.PaymentIssue, label: "Payment issue" },
  { value: SupportTicketCategory.RefundIssue, label: "Refund issue" },
  { value: SupportTicketCategory.ServiceQuality, label: "Service quality" },
  { value: SupportTicketCategory.ProfessionalConduct, label: "Professional conduct" },
  { value: SupportTicketCategory.PricingDispute, label: "Pricing dispute" },
  { value: SupportTicketCategory.TechnicalIssue, label: "Technical issue" },
  { value: SupportTicketCategory.GeneralInquiry, label: "General inquiry" },
];

export const SUPPORT_TICKET_PRIORITY_OPTIONS: { value: SupportTicketPriority; label: string }[] = [
  { value: SupportTicketPriority.Low, label: "Low" },
  { value: SupportTicketPriority.Normal, label: "Normal" },
  { value: SupportTicketPriority.High, label: "High" },
  { value: SupportTicketPriority.Urgent, label: "Urgent" },
];

/** Categories the server requires a bookingId for (CreateSupportTicketRequestValidator's server-side rule, mirrored here for client-side UX). */
export const BOOKING_REQUIRED_CATEGORIES = new Set<SupportTicketCategory>([
  SupportTicketCategory.BookingIssue,
  SupportTicketCategory.PaymentIssue,
  SupportTicketCategory.RefundIssue,
  SupportTicketCategory.ServiceQuality,
  SupportTicketCategory.ProfessionalConduct,
  SupportTicketCategory.PricingDispute,
]);

export function categoryLabel(category: SupportTicketCategory): string {
  return SUPPORT_TICKET_CATEGORY_OPTIONS.find((o) => o.value === category)?.label ?? "Unknown";
}

export function priorityLabel(priority: SupportTicketPriority): string {
  return SUPPORT_TICKET_PRIORITY_OPTIONS.find((o) => o.value === priority)?.label ?? "Unknown";
}

export function statusLabel(status: SupportTicketStatus): string {
  switch (status) {
    case SupportTicketStatus.Open:
      return "Open";
    case SupportTicketStatus.InProgress:
      return "In progress";
    case SupportTicketStatus.WaitingForCustomer:
      return "Waiting for you";
    case SupportTicketStatus.Escalated:
      return "Escalated";
    case SupportTicketStatus.Resolved:
      return "Resolved";
    case SupportTicketStatus.Closed:
      return "Closed";
    default:
      return "Unknown";
  }
}

export function authorTypeLabel(authorType: SupportTicketCommentAuthorType): string {
  switch (authorType) {
    case SupportTicketCommentAuthorType.Customer:
      return "You";
    case SupportTicketCommentAuthorType.Support:
      return "Support";
    case SupportTicketCommentAuthorType.System:
      return "System";
    default:
      return "Unknown";
  }
}
