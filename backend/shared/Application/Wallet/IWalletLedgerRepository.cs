using Nestly.Domain;

namespace Nestly.Application.Wallet;

public interface IWalletLedgerRepository
{
    /// <summary>Entries are append-only - there is deliberately no Update/Delete method (SRS 14.5).</summary>
    Task AddAsync(WalletLedgerEntry entry);

    /// <summary>The most recent entry for a customer, whose BalanceAfter is the customer's current balance (null when the customer has no wallet activity yet).</summary>
    Task<WalletLedgerEntry?> GetLatestAsync(Guid customerId);

    /// <summary>Full ledger for a customer, newest first (SRS 11.17.1).</summary>
    Task<IReadOnlyList<WalletLedgerEntry>> ListByCustomerAsync(Guid customerId);

    /// <summary>
    /// Task 175's FIFO consumption source: this customer's not-yet-expired
    /// credit entries that still have an unconsumed portion, soonest-to-expire
    /// first - the order a debit should draw against them in, so the credit
    /// closest to being wasted is spent first.
    /// </summary>
    Task<IReadOnlyList<WalletLedgerEntry>> ListUnexpiredCreditsWithRemainingAsync(Guid customerId, DateTime asOfUtc);

    /// <summary>Task 175's sweep source: every customer's credit entries that have expired with an unconsumed portion still outstanding.</summary>
    Task<IReadOnlyList<WalletLedgerEntry>> ListExpiredCreditsWithRemainingAsync(DateTime asOfUtc);

    /// <summary>
    /// Persists a mutation to <see cref="WalletLedgerEntry.RemainingAmount"/>
    /// only (via <see cref="WalletLedgerEntry.ConsumeRemaining"/> /
    /// <see cref="WalletLedgerEntry.ExpireRemaining"/>) - the audit fields
    /// (Amount, BalanceAfter, EntryType, SourceType, CreatedAtUtc) never
    /// change, so this does not violate the append-only ledger (SRS 14.5).
    /// </summary>
    Task UpdateRemainingAsync(WalletLedgerEntry entry);
}
