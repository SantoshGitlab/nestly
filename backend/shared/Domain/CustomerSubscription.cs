using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain.Events;

namespace Nestly.Domain;

/// <summary>
/// A customer's live enrollment in a <see cref="SubscriptionPlan"/>
/// (PRODUCT-ENHANCEMENTS.md #1, tasks 177-179, 183). The aggregate root for
/// the subscription domain - owns its own billing-period/benefit state as a
/// single consistency boundary, the same role <see cref="Booking"/> plays for
/// a booking.
///
/// Every plan term (<see cref="PriceSnapshot"/>, <see cref="BillingCycleSnapshot"/>,
/// <see cref="FreeVisitsIncludedSnapshot"/>, <see cref="DiscountPercentSnapshot"/>)
/// is a snapshot taken at subscribe time, not a live join to
/// <see cref="SubscriptionPlan"/> - the same "snapshot at transaction time"
/// convention <see cref="Booking"/>'s price fields already establish, so an
/// admin editing a plan's price never silently reprices an existing
/// subscriber mid-cycle; a price/term change only takes effect for
/// subscribers who subscribe fresh after the edit. <see cref="PlanId"/> is
/// kept for traceability only, same as <see cref="Booking.SlotWindowId"/>.
/// </summary>
public class CustomerSubscription : AggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }

    public Guid PlanId { get; private set; }

    public string PlanNameSnapshot { get; private set; } = string.Empty;

    public decimal PriceSnapshot { get; private set; }

    public SubscriptionBillingCycle BillingCycleSnapshot { get; private set; }

    public int FreeVisitsIncludedSnapshot { get; private set; }

    public decimal DiscountPercentSnapshot { get; private set; }

    public bool PrioritySlotFlagSnapshot { get; private set; }

    public CustomerSubscriptionStatus Status { get; private set; }

    public DateTime CurrentPeriodStartUtc { get; private set; }

    public DateTime CurrentPeriodEndUtc { get; private set; }

    /// <summary>Free-visit credits left in the current period (task 179). Consumed atomically at booking time - see <c>ICustomerSubscriptionRepository.TryConsumeFreeVisitAsync</c>, the same concurrency-safe pattern <see cref="Coupon.RedemptionCount"/>'s doc comment describes for <c>TryReserveRedemptionAsync</c>.</summary>
    public int FreeVisitsRemaining { get; private set; }

    /// <summary>When the recurring billing job (task 178) will next attempt a charge - the current period's end on a healthy subscription, or a backoff-delayed retry date after a failed charge.</summary>
    public DateTime NextBillingDateUtc { get; private set; }

    /// <summary>Consecutive failed-charge count since the last successful renewal (task 178's retry-with-backoff). Reset to 0 on every successful charge.</summary>
    public int RetryCount { get; private set; }

    public string? LastPaymentFailureReason { get; private set; }

    /// <summary>The <see cref="CurrentPeriodEndUtc"/> value an "expiring soon" reminder (task 183) was last sent for - compared against the live value so the reminder fires once per period rather than once per billing-job run.</summary>
    public DateTime? ExpiringSoonNotifiedForPeriodEndUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    protected CustomerSubscription() { }

    public CustomerSubscription(Guid id, Guid customerId, SubscriptionPlan plan, DateTime nowUtc)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(plan);

        CustomerId = customerId;
        PlanId = plan.Id;
        PlanNameSnapshot = plan.Name;
        PriceSnapshot = plan.Price;
        BillingCycleSnapshot = plan.BillingCycle;
        FreeVisitsIncludedSnapshot = plan.FreeVisitsIncluded;
        DiscountPercentSnapshot = plan.DiscountPercent;
        PrioritySlotFlagSnapshot = plan.PrioritySlotFlag;

        Status = CustomerSubscriptionStatus.Active;
        CurrentPeriodStartUtc = nowUtc;
        CurrentPeriodEndUtc = BillingCycleSnapshot.AddCycle(nowUtc);
        FreeVisitsRemaining = FreeVisitsIncludedSnapshot;
        NextBillingDateUtc = CurrentPeriodEndUtc;
        RetryCount = 0;
        CreatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Customer-initiated cancellation (task 181). Takes effect immediately -
    /// benefits stop right away rather than riding out the paid-for period -
    /// the simplest unambiguous reading of "cancel" absent a spec'd
    /// end-of-period-access model; revisit if a pro-rated/"access until
    /// period end" policy is ever required. Terminal: a cancelled
    /// subscription can never be reactivated, only re-subscribed as a new one.
    /// </summary>
    public void Cancel(DateTime nowUtc)
    {
        if (Status is CustomerSubscriptionStatus.Cancelled or CustomerSubscriptionStatus.Expired)
        {
            throw new InvalidOperationException($"Cannot cancel a subscription that is already {Status}.");
        }

        Status = CustomerSubscriptionStatus.Cancelled;
        CancelledAtUtc = nowUtc;
    }

    /// <summary>
    /// Rolls the subscription to its next billing period after a successful
    /// recurring charge (task 178). Resets the free-visit allowance and
    /// clears any retry state - including recovering a previously
    /// <see cref="CustomerSubscriptionStatus.PaymentFailed"/> subscription
    /// back to <see cref="CustomerSubscriptionStatus.Active"/>, matching
    /// PRODUCT-ENHANCEMENTS.md #1's "a subscriber shouldn't lose an active
    /// plan over one declined card without a chance to fix payment details."
    /// </summary>
    public void RecordSuccessfulRenewal(DateTime nowUtc)
    {
        EnsureBillable();

        CurrentPeriodStartUtc = CurrentPeriodEndUtc;
        CurrentPeriodEndUtc = BillingCycleSnapshot.AddCycle(CurrentPeriodStartUtc);
        FreeVisitsRemaining = FreeVisitsIncludedSnapshot;
        NextBillingDateUtc = CurrentPeriodEndUtc;
        RetryCount = 0;
        LastPaymentFailureReason = null;
        Status = CustomerSubscriptionStatus.Active;

        RaiseDomainEvent(new SubscriptionRenewedEvent(Id, CustomerId));
    }

    /// <summary>
    /// Records a failed recurring charge (task 178). Suspends the
    /// subscription (<see cref="CustomerSubscriptionStatus.PaymentFailed"/>)
    /// and schedules a backoff-delayed retry while <paramref name="retryCount"/>
    /// after this failure is still within <paramref name="retryLimit"/>; once
    /// exhausted, the subscription moves to the terminal
    /// <see cref="CustomerSubscriptionStatus.Expired"/> state instead - "a
    /// failed recurring charge retries with backoff before the subscription
    /// auto-suspends" (PRODUCT-ENHANCEMENTS.md #1).
    /// </summary>
    public void RecordFailedCharge(DateTime nowUtc, string reason, int retryLimit, TimeSpan retryBackoff)
    {
        EnsureBillable();

        RetryCount++;
        LastPaymentFailureReason = reason;

        bool isFinal = RetryCount > retryLimit;
        if (isFinal)
        {
            Status = CustomerSubscriptionStatus.Expired;
        }
        else
        {
            Status = CustomerSubscriptionStatus.PaymentFailed;
            NextBillingDateUtc = nowUtc.Add(retryBackoff);
        }

        RaiseDomainEvent(new SubscriptionPaymentFailedEvent(Id, CustomerId, isFinal));
    }

    /// <summary>Marks that an "expiring soon" reminder (task 183) has been sent for the current period, so the daily billing-job sweep does not re-send it every run until the period actually rolls over.</summary>
    public void MarkExpiringSoonNotified()
    {
        ExpiringSoonNotifiedForPeriodEndUtc = CurrentPeriodEndUtc;
        RaiseDomainEvent(new SubscriptionExpiringSoonEvent(Id, CustomerId));
    }

    private void EnsureBillable()
    {
        if (Status is CustomerSubscriptionStatus.Cancelled or CustomerSubscriptionStatus.Expired)
        {
            throw new InvalidOperationException($"Cannot bill a subscription that is {Status}.");
        }
    }
}
