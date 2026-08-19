using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Amc;

/// <summary>Admin-facing AMC operations (docs/AMC.md): plan catalog CRUD, contract visibility, and the renewal-pipeline report.</summary>
public interface IAmcAdminService
{
    Task<IReadOnlyList<AmcPlanAdminResponse>> ListAllPlansAsync();

    Task<Result<AmcPlanAdminResponse>> GetPlanByIdAsync(Guid id);

    Task<Result<AmcPlanAdminResponse>> CreatePlanAsync(AmcPlanCreateRequest request);

    Task<Result<AmcPlanAdminResponse>> UpdatePlanAsync(Guid id, AmcPlanUpdateRequest request, Guid adminUserId);

    Task<Result> ActivatePlanAsync(Guid id, Guid adminUserId);

    Task<Result> DeactivatePlanAsync(Guid id, Guid adminUserId);

    Task<Result<AmcContractAdminSearchResponse>> SearchContractsAsync(
        CustomerAmcContractStatus? status, string? customerSearch, int page, int pageSize);

    Task<Result<AmcContractAdminListItemResponse>> GetContractByIdAsync(Guid id);

    /// <summary>
    /// The renewal pipeline report (docs/AMC.md - AMC's whole value case rests
    /// on contracts actually getting renewed, not just purchased once).
    /// <paramref name="fromUtc"/>/<paramref name="toUtc"/> default to
    /// "now" through "now + 30 days" when omitted, mirroring the recurring-plan
    /// report's own default-horizon convention.
    /// </summary>
    Task<Result<AmcRenewalReportResponse>> GetRenewalReportAsync(DateTime? fromUtc, DateTime? toUtc);
}
