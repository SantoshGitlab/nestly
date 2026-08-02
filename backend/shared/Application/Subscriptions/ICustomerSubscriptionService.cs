using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Subscriptions;

/// <summary>Customer-facing subscription flow (PRODUCT-ENHANCEMENTS.md #1, task 181): browse plans, subscribe, cancel, view active subscription.</summary>
public interface ICustomerSubscriptionService
{
    /// <summary>Every plan currently open to new subscribers (task 181's "browse plans").</summary>
    Task<IReadOnlyList<SubscriptionPlanBrowseResponse>> BrowsePlansAsync();

    /// <summary>
    /// Subscribes the customer to a plan, snapshotting its current terms
    /// (see <see cref="Domain.CustomerSubscription"/>). Rejects if the plan
    /// doesn't exist or is no longer active, or if the customer already has
    /// a live (Active/PaymentFailed) subscription - see
    /// <see cref="ICustomerSubscriptionRepository.GetCurrentByCustomerAsync"/>.
    /// </summary>
    Task<Result<MySubscriptionResponse>> SubscribeAsync(Guid customerId, SubscribeRequest request);

    /// <summary>Immediate, non-reactivatable cancellation - see <see cref="Domain.CustomerSubscription.Cancel"/>.</summary>
    Task<Result> CancelAsync(Guid customerId, Guid subscriptionId);

    /// <summary>The customer's current live subscription and remaining benefits, or null if they have none.</summary>
    Task<MySubscriptionResponse?> GetMyCurrentSubscriptionAsync(Guid customerId);
}
