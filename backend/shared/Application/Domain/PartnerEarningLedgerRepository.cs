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

    /// <summary>Nestly Coins' monthly earn cap (docs/NESTLY-COINS.md FRAUD/ABUSE PREVENTION, task 201): total credited for one source type within a date range, computed as a DB-side SUM (mirrors <c>IWalletLedgerRepository</c>'s equivalent).</summary>
    Task<decimal> SumCreditsBySourceTypeInRangeAsync(Guid partnerId, PartnerEarningSourceType sourceType, DateTime fromUtc, DateTime toUtc);

    /// <summary>Nestly Coins' clawback lookup (task 201): the credit entry issued for one source event, if any.</summary>
    Task<PartnerEarningLedgerEntry?> FindBySourceAsync(PartnerEarningSourceType sourceType, Guid sourceReferenceId);
}
