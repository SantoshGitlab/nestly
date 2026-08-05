using Nestly.Application.Subscriptions;

namespace Nestly.Infrastructure.Services;

/// <summary>See <see cref="ISubscriptionBenefitService"/>.</summary>
public class SubscriptionBenefitService : ISubscriptionBenefitService
{
    private readonly ICustomerSubscriptionRepository _subscriptionRepository;

    public SubscriptionBenefitService(ICustomerSubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<SubscriptionBenefitSummary?> PreviewAsync(Guid customerId, decimal orderAmount)
    {
        var subscription = await _subscriptionRepository.GetActiveByCustomerAsync(customerId);
        if (subscription is null)
        {
            return null;
        }

        if (subscription.FreeVisitsRemaining > 0)
        {
            // "Fully free" (SubscriptionPlan.FreeVisitsIncluded's doc
            // comment) - the whole order, not just the base visit charge.
            return new SubscriptionBenefitSummary(subscription.Id, FreeVisitApplied: true, DiscountAmount: orderAmount);
        }

        if (subscription.DiscountPercentSnapshot > 0)
        {
            // Explicit MidpointRounding (task 260) - see CommissionCalculator.
            decimal discountAmount = Math.Round(orderAmount * subscription.DiscountPercentSnapshot / 100m, 2, MidpointRounding.ToEven);
            return new SubscriptionBenefitSummary(subscription.Id, FreeVisitApplied: false, DiscountAmount: discountAmount);
        }

        return null;
    }
}
