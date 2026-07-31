/**
 * Response/request shapes for the Admin API's notification template surface
 * (SRS 12.17, tasks 126a-d): `NotificationTemplatesController`. Mirrors the
 * backend contracts in `Application/Notifications/NotificationTemplateContracts.cs`
 * field for field.
 *
 * AdminApi has no JsonStringEnumConverter registered (see lib/coupon-types.ts's
 * doc comment), so every enum below serialises over the wire as its ordinal
 * and must stay in declaration-order sync with its C# source.
 */

/** Mirrors Nestly.Domain.NotificationEventType's declaration order exactly. */
export enum NotificationEventType {
  Welcome = 0,
  BookingConfirmed = 1,
  PaymentSuccess = 2,
  PaymentFailed = 3,
  BookingCancelled = 4,
  BookingRescheduled = 5,
  RefundProcessed = 6,
  SupportTicketUpdate = 7,
}

/** Mirrors Nestly.Domain.NotificationChannel's declaration order exactly. */
export enum NotificationChannel {
  Sms = 0,
  Email = 1,
  Push = 2,
}

export const NOTIFICATION_EVENT_TYPE_LABELS: Record<NotificationEventType, string> = {
  [NotificationEventType.Welcome]: "Welcome",
  [NotificationEventType.BookingConfirmed]: "Booking confirmed",
  [NotificationEventType.PaymentSuccess]: "Payment success",
  [NotificationEventType.PaymentFailed]: "Payment failed",
  [NotificationEventType.BookingCancelled]: "Booking cancelled",
  [NotificationEventType.BookingRescheduled]: "Booking rescheduled",
  [NotificationEventType.RefundProcessed]: "Refund processed",
  [NotificationEventType.SupportTicketUpdate]: "Support ticket update",
};

export const NOTIFICATION_CHANNEL_LABELS: Record<NotificationChannel, string> = {
  [NotificationChannel.Sms]: "SMS",
  [NotificationChannel.Email]: "Email",
  [NotificationChannel.Push]: "Push",
};

/** Full template detail for the admin list/edit screens (SRS 12.17.2's field set). */
export interface NotificationTemplateResponse {
  id: string;
  eventType: NotificationEventType;
  channel: NotificationChannel;
  templateKey: string;
  subject: string | null;
  body: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  updatedByAdminUserId: string | null;
}

/** Query parameters for the template list endpoint. All optional. */
export interface NotificationTemplateListParams {
  channel?: NotificationChannel;
  eventType?: NotificationEventType;
  isActive?: boolean;
}

/** Create request for a not-yet-covered (EventType, Channel) combination - rejected with a conflict if one already exists. */
export interface NotificationTemplateCreateRequest {
  eventType: NotificationEventType;
  channel: NotificationChannel;
  templateKey: string;
  subject: string | null;
  body: string;
}

/** Edit request for an existing template's content - eventType/channel/templateKey are immutable once created. */
export interface NotificationTemplateUpdateRequest {
  subject: string | null;
  body: string;
}

/** Renders a saved template's subject/body against sample values (SRS 12.17.2 "Preview/test capability") - a pure render, nothing is sent or persisted. */
export interface NotificationTemplatePreviewRequest {
  sampleVariables: Record<string, string>;
}

/** Ad-hoc preview of draft (not-yet-saved) subject/body text, for the editor's live preview. */
export interface NotificationTemplateAdHocPreviewRequest {
  channel: NotificationChannel;
  subject: string | null;
  body: string;
  sampleVariables: Record<string, string>;
}

export interface NotificationTemplatePreviewResponse {
  subject: string | null;
  body: string;
}
