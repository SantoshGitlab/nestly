using Nestly.Domain;

namespace Nestly.Application.Subscriptions;

/// <summary>One browsable plan (task 181's "browse plans") - the public subset of <see cref="Domain.SubscriptionPlan"/>, omitting admin-only bookkeeping (created/updated timestamps, who last edited it).</summary>
public sealed record SubscriptionPlanBrowseResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    SubscriptionBillingCycle BillingCycle,
    int FreeVisitsIncluded,
    decimal DiscountPercent,
    bool PrioritySlotFlag);

public sealed record SubscribeRequest(Guid PlanId);

/// <summary>"My subscription" (task 181's "view active subscription and remaining benefits") - every field a subscriber needs to see, all drawn from <see cref="CustomerSubscription"/>'s own snapshot, never a live join back to the plan (see that entity's doc comment).</summary>
public sealed record MySubscriptionResponse(
    Guid Id,
    string PlanName,
    decimal Price,
    SubscriptionBillingCycle BillingCycle,
    int FreeVisitsIncluded,
    decimal DiscountPercent,
    bool PrioritySlotFlag,
    CustomerSubscriptionStatus Status,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    int FreeVisitsRemaining,
    DateTime NextBillingDateUtc,
    string? LastPaymentFailureReason,
    DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc);

/// <summary>What an active subscription contributes to a booking's price (task 179), the subscription-side counterpart to <c>CouponSummaryResponse</c>. Exactly one of <paramref name="FreeVisitApplied"/>'s free-visit consumption or the standing percentage discount produced <paramref name="DiscountAmount"/> - never both on the same booking.</summary>
public sealed record SubscriptionBenefitSummary(Guid SubscriptionId, bool FreeVisitApplied, decimal DiscountAmount);
