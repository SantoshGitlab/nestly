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

    /// <summary>
    /// Task 175: when set on a Credit entry, this portion of the balance
    /// expires - is auto-debited by the scheduled sweep - if still unspent by
    /// this time. Null means the credit never expires (every credit type
    /// before task 175), and is always null on a Debit entry.
    /// </summary>
    public DateTime? ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Task 175's FIFO consumption-tracking: how much of THIS Credit entry's
    /// <see cref="Amount"/> is still unspent. Only meaningful when
    /// <see cref="ExpiresAtUtc"/> is set - a non-expiring credit's contribution
    /// to the balance never needs to be individually tracked, only the running
    /// <see cref="BalanceAfter"/> does. Mutated (not append-only, unlike every
    /// other field here) as later debits draw against it - this is bookkeeping
    /// on the *unconsumed* portion, not a rewrite of the audit trail itself
    /// (Amount/BalanceAfter/EntryType/SourceType/CreatedAtUtc never change).
    /// </summary>
    public decimal? RemainingAmount { get; private set; }

    protected WalletLedgerEntry() { }

    public WalletLedgerEntry(
        Guid id, Guid customerId, WalletEntryType entryType, decimal amount, decimal balanceAfter,
        WalletSourceType sourceType, Guid? sourceReferenceId, string description, DateTime? expiresAtUtc = null)
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

        if (expiresAtUtc is not null && entryType != WalletEntryType.Credit)
        {
            throw new ArgumentException("Only a credit entry can carry an expiry.", nameof(expiresAtUtc));
        }

        CustomerId = customerId;
        EntryType = entryType;
        Amount = amount;
        BalanceAfter = balanceAfter;
        SourceType = sourceType;
        SourceReferenceId = sourceReferenceId;
        Description = description ?? throw new ArgumentException("Description is required.", nameof(description));
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
        RemainingAmount = expiresAtUtc is not null ? amount : null;
    }

    /// <summary>A later debit drew <paramref name="amount"/> from this expiring credit's unconsumed portion (WalletService's FIFO consumption).</summary>
    public void ConsumeRemaining(decimal amount)
    {
        if (RemainingAmount is null)
        {
            throw new InvalidOperationException("This entry does not track a remaining amount (not an expiring credit).");
        }

        RemainingAmount = Math.Max(0, RemainingAmount.Value - amount);
    }

    /// <summary>The expiry sweep (task 175) wrote off whatever was left of this credit - it can never be drawn against again.</summary>
    public void ExpireRemaining()
    {
        if (RemainingAmount is null)
        {
            throw new InvalidOperationException("This entry does not track a remaining amount (not an expiring credit).");
        }

        RemainingAmount = 0;
    }
}
