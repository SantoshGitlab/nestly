using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>
/// Tuning for the durable notification-intent sweep (task 294). Not a secret,
/// and every value defaults to the intended production behaviour - same
/// no-appsettings-section-required reasoning as <c>BookingExpiryOptions</c> and
/// <c>FulfilmentNotificationOptions</c>.
/// </summary>
public class NotificationIntentOptions
{
    public const string SectionName = "NotificationIntents";

    /// <summary>
    /// How long an intent is left alone after the transaction that created it,
    /// before the sweep will consider it lost. Long enough that the in-process
    /// fast path - which starts microseconds after the commit - has finished
    /// or died, short enough that a genuinely lost notification is not stale
    /// by the time it arrives.
    /// </summary>
    [Range(10, 3600)]
    public int GraceSeconds { get; set; } = 120;

    /// <summary>
    /// How long a claim holds a row. This is the ceiling on how long a crashed
    /// worker can strand a notification, so it wants to be comfortably longer
    /// than a dispatch takes and comfortably shorter than a customer's
    /// patience.
    /// </summary>
    [Range(30, 3600)]
    public int LeaseSeconds { get; set; } = 300;

    /// <summary>
    /// The retry bound. The in-process attempt consumes the first one, so this
    /// is one immediate try plus four sweeps before the intent is abandoned -
    /// enough to ride out a dependency outage, few enough that a
    /// systematically broken notification stops burning sweeps and shows up as
    /// an Abandoned row instead.
    /// </summary>
    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>How many intents one pass will send. Bounds the runtime of a pass after an outage has backed thousands of them up; the next pass takes the next batch.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;
}
