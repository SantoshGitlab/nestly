namespace Nestly.Domain;

/// <summary>
/// What triggered a <see cref="ProviderNotification"/> (Provider Management UX
/// pass: providers had no in-app notification feed at all - see
/// <c>ProviderNotificationPublisher</c>'s doc comment for the full gap this
/// closes). Deliberately a much smaller set than <see cref="NotificationEventType"/>:
/// that enum covers every customer-facing SMS/email/push trigger built up
/// over many tasks, where this one only covers what a provider needs to know
/// to run their day - append-only for the same wire-format reason
/// <see cref="NotificationEventType"/> documents (provider-api registers no
/// JsonStringEnumConverter, and the column is stored as this name via
/// HasConversion&lt;string&gt;() rather than the ordinal, but callers must
/// still never renumber).
/// </summary>
public enum ProviderNotificationType
{
    /// <summary>A booking was assigned/offered to this provider (admin or auto-assignment) - the single most time-sensitive event a provider can miss.</summary>
    JobOffered,

    /// <summary>An admin rejected a submitted KYC document.</summary>
    KycRejected,

    /// <summary>An admin suspended this provider's account.</summary>
    Suspended,

    /// <summary>A payout batch was marked Paid.</summary>
    PayoutProcessed,

    /// <summary>An admin replied to or resolved the provider's support ticket.</summary>
    SupportTicketReply,
}
