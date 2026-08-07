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

    /// <summary>
    /// Looks up the contact details and active push devices for one principal
    /// (task 277). Before this, every caller assembled the recipient itself
    /// from <c>ICustomerRepository</c> plus
    /// <c>IDeviceTokenRepository.ListActiveByCustomerAsync</c>, which is why
    /// there was no way to address a <b>provider</b>: no caller had the code
    /// for it and device tokens could not belong to one.
    ///
    /// <para>
    /// Never throws for a missing principal - an unknown id resolves to a
    /// recipient with no channels at all, which <c>DispatchAsync</c> then
    /// turns into zero outcomes. That matches how the trigger handlers already
    /// treat a deleted row (log and carry on) rather than failing a
    /// post-commit notification.
    /// </para>
    /// </summary>
    Task<NotificationRecipient> ResolveRecipientAsync(DeviceTokenOwner owner, CancellationToken cancellationToken = default);
}
