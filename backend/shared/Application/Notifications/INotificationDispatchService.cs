using Nestly.Domain;

namespace Nestly.Application.Notifications;

/// <summary>
/// Renders and sends a notification across every channel the recipient has
/// contact details for, logging one <c>NotificationEvent</c> per attempted
/// channel (SRS 19.1-2, tasks 87c-d). Internal orchestration capability, not
/// a controller-facing service - trigger wiring (task 88) is the caller.
/// </summary>
public interface INotificationDispatchService
{
    /// <summary>
    /// Never throws for an individual channel's send failure - each
    /// channel's outcome (including "no template" or a provider failure) is
    /// captured in its own <see cref="NotificationDispatchOutcome"/> rather
    /// than aborting the others.
    /// </summary>
    Task<IReadOnlyList<NotificationDispatchOutcome>> DispatchAsync(
        Guid customerId,
        NotificationEventType eventType,
        NotificationRecipient recipient,
        IReadOnlyDictionary<string, string> variables,
        Guid? bookingId = null,
        Guid? supportTicketId = null,
        CancellationToken cancellationToken = default);
}
