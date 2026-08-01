using Nestly.Domain;

namespace Nestly.Application.Wallet;

/// <summary>Current wallet balance (SRS 11.17.1, 14.5).</summary>
public record WalletBalanceResponse(decimal Balance);

/// <summary>One ledger entry as shown to the customer (SRS 11.17.1 "credit/debit entries, booking references").</summary>
public record WalletLedgerEntryResponse(
    Guid Id,
    WalletEntryType EntryType,
    decimal Amount,
    decimal BalanceAfter,
    WalletSourceType SourceType,
    Guid? SourceReferenceId,
    string Description,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc);
