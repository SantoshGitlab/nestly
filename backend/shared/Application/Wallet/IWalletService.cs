using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Wallet;

/// <summary>
/// Wallet balance and ledger (SRS 11.17, 14.5, tasks 74a-c). There is no
/// wallet-at-checkout spend capability in this phase - only crediting (from
/// refunds, task 75) and reading are wired up; SRS 14.5's "wallet usage
/// should be reflected in booking price summary" is out of scope until a
/// later phase actually lets a customer apply wallet balance to a booking.
/// </summary>
public interface IWalletService
{
    Task<Result<WalletBalanceResponse>> GetBalanceAsync(Guid customerId);

    Task<Result<IReadOnlyList<WalletLedgerEntryResponse>>> GetLedgerAsync(Guid customerId);

    /// <summary>Appends a credit entry, referencing its source event (SRS 14.5 "every credit/debit must reference source event").</summary>
    Task<WalletLedgerEntry> CreditAsync(Guid customerId, decimal amount, WalletSourceType sourceType, Guid? sourceReferenceId, string description);

    /// <summary>Appends a debit entry. Fails rather than letting the balance go negative.</summary>
    Task<Result<WalletLedgerEntry>> DebitAsync(Guid customerId, decimal amount, WalletSourceType sourceType, Guid? sourceReferenceId, string description);
}
