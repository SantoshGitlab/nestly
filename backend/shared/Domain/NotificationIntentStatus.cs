namespace Nestly.Domain;

/// <summary>
/// Lifecycle of a <see cref="NotificationIntent"/> (task 294). Persisted as
/// its <i>name</i> (<c>HasConversion&lt;string&gt;</c>, the same convention
/// <see cref="NotificationDeliveryStatus"/> follows), so unlike the enums that
/// cross the wire as ordinals this one is safe to reorder - but there is no
/// reason to, so append.
/// </summary>
/// <remarks>
/// There is deliberately no "InFlight" member. Whether a row is being worked
/// on right now is expressed by <see cref="NotificationIntent.LeaseExpiresAtUtc"/>,
/// which expires on its own; a status would not, and a worker that died
/// holding one would strand the notification forever - the exact failure this
/// whole mechanism exists to prevent.
/// </remarks>
public enum NotificationIntentStatus
{
    /// <summary>Owed, not yet sent. The only state the sweep considers.</summary>
    Pending,

    /// <summary>Handed to <c>INotificationDispatchService</c>, which logged its own per-channel outcomes. Terminal.</summary>
    Delivered,

    /// <summary>Deliberately not sent - muted, superseded, or its subject no longer exists. Terminal, and not a failure.</summary>
    Skipped,

    /// <summary>Retried up to the bound and never succeeded. Terminal, and the one state that means a customer was owed something and will not get it - alert on it.</summary>
    Abandoned
}
