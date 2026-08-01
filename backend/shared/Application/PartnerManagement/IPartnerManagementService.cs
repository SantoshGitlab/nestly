using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerManagement;

/// <summary>
/// Admin partner directory management (PARTNER.md API surface "Partner CRUD",
/// task 150a) plus the performance view (task 150c). Mirrors
/// <c>ICustomerManagementService</c>'s shape: one service for the related
/// read/write operations on a partner's own record, rather than one per
/// action. KYC approval and background-check/activation are a separate
/// service (<see cref="IPartnerKycApprovalService"/>) - a distinct workflow
/// with its own approval gate, not simple field-level CRUD.
/// </summary>
public interface IPartnerManagementService
{
    Task<Result<PartnerSearchResponse>> SearchAsync(PartnerSearchRequest request);

    Task<Result<PartnerDetailResponse>> GetDetailAsync(Guid partnerId);

    /// <summary>Admin-created partner record. PartnerType is always Individual (OPEN DECISIONS #2).</summary>
    Task<Result<PartnerDetailResponse>> CreateAsync(CreatePartnerRequest request);

    Task<Result<PartnerDetailResponse>> UpdateAsync(Guid partnerId, UpdatePartnerRequest request);

    /// <summary>Suspends a partner's account - blocks further assignment until reactivated.</summary>
    Task<Result<PartnerDetailResponse>> SuspendAsync(Guid partnerId, SuspendPartnerRequest request);

    /// <summary>Reactivates a previously suspended partner. Does not re-run the KYC/background-check activation gate - that only applies to the first activation (<see cref="IPartnerKycApprovalService.ActivateAsync"/>).</summary>
    Task<Result<PartnerDetailResponse>> ReactivateAsync(Guid partnerId);

    /// <summary>Job-fulfilment performance summary (task 150c).</summary>
    Task<Result<PartnerPerformanceResponse>> GetPerformanceAsync(Guid partnerId);
}
