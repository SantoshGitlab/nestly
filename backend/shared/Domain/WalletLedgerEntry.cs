using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

/// <summary>
/// One append-only entry in a customer's wallet ledger (SRS 23.4
/// wallet_ledger, SRS 14.5). There is no separate "wallet account" entity
/// with a mutable balance column - the running balance is derived by reading
/// the latest entry's <see cref="BalanceAfter"/> (SRS 14.5 "must be append-only
/// or traceable"). Entities of this type are never updated or deleted after
/// creation; the repository intentionally exposes no Update/Delete method.
/// </summary>
public class WalletLedgerEntry : Entity<Guid>
{
    public Guid CustomerId { get; private set; }

    public WalletEntryType EntryType { get; private set; }

    /// <summary>Always positive; direction comes from <see cref="EntryType"/>.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Running wallet balance immediately after this entry - an audit snapshot, not re-derived on read.</summary>
    public decimal BalanceAfter { get; private set; }

    public WalletSourceType SourceType { get; private set; }

    /// <summary>The id of the source aggregate (e.g. a RefundTransaction) that produced this entry, when one exists.</summary>
    public Guid? SourceReferenceId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    protected WalletLedgerEntry() { }

    public WalletLedgerEntry(
        Guid id, Guid customerId, WalletEntryType entryType, decimal amount, decimal balanceAfter,
        WalletSourceType sourceType, Guid? sourceReferenceId, string description)
        : base(id)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Wallet entry amount must be positive.");
        }

        if (balanceAfter < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balanceAfter), "Wallet balance cannot go negative.");
        }

        CustomerId = customerId;
        EntryType = entryType;
        Amount = amount;
        BalanceAfter = balanceAfter;
        SourceType = sourceType;
        SourceReferenceId = sourceReferenceId;
        Description = description ?? throw new ArgumentException("Description is required.", nameof(description));
        CreatedAtUtc = DateTime.UtcNow;
    }
}
