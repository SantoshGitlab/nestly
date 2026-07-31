using System.Text.RegularExpressions;
using Nestly.Application.Notifications;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Fixed, built-in notification template set (SRS 12.17, task 87b) standing
/// in for the admin-managed template CRUD that's Phase 6's job. SMS
/// templates are short/plain; email templates carry a subject.
/// <c>{{Variable}}</c> placeholders are substituted from the caller-supplied
/// dictionary - an unresolved placeholder is left in the output verbatim
/// rather than throwing, since a missing optional variable (e.g. no
/// <c>{{Reason}}</c> supplied) shouldn't block the whole notification.
/// </summary>
public sealed partial class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    private sealed record TemplateDefinition(string Key, string? Subject, string Body);

    private static readonly Dictionary<(NotificationEventType EventType, NotificationChannel Channel), TemplateDefinition> Templates = new()
    {
        [(NotificationEventType.Welcome, NotificationChannel.Sms)] =
            new("welcome_sms", null, "Welcome to Nestly, {{CustomerName}}! Book trusted home services in a few taps."),
        [(NotificationEventType.Welcome, NotificationChannel.Email)] =
            new("welcome_email", "Welcome to Nestly", "Hi {{CustomerName}},\n\nWelcome to Nestly! Your account is ready - browse services and book your first appointment whenever you're ready."),

        [(NotificationEventType.BookingConfirmed, NotificationChannel.Sms)] =
            new("booking_confirmed_sms", null, "Booking confirmed! Your {{ServiceName}} is scheduled for {{SlotDate}}, {{SlotWindow}}. - Nestly"),
        [(NotificationEventType.BookingConfirmed, NotificationChannel.Email)] =
            new("booking_confirmed_email", "Your Nestly booking is confirmed", "Hi {{CustomerName}},\n\nYour booking for {{ServiceName}} on {{SlotDate}} ({{SlotWindow}}) is confirmed. Total payable: {{TotalPayable}}."),

        [(NotificationEventType.PaymentSuccess, NotificationChannel.Sms)] =
            new("payment_success_sms", null, "Payment of {{Amount}} received for booking {{BookingId}}. Thank you! - Nestly"),
        [(NotificationEventType.PaymentSuccess, NotificationChannel.Email)] =
            new("payment_success_email", "Payment received", "Hi {{CustomerName}},\n\nWe've received your payment of {{Amount}} for booking {{BookingId}}."),

        [(NotificationEventType.PaymentFailed, NotificationChannel.Sms)] =
            new("payment_failed_sms", null, "Payment failed for booking {{BookingId}}. Please retry from the app. - Nestly"),
        [(NotificationEventType.PaymentFailed, NotificationChannel.Email)] =
            new("payment_failed_email", "Payment failed", "Hi {{CustomerName}},\n\nYour payment of {{Amount}} for booking {{BookingId}} could not be completed. Please retry from the app."),

        [(NotificationEventType.BookingCancelled, NotificationChannel.Sms)] =
            new("booking_cancelled_sms", null, "Booking {{BookingId}} has been cancelled. Refund: {{RefundAmount}}. - Nestly"),
        [(NotificationEventType.BookingCancelled, NotificationChannel.Email)] =
            new("booking_cancelled_email", "Your booking was cancelled", "Hi {{CustomerName}},\n\nYour booking {{BookingId}} has been cancelled. Cancellation fee: {{CancellationFee}}. Refund amount: {{RefundAmount}}."),

        [(NotificationEventType.BookingRescheduled, NotificationChannel.Sms)] =
            new("booking_rescheduled_sms", null, "Booking {{BookingId}} rescheduled to {{SlotDate}}, {{SlotWindow}}. - Nestly"),
        [(NotificationEventType.BookingRescheduled, NotificationChannel.Email)] =
            new("booking_rescheduled_email", "Your booking was rescheduled", "Hi {{CustomerName}},\n\nYour booking {{BookingId}} has been rescheduled to {{SlotDate}} ({{SlotWindow}})."),

        [(NotificationEventType.RefundProcessed, NotificationChannel.Sms)] =
            new("refund_processed_sms", null, "Refund of {{Amount}} for booking {{BookingId}} has been processed via {{Method}}. - Nestly"),
        [(NotificationEventType.RefundProcessed, NotificationChannel.Email)] =
            new("refund_processed_email", "Your refund has been processed", "Hi {{CustomerName}},\n\nA refund of {{Amount}} for booking {{BookingId}} has been processed via {{Method}}."),

        [(NotificationEventType.SupportTicketUpdate, NotificationChannel.Sms)] =
            new("support_ticket_update_sms", null, "Update on ticket {{TicketId}}: {{Status}}. - Nestly"),
        [(NotificationEventType.SupportTicketUpdate, NotificationChannel.Email)] =
            new("support_ticket_update_email", "Your support ticket was updated", "Hi {{CustomerName}},\n\nYour support ticket \"{{Subject}}\" is now {{Status}}."),
    };

    public RenderedNotification Render(NotificationEventType eventType, NotificationChannel channel, IReadOnlyDictionary<string, string> variables)
    {
        if (!Templates.TryGetValue((eventType, channel), out var definition))
        {
            throw new InvalidOperationException($"No notification template registered for {eventType}/{channel}.");
        }

        return new RenderedNotification(definition.Key, Substitute(definition.Subject, variables), Substitute(definition.Body, variables)!);
    }

    public bool SupportsChannel(NotificationEventType eventType, NotificationChannel channel) =>
        Templates.ContainsKey((eventType, channel));

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern();

    private static string? Substitute(string? template, IReadOnlyDictionary<string, string> variables)
    {
        if (template is null)
        {
            return null;
        }

        return PlaceholderPattern().Replace(template, match =>
            variables.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
    }
}
