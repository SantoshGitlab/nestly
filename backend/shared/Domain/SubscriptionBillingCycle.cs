namespace Nestly.Domain;

/// <summary>Billing cadence for a <see cref="SubscriptionPlan"/> (PRODUCT-ENHANCEMENTS.md #1, task 177).</summary>
public enum SubscriptionBillingCycle
{
    Monthly,
    Quarterly,
    Yearly
}

/// <summary>Billing-cycle math shared by <see cref="SubscriptionPlan"/>, <see cref="CustomerSubscription"/>, and the recurring billing job - a single source of truth for "what is one cycle" so the period a customer is quoted always matches the period they're actually charged for.</summary>
public static class SubscriptionBillingCycleExtensions
{
    public static DateTime AddCycle(this SubscriptionBillingCycle cycle, DateTime fromUtc) => cycle switch
    {
        SubscriptionBillingCycle.Monthly => fromUtc.AddMonths(1),
        SubscriptionBillingCycle.Quarterly => fromUtc.AddMonths(3),
        SubscriptionBillingCycle.Yearly => fromUtc.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(cycle), cycle, "Unknown billing cycle.")
    };
}
