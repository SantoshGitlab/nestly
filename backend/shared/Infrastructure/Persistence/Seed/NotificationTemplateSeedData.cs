using System.Security.Cryptography;
using System.Text;
using Nestly.Domain;

namespace Nestly.Infrastructure.Persistence.Seed;

/// <summary>
/// The fixed built-in template set <c>NotificationTemplateRenderer</c> used to
/// hard-code before Phase 6 (SRS 12.17, tasks 126a-d), now the single source
/// of truth for two consumers that must never drift apart: the
/// <c>AddNotificationTemplateManagement</c> migration's seed rows, and
/// <c>NotificationTemplateRendererTests</c> (task 87b's rendering tests,
/// updated to read from the database instead of a static dictionary). Ids and
/// the timestamp are deterministic, same reasoning as
/// <c>AddSystemSettings.DeterministicId</c>/<c>SeedTimestamp</c> - a fresh
/// database gets byte-for-byte identical seed rows every time the migration
/// runs.
/// </summary>
public static class NotificationTemplateSeedData
{
    public static readonly DateTime SeedTimestampUtc = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

    public sealed record SeedRow(
        Guid Id,
        NotificationEventType EventType,
        NotificationChannel Channel,
        string TemplateKey,
        string? Subject,
        string Body);

    public static Guid DeterministicId(string seed)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash);
    }

    /// <summary>Every (EventType, Channel) combination task 87a-d's trigger wiring depends on - 8 event types x 3 channels.</summary>
    public static IReadOnlyList<SeedRow> BuildDefaults() =>
    [
        Row(NotificationEventType.Welcome, NotificationChannel.Sms, "welcome_sms", null,
            "Welcome to Nestly, {{CustomerName}}! Book trusted home services in a few taps."),
        Row(NotificationEventType.Welcome, NotificationChannel.Email, "welcome_email", "Welcome to Nestly",
            "Hi {{CustomerName}},\n\nWelcome to Nestly! Your account is ready - browse services and book your first appointment whenever you're ready."),
        Row(NotificationEventType.Welcome, NotificationChannel.Push, "welcome_push", "Welcome to Nestly",
            "Hi {{CustomerName}}, your account is ready. Book your first service today!"),

        Row(NotificationEventType.BookingConfirmed, NotificationChannel.Sms, "booking_confirmed_sms", null,
            "Booking confirmed! Your {{ServiceName}} is scheduled for {{SlotDate}}, {{SlotWindow}}. - Nestly"),
        Row(NotificationEventType.BookingConfirmed, NotificationChannel.Email, "booking_confirmed_email", "Your Nestly booking is confirmed",
            "Hi {{CustomerName}},\n\nYour booking for {{ServiceName}} on {{SlotDate}} ({{SlotWindow}}) is confirmed. Total payable: {{TotalPayable}}."),
        Row(NotificationEventType.BookingConfirmed, NotificationChannel.Push, "booking_confirmed_push", "Booking confirmed",
            "{{ServiceName}} is scheduled for {{SlotDate}}, {{SlotWindow}}."),

        Row(NotificationEventType.PaymentSuccess, NotificationChannel.Sms, "payment_success_sms", null,
            "Payment of {{Amount}} received for booking {{BookingId}}. Thank you! - Nestly"),
        Row(NotificationEventType.PaymentSuccess, NotificationChannel.Email, "payment_success_email", "Payment received",
            "Hi {{CustomerName}},\n\nWe've received your payment of {{Amount}} for booking {{BookingId}}."),
        Row(NotificationEventType.PaymentSuccess, NotificationChannel.Push, "payment_success_push", "Payment received",
            "We've received your payment of {{Amount}} for booking {{BookingId}}."),

        Row(NotificationEventType.PaymentFailed, NotificationChannel.Sms, "payment_failed_sms", null,
            "Payment failed for booking {{BookingId}}. Please retry from the app. - Nestly"),
        Row(NotificationEventType.PaymentFailed, NotificationChannel.Email, "payment_failed_email", "Payment failed",
            "Hi {{CustomerName}},\n\nYour payment of {{Amount}} for booking {{BookingId}} could not be completed. Please retry from the app."),
        Row(NotificationEventType.PaymentFailed, NotificationChannel.Push, "payment_failed_push", "Payment failed",
            "Your payment of {{Amount}} for booking {{BookingId}} failed. Please retry."),

        Row(NotificationEventType.BookingCancelled, NotificationChannel.Sms, "booking_cancelled_sms", null,
            "Booking {{BookingId}} has been cancelled. Refund: {{RefundAmount}}. - Nestly"),
        Row(NotificationEventType.BookingCancelled, NotificationChannel.Email, "booking_cancelled_email", "Your booking was cancelled",
            "Hi {{CustomerName}},\n\nYour booking {{BookingId}} has been cancelled. Cancellation fee: {{CancellationFee}}. Refund amount: {{RefundAmount}}."),
        Row(NotificationEventType.BookingCancelled, NotificationChannel.Push, "booking_cancelled_push", "Booking cancelled",
            "Booking {{BookingId}} was cancelled. Refund: {{RefundAmount}}."),

        Row(NotificationEventType.BookingRescheduled, NotificationChannel.Sms, "booking_rescheduled_sms", null,
            "Booking {{BookingId}} rescheduled to {{SlotDate}}, {{SlotWindow}}. - Nestly"),
        Row(NotificationEventType.BookingRescheduled, NotificationChannel.Email, "booking_rescheduled_email", "Your booking was rescheduled",
            "Hi {{CustomerName}},\n\nYour booking {{BookingId}} has been rescheduled to {{SlotDate}} ({{SlotWindow}})."),
        Row(NotificationEventType.BookingRescheduled, NotificationChannel.Push, "booking_rescheduled_push", "Booking rescheduled",
            "Booking {{BookingId}} moved to {{SlotDate}}, {{SlotWindow}}."),

        Row(NotificationEventType.RefundProcessed, NotificationChannel.Sms, "refund_processed_sms", null,
            "Refund of {{Amount}} for booking {{BookingId}} has been processed via {{Method}}. - Nestly"),
        Row(NotificationEventType.RefundProcessed, NotificationChannel.Email, "refund_processed_email", "Your refund has been processed",
            "Hi {{CustomerName}},\n\nA refund of {{Amount}} for booking {{BookingId}} has been processed via {{Method}}."),
        Row(NotificationEventType.RefundProcessed, NotificationChannel.Push, "refund_processed_push", "Refund processed",
            "A refund of {{Amount}} for booking {{BookingId}} was processed via {{Method}}."),

        Row(NotificationEventType.SupportTicketUpdate, NotificationChannel.Sms, "support_ticket_update_sms", null,
            "Update on ticket {{TicketId}}: {{Status}}. - Nestly"),
        Row(NotificationEventType.SupportTicketUpdate, NotificationChannel.Email, "support_ticket_update_email", "Your support ticket was updated",
            "Hi {{CustomerName}},\n\nYour support ticket \"{{Subject}}\" is now {{Status}}."),
        Row(NotificationEventType.SupportTicketUpdate, NotificationChannel.Push, "support_ticket_update_push", "Ticket updated",
            "Your ticket \"{{Subject}}\" is now {{Status}}."),

        Row(NotificationEventType.RecurringBookingUpcoming, NotificationChannel.Sms, "recurring_booking_upcoming_sms", null,
            "Your recurring {{ServiceName}} booking for {{SlotDate}}, {{SlotWindow}} is confirmed. - Nestly"),
        Row(NotificationEventType.RecurringBookingUpcoming, NotificationChannel.Email, "recurring_booking_upcoming_email", "Your upcoming recurring booking",
            "Hi {{CustomerName}},\n\nYour recurring {{ServiceName}} plan has booked its next visit for {{SlotDate}} ({{SlotWindow}})."),
        Row(NotificationEventType.RecurringBookingUpcoming, NotificationChannel.Push, "recurring_booking_upcoming_push", "Upcoming recurring booking",
            "Your recurring {{ServiceName}} visit is booked for {{SlotDate}}, {{SlotWindow}}."),

        Row(NotificationEventType.RecurringBookingSkipped, NotificationChannel.Sms, "recurring_booking_slot_unavailable_sms", null,
            "We couldn't book your recurring {{ServiceName}} visit for {{SlotDate}} - the slot is no longer available. Open the app to reschedule this occurrence. - Nestly"),
        Row(NotificationEventType.RecurringBookingSkipped, NotificationChannel.Email, "recurring_booking_slot_unavailable_email", "We couldn't book your upcoming recurring visit",
            "Hi {{CustomerName}},\n\nYour recurring {{ServiceName}} plan's visit scheduled for {{SlotDate}} could not be booked - the slot is no longer available. This occurrence has been skipped; your plan will continue with its next scheduled date. Open the app if you'd like to book this date manually."),
        Row(NotificationEventType.RecurringBookingSkipped, NotificationChannel.Push, "recurring_booking_slot_unavailable_push", "Recurring visit could not be booked",
            "Your {{ServiceName}} visit for {{SlotDate}} couldn't be booked - the slot's no longer available.")
    ];

    private static SeedRow Row(NotificationEventType eventType, NotificationChannel channel, string templateKey, string? subject, string body) =>
        new(DeterministicId($"notification_template:{eventType}:{channel}"), eventType, channel, templateKey, subject, body);
}
