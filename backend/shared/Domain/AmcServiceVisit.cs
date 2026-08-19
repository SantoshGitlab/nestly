using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// An immutable, append-only audit row of one visit redeemed against a
/// <see cref="CustomerAmcContract"/> (docs/AMC.md) - mirrors the append-only
/// pattern already used for <c>BookingStatusHistory</c>,
/// <c>WalletLedgerEntry</c> and <c>RecurringBookingOccurrence</c>. Modeled as
/// its own entity with its own repository (not a navigation collection on
/// the contract) for the same reason <see cref="RecurringBookingOccurrence"/>
/// is: a long-running contract accumulates one row per redemption, and
/// nothing that loads a contract to check remaining entitlement should have
/// to pull the whole visit history along with it.
/// </summary>
public class AmcServiceVisit : Entity<Guid>
{
    public Guid ContractId { get; private set; }

    public Guid BookingId { get; private set; }

    public DateTime ConsumedAtUtc { get; private set; }

    protected AmcServiceVisit() { }

    public AmcServiceVisit(Guid id, Guid contractId, Guid bookingId, DateTime consumedAtUtc)
        : base(id)
    {
        ContractId = contractId;
        BookingId = bookingId;
        ConsumedAtUtc = consumedAtUtc;
    }
}
