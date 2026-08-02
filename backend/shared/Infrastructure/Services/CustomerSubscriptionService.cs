using Nestly.Application.Subscriptions;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="ICustomerSubscriptionService"/>.</summary>
public class CustomerSubscriptionService : ICustomerSubscriptionService
{
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ICustomerSubscriptionRepository _subscriptionRepository;
    private readonly TimeProvider _timeProvider;

    public CustomerSubscriptionService(
        ISubscriptionPlanRepository planRepository,
        ICustomerSubscriptionRepository subscriptionRepository,
        TimeProvider timeProvider)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<SubscriptionPlanBrowseResponse>> BrowsePlansAsync()
    {
        var plans = await _planRepository.ListActiveAsync();
        return plans.Select(ToBrowseResponse).ToList();
    }

    public async Task<Result<MySubscriptionResponse>> SubscribeAsync(Guid customerId, SubscribeRequest request)
    {
        var plan = await _planRepository.GetByIdAsync(request.PlanId);
        if (plan is null || !plan.IsActive)
        {
            return Error.NotFound("Subscription.PlanNotFound", "The specified subscription plan is not available.");
        }

        var existing = await _subscriptionRepository.GetCurrentByCustomerAsync(customerId);
        if (existing is not null)
        {
            return Error.Conflict("Subscription.AlreadySubscribed", "You already have an active subscription. Cancel it before subscribing to a new plan.");
        }

        var subscription = new CustomerSubscription(Guid.NewGuid(), customerId, plan, _timeProvider.GetUtcNow().UtcDateTime);

        await _subscriptionRepository.AddAsync(subscription);
        return ToMyResponse(subscription);
    }

    public async Task<Result> CancelAsync(Guid customerId, Guid subscriptionId)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
        if (subscription is null || subscription.CustomerId != customerId)
        {
            return Result.Failure(Error.NotFound("Subscription.NotFound", "The specified subscription does not exist."));
        }

        try
        {
            subscription.Cancel(_timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Business("Subscription.CannotCancel", ex.Message));
        }

        await _subscriptionRepository.UpdateAsync(subscription);
        return Result.Success();
    }

    public async Task<MySubscriptionResponse?> GetMyCurrentSubscriptionAsync(Guid customerId)
    {
        var subscription = await _subscriptionRepository.GetCurrentByCustomerAsync(customerId);
        return subscription is null ? null : ToMyResponse(subscription);
    }

    private static SubscriptionPlanBrowseResponse ToBrowseResponse(SubscriptionPlan plan) => new(
        plan.Id, plan.Name, plan.Description, plan.Price, plan.BillingCycle,
        plan.FreeVisitsIncluded, plan.DiscountPercent, plan.PrioritySlotFlag);

    private static MySubscriptionResponse ToMyResponse(CustomerSubscription subscription) => new(
        subscription.Id,
        subscription.PlanNameSnapshot,
        subscription.PriceSnapshot,
        subscription.BillingCycleSnapshot,
        subscription.FreeVisitsIncludedSnapshot,
        subscription.DiscountPercentSnapshot,
        subscription.PrioritySlotFlagSnapshot,
        subscription.Status,
        subscription.CurrentPeriodStartUtc,
        subscription.CurrentPeriodEndUtc,
        subscription.FreeVisitsRemaining,
        subscription.NextBillingDateUtc,
        subscription.LastPaymentFailureReason,
        subscription.CreatedAtUtc,
        subscription.CancelledAtUtc);
}
