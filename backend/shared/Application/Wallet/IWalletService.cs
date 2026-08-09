using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Wallet;

/// <summary>
/// Wallet balance and ledger (SRS 11.17, 14.5, tasks 74a-c). <see cref="DebitAsync"/>
/// is also how a customer's wallet balance is applied at checkout (SRS 11.7.2,
/// task 310) - see <c>BookingService.CreateAsync</c> and <c>BookingSummaryService</c>.
/// </summary>
public interface IWalletService
{
    Task<Result<WalletBalanceResponse>> GetBalanceAsync(Guid customerId);

    Task<Result<IReadOnlyList<WalletLedgerEntryResponse>>> GetLedgerAsync(Guid customerId);

    /// <summary>
    /// Appends a credit entry, referencing its source event (SRS 14.5 "every
    /// credit/debit must reference source event"). <paramref name="expiresAtUtc"/>
    /// is task 175's expiring wallet credit - omit for a credit that never
    /// expires (every credit type before task 175).
    /// </summary>
    Task<WalletLedgerEntry> CreditAsync(Guid customerId, decimal amount, WalletSourceType sourceType, Guid? sourceReferenceId, string description, DateTime? expiresAtUtc = null);

    /// <summary>
    /// Appends a debit entry. Fails rather than letting the balance go
    /// negative. Draws first against any not-yet-expired expiring credit
    /// (soonest-to-expire first, task 175's FIFO consumption), so a customer
    /// who has both expiring and non-expiring balance never wastes the
    /// expiring portion while it's still spendable.
    /// </summary>
    Task<Result<WalletLedgerEntry>> DebitAsync(Guid customerId, decimal amount, WalletSourceType sourceType, Guid? sourceReferenceId, string description);

    /// <summary>
    /// Task 175's expiry sweep: writes off <paramref name="amount"/> (the
    /// unconsumed portion of one specific expiring credit, identified by
    /// <paramref name="expiredEntryId"/>) as a debit. Distinct from
    /// <see cref="DebitAsync"/> - this never re-runs FIFO consumption against
    /// other credits, since the amount being written off already belongs to
    /// the one credit that just expired, not to a customer-initiated spend.
    /// </summary>
    Task<WalletLedgerEntry?> ExpireCreditAsync(Guid customerId, Guid expiredEntryId, decimal amount);
}
