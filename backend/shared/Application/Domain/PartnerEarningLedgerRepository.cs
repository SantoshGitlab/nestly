using Nestly.Domain;

namespace Nestly.Application;

/// <summary>Persistence for <see cref="PartnerEarningLedgerEntry"/> (task 148), mirroring <c>IWalletLedgerRepository</c>.</summary>
public interface IPartnerEarningLedgerRepository
{
    /// <summary>Entries are append-only - there is deliberately no Update/Delete method (mirrors WalletLedgerEntry, SRS 14.5's convention).</summary>
    Task AddAsync(PartnerEarningLedgerEntry entry);

    /// <summary>The most recent entry for a partner, whose BalanceAfter is the partner's current earnings balance (null when the partner has no earning activity yet).</summary>
    Task<PartnerEarningLedgerEntry?> GetLatestAsync(Guid partnerId);

    /// <summary>Full ledger for a partner, newest first.</summary>
    Task<IReadOnlyList<PartnerEarningLedgerEntry>> ListByPartnerAsync(Guid partnerId);

    /// <summary>Entries within a date range (inclusive), for payout batch calculation (task 148).</summary>
    Task<IReadOnlyList<PartnerEarningLedgerEntry>> ListByPartnerAndPeriodAsync(Guid partnerId, DateOnly periodStart, DateOnly periodEnd);
}
