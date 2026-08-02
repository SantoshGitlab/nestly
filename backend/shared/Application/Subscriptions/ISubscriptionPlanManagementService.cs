using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Subscriptions;

/// <summary>
/// Admin CRUD over subscription plans (PRODUCT-ENHANCEMENTS.md #1, task 180)
/// - price, billing cycle, benefits, active window. Same split
/// <c>ICouponManagementService</c> draws against the consumer-facing coupon
/// flow: this is the admin management surface, distinct from
/// <see cref="ISubscriptionBenefitService"/> and the customer-facing
/// subscribe/cancel flow.
/// </summary>
public interface ISubscriptionPlanManagementService
{
    Task<IReadOnlyList<SubscriptionPlanAdminResponse>> ListAllAsync();

    Task<Result<SubscriptionPlanAdminResponse>> GetByIdAsync(Guid id);

    Task<Result<SubscriptionPlanAdminResponse>> CreateAsync(SubscriptionPlanCreateRequest request);

    Task<Result<SubscriptionPlanAdminResponse>> UpdateAsync(Guid id, SubscriptionPlanUpdateRequest request, Guid adminUserId);

    /// <summary>Re-opens a plan to new subscribers (task 180's "active window"). Existing subscribers on this plan were never affected by deactivation - see <see cref="Domain.SubscriptionPlan.Activate"/>.</summary>
    Task<Result> ActivateAsync(Guid id, Guid adminUserId);

    /// <summary>Closes a plan to new subscribers without deleting it or touching existing subscribers - see <see cref="Domain.SubscriptionPlan.Deactivate"/>.</summary>
    Task<Result> DeactivateAsync(Guid id, Guid adminUserId);
}
