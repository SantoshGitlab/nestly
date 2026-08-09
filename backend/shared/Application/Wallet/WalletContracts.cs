using Nestly.Domain;

namespace Nestly.Application.Wallet;

/// <summary>Current wallet balance (SRS 11.17.1, 14.5).</summary>
public record WalletBalanceResponse(decimal Balance);

/// <summary>
/// What the wallet contributes to a booking (SRS 11.7.2 "wallet credit used",
/// task 310). <paramref name="Balance"/> is always populated (surfaced
/// whether or not it is being applied); <paramref name="AppliedAmount"/> is
/// zero unless the caller opted in via <c>BookingSummaryRequest.ApplyWalletCredit</c>,
/// in which case it is <c>Math.Min(Balance, amount still payable after any
/// coupon/subscription discount)</c> - a customer can never apply more than
/// they have, or more than the booking actually costs.
/// </summary>
public record WalletCreditSummaryResponse(decimal Balance, decimal AppliedAmount);

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
