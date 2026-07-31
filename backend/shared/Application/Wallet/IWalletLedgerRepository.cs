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
}
