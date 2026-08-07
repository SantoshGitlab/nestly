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

    /// <summary>
    /// Every (EventType, Channel) combination the trigger wiring depends on -
    /// one row per event type per channel, three channels throughout. Grown
    /// by task 87a-d's original 8, then referral/recurring/subscription/
    /// expiry, then task 276's five fulfilment events. Each growth spurt also
    /// needs an incremental seed migration for live databases (see
    /// <c>SeedFulfilmentNotificationTemplates</c>) - this list alone only
    /// feeds tests and a freshly migrated database.
    /// </summary>
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
            "Your {{ServiceName}} visit for {{SlotDate}} couldn't be booked - the slot's no longer available."),

        Row(NotificationEventType.ReferralRegistered, NotificationChannel.Sms, "referral_registered_sms", null,
            "{{RefereeName}} just signed up with your referral code! You'll be rewarded once they complete a qualifying booking. - Nestly"),
        Row(NotificationEventType.ReferralRegistered, NotificationChannel.Email, "referral_registered_email", "Your referral just signed up",
            "Hi there,\n\n{{RefereeName}} just registered using your referral code. You'll receive your reward once they complete a qualifying booking."),
        Row(NotificationEventType.ReferralRegistered, NotificationChannel.Push, "referral_registered_push", "Referral signed up",
            "{{RefereeName}} just signed up with your referral code!"),

        Row(NotificationEventType.ReferralRewardCredited, NotificationChannel.Sms, "referral_reward_credited_sms", null,
            "Your referral reward of {{RewardValue}} has landed! Check your wallet or coupons. - Nestly"),
        Row(NotificationEventType.ReferralRewardCredited, NotificationChannel.Email, "referral_reward_credited_email", "Your referral reward has landed",
            "Hi there,\n\nYour referral reward of {{RewardValue}} has been credited. Thanks for spreading the word about Nestly!"),
        Row(NotificationEventType.ReferralRewardCredited, NotificationChannel.Push, "referral_reward_credited_push", "Referral reward credited",
            "Your referral reward of {{RewardValue}} has landed!"),

        Row(NotificationEventType.SubscriptionRenewed, NotificationChannel.Sms, "subscription_renewed_sms", null,
            "Your Nestly subscription has renewed for another period. Thanks for staying with us! - Nestly"),
        Row(NotificationEventType.SubscriptionRenewed, NotificationChannel.Email, "subscription_renewed_email", "Your subscription has renewed",
            "Hi there,\n\nYour Nestly subscription has renewed and your benefits are active for the new period."),
        Row(NotificationEventType.SubscriptionRenewed, NotificationChannel.Push, "subscription_renewed_push", "Subscription renewed",
            "Your Nestly subscription has renewed for another period."),

        Row(NotificationEventType.SubscriptionExpiringSoon, NotificationChannel.Sms, "subscription_expiring_soon_sms", null,
            "Your Nestly subscription will renew soon. Update your payment method if it's changed. - Nestly"),
        Row(NotificationEventType.SubscriptionExpiringSoon, NotificationChannel.Email, "subscription_expiring_soon_email", "Your subscription renews soon",
            "Hi there,\n\nYour Nestly subscription is due for its next charge soon. Make sure your payment details are up to date."),
        Row(NotificationEventType.SubscriptionExpiringSoon, NotificationChannel.Push, "subscription_expiring_soon_push", "Subscription renewing soon",
            "Your Nestly subscription is due for renewal soon."),

        Row(NotificationEventType.SubscriptionPaymentFailed, NotificationChannel.Sms, "subscription_payment_failed_sms", null,
            "We couldn't process your Nestly subscription payment. Please update your payment method. - Nestly"),
        Row(NotificationEventType.SubscriptionPaymentFailed, NotificationChannel.Email, "subscription_payment_failed_email", "Your subscription payment failed",
            "Hi there,\n\nWe couldn't process your Nestly subscription payment. We'll retry automatically, but please check your payment method to avoid your subscription lapsing."),
        Row(NotificationEventType.SubscriptionPaymentFailed, NotificationChannel.Push, "subscription_payment_failed_push", "Subscription payment failed",
            "We couldn't process your subscription payment - please check your payment method."),

        Row(NotificationEventType.BookingExpired, NotificationChannel.Sms, "booking_expired_sms", null,
            "Booking {{BookingId}} was cancelled - payment wasn't completed in time. Book again anytime. - Nestly"),
        Row(NotificationEventType.BookingExpired, NotificationChannel.Email, "booking_expired_email", "Your booking wasn't completed",
            "Hi {{CustomerName}},\n\nYour booking {{BookingId}} for {{ServiceName}} on {{SlotDate}} ({{SlotWindow}}) was cancelled because payment wasn't completed in time. No charge was made. Feel free to book again whenever you're ready."),
        Row(NotificationEventType.BookingExpired, NotificationChannel.Push, "booking_expired_push", "Booking not completed",
            "Booking {{BookingId}} was cancelled - payment wasn't completed in time."),

        // Task 276: the fulfilment half of the lifecycle. {{ProviderName}} is
        // the assigned provider's display name - the only new variable these
        // bodies use. BookingNotificationTriggerHandler also supplies
        // {{ProviderMobile}}, already masked through ContactMasking, which no
        // default body references: templates are admin-editable at runtime
        // (NotificationTemplatesController), so the variable an ops person
        // might reach for has to be safe *before* they reach for it. A raw
        // provider number must never become available to a template.
        Row(NotificationEventType.ProviderAssigned, NotificationChannel.Sms, "provider_assigned_sms", null,
            "{{ProviderName}} has been assigned to your {{ServiceName}} on {{SlotDate}}, {{SlotWindow}}. - Nestly"),
        Row(NotificationEventType.ProviderAssigned, NotificationChannel.Email, "provider_assigned_email", "A professional has been assigned to your booking",
            "Hi {{CustomerName}},\n\n{{ProviderName}} has been assigned to your {{ServiceName}} booking on {{SlotDate}} ({{SlotWindow}}). You can follow their progress in the app once they set off."),
        Row(NotificationEventType.ProviderAssigned, NotificationChannel.Push, "provider_assigned_push", "Professional assigned",
            "{{ProviderName}} will handle your {{ServiceName}} on {{SlotDate}}, {{SlotWindow}}."),

        Row(NotificationEventType.ProviderEnRoute, NotificationChannel.Sms, "provider_en_route_sms", null,
            "{{ProviderName}} is on the way for your {{ServiceName}}. Track them live in the app. - Nestly"),
        Row(NotificationEventType.ProviderEnRoute, NotificationChannel.Email, "provider_en_route_email", "Your professional is on the way",
            "Hi {{CustomerName}},\n\n{{ProviderName}} is on the way for your {{ServiceName}} booking {{BookingId}}. Open the app to follow their arrival."),
        Row(NotificationEventType.ProviderEnRoute, NotificationChannel.Push, "provider_en_route_push", "On the way",
            "{{ProviderName}} is on the way for your {{ServiceName}}."),

        Row(NotificationEventType.ProviderArrived, NotificationChannel.Sms, "provider_arrived_sms", null,
            "{{ProviderName}} has arrived for your {{ServiceName}}. - Nestly"),
        Row(NotificationEventType.ProviderArrived, NotificationChannel.Email, "provider_arrived_email", "Your professional has arrived",
            "Hi {{CustomerName}},\n\n{{ProviderName}} has arrived at your address for booking {{BookingId}}."),
        Row(NotificationEventType.ProviderArrived, NotificationChannel.Push, "provider_arrived_push", "Arrived",
            "{{ProviderName}} has arrived for your {{ServiceName}}."),

        Row(NotificationEventType.JobStarted, NotificationChannel.Sms, "job_started_sms", null,
            "{{ProviderName}} has started your {{ServiceName}}. - Nestly"),
        Row(NotificationEventType.JobStarted, NotificationChannel.Email, "job_started_email", "Your service has started",
            "Hi {{CustomerName}},\n\n{{ProviderName}} has started work on your {{ServiceName}} booking {{BookingId}}."),
        Row(NotificationEventType.JobStarted, NotificationChannel.Push, "job_started_push", "Service started",
            "{{ProviderName}} has started your {{ServiceName}}."),

        Row(NotificationEventType.JobCompleted, NotificationChannel.Sms, "job_completed_sms", null,
            "Your {{ServiceName}} is complete. Rate your experience in the app. - Nestly"),
        Row(NotificationEventType.JobCompleted, NotificationChannel.Email, "job_completed_email", "Your service is complete",
            "Hi {{CustomerName}},\n\nYour {{ServiceName}} booking {{BookingId}} has been completed by {{ProviderName}}. We'd love to hear how it went - you can leave a rating in the app."),
        Row(NotificationEventType.JobCompleted, NotificationChannel.Push, "job_completed_push", "Service complete",
            "Your {{ServiceName}} is complete. Tap to rate your experience.")
    ];

    private static SeedRow Row(NotificationEventType eventType, NotificationChannel channel, string templateKey, string? subject, string body) =>
        new(DeterministicId($"notification_template:{eventType}:{channel}"), eventType, channel, templateKey, subject, body);
}
