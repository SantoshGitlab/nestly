using Nestly.Domain;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Strongly typed binding of the "FulfilmentNotifications" configuration
/// section (task 276) - one on/off switch per fulfilment-lifecycle
/// notification, so ops can silence a chatty one without a code change. Not a
/// secret, and every value defaults to enabled, same
/// not-yet-adminable-policy-knob reasoning as <see cref="BookingEtaOptions"/>
/// and <see cref="ProviderLocationIngestOptions"/>. No section exists in any
/// appsettings.json for the same reason those two have none: the defaults are
/// the intended production behaviour, and an operator adds the section only
/// when they want to deviate.
/// </summary>
/// <remarks>
/// <b>Five named booleans, not a Dictionary&lt;NotificationEventType, bool&gt;.</b>
/// A dictionary would let a config edit silence <c>PaymentFailed</c> or
/// <c>BookingCancelled</c> too, and those are the notifications a customer is
/// most harmed by never receiving. The mute knob exists for the two chatty
/// tracking pings; it is deliberately not a general "turn off notifications"
/// switch. Anything outside this set is unconditionally enabled - see
/// <see cref="IsEnabled"/>.
///
/// <para>
/// Consumed through <c>IOptionsMonitor</c> rather than <c>IOptions</c> - the
/// only place in this codebase that does. Muting is an incident-response
/// action ("arrived is firing three times a job, stop it now"), and an
/// <c>IOptions</c> snapshot is fixed at startup, so silencing it would need a
/// process restart. <c>Bind</c> registers a
/// <c>ConfigurationChangeTokenSource</c>, so with the host's default
/// reload-on-change appsettings provider the monitor picks up an edited file
/// in place. Config supplied purely by environment variables still needs a
/// restart - that is a limitation of the source, not of this class.
/// </para>
/// </remarks>
public class FulfilmentNotificationOptions
{
    public const string SectionName = "FulfilmentNotifications";

    /// <summary>Send <see cref="NotificationEventType.ProviderAssigned"/> on AwaitingFulfilment -&gt; Assigned.</summary>
    public bool ProviderAssignedEnabled { get; set; } = true;

    /// <summary>Send <see cref="NotificationEventType.ProviderEnRoute"/> on entry to ProviderEnRoute. The likeliest mute candidate - the live tracking screen already shows this.</summary>
    public bool ProviderEnRouteEnabled { get; set; } = true;

    /// <summary>Send <see cref="NotificationEventType.ProviderArrived"/> on entry to ProviderArrived. The other likely mute candidate.</summary>
    public bool ProviderArrivedEnabled { get; set; } = true;

    /// <summary>Send <see cref="NotificationEventType.JobStarted"/> on entry to InProgress.</summary>
    public bool JobStartedEnabled { get; set; } = true;

    /// <summary>Send <see cref="NotificationEventType.JobCompleted"/> on entry to Completed.</summary>
    public bool JobCompletedEnabled { get; set; } = true;

    /// <summary>
    /// Whether <paramref name="eventType"/> may be dispatched. Returns true
    /// for every event type this section does not govern, so adding a
    /// notification elsewhere never silently lands behind a mute nobody
    /// configured.
    /// </summary>
    public bool IsEnabled(NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.ProviderAssigned => ProviderAssignedEnabled,
        NotificationEventType.ProviderEnRoute => ProviderEnRouteEnabled,
        NotificationEventType.ProviderArrived => ProviderArrivedEnabled,
        NotificationEventType.JobStarted => JobStartedEnabled,
        NotificationEventType.JobCompleted => JobCompletedEnabled,
        _ => true
    };
}
