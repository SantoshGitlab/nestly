using Nestly.Domain;

namespace Nestly.Application.Subscriptions;

/// <summary>Admin plan detail/list row (task 180's full field set).</summary>
public sealed record SubscriptionPlanAdminResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    SubscriptionBillingCycle BillingCycle,
    int FreeVisitsIncluded,
    decimal DiscountPercent,
    bool PrioritySlotFlag,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Admin request to create a plan (task 180). Starts active - see <see cref="SubscriptionPlan"/>'s constructor.</summary>
public sealed record SubscriptionPlanCreateRequest(
    string Name,
    string? Description,
    decimal Price,
    SubscriptionBillingCycle BillingCycle,
    int FreeVisitsIncluded,
    decimal DiscountPercent,
    bool PrioritySlotFlag);

/// <summary>Admin request to edit every mutable field of an existing plan (task 180). Existing subscribers are unaffected until their next renewal - see <see cref="SubscriptionPlan.Update"/>.</summary>
public sealed record SubscriptionPlanUpdateRequest(
    string Name,
    string? Description,
    decimal Price,
    SubscriptionBillingCycle BillingCycle,
    int FreeVisitsIncluded,
    decimal DiscountPercent,
    bool PrioritySlotFlag);
