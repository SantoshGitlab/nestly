using Nestly.Application.PartnerEarnings;
using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IPartnerEarningsService"/>
public class PartnerEarningsService : IPartnerEarningsService
{
    private readonly IPartnerEarningLedgerService _ledgerService;
    private readonly IPartnerPayoutService _payoutService;

    public PartnerEarningsService(IPartnerEarningLedgerService ledgerService, IPartnerPayoutService payoutService)
    {
        _ledgerService = ledgerService;
        _payoutService = payoutService;
    }

    public Task<Result<PartnerEarningsSummaryResponse>> GetSummaryAsync(Guid partnerId) =>
        _ledgerService.GetSummaryAsync(partnerId);

    public async Task<Result<IReadOnlyList<PartnerEarningLedgerEntryResponse>>> GetLedgerAsync(Guid partnerId)
    {
        var summary = await _ledgerService.GetSummaryAsync(partnerId);
        return summary.IsSuccess
            ? Result.Success<IReadOnlyList<PartnerEarningLedgerEntryResponse>>(summary.Value.Entries)
            : summary.Error;
    }

    public Task<Result<PartnerPayoutSearchResponse>> ListPayoutsAsync(Guid partnerId, PartnerPayoutStatus? status, int page, int pageSize) =>
        _payoutService.SearchAsync(partnerId, status, page, pageSize);

    public async Task<Result<PartnerPayoutResponse>> GetPayoutDetailAsync(Guid partnerId, Guid payoutId)
    {
        var result = await _payoutService.GetByIdAsync(payoutId);
        if (result.IsFailure)
        {
            return result;
        }

        if (result.Value.PartnerId != partnerId)
        {
            // Same code/message as a genuine not-found - never confirms
            // another partner's payout id exists (SRS 28.3 IDOR).
            return Error.NotFound("PartnerPayout.NotFound", "Payout was not found.");
        }

        return result;
    }
}
