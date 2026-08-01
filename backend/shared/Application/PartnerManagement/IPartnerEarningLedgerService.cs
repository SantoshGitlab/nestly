using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerManagement;

/// <summary>
/// A partner's append-only earning ledger (PARTNER.md Financial Domain,
/// task 148). Credits/debits are recorded here as an admin-triggered manual
/// adjustment today (e.g. a penalty, or a correction) - automatic
/// crediting on job completion is wired in once the partner-facing "complete
/// job" flow (tasks 149c/151) exists, by calling this same interface.
/// </summary>
public interface IPartnerEarningLedgerService
{
    Task<Result<PartnerEarningLedgerEntryResponse>> RecordAdjustmentAsync(Guid partnerId, RecordPartnerEarningAdjustmentRequest request);

    Task<Result<PartnerEarningsSummaryResponse>> GetSummaryAsync(Guid partnerId);
}
