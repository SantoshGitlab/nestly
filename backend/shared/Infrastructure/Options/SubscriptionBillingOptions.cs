using System.ComponentModel.DataAnnotations;

namespace Nestly.Infrastructure.Options;

/// <summary>Strongly typed binding of the "SubscriptionBilling" configuration section (PRODUCT-ENHANCEMENTS.md #1, task 178).</summary>
public class SubscriptionBillingOptions
{
    public const string SectionName = "SubscriptionBilling";

    /// <summary>
    /// How many consecutive failed charges a subscription tolerates before
    /// it auto-suspends to the terminal Expired state, rather than
    /// retrying again - see <see cref="Domain.CustomerSubscription.RecordFailedCharge"/>.
    /// 3 mirrors a typical dunning cadence (three attempts over roughly a
    /// week) - enough chances to recover a transient card issue without
    /// billing indefinitely against a card that will never succeed.
    /// </summary>
    [Range(1, 10)]
    public int RetryLimit { get; set; } = 3;

    /// <summary>How long after a failed charge before the next retry attempt (task 178's "retries with backoff").</summary>
    [Range(1, 30)]
    public int RetryBackoffDays { get; set; } = 2;

    /// <summary>How many days ahead of a subscription's next billing date the "expiring soon" reminder (task 183) fires.</summary>
    [Range(1, 30)]
    public int ExpiringSoonLeadTimeDays { get; set; } = 3;
}
