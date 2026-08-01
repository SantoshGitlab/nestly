using Nestly.Application;
using Nestly.Application.PartnerManagement;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IPartnerEarningLedgerService"/>
public class PartnerEarningLedgerService : IPartnerEarningLedgerService
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly IPartnerEarningLedgerRepository _ledgerRepository;

    public PartnerEarningLedgerService(IPartnerRepository partnerRepository, IPartnerEarningLedgerRepository ledgerRepository)
    {
        _partnerRepository = partnerRepository;
        _ledgerRepository = ledgerRepository;
    }

    public async Task<Result<PartnerEarningLedgerEntryResponse>> RecordAdjustmentAsync(Guid partnerId, RecordPartnerEarningAdjustmentRequest request)
    {
        if (!await _partnerRepository.ExistsAsync(partnerId))
        {
            return Error.NotFound("PartnerEarningLedger.PartnerNotFound", "Partner was not found.");
        }

        var latest = await _ledgerRepository.GetLatestAsync(partnerId);
        decimal currentBalance = latest?.BalanceAfter ?? 0m;

        decimal newBalance = request.EntryType == PartnerEarningEntryType.Credit
            ? currentBalance + request.Amount
            : currentBalance - request.Amount;

        if (newBalance < 0)
        {
            return Error.Business("PartnerEarningLedger.InsufficientBalance", "This debit would take the partner's earnings balance negative.");
        }

        var entry = new PartnerEarningLedgerEntry(
            Guid.NewGuid(), partnerId, request.EntryType, request.Amount, newBalance,
            request.SourceType, request.SourceReferenceId, request.Description);
        await _ledgerRepository.AddAsync(entry);

        return ToResponse(entry);
    }

    public async Task<Result<PartnerEarningsSummaryResponse>> GetSummaryAsync(Guid partnerId)
    {
        if (!await _partnerRepository.ExistsAsync(partnerId))
        {
            return Error.NotFound("PartnerEarningLedger.PartnerNotFound", "Partner was not found.");
        }

        var entries = await _ledgerRepository.ListByPartnerAsync(partnerId);
        decimal balance = entries.Count > 0 ? entries[0].BalanceAfter : 0m;

        return new PartnerEarningsSummaryResponse(partnerId, balance, entries.Select(ToResponse).ToList());
    }

    private static PartnerEarningLedgerEntryResponse ToResponse(PartnerEarningLedgerEntry entry) => new(
        entry.Id, entry.PartnerId, entry.EntryType, entry.Amount, entry.BalanceAfter,
        entry.SourceType, entry.SourceReferenceId, entry.Description, entry.CreatedAtUtc);
}
