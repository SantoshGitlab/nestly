using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerManagement;

/// <summary>
/// Admin-side KYC approval (task 150b - the counterpart to task 146c's
/// submission-only <c>IPartnerKycService</c>) and the distinct post-KYC
/// background/reference check step (task 160). A partner cannot become fully
/// <see cref="Nestly.Domain.PartnerStatus.Active"/> until both KYC is
/// approved and the background check passes - <see cref="ActivateAsync"/> is
/// where that gate is enforced, per <c>Partner.ChangeStatus</c>'s own doc
/// comment that transition legality belongs to the calling service, not the
/// entity.
/// </summary>
public interface IPartnerKycApprovalService
{
    Task<Result<PartnerKycDocumentResponse>> ApproveDocumentAsync(Guid documentId, Guid adminUserId);

    Task<Result<PartnerKycDocumentResponse>> RejectDocumentAsync(Guid documentId, Guid adminUserId, RejectPartnerKycDocumentRequest request);

    /// <summary>Records a background/reference check outcome (task 160). Append-only - a re-check adds a new row rather than overwriting the previous one.</summary>
    Task<Result<PartnerBackgroundCheckResponse>> RecordBackgroundCheckAsync(Guid partnerId, Guid adminUserId, RecordBackgroundCheckRequest request);

    /// <summary>
    /// Activates a partner (moves <see cref="Nestly.Domain.PartnerStatus"/>
    /// to Active) once its KYC onboarding status is KycVerified and its most
    /// recent background check passed (task 160's gate). Fails with a
    /// business error otherwise - both conditions are checked explicitly
    /// rather than left to the caller to sequence correctly.
    /// </summary>
    Task<Result<PartnerDetailResponse>> ActivateAsync(Guid partnerId);
}
