using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain.Events;

namespace Nestly.Domain;

/// <summary>
/// A customer's purchased Annual Maintenance Contract (docs/AMC.md) - the
/// aggregate root for the AMC domain, owning its own entitlement/term state
/// as a single consistency boundary, the same role <see cref="Booking"/>
/// plays for a booking and <see cref="CustomerSubscription"/> plays for a
/// subscription.
///
/// Every plan term (<see cref="PriceSnapshot"/>, <see cref="TermMonthsSnapshot"/>,
/// <see cref="VisitsIncludedSnapshot"/>) is a snapshot taken at purchase time,
/// not a live join to <see cref="AmcPlan"/> - the same "snapshot at
/// transaction time" convention <see cref="CustomerSubscription"/> and
/// <see cref="Booking"/> already establish, so an admin editing a plan's
/// price or terms never silently changes an existing holder's contract.
/// <see cref="PlanId"/> is kept for traceability only.
/// </summary>
public class CustomerAmcContract : AggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }

    public Guid PlanId { get; private set; }

    public string PlanNameSnapshot { get; private set; } = string.Empty;

    public Guid CategoryIdSnapshot { get; private set; }

    public decimal PriceSnapshot { get; private set; }

    public int TermMonthsSnapshot { get; private set; }

    public int VisitsIncludedSnapshot { get; private set; }

    /// <summary>Free text the customer supplies at purchase (e.g. "Living room split AC") so they can tell apart more than one contract - see docs/AMC.md OPEN DECISIONS #3 on why a contract covers exactly one asset.</summary>
    public string AssetLabel { get; private set; } = string.Empty;

    public CustomerAmcContractStatus Status { get; private set; }

    public DateTime StartDateUtc { get; private set; }

    public DateTime EndDateUtc { get; private set; }

    /// <summary>Entitlement remaining, drawn down by <see cref="RedeemVisit"/> - the field that makes this module distinct from Subscription's free-visit-per-cycle allowance and Recurring Bookings' fixed cadence (docs/AMC.md's comparison table).</summary>
    public int VisitsRemaining { get; private set; }

    /// <summary>
    /// Nullable rather than the required FK docs/AMC.md's first draft assumed:
    /// <see cref="Nestly.Domain.PaymentTransaction.BookingId"/> is a required
    /// foreign key, and the whole gateway-order/webhook/commission/escrow
    /// pipeline is built assuming every transaction belongs to a booking.
    /// Bolting a non-booking payable onto that entity is real architectural
    /// work of its own, out of scope here - see docs/AMC.md OPEN DECISIONS.
    /// For MVP this is set only when a real transaction happens to exist
    /// (none does yet); purchase is otherwise recorded without one, honestly
    /// representing that gateway charging is not yet wired up rather than
    /// faking a booking-shaped transaction to satisfy a non-nullable column.
    /// </summary>
    public Guid? PaymentTransactionId { get; private set; }

    /// <summary>The <see cref="EndDateUtc"/> value an "expiring soon" reminder was last sent for - compared against the live value so the reminder fires once per contract, mirroring <see cref="CustomerSubscription.ExpiringSoonNotifiedForPeriodEndUtc"/>.</summary>
    public DateTime? ExpiringSoonNotifiedForEndDateUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    protected CustomerAmcContract() { }

    public CustomerAmcContract(
        Guid id,
        Guid customerId,
        AmcPlan plan,
        string assetLabel,
        Guid? paymentTransactionId,
        DateTime nowUtc)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(assetLabel))
        {
            throw new ArgumentException("An asset label is required so the customer can tell contracts apart.", nameof(assetLabel));
        }

        CustomerId = customerId;
        PlanId = plan.Id;
        PlanNameSnapshot = plan.Name;
        CategoryIdSnapshot = plan.CategoryId;
        PriceSnapshot = plan.Price;
        TermMonthsSnapshot = plan.TermMonths;
        VisitsIncludedSnapshot = plan.VisitsIncluded;
        AssetLabel = assetLabel.Trim();
        PaymentTransactionId = paymentTransactionId;

        Status = CustomerAmcContractStatus.Active;
        StartDateUtc = nowUtc;
        EndDateUtc = nowUtc.AddMonths(TermMonthsSnapshot);
        VisitsRemaining = VisitsIncludedSnapshot;
        CreatedAtUtc = nowUtc;

        RaiseDomainEvent(new AmcContractPurchasedEvent(Id, CustomerId));
    }

    /// <summary>
    /// Draws down one unit of entitlement on booking completion (docs/AMC.md
    /// "on completion, not on creation" - a cancelled-before-completion visit
    /// must not cost the customer an entitlement, the same principle every
    /// other credit-consuming flow in this codebase follows). Moves to
    /// <see cref="CustomerAmcContractStatus.Exhausted"/> the moment
    /// entitlement reaches zero, distinct from <see cref="Expire"/> - see
    /// <see cref="CustomerAmcContractStatus"/>'s doc comment.
    /// </summary>
    public void RedeemVisit(Guid bookingId, DateTime nowUtc)
    {
        if (Status != CustomerAmcContractStatus.Active)
        {
            throw new InvalidOperationException($"Cannot redeem a visit on a contract that is {Status}.");
        }

        if (VisitsRemaining <= 0)
        {
            throw new InvalidOperationException("No entitlement remaining on this contract.");
        }

        if (nowUtc > EndDateUtc)
        {
            throw new InvalidOperationException("This contract's term has ended.");
        }

        VisitsRemaining--;
        RaiseDomainEvent(new AmcVisitRedeemedEvent(Id, CustomerId, bookingId, VisitsRemaining));

        if (VisitsRemaining == 0)
        {
            Status = CustomerAmcContractStatus.Exhausted;
            RaiseDomainEvent(new AmcContractExhaustedEvent(Id, CustomerId));
        }
    }

    /// <summary>Whether this contract can currently be used to redeem a visit - a read-only check the application layer uses before attempting <see cref="RedeemVisit"/>, so a doomed redemption attempt never reaches the booking orchestration.</summary>
    public bool CanRedeem(DateTime nowUtc) =>
        Status == CustomerAmcContractStatus.Active && VisitsRemaining > 0 && nowUtc <= EndDateUtc;

    /// <summary>Moves a still-Active contract whose term has passed to Expired (the scheduled sweep's job, mirroring how a Subscription or RecurringBookingPlan's own background processing advances state). No-op if already terminal - a contract already Exhausted keeps that status even once its term also lapses, since exhaustion is the more informative of the two terminal outcomes for the renewal report.</summary>
    public void Expire(DateTime nowUtc)
    {
        if (Status != CustomerAmcContractStatus.Active)
        {
            return;
        }

        if (nowUtc <= EndDateUtc)
        {
            throw new InvalidOperationException("Cannot expire a contract whose term has not yet ended.");
        }

        Status = CustomerAmcContractStatus.Expired;
    }

    /// <summary>Customer-initiated cancellation, mirroring <see cref="CustomerSubscription.Cancel"/> - takes effect immediately, terminal.</summary>
    public void Cancel(DateTime nowUtc)
    {
        if (Status is CustomerAmcContractStatus.Cancelled or CustomerAmcContractStatus.Expired or CustomerAmcContractStatus.Exhausted)
        {
            throw new InvalidOperationException($"Cannot cancel a contract that is already {Status}.");
        }

        Status = CustomerAmcContractStatus.Cancelled;
        CancelledAtUtc = nowUtc;
    }

    /// <summary>Records that an expiring-soon reminder was sent for the current <see cref="EndDateUtc"/>, so the scheduled sweep that raises <see cref="AmcContractExpiringSoonEvent"/> fires once per contract rather than once per run - mirrors <see cref="CustomerSubscription.MarkExpiringSoonNotified"/>.</summary>
    public void MarkExpiringSoonNotified(DateTime nowUtc)
    {
        ExpiringSoonNotifiedForEndDateUtc = EndDateUtc;
        RaiseDomainEvent(new AmcContractExpiringSoonEvent(Id, CustomerId));
    }

    /// <summary>Whether an expiring-soon reminder is still owed for the current term - false once <see cref="MarkExpiringSoonNotified"/> has recorded this exact <see cref="EndDateUtc"/>.</summary>
    public bool NeedsExpiringSoonNotification => ExpiringSoonNotifiedForEndDateUtc != EndDateUtc;
}
