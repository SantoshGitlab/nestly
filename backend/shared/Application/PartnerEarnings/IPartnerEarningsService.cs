using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.PartnerEarnings;

/// <summary>
/// The partner's own self-service view of their earnings and payouts (task
/// 149c, PARTNER.md API surface "Earnings"). A thin, ownership-safe facade
/// over the admin-facing <see cref="IPartnerEarningLedgerService"/>/
/// <see cref="IPartnerPayoutService"/> (task 148) rather than a second copy
/// of that logic - the underlying ledger/payout entities and their
/// transitions have exactly one owner. This facade's only added
/// responsibility is scoping every read to the caller's own partner id (SRS
/// 28.3 IDOR) - in particular <see cref="GetPayoutDetailAsync"/>, since the
/// admin-facing <c>IPartnerPayoutService.GetByIdAsync</c> takes no partner
/// id to check against.
/// </summary>
public interface IPartnerEarningsService
{
    Task<Result<PartnerEarningsSummaryResponse>> GetSummaryAsync(Guid partnerId);

    /// <summary>The caller's own append-only ledger entries, newest first.</summary>
    Task<Result<IReadOnlyList<PartnerEarningLedgerEntryResponse>>> GetLedgerAsync(Guid partnerId);

    Task<Result<PartnerPayoutSearchResponse>> ListPayoutsAsync(Guid partnerId, PartnerPayoutStatus? status, int page, int pageSize);

    /// <summary>One payout's detail - 404s (rather than the underlying service's plain not-found) when the payout exists but belongs to a different partner, so a caller can never probe another partner's payout by id.</summary>
    Task<Result<PartnerPayoutResponse>> GetPayoutDetailAsync(Guid partnerId, Guid payoutId);
}
