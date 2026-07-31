using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.PartnerIdentity;

/// <summary>
/// KYC document submission and status lookup (task 146c - the submission
/// side only; approval/rejection is task 150b's admin workflow, which calls
/// <c>PartnerKycDocument.Approve</c>/<c>Reject</c> directly rather than
/// through this interface).
/// </summary>
public interface IPartnerKycService
{
    Task<Result<PartnerKycDocumentResponse>> SubmitDocumentAsync(SubmitPartnerKycDocumentRequest request);

    Task<Result<PartnerKycStatusResponse>> GetStatusAsync(Guid partnerId);
}
