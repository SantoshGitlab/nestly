using Nestly.Application;
using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IPartnerPayoutService"/>
public class PartnerPayoutService : IPartnerPayoutService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IPartnerPayoutRepository _payoutRepository;
    private readonly IPartnerEarningLedgerRepository _ledgerRepository;

    public PartnerPayoutService(
        IPartnerRepository partnerRepository,
        IPartnerPayoutRepository payoutRepository,
        IPartnerEarningLedgerRepository ledgerRepository)
    {
        _partnerRepository = partnerRepository;
        _payoutRepository = payoutRepository;
        _ledgerRepository = ledgerRepository;
    }

    public async Task<Result<PartnerPayoutResponse>> CreateBatchAsync(Guid partnerId, CreatePartnerPayoutRequest request)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner is null)
        {
            return Error.NotFound("PartnerPayout.PartnerNotFound", "Partner was not found.");
        }

        var entries = await _ledgerRepository.ListByPartnerAndPeriodAsync(partnerId, request.PeriodStart, request.PeriodEnd);
        decimal total = entries.Sum(e => e.EntryType == PartnerEarningEntryType.Credit ? e.Amount : -e.Amount);

        if (total <= 0)
        {
            return Error.Business("PartnerPayout.NothingToPay", "There is no positive earning balance for this partner in the given period.");
        }

        var payout = new PartnerPayout(Guid.NewGuid(), partnerId, request.PeriodStart, request.PeriodEnd, total);
        await _payoutRepository.AddAsync(payout);

        return ToResponse(payout, partner.DisplayName);
    }

    public async Task<Result<PartnerPayoutResponse>> GetByIdAsync(Guid payoutId)
    {
        var payout = await _payoutRepository.GetByIdAsync(payoutId);
        if (payout is null)
        {
            return Error.NotFound("PartnerPayout.NotFound", "Payout was not found.");
        }

        var partner = await _partnerRepository.GetByIdAsync(payout.PartnerId);
        return ToResponse(payout, partner?.DisplayName ?? "(unknown partner)");
    }

    public async Task<Result<PartnerPayoutSearchResponse>> SearchAsync(Guid? partnerId, PartnerPayoutStatus? status, int page, int pageSize)
    {
        var (rows, totalCount) = await _payoutRepository.SearchAsync(partnerId, status, page, pageSize);

        var partnerCache = new Dictionary<Guid, string>();
        var items = new List<PartnerPayoutResponse>();
        foreach (var payout in rows)
        {
            if (!partnerCache.TryGetValue(payout.PartnerId, out var displayName))
            {
                var partner = await _partnerRepository.GetByIdAsync(payout.PartnerId);
                displayName = partner?.DisplayName ?? "(unknown partner)";
                partnerCache[payout.PartnerId] = displayName;
            }

            items.Add(ToResponse(payout, displayName));
        }

        return new PartnerPayoutSearchResponse(items, totalCount, page, pageSize);
    }

    public async Task<Result<PartnerPayoutResponse>> UpdateStatusAsync(Guid payoutId, UpdatePartnerPayoutStatusRequest request)
    {
        var payout = await _payoutRepository.GetByIdAsync(payoutId);
        if (payout is null)
        {
            return Error.NotFound("PartnerPayout.NotFound", "Payout was not found.");
        }

        try
        {
            switch (request.Status)
            {
                case PartnerPayoutStatus.Processing:
                    payout.MarkProcessing();
                    break;
                case PartnerPayoutStatus.Paid:
                    if (string.IsNullOrWhiteSpace(request.PayoutReference))
                    {
                        return Error.Validation("PartnerPayout.ReferenceRequired", "A payout reference is required to mark a payout paid.");
                    }

                    payout.MarkPaid(request.PayoutReference);
                    break;
                case PartnerPayoutStatus.Failed:
                    payout.MarkFailed(request.Notes);
                    break;
                default:
                    return Error.Validation("PartnerPayout.InvalidTargetStatus", "A payout can only be moved to Processing, Paid or Failed.");
            }
        }
        catch (InvalidOperationException ex)
        {
            return Error.Business("PartnerPayout.InvalidTransition", ex.Message);
        }

        await _payoutRepository.UpdateAsync(payout);

        var partner = await _partnerRepository.GetByIdAsync(payout.PartnerId);
        return ToResponse(payout, partner?.DisplayName ?? "(unknown partner)");
    }

    private static PartnerPayoutResponse ToResponse(PartnerPayout payout, string partnerDisplayName) => new(
        payout.Id, payout.PartnerId, partnerDisplayName, payout.PeriodStart, payout.PeriodEnd,
        payout.TotalAmount, payout.Status, payout.PayoutReference, payout.Notes, payout.CreatedAt, payout.UpdatedAt);
}
