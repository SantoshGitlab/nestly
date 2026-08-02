namespace Nestly.Application.Subscriptions;

/// <summary>
/// Read-only preview of what a customer's active subscription (if any) would
/// contribute to an order (PRODUCT-ENHANCEMENTS.md #1, task 179) - the
/// subscription-side counterpart to <c>ICouponService.ValidateAsync</c>.
/// Consuming the benefit (the free-visit counter) is not part of this
/// interface - that's a single atomic repository call
/// (<see cref="ICustomerSubscriptionRepository.TryConsumeFreeVisitAsync"/>)
/// made directly at booking-creation time, the same shape
/// <c>ICouponService.ReserveAsync</c> is called directly for, so there is no
/// value in a second interface member wrapping one repository call.
/// </summary>
public interface ISubscriptionBenefitService
{
    /// <summary>
    /// Null if the customer has no Active subscription, or their plan
    /// currently grants neither a free visit nor a standing discount.
    /// Otherwise: a free visit (if any remain) always wins over the
    /// percentage discount - see <see cref="Domain.SubscriptionPlan.FreeVisitsIncluded"/>'s
    /// doc comment ("fully free") - a subscriber never receives a
    /// partial discount on a visit their plan already covers in full.
    /// </summary>
    Task<SubscriptionBenefitSummary?> PreviewAsync(Guid customerId, decimal orderAmount);
}
